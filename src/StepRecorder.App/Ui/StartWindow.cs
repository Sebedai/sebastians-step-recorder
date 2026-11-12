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
    static readonly string[] Ids = { "guide", "support", "aiTrace", "custom" };
    static readonly (string glyph, string name, string audience, string text)[] Modes =
    {
        (Theme.IDoc, "Anleitung", "für Menschen", "Schlank – nur was Menschen zum Nachmachen brauchen."),
        (Theme.IWrench, "Support-Fall", "für den Helpdesk", "Ausführlich – alle Eingaben, damit der Helpdesk den Fehler nachvollziehen kann."),
        (Theme.IRobot, "KI-Spur", "für KI und Skripte", "Vollständig – so genau wie möglich, für KI-Assistenten und AutoHotkey."),
        (Theme.ISettings, "Eigene", "selbst festgelegt", "Selbst festgelegt – „Einstellen …“ öffnet die Auswahl."),
    };
    readonly TextBlock _modeText = new() { TextWrapping = TextWrapping.Wrap, FontSize = Theme.FsBody, Foreground = Theme.Fg, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel _meter = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly WrapPanel _chips = new() { VerticalAlignment = VerticalAlignment.Center };
    Button _recLink = null!, _goalLink = null!;
    Border _strip = null!;
    /// <summary>Mindestbreite des Inhalts; schmalere Fenster verkleinern ihn gleichmäßig.</summary>
    const double MinContentWidth = 1000;

    int ProfileIndex => Math.Max(0, Array.FindIndex(_profiles, r => r.IsChecked == true));

    public StartWindow()
    {
        Title = AppInfo.DisplayName;
        Theme.Prepare(this);
        var wa = Theme.ActiveWorkArea();
        Width = Math.Min(1180 + NavWidth, wa.Width - 24); Height = Math.Min(780, wa.Height - 24);
        MinWidth = Math.Min(640, wa.Width - 24); MinHeight = Math.Min(460, wa.Height - 24);
        WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.CanResize;
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0 && Session.LooksLikeSession(f[0])) UiApp.Instance.OpenViewer(f[0]); };
        var s = AppSettings.Load();

        // ---------- Seitenkopf: Titel links, Befehle rechts (wie eine Seite der Windows-Einstellungen) ----------
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(U.Heading("Neue Aufnahme", Theme.FsHero, new Thickness(0)));

        // ---------- Zweck ----------
        int sel = Math.Max(0, Array.IndexOf(Ids, s.LastProfile));
        var modes = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new Thickness(-4, 0, -4, 0) };
        _profiles = Modes.Select((m, i) => ModeCard(m.name, m.audience, i == sel)).ToArray();
        for (int k = 0; k < _profiles.Length; k++) _profiles[k].ToolTip = $"{Modes[k].name} – {Modes[k].audience}\n{Modes[k].text}";
        foreach (var r in _profiles) { r.Margin = new Thickness(4, 0, 4, 0); modes.Children.Add(r); r.Checked += (_, _) => UpdateProfile(); }

        // Detailkarte: Zeile 1 Beschreibung + Detailgrad, Zeile 2 was aufgezeichnet wird + „Anpassen …“
        _recLink = U.Btn(Theme.IEdit, "Anpassen …", CustomizeFromPreset, null, "Link");
        _recLink.Margin = new Thickness(12, 0, -8, 0); _recLink.VerticalAlignment = VerticalAlignment.Center;
        _meter.ToolTip = "Detailgrad: wie genau aufgezeichnet wird";
        _meter.Margin = new Thickness(20, 0, 0, 0);
        var stripTop = new DockPanel();
        DockPanel.SetDock(_meter, Dock.Right); stripTop.Children.Add(_meter); stripTop.Children.Add(_modeText);
        var stripBottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(_recLink, Dock.Right); stripBottom.Children.Add(_recLink); stripBottom.Children.Add(_chips);
        _strip = U.Card(U.Col(stripTop, stripBottom), new Thickness(16, 12, 16, 10), new Thickness(0, 8, 0, 0));
        _strip.ToolTip = DetailPresets.BaseRule() + " Passwortfelder werden in allen Modi maskiert.";

        // ---------- Bereich ----------
        _monitor.Items.Add("Monitor mit Aktivität"); _monitor.Items.Add("Alle Monitore");
        foreach (var m in ScreenGrabber.GetMonitors()) _monitor.Items.Add($"Monitor {m.Index + 1} · {m.Bounds.Width} × {m.Bounds.Height}{(m.Primary ? " · Hauptmonitor" : "")}");
        _monitor.SelectedIndex = 0;
        _monitor.ToolTip = "Welcher Bildschirm bei „Ganzer Bildschirm“ aufgenommen wird";
        _goalLink = U.Btn(Theme.IFlag, "Ziel und Titel …", ShowGoalDialog, "Optional: was die Aufnahme zeigen soll – hilft Menschen und KI beim Verstehen", "Link");
        _goalLink.Margin = new Thickness(8, 0, 0, 0);
        var areaHead = new DockPanel { Margin = new Thickness(0, 24, 0, 8) };
        var areaOpts = U.Row(_monitor, _goalLink); areaOpts.VerticalAlignment = VerticalAlignment.Bottom;
        DockPanel.SetDock(areaOpts, Dock.Right); areaHead.Children.Add(areaOpts);
        var areaLabel = U.SectionLabel("Bereich", new Thickness(0)); areaLabel.VerticalAlignment = VerticalAlignment.Bottom;
        areaHead.Children.Add(areaLabel);
        var screenCard = AreaCard(Theme.IMonitor, "Ganzer Bildschirm", "Alles auf dem Monitor, auf dem gerade gearbeitet wird", "Ganzer Bildschirm – Eingabetaste startet", () => Begin(ScopeMode.Screen), enter: true);
        var appCard = AreaCard(Theme.IApp, "Eine Anwendung", "Danach ein Fenster anklicken – nur dieses Programm wird aufgenommen", "Nur die Fenster eines Programms aufnehmen – alles andere bleibt außen vor", () => Begin(ScopeMode.App));
        var regionCard = AreaCard(Theme.ICrop, "Bereich", "Danach ein Rechteck aufziehen – ein fester Ausschnitt", "Einen festen Ausschnitt des Bildschirms aufnehmen", () => Begin(ScopeMode.Region));
        var areas = U.Col(screenCard, appCard, regionCard);

        var purposeLabel = U.SectionLabel("Zweck", new Thickness(0, 0, 0, 8));
        var main = U.Col(purposeLabel, modes, _strip, areaHead, areas);

        // ---------- Seitenkarte: Zuletzt ----------
        var allLink = U.Btn(Theme.INext, "Alle Aufnahmen", () => ShowPage("sessions"), "Alle Aufnahmen mit Suche anzeigen", "Link");
        allLink.Margin = new Thickness(0, -4, -8, 0);
        var sideHead = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(allLink, Dock.Right); sideHead.Children.Add(allLink);
        var recentLabel = U.SectionLabel("Zuletzt", new Thickness(0)); recentLabel.VerticalAlignment = VerticalAlignment.Center;
        sideHead.Children.Add(recentLabel);
        var side = U.Card(U.Col(sideHead, _stats, _recent), new Thickness(16, 14, 16, 12));
        side.VerticalAlignment = VerticalAlignment.Top;

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.4, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 290 });
        columns.Children.Add(main);
        Grid.SetColumn(side, 2); columns.Children.Add(side);

        // ---------- Fuß: Schutzhinweis, bei Bedarf neue Version ----------
        var foot = new DockPanel { Margin = new Thickness(0, 20, 0, 0) };
        var update = U.Btn(Theme.IGlobe, "Neue Version verfügbar", UpdateCheck.OpenReleases, "Release-Seite auf GitHub öffnen", "Link");
        update.Visibility = Visibility.Collapsed; update.Margin = new Thickness(0, -4, -8, -4);
        DockPanel.SetDock(update, Dock.Right); foot.Children.Add(update);
        if (AppSettings.Load().CheckUpdates)
            Loaded += async (_, _) =>
            {
                var r = await UpdateCheck.CheckAsync();
                if (r.Newer) { update.Content = U.IconText(Theme.IGlobe, $"Version {r.Latest} verfügbar"); update.Visibility = Visibility.Visible; }
            };
        var shield = U.Icon(Theme.IShield, 14, Theme.Muted); shield.Margin = new Thickness(0, 0, 8, 0);
        var footLeft = U.Row(shield, _protection);
        foot.Children.Add(footLeft);

        var root = new StackPanel { Margin = new Thickness(28, 8, 32, 20) };
        root.Children.Add(header); root.Children.Add(columns); root.Children.Add(foot);
        // Immer ganz sichtbar, ohne Scrollen: Breite folgt dem Fenster; reicht die Höhe nicht, wird gleichmäßig verkleinert –
        // und der Inhalt so verbreitert, dass er nach dem Verkleinern wieder die ganze Fensterbreite füllt.
        var fit = new Viewbox { Child = root, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, VerticalAlignment = VerticalAlignment.Top };
        var host = new Grid(); host.Children.Add(fit);
        host.SizeChanged += (_, e) => FitWidth(root, e.NewSize);
        _newPage = host;
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
        UpdateProfile();
        FillRecent();
        // Fenster so hoch öffnen, wie der Inhalt braucht (höchstens der Arbeitsbereich)
        root.Width = Math.Max(MinContentWidth, Width - NavWidth);
        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Height = Math.Min(wa.Height - 24, Math.Max(MinHeight, root.DesiredSize.Height + 48 + 2 * SystemParameters.ResizeFrameHorizontalBorderHeight + 8));
        SizeChanged += (_, _) => AdaptNav();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _page == "new" && !(Keyboard.FocusedElement is TextBox or Button or ComboBox or RadioButton)) { Begin(ScopeMode.Screen); e.Handled = true; }
            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { OpenSession(); e.Handled = true; }
        };
        Activated += (_, _) => { UpdateProfile(); FillRecent(); };
    }

    /// <summary>
    /// Inhaltsbreite so wählen, dass der (bei Platzmangel verkleinerte) Inhalt die Fensterbreite ausfüllt:
    /// passt die Höhe, gilt die Fensterbreite; sonst wird die Breite im Verhältnis Inhaltshöhe ÷ Fensterhöhe vergrößert.
    /// Zwei Messrunden genügen, weil die Inhaltshöhe mit der Breite nur wenig schwankt (Umbrüche).
    /// </summary>
    static void FitWidth(FrameworkElement root, Size host)
    {
        if (host.Width <= 0 || host.Height <= 0) return;
        double w = Math.Max(MinContentWidth, host.Width);
        for (int i = 0; i < 2; i++)
        {
            root.Width = w;
            root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double h = root.DesiredSize.Height;
            double scale = Math.Min(1, Math.Min(host.Width / w, host.Height / h));
            double wanted = Math.Max(MinContentWidth, Math.Min(host.Width / scale, host.Width * 1.6));
            if (Math.Abs(wanted - w) < 2) break;
            w = wanted;
        }
        root.Width = w;
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
            var hint = U.Row(U.Kbd("Eingabe"), U.Text("startet", Theme.FsCaption, fg: Theme.Muted, margin: new Thickness(2, 0, 4, 0)));
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
        col.Children.Add(U.Hint("Wird gespeichert und gilt für den Modus „Eigene“. Ausgeschaltetes wird gar nicht erst aufgezeichnet.", new Thickness(0, 0, 0, 14)));
        col.Children.Add(RecordChoice.Build(st.Record, () => Save(x => x.Record = new Dictionary<string, bool>(st.Record))));
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(24) : new GridLength(1, GridUnitType.Star) });
        int cf = st.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 };
        var (cfTrack, cfs) = U.Segmented("cfr", cf, (null, "Nur große"), (null, "Auch kleine"), (null, "Jede Änderung"));
        var cfHint = U.Hint(DetailPresets.ChangeFramesRule(st.ChangeFrames), new Thickness(0, 8, 0, 0));
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
        var w = new Window { Title = "Eigener Detailgrad", Width = Math.Min(820, Theme.ActiveWorkArea().Width - 40), SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        U.Dialog(w, "Eigener Detailgrad", BuildCustomContent(), U.Primary("Fertig", () => w.Close()));
        w.ShowDialog();
        UpdateProfile();
    }

    /// <summary>„Ziel und Titel“: optionale Angaben zur Aufnahme in einem kleinen Fenster.</summary>
    void ShowGoalDialog()
    {
        TextBox Box(string v, string tag, bool multi) => new Wpf.Ui.Controls.TextBox { Text = v, PlaceholderText = tag, ClearButtonEnabled = false, MaxLength = multi ? 2000 : 200, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 60 : 0, VerticalContentAlignment = multi ? VerticalAlignment.Top : VerticalAlignment.Center };
        var goal = Box(_goalText, "z. B. Neuen Kontakt im Adressbuch anlegen", true);
        var expected = Box(_expectedText, "z. B. Der Kontakt erscheint in der Liste", true);
        var reference = Box(_referenceText, "z. B. Ticketnummer", false);
        var title = Box(_titleText, "sonst automatisch aus der Aufnahme", false);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        void G(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); grid.Children.Add(e); }
        G(U.Field("Was wird gezeigt?", goal), 0, 0); G(U.Field("Erwartetes Ergebnis", expected), 0, 2);
        G(U.Field("Bezug", reference, margin: new Thickness(0)), 1, 0); G(U.Field("Titel der Aufnahme", title, margin: new Thickness(0)), 1, 2);
        var w = new Window { Title = "Ziel und Titel", Width = Math.Min(720, Theme.ActiveWorkArea().Width - 40), SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var body = U.Col(U.Hint("Optional. Hilft beim Verstehen der Aufnahme – für Menschen und für KI.", new Thickness(0, -6, 0, 18)), grid);
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
        _modeText.Text = Modes[i].text;
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
        if (parts.Count == 0) parts.Add("Noch keine Aufnahmen");
        _protection.Text = AppSettings.Load().PrivacyLevel == "strict" ? "Schutz: streng" : "Passwortfelder werden maskiert";
        _stats.Inlines.Add(new System.Windows.Documents.Run(string.Join(" · ", parts.Select(Loc.T))) { Foreground = Theme.Muted });

        if (list.Count == 0)
        {
            int n = 0;
            foreach (var (head, text) in new[] { ("Modus wählen", "Im Zweifel „Anleitung“."), ("Bereich anklicken", "Die Aufnahme startet sofort."),
                                                   ("Vorführen und beenden", De(AppSettings.Load().Hotkeys.GetValueOrDefault("stop", "Ctrl+Shift+F10")) + " stoppt."), ("Nachbearbeiten", "Texte anpassen, als PDF oder Word speichern.") })
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
                try { meta.Add(Relative(Json.ParseUtc(man.Str("createdUtc")).ToLocalTime())); } catch { }
                if (man.Obj("summary")?.LongN("primarySteps") is long n) meta.Add(n == 1 ? "1 Schritt" : $"{n} Schritte");
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

    static string Relative(DateTime t)
    {
        var d = DateTime.Today - t.Date;
        return d.TotalDays switch { 0 => "heute, " + t.ToString("HH:mm"), 1 => "gestern, " + t.ToString("HH:mm"), < 7 => t.ToString("dddd, HH:mm", System.Globalization.CultureInfo.GetCultureInfo("de-DE")), _ => t.ToString("dd.MM.yyyy") };
    }



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
        }).ContinueWith(t => { ThumbCache[path] = t.Result; if (t.Result is { } src) { target.Source = src; Anim.Refresh(target, 0, 320); } }, TaskScheduler.FromCurrentSynchronizationContext());
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

    protected override void OnClosed(EventArgs e) { base.OnClosed(e); UiApp.Instance.MaybeShutdown(); }
}

