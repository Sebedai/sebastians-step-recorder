using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Ein Befehl des Editors: Beschriftung, Tastenkürzel, Bedingung. Die Tabelle steuert Tasten, Menüs und F1.</summary>
sealed record ViewerCmd(string Id, string Group, string Label, Key Key, ModifierKeys Mods, Action Run, Func<bool>? When = null, bool InText = false, bool Listed = true)
{
    public string Gesture => ViewerCommands.Gesture(Key, Mods);
}

static class ViewerCommands
{
    public static (string id, string label)[] Groups() => new[]
    {
        ("steps", Loc.L("Schritte", "Steps")), ("image", Loc.L("Bild und Markierungen", "Image and markup")),
        ("copy", Loc.L("Kopieren und Exportieren", "Copy and export")), ("general", Loc.L("Allgemein", "General"))
    };

    public static string Gesture(Key key, ModifierKeys mods)
    {
        if (key == Key.None) return "";
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        parts.Add(key switch
        {
            Key.Delete => "Del", Key.Insert => "Ins", Key.Escape => "Esc", Key.Up => "↑", Key.Down => "↓", Key.Left => "←", Key.Right => "→",
            Key.Return => "Enter", Key.Space => "Space", _ => key.ToString()
        });
        return Loc.Keys(string.Join("+", parts));
    }
}

sealed partial class ViewerWindow
{
    List<ViewerCmd>? _cmds;
    List<ViewerCmd> Cmds => _cmds ??= BuildCommands();

