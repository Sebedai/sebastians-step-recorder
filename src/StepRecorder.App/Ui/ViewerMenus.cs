using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Menüs des Editors: Menüleiste (Datei · Bearbeiten · Schritt · Bild · Ansicht · Prüfen · Hilfe), Befehlsknöpfe
/// und Rechtsklick-Menüs. Namen, Kürzel und Aktionen kommen aus der Befehlstabelle (<see cref="BuildCommands"/>),
/// die Symbole aus <see cref="Glyphs"/> – jeder Befehl heißt überall gleich.
/// </summary>
sealed partial class ViewerWindow
{
    RadioButton[] _langBtns = null!, _filterBtns = null!;
    Menu? _menuBar;

    static readonly Dictionary<string, string> Glyphs = new()
    {
        ["pageView"] = Theme.IDoc, ["print"] = Theme.IPrint, ["newRecording"] = Theme.IRecord, ["open"] = Theme.IOpen, ["library"] = Theme.IHistory, ["rename"] = Theme.IEdit, ["preview"] = Theme.IEye, ["export"] = Theme.IExport,
        ["support"] = Theme.IWrench, ["continueRec"] = Theme.IRecord, ["append"] = Theme.IAdd, ["pack"] = Theme.ISave, ["showFolder"] = Theme.IFolder, ["aiConnect"] = Theme.IRobot,
        ["settings"] = Theme.ISettings, ["close"] = Theme.IClose, ["undo"] = Theme.IUndo, ["redo"] = Theme.IRedo, ["history"] = Theme.IHistory, ["copyImage"] = Theme.ICopy,
        ["copyGuide"] = Theme.IDoc, ["find"] = Theme.ISearch, ["editText"] = Theme.IEdit, ["comment"] = Theme.IComment, ["chapter"] = Theme.IDoc, ["hide"] = Theme.IEye,
        ["moveUp"] = Theme.IArrowUp, ["moveDown"] = Theme.IArrowDown, ["insertStep"] = Theme.IText, ["insertFile"] = Theme.IOpen, ["insertClip"] = Theme.ICopy, ["insertShot"] = Theme.ICamera,
        ["deleteInserted"] = Theme.IDelete, ["toolSelect"] = Theme.IPointer, ["toolArrow"] = Theme.IArrow, ["toolRect"] = Theme.IRect, ["toolHighlight"] = Theme.IHighlight,
        ["toolText"] = Theme.IText, ["toolRedact"] = Theme.IRedact, ["toolPixelate"] = Theme.IPixelate, ["toolCrop"] = Theme.ICrop, ["replaceShot"] = Theme.ICamera,
        ["replaceFile"] = Theme.IOpen, ["replaceClip"] = Theme.ICopy, ["restoreImage"] = Theme.IUndo, ["deleteMark"] = Theme.IDelete, ["saveImage"] = Theme.ISave,
        ["beforeAfter"] = Theme.ICompare, ["stepMarks"] = Theme.IMouse, ["play"] = Theme.IPlay, ["lightbox"] = Theme.IFullScreen, ["zoomIn"] = Theme.IZoomIn, ["zoomOut"] = Theme.IZoomOut,
        ["zoomFit"] = Theme.IFit, ["zoom100"] = Theme.IActual, ["review"] = Theme.IChecklist, ["quality"] = Theme.ICheck, ["piiScan"] = Theme.ISearch, ["redactStep"] = Theme.IShield,
        ["overview"] = Theme.IInfo, ["finalRedact"] = Theme.IWarn, ["shortcuts"] = Theme.IKeyboard, ["palette"] = Theme.ISearch, ["help"] = Theme.IHelp, ["prev"] = Theme.IPrev, ["next"] = Theme.INext,
        ["compare"] = Theme.ICompare, ["bookmark"] = Theme.IStar, ["snippet"] = Theme.ISnippet,
    };

    /// <summary>Anzeige-Kürzel: aus der Tabelle; Zoom wird vom Bildbereich selbst behandelt.</summary>
    string? GestureOf(string id)
    {
        var c = Cmd(id);
        if (c.Gesture.Length > 0) return c.Gesture;
        return id switch { "zoomIn" => Loc.Keys("Ctrl+Plus"), "zoomOut" => Loc.Keys("Ctrl+Minus"), "zoomFit" => Loc.Keys("Ctrl+0"), "zoom100" => Loc.Keys("Ctrl+1"), _ => null };
    }

