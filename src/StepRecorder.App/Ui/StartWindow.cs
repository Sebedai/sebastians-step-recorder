using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Native;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Startfenster („Dashboard“): Begrüßung, links „Neue Aufnahme“ (① Modus, ② Bereich – ein Klick startet),
/// rechts ein Kasten mit Reitern „Zuletzt“ und „Tastenkürzel“. Passt immer ins Fenster: Reicht der Platz nicht,
/// wird der Inhalt gleichmäßig verkleinert statt gescrollt.
/// </summary>
sealed class StartWindow : Window
{
    readonly RadioButton[] _profiles;
    string _goalText = "", _expectedText = "", _referenceText = "", _titleText = "";
    readonly ComboBox _monitor = new() { MinWidth = 200 };
    readonly StackPanel _recent = new();
    readonly TextBlock _stats = new() { FontFamily = Theme.Mono, FontSize = 11.5, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
    static readonly string[] Ids = { "guide", "support", "aiTrace", "custom" };
    static readonly (string glyph, string name, string audience, string text)[] Modes =
    {
        (Theme.IDoc, Loc.L("Anleitung", "Guide"), Loc.L("für Menschen", "for people"), Loc.L("Schlank – nur was Menschen zum Nachmachen brauchen.", "Lean – only what people need to follow along.")),
        (Theme.IWrench, Loc.L("Support-Fall", "Support case"), Loc.L("für den Helpdesk", "for the help desk"), Loc.L("Ausführlich – alle Eingaben, damit der Helpdesk den Fehler nachvollziehen kann.", "Detailed – all input, so the help desk can reproduce the problem.")),
        (Theme.IRobot, Loc.L("KI-Spur", "AI trace"), Loc.L("für KI und Skripte", "for AI and scripts"), Loc.L("Vollständig – so genau wie möglich, für KI-Assistenten und AutoHotkey.", "Complete – as precise as possible, for AI assistants and AutoHotkey.")),
        (Theme.ISettings, Loc.L("Eigene", "Custom"), Loc.L("selbst festgelegt", "user-defined"), Loc.L("Selbst festgelegt – „Einstellen …“ öffnet die Auswahl.", "User-defined – “Set up …” opens the options.")),
    };
    readonly TextBlock _modeText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Theme.Fg2 };
    readonly StackPanel _meter = new() { Orientation = Orientation.Horizontal };
    readonly WrapPanel _chips = new() { Margin = new Thickness(0, 10, 0, 0) };
    Button _recLink = null!, _goalLink = null!;
    Border _strip = null!;

    int ProfileIndex => Math.Max(0, Array.FindIndex(_profiles, r => r.IsChecked == true));

