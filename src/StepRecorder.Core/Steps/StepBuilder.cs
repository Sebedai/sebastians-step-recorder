using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Steps;

/// <summary>
/// Schritterkennung (05): reine Funktion f(Journal, Regelversion, Parameter) → steps.json.
/// Liest keine Pixel, keine Uhr (außer derivedAtUtc), keine Einstellungen des Rechners.
/// </summary>
public sealed partial class StepBuilder
{
    readonly Session _s;
    readonly List<JsonObject> _ev;           // zeitlich sortiert
    readonly Dictionary<long, JsonObject> _bySeq = new();
    readonly List<StepDraft> _steps = new();
    readonly HashSet<long> _assigned = new();
    readonly List<JsonObject> _warnings = new();
    readonly JsonObject _params;
    readonly List<long> _inputSeqs = new();  // alle Eingabe-Seqs (ohne self), nach seq sortiert
    readonly HashSet<long> _consumed = new(); // Nicht-Eingaben, die als Wirkung verbraucht wurden
    // Parameter
    readonly int _dblMs, _dblW, _dblH, _dragW, _dragH, _wheelLines, _hoverMinMs;
    const int LongPressMs = 500, ScrollGroupGapMs = 400, RepeatMergeGapMs = 1000, CharRunGapMs = 1000, TabFocusMs = 300, HoverRadiusPx = 8,
        HoverEffectMs = 500, DwellNoteMs = 150, EffectWindowMs = 2000, EffectSettleMaxMs = 3000, EffectUncertainMs = 300, SystemMergeMs = 500,
        AnomalyWindowMs = 2000, WaitMinMs = 3000, ElementMaxAgeMs = 1500, RectTolerancePx = 2, PathMaxPoints = 32, DrawPathMaxPoints = 256, TextPauseNoteMs = 2000, GapStepMinMs = 1000;
    readonly long _duration;
    readonly List<(long from, long to, string cause, string id, JsonArray channels, string inputLost, long seq)> _gapSpans = new();

    public StepBuilder(Session s)
    {
        _s = s;
        _ev = s.Events.OrderBy(e => e.Long("t")).ThenBy(e => e.LongN("inSeq") ?? long.MaxValue).ThenBy(e => e.Long("seq")).ToList();
        foreach (var e in s.Events) _bySeq[e.Long("seq")] = e;
        var input = s.Manifest.Obj("environment").Obj("inputSettings");
        int Get(string k, int def) { var v = input?.LongN(k); if (v == null) _warnings.Add(new JsonObject { ["code"] = "paramDefault", ["message"] = k }); return (int)(v ?? def); }
        _dblMs = Get("doubleClickTimeMs", 500); _dblW = Get("doubleClickWidth", 4); _dblH = Get("doubleClickHeight", 4);
        _dragW = Get("dragWidth", 4); _dragH = Get("dragHeight", 4); _wheelLines = Get("wheelScrollLines", 3);
        _hoverMinMs = Math.Max(500, input?.Int("hoverTimeMs", 400) ?? 400);
        _params = new JsonObject
        {
            ["doubleClickTimeMs"] = _dblMs, ["doubleClickWidth"] = _dblW, ["doubleClickHeight"] = _dblH, ["dragWidth"] = _dragW, ["dragHeight"] = _dragH,
            ["wheelScrollLines"] = _wheelLines, ["longPressMs"] = LongPressMs, ["scrollGroupGapMs"] = ScrollGroupGapMs, ["repeatMergeGapMs"] = RepeatMergeGapMs,
            ["charRunGapMs"] = CharRunGapMs, ["tabFocusMs"] = TabFocusMs, ["hoverMinMs"] = _hoverMinMs, ["hoverRadiusPx"] = HoverRadiusPx, ["hoverEffectMs"] = HoverEffectMs,
            ["dwellNoteMs"] = DwellNoteMs, ["effectWindowMs"] = EffectWindowMs, ["effectSettleMaxMs"] = EffectSettleMaxMs, ["effectUncertainMs"] = EffectUncertainMs,
            ["systemMergeMs"] = SystemMergeMs, ["anomalyWindowMs"] = AnomalyWindowMs, ["waitMinMs"] = WaitMinMs, ["elementMaxAgeMs"] = ElementMaxAgeMs,
            ["rectTolerancePx"] = RectTolerancePx, ["pathMaxPoints"] = PathMaxPoints, ["textPauseNoteMs"] = TextPauseNoteMs, ["gapStepMinMs"] = GapStepMinMs
        };
        _duration = s.DurationUs;
        foreach (var e in s.Events) if (IsInput(e)) _inputSeqs.Add(e.Long("seq"));
        _inputSeqs.Sort();
    }

