using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Menüs des Editors: Menüleiste (Datei · Bearbeiten · Schritt · Bild · Ansicht · Prüfen · Hilfe), Befehlsknöpfe
/// und Rechtsklick-Menüs. Namen, Symbole und Kürzel kommen aus <see cref="EditorCommands"/>; die Aktionen stehen hier
/// genau einmal (<see cref="BindCommands"/>).
/// </summary>
sealed partial class ViewerWindow
{
    readonly Dictionary<string, Action> _run = new();
    ComboBox _dispBox = null!;
    RadioButton[] _langBtns = null!, _filterBtns = null!;

    void BindCommands()
    {
        void B(string id, Action a) => _run[id] = a;
        void Step(string id, Action<ViewStep> a) => _run[id] = () => { if (Sel is { } s) a(s); };
        // Datei
        B("newRec", () => UiApp.Instance.ShowStart());
        B("open", OpenSessionDialog);
        B("rename", Rename);
        B("preview", PreviewDocument);
        B("export", ShowExport);
        B("support", StartSupportFlow);
        B("continueRec", ContinueRecording);
        B("append", AppendSession);
        B("showFolder", () => Process.Start("explorer.exe", $"\"{_view.Session.Path}\""));
        B("settings", () => UiApp.Instance.ShowStart("settings"));
        B("close", Close);
        // Bearbeiten
        B("undo", Undo); B("redo", Redo);
        B("copyImage", CopyImage); B("copyImageText", CopyImageWithText); B("copyText", CopyText);
        B("copyMarkdown", CopyMarkdown); B("copyOriginal", CopyOriginal); B("copyGuide", CopyWholeGuide);
        B("find", () => { _search.Focus(); _search.SelectAll(); });
        B("replace", ShowSearchReplace);
        // Schritt
        B("editText", FocusInlineTitle);
        B("addNote", AddNote);
        B("moreImportant", () => ChangeImportance(+1)); B("lessImportant", () => ChangeImportance(-1));
        Step("chapter", s => ToggleChapter(s.Anchor, s.ChapterTitle == null));
        B("hide", ToggleHidden);
        B("moveUp", () => Move(-1)); B("moveDown", () => Move(+1));
        B("insertText", () => InsertManual(null));
        B("insertFile", () => { if (PickImageFile() is { } b) using (b) InsertManual(b); });
        B("insertClip", () => { if (ClipboardImage() is { } b) using (b) InsertManual(b); else Msg.Show("In der Zwischenablage ist kein Bild."); });
        B("insertShot", () => CaptureAfterCountdown(b => InsertManual(b)));
        B("deleteInserted", () => DeleteManual(SelSteps));
        B("prevStep", () => { if (_list.SelectedIndex > 0) _list.SelectedIndex--; else if (_list.SelectedIndex == 0) ShowCover(); });
        B("nextStep", () => { if (_cover || _list.SelectedIndex < 0) { if (_list.Items.Count > 0) _list.SelectedIndex = 0; } else if (_list.SelectedIndex < _list.Items.Count - 1) _list.SelectedIndex++; });
        // Bild
        foreach (var (id, tool) in new[] { ("toolSelect", "select"), ("toolArrow", "arrow"), ("toolRect", "rect"), ("toolEllipse", "ellipse"), ("toolHighlight", "highlight"), ("toolText", "text"), ("toolBadge", "badge"), ("toolRedact", "redact"), ("toolPixelate", "pixelate"), ("crop", "crop") })
            B(id, () => SelectTool(tool));
        Step("cropRemove", RemoveCrop);
        B("cropAll", CropToAll);
        Step("replaceShot", s => CaptureAfterCountdown(b => ReplaceImage(s, b)));
        Step("replaceFile", s => { if (PickImageFile() is { } b) using (b) ReplaceImage(s, b); });
        Step("replaceClip", s => { if (ClipboardImage() is { } b) using (b) ReplaceImage(s, b); else Msg.Show("In der Zwischenablage ist kein Bild."); });
        Step("restoreImage", s => Change(e => StepEdit(e, s.Anchor).Remove("frameChoice"), refill: false));
        B("deleteDrawing", DeleteSelectedOrAll);
        B("clearDrawings", () => { _selAnn = null; DeleteSelectedOrAll(); });
        B("saveImage", SaveImage); B("saveClip", SaveClip);
        // Ansicht
        B("beforeAfter", ToggleBeforeAfter);
        B("stepMarks", ToggleStepMarks);
        B("play", TogglePlay);
        B("lightbox", OpenLightbox);
        B("zoomIn", () => _zoom.ZoomIn()); B("zoomOut", () => _zoom.ZoomOut()); B("zoomFit", () => _zoom.FitNow()); B("zoom100", () => _zoom.Actual());
        B("compare", Compare);
        // Prüfen
        B("review", ShowReview);
        B("piiScan", ScanImagesForPersonalData);
        B("redactText", RedactText);
        B("redactStep", RedactStep);
        B("generalize", Generalize);
        B("cleanup", ShowCleanup);
        B("overview", ShowOverview);
        B("finalRedact", FinalRedact);
        // Hilfe
        B("shortcuts", ShowShortcuts);
        B("help", () => UiApp.Instance.ShowStart("help"));
    }

