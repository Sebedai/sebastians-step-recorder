using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Capture;

public sealed class WinEntry
{
    public string Id = "";
    public nint Hwnd;
    public uint Pid;
    public string Process = "";
    public string Title = "";
    public string Class = "";
    public string Kind = "window";
    public string? OwnerId;
    public bool Excluded;
    public string? ExcludedRule;
    public bool InScope = true;
    public bool? Elevated;
    public bool Own;
    public bool Closed;
}

/// <summary>
/// Fensterverzeichnis: vergibt <c>w&lt;n&gt;</c> je Oberfenster (bei HWND-Wiederverwendung neu) und
/// schreibt <c>window.info</c> vor der ersten Verwendung.
/// </summary>
public sealed class WindowRegistry
{
    readonly object _lock = new();
    readonly Dictionary<nint, WinEntry> _byHwnd = new();
    int _next = 1;
    readonly Func<JsonObject, long> _emit;
    readonly Func<long> _now;
    readonly RecordingOptions _opt;
    readonly Func<Topology> _topo;
    readonly int _ownPid = Environment.ProcessId;
    readonly HashSet<int> _ownPids;
    public Func<WinEntry, bool>? ScopeTest;
    /// <summary>Nach dem Schreiben von <c>window.info</c> (Programm-Kontext, Paket 10); muss schnell sein.</summary>
    public Action<WinEntry, long>? Registered;

    public WindowRegistry(RecordingOptions opt, Func<JsonObject, long> emit, Func<long> now, Func<Topology> topo, IEnumerable<int>? ownPids = null)
    {
        _opt = opt; _emit = emit; _now = now; _topo = topo;
        _ownPids = new HashSet<int>(ownPids ?? Array.Empty<int>()) { _ownPid };
    }

    public void AddOwnPid(int pid) { lock (_lock) _ownPids.Add(pid); }

    public static nint Root(nint hwnd)
    {
        if (hwnd == 0) return 0;
        var r = GetAncestor(hwnd, GA_ROOT);
        return r == 0 ? hwnd : r;
    }

    public WinEntry? Peek(nint hwnd) { lock (_lock) return _byHwnd.TryGetValue(Root(hwnd), out var e) ? e : null; }

    public bool IsOwn(nint hwnd)
    {
        if (hwnd == 0) return false;
        GetWindowThreadProcessId(Root(hwnd), out uint pid);
        lock (_lock) return _ownPids.Contains((int)pid);
    }

    /// <summary>Liefert den Eintrag zum Oberfenster; schreibt bei Bedarf <c>window.info</c>.</summary>
    public WinEntry? Ensure(nint hwnd, long? t = null)
    {
        hwnd = Root(hwnd);
        if (hwnd == 0) return null;
        lock (_lock)
        {
            if (_byHwnd.TryGetValue(hwnd, out var e))
            {
                if (!IsWindow(hwnd) && !e.Closed) e.Closed = true;
                if (!e.Closed) return e;
            }
            e = Build(hwnd);
            _byHwnd[hwnd] = e;
            if (!e.Own) { long tt = t ?? _now(); _emit(InfoEvent(e, tt)); try { Registered?.Invoke(e, tt); } catch { } }
            return e;
        }
    }

    WinEntry Build(nint hwnd)
    {
        var e = new WinEntry { Id = "w" + _next++, Hwnd = hwnd };
        GetWindowThreadProcessId(hwnd, out e.Pid);
        e.Own = _ownPids.Contains((int)e.Pid);
        var path = GetProcessPath(e.Pid);
        e.Process = path != null ? System.IO.Path.GetFileName(path) : "";
        e.Class = GetClassNameOf(hwnd);
        e.Title = GetWindowTitle(hwnd);
        nint owner = GetAncestor(hwnd, GA_ROOTOWNER);
        if (owner != 0 && owner != hwnd && _byHwnd.TryGetValue(owner, out var oe)) e.OwnerId = oe.Id;
        e.Kind = KindOf(hwnd, e.Class, owner != hwnd);
        e.Elevated = IsProcessElevated(e.Pid);
        var rule = _opt.ExcludedProcesses.FirstOrDefault(p => string.Equals(p, e.Process, StringComparison.OrdinalIgnoreCase));
        if (rule != null) { e.Excluded = true; e.ExcludedRule = rule; }
        e.InScope = ScopeTest?.Invoke(e) ?? true;
        return e;
    }

    static string KindOf(nint hwnd, string cls, bool owned)
    {
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        return cls switch
        {
            "#32770" => "dialog",
            "#32768" => "menu",
            "tooltips_class32" => "tooltip",
            "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Windows.UI.Core.CoreWindow" or "Progman" or "WorkerW" or "MultitaskingViewFrame" or "XamlExplorerHostIslandWindow" => "systemUi",
            "ComboLBox" => "dropdown",
            _ when (ex & WS_EX_TOOLWINDOW) != 0 && owned => "popup",
            _ when owned && cls.Contains("Dialog", StringComparison.OrdinalIgnoreCase) => "dialog",
            _ => "window"
        };
    }