    public static bool IsInputType(string? t) => t is "mouse.move" or "mouse.down" or "mouse.up" or "mouse.wheel" or "key.down" or "key.up" or "text.masked";
    static bool IsInput(JsonObject e) => IsInputType(e.Str("type")) && !e.Bool("self");
    static bool IsRedactedLine(JsonObject e) => e.Arr("redacted") is { } r && r.Any(x => x?.GetValue<string>() == "*");

    /// <summary>Leitet steps.json ab.</summary>
    public static JsonObject Build(Session s, string? derivedAtUtc = null) => new StepBuilder(s).Run(derivedAtUtc ?? Json.UtcNow());

    JsonObject Run(string derivedAt)
    {
        CollectGaps();
        BuildInputSteps();
        BuildMarkerCommentSteps();
        BuildGapSteps();
        foreach (var st in _steps.Where(x => x.IsAction)) BindTargetAndContext(st);
        _steps.Sort((a, b) => a.TFrom != b.TFrom ? a.TFrom.CompareTo(b.TFrom) : a.AnchorSeq.CompareTo(b.AnchorSeq));
        BindEffects();
        BuildSystemSteps();
        BuildWaitSteps();
        BindSnapshots();
        _steps.Sort((a, b) => a.TFrom != b.TFrom ? a.TFrom.CompareTo(b.TFrom) : a.AnchorSeq.CompareTo(b.AnchorSeq));
        foreach (var st in _steps) BindFrames(st);
        ApplyDiscards();
        foreach (var st in _steps) { AssignEvidence(st); AssignImportance(st); }
        return Serialize(derivedAt);
    }

    // =====================================================================================
    // Lücken
    // =====================================================================================

    void CollectGaps()
    {
        var open = new Dictionary<string, JsonObject>();
        foreach (var e in _ev)
        {
            if (e.Str("type") != "gap") continue;
            string id = e.Str("gapId") ?? "";
            switch (e.Str("phase"))
            {
                case "begin": open[id] = e; break;
                case "end":
                    if (open.Remove(id, out var b))
                        _gapSpans.Add((b.Long("tFrom", b.Long("t")), e.Long("tTo", e.Long("t")), b.Str("cause") ?? "unknown", id, b.Arr("channels") ?? new JsonArray(), b.Str("inputLost") ?? "unknown", b.Long("seq")));
                    break;
                case "single":
                    _gapSpans.Add((e.Long("tFrom", e.Long("t")), e.Long("tTo", e.Long("t")), e.Str("cause") ?? "unknown", id, e.Arr("channels") ?? new JsonArray(), e.Str("inputLost") ?? "unknown", e.Long("seq")));
                    break;
            }
        }
        foreach (var b in open.Values)
            _gapSpans.Add((b.Long("tFrom", b.Long("t")), _duration, b.Str("cause") ?? "unknown", b.Str("gapId") ?? "", b.Arr("channels") ?? new JsonArray(), b.Str("inputLost") ?? "unknown", b.Long("seq")));
    }

    static bool InputChannels(JsonArray ch) => ch.Any(c => c?.GetValue<string>() is "keyboard" or "mouse" or "all");

    /// <summary>Liegt der Zeitpunkt in einer Lücke ohne verwertbaren Bildschirm (sicherer Desktop, Sperre, Ruhezustand)?</summary>
    bool InBlindGap(long t) => _gapSpans.Any(g => g.cause is "secureDesktop" or "sessionLocked" or "sessionDisconnected" or "suspend" && t >= g.from && t <= g.to);

    // =====================================================================================
    // Eingabeschritte
    // =====================================================================================

