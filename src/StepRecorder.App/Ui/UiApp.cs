using System.Windows;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>WPF-Anwendung: Startfenster, Aufnahme-Steuerung, Viewer.</summary>
sealed class UiApp : Application
{
    public static UiApp Instance => (UiApp)Current;
    public RecordingController? Recording { get; set; }
    StartWindow? _start;
    Hotkeys? _globalHotkeys;

    public static int Start(string[] args)
    {
        if (!SingleInstance.TryAcquire())
        {
            bool fwd = SingleInstance.Forward(args.Length == 0 ? new[] { "--activate" } : args);
            if (!fwd) return 6;
            // record an die laufende Oberfläche: auf das Ende warten und den Pfad ausgeben
            if (args.Length > 0 && args[0] == "record") return SingleInstance.WaitForForwardedRecording(args.Contains("--json"));
            return 0;
        }
        _firstRun = !File.Exists(AppPaths.SettingsFile);
        Loc.Init(AppSettings.Load().UiLanguage, applyToThreads: true);
        AppLog.InstallCrashHandlers(TryEmergencyStop);
        var app = new UiApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { ShowError(e.Exception); e.Handled = true; };
        app.Startup += (_, _) => app.OnStart(args);
        return app.Run();
    }

    void OnStart(string[] args)
    {
        if (Environment.GetEnvironmentVariable("STEPRECORDER_TRACE") == "1") Recorder.Trace = Log;
        Theme.Init(this);
        Accessibility.Install();
        DevSnapshots.Install();
        Invisibility.Install();
        SingleInstance.Listen();
        SingleInstance.ArgsReceived += HandleArgs;
        RegisterGlobalHotkeys();
        _cliRecord = args.Length > 0 && args[0] == "record";
        _cliJson = args.Contains("--json");
        _cliRecordOnly = _cliRecord && Array.IndexOf(args, "--after-stop") is int ai && ai >= 0 && ai + 1 < args.Length && args[ai + 1] == "none";
        // Ersteinrichtung: nur beim normalen Start ohne Befehl
        if ((args.Length == 0 || args[0] == "--activate") && FirstRunDialog.ShowIfFirstRun(_firstRun))
        {
            Resources.MergedDictionaries.Clear();
            Theme.Init(this);
        }
        HandleArgs(args);
    }

    static bool _firstRun;

    /// <summary>Diese Instanz wurde mit <c>record</c> gestartet: beim Ende der ersten Aufnahme den Pfad ausgeben (02 §8.4).</summary>
    bool _cliRecord, _cliJson;
    /// <summary>Mit <c>record --after-stop none</c> gestartet: danach beenden, nicht in Bereitschaft gehen.</summary>
    bool _cliRecordOnly;
    int _exitCode;

    /// <summary>Tastenkürzel, die beim letzten Anmelden belegt waren (für Startfenster und Diagnose).</summary>
    public List<string> HotkeyFailures { get; } = new();

    /// <summary>In „Bereit“ ist nur die Sofortaufnahme registriert.</summary>
    public void RegisterGlobalHotkeys()
    {
        _globalHotkeys ??= new Hotkeys();
        var s = AppSettings.Load();
        _globalHotkeys.Failed.Clear();
        string combo = s.Hotkey("instantPause");
        if (combo.Length > 0)
            _globalHotkeys.Register("instant", combo, () => { if (Recording == null) { var o = OptionsFromSettings(ScopeMode.Screen); ApplyProfile(o, AppSettings.Load().LastProfile); o.Scope = ScopeMode.Screen; StartRecording(o); } });
        // Sofort-Bildschirmfoto (Paket 15): Bereich wählen → eine Aufnahme mit einem Bildschritt → Editor
        string shot = s.Hotkey("screenshot");
        if (shot.Length > 0) _globalHotkeys.Register("screenshot", shot, () => { if (Recording == null) QuickScreenshot(); });
        HotkeyFailures.Clear();
        if (_globalHotkeys.Failed.Contains("instant")) HotkeyFailures.Add(Loc.Keys(combo));
        if (_globalHotkeys.Failed.Contains("screenshot")) HotkeyFailures.Add(Loc.Keys(shot));
    }

