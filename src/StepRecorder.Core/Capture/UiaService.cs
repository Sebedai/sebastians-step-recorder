using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using StepRecorder.Data;

namespace StepRecorder.Capture;

/// <summary>Ergebnis einer Elementermittlung.</summary>
public sealed class UiaResult
{
    public string Status = "ok";       // ok | timeout | notAvailable | accessDenied | skipped | error
    public string Src = "uia";
    public long TStart, TEnd;
    public int Ms;
    public JsonObject? El;
    public JsonArray? Path;
    public string Ctx = "unknown";      // textField | passwordField | nonText | opaque | unknown
    public bool IsPassword;
    public string? Value;
    public int ValueLen;
    public AutomationElement? Element;
    public nint TopHwnd;
    public Rectangle? ScreenRect;
    /// <summary>Win32-Kontext (Fenster, ClassNN, Koordinaten) für AutoHotkey, siehe <see cref="Win32Detail"/>.</summary>
    public JsonObject? Win32;
    /// <summary>Terminal (10 §6.1): letzte nicht leere Zeilen – nur im Arbeitsspeicher, nie ins Protokoll.</summary>
    public string[]? Lines;
    /// <summary>Terminal: Zeile mit der Schreibmarke ist leer (<c>null</c>: Schreibmarke nicht ermittelbar).</summary>
    public bool? CursorBlank;
}

/// <summary>
/// UI Automation nur lesend, auf eigenen MTA-Threads ohne Fenster. Ein Arbeiter bearbeitet Anfragen
/// nacheinander; hängt ein Aufruf länger als 1,5 s, übernimmt ein Ersatzarbeiter (höchstens 4).
/// </summary>
public sealed class UiaService : IDisposable
{
    sealed record Job(Func<UiaResult> Work, Action<UiaResult> Done, bool Droppable, long TStart);

    readonly BlockingCollection<Job> _queue = new(new ConcurrentQueue<Job>(), 512);
    readonly Func<long> _now;
    readonly Func<Rectangle, JsonObject> _rectJson;
    readonly int _valueMax;
    Thread? _worker;
    volatile Job? _current;
    long _currentStarted;
    int _hungWorkers;
    volatile bool _disposed;
    public int Queries, Timeouts, WorkersReplaced;
    Job? _pendingDwell;
    readonly object _dwellLock = new();

    public UiaService(Func<long> now, Func<Rectangle, JsonObject> rectJson, int valueMax)
    {
        _now = now; _rectJson = rectJson; _valueMax = valueMax;
        StartWorker();
        var wd = new Thread(Watchdog) { IsBackground = true, Name = "UIA.Watchdog" };
        wd.Start();
    }

