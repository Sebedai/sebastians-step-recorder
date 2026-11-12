using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using Microsoft.Win32;
using StepRecorder.Data;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Capture;

/// <summary>Zuletzt bekannter Fokuszustand (für Rückhaltepuffer und Passwortmasken).</summary>
public sealed class FocusInfo
{
    public long TObs;
    public string Ctx = "unknown";
    public bool IsPassword;
    public Rectangle? ScreenRect;
    public nint Hwnd;
    public JsonObject? El;
}

public sealed partial class Recorder
{
    volatile FocusInfo? _focus;
    long _focusSeq;
    bool _probePending;
    string? _lastFocusRid;

    void RequestFocusProbe(long t)
    {
        if (_probePending) return;
        _probePending = true;
        var level = Win32Level;
        _uia.Enqueue(() => WithFocusWin32(() => _uia.Focused(false), level), res =>
        {
            lock (_gate)
            {
                _probePending = false;
                ApplyFocus(res, fromEvent: false);
                if (_pendingKeys.Count > 0 && (_focus == null || _focus.TObs < _pendingKeys[^1].T)) RequestFocusProbe(Now());
            }
        });
    }

    void ApplyFocus(UiaResult res, bool fromEvent)
    {
        if (_w.Closed) return;
        nint hwnd = 0;
        if (res.El?.Str("hwnd") is { } hs) hwnd = WindowRegistry.Root((nint)Convert.ToInt64(hs, 16));
        if (hwnd == 0) hwnd = GetForegroundWindow();
        var win = _reg.Ensure(hwnd, res.TStart);
        if (win != null && win.Own) { return; }
        string ctx = res.Status != "ok" ? (res.Status == "timeout" ? "unknown" : "unknown") : res.Ctx;
        if (win != null && WindowRegistry.SurfaceOf(win) == "opaque") ctx = "opaque";
        Rectangle? rect = res.ScreenRect;
        _focus = new FocusInfo { TObs = res.TEnd, Ctx = ctx, IsPassword = res.IsPassword, ScreenRect = rect, Hwnd = hwnd, El = res.El };
        string? rid = res.El?.Str("rid");
        // Neues Fokuselement: focus- und element-Ereignis schreiben
        if (res.Status == "ok" && res.El != null && rid != _lastFocusRid && win is not { Excluded: true })
        {
            _lastFocusRid = rid;
            FlushPendingKeys(force: false);
            var fev = new JsonObject { ["t"] = res.TStart, ["tObs"] = res.TEnd, ["type"] = "focus", ["win"] = win?.Id, ["ctx"] = ctx, ["ctxSrc"] = "uia", ["ms"] = res.Ms, ["el"] = ElementRef(res.El) };
            fev.Clean();
            _focusSeq = Emit(fev);
            var eev = ElementEvent("focus", _focusSeq, res, null, 0, 0, win);
            Emit(eev);
            if (res.Value != null && !res.IsPassword && !PrivacyMode)
                Emit(new JsonObject { ["t"] = res.TEnd, ["type"] = "ui.value", ["el"] = ElementRef(res.El), ["win"] = win?.Id, ["phase"] = "focusIn", ["value"] = res.Value }.Clean());
        }
    }

    /// <summary>
    /// Fokusverfolgung über WinEvents (EVENT_OBJECT_FOCUS) mit anschließender UIA-Abfrage auf dem Arbeiter.
    /// Keine UIA-Ereignisregistrierung im Prozess mit eigener Oberfläche (blockiert sonst).
    /// </summary>
    void StartFocusTracking() { }

    long _focusQueryPending;
    void OnFocusWinEvent()
    {
        if (Interlocked.Exchange(ref _focusQueryPending, 1) == 1) return;
        var level = Win32Level;
        _uia.Enqueue(() => { Interlocked.Exchange(ref _focusQueryPending, 0); return WithFocusWin32(() => _uia.Focused(true), level); }, res => { lock (_gate) ApplyFocus(res, true); });
    }

    static UiaResult WithFocusWin32(Func<UiaResult> query, Win32Detail.TextLevel level)
    {
        JsonObject? w32 = null;
        try { w32 = Win32Detail.Focused(level); } catch { }
        var r = UiaService.Safe(query);
        r.Win32 = w32;
        return r;
    }

    // =========================================================================================
    // Fenster- und Systemereignisse
    // =========================================================================================

