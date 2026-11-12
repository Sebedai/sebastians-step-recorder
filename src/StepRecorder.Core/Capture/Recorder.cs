using System.Diagnostics;
using System.Drawing;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using StepRecorder.Capture.Host;
using StepRecorder.Data;
using StepRecorder.Native;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Capture;

public enum RecorderState { Ready, PreRun, Recording, Paused, Finishing, Stopped }

/// <summary>
/// Aufzeichnungssteuerung: Zustandsmaschine, Eingabeverarbeitung aus dem Ring, Schutzfilter
/// (Passwort-Rückhaltung, Privattaste, Ausschlüsse, Aufnahmebereich), Lückenmarker, Bilder,
/// Fenster- und Systemereignisse. Alle Rückrufe reihen nur ein; geschrieben wird über den SessionWriter.
/// </summary>
public sealed partial class Recorder : IDisposable
{
    public RecordingOptions Options { get; }
    public RecorderState State { get; private set; } = RecorderState.Ready;
    public string? SessionFolder { get; private set; }
    public event Action<RecorderState>? StateChanged;
    public event Action<string>? ActionLine;
    /// <summary>Wird nach dem Entsperren ausgelöst; die Aufnahme läuft erst nach Bestätigung weiter.</summary>
    public event Action? ResumeConfirmationNeeded;
    public event Action<string>? Warning;
    public bool PrivacyMode { get; private set; }
    public int ActionCount { get; private set; }
    public bool SelfTestPassed { get; set; } = true;
    /// <summary>Bedienelement des Recorders außerhalb eigener Fenster (z. B. Symbol im Infobereich): Klicks dort nicht aufzeichnen.</summary>
    public Func<int, int, bool>? IsRecorderControl { get; set; }
    public string SelfTestMode { get; set; } = "excluded";

    SessionWriter _w = null!;
    HostClient _host = null!;
    FrameEngine _frames = null!;
    UiaService _uia = null!;
    WindowRegistry _reg = null!;
    Topology _topo = null!;
    readonly KeyTranslator _keys = new();
    long _qpc0; readonly double _usPerTick = 1_000_000.0 / Stopwatch.Frequency;
    Thread? _inputThread, _sysThread;
    volatile bool _stopInput;
    long _inSeq;
    DateTime _startUtc;
    readonly object _gate = new();
    readonly List<int> _ownPids = new() { Environment.ProcessId };

    public Recorder(RecordingOptions options) { Options = options; }
    public static Action<string>? Trace;
    static void T(string s) => Trace?.Invoke("[rec] " + s);

    public long Now() => TFromQpc(Stopwatch.GetTimestamp());
    public long TFromQpc(long qpc) => Math.Max(0, (long)((qpc - _qpc0) * _usPerTick));

    long Emit(JsonObject ev) => _w.Append(ev);

    void SetState(RecorderState s) { State = s; try { StateChanged?.Invoke(s); } catch { } }

    // =========================================================================================
    // Start
    // =========================================================================================