    StepDraft NewStep(string kind, JsonObject anchor)
    {
        var st = new StepDraft { Kind = kind, AnchorSeq = anchor.Long("seq"), TFrom = anchor.Long("t"), TTo = anchor.Long("t") };
        _steps.Add(st);
        return st;
    }

    void AddInput(StepDraft st, JsonObject e)
    {
        long seq = e.Long("seq");
        if (!_assigned.Add(seq)) return;
        st.Input.Add(seq);
        long t = e.Long("t");
        long tEnd = e.LongN("tEnd") ?? t;
        if (t < st.TFrom) st.TFrom = t;
        if (tEnd > st.TTo) st.TTo = tEnd;
        if (st.FirstInputSeq == 0 || seq < st.FirstInputSeq) st.FirstInputSeq = seq;
        if (seq > st.LastInputSeq) st.LastInputSeq = seq;
    }

    void AddApproach(StepDraft st, IEnumerable<JsonObject> moves)
    {
        foreach (var m in moves) if (_assigned.Add(m.Long("seq"))) st.Approach.Add(m.Long("seq"));
    }

    // Laufzustände
    StepDraft? _run;            // offener Tipplauf
    long _runFocus; string? _runFg; string? _runCtx; long _runLastT;
    readonly List<JsonObject> _pendingMoves = new();
    readonly Dictionary<long, StepDraft> _keyOwner = new(); // vk → Schritt der key.down
    readonly List<JsonObject> _pendingMods = new();         // nicht zugeordnete Modifier-Drücke
    StepDraft? _lastHotkey; string? _lastCombo; long _lastHotkeyT;
    StepDraft? _lastKeyStep; string? _lastKeyName; long _lastKeyT;
    StepDraft? _altTab; bool _altHeld;
    StepDraft? _press; JsonObject? _pressDown; readonly List<JsonObject> _pressMoves = new();
    StepDraft? _lastClick; JsonObject? _lastClickFirstDown; long _lastClickDownT; int _clickCount;
    StepDraft? _wheel; JsonObject? _lastWheel;
    StepDraft? _redactedGap;

    void BuildInputSteps()
    {
        foreach (var e in _ev)
        {
            string type = e.Str("type") ?? "";
            if (IsRedactedLine(e))
            {
                if (IsInputType(type)) { EndRun(); EndWheel(); FlushMoves(null); _redactedGap ??= NewRedactedGap(e); AddInput(_redactedGap, e); }
                continue;
            }
            if (type is not ("mouse.move" or "mouse.down" or "mouse.up" or "mouse.wheel" or "key.down" or "key.up" or "text.masked"))
            {
                if (type == "gap" && e.Str("phase") is "begin" or "single" && InputChannels(e.Arr("channels") ?? new JsonArray()) && e.Str("cause") != "recorderInteraction")
                { if (_run != null && _keyOwner.Values.Contains(_run)) _run.Flags.Add("incomplete"); EndRun(); EndWheel(); FlushMoves(null); EndPressIncomplete(); }
                if (type is "focus") OnFocusEvent(e);
                if (type is "session.start" or "display.topology") { FlushMoves(null); }
                continue;
            }
            _redactedGap = null;
            if (e.Bool("self")) continue;
            switch (type)
            {
                case "mouse.move": OnMove(e); break;
                case "mouse.down": OnDown(e); break;
                case "mouse.up": OnUp(e); break;
                case "mouse.wheel": OnWheel(e); break;
                case "key.down": OnKeyDown(e); break;
                case "key.up": OnKeyUp(e); break;
                case "text.masked": OnMasked(e); break;
            }
        }
        EndRun(); EndWheel(); EndPressIncomplete();
        FlushMoves(null);
        // nicht zugeordnete Modifier → Tastenschritte
        foreach (var m in _pendingMods.ToList()) LoneModifier(m);
    }

    StepDraft NewRedactedGap(JsonObject e)
    {
        var st = NewStep("gap", e);
        st.InputObj = new JsonObject { ["cause"] = "removedByAuthor", ["channels"] = new JsonArray("all"), ["inputLost"] = "none" };
        st.Flags.Add("redacted");
        return st;
    }

