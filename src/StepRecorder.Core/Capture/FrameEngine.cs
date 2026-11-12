using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Native;

namespace StepRecorder.Capture;

/// <summary>Maske im Bild (monitorrelativ).</summary>
public readonly record struct FrameMask(Rectangle Rect, string Why);

/// <summary>
/// Bildaufnahme per GDI mit Ringpuffer je Monitor: Vorher-Bild aus dem Ring (vor der Eingabe fertig
/// aufgenommen), Nachher-Bild nach Beruhigung, Zwischenbilder bei wesentlicher Änderung. Speicherung als
/// Schlüssel-, Differenz- oder Verweisbild (04 §Frames); Blob wird vor dem Ereignis geschrieben.
/// </summary>
public sealed class FrameEngine : IDisposable
{
    sealed class RingFrame { public Bitmap Bmp = null!; public long TStart, TEnd; }
    sealed class MonState
    {
        public string Id = "";
        public Rectangle Bounds;
        public readonly List<RingFrame> Ring = new();
        public long LastChangeT;
        public long ChangeSinceStoredT = -1;
        public long LastChangeFrameT;
        public long LastActivityT;
    }
    sealed class Job
    {
        public string Id = ""; public string M = ""; public Bitmap Bmp = null!; public long T, TObs; public string Trigger = "";
        public long? Ref; public string? Guarantee; public JsonObject? Settle; public int Epoch; public bool ForceKey;
        public List<FrameMask> Masks = new(); public List<Rectangle>? ScopeRects; public JsonObject? Cursor; public string? NoneReason;
    }

    readonly RecordingOptions _opt;
    readonly SessionWriter _writer;
    readonly Func<long> _now;
    readonly Func<long, long> _qpcToT;
    readonly object _lock = new();
    readonly Dictionary<string, MonState> _mons = new();
    readonly BlockingCollection<Job> _encode = new(256);
    readonly Thread _capThread, _encThread;
    volatile bool _stop, _paused;
    int _nextFrame = 1;
    Topology _topo;
    /// <summary>Masken je Monitor (Ausschlüsse, Passwortfelder, Aufnahmebereich) zum aktuellen Zeitpunkt.</summary>
    public Func<string, Rectangle, (List<FrameMask> masks, List<Rectangle>? scopeRects)>? MaskProvider;
    /// <summary>Monitore, die zu einem Auslöser gehören (Aufnahmebereich, „Monitor mit Aktivität“).</summary>
    public Func<string?, IEnumerable<string>>? MonitorsForTrigger;

    // Nachher-Bild
    long? _settleRef; long _settleInputT; string? _settleMonitor; bool _settleAll;
    // Statistik
    public int Requested, Stored, Same, Dropped, PreGuaranteed, PreNotGuaranteed, KeyFrames;
    public string LastFrameId { get; private set; } = "";
    readonly Dictionary<string, string> _lastFrameByMonitor = new();
    readonly Dictionary<string, (Bitmap? bmp, string id, int chain, int epoch)> _ref = new();

    // Bildschirmaufnahme: DXGI Desktop Duplication (schnell, ganze Komposition), bei jedem Fehler GDI
    DxgiGrabber? _dxgi;
    public string Backend { get; private set; } = "gdi";
    public int DxgiFrames, GdiFrames;

    static bool UseDxgi()
    {
        var env = Environment.GetEnvironmentVariable("STEPRECORDER_CAPTURE");
        if (env is "gdi" or "dxgi") return env == "dxgi";
        try { return Settings.AppSettings.Load().CaptureBackend != "gdi"; } catch { return true; }
    }

    /// <summary>DXGI, sonst GDI. Null, wenn beides scheitert (z. B. sicherer Desktop) – nie ein schwarzes Ersatzbild.</summary>
    Bitmap? Grab(Rectangle bounds)
    {
        if (_dxgi != null)
        {
            Bitmap? b = null;
            try { b = _dxgi.Capture(bounds); } catch { }
            if (b != null) { Interlocked.Increment(ref DxgiFrames); return b; }
        }
        var g = ScreenGrabber.TryCapture(bounds, out _);
        if (g != null) Interlocked.Increment(ref GdiFrames);
        return g;
    }

