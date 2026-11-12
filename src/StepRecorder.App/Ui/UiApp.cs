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
            return fwd ? 0 : 6;
        }
        var app = new UiApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { ShowError(e.Exception); e.Handled = true; };
        app.Startup += (_, _) => app.OnStart(args);
        return app.Run();
    }

    void OnStart(string[] args)
    {
        if (Environment.GetEnvironmentVariable("STEPRECORDER_TRACE") == "1") Recorder.Trace = Log;
        Theme.Init(this);
        Loc.Init();
        Accessibility.Install();
        DevSnapshots.Install();
        Invisibility.Install();
        SingleInstance.Listen();
        SingleInstance.ArgsReceived += HandleArgs;
        RegisterGlobalHotkeys();
        HandleArgs(args);
    }

    /// <summary>In „Bereit“ ist nur die Sofortaufnahme registriert.</summary>
    public void RegisterGlobalHotkeys()
    {
        _globalHotkeys ??= new Hotkeys();
        var s = AppSettings.Load();
        _globalHotkeys.Register("instant", s.Hotkeys.GetValueOrDefault("instantPause", "Ctrl+Shift+F9"), () => { if (Recording == null) { var o = OptionsFromSettings(ScopeMode.Screen); ApplyProfile(o, AppSettings.Load().LastProfile); o.Scope = ScopeMode.Screen; StartRecording(o); } });
    }

    public void UnregisterGlobalHotkeys() => _globalHotkeys?.UnregisterAll();

    void HandleArgs(string[] args)
    {
        Log("args: " + string.Join(" ", args));
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
            StartRecording(ParseRecordArgs(args));
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

    public void MaybeShutdown()
    {
        if (Recording == null && !Windows.OfType<Window>().Any(w => w.IsVisible && w is StartWindow or ViewerWindow)) Shutdown();
    }

    public ViewerWindow OpenViewer(string path)
    {
        var v = new ViewerWindow(path);
        v.Closed += (_, _) => MaybeShutdown();
        v.Show();
        v.Activate();
        AppSettings.Load().AddRecent(path);
        return v;
    }

    public static RecordingOptions OptionsFromSettings(ScopeMode scope)
    {
        var s = AppSettings.Load();
        var o = new RecordingOptions
        {
            Scope = scope, Language = s.Language, PrivacyLevel = s.PrivacyLevel, ChangeFrames = s.ChangeFrames, Monitors = "active",
            ExcludedProcesses = s.Excluded.Count > 0 ? new List<string>(s.Excluded) : new List<string>(RecordingOptions.DefaultExcluded)
        };
        foreach (var hk in s.Hotkeys.Values)
            if (Hotkeys.Parse(hk, out var m, out var vk)) o.SelfHotkeys.Add((m, vk));
        foreach (var (key, _, _) in RecordingOptions.Channels) if (!s.Records(key)) o.Off.Add(key);
        return o;
    }

    static RecordingOptions ParseRecordArgs(string[] a)
    {
        string? Opt(string n) { int i = Array.IndexOf(a, n); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
        var scope = (Opt("--scope") ?? "screen") switch { "app" => ScopeMode.App, "region" => ScopeMode.Region, _ => ScopeMode.Screen };
        var o = OptionsFromSettings(scope);
        o.Monitors = Opt("--monitor") ?? "active";
        o.AppProcess = Opt("--app");
        if (Opt("--region") is { } r && r.Split(',').Select(int.Parse).ToArray() is { Length: 4 } v) o.Region = new System.Drawing.Rectangle(v[0], v[1], v[2], v[3]);
        o.Title = Opt("--title") ?? "";
        o.GoalDescription = Opt("--goal"); o.GoalExpected = Opt("--expected"); o.GoalReference = Opt("--reference");
        if (Opt("--protection") is { } p) o.PrivacyLevel = p;
        if (Opt("--out") is { } outDir) o.OutputFolder = outDir;
        if (Opt("--profile") is { } prof) ApplyProfile(o, prof);
        if (Opt("--after-stop") is { } after) o.AfterStop = after;
        // --skip moves,typing … schaltet Eingabearten nur für diese Aufnahme ab
        if (Opt("--skip") is { } skip) foreach (var k in skip.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) o.Off.Add(k);
        return o;
    }

    /// <summary>
    /// Modus anwenden: Anleitung, Support-Fall und KI-Spur bringen einen festen Detailgrad mit;
    /// „Eigene“ nimmt Eingabearten, Zusatzbilder, Programmkontext und „nach dem Stopp“ aus den Einstellungen.
    /// </summary>
    public static void ApplyProfile(RecordingOptions o, string profile)
    {
        switch (profile.ToLowerInvariant())
        {
            case "support" or "support-fall": o.ProfileId = "support"; o.ProfileName = "Support-Fall"; o.Scope = ScopeMode.Screen; o.Monitors = "active"; o.AfterStop = "supportPackage"; break;
            case "aitrace" or "ki-spur": o.ProfileId = "aiTrace"; o.ProfileName = "KI-Spur"; o.Scope = ScopeMode.Screen; o.AfterStop = "export"; break;
            case "custom" or "eigene":
                var s = AppSettings.Load();
                o.ProfileId = "custom"; o.ProfileName = "Eigene";
                o.Off = new HashSet<string>(RecordingOptions.Channels.Where(c => !s.Records(c.key)).Select(c => c.key));
                o.ChangeFrames = s.ChangeFrames; o.AppContext = s.CustomAppContext; o.AfterStop = s.CustomAfterStop; o.DetailLevel = "Eigene";
                return;
            default: o.ProfileId = "guide"; o.ProfileName = "Anleitung"; break;
        }
        if (DetailPresets.For(o.ProfileId) is { } p) DetailPresets.Apply(o, p);
    }

    /// <param name="onFinished">Statt des üblichen „nach dem Stopp“: eigener Abschluss (z. B. Fortsetzung anhängen); null = verworfen.</param>
    public void StartRecording(RecordingOptions o, Action<string?>? onFinished = null)
    {
        if (Recording != null) return;
        HideStart();
        foreach (var v in Windows.OfType<ViewerWindow>()) v.Hide();
        Recording = new RecordingController(o);
        Recording.Finished += folder =>
        {
            Recording = null;
            RegisterGlobalHotkeys();
            foreach (var v in Windows.OfType<ViewerWindow>()) v.Show();
            if (folder == null) { if (onFinished != null) onFinished(null); else ShowStart(); return; }
            SingleInstance.WriteLastStopped(folder);
            if (onFinished != null) { onFinished(folder); return; }
            switch (o.AfterStop)
            {
                case "none": MaybeShutdown(); break;
                case "folder": System.Diagnostics.Process.Start("explorer.exe", folder); ShowStart(); break;
                default:
                    var viewer = OpenViewer(folder);
                    if (o.AfterStop == "supportPackage") viewer.StartSupportFlow();
                    else if (o.AfterStop == "export") viewer.ShowExportDialog("ki");
                    break;
            }
        };
        UnregisterGlobalHotkeys();
        if (!Recording.Start()) { Recording = null; RegisterGlobalHotkeys(); ShowStart(); }
    }

    public static void Log(string text)
    {
        try { File.AppendAllText(Path.Combine(AppPaths.StateDir, "StepRecorder.log"), $"{DateTime.Now:O} {text}{Environment.NewLine}"); } catch { }
    }

    public static void ShowError(Exception ex)
    {
        Log(ex.ToString());
        Msg.Show(ex.Message, AppInfo.DisplayName, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