    uint _sysThreadId;
    readonly List<nint> _winEventHooks = new();
    WinEventProc? _winEventProc;
    ClipboardWindow? _clipWin;
    int _clipN;

    void StartSystemObserver()
    {
        // ausgeschlossene Programme: Win32-Zusatzangaben (Listen, Menüs, Schreibmarke, Statusleiste) gar nicht erst lesen
        Win32Detail.ExcludedExe = exe => Options.ExcludedProcesses.Any(p => string.Equals(p, exe, StringComparison.OrdinalIgnoreCase));
        var ready = new ManualResetEventSlim();
        _sysThread = new Thread(() =>
        {
            _sysThreadId = GetCurrentThreadId();
            _winEventProc = OnWinEvent;
            void Hook(uint min, uint max) { var h = SetWinEventHook(min, max, 0, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS); if (h != 0) _winEventHooks.Add(h); }
            Hook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND);
            Hook(EVENT_SYSTEM_MENUSTART, EVENT_SYSTEM_MENUPOPUPEND);
            Hook(EVENT_SYSTEM_DIALOGSTART, EVENT_SYSTEM_DIALOGEND);
            Hook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND);
            Hook(EVENT_OBJECT_SHOW, EVENT_OBJECT_HIDE);
            Hook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY);
            Hook(EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE);
            Hook(0x8005, 0x8005); // EVENT_OBJECT_FOCUS
            try { _clipWin = new ClipboardWindow(OnClipboard); } catch { }
            ready.Set();
            while (GetMessage(out var msg, 0, 0, 0) > 0) { TranslateMessage(ref msg); DispatchMessage(ref msg); }
            foreach (var h in _winEventHooks) UnhookWinEvent(h);
            _winEventHooks.Clear();
            _clipWin?.DestroyHandle();
        }) { IsBackground = true, Name = "System.Observer" };
        _sysThread.SetApartmentState(ApartmentState.STA);
        _sysThread.Start();
        ready.Wait(2000);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        SystemEvents.PowerModeChanged += OnPowerMode;
    }

    void StopObservers()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        SystemEvents.PowerModeChanged -= OnPowerMode;
        if (_sysThreadId != 0) PostThreadMessage(_sysThreadId, WM_QUIT, 0, 0);
        _sysThread?.Join(1000);
    }

    void OnWinEvent(nint hook, uint ev, nint hwnd, int idObject, int idChild, uint thread, uint ms)
    {
        if (ev == 0x8005)
        {
            if (State == RecorderState.Recording && hwnd != 0 && !_reg.IsOwn(hwnd)) OnFocusWinEvent();
            return;
        }
        if (idObject != OBJID_WINDOW || idChild != 0 || hwnd == 0) return;
        long t = Now();
        // Systemzeit (ms) → Sitzungszeit; WinEvents haben ±16 ms
        long tick = (long)GetTickCount64();
        long tEv = Math.Max(0, t - Math.Max(0, ((tick & 0xFFFFFFFF) - ms) * 1000L));
        if (tEv > t || t - tEv > 5_000_000) tEv = t;
        lock (_gate)
        {
            if (_w.Closed || State is not (RecorderState.Recording)) return;
            try { HandleWinEvent(ev, hwnd, tEv, t); } catch { }
        }
    }

    void HandleWinEvent(uint ev, nint hwnd, long t, long tObs)
    {
        bool topLevel = GetAncestor(hwnd, GA_ROOT) == hwnd;
        switch (ev)
        {
            case EVENT_SYSTEM_FOREGROUND:
                {
                    var e = _reg.Ensure(hwnd, t);
                    if (e == null || e.Own) return;
                    if (_fgWin == e) return;
                    var prev = _fgWin;
                    _fgWin = e;
                    Emit(new JsonObject { ["t"] = t, ["tObs"] = tObs, ["tSrc"] = "sys", ["tErr"] = 16000, ["type"] = "window.foreground", ["win"] = e.Id, ["prev"] = prev?.Id }.Clean());
                    CheckForegroundGaps(e, t);
                    break;
                }
            case EVENT_OBJECT_SHOW when topLevel:
                {
                    if (_reg.IsOwn(hwnd) || !IsWindowVisible(hwnd)) return;
                    bool known = _reg.Peek(hwnd) is { Closed: false };
                    var e = _reg.Ensure(hwnd, t);
                    if (e == null || known) return;
                    if (GetWindowRect(hwnd, out var r) && (r.Width < 4 || r.Height < 4)) return;
                    Emit(new JsonObject { ["t"] = t, ["tObs"] = tObs, ["tSrc"] = "sys", ["tErr"] = 16000, ["type"] = "window.open", ["win"] = e.Id, ["kind"] = e.Kind, ["first"] = true });
                    break;
                }
            case EVENT_OBJECT_DESTROY or EVENT_OBJECT_HIDE when topLevel:
                {
                    var e = _reg.Peek(hwnd);
                    if (e == null || e.Own || e.Closed) return;
                    if (ev == EVENT_OBJECT_HIDE && IsWindow(hwnd) && e.Kind == "window" && IsIconic(hwnd)) return;
                    _reg.MarkClosed(hwnd);
                    Emit(new JsonObject { ["t"] = t, ["tObs"] = tObs, ["tSrc"] = "sys", ["tErr"] = 16000, ["type"] = "window.close", ["win"] = e.Id, ["destroyed"] = ev == EVENT_OBJECT_DESTROY });
                    break;
                }
            case EVENT_OBJECT_NAMECHANGE when topLevel:
                _reg.TitleChanged(hwnd, t);
                break;
            case EVENT_SYSTEM_MENUPOPUPSTART or EVENT_SYSTEM_MENUPOPUPEND or EVENT_SYSTEM_MENUSTART or EVENT_SYSTEM_MENUEND:
                {
                    var owner = _fgWin;
                    string phase = ev switch { EVENT_SYSTEM_MENUSTART => "start", EVENT_SYSTEM_MENUEND => "end", EVENT_SYSTEM_MENUPOPUPSTART => "popupStart", _ => "popupEnd" };
                    Emit(new JsonObject { ["t"] = t, ["tObs"] = tObs, ["tSrc"] = "sys", ["tErr"] = 16000, ["type"] = "window.menu", ["win"] = owner?.Id, ["phase"] = phase }.Clean());
                    break;
                }
            case EVENT_SYSTEM_DIALOGSTART or EVENT_SYSTEM_DIALOGEND:
                {
                    var e = _reg.Ensure(hwnd, t);
                    if (e == null || e.Own) return;
                    Emit(new JsonObject { ["t"] = t, ["tObs"] = tObs, ["tSrc"] = "sys", ["tErr"] = 16000, ["type"] = "window.dialog", ["win"] = e.Id, ["phase"] = ev == EVENT_SYSTEM_DIALOGSTART ? "start" : "end" });
                    break;
                }
            case EVENT_SYSTEM_MINIMIZESTART or EVENT_SYSTEM_MINIMIZEEND:
                {
                    var e = _reg.Peek(hwnd);
                    if (e == null || e.Own || !GetWindowRect(hwnd, out var r)) return;
                    Emit(new JsonObject { ["t"] = t, ["tSrc"] = "sys", ["tErr"] = 16000, ["type"] = "window.rect", ["win"] = e.Id, ["rect"] = _topo.RectJson(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)), ["state"] = ev == EVENT_SYSTEM_MINIMIZESTART ? "minimized" : "normal", ["ep"] = _topo.Epoch });
                    break;
                }
        }
    }

    void OnClipboard()
    {
        long t = Now();
        lock (_gate)
        {
            if (_w.Closed || State != RecorderState.Recording) return;
            var formats = ClipboardFormats();
            nint owner = GetClipboardOwner();
            var ownerWin = owner != 0 ? _reg.Ensure(owner, t) : _fgWin;
            if (ownerWin is { Own: true }) return;
            string kind = formats.Count == 0 ? "empty"
                : formats.Contains("CF_HDROP") ? "files"
                : formats.Contains("HTML Format") ? (formats.Contains("CF_UNICODETEXT") ? "mixed" : "html")
                : formats.Contains("CF_UNICODETEXT") || formats.Contains("CF_TEXT") ? "text"
                : formats.Contains("CF_BITMAP") || formats.Contains("CF_DIB") || formats.Contains("PNG") ? "image" : "other";
            bool sensitive = formats.Contains("ExcludeClipboardContentFromMonitorProcessing") || formats.Contains("CanIncludeInClipboardHistory") || formats.Contains("CanUploadToCloudClipboard");
            var ev = new JsonObject { ["t"] = t, ["type"] = "clipboard", ["clip"] = "cb" + ++_clipN, ["seqNo"] = GetClipboardSequenceNumber(), ["kind"] = kind, ["formats"] = new JsonArray(formats.Take(20).Select(f => (JsonNode)f).ToArray()) };
            if (ownerWin != null && !ownerWin.Excluded) ev["ownerWin"] = ownerWin.Id;
            if (sensitive) ev["sensitive"] = true;
            if (kind != "empty") ev["withheld"] = ownerWin is { Excluded: true } ? "excludedApp" : PrivacyMode ? "privacyMode" : sensitive ? "sensitiveFormat" : "optOut";
            Emit(ev);
        }
    }

    static List<string> ClipboardFormats()
    {
        var list = new List<string>();
        var buf = new uint[64];
        if (!GetUpdatedClipboardFormats(buf, (uint)buf.Length, out uint n)) return list;
        for (int i = 0; i < n; i++) list.Add(FormatName(buf[i]));
        return list;
    }

    static string FormatName(uint f) => f switch
    {
        1 => "CF_TEXT", 2 => "CF_BITMAP", 7 => "CF_OEMTEXT", 8 => "CF_DIB", 13 => "CF_UNICODETEXT", 15 => "CF_HDROP", 16 => "CF_LOCALE", 17 => "CF_DIBV5",
        _ => GetClipboardFormatName(f, out var s) ? s : "CF_" + f
    };

    static bool GetClipboardFormatName(uint f, out string name)
    {
        var sb = new StringBuilder(128);
        int n = GetClipboardFormatNameW(f, sb, sb.Capacity);
        name = sb.ToString();
        return n > 0;
    }

    [DllImport("user32.dll")] static extern bool GetUpdatedClipboardFormats([Out] uint[] formats, uint count, out uint actual);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClipboardFormatNameW")] static extern int GetClipboardFormatNameW(uint f, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern nint GetClipboardOwner();

    void OnSessionSwitch(object? s, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
        {
            lock (_gate)
            {
                if (State != RecorderState.Recording) return;
                long t = Now();
                Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = e.Reason == SessionSwitchReason.SessionLock ? "sessionLock" : "consoleDisconnect" });
                BeginGap(e.Reason == SessionSwitchReason.SessionLock ? "sessionLocked" : "sessionDisconnected", t, "possible", "all");
            }
        }
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
        {
            lock (_gate)
            {
                long t = Now();
                if (!_gaps.ContainsKey("sessionLocked") && !_gaps.ContainsKey("sessionDisconnected")) return;
                Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = e.Reason == SessionSwitchReason.SessionUnlock ? "sessionUnlock" : "consoleConnect" });
                EndGap("sessionLocked", t); EndGap("sessionDisconnected", t);
            }
            Pause("lock");
            ResumeConfirmationNeeded?.Invoke();
        }
    }

    void OnPowerMode(object? s, PowerModeChangedEventArgs e)
    {
        lock (_gate)
        {
            if (_w.Closed) return;
            long t = Now();
            if (e.Mode == PowerModes.Suspend) { Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "powerSuspend" }); BeginGap("suspend", t, "none", "all"); }
            else if (e.Mode == PowerModes.Resume) { Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "powerResume" }); EndGap("suspend", t); }
        }
    }

    void OnDisplayChanged(object? s, EventArgs e)
    {
        lock (_gate)
        {
            if (_w.Closed) return;
            var nt = Topology.Read(_topo.Epoch + 1);
            if (nt.SameAs(_topo)) return;
            FlushMove();
            _topo = nt;
            var ev = nt.ToJson("displayChange"); ev["t"] = Now(); ev["type"] = "display.topology";
            Emit(Reorder(ev));
            _frames.SetTopology(nt);
            _frames.Immediate("topology", null, forceKey: true);
        }
    }

    /// <summary>Unsichtbares Fenster für die Zwischenablage-Benachrichtigung (öffnet die Zwischenablage nie).</summary>
    sealed class ClipboardWindow : System.Windows.Forms.NativeWindow
    {
        readonly Action _onUpdate;
        public ClipboardWindow(Action onUpdate)
        {
            _onUpdate = onUpdate;
            CreateHandle(new System.Windows.Forms.CreateParams { Parent = new nint(-3) });
            AddClipboardFormatListener(Handle);
        }
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE) { try { _onUpdate(); } catch { } }
            base.WndProc(ref m);
        }
        public new void DestroyHandle() { try { RemoveClipboardFormatListener(Handle); } catch { } base.DestroyHandle(); }
    }
}