    public FrameEngine(RecordingOptions opt, SessionWriter writer, Topology topo, Func<long> now, Func<long, long> qpcToT)
    {
        _opt = opt; _writer = writer; _topo = topo; _now = now; _qpcToT = qpcToT;
        SetTopology(topo);
        if (UseDxgi()) { try { _dxgi = DxgiGrabber.TryCreate(); } catch { _dxgi = null; } }
        Backend = _dxgi != null ? "dxgi" : "gdi";
        _capThread = new Thread(CaptureLoop) { IsBackground = true, Name = "Frames.Capture", Priority = ThreadPriority.AboveNormal };
        _encThread = new Thread(EncodeLoop) { IsBackground = true, Name = "Frames.Encode" };
    }

    public void Start() { _capThread.Start(); _encThread.Start(); }

    public void SetTopology(Topology topo)
    {
        lock (_lock)
        {
            _topo = topo;
            foreach (var m in _mons.Values) foreach (var r in m.Ring) r.Bmp.Dispose();
            _mons.Clear();
            foreach (var mon in topo.Monitors)
                _mons[topo.IdOf(mon)] = new MonState { Id = topo.IdOf(mon), Bounds = mon.Bounds };
        }
    }

    public bool Paused { get => _paused; set => _paused = value; }

    volatile bool _suspended;
    /// <summary>
    /// Bildschutz bei sicherem Desktop (UAC), Sperre oder Ruhezustand: keine Aufnahmen, Ringpuffer verwerfen (er könnte
    /// schwarze oder halbe Bilder enthalten), ein offenes Nachher-Bild entfällt. Danach erzwingt der Recorder ein Schlüsselbild.
    /// </summary>
    public void Suspend()
    {
        lock (_lock)
        {
            _suspended = true;
            foreach (var m in _mons.Values) { foreach (var r in m.Ring) r.Bmp.Dispose(); m.Ring.Clear(); m.ChangeSinceStoredT = -1; }
            _settleRef = null;
        }
    }

    public void ResumeAfterSuspend() { lock (_lock) _suspended = false; }
    public bool Suspended => _suspended;

    string NextId() => "f" + (_nextFrame++).ToString("D6");

    public void MarkActivity(string m, long t) { lock (_lock) if (_mons.TryGetValue(m, out var s)) s.LastActivityT = t; }

    // ---------------- Aufnahmeschleife ----------------

    void CaptureLoop()
    {
        Win32.SetThreadDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        while (!_stop)
        {
            var cycleStart = Environment.TickCount64;
            if (!_paused && !_suspended)
            {
                List<MonState> mons;
                lock (_lock) mons = _mons.Values.ToList();
                foreach (var ms in mons) CaptureInto(ms);
                CheckSettleAndChange();
            }
            int interval = _mons.Values.Any(m => m.Bounds.Width * m.Bounds.Height > 5_000_000) ? 160 : 110;
            int sleep = interval - (int)(Environment.TickCount64 - cycleStart);
            Thread.Sleep(Math.Max(10, sleep));
        }
    }

    void CaptureInto(MonState ms)
    {
        long q0 = System.Diagnostics.Stopwatch.GetTimestamp();
        Bitmap? bmp;
        // fehlgeschlagene Aufnahme (z. B. sicherer Desktop) liefert kein Bild – nie ein schwarzes in den Ringpuffer
        try { bmp = Grab(ms.Bounds); }
        catch { return; }
        if (bmp == null) return;
        long q1 = System.Diagnostics.Stopwatch.GetTimestamp();
        var rf = new RingFrame { Bmp = bmp, TStart = _qpcToT(q0), TEnd = _qpcToT(q1) };
        lock (_lock)
        {
            if (ms.Ring.Count > 0)
            {
                var prev = ms.Ring[^1];
                double ratio = ScreenGrabber.ChangedRatio(prev.Bmp, bmp);
                if (ratio > 0)
                {
                    ms.LastChangeT = rf.TStart;
                    if (ratio >= ChangeThreshold && ms.ChangeSinceStoredT < 0) ms.ChangeSinceStoredT = rf.TStart;
                }
            }
            ms.Ring.Add(rf);
            while (ms.Ring.Count > 3) { ms.Ring[0].Bmp.Dispose(); ms.Ring.RemoveAt(0); }
        }
    }

    double ChangeThreshold => DetailPresets.ChangeRule(_opt.ChangeFrames).ratio;
    int ChangeMinGapMs => DetailPresets.ChangeRule(_opt.ChangeFrames).gapMs;