    void StartWorker()
    {
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "UIA.Worker" };
        _worker.SetApartmentState(ApartmentState.MTA);
        _worker.Start();
    }

    void WorkerLoop()
    {
        var me = Thread.CurrentThread;
        while (!_disposed && me == _worker)
        {
            Job? job = null;
            lock (_dwellLock) { if (_pendingDwell != null) { job = _pendingDwell; _pendingDwell = null; } }
            if (job == null && !_queue.TryTake(out job, 50)) continue;
            if (me != _worker) { _queue.Add(job); return; }
            _current = job;
            Interlocked.Exchange(ref _currentStarted, Stopwatch.GetTimestamp());
            UiaResult r;
            try { r = job.Work(); }
            catch (ElementNotAvailableException) { r = new UiaResult { Status = "notAvailable", Src = "none" }; }
            catch (UnauthorizedAccessException) { r = new UiaResult { Status = "accessDenied", Src = "none" }; }
            catch (Exception) { r = new UiaResult { Status = "error", Src = "none" }; }
            if (me != _worker) return; // abgelöst – Ergebnis wurde bereits als Zeitüberschreitung gemeldet
            _current = null;
            r.TStart = job.TStart; r.TEnd = _now(); r.Ms = (int)((r.TEnd - r.TStart) / 1000);
            Queries++;
            try { job.Done(r); } catch { }
        }
    }

    void Watchdog()
    {
        while (!_disposed)
        {
            Thread.Sleep(100);
            var job = _current;
            if (job == null) continue;
            double ms = (Stopwatch.GetTimestamp() - Interlocked.Read(ref _currentStarted)) * 1000.0 / Stopwatch.Frequency;
            if (ms < 1500) continue;
            // Arbeiter hängt: Ersatz starten, Anfrage als Zeitüberschreitung abschließen
            _current = null;
            Timeouts++;
            var r = new UiaResult { Status = "timeout", Src = "none", TStart = job.TStart, TEnd = _now() };
            r.Ms = (int)((r.TEnd - r.TStart) / 1000);
            try { job.Done(r); } catch { }
            if (_hungWorkers < 4) { _hungWorkers++; WorkersReplaced++; StartWorker(); }
        }
    }

    /// <summary>Stellt eine Anfrage ein. Verweil-Anfragen sind verwerfbar (nur die neueste zählt).</summary>
    public void Enqueue(Func<UiaResult> work, Action<UiaResult> done, bool droppable = false)
    {
        var job = new Job(work, done, droppable, _now());
        if (droppable) { lock (_dwellLock) _pendingDwell = job; return; }
        if (!_queue.TryAdd(job)) done(new UiaResult { Status = "skipped", Src = "none", TStart = job.TStart, TEnd = job.TStart });
    }

    // ---------- Abfragen ----------

    /// <summary>Führt eine Abfrage aus und bildet Ausnahmen auf einen Status ab (für zusammengesetzte Abfragen).</summary>
    public static UiaResult Safe(Func<UiaResult> f)
    {
        try { return f(); }
        catch (ElementNotAvailableException) { return new UiaResult { Status = "notAvailable", Src = "none" }; }
        catch (UnauthorizedAccessException) { return new UiaResult { Status = "accessDenied", Src = "none" }; }
        catch (Exception) { return new UiaResult { Status = "error", Src = "none" }; }
    }

    /// <summary>
    /// Text-Schnappschuss eines Fensters (03 §7.5; 12, Paket 11): Durchlauf in Lesereihenfolge durch die Steuerelement-Sicht,
    /// nur sichtbare Elemente innerhalb des Fensterrechtecks, höchstens <paramref name="maxNodes"/> Knoten, 12 Ebenen und
    /// <paramref name="budgetMs"/>. Werte (≤ 200 Zeichen) nur mit <paramref name="values"/> und nie aus Kennwortfeldern.
    /// Ergebnis in <see cref="UiaResult.El"/>: <c>nodes</c>, <c>title</c>, <c>incomplete</c>.
    /// </summary>
    public UiaResult Snapshot(nint hwnd, int maxNodes, bool values, int budgetMs = 250, int maxDepth = 12)
    {
        var r = new UiaResult();
        var root = AutomationElement.FromHandle(hwnd);
        if (root == null) { r.Status = "notAvailable"; r.Src = "none"; return r; }
        var sw = Stopwatch.StartNew();
        var bounds = root.Current.BoundingRectangle;
        var nodes = new JsonArray();
        bool incomplete = false;
        var walker = TreeWalker.ControlViewWalker;
        void Visit(AutomationElement e, int depth)
        {
            if (incomplete) return;
            if (nodes.Count >= maxNodes || sw.ElapsedMilliseconds > budgetMs || depth > maxDepth) { incomplete = true; return; }
            AutomationElement.AutomationElementInformation c;
            try { c = e.Current; } catch { return; }
            if (c.IsOffscreen && depth > 0) return;
            var b = c.BoundingRectangle;
            bool inside = !b.IsEmpty && b.Width > 0 && b.Height > 0 && (bounds.IsEmpty || bounds.IntersectsWith(b));
            if (depth > 0 && !inside) return;
            string ct = ControlTypeName(c.ControlType);
            string name = c.Name ?? "";
            string? value = null;
            if (values && !c.IsPassword && ct is "Text")
                try { if (e.TryGetCurrentPattern(ValuePattern.Pattern, out var vp) && vp is ValuePattern v) value = v.Current.Value; } catch { }
            if (depth > 0 && (name.Length > 0 || !string.IsNullOrEmpty(value)) && ct is not ("Image" or "ScrollBar" or "Thumb" or "Separator" or "TitleBar"))
            {
                var n = new JsonObject { ["ct"] = ct };
                if (name.Length > 0) n["name"] = Trunc(name.Replace("\r\n", "\n"), 500);
                if (!string.IsNullOrEmpty(value) && value != name) n["value"] = Trunc(value, 200);
                if (c.IsPassword) n["password"] = true;
                if (!string.IsNullOrEmpty(c.AutomationId) && !c.AutomationId.All(char.IsDigit)) n["aid"] = Trunc(c.AutomationId, 100);
                if (!c.IsEnabled) n["enabled"] = false;
                nodes.Add(n);
            }
            AutomationElement? child;
            try { child = walker.GetFirstChild(e); } catch { return; }
            while (child != null && !incomplete)
            {
                Visit(child, depth + 1);
                try { child = walker.GetNextSibling(child); } catch { break; }
            }
        }
        Visit(root, 0);
        string title = "";
        try { title = root.Current.Name ?? ""; } catch { }
        r.El = new JsonObject { ["title"] = Trunc(title, 500), ["nodes"] = nodes, ["incomplete"] = incomplete ? true : null };
        r.El.Clean();
        return r;
    }

    /// <summary>
    /// Ende des sichtbaren Terminaltexts (10 §6.1) über das TextPattern: bevorzugt die Zeilen bis zur Schreibmarke (leere Auswahl
    /// = Schreibmarke, so bei Windows Terminal und der Konsole), sonst die sichtbaren Zeilen. Gelesen wird nur aus dem Element mit
    /// Fokus (bzw. dem Fenster selbst) desselben Prozesses wie <paramref name="hwnd"/>. Ergebnis in <see cref="UiaResult.Lines"/>
    /// (höchstens drei nicht leere Zeilen) – kein Ereignis, kein Protokoll. Ohne Textzugriff: Status <c>notAvailable</c>.
    /// </summary>
    public UiaResult TerminalTail(nint hwnd, int maxLines = TerminalPrompt.MaxLines)
    {
        var r = new UiaResult();
        var root = AutomationElement.FromHandle(hwnd);
        if (root == null) { r.Status = "notAvailable"; r.Src = "none"; return r; }
        int pid = root.Current.ProcessId;
        AutomationElement? el = null;
        try
        {
            // Fokus im Terminal (Windows Terminal: TermControl) oder ein Vorfahre mit TextPattern
            var f = AutomationElement.FocusedElement;
            for (int i = 0; f != null && i < 6 && el == null; i++)
            {
                var c = f.Current;
                if (c.ProcessId != pid) break;
                if (f.GetCurrentPropertyValue(AutomationElement.IsTextPatternAvailableProperty) is true) { el = f; break; }
                f = TreeWalker.ControlViewWalker.GetParent(f);
                if (f == AutomationElement.RootElement) break;
            }
        }
        catch { }
        if (el == null)
        {
            if (root.GetCurrentPropertyValue(AutomationElement.IsTextPatternAvailableProperty) is true) el = root;
            else el = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true));
        }
        if (el == null || !el.TryGetCurrentPattern(TextPattern.Pattern, out var p) || p is not TextPattern tp) { r.Status = "notAvailable"; r.Src = "none"; return r; }
        string? text = null;
        try
        {
            var sel = tp.GetSelection();
            if (sel is { Length: > 0 })
            {
                var c = sel[0].Clone();
                c.ExpandToEnclosingUnit(System.Windows.Automation.Text.TextUnit.Line);
                c.MoveEndpointByUnit(System.Windows.Automation.Text.TextPatternRangeEndpoint.Start, System.Windows.Automation.Text.TextUnit.Line, -(maxLines + 4));
                text = c.GetText(8000);
                if (text != null) r.CursorBlank = TerminalPrompt.CursorLineBlank(text);
            }
        }
        catch { text = null; r.CursorBlank = null; }
        if (string.IsNullOrEmpty(text))
        {
            try
            {
                var vis = tp.GetVisibleRanges();
                if (vis is { Length: > 0 })
                {
                    var all = vis[0].Clone();
                    all.MoveEndpointByRange(System.Windows.Automation.Text.TextPatternRangeEndpoint.End, vis[^1], System.Windows.Automation.Text.TextPatternRangeEndpoint.End);
                    text = all.GetText(20000);
                }
            }
            catch { }
        }
        r.Lines = TerminalPrompt.TailLines(text, maxLines);
        r.Src = "uia";
        return r;
    }

    public UiaResult AtPoint(int vx, int vy, bool withPath = true)
    {
        var el = AutomationElement.FromPoint(new System.Windows.Point(vx, vy));
        return Describe(el, withPath);
    }

    public UiaResult Focused(bool withPath = true)
    {
        var el = AutomationElement.FocusedElement;
        return Describe(el, withPath);
    }

    public UiaResult Describe(AutomationElement? el, bool withPath)
    {
        var r = new UiaResult();
        if (el == null) { r.Status = "notAvailable"; r.Src = "none"; return r; }
        r.Element = el;
        var c = el.Current;
        var o = new JsonObject();
        try
        {
            var rid = el.GetRuntimeId();
            if (rid != null) o["rid"] = string.Join('.', rid);
        }
        catch { }
        string ct = ControlTypeName(c.ControlType);
        o["ct"] = ct;
        if (!string.IsNullOrEmpty(c.LocalizedControlType)) o["lct"] = c.LocalizedControlType;
        if (!string.IsNullOrEmpty(c.Name)) o["name"] = Trunc(c.Name, 500);
        if (!string.IsNullOrEmpty(c.AutomationId)) o["aid"] = Trunc(c.AutomationId, 200);
        if (!string.IsNullOrEmpty(c.ClassName)) o["cls"] = Trunc(c.ClassName, 200);
        if (!string.IsNullOrEmpty(c.FrameworkId)) o["fw"] = c.FrameworkId;
        if (c.NativeWindowHandle != 0) o["hwnd"] = Json.Hex(c.NativeWindowHandle);
        if (c.ProcessId != 0) o["pid"] = c.ProcessId;
        var b = c.BoundingRectangle;
        if (!b.IsEmpty && b.Width > 0 && b.Height > 0)
        {
            r.ScreenRect = new Rectangle((int)b.X, (int)b.Y, (int)b.Width, (int)b.Height);
            o["rect"] = _rectJson(r.ScreenRect.Value);
        }
        o["enabled"] = c.IsEnabled;
        if (c.IsOffscreen) o["offscreen"] = true;
        if (c.IsKeyboardFocusable) o["focusable"] = true;
        if (c.HasKeyboardFocus) o["focused"] = true;
        r.IsPassword = c.IsPassword;
        if (c.IsPassword) o["password"] = true;
        if (!string.IsNullOrEmpty(c.HelpText)) o["help"] = Trunc(c.HelpText, 300);
        if (!string.IsNullOrEmpty(c.AcceleratorKey)) o["accel"] = c.AcceleratorKey;
        if (!string.IsNullOrEmpty(c.AccessKey)) o["access"] = c.AccessKey;
        if (!string.IsNullOrEmpty(c.ItemStatus)) o["itemStatus"] = Trunc(c.ItemStatus, 200);

        var patterns = new JsonArray();
        object? p;
        if (el.TryGetCurrentPattern(InvokePattern.Pattern, out _)) patterns.Add("Invoke");
        if (el.TryGetCurrentPattern(ValuePattern.Pattern, out p))
        {
            patterns.Add("Value");
            if (!c.IsPassword)
            {
                try
                {
                    var v = ((ValuePattern)p).Current.Value ?? "";
                    r.Value = v.Length > _valueMax ? v[.._valueMax] : v;
                    r.ValueLen = v.Length;
                    o["value"] = r.Value;
                    if (v.Length > _valueMax) o["valueLen"] = v.Length;
                }
                catch { }
            }
            else o["valueWithheld"] = "password";
        }
        if (el.TryGetCurrentPattern(TogglePattern.Pattern, out p))
        {
            patterns.Add("Toggle");
            try { o["toggle"] = ((TogglePattern)p).Current.ToggleState switch { ToggleState.On => "on", ToggleState.Off => "off", _ => "mixed" }; } catch { }
        }
        if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p))
        {
            patterns.Add("SelectionItem");
            try { if (((SelectionItemPattern)p).Current.IsSelected) o["selected"] = true; } catch { }
        }
        if (el.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out p))
        {
            patterns.Add("ExpandCollapse");
            try
            {
                o["expanded"] = ((ExpandCollapsePattern)p).Current.ExpandCollapseState switch
                { ExpandCollapseState.Expanded => "expanded", ExpandCollapseState.Collapsed => "collapsed", ExpandCollapseState.PartiallyExpanded => "partial", _ => "leaf" };
            }
            catch { }
        }
        if (el.TryGetCurrentPattern(TextPattern.Pattern, out _)) patterns.Add("Text");
        if (el.TryGetCurrentPattern(ScrollPattern.Pattern, out _)) patterns.Add("Scroll");
        if (el.TryGetCurrentPattern(RangeValuePattern.Pattern, out p))
        {
            patterns.Add("RangeValue");
            try { var rv = ((RangeValuePattern)p).Current; o["range"] = new JsonObject { ["value"] = rv.Value, ["min"] = rv.Minimum, ["max"] = rv.Maximum }; } catch { }
        }
        if (el.TryGetCurrentPattern(SelectionPattern.Pattern, out _)) patterns.Add("Selection");
        if (el.TryGetCurrentPattern(GridItemPattern.Pattern, out p))
        {
            patterns.Add("GridItem");
            try { var gi = ((GridItemPattern)p).Current; o["cell"] = new JsonObject { ["row"] = gi.Row, ["col"] = gi.Column }; } catch { }
        }
        if (el.TryGetCurrentPattern(WindowPattern.Pattern, out _)) patterns.Add("Window");
        if (patterns.Count > 0) o["patterns"] = patterns;
        try { var lb = c.LabeledBy; if (lb != null && !string.IsNullOrEmpty(lb.Current.Name)) o["labeledBy"] = new JsonObject { ["ct"] = ControlTypeName(lb.Current.ControlType), ["name"] = Trunc(lb.Current.Name, 200) }; } catch { }

        // Terminals in Electron-Anwendungen (VS Code, Teams …): xterm.js nimmt Eingaben über ein leeres Hilfsfeld an – Inhalt nicht lesbar
        bool xterm = string.Equals(c.ClassName, "xterm-helper-textarea", StringComparison.OrdinalIgnoreCase) || (c.ClassName?.Contains("xterm", StringComparison.OrdinalIgnoreCase) ?? false);
        r.Ctx = c.IsPassword ? "passwordField"
            : xterm ? "opaque"
            : ct is "Edit" or "Document" || (ct == "ComboBox" && patterns.Contains("Value")) ? "textField"
            : "nonText";

        if (withPath)
        {
            var path = new JsonArray();
            var sw = Stopwatch.StartNew();
            // Position unter gleichartigen Geschwistern und Eindeutigkeit des Namens (stabile UIA-Wege)
            try
            {
                var w = TreeWalker.ControlViewWalker;
                var parent = w.GetParent(el);
                if (parent != null && parent != AutomationElement.RootElement)
                {
                    int idx = 0, same = 0, sameName = 0, n = 0;
                    var rid = el.GetRuntimeId();
                    for (var ch = w.GetFirstChild(parent); ch != null && n < 300 && sw.ElapsedMilliseconds < 60; ch = w.GetNextSibling(ch), n++)
                    {
                        var cc = ch.Current;
                        if (cc.ControlType != c.ControlType) continue;
                        same++;
                        if (!string.IsNullOrEmpty(c.Name) && cc.Name == c.Name) sameName++;
                        try { if (rid != null && System.Linq.Enumerable.SequenceEqual(ch.GetRuntimeId(), rid)) idx = same; } catch { }
                    }
                    if (idx > 0) { o["idx"] = idx; o["sibCount"] = same; }
                    if (sameName > 1) o["nameDup"] = sameName;
                }
            }
            catch { }
            try
            {
                var walker = TreeWalker.ControlViewWalker;
                var cur = walker.GetParent(el);
                var list = new List<JsonObject>();
                while (cur != null && list.Count < 12 && sw.ElapsedMilliseconds < 150)
                {
                    if (cur == AutomationElement.RootElement) break;
                    var cc = cur.Current;
                    var item = new JsonObject { ["ct"] = ControlTypeName(cc.ControlType) };
                    if (!string.IsNullOrEmpty(cc.Name)) item["name"] = Trunc(cc.Name, 200);
                    if (!string.IsNullOrEmpty(cc.AutomationId)) item["aid"] = Trunc(cc.AutomationId, 100);
                    if (!string.IsNullOrEmpty(cc.ClassName)) item["cls"] = Trunc(cc.ClassName, 100);
                    list.Add(item);
                    cur = walker.GetParent(cur);
                }
                list.Reverse();
                foreach (var i in list) path.Add(i);
            }
            catch { }
            if (path.Count > 0) r.Path = path;
        }
        r.El = o;
        return r;
    }

    public static string ControlTypeName(ControlType? ct)
    {
        if (ct == null) return "Unknown";
        var n = ct.ProgrammaticName; // "ControlType.Button"
        var i = n.LastIndexOf('.');
        return i >= 0 ? n[(i + 1)..] : n;
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];

    public void Dispose()
    {
        _disposed = true;
        _queue.CompleteAdding();
    }
}
