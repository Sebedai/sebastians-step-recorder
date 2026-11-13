﻿using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Aufbau des Editors: Kopfzeile, Schrittliste mit Vorschaubildern (Ziehen zum Umsortieren, Rechtsklick-Menü),
/// Bildbereich mit schwebender Werkzeugpalette (Farben, Auswahl, Verschieben, Löschen) und Detailbereich
/// mit den Reitern „Schritt“ (direkt bearbeitbar) und „Technik“.
/// </summary>
sealed partial class ViewerWindow
{
    // Zwölf Farben (13, Paket 22); die ersten sechs sind die bisherigen
    static readonly (string hex, string name)[] Palette =
    {
        ("#E5484D", Loc.L("Rot", "Red")), ("#F5A524", Loc.L("Orange", "Orange")), ("#1F9D55", Loc.L("Grün", "Green")),
        ("#4361EE", Loc.L("Blau", "Blue")), ("#1B1C21", Loc.L("Schwarz", "Black")), ("#FFFFFF", Loc.L("Weiß", "White")),
        ("#FFEB3B", Loc.L("Gelb", "Yellow")), ("#12A594", Loc.L("Türkis", "Teal")), ("#8E4EC6", Loc.L("Violett", "Violet")),
        ("#D6409F", Loc.L("Pink", "Pink")), ("#8D6E63", Loc.L("Braun", "Brown")), ("#6B7280", Loc.L("Grau", "Gray")),
    };
    static readonly string[] ImportanceOrder = { "primary", "secondary", "trace" };
    static readonly (int width, int fontSize, string name)[] StrokeSizes = { (2, 14, Loc.L("dünn", "thin")), (4, 18, Loc.L("mittel", "medium")), (7, 26, Loc.L("dick", "thick")) };
    readonly List<RadioButton> _strokeBtns = new();
    int _stroke = 4, _fontSize = 18;
    ZoomPanel _zoom = null!;
    Button _cropOff = null!;
    int _handle = -1;               // gezogener Anfasser der Auswahl (-1 = keiner)
    readonly List<Shape> _handleShapes = new();

