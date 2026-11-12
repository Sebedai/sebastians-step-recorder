using System.Text.Json.Nodes;
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
    static readonly (string hex, string name)[] Palette = { ("#E5484D", "Rot"), ("#F5A524", "Orange"), ("#1F9D55", "Grün"), ("#4361EE", "Blau"), ("#1B1C21", "Schwarz"), ("#FFFFFF", "Weiß") };
    static readonly string[] ImportanceOrder = { "primary", "secondary", "trace" };
    static readonly (int width, int fontSize, string name)[] StrokeSizes = { (2, 14, "dünn"), (4, 18, "mittel"), (7, 26, "dick") };
    readonly List<RadioButton> _strokeBtns = new();
    int _stroke = 4, _fontSize = 18;
    ZoomPanel _zoom = null!;
    Button _cropOff = null!;
    int _handle = -1;               // gezogener Anfasser der Auswahl (-1 = keiner)
    JsonArray? _handleGeom;
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
    readonly TextBlock _stepPos = new() { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), MinWidth = 54, TextAlignment = TextAlignment.Center };
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
    TextBox? _editTitle, _editBody;
    string _editOrigTitle = "", _editOrigBody = "";

    void BuildLayout()
    {
        // ---------------- Kopfzeile ----------------
        _titleBlock.Text = _view.Title;
        _titleBlock.MouseLeftButtonUp += (_, _) => Rename();
        var logo = Theme.Logo(28); logo.Margin = new Thickness(0, 0, 12, 0);
        _titleBlock.FontFamily = Theme.Display; _metaBlock.FontFamily = Theme.Mono; _metaBlock.FontSize = 11; _stepPos.FontFamily = Theme.Mono; _listCount.FontFamily = Theme.Mono; _listCount.FontSize = 11; logo.VerticalAlignment = VerticalAlignment.Center;
        var titleCol = U.Col(_titleBlock, _metaBlock); titleCol.VerticalAlignment = VerticalAlignment.Center;
        var brand = new DockPanel { Margin = new Thickness(0, 0, 16, 0) };
        DockPanel.SetDock(logo, Dock.Left); brand.Children.Add(logo); brand.Children.Add(titleCol);

        var cmds = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Border Sep() => new() { Width = 1, Height = 22, Background = Theme.Line, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        _undoBtn = U.Ghost(Theme.IUndo, null, Undo, "Rückgängig (Strg+Z)");
        _redoBtn = U.Ghost(Theme.IRedo, null, Redo, "Wiederholen (Strg+Y)");
        _undoBtn.Margin = _redoBtn.Margin = new Thickness(1, 0, 1, 0);
        _playIcon.Text = Theme.IPlay; _playIcon.FontFamily = Theme.Icons; _playIcon.FontSize = 14; _playIcon.VerticalAlignment = VerticalAlignment.Center;
        var play = new Button { Style = Theme.Style("Ghost"), Content = U.Row(_playIcon, new TextBlock { Text = "Abspielen", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }), ToolTip = "Schritte nacheinander zeigen (F5)", Margin = new Thickness(2, 0, 2, 0) };
        play.Click += (_, _) => TogglePlay();
        var copy = MenuButton(Theme.ICopy, "Kopieren", "Bild oder Text dieses Schritts in die Zwischenablage", null, () => new (string, Action)[]
            { M("copyImage", Loc.L("Bild", "Image")), M("copyImageText", Loc.L("Bild mit Text", "Image with text")), M("copyText"), M("copyMarkdown"), M("copyOriginal", Loc.L("Originalbild", "Original image")), ("-", () => { }), (Loc.L("Ganze Anleitung", "Whole guide"), CopyWholeGuide), M("saveImage") });
        var privacy = MenuButton(Theme.IShield, "Schwärzen", "Persönliche Daten aus Bildern und Texten entfernen", null, () => new (string, Action)[]
            { (Loc.L("Persönliche Daten in Bildern suchen …", "Find personal data in images …"), ScanImagesForPersonalData), M("toolRedact", Loc.L("Bereich im Bild schwärzen", "Redact area in image")), M("redactStep", Loc.L("Diesen Schritt schwärzen", "Redact this step")), (Loc.L("Text überall schwärzen …", "Redact text everywhere …"), RedactText), M("generalize"), ("-", () => { }), (Loc.L("Endgültig schwärzen …", "Redact permanently …"), FinalRedact) });
        var review = new Button { Style = Theme.Style("Ghost"), ToolTip = "Auffällige Eingaben durchsehen, bevor die Aufnahme weitergegeben wird (Strg+Umschalt+R)", Margin = new Thickness(2, 0, 2, 0) };
        var reviewContent = (StackPanel)U.IconText(Theme.IChecklist, "Durchsicht");
        _reviewBadge = new Border { Background = Theme.WarnSoft, CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(8, 0, 0, 0), Child = _reviewCount, VerticalAlignment = VerticalAlignment.Center };
        reviewContent.Children.Add(_reviewBadge);
        review.Content = reviewContent;
        review.Click += (_, _) => { try { ShowReview(); } catch (Exception ex) { UiApp.ShowError(ex); } };
        var preview = new Button { Style = Theme.Style("Ghost"), Content = U.IconText(Theme.IEye, "Vorschau"), ToolTip = "Anleitung als HTML im Browser ansehen", Margin = new Thickness(2, 0, 2, 0) };
        preview.Click += (_, _) => PreviewDocument();
        var more = MenuButton(Theme.IMore, "", "Weitere Funktionen", null, () => new (string, Action)[]
            { M("newRecording"), M("library"), (Loc.L("Als .steprec packen …", "Pack as .steprec …"), () => { if (_view.Session.ReadOnly) Msg.Show(Loc.L("Das ist bereits ein .steprec-Archiv.", "This already is a .steprec archive.")); else PackDialog.Show(this, _path); }), ("-", () => { }), M("replace"), (Loc.L("Übersicht und Vollständigkeit", "Overview and completeness"), ShowOverview), M("compare"), ("-", () => { }), (Loc.L("Mit KI verbinden …", "Connect to AI …"), () => AiConnect.Show(this)), M("shortcuts") });
        var export = MenuButton(Theme.IExport, Loc.L("Exportieren", "Export"), Loc.F("Als Anleitung, KI-Paket oder Skript speichern ({0}) · letzten Export wiederholen ({1})", "Save as guide, AI package or script ({0}) · repeat last export ({1})", Cmd("export").Gesture, Cmd("repeatExport").Gesture), "Primary", ExportMenuItems);
        export.Margin = new Thickness(8, 0, 0, 0);
        foreach (var e in new UIElement[] { _undoBtn, _redoBtn, Sep(), play, preview, copy, privacy, review, Sep(), more, export }) cmds.Children.Add(e);
        void Lbl(Button b, int level) { if (b.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock t) _labels.Add((t, level)); }
        Lbl(play, 1); Lbl(preview, 1); Lbl(copy, 2); Lbl(privacy, 2); Lbl(review, 2);
        var bar = new DockPanel();
        DockPanel.SetDock(cmds, Dock.Right); bar.Children.Add(cmds); bar.Children.Add(brand);
        var top = new Border { Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(16, 10, 14, 10), Child = bar };

        // ---------------- Schrittliste ----------------
        var searchHost = new Grid { Margin = new Thickness(14, 14, 14, 10) };
        searchHost.Children.Add(_search);
        var searchIcon = U.Icon(Theme.ISearch, 13, Theme.Muted); searchIcon.HorizontalAlignment = HorizontalAlignment.Left; searchIcon.Margin = new Thickness(12, 0, 0, 0); searchIcon.IsHitTestVisible = false;
        searchHost.Children.Add(searchIcon);
        _search.TextChanged += (_, _) => Fill();
        var (impTrack, imp) = U.Segmented("imp", 0, (null, "Hauptschritte"), (null, "Alle"));
        impTrack.ToolTip = "Hauptschritte: Nebenschritte und Hauptschritte. Alle: zusätzlich die Spur (Mausbewegungen, Fensterwechsel …).";
        _allToggle = imp[1];
        foreach (var rb in imp) rb.Checked += (_, _) => { if (IsLoaded) Fill(); };
        var filterRow = new DockPanel { Margin = new Thickness(14, 0, 14, 8) };
        DockPanel.SetDock(_listCount, Dock.Right); filterRow.Children.Add(_listCount); filterRow.Children.Add(impTrack);
        _list.SelectionMode = SelectionMode.Extended;
        _list.SelectionChanged += (_, _) => { _selAnn = null; _variantOverride = null; ShowStep(); };
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
        var hintText = U.Text("Ziehen: umsortieren · Strg+Klick: mehrere", 11.5, fg: Theme.Muted); hintText.VerticalAlignment = VerticalAlignment.Center;
        var insertBtn = U.Btn(Theme.IAdd, "Einfügen", () => { }, "Text- oder Bildschritt nach dem ausgewählten Schritt einfügen (Umschalt+Einfg)", "Ghost");
        insertBtn.Click += (_, _) => InsertMenu(insertBtn); insertBtn.Margin = new Thickness(6, 0, 0, 0);
        var listHint = new DockPanel { Margin = new Thickness(16, 4, 8, 8) };
        DockPanel.SetDock(insertBtn, Dock.Right); listHint.Children.Add(insertBtn); listHint.Children.Add(hintText);
        var left = new DockPanel { Background = Theme.Surface };
        DockPanel.SetDock(searchHost, Dock.Top); DockPanel.SetDock(filterRow, Dock.Top); DockPanel.SetDock(listHint, Dock.Bottom);
        left.Children.Add(searchHost); left.Children.Add(filterRow); left.Children.Add(listHint); left.Children.Add(_list);

        // ---------------- Ansicht (über dem Bild) ----------------
        var (baTrack, ba) = U.Segmented("ba", 0, (null, "Vorher"), (null, "Nachher"));
        _beforeBtn = ba[0]; _afterBtn = ba[1];
        foreach (var rb in ba) rb.Checked += (_, _) => { if (_syncBA) return; _variantOverride = _afterBtn.IsChecked == true ? "after" : "before"; if (IsLoaded) ShowStep(); };
        baTrack.ToolTip = "Bild vor oder nach der Aktion (W)";
        var disp = _dispBox = new ComboBox { Items = { Loc.L("Ganzer Bildschirm", "Full screen"), Loc.L("Anwendungsfenster", "Application window"), Loc.L("Ausschnitt", "Detail") }, SelectedIndex = 0, MinWidth = 170, ToolTip = Loc.L("Bildausschnitt", "Image area") };
        disp.SelectionChanged += (_, _) => { _display = disp.SelectedIndex switch { 1 => "window", 2 => "crop", _ => "screen" }; ShowStep(); };
        var marksContent = (StackPanel)U.IconText(Theme.IMouse, "Markierungen");
        marksContent.Children.Add(new TextBlock { Text = Theme.IDown, FontFamily = Theme.Icons, FontSize = 9, Margin = new Thickness(8, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 });
        var marksBtn = new Button { Style = Theme.Style("Ghost"), Content = marksContent, ToolTip = "Klickpunkt, Zugweg, Zielrahmen, Hinweise sowie gedrückte Tasten und getippten Text im Bild ein-/ausblenden (H: nur dieser Schritt)" };
        marksBtn.Click += (_, _) => ShowMarksMenu(marksBtn);
        if (marksContent.Children[1] is TextBlock ml) _labels.Add((ml, 2));
        var (langTrack, langs) = U.Segmented("tlang", _view.Language == "en" ? 1 : 0, (null, "DE"), (null, "EN"));
        langTrack.ToolTip = "Sprache der Texte: Überschriften, Anleitungstexte, Kapitel und Dokumenttexte gibt es je Sprache – so entstehen aus einer Aufnahme eine deutsche und eine englische Anleitung.";
        langs[0].Checked += (_, _) => { if (IsLoaded) SetTextLanguage("de"); }; langs[1].Checked += (_, _) => { if (IsLoaded) SetTextLanguage("en"); };
        var prev = U.Ghost(Theme.IPrev, null, () => { if (_list.SelectedIndex > 0) _list.SelectedIndex--; }, "Vorheriger Schritt (↑)");
        var next = U.Ghost(Theme.INext, null, () => { if (_list.SelectedIndex < _list.Items.Count - 1) _list.SelectedIndex++; }, "Nächster Schritt (↓)");
        _stepPos.Foreground = Theme.Muted;
        var nav = U.Row(prev, _stepPos, next);
        var viewLeft = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var c in new FrameworkElement[] { baTrack, disp, marksBtn, langTrack }) { c.VerticalAlignment = VerticalAlignment.Center; c.Margin = new Thickness(viewLeft.Children.Count == 0 ? 0 : 10, 0, 0, 0); viewLeft.Children.Add(c); }
        var viewRow = new DockPanel { Margin = new Thickness(16, 9, 12, 9) };
        DockPanel.SetDock(nav, Dock.Right); viewRow.Children.Add(nav); viewRow.Children.Add(viewLeft);

        // ---------------- Werkzeugpalette (links am Bild, zweispaltig) ----------------
        var pal = new StackPanel();
        var toolGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        pal.Children.Add(toolGrid);
        void Tool(string id, UIElement content, string tip)
        {
            var b = new RadioButton { Content = content, GroupName = "tool", IsChecked = id == "select", ToolTip = tip, Style = Theme.Style("ToolToggle"), Margin = new Thickness(1), MinHeight = 34 };
            b.Checked += (_, _) =>
            {
                bool cropChanged = (_tool == "crop") != (id == "crop");
                _tool = id;
                _overlay.Cursor = id == "select" ? Cursors.Arrow : Cursors.Cross;
                if (id != "select" && _selAnn != null) { _selAnn = null; DrawSelection(); }
                if (cropChanged && IsLoaded) ShowStep();
                if (id == "crop") _status.Text = "Zuschneiden: Rechteck aufziehen – gilt für Vorher- und Nachher-Bild dieses Schritts, auch im Export.";
            };
            _toolBtns[id] = b; toolGrid.Children.Add(b);
        }
        TextBlock G(string glyph) => U.Icon(glyph, 15);
        FrameworkElement Bound(FrameworkElement e, DependencyProperty p) { e.SetBinding(p, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(RadioButton), 1) }); return e; }
        var badgeIcon = Bound(new Border { Width = 17, Height = 17, CornerRadius = new CornerRadius(8.5), BorderThickness = new Thickness(1.5), Child = new TextBlock { Text = "1", FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }, Border.BorderBrushProperty);
        var ellipseIcon = Bound(new Ellipse { Width = 17, Height = 12, StrokeThickness = 1.6 }, Shape.StrokeProperty);
        Tool("select", G(Theme.IPointer), "Auswählen (V) – Markierung anklicken, ziehen zum Verschieben, Entf löscht, Doppelklick ändert Text");
        Tool("arrow", G(Theme.IArrow), "Pfeil (A)");
        Tool("rect", G(Theme.IRect), "Rahmen (R)");
        Tool("ellipse", ellipseIcon, "Ellipse (E)");
        Tool("highlight", G(Theme.IHighlight), "Hervorheben (M)");
        Tool("text", G(Theme.IText), "Text (T)");
        Tool("badge", badgeIcon, "Nummer (N)");
        Tool("redact", G(Theme.IRedact), "Schwärzen (X) – gilt ab diesem Bild für alle folgenden, auch im Export und KI-Paket");
        Tool("pixelate", G(Theme.IPixelate), "Verpixeln (P) – wie Schwärzen, aber unauffälliger; gilt ab diesem Bild für alle folgenden");
        Tool("crop", G(Theme.ICrop), "Zuschneiden (C) – Rechteck aufziehen; gilt für die Bilder dieses Schritts, auch im Export");
        Border PalSep() => new() { Height = 1, Background = Theme.Line, Margin = new Thickness(6, 5, 6, 5) };
        pal.Children.Add(PalSep());
        var colorGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var (hex, name) in Palette)
        {
            var dot = new Ellipse { Width = 14, Height = 14, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), Stroke = Theme.LineStrong, StrokeThickness = 1 };
            var rb = new RadioButton { Content = dot, GroupName = "color", IsChecked = hex == _color, ToolTip = "Farbe: " + name, Style = Theme.Style("ToolToggle"), Margin = new Thickness(1), MinHeight = 26, MinWidth = 0, Padding = new Thickness(3), Tag = hex };
            rb.Checked += (_, _) => { _color = hex; if (!_syncingColor) RecolorSelected(hex); };
            _colorBtns.Add(rb); colorGrid.Children.Add(rb);
        }
        pal.Children.Add(colorGrid);
        pal.Children.Add(PalSep());
        var widthGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var (w, fs, name) in StrokeSizes)
        {
            var line = new Rectangle { Width = 14, Height = Math.Max(1.5, w / 1.6), RadiusX = 1, RadiusY = 1 };
            line.SetBinding(Shape.FillProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(RadioButton), 1) });
            var rb = new RadioButton { Content = line, GroupName = "stroke", IsChecked = w == _stroke, ToolTip = "Strichstärke und Textgröße: " + name, Style = Theme.Style("ToolToggle"), Margin = new Thickness(1), MinHeight = 26, MinWidth = 0, Padding = new Thickness(3), Tag = w };
            rb.Checked += (_, _) => { _stroke = w; _fontSize = fs; if (!_syncingColor) RestrokeSelected(w, fs); };
            _strokeBtns.Add(rb); widthGrid.Children.Add(rb);
        }
        pal.Children.Add(widthGrid);
        pal.Children.Add(PalSep());
        var del = U.Ghost(Theme.IDelete, null, DeleteSelectedOrAll, "Ausgewählte Markierung löschen (Entf) – ohne Auswahl: alle Markierungen dieses Bildes");
        del.Margin = new Thickness(0); del.MinWidth = 34; del.HorizontalAlignment = HorizontalAlignment.Stretch;
        pal.Children.Add(del);
        var palette = new Border
        {
            Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(4), Child = pal, Width = 86,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 8, 0, 8), Effect = Theme.Shadow(12, 1)
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
        _zoom = new ZoomPanel(stage, () => new Size(_imgHost.Width is double w && !double.IsNaN(w) ? w : 0, _imgHost.Height is double h && !double.IsNaN(h) ? h : 0), new Thickness(112, 48, 28, 58));
        _zoom.ZoomChanged += () => { if (_selAnn != null) DrawSelection(); };
        var imgArea = new Grid { Background = Theme.CanvasBg, ClipToBounds = true };
        imgArea.Children.Add(_zoom);
        imgArea.Children.Add(_noImage);
        imgArea.Children.Add(_imgCaptionPill);
        imgArea.Children.Add(palette);
        // Zoomleiste und Großansicht (unten rechts)
        _cropOff = U.Btn(Theme.ICrop, "Zuschnitt entfernen", () => { if (Sel is { } cs) RemoveCrop(cs); }, "Ganzes Bild wieder zeigen", "Ghost");
        _cropOff.Margin = new Thickness(0, 0, 6, 0); _cropOff.Visibility = Visibility.Collapsed;
        var full = U.Ghost(Theme.IFullScreen, null, OpenLightbox, "Großansicht (F11 oder Doppelklick aufs Bild)"); full.Margin = new Thickness(4, 0, 0, 0);
        var zoomBar = new Border
        {
            Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(4, 2, 4, 2), Effect = Theme.Shadow(12, 1),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 14, 10),
            Child = U.Row(_cropOff, _zoom.Toolbar(), full)
        };
        imgArea.Children.Add(zoomBar);
        var center = new DockPanel { Background = Theme.Bg };
        var viewBorder = new Border { Background = Theme.Bg, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = viewRow };
        DockPanel.SetDock(viewBorder, Dock.Top);
        center.Children.Add(viewBorder); center.Children.Add(imgArea);

        // ---------------- Details ----------------
        var (tabTrack, tabs) = U.Segmented("dtab", 0, (Theme.IEdit, "Schritt"), (Theme.ICode, "Technik"), (Theme.IDoc, "Dokument"));
        _tabBtns = tabs;
        for (int i = 0; i < tabs.Length; i++) { int ti = i; tabs[i].Checked += (_, _) => { _detailTab = ti; if (ti == 2) DocDetails(); else if (Sel is { } s) ShowDetails(s); }; }
        tabs[2].ToolTip = "Bedienungsanleitung einrichten: Titelseite, Einleitung, Voraussetzungen, Inhaltsverzeichnis, Bilder, Farbe, Fußzeile";
        tabTrack.Margin = new Thickness(22, 14, 22, 0);
        tabs[1].ToolTip = "Fenster, Ziel, Belege, AutoHotkey-Angaben und das Protokoll für KI";
        var detPanel = new DockPanel { Background = Theme.Surface };
        DockPanel.SetDock(tabTrack, Dock.Top); detPanel.Children.Add(tabTrack);
        detPanel.Children.Add(new ScrollViewer { Content = _details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Surface });

        var grid = new Grid();
        _leftCol = new ColumnDefinition { Width = new GridLength(320), MinWidth = 230 };
        _rightCol = new ColumnDefinition { Width = new GridLength(370), MinWidth = 260 };
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
        var statusBar = new Border { Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 6, 16, 7), Child = _status };

        var root = new DockPanel();
        Anim.Appear(top, 0, -6, 360); Anim.Appear(left, 60, 10); Anim.Appear(center, 130, 10); Anim.Appear(detPanel, 200, 10);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        if (_view.Session.ReadOnly)
        {
            // .steprec: nur lesen – Entpacken macht daraus eine bearbeitbare Aufnahme im Aufnahmeordner
            var unpack = U.Btn(Theme.IOpen, Loc.L("Entpacken …", "Unpack …"), async () => { if (await SessionImport.Unpack(this, _path) != null) Close(); }, null, "Ghost");
            var bannerText = U.Text(Loc.L("Schreibgeschützt (.steprec) – zum Bearbeiten bitte entpacken.", "Read-only (.steprec) – unpack to edit."), 12.5, fg: Theme.Warn);
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

    void UpdateUndo()
    {
        if (_undoBtn == null) return;
        _undoBtn.IsEnabled = _undo.Count > 0; _redoBtn.IsEnabled = _redo.Count > 0;
        _undoBtn.Opacity = _undoBtn.IsEnabled ? 1 : 0.45; _redoBtn.Opacity = _redoBtn.IsEnabled ? 1 : 0.45;
    }

    void SelectTool(string id) { if (_toolBtns.TryGetValue(id, out var b)) b.IsChecked = true; }

    static ContextMenu Menu(IEnumerable<(string label, Action action)> items)
    {
        var m = new ContextMenu();
        foreach (var (label, action) in items)
        {
            if (label == "-") { m.Items.Add(new Separator()); continue; }
            var (header, gesture) = SplitLabel(label);
            var mi = new MenuItem { Header = header, InputGestureText = gesture };
            mi.Click += (_, _) => { try { action(); } catch (Exception ex) { UiApp.ShowError(ex); } };
            m.Items.Add(mi);
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
        // Reihenfolge der Anleitung (umsortierte Schritte stehen dort, wo sie im Export erscheinen); ausgeblendete bleiben sichtbar
        foreach (var s in _view.Effective(guide: false, importance: _allToggle.IsChecked == true ? "all" : "secondary", includeManual: true, ordered: true))
        {
            if (!StepSearch.Matches(s, query)) continue;
            n++;
            bool primary = s.Importance == "primary", gap = s.Kind == "gap", dim = s.Hidden || s.Discarded;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            FrameworkElement lead;
            var numBadge = new TextBlock
            {
                Text = s.Manual ? " +" : s.Number.ToString("00"), FontFamily = Theme.Mono, FontSize = 11, FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = primary ? Theme.AccentText : Theme.Muted, Width = 22, Margin = new Thickness(0, 1, 6, 0), VerticalAlignment = VerticalAlignment.Top
            };
            if (gap) lead = U.Row(numBadge, U.IconBadge(Theme.IWarn, 30, Theme.WarnSoft, Theme.Warn, 13));
            else
            {
                var thumbImg = new Image { Stretch = Stretch.UniformToFill, Width = 76, Height = 48, Clip = new RectangleGeometry(new Rect(0, 0, 76, 48), 2, 2) };
                RenderOptions.SetBitmapScalingMode(thumbImg, BitmapScalingMode.HighQuality);
                var thumbBox = new Border { Width = 78, Height = 50, CornerRadius = new CornerRadius(3), Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Child = thumbImg };
                var placeholder = U.Icon(KindIcon(s), 15, Theme.Muted); placeholder.IsHitTestVisible = false;
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
            var head = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal, Foreground = dim ? Theme.Muted : s.Importance == "trace" ? Theme.Fg2 : Theme.Fg, FontStyle = gap ? FontStyles.Italic : FontStyles.Normal, MaxHeight = 40, TextTrimming = TextTrimming.CharacterEllipsis };
            head.Inlines.Add(new System.Windows.Documents.Run(s.AuthorTitle ?? s.Title));
            var meta = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            meta.Children.Add(U.Row(U.Icon(KindIcon(s), 11, Theme.Muted), U.Text(KindLabel(s.Kind), 11.5, fg: Theme.Muted, margin: new Thickness(5, 0, 6, 2)), new TextBlock { Text = ShortTime(Fmt.Time(s.TFrom, "de")), FontFamily = Theme.Mono, FontSize = 10.5, Foreground = Theme.Muted, Margin = new Thickness(0, 1, 8, 2), VerticalAlignment = VerticalAlignment.Top }));
            if (s.Unreviewed) meta.Children.Add(U.Chip("ungeprüft", Theme.WarnSoft, Theme.Warn, Theme.IWarn));
            if (s.EvidenceLevel == "inputOnly" && s.Kind is not ("gap" or "system")) meta.Children.Add(U.Chip("nur Eingabe", Theme.Surface2, Theme.Muted));
            if (s.Hidden) meta.Children.Add(U.Chip("ausgeblendet", Theme.Surface2, Theme.Muted, Theme.IEye));
            if (s.Discarded) meta.Children.Add(U.Chip("verworfen", Theme.DangerSoft, Theme.Danger));
            if (s.Comments.Count > 0) meta.Children.Add(U.Chip(s.Comments.Count.ToString(), Theme.AccentSoft, Theme.AccentText, Theme.IComment));
            if (s.AuthorTitle != null || s.AuthorBody != null) meta.Children.Add(U.Chip("bearbeitet", Theme.Surface2, Theme.Muted, Theme.IEdit));
            var col = U.Col(head, meta); col.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(col, 1); g.Children.Add(col);
            if (s.ChapterTitle != null)
            {
                var chap = new TextBlock { Text = "§  " + s.ChapterTitle, FontFamily = Theme.Mono, FontSize = 10.5, Foreground = Theme.AccentText, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 3), ToolTip = "Kapitelbeginn" };
                col.Children.Insert(0, chap);
            }
            _list.Items.Add(new ListBoxItem { Content = g, Tag = s, ToolTip = s.AuthorTitle ?? s.Title });
        }
        var sel = _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => ((ViewStep)i.Tag).Id == keepId) ?? _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => ((ViewStep)i.Tag).Importance == "primary");
        if (sel != null) { _list.SelectedItem = sel; _list.ScrollIntoView(sel); }
        _listCount.Text = query.IsEmpty ? Loc.N(n, "1 Schritt", "{0} Schritte", "1 step", "{0} steps") : Loc.N(n, "1 Treffer", "{0} Treffer", "1 match", "{0} matches");
        UpdateStatus();
        PumpThumbs();
    }

    /// <summary>Listeneintrag unter einem Ereignisursprung (auch für Text-Runs, die keine Visuals sind).</summary>
    static ListBoxItem? ItemAt(object src)
    {
        var d = src as DependencyObject;
        while (d != null && d is not ListBoxItem)
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as ListBoxItem;
    }

    void StepMenu()
    {
        var s = Sel; if (s == null) return;
        if (_list.SelectedItems.Count > 1)
        {
            var multi = SelSteps;
            var mm = Menu(new (string, Action)[]
            {
                ($"{multi.Count} Schritte ausblenden", () => BulkEdit(multi, se => se["hidden"] = true)), ($"{multi.Count} Schritte einblenden", () => BulkEdit(multi, se => se["hidden"] = false)), ("-", () => { }),
                ("Als Hauptschritte", () => BulkEdit(multi.Where(x => !x.Manual), se => se["importance"] = "primary")), ("Als Nebenschritte", () => BulkEdit(multi.Where(x => !x.Manual), se => se["importance"] = "secondary")), ("-", () => { }),
                ("Kapitel vor dem ersten beginnen", () => ToggleChapter(multi[0].Anchor, true)), ("Bilder weglassen", () => BulkEdit(multi, se => se["showImage"] = false)), ("Bilder zeigen", () => BulkEdit(multi, se => se.Remove("showImage"))),
            });
            mm.Placement = PlacementMode.MousePoint; mm.IsOpen = true;
            return;
        }
        var items = new List<(string, Action)>
        {
            M("editText"), M("comment"), (s.ChapterTitle == null ? Loc.L("Hier ein Kapitel beginnen", "Start a chapter here") : Loc.L("Kapitel hier entfernen", "Remove chapter here"), () => ToggleChapter(s.Anchor, s.ChapterTitle == null)), M("hide", s.Hidden ? Loc.L("Einblenden", "Show") : Loc.L("Ausblenden", "Hide")), ("-", () => { }),
            M("moreImportant"), M("lessImportant"), M("moveUp", Loc.L("Nach oben", "Move up")), M("moveDown", Loc.L("Nach unten", "Move down")), ("-", () => { }),
            M("lightbox"), M("copyImage"), M("copyText", Loc.L("Text kopieren", "Copy text")), ("-", () => { }),
            M("toolCrop"), (Loc.L("Bild ersetzen oder neu aufnehmen …", "Replace or retake image …"), () => ReplaceImageMenu(s)),
            ("-", () => { }), (Loc.L("Schritt danach einfügen …", "Insert step after …"), () => InsertMenu(_list)),
        };
        if (s.Manual) items.Add((Loc.L("Eingefügten Schritt löschen", "Delete inserted step"), () => DeleteManual(new List<ViewStep> { s })));
        if (_view.CropFor(s) != null) items.InsertRange(items.IndexOf(M("toolCrop")) + 1, new (string, Action)[] { (Loc.L("Zuschnitt entfernen", "Remove crop"), () => RemoveCrop(s)), (Loc.L("Zuschnitt auf alle Schritte übertragen", "Apply crop to all steps"), CropToAll) });
        items.AddRange(new (string, Action)[] {
            ("-", () => { }),
            M("redactStep")
        });
        var m = Menu(items);
        m.Placement = PlacementMode.MousePoint;
        m.IsOpen = true;
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
        using var img = StepImages.Render(_view, s, variant, "original", annotate: annotate, viewMode: "screen", authorAnnotations: true) ?? (variant == "after" ? StepImages.Render(_view, s, "before", "original", authorAnnotations: true) : null);
        if (img == null) return null;
        int w = 160, h = Math.Max(1, (int)Math.Round(img.Bitmap.Height * 160.0 / Math.Max(1, img.Bitmap.Width)));
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

    void ShowDetails(ViewStep s)
    {
        s = _view.All.FirstOrDefault(x => x.Id == s.Id) ?? s;   // nach dem Speichern sind die Listeneinträge veraltet
        _details.Children.Clear();
        _editTitle = _editBody = null; _editAnchor = null;
        if (_detailTab == 1) { TechDetails(s); return; }
        if (_detailTab == 2) { DocDetails(); return; }
        if (_list.SelectedItems.Count > 1) { ShowBulk(); return; }

        var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        if (s.Manual) chips.Children.Add(U.Chip("Eigener Schritt – nicht aus der Aufnahme", Theme.AccentSoft, Theme.AccentText, KindIcon(s)));
        else
        {
        chips.Children.Add(U.Chip($"Schritt {s.Number}", Theme.Surface2, Theme.Fg2));
        chips.Children.Add(U.Chip(KindLabel(s.Kind), Theme.AccentSoft, Theme.AccentText, KindIcon(s)));
        if (s.Hidden) chips.Children.Add(U.Chip("ausgeblendet", Theme.Surface2, Theme.Muted, Theme.IEye));
        var (evText, evBg, evFg) = s.EvidenceLevel switch { "confirmed" => ("belegt", Theme.SuccessSoft, Theme.Success), "inputOnly" => ("nur Eingabe", Theme.WarnSoft, Theme.Warn), _ => ("unbekannt", Theme.Surface2, Theme.Muted) };
        var ev = U.Chip(evText, evBg, evFg, s.EvidenceLevel == "confirmed" ? Theme.ICheck : null);
        ev.ToolTip = s.EvidenceLevel switch { "confirmed" => "Die Wirkung der Aktion wurde beobachtet.", "inputOnly" => "Nur die Eingabe ist bekannt, eine Wirkung wurde nicht beobachtet.", _ => "Für diesen Schritt liegen keine Belege vor." };
        chips.Children.Add(ev);
        if (s.ImageReplaced) chips.Children.Add(U.Chip("Bild ersetzt", Theme.Surface2, Theme.Muted, Theme.ICamera));
        }
        _details.Children.Add(chips);

        if (s.Unreviewed)
            _details.Children.Add(Callout(Theme.IWarn, Theme.WarnSoft, Theme.Warn, "Auffällige Eingabe", $"{Patterns.KindName(s.FlagReason ?? "", "de")} – bitte in der Durchsicht prüfen, bevor Sie die Aufnahme weitergeben."));

        // Während der Aufnahme geschriebener Kommentar: als intern markieren (erscheint dann in keiner Ausgabe)
        if (s.Kind == "comment" && !s.Manual)
        {
            long src = s.Json.Obj("input")?.Long("refSeq") ?? 0;
            if (s.CommentHidden && _view.OriginalCommentText(s) is { Length: > 0 } orig)
                _details.Children.Add(Callout(Theme.ILock, Theme.Surface2, Theme.Fg2, "Interner Kommentar (nur hier sichtbar)", orig));
            var internalBox = new CheckBox { Content = "Interner Kommentar – erscheint in keiner Ausgabe", IsChecked = s.CommentHidden, IsEnabled = !_view.Session.ReadOnly && src > 0, Margin = new Thickness(0, 0, 0, 12), ToolTip = "Der Text dieses Kommentars wird in Anleitung, KI-Paket, MCP und Kommandozeilen-Export durch „[geschwärzt]“ ersetzt; der Schritt wird ausgeblendet." };
            internalBox.Checked += (_, _) => SetRecordedCommentHidden(src, true);
            internalBox.Unchecked += (_, _) => SetRecordedCommentHidden(src, false);
            _details.Children.Add(internalBox);
        }

        // Überschrift und Text direkt bearbeiten
        bool ro = _view.Session.ReadOnly;
        _editAnchor = s.Anchor;
        _editOrigTitle = s.AuthorTitle ?? s.Title; _editOrigBody = s.AuthorBody ?? s.Guide;
        _editTitle = new TextBox { Text = _editOrigTitle, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, IsReadOnly = ro, MaxLength = 300 };
        _editBody = new TextBox { Text = _editOrigBody, FontSize = 13.5, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 84, VerticalContentAlignment = VerticalAlignment.Top, IsReadOnly = ro, MaxLength = 8000 };
        var editTitle = _editTitle; var editBody = _editBody;
        Spell(editTitle); Spell(editBody);
        editTitle.KeyDown += (_, e) => { if (e.Key == Key.Enter) { editBody.Focus(); editBody.CaretIndex = editBody.Text.Length; e.Handled = true; } };
        foreach (var box in new[] { editTitle, editBody })
        {
            box.LostKeyboardFocus += (_, e) => { if (e.NewFocus == editTitle || e.NewFocus == editBody) return; InlineCommit(); };
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { editTitle.Text = _editOrigTitle; editBody.Text = _editOrigBody; _list.Focus(); e.Handled = true; }
                else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { _list.Focus(); e.Handled = true; }
            };
        }
        bool edited = s.AuthorTitle != null || s.AuthorBody != null;
        _details.Children.Add(U.Field("Überschrift", editTitle));
        var bodyField = U.Field("Anleitungstext", editBody, margin: new Thickness(0, 0, 0, 6));
        _details.Children.Add(bodyField);
        var editInfo = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        if (edited && !ro)
        {
            var reset = U.Btn(Theme.IUndo, "Original", () => ResetText(s), "Automatisch erzeugten Text wiederherstellen", "Link");
            reset.Margin = new Thickness(0); reset.Padding = new Thickness(4, 0, 0, 0);
            DockPanel.SetDock(reset, Dock.Right); editInfo.Children.Add(reset);
        }
        editInfo.Children.Add(U.Text(ro ? "Schreibgeschützt – zum Bearbeiten bitte entpacken." : s.Manual ? "Eigener Text · wird automatisch gespeichert" : edited ? "Von Ihnen geändert · wird automatisch gespeichert" : "Automatisch erzeugt – einfach überschreiben, wird automatisch gespeichert", 11.5, fg: Theme.Muted));
        _details.Children.Add(editInfo);

        // Wichtigkeit
        int impIdx = Math.Max(0, Array.IndexOf(ImportanceOrder, s.Importance));
        var (impTrack, imps) = U.Segmented("dimp", impIdx, (null, "Hauptschritt"), (null, "Nebenschritt"), (null, "Spur"));
        var impHint = U.Text(ImportanceHint(s.Importance), 11.5, fg: Theme.Muted, margin: new Thickness(0, 6, 0, 0));
        for (int i = 0; i < imps.Length; i++) { string target = ImportanceOrder[i]; imps[i].Checked += (_, _) => { if (target != s.Importance) Change(e => StepEdit(e, s.Anchor)["importance"] = target); }; imps[i].IsEnabled = !ro; }
        if (!s.Manual)
        {
            _details.Children.Add(U.Overline("Wichtigkeit", new Thickness(0, 18, 0, 8)));
            _details.Children.Add(impTrack);
            _details.Children.Add(impHint);
        }

        // Aktionen
        _details.Children.Add(U.Overline("Aktionen", new Thickness(0, 18, 0, 6)));
        var actions = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) };
        actions.Children.Add(U.Btn(Theme.IComment, "Hinweis", () => AddNoteBox(s.Anchor, "note"), "Hinweiskasten zum Schritt (K öffnet den Dialog)"));
        actions.Children.Add(U.Btn(Theme.IEye, s.Hidden ? "Einblenden" : "Ausblenden", ToggleHidden, "Schritt in Anleitung und Export aus- oder einblenden (Entf)"));
        foreach (var (glyph, dir, tip) in new[] { (Theme.IArrowUp, -1, "Nach oben (Alt+↑)"), (Theme.IArrowDown, +1, "Nach unten (Alt+↓)") })
        {
            var mv = U.Ghost(glyph, null, () => Move(dir), tip); mv.Margin = new Thickness(1, 3, 1, 3); mv.MinWidth = 32; mv.Padding = new Thickness(7, 5, 7, 5);
            actions.Children.Add(mv);
        }
        _details.Children.Add(actions);

        // Kapitel, Bild im Dokument, Hinweiskästen
        StepDocSection(s);

        // Kurzinfo
        _details.Children.Add(U.Divider(new Thickness(0, 20, 0, 4)));
        if (s.Json.Obj("ctx") is { } ctx) InfoRow("Fenster", ReadOnlyText($"„{ctx.Str("title")}“ · {ctx.Str("process")}", Theme.Fg2, 12.5));
        if (s.Json.Obj("target") is not null) InfoRow("Ziel", ReadOnlyText(Steps.TextTemplates.TargetLabel(s.Json, "de", false), Theme.Fg2, 12.5));
        var more = U.Btn(Theme.ICode, "Technische Angaben", () => _tabBtns[1].IsChecked = true, "Zum Reiter „Technik“", "Link");
        more.HorizontalAlignment = HorizontalAlignment.Left; more.Margin = new Thickness(-4, 8, 0, 0);
        _details.Children.Add(more);
    }

    static string ImportanceHint(string imp) => imp switch
    {
        "primary" => "Erscheint in der Anleitung und im Export.",
        "secondary" => "Begleitender Schritt – im Export nur, wenn „Nebenschritte“ gewählt ist.",
        _ => "Reine Spur (z. B. Mausbewegung) – nur unter „Alle“ sichtbar."
    };

    void InfoRow(string label, UIElement value)
    {
        var g = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(U.Text(label, 12.5, fg: Theme.Muted, margin: new Thickness(0, 1, 8, 0)));
        Grid.SetColumn(value, 1); g.Children.Add(value);
        _details.Children.Add(g);
    }

    void TechDetails(ViewStep s)
    {
        var j = s.Json;
        _details.Children.Add(U.Text(s.AuthorTitle ?? s.Title, 15, true, margin: new Thickness(0, 0, 0, 4)));
        _details.Children.Add(U.Text($"Schritt {s.Number} · {KindLabel(s.Kind)} · {ImportanceLabel(s.Importance)} · {Fmt.Time(s.TFrom, "de")}", 12, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 6)));
        if (j.Obj("ctx") is { } ctx) InfoRow("Fenster", ReadOnlyText($"„{ctx.Str("title")}“\n{ctx.Str("process")}"));
        if (j.Obj("target") is { } t)
        {
            var v = U.Col(ReadOnlyText(Steps.TextTemplates.TargetLabel(j, "de", false)));
            if (t.Str("status") is { } st) { var chip = U.Chip(st switch { "agreed" => "übereinstimmend", "corroborated" => "bestätigt", "single" => "eine Quelle", "conflict" => "widersprüchlich", "position" => "nur Position", _ => st }, Theme.Surface2, Theme.Muted); chip.HorizontalAlignment = HorizontalAlignment.Left; chip.Margin = new Thickness(0, 4, 0, 0); v.Children.Add(chip); }
            InfoRow("Ziel", v);
        }
        if (s.Kind == "type" && j.Obj("input") is { } inp)
        {
            var v = U.Col(ReadOnlyText(s.Unreviewed ? "(ungeprüft)" : Steps.TextTemplates.TokensNotation(inp), font: Theme.Mono, size: 12.5));
            if (inp.Str("valueAfter") is { } va && !s.Unreviewed) v.Children.Add(ReadOnlyText("Feldinhalt danach: " + va, Theme.Muted, 12));
            InfoRow("Eingabe", v);
        }
        if (s.Kind is "click" or "doubleClick" or "tripleClick" or "rightClick" or "drag" && j.Obj("input") is { } mi) InfoRow("Maus", ReadOnlyText(MouseSummary(s.Kind, mi)));
        if (j.Obj("effect") is { } eff && eff.Bool("observed")) InfoRow("Wirkung", ReadOnlyText(EffectSummary(eff)));
        var (evText, evBg, evFg) = s.EvidenceLevel switch { "confirmed" => ("belegt", Theme.SuccessSoft, Theme.Success), "inputOnly" => ("nur Eingabe", Theme.WarnSoft, Theme.Warn), _ => ("unbekannt", Theme.Surface2, Theme.Muted) };
        var evChip = U.Chip(evText, evBg, evFg, s.EvidenceLevel == "confirmed" ? Theme.ICheck : null); evChip.HorizontalAlignment = HorizontalAlignment.Left;
        var reasons = string.Join(", ", j.Obj("evidence")?.StrList("reasons") ?? new());
        InfoRow("Beleg", reasons.Length > 0 ? U.Col(evChip, ReadOnlyText(reasons, Theme.Muted, 12)) : evChip);

        if (j.Obj("target")?.Obj("win32") is { } w32) Win32Section(s, w32, InfoRow);
        AppContextSection(j);

        _details.Children.Add(U.Overline("Protokoll für KI", new Thickness(0, 22, 0, 8)));
        _details.Children.Add(new Border { Background = Theme.Surface2, CornerRadius = new CornerRadius(5), Padding = new Thickness(12, 10, 12, 10), Child = ReadOnlyText(s.Protocol, Theme.Fg2, 12, Theme.Mono) });
        _details.Children.Add(new Expander
        {
            Header = "Rohdaten (JSON)", Margin = new Thickness(0, 16, 0, 0),
            Content = new Border { Background = Theme.Surface2, CornerRadius = new CornerRadius(5), Padding = new Thickness(12, 10, 12, 10), Child = new ScrollViewer { MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = ReadOnlyText(Json.Pretty(j), Theme.Fg2, 11.5, Theme.Mono) } }
        });
    }

    /// <summary>Programmkontext für Automatisierung: Menübefehl, Liste, Explorer, Browser, Office, Statusleiste, Schreibmarke, MSAA.</summary>
    void AppContextSection(JsonObject j)
    {
        var rows = new List<(string label, string value)>();
        string Q(string? t) => $"„{t}“";
        if (j.Obj("menuPath") is { } mp && (mp.LongN("commandId") != null || mp.Str("source") != null))
            rows.Add(("Menübefehl", string.Join(" › ", (mp.Arr("items") ?? new JsonArray()).Select(x => x?.ToString())) + (mp.LongN("commandId") is long id ? $"\nBefehls-ID {id} (WM_COMMAND)" : "") + (mp.Bool("disabled") ? "\ndeaktiviert" : "")));
        if (j.Obj("choose")?.Obj("win32") is { } cw)
            rows.Add(("Liste", $"{cw.Str("kind")} {cw.Str("classNN")}" + (cw.Str("text") is { } ct ? $"\nEintrag {Q(ct)}" : "") + (cw.LongN("index") is long ix ? $" · Nr. {ix}" + (cw.LongN("count") is long cn ? $" von {cn}" : "") : "") + (cw.Str("path") is { } pth ? $"\nPfad {pth}" : "")));
        if (j.Obj("explorer") is { } ex)
            rows.Add(("Explorer", (ex.Str("folder") ?? ex.Str("location") ?? "") + (ex.Str("folderAfter") is { } fa && fa != ex.Str("folder") ? $"\n→ {fa}" : "") + (ex.LongN("selectedCount") is long sc && sc > 0 ? $"\n{sc} ausgewählt" : "")));
        if (j.Obj("data")?.Obj("fileDialog") is { } fd && fd.Str("folder") is { } fdf)
            rows.Add(("Dateidialog", fdf + (fd.Str("fileNameControl") is { } fnc ? $"\nDateiname-Feld {fnc}" : "")));
        if (j.Obj("browser") is { } br)
            rows.Add(("Browser", $"{br.Str("browser")}\n{br.Str("url")}" + (br.Str("urlAfter") is { } ua && ua != br.Str("url") ? $"\n→ {ua}" : "")));
        if (j.Obj("office") is { } of)
            rows.Add(("Office", string.Join("\n", new[] { of.Str("app"), of.Str("workbookName") ?? of.Str("documentName"), of.Str("sheet") is { } sh ? $"Blatt {sh}" + (of.Str("cell") is { } cell ? $" · Zelle {cell}" : "") : null }.Where(x => x != null))));
        if (j.Obj("verify")?.Obj("statusBar") is { } sb)
            rows.Add(("Statusleiste", string.Join(" | ", (sb.Arr("parts") ?? new JsonArray()).Select(x => x?.ToString())) + ((sb.Arr("changed")?.Count ?? 0) > 0 ? "\n(nach der Aktion geändert)" : "")));
        if (j.Obj("target")?.Obj("win32")?.Obj("control")?.Obj("caret") is { } ca)
            rows.Add(("Schreibmarke", $"Zeile {ca.LongN("line")}, Spalte {ca.LongN("col")}" + (ca.Long("selLen") > 0 ? $" · {ca.Long("selLen")} Zeichen markiert" : "") + (ca.Bool("readOnly") ? " · schreibgeschützt" : "")));
        if (j.Obj("target")?.Obj("win32")?.Obj("msaa") is { } ms)
            rows.Add(("MSAA", $"{ms.Str("roleText") ?? ms.Str("role")}" + (ms.Str("defaultAction") is { } da ? $" · Standardaktion {Q(da)}" : "")));
        if (rows.Count == 0) return;
        _details.Children.Add(U.Overline("Programmkontext", new Thickness(0, 22, 0, 2)));
        foreach (var (label, value) in rows) InfoRow(label, ReadOnlyText(value.Trim(), Theme.Fg, 12));
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
        string t = _editTitle.Text.Trim(), b = _editBody.Text.TrimEnd();
        if (t == _editOrigTitle.Trim() && b == _editOrigBody.TrimEnd()) return;
        if (t.Length == 0) { _editTitle.Text = _editOrigTitle; return; }
        _editOrigTitle = t; _editOrigBody = b;
        if (now) SaveText(anchor, t, b);
        else Dispatcher.BeginInvoke(DispatcherPriority.Background, () => SaveText(anchor, t, b));
    }

    void SaveText(long anchor, string title, string body) => Change(e =>
    {
        var se = StepEdit(e, anchor);
        var text = se["text"] as JsonObject ?? new JsonObject();
        text[_view.Language] = new JsonObject { ["title"] = title, ["body"] = body, ["by"] = "user", ["atUtc"] = Json.UtcNow() };
        se["text"] = text;
    });

    void ResetText(ViewStep s) => Change(e =>
    {
        var se = StepEdit(e, s.Anchor);
        if (se["text"] is JsonObject t) { t.Remove(_view.Language); if (t.Count == 0) se.Remove("text"); }
    });

    // =====================================================================================
    // Zeichnen, Auswählen, Verschieben
    // =====================================================================================

    static double D(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<double>(out var d) ? d : v.TryGetValue<int>(out var i) ? i : v.TryGetValue<long>(out var l) ? l : 0) : 0;

    /// <summary>Umriss einer Markierung in Bildkoordinaten.</summary>
    static Rect? Bounds(JsonObject a)
    {
        if (a["geom"] is not JsonArray g) return null;
        switch (a.Str("type"))
        {
            case "arrow" or "line" when g.Count >= 4: return new Rect(new Point(D(g[0]), D(g[1])), new Point(D(g[2]), D(g[3])));
            case "rect" or "ellipse" or "highlight" when g.Count >= 4: return new Rect(D(g[0]), D(g[1]), Math.Max(1, D(g[2])), Math.Max(1, D(g[3])));
            case "badge" when g.Count >= 2: return new Rect(D(g[0]) - 15, D(g[1]) - 15, 30, 30);
            case "text" when g.Count >= 2:
                double fs = a.Obj("style")?.Dbl("fontSize", 18) ?? 18;
                return new Rect(D(g[0]) - 2, D(g[1]) - 2, Math.Max(1, a.Str("text")?.Length ?? 1) * fs * 0.62 + 8, fs * 1.45);
        }
        return null;
    }

    static double DistToSegment(Point p, Point a, Point b)
    {
        var ab = b - a; double len = ab.LengthSquared;
        if (len < 1e-6) return (p - a).Length;
        double t = Math.Clamp(((p - a) * ab) / len, 0, 1);
        return (p - (a + t * ab)).Length;
    }

    JsonObject? HitAnnotation(Point p)
    {
        if (_currentMeta == null) return null;
        double tol = 8 / Math.Max(0.05, _currentMeta.Scale);
        foreach (var a in AnnotationsFor(_currentMeta.FrameId).Reverse())
        {
            if (Bounds(a) is not Rect b) continue;
            if (a.Str("type") is "arrow" or "line" && a["geom"] is JsonArray g)
            { if (DistToSegment(p, new Point(D(g[0]), D(g[1])), new Point(D(g[2]), D(g[3]))) <= tol + 4) return a; }
            else { b.Inflate(tol, tol); if (b.Contains(p)) return a; }
        }
        return null;
    }

    Point ToOverlay(Point img) => _currentMeta == null ? img : new Point((img.X - _currentMeta.SourceRect.X) * _currentMeta.Scale, (img.Y - _currentMeta.SourceRect.Y) * _currentMeta.Scale);

    /// <summary>Bildschirmpunkte → Overlay-Einheiten (damit Rahmen und Anfasser bei jedem Zoom gleich groß wirken).</summary>
    double Px(double screenPx) => screenPx / Math.Max(0.02, _zoom?.Zoom ?? 1);

    /// <summary>Anfasser einer Markierung in Bildkoordinaten: Pfeil = Anfang/Ende, Flächen = vier Ecken.</summary>
    static List<Point> HandlesOf(JsonObject a)
    {
        var l = new List<Point>();
        if (a["geom"] is not JsonArray g) return l;
        if (a.Str("type") is "arrow" or "line" && g.Count >= 4) { l.Add(new Point(D(g[0]), D(g[1]))); l.Add(new Point(D(g[2]), D(g[3]))); }
        else if (a.Str("type") is "rect" or "ellipse" or "highlight" && g.Count >= 4)
        {
            double x = D(g[0]), y = D(g[1]), w = D(g[2]), h = D(g[3]);
            l.Add(new Point(x, y)); l.Add(new Point(x + w, y)); l.Add(new Point(x + w, y + h)); l.Add(new Point(x, y + h));
        }
        return l;
    }

    /// <summary>Neue Geometrie, wenn Anfasser <paramref name="h"/> nach <paramref name="p"/> gezogen wird.</summary>
    static JsonArray Resized(JsonObject a, int h, Point p)
    {
        var g = (JsonArray)a["geom"]!;
        if (a.Str("type") is "arrow" or "line")
            return h == 0 ? new JsonArray((int)p.X, (int)p.Y, (int)D(g[2]), (int)D(g[3])) : new JsonArray((int)D(g[0]), (int)D(g[1]), (int)p.X, (int)p.Y);
        double x = D(g[0]), y = D(g[1]), r = x + D(g[2]), b = y + D(g[3]);
        // gegenüberliegende Ecke bleibt fest
        var fixedPt = h switch { 0 => new Point(r, b), 1 => new Point(x, b), 2 => new Point(x, y), _ => new Point(r, y) };
        var rc = new Rect(fixedPt, p);
        return new JsonArray((int)rc.X, (int)rc.Y, Math.Max(4, (int)rc.Width), Math.Max(4, (int)rc.Height));
    }

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
        foreach (var hp in HandlesOf(a))
        {
            var o = ToOverlay(hp);
            var sq = new Rectangle { Width = hsz, Height = hsz, Fill = Brushes.White, Stroke = Theme.Accent, StrokeThickness = Px(1.5), RadiusX = Px(2), RadiusY = Px(2), IsHitTestVisible = false };
            Canvas.SetLeft(sq, o.X - hsz / 2); Canvas.SetTop(sq, o.Y - hsz / 2);
            _overlay.Children.Add(sq); _handleShapes.Add(sq);
        }
    }

    void SyncColorTo(JsonObject a)
    {
        _syncingColor = true;
        if (a.Obj("style")?.Str("color") is { } hex && a.Str("type") is not "highlight")
            _colorBtns.FirstOrDefault(c => string.Equals((string)c.Tag, hex, StringComparison.OrdinalIgnoreCase))?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
        int w = (int)(a.Obj("style")?.Dbl("width", 4) ?? 4);
        _strokeBtns.OrderBy(b => Math.Abs((int)b.Tag - w)).FirstOrDefault()?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
        _syncingColor = false;
    }

    void OverlayDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentMeta == null) return;
        var pos = e.GetPosition(_overlay);
        if (_tool == "select")
        {
            var p = ToImage(pos);
            // Anfasser der aktuellen Auswahl?
            if (_selAnn != null && AnnotationsFor(_currentMeta.FrameId).FirstOrDefault(x => x.Str("id") == _selAnn) is { } sel)
            {
                double tol = Px(9) / Math.Max(0.05, _currentMeta.Scale);
                var hs = HandlesOf(sel);
                for (int i = 0; i < hs.Count; i++)
                    if ((hs[i] - p).Length <= tol) { _handle = i; _handleGeom = (JsonArray)sel["geom"]!.DeepClone(); _overlay.CaptureMouse(); return; }
            }
            var hit = HitAnnotation(p);
            _selAnn = hit?.Str("id");
            DrawSelection();
            if (hit == null) { if (e.ClickCount == 2) OpenLightbox(); return; }
            SyncColorTo(hit);
            if (e.ClickCount == 2 && hit.Str("type") == "text") { EditAnnotationText(hit); return; }
            _moveFrom = p; _overlay.CaptureMouse();
            return;
        }
        _dragStart = pos;
        if (_tool is "text" or "badge")
        {
            var p = ToImage(pos);
            string text = _tool == "badge" ? ((_view.Edits.Arr("annotations")?.Count(a => a.Str("frame") == _currentMeta.FrameId && a.Str("type") == "badge") ?? 0) + 1).ToString() : "";
            if (_tool == "text") { var d = new InputDialog("Text der Markierung", "") { Owner = this }; if (d.ShowDialog() != true || d.Value.Trim().Length == 0) { _dragStart = null; return; } text = d.Value; }
            AddAnnotation(_tool, new JsonArray((int)p.X, (int)p.Y), text);
            _dragStart = null;
            return;
        }
        var stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_color));
        _rubber = _tool switch
        {
            "arrow" => new Line { Stroke = stroke, StrokeThickness = _stroke },
            "ellipse" => new Ellipse { Stroke = stroke, StrokeThickness = _stroke },
            "redact" => new Rectangle { Fill = Brushes.Black, Stroke = Brushes.Black, StrokeThickness = 1 },
            "pixelate" => new Rectangle { Fill = new DrawingBrush { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, Px(12), Px(12)), ViewportUnits = BrushMappingMode.Absolute, Drawing = new GeometryDrawing(new SolidColorBrush(Color.FromArgb(150, 120, 125, 135)), null, new RectangleGeometry(new Rect(0, 0, 1, 1))) }, Stroke = Brushes.Gray, StrokeThickness = Px(1) },
            "crop" => new Rectangle { Stroke = Theme.Accent, StrokeThickness = Px(2), StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)) },
            "highlight" => new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(90, 255, 235, 59)) },
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
            var probe = new JsonObject { ["type"] = ha.Str("type"), ["geom"] = _handleGeom!.DeepClone(), ["style"] = ha.Obj("style")?.DeepClone() };
            probe["geom"] = Resized(probe, _handle, ToImage(e.GetPosition(_overlay)));
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
            _overlay.Cursor = HitAnnotation(ToImage(e.GetPosition(_overlay))) != null ? Cursors.SizeAll : Cursors.Arrow;
        if (_dragStart == null || _rubber == null) return;
        var a = _dragStart.Value; var b = e.GetPosition(_overlay);
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
                Change(ed => { if (AnnotationEdit(ed, rid) is { } x) { var probe = new JsonObject { ["type"] = x.Str("type"), ["geom"] = _handleGeom.DeepClone() }; x["geom"] = Resized(probe, hi, p); } }, refill: false);
            return;
        }
        if (_moveFrom != null)
        {
            _overlay.ReleaseMouseCapture();
            var p = ToImage(e.GetPosition(_overlay));
            var d = p - _moveFrom.Value; _moveFrom = null;
            if (_selAnn != null && (Math.Abs(d.X) >= 1 || Math.Abs(d.Y) >= 1)) MoveAnnotation(_selAnn, d.X, d.Y);
            else DrawSelection();
            return;
        }
        if (_dragStart == null) return;
        _overlay.ReleaseMouseCapture();
        var a = ToImage(_dragStart.Value); var b = ToImage(e.GetPosition(_overlay));
        _dragStart = null; _rubber = null;
        if (Math.Abs(b.X - a.X) < 3 && Math.Abs(b.Y - a.Y) < 3) { ShowStep(); return; }
        int x = (int)Math.Min(a.X, b.X), y = (int)Math.Min(a.Y, b.Y), w = (int)Math.Abs(b.X - a.X), h = (int)Math.Abs(b.Y - a.Y);
        if (_tool is "redact" or "pixelate") { AddImageRedaction(x, y, w, h, _tool == "pixelate" ? "pixelate" : "black"); return; }
        if (_tool == "crop") { SetCrop(x, y, w, h); return; }
        if (_tool == "arrow") AddAnnotation("arrow", new JsonArray((int)a.X, (int)a.Y, (int)b.X, (int)b.Y), null);
        else AddAnnotation(_tool, new JsonArray(x, y, w, h), null);
    }

    void AddAnnotation(string type, JsonArray geom, string? text)
    {
        var s = Sel; if (s == null || _currentMeta == null) return;
        string frame = _currentMeta.FrameId;
        string color = _color; int width = _stroke, fontSize = _fontSize;
        Change(e =>
        {
            var arr = e["annotations"] as JsonArray ?? new JsonArray();
            var a = new JsonObject { ["id"] = NextId(e, "annotations", "a"), ["frame"] = frame, ["anchor"] = s.Anchor, ["type"] = type, ["geom"] = geom, ["style"] = new JsonObject { ["color"] = color, ["width"] = width, ["fontSize"] = fontSize } };
            if (text != null) a["text"] = text;
            arr.Add(a); e["annotations"] = arr;
        }, refill: false);
    }

    JsonObject? AnnotationEdit(JsonObject edits, string id) => (edits.Arr("annotations") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(x => x.Str("id") == id);

    void MoveAnnotation(string id, double dx, double dy) => Change(e =>
    {
        if (AnnotationEdit(e, id) is not { } a || a["geom"] is not JsonArray g) return;
        var v = g.Select(D).ToList();
        if (a.Str("type") is "arrow" or "line" && v.Count >= 4) { v[0] += dx; v[1] += dy; v[2] += dx; v[3] += dy; }
        else if (v.Count >= 2) { v[0] += dx; v[1] += dy; }
        a["geom"] = new JsonArray(v.Select(x => (JsonNode)(int)Math.Round(x)).ToArray());
    }, refill: false);

    void RecolorSelected(string hex)
    {
        if (_selAnn is not { } id) return;
        Change(e => { if (AnnotationEdit(e, id) is { } a) { var st = a.Obj("style") ?? new JsonObject(); st["color"] = hex; a["style"] = st; } }, refill: false);
    }

    void RestrokeSelected(int width, int fontSize)
    {
        if (_selAnn is not { } id) return;
        Change(e => { if (AnnotationEdit(e, id) is { } a) { var st = a.Obj("style") ?? new JsonObject(); st["width"] = width; st["fontSize"] = fontSize; a["style"] = st; } }, refill: false);
    }

    // ---------------- Zuschneiden ----------------

    void SetCrop(int x, int y, int w, int h)
    {
        var s = Sel; if (s == null) return;
        if (w < 32 || h < 32) { _status.Text = "Der Ausschnitt ist zu klein (mindestens 32 × 32 Pixel)."; return; }
        Change(e => StepEdit(e, s.Anchor)["crop"] = new JsonArray(x, y, w, h), refill: false);
        SelectTool("select");
        _zoom.FitNow();
        _status.Text = $"Schritt {s.Number} zugeschnitten ({w} × {h}) – rückgängig mit Strg+Z oder „Zuschnitt entfernen“.";
    }

    void RemoveCrop(ViewStep s) => Change(e => StepEdit(e, s.Anchor).Remove("crop"), refill: false);

    /// <summary>Überträgt den Zuschnitt des gewählten Schritts auf alle Schritte mit Bildern desselben Monitors.</summary>
    void CropToAll()
    {
        var s = Sel; if (s == null || _view.CropFor(s) is not { } c) return;
        var m = s.Monitor ?? s.Json.Obj("position")?.Str("m");
        var targets = _view.All.Where(x => x.PreFrame != null && (m == null || (x.Monitor ?? x.Json.Obj("position")?.Str("m")) == m || x.Monitor == null)).ToList();
        Change(e => { foreach (var t in targets) StepEdit(e, t.Anchor)["crop"] = new JsonArray(c.X, c.Y, c.Width, c.Height); });
        _status.Text = $"Zuschnitt auf {targets.Count} Schritte übertragen – rückgängig mit Strg+Z.";
    }

    // ---------------- Großansicht ----------------

    /// <summary>Bild eines Listeneintrags in voller Auflösung mit allen Markierungen.</summary>
    (BitmapSource img, string caption)? LightboxItem(int i)
    {
        if (i < 0 || i >= _list.Items.Count || ((ListBoxItem)_list.Items[i]).Tag is not ViewStep s) return null;
        string variant = i == _list.SelectedIndex && _variantOverride != null ? _variantOverride : DefaultVariant(s);
        using var img = StepImages.Render(_view, s, _display == "crop" ? "crop" : variant, "original", annotate: true, viewMode: _display == "window" ? "window" : "screen", authorAnnotations: true)
            ?? StepImages.Render(_view, s, "before", "original", authorAnnotations: true);
        if (img == null) return null;
        return (U.ToSource(img.Bitmap), $"Schritt {s.Number} von {_view.All.Count}  ·  {s.AuthorTitle ?? s.Title}");
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
        var d = new InputDialog("Text der Markierung", a.Str("text") ?? "") { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        Change(e => { if (AnnotationEdit(e, id) is { } x) x["text"] = d.Value; }, refill: false);
    }

    /// <summary>Nur für Entwickler-Schnappschüsse: Reiter „Technik“ und eigene Markierungen mit Auswahl vorführen.</summary>
    internal void DevDemo(Action save)
    {
        void Flush() { for (int i = 0; i < 6; i++) Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { }); }
        if (_list.Items.Count < 2) return;
        _list.SelectedIndex = 1;
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
        // Bedienungsanleitung: Kapitel und Hinweiskasten am Schritt, dann Reiter „Dokument“
        if (Sel is { } ds)
        {
            if (ds.ChapterTitle == null) ToggleChapter(ds.Anchor, true);
            if (Sel is { } ds2 && ds2.Comments.Count == 0)
                Change(e => { var arr = e["comments"] as JsonArray ?? new JsonArray(); arr.Add(new JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = ds2.Anchor, ["kind"] = "tip", ["text"] = "Mit Strg+1 geht es schneller.", ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" }); e["comments"] = arr; }, refill: false);
            Flush();
            if (_details.Parent is ScrollViewer sv) { sv.ScrollToEnd(); Flush(); save(); sv.ScrollToHome(); }
        }
        _tabBtns[2].IsChecked = true; Flush(); save();
        if (_details.Parent is ScrollViewer sv2) { sv2.ScrollToEnd(); Flush(); save(); sv2.ScrollToHome(); }
        _tabBtns[0].IsChecked = true; Flush();
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
            Change(e => { if (e["annotations"] is JsonArray arr && AnnotationEdit(e, id) is { } a) arr.Remove(a); }, refill: false);
            _status.Text = "Markierung gelöscht – rückgängig mit Strg+Z";
            return;
        }
        if (_currentMeta == null || !AnnotationsFor(_currentMeta.FrameId).Any()) { _status.Text = "Dieses Bild hat keine eigenen Markierungen."; return; }
        if (Msg.Show("Alle eigenen Markierungen dieses Bildes entfernen?", "Markierungen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) ClearAnnotations();
    }
}