    void CheckSettleAndChange()
    {
        long now = _now();
        lock (_lock)
        {
            if (_settleRef != null)
            {
                var target = _settleAll ? _mons.Values.ToList() : _mons.Values.Where(m => m.Id == _settleMonitor).ToList();
                long lastChange = target.Count == 0 ? 0 : target.Max(m => m.LastChangeT);
                long newest = target.Count == 0 ? 0 : target.Min(m => m.Ring.Count > 0 ? m.Ring[^1].TStart : 0);
                long quietUs = now - Math.Max(lastChange, _settleInputT);
                if (newest > _settleInputT && quietUs >= _opt.SettleQuietMs * 1000L)
                    EmitSettleLocked("quiet", (int)(quietUs / 1000), target);
                else if (now - _settleInputT >= _opt.SettleMaxMs * 1000L)
                    EmitSettleLocked("timeout", (int)Math.Max(0, quietUs / 1000), target);
                return;
            }
            // Zwischenbilder bei wesentlicher Änderung ohne Eingabe
            foreach (var ms in _mons.Values)
            {
                if (ms.ChangeSinceStoredT < 0 || ms.Ring.Count == 0) continue;
                bool quiet = now - ms.LastChangeT >= _opt.SettleQuietMs * 1000L;
                bool continuous = now - ms.ChangeSinceStoredT >= 5_000_000;
                if ((quiet || continuous) && now - ms.LastChangeFrameT >= ChangeMinGapMs * 1000L)
                {
                    if (!InTriggerSet(ms.Id, null)) { ms.ChangeSinceStoredT = -1; continue; }
                    var rf = ms.Ring[^1];
                    EnqueueLocked(ms, Copy(rf.Bmp), rf.TStart, rf.TEnd, "change", null, null, null);
                    ms.ChangeSinceStoredT = -1;
                    ms.LastChangeFrameT = now;
                }
            }
        }
    }

    bool InTriggerSet(string monId, string? inputMon)
        => MonitorsForTrigger == null || MonitorsForTrigger(inputMon).Contains(monId);

    void EmitSettleLocked(string reason, int quietMs, List<MonState> target)
    {
        long? reference = _settleRef;
        _settleRef = null;
        foreach (var ms in target)
        {
            if (ms.Ring.Count == 0) continue;
            var rf = ms.Ring[^1];
            EnqueueLocked(ms, Copy(rf.Bmp), rf.TStart, rf.TEnd, "settle", reference, null,
                new JsonObject { ["reason"] = reason, ["quietMs"] = quietMs });
            ms.ChangeSinceStoredT = -1;
        }
    }

    // ---------------- Auslöser ----------------

    /// <summary>Vorher-Bild zu einer Eingabe (Zeitpunkt <paramref name="tInput"/> aus dem Hook).</summary>
    public void PreInput(long tInput, long refSeq, string? inputMonitor)
    {
        lock (_lock)
        {
            var monIds = MonitorsForTrigger?.Invoke(inputMonitor).ToList() ?? _mons.Keys.ToList();
            // Ein noch offenes Nachher-Bild endet spätestens mit dem nächsten Vorher-Bild
            if (_settleRef != null)
            {
                var target = _settleAll ? _mons.Values.ToList() : _mons.Values.Where(m => m.Id == _settleMonitor).ToList();
                var refSave = _settleRef; _settleRef = null;
                foreach (var ms in target)
                {
                    var cand = Candidate(ms, tInput);
                    if (cand == null) continue;
                    EnqueueLocked(ms, Copy(cand.Bmp), cand.TStart, cand.TEnd, "settle", refSave, null,
                        new JsonObject { ["reason"] = "nextInput", ["quietMs"] = (int)Math.Max(0, (tInput - ms.LastChangeT) / 1000) });
                }
            }
            foreach (var id in monIds)
            {
                if (!_mons.TryGetValue(id, out var ms)) continue;
                Requested++;
                var cand = Candidate(ms, tInput);
                if (cand != null && tInput - cand.TEnd <= 1_000_000)
                {
                    PreGuaranteed++;
                    EnqueueLocked(ms, Copy(cand.Bmp), cand.TStart, cand.TEnd, "preInput", refSeq, "beforeInput", null);
                }
                else if (_suspended) { PreNotGuaranteed++; EnqueueNoneLocked(ms, tInput, "preInput", refSeq, "secureDesktop"); }
                else
                {
                    PreNotGuaranteed++;
                    long q0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    Bitmap? bmp = null;
                    try { bmp = Grab(ms.Bounds); } catch { }
                    if (bmp == null) { EnqueueNoneLocked(ms, tInput, "preInput", refSeq, "captureFailed"); continue; }
                    long t0 = _qpcToT(q0);
                    EnqueueLocked(ms, bmp, t0, _now(), "preInput", refSeq, "atInput", null);
                }
            }
        }
    }