    readonly Dictionary<string, RadioButton> _toolBtns = new();
    readonly List<RadioButton> _colorBtns = new();
    RadioButton[] _tabBtns = null!;
    string _color = "#E5484D";
    bool _syncingColor;
    string? _selAnn;
    Point? _moveFrom;
    Rect _selBase;
    Rectangle? _selRect;
    Button _undoBtn = null!, _redoBtn = null!;
    readonly TextBlock _stepPos = new() { FontSize = Theme.FsHint, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), MinWidth = 54, TextAlignment = TextAlignment.Center };
    int _detailTab;
    bool _closed;

    // Vorschaubilder der Schrittliste (werden im Leerlauf nacheinander erzeugt)
    readonly Dictionary<string, BitmapSource?> _thumbs = new();
    readonly Dictionary<string, Image> _thumbTargets = new();
    readonly Queue<ViewStep> _thumbQueue = new();
    bool _thumbBusy;

    // Umsortieren per Ziehen
    Point? _listDragFrom;
    ListBoxItem? _listDragItem;

    // Direktbearbeitung von Überschrift und Text
    long? _editAnchor;
    TextBox? _editTitle;
    RichEdit? _editBody;
    string _editOrigTitle = "";

    void BuildLayout()
    {
        // ---------------- Kopfzeile: Menüleiste links, häufige Befehle rechts ----------------
        var menuBar = BuildMenuBar();
        var cmds = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _undoBtn = CmdButton("undo", null); _redoBtn = CmdButton("redo", null);
        _undoBtn.Margin = _redoBtn.Margin = new Thickness(1, 0, 1, 0);
        _playIcon.Text = Theme.IPlay; _playIcon.FontFamily = Theme.Icons; _playIcon.FontSize = 16; _playIcon.VerticalAlignment = VerticalAlignment.Center;
        var playText = new TextBlock { Text = Loc.L("Abspielen", "Play"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var play = U.Make("Ghost").Set(b => { b.Content = U.Row(_playIcon, playText); b.ToolTip = TipOf("play"); b.Margin = new Thickness(2, 0, 2, 0); });
        play.Click += (_, _) => Run("play");
        _labels.Add((playText, 1));
        var preview = CmdButton("pageView", Loc.L("Seitenansicht", "Page view"), 1);
        var review = U.Make("Ghost").Set(b => { b.ToolTip = Loc.L("Auffällige Eingaben durchsehen, bevor die Aufnahme weitergegeben wird (Strg+Umschalt+R)", "Review conspicuous input before the recording is shared (Ctrl+Shift+R)"); b.Margin = new Thickness(2, 0, 2, 0); });
        var reviewContent = (StackPanel)U.IconText(Theme.IChecklist, Loc.L("Durchsicht", "Review"));
        _reviewBadge = new Border { Background = Theme.WarnSoft, CornerRadius = new CornerRadius(999), Padding = new Thickness(7, 1, 7, 2), MinWidth = 22, Margin = new Thickness(8, 0, 0, 0), Child = _reviewCount, VerticalAlignment = VerticalAlignment.Center };
        _reviewCount.HorizontalAlignment = HorizontalAlignment.Center;
        reviewContent.Children.Add(_reviewBadge);
        review.Content = reviewContent;
        review.Click += (_, _) => Run("review");
        if (reviewContent.Children[1] is TextBlock rt) _labels.Add((rt, 2));
        var copy = MenuCmdButton(Theme.ICopy, Loc.L("Kopieren", "Copy"), Loc.L("Bild oder Text dieses Schritts in die Zwischenablage", "Copy the image or text of this step to the clipboard"), 2,
            () => new() { Item("copyImage", enabled: _current != null), Item("copyImageText", enabled: Sel != null), Item("copyText", enabled: Sel != null), Item("copyMarkdown", enabled: Sel != null), Item("copyOriginal", enabled: Sel != null), Sep(), Item("copyGuide"), Item("copyAllMarkdown"), Item("copyBugReport") });
        var export = CmdButton("export", Loc.L("Exportieren …", "Export …"), 0, "Primary");
        export.Margin = new Thickness(10, 0, 0, 0);
        foreach (var e in new UIElement[] { _undoBtn, _redoBtn, U.VSep(), play, preview, review, U.VSep(), copy, export }) cmds.Children.Add(e);
        // Menüleiste links, Befehlsknöpfe rechts – Tab-Taste in derselben Reihenfolge
        var bar = U.LeftRight(menuBar, cmds);
        var top = new Border { Padding = new Thickness(8, 0, 16, 8), Child = bar };

        // ---------------- Schrittliste ----------------
        var searchHost = new Grid { Margin = new Thickness(16, 16, 16, 12) };
        searchHost.Children.Add(_search);
        _search.TextChanged += (_, _) => Fill();
        var (impTrack, imp) = U.Segmented("imp", 0, (null, Loc.L("Schritte", "Steps")), (null, Loc.L("Mit Spur", "With trace")));
        impTrack.ToolTip = Loc.L("Schritte: Haupt- und Nebenschritte der Anleitung. Mit Spur: zusätzlich Mausbewegungen, Fensterwechsel und andere Spuren.", "Steps: main and secondary steps of the guide. With trace: also mouse movements, window switches and other traces.");
        _allToggle = imp[1]; _filterBtns = imp;
        foreach (var rb in imp) rb.Checked += (_, _) => { if (IsLoaded) Fill(); };
        // Sprache der Texte (DE/EN) steht bei der Liste – sie betrifft Überschriften und Texte aller Schritte
        var (langTrack, langs) = U.Segmented("tlang", _view.Language == "en" ? 1 : 0, (null, "DE"), (null, "EN"));
        _langBtns = langs;
        langTrack.ToolTip = Loc.L("Sprache der Texte: Überschriften, Anleitungstexte, Kapitel und Dokumenttexte gibt es je Sprache – so entstehen aus einer Aufnahme eine deutsche und eine englische Anleitung.", "Language of the texts: headings, guide texts, chapters and document texts exist per language – so one recording yields a German and an English guide.");
        langs[0].Checked += (_, _) => { if (IsLoaded) SetTextLanguage("de"); }; langs[1].Checked += (_, _) => { if (IsLoaded) SetTextLanguage("en"); };
        var filterRow = new DockPanel { Margin = new Thickness(16, 0, 16, 8) };
        _slowToggle = new System.Windows.Controls.Primitives.ToggleButton
        {
            Content = U.Row(U.Icon(Theme.IStopwatch, 13, Theme.Fg2), U.Text(Loc.L("Langsam", "Slow"), Theme.FsCaption, margin: new Thickness(5, 0, 0, 0))),
            Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Loc.F("Nur Schritte, nach denen die Anwendung mindestens {0} brauchte, bis sie bereit war", "Only steps after which the application needed at least {0} to be ready", StepRecorder.ReadLayer.Timing.Ms(StepRecorder.ReadLayer.Timing.SlowMs, Loc.Lang == "de"))
        };
        System.Windows.Automation.AutomationProperties.SetName(_slowToggle, Loc.L("Nur langsame Schritte", "Slow steps only"));
        _slowToggle.Checked += (_, _) => { if (IsLoaded) Fill(); }; _slowToggle.Unchecked += (_, _) => { if (IsLoaded) Fill(); };
        DockPanel.SetDock(langTrack, Dock.Right); filterRow.Children.Add(langTrack);
        DockPanel.SetDock(_slowToggle, Dock.Right); filterRow.Children.Add(_slowToggle);
        // Filter „nur Lesezeichen“: Stern rechts neben der Suche (in der Filterzeile ist kein Platz mehr)
        _bookmarkToggle = BuildBookmarkFilter();
        searchHost.ColumnDefinitions.Add(new ColumnDefinition()); searchHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_bookmarkToggle, 1); searchHost.Children.Add(_bookmarkToggle);
        filterRow.Children.Add(impTrack);
        _list.SelectionMode = SelectionMode.Extended;
        _list.ItemContainerStyle = Theme.Style("StepItem");
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItems.Count > 0) LeaveCover(); _selAnn = null; _variantOverride = null; ShowStep(); };
        _list.MouseDoubleClick += (_, e) =>
        {
            if (ItemAt(e.OriginalSource) == null) return;
            if (e.OriginalSource is Image) OpenLightbox(); else FocusInlineTitle();
        };
        _list.PreviewMouseRightButtonDown += (_, e) => { if (ItemAt(e.OriginalSource) is { } it && !it.IsSelected) { _list.SelectedItems.Clear(); it.IsSelected = true; } };
        _list.MouseRightButtonUp += (_, e) => { if (ItemAt(e.OriginalSource) != null) { StepMenu(); e.Handled = true; } };
        _list.PreviewMouseLeftButtonDown += (_, e) => { _listDragItem = ItemAt(e.OriginalSource); _listDragFrom = e.GetPosition(_list); };
        _list.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _listDragFrom == null || _listDragItem == null) return;
            var d = e.GetPosition(_list) - _listDragFrom.Value;
            if (Math.Abs(d.X) < 6 && Math.Abs(d.Y) < 6) return;
            var item = _listDragItem; _listDragFrom = null; _listDragItem = null;
            if (item.Tag is ViewStep vs && !_view.Session.ReadOnly) DragDrop.DoDragDrop(item, new DataObject(typeof(ViewStep), vs), DragDropEffects.Move);
        };
        _list.AllowDrop = true;
        _list.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(typeof(ViewStep)) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
        _list.Drop += (_, e) =>
        {
            if (e.Data.GetData(typeof(ViewStep)) is ViewStep src && ItemAt(e.OriginalSource)?.Tag is ViewStep dst && src.Anchor != dst.Anchor) MoveTo(src, dst);
            e.Handled = true;
        };
        var hintText = U.Text(Loc.L("Ziehen: umsortieren · Strg+Klick: mehrere", "Drag: reorder · Ctrl+click: select several"), Theme.FsCaption, fg: Theme.Muted); hintText.VerticalAlignment = VerticalAlignment.Center;
        var insertBtn = U.Btn(Theme.IAdd, Loc.L("Einfügen", "Insert"), () => { }, Loc.L("Text- oder Bildschritt nach dem ausgewählten Schritt einfügen (Umschalt+Einfg)", "Insert a text or image step after the selected step (Shift+Ins)"), "Ghost");
        insertBtn.Click += (_, _) => InsertMenu(insertBtn); insertBtn.Margin = new Thickness(6, 0, 0, 0);
        var listHint = new DockPanel { Margin = new Thickness(18, 6, 10, 10) };
        hintText.TextWrapping = TextWrapping.Wrap;
        DockPanel.SetDock(insertBtn, Dock.Right); listHint.Children.Add(insertBtn); listHint.Children.Add(U.Col(_listCount, hintText));
        var coverEntry = BuildCoverEntry();
        var left = new DockPanel { Background = Theme.Surface };
        DockPanel.SetDock(searchHost, Dock.Top); DockPanel.SetDock(filterRow, Dock.Top); DockPanel.SetDock(coverEntry, Dock.Top); DockPanel.SetDock(listHint, Dock.Bottom);
        left.Children.Add(searchHost); left.Children.Add(filterRow); left.Children.Add(coverEntry); left.Children.Add(listHint); left.Children.Add(_list);

        // ---------------- Ansicht (über dem Bild) ----------------
        var (baTrack, ba) = U.Segmented("ba", 0, (null, Loc.L("Vorher", "Before")), (null, Loc.L("Nachher", "After")));
        _beforeBtn = ba[0]; _afterBtn = ba[1];
        foreach (var rb in ba) rb.Checked += (_, _) => { if (_syncBA) return; _variantOverride = _afterBtn.IsChecked == true ? "after" : "before"; if (IsLoaded) ShowStep(); };
        baTrack.ToolTip = Loc.L("Bild vor oder nach der Aktion (W)", "Image before or after the action (W)");
        var disp = _dispBox = new ComboBox { Items = { Loc.L("Ganzer Bildschirm", "Full screen"), Loc.L("Anwendungsfenster", "Application window"), Loc.L("Bereich um die Aktion", "Area around the action") }, SelectedIndex = 0, MinWidth = 160, ToolTip = Loc.L("Bildausschnitt", "Image area") };
        disp.SelectionChanged += (_, _) => { _display = disp.SelectedIndex switch { 1 => "window", 2 => "crop", _ => "screen" }; ShowStep(); };
        var marksBtn = MenuCmdButton(Theme.IMouse, Loc.L("Mausmarken", "Mouse marks"), Loc.L("Klickpunkt, Zugweg, Zielrahmen, Haltedauer sowie gedrückte Tasten und getippten Text im Bild ein-/ausblenden (H: nur dieser Schritt)", "Show/hide click point, drag path, target frame, hold time as well as pressed keys and typed text in the image (H: this step only)"), 3, MarksItems);
        var prev = CmdButton("prev", null); var next = CmdButton("next", null);
        prev.Margin = next.Margin = new Thickness(0);
        _stepPos.Foreground = Theme.Muted;
        var nav = U.Row(prev, _stepPos, next);
        var viewLeft = new StackPanel { Orientation = Orientation.Horizontal };
        _imageOnly.AddRange(new FrameworkElement[] { baTrack, disp, marksBtn });
        foreach (var c in new FrameworkElement[] { BuildCenterModeToggle(), baTrack, disp, marksBtn }) { c.VerticalAlignment = VerticalAlignment.Center; c.Margin = new Thickness(viewLeft.Children.Count == 0 ? 0 : 10, 0, 0, 0); viewLeft.Children.Add(c); }
        var viewRow = new DockPanel { Margin = new Thickness(16, 10, 16, 10) };
        DockPanel.SetDock(nav, Dock.Right); viewRow.Children.Add(nav); viewRow.Children.Add(viewLeft);

        // ---------------- Werkzeugpalette (links am Bild, dreispaltig) ----------------
        var pal = new StackPanel();
        var toolGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        pal.Children.Add(toolGrid);
        void Tool(string id, UIElement content, string tip)
        {
            var b = new RadioButton { Content = content, GroupName = "tool", IsChecked = id == "select", ToolTip = tip, Style = Theme.Style("ToolToggle"), Margin = new Thickness(1), MinHeight = 36 };
            b.Checked += (_, _) =>
            {
                bool cropChanged = (_tool == "crop") != (id == "crop");
                _tool = id;
                if (_pipette) SetPipette(false);
                ToolColorFor(id);
                _overlay.Cursor = id == "select" ? Cursors.Arrow : Cursors.Cross;
                if (id != "select" && _selAnn != null) { _selAnn = null; DrawSelection(); }
                if (cropChanged && IsLoaded) ShowStep();
                if (id == "crop") _status.Text = Loc.L("Zuschneiden: Rechteck aufziehen – gilt für Vorher- und Nachher-Bild dieses Schritts, auch im Export.", "Crop: drag a rectangle – applies to the before and after image of this step, also in the export.");
            };
            _toolBtns[id] = b; toolGrid.Children.Add(b);
        }
        TextBlock G(string glyph) => U.Icon(glyph, 16);
        FrameworkElement Bound(FrameworkElement e, DependencyProperty p) { e.SetBinding(p, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(RadioButton), 1) }); return e; }
        var badgeIcon = Bound(new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1.5), Child = new TextBlock { Text = "1", FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }, Border.BorderBrushProperty);
        var ellipseIcon = Bound(new Ellipse { Width = 18, Height = 13, StrokeThickness = 1.6 }, Shape.StrokeProperty);
        Tool("select", G(Theme.IPointer), Loc.L("Auswählen (V) – Zeichnung anklicken, ziehen zum Verschieben, Entf löscht, Doppelklick ändert Text", "Select (V) – click a markup, drag to move, Del deletes, double-click edits text"));
        Tool("arrow", G(Theme.IArrow), Loc.L("Pfeil (A)", "Arrow (A)"));
        var lineIcon = Bound(new Line { X1 = 1, Y1 = 15, X2 = 17, Y2 = 3, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 18, Height = 18 }, Shape.StrokeProperty);
        Tool("line", lineIcon, Loc.L("Linie (Umschalt+A)", "Line (Shift+A)"));
        Tool("freehand", G(Theme.IInk), Loc.L("Freihand (F) – frei zeichnen; die Linie wird beim Loslassen geglättet", "Freehand (F) – draw freely; the line is smoothed on release"));
        Tool("rect", G(Theme.IRect), Loc.L("Rahmen (R)", "Rectangle (R)"));
        Tool("ellipse", ellipseIcon, Loc.L("Ellipse (E)", "Ellipse (E)"));
        Tool("highlight", G(Theme.IHighlight), Loc.L("Hervorheben (M)", "Highlight (M)"));
        Tool("spotlight", G(Theme.ISpot), Loc.L("Spotlight (U) – Bereich bleibt hell, der Rest des Bildes wird abgedunkelt; die Strichstärke bestimmt, wie stark", "Spotlight (U) – the area stays bright, the rest of the image is dimmed; stroke width sets how much"));
        Tool("magnifier", G(Theme.IMagnifier), Loc.L("Lupe (L) – Bereich aufziehen; die vergrößerte Linse lässt sich getrennt verschieben, der Griff an der Linse ändert den Zoom", "Magnifier (L) – drag an area; the enlarged lens can be moved separately, the handle on the lens changes the zoom"));
        Tool("text", G(Theme.IText), Loc.L("Text (T)", "Text (T)"));
        Tool("badge", badgeIcon, Loc.L("Nummer (N)", "Number (N)"));
        Tool("redact", G(Theme.IRedact), Loc.L("Schwärzen (X) – gilt ab diesem Bild für alle folgenden, auch im Export und KI-Paket", "Redact (X) – applies from this image onwards, also in the export and AI package"));
        Tool("pixelate", G(Theme.IPixelate), Loc.L("Verpixeln (P) – wie Schwärzen, aber unauffälliger; gilt ab diesem Bild für alle folgenden", "Pixelate (P) – like redact, but less conspicuous; applies from this image onwards"));
        Tool("crop", G(Theme.ICrop), Loc.L("Zuschneiden (C) – Rechteck aufziehen; gilt für die Bilder dieses Schritts, auch im Export", "Crop (C) – drag a rectangle; applies to the images of this step, also in the export"));
        Border PalSep() => new() { Height = 1, Background = Theme.Line, Margin = new Thickness(6, 6, 6, 6) };
        pal.Children.Add(PalSep());
        BuildColorGrid(pal);
        pal.Children.Add(PalSep());
        var widthGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var (w, fs, name) in StrokeSizes)
        {
            var line = new Rectangle { Width = 16, Height = Math.Max(1.5, w / 1.6), RadiusX = 1, RadiusY = 1 };
            line.SetBinding(Shape.FillProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(RadioButton), 1) });
            var rb = new RadioButton { Content = line, GroupName = "stroke", IsChecked = w == _stroke, ToolTip = Loc.F("Strichstärke und Textgröße: {0}", "Stroke width and text size: {0}", name), Style = Theme.Style("ToolToggle"), Margin = new Thickness(1), MinHeight = 28, MinWidth = 0, Padding = new Thickness(3), Tag = w };
            rb.Checked += (_, _) => { _stroke = w; _fontSize = fs; if (!_syncingColor) RestrokeSelected(w, fs); };
            _strokeBtns.Add(rb); widthGrid.Children.Add(rb);
        }
        pal.Children.Add(widthGrid);
        var styleBtn = U.Ghost(Theme.IStroke, Loc.L("Stil", "Style"), () => { }, Loc.L("Linienart, Spitzen, Füllung, Hintergrund, Form und Zählweise von Nummern, Deckkraft – für die ausgewählte Zeichnung oder neue Zeichnungen; Stil auf alle übertragen", "Line style, heads, fill, background, shape and counting of numbers, opacity – for the selected drawing or new drawings; apply style to all"));
        styleBtn.Click += (_, _) => ShowStyleMenu(styleBtn);
        styleBtn.Margin = new Thickness(0, 4, 0, 0); styleBtn.MinWidth = 36; styleBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        pal.Children.Add(styleBtn);
        pal.Children.Add(PalSep());
        var del = U.Ghost(Theme.IDelete, null, DeleteSelectedOrAll, Loc.L("Ausgewählte Zeichnung löschen (Entf) – ohne Auswahl: alle Zeichnungen dieses Bildes", "Delete the selected markup (Del) – without a selection: all markup on this image"));
        del.Margin = new Thickness(0); del.MinWidth = 36; del.HorizontalAlignment = HorizontalAlignment.Stretch;
        pal.Children.Add(del);
        var palette = new Border
        {
            Background = Theme.Raised, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RInset), Padding = new Thickness(5), Child = pal, Width = PaletteWidth,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(PaletteGap, 8, 0, 8), Effect = Theme.Shadow(18, 3)
        };

        // ---------------- Bildbereich mit Zoom ----------------
        _imgHost.Children.Add(_img);
        _imgHost.Children.Add(_overlay);
        RenderOptions.SetBitmapScalingMode(_img, BitmapScalingMode.HighQuality);
        _overlay.MouseLeftButtonDown += OverlayDown;
        _overlay.MouseMove += OverlayMove;
        _overlay.MouseLeftButtonUp += OverlayUp;
        var stage = new Grid();
        stage.Children.Add(new Border { Background = Theme.Surface, Effect = Theme.Shadow(36, 6, Theme.Dark ? 0.6 : 0.2) });
        stage.Children.Add(_imgHost);
        // Rand um das Bild: links Platz für die Werkzeugpalette, oben für die Bildunterschrift, unten für die Zoomleiste
        _zoom = new ZoomPanel(stage, () => new Size(_imgHost.Width is double w && !double.IsNaN(w) ? w : 0, _imgHost.Height is double h && !double.IsNaN(h) ? h : 0), new Thickness(PaletteGap + PaletteWidth + 16, 52, 28, 64));
        _zoom.ZoomChanged += () => { if (_selAnn != null) DrawSelection(); };
        var imgArea = new Grid { Background = Theme.CanvasBg, ClipToBounds = true };
        imgArea.Children.Add(_zoom);
        imgArea.Children.Add(_noImage);
        imgArea.Children.Add(_imgCaptionPill);
        imgArea.Children.Add(palette);
        // Zoomleiste und Großansicht (unten rechts)
        _cropOff = U.Btn(Theme.ICrop, Loc.L("Zuschnitt entfernen", "Remove crop"), () => { if (Sel is { } cs) RemoveCrop(cs); }, Loc.L("Ganzes Bild wieder zeigen", "Show the whole image again"), "Ghost");
        _cropOff.Margin = new Thickness(0, 0, 4, 0); _cropOff.Visibility = Visibility.Collapsed;
        var full = U.Ghost(Theme.IFullScreen, null, OpenLightbox, Loc.L("Großansicht (F11 oder Doppelklick aufs Bild)", "Enlarged view (F11 or double-click the image)")); full.Margin = new Thickness(0);
        var zoomBar = new Border
        {
            Background = Theme.Raised, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RInset), Padding = new Thickness(4, 3, 4, 3), Effect = Theme.Shadow(18, 3),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 16, 12),
            Child = U.Row(_cropOff, _zoom.Toolbar(), U.VSep(20), full)
        };
        imgArea.Children.Add(zoomBar);
        imgArea.Children.Add(_coverHost);
        EnsurePageView(imgArea);
        var center = new DockPanel { Background = Theme.Bg };
        var viewBorder = new Border { Background = Theme.Bg, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = viewRow };
        _viewBorder = viewBorder; _paletteEl = palette; _zoomBarEl = zoomBar;
        DockPanel.SetDock(viewBorder, Dock.Top);
        center.Children.Add(viewBorder); center.Children.Add(imgArea);

        // ---------------- Details ----------------
        var (tabTrack, tabs) = U.Segmented("dtab", 0, (Theme.IEdit, Loc.L("Schritt", "Step")), (Theme.ICode, Loc.L("Technik", "Technical")));
        _tabBtns = tabs; _detailTabs = tabTrack;
        for (int i = 0; i < tabs.Length; i++) { int ti = i; tabs[i].Checked += (_, _) => { _detailTab = ti; if (Sel is { } s) ShowDetails(s); }; }
        tabs[0].ToolTip = Loc.L("Text, Hinweise, Einordnung und Bild dieses Schritts", "Text, notes, placement and image of this step");
        tabTrack.Margin = new Thickness(24, 16, 24, 4);
        tabs[1].ToolTip = Loc.L("Fenster, Ziel, Belege, AutoHotkey-Angaben und das Protokoll für KI", "Window, target, evidence, AutoHotkey details and the log for AI");
        var detPanel = new DockPanel { Background = Theme.Surface };
        DockPanel.SetDock(tabTrack, Dock.Top); detPanel.Children.Add(tabTrack);
        detPanel.Children.Add(new ScrollViewer { Content = _details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Surface });

        var grid = new Grid();
        _leftCol = new ColumnDefinition { Width = new GridLength(LeftWide), MinWidth = 250 };
        _rightCol = new ColumnDefinition { Width = new GridLength(RightWide), MinWidth = 280 };
        grid.ColumnDefinitions.Add(_leftCol);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
        grid.ColumnDefinitions.Add(_rightCol);
        Grid.SetColumn(left, 0); grid.Children.Add(left);
        Grid.SetColumn(center, 2); grid.Children.Add(center);
        Grid.SetColumn(detPanel, 4); grid.Children.Add(detPanel);
        foreach (int col in new[] { 1, 3 })
        {
            var line = new Border { Background = Theme.Line }; Grid.SetColumn(line, col); grid.Children.Add(line);
            var split = new GridSplitter { Width = 7, Margin = new Thickness(-3, 0, -3, 0), HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Cursor = Cursors.SizeWE };
            split.DragCompleted += (_, _) => { _prefLeft = _leftCol.ActualWidth; _prefRight = _rightCol.ActualWidth; };
            Grid.SetColumn(split, col); Panel.SetZIndex(split, 10); grid.Children.Add(split);
        }

        // ---------------- Statuszeile ----------------
        var statusDock = new DockPanel();
        var qlink = BuildQualityLink(); DockPanel.SetDock(qlink, Dock.Right); statusDock.Children.Add(qlink);
        _metaBlock.Margin = new Thickness(16, 0, 12, 0); _metaBlock.VerticalAlignment = VerticalAlignment.Center; _metaBlock.FontSize = Theme.FsHint;
        DockPanel.SetDock(_metaBlock, Dock.Right); statusDock.Children.Add(_metaBlock);
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        statusDock.Children.Add(_status);
        var statusBar = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 5, 12, 6), Child = statusDock };

        var root = new DockPanel();
        // Titelleiste wie in Windows üblich: „Aufnahmetitel – Programmname“
        _titleBar = new Wpf.Ui.Controls.TitleBar { Title = Title, Icon = new Wpf.Ui.Controls.ImageIcon { Source = Theme.LogoImage(32), Width = 16, Height = 16 } };
        DockPanel.SetDock(_titleBar, Dock.Top); root.Children.Add(_titleBar);
        Anim.Appear(grid, 0, 4, 200);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        if (_view.Session.ReadOnly)
        {
            // .steprec: nur lesen – Entpacken macht daraus eine bearbeitbare Aufnahme im Aufnahmeordner
            var unpack = U.Btn(Theme.IOpen, Loc.L("Entpacken …", "Unpack …"), async () => { if (await SessionImport.Unpack(this, _path) != null) Close(); }, null, "Ghost");
            var bannerText = U.Text(Loc.L("Schreibgeschützt (.steprec) – zum Bearbeiten bitte entpacken.", "Read-only (.steprec) – unpack to edit."), Theme.FsHint, fg: Theme.Warn);
            bannerText.VerticalAlignment = VerticalAlignment.Center; bannerText.Margin = new Thickness(8, 0, 0, 0);
            var bannerDock = new DockPanel();
            DockPanel.SetDock(unpack, Dock.Right); bannerDock.Children.Add(unpack);
            bannerDock.Children.Add(U.Row(U.Icon(Theme.ILock, 13, Theme.Warn), bannerText));
            var banner = new Border { Background = Theme.WarnSoft, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(16, 4, 12, 4), Child = bannerDock };
            DockPanel.SetDock(banner, Dock.Top); root.Children.Add(banner);
        }
        DockPanel.SetDock(statusBar, Dock.Bottom); root.Children.Add(statusBar);
        root.Children.Add(grid);
        Content = root;
        Closing += (_, _) => { InlineCommit(now: true); SaveUiState(); };
        Closed += (_, _) => _closed = true;
        UpdateUndo();
    }

    /// <summary>Breite der Werkzeugpalette (drei Spalten à 36 px plus Rand) und ihr Abstand zum Rand (bestimmen den linken Rand um das Bild).</summary>
    const double PaletteWidth = 132, PaletteGap = 12;
    /// <summary>Spaltenbreiten der Schrittliste und der Details – breit und bei schmalen Fenstern.</summary>
    const double LeftWide = 340, LeftNarrow = 270, RightWide = 380, RightNarrow = 300;

    void UpdateUndo()
    {
        if (_undoBtn == null) return;
        _undoBtn.IsEnabled = _undo.Count > 0; _redoBtn.IsEnabled = _redo.Count > 0;   // gedimmt über den Stil
    }

    void SelectTool(string id) { if (_toolBtns.TryGetValue(id, out var b)) b.IsChecked = true; }

    static ContextMenu Menu(IEnumerable<(string label, Action action)> items)
    {
        var m = U.MenuScope(new ContextMenu());
        foreach (var (label, action) in items)
        {
            if (label == "-") { m.Items.Add(new Separator()); continue; }
            var (header, gesture) = SplitLabel(label);
            m.Items.Add(U.Mi(header, gesture.Length > 0 ? gesture : null, action));
        }
        return m;
    }

    void ShowShortcuts() => ShortcutsWindow.Show(this, Cmds);

    // =====================================================================================
    // Schrittliste
    // =====================================================================================

    /// <summary>„00:00:02,975“ → „0:02,9“, „01:12:05,1“ bleibt mit Stunden: kurz genug für die Liste.</summary>
    static string ShortTime(string t)
    {
        var m = System.Text.RegularExpressions.Regex.Match(t, @"^(\d+):(\d\d):(\d\d)(?:[,.](\d))?");
        if (!m.Success) return t;
        int h = int.Parse(m.Groups[1].Value), min = int.Parse(m.Groups[2].Value);
        string frac = m.Groups[4].Success ? "," + m.Groups[4].Value : "";
        return (h > 0 ? $"{h}:{min:00}" : min.ToString()) + ":" + m.Groups[3].Value + frac;
    }

    void Fill(string? keepId = null)
    {
        keepId ??= Sel?.Id;
        _list.Items.Clear();
        _thumbTargets.Clear(); _thumbQueue.Clear();
        var query = SearchQuery.Parse(_search.Text);
        int n = 0;
        var qualityWarn = Findings().Where(f => f.Severity == "warning" && f.Anchor != null).GroupBy(f => f.Anchor!.Value).ToDictionary(g => g.Key, g => g.First());
        // Reihenfolge der Anleitung (umsortierte Schritte stehen dort, wo sie im Export erscheinen); ausgeblendete bleiben sichtbar
        foreach (var s in WithInactive(_view.Effective(guide: false, importance: _allToggle.IsChecked == true ? "all" : "secondary", includeManual: true, ordered: true)))
        {
            if (!StepSearch.Matches(s, query)) continue;
            long? waitMs = s.Redacted || s.Manual ? null : StepRecorder.ReadLayer.Timing.WaitMsOf(s.Json);
            if (_slowToggle.IsChecked == true && (waitMs ?? 0) < StepRecorder.ReadLayer.Timing.SlowMs) continue;
            if (!PassesBookmarkFilter(s)) continue;
            n++;
            bool primary = s.Importance == "primary", gap = s.Kind == "gap", dim = s.Hidden || s.Discarded || s.State != "active";
            var merge = s.Manual ? null : _view.MergeOf(s.Anchor);
            if (merge is { lead: false }) dim = true;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            FrameworkElement lead;
            var numBadge = new TextBlock
            {
                Text = s.Manual ? "+" : s.Number > 0 ? s.Number.ToString() : "–", FontSize = Theme.FsCaption, FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = primary ? Theme.Fg2 : Theme.Muted, MinWidth = 22, Margin = new Thickness(0, 2, 6, 0), VerticalAlignment = VerticalAlignment.Top
            };
            if (gap) lead = U.Row(numBadge, new Border { Width = ThumbW + 2, Child = U.IconBadge(Theme.IWarn, 40, Theme.WarnSoft, Theme.Warn, 16) });
            else
            {
                var thumbImg = new Image { Stretch = Stretch.UniformToFill, Width = ThumbW, Height = ThumbH, Clip = new RectangleGeometry(new Rect(0, 0, ThumbW, ThumbH), Theme.RSmall - 1, Theme.RSmall - 1) };
                RenderOptions.SetBitmapScalingMode(thumbImg, BitmapScalingMode.HighQuality);
                var thumbBox = new Border { Width = ThumbW + 2, Height = ThumbH + 2, CornerRadius = new CornerRadius(Theme.RSmall), Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Child = thumbImg };
                var placeholder = U.Icon(KindIcon(s), 16, Theme.Muted); placeholder.IsHitTestVisible = false;
                var tg = new Grid();
                tg.Children.Add(thumbBox); tg.Children.Add(placeholder);
                var lg = U.Row(numBadge, tg);
                thumbImg.Loaded += (_, _) => placeholder.Visibility = thumbImg.Source != null ? Visibility.Collapsed : Visibility.Visible;
                if (dim) lg.Opacity = 0.45;
                lead = lg;
                _thumbTargets[s.Id] = thumbImg;
                if (_thumbs.TryGetValue(s.Id, out var t)) thumbImg.Source = t;
                else _thumbQueue.Enqueue(s);
            }
            lead.VerticalAlignment = VerticalAlignment.Top; lead.Margin = new Thickness(lead.Margin.Left, lead.Margin.Top, 12, 0);
            g.Children.Add(lead);
            var head = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal, Foreground = dim ? Theme.Muted : s.Importance == "trace" ? Theme.Fg2 : Theme.Fg, FontStyle = gap ? FontStyles.Italic : FontStyles.Normal, MaxHeight = 2 * 19 + 2, LineHeight = 19, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, TextTrimming = TextTrimming.CharacterEllipsis };
            if (s.Bookmarked) head.Inlines.Add(new System.Windows.Documents.Run(Theme.IStarFill + " ") { FontFamily = Theme.Icons, FontSize = Theme.FsCaption, Foreground = Theme.Warn, BaselineAlignment = BaselineAlignment.Center });
            head.Inlines.Add(new System.Windows.Documents.Run(s.AuthorTitle ?? s.Title));
            var meta = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            var kindRow = U.Row(U.Icon(KindIcon(s), 12, Theme.Muted), U.Meta(KindLabel(s.Kind), new Thickness(5, 0, 8, 0)), U.Meta(ShortTime(Fmt.Time(s.TFrom, "de")), new Thickness(0, 0, 8, 0)));
            kindRow.Margin = new Thickness(0, 1, 0, 3);
            meta.Children.Add(kindRow);
            if (merge is { lead: true } ml) meta.Children.Add(U.Chip(Loc.F("{0} zusammengefasst", "{0} merged", ml.count), Theme.AccentSoft, Theme.AccentText, Theme.ICompare));
            else if (merge is { lead: false } mm) meta.Children.Add(U.Chip(Loc.F("→ Schritt {0}", "→ step {0}", mm.leadNumber), Theme.Surface2, Theme.Muted));
            if (s.Unreviewed) meta.Children.Add(U.Chip(Loc.L("ungeprüft", "unreviewed"), Theme.WarnSoft, Theme.Warn, Theme.IWarn));
            else if (qualityWarn.TryGetValue(s.Anchor, out var qf)) { var qc = U.Chip(qf.Rule, Theme.WarnSoft, Theme.Warn, Theme.IChecklist); qc.ToolTip = qf.Text(Loc.Lang); meta.Children.Add(qc); }
            if (s.EvidenceLevel == "inputOnly" && s.Kind is not ("gap" or "system")) meta.Children.Add(U.Chip(Loc.L("nur Eingabe", "input only"), Theme.Surface2, Theme.Muted));
            if (waitMs is long wm && wm >= 500)
            {
                bool slow = wm >= StepRecorder.ReadLayer.Timing.SlowMs;
                var tc = U.Chip(StepRecorder.ReadLayer.Timing.Ms(wm, Loc.Lang == "de"), slow ? Theme.WarnSoft : Theme.Surface2, slow ? Theme.Warn : Theme.Muted, Theme.IStopwatch);
                tc.ToolTip = StepRecorder.ReadLayer.Timing.Of(s.Json) is { } tj ? Loc.L("Reaktion der Anwendung: ", "Application response: ") + StepRecorder.ReadLayer.Timing.Text(tj, Loc.Lang) : null;
                meta.Children.Add(tc);
            }
            if (s.Hidden) { var hc = U.Chip(s.CleanupReason != null ? Loc.L("aufgeräumt", "cleaned up") : Loc.L("ausgeblendet", "hidden"), Theme.Surface2, Theme.Muted, Theme.IEye); if (s.CleanupReason != null) hc.ToolTip = s.CleanupReason; meta.Children.Add(hc); }
            if (s.Discarded) meta.Children.Add(U.Chip(Loc.L("verworfen", "discarded"), Theme.DangerSoft, Theme.Danger));
            SectionChips(s, meta);
            if (NotesOf(s).Count is int nc && nc > 0) meta.Children.Add(U.Chip(nc.ToString(), Theme.Surface2, Theme.Muted, Theme.IComment));
            if (s.AuthorTitle != null || s.AuthorBody != null) meta.Children.Add(U.Chip(Loc.L("bearbeitet", "edited"), Theme.Surface2, Theme.Muted, Theme.IEdit));
            var col = U.Col(head, meta); col.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(col, 1); g.Children.Add(col);
            if (s.ChapterTitle != null)
            {
                var chap = new TextBlock { Text = (s.ChapterLevel == 2 ? "§§  " : "§  ") + s.ChapterTitle + (s.ChapterHidden ? "  ·  " + Loc.L("ausgeblendet", "hidden") : ""), TextDecorations = s.ChapterHidden ? TextDecorations.Strikethrough : null, FontSize = Theme.FsCaption, FontWeight = FontWeights.SemiBold, Foreground = Theme.Fg2, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 4), ToolTip = Loc.F("Kapitelbeginn: {0}", "Chapter start: {0}", s.ChapterTitle) };
                col.Children.Insert(0, chap);
            }
            _list.Items.Add(new ListBoxItem { Content = g, Tag = s, ToolTip = s.AuthorTitle ?? s.Title });
        }
        var sel = _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => ((ViewStep)i.Tag).Id == keepId) ?? _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => ((ViewStep)i.Tag).Importance == "primary");
        if (sel != null && !_cover) { _list.SelectedItem = sel; _list.ScrollIntoView(sel); }
        _listCount.Text = query.IsEmpty ? Loc.N(n, "1 Schritt", "{0} Schritte", "1 step", "{0} steps") : Loc.N(n, "1 Treffer", "{0} Treffer", "1 match", "{0} matches");
        UpdateStatus();
        UpdateQualityLink();
        PumpThumbs();
        if (_cover) ApplyCoverMode();
    }

    /// <summary>Listeneintrag unter einem Ereignisursprung (auch für Text-Runs, die keine Visuals sind).</summary>
    static ListBoxItem? ItemAt(object src)
    {
        var d = src as DependencyObject;
        while (d != null && d is not ListBoxItem)
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as ListBoxItem;
    }

    void MoveTo(ViewStep src, ViewStep dst)
    {
        // dieselbe Folge wie in der Liste (samt ausgeblendeten Schritten, die so ihren Platz behalten)
        var seq = _view.Effective(importance: "all", includeManual: true, ordered: true).Select(x => x.Anchor).ToList();
        int from = seq.IndexOf(src.Anchor), to = seq.IndexOf(dst.Anchor);
        if (from < 0 || to < 0 || from == to) return;
        seq.RemoveAt(from); seq.Insert(to, src.Anchor);
        Change(e => e["order"] = new JsonArray(seq.Select(x => (JsonNode)x).ToArray()));
        _status.Text = Loc.F("Schritt verschoben – rückgängig mit {0}", "Step moved – undo with {0}", Cmd("undo").Gesture);
    }

    // ---------------- Vorschaubilder ----------------

    void PumpThumbs()
    {
        if (_thumbBusy || _thumbQueue.Count == 0 || _closed) return;
        _thumbBusy = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            _thumbBusy = false;
            if (_closed || _thumbQueue.Count == 0) return;
            var s = _thumbQueue.Dequeue();
            if (!_thumbs.ContainsKey(s.Id))
            {
                BitmapSource? t = null;
                try { t = RenderThumb(s); } catch (Exception ex) { UiApp.Log("thumb: " + ex.Message); }
                _thumbs[s.Id] = t;
            }
            if (_thumbTargets.TryGetValue(s.Id, out var img) && _thumbs[s.Id] is { } src)
            {
                img.Source = src;
                Anim.Refresh(img, 0, 320);
                if (img.Parent is Border { Parent: Grid lg } && lg.Children.Count > 1) lg.Children[1].Visibility = Visibility.Collapsed;
            }
            PumpThumbs();
        });
    }

    BitmapSource? RenderThumb(ViewStep s)
    {
        string variant = DefaultVariant(s);
        bool annotate = true;
        using var img = StepImages.Render(_view, s, variant, "original", annotate: annotate, viewMode: "screen", authorAnnotations: true, stepLabel: StepLabel(s)) ?? (variant == "after" ? StepImages.Render(_view, s, "before", "original", authorAnnotations: true) : null);
        if (img == null) return null;
        int w = ThumbRender, h = Math.Max(1, (int)Math.Round(img.Bitmap.Height * (double)ThumbRender / Math.Max(1, img.Bitmap.Width)));
        using var small = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(small))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(img.Bitmap, 0, 0, w, h);
        }
        return U.ToSource(small);
    }

    /// <summary>Vorschaubild eines Schritts neu erzeugen (nach Markierungen).</summary>
    void InvalidateThumb(ViewStep? s)
    {
        if (s == null) return;
        _thumbs.Remove(s.Id);
        if (_thumbTargets.ContainsKey(s.Id)) { _thumbQueue.Enqueue(s); PumpThumbs(); }
    }

    /// <summary>Alle Vorschaubilder neu erzeugen (nach Schwärzungen oder Rückgängig).</summary>
    void InvalidateAllThumbs()
    {
        _thumbs.Clear();
        _thumbQueue.Clear();
        foreach (var it in _list.Items.OfType<ListBoxItem>()) if (it.Tag is ViewStep vs && _thumbTargets.ContainsKey(vs.Id)) _thumbQueue.Enqueue(vs);
        PumpThumbs();
    }

    // =====================================================================================
    // Detailbereich
    // =====================================================================================

    /// <summary>Vorschaubild in der Schrittliste (Anzeige) und Renderbreite (für hohe Bildschirmauflösung doppelt).</summary>
    const double ThumbW = 88, ThumbH = 55;
    const int ThumbRender = 192;
    /// <summary>Breite der Beschriftungsspalte in den Info-Zeilen (Fenster, Ziel, Eingabe …).</summary>
    const double InfoLabelWidth = 108;

    void ShowDetails(ViewStep s)
    {
        s = _view.All.FirstOrDefault(x => x.Id == s.Id) ?? s;   // nach dem Speichern sind die Listeneinträge veraltet
        _details.Children.Clear();
        _editTitle = null; _editBody = null; _editAnchor = null;
        if (_detailTab == 1)
        {
            if (_list.SelectedItems.Count > 1) _details.Children.Add(U.InfoBar(Loc.L("Mehrere Schritte ausgewählt", "Several steps selected"), Loc.F("Die technischen Angaben zeigen den ersten von {0} Schritten.", "The technical details show the first of {0} steps.", _list.SelectedItems.Count), false, new Thickness(0, 0, 0, 12)));
            TechDetails(s); return;
        }
        if (_list.SelectedItems.Count > 1) { ShowBulk(); return; }

        var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        if (s.Manual) chips.Children.Add(U.Chip(Loc.L("Eigener Schritt – nicht aus der Aufnahme", "Own step – not from the recording"), Theme.AccentSoft, Theme.AccentText, KindIcon(s)));
        else
        {
        chips.Children.Add(U.Chip(Loc.F("Schritt {0}", "Step {0}", s.Number), Theme.Surface2, Theme.Fg2));
        chips.Children.Add(U.Chip(KindLabel(s.Kind), Theme.AccentSoft, Theme.AccentText, KindIcon(s)));
        if (s.Hidden) { var hc = U.Chip(s.CleanupReason != null ? Loc.L("aufgeräumt", "cleaned up") : Loc.L("ausgeblendet", "hidden"), Theme.Surface2, Theme.Muted, Theme.IEye); if (s.CleanupReason != null) hc.ToolTip = s.CleanupReason; chips.Children.Add(hc); }
        var (evText, evBg, evFg) = s.EvidenceLevel switch { "confirmed" => (Loc.L("belegt", "confirmed"), Theme.SuccessSoft, Theme.Success), "inputOnly" => (Loc.L("nur Eingabe", "input only"), Theme.WarnSoft, Theme.Warn), _ => (Loc.L("unbekannt", "unknown"), Theme.Surface2, Theme.Muted) };
        var ev = U.Chip(evText, evBg, evFg, s.EvidenceLevel == "confirmed" ? Theme.ICheck : null);
        ev.ToolTip = s.EvidenceLevel switch { "confirmed" => Loc.L("Die Wirkung der Aktion wurde beobachtet.", "The effect of the action was observed."), "inputOnly" => Loc.L("Nur die Eingabe ist bekannt, eine Wirkung wurde nicht beobachtet.", "Only the input is known; no effect was observed."), _ => Loc.L("Für diesen Schritt liegen keine Belege vor.", "There is no evidence for this step.") };
        chips.Children.Add(ev);
        if (s.ImageReplaced) chips.Children.Add(U.Chip(Loc.L("Bild ersetzt", "Image replaced"), Theme.Surface2, Theme.Muted, Theme.ICamera));
        }
        // Stern rechts neben den Angaben: Lesezeichen (nur für den Autor)
        var chipRow = new DockPanel();
        var star = BookmarkButton(s); DockPanel.SetDock(star, Dock.Right); chipRow.Children.Add(star);
        chipRow.Children.Add(chips);
        _details.Children.Add(chipRow);

        if (s.Unreviewed)
            _details.Children.Add(Callout(Theme.IWarn, Theme.WarnSoft, Theme.Warn, Loc.L("Auffällige Eingabe", "Conspicuous input"), Loc.F("{0} – bitte in der Durchsicht prüfen, bevor Sie die Aufnahme weitergeben.", "{0} – please check it in the review before you share the recording.", Patterns.KindName(s.FlagReason ?? "", Loc.Lang))));

        // Während der Aufnahme geschriebener Kommentar: als intern markieren (erscheint dann in keiner Ausgabe)
        if (s.Kind == "comment" && !s.Manual)
        {
            long src = s.Json.Obj("input")?.Long("refSeq") ?? 0;
            if (s.CommentHidden && _view.OriginalCommentText(s) is { Length: > 0 } orig)
                _details.Children.Add(Callout(Theme.ILock, Theme.Surface2, Theme.Fg2, Loc.L("Interner Kommentar (nur hier sichtbar)", "Internal comment (visible only here)"), orig));
            var internalBox = new CheckBox { Content = Loc.L("Interner Kommentar – erscheint in keiner Ausgabe", "Internal comment – appears in no output"), IsChecked = s.CommentHidden, IsEnabled = !_view.Session.ReadOnly && src > 0, Margin = new Thickness(0, 0, 0, 12), ToolTip = Loc.F("Der Text dieses Kommentars wird in Anleitung, KI-Paket, MCP und Kommandozeilen-Export durch „{0}“ ersetzt; der Schritt wird ausgeblendet.", "The text of this comment is replaced by “{0}” in the guide, AI package, MCP and command-line export; the step is hidden.", _view.RedactedText) };
            internalBox.Checked += (_, _) => SetRecordedCommentHidden(src, true);
            internalBox.Unchecked += (_, _) => SetRecordedCommentHidden(src, false);
            _details.Children.Add(internalBox);
        }

        // Zusammengefasste Schritte: der führende Schritt zeigt und bearbeitet den Text der Gruppe
        var merge = s.Manual ? null : _view.MergeOf(s.Anchor);
        var composite = merge is { lead: true } ? _view.GuideStepFor(s) : null;
        if (merge is { lead: true } ml)
            _details.Children.Add(Callout(Theme.ICompare, Theme.AccentSoft, Theme.AccentText, Loc.N(ml.count, "1 Schritt zusammengefasst", "{0} Schritte zusammengefasst", "1 step merged", "{0} steps merged"),
                Loc.F("In Anleitungen erscheinen die Schritte {0} als ein Schritt mit diesem Text; Bild vorher vom ersten, nachher vom letzten. KI-Paket und AutoHotkey zeigen weiter alle. Aufheben: {1}.", "In guides, steps {0} appear as one step with this text; before image from the first, after image from the last. AI package and AutoHotkey still show all. Dissolve: {1}.",
                    string.Join(", ", ml.numbers), Cmd("unmerge").Gesture)));
        else if (merge is { lead: false } mm)
            _details.Children.Add(Callout(Theme.ICompare, Theme.Surface2, Theme.Fg2, Loc.F("Teil von Schritt {0}", "Part of step {0}", mm.leadNumber),
                Loc.L("Dieser Schritt ist in Anleitungen im führenden Schritt enthalten; sein Text fließt dort ein, solange der führende Schritt keinen eigenen Text hat.", "In guides this step is contained in the leading step; its text is used there as long as the leading step has no text of its own.")));

        // Überschrift und Text direkt bearbeiten
        bool ro = _view.Session.ReadOnly;
        _editAnchor = s.Anchor;
        _editOrigTitle = composite != null ? composite.AuthorTitle ?? composite.Title : s.AuthorTitle ?? s.Title;
        // Anleitungstext: formatiert (13, Paket 21) – formatierter Autorentext wird als Auszeichnung geladen, sonst als reiner Text
        var bodySrc = composite ?? s;
        string bodyText = bodySrc.BodyRich != null ? bodySrc.BodyRich.Doc.ToMarkup() : bodySrc.AuthorBody ?? bodySrc.Guide;
        _editTitle = new TextBox { Text = _editOrigTitle, FontSize = Theme.FsSub, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, IsReadOnly = ro, MaxLength = 300 };
        _editBody = new RichEdit(Loc.L("Anleitungstext", "Guide text"), 96, 8000, _view.Language) { IsReadOnly = ro, AutoCommit = false };
        _editBody.Load(bodyText, bodySrc.BodyRich != null);
        var editTitle = _editTitle; var editBody = _editBody;
        Spell(editTitle);
        editTitle.GotKeyboardFocus += (_, _) => _lastInlineTitle = true; editBody.GotKeyboardFocus += (_, _) => _lastInlineTitle = false;
        _lastInlineTitle = false;
        editTitle.KeyDown += (_, e) => { if (e.Key == Key.Enter) { editBody.FocusEnd(); e.Handled = true; } };
        bool Inside(object? f) => f is DependencyObject d && (d == editTitle || d == editBody || editBody.IsAncestorOf(d) || (d is FrameworkContentElement fce && fce.Parent is DependencyObject p && editBody.IsAncestorOf(p)));
        editTitle.LostKeyboardFocus += (_, e) => { if (Inside(e.NewFocus)) return; InlineCommit(); };
        editBody.LostKeyboardFocus += (_, e) => { if (Inside(e.NewFocus)) return; InlineCommit(); };
        editTitle.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { editTitle.Text = _editOrigTitle; editBody.Revert(); _list.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { _list.Focus(); e.Handled = true; }
        };
        editBody.Left += () => { editTitle.Text = _editOrigTitle; _list.Focus(); };
        bool edited = composite?.MergeId is { } cmid ? MergeTextEdited(cmid) : s.AuthorTitle != null || s.AuthorBody != null;
        var textBox = Section(Loc.L("Text", "Text"), labelMargin: new Thickness(0, 4, 0, 8));
        textBox.Children.Add(U.Field(Loc.L("Überschrift", "Heading"), editTitle));
        var bodyField = U.Field(Loc.L("Anleitungstext", "Guide text"), editBody, margin: new Thickness(0, 0, 0, 8));
        textBox.Children.Add(bodyField);
        var editInfo = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        if (edited && !ro)
        {
            var reset = U.Btn(Theme.IUndo, Loc.L("Original", "Original"), () => ResetText(s), Loc.L("Automatisch erzeugten Text wiederherstellen", "Restore the automatically generated text"), "Link");
            reset.Margin = new Thickness(8, -2, -6, 0); reset.Padding = new Thickness(6, 2, 6, 2); reset.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(reset, Dock.Right); editInfo.Children.Add(reset);
        }
        if (!ro) { var snip = SnippetButton(); DockPanel.SetDock(snip, Dock.Right); editInfo.Children.Add(snip); }
        var infoText = U.Text(ro ? Loc.L("Schreibgeschützt – zum Bearbeiten bitte entpacken.", "Read-only – unpack to edit.") : s.Manual ? Loc.L("Eigener Text · wird automatisch gespeichert", "Own text · saved automatically") : edited ? Loc.L("Von Ihnen geändert · wird automatisch gespeichert", "Changed by you · saved automatically") : Loc.L("Automatisch erzeugt – einfach überschreiben, wird automatisch gespeichert", "Generated automatically – just overwrite it, saved automatically"), Theme.FsCaption, fg: Theme.Muted);
        infoText.TextWrapping = TextWrapping.Wrap;
        editInfo.Children.Add(infoText);
        editInfo.Margin = new Thickness(0, 0, 0, 12);
        textBox.Children.Add(editInfo);

        // Hinweise
        NotesSection(s);

        // Einordnung: Wichtigkeit, Kapitel, Reihenfolge und Sichtbarkeit
        var place = Section(Loc.L("Einordnung", "Placement"));
        if (!s.Manual)
        {
            int impIdx = Math.Max(0, Array.IndexOf(ImportanceOrder, s.Importance));
            var (impTrack, imps) = U.Segmented("dimp", impIdx, (null, Loc.L("Hauptschritt", "Main step")), (null, Loc.L("Nebenschritt", "Secondary step")), (null, Loc.L("Spur", "Trace")));
            for (int i = 0; i < imps.Length; i++) { string target = ImportanceOrder[i]; imps[i].Checked += (_, _) => { if (target != s.Importance) Change(e => StepEdit(e, s.Anchor)["importance"] = target); }; imps[i].IsEnabled = !ro; }
            place.Children.Add(U.Field(Loc.L("Wichtigkeit", "Importance"), impTrack, ImportanceHint(s.Importance)));
        }
        ChapterBlock(s, place);
        var acts = new WrapPanel { Margin = new Thickness(-3, -3, 0, 12) };
        var hide = U.Btn(Theme.IEye, s.Hidden ? Loc.L("Einblenden", "Show") : Loc.L("Ausblenden", "Hide"), () => Run("hide"), TipOf("hide")); hide.IsEnabled = !ro;
        acts.Children.Add(hide);
        foreach (var id in new[] { "moveUp", "moveDown" })
        {
            var mv = U.Ghost(Glyphs[id], null, () => Run(id), TipOf(id)); mv.Margin = new Thickness(1, 3, 1, 3); mv.MinWidth = 36; mv.Padding = new Thickness(8, 6, 8, 6); mv.IsEnabled = !ro;
            acts.Children.Add(mv);
        }
        place.Children.Add(acts);

        // Bild in der Anleitung
        ImageBlock(s);

        // Kurzangaben
        _details.Children.Add(U.Divider(new Thickness(0, 24, 0, 4)));
        if (s.Json.Obj("ctx") is { } ctx) InfoRow(Loc.L("Fenster", "Window"), ReadOnlyText(Loc.F("„{0}“ · {1}", "“{0}” · {1}", ctx.Str("title"), ctx.Str("process")), Theme.Fg2, Theme.FsHint));
        if (s.Json.Obj("target") is not null) InfoRow(Loc.L("Ziel", "Target"), ReadOnlyText(Steps.TextTemplates.TargetLabel(s.Json, Loc.Lang, false), Theme.Fg2, Theme.FsHint));
        var more = U.Btn(Theme.ICode, Loc.L("Technische Angaben", "Technical details"), () => _tabBtns[1].IsChecked = true, Loc.L("Zum Reiter „Technik“", "Go to the “Technical” tab"), "Link");
        more.HorizontalAlignment = HorizontalAlignment.Left; more.Margin = new Thickness(-8, 10, 0, 0);
        _details.Children.Add(more);
    }

    static string ImportanceHint(string imp) => imp switch
    {
        "primary" => Loc.L("Erscheint in der Anleitung und im Export.", "Appears in the guide and in the export."),
        "secondary" => Loc.L("Begleitender Schritt – im Export nur, wenn „Haupt- und Nebenschritte“ gewählt ist.", "Supporting step – in the export only if “Main and secondary steps” is selected."),
        _ => Loc.L("Reine Spur (z. B. Mausbewegung) – nur unter „Mit Spur“ sichtbar.", "Trace only (e.g. mouse movement) – visible only under “With trace”.")
    };

    void InfoRow(string label, UIElement value)
    {
        var g = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(InfoLabelWidth) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var l = U.Text(label, Theme.FsHint, fg: Theme.Muted, margin: new Thickness(0, 1, 10, 0)); l.TextTrimming = TextTrimming.CharacterEllipsis; l.TextWrapping = TextWrapping.NoWrap; l.ToolTip = label;
        g.Children.Add(l);
        Grid.SetColumn(value, 1); g.Children.Add(value);
        _details.Children.Add(g);
    }

    void TechDetails(ViewStep s)
    {
        var j = s.Json;
        _details.Children.Add(U.Heading(s.AuthorTitle ?? s.Title, Theme.FsSub, new Thickness(0, 0, 0, 4)));
        _details.Children.Add(U.Text(Loc.F("Schritt {0} · {1} · {2} · {3}", "Step {0} · {1} · {2} · {3}", s.Number, KindLabel(s.Kind), ImportanceLabel(s.Importance), Fmt.Time(s.TFrom, Loc.Lang)), Theme.FsHint, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 6)));
        if (j.Obj("ctx") is { } ctx) InfoRow(Loc.L("Fenster", "Window"), ReadOnlyText(Loc.F("„{0}“\n{1}", "“{0}”\n{1}", ctx.Str("title"), ctx.Str("process"))));
        if (j.Obj("target") is { } t)
        {
            var v = U.Col(ReadOnlyText(Steps.TextTemplates.TargetLabel(j, Loc.Lang, false)));
            if (t.Str("status") is { } st) { var chip = U.Chip(st switch { "agreed" => Loc.L("übereinstimmend", "agreed"), "corroborated" => Loc.L("bestätigt", "corroborated"), "single" => Loc.L("eine Quelle", "single source"), "conflict" => Loc.L("widersprüchlich", "conflicting"), "position" => Loc.L("nur Position", "position only"), _ => st }, Theme.Surface2, Theme.Muted); chip.HorizontalAlignment = HorizontalAlignment.Left; chip.Margin = new Thickness(0, 4, 0, 0); v.Children.Add(chip); }
            InfoRow(Loc.L("Ziel", "Target"), v);
        }
        if (s.Kind == "type" && j.Obj("input") is { } inp)
        {
            var v = U.Col(ReadOnlyText(s.Unreviewed ? Loc.L("(ungeprüft)", "(unreviewed)") : Steps.TextTemplates.TokensNotation(inp), font: Theme.Mono, size: Theme.FsHint));
            if (inp.Str("valueAfter") is { } va && !s.Unreviewed) v.Children.Add(ReadOnlyText(Loc.F("Feldinhalt danach: {0}", "Field content afterwards: {0}", va), Theme.Muted, Theme.FsHint));
            InfoRow(Loc.L("Eingabe", "Input"), v);
        }
        if (s.Kind is "click" or "doubleClick" or "tripleClick" or "rightClick" or "drag" && j.Obj("input") is { } mi) InfoRow(Loc.L("Maus", "Mouse"), ReadOnlyText(MouseSummary(s.Kind, mi)));
        if (j.Obj("effect") is { } eff && eff.Bool("observed")) InfoRow(Loc.L("Wirkung", "Effect"), ReadOnlyText(EffectSummary(eff)));
        var (evText, evBg, evFg) = s.EvidenceLevel switch { "confirmed" => (Loc.L("belegt", "confirmed"), Theme.SuccessSoft, Theme.Success), "inputOnly" => (Loc.L("nur Eingabe", "input only"), Theme.WarnSoft, Theme.Warn), _ => (Loc.L("unbekannt", "unknown"), Theme.Surface2, Theme.Muted) };
        var evChip = U.Chip(evText, evBg, evFg, s.EvidenceLevel == "confirmed" ? Theme.ICheck : null); evChip.HorizontalAlignment = HorizontalAlignment.Left;
        var reasons = string.Join(", ", j.Obj("evidence")?.StrList("reasons") ?? new());
        InfoRow(Loc.L("Beleg", "Evidence"), reasons.Length > 0 ? U.Col(evChip, ReadOnlyText(reasons, Theme.Muted, Theme.FsHint)) : evChip);

        if (j.Obj("target")?.Obj("win32") is { } w32) Win32Section(s, w32, InfoRow);
        AppContextSection(j);

        _details.Children.Add(U.SectionLabel(Loc.L("Protokoll für KI", "Log for AI")));
        _details.Children.Add(U.Inset(ReadOnlyText(s.Protocol, Theme.Fg2, Theme.FsCaption, Theme.Mono), new Thickness(14, 12, 14, 12)));
        _details.Children.Add(new Expander
        {
            Header = Loc.L("Rohdaten (JSON)", "Raw data (JSON)"), Margin = new Thickness(0, 20, 0, 0),
            Content = U.Inset(new ScrollViewer { MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = ReadOnlyText(Json.Pretty(j), Theme.Fg2, Theme.FsCaption, Theme.Mono) }, new Thickness(14, 12, 14, 12))
        });
    }

    /// <summary>Programmkontext für Automatisierung: Menübefehl, Liste, Explorer, Browser, Office, Statusleiste, Schreibmarke, MSAA.</summary>
    void AppContextSection(JsonObject j)
    {
        var rows = new List<(string label, string value)>();
        string Q(string? t) => Loc.F("„{0}“", "“{0}”", t);
        if (j.Obj("menuPath") is { } mp && (mp.LongN("commandId") != null || mp.Str("source") != null))
            rows.Add((Loc.L("Menübefehl", "Menu command"), string.Join(" › ", (mp.Arr("items") ?? new JsonArray()).Select(x => x?.ToString())) + (mp.LongN("commandId") is long id ? Loc.F("\nBefehls-ID {0} (WM_COMMAND)", "\nCommand ID {0} (WM_COMMAND)", id) : "") + (mp.Bool("disabled") ? Loc.L("\ndeaktiviert", "\ndisabled") : "")));
        if (j.Obj("choose")?.Obj("win32") is { } cw)
            rows.Add((Loc.L("Liste", "List"), $"{cw.Str("kind")} {cw.Str("classNN")}" + (cw.Str("text") is { } ct ? Loc.F("\nEintrag {0}", "\nItem {0}", Q(ct)) : "") + (cw.LongN("index") is long ix ? Loc.F(" · Nr. {0}", " · No. {0}", ix) + (cw.LongN("count") is long cn ? Loc.F(" von {0}", " of {0}", cn) : "") : "") + (cw.Str("path") is { } pth ? Loc.F("\nPfad {0}", "\nPath {0}", pth) : "")));
        if (j.Obj("explorer") is { } ex)
            rows.Add(("Explorer", (ex.Str("folder") ?? ex.Str("location") ?? "") + (ex.Str("folderAfter") is { } fa && fa != ex.Str("folder") ? $"\n→ {fa}" : "") + (ex.LongN("selectedCount") is long sc && sc > 0 ? Loc.F("\n{0} ausgewählt", "\n{0} selected", sc) : "")));
        if (j.Obj("data")?.Obj("fileDialog") is { } fd && fd.Str("folder") is { } fdf)
            rows.Add((Loc.L("Dateidialog", "File dialog"), fdf + (fd.Str("fileNameControl") is { } fnc ? Loc.F("\nDateiname-Feld {0}", "\nFile name field {0}", fnc) : "")));
        if (j.Obj("browser") is { } br)
            rows.Add(("Browser", $"{br.Str("browser")}\n{br.Str("url")}" + (br.Str("urlAfter") is { } ua && ua != br.Str("url") ? $"\n→ {ua}" : "")));
        if (j.Obj("office") is { } of)
            rows.Add(("Office", string.Join("\n", new[] { of.Str("app"), of.Str("workbookName") ?? of.Str("documentName"), of.Str("sheet") is { } sh ? Loc.F("Blatt {0}", "Sheet {0}", sh) + (of.Str("cell") is { } cell ? Loc.F(" · Zelle {0}", " · Cell {0}", cell) : "") : null }.Where(x => x != null))));
        if (j.Obj("verify")?.Obj("statusBar") is { } sb)
            rows.Add((Loc.L("Statusleiste", "Status bar"), string.Join(" | ", (sb.Arr("parts") ?? new JsonArray()).Select(x => x?.ToString())) + ((sb.Arr("changed")?.Count ?? 0) > 0 ? Loc.L("\n(nach der Aktion geändert)", "\n(changed after the action)") : "")));
        if (j.Obj("target")?.Obj("win32")?.Obj("control")?.Obj("caret") is { } ca)
            rows.Add((Loc.L("Schreibmarke", "Caret"), Loc.F("Zeile {0}, Spalte {1}", "Line {0}, column {1}", ca.LongN("line"), ca.LongN("col")) + (ca.Long("selLen") > 0 ? Loc.F(" · {0} Zeichen markiert", " · {0} characters selected", ca.Long("selLen")) : "") + (ca.Bool("readOnly") ? Loc.L(" · schreibgeschützt", " · read-only") : "")));
        if (j.Obj("target")?.Obj("win32")?.Obj("msaa") is { } ms)
            rows.Add(("MSAA", $"{ms.Str("roleText") ?? ms.Str("role")}" + (ms.Str("defaultAction") is { } da ? Loc.F(" · Standardaktion {0}", " · Default action {0}", Q(da)) : "")));
        if (rows.Count == 0) return;
        _details.Children.Add(U.SectionLabel(Loc.L("Programmkontext", "Application context"), new Thickness(0, 24, 0, 2)));
        foreach (var (label, value) in rows) InfoRow(label, ReadOnlyText(value.Trim(), Theme.Fg, Theme.FsHint));
    }

    // ---------------- Direktbearbeitung ----------------

    void FocusInlineTitle()
    {
        if (Sel == null) return;
        if (_detailTab != 0) _tabBtns[0].IsChecked = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { _editTitle?.Focus(); _editTitle?.SelectAll(); });
    }

    /// <summary>Übernimmt geänderte Überschrift/Text. Verzögert, damit ein Klick in die Liste zuerst ausgewertet wird.</summary>
    void InlineCommit(bool now = false)
    {
        if (_editAnchor is not long anchor || _editTitle == null || _editBody == null || _editTitle.IsReadOnly) return;
        string t = _editTitle.Text.Trim();
        bool bodyChanged = _editBody.Changed(out var b, out var rich);
        if (t == _editOrigTitle.Trim() && !bodyChanged) return;
        if (t.Length == 0) { _editTitle.Text = _editOrigTitle; return; }
        b = b.TrimEnd();
        _editOrigTitle = t; _editBody.MarkSaved(b, rich);
        if (now) SaveText(anchor, t, b, rich);
        else Dispatcher.BeginInvoke(DispatcherPriority.Background, () => SaveText(anchor, t, b, rich));
    }

    void SaveText(long anchor, string title, string body, bool rich = false)
    {
        // führender Schritt einer Gruppe: der Text gehört dem zusammengefassten Schritt
        if (anchor >= 0 && _view.MergeOf(anchor) is { lead: true } mg) { SaveMergeText(mg.id, title, body, rich); return; }
        Change(e =>
        {
            var se = StepEdit(e, anchor);
            var text = se["text"] as JsonObject ?? new JsonObject();
            var o = new JsonObject { ["title"] = title, ["body"] = body, ["by"] = "user", ["atUtc"] = Json.UtcNow() };
            if (rich) o["fmt"] = 1;
            text[_view.Language] = o;
            se["text"] = text;
        });
    }

    void ResetText(ViewStep s)
    {
        if (!s.Manual && _view.MergeOf(s.Anchor) is { lead: true } mg) { ResetMergeText(mg.id); return; }
        Change(e =>
        {
            var se = StepEdit(e, s.Anchor);
            if (se["text"] is JsonObject t) { t.Remove(_view.Language); if (t.Count == 0) se.Remove("text"); }
        });
    }

    // =====================================================================================
    // Zeichnen, Auswählen, Verschieben
    // =====================================================================================

    static double D(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<double>(out var d) ? d : v.TryGetValue<int>(out var i) ? i : v.TryGetValue<long>(out var l) ? l : 0) : 0;

    static System.Drawing.PointF PF(Point p) => new((float)p.X, (float)p.Y);

    /// <summary>Umriss einer Markierung in Bildkoordinaten (Geometrie aus dem Core, wie im Export).</summary>
    static Rect? Bounds(JsonObject a) => AnnotationGeometry.Bounds(a) is { } r ? new Rect(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)) : null;

    /// <summary>Markierung unter dem Punkt samt Teil (1 Markierung, 2 Linse, 3 Quelle der Lupe). Spotlights zuletzt, weil sie große Flächen decken.</summary>
    (JsonObject? ann, int part) HitAnnotation(Point p)
    {
        if (_currentMeta == null) return (null, 0);
        float tol = (float)(8 / Math.Max(0.05, _currentMeta.Scale));
        var anns = AnnotationsFor(_currentMeta.FrameId).Reverse().ToList();
        foreach (var a in anns.Where(x => x.Str("type") != "spotlight").Concat(anns.Where(x => x.Str("type") == "spotlight")))
            if (AnnotationGeometry.Hit(a, PF(p), tol) is int part and > 0) return (a, part);
        return (null, 0);
    }

    Point ToOverlay(Point img) => _currentMeta == null ? img : new Point((img.X - _currentMeta.SourceRect.X) * _currentMeta.Scale, (img.Y - _currentMeta.SourceRect.Y) * _currentMeta.Scale);

    /// <summary>Bildschirmpunkte → Overlay-Einheiten (damit Rahmen und Anfasser bei jedem Zoom gleich groß wirken).</summary>
    double Px(double screenPx) => screenPx / Math.Max(0.02, _zoom?.Zoom ?? 1);

    static List<Point> HandlesOf(JsonObject a) => AnnotationGeometry.Handles(a).Select(p => new Point(p.X, p.Y)).ToList();

    void DrawSelection()
    {
        if (_selRect != null) { _overlay.Children.Remove(_selRect); _selRect = null; }
        foreach (var hs in _handleShapes) _overlay.Children.Remove(hs);
        _handleShapes.Clear();
        if (_selAnn == null || _currentMeta == null) return;
        var a = AnnotationsFor(_currentMeta.FrameId).FirstOrDefault(x => x.Str("id") == _selAnn);
        if (a == null || Bounds(a) is not Rect b) { _selAnn = null; return; }
        double pad = Px(5) / Math.Max(0.05, _currentMeta.Scale);
        b.Inflate(pad, pad);
        var tl = ToOverlay(b.TopLeft); var br = ToOverlay(b.BottomRight);
        var ac = Theme.AccentColor;
        _selRect = new Rectangle
        {
            Stroke = Theme.Accent, StrokeThickness = Px(1.5), StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = new SolidColorBrush(Color.FromArgb(22, ac.R, ac.G, ac.B)),
            Width = Math.Max(4, br.X - tl.X), Height = Math.Max(4, br.Y - tl.Y), IsHitTestVisible = false
        };
        _selBase = new Rect(tl, br);
        Canvas.SetLeft(_selRect, tl.X); Canvas.SetTop(_selRect, tl.Y);
        _overlay.Children.Add(_selRect);
        double hsz = Px(10);
        var handles = HandlesOf(a);
        for (int i = 0; i < handles.Count; i++)
        {
            var o = ToOverlay(handles[i]);
            bool zoomHandle = a.Str("type") == "magnifier" && i == 4;
            Shape sq = zoomHandle
                ? new Ellipse { Width = hsz * 1.2, Height = hsz * 1.2, Fill = Theme.Accent, Stroke = Brushes.White, StrokeThickness = Px(1.5), IsHitTestVisible = false, ToolTip = Loc.L("Zoom ändern", "Change zoom") }
                : new Rectangle { Width = hsz, Height = hsz, Fill = Brushes.White, Stroke = Theme.Accent, StrokeThickness = Px(1.5), RadiusX = Px(2), RadiusY = Px(2), IsHitTestVisible = false };
            Canvas.SetLeft(sq, o.X - sq.Width / 2); Canvas.SetTop(sq, o.Y - sq.Height / 2);
            _overlay.Children.Add(sq); _handleShapes.Add(sq);
        }
    }

    void SyncColorTo(JsonObject a)
    {
        _syncingColor = true;
        string? shown = a.Str("type") == "highlight" ? a.Obj("style")?.Str("hue") ?? HighlightYellow : a.Str("type") == "spotlight" ? null : a.Obj("style")?.Str("color");
        if (shown != null) { ShowColor(shown); _color = shown; }
        int w = a.Str("type") == "spotlight" ? DimToWidth(a.Obj("style")?.Dbl("dim", 0.55) ?? 0.55) : (int)(a.Obj("style")?.Dbl("width", 4) ?? 4);
        _strokeBtns.OrderBy(b => Math.Abs((int)b.Tag - w)).FirstOrDefault()?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
        _syncingColor = false;
    }

    /// <summary>Strichstärke steuert beim Spotlight die Abdunklung: dünn 0,4 · mittel 0,55 · dick 0,7.</summary>
    static double WidthToDim(int w) => w <= 2 ? 0.4 : w >= 7 ? 0.7 : 0.55;
    static int DimToWidth(double d) => d <= 0.45 ? 2 : d >= 0.65 ? 7 : 4;

    int _movePart = 1;
    JsonNode? _handleGeom;
    List<System.Drawing.PointF>? _freePts;

    void OverlayDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentMeta == null) return;
        var pos = e.GetPosition(_overlay);
        if (PipetteAt(pos)) return;
        if (_tool == "select")
        {
            var p = ToImage(pos);
            // Anfasser der aktuellen Auswahl?
            if (_selAnn != null && AnnotationsFor(_currentMeta.FrameId).FirstOrDefault(x => x.Str("id") == _selAnn) is { } sel)
            {
                double tol = Px(9) / Math.Max(0.05, _currentMeta.Scale);
                var hs = HandlesOf(sel);
                for (int i = 0; i < hs.Count; i++)
                    if ((hs[i] - p).Length <= tol) { _handle = i; _handleGeom = sel["geom"]?.DeepClone(); _overlay.CaptureMouse(); return; }
            }
            var (hit, part) = HitAnnotation(p);
            _selAnn = hit?.Str("id");
            DrawSelection();
            if (hit == null) { if (e.ClickCount == 2) OpenLightbox(); return; }
            SyncColorTo(hit);
            if (e.ClickCount == 2 && hit.Str("type") == "text") { EditAnnotationText(hit); return; }
            if (e.ClickCount == 2 && hit.Str("type") == "badge") { ToggleBadgeAuto(hit); return; }
            _movePart = part;
            _moveFrom = p; _overlay.CaptureMouse();
            return;
        }
        _dragStart = pos;
        if (_tool is "text" or "badge")
        {
            var p = ToImage(pos);
            // Nummern zählen automatisch (auto); der Text wird beim Speichern neu vergeben (Annotations.Renumber)
            string text = _tool == "badge" ? "1" : "";
            if (_tool == "text") { var d = new InputDialog(Loc.L("Text der Zeichnung", "Text of the drawing"), "") { Owner = this }; if (d.ShowDialog() != true || d.Value.Trim().Length == 0) { _dragStart = null; return; } text = d.Value; }
            AddAnnotation(_tool, new JsonArray((int)p.X, (int)p.Y), text);
            _dragStart = null;
            return;
        }
        var stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_color));
        if (_tool == "freehand") _freePts = new() { PF(ToImage(pos)) };
        _rubber = _tool switch
        {
            "arrow" or "line" => new Line { Stroke = stroke, StrokeThickness = _stroke },
            "freehand" => new Polyline { Stroke = stroke, StrokeThickness = _stroke, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Points = new PointCollection { pos } },
            "ellipse" => new Ellipse { Stroke = stroke, StrokeThickness = _stroke },
            "redact" => new Rectangle { Fill = Brushes.Black, Stroke = Brushes.Black, StrokeThickness = 1 },
            "pixelate" => new Rectangle { Fill = new DrawingBrush { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, Px(12), Px(12)), ViewportUnits = BrushMappingMode.Absolute, Drawing = new GeometryDrawing(new SolidColorBrush(Color.FromArgb(150, 120, 125, 135)), null, new RectangleGeometry(new Rect(0, 0, 1, 1))) }, Stroke = Brushes.Gray, StrokeThickness = Px(1) },
            "crop" => new Rectangle { Stroke = Theme.Accent, StrokeThickness = Px(2), StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)) },
            "highlight" => new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(90, ((SolidColorBrush)stroke).Color.R, ((SolidColorBrush)stroke).Color.G, ((SolidColorBrush)stroke).Color.B)) },
            "spotlight" => new Rectangle { Stroke = Brushes.White, StrokeThickness = Px(1.5), StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) },
            "magnifier" => new Rectangle { Stroke = stroke, StrokeThickness = Px(2), StrokeDashArray = new DoubleCollection { 2, 2 } },
            _ => new Rectangle { Stroke = stroke, StrokeThickness = _stroke }
        };
        _overlay.Children.Add(_rubber);
        _overlay.CaptureMouse();
    }

    void OverlayMove(object sender, MouseEventArgs e)
    {
        if (_handle >= 0 && _currentMeta != null && _selAnn != null && AnnotationsFor(_currentMeta.FrameId).FirstOrDefault(x => x.Str("id") == _selAnn) is { } ha)
        {
            // Vorschau der neuen Größe über den Auswahlrahmen
            var probe = new JsonObject { ["type"] = ha.Str("type"), ["geom"] = _handleGeom?.DeepClone(), ["style"] = ha.Obj("style")?.DeepClone() };
            probe["geom"] = AnnotationGeometry.Resized(probe, _handle, PF(ToImage(e.GetPosition(_overlay))));
            if (Bounds(probe) is Rect nb && _selRect != null)
            {
                var tl = ToOverlay(nb.TopLeft); var br = ToOverlay(nb.BottomRight);
                Canvas.SetLeft(_selRect, tl.X); Canvas.SetTop(_selRect, tl.Y); _selRect.Width = Math.Max(4, br.X - tl.X); _selRect.Height = Math.Max(4, br.Y - tl.Y);
            }
            return;
        }
        if (_moveFrom != null && _selRect != null && _currentMeta != null)
        {
            var p = ToImage(e.GetPosition(_overlay));
            double dx = (p.X - _moveFrom.Value.X) * _currentMeta.Scale, dy = (p.Y - _moveFrom.Value.Y) * _currentMeta.Scale;
            Canvas.SetLeft(_selRect, _selBase.X + dx); Canvas.SetTop(_selRect, _selBase.Y + dy);
            return;
        }
        if (_tool == "select" && _currentMeta != null && e.LeftButton == MouseButtonState.Released)
            _overlay.Cursor = HitAnnotation(ToImage(e.GetPosition(_overlay))).ann != null ? Cursors.SizeAll : Cursors.Arrow;
        if (_dragStart == null || _rubber == null) return;
        var a = _dragStart.Value; var b = e.GetPosition(_overlay);
        if (_rubber is Polyline pl && _freePts != null)
        {
            if ((pl.Points[^1] - b).Length >= Px(2)) { pl.Points.Add(b); _freePts.Add(PF(ToImage(b))); }
            return;
        }
        if (_rubber is Line l) { l.X1 = a.X; l.Y1 = a.Y; l.X2 = b.X; l.Y2 = b.Y; }
        else { Canvas.SetLeft(_rubber, Math.Min(a.X, b.X)); Canvas.SetTop(_rubber, Math.Min(a.Y, b.Y)); _rubber.Width = Math.Abs(b.X - a.X); _rubber.Height = Math.Abs(b.Y - a.Y); }
    }

    void OverlayUp(object sender, MouseButtonEventArgs e)
    {
        if (_handle >= 0)
        {
            _overlay.ReleaseMouseCapture();
            int hi = _handle; _handle = -1;
            var p = ToImage(e.GetPosition(_overlay));
            if (_selAnn is { } rid && _handleGeom != null)
            {
                var start = _handleGeom.DeepClone();
                Change(ed => { if (AnnotationEdit(ed, rid) is { } x) { var probe = new JsonObject { ["type"] = x.Str("type"), ["geom"] = start, ["style"] = x.Obj("style")?.DeepClone() }; x["geom"] = AnnotationGeometry.Resized(probe, hi, PF(p)); } }, refill: false);
            }
            return;
        }
        if (_moveFrom != null)
        {
            _overlay.ReleaseMouseCapture();
            var p = ToImage(e.GetPosition(_overlay));
            var d = p - _moveFrom.Value; _moveFrom = null;
            if (_selAnn != null && (Math.Abs(d.X) >= 1 || Math.Abs(d.Y) >= 1)) MoveAnnotation(_selAnn, d.X, d.Y, _movePart);
            else DrawSelection();
            return;
        }
        if (_dragStart == null) return;
        _overlay.ReleaseMouseCapture();
        var a = ToImage(_dragStart.Value); var b = ToImage(e.GetPosition(_overlay));
        _dragStart = null; _rubber = null;
        if (_tool == "freehand")
        {
            var pts = _freePts; _freePts = null;
            if (pts == null || pts.Count < 2 || Math.Max(pts.Max(q => q.X) - pts.Min(q => q.X), pts.Max(q => q.Y) - pts.Min(q => q.Y)) < 3) { ShowStep(); return; }
            AddAnnotation("freehand", AnnotationGeometry.Simplify(pts), null);
            return;
        }
        if (Math.Abs(b.X - a.X) < 3 && Math.Abs(b.Y - a.Y) < 3) { ShowStep(); return; }
        int x = (int)Math.Min(a.X, b.X), y = (int)Math.Min(a.Y, b.Y), w = (int)Math.Abs(b.X - a.X), h = (int)Math.Abs(b.Y - a.Y);
        switch (_tool)
        {
            case "redact" or "pixelate": AddImageRedaction(x, y, w, h, _tool == "pixelate" ? "pixelate" : "black"); return;
            case "crop": SetCrop(x, y, w, h); return;
            case "arrow" or "line": AddAnnotation(_tool, new JsonArray((int)a.X, (int)a.Y, (int)b.X, (int)b.Y), null); return;
            case "spotlight": AddAnnotation("spotlight", new JsonArray(x, y, w, h), null, new JsonObject { ["dim"] = WidthToDim(_stroke) }); return;
            case "magnifier":
                {
                    if (w < 8 || h < 8 || _currentMeta == null) { ShowStep(); return; }
                    // Linse im sichtbaren Ausschnitt platzieren (Rechnung relativ zum Ausschnitt, dann zurückverschieben)
                    var sr = _currentMeta.SourceRect;
                    var rel = AnnotationGeometry.NewMagnifier(new System.Drawing.RectangleF(x - sr.X, y - sr.Y, w, h), new System.Drawing.Size(sr.Width, sr.Height));
                    var mag = AnnotationGeometry.Moved(new JsonObject { ["type"] = "magnifier", ["geom"] = rel }, sr.X, sr.Y)!;
                    AddAnnotation("magnifier", mag, null, new JsonObject { ["shape"] = "round" });
                    return;
                }
            default: AddAnnotation(_tool, new JsonArray(x, y, w, h), null); return;
        }
    }

    void AddAnnotation(string type, JsonNode geom, string? text, JsonObject? extraStyle = null)
    {
        var s = Sel; if (s == null || _currentMeta == null) return;
        string frame = _currentMeta.FrameId;
        string color = _color; int width = _stroke, fontSize = _fontSize;
        var toolStyle = ToolStyleFor(type);
        Change(e =>
        {
            var arr = e["annotations"] as JsonArray ?? new JsonArray();
            var style = new JsonObject { ["color"] = color, ["width"] = width, ["fontSize"] = fontSize };
            foreach (var kv in toolStyle ?? new JsonObject()) style[kv.Key] = kv.Value?.DeepClone();
            foreach (var kv in extraStyle ?? new JsonObject()) style[kv.Key] = kv.Value?.DeepClone();
            var a = new JsonObject { ["id"] = NextId(e, "annotations", "a"), ["frame"] = frame, ["anchor"] = s.Anchor, ["type"] = type, ["geom"] = geom, ["style"] = style };
            if (text != null) a["text"] = text;
            if (type == "badge") a["auto"] = true;
            arr.Add(a); e["annotations"] = arr;
            if (type == "badge") Annotations.Renumber(e, frame);
        }, refill: false);
        _status.Text = type switch
        {
            "magnifier" => Loc.L("Lupe eingefügt – Linse und Quelle lassen sich getrennt verschieben, der runde Griff ändert den Zoom.", "Magnifier added – lens and source can be moved separately, the round handle changes the zoom."),
            "spotlight" => Loc.L("Spotlight eingefügt – die Strichstärke bestimmt, wie stark der Rest abgedunkelt wird.", "Spotlight added – the stroke width sets how much the rest is dimmed."),
            _ => _status.Text
        };
    }

    JsonObject? AnnotationEdit(JsonObject edits, string id) => (edits.Arr("annotations") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(x => x.Str("id") == id);

    void MoveAnnotation(string id, double dx, double dy, int part = 1) => Change(e =>
    {
        if (AnnotationEdit(e, id) is not { } a) return;
        a["geom"] = AnnotationGeometry.Moved(a, (float)dx, (float)dy, part);
    }, refill: false);

    void RecolorSelected(string hex)
    {
        if (_selAnn is not { } id) return;
        Change(e => { if (AnnotationEdit(e, id) is { } a) { var st = a.Obj("style") ?? new JsonObject(); st["color"] = hex; a["style"] = st; } }, refill: false);
    }

    void RestrokeSelected(int width, int fontSize)
    {
        if (_selAnn is not { } id) return;
        Change(e =>
        {
            if (AnnotationEdit(e, id) is not { } a) return;
            var st = a.Obj("style") ?? new JsonObject();
            st["width"] = width; st["fontSize"] = fontSize;
            if (a.Str("type") == "spotlight") st["dim"] = WidthToDim(width);
            a["style"] = st;
        }, refill: false);
    }

    // ---------------- Zuschneiden ----------------

    void SetCrop(int x, int y, int w, int h)
    {
        var s = Sel; if (s == null) return;
        if (w < 32 || h < 32) { _status.Text = Loc.L("Der Ausschnitt ist zu klein (mindestens 32 × 32 Pixel).", "The selection is too small (at least 32 × 32 pixels)."); return; }
        Change(e => StepEdit(e, s.Anchor)["crop"] = new JsonArray(x, y, w, h), refill: false);
        SelectTool("select");
        _zoom.FitNow();
        _status.Text = Loc.F("Schritt {0} zugeschnitten ({1} × {2}) – rückgängig mit Strg+Z oder „Zuschnitt entfernen“.", "Step {0} cropped ({1} × {2}) – undo with Ctrl+Z or “Remove crop”.", s.Number, w, h);
    }

    void RemoveCrop(ViewStep s) => Change(e => StepEdit(e, s.Anchor).Remove("crop"), refill: false);

    /// <summary>Überträgt den Zuschnitt des gewählten Schritts auf alle Schritte mit Bildern desselben Monitors.</summary>
    void CropToAll()
    {
        var s = Sel; if (s == null || _view.CropFor(s) is not { } c) return;
        var m = s.Monitor ?? s.Json.Obj("position")?.Str("m");
        var targets = _view.All.Where(x => x.PreFrame != null && (m == null || (x.Monitor ?? x.Json.Obj("position")?.Str("m")) == m || x.Monitor == null)).ToList();
        _view.SnapshotBefore = "cropAll";
        Change(e => { foreach (var t in targets) StepEdit(e, t.Anchor)["crop"] = new JsonArray(c.X, c.Y, c.Width, c.Height); });
        _status.Text = Loc.N(targets.Count, "Zuschnitt auf 1 Schritt übertragen – rückgängig mit Strg+Z.", "Zuschnitt auf {0} Schritte übertragen – rückgängig mit Strg+Z.", "Crop applied to 1 step – undo with Ctrl+Z.", "Crop applied to {0} steps – undo with Ctrl+Z.");
    }

    // ---------------- Großansicht ----------------

    /// <summary>Bild eines Listeneintrags in voller Auflösung mit allen Markierungen.</summary>
    (BitmapSource img, string caption)? LightboxItem(int i)
    {
        if (i < 0 || i >= _list.Items.Count || ((ListBoxItem)_list.Items[i]).Tag is not ViewStep s) return null;
        string variant = i == _list.SelectedIndex && _variantOverride != null ? _variantOverride : DefaultVariant(s);
        using var img = StepImages.Render(_view, s, _display == "crop" ? "crop" : variant, "original", annotate: true, viewMode: _display == "window" ? "window" : "screen", authorAnnotations: true, stepLabel: StepLabel(s))
            ?? StepImages.Render(_view, s, "before", "original", authorAnnotations: true);
        if (img == null) return null;
        return (U.ToSource(img.Bitmap), Loc.F("Schritt {0} von {1}  ·  {2}", "Step {0} of {1}  ·  {2}", s.Number, _view.All.Count, s.AuthorTitle ?? s.Title));
    }

    void OpenLightbox()
    {
        if (_list.SelectedIndex < 0) return;
        var lb = new ImageLightbox(LightboxItem, _list.SelectedIndex, _list.Items.Count) { Owner = this };
        lb.IndexChanged += i => _list.SelectedIndex = i;
        lb.ShowDialog();
    }

    void EditAnnotationText(JsonObject a)
    {
        var id = a.Str("id"); if (id == null) return;
        var d = new InputDialog(Loc.L("Text der Zeichnung", "Text of the drawing"), a.Str("text") ?? "") { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        Change(e => { if (AnnotationEdit(e, id) is { } x) x["text"] = d.Value; }, refill: false);
    }

    /// <summary>Nur für Entwickler-Schnappschüsse: Reiter „Technik“ und eigene Markierungen mit Auswahl vorführen.</summary>
    internal void DevDemo(Action save)
    {
        void Flush() { for (int i = 0; i < 6; i++) Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { }); }
        if (_list.Items.Count < 2) return;
        _list.SelectedIndex = 1;
        // formatierter Anleitungstext (Paket 21)
        if (Sel is { } rs)
        {
            // dazu Querverweis und Tabelle (Paket 23)
            string back = _view.GuideSteps().FirstOrDefault(x => x.Kind != "move") is { } f0 ? "[[ref:s" + f0.Anchor + "]]" : "";
            SaveText(rs.Anchor, rs.AuthorTitle ?? rs.Title, Loc.L("Geben Sie **notiz.txt** ein und bestätigen Sie mit [[Eingabe]] (wie in " + back + ").\n- Dateityp: `Textdateien`\n- Ordner: [[ui:Dokumente]]\n\n| Taste | Wirkung |\n|---|---|\n| [[Strg+S]] | Speichern |\n| [[Esc]] | Abbrechen |",
                "Type **notiz.txt** and confirm with [[Enter]] (as in " + back + ").\n- File type: `Text files`\n- Folder: [[ui:Documents]]\n\n| Key | Effect |\n|---|---|\n| [[Ctrl+S]] | Save |\n| [[Esc]] | Cancel |"), true);
            Flush();
        }
        _tabBtns[1].IsChecked = true; Flush(); save();
        _tabBtns[0].IsChecked = true;
        if (_currentMeta != null)
        {
            var r = _currentMeta.SourceRect;
            if (!AnnotationsFor(_currentMeta.FrameId).Any())
            {
                AddAnnotation("rect", new JsonArray(r.X + r.Width / 5, r.Y + r.Height / 3, r.Width / 3, r.Height / 5), null);
                _color = "#4361EE"; AddAnnotation("badge", new JsonArray(r.X + r.Width / 5, r.Y + r.Height / 3), "1");
                _color = "#E5484D"; AddAnnotation("arrow", new JsonArray(r.X + r.Width * 3 / 4, r.Y + r.Height * 3 / 4, r.X + r.Width * 3 / 5, r.Y + r.Height * 11 / 20), null);
            }
            _selAnn = _currentMeta == null ? null : AnnotationsFor(_currentMeta.FrameId).FirstOrDefault(a => a.Str("type") == "rect")?.Str("id");
            DrawSelection();
        }
        Flush(); save();
        // Zoom 160 % und Zuschnitt vorführen
        _zoom.SetZoom(1.6); Flush(); save();
        if (_currentMeta != null && Sel is { } cs)
        {
            var r = _currentMeta.SourceRect;
            SetCrop(r.X + r.Width / 8, r.Y + r.Height / 8, r.Width / 2, r.Height / 2); Flush(); save();
            RemoveCrop(cs); Flush();
        }
        _zoom.FitNow();
        // Markenstil „Nummern“: Schrittnummer wie in der Anleitung an der Klickstelle (Paket 22)
        int keepIndex = _list.SelectedIndex;
        _list.SelectedIndex = 0; Flush();
        ApplyMarkPreset("numbers"); Flush(); save();
        ApplyMarkPreset("classic"); _list.SelectedIndex = keepIndex; Flush();
        // Bedienungsanleitung: Kapitel und Hinweiskasten am Schritt, dann Reiter „Dokument“
        if (Sel is { } ds)
        {
            if (ds.ChapterTitle == null) ToggleChapter(ds.Anchor, true);
            if (Sel is { } ds2 && ds2.Comments.Count == 0)
                Change(e => { var arr = e["comments"] as JsonArray ?? new JsonArray(); arr.Add(new JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = ds2.Anchor, ["kind"] = "tip", ["text"] = _view.Language == "en" ? "Ctrl+1 is faster." : "Mit Strg+1 geht es schneller.", ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" }); e["comments"] = arr; }, refill: false);
            Flush();
            if (_details.Parent is ScrollViewer sv) { sv.ScrollToEnd(); Flush(); save(); sv.ScrollToHome(); }
        }
        ShowCover(); Flush(); DevWaitPreview(); save();
        if (_details.Parent is ScrollViewer sv2) { sv2.ScrollToEnd(); Flush(); save(); sv2.ScrollToHome(); }
        _list.SelectedIndex = 1; Flush();
        // Seitenansicht mit gewähltem Schritt
        SetPageMode(true); DevWaitPreview(); save(); SetPageMode(false); Flush();
        // eingefügter Textschritt, Mehrfachauswahl, Suchen und Ersetzen
        _list.SelectedIndex = 1; Flush();
        InsertManual(null); Flush(); save();
        var added = _view.Manual.LastOrDefault();
        _list.SelectedItems.Clear();
        foreach (var it in _list.Items.OfType<ListBoxItem>().Take(3)) it.IsSelected = true;
        Flush(); save();
        if (added != null) DeleteManual(new List<ViewStep> { added });
        _list.SelectedItems.Clear(); _list.SelectedIndex = 1; Flush();
        ShowSearchReplace();
        OpenLightbox(); // wird vom Schnappschuss-Modus abgebildet und geschlossen
    }

    void DeleteSelectedOrAll()
    {
        if (_selAnn is { } id)
        {
            _selAnn = null;
            Change(e => { if (e["annotations"] is JsonArray arr && AnnotationEdit(e, id) is { } a) { arr.Remove(a); if (a.Str("type") == "badge") Annotations.Renumber(e, a.Str("frame") ?? ""); } }, refill: false);
            _status.Text = Loc.L("Zeichnung gelöscht – rückgängig mit Strg+Z", "Drawing deleted – undo with Ctrl+Z");
            return;
        }
        if (_currentMeta == null || !AnnotationsFor(_currentMeta.FrameId).Any()) { _status.Text = Loc.L("Dieses Bild hat keine Zeichnungen.", "This image has no drawings."); return; }
        if (Msg.Show(Loc.L("Alle Zeichnungen dieses Bildes löschen?", "Delete all drawings in this image?"), Loc.L("Zeichnungen", "Drawings"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) ClearAnnotations();
    }
}
