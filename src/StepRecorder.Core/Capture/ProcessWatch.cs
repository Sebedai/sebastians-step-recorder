using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Native;

namespace StepRecorder.Capture;

/// <summary>
/// Programm-Kontext (12, Paket 10): Programme, mit deren Fenstern gearbeitet wurde – Start (<c>process.info</c> mit
/// Build-Kennung), Ende mit Exit-Code (<c>process.exit</c>), Hänger (<c>window.hang</c>) und Fakten aus der
/// Ereignisanzeige (<c>eventlog.entry</c>). Ohne Adminrechte: nur ein Handle mit SYNCHRONIZE und eingeschränkter
/// Abfrage, keine Injektion, kein Prozessspeicher. Dateien und <c>.git</c> werden auf einem eigenen Arbeiter gelesen,
/// nie im Eingabe- oder Beobachter-Thread. Ausgeschlossene und eigene Programme werden nie beobachtet.
/// </summary>
public sealed class ProcessWatch : IDisposable
{
    public const int MaxProcesses = 64, MaxWindowsPerProcess = 24;

    sealed class Tracked
    {
        public string Id = ""; public uint Pid; public string Process = ""; public nint Handle; public string? LastWin;
        public readonly List<(nint hwnd, string id)> Windows = new();
        public volatile bool Ready, Exited;
        public string? HungWin; public long HungSince;
    }

    readonly Dictionary<uint, Tracked> _byPid = new();
    readonly object _lock = new();
    readonly BlockingCollection<Action> _jobs = new(256);
    readonly Thread _worker;
    readonly Func<long> _now;
    readonly DateTime _startUtc;
    readonly Action<JsonObject> _emit;
    readonly bool _fileContext;
    readonly HashSet<long> _seenRecords = new();
    long _eventLogDueTick;
    int _next = 1;
    volatile bool _disposed;

    /// <param name="emit">Schreibt ein Ereignis unter der Sperre der Aufnahme (verwirft es, wenn die Aufnahme schon endet).</param>
    /// <param name="fileContext">Programmdatei und <c>.git</c> lesen („Erweiterter Programmkontext“).</param>
    public ProcessWatch(Func<long> now, DateTime startUtc, Action<JsonObject> emit, bool fileContext)
    {
        _now = now; _startUtc = startUtc; _emit = emit; _fileContext = fileContext;
        _worker = new Thread(Work) { IsBackground = true, Name = "Process.Watch", Priority = ThreadPriority.BelowNormal };
        _worker.Start();
    }

    /// <summary>Ein Fenster wurde registriert (aus dem Fensterverzeichnis; muss schnell sein).</summary>
    public void Observe(WinEntry e, long t)
    {
        if (_disposed || e.Own || e.Excluded || !e.InScope || e.Pid == 0 || e.Process.Length == 0 || e.Kind is "systemUi" or "tooltip") return;
        lock (_lock)
        {
            if (_byPid.TryGetValue(e.Pid, out var tr))
            {
                if (!tr.Exited && tr.Windows.Count < MaxWindowsPerProcess && !tr.Windows.Any(w => w.hwnd == e.Hwnd)) tr.Windows.Add((e.Hwnd, e.Id));
                tr.LastWin = e.Id;
                return;
            }
            if (_byPid.Count >= MaxProcesses) return;
            nint h = Win32.OpenProcess(0x00100000 /*SYNCHRONIZE*/ | Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, e.Pid);
            tr = new Tracked { Id = "p" + _next++, Pid = e.Pid, Process = e.Process, Handle = h, LastWin = e.Id };
            tr.Windows.Add((e.Hwnd, e.Id));
            _byPid[e.Pid] = tr;
            var captured = tr;
            _jobs.TryAdd(() => Describe(captured, t));
        }
    }