    RingFrame? Candidate(MonState ms, long tInput)
    {
        for (int i = ms.Ring.Count - 1; i >= 0; i--)
            if (ms.Ring[i].TEnd < tInput) return ms.Ring[i];
        return null;
    }

    /// <summary>Nach einer Eingabe auf Beruhigung warten (Nachher-Bild).</summary>
    public void AwaitSettle(long tInput, long refSeq, string? monitor)
    {
        lock (_lock)
        {
            _settleRef = refSeq; _settleInputT = tInput;
            _settleMonitor = monitor;
            _settleAll = _opt.Scope != ScopeMode.Screen || _opt.Monitors == "all" || monitor == null;
        }
    }

    /// <summary>Sofortbild aller Monitore des Aufnahmebereichs (Start, Stopp, Fortsetzen, Marker, Kommentar).</summary>
    public List<string> Immediate(string trigger, long? refSeq, bool forceKey = false, string? inputMonitor = null)
    {
        var ids = new List<string>();
        lock (_lock)
        {
            var monIds = trigger is "sessionStart" or "sessionStop" or "resume" or "topology" or "scope"
                ? AllScopeMonitors() : (MonitorsForTrigger?.Invoke(inputMonitor).ToList() ?? _mons.Keys.ToList());
            foreach (var id in monIds)
            {
                if (!_mons.TryGetValue(id, out var ms)) continue;
                long q0 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (_suspended) { ids.Add(EnqueueNoneLocked(ms, _qpcToT(q0), trigger, refSeq, "secureDesktop")); continue; }
                Bitmap? bmp = null;
                try { bmp = Grab(ms.Bounds); } catch { }
                if (bmp == null) { ids.Add(EnqueueNoneLocked(ms, _qpcToT(q0), trigger, refSeq, "captureFailed")); continue; }
                ids.Add(EnqueueLocked(ms, bmp, _qpcToT(q0), _now(), trigger, refSeq, null, null, forceKey));
            }
        }
        return ids;
    }

    List<string> AllScopeMonitors()
        => MonitorsForTrigger != null ? MonitorsForTrigger("*").ToList() : _mons.Keys.ToList();

    /// <summary>Letztes Ringbild eines Monitors als Kopie (für eingefrorene Bilder bei Kommentar/Marker).</summary>
    public string? FreezeLatest(string trigger, long? refSeq, string? monitor)
    {
        lock (_lock)
        {
            var ms = monitor != null && _mons.TryGetValue(monitor, out var m) ? m : _mons.Values.FirstOrDefault();
            if (ms == null || ms.Ring.Count == 0) return null;
            var rf = ms.Ring[^1];
            return EnqueueLocked(ms, Copy(rf.Bmp), rf.TStart, rf.TEnd, trigger, refSeq, null, null);
        }
    }

    string EnqueueLocked(MonState ms, Bitmap bmp, long t, long tObs, string trigger, long? refSeq, string? guarantee, JsonObject? settle, bool forceKey = false)
    {
        var job = new Job
        {
            Id = NextId(), M = ms.Id, Bmp = bmp, T = t, TObs = tObs, Trigger = trigger, Ref = refSeq, Guarantee = guarantee,
            Settle = settle, Epoch = _topo.Epoch, ForceKey = forceKey
        };
        if (MaskProvider != null)
        {
            try { var (masks, scope) = MaskProvider(ms.Id, ms.Bounds); job.Masks = masks; job.ScopeRects = scope; } catch { }
        }
        if (Win32.GetCursorPos(out var cp) && ms.Bounds.Contains(cp.X, cp.Y))
            job.Cursor = new JsonObject { ["x"] = cp.X - ms.Bounds.X, ["y"] = cp.Y - ms.Bounds.Y, ["visible"] = true };
        LastFrameId = job.Id;
        _lastFrameByMonitor[ms.Id] = job.Id;
        if (!_encode.TryAdd(job, 2000)) { Dropped++; bmp.Dispose(); }
        return job.Id;
    }