    public void UnregisterGlobalHotkeys() => _globalHotkeys?.UnregisterAll();

    void HandleArgs(string[] args)
    {
        // nur Befehl und Optionsnamen protokollieren – Werte (Titel, Ziel, Pfade) können Vertrauliches enthalten
        Log("args: " + AppLog.ArgNames(args));
        // Anfrage eines KI-Agenten (vom MCP-Server zugestellt) – der Mensch entscheidet im Dialog
        if (args.Length > 1 && args[0] == "--recording-request") { AgentRequests.Handle(args[1]); return; }
        if (args.Length == 0 || args[0] == "--activate")
        {
            if (Recording != null) { Recording.ShowRunningNotice(); return; }
            ShowStart();
            return;
        }
        if (args[0] == "record")
        {
            if (args.Contains("--stop")) { Recording?.Stop(); return; }
            if (Recording != null) { Recording.ShowRunningNotice(); return; }
            var ro = ParseRecordArgs(args);
            if (ro.Scope == ScopeMode.Region && (ro.Region == null || ro.RegionNeedsConfirm))
            {
                // nie stillschweigend den ganzen Bildschirm: Bereich wählen (bzw. angepassten letzten Bereich bestätigen)
                var picked = RegionPicker.Pick(ro.Region);
                if (picked == null) { if (_start == null) MaybeShutdown(); return; }
                ro.Region = picked;
            }
            StartRecording(ro);
            return;
        }
        if (args[0] == "support") { ShowStart(); _start?.ShowSupport(); return; }
        if (Session.LooksLikeSession(args[0])) { OpenViewer(args[0]); return; }
        ShowStart();
    }

    /// <summary>Startfenster zeigen, auf Wunsch gleich mit einer bestimmten Seite (new | sessions | settings | help).</summary>
    public void ShowStart(string page = "new")
    {
        if (_start == null || !_start.IsLoaded) { _start = new StartWindow(); _start.Closed += (_, _) => { _start = null; MaybeShutdown(); }; }
        _start.Show();
        _start.Activate();
        _start.ShowPage(page);
    }

    public void HideStart() => _start?.Hide();

    /// <summary>Sofort-Bildschirmfoto: Bereich wählen, aufnehmen, als Aufnahme mit einem Bildschritt im Editor öffnen.</summary>
    public void QuickScreenshot()
    {
        var rect = RegionPicker.Pick(null);
        if (rect is not { } r || r.Width < 4 || r.Height < 4) { if (_start == null) MaybeShutdown(); return; }
        // Auswahlfenster ist geschlossen und ohnehin aus Bildschirmfotos ausgeschlossen; kurz warten, bis es verschwunden ist
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        System.Threading.Thread.Sleep(80);
        using var bmp = StepRecorder.Native.ScreenGrabber.TryCapture(r, out int err);
        if (bmp == null) { ShowError(new InvalidOperationException(Loc.F("Bildschirmfoto fehlgeschlagen (Fehler {0}).", "Screenshot failed (error {0}).", err))); return; }
        var s = AppSettings.Load();
        string folder = StepRecorder.ReadLayer.QuickShot.Create(AppPaths.DefaultSessionsFolder(), bmp, s.Language);
        var v = OpenViewer(folder);
        v.FreshRecording = true;
        v.SelectFirstStep();
    }

    TrayIcon? _readyTray;

    /// <summary>
    /// Bereitschaft im Infobereich (Paket 14, Einstellung „Im Infobereich bereit bleiben“, ab Werk aus): Das Programm bleibt
    /// mit einem eigenen grauen Symbol erreichbar; nur das Kürzel „Sofort aufnehmen“ ist angemeldet – keine Hooks,
    /// keine Bildaufnahme, kein Autostart. Linksklick: Startfenster; Rechtsklick: Menü.
    /// </summary>
    void ShowReadyTray()
    {
        if (_readyTray != null) return;
        try
        {
            string combo = AppSettings.Load().Hotkey("instantPause");
            _readyTray = new TrayIcon(TrayIcon.Render("ready"), StepRecorder.Data.AppInfo.DisplayName + Loc.L(" – bereit", " – ready") + (combo.Length > 0 ? Loc.F(" · Sofort aufnehmen: {0}", " · Record now: {0}", Loc.Keys(combo)) : ""));
            _readyTray.LeftClick += () => Dispatcher.BeginInvoke(ShowStart);
            _readyTray.RightClick += () => Dispatcher.BeginInvoke(ReadyMenu);
        }
        catch (Exception ex) { Log("Bereitschaft: " + ex.Message); _readyTray = null; }
    }