    /// <summary>Im 250-ms-Takt (außerhalb der Sperre): Programmende und Hänger.</summary>
    public void Poll(bool recording)
    {
        if (_disposed) return;
        List<Tracked> list;
        lock (_lock) list = _byPid.Values.Where(x => x.Ready && !x.Exited).ToList();
        foreach (var tr in list)
        {
            if (tr.Handle != 0 && WaitForSingleObject(tr.Handle, 0) == 0)
            {
                if (!recording) continue;   // erst nach der Pause melden
                tr.Exited = true;
                uint code = GetExitCodeProcess(tr.Handle, out var c) ? c : 0xFFFFFFFF;
                long t = _now();
                if (GetProcessTimes(tr.Handle, out _, out long exitFt, out _, out _) && exitFt > 0)
                {
                    var exitUtc = DateTime.FromFileTimeUtc(exitFt);
                    long ago = (long)((DateTime.UtcNow - exitUtc).TotalMilliseconds * 1000);
                    if (ago > 0 && ago < 60_000_000) t = Math.Max(0, t - ago);
                }
                string cls = code == 0xFFFFFFFF ? "unknown" : ExitCodes.Classify(code);
                var ev = new JsonObject
                {
                    ["t"] = t, ["tSrc"] = "sys", ["type"] = "process.exit", ["proc"] = tr.Id, ["pid"] = tr.Pid, ["process"] = tr.Process, ["win"] = tr.LastWin,
                    ["exitCode"] = code == 0xFFFFFFFF ? null : code, ["exitCodeHex"] = code == 0xFFFFFFFF ? null : ExitCodes.Hex(code), ["class"] = cls
                };
                if (tr.HungWin != null) ev["wasHung"] = true;
                _emit(ev.Clean());
                if (cls is "crash" or "error") Interlocked.Exchange(ref _eventLogDueTick, Environment.TickCount64 + 2500);
                continue;
            }
            // Hänger: ein sichtbares Fenster des Programms reagiert nicht (Windows-Definition: etwa 5 s keine Nachrichten)
            if (!recording) continue;
            (nint hwnd, string id)? hung = null;
            lock (_lock) foreach (var w in tr.Windows) if (Win32.IsWindow(w.hwnd) && Win32.IsWindowVisible(w.hwnd) && IsHungAppWindow(w.hwnd)) { hung = w; break; }
            if (hung is { } hw && tr.HungWin == null)
            {
                tr.HungWin = hw.id; tr.HungSince = _now();
                _emit(new JsonObject { ["t"] = tr.HungSince, ["tSrc"] = "obs", ["type"] = "window.hang", ["phase"] = "start", ["win"] = hw.id, ["proc"] = tr.Id, ["process"] = tr.Process });
            }
            else if (hung == null && tr.HungWin != null)
            {
                long t = _now();
                _emit(new JsonObject { ["t"] = t, ["tSrc"] = "obs", ["type"] = "window.hang", ["phase"] = "end", ["win"] = tr.HungWin, ["proc"] = tr.Id, ["process"] = tr.Process, ["durationMs"] = (t - tr.HungSince) / 1000 });
                tr.HungWin = null;
            }
        }
        long due = Interlocked.Read(ref _eventLogDueTick);
        if (due > 0 && Environment.TickCount64 >= due && Interlocked.CompareExchange(ref _eventLogDueTick, 0, due) == due) _jobs.TryAdd(QueryEventLog);
    }

    /// <summary>Beim Stopp: Ereignisanzeige ein letztes Mal abfragen und offene Hänger schließen (synchron, mit Frist).</summary>
    public void Final(int timeoutMs = 1500)
    {
        if (_disposed) return;
        List<Tracked> hung;
        lock (_lock) hung = _byPid.Values.Where(x => x.HungWin != null).ToList();
        foreach (var tr in hung)
        {
            long t = _now();
            _emit(new JsonObject { ["t"] = t, ["tSrc"] = "obs", ["type"] = "window.hang", ["phase"] = "end", ["win"] = tr.HungWin, ["proc"] = tr.Id, ["process"] = tr.Process, ["durationMs"] = (t - tr.HungSince) / 1000, ["untilStop"] = true });
            tr.HungWin = null;
        }
        var done = new ManualResetEventSlim();
        if (_jobs.TryAdd(() => { try { QueryEventLog(); } finally { done.Set(); } })) done.Wait(timeoutMs);
    }

