using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.App.Ui;

/// <summary>
/// „Titelseite und Einleitung“: fester Eintrag über der Schrittliste. Ausgewählt zeigt die Mitte eine Vorschau der
/// Titelseite mit der Gliederung, rechts stehen die Einstellungen der Anleitung (früher Reiter „Dokument“).
/// Dazu die Karten-Abschnitte des Detailbereichs (Hinweise, Einordnung, Bild in der Anleitung).
/// </summary>
sealed partial class ViewerWindow
{
    bool _cover;
    RadioButton _coverBtn = null!;
    readonly ScrollViewer _coverHost = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
    FrameworkElement _viewBorder = null!, _paletteEl = null!, _zoomBarEl = null!, _detailTabs = null!;

    internal static readonly (string kind, string name, string glyph)[] NoteKindsUi =
    {
        ("note", "Hinweis", Theme.IComment), ("tip", "Tipp", Theme.IInfo), ("warning", "Achtung", Theme.IWarn),
        ("important", "Wichtig", Theme.IImportant), ("result", "Ergebnis", Theme.ICheck), ("internal", "Intern", Theme.ILock),
    };

    // =====================================================================================
    // Eintrag und Modus „Titelseite“
    // =====================================================================================

    FrameworkElement BuildCoverEntry()
    {
        var icon = U.Icon(Theme.IDoc, 18, Theme.Fg2); icon.Margin = new Thickness(0, 0, 14, 0); icon.VerticalAlignment = VerticalAlignment.Center;
        var title = U.Text("Titelseite und Einleitung", Theme.FsBody, true);
        var sub = U.Meta("Titel, Einleitung, Inhaltsverzeichnis, Gestaltung", new Thickness(0, 2, 0, 0));
        sub.TextTrimming = TextTrimming.CharacterEllipsis; sub.TextWrapping = TextWrapping.NoWrap;
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon); row.Children.Add(U.Col(title, sub));
        _coverBtn = new RadioButton
        {
            Style = Theme.Style("NavItem"), GroupName = "cover", Content = row, Height = double.NaN, Padding = new Thickness(16, 9, 12, 9), Margin = new Thickness(6, 0, 6, 6),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ToolTip = "Was aus den Schritten eine fertige Anleitung macht: Titelseite, Einleitung, Voraussetzungen, Inhaltsverzeichnis, Bilder, Farbe, Fußzeile"
        };
        _coverBtn.Checked += (_, _) => { if (!_cover) ShowCover(); };
        return _coverBtn;
    }

    void ShowCover()
    {
        InlineCommit(now: true);
        StopPlay();
        _cover = true;
        _coverBtn.IsChecked = true;
        if (_list.SelectedItems.Count > 0) _list.UnselectAll();   // löst ShowStep aus, das im Titelseiten-Modus hierher zurückkehrt
        else ApplyCoverMode();
    }

    /// <summary>Oberfläche auf „Titelseite“ umstellen und Vorschau sowie Einstellungen aufbauen.</summary>
    void ApplyCoverMode()
    {
        foreach (var e in new[] { _viewBorder, _paletteEl, _zoomBarEl, _detailTabs, _zoom, _noImage, _imgCaptionPill }) e.Visibility = Visibility.Collapsed;
        _cropOff.Visibility = Visibility.Collapsed;
        _coverHost.Visibility = Visibility.Visible;
        _coverHost.Content = CoverPreview();
        _overlay.Children.Clear();
        _stepPos.Text = "";
        DocDetails();
        UpdateStatus();
    }

    /// <summary>Zurück zu den Schritten (beim Wählen eines Schritts).</summary>
    void LeaveCover()
    {
        if (!_cover) return;
        _cover = false;
        _coverBtn.IsChecked = false;
        foreach (var e in new[] { _viewBorder, _paletteEl, _zoomBarEl, _detailTabs, _zoom }) e.Visibility = Visibility.Visible;
        _coverHost.Visibility = Visibility.Collapsed;
        _coverHost.Content = null;
    }

    /// <summary>Vorschau der Titelseite (wie gedruckt: weißes Blatt) und darunter die Gliederung.</summary>
    FrameworkElement CoverPreview()
    {
        var guide = _view.Edits.Obj("guide");
        var d = DocSettings.From(guide, _view.Language);
        Color accent;
        try { accent = (Color)ColorConverter.ConvertFromString(d.Accent); } catch { accent = Color.FromRgb(0x2F, 0x5C, 0x9C); }
        var ink = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        var muted = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C));

        var col = new StackPanel();
        if (d.LogoBytes() is { } lb)
            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage(); bi.BeginInit(); bi.StreamSource = new MemoryStream(lb); bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
                col.Children.Add(new Image { Source = bi, MaxHeight = 44, MaxWidth = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 40) });
            }
            catch { }
        col.Children.Add(new Border { Width = 56, Height = 4, Background = new SolidColorBrush(accent), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, d.LogoBytes() == null ? 84 : 0, 0, 18) });
        col.Children.Add(new TextBlock { Text = _view.Title, FontFamily = Theme.Display, FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = ink, TextWrapping = TextWrapping.Wrap });
        if (d.Subtitle.Length > 0) col.Children.Add(new TextBlock { Text = d.Subtitle, FontSize = 15, Foreground = muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        var meta = new List<string>();
        if (d.Audience.Length > 0) meta.Add((_view.Language == "de" ? "Zielgruppe: " : "Audience: ") + d.Audience);
        if (d.Version.Length > 0) meta.Add("Version " + d.Version);
        if (d.Author.Length > 0) meta.Add(d.Author);
        if (d.ShowDate) meta.Add(_view.Session.StartUtc.ToLocalTime().ToString(_view.Language == "de" ? "dd.MM.yyyy" : "yyyy-MM-dd"));
        var metaBlock = new TextBlock { Text = string.Join("\n", meta), FontSize = 12, Foreground = muted, LineHeight = 19, VerticalAlignment = VerticalAlignment.Bottom };
        var pageGrid = new Grid();
        pageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        pageGrid.RowDefinitions.Add(new RowDefinition());
        pageGrid.Children.Add(col);
        Grid.SetRow(metaBlock, 1); pageGrid.Children.Add(metaBlock);
        var page = new Border
        {
            Width = 380, Height = 537, Background = Brushes.White, CornerRadius = new CornerRadius(2), Padding = new Thickness(40, 44, 40, 36),
            Effect = Theme.Shadow(30, 4, Theme.Dark ? 0.6 : 0.18), Child = pageGrid, HorizontalAlignment = HorizontalAlignment.Center
        };

        var stack = new StackPanel { Margin = new Thickness(24, 28, 24, 28), HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(U.Text(d.Cover ? "Titelseite" : "Titelseite ist ausgeschaltet – die Angaben stehen im Kopf der ersten Seite", Theme.FsCaption, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 8)));
        stack.Children.Add(page);

        // Gliederung: Kapitel mit Schrittzahl (wie im Export mit Hauptschritten)
        var outline = new StackPanel();
        outline.Children.Add(U.Text(_view.Language == "de" ? "Inhalt" : "Contents", Theme.FsBody, true, margin: new Thickness(0, 0, 0, 8)));
        var parts = new List<(string title, int n)>();
        foreach (var s in _view.Effective(guide: true, importance: "primary", includeManual: true))
        {
            if (s.ChapterTitle != null || parts.Count == 0) parts.Add((s.ChapterTitle ?? "", 0));
            var last = parts[^1]; parts[^1] = (last.title, last.n + 1);
        }
        bool chapters = parts.Any(p => p.title.Length > 0);
        if (!chapters)
            outline.Children.Add(U.Hint($"{parts.Sum(p => p.n)} Schritte, noch ohne Kapitel. Kapitel legen Sie am Schritt unter „Einordnung“ an."));
        int k = 0;
        foreach (var (title, n) in parts.Where(_ => chapters))
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var count = U.Meta(n == 1 ? "1 Schritt" : $"{n} Schritte"); count.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(count, Dock.Right); row.Children.Add(count);
            row.Children.Add(U.Text(title.Length > 0 ? $"{++k}   {title}" : "Vor dem ersten Kapitel", Theme.FsHint, fg: title.Length > 0 ? Theme.Fg : Theme.Muted));
            outline.Children.Add(row);
        }
        var outlineCard = U.Card(outline, new Thickness(16, 12, 16, 12), new Thickness(0, 20, 0, 0));
        outlineCard.Width = 380;
        stack.Children.Add(outlineCard);
        return stack;
    }

    // =====================================================================================
    // Karten-Abschnitte im Detailbereich
    // =====================================================================================

    /// <summary>Abschnitt im Detailbereich: Überschrift und Karte; liefert den Inhalt der Karte.</summary>
    StackPanel Section(string title, string? hint = null, Thickness? labelMargin = null)
    {
        _details.Children.Add(U.SectionLabel(title, labelMargin));
        var inner = new StackPanel();
        if (hint != null) inner.Children.Add(U.Hint(hint, new Thickness(0, 0, 0, 12)));
        _details.Children.Add(U.Card(inner, new Thickness(16, 14, 16, 2)));
        return inner;
    }

    /// <summary>Alle Hinweise eines Schritts, auch „Intern“ (die erscheinen in keiner Ausgabe, sind hier aber bearbeitbar).</summary>
    List<JsonObject> NotesOf(ViewStep s) => (_view.Edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>().Where(c => c.Long("anchor") == s.Anchor && !c.Bool("hidden")).ToList();

    void NotesSection(ViewStep s)
    {
        bool ro = _view.Session.ReadOnly;
        long anchor = s.Anchor;
        var notes = NotesOf(s);
        var box = Section(notes.Count > 0 ? $"Hinweise ({notes.Count})" : "Hinweise");
        TextBox? focus = null;
        foreach (var c in notes)
        {
            string? cid = c.Str("id");
            string kind = c.Str("kind") ?? "note";
            var barBrush = kind == "internal" ? Theme.Muted : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + GuideExport.NoteStyle(kind, "de").bar));
            var kinds = new ComboBox { MinWidth = 130, IsEnabled = !ro, Margin = new Thickness(0, 0, 6, 0), HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var (_, name, _) in NoteKindsUi) kinds.Items.Add(name);
            kinds.SelectedIndex = Math.Max(0, Array.FindIndex(NoteKindsUi, k => k.kind == kind));
            kinds.ToolTip = "„Intern“ erscheint in keiner Ausgabe";
            kinds.SelectionChanged += (_, _) => { string k = NoteKindsUi[Math.Max(0, kinds.SelectedIndex)].kind; Change(e => { if (e["comments"] is JsonArray arr) foreach (var x in arr.OfType<JsonObject>().Where(x => x.Str("id") == cid)) x["kind"] = k; }, refill: false); };
            var del = U.Ghost(Theme.IDelete, null, () => RemoveComment(cid), "Hinweis löschen");
            del.MinHeight = 32; del.Padding = new Thickness(8, 5, 8, 5); del.Margin = new Thickness(0); del.IsEnabled = !ro;
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(del, Dock.Right); head.Children.Add(del); head.Children.Add(kinds);
            var text = CommitBox(c.Str("text") ?? "", v => QuietChange(e => { if (e["comments"] is JsonArray arr) foreach (var x in arr.OfType<JsonObject>().Where(x => x.Str("id") == cid)) x["text"] = v; }), "Text des Hinweises", true, 44);
            if (cid != null && cid == _focusCommentId) focus = text;
            var stripe = new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = barBrush, Margin = new Thickness(0, 2, 12, 2) };
            var inner = new DockPanel();
            DockPanel.SetDock(stripe, Dock.Left); inner.Children.Add(stripe); inner.Children.Add(U.Col(head, text));
            box.Children.Add(U.Inset(inner, new Thickness(12, 10, 10, 12), new Thickness(0, 0, 0, 10)));
        }
        if (notes.Count == 0) box.Children.Add(U.Hint("Hinweise erscheinen als farbige Kästen unter dem Schritttext – z. B. Tipp, Achtung oder Ergebnis.", new Thickness(0, 0, 0, 10)));
        if (!ro)
        {
            Button? add = null;
            add = U.Btn(Theme.IAdd, null, () =>
            {
                var m = U.MenuScope(new ContextMenu { PlacementTarget = add, Placement = PlacementMode.Bottom });
                foreach (var (k, name, glyph) in NoteKindsUi) { string kk = k; m.Items.Add(U.Mi(name + (k == "internal" ? " (erscheint in keiner Ausgabe)" : ""), null, () => AddNoteBox(anchor, kk), glyph)); }
                m.IsOpen = true;
            }, "Hinweis zu diesem Schritt hinzufügen (K öffnet den Dialog)");
            add.Content = U.MenuContent(Theme.IAdd, "Hinweis hinzufügen");
            add.HorizontalAlignment = HorizontalAlignment.Left; add.Margin = new Thickness(0, 0, 0, 12);
            box.Children.Add(add);
        }
        if (focus != null) { _focusCommentId = null; var f = focus; Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { f.Focus(); f.BringIntoView(); }); }
    }

    /// <summary>Kapitel: Häkchen „Kapitel beginnt hier“, dann Titel und Einleitung.</summary>
    void ChapterBlock(ViewStep s, Panel target)
    {
        bool ro = _view.Session.ReadOnly;
        long anchor = s.Anchor;
        var chap = new CheckBox { Content = "Kapitel beginnt hier", IsChecked = s.ChapterTitle != null, IsEnabled = !ro, ToolTip = "Gliedert die Anleitung: Kapitelüberschrift mit Einleitung vor diesem Schritt, Eintrag im Inhaltsverzeichnis" };
        chap.Checked += (_, _) => ToggleChapter(anchor, true);
        chap.Unchecked += (_, _) => ToggleChapter(anchor, false);
        var p = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        p.Children.Add(U.Text("Kapitel", Theme.FsBody, false, Theme.Fg, new Thickness(0, 0, 0, 6)));
        p.Children.Add(chap);
        if (s.ChapterTitle != null)
        {
            var ct = CommitBox(s.ChapterTitle, v => { if (v.Length > 0) QuietChange(e => ChapterTarget(e, anchor)["title"] = v, refillLater: true); }, "Titel des Kapitels");
            ct.FontWeight = FontWeights.SemiBold; ct.Margin = new Thickness(0, 8, 0, 0);
            var cx = CommitBox(s.ChapterText ?? "", v => QuietChange(e => ChapterTarget(e, anchor)["text"] = v), "Kurze Einleitung zum Kapitel (optional)", true, 48);
            cx.Margin = new Thickness(0, 6, 0, 0);
            p.Children.Add(ct); p.Children.Add(cx);
        }
        target.Children.Add(p);
    }

    /// <summary>Bild in der Anleitung: zeigen, Größe, ersetzen.</summary>
    void ImageBlock(ViewStep s)
    {
        bool ro = _view.Session.ReadOnly;
        long anchor = s.Anchor;
        var box = Section("Bild in der Anleitung");
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var show = new CheckBox { Content = "Bild zeigen", IsChecked = s.ShowImage != false, IsEnabled = !ro, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), ToolTip = "Für reine Textschritte ausschalten" };
        show.Checked += (_, _) => Change(e => StepEdit(e, anchor).Remove("showImage"), refill: false);
        show.Unchecked += (_, _) => Change(e => StepEdit(e, anchor)["showImage"] = false, refill: false);
        DockPanel.SetDock(show, Dock.Left); row.Children.Add(show);
        var size = new ComboBox { MinWidth = 150, IsEnabled = !ro && s.ShowImage != false, ToolTip = "Bildgröße nur für diesen Schritt" };
        size.Items.Add("Wie im Dokument");
        foreach (var (_, name) in DocSettings.Sizes) size.Items.Add("Bild " + name);
        size.SelectedIndex = s.ImageSize == null ? 0 : 1 + Math.Max(0, Array.FindIndex(DocSettings.Sizes, x => x.id == s.ImageSize));
        size.SelectionChanged += (_, _) =>
        {
            int i = size.SelectedIndex;
            Change(e => { if (i <= 0) StepEdit(e, anchor).Remove("imageSize"); else StepEdit(e, anchor)["imageSize"] = DocSettings.Sizes[i - 1].id; }, refill: false);
        };
        row.Children.Add(size);
        box.Children.Add(row);
        var acts = new WrapPanel { Margin = new Thickness(-3, 0, 0, 10) };
        Button? repl = null;
        repl = U.Btn(Theme.ICamera, s.Manual && s.PreFrame == null ? "Bild hinzufügen …" : "Bild ersetzen …", () => ReplaceImageMenu(s, repl), "Bildschirmfoto neu aufnehmen oder Bild aus Datei bzw. Zwischenablage nehmen");
        repl.IsEnabled = !ro; acts.Children.Add(repl);
        if (s.ImageReplaced) acts.Children.Add(U.Btn(Theme.IUndo, "Originalbild", () => Change(e => StepEdit(e, anchor).Remove("frameChoice"), refill: false), "Aufgenommenes Bild wiederherstellen"));
        if (s.Manual) acts.Children.Add(U.Btn(Theme.IDelete, "Schritt löschen", () => DeleteManual(new List<ViewStep> { s }), "Eingefügten Schritt löschen"));
        box.Children.Add(acts);
    }
}