    void HideReadyTray() { try { _readyTray?.Dispose(); } catch { } _readyTray = null; }

    void ReadyMenu()
    {
        var m = U.MenuScope(new System.Windows.Controls.ContextMenu());
        string combo = AppSettings.Load().Hotkey("instantPause");
        m.Items.Add(U.Mi(Loc.L("Sofort aufnehmen", "Record now"), combo.Length > 0 ? Loc.Keys(combo) : null, () => { var o = OptionsFromSettings(ScopeMode.Screen); ApplyProfile(o, AppSettings.Load().LastProfile); o.Scope = ScopeMode.Screen; StartRecording(o); }, Theme.IRecord));
        string shotKey = AppSettings.Load().Hotkey("screenshot");
        m.Items.Add(U.Mi(Loc.L("Bildschirmfoto …", "Screenshot …"), shotKey.Length > 0 ? Loc.Keys(shotKey) : null, QuickScreenshot, Theme.ICamera));
        m.Items.Add(U.Mi(Loc.L("Neue Aufnahme …", "New recording …"), null, () => ShowStart(), Theme.IAdd));
        m.Items.Add(U.Mi(Loc.L("Alle Aufnahmen …", "All recordings …"), null, () => ShowLibrary(), Theme.IFolder));
        m.Items.Add(U.Mi(Loc.L("Einstellungen …", "Settings …"), null, () => { ShowStart(); _start?.ShowPage("settings"); }, Theme.ISettings));
        m.Items.Add(new System.Windows.Controls.Separator());
        m.Items.Add(U.Mi(Loc.L("Beenden", "Exit"), null, () => { HideReadyTray(); Shutdown(_exitCode); }, null));
        m.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        m.IsOpen = true;
    }

    public void MaybeShutdown()
    {
        if (Recording == null && !Windows.OfType<Window>().Any(w => w.IsVisible && w is StartWindow or ViewerWindow))
        {
            if (AppSettings.Load().StayInTray && !_cliRecordOnly) { ShowReadyTray(); return; }
            HideReadyTray();
            Shutdown(_exitCode);
        }
    }

    /// <summary>„Alle Aufnahmen“: Seite „Aufnahmen“ im Startfenster.</summary>
    public void ShowLibrary() => ShowStart("sessions");

    static string Norm(string p) { try { return Path.GetFullPath(p).TrimEnd('\\', '/'); } catch { return p; } }

    public ViewerWindow? ViewerFor(string path) => Windows.OfType<ViewerWindow>().FirstOrDefault(v => string.Equals(Norm(v.SessionPath), Norm(path), StringComparison.OrdinalIgnoreCase));
    public bool IsOpenInViewer(string path) => ViewerFor(path) != null;

    /// <summary>Öffnet den Editor – oder holt ein schon offenes Fenster derselben Aufnahme nach vorn. Optional zu einem Schritt springen.</summary>
    public ViewerWindow OpenViewer(string path, long? anchor = null, string? search = null)
    {
        var v = ViewerFor(path);
        if (v == null)
        {
            v = new ViewerWindow(path);
            v.Closed += (_, _) => { MaybeShutdown(); _start?.RescanLibrary(); };
            v.Show();
            AppSettings.Load().AddRecent(path);
        }
        else if (v.WindowState == WindowState.Minimized) v.WindowState = WindowState.Normal;
        v.Activate();
        if (anchor is long a) v.JumpTo(a, search);
        return v;
    }