    void OnFocusEvent(JsonObject e)
    {
        if (_run != null && e.Long("seq") != _runFocus && e.Long("t") > _runLastT)
        {
            // Fokuswechsel auf ein anderes Element beendet den Lauf
            string? rid = e.Obj("el").Str("rid");
            string? runRid = _runFocus != 0 && _bySeq.TryGetValue(_runFocus, out var rf) ? rf.Obj("el").Str("rid") : null;
            if (rid == null || runRid == null || rid != runRid) EndRun();
        }
    }

    // ---------------- Maus ----------------

    void OnMove(JsonObject e)
    {
        if (_press != null) { _pressMoves.Add(e); AddInput(_press, e); return; }
        _pendingMoves.Add(e);
    }

    void OnDown(JsonObject e)
    {
        EndRun(); EndWheel();
        string button = e.Str("button") ?? "primary";
        int x = e.Int("x"), y = e.Int("y");
        // Mehrfachklick-Kette
        if (_lastClick != null && _lastClickFirstDown != null && _press == null
            && _lastClickFirstDown.Str("button") == button && _lastClickFirstDown.Str("win") == e.Str("win") && _lastClickFirstDown.Str("m") == e.Str("m")
            && e.Long("t") - _lastClickDownT <= _dblMs * 1000L
            && Math.Abs(x - _lastClickFirstDown.Int("x")) <= Math.Max(1, _dblW / 2) && Math.Abs(y - _lastClickFirstDown.Int("y")) <= Math.Max(1, _dblH / 2)
            && _pendingMoves.All(m => true))
        {
            foreach (var m in _pendingMoves) AddInput(_lastClick, m);
            _pendingMoves.Clear();
            _press = _lastClick; _pressDown = e; _pressMoves.Clear();
            _clickCount++;
            _lastClickDownT = e.Long("t");
            AddInput(_press, e);
            return;
        }
        _lastClick = null;
        var st = NewStep("click", e);
        FlushMoves(st);
        AddInput(st, e);
        st.Monitor = e.Str("m"); st.Win = e.Str("win");
        st.Position = new JsonObject { ["ep"] = e.Int("ep"), ["m"] = e.Str("m"), ["x"] = x, ["y"] = y };
        st.InputObj["button"] = button;
        st.InputObj["physButton"] = e.Str("physButton") ?? "left";
        st.InputObj["mods"] = e.Arr("mods")?.DeepClone() ?? new JsonArray();
        if (e.Arr("held") is { Count: > 0 } held) st.InputObj["held"] = held.DeepClone();
        st.InputObj["device"] = e.Str("device") ?? "mouse";
        if (e.Bool("inj")) st.Flags.Add("injected");
        if (e.Bool("recovered")) st.Flags.Add("recovered");
        // ausstehende Modifier gehören zum Klick (Strg+Klick)
        foreach (var m in _pendingMods) AddInput(st, m);
        _pendingMods.Clear();
        _press = st; _pressDown = e; _pressMoves.Clear();
        _clickCount = 1; _lastClickFirstDown = e; _lastClickDownT = e.Long("t");
    }