    /// <summary>Reihenfolge zählt: die erste passende Zeile gewinnt (Bedingungen zuerst).</summary>
    List<ViewerCmd> BuildCommands()
    {
        const ModifierKeys N = ModifierKeys.None, C = ModifierKeys.Control, S = ModifierKeys.Shift, A = ModifierKeys.Alt, CS = ModifierKeys.Control | ModifierKeys.Shift;
        bool ListUnfocused() => !_list.IsKeyboardFocusWithin;
        void Next() { if (_list.SelectedIndex < _list.Items.Count - 1) _list.SelectedIndex++; }
        void Prev() { if (_list.SelectedIndex > 0) _list.SelectedIndex--; }
        return new List<ViewerCmd>
        {
            // Kopieren und Exportieren
            new("copyImage", "copy", Loc.L("Bild kopieren", "Copy image"), Key.C, C, CopyImage),
            new("copyImageText", "copy", Loc.L("Bild mit Text kopieren", "Copy image with text"), Key.C, CS, CopyImageWithText),
            new("copyText", "copy", Loc.L("Nur Text kopieren", "Copy text only"), Key.T, CS, CopyText),
            new("copyOriginal", "copy", Loc.L("Originalbild kopieren", "Copy original image"), Key.O, CS, CopyOriginal),
            new("saveImage", "copy", Loc.L("Bild speichern …", "Save image …"), Key.S, CS, SaveImage),
            new("copyMarkdown", "copy", Loc.L("Als Markdown kopieren", "Copy as Markdown"), Key.M, C, CopyMarkdown),
            new("export", "copy", Loc.L("Exportieren …", "Export …"), Key.E, C, () => ShowExportDialog(AppState.Load().Export.LastFormat ?? "html")),
            new("repeatExport", "copy", Loc.L("Letzten Export wiederholen", "Repeat last export"), Key.E, CS, RepeatLastExport),
            new("save", "general", Loc.L("Speichern (geschieht automatisch)", "Save (automatic)"), Key.S, C, () => _status.Text = Loc.L("Automatisch gespeichert", "Saved automatically"), Listed: false),
            // Allgemein
            new("review", "general", Loc.L("Durchsicht der Eingaben", "Review input"), Key.R, CS, ShowReview),
            new("generalize", "general", Loc.L("Werte verallgemeinern …", "Generalize values …"), Key.G, CS, Generalize),
            new("undo", "general", Loc.L("Rückgängig", "Undo"), Key.Z, C, Undo),
            new("redo", "general", Loc.L("Wiederholen", "Redo"), Key.Y, C, Redo),
            new("find", "general", Loc.L("Schritte durchsuchen", "Search steps"), Key.F, C, () => { _search.Focus(); _search.SelectAll(); }),
            new("replace", "general", Loc.L("Suchen und Ersetzen …", "Find and replace …"), Key.H, C, ShowSearchReplace),
            new("insertStep", "steps", Loc.L("Schritt einfügen …", "Insert step …"), Key.Insert, S, () => InsertManual(null)),
            new("newRecording", "general", Loc.L("Neue Aufnahme", "New recording"), Key.N, C, () => UiApp.Instance.ShowStart()),
            new("library", "general", Loc.L("Alle Aufnahmen …", "All recordings …"), Key.L, C, () => UiApp.Instance.ShowLibrary()),
            new("close", "general", Loc.L("Fenster schließen", "Close window"), Key.W, C, Close),
            // Schritte
            new("moreImportant", "steps", Loc.L("Wichtiger", "More important"), Key.Up, C, () => ChangeImportance(+1)),
            new("lessImportant", "steps", Loc.L("Unwichtiger", "Less important"), Key.Down, C, () => ChangeImportance(-1)),
            new("moveUp", "steps", Loc.L("Nach oben verschieben", "Move up"), Key.Up, A, () => Move(-1)),
            new("moveDown", "steps", Loc.L("Nach unten verschieben", "Move down"), Key.Down, A, () => Move(+1)),
            new("merge", "steps", Loc.L("Schritte zusammenfassen", "Merge steps"), Key.J, C, MergeSelected),
            new("unmerge", "steps", Loc.L("Zusammenfassung aufheben", "Dissolve merge"), Key.J, CS, UnmergeSelected),
            new("redactStep", "steps", Loc.L("Schritt schwärzen", "Redact step"), Key.Delete, S, RedactStep),
            new("deleteMark", "image", Loc.L("Ausgewählte Markierung löschen", "Delete selected markup"), Key.Delete, N, DeleteSelectedOrAll, () => _selAnn != null),
            new("hide", "steps", Loc.L("Schritt aus-/einblenden", "Hide/show step"), Key.Delete, N, ToggleHidden),
            new("editText", "steps", Loc.L("Überschrift und Text bearbeiten", "Edit heading and text"), Key.F2, N, FocusInlineTitle, InText: true),
            new("comment", "steps", Loc.L("Kommentar hinzufügen", "Add comment"), Key.K, N, AddComment),
            new("note", "steps", Loc.L("Hinweis einfügen …", "Insert note …"), Key.Insert, N, InsertNote),
            new("next", "steps", Loc.L("Nächster Schritt", "Next step"), Key.Down, N, Next, ListUnfocused),
            new("prev", "steps", Loc.L("Vorheriger Schritt", "Previous step"), Key.Up, N, Prev, ListUnfocused),
            new("next2", "steps", Loc.L("Nächster Schritt", "Next step"), Key.Right, N, Next, ListUnfocused, Listed: false),
            new("prev2", "steps", Loc.L("Vorheriger Schritt", "Previous step"), Key.Left, N, Prev, ListUnfocused, Listed: false),
            // Bild und Markierungen
            new("toolSelect", "image", Loc.L("Auswählen", "Select"), Key.V, N, () => SelectTool("select")),
            new("toolArrow", "image", Loc.L("Pfeil", "Arrow"), Key.A, N, () => SelectTool("arrow")),
            new("toolLine", "image", Loc.L("Linie", "Line"), Key.A, S, () => SelectTool("line")),
            new("toolFreehand", "image", Loc.L("Freihand", "Freehand"), Key.F, N, () => SelectTool("freehand")),
            new("toolSpotlight", "image", Loc.L("Spotlight", "Spotlight"), Key.U, N, () => SelectTool("spotlight")),
            new("toolMagnifier", "image", Loc.L("Lupe", "Magnifier"), Key.L, N, () => SelectTool("magnifier")),
            new("toolRect", "image", Loc.L("Rahmen", "Rectangle"), Key.R, N, () => SelectTool("rect")),
            new("toolEllipse", "image", Loc.L("Ellipse", "Ellipse"), Key.E, N, () => SelectTool("ellipse")),
            new("toolHighlight", "image", Loc.L("Hervorheben", "Highlight"), Key.M, N, () => SelectTool("highlight")),
            new("toolText", "image", Loc.L("Text", "Text"), Key.T, N, () => SelectTool("text")),
            new("toolBadge", "image", Loc.L("Nummer", "Number"), Key.N, N, () => SelectTool("badge")),
            new("toolRedact", "image", Loc.L("Schwärzen", "Redact"), Key.X, N, () => SelectTool("redact")),
            new("toolPixelate", "image", Loc.L("Verpixeln", "Pixelate"), Key.P, N, () => SelectTool("pixelate")),
            new("toolCrop", "image", Loc.L("Zuschneiden", "Crop"), Key.C, N, () => SelectTool("crop")),
            new("deselect", "image", Loc.L("Auswahl aufheben", "Clear selection"), Key.Escape, N, () => { _selAnn = null; DrawSelection(); }, () => _selAnn != null),
            new("beforeAfter", "image", Loc.L("Vorher-/Nachher-Bild", "Before/after image"), Key.W, N, ToggleBeforeAfter),
            new("stepMarks", "image", Loc.L("Mausmarkierungen dieses Schritts", "Mouse markers of this step"), Key.H, N, ToggleStepMarks),
            new("lightbox", "image", Loc.L("Großansicht", "Full view"), Key.F11, N, OpenLightbox),
            // Allgemein (Funktionstasten)
            new("play", "general", Loc.L("Abspielen / anhalten", "Play / pause"), Key.F5, N, TogglePlay, InText: true),
            new("quality", "general", Loc.L("Qualitätsprüfung", "Quality check"), Key.F8, N, ShowQuality, InText: true),
            new("nextFinding", "general", Loc.L("Nächster Befund", "Next finding"), Key.F4, N, () => NextFinding(+1), InText: true),
            new("prevFinding", "general", Loc.L("Vorheriger Befund", "Previous finding"), Key.F4, S, () => NextFinding(-1), InText: true),
            new("compare", "general", Loc.L("Mit anderer Aufnahme vergleichen", "Compare with another recording"), Key.F7, N, Compare),
            new("stopPlay", "general", Loc.L("Abspielen beenden", "Stop playback"), Key.Escape, N, StopPlay, Listed: false),
            new("shortcuts", "general", Loc.L("Alle Tastenkürzel", "All shortcuts"), Key.F1, N, () => ShortcutsWindow.Show(this, Cmds), InText: true),
        };
    }