    string EnqueueNoneLocked(MonState ms, long t, string trigger, long? refSeq, string reason)
    {
        var job = new Job { Id = NextId(), M = ms.Id, T = t, TObs = t, Trigger = trigger, Ref = refSeq, Epoch = _topo.Epoch, NoneReason = reason, Guarantee = trigger == "preInput" ? "none" : null };
        Dropped++;
        _encode.TryAdd(job, 2000);
        return job.Id;
    }

    public string? LastFrameOf(string m) { lock (_lock) return _lastFrameByMonitor.TryGetValue(m, out var id) ? id : null; }

    // ---------------- Kodierung ----------------

    void EncodeLoop()
    {
        foreach (var job in _encode.GetConsumingEnumerable())
        {
            try { Encode(job); }
            catch (Exception) { Dropped++; }
            finally { job.Bmp?.Dispose(); }
        }
    }

    void Encode(Job job)
    {
        var ev = new JsonObject
        {
            ["t"] = job.T, ["type"] = "frame", ["tObs"] = job.TObs, ["tSrc"] = "obs", ["src"] = "gdi", ["id"] = job.Id, ["ep"] = job.Epoch, ["m"] = job.M,
        };
        if (job.NoneReason != null)
        {
            var mon = _topo.ById(job.M);
            ev["w"] = mon?.Bounds.Width ?? 0; ev["h"] = mon?.Bounds.Height ?? 0;
            ev["trigger"] = job.Trigger;
            if (job.Ref != null) ev["ref"] = job.Ref;
            if (job.Guarantee != null) ev["guarantee"] = job.Guarantee;
            ev["store"] = new JsonObject { ["kind"] = "none", ["reason"] = job.NoneReason };
            _writer.Append(ev);
            _writer.Append(new JsonObject { ["t"] = job.T, ["type"] = "gap", ["gapId"] = GapIds.Next(), ["phase"] = "single", ["cause"] = "frameDropped", ["tFrom"] = job.T, ["tTo"] = job.T, ["channels"] = new JsonArray("frames"), ["inputLost"] = "none", ["detail"] = new JsonObject { ["frame"] = job.Id } });
            return;
        }
        var bmp = job.Bmp;
        ev["w"] = bmp.Width; ev["h"] = bmp.Height;
        ev["trigger"] = job.Trigger;
        if (job.Ref != null) ev["ref"] = job.Ref;
        if (job.Guarantee != null) ev["guarantee"] = job.Guarantee;
        if (job.Settle != null) ev["settle"] = job.Settle;

        // Masken vor dem Vergleich anwenden (schwarz)
        var maskArr = new JsonArray();
        using (var g = Graphics.FromImage(bmp))
        {
            if (job.ScopeRects != null)
            {
                using var outside = new Region(new Rectangle(0, 0, bmp.Width, bmp.Height));
                foreach (var r in job.ScopeRects) outside.Exclude(r);
                g.FillRegion(Brushes.Black, outside);
            }
            foreach (var mk in job.Masks)
            {
                var r = Rectangle.Intersect(mk.Rect, new Rectangle(0, 0, bmp.Width, bmp.Height));
                if (r.Width <= 0 || r.Height <= 0) continue;
                g.FillRectangle(Brushes.Black, r);
                maskArr.Add(new JsonObject { ["x"] = r.X, ["y"] = r.Y, ["w"] = r.Width, ["h"] = r.Height, ["why"] = mk.Why });
            }
        }
        if (job.ScopeRects != null)
            ev["scopeRects"] = new JsonArray(job.ScopeRects.Select(r => (JsonNode)new JsonArray(r.X, r.Y, r.Width, r.Height)).ToArray());
        if (maskArr.Count > 0) ev["masks"] = maskArr;
        if (job.Cursor != null) ev["cursor"] = job.Cursor;

        _ref.TryGetValue(job.M, out var reference);
        bool sizeOk = reference.bmp != null && reference.bmp.Width == bmp.Width && reference.bmp.Height == bmp.Height && reference.epoch == job.Epoch;
        Rectangle? diff = sizeOk ? ScreenGrabber.DiffBounds(reference.bmp!, bmp) : new Rectangle(0, 0, bmp.Width, bmp.Height);
        double ratio = diff == null ? 0 : (double)diff.Value.Width * diff.Value.Height / ((double)bmp.Width * bmp.Height);

        if (sizeOk && diff == null && !job.ForceKey)
        {
            ev["store"] = new JsonObject { ["kind"] = "same", ["as"] = reference.id };
            Same++;
            _writer.FramesWritten++;
            _writer.Append(ev);
            return;
        }
        bool key = !sizeOk || job.ForceKey || ratio >= _opt.KeyframeChangeRatio || reference.chain + 1 > _opt.MaxDeltaChain;
        byte[] png;
        JsonObject store;
        if (key)
        {
            png = EncodeRgb(bmp);
            store = new JsonObject { ["kind"] = "key", ["blob"] = _writer.WriteBlob(png), ["bytes"] = png.Length };
            KeyFrames++; _writer.KeyFrames++;
        }
        else
        {
            var d = diff!.Value;
            png = EncodeDelta(reference.bmp!, bmp, d);
            store = new JsonObject
            {
                ["kind"] = "delta", ["base"] = reference.id,
                ["rect"] = new JsonObject { ["x"] = d.X, ["y"] = d.Y, ["w"] = d.Width, ["h"] = d.Height },
                ["blob"] = _writer.WriteBlob(png), ["bytes"] = png.Length
            };
        }
        ev["store"] = store;
        if (diff != null)
            ev["changed"] = new JsonObject { ["ratio"] = Math.Round(ratio, 4), ["rects"] = new JsonArray(new JsonArray(diff.Value.X, diff.Value.Y, diff.Value.Width, diff.Value.Height)) };
        Stored++;
        _writer.FramesWritten++;
        _writer.Append(ev);
        reference.bmp?.Dispose();
        _ref[job.M] = (Copy(bmp), job.Id, key ? 0 : reference.chain + 1, job.Epoch);
    }

