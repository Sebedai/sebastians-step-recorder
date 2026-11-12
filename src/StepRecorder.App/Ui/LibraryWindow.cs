using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Native;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// „Alle Aufnahmen“: Liste aller Aufnahmen im Aufnahmeordner mit Vorschaubild (über die Leseschicht, also geschwärzt),
/// Filter nach Titel oder Suche in den Inhalten, Umbenennen, Packen, Löschen (Papierkorb), Entpacken von .steprec.
/// </summary>
sealed class LibraryWindow : Window
{
    readonly string _root;
    readonly ListBox _list = new() { SelectionMode = SelectionMode.Extended, BorderThickness = new Thickness(0) };
    readonly TextBox _filter = new() { Padding = new Thickness(32, 7, 8, 7) };
    RadioButton _modeTitles = null!, _modeContents = null!;
    readonly ComboBox _sort = new() { MinWidth = 170 };
    readonly TextBlock _count = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _status = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal };
    List<SessionInfo> _all = new();
    CancellationTokenSource? _scanCts, _searchCts, _workCts;
    readonly Dictionary<string, BitmapSource?> _thumbs = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, SessionDetails?> _details = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (Image img, WrapPanel chips, SessionInfo info)> _rows = new(StringComparer.OrdinalIgnoreCase);
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(350) };

    static readonly (string id, Func<string> label)[] Sorts =
    {
        ("date", () => Loc.L("Neueste zuerst", "Newest first")), ("dateAsc", () => Loc.L("Älteste zuerst", "Oldest first")),
        ("title", () => Loc.L("Titel", "Title")), ("duration", () => Loc.L("Dauer", "Duration")), ("steps", () => Loc.L("Schritte", "Steps"))
    };

    IEnumerable<SessionInfo> Selected => _list.SelectedItems.OfType<ListBoxItem>().Select(i => i.Tag switch { SessionInfo s => s, (SessionInfo s, SearchHit) => s, _ => null }).Where(s => s != null).Distinct()!;

    public LibraryWindow()
    {
        Title = Loc.L("Alle Aufnahmen", "All recordings") + " – " + AppInfo.DisplayName;
        Theme.Prepare(this, Theme.Surface);
        var wa = Theme.ActiveWorkArea();
        Width = Math.Min(1080, wa.Width - 40); Height = Math.Min(760, wa.Height - 40); MinWidth = 640; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowPlacer.Attach(this, "library");
        _root = AppPaths.DefaultSessionsFolder();
        Build();
        Loaded += (_, _) => { Rescan(); _filter.Focus(); };
        PreviewKeyDown += OnKey;
        AllowDrop = true;
        Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            foreach (var f in files)
            {
                if (File.Exists(f) && f.EndsWith(".steprec", StringComparison.OrdinalIgnoreCase)) _ = SessionImport.Unpack(this, f, Rescan);
                else if (Session.LooksLikeSession(f)) UiApp.Instance.OpenViewer(f);
            }
        };
        Closed += (_, _) => { _scanCts?.Cancel(); _searchCts?.Cancel(); _workCts?.Cancel(); };
    }

    void Build()
    {
        // Kopf
        var logo = Theme.Logo(26); logo.Margin = new Thickness(0, 0, 12, 0); logo.VerticalAlignment = VerticalAlignment.Center;
        var head = U.Text(Loc.L("Alle Aufnahmen", "All recordings"), 17, true); head.FontFamily = Theme.Display; head.VerticalAlignment = VerticalAlignment.Center;
        var import = U.Btn(Theme.IOpen, Loc.L("Entpacken …", "Unpack …"), ImportDialog, Loc.L(".steprec-Datei entpacken und öffnen (oder hierher ziehen)", "Unpack and open a .steprec file (or drop it here)"), "Ghost");
        var refresh = U.Btn(Theme.IRedo, null, Rescan, Loc.L("Aktualisieren (F5)", "Refresh (F5)"), "Ghost");
        var folder = U.Btn(Theme.IFolder, null, () => { Directory.CreateDirectory(_root); Shell.Reveal(_root); }, Loc.L("Aufnahmeordner im Explorer öffnen", "Open recordings folder in Explorer"), "Ghost");
        var top = new DockPanel { Margin = new Thickness(20, 14, 16, 10) };
        var right = U.Row(import, refresh, folder); right.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(right, Dock.Right); top.Children.Add(right);
        DockPanel.SetDock(logo, Dock.Left); top.Children.Add(logo); top.Children.Add(head);

        // Filterzeile
        _filter.Tag = Loc.L("Titel filtern (Strg+F) · in Inhalten suchen (Strg+Umschalt+F)", "Filter titles (Ctrl+F) · search contents (Ctrl+Shift+F)");
        var filterHost = new Grid { MinWidth = 320 };
        filterHost.Children.Add(_filter);
        var icon = U.Icon(Theme.ISearch, 13, Theme.Muted); icon.HorizontalAlignment = HorizontalAlignment.Left; icon.Margin = new Thickness(12, 0, 0, 0); icon.IsHitTestVisible = false;
        filterHost.Children.Add(icon);
        var (modeTrack, modes) = U.Segmented("libmode", AppState.Load().Library.Mode == "contents" ? 1 : 0, (null, Loc.L("Titel", "Titles")), (null, Loc.L("Inhalte", "Contents")));
        _modeTitles = modes[0]; _modeContents = modes[1];
        modeTrack.ToolTip = Loc.L("Titel: Liste nach Titel filtern. Inhalte: in Schritten, Fenstern, Eingaben und Kommentaren aller Aufnahmen suchen – Geschwärztes und Ungeprüftes wird nie gefunden.",
                                  "Titles: filter the list by title. Contents: search steps, windows, input and comments of all recordings – redacted or unreviewed content is never found.");
        foreach (var s in Sorts) _sort.Items.Add(s.label());
        _sort.SelectedIndex = Math.Max(0, Array.FindIndex(Sorts, s => s.id == AppState.Load().Library.Sort));
        _sort.SelectionChanged += (_, _) => { AppState.Update(st => st.Library.Sort = Sorts[Math.Max(0, _sort.SelectedIndex)].id); Fill(); };
        foreach (var m in modes) m.Checked += (_, _) => { if (!IsLoaded) return; AppState.Update(st => st.Library.Mode = _modeContents.IsChecked == true ? "contents" : "titles"); Refilter(); };
        _filter.TextChanged += (_, _) => { if (_modeContents.IsChecked == true) { _debounce.Stop(); _debounce.Start(); } else Fill(); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Fill(); };
        var filterRow = new DockPanel { Margin = new Thickness(20, 0, 16, 10) };
        var sortBox = U.Row(_sort); sortBox.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(sortBox, Dock.Right); filterRow.Children.Add(sortBox);
        modeTrack.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(modeTrack, Dock.Right); filterRow.Children.Add(modeTrack);
        filterRow.Children.Add(filterHost);

        // Aktionen für die Auswahl
        void A(string glyph, string text, Action a, string tip) { var b = U.Btn(glyph, text, a, tip, "Ghost"); b.Margin = new Thickness(0, 0, 4, 0); _actions.Children.Add(b); }
        A(Theme.IOpen, Loc.L("Öffnen", "Open"), OpenSelected, Loc.L("Im Editor öffnen (Eingabe)", "Open in editor (Enter)"));
        A(Theme.IEdit, Loc.L("Umbenennen", "Rename"), RenameSelected, Loc.L("Titel ändern (F2) – der Ordnername bleibt", "Change title (F2) – folder name stays"));
        A(Theme.IExport, Loc.L("Packen …", "Pack …"), PackSelected, Loc.L("Als .steprec-Datei speichern – vollständig oder bereinigt", "Save as .steprec file – complete or sanitized"));
        A(Theme.IFolder, Loc.L("Im Explorer zeigen", "Show in Explorer"), () => { foreach (var s in Selected.Take(3)) Shell.Reveal(s.Path); }, "");
        A(Theme.IDelete, Loc.L("Löschen", "Delete"), DeleteSelected, Loc.L("In den Papierkorb verschieben (Entf)", "Move to recycle bin (Del)"));
        var more = U.Btn(Theme.IMore, null, () => { }, Loc.L("Weitere Aktionen", "More actions"), "Ghost");
        more.Click += (_, _) =>
        {
            var m = new ContextMenu { PlacementTarget = more, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            void I(string label, Action a) { var mi = new MenuItem { Header = label }; mi.Click += (_, _) => { try { a(); } catch (Exception ex) { UiApp.ShowError(ex); } }; m.Items.Add(mi); }
            I(Loc.L("Exporte löschen …", "Delete exports …"), DeleteExports);
            I(Loc.L("Aus „Zuletzt“ entfernen", "Remove from \"Recent\""), () => { var s = AppSettings.Load(); foreach (var x in Selected) s.RemoveRecent(x.Path); _status.Text = Loc.L("Aus „Zuletzt“ entfernt", "Removed from \"Recent\""); });
            I(Loc.L("Pfad kopieren", "Copy path"), () => Shell.CopyPath(string.Join(Environment.NewLine, Selected.Select(x => x.Path))));
            m.IsOpen = true;
        };
        _actions.Children.Add(more);
        var actionRow = new DockPanel { Margin = new Thickness(16, 0, 16, 6) };
        DockPanel.SetDock(_count, Dock.Right); actionRow.Children.Add(_count); actionRow.Children.Add(_actions);
        _count.Foreground = Theme.Muted; _status.Foreground = Theme.Muted; _count.FontFamily = Theme.Mono;

        _list.Background = Theme.Surface;
        _list.SelectionChanged += (_, _) => UpdateActions();
        _list.MouseDoubleClick += (_, e) => { if (ItemAt(e.OriginalSource) != null) OpenSelected(); };
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);

        var statusBar = new Border { Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 6, 16, 7), Child = _status };
        var listBorder = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = _list };
        var root = new DockPanel { Background = Theme.Surface };
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        DockPanel.SetDock(filterRow, Dock.Top); root.Children.Add(filterRow);
        DockPanel.SetDock(actionRow, Dock.Top); root.Children.Add(actionRow);
        DockPanel.SetDock(statusBar, Dock.Bottom); root.Children.Add(statusBar);
        root.Children.Add(listBorder);
        Content = root;
        UpdateActions();
    }

    // ---------------- Laden ----------------

    public void Rescan()
    {
        _scanCts?.Cancel();
        var cts = _scanCts = new CancellationTokenSource();
        _status.Text = Loc.L("Aufnahmen werden gesucht …", "Looking for recordings …");
        string root = _root;
        Task.Run(() => SessionCatalog.Scan(root, ct: cts.Token), cts.Token).ContinueWith(t =>
        {
            if (cts.IsCancellationRequested || !IsLoaded) return;
            if (t.IsFaulted) { _status.Text = t.Exception?.InnerException?.Message ?? ""; return; }
            _all = t.Result;
            // gelöschte oder geänderte Aufnahmen: Zwischenspeicher der Liste verwerfen
            _thumbs.Clear(); _details.Clear();
            Fill();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    void Refilter() { _filter.Focus(); Fill(); }

    IEnumerable<SessionInfo> Sorted(IEnumerable<SessionInfo> list) => Sorts[Math.Max(0, _sort.SelectedIndex)].id switch
    {
        "dateAsc" => list.OrderBy(x => x.StartUtc),
        "title" => list.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase),
        "duration" => list.OrderByDescending(x => x.DurationUs),
        "steps" => list.OrderByDescending(x => x.PrimarySteps),
        _ => list.OrderByDescending(x => x.StartUtc)
    };

    void Fill()
    {
        var keep = Selected.Select(s => s.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _list.Items.Clear(); _rows.Clear();
        _searchCts?.Cancel();
        var q = SearchQuery.Parse(_filter.Text);
        if (_modeContents.IsChecked == true && !q.IsEmpty) { SearchContents(q); return; }
        var shown = Sorted(_all.Where(s => q.Matches(s.Title + " " + Path.GetFileName(s.Path)))).ToList();
        foreach (var s in shown)
        {
            var item = new ListBoxItem { Content = Row(s), Tag = s, Padding = new Thickness(14, 8, 14, 8) };
            _list.Items.Add(item);
            if (keep.Contains(s.Path)) item.IsSelected = true;
        }
        _count.Text = q.IsEmpty ? Loc.N(_all.Count, "1 Aufnahme", "{0} Aufnahmen", "1 recording", "{0} recordings") : Loc.F("{0} von {1}", "{0} of {1}", shown.Count, _all.Count);
        _status.Text = _root;
        if (_all.Count == 0) _list.Items.Add(new ListBoxItem { Content = U.Text(Loc.L("Noch keine Aufnahmen in diesem Ordner.", "No recordings in this folder yet."), 13, fg: Theme.Muted), IsEnabled = false, Padding = new Thickness(18) });
        LoadExtras(shown);
        UpdateActions();
    }

    FrameworkElement Row(SessionInfo s)
    {
        var img = new Image { Stretch = Stretch.UniformToFill, Width = 96, Height = 60 };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        if (_thumbs.TryGetValue(s.Path, out var t)) img.Source = t;
        var box = new Border { Width = 98, Height = 62, CornerRadius = new CornerRadius(4), Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), ClipToBounds = true, Margin = new Thickness(0, 0, 14, 0),
            Child = new Grid { Children = { U.Icon(s.Archive ? Theme.ILock : Theme.IDoc, 16, Theme.Muted), img } } };
        var meta = new List<string>();
        if (s.StartUtc > DateTime.MinValue) meta.Add(Loc.DateAndTime(s.StartUtc.ToLocalTime()));
        if (s.DurationUs > 0) meta.Add(Export.Fmt.Time(s.DurationUs, Loc.Lang).Split(',')[0]);
        if (s.PrimarySteps > 0) meta.Add(Loc.N(s.PrimarySteps, "1 Hauptschritt", "{0} Hauptschritte", "1 main step", "{0} main steps"));
        if (ProfileName(s.ProfileId) is { } pn) meta.Add(pn);
        var chips = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        FillChips(chips, s);
        var title = new TextBlock { Text = s.Title, FontWeight = FontWeights.SemiBold, FontSize = 13.5, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Theme.Fg };
        var col = U.Col(title, new TextBlock { Text = string.Join("  ·  ", meta), FontFamily = Theme.Mono, FontSize = 11, Foreground = Theme.Muted, Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis }, chips);
        col.VerticalAlignment = VerticalAlignment.Center;
        var g = new DockPanel { ToolTip = s.Path };
        DockPanel.SetDock(box, Dock.Left); g.Children.Add(box); g.Children.Add(col);
        _rows[s.Path] = (img, chips, s);
        return g;
    }

    static string? ProfileName(string id) => id switch
    {
        "guide" => Loc.L("Anleitung", "Guide"), "support" => Loc.L("Support-Fall", "Support case"), "aiTrace" => Loc.L("KI-Spur", "AI trace"), "custom" => Loc.L("Eigene", "Custom"), _ => null
    };

    void FillChips(WrapPanel chips, SessionInfo s)
    {
        chips.Children.Clear();
        if (s.Recording) chips.Children.Add(U.Chip(Loc.L("Aufnahme läuft", "Recording"), Theme.DangerSoft, Theme.Danger, Theme.IRecord));
        else if (s.State == "recovered") chips.Children.Add(U.Chip(Loc.L("nicht regulär beendet", "not stopped normally"), Theme.WarnSoft, Theme.Warn, Theme.IWarn));
        if (s.State == "unreadable") chips.Children.Add(U.Chip(Loc.L("nicht lesbar", "unreadable"), Theme.DangerSoft, Theme.Danger, Theme.IError));
        if (s.Archive) chips.Children.Add(U.Chip(Loc.L("schreibgeschützt (.steprec)", "read-only (.steprec)"), Theme.Surface2, Theme.Muted, Theme.ILock));
        if (_details.TryGetValue(s.Path, out var d) && d is { Unreviewed: > 0 })
            chips.Children.Add(U.Chip(Loc.N(d.Unreviewed, "Durchsicht offen (1)", "Durchsicht offen ({0})", "review pending (1)", "review pending ({0})"), Theme.WarnSoft, Theme.Warn, Theme.IChecklist));
        if (UiApp.Instance.IsOpenInViewer(s.Path)) chips.Children.Add(U.Chip(Loc.L("im Editor geöffnet", "open in editor"), Theme.AccentSoft, Theme.AccentText, Theme.IEdit));
    }

    /// <summary>Vorschaubilder und Durchsicht-Stand nacheinander im Hintergrund (sichtbare Reihenfolge).</summary>
    void LoadExtras(List<SessionInfo> shown)
    {
        _workCts?.Cancel();
        var cts = _workCts = new CancellationTokenSource();
        var todo = shown.Where(s => !s.Recording && s.State != "unreadable" && (!_thumbs.ContainsKey(s.Path) || !_details.ContainsKey(s.Path))).ToList();
        if (todo.Count == 0) return;
        var ui = TaskScheduler.FromCurrentSynchronizationContext();
        Task.Run(() =>
        {
            foreach (var s in todo)
            {
                if (cts.IsCancellationRequested) return;
                BitmapSource? src = null;
                try
                {
                    if (SessionCatalog.Thumbnail(s.Path, 192) is { } png)
                    {
                        using var ms = new MemoryStream(png);
                        var bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                        src = bi;
                    }
                }
                catch { }
                var det = SessionCatalog.Details(s.Path);
                Task.Factory.StartNew(() =>
                {
                    if (cts.IsCancellationRequested) return;
                    _thumbs[s.Path] = src; _details[s.Path] = det;
                    if (_rows.TryGetValue(s.Path, out var row))
                    {
                        if (src != null) { row.img.Source = src; Anim.Refresh(row.img, 0, 260); }
                        FillChips(row.chips, row.info);
                    }
                }, CancellationToken.None, TaskCreationOptions.None, ui);
            }
        }, cts.Token);
    }

    // ---------------- Suche in Inhalten ----------------

    void SearchContents(SearchQuery q)
    {
        var cts = _searchCts = new CancellationTokenSource();
        var sessions = Sorted(_all).ToList();
        _status.Text = Loc.L("Suche läuft …", "Searching …");
        int hitCount = 0, sessionCount = 0;
        string? lastSession = null;
        var ui = TaskScheduler.FromCurrentSynchronizationContext();
        string lang = AppSettings.Load().Language;
        Task.Run(() => SessionSearch.SearchAll(sessions, q, hit =>
        {
            Task.Factory.StartNew(() =>
            {
                if (cts.IsCancellationRequested) return;
                if (hit.SessionPath != lastSession)
                {
                    lastSession = hit.SessionPath; sessionCount++;
                    var info = sessions.First(x => x.Path == hit.SessionPath);
                    _list.Items.Add(new ListBoxItem { Content = Row(info), Tag = info, Padding = new Thickness(14, 10, 14, 4) });
                }
                hitCount++;
                var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Foreground = Theme.Fg2 };
                tb.Inlines.Add(new Run(Loc.F("Schritt {0}", "Step {0}", hit.Entry.Number) + "  ") { FontFamily = Theme.Mono, FontSize = 11, Foreground = Theme.AccentText });
                AddSnippet(tb, hit.Snippet);
                var infoRow = sessions.First(x => x.Path == hit.SessionPath);
                _list.Items.Add(new ListBoxItem { Content = tb, Tag = (infoRow, hit), Padding = new Thickness(126, 3, 14, 3), ToolTip = FieldName(hit.Entry.Field) });
                _count.Text = Loc.F("{0} Treffer in {1} Aufnahmen", "{0} matches in {1} recordings", hitCount, sessionCount);
            }, CancellationToken.None, TaskCreationOptions.None, ui);
        }, lang, ct: cts.Token), cts.Token).ContinueWith(t =>
        {
            if (cts.IsCancellationRequested) return;
            _status.Text = t.IsFaulted ? (t.Exception?.InnerException?.Message ?? "") : Loc.N(t.Result, "1 Aufnahme durchsucht · Geschwärztes und Ungeprüftes wird nie gefunden", "{0} Aufnahmen durchsucht · Geschwärztes und Ungeprüftes wird nie gefunden", "1 recording searched · redacted or unreviewed content is never found", "{0} recordings searched · redacted or unreviewed content is never found");
            if (hitCount == 0) { _count.Text = Loc.L("Keine Treffer", "No matches"); _list.Items.Add(new ListBoxItem { Content = U.Text(Loc.L("Nichts gefunden.", "Nothing found."), 13, fg: Theme.Muted), IsEnabled = false, Padding = new Thickness(18) }); }
            LoadExtras(_rows.Values.Select(r => r.info).ToList());
        }, ui);
    }

    static void AddSnippet(TextBlock tb, string snippet)
    {
        int a = snippet.IndexOf('«'), b = snippet.IndexOf('»');
        if (a < 0 || b < a) { tb.Inlines.Add(new Run(snippet)); return; }
        tb.Inlines.Add(new Run(snippet[..a]));
        tb.Inlines.Add(new Run(snippet[(a + 1)..b]) { FontWeight = FontWeights.SemiBold, Foreground = Theme.Fg, Background = Theme.AccentSoft });
        tb.Inlines.Add(new Run(snippet[(b + 1)..]));
    }

    static string FieldName(string f) => f switch
    {
        "elements" => Loc.L("Bedienelement", "Element"), "windows" => Loc.L("Fenstertitel", "Window title"), "typed" => Loc.L("Eingabe", "Input"),
        "comments" => Loc.L("Kommentar", "Comment"), "markers" => Loc.L("Marker", "Marker"), _ => Loc.L("Schritttext", "Step text")
    };

    // ---------------- Aktionen ----------------

    void UpdateActions()
    {
        int n = Selected.Count();
        foreach (Button b in _actions.Children) b.IsEnabled = n > 0;
        if (n > 1) _status.Text = Loc.N(n, "1 ausgewählt", "{0} ausgewählt", "1 selected", "{0} selected");
    }

    static ListBoxItem? ItemAt(object src)
    {
        var d = src as DependencyObject;
        while (d != null && d is not ListBoxItem) d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as ListBoxItem;
    }

    void OpenSelected()
    {
        foreach (var item in _list.SelectedItems.OfType<ListBoxItem>().Take(5).ToList())
        {
            switch (item.Tag)
            {
                case (SessionInfo s, SearchHit h): UiApp.Instance.OpenViewer(s.Path, h.Entry.Anchor, _filter.Text.Trim()); break;
                case SessionInfo s when s.State != "unreadable": UiApp.Instance.OpenViewer(s.Path); break;
            }
        }
    }

    void RenameSelected()
    {
        var s = Selected.FirstOrDefault(); if (s == null) return;
        if (s.Archive) { Msg.Show(Loc.L("Ein .steprec-Archiv ist schreibgeschützt. Zum Umbenennen bitte entpacken.", "A .steprec archive is read-only. Unpack it to rename."), Title); return; }
        if (s.Recording) return;
        var d = new InputDialog(Loc.L("Titel der Aufnahme (der Ordnername bleibt)", "Title of the recording (the folder name stays)"), s.Title) { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        if (UiApp.Instance.ViewerFor(s.Path) is { } v) v.RenameTo(d.Value.Trim());
        else if (!SessionEdits.SetTitle(s.Path, d.Value)) { Msg.Show(Loc.L("Die Aufnahme wurde gerade an anderer Stelle geändert; bitte erneut versuchen.", "The recording was just changed elsewhere; please try again."), Title); return; }
        Rescan();
    }

    void PackSelected()
    {
        var s = Selected.FirstOrDefault(); if (s == null) return;
        if (s.Archive) { Msg.Show(Loc.L("Das ist bereits ein .steprec-Archiv.", "This already is a .steprec archive."), Title); return; }
        PackDialog.Show(this, s.Path);
    }

    async void DeleteSelected()
    {
        var sel = Selected.ToList(); if (sel.Count == 0) return;
        var open = sel.Where(s => UiApp.Instance.IsOpenInViewer(s.Path) || s.Recording).ToList();
        if (open.Count > 0) { Msg.Show(Loc.L("Geöffnete oder laufende Aufnahmen können nicht gelöscht werden. Bitte zuerst den Editor schließen.", "Open or running recordings cannot be deleted. Please close the editor first."), Title); return; }
        string what = sel.Count == 1 ? Loc.F("„{0}“", "\"{0}\"", sel[0].Title) : Loc.N(sel.Count, "1 Aufnahme", "{0} Aufnahmen", "1 recording", "{0} recordings");
        bool bin = sel.All(s => RecycleBin.LikelyAvailable(s.Path));
        if (Msg.Show(bin ? Loc.F("{0} in den Papierkorb verschieben?", "Move {0} to the recycle bin?", what) : Loc.F("{0} löschen?\n\nFür dieses Laufwerk gibt es vermutlich keinen Papierkorb – es wird vorher noch einmal gefragt.", "Delete {0}?\n\nThis drive probably has no recycle bin – you will be asked again.", what),
                Loc.L("Löschen", "Delete"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var paths = sel.Select(s => s.Path).ToList();
        string? error = null;
        var result = await ExportProgressWindow.Run(this, Loc.L("Wird gelöscht …", "Deleting …"), (_, _) => RecycleBin.Delete(paths, out error));
        if (result == RecycleResult.NoRecycleBin)
        {
            if (Msg.Show(Loc.F("Für dieses Laufwerk gibt es keinen Papierkorb.\n\n{0} endgültig löschen? Das lässt sich nicht rückgängig machen.", "This drive has no recycle bin.\n\nDelete {0} permanently? This cannot be undone.", what),
                    Loc.L("Endgültig löschen", "Delete permanently"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            foreach (var p in paths)
                try { if (File.Exists(p)) File.Delete(p); else Directory.Delete(p, true); }
                catch (Exception ex) { UiApp.ShowError(ex); break; }
        }
        else if (result == RecycleResult.Failed) { Msg.Show(Loc.F("Löschen fehlgeschlagen: {0}", "Delete failed: {0}", error ?? "?"), Title); }
        var settings = AppSettings.Load();
        foreach (var p in paths) if (!Directory.Exists(p) && !File.Exists(p)) settings.RemoveRecent(p);
        Rescan();
    }

    async void DeleteExports()
    {
        var sel = Selected.Where(s => !s.Archive && Directory.Exists(Path.Combine(s.Path, "export"))).ToList();
        if (sel.Count == 0) { Msg.Show(Loc.L("Die ausgewählten Aufnahmen haben keine Exporte.", "The selected recordings have no exports."), Title); return; }
        long bytes = sel.Sum(s => SessionCatalog.SizeOnDisk(s.Path, exportsOnly: true));
        if (Msg.Show(Loc.F("Exporte von {0} löschen ({1})? Die Aufnahmen selbst bleiben erhalten.", "Delete exports of {0} ({1})? The recordings themselves are kept.", Loc.N(sel.Count, "1 Aufnahme", "{0} Aufnahmen", "1 recording", "{0} recordings"), Loc.Bytes(bytes)),
                Loc.L("Exporte löschen", "Delete exports"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var dirs = sel.Select(s => Path.Combine(s.Path, "export")).ToList();
        string? error = null;
        var r = await ExportProgressWindow.Run(this, Loc.L("Wird gelöscht …", "Deleting …"), (_, _) => RecycleBin.Delete(dirs, out error));
        if (r == RecycleResult.NoRecycleBin && Msg.Show(Loc.L("Kein Papierkorb auf diesem Laufwerk. Exporte endgültig löschen?", "No recycle bin on this drive. Delete exports permanently?"), Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            foreach (var d in dirs) try { Directory.Delete(d, true); } catch { }
        else if (r == RecycleResult.Failed) Msg.Show(error ?? "?", Title);
        _status.Text = Loc.L("Exporte gelöscht", "Exports deleted");
    }

    void ImportDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.L("Aufnahme-Archiv (*.steprec)|*.steprec", "Recording archive (*.steprec)|*.steprec") };
        if (dlg.ShowDialog(this) == true) _ = SessionImport.Unpack(this, dlg.FileName, Rescan);
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        var m = Keyboard.Modifiers;
        bool inText = Keyboard.FocusedElement is TextBox;
        switch (e.Key)
        {
            case Key.F when m == ModifierKeys.Control: _modeTitles.IsChecked = true; _filter.Focus(); _filter.SelectAll(); break;
            case Key.F when m == (ModifierKeys.Control | ModifierKeys.Shift): _modeContents.IsChecked = true; _filter.Focus(); _filter.SelectAll(); break;
            case Key.F5: Rescan(); break;
            case Key.Escape when inText && _filter.Text.Length > 0: _filter.Clear(); break;
            case Key.Escape: Close(); break;
            case Key.Enter when !inText || _list.SelectedItems.Count > 0: OpenSelected(); break;
            case Key.F2 when !inText: RenameSelected(); break;
            case Key.Delete when !inText: DeleteSelected(); break;
            case Key.Down when inText && _list.Items.Count > 0: _list.SelectedIndex = Math.Max(0, _list.SelectedIndex); (_list.ItemContainerGenerator.ContainerFromIndex(_list.SelectedIndex) as ListBoxItem)?.Focus(); break;
            default: return;
        }
        e.Handled = true;
    }
}
