﻿using System.Diagnostics;
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
/// Startfenster als kleine Windows-11-App (WPF-UI, Mica, eigene Titelleiste) mit linker Navigation:
/// „Neue Aufnahme“ (Zweck und Bereich – ein Klick startet, rechts „Zuletzt“), „Aufnahmen“, „Einstellungen“, „Hilfe“.
/// Die Seite „Neue Aufnahme“ passt immer ins Fenster: Reicht der Platz nicht, wird sie gleichmäßig verkleinert.
/// </summary>
sealed partial class StartWindow : Wpf.Ui.Controls.FluentWindow
{
    readonly RadioButton[] _profiles;
    string _goalText = "", _expectedText = "", _referenceText = "", _titleText = "";
    readonly ComboBox _monitor = new() { MinWidth = 220 };
    readonly StackPanel _recent = new();
    readonly TextBlock _stats = new() { FontSize = Theme.FsCaption, Foreground = Theme.Muted, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 10) };
    readonly TextBlock _protection = new() { FontSize = Theme.FsCaption, Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
    static readonly string[] Ids = RecordingModes.All.Select(m => m.Id).ToArray();
    static readonly (string glyph, string name, string audience, string text)[] Modes =
    {
        (Theme.IDoc, Loc.L("Anleitung", "Guide"), Loc.L("für Menschen", "for people"), Loc.L("Schlank – nur was Menschen zum Nachmachen brauchen.", "Lean – only what people need to follow along.")),
        (Theme.IWrench, Loc.L("Support-Fall", "Support case"), Loc.L("für den Helpdesk", "for the help desk"), Loc.L("Ausführlich – alle Eingaben, damit der Helpdesk den Fehler nachvollziehen kann.", "Detailed – all input, so the help desk can reproduce the problem.")),
        (Theme.IRobot, Loc.L("KI-Spur", "AI trace"), Loc.L("für KI und Skripte", "for AI and scripts"), Loc.L("Vollständig – so genau wie möglich, für KI-Assistenten und AutoHotkey.", "Complete – as precise as possible, for AI assistants and AutoHotkey.")),
        (Theme.IError, Loc.L("Fehler nachstellen", "Reproduce bug"), Loc.L("für Entwickler", "for developers"), Loc.L("Ausführlich mit Programm-Kontext – Absturz, Fehlertexte, Build-Stand; danach der Editor direkt am Fehler.", "Detailed with program context – crash, error texts, build; then the editor right at the failure.")),
        (Theme.ISettings, Loc.L("Eigene", "Custom"), Loc.L("selbst festgelegt", "user-defined"), Loc.L("Selbst festgelegt – „Einstellen …“ öffnet die Auswahl.", "User-defined – “Set up …” opens the options.")),
    };
    readonly TextBlock _modeText = new() { TextWrapping = TextWrapping.Wrap, FontSize = Theme.FsBody, Foreground = Theme.Fg, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel _meter = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly WrapPanel _chips = new() { VerticalAlignment = VerticalAlignment.Center };
    Button _recLink = null!, _goalLink = null!;
    Border _strip = null!;
    /// <summary>Ab dieser Inhaltsbreite steht „Zuletzt“ neben der Auswahl, darunter rutscht es unter sie.</summary>
    const double WideContent = 880;

    int ProfileIndex => Math.Max(0, Array.FindIndex(_profiles, r => r.IsChecked == true));

    public StartWindow()
    {
        Title = AppInfo.DisplayName;
        Theme.Prepare(this);
        var wa = Theme.ActiveWorkArea();
        Width = Math.Min(1180 + NavWidth, wa.Width - 24); Height = Math.Min(780, wa.Height - 24);
        MinWidth = Math.Min(640, wa.Width - 24); MinHeight = Math.Min(460, wa.Height - 24);
        WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.CanResize;
        WindowPlacer.Attach(this, "start");
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0 && Session.LooksLikeSession(f[0])) UiApp.Instance.OpenViewer(f[0]); };
        var s = AppSettings.Load();

        // ---------- Seitenkopf ----------
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(U.Heading(Loc.L("Neue Aufnahme", "New recording"), Theme.FsHero, new Thickness(0)));

        // ---------- Zweck ----------
        int sel = Math.Max(0, Array.IndexOf(Ids, s.LastProfile));
        var modes = new System.Windows.Controls.Primitives.UniformGrid { Columns = Modes.Length, Margin = new Thickness(-4, 0, -4, 0) };
        _profiles = Modes.Select((m, i) => ModeCard(m.name, m.audience, i == sel)).ToArray();
        for (int k = 0; k < _profiles.Length; k++) _profiles[k].ToolTip = $"{Modes[k].name} – {Modes[k].audience}\n{Modes[k].text}";
        foreach (var r in _profiles) { r.Margin = new Thickness(4, 0, 4, 0); modes.Children.Add(r); r.Checked += (_, _) => UpdateProfile(); }

        // Detailkarte: Zeile 1 Beschreibung + Detailgrad, Zeile 2 was aufgezeichnet wird + „Anpassen …“
        _recLink = U.Btn(Theme.IEdit, Loc.L("Anpassen …", "Customize …"), CustomizeFromPreset, null, "Link");
        _recLink.Margin = new Thickness(12, 0, -8, 0); _recLink.VerticalAlignment = VerticalAlignment.Center;
        _meter.ToolTip = Loc.L("Detailgrad: wie genau aufgezeichnet wird", "Level of detail: how precisely input is recorded");
        _meter.Margin = new Thickness(20, 0, 0, 0);
        var stripTop = new DockPanel();
        DockPanel.SetDock(_meter, Dock.Right); stripTop.Children.Add(_meter); stripTop.Children.Add(_modeText);
        var stripBottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(_recLink, Dock.Right); stripBottom.Children.Add(_recLink); stripBottom.Children.Add(_chips);
        _strip = U.Card(U.Col(stripTop, stripBottom), new Thickness(16, 12, 16, 10), new Thickness(0, 8, 0, 0));
        _strip.ToolTip = DetailPresets.BaseRule() + Loc.L(" Passwortfelder werden in allen Modi maskiert.", " Password fields are masked in all modes.");

        // ---------- Bereich ----------
        _monitor.Items.Add(Loc.L("Monitor mit Aktivität", "Monitor with activity")); _monitor.Items.Add(Loc.L("Alle Monitore", "All monitors"));
        foreach (var m in ScreenGrabber.GetMonitors())
            _monitor.Items.Add(m.Primary
                ? Loc.F("Monitor {0} · {1} × {2} · Hauptmonitor", "Monitor {0} · {1} × {2} · primary", m.Index + 1, m.Bounds.Width, m.Bounds.Height)
                : Loc.F("Monitor {0} · {1} × {2}", "Monitor {0} · {1} × {2}", m.Index + 1, m.Bounds.Width, m.Bounds.Height));
        _monitor.SelectedIndex = 0;
        _monitor.ToolTip = Loc.L("Welcher Bildschirm bei „Ganzer Bildschirm“ aufgenommen wird", "Which screen is recorded for “Full screen”");
        _goalLink = U.Btn(Theme.IFlag, Loc.L("Ziel und Titel …", "Goal and title …"), ShowGoalDialog, Loc.L("Optional: was die Aufnahme zeigen soll – hilft Menschen und KI beim Verstehen", "Optional: what the recording should show – helps people and AI understand it"), "Link");
        _goalLink.Margin = new Thickness(8, 0, 0, 0);
        var areaHead = new DockPanel { Margin = new Thickness(0, 24, 0, 8) };
        var areaOpts = U.Row(_monitor, _goalLink); areaOpts.VerticalAlignment = VerticalAlignment.Bottom;
        DockPanel.SetDock(areaOpts, Dock.Right); areaHead.Children.Add(areaOpts);
        var areaLabel = U.SectionLabel(Loc.L("Bereich", "Area"), new Thickness(0)); areaLabel.VerticalAlignment = VerticalAlignment.Bottom;
        areaHead.Children.Add(areaLabel);
        var screenCard = AreaCard(Theme.IMonitor, Loc.L("Ganzer Bildschirm", "Full screen"), Loc.L("Alles auf dem Monitor, auf dem gerade gearbeitet wird", "Everything on the monitor you are working on"), Loc.L("Ganzer Bildschirm – Eingabetaste startet", "Full screen – Enter starts"), () => Begin(ScopeMode.Screen), enter: true);
        var appCard = AreaCard(Theme.IApp, Loc.L("Eine Anwendung", "One application"), Loc.L("Danach ein Fenster anklicken – nur dieses Programm wird aufgenommen", "Then click a window – only this program is recorded"), Loc.L("Nur die Fenster eines Programms aufnehmen – alles andere bleibt außen vor", "Record only the windows of one program – everything else stays out"), () => Begin(ScopeMode.App));
        var regionCard = AreaCard(Theme.ICrop, Loc.L("Bereich", "Area"), Loc.L("Danach ein Rechteck aufziehen – ein fester Ausschnitt", "Then drag a rectangle – a fixed area"), Loc.L("Einen festen Ausschnitt des Bildschirms aufnehmen", "Record a fixed part of the screen"), () => Begin(ScopeMode.Region));
        _lastRegionLink = U.Btn(Theme.ICrop, "", () => BeginLastRegion(), Loc.L("Den zuletzt gewählten Bereich anpassen und aufnehmen – gestartet wird erst mit Eingabe", "Adjust and record the last selected area – recording starts only with Enter"), "Link");
        _lastRegionLink.HorizontalAlignment = HorizontalAlignment.Right; _lastRegionLink.Margin = new Thickness(0, 2, -8, -6);
        UpdateLastRegionLink();
        var areas = U.Col(screenCard, appCard, regionCard, _lastRegionLink);

        var purposeLabel = U.SectionLabel(Loc.L("Zweck", "Purpose"), new Thickness(0, 0, 0, 8));
        var main = U.Col(purposeLabel, modes, _strip, areaHead, areas);

        // ---------- Seitenkarte: Zuletzt ----------
        var allLink = U.Btn(Theme.INext, Loc.L("Alle Aufnahmen", "All recordings"), () => ShowPage("sessions"), Loc.L("Alle Aufnahmen: suchen, umbenennen, packen, löschen (Strg+L)", "All recordings: search, rename, pack, delete (Ctrl+L)"), "Link");
        allLink.Margin = new Thickness(0, -4, -8, 0);
        var sideHead = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(allLink, Dock.Right); sideHead.Children.Add(allLink);
        var recentLabel = U.SectionLabel(Loc.L("Zuletzt", "Recent"), new Thickness(0)); recentLabel.VerticalAlignment = VerticalAlignment.Center;
        sideHead.Children.Add(recentLabel);
        var side = U.Card(U.Col(sideHead, _stats, _recent), new Thickness(16, 14, 16, 12));
        side.VerticalAlignment = VerticalAlignment.Top;

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.4, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 290 });
        columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        columns.Children.Add(main); columns.Children.Add(side);
        // Bei wenig Platz bricht die Seite um, statt zu schrumpfen: „Zuletzt“ unter die Auswahl, Moduskarten in zwei Reihen
        void Reflow(double w)
        {
            if (w <= 0) return;
            bool wide = w >= WideContent;
            Grid.SetColumn(side, wide ? 2 : 0); Grid.SetRow(side, wide ? 0 : 1); Grid.SetColumnSpan(side, wide ? 1 : 3);
            Grid.SetColumnSpan(main, wide ? 1 : 3);
            side.Margin = new Thickness(0, wide ? 0 : 24, 0, 0);
            columns.ColumnDefinitions[1].Width = new GridLength(wide ? 24 : 0);
            columns.ColumnDefinitions[2].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            columns.ColumnDefinitions[2].MinWidth = wide ? 290 : 0;
            double mainW = wide ? w - 24 - Math.Max(290, (w - 24) / 3.4) : w;
            int cols = mainW >= 600 ? 4 : 2;
            if (modes.Columns != cols)
            {
                modes.Columns = cols;
                foreach (var r in _profiles) r.Margin = new Thickness(4, 0, 4, cols == 2 ? 8 : 0);
            }
        }

        // ---------- Fuß: Schutzhinweis, belegte Kürzel, bei Bedarf neue Version ----------
        var foot = new DockPanel { Margin = new Thickness(0, 20, 0, 0) };
        var update = U.Btn(Theme.IGlobe, Loc.L("Neue Version verfügbar", "New version available"), UpdateCheck.OpenReleases, Loc.L("Release-Seite auf GitHub öffnen", "Open release page on GitHub"), "Link");
        update.Visibility = Visibility.Collapsed; update.Margin = new Thickness(0, -4, -8, -4);
        DockPanel.SetDock(update, Dock.Right); foot.Children.Add(update);
        if (AppSettings.Load().CheckUpdates)
            Loaded += async (_, _) =>
            {
                var r = await UpdateCheck.CheckAsync();
                if (r.Newer) { U.Relabel(update, Theme.IGlobe, Loc.F("Version {0} verfügbar", "Version {0} available", r.Latest)); update.Visibility = Visibility.Visible; }
            };
        var shield = U.Icon(Theme.IShield, 14, Theme.Muted); shield.Margin = new Thickness(0, 0, 8, 0);
        var footLeft = U.Row(shield, _protection);
        if (UiApp.Instance.HotkeyFailures.Count > 0)
        {
            var warnChip = U.Chip(Loc.F("Tastenkürzel belegt: {0}", "Shortcuts taken: {0}", string.Join(", ", UiApp.Instance.HotkeyFailures)), Theme.WarnSoft, Theme.Warn, Theme.IWarn);
            warnChip.Margin = new Thickness(16, 0, 0, 0); warnChip.ToolTip = Loc.L("Ändern: Einstellungen › Tastenkürzel", "Change: Settings › Shortcuts");
            footLeft.Children.Add(warnChip);
        }
        foot.Children.Add(footLeft);

        var root = new StackPanel { Margin = new Thickness(28, 8, 32, 20) };
        root.Children.Add(header); root.Children.Add(columns); root.Children.Add(foot);
        // Schrift bleibt immer in voller Größe: reicht die Höhe nicht, scrollt die Seite
        var scroll = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        scroll.SizeChanged += (_, e) => Reflow(e.NewSize.Width - root.Margin.Left - root.Margin.Right);
        _newPage = scroll;
        // Fenster: Windows-Titelleiste (WPF-UI), darunter Navigation links und die Seite rechts
        var titleBar = new Wpf.Ui.Controls.TitleBar { Title = AppInfo.DisplayName, Icon = new Wpf.Ui.Controls.ImageIcon { Source = Theme.LogoImage(32), Width = 16, Height = 16 } };
        var body = new DockPanel();
        var nav = BuildNav();
        DockPanel.SetDock(nav, Dock.Left); body.Children.Add(nav); body.Children.Add(_pageHost);
        var frame = new DockPanel();
        DockPanel.SetDock(titleBar, Dock.Top); frame.Children.Add(titleBar); frame.Children.Add(body);
        Content = frame;
        ShowPage("new");

        Anim.Appear(root, 0, 4, 220);
        // Fenster so hoch öffnen, wie der Inhalt braucht (höchstens der Arbeitsbereich)
        double pageW = Width - NavWidth - 2 * SystemParameters.ResizeFrameVerticalBorderWidth;
        Reflow(pageW - root.Margin.Left - root.Margin.Right);
        root.Measure(new Size(pageW, double.PositiveInfinity));
        Height = Math.Min(wa.Height - 24, Math.Max(MinHeight, root.DesiredSize.Height + 48 + 2 * SystemParameters.ResizeFrameHorizontalBorderHeight + 8));
        SizeChanged += (_, _) => AdaptNav();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _page == "new" && !(Keyboard.FocusedElement is TextBox or Button or ComboBox or RadioButton)) { Begin(ScopeMode.Screen); e.Handled = true; }
            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { OpenSession(); e.Handled = true; }
            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control) { ShowPage("sessions"); e.Handled = true; }
        };
        Activated += (_, _) => { if (_page == "new") { UpdateProfile(); FillRecent(); UpdateLastRegionLink(); } };
    }

    // =====================================================================================
    // Bausteine
    // =====================================================================================

    /// <summary>Moduskarte: Optionskreis, Name und Zielgruppe (Fluent-Karte, ausgewählt mit Akzentrand).</summary>
    static RadioButton ModeCard(string title, string audience, bool selected)
    {
        var name = U.Text(title, Theme.FsBody, true); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
        var aud = U.Meta(audience); aud.Margin = new Thickness(0, 2, 0, 0);
        return new RadioButton { GroupName = "purpose", Style = Theme.Style("PurposeCard"), Content = U.Col(name, aud), IsChecked = selected };
    }

    /// <summary>Bereichs-Karte (WPF-UI CardAction): Symbol, Titel, Beschreibung, Pfeil – ein Klick startet die Aufnahme.</summary>
    Wpf.Ui.Controls.CardAction AreaCard(string glyph, string title, string description, string tip, Action click, bool enter = false)
    {
        var text = U.Col(U.Text(title, Theme.FsBody, true), U.Text(description, Theme.FsCaption, fg: Theme.Muted, margin: new Thickness(0, 2, 0, 0)));
        var content = new DockPanel();
        if (enter)
        {
            var hint = U.Row(U.Kbd(Loc.Keys("Enter")), U.Text(Loc.L("startet", "starts"), Theme.FsCaption, fg: Theme.Muted, margin: new Thickness(2, 0, 4, 0)));
            ((FrameworkElement)hint.Children[1]).VerticalAlignment = VerticalAlignment.Center;
            hint.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(hint, Dock.Right); content.Children.Add(hint);
        }
        content.Children.Add(text);
        var card = new Wpf.Ui.Controls.CardAction
        {
            Icon = new Wpf.Ui.Controls.FontIcon { Glyph = glyph, FontFamily = Theme.Icons, FontSize = 22 },
            Content = content, ToolTip = tip, Margin = new Thickness(0, 0, 0, 4), HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        System.Windows.Automation.AutomationProperties.SetName(card, title);
        card.Click += (_, _) => { try { click(); } catch (Exception ex) { UiApp.ShowError(ex); } };
        return card;
    }

    /// <summary>Detailgrad als vier Balken plus Bezeichnung.</summary>
    static void Meter(StackPanel target, int bars, string label)
    {
        target.Children.Clear();
        for (int i = 0; i < 4; i++)
            target.Children.Add(new Border { Width = 16, Height = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center, Background = i < bars ? Theme.Accent : Theme.LineStrong });
        target.Children.Add(U.Text(label, Theme.FsCaption, true, Theme.Fg, new Thickness(8, 0, 0, 1)));
    }

    /// <summary>Was wird aufgezeichnet – schlichte Angaben mit Haken (Einzelheiten im Tooltip).</summary>
    static void DetailChips(WrapPanel target, ICollection<string> off, string changeFrames, bool appContext)
    {
        target.Children.Clear();
        bool On(params string[] keys) => keys.Any(k => !off.Contains(k));
        void C(string text, bool on, string tip)
        {
            var c = on ? U.Chip(text, Brushes.Transparent, Theme.Fg2, Theme.ICheck) : U.Chip(text, Brushes.Transparent, Theme.Muted);
            c.Margin = new Thickness(0, 2, 16, 2); c.ToolTip = tip;
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

    bool IsCustom => Ids[ProfileIndex] == "custom";

    void UpdateCustomCard() { if (_profiles != null && IsCustom) UpdateProfile(); }

    /// <summary>Inhalt „Eigener Detailgrad“: Eingabearten, Zusatzbilder, Programmkontext, nach dem Stopp.</summary>
    internal FrameworkElement BuildCustomContent()
    {
        var st = AppSettings.Load();
        void Save(Action<AppSettings> change) { var x = AppSettings.Load(); change(x); x.Save(); UpdateCustomCard(); }
        var col = new StackPanel();
        col.Children.Add(U.Hint(Loc.L("Wird gespeichert und gilt für den Modus „Eigene“. Ausgeschaltetes wird gar nicht erst aufgezeichnet.", "Saved and applied to the “Custom” mode. Anything switched off is not recorded at all."), new Thickness(0, 0, 0, 14)));
        col.Children.Add(RecordChoice.Build(st.Record, () => Save(x => x.Record = new Dictionary<string, bool>(st.Record))));
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(24) : new GridLength(1, GridUnitType.Star) });
        int cf = st.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 };
        var (cfTrack, cfs) = U.Segmented("cfr", cf, (null, Loc.L("Nur große", "Large only")), (null, Loc.L("Auch kleine", "Small too")), (null, Loc.L("Jede Änderung", "Every change")));
        var cfHint = U.Hint(DetailPresets.ChangeFramesRule(st.ChangeFrames), new Thickness(0, 8, 0, 0));
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
        var w = new DialogWindow { Title = Loc.L("Eigener Detailgrad", "Custom level of detail"), Width = Math.Min(820, Theme.ActiveWorkArea().Width - 40), SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        U.Dialog(w, Loc.L("Eigener Detailgrad", "Custom level of detail"), BuildCustomContent(), U.Primary(Loc.L("Fertig", "Done"), () => w.Close()));
        w.ShowDialog();
        UpdateProfile();
    }

    /// <summary>„Ziel und Titel“: optionale Angaben zur Aufnahme in einem kleinen Fenster.</summary>
    void ShowGoalDialog()
    {
        TextBox Box(string v, string tag, bool multi) => new Wpf.Ui.Controls.TextBox { Text = v, PlaceholderText = tag, ClearButtonEnabled = false, MaxLength = multi ? 2000 : 200, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 60 : 0, VerticalContentAlignment = multi ? VerticalAlignment.Top : VerticalAlignment.Center };
        var goal = Box(_goalText, Loc.L("z. B. Neuen Kontakt im Adressbuch anlegen", "e.g. Create a new contact in the address book"), true);
        var expected = Box(_expectedText, Loc.L("z. B. Der Kontakt erscheint in der Liste", "e.g. The contact appears in the list"), true);
        var reference = Box(_referenceText, Loc.L("z. B. Ticketnummer", "e.g. Ticket number"), false);
        var title = Box(_titleText, Loc.L("sonst automatisch aus der Aufnahme", "otherwise taken from the recording automatically"), false);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        void G(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); grid.Children.Add(e); }
        G(U.Field(Loc.L("Was wird gezeigt?", "What is shown?"), goal), 0, 0); G(U.Field(Loc.L("Erwartetes Ergebnis", "Expected result"), expected), 0, 2);
        G(U.Field(Loc.L("Bezug", "Reference"), reference, margin: new Thickness(0)), 1, 0); G(U.Field(Loc.L("Titel der Aufnahme", "Recording title"), title, margin: new Thickness(0)), 1, 2);
        var w = new DialogWindow { Title = Loc.L("Ziel und Titel", "Goal and title"), Width = Math.Min(720, Theme.ActiveWorkArea().Width - 40), SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var body = U.Col(U.Hint(Loc.L("Optional. Hilft beim Verstehen der Aufnahme – für Menschen und für KI.", "Optional. Helps to understand the recording – for people and for AI."), new Thickness(0, -6, 0, 18)), grid);
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
        bool recommended = RecordingModes.All[ProfileIndex].ShowGoalFields && !IsCustom;
        U.Relabel(_goalLink, set ? Theme.ICheck : Theme.IFlag, set ? Loc.L("Ziel und Titel ✓", "Goal and title ✓") : recommended ? Loc.L("Ziel und Titel (empfohlen) …", "Goal and title (recommended) …") : Loc.L("Ziel und Titel …", "Goal and title …"));
    }

    /// <summary>„Anpassen …“ bei einem festen Modus: Detailgrad in „Eigene“ übernehmen; bei „Eigene“: Einstellungen öffnen.</summary>
    void CustomizeFromPreset()
    {
        if (IsCustom) { ShowCustomDialog(); return; }
        var p = DetailPresets.For(Ids[ProfileIndex])!;
        var x = AppSettings.Load();
        x.Record = RecordingOptions.Channels.ToDictionary(c => c.key, c => !p.Off.Contains(c.key));
        x.ChangeFrames = p.ChangeFrames; x.CustomAppContext = p.AppContext;
        x.CustomAfterStop = RecordingModes.All[ProfileIndex].AfterStop is "viewerError" ? "viewer" : RecordingModes.All[ProfileIndex].AfterStop;
        x.Save();
        _profiles[Array.IndexOf(Ids, "custom")].IsChecked = true;
        ShowCustomDialog();
    }


    /// <summary>Detailstreifen zum gewählten Modus.</summary>
    void UpdateProfile()
    {
        if (_profiles == null || _recLink == null) return;
        int i = ProfileIndex;
        _modeText.Text = Modes[i].text;
        U.Relabel(_recLink, Theme.IEdit, Ids[i] == "custom" ? Loc.L("Einstellen …", "Set up …") : Loc.L("Anpassen …", "Customize …"));
        _recLink.ToolTip = Ids[i] == "custom" ? Loc.L("Eingabearten, Zusatzbilder, Programmkontext und „nach dem Stopp“ festlegen", "Set input types, extra images, program context and “after stopping”") : Loc.L("Diesen Detailgrad als Ausgangspunkt für den Modus „Eigene“ übernehmen", "Use this level of detail as the starting point for the “Custom” mode");
        if (Ids[i] == "custom")
        {
            var st = AppSettings.Load();
            var off = RecordingOptions.Channels.Where(c => !st.Records(c.key)).Select(c => c.key).ToList();
            Meter(_meter, DetailPresets.BarsFor(off, st.ChangeFrames, st.CustomAppContext), Loc.L("Eigene", "Custom"));
            DetailChips(_chips, off, st.ChangeFrames, st.CustomAppContext);
        }
        else
        {
            var p = DetailPresets.For(Ids[i])!;
            Meter(_meter, p.Bars, DetailPresets.LevelName(p.Level));
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
        if (parts.Count == 0) parts.Add(Loc.L("Noch keine Aufnahmen", "No recordings yet"));
        _protection.Text = AppSettings.Load().PrivacyLevel == "strict" ? Loc.L("Schutz: streng", "Protection: strict") : Loc.L("Passwortfelder werden maskiert", "Password fields are masked");
        _stats.Inlines.Add(new System.Windows.Documents.Run(string.Join(" · ", parts)) { Foreground = Theme.Muted });

        if (list.Count == 0)
        {
            int n = 0;
            foreach (var (head, text) in new[] { (Loc.L("Modus wählen", "Choose a mode"), Loc.L("Im Zweifel „Anleitung“.", "When in doubt, “Guide”.")), (Loc.L("Bereich anklicken", "Click an area"), Loc.L("Die Aufnahme startet sofort.", "Recording starts right away.")),
                                                   (Loc.L("Vorführen und beenden", "Demonstrate and stop"), Loc.F("{0} stoppt.", "{0} stops it.", RecordingController.Key("stop"))), (Loc.L("Nachbearbeiten", "Edit afterwards"), Loc.L("Texte anpassen, als PDF oder Word speichern.", "Adjust texts, save as PDF or Word.")) })
            {
                var num = U.Text($"{++n}.", Theme.FsBody, true, Theme.Fg2); num.Width = 20; num.VerticalAlignment = VerticalAlignment.Top;
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
                DockPanel.SetDock(num, Dock.Left); row.Children.Add(num);
                row.Children.Add(U.Col(U.Text(head, Theme.FsBody, true), U.Text(text, Theme.FsHint, fg: Theme.Muted, margin: new Thickness(0, 2, 0, 0))));
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
            var thumb = new Image { Stretch = Stretch.UniformToFill, Width = 72, Height = 46, Clip = new RectangleGeometry(new Rect(0, 0, 72, 46), Theme.RSmall - 1, Theme.RSmall - 1) };
            RenderOptions.SetBitmapScalingMode(thumb, BitmapScalingMode.HighQuality);
            var thumbBox = new Border { Width = 74, Height = 48, CornerRadius = new CornerRadius(Theme.RSmall), Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 14, 0),
                Child = new Grid { Children = { U.Icon(Theme.IDoc, 16, Theme.Muted), thumb } } };
            LoadThumb(p, thumb);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var text = U.Col(new TextBlock { Text = t, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }, U.Meta(string.Join("  ·  ", meta), new Thickness(0, 3, 0, 0)));
            text.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(text, 1);
            g.Children.Add(thumbBox); g.Children.Add(text);
            var b = new Button { Style = Theme.Style("RowButton"), Content = g, ToolTip = p, Margin = new Thickness(-10, 0, -10, 4), Padding = new Thickness(10, 7, 10, 7) };
            b.Click += (_, _) => UiApp.Instance.OpenViewer(p);
            _recent.Children.Add(b);
        }
    }

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

    void ShowSettings() => ShowPage("settings");

    void ShowHelp() => ShowPage("help");

    protected override void OnClosed(EventArgs e) { base.OnClosed(e); UiApp.Instance.MaybeShutdown(); }
}

/// <summary>Auswahl, welche Eingabearten aufgezeichnet werden (Mausgruppe oben, Tastaturgruppe unten).</summary>
static class RecordChoice
{
    /// <summary>Baut die Auswahl; jede Änderung wird in <paramref name="target"/> geschrieben und <paramref name="changed"/> gemeldet.</summary>
    public static FrameworkElement Build(Dictionary<string, bool> target, Action? changed)
    {
        var col = new StackPanel();
        var warn = U.Text(Loc.L("Alles ausgeschaltet – es entstehen nur noch Bilder bei Bildschirmänderungen.", "Everything is switched off – only images of screen changes will be created."), Theme.FsHint, fg: Theme.Warn, margin: new Thickness(0, 4, 0, 0));
        void UpdWarn() => warn.Visibility = RecordingOptions.Channels.All(c => !target.GetValueOrDefault(c.key, true)) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (group, glyph, keys) in new[] { (Loc.L("Maus", "Mouse"), Theme.IMouse, new[] { "clicks", "wheel", "moves" }), (Loc.L("Tastatur", "Keyboard"), Theme.IKeyboard, new[] { "typing", "hotkeys", "keys" }) })
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            var head = U.Row(U.Icon(glyph, 14, Theme.Muted), U.Text(group, Theme.FsHint, true, Theme.Fg2, new Thickness(8, 0, 0, 0)));
            head.VerticalAlignment = VerticalAlignment.Top; head.Margin = new Thickness(0, 9, 0, 0);
            grid.Children.Add(head);
            for (int i = 0; i < keys.Length; i++)
            {
                var (key, name, hint) = RecordingOptions.Channels.First(c => c.key == keys[i]);
                var cb = new CheckBox { IsChecked = target.GetValueOrDefault(key, true), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 10, 0) };
                var text = U.Col(U.Text(name, Theme.FsBody, true), U.Text(hint, Theme.FsCaption, fg: Theme.Muted, margin: new Thickness(0, 2, 0, 0)));
                var item = new DockPanel();
                DockPanel.SetDock(cb, Dock.Left); item.Children.Add(cb); item.Children.Add(text);
                var box = new Border { Child = item, Padding = new Thickness(10, 8, 10, 9), Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(Theme.RControl), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = hint };
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
    public static Brush PillBg => Theme.Night.Pill;
    public static Brush DarkLine => Theme.Night.Line;

    public static Border Pill(string glyph, string text)
    {
        var row = U.Row(U.Icon(glyph, 16, Theme.Night.Fg), U.Text(text, Theme.FsBody, false, Theme.Night.Fg, new Thickness(10, 0, 16, 0)), U.Kbd("Esc", Theme.Night.Chip, Theme.Night.Fg, Theme.Night.Line), U.Text(Loc.L("abbrechen", "cancel"), Theme.FsHint, false, Theme.Night.Muted, new Thickness(2, 0, 0, 0)));
        foreach (var c in row.Children.OfType<FrameworkElement>()) c.VerticalAlignment = VerticalAlignment.Center;
        return new Border
        {
            Background = PillBg, BorderBrush = Theme.Night.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RCard), Padding = new Thickness(20, 11, 20, 11), Child = row,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 48), Effect = Theme.Shadow(24, 4, 0.4)
        };
    }

    public static Border Label(TextBlock t)
    {
        t.Foreground = Theme.Night.Fg; t.FontSize = Theme.FsHint; t.LineHeight = 19;
        return new Border { Background = PillBg, BorderBrush = Theme.Night.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RControl), Padding = new Thickness(12, 7, 12, 8), Child = t };
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