    /// <summary>Startet die Aufnahme. Gibt den Sitzungsordner zurück.</summary>
    public string Start(string? hostExe = null)
    {
        if (State != RecorderState.Ready) throw new InvalidOperationException("Aufnahme läuft bereits");
        SetState(RecorderState.PreRun);
        GapIds.Reset();
        _topo = Topology.Read(0);

        // Capture-Host vorbereiten (noch ohne Hooks)
        _host = new HostClient { HostExe = hostExe };
        _host.Start(allowProcess: hostExe != null || Environment.ProcessPath?.EndsWith("StepRecorder.exe", StringComparison.OrdinalIgnoreCase) == true);
        if (!_host.InProcess && _host.Ring.HostPid != 0) _ownPids.Add(_host.Ring.HostPid);

        // Sitzungsordner
        _startUtc = DateTime.UtcNow;
        string baseDir = string.IsNullOrWhiteSpace(Options.OutputFolder) ? Settings.AppPaths.DefaultSessionsFolder() : Options.OutputFolder;
        Directory.CreateDirectory(baseDir);
        string name = DateTime.Now.ToString("yyyy-MM-dd_HHmm") + (string.IsNullOrWhiteSpace(Options.Title) ? "" : "_" + SafeName(Options.Title));
        string folder = Path.Combine(baseDir, name);
        for (int i = 2; Directory.Exists(folder); i++) folder = Path.Combine(baseDir, name + "_" + i);
        SessionFolder = folder;

        string sessionId = Json.NewUuid7();
        var scopeJson = Options.ScopeJson();
        var settings = Options.SettingsJson(scopeJson);
        var recorderInfo = new JsonObject { ["name"] = AppInfo.Name, ["version"] = AppInfo.Version, ["arch"] = "x64", ["elevated"] = IsElevated(), ["captureHost"] = _host.InProcess ? "inProcess" : "process" };
        var tz = TimeZoneInfo.Local;
        var tzJson = new JsonObject { ["id"] = tz.Id, ["offsetMin"] = (int)tz.GetUtcOffset(DateTime.Now).TotalMinutes };
        var env = EnvironmentInfo.Collect(_topo);
        var manifest = new JsonObject
        {
            ["format"] = SessionWriter.FormatName, ["schemaVersion"] = SessionWriter.SchemaVersion, ["sessionId"] = sessionId,
            ["title"] = string.IsNullOrWhiteSpace(Options.Title) ? null : Options.Title, ["state"] = "recording",
            ["createdUtc"] = Json.Utc(_startUtc), ["timeZone"] = tzJson.DeepClone(),
            ["parts"] = new JsonArray(new JsonObject { ["part"] = 0, ["startUtc"] = Json.Utc(_startUtc), ["tStart"] = 0, ["qpcFrequency"] = Stopwatch.Frequency, ["purpose"] = "initial" }),
            ["recorder"] = recorderInfo.DeepClone(), ["environment"] = env.DeepClone(), ["topology"] = _topo.ToJson("initial"),
            ["settings"] = settings.DeepClone(), ["profile"] = Options.ProfileJson(),
            ["history"] = new JsonArray(new JsonObject { ["atUtc"] = Json.Utc(_startUtc), ["action"] = "created", ["tool"] = "StepRecorder " + AppInfo.Version })
        };
        if (Options.GoalJson() is { } goal) manifest["goal"] = goal;
        manifest.Clean();
        _w = SessionWriter.Create(folder, manifest);

        _uia = new UiaService(Now, r => _topo.RectJson(r), Options.ValueMaxChars);
        _reg = new WindowRegistry(Options, Emit, Now, () => _topo, _ownPids) { ScopeTest = InScope };

        // Hooks setzen → Bestätigung = t0
        if (!_host.Command(InputRing.TargetRun, 1500))
            throw new InvalidOperationException("Capture-Host hat die Hooks nicht bestätigt.");
        _qpc0 = Stopwatch.GetTimestamp();
        var part0 = (JsonObject)manifest["parts"]![0]!;
        part0["qpcAtStart"] = _qpc0; part0["tickCountAtStart"] = (long)GetTickCount64();
        _w.SaveManifest();

        T("hooks ok");
        // Sitzungskopf in fester Reihenfolge
        var start = new JsonObject
        {
            ["t"] = 0, ["type"] = "session.start", ["part"] = 0, ["schemaVersion"] = SessionWriter.SchemaVersion, ["sessionId"] = sessionId,
            ["utc"] = Json.Utc(_startUtc), ["tz"] = tzJson, ["qpc"] = _qpc0, ["qpcFrequency"] = Stopwatch.Frequency, ["tickCount"] = (long)GetTickCount64(),
            ["recorder"] = recorderInfo, ["settings"] = settings, ["profile"] = Options.ProfileJson(), ["purpose"] = "initial"
        };
        if (Options.GoalJson() is { } g2) start["goal"] = g2;
        Emit(start);
        var envEv = new JsonObject { ["t"] = 0, ["type"] = "system", ["kind"] = "environment" };
        foreach (var kv in env) envEv[kv.Key] = kv.Value?.DeepClone();
        Emit(envEv);
        var topoEv = _topo.ToJson("initial"); topoEv["t"] = 0; topoEv["type"] = "display.topology";
        Emit(Reorder(topoEv));
        _keys.Reset();
        Emit(KeyStateSync("initial", null));
        var sc = scopeJson.DeepClone().AsObject(); sc["t"] = 0; sc["type"] = "scope.change";
        Emit(Reorder(sc));
        Emit(new JsonObject { ["t"] = 0, ["type"] = "system", ["kind"] = "selfTest", ["test"] = "invisibility", ["passed"] = SelfTestPassed, ["mode"] = SelfTestMode, ["backend"] = "gdi" });
        T("header written");
        var fg = GetForegroundWindow();
        var fgEntry = _reg.Ensure(fg, 0);
        if (fgEntry != null && !fgEntry.Own) { Emit(new JsonObject { ["t"] = 0, ["type"] = "window.foreground", ["win"] = fgEntry.Id }); _fgWin = fgEntry; }

        T("fg ensured");
        _frames = new FrameEngine(Options, _w, _topo, Now, TFromQpc) { MaskProvider = MasksFor, MonitorsForTrigger = MonitorsFor };
        _frames.Start();
        Thread.Sleep(30);
        _frames.Immediate("sessionStart", null, forceKey: true);

        T("first frame");
        // Beobachter starten
        StartSystemObserver();
        T("system observer");
        StartFocusTracking();
        T("focus tracking");
        _stopInput = false;
        _inputThread = new Thread(InputLoop) { IsBackground = true, Name = "Input.Processing", Priority = ThreadPriority.AboveNormal };
        _inputThread.Start();
        SetState(RecorderState.Recording);
        if (fgEntry != null) CheckForegroundGaps(fgEntry, 0);
        return folder;
    }