/// <summary>Auswahl, welche Eingabearten aufgezeichnet werden (Mausgruppe oben, Tastaturgruppe unten).</summary>
static class RecordChoice
{
    /// <summary>Baut die Auswahl; jede Änderung wird in <paramref name="target"/> geschrieben und <paramref name="changed"/> gemeldet.</summary>
    public static FrameworkElement Build(Dictionary<string, bool> target, Action? changed)
    {
        var col = new StackPanel();
        var warn = U.Text("Alles ausgeschaltet – es entstehen nur noch Bilder bei Bildschirmänderungen.", Theme.FsHint, fg: Theme.Warn, margin: new Thickness(0, 4, 0, 0));
        void UpdWarn() => warn.Visibility = RecordingOptions.Channels.All(c => !target.GetValueOrDefault(c.key, true)) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (group, glyph, keys) in new[] { ("Maus", Theme.IMouse, new[] { "clicks", "wheel", "moves" }), ("Tastatur", Theme.IKeyboard, new[] { "typing", "hotkeys", "keys" }) })
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
    public static Brush PillBg => Theme.Night.Pill;
    public static Brush DarkLine => Theme.Night.Line;

    public static Border Pill(string glyph, string text)
    {
        var row = U.Row(U.Icon(glyph, 16, Theme.Night.Fg), U.Text(text, Theme.FsBody, false, Theme.Night.Fg, new Thickness(10, 0, 16, 0)), U.Kbd("Esc", Theme.Night.Chip, Theme.Night.Fg, Theme.Night.Line), U.Text("abbrechen", Theme.FsHint, false, Theme.Night.Muted, new Thickness(2, 0, 0, 0)));
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
/// <summary>
/// Seite „Einstellungen“ im Startfenster: die vier Bereiche als Auswahlleiste (wie in den Windows-Einstellungen
/// innerhalb einer Seite), unten „Speichern“ und „Verwerfen“.
/// </summary>
static class SettingsPage
{
    static DockPanel Note(string text)
    {
        var d = new DockPanel();
        var i = U.Icon(Theme.IInfo, 14, Theme.Muted); i.VerticalAlignment = VerticalAlignment.Top; i.Margin = new Thickness(0, 2, 8, 0);
        DockPanel.SetDock(i, Dock.Left); d.Children.Add(i); d.Children.Add(U.Hint(text));
        return d;
    }

    public static readonly string[] Tabs = { "Allgemein", "Aufnahme", "Modus „Eigene“", "Speicher und Schutz" };

    /// <summary>Baut die Seite; <paramref name="rebuild"/> baut sie mit dem genannten Bereich neu auf (für „Verwerfen“).</summary>
    public static FrameworkElement Build(int tab, Action<int> rebuild)
    {
        var s = AppSettings.Load();
        var lang = new ComboBox { Items = { "Deutsch", "English" }, SelectedIndex = s.Language == "en" ? 1 : 0 };
        var address = new ComboBox { Items = { "Sie (förmlich)", "du (persönlich)" }, SelectedIndex = s.Address == "informal" ? 1 : 0 };
        var level = new ComboBox { Items = { "Schutznetz – Passwortfelder maskieren", "Streng – unbekannte Felder maskieren" }, SelectedIndex = s.PrivacyLevel == "strict" ? 1 : 0 };
        var change = new ComboBox { Items = { DetailPresets.ChangeFramesName("few"), DetailPresets.ChangeFramesName("normal"), DetailPresets.ChangeFramesName("many") }, SelectedIndex = s.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 } };
        var changeHint = U.Hint(DetailPresets.ChangeFramesRule(s.ChangeFrames), new Thickness(0, 6, 0, 0));
        change.SelectionChanged += (_, _) => changeHint.Text = DetailPresets.ChangeFramesRule(change.SelectedIndex switch { 0 => "few", 2 => "many", _ => "normal" });
        var folder = new TextBox { Text = s.SessionsFolder ?? AppPaths.DefaultSessionsFolder() };
        var warn = U.Text("", Theme.FsHint, fg: Theme.Warn, margin: new Thickness(0, 6, 0, 0));
        void CheckSync() { warn.Text = AppPaths.LooksSynced(folder.Text) ? "Der Ordner wird synchronisiert – Aufnahmen können dadurch Unbeteiligten zugänglich werden." : ""; warn.Visibility = warn.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed; }
        folder.TextChanged += (_, _) => CheckSync(); CheckSync();
        var browse = U.Btn(Theme.IFolder, null, () =>
        {
            using var d = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = folder.Text, Description = "Aufnahmeordner wählen", UseDescriptionForTitle = true };
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) folder.Text = d.SelectedPath;
        }, "Ordner wählen …");
        browse.Margin = new Thickness(8, 0, 0, 0);
        var folderRow = new DockPanel(); DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(folder);
        var excl = new TextBox { Text = string.Join(Environment.NewLine, s.Excluded), AcceptsReturn = true, Height = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top, FontFamily = Theme.Mono, FontSize = Theme.FsHint };

