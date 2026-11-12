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
        (Theme.IDoc, "Anleitung", "für Menschen", "Schlank – nur was Menschen zum Nachmachen brauchen."),
        (Theme.IWrench, "Support-Fall", "für den Helpdesk", "Ausführlich – alle Eingaben, damit der Helpdesk den Fehler nachvollziehen kann."),
        (Theme.IRobot, "KI-Spur", "für KI und Skripte", "Vollständig – so genau wie möglich, für KI-Assistenten und AutoHotkey."),
        (Theme.ISettings, "Eigene", "selbst festgelegt", "Selbst festgelegt – „Einstellen …“ öffnet die Auswahl."),
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
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0 && Session.LooksLikeSession(f[0])) UiApp.Instance.OpenViewer(f[0]); };
        var s = AppSettings.Load();

        // ---------- Kopfleiste ----------
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 22) };
        var actions = U.Row(U.Ghost(Theme.IOpen, "Öffnen", OpenSession, "Aufnahme öffnen (Strg+O) – oder einen Aufnahmeordner hierher ziehen"), U.Ghost(Theme.ISettings, "Einstellungen", ShowSettings), U.Ghost(Theme.IHelp, "Hilfe", ShowHelp));
        actions.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        var logo = Theme.Logo(30); logo.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(logo, Dock.Left); header.Children.Add(logo);
        header.Children.Add(Theme.Wordmark(17));

        // ---------- Begrüßung ----------
        var hello = new TextBlock { FontFamily = Theme.Display, FontSize = 28, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
        hello.Inlines.Add(new System.Windows.Documents.Run(Greeting() + " ") { FontWeight = FontWeights.SemiBold, Foreground = Theme.Fg });
        hello.Inlines.Add(new System.Windows.Documents.Run(Loc.T("Was möchten Sie festhalten?")) { Foreground = Theme.Muted });
        _stats.Margin = new Thickness(2, 8, 0, 0);
        var hero = U.Col(hello, _stats);
        hero.Margin = new Thickness(0, 0, 0, 20);

        // ---------- Neue Aufnahme: ① Modus ----------
        int sel = Math.Max(0, Array.IndexOf(Ids, s.LastProfile));
        var modes = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new Thickness(-5, 0, -5, 0) };
        _profiles = Modes.Select((m, i) => ModeCard(m.glyph, m.name, m.audience, i == sel)).ToArray();
        for (int k = 0; k < _profiles.Length; k++) _profiles[k].ToolTip = $"{Modes[k].name} – {Modes[k].audience}\n{Modes[k].text}";
        foreach (var r in _profiles) { r.Margin = new Thickness(5, 0, 5, 0); modes.Children.Add(r); r.Checked += (_, _) => UpdateProfile(); }
        _recLink = U.Btn(Theme.IEdit, "Anpassen …", CustomizeFromPreset, null, "Link");
        _recLink.Margin = new Thickness(0, 2, -6, 0); _recLink.HorizontalAlignment = HorizontalAlignment.Right;
        var meterBox = U.Col(_meter, _recLink);
        meterBox.Margin = new Thickness(20, 0, 0, 0);
        meterBox.ToolTip = "Detailgrad: wie genau aufgezeichnet wird";
        var stripTop = new DockPanel();
        DockPanel.SetDock(meterBox, Dock.Right); stripTop.Children.Add(meterBox); stripTop.Children.Add(_modeText);
        _strip = new Border
        {
            Background = Theme.Surface2, CornerRadius = new CornerRadius(7), Padding = new Thickness(16, 12, 16, 10), Margin = new Thickness(0, 10, 0, 0),
            Child = U.Col(stripTop, _chips), ToolTip = DetailPresets.BaseRule() + " Passwortfelder werden in allen Modi maskiert."
        };

        // ---------- Neue Aufnahme: ② Bereich ----------
        _monitor.Items.Add("Monitor mit Aktivität"); _monitor.Items.Add("Alle Monitore");
        foreach (var m in ScreenGrabber.GetMonitors()) _monitor.Items.Add($"Monitor {m.Index + 1} · {m.Bounds.Width} × {m.Bounds.Height}{(m.Primary ? " · Hauptmonitor" : "")}");
        _monitor.SelectedIndex = 0;
        _monitor.ToolTip = "Welcher Bildschirm bei „Ganzer Bildschirm“ aufgenommen wird";
        _goalLink = U.Btn(Theme.IFlag, "Ziel und Titel …", ShowGoalDialog, "Optional: was die Aufnahme zeigen soll – hilft Menschen und KI beim Verstehen", "Link");
        var areaHead = new DockPanel { Margin = new Thickness(0, 18, 0, 10) };
        var areaOpts = U.Row(_monitor, _goalLink); areaOpts.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(areaOpts, Dock.Right); areaHead.Children.Add(areaOpts);
        areaHead.Children.Add(StepLabel("2", "Was soll aufgenommen werden?"));

        var tiles = new Grid();
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition());
        tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        tiles.ColumnDefinitions.Add(new ColumnDefinition());
        void Place(UIElement e, int col) { Grid.SetColumn(e, col); tiles.Children.Add(e); }
        Place(StartTile(), 0);
        Place(Tile(Theme.IApp, "Eine Anwendung", "danach Fenster anklicken", "Nur die Fenster eines Programms aufnehmen – alles andere bleibt außen vor", () => Begin(ScopeMode.App)), 2);
        Place(Tile(Theme.ICrop, "Bereich", "danach Rechteck aufziehen", "Einen festen Ausschnitt des Bildschirms aufnehmen", () => Begin(ScopeMode.Region)), 4);

        var stepOne = StepLabel("1", "Wofür ist die Aufnahme?"); stepOne.Margin = new Thickness(0, 0, 0, 10);
        var main = Panel("Neue Aufnahme", Theme.IRecord, U.Col(stepOne, modes, _strip, areaHead, tiles), null);

        // ---------- Seitenkasten mit Reitern ----------
        var hk = s.Hotkeys;
        string HK(string key, string def) => De(hk.GetValueOrDefault(key, def));
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
        K("Sofort starten · Pause", HK("instantPause", "Ctrl+Shift+F9"));
        K("Beenden", HK("stop", "Ctrl+Shift+F10"));
        K("Fehlerstelle markieren", HK("markerError", "Ctrl+Shift+F1"));
        K("Kommentar", HK("comment", "Ctrl+Shift+F12"));
        K("Diktat (Mikrofon)", HK("dictation", "Ctrl+Shift+F5"));
        K("Privattaste", HK("privacy", "Ctrl+Shift+F8"));
        var keysTab = U.Col(keyGrid, U.Text("Alle Befehle auch per Rechtsklick auf das Symbol unten rechts im Infobereich.", 12, fg: Theme.Muted, margin: new Thickness(0, 10, 0, 0)));

        var (tabTrack, tabs) = U.Segmented("stab", 0, (Theme.IHistory, "Zuletzt"), (Theme.IKeyboard, "Tastenkürzel"));
        var openFolder = U.Btn(Theme.IFolder, null, () => { var d = AppPaths.DefaultSessionsFolder(); Directory.CreateDirectory(d); Process.Start("explorer.exe", $"\"{d}\""); }, "Aufnahmeordner im Explorer öffnen", "Ghost");
        openFolder.Margin = new Thickness(0);
        var sideHead = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(openFolder, Dock.Right); sideHead.Children.Add(openFolder); sideHead.Children.Add(tabTrack);
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
        var ver = U.Text($"Version {AppInfo.Version} · offline · kostenlos", 11.5, fg: Theme.Muted);
        DockPanel.SetDock(ver, Dock.Right); foot.Children.Add(ver);
        var update = U.Btn(Theme.IGlobe, "Neue Version verfügbar", UpdateCheck.OpenReleases, "Release-Seite auf GitHub öffnen", "Link");
        update.Visibility = Visibility.Collapsed; update.Margin = new Thickness(0, -4, 14, -4);
        DockPanel.SetDock(update, Dock.Right); foot.Children.Add(update);
        if (AppSettings.Load().CheckUpdates)
            Loaded += async (_, _) =>
            {
                var r = await UpdateCheck.CheckAsync();
                if (r.Newer) { update.Content = U.IconText(Theme.IGlobe, $"Version {r.Latest} verfügbar"); update.Visibility = Visibility.Visible; Anim.Appear(update); }
            };
        var folderText = U.Text(folder, 11.5, fg: Theme.Muted, margin: new Thickness(6, 0, 12, 0)); folderText.TextWrapping = TextWrapping.NoWrap; folderText.TextTrimming = TextTrimming.CharacterEllipsis; folderText.ToolTip = folder;
        var folderRow = U.Row(U.Icon(Theme.IFolder, 12, Theme.Muted), folderText);
        if (AppPaths.LooksSynced(folder)) folderRow.Children.Add(U.Chip("synchronisierter Ordner", Theme.WarnSoft, Theme.Warn, Theme.IWarn));
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
        };
        Activated += (_, _) => { UpdateProfile(); FillRecent(); };
    }

    // =====================================================================================
    // Bausteine
    // =====================================================================================

    /// <summary>Begrüßung je Tageszeit, wenn möglich mit Vornamen aus dem Windows-Konto.</summary>
    static string Greeting()
    {
        int h = DateTime.Now.Hour;
        string g = h < 5 ? "Guten Abend" : h < 11 ? "Guten Morgen" : h < 18 ? "Guten Tag" : "Guten Abend";
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
        var head = U.Text("Ganzer Bildschirm", 17, true, Theme.OnAccent, new Thickness(0, 10, 0, 2)); head.FontFamily = Theme.Display;
        var hint = U.Row(U.Kbd("Eingabe", new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), Theme.OnAccent, new SolidColorBrush(Color.FromArgb(90, 255, 255, 255))), U.Text("Aufnahme starten", 12.5, fg: Theme.OnAccent, margin: new Thickness(6, 0, 0, 0)));
        ((FrameworkElement)hint.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        hint.Margin = new Thickness(0, 6, 0, 0);
        var ac = Theme.AccentColor;
        Color Mix(Color c, double t) => Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));
        var rest = new LinearGradientBrush(ac, Mix(ac, 0.22), new Point(0, 1), new Point(1, 0));
        var hot = new LinearGradientBrush(Mix(ac, 0.08), Mix(ac, 0.32), new Point(0, 1), new Point(1, 0));
        var b = new Button { Style = Theme.Style("Tile"), Content = U.Col(icon, head, hint), Background = rest, BorderBrush = Brushes.Transparent, Foreground = Theme.OnAccent, MinHeight = 108, Padding = new Thickness(18, 14, 18, 14), ToolTip = "Alles auf dem Monitor, auf dem gerade gearbeitet wird (Eingabetaste)" };
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
        C("Klicks", On("clicks", "wheel"), "Mausklicks mit Bild und angeklicktem Element, Scrollen");
        C(On("typing", "hotkeys", "keys") ? "Tastatur" : "ohne Tastatur", On("typing", "hotkeys", "keys"), "Getippter Text, Tastenkombinationen und Einzeltasten (Passwortfelder immer maskiert)");
        C(On("moves") ? "Mauswege" : "ohne Mauswege", On("moves"), "Mausbewegungen und Verweilen über Elementen – nützlich für KI und Fehlersuche");
        C("Zusatzbilder: " + char.ToLower(DetailPresets.ChangeFramesName(changeFrames)[0]) + DetailPresets.ChangeFramesName(changeFrames)[1..], true, DetailPresets.ChangeFramesRule(changeFrames) + "\n" + DetailPresets.BaseRule());
        C(appContext ? "Programmkontext erweitert" : "Programmkontext Basis", true, appContext
            ? "Zusätzlich Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument, Statusleiste – für AutoHotkey und KI"
            : "Fenster und angeklicktes Element – reicht für Anleitungen");
    }

    static readonly (string id, string name, string result)[] AfterStops =
    {
        ("viewer", "Editor öffnen", "Anleitung im Editor – Export in jedes Format"),
        ("export", "KI-Paket exportieren", "KI-Paket: Protokoll, Bilder und Lesehilfe"),
        ("supportPackage", "Support-Paket (ZIP) erstellen", "ZIP-Paket: Anleitung (HTML) + KI-Paket"),
        ("folder", "Aufnahmeordner öffnen", "Aufnahme im Ordner – später im Editor öffnen"),
    };

    void UpdateCustomCard() { if (_profiles != null && ProfileIndex == 3) UpdateProfile(); }

    /// <summary>Inhalt „Eigener Detailgrad“: Eingabearten, Zusatzbilder, Programmkontext, nach dem Stopp.</summary>
    internal FrameworkElement BuildCustomContent()
    {
        var st = AppSettings.Load();
        void Save(Action<AppSettings> change) { var x = AppSettings.Load(); change(x); x.Save(); UpdateCustomCard(); }
        var col = new StackPanel();
        col.Children.Add(U.Text("Wird gespeichert und gilt für den Modus „Eigene“. Ausgeschaltetes wird gar nicht erst aufgezeichnet.", 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 12)));
        col.Children.Add(RecordChoice.Build(st.Record, () => Save(x => x.Record = new Dictionary<string, bool>(st.Record))));
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(24) : new GridLength(1, GridUnitType.Star) });
        int cf = st.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 };
        var (cfTrack, cfs) = U.Segmented("cfr", cf, (null, "Nur große"), (null, "Auch kleine"), (null, "Jede Änderung"));
        var cfHint = U.Text(DetailPresets.ChangeFramesRule(st.ChangeFrames), 11.5, fg: Theme.Muted, margin: new Thickness(0, 6, 0, 0));
        for (int i = 0; i < 3; i++) { string v = new[] { "few", "normal", "many" }[i]; cfs[i].Checked += (_, _) => { cfHint.Text = DetailPresets.ChangeFramesRule(v); Save(x => x.ChangeFrames = v); }; }
        var left = U.Field("Zusatzbilder ohne Eingabe", U.Col(cfTrack, cfHint));
        var ctx = new CheckBox { Content = "Erweiterter Programmkontext", IsChecked = st.CustomAppContext, Margin = new Thickness(0, 4, 0, 0), ToolTip = "Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument und Statusleiste – nötig für gute AutoHotkey-Skripte" };
        ctx.Checked += (_, _) => Save(x => x.CustomAppContext = true); ctx.Unchecked += (_, _) => Save(x => x.CustomAppContext = false);
        var after = new ComboBox { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var a in AfterStops) after.Items.Add(a.name);
        after.SelectedIndex = Math.Max(0, Array.FindIndex(AfterStops, a => a.id == st.CustomAfterStop));
        after.SelectionChanged += (_, _) => { string id = AfterStops[Math.Max(0, after.SelectedIndex)].id; Save(x => x.CustomAfterStop = id); };
        var right = U.Col(U.Field("Programmkontext", ctx), U.Field("Nach dem Stopp", after, margin: new Thickness(0)));
        Grid.SetColumn(left, 0); Grid.SetColumn(right, 2);
        grid.Children.Add(left); grid.Children.Add(right);
        col.Children.Add(grid);
        return col;
    }

    void ShowCustomDialog()
    {
        var w = new Window { Title = "Eigener Detailgrad", Width = Math.Min(760, Theme.ActiveWorkArea().Width - 40), SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        U.Dialog(w, "Eigener Detailgrad", BuildCustomContent(), U.Primary("Fertig", () => w.Close()));
        w.ShowDialog();
        UpdateProfile();
    }

    /// <summary>„Ziel und Titel“: optionale Angaben zur Aufnahme in einem kleinen Fenster.</summary>
    void ShowGoalDialog()
    {
        TextBox Box(string v, string tag, bool multi) => new() { Text = v, Tag = tag, MaxLength = multi ? 2000 : 200, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 60 : 0, VerticalContentAlignment = multi ? VerticalAlignment.Top : VerticalAlignment.Center };
        var goal = Box(_goalText, "z. B. Neuen Kontakt im Adressbuch anlegen", true);
        var expected = Box(_expectedText, "z. B. Der Kontakt erscheint in der Liste", true);
        var reference = Box(_referenceText, "z. B. Ticketnummer", false);
        var title = Box(_titleText, "sonst automatisch aus der Aufnahme", false);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        void G(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); grid.Children.Add(e); }
        G(U.Field("Was wird gezeigt?", goal), 0, 0); G(U.Field("Erwartetes Ergebnis", expected), 0, 2);
        G(U.Field("Bezug", reference, margin: new Thickness(0)), 1, 0); G(U.Field("Titel der Aufnahme", title, margin: new Thickness(0)), 1, 2);
        var w = new Window { Title = "Ziel und Titel", Width = Math.Min(680, Theme.ActiveWorkArea().Width - 40), SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var body = U.Col(U.Text("Optional. Hilft beim Verstehen der Aufnahme – für Menschen und für KI.", 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 14)), grid);
        U.Dialog(w, "Ziel der Aufnahme", body,
            U.Primary("Übernehmen", () => { _goalText = goal.Text; _expectedText = expected.Text; _referenceText = reference.Text; _titleText = title.Text.Trim(); w.Close(); }),
            U.Btn("Leeren", () => { goal.Clear(); expected.Clear(); reference.Clear(); title.Clear(); }), U.Btn("Abbrechen", () => w.Close()));
        w.Loaded += (_, _) => goal.Focus();
        w.ShowDialog();
        UpdateGoalLink();
    }

    void UpdateGoalLink()
    {
        bool set = (_goalText + _expectedText + _referenceText + _titleText).Trim().Length > 0;
        bool recommended = ProfileIndex is 1 or 2;
        _goalLink.Content = U.IconText(set ? Theme.ICheck : Theme.IFlag, set ? "Ziel und Titel ✓" : recommended ? "Ziel und Titel (empfohlen) …" : "Ziel und Titel …");
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

    /// <summary>„Ctrl+Shift+F9“ → „Strg+Umschalt+F9“.</summary>
    static string De(string combo) => string.Join("+", combo.Split('+').Select(p => p.Trim() switch { "Ctrl" => "Strg", "Shift" => "Umschalt", var x => x }));

    /// <summary>Detailstreifen zum gewählten Modus.</summary>
    void UpdateProfile()
    {
        if (_profiles == null || _recLink == null) return;
        int i = ProfileIndex;
        bool changed = _modeText.Text != Modes[i].text && _modeText.Text.Length > 0;
        _modeText.Text = Modes[i].text;
        if (changed) { Anim.Refresh(_strip, 0.3, 260); Anim.Stagger(_chips, 40, 35, 4); }
        _recLink.Content = U.IconText(Theme.IEdit, i == 3 ? "Einstellen …" : "Anpassen …");
        _recLink.ToolTip = i == 3 ? "Eingabearten, Zusatzbilder, Programmkontext und „nach dem Stopp“ festlegen" : "Diesen Detailgrad als Ausgangspunkt für den Modus „Eigene“ übernehmen";
        if (i == 3)
        {
            var st = AppSettings.Load();
            var off = RecordingOptions.Channels.Where(c => !st.Records(c.key)).Select(c => c.key).ToList();
            Meter(_meter, DetailPresets.BarsFor(off, st.ChangeFrames, st.CustomAppContext), "Eigene");
            DetailChips(_chips, off, st.ChangeFrames, st.CustomAppContext);
        }
        else
        {
            var p = DetailPresets.For(Ids[i])!;
            Meter(_meter, p.Bars, p.Level);
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
        if (total > 0) parts.Add(total == 1 ? "1 Aufnahme" : $"{total} Aufnahmen");
        if (list.Count > 0 && Json.ReadObject(System.IO.Path.Combine(list[0], "manifest.json")) is { } last)
            try { parts.Add("zuletzt " + Relative(Json.ParseUtc(last.Str("createdUtc")).ToLocalTime())); } catch { }
        parts.Add(AppSettings.Load().PrivacyLevel == "strict" ? "Schutz: streng" : "Passwortfelder werden maskiert");
        _stats.Inlines.Add(new System.Windows.Documents.Run("●  ") { Foreground = Theme.Success });
        _stats.Inlines.Add(new System.Windows.Documents.Run(string.Join("   ·   ", parts.Select(Loc.T))) { Foreground = Theme.Muted });

        if (list.Count == 0)
        {
            int n = 0;
            foreach (var (head, text) in new[] { ("Modus wählen", "Im Zweifel „Anleitung“."), ("Bereich anklicken", "Die Aufnahme startet sofort."),
                                                   ("Vorführen und beenden", De(AppSettings.Load().Hotkeys.GetValueOrDefault("stop", "Ctrl+Shift+F10")) + " stoppt."), ("Nachbearbeiten", "Texte anpassen, als PDF oder Word speichern.") })
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
                try { meta.Add(Relative(Json.ParseUtc(man.Str("createdUtc")).ToLocalTime())); } catch { }
                if (man.Obj("summary")?.LongN("primarySteps") is long n) meta.Add(n == 1 ? "1 Schritt" : $"{n} Schritte");
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

    static string Relative(DateTime t)
    {
        var d = DateTime.Today - t.Date;
        return d.TotalDays switch { 0 => "heute, " + t.ToString("HH:mm"), 1 => "gestern, " + t.ToString("HH:mm"), < 7 => t.ToString("dddd, HH:mm", System.Globalization.CultureInfo.GetCultureInfo("de-DE")), _ => t.ToString("dd.MM.yyyy") };
    }

    static TextBlock Mono(string text) => new() { Text = text, FontFamily = Theme.Mono, FontSize = 11, Foreground = Theme.Muted, Margin = new Thickness(0, 3, 0, 0) };

    /// <summary>Vorschaubild einer Aufnahme (ein frühes Bild der Sitzung) im Hintergrund laden.</summary>
    static void LoadThumb(string path, Image target)
    {
        Task.Run(() =>
        {
            try
            {
                using var s = Session.Open(path, recover: false);
                var ids = s.Frames.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (ids.Count == 0) return null;
                using var bmp = s.RestoreFrameCopy(ids[Math.Min(1, ids.Count - 1)]);
                if (bmp == null) return null;
                using var small = new System.Drawing.Bitmap(bmp, 192, Math.Max(1, 192 * bmp.Height / bmp.Width));
                return U.ToSource(small);
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

    void Begin(ScopeMode scope)
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
            var r = RegionPicker.Pick();
            if (r == null) { Show(); return; }
            o.Region = r;
        }
        _goalText = _expectedText = _referenceText = _titleText = "";
        UiApp.Instance.StartRecording(o);
    }

    public void ShowSupport()
    {
        var r = Msg.Show("Problem aufnehmen\n\n1. Aufnahme starten und das Problem vorführen.\n2. Fehlerstelle mit Strg+Umschalt+F1 markieren, mit Strg+Umschalt+F10 beenden.\n3. Eingaben durchsehen und das ZIP erstellen.\n\nDas Programm versendet nichts – Sie geben das ZIP selbst weiter. Aufnahme jetzt starten?",
            "Support-Paket", MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (r != MessageBoxResult.OK) return;
        _profiles[1].IsChecked = true;
        Begin(ScopeMode.Screen);
    }

    void OpenSession()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Aufnahme (manifest.json;*.steprec)|manifest.json;*.steprec", InitialDirectory = AppPaths.DefaultSessionsFolder() };
        if (dlg.ShowDialog(this) == true)
        {
            var p = dlg.FileName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase) ? System.IO.Path.GetDirectoryName(dlg.FileName)! : dlg.FileName;
            UiApp.Instance.OpenViewer(p);
        }
    }

    void ShowSettings() => new SettingsWindow { Owner = this }.ShowDialog();

    void ShowHelp() => Msg.Show(
        $"So funktioniert {AppInfo.DisplayName}\n\n" +
        "1. Modus wählen: Anleitung (schlank), Support-Fall (ausführlich), KI-Spur (vollständig) oder Eigene (selbst festgelegt). Der Modus bestimmt den Detailgrad – Eingabearten, Zusatzbilder bei Bildschirmänderungen, Programmkontext – und was nach dem Stopp passiert.\n\n" +
        "2. Bereich wählen: ganzer Bildschirm, eine Anwendung oder ein Ausschnitt. Ein Klick startet die Aufnahme; vorführen und mit Strg+Umschalt+F10 beenden.\n\n" +
        "Privattaste Strg+Umschalt+F8: Eingaben werden nur gezählt, nicht gespeichert.\n\n" +
        "Editor: Texte direkt rechts ändern, Kapitel und Hinweiskästen am Schritt anlegen, im Reiter „Dokument“ Deckblatt, Inhaltsverzeichnis, Bildgröße und Farbe festlegen, Werkzeuge links am Bild (V Auswahl, A Pfeil, R Rahmen, E Ellipse, M Hervorheben, T Text, N Nummer, X Schwärzen), Markierungen anklicken und mit Entf löschen, Schritte per Ziehen umsortieren. Strg+C kopiert das Bild, Strg+E exportiert.\n\n" +
        "KI-Zugriff: SebastiansStepRecorder.exe mcp --root <Aufnahmeordner> startet den MCP-Server (stdio).\n\n" +
        "Portabel, offline, ohne Installation. Das Programm ist in seinen eigenen Bildern nie zu sehen.\n\n" +
        "Kostenlos nutzbar, auch beruflich. Lizenz: CC BY-ND 4.0 (Namensnennung – keine Bearbeitungen). © 2026 Sebedai · github.com/Sebedai/sebastians-step-recorder",
        "Hilfe", MessageBoxButton.OK, MessageBoxImage.Information);

    protected override void OnClosed(EventArgs e) { base.OnClosed(e); UiApp.Instance.MaybeShutdown(); }
}

/// <summary>Auswahl, welche Eingabearten aufgezeichnet werden (Mausgruppe oben, Tastaturgruppe unten).</summary>
static class RecordChoice
{
    /// <summary>Baut die Auswahl; jede Änderung wird in <paramref name="target"/> geschrieben und <paramref name="changed"/> gemeldet.</summary>
    public static FrameworkElement Build(Dictionary<string, bool> target, Action? changed)
    {
        var col = new StackPanel();
        var warn = U.Text("Alles ausgeschaltet – es entstehen nur noch Bilder bei Bildschirmänderungen.", 12, fg: Theme.Warn, margin: new Thickness(0, 2, 0, 0));
        void UpdWarn() => warn.Visibility = RecordingOptions.Channels.All(c => !target.GetValueOrDefault(c.key, true)) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (group, glyph, keys) in new[] { ("Maus", Theme.IMouse, new[] { "clicks", "wheel", "moves" }), ("Tastatur", Theme.IKeyboard, new[] { "typing", "hotkeys", "keys" }) })
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
        t.Inlines.Add(new System.Windows.Documents.Run("Aufgezeichnet: ") { FontWeight = FontWeights.SemiBold });
        t.Inlines.Add(new System.Windows.Documents.Run(off.Count == 0 ? "alles – " + string.Join(", ", on) : on.Count == 0 ? "keine Eingaben" : string.Join(", ", on)));
        if (off.Count > 0 && on.Count > 0)
        {
            t.Inlines.Add(new System.Windows.Documents.Run("   ·   Nicht: ") { FontWeight = FontWeights.SemiBold, Foreground = Theme.Warn });
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
        var row = U.Row(U.Icon(glyph, 15, Brushes.White), U.Text(text, 14, false, Brushes.White, new Thickness(10, 0, 14, 0)), U.Kbd("Esc", new SolidColorBrush(Color.FromRgb(0x26, 0x25, 0x22)), Brushes.Gainsboro, DarkLine), U.Text("abbrechen", 13, false, Brushes.Gainsboro, new Thickness(4, 0, 0, 0)));
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
        Content = new Grid { Children = { _canvas, OverlayUi.Pill(Theme.IPointer, "Anwendung anklicken, die aufgenommen werden soll") } };
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
        _labelText.Text = $"{Win32.GetWindowTitle(found)}\n{proc} · alle Fenster dieser Anwendung{(excluded ? "\nausgeschlossen – wird nicht aufgezeichnet" : "")}";
        Canvas.SetLeft(_label, (wr.Left - _virt.X) / _scale + 12); Canvas.SetTop(_label, (wr.Top - _virt.Y) / _scale + 12);
    }

    public static (nint hwnd, uint pid, string process)? Pick()
    {
        var w = new WindowPicker();
        return w.ShowDialog() == true ? w._result : null;
    }

    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);
}

/// <summary>Bereichsauswahl: Rechteck aufziehen (mindestens 64×64, auf einem Monitor).</summary>
sealed class RegionPicker : Window
{
    readonly Rectangle _sel = new() { StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), Visibility = Visibility.Hidden };
    readonly TextBlock _sizeText = new();
    readonly Border _size;
    readonly System.Drawing.Rectangle _virt;
    Win32.POINT _start; bool _drag; double _scale = 1;
    System.Drawing.Rectangle? _result;

    RegionPicker()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)); Topmost = true; ShowInTaskbar = false; Cursor = Cursors.Cross;
        FontFamily = Theme.UiFont;
        _sel.Stroke = Theme.Accent;
        _size = OverlayUi.Label(_sizeText); _size.Visibility = Visibility.Hidden;
        _virt = ScreenGrabber.VirtualScreen(ScreenGrabber.GetMonitors());
        var c = new Canvas { Children = { _sel, _size } };
        Content = new Grid { Children = { c, OverlayUi.Pill(Theme.ICrop, "Rechteck aufziehen, das aufgenommen werden soll") } };
        SourceInitialized += (_, _) =>
        {
            SetWindowPos(new WindowInteropHelper(this).Handle, -1, _virt.X, _virt.Y, _virt.Width, _virt.Height, 0x0040);
            _scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1;
        };
        MouseLeftButtonDown += (_, _) => { Win32.GetCursorPos(out _start); _drag = true; CaptureMouse(); };
        MouseMove += (_, _) =>
        {
            if (!_drag) return;
            var r = Current();
            _sel.Visibility = Visibility.Visible; _size.Visibility = Visibility.Visible;
            Canvas.SetLeft(_sel, (r.X - _virt.X) / _scale); Canvas.SetTop(_sel, (r.Y - _virt.Y) / _scale); _sel.Width = r.Width / _scale; _sel.Height = r.Height / _scale;
            _sizeText.Text = $"{r.Width} × {r.Height} px"; Canvas.SetLeft(_size, (r.X - _virt.X) / _scale); Canvas.SetTop(_size, Math.Max(0, (r.Y - _virt.Y) / _scale - 36));
        };
        MouseLeftButtonUp += (_, _) =>
        {
            if (!_drag) return;
            _drag = false; ReleaseMouseCapture();
            var r = Current();
            if (r.Width < 64 || r.Height < 64) { _sel.Visibility = Visibility.Hidden; _sizeText.Text = "mindestens 64 × 64 px"; return; }
            var mon = ScreenGrabber.GetMonitors().OrderByDescending(m => { var i = System.Drawing.Rectangle.Intersect(m.Bounds, r); return (long)i.Width * i.Height; }).First();
            _result = System.Drawing.Rectangle.Intersect(r, mon.Bounds);
            DialogResult = true;
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
    }

    System.Drawing.Rectangle Current()
    {
        Win32.GetCursorPos(out var p);
        return System.Drawing.Rectangle.FromLTRB(Math.Min(p.X, _start.X), Math.Min(p.Y, _start.Y), Math.Max(p.X, _start.X), Math.Max(p.Y, _start.Y));
    }

    public static System.Drawing.Rectangle? Pick()
    {
        var w = new RegionPicker();
        return w.ShowDialog() == true ? w._result : null;
    }

    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);
}