    static JsonObject Reorder(JsonObject o)
    {
        var r = new JsonObject { ["t"] = o["t"]?.DeepClone(), ["type"] = o["type"]?.DeepClone() };
        foreach (var kv in o.ToList()) if (kv.Key is not ("t" or "type")) { o.Remove(kv.Key); r[kv.Key] = kv.Value; }
        return r;
    }

    static string SafeName(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var t = new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return t.Length > 40 ? t[..40] : t;
    }

    JsonObject KeyStateSync(string reason, string? gapId)
    {
        var down = new JsonArray();
        foreach (uint vk in new uint[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
            if ((GetAsyncKeyState((int)vk) & 0x8000) != 0) { down.Add(new JsonObject { ["vk"] = vk, ["key"] = KeyNames.Name(vk, false) }); _keys.Down(vk); }
        var mouse = new JsonArray();
        if ((GetAsyncKeyState(0x01) & 0x8000) != 0) mouse.Add("primary");
        if ((GetAsyncKeyState(0x02) & 0x8000) != 0) mouse.Add("secondary");
        _keys.SyncLocks();
        var o = new JsonObject { ["t"] = Now(), ["type"] = "keystate.sync", ["reason"] = reason, ["down"] = down, ["mouseDown"] = mouse, ["locks"] = new JsonArray(_keys.Locks().Select(l => (JsonNode)l).ToArray()) };
        if (gapId != null) o["gapId"] = gapId;
        return o;
    }

    // =========================================================================================
    // Aufnahmebereich und Masken
    // =========================================================================================

    bool InScope(WinEntry e)
    {
        if (Options.Scope != ScopeMode.App) return true;
        if (Options.AppWindow != 0) return e.Hwnd == Options.AppWindow || OwnerChainContains(e.Hwnd, Options.AppWindow);
        if (Options.AppPid != null && e.Pid == Options.AppPid) return true;
        if (!string.IsNullOrEmpty(Options.AppProcess) && string.Equals(e.Process, Options.AppProcess, StringComparison.OrdinalIgnoreCase)) return true;
        if (Options.IncludeRelated && e.Process is "ApplicationFrameHost.exe") return true;
        return false;
    }

    static bool OwnerChainContains(nint hwnd, nint target)
    {
        var o = GetAncestor(hwnd, GA_ROOTOWNER);
        return o == target;
    }

    readonly Dictionary<string, long> _monActivity = new();

    IEnumerable<string> MonitorsFor(string? inputMonitor)
    {
        var all = _topo.Monitors.Select(m => _topo.IdOf(m)).ToList();
        switch (Options.Scope)
        {
            case ScopeMode.Region when Options.Region is { } r:
                return _topo.Monitors.Where(m => m.Bounds.IntersectsWith(r)).Select(m => _topo.IdOf(m)).ToList();
            case ScopeMode.Screen when Options.Monitors == "active":
                {
                    long now = Now();
                    var list = _monActivity.Where(kv => now - kv.Value <= 10_000_000).Select(kv => kv.Key).ToList();
                    if (inputMonitor != null && inputMonitor != "*" && !list.Contains(inputMonitor)) list.Add(inputMonitor);
                    if (list.Count == 0) list.Add(_topo.IdOf(_topo.MonitorAt(Cursor().X, Cursor().Y)));
                    return list;
                }
            case ScopeMode.Screen when Options.Monitors != "all":
                return Options.Monitors.Split(',').Select(s => s.Trim()).Where(all.Contains).ToList();
            default:
                return all;
        }
    }

    static Point Cursor() { GetCursorPos(out var p); return new Point(p.X, p.Y); }

    /// <summary>Masken je Monitor: Aufnahmebereich, ausgeschlossene Anwendungen, Passwortfelder, Privatmodus.</summary>
    (List<FrameMask>, List<Rectangle>?) MasksFor(string monId, Rectangle bounds)
    {
        var masks = new List<FrameMask>();
        List<Rectangle>? scope = null;
        // Sichtbare Oberfenster in Z-Reihenfolge
        var covered = new Region(Rectangle.Empty);
        var inScopeRegion = new Region(Rectangle.Empty);
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h) || IsIconic(h)) return true;
            if (!GetWindowRect(h, out var rr) || rr.Width <= 0 || rr.Height <= 0) return true;
            var r = Rectangle.FromLTRB(rr.Left, rr.Top, rr.Right, rr.Bottom);
            if (!r.IntersectsWith(bounds)) return true;
            if (_reg.IsOwn(h)) return true; // eigene Fenster sind per Ausnahme ohnehin unsichtbar
            GetWindowThreadProcessId(h, out uint pid);
            var e = _reg.Peek(h);
            string proc = e?.Process ?? Path.GetFileName(GetProcessPath(pid) ?? "");
            bool excluded = Options.ExcludedProcesses.Any(p => string.Equals(p, proc, StringComparison.OrdinalIgnoreCase));
            using var visible = new Region(r);
            visible.Exclude(covered);
            if (excluded)
            {
                foreach (var vr in Rects(visible, bounds)) masks.Add(new FrameMask(vr, "excludedApp"));
            }
            else if (Options.Scope == ScopeMode.App)
            {
                var entry = e ?? new WinEntry { Hwnd = h, Pid = pid, Process = proc };
                if (InScope(entry)) inScopeRegion.Union(visible);
            }
            covered.Union(r);
            return true;
        }, 0);
        if (Options.Scope == ScopeMode.App)
            scope = Rects(inScopeRegion, bounds).ToList();
        else if (Options.Scope == ScopeMode.Region && Options.Region is { } reg)
        {
            var i = Rectangle.Intersect(reg, bounds);
            scope = i.Width > 0 ? new List<Rectangle> { new(i.X - bounds.X, i.Y - bounds.Y, i.Width, i.Height) } : new List<Rectangle>();
        }
        // Passwortfeld im Fokus schwärzen
        var f = _focus;
        if (f != null && f.IsPassword && f.ScreenRect is { } pr && pr.IntersectsWith(bounds))
        {
            var i = Rectangle.Intersect(pr, bounds);
            masks.Add(new FrameMask(new Rectangle(i.X - bounds.X, i.Y - bounds.Y, i.Width, i.Height), "passwordField"));
        }
        covered.Dispose(); inScopeRegion.Dispose();
        return (masks, scope);
    }

    static IEnumerable<Rectangle> Rects(Region region, Rectangle bounds)
    {
        using var m = new System.Drawing.Drawing2D.Matrix();
        foreach (var rf in region.GetRegionScans(m))
        {
            var r = Rectangle.Round(rf);
            var i = Rectangle.Intersect(r, bounds);
            if (i.Width > 0 && i.Height > 0) yield return new Rectangle(i.X - bounds.X, i.Y - bounds.Y, i.Width, i.Height);
        }
    }

    // =========================================================================================
    // Lücken
    // =========================================================================================

    sealed class OpenGap { public string Id = ""; public string Cause = ""; public long TFrom; public JsonArray Channels = new(); public string InputLost = "none"; public JsonObject? Detail; public int Keys, Clicks, Wheel, Moves; }
    readonly Dictionary<string, OpenGap> _gaps = new();

    OpenGap BeginGap(string cause, long t, string inputLost, params string[] channels)
    {
        if (_gaps.TryGetValue(cause, out var g)) return g;
        g = new OpenGap { Id = GapIds.Next(), Cause = cause, TFrom = t, InputLost = inputLost, Channels = new JsonArray(channels.Select(c => (JsonNode)c).ToArray()) };
        _gaps[cause] = g;
        FlushMove();
        Emit(new JsonObject { ["t"] = t, ["type"] = "gap", ["gapId"] = g.Id, ["phase"] = "begin", ["cause"] = cause, ["tFrom"] = t, ["channels"] = g.Channels.DeepClone(), ["inputLost"] = inputLost });
        return g;
    }

    void EndGap(string cause, long t, JsonObject? detail = null)
    {
        if (!_gaps.Remove(cause, out var g)) return;
        var ev = new JsonObject { ["t"] = t, ["type"] = "gap", ["gapId"] = g.Id, ["phase"] = "end", ["cause"] = cause, ["tFrom"] = g.TFrom, ["tTo"] = t, ["channels"] = g.Channels.DeepClone(), ["inputLost"] = g.InputLost };
        if (cause == "outOfScope") detail = new JsonObject { ["counts"] = new JsonObject { ["keys"] = g.Keys, ["clicks"] = g.Clicks, ["wheel"] = g.Wheel, ["moves"] = g.Moves } };
        if (detail != null) ev["detail"] = detail;
        Emit(ev);
    }

    void SingleGap(string cause, long tFrom, long tTo, string inputLost, JsonObject? detail, params string[] channels)
    {
        var ev = new JsonObject { ["t"] = tFrom, ["type"] = "gap", ["gapId"] = GapIds.Next(), ["phase"] = "single", ["cause"] = cause, ["tFrom"] = tFrom, ["tTo"] = tTo, ["channels"] = new JsonArray(channels.Select(c => (JsonNode)c).ToArray()), ["inputLost"] = inputLost };
        if (detail != null) ev["detail"] = detail;
        Emit(ev);
    }

    // =========================================================================================
    // Steuerung während der Aufnahme
    // =========================================================================================

    public void Pause(string reason = "user")
    {
        lock (_gate)
        {
            if (State != RecorderState.Recording) return;
            long t = Now();
            FlushPendingKeys(force: true);
            FlushMasked(); FlushMove();
            var g = BeginGap("paused", t, "possible", "all");
            Emit(new JsonObject { ["t"] = t, ["type"] = "session.pause", ["reason"] = reason, ["gapId"] = g.Id });
            _w.Checkpoint(t);
            _host.Command(InputRing.TargetPause, 500);
            _frames.Paused = true;
            SetState(RecorderState.Paused);
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (State != RecorderState.Paused) return;
            _host.Command(InputRing.TargetRun, 1000);
            long t = Now();
            var gid = _gaps.TryGetValue("paused", out var g) ? g.Id : GapIds.Next();
            EndGap("paused", t);
            Emit(new JsonObject { ["t"] = t, ["type"] = "session.resume", ["gapId"] = gid });
            _keys.Reset();
            Emit(KeyStateSync("resume", gid));
            _frames.Paused = false;
            _frames.Immediate("resume", null, forceKey: true);
            SetState(RecorderState.Recording);
        }
    }

    public void TogglePrivacy(string trigger = "hotkey")
    {
        lock (_gate)
        {
            long t = Now();
            FlushPendingKeys(force: true);
            if (!PrivacyMode)
            {
                var g = BeginGap("privacyMode", t, "none", "keyboard", "clipboard", "fieldValues");
                Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "privacyMode", ["active"] = true, ["trigger"] = trigger, ["gapId"] = g.Id });
                PrivacyMode = true;
            }
            else
            {
                FlushMasked();
                var gid = _gaps.TryGetValue("privacyMode", out var g) ? g.Id : "";
                EndGap("privacyMode", t, new JsonObject { ["trigger"] = trigger, ["end"] = "toggle" });
                Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "privacyMode", ["active"] = false, ["trigger"] = trigger, ["end"] = "toggle", ["gapId"] = gid });
                PrivacyMode = false;
            }
        }
    }

    int _markerN, _commentN, _redactN;

    /// <summary>Marker setzen (note, error, expected, important, snapshot). Das Bild wird zum Auslösezeitpunkt eingefroren.</summary>
    public void AddMarker(string kind, string? label = null)
    {
        lock (_gate)
        {
            if (State != RecorderState.Recording && State != RecorderState.Paused) return;
            long t = Now();
            FlushMove();
            string id = "mk" + ++_markerN;
            var m = new JsonObject { ["t"] = t, ["type"] = "marker", ["id"] = id, ["kind"] = kind };
            if (!string.IsNullOrWhiteSpace(label)) m["label"] = label.Length > 200 ? label[..200] : label;
            // Bild vorab festlegen: Ereignis braucht die Bild-Id; das Bild verweist auf den Marker
            long seq = _w.LastSeq + 1;
            var frameIds = _frames.Immediate("manual", seq, inputMonitor: CurrentMonitor());
            if (frameIds.Count > 0) m["frame"] = frameIds[0];
            Emit(m);
            ActionLine?.Invoke(kind == "snapshot" ? "Schnappschuss" : "Marker gesetzt");
        }
    }

    /// <summary>Kommentar während der Aufnahme (Bild = eingefrorener Zustand beim Auslösen).</summary>
    public void AddComment(string text, long tTrigger, string? frozenFrameId)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string id = "cm" + ++_commentN;
            var c = new JsonObject { ["t"] = tTrigger, ["tObs"] = Now(), ["type"] = "comment", ["id"] = id, ["text"] = text.Length > 4000 ? text[..4000] : text };
            if (frozenFrameId != null) c["frame"] = frozenFrameId;
            Emit(c);
        }
    }

    /// <summary>Friert vor dem Öffnen eines eigenen Fensters ein Bild ein (Kommentar).</summary>
    public (long t, string? frame) FreezeForComment()
    {
        long t = Now();
        string? f = _frames.FreezeLatest("comment", _w.LastSeq + 1, CurrentMonitor());
        return (t, f);
    }

    public void DiscardPrevious()
    {
        lock (_gate)
        {
            long t = Now();
            Emit(new JsonObject { ["t"] = t, ["type"] = "marker", ["id"] = "mk" + ++_markerN, ["kind"] = "discardPrevious" });
            ActionLine?.Invoke("Letzter Schritt verworfen");
        }
    }

    /// <summary>„Letzte Eingabe schwärzen“: Zeitraum-Schwärzung im Overlay, beim Stopp endgültig angewendet.</summary>
    public void RedactLastInput()
    {
        lock (_gate)
        {
            FlushPendingKeys(force: true);
            FlushMasked();
            if (_lastRunFromSeq == 0) return;
            long t = Now();
            string rid = "r" + ++_redactN;
            long markerSeq = _w.LastSeq + 1;
            Emit(new JsonObject { ["t"] = t, ["type"] = "marker", ["id"] = "mk" + ++_markerN, ["kind"] = "redactLastInput", ["ref"] = _lastRunFromSeq, ["tFrom"] = _lastRunFromT, ["redactionId"] = rid });
            _pendingRedactions.Add(new JsonObject
            {
                ["id"] = rid, ["kind"] = "range", ["origin"] = "period", ["trigger"] = "lastInput", ["markerSeq"] = markerSeq,
                ["seqRanges"] = new JsonArray(new JsonArray(_lastRunFromSeq, _lastRunToSeq)), ["images"] = true, ["createdUtc"] = Json.UtcNow(), ["by"] = "user"
            });
            _lastRunFromSeq = 0;
            ActionLine?.Invoke("Letzte Eingabe geschwärzt");
        }
    }
    readonly List<JsonObject> _pendingRedactions = new();
    long _lastRunFromSeq, _lastRunToSeq, _lastRunFromT;

    string? CurrentMonitor() { var c = Cursor(); return _topo.ToLocal(c.X, c.Y).m; }

    // =========================================================================================
    // Stopp
    // =========================================================================================

    /// <summary>Beendet die Aufnahme sauber und schließt den Sitzungsordner ab.</summary>
    public string Stop(string reason = "user")
    {
        T("stop requested");
        lock (_gate)
        {
            if (State is RecorderState.Ready or RecorderState.Stopped) return SessionFolder ?? "";
            SetState(RecorderState.Finishing);
        }
        try { _host.Command(InputRing.TargetQuit, 500); } catch { }
        _stopInput = true;
        _inputThread?.Join(2000);
        lock (_gate)
        {
            DrainRing();
            FlushPendingKeys(force: true);
            FlushMasked(); FlushMove();
        }
        StopObservers();
        long t = Now();
        if (State != RecorderState.Paused) _frames.Immediate("sessionStop", null);
        _frames.StopAndDrain();
        Thread.Sleep(50);
        t = Math.Max(t, Now());
        foreach (var cause in _gaps.Keys.ToList())
        {
            if (cause == "privacyMode")
            {
                var gid = _gaps[cause].Id;
                EndGap(cause, t, new JsonObject { ["end"] = "stopped" });
                Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "privacyMode", ["active"] = false, ["trigger"] = "hotkey", ["end"] = "stopped", ["gapId"] = gid });
            }
            else EndGap(cause, t);
        }
        Emit(new JsonObject
        {
            ["t"] = t, ["type"] = "session.stop", ["reason"] = reason, ["utc"] = Json.Utc(_startUtc.AddTicks(t * 10)),
            ["counts"] = new JsonObject { ["events"] = _w.LastSeq + 1, ["frames"] = _w.FramesWritten },
            ["performance"] = Performance()
        });
        var endUtc = _startUtc.AddTicks(t * 10);
        _w.Close(t, m =>
        {
            m["state"] = "complete";
            m["endedUtc"] = Json.Utc(endUtc);
            m["durationUs"] = t;
            if (m["parts"] is JsonArray parts && parts[0] is JsonObject p0) { p0["endUtc"] = Json.Utc(endUtc); p0["tEnd"] = t; p0["firstSeq"] = 1; p0["lastSeq"] = _w.LastSeq; }
            m["performance"] = new JsonArray(Performance().AsObject().Also(o => o["part"] = 0));
            ((JsonArray)m["history"]!).Add(new JsonObject { ["atUtc"] = Json.UtcNow(), ["action"] = "finalized", ["tool"] = "StepRecorder " + AppInfo.Version });
        });
        // „Letzte Eingabe schwärzen“ endgültig anwenden
        if (_pendingRedactions.Count > 0) ReadLayer.Redactor.ApplyLastInputRedactions(_w.Folder, _pendingRedactions);
        _uia.Dispose();
        _host.Dispose();
        SetState(RecorderState.Stopped);
        return _w.Folder;
    }

    JsonNode Performance() => new JsonObject
    {
        ["hookCalls"] = _host.Ring.HookCalls,
        ["hookMaxUs"] = (long)(_host.Ring.HookMaxTicks * _usPerTick),
        ["hookP50Us"] = _host.Ring.HookCalls == 0 ? 0 : (long)(_host.Ring.HookSumTicks * _usPerTick / _host.Ring.HookCalls),
        ["hookOver1ms"] = _host.Ring.HookOver1ms,
        ["ringOverflows"] = _host.Ring.Overflows,
        ["framesTriggered"] = _frames.Requested, ["framesKey"] = _frames.KeyFrames, ["framesSame"] = _frames.Same, ["framesDropped"] = _frames.Dropped,
        ["uiaQueries"] = _uia.Queries, ["uiaTimeouts"] = _uia.Timeouts, ["uiaWorkersReplaced"] = _uia.WorkersReplaced,
    };

    /// <summary>Verwirft die Aufnahme vollständig (Ordner wird gelöscht).</summary>
    public void Discard()
    {
        var folder = Stop("discarded");
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
    }

    public void Dispose()
    {
        if (State is RecorderState.Recording or RecorderState.Paused) { try { Stop("error"); } catch { } }
        try { _frames?.Dispose(); } catch { }
        try { _w?.Dispose(); } catch { }
    }
}

static class JsonExt
{
    public static JsonObject Also(this JsonObject o, Action<JsonObject> a) { a(o); return o; }
}