    public StartWindow()
    {
        Title = AppInfo.DisplayName;
        Theme.Prepare(this, Theme.Bg);
        var wa = Theme.ActiveWorkArea();
        Width = Math.Min(1180, wa.Width - 24); Height = Math.Min(760, wa.Height - 24);
        MinWidth = Math.Min(640, wa.Width - 24); MinHeight = Math.Min(460, wa.Height - 24);
        WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.CanResize;
        WindowPlacer.Attach(this, "start");
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0 && Session.LooksLikeSession(f[0])) UiApp.Instance.OpenViewer(f[0]); };
        var s = AppSettings.Load();

        // ---------- Kopfleiste ----------
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 22) };
        var actions = U.Row(U.Ghost(Theme.IOpen, Loc.L("Öffnen", "Open"), OpenSession, Loc.L("Aufnahme öffnen (Strg+O) – oder einen Aufnahmeordner hierher ziehen", "Open a recording (Ctrl+O) – or drag a recording folder here")), U.Ghost(Theme.ISettings, Loc.L("Einstellungen", "Settings"), ShowSettings), U.Ghost(Theme.IHelp, Loc.L("Hilfe", "Help"), ShowHelp));
        actions.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        var logo = Theme.Logo(30); logo.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(logo, Dock.Left); header.Children.Add(logo);
        header.Children.Add(Theme.Wordmark(17));

        // ---------- Begrüßung ----------
        var hello = new TextBlock { FontFamily = Theme.Display, FontSize = 28, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
        hello.Inlines.Add(new System.Windows.Documents.Run(Greeting() + " ") { FontWeight = FontWeights.SemiBold, Foreground = Theme.Fg });
        hello.Inlines.Add(new System.Windows.Documents.Run(Loc.L("Was möchten Sie festhalten?", "What would you like to capture?")) { Foreground = Theme.Muted });
        _stats.Margin = new Thickness(2, 8, 0, 0);
        var hero = U.Col(hello, _stats);
        hero.Margin = new Thickness(0, 0, 0, 20);

        // ---------- Neue Aufnahme: ① Modus ----------
        int sel = Math.Max(0, Array.IndexOf(Ids, s.LastProfile));
        var modes = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new Thickness(-5, 0, -5, 0) };
        _profiles = Modes.Select((m, i) => ModeCard(m.glyph, m.name, m.audience, i == sel)).ToArray();
        for (int k = 0; k < _profiles.Length; k++) _profiles[k].ToolTip = $"{Modes[k].name} – {Modes[k].audience}\n{Modes[k].text}";
        foreach (var r in _profiles) { r.Margin = new Thickness(5, 0, 5, 0); modes.Children.Add(r); r.Checked += (_, _) => UpdateProfile(); }
        _recLink = U.Btn(Theme.IEdit, Loc.L("Anpassen …", "Customize …"), CustomizeFromPreset, null, "Link");
        _recLink.Margin = new Thickness(0, 2, -6, 0); _recLink.HorizontalAlignment = HorizontalAlignment.Right;
        var meterBox = U.Col(_meter, _recLink);
        meterBox.Margin = new Thickness(20, 0, 0, 0);
        meterBox.ToolTip = Loc.L("Detailgrad: wie genau aufgezeichnet wird", "Level of detail: how precisely input is recorded");
        var stripTop = new DockPanel();
        DockPanel.SetDock(meterBox, Dock.Right); stripTop.Children.Add(meterBox); stripTop.Children.Add(_modeText);
        _strip = new Border
        {
            Background = Theme.Surface2, CornerRadius = new CornerRadius(7), Padding = new Thickness(16, 12, 16, 10), Margin = new Thickness(0, 10, 0, 0),
            Child = U.Col(stripTop, _chips), ToolTip = DetailPresets.BaseRule() + Loc.L(" Passwortfelder werden in allen Modi maskiert.", " Password fields are masked in all modes.")
        };

        // ---------- Neue Aufnahme: ② Bereich ----------
        _monitor.Items.Add(Loc.L("Monitor mit Aktivität", "Monitor with activity")); _monitor.Items.Add(Loc.L("Alle Monitore", "All monitors"));
        foreach (var m in ScreenGrabber.GetMonitors())
            _monitor.Items.Add(m.Primary
                ? Loc.F("Monitor {0} · {1} × {2} · Hauptmonitor", "Monitor {0} · {1} × {2} · primary", m.Index + 1, m.Bounds.Width, m.Bounds.Height)
                : Loc.F("Monitor {0} · {1} × {2}", "Monitor {0} · {1} × {2}", m.Index + 1, m.Bounds.Width, m.Bounds.Height));
        _monitor.SelectedIndex = 0;
        _monitor.ToolTip = Loc.L("Welcher Bildschirm bei „Ganzer Bildschirm“ aufgenommen wird", "Which screen is recorded for “Full screen”");
        _goalLink = U.Btn(Theme.IFlag, Loc.L("Ziel und Titel …", "Goal and title …"), ShowGoalDialog, Loc.L("Optional: was die Aufnahme zeigen soll – hilft Menschen und KI beim Verstehen", "Optional: what the recording should show – helps people and AI understand it"), "Link");
        var areaHead = new DockPanel { Margin = new Thickness(0, 18, 0, 10) };
        var areaOpts = U.Row(_monitor, _goalLink); areaOpts.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(areaOpts, Dock.Right); areaHead.Children.Add(areaOpts);
        areaHead.Children.Add(StepLabel("2", Loc.L("Was soll aufgenommen werden?", "What should be recorded?")));

        var tiles = new Grid();
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition());
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition());
        void Place(UIElement e, int col) { Grid.SetColumn(e, col); tiles.Children.Add(e); }
        Place(StartTile(), 0);
        Place(Tile(Theme.IApp, Loc.L("Eine Anwendung", "One application"), Loc.L("danach Fenster anklicken", "then click a window"), Loc.L("Nur die Fenster eines Programms aufnehmen – alles andere bleibt außen vor", "Record only the windows of one program – everything else stays out"), () => Begin(ScopeMode.App)), 2);
        Place(Tile(Theme.ICrop, Loc.L("Bereich", "Area"), Loc.L("danach Rechteck aufziehen", "then drag a rectangle"), Loc.L("Einen festen Ausschnitt des Bildschirms aufnehmen", "Record a fixed part of the screen"), () => Begin(ScopeMode.Region)), 4);

        _lastRegionLink = U.Btn(Theme.ICrop, "", () => BeginLastRegion(), Loc.L("Den zuletzt gewählten Bereich anpassen und aufnehmen – gestartet wird erst mit Eingabe", "Adjust and record the last selected area – recording starts only with Enter"), "Link");
        _lastRegionLink.HorizontalAlignment = HorizontalAlignment.Right; _lastRegionLink.Margin = new Thickness(0, 4, -4, 0);
        UpdateLastRegionLink();

        var stepOne = StepLabel("1", Loc.L("Wofür ist die Aufnahme?", "What is the recording for?")); stepOne.Margin = new Thickness(0, 0, 0, 10);
        var main = Panel(Loc.L("Neue Aufnahme", "New recording"), Theme.IRecord, U.Col(stepOne, modes, _strip, areaHead, tiles, _lastRegionLink), null);

        // ---------- Seitenkasten mit Reitern ----------
        string HK(string id) => RecordingController.Key(id);
        var keyGrid = new Grid();
        keyGrid.ColumnDefinitions.Add(new ColumnDefinition()); keyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        int kr = 0;
        void K(string label, string combo)
        {
            keyGrid.RowDefinitions.Add(new RowDefinition());
            var l = U.Text(label, 12.5, fg: Theme.Fg2, margin: new Thickness(0, 4, 10, 4)); l.VerticalAlignment = VerticalAlignment.Center;
            var k = U.Kbd(combo); Grid.SetRow(l, kr); Grid.SetRow(k, kr); Grid.SetColumn(k, 1);
            keyGrid.Children.Add(l); keyGrid.Children.Add(k); kr++;
        }
        foreach (var id in new[] { "instantPause", "stop", "markerError", "comment", "privacy" })
            if (HK(id) is { Length: > 0 } combo) K(HotkeyActions.Label(HotkeyActions.ById(id)!), combo);
        if (UiApp.Instance.HotkeyFailures.Count > 0)
        {
            keyGrid.RowDefinitions.Add(new RowDefinition());
            var warnChip = U.Chip(Loc.F("belegt: {0}", "taken: {0}", string.Join(", ", UiApp.Instance.HotkeyFailures)), Theme.WarnSoft, Theme.Warn, Theme.IWarn);
            warnChip.Margin = new Thickness(0, 6, 0, 0); warnChip.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetRow(warnChip, kr); Grid.SetColumnSpan(warnChip, 2); keyGrid.Children.Add(warnChip); kr++;
        }
        var keysTab = U.Col(keyGrid, U.Text(Loc.L("Alle Befehle auch per Rechtsklick auf das Symbol unten rechts im Infobereich.", "All commands are also available by right-clicking the icon in the notification area."), 12, fg: Theme.Muted, margin: new Thickness(0, 10, 0, 0)));

        var (tabTrack, tabs) = U.Segmented("stab", 0, (Theme.IHistory, Loc.L("Zuletzt", "Recent")), (Theme.IKeyboard, Loc.L("Tastenkürzel", "Shortcuts")));
        var openFolder = U.Btn(Theme.IFolder, null, () => { var d = AppPaths.DefaultSessionsFolder(); Directory.CreateDirectory(d); Process.Start("explorer.exe", $"\"{d}\""); }, Loc.L("Aufnahmeordner im Explorer öffnen", "Open the recordings folder in Explorer"), "Ghost");
        openFolder.Margin = new Thickness(0);
        var allRec = U.Btn(Theme.IHistory, null, () => UiApp.Instance.ShowLibrary(), Loc.L("Alle Aufnahmen (Strg+L): suchen, umbenennen, packen, löschen", "All recordings (Ctrl+L): search, rename, pack, delete"), "Ghost");
        allRec.Margin = new Thickness(0);
        var sideHead = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var headButtons = U.Row(allRec, openFolder);
        DockPanel.SetDock(headButtons, Dock.Right); sideHead.Children.Add(headButtons); sideHead.Children.Add(tabTrack);
        var tabHost = new Grid();
        tabHost.Children.Add(_recent); tabHost.Children.Add(keysTab);
        keysTab.Visibility = Visibility.Hidden;   // „Hidden“ statt „Collapsed“: beide Reiter gleich hoch, nichts springt
        tabs[0].Checked += (_, _) => { _recent.Visibility = Visibility.Visible; keysTab.Visibility = Visibility.Hidden; Anim.Refresh(_recent); };
        tabs[1].Checked += (_, _) => { _recent.Visibility = Visibility.Hidden; keysTab.Visibility = Visibility.Visible; Anim.Refresh(keysTab); };
        var side = new Border
        {
            Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 16, 18, 16), Child = U.Col(sideHead, tabHost), VerticalAlignment = VerticalAlignment.Top
        };

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 290 });
        columns.Children.Add(main);
        Grid.SetColumn(side, 2); columns.Children.Add(side);

        // ---------- Fuß ----------
        string folder = AppPaths.DefaultSessionsFolder();
        var foot = new DockPanel { Margin = new Thickness(2, 16, 0, 0) };
        var ver = U.Text(Loc.F("Version {0} · offline · kostenlos", "Version {0} · offline · free", AppInfo.Version), 11.5, fg: Theme.Muted);
        DockPanel.SetDock(ver, Dock.Right); foot.Children.Add(ver);
        var folderText = U.Text(folder, 11.5, fg: Theme.Muted, margin: new Thickness(6, 0, 12, 0)); folderText.TextWrapping = TextWrapping.NoWrap; folderText.TextTrimming = TextTrimming.CharacterEllipsis; folderText.ToolTip = folder;
        var folderRow = U.Row(U.Icon(Theme.IFolder, 12, Theme.Muted), folderText);
        if (AppPaths.LooksSynced(folder)) folderRow.Children.Add(U.Chip(Loc.L("synchronisierter Ordner", "synced folder"), Theme.WarnSoft, Theme.Warn, Theme.IWarn));
        foot.Children.Add(folderRow);

        var root = new StackPanel { Margin = new Thickness(32, 20, 32, 18) };
        root.Children.Add(header); root.Children.Add(hero); root.Children.Add(columns); root.Children.Add(foot);
        // immer ganz sichtbar: Breite folgt dem Fenster, bei zu wenig Platz wird gleichmäßig verkleinert
        var fit = new Viewbox { Child = root, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, VerticalAlignment = VerticalAlignment.Top };
        var host = new Grid(); host.Children.Add(fit);
        host.SizeChanged += (_, e) => root.Width = Math.Max(1000, e.NewSize.Width);
        Content = Anim.WithAmbientGlow(host, Theme.AccentColor);

        Anim.Appear(header, 0, 6); Anim.Appear(hero, 70, 12); Anim.Appear(main, 160, 14); Anim.Appear(side, 240, 14); Anim.Appear(foot, 320, 6);
        UpdateProfile();
        FillRecent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !(Keyboard.FocusedElement is TextBox or Button or ComboBox)) { Begin(ScopeMode.Screen); e.Handled = true; }
            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { OpenSession(); e.Handled = true; }
            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control) { UiApp.Instance.ShowLibrary(); e.Handled = true; }
        };
        Activated += (_, _) => { UpdateProfile(); FillRecent(); UpdateLastRegionLink(); };
    }

    // =====================================================================================
    // Bausteine
    // =====================================================================================

    /// <summary>Begrüßung je Tageszeit, wenn möglich mit Vornamen aus dem Windows-Konto.</summary>
    static string Greeting()
    {
        int h = DateTime.Now.Hour;
        string g = h < 5 ? Loc.L("Guten Abend", "Good evening") : h < 11 ? Loc.L("Guten Morgen", "Good morning") : h < 18 ? Loc.L("Guten Tag", "Good afternoon") : Loc.L("Guten Abend", "Good evening");
        string? first = null;
        try
        {
            var sb = new System.Text.StringBuilder(256); int len = sb.Capacity;
            if (GetUserNameEx(3 /*NameDisplay*/, sb, ref len) && sb.ToString().Trim() is { Length: > 0 } full)
                first = full.Contains(',') ? full.Split(',')[1].Trim().Split(' ')[0] : full.Split(' ')[0];
        }
        catch { }
        return first is { Length: > 1 } && first.All(c => char.IsLetter(c) || c == '-') ? $"{g}, {first}." : $"{g}.";
    }

    [DllImport("secur32.dll", CharSet = CharSet.Unicode)] static extern bool GetUserNameEx(int format, System.Text.StringBuilder name, ref int size);

    /// <summary>Fläche mit Kopf (Symbol, Titel, optional rechts eine Aktion).</summary>
    static Border Panel(string title, string glyph, UIElement body, FrameworkElement? action)
    {
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        if (action != null) { DockPanel.SetDock(action, Dock.Right); head.Children.Add(action); action.VerticalAlignment = VerticalAlignment.Center; }
        var t = U.Text(title, 15, true); t.FontFamily = Theme.Display; t.VerticalAlignment = VerticalAlignment.Center;
        var ic = U.Icon(glyph, 14, Theme.AccentText);
        if (glyph == Theme.IRecord) Anim.Breathe(ic, 0.3, 1800);   // „Neue Aufnahme“: ruhig pulsierender Aufnahmepunkt
        head.Children.Add(U.Row(ic, new Border { Width = 10 }, t));
        return new Border
        {
            Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(22, 18, 22, 20), Child = U.Col(head, body), VerticalAlignment = VerticalAlignment.Top
        };
    }

    /// <summary>„① Titel“ – Schritt innerhalb von „Neue Aufnahme“.</summary>
    static FrameworkElement StepLabel(string n, string title)
    {
        var num = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Theme.AccentSoft, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = n, FontFamily = Theme.Mono, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Theme.AccentText, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var t = U.Text(title, 14.5, true); t.VerticalAlignment = VerticalAlignment.Center;
        return U.Row(num, t);
    }

    /// <summary>Kompakte Moduskarte: Symbol links, Name und Zielgruppe rechts (Auswahl mit Haken).</summary>
    static RadioButton ModeCard(string glyph, string title, string audience, bool selected)
    {
        var name = U.Text(title, 14, true); name.FontFamily = Theme.Display; name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
        var text = U.Col(name); text.Margin = new Thickness(9, 0, 0, 0); text.VerticalAlignment = VerticalAlignment.Center;
        var row = new DockPanel();
        var badge = U.IconBadge(glyph, 28, Theme.Surface2, Theme.Fg2, 13);
        DockPanel.SetDock(badge, Dock.Left); row.Children.Add(badge); row.Children.Add(text);
        return new RadioButton { GroupName = "purpose", Style = Theme.Style("PurposeCard"), Content = row, IsChecked = selected, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(12, 11, 10, 11) };
    }

    /// <summary>Hauptkachel: ganzer Bildschirm – deutlich hervorgehoben, Eingabetaste startet.</summary>
    Button StartTile()
    {
        var icon = U.Icon(Theme.IMonitor, 22, Theme.OnAccent); icon.HorizontalAlignment = HorizontalAlignment.Left;
        var head = U.Text(Loc.L("Ganzer Bildschirm", "Full screen"), 17, true, Theme.OnAccent, new Thickness(0, 10, 0, 2)); head.FontFamily = Theme.Display;
        var hint = U.Row(U.Kbd(Loc.Keys("Enter"), new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), Theme.OnAccent, new SolidColorBrush(Color.FromArgb(90, 255, 255, 255))), U.Text(Loc.L("Aufnahme starten", "Start recording"), 12.5, fg: Theme.OnAccent, margin: new Thickness(6, 0, 0, 0)));
        ((FrameworkElement)hint.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        hint.Margin = new Thickness(0, 6, 0, 0);
        var ac = Theme.AccentColor;
        Color Mix(Color c, double t) => Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));
        var rest = new LinearGradientBrush(ac, Mix(ac, 0.22), new Point(0, 1), new Point(1, 0));
        var hot = new LinearGradientBrush(Mix(ac, 0.08), Mix(ac, 0.32), new Point(0, 1), new Point(1, 0));
        var b = new Button { Style = Theme.Style("Tile"), Content = U.Col(icon, head, hint), Background = rest, BorderBrush = Brushes.Transparent, Foreground = Theme.OnAccent, MinHeight = 108, Padding = new Thickness(18, 14, 18, 14), ToolTip = Loc.L("Alles auf dem Monitor, auf dem gerade gearbeitet wird (Eingabetaste)", "Everything on the monitor you are working on (Enter key)") };
        b.MouseEnter += (_, _) => b.Background = hot;
        b.MouseLeave += (_, _) => b.Background = rest;
        Anim.Glow(b, ac, 0.28, 0.55, 34);
        b.Click += (_, _) => { try { Begin(ScopeMode.Screen); } catch (Exception ex) { UiApp.ShowError(ex); } };
        return b;
    }

    static Button Tile(string glyph, string title, string hint, string tip, Action click)
    {
        var icon = U.Icon(glyph, 22, Theme.Fg2); icon.HorizontalAlignment = HorizontalAlignment.Left;
        var head = U.Text(title, 15.5, true, margin: new Thickness(0, 10, 0, 2)); head.FontFamily = Theme.Display;
        var b = new Button { Style = Theme.Style("Tile"), Content = U.Col(icon, head, U.Text(hint, 12, fg: Theme.Muted)), MinHeight = 108, Background = Theme.Surface, Padding = new Thickness(18, 14, 18, 14), ToolTip = tip };
        b.Click += (_, _) => { try { click(); } catch (Exception ex) { UiApp.ShowError(ex); } };
        return b;
    }

    /// <summary>Detailgrad als vier Balken plus Bezeichnung.</summary>
    static void Meter(StackPanel target, int bars, string label)
    {
        target.Children.Clear();
        for (int i = 0; i < 4; i++)
            target.Children.Add(new Border { Width = 16, Height = 6, CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center, Background = i < bars ? Theme.Accent : Theme.LineStrong });
        target.Children.Add(U.Text(label, 12.5, true, Theme.Fg, new Thickness(8, 0, 0, 1)));
    }

    /// <summary>Was wird aufgezeichnet – als kurze Chips (Einzelheiten im Tooltip).</summary>
    static void DetailChips(WrapPanel target, ICollection<string> off, string changeFrames, bool appContext)
    {
        target.Children.Clear();
        bool On(params string[] keys) => keys.Any(k => !off.Contains(k));
        void C(string text, bool on, string tip)
        {
            var c = on ? U.Chip(text, Theme.Surface, Theme.Fg2, Theme.ICheck) : U.Chip(text, Brushes.Transparent, Theme.Muted);
            c.Margin = new Thickness(0, 0, 6, 4); c.Padding = new Thickness(8, 3, 9, 4); c.ToolTip = tip;
            if (!on) { c.BorderBrush = Theme.LineStrong; c.BorderThickness = new Thickness(1); }
            target.Children.Add(c);
        }
        C(Loc.L("Klicks", "Clicks"), On("clicks", "wheel"), Loc.L("Mausklicks mit Bild und angeklicktem Element, Scrollen", "Mouse clicks with image and clicked element, scrolling"));
        C(On("typing", "hotkeys", "keys") ? Loc.L("Tastatur", "Keyboard") : Loc.L("ohne Tastatur", "no keyboard"), On("typing", "hotkeys", "keys"), Loc.L("Getippter Text, Tastenkombinationen und Einzeltasten (Passwortfelder immer maskiert)", "Typed text, key combinations and single keys (password fields always masked)"));
        C(On("moves") ? Loc.L("Mauswege", "Mouse paths") : Loc.L("ohne Mauswege", "no mouse paths"), On("moves"), Loc.L("Mausbewegungen und Verweilen über Elementen – nützlich für KI und Fehlersuche", "Mouse movements and hovering over elements – useful for AI and troubleshooting"));
        C(Loc.L("Zusatzbilder: ", "Extra images: ") + char.ToLower(DetailPresets.ChangeFramesName(changeFrames)[0]) + DetailPresets.ChangeFramesName(changeFrames)[1..], true, DetailPresets.ChangeFramesRule(changeFrames) + "\n" + DetailPresets.BaseRule());
        C(appContext ? Loc.L("Programmkontext erweitert", "Extended program context") : Loc.L("Programmkontext Basis", "Basic program context"), true, appContext
            ? Loc.L("Zusätzlich Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument, Statusleiste – für AutoHotkey und KI", "Also menu paths, lists, Explorer folder, browser address, Office document, status bar – for AutoHotkey and AI")
            : Loc.L("Fenster und angeklicktes Element – reicht für Anleitungen", "Window and clicked element – enough for guides"));
    }

    static readonly (string id, string name, string result)[] AfterStops =
    {
        ("viewer", Loc.L("Editor öffnen", "Open editor"), Loc.L("Anleitung im Editor – Export in jedes Format", "Guide in the editor – export to any format")),
        ("export", Loc.L("KI-Paket exportieren", "Export AI package"), Loc.L("KI-Paket: Protokoll, Bilder und Lesehilfe", "AI package: log, images and reading guide")),
        ("supportPackage", Loc.L("Support-Paket (ZIP) erstellen", "Create support package (ZIP)"), Loc.L("ZIP-Paket: Anleitung (HTML) + KI-Paket", "ZIP package: guide (HTML) + AI package")),
        ("folder", Loc.L("Aufnahmeordner öffnen", "Open recording folder"), Loc.L("Aufnahme im Ordner – später im Editor öffnen", "Recording in its folder – open it in the editor later")),
    };

    void UpdateCustomCard() { if (_profiles != null && ProfileIndex == 3) UpdateProfile(); }

    /// <summary>Inhalt „Eigener Detailgrad“: Eingabearten, Zusatzbilder, Programmkontext, nach dem Stopp.</summary>
    internal FrameworkElement BuildCustomContent()
    {
        var st = AppSettings.Load();
        void Save(Action<AppSettings> change) { var x = AppSettings.Load(); change(x); x.Save(); UpdateCustomCard(); }
        var col = new StackPanel();
        col.Children.Add(U.Text(Loc.L("Wird gespeichert und gilt für den Modus „Eigene“. Ausgeschaltetes wird gar nicht erst aufgezeichnet.", "Saved and applied to the “Custom” mode. Anything switched off is not recorded at all."), 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 12)));
        col.Children.Add(RecordChoice.Build(st.Record, () => Save(x => x.Record = new Dictionary<string, bool>(st.Record))));
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(24) : new GridLength(1, GridUnitType.Star) });
        int cf = st.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 };
        var (cfTrack, cfs) = U.Segmented("cfr", cf, (null, Loc.L("Nur große", "Large only")), (null, Loc.L("Auch kleine", "Small too")), (null, Loc.L("Jede Änderung", "Every change")));
        var cfHint = U.Text(DetailPresets.ChangeFramesRule(st.ChangeFrames), 11.5, fg: Theme.Muted, margin: new Thickness(0, 6, 0, 0));
        for (int i = 0; i < 3; i++) { string v = new[] { "few", "normal", "many" }[i]; cfs[i].Checked += (_, _) => { cfHint.Text = DetailPresets.ChangeFramesRule(v); Save(x => x.ChangeFrames = v); }; }
        var left = U.Field(Loc.L("Zusatzbilder ohne Eingabe", "Extra images without input"), U.Col(cfTrack, cfHint));
        var ctx = new CheckBox { Content = Loc.L("Erweiterter Programmkontext", "Extended program context"), IsChecked = st.CustomAppContext, Margin = new Thickness(0, 4, 0, 0), ToolTip = Loc.L("Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument und Statusleiste – nötig für gute AutoHotkey-Skripte", "Menu paths, lists, Explorer folder, browser address, Office document and status bar – needed for good AutoHotkey scripts") };
        ctx.Checked += (_, _) => Save(x => x.CustomAppContext = true); ctx.Unchecked += (_, _) => Save(x => x.CustomAppContext = false);
        var after = new ComboBox { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var a in AfterStops) after.Items.Add(a.name);
        after.SelectedIndex = Math.Max(0, Array.FindIndex(AfterStops, a => a.id == st.CustomAfterStop));
        after.SelectionChanged += (_, _) => { string id = AfterStops[Math.Max(0, after.SelectedIndex)].id; Save(x => x.CustomAfterStop = id); };
        var right = U.Col(U.Field(Loc.L("Programmkontext", "Program context"), ctx), U.Field(Loc.L("Nach dem Stopp", "After stopping"), after, margin: new Thickness(0)));
        Grid.SetColumn(left, 0); Grid.SetColumn(right, 2);
        grid.Children.Add(left); grid.Children.Add(right);
        col.Children.Add(grid);
        return col;
    }

    void ShowCustomDialog()
    {
        var w = new Window { Title = Loc.L("Eigener Detailgrad", "Custom level of detail"), Width = Math.Min(760, Theme.ActiveWorkArea().Width - 40), SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        U.Dialog(w, Loc.L("Eigener Detailgrad", "Custom level of detail"), BuildCustomContent(), U.Primary(Loc.L("Fertig", "Done"), () => w.Close()));
        w.ShowDialog();
        UpdateProfile();
    }

    /// <summary>„Ziel und Titel“: optionale Angaben zur Aufnahme in einem kleinen Fenster.</summary>
    void ShowGoalDialog()
    {
        TextBox Box(string v, string tag, bool multi) => new() { Text = v, Tag = tag, MaxLength = multi ? 2000 : 200, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 60 : 0, VerticalContentAlignment = multi ? VerticalAlignment.Top : VerticalAlignment.Center };
        var goal = Box(_goalText, Loc.L("z. B. Neuen Kontakt im Adressbuch anlegen", "e.g. Create a new contact in the address book"), true);
        var expected = Box(_expectedText, Loc.L("z. B. Der Kontakt erscheint in der Liste", "e.g. The contact appears in the list"), true);
        var reference = Box(_referenceText, Loc.L("z. B. Ticketnummer", "e.g. Ticket number"), false);
        var title = Box(_titleText, Loc.L("sonst automatisch aus der Aufnahme", "otherwise taken from the recording automatically"), false);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        void G(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); grid.Children.Add(e); }
        G(U.Field(Loc.L("Was wird gezeigt?", "What is shown?"), goal), 0, 0); G(U.Field(Loc.L("Erwartetes Ergebnis", "Expected result"), expected), 0, 2);
        G(U.Field(Loc.L("Bezug", "Reference"), reference, margin: new Thickness(0)), 1, 0); G(U.Field(Loc.L("Titel der Aufnahme", "Recording title"), title, margin: new Thickness(0)), 1, 2);
        var w = new Window { Title = Loc.L("Ziel und Titel", "Goal and title"), Width = Math.Min(680, Theme.ActiveWorkArea().Width - 40), SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var body = U.Col(U.Text(Loc.L("Optional. Hilft beim Verstehen der Aufnahme – für Menschen und für KI.", "Optional. Helps to understand the recording – for people and for AI."), 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 14)), grid);
        U.Dialog(w, Loc.L("Ziel der Aufnahme", "Recording goal"), body,
            U.Primary(Loc.L("Übernehmen", "Apply"), () => { _goalText = goal.Text; _expectedText = expected.Text; _referenceText = reference.Text; _titleText = title.Text.Trim(); w.Close(); }),
            U.Btn(Loc.L("Leeren", "Clear"), () => { goal.Clear(); expected.Clear(); reference.Clear(); title.Clear(); }), U.Btn(Loc.L("Abbrechen", "Cancel"), () => w.Close()));
        w.Loaded += (_, _) => goal.Focus();
        w.ShowDialog();
        UpdateGoalLink();
    }

    void UpdateGoalLink()
    {
        bool set = (_goalText + _expectedText + _referenceText + _titleText).Trim().Length > 0;
        bool recommended = ProfileIndex is 1 or 2;
        _goalLink.Content = U.IconText(set ? Theme.ICheck : Theme.IFlag, set ? Loc.L("Ziel und Titel ✓", "Goal and title ✓") : recommended ? Loc.L("Ziel und Titel (empfohlen) …", "Goal and title (recommended) …") : Loc.L("Ziel und Titel …", "Goal and title …"));
    }

    /// <summary>„Anpassen …“ bei einem festen Modus: Detailgrad in „Eigene“ übernehmen; bei „Eigene“: Einstellungen öffnen.</summary>
    void CustomizeFromPreset()
    {
        if (ProfileIndex == 3) { ShowCustomDialog(); return; }
        var p = DetailPresets.For(Ids[ProfileIndex])!;
        var x = AppSettings.Load();
        x.Record = RecordingOptions.Channels.ToDictionary(c => c.key, c => !p.Off.Contains(c.key));
        x.ChangeFrames = p.ChangeFrames; x.CustomAppContext = p.AppContext;
        x.CustomAfterStop = Ids[ProfileIndex] switch { "support" => "supportPackage", "aiTrace" => "export", _ => "viewer" };
        x.Save();
        _profiles[3].IsChecked = true;
        ShowCustomDialog();
    }


    /// <summary>Detailstreifen zum gewählten Modus.</summary>
    void UpdateProfile()
    {
        if (_profiles == null || _recLink == null) return;
        int i = ProfileIndex;
        bool changed = _modeText.Text != Modes[i].text && _modeText.Text.Length > 0;
        _modeText.Text = Modes[i].text;
        if (changed) { Anim.Refresh(_strip, 0.3, 260); Anim.Stagger(_chips, 40, 35, 4); }
        _recLink.Content = U.IconText(Theme.IEdit, i == 3 ? Loc.L("Einstellen …", "Set up …") : Loc.L("Anpassen …", "Customize …"));
        _recLink.ToolTip = i == 3 ? Loc.L("Eingabearten, Zusatzbilder, Programmkontext und „nach dem Stopp“ festlegen", "Set input types, extra images, program context and “after stopping”") : Loc.L("Diesen Detailgrad als Ausgangspunkt für den Modus „Eigene“ übernehmen", "Use this level of detail as the starting point for the “Custom” mode");
        if (i == 3)
        {
            var st = AppSettings.Load();
            var off = RecordingOptions.Channels.Where(c => !st.Records(c.key)).Select(c => c.key).ToList();
            Meter(_meter, DetailPresets.BarsFor(off, st.ChangeFrames, st.CustomAppContext), Loc.L("Eigene", "Custom"));
            DetailChips(_chips, off, st.ChangeFrames, st.CustomAppContext);
        }
        else
        {
            var p = DetailPresets.For(Ids[i])!;
            Meter(_meter, p.Bars, p.Id switch { "guide" => Loc.L("Schlank", "Lean"), "support" => Loc.L("Ausführlich", "Detailed"), "aiTrace" => Loc.L("Vollständig", "Complete"), _ => p.Level });
            DetailChips(_chips, p.Off, p.ChangeFrames, p.AppContext);
        }
        UpdateGoalLink();
    }

    /// <summary>Zuletzt verwendet: bis zu vier Aufnahmen mit Vorschaubild; ohne Aufnahmen „Erste Schritte“.</summary>
    void FillRecent()
    {
        _recent.Children.Clear();
        var list = AppSettings.Load().RecentSessions.Where(Session.LooksLikeSession).Take(4).ToList();
        _stats.Inlines.Clear();
        int total = 0;
        try { var d = AppPaths.DefaultSessionsFolder(); if (Directory.Exists(d)) total = Directory.EnumerateDirectories(d).Count(Session.LooksLikeSession); } catch { }
        total = Math.Max(total, list.Count);
        var parts = new List<string>();
        if (total > 0) parts.Add(Loc.N(total, "1 Aufnahme", "{0} Aufnahmen", "1 recording", "{0} recordings"));
        if (list.Count > 0 && Json.ReadObject(System.IO.Path.Combine(list[0], "manifest.json")) is { } last)
            try { parts.Add(Loc.F("zuletzt {0}", "last {0}", Loc.Relative(Json.ParseUtc(last.Str("createdUtc")).ToLocalTime()))); } catch { }
        parts.Add(AppSettings.Load().PrivacyLevel == "strict" ? Loc.L("Schutz: streng", "Protection: strict") : Loc.L("Passwortfelder werden maskiert", "Password fields are masked"));
        _stats.Inlines.Add(new System.Windows.Documents.Run("●  ") { Foreground = Theme.Success });
        _stats.Inlines.Add(new System.Windows.Documents.Run(string.Join("   ·   ", parts)) { Foreground = Theme.Muted });

        if (list.Count == 0)
        {
            int n = 0;
            foreach (var (head, text) in new[] { (Loc.L("Modus wählen", "Choose a mode"), Loc.L("Im Zweifel „Anleitung“.", "When in doubt, “Guide”.")), (Loc.L("Bereich anklicken", "Click an area"), Loc.L("Die Aufnahme startet sofort.", "Recording starts right away.")),
                                                   (Loc.L("Vorführen und beenden", "Demonstrate and stop"), Loc.F("{0} stoppt.", "{0} stops it.", RecordingController.Key("stop"))), (Loc.L("Nachbearbeiten", "Edit afterwards"), Loc.L("Texte anpassen, als PDF oder Word speichern.", "Adjust texts, save as PDF or Word.")) })
            {
                var num = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Theme.AccentSoft, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Top,
                    Child = new TextBlock { Text = (++n).ToString(), FontFamily = Theme.Mono, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Theme.AccentText, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
                DockPanel.SetDock(num, Dock.Left); row.Children.Add(num);
                row.Children.Add(U.Col(U.Text(head, 13, true), U.Text(text, 12, fg: Theme.Muted)));
                _recent.Children.Add(row);
            }
            return;
        }
        foreach (var p in list)
        {
            var man = Json.ReadObject(System.IO.Path.Combine(p, "manifest.json"));
            string t = man?.Str("title") is { Length: > 0 } title ? title : System.IO.Path.GetFileName(p);
            var meta = new List<string>();
            if (man != null)
            {
                try { meta.Add(Loc.Relative(Json.ParseUtc(man.Str("createdUtc")).ToLocalTime())); } catch { }
                if (man.Obj("summary")?.LongN("primarySteps") is long n) meta.Add(Loc.N(n, "1 Schritt", "{0} Schritte", "1 step", "{0} steps"));
            }
            var thumb = new Image { Stretch = Stretch.UniformToFill, Width = 64, Height = 40 };
            RenderOptions.SetBitmapScalingMode(thumb, BitmapScalingMode.HighQuality);
            var thumbBox = new Border { Width = 66, Height = 42, CornerRadius = new CornerRadius(4), Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0),
                Child = new Grid { Children = { U.Icon(Theme.IDoc, 14, Theme.Muted), thumb } }, ClipToBounds = true };
            LoadThumb(p, thumb);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var text = U.Col(new TextBlock { Text = t, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }, Mono(string.Join("  ·  ", meta)));
            text.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(text, 1);
            g.Children.Add(thumbBox); g.Children.Add(text);
            var b = new Button { Style = Theme.Style("RowButton"), Content = g, ToolTip = p, Margin = new Thickness(-10, 0, -10, 2), Padding = new Thickness(10, 6, 10, 6) };
            b.Click += (_, _) => UiApp.Instance.OpenViewer(p);
            _recent.Children.Add(b);
        }
    }

    static TextBlock Mono(string text) => new() { Text = text, FontFamily = Theme.Mono, FontSize = 11, Foreground = Theme.Muted, Margin = new Thickness(0, 3, 0, 0) };

    /// <summary>Vorschaubild einer Aufnahme im Hintergrund laden – über die Leseschicht, also mit allen Schwärzungen.</summary>
    static void LoadThumb(string path, Image target)
    {
        Task.Run(() =>
        {
            try
            {
                if (StepRecorder.ReadLayer.SessionCatalog.Thumbnail(path, 192) is not { } png) return null;
                using var ms = new MemoryStream(png);
                var bi = new System.Windows.Media.Imaging.BitmapImage(); bi.BeginInit(); bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
                return (System.Windows.Media.Imaging.BitmapSource)bi;
            }
            catch { return null; }
        }).ContinueWith(t => { if (t.Result is { } src) { target.Source = src; Anim.Refresh(target, 0, 320); } }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    RecordingOptions BuildOptions(ScopeMode scope)
    {
        var o = UiApp.OptionsFromSettings(scope);
        UiApp.ApplyProfile(o, Ids[ProfileIndex]);
        o.Scope = scope;
        o.Monitors = _monitor.SelectedIndex switch { 0 => "active", 1 => "all", var i => "M" + (i - 1) };
        o.GoalDescription = _goalText; o.GoalExpected = _expectedText; o.GoalReference = _referenceText;
        o.Title = _titleText;
        var s = AppSettings.Load(); s.LastProfile = o.ProfileId; s.Save();
        return o;
    }

    Button _lastRegionLink = null!;

    /// <summary>„Letzter Bereich · B × H“ – nur, wenn der gemerkte Bereich auf den heutigen Monitoren noch passt.</summary>
    void UpdateLastRegionLink()
    {
        var (r, fit) = RegionMemory.Resolve(AppState.Load().LastRegion, ScreenGrabber.GetMonitors());
        _lastRegionLink.Visibility = r == null ? Visibility.Collapsed : Visibility.Visible;
        if (r is { } rr && _lastRegionLink.Content is StackPanel sp && sp.Children.Count > 0)
        {
            string text = Loc.F("Letzter Bereich · {0} × {1}", "Last area · {0} × {1}", rr.Width, rr.Height) + (fit == RegionFit.Clamped ? Loc.L(" (angepasst)", " (adjusted)") : "");
            if (sp.Children.Count > 1 && sp.Children[1] is TextBlock tb) tb.Text = text;
            else sp.Children.Add(new TextBlock { Text = text, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        }
    }

    void BeginLastRegion()
    {
        var (r, _) = RegionMemory.Resolve(AppState.Load().LastRegion, ScreenGrabber.GetMonitors());
        Begin(ScopeMode.Region, r);
    }

    void Begin(ScopeMode scope, System.Drawing.Rectangle? preset = null)
    {
        var o = BuildOptions(scope);
        if (scope == ScopeMode.App)
        {
            Hide();
            var pick = WindowPicker.Pick();
            if (pick == null) { Show(); return; }
            o.AppProcess = pick.Value.process; o.AppPid = (int)pick.Value.pid; o.AppWindow = 0;
        }
        else if (scope == ScopeMode.Region)
        {
            Hide();
            var r = RegionPicker.Pick(preset);
            if (r == null) { Show(); return; }
            o.Region = r;
        }
        _goalText = _expectedText = _referenceText = _titleText = "";
        UiApp.Instance.StartRecording(o);
    }

    public void ShowSupport()
    {
        var r = Msg.Show(Loc.F("Problem aufnehmen\n\n1. Aufnahme starten und das Problem vorführen.\n2. Fehlerstelle mit {0} markieren, mit {1} beenden.\n3. Eingaben durchsehen und das ZIP erstellen.\n\nDas Programm versendet nichts – Sie geben das ZIP selbst weiter. Aufnahme jetzt starten?",
                "Record a problem\n\n1. Start recording and demonstrate the problem.\n2. Mark the error with {0}, stop with {1}.\n3. Review the input and create the ZIP.\n\nThe program sends nothing – you pass the ZIP on yourself. Start recording now?", RecordingController.Key("markerError"), RecordingController.Key("stop")),
            Loc.L("Support-Paket", "Support package"), MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (r != MessageBoxResult.OK) return;
        _profiles[1].IsChecked = true;
        Begin(ScopeMode.Screen);
    }

    void OpenSession()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.L("Aufnahme", "Recording") + " (manifest.json;*.steprec)|manifest.json;*.steprec", InitialDirectory = AppPaths.DefaultSessionsFolder() };
        if (dlg.ShowDialog(this) == true)
        {
            var p = dlg.FileName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase) ? System.IO.Path.GetDirectoryName(dlg.FileName)! : dlg.FileName;
            UiApp.Instance.OpenViewer(p);
        }
    }

    void ShowSettings()
    {
        for (int i = 0; i < 3; i++)
        {
            var w = new SettingsWindow(i == 0 ? 0 : 4) { Owner = this };
            w.ShowDialog();
            if (!w.ReopenRequested) break;
        }
        FillRecent(); UpdateProfile();
    }

    void ShowHelp() => HelpDialog.Show(this);

    protected override void OnClosed(EventArgs e) { base.OnClosed(e); UiApp.Instance.MaybeShutdown(); }
}

/// <summary>Auswahl, welche Eingabearten aufgezeichnet werden (Mausgruppe oben, Tastaturgruppe unten).</summary>
static class RecordChoice
{
    /// <summary>Baut die Auswahl; jede Änderung wird in <paramref name="target"/> geschrieben und <paramref name="changed"/> gemeldet.</summary>
    public static FrameworkElement Build(Dictionary<string, bool> target, Action? changed)
    {
        var col = new StackPanel();
        var warn = U.Text(Loc.L("Alles ausgeschaltet – es entstehen nur noch Bilder bei Bildschirmänderungen.", "Everything is switched off – only images of screen changes will be created."), 12, fg: Theme.Warn, margin: new Thickness(0, 2, 0, 0));
        void UpdWarn() => warn.Visibility = RecordingOptions.Channels.All(c => !target.GetValueOrDefault(c.key, true)) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (group, glyph, keys) in new[] { (Loc.L("Maus", "Mouse"), Theme.IMouse, new[] { "clicks", "wheel", "moves" }), (Loc.L("Tastatur", "Keyboard"), Theme.IKeyboard, new[] { "typing", "hotkeys", "keys" }) })
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            var head = U.Row(U.Icon(glyph, 13, Theme.Muted), U.Text(group, 12.5, true, Theme.Fg2, new Thickness(7, 0, 0, 0)));
            head.VerticalAlignment = VerticalAlignment.Top; head.Margin = new Thickness(0, 9, 0, 0);
            grid.Children.Add(head);
            for (int i = 0; i < keys.Length; i++)
            {
                var (key, name, hint) = RecordingOptions.Channels.First(c => c.key == keys[i]);
                var cb = new CheckBox { IsChecked = target.GetValueOrDefault(key, true), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 10, 0) };
                var text = U.Col(U.Text(name, 13, true), U.Text(hint, 11.5, fg: Theme.Muted, margin: new Thickness(0, 2, 0, 0)));
                var item = new DockPanel();
                DockPanel.SetDock(cb, Dock.Left); item.Children.Add(cb); item.Children.Add(text);
                var box = new Border { Child = item, Padding = new Thickness(10, 7, 10, 8), Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = hint };
                box.MouseEnter += (_, _) => box.Background = Theme.HoverBg;
                box.MouseLeave += (_, _) => box.Background = Brushes.Transparent;
                text.MouseLeftButtonUp += (_, _) => cb.IsChecked = cb.IsChecked != true;
                void Set() { target[key] = cb.IsChecked == true; UpdWarn(); changed?.Invoke(); }
                cb.Checked += (_, _) => Set(); cb.Unchecked += (_, _) => Set();
                Grid.SetColumn(box, i + 1); grid.Children.Add(box);
            }
            col.Children.Add(grid);
        }
        col.Children.Add(warn);
        UpdWarn();
        return col;
    }

    /// <summary>Kurzfassung für eine Zeile: „alles“ oder was fehlt.</summary>
    public static void Summary(TextBlock t, Dictionary<string, bool> rec)
    {
        var off = RecordingOptions.Channels.Where(c => !rec.GetValueOrDefault(c.key, true)).Select(c => c.name).ToList();
        var on = RecordingOptions.Channels.Where(c => rec.GetValueOrDefault(c.key, true)).Select(c => c.name).ToList();
        t.Inlines.Clear();
        t.Inlines.Add(new System.Windows.Documents.Run(Loc.L("Aufgezeichnet: ", "Recorded: ")) { FontWeight = FontWeights.SemiBold });
        t.Inlines.Add(new System.Windows.Documents.Run(off.Count == 0 ? Loc.L("alles – ", "everything – ") + string.Join(", ", on) : on.Count == 0 ? Loc.L("keine Eingaben", "no input") : string.Join(", ", on)));
        if (off.Count > 0 && on.Count > 0)
        {
            t.Inlines.Add(new System.Windows.Documents.Run(Loc.L("   ·   Nicht: ", "   ·   Not: ")) { FontWeight = FontWeights.SemiBold, Foreground = Theme.Warn });
            t.Inlines.Add(new System.Windows.Documents.Run(string.Join(", ", off)) { Foreground = Theme.Warn });
        }
    }
}