/// <summary>Einstellungen: Sprache, Anrede, Schutzstufe, Zusatzbilder, Aufnahmeordner, Ausschlussliste.</summary>
sealed class SettingsWindow : Window
{
    static DockPanel Note(string text)
    {
        var d = new DockPanel();
        var i = U.Icon(Theme.IInfo, 12, Theme.Muted); i.VerticalAlignment = VerticalAlignment.Top; i.Margin = new Thickness(0, 2, 8, 0);
        DockPanel.SetDock(i, Dock.Left); d.Children.Add(i); d.Children.Add(U.Text(text, 12, fg: Theme.Muted));
        return d;
    }

    public SettingsWindow(int tab = 0)
    {
        var wa = Theme.ActiveWorkArea();
        Title = "Einstellungen"; Width = Math.Min(820, wa.Width - 24); SizeToContent = SizeToContent.Height; MaxHeight = wa.Height - 24;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        var s = AppSettings.Load();
        var lang = new ComboBox { Items = { "Deutsch", "English" }, SelectedIndex = s.Language == "en" ? 1 : 0 };
        var address = new ComboBox { Items = { "Sie (förmlich)", "du (persönlich)" }, SelectedIndex = s.Address == "informal" ? 1 : 0 };
        var level = new ComboBox { Items = { "Schutznetz – Passwortfelder maskieren", "Streng – unbekannte Felder maskieren" }, SelectedIndex = s.PrivacyLevel == "strict" ? 1 : 0 };
        var change = new ComboBox { Items = { DetailPresets.ChangeFramesName("few"), DetailPresets.ChangeFramesName("normal"), DetailPresets.ChangeFramesName("many") }, SelectedIndex = s.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 } };
        var changeHint = U.Text(DetailPresets.ChangeFramesRule(s.ChangeFrames), 12, fg: Theme.Muted, margin: new Thickness(0, 5, 0, 0));
        change.SelectionChanged += (_, _) => changeHint.Text = DetailPresets.ChangeFramesRule(change.SelectedIndex switch { 0 => "few", 2 => "many", _ => "normal" });
        var folder = new TextBox { Text = s.SessionsFolder ?? AppPaths.DefaultSessionsFolder() };
        var warn = U.Text("", 12, fg: Theme.Warn, margin: new Thickness(0, 5, 0, 0));
        void CheckSync() { warn.Text = AppPaths.LooksSynced(folder.Text) ? "Der Ordner wird synchronisiert – Aufnahmen können dadurch Unbeteiligten zugänglich werden." : ""; warn.Visibility = warn.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed; }
        folder.TextChanged += (_, _) => CheckSync(); CheckSync();
        var browse = U.Btn(Theme.IFolder, null, () =>
        {
            using var d = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = folder.Text, Description = "Aufnahmeordner wählen", UseDescriptionForTitle = true };
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) folder.Text = d.SelectedPath;
        }, "Ordner wählen …");
        browse.Margin = new Thickness(8, 0, 0, 0);
        var folderRow = new DockPanel(); DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(folder);
        var excl = new TextBox { Text = string.Join(Environment.NewLine, s.Excluded), AcceptsReturn = true, Height = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top, FontFamily = Theme.Mono, FontSize = 12.5 };

        // zweispaltiges Raster für Felder
        static Grid Two(params UIElement[] items)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            for (int k = 0; k < items.Length; k++)
            {
                if (k % 2 == 0) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(items[k], k / 2); Grid.SetColumn(items[k], k % 2 == 0 ? 0 : 2); g.Children.Add(items[k]);
            }
            return g;
        }
        var theme = new ComboBox { Items = { "Dunkel", "Hell", "Wie Windows" }, SelectedIndex = s.Theme switch { "light" => 1, "system" => 2, _ => 0 } };
        var rec = new Dictionary<string, bool>(s.Record);
        var fallback = new CheckBox { Content = "Ohne Nachfrage mit ausgeblendeter Anzeige aufnehmen, wenn die Unsichtbarkeit nicht bestätigt werden kann", IsChecked = s.InvisibleFallback == "hidden", Margin = new Thickness(0, 6, 0, 0),
            ToolTip = "Z. B. unter Windows Server 2019: Leiste und Rahmen bleiben ausgeblendet, gesteuert wird über Tastenkürzel und das Symbol im Infobereich." };
        var countdown = new ComboBox { Items = { "aus", "3 Sekunden", "5 Sekunden" }, SelectedIndex = s.Countdown >= 5 ? 2 : s.Countdown >= 3 ? 1 : 0 };
        var sounds = new ComboBox { Items = { "nur bei ausgeblendeter Anzeige", "immer", "nie" }, SelectedIndex = s.SoundCues switch { "always" => 1, "never" => 2, _ => 0 } };
        var uiLang = new ComboBox { Items = { "Deutsch", "English" }, SelectedIndex = s.UiLanguage == "en" ? 1 : 0 };
        var glossary = new TextBox { Text = string.Join(Environment.NewLine, s.Glossary), AcceptsReturn = true, Height = 74, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top, FontFamily = Theme.Mono, FontSize = 12.5,
            ToolTip = "Eine Ersetzung je Zeile, z. B.  Text-Editor=Editor  oder  SAP Easy Access=SAP-Startseite" };
        var cleanup = new CheckBox { Content = "Neue Aufnahmen beim ersten Öffnen aufräumen (doppelte Klicks, Klick vor der Eingabe, Fensterwechsel hin und zurück)", IsChecked = s.AutoCleanup, Margin = new Thickness(0, 8, 0, 0),
            ToolTip = "Überflüssige Schritte werden nur ausgeblendet, nicht gelöscht – im Editor rückgängig mit Strg+Z oder über ⋯ › Aufräumen." };
        var updates = new CheckBox { Content = "Beim Start nach einer neuen Version suchen", IsChecked = s.CheckUpdates, VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Fragt nur die öffentliche Release-Seite auf GitHub ab – ohne Kennung oder persönliche Daten. Heruntergeladen wird nichts." };
        var checkNow = U.Btn(Theme.ISearch, "Jetzt suchen", () => _ = UpdateCheck.CheckInteractiveAsync(), "Einmalig auf GitHub nach einer neuen Version suchen", "Link");
        checkNow.Margin = new Thickness(12, 0, 0, 0);

        // ---------- Reiter ----------
        var general = U.Col(Two(U.Field("Darstellung", theme, "Gilt ab dem nächsten Start."), U.Field("Sprache der Oberfläche", uiLang, "Gilt ab dem nächsten Start."),
                                U.Field("Sprache der Texte", lang), U.Field("Anrede in Anleitungen", address)),
                            U.Field("Begriffe in automatischen Texten", glossary, "„Fundstelle=Ersatz“ je Zeile – z. B. für Firmenbegriffe oder sprechende Namen von Programmen."),
                            U.Row(updates, checkNow));
        var recording = U.Col(Two(U.Field("Schutzstufe", level, "Was bei Eingaben maskiert wird."), U.Field("Countdown vor der Aufnahme", countdown, "Bei ausgeblendeter Anzeige mindestens 3 Sekunden."),
                                  U.Field("Signaltöne", sounds, "Bei Start, Stopp, Pause und Markern."), new Border()), fallback, cleanup);
        var custom = U.Col(U.Text("Gilt nur für den Modus „Eigene“ – die anderen Modi haben einen festen Detailgrad.", 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 10)),
                           RecordChoice.Build(rec, null), U.Field("Zusatzbilder ohne Eingabe", U.Col(change, changeHint), margin: new Thickness(0, 10, 0, 0)));
        var storage = U.Col(U.Field("Aufnahmeordner", U.Col(folderRow, warn)),
                            U.Field("Ausgeschlossene Programme", excl, "Ein Prozessname je Zeile – von diesen Programmen wird nichts aufgezeichnet."),
                            Note(AppPaths.Portable ? $"Portabler Modus – Einstellungen liegen neben der EXE ({AppPaths.DataDir})." : $"Einstellungen liegen in {AppPaths.DataDir}."));
        var pages = new UIElement[] { general, recording, custom, storage };
        var (tabTrack, tabs) = U.Segmented("settab", 0, (Theme.ISettings, "Allgemein"), (Theme.IRecord, "Aufnahme"), (Theme.IChecklist, "Modus „Eigene“"), (Theme.IFolder, "Speicher und Schutz"));
        tabTrack.Margin = new Thickness(0, 0, 0, 20);
        var host = new Grid();
        foreach (var pg in pages) { pg.Visibility = Visibility.Hidden; ((FrameworkElement)pg).VerticalAlignment = VerticalAlignment.Top; host.Children.Add(pg); }   // „Hidden“: alle Reiter gleich hoch
        general.Visibility = Visibility.Visible;
        for (int t = 0; t < tabs.Length; t++) { int k = t; tabs[t].Checked += (_, _) => { foreach (var pg in pages) pg.Visibility = Visibility.Hidden; pages[k].Visibility = Visibility.Visible; Anim.Refresh(pages[k], 0.2, 200); }; }
        var body = U.Col(tabTrack, host);
        if (tab > 0 && tab < tabs.Length) tabs[tab].IsChecked = true;
        U.Dialog(this, null, body, U.Primary("Speichern", () =>
        {
            var removed = s.Excluded.Except(excl.Text.Split('\n').Select(x => x.Trim())).ToList();
            if (removed.Count > 0 && Msg.Show("Schutz abschwächen?\n\nFolgende Ausschlüsse werden entfernt und deren Eingaben künftig aufgezeichnet:\n" + string.Join(", ", removed), "Einstellungen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (level.SelectedIndex == 0 && s.PrivacyLevel == "strict" && Msg.Show("Schutz abschwächen?\n\nSchutzstufe von „Streng“ auf „Schutznetz“ senken?", "Einstellungen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            s.Language = lang.SelectedIndex == 1 ? "en" : "de"; s.PrivacyLevel = level.SelectedIndex == 1 ? "strict" : "balanced";
            s.ChangeFrames = change.SelectedIndex switch { 0 => "few", 2 => "many", _ => "normal" };
            s.SessionsFolder = folder.Text.Trim(); s.Address = address.SelectedIndex == 1 ? "informal" : "formal";
            s.Record = rec;
            s.InvisibleFallback = fallback.IsChecked == true ? "hidden" : "ask";
            string newTheme = theme.SelectedIndex switch { 1 => "light", 2 => "system", _ => "dark" };
            if (newTheme != s.Theme) Msg.Show("Darstellung geändert\n\nDie neue Darstellung gilt ab dem nächsten Start des Programms.", "Einstellungen", MessageBoxButton.OK, MessageBoxImage.Information);
            s.Theme = newTheme;
            s.Countdown = countdown.SelectedIndex switch { 1 => 3, 2 => 5, _ => 0 };
            s.SoundCues = sounds.SelectedIndex switch { 1 => "always", 2 => "never", _ => "hidden" };
            string newUi = uiLang.SelectedIndex == 1 ? "en" : "de";
            if (newUi != s.UiLanguage) Msg.Show("Sprache der Oberfläche geändert\n\nDie neue Sprache gilt ab dem nächsten Start des Programms.", "Einstellungen", MessageBoxButton.OK, MessageBoxImage.Information);
            s.UiLanguage = newUi;
            s.Glossary = glossary.Text.Split('\n').Select(x => x.Trim()).Where(x => x.Contains('=') && x.Length > 2).ToList();
            s.AutoCleanup = cleanup.IsChecked == true; s.CheckUpdates = updates.IsChecked == true;
            s.Excluded = excl.Text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            s.Save(); Close();
        }), U.Btn("Abbrechen", Close));
        if (Content is FrameworkElement dlg)
        {
            Content = null;
            dlg.Width = 800;
            Content = new Viewbox { Child = dlg, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, VerticalAlignment = VerticalAlignment.Top };
        }
    }
}