    void OnUp(JsonObject e)
    {
        if (_press == null || _pressDown == null || _pressDown.Str("button") != e.Str("button"))
        {
            // Loslassen ohne Druck: dem letzten Mausschritt zuordnen
            var last = _steps.LastOrDefault(s => s.IsMouse);
            if (last != null) AddInput(last, e); else { var st = NewStep("click", e); AddInput(st, e); st.Flags.Add("incomplete"); }
            return;
        }
        var stp = _press;
        AddInput(stp, e);
        stp.InputObj["pressUs"] = e.Long("t") - _pressDown.Long("t");
        stp.InputObj["longPress"] = e.Long("t") - _pressDown.Long("t") >= LongPressMs * 1000L;
        // Ziehen?
        int dx = 0, dy = 0;
        foreach (var m in _pressMoves.Concat(new[] { e }))
            foreach (var (_, mx, my) in Unpack(m))
            { dx = Math.Max(dx, Math.Abs(mx - _pressDown.Int("x"))); dy = Math.Max(dy, Math.Abs(my - _pressDown.Int("y"))); }
        bool sameMon = e.Str("m") == _pressDown.Str("m");
        if ((dx > _dragW || dy > _dragH || !sameMon) && _clickCount == 1)
        {
            stp.Kind = "drag";
            stp.EndPosition = new JsonObject { ["ep"] = e.Int("ep"), ["m"] = e.Str("m"), ["x"] = e.Int("x"), ["y"] = e.Int("y") };
            var path = new List<(long t, int x, int y)> { (_pressDown.Long("t"), _pressDown.Int("x"), _pressDown.Int("y")) };
            foreach (var m in _pressMoves) path.AddRange(Unpack(m));
            path.Add((e.Long("t"), e.Int("x"), e.Int("y")));
            var simplified = Rdp(path, 2, PathMaxPoints);
            long t0 = _pressDown.Long("t");
            stp.InputObj["path"] = new JsonArray(simplified.Select(p => (JsonNode)new JsonArray(p.x, p.y, (p.t - t0) / 1000)).ToArray());
            // gedrückt halten vor der ersten Bewegung und über dem Ablageziel vor dem Loslassen
            var start = path[0]; var end = path[^1];
            var firstMove = path.FirstOrDefault(p => Math.Abs(p.x - start.x) > 3 || Math.Abs(p.y - start.y) > 3);
            stp.InputObj["holdBeforeMoveUs"] = Math.Max(0, (firstMove == default ? end.t : firstMove.t) - t0);
            int lastAway = path.FindLastIndex(p => Math.Abs(p.x - end.x) > 3 || Math.Abs(p.y - end.y) > 3);
            stp.InputObj["holdAtEndUs"] = lastAway >= 0 && lastAway + 1 < path.Count ? Math.Max(0, end.t - path[lastAway + 1].t) : 0;
            stp.InputObj["distancePx"] = (int)PathLen(path);
            stp.InputObj["durationUs"] = e.Long("t") - _pressDown.Long("t");
            stp.InputObj["mods"] = e.Arr("mods")?.DeepClone() ?? new JsonArray();
            _lastClick = null;
        }
        else
        {
            stp.InputObj["count"] = _clickCount;
            _lastClick = stp;
        }
        _press = null; _pressDown = null; _pressMoves.Clear();
        FinalizeClickKind(stp);
    }

    void FinalizeClickKind(StepDraft st)
    {
        if (st.Kind == "drag") return;
        string button = st.InputObj.Str("button") ?? "primary";
        int count = st.InputObj.Int("count", 1);
        st.Kind = (button, count) switch
        {
            ("primary", 2) => "doubleClick",
            ("primary", 3) => "tripleClick",
            ("secondary", 1) => "rightClick",
            _ => "click"
        };
    }

    void EndPressIncomplete()
    {
        if (_press != null) { _press.Flags.Add("incomplete"); _press.InputObj["count"] = _clickCount; FinalizeClickKind(_press); }
        _press = null; _pressDown = null; _pressMoves.Clear();
    }

    void OnWheel(JsonObject e)
    {
        EndRun();
        _lastClick = null;
        int delta = e.Int("delta");
        if (_wheel != null && _lastWheel != null && _lastWheel.Str("axis") == e.Str("axis") && Math.Sign(_lastWheel.Int("delta")) == Math.Sign(delta)
            && _lastWheel.Str("win") == e.Str("win") && ModsKey(_lastWheel) == ModsKey(e) && e.Long("t") - _lastWheel.Long("t") <= ScrollGroupGapMs * 1000L)
        {
            foreach (var m in _pendingMoves) AddInput(_wheel, m);
            _pendingMoves.Clear();
            AddInput(_wheel, e);
            _wheel.InputObj["events"] = _wheel.InputObj.Int("events") + 1;
            _wheel.InputObj["deltaSum"] = _wheel.InputObj.Int("deltaSum") + delta;
            _lastWheel = e;
            return;
        }
        EndWheel();
        var st = NewStep("scroll", e);
        FlushMoves(st);
        AddInput(st, e);
        st.Monitor = e.Str("m"); st.Win = e.Str("win");
        st.Position = new JsonObject { ["ep"] = e.Int("ep"), ["m"] = e.Str("m"), ["x"] = e.Int("x"), ["y"] = e.Int("y") };
        st.InputObj["axis"] = e.Str("axis") ?? "v";
        st.InputObj["events"] = 1;
        st.InputObj["deltaSum"] = delta;
        st.InputObj["mods"] = e.Arr("mods")?.DeepClone() ?? new JsonArray();
        foreach (var m in _pendingMods) AddInput(st, m);
        _pendingMods.Clear();
        _wheel = st; _lastWheel = e;
    }

