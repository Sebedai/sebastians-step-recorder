using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Native;

namespace StepRecorder.App.Ui;

/// <summary>Unsichtbarkeit: jedes eigene Fenster (auch Menüs, Popups, Tooltips) wird von der Bildschirmaufnahme ausgenommen.</summary>
static class Invisibility
{
    static DispatcherTimer? _timer;
    public static int Violations;

    public static void Install()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) => Apply((Window)s)));
        // Wächter: alle 100 ms alle Fenster des Prozesses prüfen (Popups und Menüs sind eigene HWNDs)
        _timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Sweep();
        _timer.Start();
    }

    public static void Apply(Window w)
    {
        var h = new WindowInteropHelper(w).EnsureHandle();
        Win32.SetWindowDisplayAffinity(h, Win32.WDA_EXCLUDEFROMCAPTURE);
    }

    public static void Sweep()
    {
        uint pid = (uint)Environment.ProcessId;
        Win32.EnumWindows((h, _) =>
        {
            Win32.GetWindowThreadProcessId(h, out uint p);
            if (p == pid && Win32.GetWindowDisplayAffinity(h, out uint aff) && aff != Win32.WDA_EXCLUDEFROMCAPTURE)
            {
                if (Win32.IsWindowVisible(h)) Violations++;
                Win32.SetWindowDisplayAffinity(h, Win32.WDA_EXCLUDEFROMCAPTURE);
            }
            return true;
        }, 0);
    }

    /// <summary>
    /// Selbsttest: Testfenster mit Muster anzeigen, aufnehmen, prüfen, dass es nicht im Bild ist.
    /// Bewusst tolerant gegenüber Änderungen im Hintergrund (Fokuswechsel, Cursor, Animationen):
    /// geprüft wird nur, ob das Muster oder ein Ersatzbalken (schwarz bei WDA_MONITOR) im Bild erscheint.
    /// </summary>
    public static bool SelfTest()
    {
        // Entwicklertest: Verhalten wie unter Windows Server 2019 nachstellen
        if (Environment.GetEnvironmentVariable("STEPRECORDER_FORCE_NOINVIS") == "1") return Fail("erzwungen (STEPRECORDER_FORCE_NOINVIS)");
        Window? probe = null;
        try
        {
            probe = new Window
            {
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, ShowActivated = false,
                Width = 64, Height = 64, Left = 40, Top = 40, Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xF7))
            };
            bool rendered = false;
            probe.SourceInitialized += (_, _) => Apply(probe);
            probe.ContentRendered += (_, _) => rendered = true;
            var h = new WindowInteropHelper(probe).EnsureHandle();
            if (!Win32.SetWindowDisplayAffinity(h, Win32.WDA_EXCLUDEFROMCAPTURE)) return Fail("SetWindowDisplayAffinity fehlgeschlagen");
            if (!Win32.GetWindowDisplayAffinity(h, out uint aff) || aff != Win32.WDA_EXCLUDEFROMCAPTURE) return Fail($"Affinität nicht gesetzt (0x{aff:X})");

            var src = PresentationSource.FromVisual(probe) ?? HwndSource.FromHwnd(h);
            double sx = src?.CompositionTarget?.TransformToDevice.M11 ?? 1, sy = src?.CompositionTarget?.TransformToDevice.M22 ?? 1;
            var rect = new System.Drawing.Rectangle((int)(probe.Left * sx) + 8, (int)(probe.Top * sy) + 8, 32, 32);
            using var before = ScreenGrabber.Capture(rect);

            probe.Show();
            var sw = Stopwatch.StartNew();
            while (!rendered && sw.ElapsedMilliseconds < 1000) Pump(20);
            Pump(150); // DWM muss den Frame zusammensetzen
            using var during = ScreenGrabber.Capture(rect);
            probe.Close(); probe = null;

            double magenta = Ratio(during, c => c.R > 200 && c.G < 60 && c.B > 200);
            double blackNow = Ratio(during, c => c.R < 8 && c.G < 8 && c.B < 8), blackBefore = Ratio(before, c => c.R < 8 && c.G < 8 && c.B < 8);
            if (magenta > 0.2) return Fail($"Testmuster sichtbar ({magenta:P0})");
            if (blackNow > 0.9 && blackBefore < 0.5) return Fail("Ersatzbalken (schwarz) sichtbar");
            LastFailure = null; LastResult = true;
            return true;
        }
        catch (Exception ex) { return Fail(ex.Message); }
        finally { try { probe?.Close(); } catch { } }
    }

    /// <summary>Ergebnis des letzten Selbsttests (null = bestanden oder nicht gelaufen) – für Diagnose und <c>record --self-test</c>.</summary>
    public static string? LastFailure { get; private set; }
    public static bool? LastResult { get; private set; }

    static bool Fail(string why) { LastFailure = why; LastResult = false; Settings.AppLog.Write("Selbsttest Unsichtbarkeit fehlgeschlagen: " + why); return false; }

    static double Ratio(System.Drawing.Bitmap b, Func<System.Drawing.Color, bool> pred)
    {
        int hit = 0, total = 0;
        for (int y = 0; y < b.Height; y += 2)
            for (int x = 0; x < b.Width; x += 2) { total++; if (pred(b.GetPixel(x, y))) hit++; }
        return total == 0 ? 0 : (double)hit / total;
    }

    static void Pump(int ms)
    {
        // Dispatcher des aktuellen Threads – funktioniert auch ohne Application (Kommandozeile: record --self-test)
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            Thread.Sleep(10);
        }
    }
}