        // zweispaltiges Raster für Felder
        static Grid Two(params UIElement[] items)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); g.ColumnDefinitions.Add(new ColumnDefinition());
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
        var glossary = new TextBox { Text = string.Join(Environment.NewLine, s.Glossary), AcceptsReturn = true, Height = 74, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top, FontFamily = Theme.Mono, FontSize = Theme.FsHint,
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
        var custom = U.Col(U.Hint("Gilt nur für den Modus „Eigene“ – die anderen Modi haben einen festen Detailgrad.", new Thickness(0, 0, 0, 12)),
                           RecordChoice.Build(rec, null), U.Field("Zusatzbilder ohne Eingabe", U.Col(change, changeHint), margin: new Thickness(0, 10, 0, 0)));
        var storage = U.Col(U.Field("Aufnahmeordner", U.Col(folderRow, warn)),
                            U.Field("Ausgeschlossene Programme", excl, "Ein Prozessname je Zeile – von diesen Programmen wird nichts aufgezeichnet."),
                            Note(AppPaths.Portable ? $"Portabler Modus – Einstellungen liegen neben der EXE ({AppPaths.DataDir})." : $"Einstellungen liegen in {AppPaths.DataDir}."));
        var pages = new UIElement[] { general, recording, custom, storage };
        int current = Math.Clamp(tab, 0, pages.Length - 1);
        var (track, tabs) = U.Segmented("settab", current, Tabs.Select(n => ((string?)null, n)).ToArray());
        track.Margin = new Thickness(-4, 0, 0, 20);
        var host = new Grid();
        foreach (var pg in pages) { pg.Visibility = Visibility.Hidden; ((FrameworkElement)pg).VerticalAlignment = VerticalAlignment.Top; host.Children.Add(pg); }   // „Hidden“: alle Bereiche gleich hoch, nichts springt
        pages[current].Visibility = Visibility.Visible;
        for (int t = 0; t < tabs.Length; t++) { int k = t; tabs[t].Checked += (_, _) => { foreach (var pg in pages) pg.Visibility = Visibility.Hidden; pages[k].Visibility = Visibility.Visible; current = k; }; }
        var saved = U.Text("", Theme.FsHint, fg: Theme.Muted, margin: new Thickness(14, 0, 0, 0)); saved.VerticalAlignment = VerticalAlignment.Center;
        var save = U.Primary("Speichern", () =>
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
            s.Save();
            saved.Text = Loc.T("Gespeichert");
        });
        var discard = U.Btn("Verwerfen", () => rebuild(current), "Nicht gespeicherte Änderungen zurücknehmen");
        var foot = U.Row(save, discard, saved);
        foot.Margin = new Thickness(-3, 8, 0, 0);
        var page = new StackPanel { Width = 860, HorizontalAlignment = HorizontalAlignment.Left };
        page.Children.Add(U.Heading("Einstellungen", Theme.FsHero, new Thickness(0, 0, 0, 16)));
        page.Children.Add(track);
        page.Children.Add(host);
        page.Children.Add(foot);
        return page;
    }
}