    static string ModsKey(JsonObject e) => string.Join(",", e.StrList("mods"));

    void EndWheel()
    {
        if (_wheel == null) return;
        var st = _wheel;
        double notches = st.InputObj.Int("deltaSum") / 120.0;
        st.InputObj["notches"] = Math.Round(notches, 2);
        bool ctrl = st.InputObj.StrList("mods").Contains("ctrl");
        if (st.InputObj.Str("axis") == "v" && st.InputObj.StrList("mods").Count == 0) st.InputObj["lines"] = Math.Round(notches * _wheelLines, 2);
        st.InputObj["semantic"] = ctrl ? "zoom" : "scroll";
        _wheel = null; _lastWheel = null;
    }

    void FlushMoves(StepDraft? next)
    {
        if (_pendingMoves.Count == 0) return;
        // Verweilen mit Wirkung → Hover-Schritt
        var hoverSplit = FindHover(_pendingMoves);
        if (hoverSplit != null)
        {
            var (idx, dwellUs) = hoverSplit.Value;
            var hoverMoves = _pendingMoves.Take(idx + 1).ToList();
            var anchor = hoverMoves[^1];
            var hv = NewStep("hover", anchor);
            var last = Unpack(anchor).Last();
            hv.Position = new JsonObject { ["ep"] = anchor.Int("ep"), ["m"] = anchor.Str("m"), ["x"] = last.x, ["y"] = last.y };
            hv.Monitor = anchor.Str("m");
            hv.InputObj["dwellUs"] = dwellUs;
            foreach (var m in hoverMoves) AddInput(hv, m);
            hv.ApproachObj = ApproachInfo(hoverMoves);
            hv.TTo = Math.Max(hv.TTo, anchor.Long("tEnd", anchor.Long("t")) + dwellUs);
            _pendingMoves.RemoveRange(0, idx + 1);
        }
        if (_pendingMoves.Count == 0) return;
        if (next != null)
        {
            next.ApproachObj = ApproachInfo(_pendingMoves);
            AddApproach(next, _pendingMoves);
        }
        else
        {
            var mv = NewStep("move", _pendingMoves[0]);
            foreach (var m in _pendingMoves) AddInput(mv, m);
            mv.ApproachObj = ApproachInfo(_pendingMoves);
            var last = Unpack(_pendingMoves[^1]).Last();
            mv.Position = new JsonObject { ["ep"] = _pendingMoves[^1].Int("ep"), ["m"] = _pendingMoves[^1].Str("m"), ["x"] = last.x, ["y"] = last.y };
            mv.Monitor = _pendingMoves[^1].Str("m");
        }
        _pendingMoves.Clear();
    }