    static Bitmap Copy(Bitmap b) => b.Clone(new Rectangle(0, 0, b.Width, b.Height), PixelFormat.Format32bppRgb);

    static byte[] EncodeRgb(Bitmap bmp)
    {
        using var rgb = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(rgb)) g.DrawImageUnscaled(bmp, 0, 0);
        using var ms = new MemoryStream();
        rgb.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    static unsafe byte[] EncodeDelta(Bitmap baseBmp, Bitmap cur, Rectangle rect)
    {
        using var patch = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        var full = new Rectangle(0, 0, cur.Width, cur.Height);
        var da = baseBmp.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var db = cur.LockBits(full, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var dp = patch.LockBits(new Rectangle(0, 0, rect.Width, rect.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < rect.Height; y++)
            {
                uint* pa = (uint*)((byte*)da.Scan0 + (y + rect.Y) * da.Stride) + rect.X;
                uint* pb = (uint*)((byte*)db.Scan0 + (y + rect.Y) * db.Stride) + rect.X;
                uint* pp = (uint*)((byte*)dp.Scan0 + y * dp.Stride);
                for (int x = 0; x < rect.Width; x++)
                {
                    uint a = pa[x] & 0x00FFFFFF, b = pb[x] & 0x00FFFFFF;
                    pp[x] = a == b ? 0u : (b | 0xFF000000u);
                }
            }
        }
        finally { baseBmp.UnlockBits(da); cur.UnlockBits(db); patch.UnlockBits(dp); }
        using var ms = new MemoryStream();
        patch.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>Beendet die Aufnahme und wartet, bis alle Bilder geschrieben sind.</summary>
    public void StopAndDrain()
    {
        _stop = true;
        _capThread.Join(2000);
        _encode.CompleteAdding();
        _encThread.Join(60000);
    }

    public void Dispose()
    {
        _stop = true;
        lock (_lock) foreach (var m in _mons.Values) foreach (var r in m.Ring) r.Bmp.Dispose();
        foreach (var r in _ref.Values) r.bmp?.Dispose();
        try { _dxgi?.Dispose(); } catch { }
        _dxgi = null;
    }
}

/// <summary>Fortlaufende Lücken-Kennungen (g1, g2 …) für eine Aufnahme.</summary>
public static class GapIds
{
    static int _n;
    public static void Reset(int start = 0) => Interlocked.Exchange(ref _n, start);
    public static string Next() => "g" + Interlocked.Increment(ref _n);
}