/// <summary>Hinweis-Pille für die Auswahl-Overlays.</summary>
static class OverlayUi
{
    public static readonly Brush PillBg = new SolidColorBrush(Color.FromArgb(0xEE, 0x17, 0x16, 0x14));
    public static readonly Brush DarkLine = new SolidColorBrush(Color.FromRgb(0x3B, 0x3A, 0x36));

    public static Border Pill(string glyph, string text)
    {
        var row = U.Row(U.Icon(glyph, 15, Brushes.White), U.Text(text, 14, false, Brushes.White, new Thickness(10, 0, 14, 0)), U.Kbd("Esc", new SolidColorBrush(Color.FromRgb(0x26, 0x25, 0x22)), Brushes.Gainsboro, DarkLine), U.Text(Loc.L("abbrechen", "cancel"), 13, false, Brushes.Gainsboro, new Thickness(4, 0, 0, 0)));
        foreach (var c in row.Children.OfType<FrameworkElement>()) c.VerticalAlignment = VerticalAlignment.Center;
        return new Border
        {
            Background = PillBg, CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 11, 18, 11), Child = row,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 48), Effect = Theme.Shadow(24, 4, 0.4)
        };
    }

    public static Border Label(TextBlock t)
    {
        t.Foreground = Brushes.White; t.FontSize = 13;
        return new Border { Background = PillBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 7), Child = t };
    }
}