    void Run(string id) { try { Cmd(id).Run(); } catch (Exception ex) { UiApp.ShowError(ex); } }

    static string Trim(string label) => label.EndsWith(" …") ? label[..^2] : label;

    /// <summary>Tooltip „Name (Kürzel)“.</summary>
    string TipOf(string id) => GestureOf(id) is { } g ? $"{Trim(Cmd(id).Label)} ({g})" : Trim(Cmd(id).Label);

    /// <summary>Menüeintrag zu einem Befehl; <paramref name="label"/> ersetzt den Namen (z. B. „Einblenden“ statt „Ausblenden“).</summary>
    MenuItem Item(string id, string? label = null, bool enabled = true)
    {
        var mi = U.Mi(label ?? Cmd(id).Label, GestureOf(id), () => Run(id), Glyphs.GetValueOrDefault(id));
        mi.IsEnabled = enabled;
        return mi;
    }

    /// <summary>Untermenü mit Symbol.</summary>
    static MenuItem Sub(string label, string? glyph, params object[] items)
    {
        var mi = U.Mi(label, null, null, glyph);
        foreach (var it in items) if (it != null) mi.Items.Add(it);
        return mi;
    }

    static MenuItem Check(string label, bool on, Action a)
    {
        var mi = U.Mi(label, null, a);
        mi.IsCheckable = true; mi.IsChecked = on; mi.Icon = null;   // Häkchen steht in der Symbolspalte
        return mi;
    }

    static Separator Sep() => new();

    // =====================================================================================
    // Menüleiste
    // =====================================================================================

    Menu BuildMenuBar()
    {
        var bar = _menuBar = new Menu { VerticalAlignment = VerticalAlignment.Center, Background = System.Windows.Media.Brushes.Transparent };
        void Top(string header, Func<List<object>> items)
        {
            var top = new MenuItem { Header = header };
            top.Items.Add(new MenuItem { Header = "…" });   // Platzhalter, damit sich das Menü öffnen lässt
            top.SubmenuOpened += (_, e) =>
            {
                if (!ReferenceEquals(e.OriginalSource, top)) return;
                InlineCommit(now: true);
                top.Items.Clear();
                foreach (var it in items()) top.Items.Add(it);
            };
            bar.Items.Add(top);
        }
        Top(Loc.L("_Datei", "_File"), FileMenu);
        Top(Loc.L("_Bearbeiten", "_Edit"), EditMenu);
        Top(Loc.L("_Schritt", "_Step"), StepMenuItems);
        Top(Loc.L("B_ild", "_Image"), ImageMenu);
        Top(Loc.L("_Ansicht", "_View"), ViewMenu);
        Top(Loc.L("_Prüfen", "_Review"), ReviewMenu);
        Top(Loc.L("_Hilfe", "_Help"), () => new() { Item("palette"), Item("shortcuts"), Item("aiConnect"), Sep(), Item("help") });
        return bar;
    }