    ViewerCmd Cmd(string id) => Cmds.First(c => c.Id == id);

    /// <summary>Für Entwickler-Schnappschüsse.</summary>
    internal void DevShowShortcuts() => ShowShortcuts();

    /// <summary>Menüeintrag aus der Befehlstabelle: Beschriftung und Kürzel (durch Tabulator getrennt) samt Aktion.</summary>
    (string label, Action action) M(string id, string? label = null)
    {
        var c = Cmd(id);
        return ((label ?? c.Label) + (c.Gesture.Length > 0 ? "\t" + c.Gesture : ""), c.Run);
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        bool inText = Keyboard.FocusedElement is TextBox;
        if (!inText && _zoom.HandleKey(e)) { e.Handled = true; return; }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        foreach (var c in Cmds)
        {
            if (c.Key != key || c.Mods != mods || (inText && !c.InText)) continue;
            if (c.When != null && !c.When()) continue;
            e.Handled = true;
            try { c.Run(); } catch (Exception ex) { UiApp.ShowError(ex); }
            return;
        }
    }

    /// <summary>Menübeschriftung zerlegen: „Text\tKürzel“ oder alt „Text (Kürzel)“.</summary>
    static (string header, string gesture) SplitLabel(string label)
    {
        int tab = label.IndexOf('\t');
        if (tab >= 0) return (label[..tab], label[(tab + 1)..]);
        var mt = System.Text.RegularExpressions.Regex.Match(label, @"^(.*?)\s*\((K|[^()]*(?:Strg|Ctrl|Alt|Umschalt|Shift|Entf|Del|Einfg|Ins|F\d)[^()]*)\)$");
        return mt.Success ? (mt.Groups[1].Value, mt.Groups[2].Value) : (label, "");
    }
}

/// <summary>F1: alle Tastenkürzel des Editors und der Aufnahme, durchsuchbar.</summary>
static class ShortcutsWindow
{
    public static void Show(Window owner, IEnumerable<ViewerCmd> commands)
    {
        var w = new Window { Title = Loc.L("Tastenkürzel", "Keyboard shortcuts"), Width = 640, Height = Math.Min(760, Theme.ActiveWorkArea().Height - 60), ResizeMode = ResizeMode.CanResize, ShowInTaskbar = false, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var rows = new List<(string group, string label, string keys)>();
        foreach (var (gid, groupLabel) in ViewerCommands.Groups())
            foreach (var c in commands.Where(c => c.Group == gid && c.Listed))
                rows.Add((groupLabel, c.Label, c.Gesture));
        var rec = Loc.L("Während der Aufnahme (systemweit)", "While recording (system-wide)");
        var s = AppSettings.Load();
        foreach (var a in HotkeyActions.All)
            rows.Add((rec, Loc.L(a.De, a.En), Loc.Keys(s.Hotkey(a.Id)) is { Length: > 0 } k ? k : Loc.L("nicht belegt", "not assigned")));

        var search = new TextBox { Tag = Loc.L("Kürzel oder Befehl suchen", "Search shortcut or command"), Padding = new Thickness(10, 7, 8, 7), Margin = new Thickness(0, 0, 0, 12) };
        var list = new StackPanel();
        void Fill()
        {
            list.Children.Clear();
            var q = StepRecorder.ReadLayer.SearchQuery.Parse(search.Text);
            string? last = null;
            foreach (var (group, label, keys) in rows)
            {
                if (!q.Matches(group + " " + label + " " + keys)) continue;
                if (group != last)
                {
                    list.Children.Add(U.Overline(group, new Thickness(0, last == null ? 0 : 16, 0, 6)));
                    last = group;
                }
                var dock = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                var k = new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 3), Child = new TextBlock { Text = keys, FontFamily = Theme.Mono, FontSize = 12, Foreground = Theme.Fg } };
                DockPanel.SetDock(k, Dock.Right); dock.Children.Add(k);
                dock.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = Theme.Fg, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                list.Children.Add(dock);
            }
            if (last == null) list.Children.Add(U.Text(Loc.L("Kein Treffer.", "No match."), 13, fg: Theme.Muted));
        }
        search.TextChanged += (_, _) => Fill();
        Fill();
        var body = new DockPanel();
        DockPanel.SetDock(search, Dock.Top); body.Children.Add(search);
        body.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = Math.Max(240, w.Height - 220) });
        U.Dialog(w, Loc.L("Tastenkürzel", "Keyboard shortcuts"), body, U.Primary(Loc.L("Schließen", "Close"), () => w.Close()));
        w.Loaded += (_, _) => search.Focus();
        w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
        w.ShowDialog();
    }
}