/// <summary>Systemweite Tastenkürzel über RegisterHotKey (erreichen die Zielanwendung nicht).</summary>
sealed class Hotkeys : IDisposable
{
    readonly HwndSource _src;
    readonly Dictionary<int, Action> _actions = new();
    readonly Dictionary<string, int> _ids = new();
    int _next = 1;
    public List<string> Failed { get; } = new();

    public Hotkeys()
    {
        _src = new HwndSource(new HwndSourceParameters("StepRecorder.Hotkeys") { ParentWindow = new IntPtr(-3) });
        _src.AddHook(WndProc);
    }

    public static bool Parse(string combo, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        if (!Settings.HotkeyCombo.TryParse(combo, out var c)) return false;
        mods = c.Mods; vk = c.Vk;
        return true;
    }

    /// <summary>
    /// Ist die Kombination frei? Probe-Registrierung an einem eigenen Fenster und sofortige Freigabe.
    /// Erkennt nur RegisterHotKey-Belegungen anderer Programme, keine Tastatur-Hooks.
    /// </summary>
    public static bool Probe(string combo)
    {
        if (!Parse(combo, out var mods, out var vk)) return false;
        using var src = new HwndSource(new HwndSourceParameters("StepRecorder.HotkeyProbe") { ParentWindow = new IntPtr(-3) });
        if (!Win32.RegisterHotKey(src.Handle, 0x7F00, mods | Win32.MOD_NOREPEAT, vk)) return false;
        Win32.UnregisterHotKey(src.Handle, 0x7F00);
        return true;
    }

    public bool Register(string name, string combo, Action action)
    {
        Unregister(name);
        if (!Parse(combo, out var mods, out var vk)) return false;
        int id = _next++;
        if (!Win32.RegisterHotKey(_src.Handle, id, mods | Win32.MOD_NOREPEAT, vk)) { Failed.Add(name); return false; }
        _actions[id] = action; _ids[name] = id;
        return true;
    }

    public void Unregister(string name)
    {
        if (_ids.Remove(name, out int id)) { Win32.UnregisterHotKey(_src.Handle, id); _actions.Remove(id); }
    }

    public void UnregisterAll() { foreach (var n in _ids.Keys.ToList()) Unregister(n); }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && _actions.TryGetValue((int)w, out var a)) { handled = true; try { a(); } catch (Exception ex) { UiApp.ShowError(ex); } }
        return IntPtr.Zero;
    }

    public void Dispose() { UnregisterAll(); _src.Dispose(); }
}