    List<object> FileMenu()
    {
        var recent = Sub(Loc.L("Zuletzt geöffnet", "Recently opened"), Theme.IHistory);
        foreach (var p in AppSettings.Load().RecentSessions.Where(Session.LooksLikeSession).Where(p => !string.Equals(Path.GetFullPath(p).TrimEnd('\\'), Path.GetFullPath(_path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)).Take(8))
        {
            string t = Json.ReadObject(Path.Combine(p, "manifest.json"))?.Str("title") is { Length: > 0 } mt ? mt : Path.GetFileName(p);
            string path = p;
            var mi = U.Mi(t, null, () => UiApp.Instance.OpenViewer(path)); mi.ToolTip = p;
            recent.Items.Add(mi);
        }
        if (recent.Items.Count == 0) recent.IsEnabled = false;
        bool ro = _view.Session.ReadOnly;
        var formats = Sub(Loc.L("Exportieren als", "Export as"), null, Item("repeatExport"), Sep());
        foreach (var (f, label) in new[]
        {
            ("html", "HTML …"), ("md", "Markdown …"), ("docx", "Word (DOCX) …"), ("pdf", "PDF …"), ("pptx", "PowerPoint …"),
            ("tour", Loc.L("Klick-Tour …", "Click tour …")), ("bugreport", Loc.L("Fehlerbericht …", "Bug report …")), ("gif", Loc.L("GIF-Animation …", "GIF animation …")), ("mp4", Loc.L("Video (MP4) …", "Video (MP4) …"))
        })
            if (Exporter.IsKnownFormat(f)) { string ff = f; formats.Items.Add(U.Mi(label, null, () => ShowExportDialog(ff))); }
        formats.Items.Add(Sep());
        formats.Items.Add(U.Mi(Loc.L("KI-Paket …", "AI package …"), null, () => ShowExportDialog("ki"), Theme.IRobot));
        formats.Items.Add(U.Mi(Loc.L("AutoHotkey-Skript …", "AutoHotkey script …"), null, () => ShowExportDialog("ahk"), Theme.IScript));
        formats.Items.Add(U.Mi(Loc.L("UI-Test-Entwurf (FlaUI, pywinauto, Playwright) …", "UI test draft (FlaUI, pywinauto, Playwright) …"), null, () => ShowExportDialog(LastTestFormat()), Theme.IChecklist));
        formats.Items.Add(U.Mi(Loc.L("Testbarkeit prüfen …", "Check testability …"), null, ShowTestability, Theme.ICheck));
        formats.Items.Add(Sep());
        formats.Items.Add(Item("support"));
        // kurz halten (höchstens ~13 Einträge, sonst scrollt das Menü): Verwandtes in Untermenüs
        var more = Sub(Loc.L("Weiter aufnehmen", "Continue recording"), Theme.IRecord, Item("continueRec", enabled: !ro), Item("append", enabled: !ro));
        return new()
        {
            Item("newRecording"), Item("open"), Item("library"), recent, Sep(),
            Item("preview"), Item("print"), Item("export"), formats, Sep(),
            Item("rename", enabled: !ro), more, Item("pack", enabled: !ro), Item("showFolder"), Sep(),
            Item("settings"), Item("close"),
        };
    }

    List<object> EditMenu() => new()
    {
        Item("undo", enabled: _undo.Count > 0), Item("redo", enabled: _redo.Count > 0), Item("history"), Sep(),
        Item("copyImage", enabled: _current != null), Item("copyImageText", enabled: Sel != null), Item("copyText", enabled: Sel != null), Item("copyMarkdown", enabled: Sel != null),
        Item("copyOriginal", enabled: Sel != null), Item("copyGuide"), Item("copyAllMarkdown"), Item("copyBugReport"), Sep(),
        Item("find"), Item("replace"),
    };

    /// <summary>Menü „Schritt“ – zugleich Grundlage des Rechtsklick-Menüs.</summary>
    List<object> StepMenuItems()
    {
        var s = Sel;
        bool ro = _view.Session.ReadOnly, edit = s != null && !ro;
        return new()
        {
            Item("editText", enabled: edit), Item("comment", enabled: edit), Item("snippet", enabled: edit), Sep(),
            Item("bookmark", s?.Bookmarked == true ? Loc.L("Lesezeichen entfernen", "Remove bookmark") : Loc.L("Lesezeichen setzen", "Set bookmark"), edit),
            ImportanceMenu(s, edit),
            Item("chapter", s?.ChapterTitle != null ? Loc.L("Kapitel entfernen", "Remove chapter") : null, edit),
            ChapterSubMenu(edit),
            Item("hide", s?.Hidden == true ? Loc.L("Einblenden", "Show") : Loc.L("Ausblenden", "Hide"), edit),
            Item("moveUp", enabled: edit), Item("moveDown", enabled: edit), Sep(),
            Item("merge", enabled: !ro && _list.SelectedItems.Count > 1), Item("unmerge", enabled: edit), Sep(),
            ReRecordSubMenu(ro), Sep(),
            InsertSubMenu(Loc.L("Schritt einfügen", "Insert step"), !ro), SectionSubMenu(!ro),
            Item("deleteInserted", enabled: !ro && SelSteps.Any(x => x.Manual)),
        };
    }

    MenuItem ImportanceMenu(ViewStep? s, bool edit)
    {
        var sub = Sub(Loc.L("Wichtigkeit", "Importance"), null);
        foreach (var (imp, name) in new[] { ("primary", Loc.L("Hauptschritt", "Main step")), ("secondary", Loc.L("Nebenschritt", "Secondary step")), ("trace", Loc.L("Spur", "Trace")) })
        {
            string target = imp;
            sub.Items.Add(Check(name, s?.Importance == imp, () => BulkEdit(SelSteps.Where(x => !x.Manual), se => se["importance"] = target)));
        }
        sub.Items.Add(Sep());
        sub.Items.Add(Item("moreImportant")); sub.Items.Add(Item("lessImportant"));
        sub.IsEnabled = edit && s?.Manual == false;
        return sub;
    }

    MenuItem InsertSubMenu(string label, bool enabled)
    {
        var sub = Sub(label, Theme.IAdd, Item("insertStep", Loc.L("Textschritt", "Text step")), Sep(), Item("insertFile"), Item("insertClip"), Item("insertShot"));
        sub.IsEnabled = enabled;
        return sub;
    }

    MenuItem ReplaceSubMenu(ViewStep? s, bool enabled)
    {
        var sub = Sub(s is { Manual: true, PreFrame: null } ? Loc.L("Bild hinzufügen", "Add image") : Loc.L("Bild ersetzen", "Replace image"), Theme.ICamera, Item("replaceShot"), Item("replaceFile"), Item("replaceClip"));
        sub.IsEnabled = enabled;
        return sub;
    }

    static readonly (string id, string tool)[] Tools =
    {
        ("toolSelect", "select"), ("toolArrow", "arrow"), ("toolLine", "line"), ("toolFreehand", "freehand"), ("toolRect", "rect"), ("toolEllipse", "ellipse"), ("toolHighlight", "highlight"),
        ("toolText", "text"), ("toolBadge", "badge"), ("toolSpotlight", "spotlight"), ("toolMagnifier", "magnifier"), ("toolRedact", "redact"), ("toolPixelate", "pixelate"),
    };

    List<object> ImageMenu()
    {
        var s = Sel;
        bool img = _current != null, ro = _view.Session.ReadOnly, edit = img && !ro;
        var tools = Sub(Loc.L("Werkzeug", "Tool"), Theme.IPointer);
        foreach (var (id, tool) in Tools)
        {
            var mi = Item(id, enabled: edit);
            if (_tool == tool) mi.Icon = U.Icon(Theme.ICheck, 16, Theme.AccentText);
            tools.Items.Add(mi);
        }
        bool cropped = s != null && _view.CropFor(s) != null;
        return new()
        {
            tools, Sep(),
            Item("toolCrop", enabled: edit), Item("cropRemove", enabled: cropped && !ro), Item("cropAll", enabled: cropped && !ro), Sep(),
            ReplaceSubMenu(s, s != null && !ro), Item("restoreImage", enabled: s?.ImageReplaced == true && !ro), Sep(),
            Item("markTarget", enabled: s != null && !ro), Item("deleteMark", enabled: _selAnn != null && !ro), Item("clearDrawings", enabled: img && !ro && _currentMeta != null && AnnotationsFor(_currentMeta.FrameId).Any()), Sep(),
            Item("saveImage", enabled: img), Item("saveClip", enabled: s != null),
        };
    }

    List<object> ViewMenu()
    {
        var disp = Sub(Loc.L("Bildausschnitt", "Image area"), Theme.IMonitor);
        string[] dispNames = { Loc.L("Ganzer Bildschirm", "Full screen"), Loc.L("Anwendungsfenster", "Application window"), Loc.L("Bereich um die Aktion", "Area around the action") };
        for (int i = 0; i < 3; i++) { int k = i; disp.Items.Add(Check(dispNames[i], _dispBox.SelectedIndex == i, () => _dispBox.SelectedIndex = k)); }
        var lang = Sub(Loc.L("Textsprache", "Text language"), Theme.IGlobe,
            Check(Loc.L("Deutsch", "German"), _view.Language == "de", () => _langBtns[0].IsChecked = true),
            Check(Loc.L("Englisch", "English"), _view.Language == "en", () => _langBtns[1].IsChecked = true));
        var zoom = Sub(Loc.L("Zoom", "Zoom"), Theme.IZoomIn, Item("zoomIn"), Item("zoomOut"), Item("zoomFit"), Item("zoom100"));
        return new()
        {
            Check(Loc.L("Seitenansicht", "Page view"), _pageMode, TogglePageMode), Item("beforeAfter", enabled: Sel != null && !_pageMode), disp, MarksMenu(), Sep(),
            Check(Loc.L("Schritte", "Steps"), _filterBtns[0].IsChecked == true, () => _filterBtns[0].IsChecked = true),
            Check(Loc.L("Mit Spur", "With trace"), _filterBtns[1].IsChecked == true, () => _filterBtns[1].IsChecked = true),
            Check(Loc.L("Nur Lesezeichen", "Bookmarks only"), _bookmarkToggle.IsChecked == true, () => _bookmarkToggle.IsChecked = _bookmarkToggle.IsChecked != true), Sep(),
            lang, Sep(),
            Item("play", _play.IsEnabled ? Loc.L("Abspielen beenden", "Stop playback") : null), Item("lightbox", enabled: _current != null), zoom, Sep(),
            Item("compare"),
        };
    }

    /// <summary>Untermenü „Mausmarken“: für alle Schritte je Art, dazu nur dieser Schritt.</summary>
    MenuItem MarksMenu() => Sub(Loc.L("Mausmarken", "Mouse marks"), Theme.IMouse, MarksItems().ToArray());

    List<object> MarksItems()
    {
        var items = new List<object>();
        var head = U.Text(Loc.L("Alle Schritte", "All steps"), Theme.FsCaption, true, Theme.Muted);
        items.Add(new MenuItem { Header = head, IsHitTestVisible = false, Focusable = false });
        var all = _view.MarksFor(null);
        foreach (var (key, label) in MarkKeys) { bool on = MarkOf(all, key); string k = key; items.Add(Check(label, on, () => SetMark(null, k, !on))); }
        items.Add(MarkStyleMenu());
        if (Sel is { } s)
        {
            items.Add(Sep());
            var cur = _view.MarksFor(s);
            items.Add(Item("stepMarks", cur.Any ? Loc.F("Bei Schritt {0} ausblenden", "Hide for step {0}", s.Number) : Loc.F("Bei Schritt {0} einblenden", "Show for step {0}", s.Number)));
            if (StepMarksEdit(s) != null) items.Add(U.Mi(Loc.L("Schritt wieder wie alle", "Step like all others again"), null, () => Change(e => StepEdit(e, s.Anchor).Remove("auto"), refill: false)));
        }
        return items;
    }

    List<object> ReviewMenu()
    {
        bool ro = _view.Session.ReadOnly;
        var review = Item("review", _view.UnreviewedCount > 0 ? Loc.F("Durchsicht der Eingaben ({0} offen) …", "Review inputs ({0} open) …", _view.UnreviewedCount) : Loc.L("Durchsicht der Eingaben …", "Review inputs …"));
        return new()
        {
            review, Sep(),
            Item("quality"), Item("nextFinding"), Item("prevFinding"), Sep(),
            Item("piiScan", enabled: !ro), Item("redactText", enabled: !ro), Item("redactStep", enabled: Sel != null && !ro), Item("generalize", enabled: !ro), Sep(),
            Item("cleanup", enabled: !ro), Item("overview"), Item("translations"), Sep(),
            Item("finalRedact", enabled: !ro),
        };
    }

    // =====================================================================================
    // Rechtsklick in der Schrittliste
    // =====================================================================================

    void StepMenu()
    {
        var s = Sel; if (s == null) return;
        bool ro = _view.Session.ReadOnly;
        var m = U.MenuScope(new ContextMenu { Placement = PlacementMode.MousePoint });
        void Add(params object[] items) { foreach (var it in items) m.Items.Add(it); }
        if (_list.SelectedItems.Count > 1)
        {
            var multi = SelSteps;
            Add(U.Mi(Loc.F("{0} Schritte ausblenden", "Hide {0} steps", multi.Count), null, () => BulkEdit(multi, se => se["hidden"] = true), Theme.IEye),
                U.Mi(Loc.F("{0} Schritte einblenden", "Show {0} steps", multi.Count), null, () => BulkEdit(multi, se => se.Remove("hidden"))), Sep(),
                ImportanceMenu(s, !ro), Item("chapter", Loc.L("Kapitel vor dem ersten beginnen", "Start chapter before the first"), !ro), Item("merge", enabled: !ro), Item("rerecord", enabled: !ro),
                Sub(Loc.L("Bilder in der Anleitung", "Images in the guide"), Theme.ICamera, U.Mi(Loc.L("Bilder zeigen", "Show images"), null, () => BulkEdit(multi, se => se.Remove("showImage"))), U.Mi(Loc.L("Bilder weglassen", "Omit images"), null, () => BulkEdit(multi, se => se["showImage"] = false))), Sep(),
                U.Mi(Loc.F("Hinweis zu {0} Schritten hinzufügen …", "Add note to {0} steps …", multi.Count), null, () => AddNoteToMany(multi), Theme.IComment));
            if (multi.Any(x => x.Manual)) Add(Sep(), Item("deleteInserted"));
            if (ro) foreach (var it in m.Items.OfType<MenuItem>()) it.IsEnabled = false;
            m.IsOpen = true;
            return;
        }
        bool cropped = _view.CropFor(s) != null;
        var image = Sub(Loc.L("Bild", "Image"), Theme.ICamera, Item("lightbox"), Item("toolCrop", enabled: !ro));
        if (cropped) { image.Items.Add(Item("cropRemove", enabled: !ro)); image.Items.Add(Item("cropAll", enabled: !ro)); }
        image.Items.Add(Sep());
        image.Items.Add(ReplaceSubMenu(s, !ro));
        if (s.ImageReplaced) image.Items.Add(Item("restoreImage", enabled: !ro));
        var copy = Sub(Loc.L("Kopieren", "Copy"), Theme.ICopy, Item("copyImage"), Item("copyImageText"), Item("copyText"), Item("copyMarkdown"));
        Add(Item("editText", enabled: !ro), Item("comment", enabled: !ro), Sep(),
            Item("bookmark", s.Bookmarked ? Loc.L("Lesezeichen entfernen", "Remove bookmark") : Loc.L("Lesezeichen setzen", "Set bookmark"), !ro),
            ImportanceMenu(s, !ro),
            Item("chapter", s.ChapterTitle != null ? Loc.L("Kapitel entfernen", "Remove chapter") : null, !ro),
            Item("hide", s.Hidden ? Loc.L("Einblenden", "Show") : Loc.L("Ausblenden", "Hide"), !ro),
            Sub(Loc.L("Verschieben", "Move"), Theme.IMove, Item("moveUp", enabled: !ro), Item("moveDown", enabled: !ro)), ReRecordSubMenu(ro), Sep(),
            copy, image, InsertSubMenu(Loc.L("Schritt danach einfügen", "Insert step after"), !ro), Sep(),
            Item("redactStep", enabled: !ro));
        if (s.Manual) Add(Item("deleteInserted", enabled: !ro));
        m.IsOpen = true;
    }

    /// <summary>Befehlsknopf der Kopfzeile (Name, Symbol und Tooltip aus der Befehlstabelle).</summary>
    Button CmdButton(string id, string? text, int labelLevel = 0, string style = "Ghost")
    {
        var b = U.Btn(Glyphs.GetValueOrDefault(id), text, () => Run(id), TipOf(id), style);
        b.Margin = new Thickness(2, 0, 2, 0);
        if (labelLevel > 0 && b.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock t) _labels.Add((t, labelLevel));
        return b;
    }

    /// <summary>Knopf mit Menü (Pfeil rechts).</summary>
    Button MenuCmdButton(string glyph, string text, string tip, int labelLevel, Func<List<object>> items)
    {
        var content = U.MenuContent(glyph, text);
        var b = U.Make("Ghost").Named(string.IsNullOrEmpty(text) ? tip : text, tip); b.Content = content; b.ToolTip = tip; b.Margin = new Thickness(2, 0, 2, 0);
        b.Click += (_, _) =>
        {
            var m = U.MenuScope(new ContextMenu { PlacementTarget = b, Placement = PlacementMode.Bottom });
            foreach (var it in items()) m.Items.Add(it);
            m.IsOpen = true;
        };
        if (labelLevel > 0 && content.Children.Count > 1 && content.Children[1] is TextBlock t) _labels.Add((t, labelLevel));
        return b;
    }

    /// <summary>Nur für Entwickler-Schnappschüsse: jedes Menü der Menüleiste und das Rechtsklick-Menü öffnen und abbilden.</summary>
    internal void DevMenus()
    {
        void Flush() { var until = DateTime.Now.AddMilliseconds(450); while (DateTime.Now < until) Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { }); }
        if (_menuBar == null) return;
        if (_list.Items.Count > 1) _list.SelectedIndex = 1;
        Flush();
        foreach (var top in _menuBar.Items.OfType<MenuItem>())
        {
            top.IsSubmenuOpen = true; Flush();
            DevSnapshots.SaveOpenPopups(((string)top.Header).Replace("_", ""));
            top.IsSubmenuOpen = false; Flush();
        }
        StepMenu(); Flush();
        DevSnapshots.SaveOpenPopups("Context");
        foreach (var cm in OpenContextMenus()) cm.IsOpen = false;
        Flush();
    }