    JsonObject InfoEvent(WinEntry e, long t)
    {
        var o = new JsonObject { ["t"] = t, ["type"] = "window.info", ["win"] = e.Id, ["hwnd"] = Json.Hex(e.Hwnd), ["kind"] = e.Kind };
        if (e.Excluded)
        {
            o["excluded"] = true; o["excludedRule"] = e.ExcludedRule;
            return o;
        }
        o["pid"] = e.Pid;
        if (e.Process.Length > 0) o["process"] = e.Process;
        if (e.Title.Length > 0) o["title"] = Trunc(e.Title, 500);
        if (e.Class.Length > 0) o["class"] = e.Class;
        if (e.OwnerId != null) o["owner"] = e.OwnerId;
        if (GetWindowRect(e.Hwnd, out var r) && r.Width > 0)
        {
            var topo = _topo();
            o["ep"] = topo.Epoch;
            o["rect"] = topo.RectJson(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
        }
        o["state"] = IsIconic(e.Hwnd) ? "minimized" : !IsWindowVisible(e.Hwnd) ? "hidden" : "normal";
        if (e.Elevated != null) o["elevated"] = e.Elevated;
        o["surface"] = SurfaceOf(e);
        if (SurfaceKindOf(e) is { } sk) o["surfaceKind"] = sk;
        if (!e.InScope) o["inScope"] = false;
        return o;
    }

    public static string SurfaceOf(WinEntry e) => SurfaceFor(SurfaceKindOf(e));

    /// <summary>Oberflächenart → structured | partial (Terminal) | opaque (Remote-Desktop, VM).</summary>
    public static string SurfaceFor(string? kind) => kind is "remoteDesktop" or "virtualMachine" ? "opaque" : kind == "terminal" ? "partial" : "structured";

    public static string? SurfaceKindOf(WinEntry e) => SurfaceKindOf(e.Process, e.Class);

    public static string? SurfaceKindOf(string process, string cls)
    {
        var p = process.ToLowerInvariant();
        if (p is "mstsc.exe" or "msrdc.exe" or "wfica32.exe" or "cdviewer.exe" or "vncviewer.exe" or "tvnviewer.exe" or "anydesk.exe" or "teamviewer.exe" or "rustdesk.exe" or "parsecd.exe") return "remoteDesktop";
        if (p is "vmconnect.exe" or "vmware.exe" or "vmware-vmx.exe" or "vmplayer.exe" or "virtualboxvm.exe" or "qemu-system-x86_64.exe" or "qemu-system-x86_64w.exe") return "virtualMachine";
        if (cls is "ConsoleWindowClass" or "CASCADIA_HOSTING_WINDOW_CLASS" or "PuTTY" or "mintty" or "VirtualConsoleClass"
            || p is "windowsterminal.exe" or "conhost.exe" or "openconsole.exe" or "putty.exe" or "kitty.exe" or "mintty.exe" or "alacritty.exe" or "wezterm-gui.exe"
                 or "tabby.exe" or "hyper.exe" or "mobaxterm.exe" or "conemu.exe" or "conemu64.exe" or "cmder.exe" or "securecrt.exe" or "xshell.exe" or "termius.exe" or "fluentterminal.app.exe")
            return "terminal";
        return null;
    }

    /// <summary>Oberfläche aus einem <c>window.info</c>-Ereignis (ältere Aufnahmen ohne <c>surface</c>: aus Prozess und Klasse).</summary>
    public static string SurfaceOfInfo(JsonObject w) => w["surface"]?.GetValue<string>() ?? SurfaceFor(SurfaceKindOf(w["process"]?.GetValue<string>() ?? "", w["class"]?.GetValue<string>() ?? ""));

    /// <summary>Titeländerung erkennen und als <c>window.title</c> schreiben.</summary>
    public void TitleChanged(nint hwnd, long t)
    {
        WinEntry? e;
        lock (_lock) { if (!_byHwnd.TryGetValue(hwnd, out e) || e.Own || e.Closed) return; }
        var title = GetWindowTitle(hwnd);
        if (title == e.Title) return;
        e.Title = title;
        var o = new JsonObject { ["t"] = t, ["tSrc"] = "sys", ["tErr"] = 16000, ["type"] = "window.title", ["win"] = e.Id };
        if (e.Excluded) o["titleWithheld"] = "excludedApp"; else o["title"] = Trunc(title, 500);
        _emit(o);
    }

    public WinEntry? MarkClosed(nint hwnd)
    {
        lock (_lock)
        {
            if (_byHwnd.TryGetValue(hwnd, out var e) && !e.Closed) { e.Closed = true; return e; }
            return null;
        }
    }

    public List<WinEntry> All() { lock (_lock) return _byHwnd.Values.ToList(); }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
}