/// <summary>Eine Instanz je Windows-Sitzung; weitere Starts reichen ihre Argumente per WM_COPYDATA weiter.</summary>
static class SingleInstance
{
    const string MutexName = @"Local\StepRecorder.UI";
    const string WindowTitle = "StepRecorder.Instance";
    static Mutex? _mutex;
    static HwndSource? _src;
    public static event Action<string[]>? ArgsReceived;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool created);
        return created;
    }

    public static void Listen()
    {
        _src = new HwndSource(new HwndSourceParameters(WindowTitle) { ParentWindow = new IntPtr(-3) });
        _src.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (msg == 0x004A) // WM_COPYDATA
            {
                var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(l);
                var text = (Marshal.PtrToStringUni(cds.lpData, cds.cbData / 2) ?? "").TrimEnd('\0');
                var args = text.Split('\u001F', StringSplitOptions.None);
                handled = true;
                // Abfrage ohne Wirkung: läuft eine Aufnahme? (2 = ja, 1 = nein)
                if (args.Length > 0 && args[0] == "--is-recording") return new IntPtr(UiApp.Instance.Recording != null ? 2 : 1);
                Application.Current.Dispatcher.BeginInvoke(() => ArgsReceived?.Invoke(args));
                return new IntPtr(1);
            }
            return IntPtr.Zero;
        });
    }

    public static bool Forward(string[] args) => Send(args, out _);

    /// <summary>Läuft die Oberfläche bereits (in dieser Windows-Sitzung)?</summary>
    public static bool IsUiRunning()
    {
        if (Mutex.TryOpenExisting(MutexName, out var m)) { m.Dispose(); return true; }
        return false;
    }

    /// <summary>Läuft in der Oberfläche gerade eine Aufnahme?</summary>
    public static bool IsRecording() => Send(new[] { "--is-recording" }, out var r) && r == 2;

    static bool Send(string[] args, out long result)
    {
        result = 0;
        var h = FindWindowEx(new IntPtr(-3), IntPtr.Zero, null, WindowTitle);
        if (h == IntPtr.Zero) return false;
        var text = string.Join('\u001F', args);
        var ptr = Marshal.StringToHGlobalUni(text);
        try
        {
            var cds = new COPYDATASTRUCT { dwData = 1, cbData = (text.Length + 1) * 2, lpData = ptr };
            bool ok = SendMessageTimeout(h, 0x004A, IntPtr.Zero, ref cds, 2, 5000, out var res) != IntPtr.Zero;
            result = (long)res;
            return ok;
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    /// <summary><c>record --stop</c>: laufende Aufnahme beenden und den Sitzungspfad ausgeben.</summary>
    public static int SendStop()
    {
        var stateFile = Settings.AppPaths.StateFile;
        // Erkennung über den Zeitstempel „atUtc“ des Stopps – andere Zustandsänderungen (Fensterlage …) zählen nicht
        string? before = Data.Json.ReadObject(stateFile).Str("atUtc");
        if (!Forward(new[] { "record", "--stop" })) { Console.Error.WriteLine(Settings.Loc.L("Keine laufende Aufnahme.", "No recording in progress.")); return 7; }
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 30000)
        {
            Thread.Sleep(200);
            var o = Data.Json.ReadObject(stateFile);
            if (o.Str("atUtc") is { } at && at != before && o.Str("lastStopped") is { } path) { Console.WriteLine(path); return 0; }
        }
        return 1;
    }

    public static void WriteLastStopped(string path)
        => Settings.AppState.Update(s => { s.LastStopped = path; s.AtUtc = Data.Json.UtcNow(); });

    [StructLayout(LayoutKind.Sequential)] struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, int msg, IntPtr w, ref COPYDATASTRUCT l, int flags, int timeout, out IntPtr result);
}