    static IEnumerable<ContextMenu> OpenContextMenus() => PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>()
        .Select(s => s.RootVisual).OfType<FrameworkElement>().Select(r => System.Windows.Media.VisualTreeHelper.GetChildrenCount(r) > 0 ? System.Windows.Media.VisualTreeHelper.GetChild(r, 0) : null)
        .OfType<FrameworkElement>().Select(FindMenu).OfType<ContextMenu>().ToList();

    static ContextMenu? FindMenu(DependencyObject d)
    {
        if (d is ContextMenu cm) return cm;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++) if (FindMenu(System.Windows.Media.VisualTreeHelper.GetChild(d, i)) is { } r) return r;
        return null;
    }

    void OpenSessionDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.L("Aufnahme (manifest.json;*.steprec)|manifest.json;*.steprec", "Recording (manifest.json;*.steprec)|manifest.json;*.steprec"), InitialDirectory = AppPaths.DefaultSessionsFolder() };
        if (dlg.ShowDialog(this) != true) return;
        var p = dlg.FileName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(dlg.FileName)! : dlg.FileName;
        UiApp.Instance.OpenViewer(p);
    }

    /// <summary>„Exportieren …“: Exportdialog mit dem zuletzt gewählten Format (wie Strg+E).</summary>
    void ShowExport() => Run("export");

    /// <summary>Hinweis zum gewählten Schritt (K oder Einfg): Art und Text im Dialog.</summary>
    void AddNote()
    {
        var s = Sel; if (s == null) return;
        if (_list.SelectedItems.Count > 1) { AddNoteToMany(SelSteps); return; }
        var d = new InputDialog(Loc.L("Hinweis hinzufügen", "Add note"), "", null, kinds: true) { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        Change(e =>
        {
            var arr = e["comments"] as System.Text.Json.Nodes.JsonArray ?? new System.Text.Json.Nodes.JsonArray();
            var nc = new System.Text.Json.Nodes.JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = s.Anchor, ["kind"] = d.Kind, ["text"] = d.Value.Trim(), ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" };
            SetNoteKind(nc, d.Kind);
            arr.Add(nc);
            e["comments"] = arr;
        });
    }

    void AddNoteToMany(List<ViewStep> steps)
    {
        if (steps.Count == 0) return;
        var d = new InputDialog(Loc.F("Hinweis zu {0} Schritten hinzufügen", "Add note to {0} steps", steps.Count), "", null, kinds: true) { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        Change(e =>
        {
            var arr = e["comments"] as System.Text.Json.Nodes.JsonArray ?? new System.Text.Json.Nodes.JsonArray();
            foreach (var s in steps) arr.Add(new System.Text.Json.Nodes.JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = s.Anchor, ["kind"] = d.Kind, ["text"] = d.Value.Trim(), ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" });
            e["comments"] = arr;
        });
    }
}