    void Run(string id) { try { _run[id](); } catch (Exception ex) { UiApp.ShowError(ex); } }

    /// <summary>Menüeintrag zu einem Befehl; <paramref name="label"/> ersetzt den Namen (z. B. „Einblenden“ statt „Ausblenden“).</summary>
    MenuItem M(string id, string? label = null, bool enabled = true)
    {
        var c = EditorCommands.Get(id);
        var mi = U.Mi(label ?? c.Label, c.Gesture, () => Run(id), c.Glyph);
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

    Menu? _menuBar;

    Menu BuildMenuBar()
    {
        var bar = _menuBar = new Menu { VerticalAlignment = VerticalAlignment.Center, Background = System.Windows.Media.Brushes.Transparent };
        void Top(string header, Func<List<object>> items)
        {
            var top = new MenuItem { Header = header };
            top.Items.Add(new MenuItem { Header = "…" });   // Platzhalter, damit sich das Menü öffnen lässt
            Grid.SetIsSharedSizeScope(top, true);
            top.SubmenuOpened += (_, e) =>
            {
                if (!ReferenceEquals(e.OriginalSource, top)) return;
                InlineCommit(now: true);
                top.Items.Clear();
                foreach (var it in items()) top.Items.Add(it);
            };
            bar.Items.Add(top);
        }
        Top("_Datei", FileMenu);
        Top("_Bearbeiten", EditMenu);
        Top("_Schritt", StepMenuItems);
        Top("B_ild", ImageMenu);
        Top("_Ansicht", ViewMenu);
        Top("_Prüfen", ReviewMenu);
        Top("_Hilfe", () => new() { M("shortcuts"), M("help") });
        return bar;
    }

    List<object> FileMenu()
    {
        var recent = Sub("Zuletzt geöffnet", Theme.IHistory);
        foreach (var p in AppSettings.Load().RecentSessions.Where(Session.LooksLikeSession).Where(p => !string.Equals(Path.GetFullPath(p).TrimEnd('\\'), Path.GetFullPath(_path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)).Take(8))
        {
            string t = Json.ReadObject(Path.Combine(p, "manifest.json"))?.Str("title") is { Length: > 0 } mt ? mt : Path.GetFileName(p);
            string path = p;
            var mi = U.Mi(t, null, () => UiApp.Instance.OpenViewer(path)); mi.ToolTip = p;
            recent.Items.Add(mi);
        }
        if (recent.Items.Count == 0) recent.IsEnabled = false;
        var formats = Sub("Exportieren als", null,
            U.Mi("HTML …", null, () => ShowExportDialog("html")), U.Mi("Markdown …", null, () => ShowExportDialog("md")), U.Mi("Word (DOCX) …", null, () => ShowExportDialog("docx")),
            U.Mi("PDF …", null, () => ShowExportDialog("pdf")), U.Mi("GIF-Animation …", null, () => ShowExportDialog("gif")), Sep(),
            U.Mi("KI-Paket …", null, () => ShowExportDialog("ki")), U.Mi("AutoHotkey-Skript …", null, () => ShowExportDialog("ahk")));
        bool ro = _view.Session.ReadOnly;
        return new()
        {
            M("newRec"), M("open"), recent, Sep(),
            M("rename", enabled: !ro), Sep(),
            M("preview"), M("export"), formats, M("support"), Sep(),
            M("continueRec", enabled: !ro), M("append", enabled: !ro), Sep(),
            M("showFolder"), M("settings"), Sep(),
            M("close"),
        };
    }

    List<object> EditMenu() => new()
    {
        M("undo", enabled: _undo.Count > 0), M("redo", enabled: _redo.Count > 0), Sep(),
        M("copyImage", enabled: _current != null), M("copyImageText", enabled: Sel != null), M("copyText", enabled: Sel != null), M("copyMarkdown", enabled: Sel != null), M("copyOriginal", enabled: Sel != null), M("copyGuide"), Sep(),
        M("find"), M("replace"),
    };

    /// <summary>Menü „Schritt“ – zugleich Grundlage des Rechtsklick-Menüs.</summary>
    List<object> StepMenuItems()
    {
        var s = Sel;
        bool has = s != null, ro = _view.Session.ReadOnly, edit = has && !ro;
        return new()
        {
            M("editText", enabled: edit), M("addNote", enabled: edit), Sep(),
            ImportanceMenu(s, edit),
            M("chapter", s?.ChapterTitle != null ? "Kapitel entfernen" : null, edit),
            M("hide", s?.Hidden == true ? "Einblenden" : null, edit),
            M("moveUp", enabled: edit), M("moveDown", enabled: edit), Sep(),
            InsertSubMenu("Schritt einfügen", !ro),
            M("deleteInserted", enabled: !ro && SelSteps.Any(x => x.Manual)),
        };
    }

    MenuItem ImportanceMenu(ViewStep? s, bool edit)
    {
        var sub = Sub("Wichtigkeit", null);
        foreach (var (imp, name) in new[] { ("primary", "Hauptschritt"), ("secondary", "Nebenschritt"), ("trace", "Spur") })
        {
            string target = imp;
            sub.Items.Add(Check(name, s?.Importance == imp, () => BulkEdit(SelSteps.Where(x => !x.Manual), se => se["importance"] = target)));
        }
        sub.Items.Add(Sep());
        sub.Items.Add(M("moreImportant")); sub.Items.Add(M("lessImportant"));
        sub.IsEnabled = edit && s?.Manual == false;
        return sub;
    }

    MenuItem InsertSubMenu(string label, bool enabled)
    {
        var sub = Sub(label, Theme.IAdd, M("insertText"), Sep(), M("insertFile"), M("insertClip"), M("insertShot"));
        sub.IsEnabled = enabled;
        return sub;
    }

    MenuItem ReplaceSubMenu(ViewStep? s, bool enabled)
    {
        var sub = Sub(s is { Manual: true, PreFrame: null } ? "Bild hinzufügen" : "Bild ersetzen", Theme.ICamera, M("replaceShot"), M("replaceFile"), M("replaceClip"));
        sub.IsEnabled = enabled;
        return sub;
    }

    List<object> ImageMenu()
    {
        var s = Sel;
        bool img = _current != null, ro = _view.Session.ReadOnly, edit = img && !ro;
        var tools = Sub("Werkzeug", Theme.IPointer);
        foreach (var id in new[] { "toolSelect", "toolArrow", "toolRect", "toolEllipse", "toolHighlight", "toolText", "toolBadge", "toolRedact", "toolPixelate" })
        {
            var mi = M(id, enabled: edit);
            if (_tool == ToolOf(id)) mi.Icon = U.Icon(Theme.ICheck, 16, Theme.AccentText);
            tools.Items.Add(mi);
        }
        bool cropped = s != null && _view.CropFor(s) != null;
        return new()
        {
            tools, Sep(),
            M("crop", enabled: edit), M("cropRemove", enabled: cropped && !ro), M("cropAll", enabled: cropped && !ro), Sep(),
            ReplaceSubMenu(s, s != null && !ro), M("restoreImage", enabled: s?.ImageReplaced == true && !ro), Sep(),
            M("deleteDrawing", enabled: _selAnn != null && !ro), M("clearDrawings", enabled: img && !ro && _currentMeta != null && AnnotationsFor(_currentMeta.FrameId).Any()), Sep(),
            M("saveImage", enabled: img), M("saveClip", enabled: s != null),
        };
    }

    static string ToolOf(string id) => id switch { "toolSelect" => "select", "toolArrow" => "arrow", "toolRect" => "rect", "toolEllipse" => "ellipse", "toolHighlight" => "highlight", "toolText" => "text", "toolBadge" => "badge", "toolRedact" => "redact", "toolPixelate" => "pixelate", _ => "crop" };

    List<object> ViewMenu()
    {
        var disp = Sub("Bildausschnitt", Theme.IMonitor);
        string[] dispNames = { "Ganzer Bildschirm", "Anwendungsfenster", "Bereich um die Aktion" };
        for (int i = 0; i < 3; i++) { int k = i; disp.Items.Add(Check(dispNames[i], _dispBox.SelectedIndex == i, () => _dispBox.SelectedIndex = k)); }
        var lang = Sub("Textsprache", Theme.IGlobe,
            Check("Deutsch", _view.Language == "de", () => _langBtns[0].IsChecked = true),
            Check("Englisch", _view.Language == "en", () => _langBtns[1].IsChecked = true));
        var zoom = Sub("Zoom", Theme.IZoomIn, M("zoomIn"), M("zoomOut"), M("zoomFit"), M("zoom100"));
        return new()
        {
            M("beforeAfter", enabled: Sel != null), disp, MarksMenu(), Sep(),
            Check("Schritte", _filterBtns[0].IsChecked == true, () => _filterBtns[0].IsChecked = true),
            Check("Mit Spur", _filterBtns[1].IsChecked == true, () => _filterBtns[1].IsChecked = true), Sep(),
            lang, Sep(),
            M("play", _play.IsEnabled ? "Abspielen beenden" : null), M("lightbox", enabled: _current != null), zoom, Sep(),
            M("compare"),
        };
    }

    /// <summary>Untermenü „Mausmarken“: für alle Schritte je Art, dazu nur dieser Schritt.</summary>
    MenuItem MarksMenu() => Sub("Mausmarken", Theme.IMouse, MarksItems().ToArray());

    List<object> MarksItems()
    {
        var items = new List<object>();
        var head = U.Text("Alle Schritte", Theme.FsCaption, true, Theme.Muted);
        items.Add(new MenuItem { Header = head, IsHitTestVisible = false, Focusable = false });
        var all = _view.MarksFor(null);
        foreach (var (key, label) in MarkKeys) { bool on = MarkOf(all, key); string k = key; items.Add(Check(label, on, () => SetMark(null, k, !on))); }
        if (Sel is { } s)
        {
            items.Add(Sep());
            var cur = _view.MarksFor(s);
            items.Add(M("stepMarks", cur.Any ? $"Bei Schritt {s.Number} ausblenden" : $"Bei Schritt {s.Number} einblenden"));
            if (StepMarksEdit(s) != null) items.Add(U.Mi("Schritt wieder wie alle", null, () => Change(e => StepEdit(e, s.Anchor).Remove("auto"), refill: false)));
        }
        return items;
    }

    List<object> ReviewMenu()
    {
        bool ro = _view.Session.ReadOnly;
        var review = M("review", _view.UnreviewedCount > 0 ? $"Durchsicht der Eingaben ({_view.UnreviewedCount} offen) …" : null);
        return new()
        {
            review, Sep(),
            M("piiScan", enabled: !ro), M("redactText", enabled: !ro), M("redactStep", enabled: Sel != null && !ro), M("generalize", enabled: !ro), Sep(),
            M("cleanup", enabled: !ro), M("overview"), Sep(),
            M("finalRedact", enabled: !ro),
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
            Add(U.Mi($"{multi.Count} Schritte ausblenden", null, () => BulkEdit(multi, se => se["hidden"] = true), Theme.IEye),
                U.Mi($"{multi.Count} Schritte einblenden", null, () => BulkEdit(multi, se => se.Remove("hidden"))), Sep(),
                ImportanceMenu(s, !ro), M("chapter", "Kapitel vor dem ersten beginnen", !ro),
                Sub("Bilder in der Anleitung", Theme.ICamera, U.Mi("Bilder zeigen", null, () => BulkEdit(multi, se => se.Remove("showImage"))), U.Mi("Bilder weglassen", null, () => BulkEdit(multi, se => se["showImage"] = false))), Sep(),
                U.Mi($"Hinweis zu {multi.Count} Schritten hinzufügen …", null, () => AddNoteToMany(multi), Theme.IComment));
            if (multi.Any(x => x.Manual)) Add(Sep(), M("deleteInserted"));
            foreach (var it in m.Items.OfType<MenuItem>()) if (ro) it.IsEnabled = false;
            m.IsOpen = true;
            return;
        }
        bool cropped = _view.CropFor(s) != null;
        var image = Sub("Bild", Theme.ICamera, M("lightbox"), M("crop", enabled: !ro));
        if (cropped) { image.Items.Add(M("cropRemove", enabled: !ro)); image.Items.Add(M("cropAll", enabled: !ro)); }
        image.Items.Add(Sep());
        image.Items.Add(ReplaceSubMenu(s, !ro));
        if (s.ImageReplaced) image.Items.Add(M("restoreImage", enabled: !ro));
        var copy = Sub("Kopieren", Theme.ICopy, M("copyImage"), M("copyImageText"), M("copyText"), M("copyMarkdown"));
        Add(M("editText", enabled: !ro), M("addNote", enabled: !ro), Sep(),
            ImportanceMenu(s, !ro),
            M("chapter", s.ChapterTitle != null ? "Kapitel entfernen" : null, !ro),
            M("hide", s.Hidden ? "Einblenden" : null, !ro),
            Sub("Verschieben", Theme.IMove, M("moveUp", enabled: !ro), M("moveDown", enabled: !ro)), Sep(),
            copy, image, InsertSubMenu("Schritt danach einfügen", !ro), Sep(),
            M("redactStep", enabled: !ro));
        if (s.Manual) Add(M("deleteInserted", enabled: !ro));
        m.IsOpen = true;
    }

    /// <summary>Befehlsknopf der Kopfzeile (Name, Symbol und Tooltip aus dem Katalog).</summary>
    Button CmdButton(string id, string? text, int labelLevel = 0, string style = "Ghost")
    {
        var c = EditorCommands.Get(id);
        var b = U.Btn(c.Glyph, text, () => Run(id), EditorCommands.Tip(id), style);
        b.Margin = new Thickness(2, 0, 2, 0);
        if (labelLevel > 0 && b.Content is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is TextBlock t) _labels.Add((t, labelLevel));
        return b;
    }

    /// <summary>Knopf mit Menü (Pfeil rechts).</summary>
    Button MenuCmdButton(string glyph, string text, string tip, int labelLevel, Func<List<object>> items)
    {
        var content = U.MenuContent(glyph, text);
        var b = U.Make("Ghost"); b.Content = content; b.ToolTip = tip; b.Margin = new Thickness(2, 0, 2, 0);
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
        DevSnapshots.SaveOpenPopups("Rechtsklick");
        foreach (var cm in LogicalOpenMenus()) cm.IsOpen = false;
        Flush();
    }

    static IEnumerable<ContextMenu> LogicalOpenMenus() => PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>()
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
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Aufnahme (manifest.json;*.steprec)|manifest.json;*.steprec", InitialDirectory = AppPaths.DefaultSessionsFolder() };
        if (dlg.ShowDialog(this) != true) return;
        var p = dlg.FileName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(dlg.FileName)! : dlg.FileName;
        UiApp.Instance.OpenViewer(p);
    }

    void ShowShortcuts() => Shortcuts.ShowWindow(this, Shortcuts.Editor());

    /// <summary>Hinweis zum gewählten Schritt (K oder Einfg): Art und Text im Dialog.</summary>
    void AddNote()
    {
        var s = Sel; if (s == null) return;
        if (_list.SelectedItems.Count > 1) { AddNoteToMany(SelSteps); return; }
        var d = new InputDialog("Hinweis hinzufügen", "", null, kinds: true) { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        Change(e =>
        {
            var arr = e["comments"] as System.Text.Json.Nodes.JsonArray ?? new System.Text.Json.Nodes.JsonArray();
            arr.Add(new System.Text.Json.Nodes.JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = s.Anchor, ["kind"] = d.Kind, ["text"] = d.Value.Trim(), ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" });
            e["comments"] = arr;
        });
    }

    void AddNoteToMany(List<ViewStep> steps)
    {
        if (steps.Count == 0) return;
        var d = new InputDialog($"Hinweis zu {steps.Count} Schritten hinzufügen", "", null, kinds: true) { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        Change(e =>
        {
            var arr = e["comments"] as System.Text.Json.Nodes.JsonArray ?? new System.Text.Json.Nodes.JsonArray();
            foreach (var s in steps) arr.Add(new System.Text.Json.Nodes.JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = s.Anchor, ["kind"] = d.Kind, ["text"] = d.Value.Trim(), ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" });
            e["comments"] = arr;
        });
    }
}