    void Work()
    {
        try { foreach (var job in _jobs.GetConsumingEnumerable()) { try { job(); } catch { } } }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    /// <summary><c>process.info</c>: Programmname, Startzeit, Build-Kennung (auf dem Arbeiter).</summary>
    void Describe(Tracked tr, long t)
    {
        var o = new JsonObject { ["t"] = t, ["type"] = "process.info", ["proc"] = tr.Id, ["pid"] = tr.Pid, ["process"] = tr.Process, ["win"] = tr.Windows.FirstOrDefault().id };
        if (tr.Handle != 0 && GetProcessTimes(tr.Handle, out long createFt, out _, out _, out _) && createFt > 0)
            o["startedBeforeSession"] = DateTime.FromFileTimeUtc(createFt) < _startUtc;
        if (tr.Handle == 0) o["handle"] = "denied";   // z. B. Programm eines anderen Benutzers mit Adminrechten: Ende nicht beobachtbar
        if (Win32.GetProcessPath(tr.Pid) is { } path)
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(vi.ProductName)) o["product"] = vi.ProductName.Trim();
                if (!string.IsNullOrWhiteSpace(vi.ProductVersion)) o["version"] = vi.ProductVersion.Trim();
                else if (!string.IsNullOrWhiteSpace(vi.FileVersion)) o["version"] = vi.FileVersion.Trim();
            }
            catch { }
            if (_fileContext)
            {
                var build = new JsonObject();
                try { var fi = new FileInfo(path); build["fileTimeUtc"] = fi.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"); build["size"] = fi.Length; } catch { }
                if (PeInfo.Read(path) is { } pe) foreach (var kv in pe.ToList()) { pe.Remove(kv.Key); build[kv.Key] = kv.Value; }
                if (build.Count > 0) o["build"] = build;
                if (GitInfo.Read(path) is { } git) o["git"] = git;
            }
        }
        tr.Ready = true;
        _emit(o.Clean());
    }

    /// <summary>Anwendungsprotokoll im Zeitraum der Aufnahme: Fakten zu aufgenommenen Programmen (ohne Freitext).</summary>
    void QueryEventLog()
    {
        Dictionary<string, List<Tracked>> byName;
        lock (_lock) byName = _byPid.Values.GroupBy(x => x.Process, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        if (byName.Count == 0) return;
        var sw = Stopwatch.StartNew();
        try
        {
            string since = _startUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            string xpath = $"*[System[(EventID=1000 or EventID=1002 or EventID=1026) and TimeCreated[@SystemTime>='{since}']]]";
            var q = new System.Diagnostics.Eventing.Reader.EventLogQuery("Application", System.Diagnostics.Eventing.Reader.PathType.LogName, xpath) { ReverseDirection = false };
            using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(q);
            int n = 0;
            for (var rec = reader.ReadEvent(); rec != null && n < 20 && sw.ElapsedMilliseconds < 800; rec = reader.ReadEvent())
            {
                using (rec)
                {
                    if (rec.RecordId is not long rid || !_seenRecords.Add(rid)) continue;
                    var props = rec.Properties.Select(p => p.Value).ToList();
                    var facts = EventLogFacts.FromProperties(rec.Id, props);
                    if (facts?.Str("app") is not { } app || !byName.TryGetValue(app, out var cands)) continue;
                    var tr = facts.LongN("pid") is long pid ? cands.FirstOrDefault(c => c.Pid == pid) : cands.Count == 1 ? cands[0] : null;
                    if (tr == null) continue;
                    facts.Remove("pid");
                    long t = _now();
                    if (rec.TimeCreated is DateTime created)
                    {
                        long ago = (long)((DateTime.UtcNow - created.ToUniversalTime()).TotalMilliseconds * 1000);
                        if (ago > 0) t = Math.Max(0, t - ago);
                    }
                    _emit(new JsonObject { ["t"] = t, ["tSrc"] = "sys", ["type"] = "eventlog.entry", ["proc"] = tr.Id, ["process"] = tr.Process, ["win"] = tr.LastWin, ["eventId"] = rec.Id, ["recordId"] = rid, ["provider"] = rec.ProviderName, ["facts"] = facts }.Clean());
                    n++;
                }
            }
        }
        catch { }   // Protokoll nicht lesbar: keine Fakten, das Programmende steht trotzdem in der Aufnahme
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _jobs.CompleteAdding();
        _worker.Join(1000);
        lock (_lock) { foreach (var tr in _byPid.Values) if (tr.Handle != 0) Win32.CloseHandle(tr.Handle); _byPid.Clear(); }
    }

    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(nint h, uint ms);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(nint h, out uint code);
    [DllImport("kernel32.dll")] static extern bool GetProcessTimes(nint h, out long creation, out long exit, out long kernel, out long user);
    [DllImport("user32.dll")] static extern bool IsHungAppWindow(nint hwnd);
}
