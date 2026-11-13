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
        ("steps", Loc.L("Schritte", "Steps")), ("image", Loc.L("Bild und Zeichnungen", "Image and drawings")),
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
            new("open", "general", Loc.L("Öffnen …", "Open …"), Key.O, C, OpenSessionDialog),
            new("close", "general", Loc.L("Fenster schließen", "Close window"), Key.W, C, Close),
            // Schritte
            new("moreImportant", "steps", Loc.L("Wichtiger", "More important"), Key.Up, C, () => ChangeImportance(+1)),
            new("lessImportant", "steps", Loc.L("Unwichtiger", "Less important"), Key.Down, C, () => ChangeImportance(-1)),
            new("moveUp", "steps", Loc.L("Nach oben verschieben", "Move up"), Key.Up, A, () => Move(-1)),
            new("moveDown", "steps", Loc.L("Nach unten verschieben", "Move down"), Key.Down, A, () => Move(+1)),
            new("merge", "steps", Loc.L("Schritte zusammenfassen", "Merge steps"), Key.J, C, MergeSelected),
            new("unmerge", "steps", Loc.L("Zusammenfassung aufheben", "Dissolve merge"), Key.J, CS, UnmergeSelected),
            new("rerecord", "steps", Loc.L("Neu aufnehmen und ersetzen …", "Re-record and replace …"), Key.R, C, ReRecordSelected),
            new("unreplace", "steps", Loc.L("Ersetzung aufheben", "Undo replacement"), Key.None, N, UndoReplacement, Listed: false),
            new("reapply", "steps", Loc.L("Ersetzung erneut einsetzen", "Reapply replacement"), Key.None, N, ReapplyReplacement, Listed: false),
            new("redactStep", "steps", Loc.L("Schritt schwärzen", "Redact step"), Key.Delete, S, RedactStep),
            new("deleteMark", "image", Loc.L("Ausgewählte Zeichnung löschen", "Delete selected drawing"), Key.Delete, N, DeleteSelectedOrAll, () => _selAnn != null),
            new("hide", "steps", Loc.L("Schritt aus-/einblenden", "Hide/show step"), Key.Delete, N, ToggleHidden),
            new("editText", "steps", Loc.L("Überschrift und Text bearbeiten", "Edit heading and text"), Key.F2, N, FocusInlineTitle, InText: true),
            new("comment", "steps", Loc.L("Hinweis hinzufügen …", "Add note …"), Key.K, N, AddComment),
            new("note", "steps", Loc.L("Hinweis hinzufügen (zweite Taste)", "Add note (second key)"), Key.Insert, N, InsertNote),
            new("bookmark", "steps", Loc.L("Lesezeichen setzen/entfernen", "Set/remove bookmark"), Key.L, CS, ToggleBookmark),
            new("snippet", "steps", Loc.L("Textbaustein einfügen …", "Insert text snippet …"), SnippetUi.ShortcutKey, SnippetUi.ShortcutMods, InsertSnippet, InText: true),
            new("next", "steps", Loc.L("Nächster Schritt", "Next step"), Key.Down, N, Next, ListUnfocused),
            new("prev", "steps", Loc.L("Vorheriger Schritt", "Previous step"), Key.Up, N, Prev, ListUnfocused),
            new("next2", "steps", Loc.L("Nächster Schritt", "Next step"), Key.Right, N, Next, ListUnfocused, Listed: false),
            new("prev2", "steps", Loc.L("Vorheriger Schritt", "Previous step"), Key.Left, N, Prev, ListUnfocused, Listed: false),
            // Bild und Zeichnungen
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
            new("beforeAfter", "image", Loc.L("Vorher/Nachher wechseln", "Switch before/after"), Key.W, N, ToggleBeforeAfter),
            new("stepMarks", "image", Loc.L("Mausmarken dieses Schritts ein/aus", "Mouse marks for this step on/off"), Key.H, N, ToggleStepMarks),
            new("lightbox", "image", Loc.L("Großansicht", "Full view"), Key.F11, N, OpenLightbox),
            // Allgemein (Funktionstasten)
            new("play", "general", Loc.L("Abspielen / anhalten", "Play / pause"), Key.F5, N, TogglePlay, InText: true),
            new("quality", "general", Loc.L("Qualitätsprüfung", "Quality check"), Key.F8, N, ShowQuality, InText: true),
            new("nextFinding", "general", Loc.L("Nächster Befund", "Next finding"), Key.F4, N, () => NextFinding(+1), InText: true),
            new("prevFinding", "general", Loc.L("Vorheriger Befund", "Previous finding"), Key.F4, S, () => NextFinding(-1), InText: true),
            new("compare", "general", Loc.L("Mit anderer Aufnahme vergleichen", "Compare with another recording"), Key.F7, N, Compare),
            new("stopPlay", "general", Loc.L("Abspielen beenden", "Stop playback"), Key.Escape, N, StopPlay, Listed: false),
            new("shortcuts", "general", Loc.L("Alle Tastenkürzel", "All shortcuts"), Key.F1, N, () => ShortcutsWindow.Show(this, Cmds), InText: true),
            new("palette", "general", Loc.L("Befehlspalette", "Command palette"), Key.P, CS, () => CommandPalette.Show(this, Cmds), InText: true),
            // Nur in Menüs (ohne Tastenkürzel) – damit Menüleiste, Rechtsklick und Knöpfe dieselben Namen tragen
            new("rename", "general", Loc.L("Umbenennen …", "Rename …"), Key.None, N, Rename, Listed: false),
            new("preview", "copy", Loc.L("Vorschau im Browser", "Preview in browser"), Key.None, N, PreviewDocument, Listed: false),
            new("support", "copy", Loc.L("Support-Paket erstellen …", "Create support package …"), Key.None, N, StartSupportFlow, Listed: false),
            new("continueRec", "general", Loc.L("Aufnahme fortsetzen", "Continue recording"), Key.None, N, ContinueRecording, Listed: false),
            new("append", "general", Loc.L("Aufnahme anhängen …", "Append recording …"), Key.None, N, AppendSession, Listed: false),
            new("pack", "general", Loc.L("Als .steprec packen …", "Pack as .steprec …"), Key.None, N, () => { if (_view.Session.ReadOnly) Msg.Show(Loc.L("Das ist bereits ein .steprec-Archiv.", "This already is a .steprec archive.")); else PackDialog.Show(this, _path); }, Listed: false),
            new("showFolder", "general", Loc.L("Aufnahmeordner zeigen", "Show recording folder"), Key.None, N, () => Shell.Reveal(_view.Session.Path), Listed: false),
            new("aiConnect", "general", Loc.L("Mit KI verbinden …", "Connect to AI …"), Key.None, N, () => AiConnect.Show(this), Listed: false),
            new("settings", "general", Loc.L("Einstellungen …", "Settings …"), Key.None, N, () => UiApp.Instance.ShowStart("settings"), Listed: false),
            new("history", "general", Loc.L("Bearbeitungsstände …", "Edit history …"), Key.None, N, ShowHistory, Listed: false),
            new("copyGuide", "copy", Loc.L("Ganze Anleitung kopieren", "Copy whole guide"), Key.None, N, CopyWholeGuide, Listed: false),
            new("copyAllMarkdown", "copy", Loc.L("Alle Schritte als Markdown kopieren", "Copy all steps as Markdown"), Key.M, CS, CopyAllMarkdown),
            new("copyBugReport", "copy", Loc.L("Als Fehlerbericht kopieren (Markdown)", "Copy as bug report (Markdown)"), Key.None, N, CopyBugReport, Listed: false),
            new("saveClip", "copy", Loc.L("Kurzen Clip speichern (GIF) …", "Save short clip (GIF) …"), Key.None, N, SaveClip, Listed: false),
            new("insertFile", "steps", Loc.L("Bild aus Datei …", "Image from file …"), Key.None, N, () => { if (PickImageFile() is { } b) using (b) InsertManual(b); }, Listed: false),
            new("insertClip", "steps", Loc.L("Bild aus der Zwischenablage", "Image from clipboard"), Key.None, N, () => { if (ClipboardImage() is { } b) using (b) InsertManual(b); else Msg.Show(Loc.L("In der Zwischenablage ist kein Bild.", "There is no image on the clipboard.")); }, Listed: false),
            new("insertShot", "steps", Loc.L("Bildschirmfoto in 3 Sekunden …", "Screenshot in 3 seconds …"), Key.None, N, () => CaptureAfterCountdown(b => InsertManual(b)), Listed: false),
            new("deleteInserted", "steps", Loc.L("Eingefügten Schritt löschen", "Delete inserted step"), Key.None, N, () => DeleteManual(SelSteps), Listed: false),
            new("chapter", "steps", Loc.L("Kapitel hier beginnen", "Start chapter here"), Key.None, N, () => { if (Sel is { } s) ToggleChapter(s.Anchor, s.ChapterTitle == null); }, Listed: false),
            new("cropRemove", "image", Loc.L("Zuschnitt entfernen", "Remove crop"), Key.None, N, () => { if (Sel is { } s) RemoveCrop(s); }, Listed: false),
            new("cropAll", "image", Loc.L("Zuschnitt auf alle Schritte übertragen", "Apply crop to all steps"), Key.None, N, CropToAll, Listed: false),
            new("replaceShot", "image", Loc.L("Bildschirmfoto in 3 Sekunden …", "Screenshot in 3 seconds …"), Key.None, N, () => { if (Sel is { } s) CaptureAfterCountdown(b => ReplaceImage(s, b)); }, Listed: false),
            new("replaceFile", "image", Loc.L("Bild aus Datei …", "Image from file …"), Key.None, N, () => { if (Sel is { } s && PickImageFile() is { } b) using (b) ReplaceImage(s, b); }, Listed: false),
            new("replaceClip", "image", Loc.L("Bild aus der Zwischenablage", "Image from clipboard"), Key.None, N, () => { if (Sel is not { } s) return; if (ClipboardImage() is { } b) using (b) ReplaceImage(s, b); else Msg.Show(Loc.L("In der Zwischenablage ist kein Bild.", "There is no image on the clipboard.")); }, Listed: false),
            new("restoreImage", "image", Loc.L("Originalbild wiederherstellen", "Restore original image"), Key.None, N, () => { if (Sel is { } s) Change(e => StepEdit(e, s.Anchor).Remove("frameChoice"), refill: false); }, Listed: false),
            new("clearDrawings", "image", Loc.L("Alle Zeichnungen dieses Bilds löschen", "Delete all drawings on this image"), Key.None, N, () => { _selAnn = null; DeleteSelectedOrAll(); }, Listed: false),
            new("zoomIn", "image", Loc.L("Vergrößern", "Zoom in"), Key.None, N, () => _zoom.ZoomIn(), Listed: false),
            new("zoomOut", "image", Loc.L("Verkleinern", "Zoom out"), Key.None, N, () => _zoom.ZoomOut(), Listed: false),
            new("zoomFit", "image", Loc.L("Einpassen", "Fit"), Key.None, N, () => _zoom.FitNow(), Listed: false),
            new("zoom100", "image", Loc.L("Originalgröße (100 %)", "Actual size (100 %)"), Key.None, N, () => _zoom.Actual(), Listed: false),
            new("piiScan", "general", Loc.L("Persönliche Daten in Bildern suchen …", "Find personal data in images …"), Key.None, N, ScanImagesForPersonalData, Listed: false),
            new("redactText", "general", Loc.L("Text überall schwärzen …", "Redact text everywhere …"), Key.None, N, RedactText, Listed: false),
            new("cleanup", "general", Loc.L("Aufräumen …", "Clean up …"), Key.None, N, ShowCleanup, Listed: false),
            new("overview", "general", Loc.L("Übersicht und Vollständigkeit …", "Overview and completeness …"), Key.None, N, ShowOverview, Listed: false),
            new("finalRedact", "general", Loc.L("Endgültig schwärzen …", "Redact permanently …"), Key.None, N, FinalRedact, Listed: false),
            new("help", "general", Loc.L("Hilfe und Info", "Help and info"), Key.None, N, () => UiApp.Instance.ShowStart("help"), Listed: false),
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
        var w = new DialogWindow { Title = Loc.L("Tastenkürzel", "Keyboard shortcuts"), Width = 640, Height = Math.Min(760, Theme.ActiveWorkArea().Height - 60), ResizeMode = ResizeMode.CanResize, ShowInTaskbar = false, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var rows = new List<(string group, string label, string keys)>();
        foreach (var (gid, groupLabel) in ViewerCommands.Groups())
            foreach (var c in commands.Where(c => c.Group == gid && c.Listed))
                rows.Add((groupLabel, c.Label, c.Gesture));
        var rec = Loc.L("Während der Aufnahme (systemweit)", "While recording (system-wide)");
        var s = AppSettings.Load();
        foreach (var a in HotkeyActions.All)
            rows.Add((rec, Loc.L(a.De, a.En), Loc.Keys(s.Hotkey(a.Id)) is { Length: > 0 } k ? k : Loc.L("nicht belegt", "not assigned")));

        var search = new Wpf.Ui.Controls.TextBox
        {
            PlaceholderText = Loc.L("Kürzel oder Befehl suchen", "Search shortcut or command"), Margin = new Thickness(0, 0, 0, 12),
            Icon = new Wpf.Ui.Controls.FontIcon { Glyph = Theme.ISearch, FontFamily = Theme.Icons, FontSize = 14 }
        };
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
                    list.Children.Add(U.Text(group, Theme.FsBody, true, margin: new Thickness(0, last == null ? 0 : 18, 0, 6)));
                    last = group;
                }
                // wie auf der Hilfe-Seite: Befehl links, Tastenkappe rechts
                var dock = new DockPanel { Margin = new Thickness(0, 2, 12, 2) };
                var k = Shortcuts.Keycap(keys);
                DockPanel.SetDock(k, Dock.Right); dock.Children.Add(k);
                dock.Children.Add(new TextBlock { Text = label, FontSize = Theme.FsHint, Foreground = Theme.Fg2, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 12, 3) });
                list.Children.Add(dock);
            }
            if (last == null) list.Children.Add(U.Text(Loc.L("Kein Treffer.", "No match."), Theme.FsHint, fg: Theme.Muted));
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