    public static RecordingOptions OptionsFromSettings(ScopeMode scope)
    {
        var s = AppSettings.Load();
        var o = new RecordingOptions
        {
            Scope = scope, Language = s.Language, PrivacyLevel = s.PrivacyLevel, ChangeFrames = s.ChangeFrames, Monitors = "active",
            IdleReminderMinutes = s.IdleReminderMinutes, PrivacyReminderSeconds = s.PrivacyReminderSeconds,
            ExcludedProcesses = s.Excluded.Count > 0 ? new List<string>(s.Excluded) : new List<string>(RecordingOptions.DefaultExcluded)
        };
        // alle wirksamen Kürzel (auch nicht gespeicherte Standardwerte) als eigene markieren – sonst würden sie als Schritt aufgezeichnet
        foreach (var hk in s.EffectiveHotkeys().Values)
            if (Hotkeys.Parse(hk, out var m, out var vk)) o.SelfHotkeys.Add((m, vk));
        foreach (var (key, _, _) in RecordingOptions.Channels) if (!s.Records(key)) o.Off.Add(key);
        // Entwickler-Kontext (Pakete 12, 13): ab Werk aus; in „Streng“ wirkungslos (RecordingOptions.ContentSources)
        o.EventLogText = s.EventLogText; o.ClipboardContent = s.ClipboardContent; o.LogFiles = new List<string>(s.LogFiles);
        o.SettingsPrivacyLevel = s.PrivacyLevel;
        return o;
    }