    /// <summary>Sucht eine Ruhepause ≥ hoverMinMs mit anschließender Wirkung (Menü/Popup/Tooltip).</summary>
    (int idx, long dwellUs)? FindHover(List<JsonObject> moves)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            long restStart = moves[i].Long("tEnd", moves[i].Long("t"));
            long restEnd = i + 1 < moves.Count ? moves[i + 1].Long("t") : restStart; // letzte Ruhe = Ziel, kein Verweilen
            if (i + 1 >= moves.Count) break;
            long dwell = restEnd - restStart;
            if (dwell < _hoverMinMs * 1000L) continue;
            bool effect = _ev.Any(e => e.Long("t") >= restStart && e.Long("t") <= restEnd + HoverEffectMs * 1000L &&
                ((e.Str("type") == "window.open" && e.Str("kind") is "menu" or "popup" or "dropdown" or "tooltip") ||
                 (e.Str("type") == "window.menu" && e.Str("phase") == "popupStart") || e.Str("type") == "ui.expand"));
            if (effect) return (i, dwell);
        }
        return null;
    }

    JsonObject ApproachInfo(List<JsonObject> moves)
    {
        var pts = moves.SelectMany(Unpack).ToList();
        if (pts.Count == 0) return new JsonObject();
        double dist = PathLen(pts);
        var a = pts[0]; var b = pts[^1];
        double direct = Math.Sqrt(Math.Pow(b.x - a.x, 2) + Math.Pow(b.y - a.y, 2));
        var simplified = Rdp(pts, 3, PathMaxPoints);
        var dwells = new JsonArray();
        for (int i = 0; i + 1 < moves.Count; i++)
        {
            long gap = moves[i + 1].Long("t") - moves[i].Long("tEnd", moves[i].Long("t"));
            if (gap >= DwellNoteMs * 1000L) { var p = Unpack(moves[i]).Last(); dwells.Add(new JsonObject { ["x"] = p.x, ["y"] = p.y, ["durationUs"] = gap }); }
        }
        return new JsonObject
        {
            ["from"] = new JsonObject { ["m"] = moves[0].Str("m"), ["x"] = a.x, ["y"] = a.y },
            ["distancePx"] = (int)dist, ["directPx"] = (int)direct,
            ["durationUs"] = b.t - a.t, ["straightness"] = dist < 1 ? 1.0 : Math.Round(direct / dist, 2),
            ["path"] = new JsonArray(simplified.Select(p => (JsonNode)new JsonArray(p.x, p.y, (p.t - a.t) / 1000)).ToArray()),
            ["dwells"] = dwells
        };
    }

    public static IEnumerable<(long t, int x, int y)> Unpack(JsonObject m)
    {
        long t = m.Long("t"); int x = m.Int("x"), y = m.Int("y");
        yield return (t, x, y);
        if (m.Str("type") != "mouse.move") yield break;
        foreach (var d in m.Arr("d") ?? new JsonArray())
        {
            if (d is not JsonArray a || a.Count < 3) continue;
            t += a[0]!.GetValue<long>(); x += a[1]!.GetValue<int>(); y += a[2]!.GetValue<int>();
            yield return (t, x, y);
        }
    }

    static double PathLen(List<(long t, int x, int y)> p)
    {
        double d = 0;
        for (int i = 1; i < p.Count; i++) d += Math.Sqrt(Math.Pow(p[i].x - p[i - 1].x, 2) + Math.Pow(p[i].y - p[i - 1].y, 2));
        return d;
    }

    static List<(long t, int x, int y)> Rdp(List<(long t, int x, int y)> pts, double eps, int max)
    {
        if (pts.Count <= 2) return pts;
        var keep = new bool[pts.Count]; keep[0] = keep[^1] = true;
        void Rec(int a, int b)
        {
            double maxD = 0; int idx = -1;
            for (int i = a + 1; i < b; i++)
            {
                double d = DistToSeg(pts[i], pts[a], pts[b]);
                if (d > maxD) { maxD = d; idx = i; }
            }
            if (idx >= 0 && maxD > eps) { keep[idx] = true; Rec(a, idx); Rec(idx, b); }
        }
        Rec(0, pts.Count - 1);
        var res = pts.Where((_, i) => keep[i]).ToList();
        if (res.Count > max)
        {
            var r2 = new List<(long, int, int)>();
            for (int i = 0; i < max; i++) r2.Add(res[(int)Math.Round(i * (res.Count - 1) / (double)(max - 1))]);
            return r2;
        }
        return res;
    }

    static double DistToSeg((long t, int x, int y) p, (long t, int x, int y) a, (long t, int x, int y) b)
    {
        double dx = b.x - a.x, dy = b.y - a.y;
        double len = dx * dx + dy * dy;
        if (len == 0) return Math.Sqrt(Math.Pow(p.x - a.x, 2) + Math.Pow(p.y - a.y, 2));
        double tt = Math.Clamp(((p.x - a.x) * dx + (p.y - a.y) * dy) / len, 0, 1);
        return Math.Sqrt(Math.Pow(p.x - (a.x + tt * dx), 2) + Math.Pow(p.y - (a.y + tt * dy), 2));
    }
}
