using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

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

    internal static (string kind, string name, string glyph)[] NoteKindsUi => new[]
    {
        ("note", Loc.L("Hinweis", "Note"), Theme.IComment), ("tip", Loc.L("Tipp", "Tip"), Theme.IInfo), ("warning", Loc.L("Achtung", "Caution"), Theme.IWarn),
        ("important", Loc.L("Wichtig", "Important"), Theme.IImportant), ("result", Loc.L("Ergebnis", "Result"), Theme.ICheck), ("internal", Loc.L("Intern", "Internal"), Theme.ILock),
        ("agent", Loc.L("Vom KI-Agenten (intern)", "From AI agent (internal)"), Theme.IRobot),
    };

    // =====================================================================================
    // Eintrag und Modus „Titelseite“
    // =====================================================================================

    FrameworkElement BuildCoverEntry()
    {
        var icon = U.Icon(Theme.IDoc, 18, Theme.Fg2); icon.Margin = new Thickness(0, 0, 14, 0); icon.VerticalAlignment = VerticalAlignment.Center;
        var title = U.Text(Loc.L("Titelseite und Einleitung", "Title page and introduction"), Theme.FsBody, true);
        var sub = U.Meta(Loc.L("Titel, Einleitung, Inhaltsverzeichnis, Gestaltung", "Title, introduction, table of contents, design"), new Thickness(0, 2, 0, 0));
        sub.TextTrimming = TextTrimming.CharacterEllipsis; sub.TextWrapping = TextWrapping.NoWrap;
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon); row.Children.Add(U.Col(title, sub));
        _coverBtn = new RadioButton
        {
            Style = Theme.Style("NavItem"), GroupName = "cover", Content = row, Height = double.NaN, Padding = new Thickness(16, 9, 12, 9), Margin = new Thickness(6, 0, 6, 6),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ToolTip = Loc.L("Was aus den Schritten eine fertige Anleitung macht: Titelseite, Einleitung, Voraussetzungen, Inhaltsverzeichnis, Bilder, Farbe, Fußzeile",
                "What turns the steps into a finished guide: title page, introduction, prerequisites, table of contents, images, colour, footer")
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
        // Mit Seitenansicht: statt der nachgebauten Titelseite die echten Seiten von vorn
        if (ApplyCenterMode()) ShowStepInPages(null);
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
        if (d.Audience.Length > 0) meta.Add(Loc.For(_view.Language, "Zielgruppe: ", "Audience: ") + d.Audience);
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
        stack.Children.Add(U.Text(d.Cover ? Loc.L("Titelseite", "Title page") : Loc.L("Titelseite ist ausgeschaltet – die Angaben stehen im Kopf der ersten Seite", "Title page is off – the details appear in the header of the first page"), Theme.FsCaption, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 8)));
        stack.Children.Add(page);

        // Gliederung: Kapitel mit Schrittzahl (wie im Export mit Hauptschritten)
        var outline = new StackPanel();
        outline.Children.Add(U.Text(Loc.L("Inhalt", "Contents"), Theme.FsBody, true, margin: new Thickness(0, 0, 0, 8)));
        var parts = new List<(string title, int n)>();
        foreach (var s in _view.Effective(guide: true, importance: "primary", includeManual: true))
        {
            if (s.ChapterTitle != null || parts.Count == 0) parts.Add((s.ChapterTitle ?? "", 0));
            var last = parts[^1]; parts[^1] = (last.title, last.n + 1);
        }
        bool chapters = parts.Any(p => p.title.Length > 0);
        if (!chapters)
            outline.Children.Add(U.Hint(Loc.N(parts.Sum(p => p.n), "1 Schritt, noch ohne Kapitel. Kapitel legen Sie am Schritt unter „Einordnung“ an.", "{0} Schritte, noch ohne Kapitel. Kapitel legen Sie am Schritt unter „Einordnung“ an.",
                "1 step, no chapters yet. Add chapters at a step under “Placement”.", "{0} steps, no chapters yet. Add chapters at a step under “Placement”.")));
        int k = 0;
        foreach (var (title, n) in parts.Where(_ => chapters))
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var count = U.Meta(Loc.N(n, "1 Schritt", "{0} Schritte", "1 step", "{0} steps")); count.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(count, Dock.Right); row.Children.Add(count);
            row.Children.Add(U.Text(title.Length > 0 ? $"{++k}   {title}" : Loc.L("Vor dem ersten Kapitel", "Before the first chapter"), Theme.FsHint, fg: title.Length > 0 ? Theme.Fg : Theme.Muted));
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
        var box = Section(notes.Count > 0 ? Loc.F("Hinweise ({0})", "Notes ({0})", notes.Count) : Loc.L("Hinweise", "Notes"));
        RichEdit? focus = null;
        foreach (var c in notes)
        {
            string? cid = c.Str("id");
            string kind = c.Str("kind") ?? "note";
            var barBrush = kind is "internal" or "agent" ? Theme.Muted : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + GuideExport.NoteStyle(kind, "de").bar));
            var kinds = new ComboBox { MinWidth = 130, IsEnabled = !ro, Margin = new Thickness(0, 0, 6, 0), HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var (_, name, _) in NoteKindsUi) kinds.Items.Add(name);
            kinds.SelectedIndex = Math.Max(0, Array.FindIndex(NoteKindsUi, k => k.kind == kind));
            kinds.ToolTip = Loc.L("„Intern“ erscheint in keiner Ausgabe", "“Internal” appears in no output");
            kinds.SelectionChanged += (_, _) => { string k = NoteKindsUi[Math.Max(0, kinds.SelectedIndex)].kind; Change(e => { if (e["comments"] is JsonArray arr) foreach (var x in arr.OfType<JsonObject>().Where(x => x.Str("id") == cid)) x["kind"] = k; }, refill: false); };
            var del = U.Ghost(Theme.IDelete, null, () => RemoveComment(cid), Loc.L("Hinweis löschen", "Delete note"));
            del.MinHeight = 32; del.Padding = new Thickness(8, 5, 8, 5); del.Margin = new Thickness(0); del.IsEnabled = !ro;
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(del, Dock.Right); head.Children.Add(del); head.Children.Add(kinds);
            // Hinweistext formatierbar (13, Paket 21); gespeichert mit Kennzeichen fmt
            var text = RichBox(c.Str("text") ?? "", ViewSessionRich(c), (v, rich) => QuietChange(e =>
            {
                if (e["comments"] is JsonArray arr)
                    foreach (var x in arr.OfType<JsonObject>().Where(x => x.Str("id") == cid)) { x["text"] = v; if (rich) x["fmt"] = 1; else x.Remove("fmt"); }
            }), Loc.L("Text des Hinweises", "Note text"), 44);
            if (cid != null && cid == _focusCommentId) focus = text;
            var stripe = new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = barBrush, Margin = new Thickness(0, 2, 12, 2) };
            var inner = new DockPanel();
            DockPanel.SetDock(stripe, Dock.Left); inner.Children.Add(stripe); inner.Children.Add(U.Col(head, text));
            box.Children.Add(U.Inset(inner, new Thickness(12, 10, 10, 12), new Thickness(0, 0, 0, 10)));
        }
        if (notes.Count == 0) box.Children.Add(U.Hint(Loc.L("Hinweise erscheinen als farbige Kästen unter dem Schritttext – z. B. Tipp, Achtung oder Ergebnis.", "Notes appear as coloured boxes below the step text – e.g. tip, caution or result."), new Thickness(0, 0, 0, 10)));
        if (!ro)
        {
            Button? add = null;
            add = U.Btn(Theme.IAdd, null, () =>
            {
                var m = U.MenuScope(new ContextMenu { PlacementTarget = add, Placement = PlacementMode.Bottom });
                foreach (var (k, name, glyph) in NoteKindsUi) { string kk = k; m.Items.Add(U.Mi(name + (k == "internal" ? Loc.L(" (erscheint in keiner Ausgabe)", " (appears in no output)") : ""), null, () => AddNoteBox(anchor, kk), glyph)); }
                m.IsOpen = true;
            }, Loc.L("Hinweis zu diesem Schritt hinzufügen (K öffnet den Dialog)", "Add a note to this step (K opens the dialog)"));
            add.Content = U.MenuContent(Theme.IAdd, Loc.L("Hinweis hinzufügen", "Add note"));
            add.Named(Loc.L("Hinweis hinzufügen", "Add note"));
            add.HorizontalAlignment = HorizontalAlignment.Left; add.Margin = new Thickness(0, 0, 0, 12);
            box.Children.Add(add);
        }
        if (focus != null) { _focusCommentId = null; var f = focus; Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { f.FocusEnd(); f.BringIntoView(); }); }
    }

    /// <summary>Kapitel: Häkchen „Kapitel beginnt hier“, dann Titel und Einleitung.</summary>
    void ChapterBlock(ViewStep s, Panel target)
    {
        bool ro = _view.Session.ReadOnly;
        long anchor = s.Anchor;
        var chap = new CheckBox { Content = Loc.L("Kapitel beginnt hier", "Chapter starts here"), IsChecked = s.ChapterTitle != null, IsEnabled = !ro, ToolTip = Loc.L("Gliedert die Anleitung: Kapitelüberschrift mit Einleitung vor diesem Schritt, Eintrag im Inhaltsverzeichnis", "Structures the guide: chapter heading with introduction before this step, entry in the table of contents") };
        chap.Checked += (_, _) => ToggleChapter(anchor, true);
        chap.Unchecked += (_, _) => ToggleChapter(anchor, false);
        var p = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        p.Children.Add(U.Text(Loc.L("Kapitel", "Chapter"), Theme.FsBody, false, Theme.Fg, new Thickness(0, 0, 0, 6)));
        p.Children.Add(chap);
        if (s.ChapterTitle != null)
        {
            var ct = CommitBox(s.ChapterTitle, v => { if (v.Length > 0) QuietChange(e => ChapterTarget(e, anchor)["title"] = v, refillLater: true); }, Loc.L("Titel des Kapitels", "Chapter title"));
            ct.FontWeight = FontWeights.SemiBold; ct.Margin = new Thickness(0, 8, 0, 0);
            var cx = RichBox(s.ChapterRich?.Doc.ToMarkup() ?? s.ChapterText ?? "", s.ChapterRich != null, (v, rich) => QuietChange(e => { var t = ChapterTarget(e, anchor); t["text"] = v; if (rich) t["fmt"] = 1; else t.Remove("fmt"); }), Loc.L("Kurze Einleitung zum Kapitel (optional)", "Short chapter introduction (optional)"), 48);
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
        var box = Section(Loc.L("Bild in der Anleitung", "Image in the guide"));
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var show = new CheckBox { Content = Loc.L("Bild zeigen", "Show image"), IsChecked = s.ShowImage != false, IsEnabled = !ro, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), ToolTip = Loc.L("Für reine Textschritte ausschalten", "Turn off for text-only steps") };
        show.Checked += (_, _) => Change(e => StepEdit(e, anchor).Remove("showImage"), refill: false);
        show.Unchecked += (_, _) => Change(e => StepEdit(e, anchor)["showImage"] = false, refill: false);
        DockPanel.SetDock(show, Dock.Left); row.Children.Add(show);
        var size = new ComboBox { MinWidth = 150, IsEnabled = !ro && s.ShowImage != false, ToolTip = Loc.L("Bildgröße nur für diesen Schritt", "Image size for this step only") };
        size.Items.Add(Loc.L("Wie im Dokument", "As in document"));
        foreach (var (_, name) in DocSettings.Sizes) size.Items.Add(Loc.F("Bild {0}", "Image {0}", name));
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
        repl = U.Btn(Theme.ICamera, s.Manual && s.PreFrame == null ? Loc.L("Bild hinzufügen …", "Add image …") : Loc.L("Bild ersetzen …", "Replace image …"), () => ReplaceImageMenu(s, repl), Loc.L("Bildschirmfoto neu aufnehmen oder Bild aus Datei bzw. Zwischenablage nehmen", "Take a new screenshot or use an image from a file or the clipboard"));
        repl.IsEnabled = !ro; acts.Children.Add(repl);
        if (s.ImageReplaced) acts.Children.Add(U.Btn(Theme.IUndo, Loc.L("Originalbild", "Original image"), () => Change(e => StepEdit(e, anchor).Remove("frameChoice"), refill: false), Loc.L("Aufgenommenes Bild wiederherstellen", "Restore the recorded image")));
        if (s.Manual) acts.Children.Add(U.Btn(Theme.IDelete, Loc.L("Schritt löschen", "Delete step"), () => DeleteManual(new List<ViewStep> { s }), Loc.L("Eingefügten Schritt löschen", "Delete inserted step")));
        box.Children.Add(acts);
    }
}