    static RecordingOptions ParseRecordArgs(string[] a)
    {
        string? Opt(string n) { int i = Array.IndexOf(a, n); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
        var scope = (Opt("--scope") ?? "screen") switch { "app" => ScopeMode.App, "region" => ScopeMode.Region, _ => ScopeMode.Screen };
        var o = OptionsFromSettings(scope);
        o.Monitors = Opt("--monitor") ?? "active";
        o.AppProcess = Opt("--app");
        if (Opt("--region") is { } r)
        {
            if (r == "last")
            {
                var (lr, fit) = RegionMemory.Resolve(AppState.Load().LastRegion, StepRecorder.Native.ScreenGrabber.GetMonitors());
                // passt er unverändert: direkt nehmen; sonst öffnet StartRecording die Auswahl mit dieser Vorgabe
                if (lr != null) { o.Region = lr; o.RegionNeedsConfirm = fit is RegionFit.Clamped; }
            }
            else if (r.Split(',').Select(x => int.TryParse(x.Trim(), out var n) ? n : -1).ToArray() is { Length: 4 } v && v[2] > 0 && v[3] > 0) o.Region = new System.Drawing.Rectangle(v[0], v[1], v[2], v[3]);
        }
        o.Title = Opt("--title") ?? "";
        o.GoalDescription = Opt("--goal"); o.GoalExpected = Opt("--expected"); o.GoalReference = Opt("--reference");
        if (Opt("--protection") is { } p) o.PrivacyLevel = p;
        if (Opt("--out") is { } outDir) o.OutputFolder = outDir;
        if (Opt("--profile") is { } prof) ApplyProfile(o, prof);
        if (Opt("--after-stop") is { } after) o.AfterStop = after;
        // --skip moves,typing … schaltet Eingabearten nur für diese Aufnahme ab
        if (Opt("--skip") is { } skip) foreach (var k in skip.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) o.Off.Add(k);
        // --log <Datei> (mehrfach): Protokolldateien nur für diese Aufnahme mitlesen (Paket 13)
        for (int i = 1; i + 1 < a.Length; i++) if (a[i] == "--log") o.LogFiles.Add(a[++i]);
        // schwächerer Schutz als in den Einstellungen: vor dem Start auf stderr (02 §12.5)
        if (o.Weakens(AppSettings.Load().PrivacyLevel) is { Count: > 0 } weak)
            try { Console.Error.WriteLine(Loc.F("Hinweis: Diese Aufnahme schützt schwächer als die Einstellungen: {0}", "Note: this recording protects less than the settings: {0}", string.Join(", ", weak.Select(w => RecordingOptions.WeakenName(w, Loc.Lang))))); } catch { }
        return o;
    }

    /// <summary>
    /// Modus anwenden: Anleitung, Support-Fall und KI-Spur bringen einen festen Detailgrad mit;
    /// „Eigene“ nimmt Eingabearten, Zusatzbilder, Programmkontext und „nach dem Stopp“ aus den Einstellungen.
    /// </summary>
    public static void ApplyProfile(RecordingOptions o, string profile)
    {
        switch (RecordingModes.Find(profile)?.Id.ToLowerInvariant() ?? profile.ToLowerInvariant())
        {
            case "bugrepro":
                o.ProfileId = "bugRepro"; o.ProfileName = Loc.L("Fehler nachstellen", "Reproduce bug"); o.Scope = ScopeMode.Screen; o.Monitors = "active"; o.AfterStop = "viewerError"; break;
            case "support" or "support-fall": o.ProfileId = "support"; o.ProfileName = Loc.L("Support-Fall", "Support case"); o.Scope = ScopeMode.Screen; o.Monitors = "active"; o.AfterStop = "supportPackage"; break;
            case "aitrace" or "ki-spur": o.ProfileId = "aiTrace"; o.ProfileName = Loc.L("KI-Spur", "AI trace"); o.Scope = ScopeMode.Screen; o.AfterStop = "export"; break;
            case "custom" or "eigene":
                var s = AppSettings.Load();
                o.ProfileId = "custom"; o.ProfileName = Loc.L("Eigene", "Custom");
                o.Off = new HashSet<string>(RecordingOptions.Channels.Where(c => !s.Records(c.key)).Select(c => c.key));
                o.ChangeFrames = s.ChangeFrames; o.AppContext = s.CustomAppContext; o.AfterStop = s.CustomAfterStop; o.DetailLevel = "Eigene";
                return;
            default: o.ProfileId = "guide"; o.ProfileName = Loc.L("Anleitung", "Guide"); break;
        }
        if (DetailPresets.For(o.ProfileId) is { } p) DetailPresets.Apply(o, p);
    }

    /// <param name="onFinished">Statt des üblichen „nach dem Stopp“: eigener Abschluss (z. B. Fortsetzung anhängen); null = verworfen.</param>
    public void StartRecording(RecordingOptions o, Action<string?>? onFinished = null)
    {
        PresentationWindow.CloseAll();   // nie in den Bildern einer Aufnahme
        if (Recording != null) return;
        HideReadyTray();
        HideStart();
        foreach (var v in Windows.OfType<ViewerWindow>()) v.Hide();
        Recording = new RecordingController(o);
        Recording.Finished += folder =>
        {
            Recording = null;
            RegisterGlobalHotkeys();
            foreach (var v in Windows.OfType<ViewerWindow>()) v.Show();
            if (_cliRecord) { _cliRecord = false; if (folder != null) SingleInstance.PrintStopped(folder, _cliJson); else _exitCode = 5; }
            if (folder == null) { if (onFinished != null) onFinished(null); else ShowStart(); return; }
            SingleInstance.WriteLastStopped(folder);
            if (onFinished != null) { onFinished(folder); return; }
            switch (o.AfterStop)
            {
                case "none": MaybeShutdown(); break;
                case "folder": System.Diagnostics.Process.Start("explorer.exe", $"\"{folder}\""); ShowStart(); break;
                default:
                    var viewer = OpenViewer(folder);
                    viewer.FreshRecording = true;
                    if (o.AfterStop == "supportPackage") viewer.StartSupportFlow();
                    else if (o.AfterStop == "viewerError") viewer.JumpToFailure();
                    else if (o.AfterStop == "export") viewer.ShowExportDialog("ki");
                    break;
            }
        };
        UnregisterGlobalHotkeys();
        if (!Recording.Start()) { Recording = null; RegisterGlobalHotkeys(); ShowStart(); }
    }

    public static void Log(string text) => AppLog.Write(text);

    /// <summary>Bei einem fatalen Fehler: laufende Aufnahme so weit wie möglich sauber beenden (Rest übernimmt die Wiederherstellung).</summary>
    static void TryEmergencyStop()
    {
        try { (Current as UiApp)?.Recording?.EmergencyStop(); } catch { }
    }

    public static void ShowError(Exception ex)
    {
        Log(ex.ToString());
        Msg.Show(ex.Message, AppInfo.DisplayName, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