/// <summary>Fensterauswahl: Bildschirm abgedunkelt, Fenster unter der Maus hervorgehoben; Klick wählt die Anwendung.</summary>
sealed class WindowPicker : Window
{
    readonly Rectangle _hl = new() { StrokeThickness = 3, RadiusX = 8, RadiusY = 8, Fill = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)) };
    readonly TextBlock _labelText = new();
    readonly Border _label;
    readonly Canvas _canvas = new();
    (nint hwnd, uint pid, string process)? _result, _current;
    readonly System.Drawing.Rectangle _virt;
    double _scale = 1;

    WindowPicker()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)); Topmost = true; ShowInTaskbar = false; Cursor = Cursors.Cross;
        FontFamily = Theme.UiFont;
        _hl.Stroke = Theme.Accent;
        _label = OverlayUi.Label(_labelText);
        _virt = ScreenGrabber.VirtualScreen(ScreenGrabber.GetMonitors());
        _canvas.Children.Add(_hl); _canvas.Children.Add(_label);
        Content = new Grid { Children = { _canvas, OverlayUi.Pill(Theme.IPointer, Loc.L("Anwendung anklicken, die aufgenommen werden soll", "Click the application to record")) } };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowPos(h, -1, _virt.X, _virt.Y, _virt.Width, _virt.Height, 0x0040);
            _scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1;
        };
        MouseMove += (_, _) => Track();
        MouseLeftButtonDown += (_, _) => { _result = _current; DialogResult = _result != null; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
    }

    void Track()
    {
        Win32.GetCursorPos(out var p);
        nint own = new WindowInteropHelper(this).Handle;
        nint found = 0;
        Win32.EnumWindows((h, _) =>
        {
            if (h == own || !Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return true;
            Win32.GetWindowThreadProcessId(h, out uint pid);
            if (pid == Environment.ProcessId) return true;
            if (!Win32.GetWindowRect(h, out var r) || r.Width < 50 || r.Height < 30) return true;
            if (p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom && Win32.GetWindowTitle(h).Length > 0) { found = h; return false; }
            return true;
        }, 0);
        if (found == 0) { _current = null; _hl.Visibility = Visibility.Hidden; _label.Visibility = Visibility.Hidden; return; }
        Win32.GetWindowThreadProcessId(found, out uint fpid);
        string proc = System.IO.Path.GetFileName(Win32.GetProcessPath(fpid) ?? "?");
        _current = (found, fpid, proc);
        Win32.GetWindowRect(found, out var wr);
        _hl.Visibility = Visibility.Visible; _label.Visibility = Visibility.Visible;
        Canvas.SetLeft(_hl, (wr.Left - _virt.X) / _scale); Canvas.SetTop(_hl, (wr.Top - _virt.Y) / _scale);
        _hl.Width = wr.Width / _scale; _hl.Height = wr.Height / _scale;
        bool excluded = RecordingOptions.DefaultExcluded.Contains(proc, StringComparer.OrdinalIgnoreCase);
        _labelText.Text = Win32.GetWindowTitle(found) + "\n" + Loc.F("{0} · alle Fenster dieser Anwendung", "{0} · all windows of this application", proc) + (excluded ? "\n" + Loc.L("ausgeschlossen – wird nicht aufgezeichnet", "excluded – not recorded") : "");
        Canvas.SetLeft(_label, (wr.Left - _virt.X) / _scale + 12); Canvas.SetTop(_label, (wr.Top - _virt.Y) / _scale + 12);
    }

    public static (nint hwnd, uint pid, string process)? Pick()
    {
        var w = new WindowPicker();
        return w.ShowDialog() == true ? w._result : null;
    }

    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);
}

/// <summary>
/// Bereichsauswahl: Rechteck aufziehen (mindestens 64×64, auf einem Monitor). Mit Vorgabe (letzter Bereich) im
/// Anpassmodus: Rahmen verschieben, an acht Griffen ziehen, Pfeiltasten (Umschalt: 10 px) – erst Eingabe startet.
/// </summary>
sealed class RegionPicker : Window
{
    readonly Rectangle _sel = new() { StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), Visibility = Visibility.Hidden };
    readonly List<Rectangle> _grips = new();
    readonly TextBlock _sizeText = new();
    readonly Border _size;
    readonly System.Drawing.Rectangle _virt;
    readonly bool _adjust;
    double _scale = 1;
    System.Drawing.Rectangle? _rect, _result;
    Win32.POINT _start;
    int _mode = -1;                       // -1 nichts, 0–7 Griff, 8 neu aufziehen, 9 verschieben
    System.Drawing.Rectangle _orig;

    RegionPicker(System.Drawing.Rectangle? preset)
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)); Topmost = true; ShowInTaskbar = false; Cursor = Cursors.Cross;
        FontFamily = Theme.UiFont;
        _sel.Stroke = Theme.Accent;
        _adjust = preset != null; _rect = preset;
        _size = OverlayUi.Label(_sizeText); _size.Visibility = Visibility.Hidden;
        _virt = ScreenGrabber.VirtualScreen(ScreenGrabber.GetMonitors());
        var c = new Canvas { Children = { _sel, _size } };
        for (int i = 0; i < 8; i++) { var g = new Rectangle { Width = 10, Height = 10, Fill = Brushes.White, Stroke = Theme.Accent, StrokeThickness = 1.5, Visibility = Visibility.Hidden, IsHitTestVisible = false }; _grips.Add(g); c.Children.Add(g); }
        string hint = _adjust
            ? Loc.L("Letzten Bereich anpassen: verschieben, an den Griffen ziehen, Pfeiltasten – Eingabe startet, Esc bricht ab", "Adjust the last area: move, drag the handles, arrow keys – Enter starts, Esc cancels")
            : Loc.L("Rechteck aufziehen, das aufgenommen werden soll", "Drag a rectangle to record");
        Content = new Grid { Children = { c, OverlayUi.Pill(Theme.ICrop, hint) } };
        SourceInitialized += (_, _) =>
        {
            SetWindowPos(new WindowInteropHelper(this).Handle, -1, _virt.X, _virt.Y, _virt.Width, _virt.Height, 0x0040);
            _scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1;
        };
        Loaded += (_, _) => { if (_rect != null) Draw(); };
        MouseLeftButtonDown += (_, _) =>
        {
            Win32.GetCursorPos(out _start);
            _mode = Hit(_start);
            if (_mode < 0) _mode = 8;
            _orig = _rect ?? default;
            CaptureMouse();
        };
        MouseMove += (_, _) =>
        {
            Win32.GetCursorPos(out var p);
            if (_mode < 0) { Cursor = Hit(p) switch { 9 => Cursors.SizeAll, 0 or 4 => Cursors.SizeNWSE, 2 or 6 => Cursors.SizeNESW, 1 or 5 => Cursors.SizeNS, 3 or 7 => Cursors.SizeWE, _ => Cursors.Cross }; return; }
            int dx = p.X - _start.X, dy = p.Y - _start.Y;
            var o = _orig;
            _rect = _mode switch
            {
                8 => System.Drawing.Rectangle.FromLTRB(Math.Min(p.X, _start.X), Math.Min(p.Y, _start.Y), Math.Max(p.X, _start.X), Math.Max(p.Y, _start.Y)),
                9 => new System.Drawing.Rectangle(o.X + dx, o.Y + dy, o.Width, o.Height),
                _ => Resize(o, _mode, dx, dy)
            };
            Draw();
        };
        MouseLeftButtonUp += (_, _) =>
        {
            if (_mode < 0) return;
            bool wasNew = _mode == 8;
            _mode = -1; ReleaseMouseCapture();
            if (!_adjust && wasNew) Confirm();      // ohne Vorgabe wie bisher: Loslassen startet
        };
        KeyDown += (_, e) =>
        {
            int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            switch (e.Key)
            {
                case Key.Escape: DialogResult = false; break;
                case Key.Enter: Confirm(); break;
                case Key.Left when _rect is { } r: _rect = r with { X = r.X - step }; Draw(); break;
                case Key.Right when _rect is { } r: _rect = r with { X = r.X + step }; Draw(); break;
                case Key.Up when _rect is { } r: _rect = r with { Y = r.Y - step }; Draw(); break;
                case Key.Down when _rect is { } r: _rect = r with { Y = r.Y + step }; Draw(); break;
                default: return;
            }
            e.Handled = true;
        };
    }

    static System.Drawing.Rectangle Resize(System.Drawing.Rectangle o, int grip, int dx, int dy)
    {
        int l = o.Left, t = o.Top, r = o.Right, b = o.Bottom;
        if (grip is 0 or 6 or 7) l += dx;
        if (grip is 2 or 3 or 4) r += dx;
        if (grip is 0 or 1 or 2) t += dy;
        if (grip is 4 or 5 or 6) b += dy;
        return System.Drawing.Rectangle.FromLTRB(Math.Min(l, r), Math.Min(t, b), Math.Max(l, r), Math.Max(t, b));
    }

    static System.Drawing.Point[] GripPoints(System.Drawing.Rectangle r) => new System.Drawing.Point[]
    {
        new(r.Left, r.Top), new(r.Left + r.Width / 2, r.Top), new(r.Right, r.Top), new(r.Right, r.Top + r.Height / 2),
        new(r.Right, r.Bottom), new(r.Left + r.Width / 2, r.Bottom), new(r.Left, r.Bottom), new(r.Left, r.Top + r.Height / 2)
    };

    /// <summary>Griff 0–7, 9 im Rahmen, -1 außerhalb.</summary>
    int Hit(Win32.POINT p)
    {
        if (_rect is not { } r) return -1;
        int tol = (int)Math.Ceiling(8 * _scale);
        var g = GripPoints(r);
        for (int i = 0; i < g.Length; i++) if (Math.Abs(p.X - g[i].X) <= tol && Math.Abs(p.Y - g[i].Y) <= tol) return i;
        return r.Contains(p.X, p.Y) ? 9 : -1;
    }

    void Draw()
    {
        if (_rect is not { } r) return;
        _sel.Visibility = Visibility.Visible; _size.Visibility = Visibility.Visible;
        Canvas.SetLeft(_sel, (r.X - _virt.X) / _scale); Canvas.SetTop(_sel, (r.Y - _virt.Y) / _scale); _sel.Width = r.Width / _scale; _sel.Height = r.Height / _scale;
        bool small = r.Width < RegionMemory.MinSize || r.Height < RegionMemory.MinSize;
        _sizeText.Text = small ? Loc.F("{0} × {1} px – mindestens 64 × 64", "{0} × {1} px – at least 64 × 64", r.Width, r.Height) : $"{r.Width} × {r.Height} px";
        Canvas.SetLeft(_size, (r.X - _virt.X) / _scale); Canvas.SetTop(_size, Math.Max(0, (r.Y - _virt.Y) / _scale - 36));
        var g = GripPoints(r);
        for (int i = 0; i < 8; i++)
        {
            _grips[i].Visibility = _adjust ? Visibility.Visible : Visibility.Hidden;
            Canvas.SetLeft(_grips[i], (g[i].X - _virt.X) / _scale - 5); Canvas.SetTop(_grips[i], (g[i].Y - _virt.Y) / _scale - 5);
        }
    }

    void Confirm()
    {
        if (_rect is not { } r || r.Width < RegionMemory.MinSize || r.Height < RegionMemory.MinSize) { if (_rect != null) Draw(); return; }
        var mons = ScreenGrabber.GetMonitors();
        var mon = RegionMemory.MonitorOf(r, mons) ?? mons.First();
        var cut = System.Drawing.Rectangle.Intersect(r, mon.Bounds);
        if (cut.Width < RegionMemory.MinSize || cut.Height < RegionMemory.MinSize) return;
        _result = cut;
        // für „Letzter Bereich“ merken (relativ zum Monitor)
        try { AppState.Update(s => s.LastRegion = RegionMemory.Save(cut, mon)); } catch { }
        DialogResult = true;
    }

    /// <param name="preset">Vorgabe (z. B. letzter Bereich): öffnet im Anpassmodus, gestartet wird erst mit Eingabe.</param>
    public static System.Drawing.Rectangle? Pick(System.Drawing.Rectangle? preset = null)
    {
        var w = new RegionPicker(preset);
        return w.ShowDialog() == true ? w._result : null;
    }

    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);
}

