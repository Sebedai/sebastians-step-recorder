using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Steps;

public sealed partial class StepBuilder
{
    // =====================================================================================
    // Marker, Kommentare, Lücken
    // =====================================================================================

    void BuildMarkerCommentSteps()
    {
        foreach (var e in _ev)
        {
            switch (e.Str("type"))
            {
                case "comment":
                    {
                        var st = NewStep("comment", e);
                        st.InputObj["refSeq"] = e.Long("seq");
                        st.Prov.Add(new JsonObject { ["path"] = "input.text", ["seq"] = e.Long("seq"), ["field"] = "text", ["copy"] = true });
                        break;
                    }
                case "marker":
                    {
                        string kind = e.Str("kind") ?? "note";
                        if (kind is "discardPrevious" or "redactLastInput") break;
                        if (kind == "snapshot") { var sn = NewStep("snapshot", e); sn.InputObj["refSeq"] = e.Long("seq"); break; }
                        if (kind is not ("note" or "error" or "expected" or "important")) kind = "note";
                        var st = NewStep("marker", e);
                        st.InputObj["refSeq"] = e.Long("seq");
                        st.InputObj["markerKind"] = kind;
                        if (e.Str("label") is { } l) { st.InputObj["label"] = l; st.Prov.Add(new JsonObject { ["path"] = "input.label", ["seq"] = e.Long("seq"), ["field"] = "label", ["copy"] = true }); }
                        break;
                    }
            }
        }
    }

    void BuildGapSteps()
    {
        var maskedTimes = _ev.Where(e => e.Str("type") == "text.masked").Select(e => e.Long("t")).ToList();
        foreach (var g in _gapSpans)
        {
            if (g.cause == "recorderInteraction" || g.cause == "frameDropped") continue;
            bool input = InputChannels(g.channels);
            if (g.cause == "privacyMode" && maskedTimes.Any(t => t >= g.from && t <= g.to)) continue;
            if (!input && (g.to - g.from) < GapStepMinMs * 1000L) continue;
            if (!_bySeq.TryGetValue(g.seq, out var anchor)) continue;
            var st = NewStep("gap", anchor);
            st.TFrom = g.from; st.TTo = g.to;
            st.GapId = g.id;
            st.InputObj["cause"] = g.cause;
            st.InputObj["channels"] = g.channels.DeepClone();
            st.InputObj["inputLost"] = g.inputLost;
            // sicherer Desktop: Art (uac/other) steht im Ende-Ereignis der Lücke
            if (g.cause == "secureDesktop" && _ev.LastOrDefault(e => e.Str("type") == "gap" && e.Str("gapId") == g.id && e.Str("phase") == "end")?.Obj("detail")?.Str("kind") is { } dk)
                st.InputObj["desktopKind"] = dk;
            if (!input) st.Importance = "trace";
            // offene Schritte, die in die Lücke laufen, sind unvollständig
            foreach (var s in _steps.Where(s => s.IsAction && s.TFrom < g.from && s.TTo > g.from)) s.Flags.Add("incomplete");
        }
    }

    // =====================================================================================
    // Ziel und Kontext
    // =====================================================================================

    JsonObject? Win(string? id) => id == null ? null : _s.Window(id);

    void BindTargetAndContext(StepDraft st)
    {
        var anchor = _bySeq[st.AnchorSeq];
        string? win = st.Win ?? anchor.Str("win") ?? anchor.Str("fg");
        st.Win = win;
        var w = Win(win);
        if (w != null)
        {
            st.Ctx = new JsonObject { ["win"] = win, ["process"] = w.Str("process"), ["title"] = w.Str("title"), ["kind"] = w.Str("kind") };
            if (w.Str("surface") is { } sf && sf != "structured") st.Ctx["surface"] = sf;
            if (w.Str("surfaceKind") is { } sk) st.Ctx["surfaceKind"] = sk;
            if (w["elevated"] != null) st.Ctx["elevated"] = w.Bool("elevated");
            if (st.Monitor != null) { st.Ctx["ep"] = anchor.Int("ep"); st.Ctx["m"] = st.Monitor; }
            st.Ctx.Clean();
            if (w.Str("title") != null) st.Prov.Add(new JsonObject { ["path"] = "ctx.title", ["seq"] = w.Long("seq"), ["field"] = "title", ["copy"] = true });
            var surface = Capture.WindowRegistry.SurfaceOfInfo(w);
            if (surface == "opaque") st.Flags.Add("opaque");
            // Getipptes in Fenstern ohne erkennbare Passwortfelder (Terminal, Remote-Desktop, VM): Klartext → Durchsicht nötig
            if (st.Kind == "type" && !st.InputObj.Bool("masked") && surface != "structured") st.Flags.Add("plaintext");
        }
        switch (st.Kind)
        {
            case "click" or "doubleClick" or "tripleClick" or "rightClick" or "drag": ClickTarget(st, anchor); break;
            case "scroll": ScrollTarget(st, anchor); break;
            case "hover": HoverTarget(st, anchor); break;
            case "type" or "hotkey" or "key": KeyTarget(st); break;
        }
        if (st.Kind == "type" && !st.InputObj.Bool("masked")) ReconcileTyping(st);
        if (st.Kind == "drag") DragKind(st);
        BuildAction(st);
    }

    JsonObject? ElementFor(string trigger, long refSeq)
        => _ev.FirstOrDefault(e => e.Str("type") == "element" && e.Str("trigger") == trigger && e.Long("ref") == refSeq);

    bool Usable(JsonObject? el, JsonObject point, string? win, out string? why)
    {
        why = null;
        if (el == null) { why = "uiaUnavailable"; return false; }
        if (el.Str("status") != "ok") { why = el.Str("status") == "timeout" ? "uiaTimeout" : "uiaUnavailable"; return false; }
        var d = el.Obj("el");
        if (d == null) return false;
        if (d.Bool("opaqueHint")) { why = "opaqueSurface"; return false; }
        var r = d.Obj("rect");
        if (r != null && r.Str("m") == point.Str("m"))
        {
            int x = point.Int("x"), y = point.Int("y");
            if (x < r.Int("x") - RectTolerancePx || y < r.Int("y") - RectTolerancePx || x > r.Int("x") + r.Int("w") + RectTolerancePx || y > r.Int("y") + r.Int("h") + RectTolerancePx)
            { why = "rectImplausible"; return false; }
        }
        // Wrapper-Elemente ohne Namen, die (nahezu) das ganze Fenster bedecken, sind kein Ziel
        if (d.Str("ct") is "Pane" or "Window" or "Custom" or "Document" or "Image" or "Group" && string.IsNullOrEmpty(d.Str("name")))
        {
            var wr = Win(win)?.Obj("rect");
            if (wr != null && r != null && (double)r.Int("w") * r.Int("h") >= 0.6 * Math.Max(1, wr.Int("w") * wr.Int("h"))) { why = "opaqueSurface"; return false; }
        }
        return true;
    }

    static bool SameElement(JsonObject? a, JsonObject? b)
    {
        if (a == null || b == null) return false;
        if (a.Str("rid") != null && a.Str("rid") == b.Str("rid")) return true;
        return a.Str("ct") == b.Str("ct") && a.Str("name") == b.Str("name") && a.Str("aid") == b.Str("aid") && a.Str("ct") != null;
    }

    /// <summary>Win32-Kontext (Fenster, ClassNN, Koordinaten, AHK-WinTitle) des auslösenden Ereignisses an das Ziel hängen.</summary>
    static void AttachWin32(JsonObject? target, JsonObject? ev, StepDraft st, string path)
    {
        if (target == null || ev?.Obj("win32") is not { } w) return;
        target["win32"] = w.DeepClone();
        st.Prov.Add(new JsonObject { ["path"] = path, ["seq"] = ev.Long("seq"), ["field"] = "win32", ["copy"] = true });
    }

    void ClickTarget(StepDraft st, JsonObject down)
    {
        var T = ElementFor("buttonDown", down.Long("seq"));
        JsonObject? V = null;
        if (T?.LongN("dwellRef") is long dr && _bySeq.TryGetValue(dr, out var dv)) V = dv;
        V ??= _ev.LastOrDefault(e => e.Str("type") == "element" && e.Str("trigger") == "dwell" && e.Long("t") <= down.Long("t") && down.Long("t") - e.Long("t") <= ElementMaxAgeMs * 1000L);
        var reasons = new JsonArray();
        bool tu = Usable(T, down, st.Win, out var tw), vu = V != null && Usable(V, down, st.Win, out _) && T?["dwellValid"]?.GetValue<bool>() != false;
        if (tw != null) reasons.Add(tw);
        // Reaktion: Fokus auf dasselbe Element kurz nach dem Klick
        var R = _ev.FirstOrDefault(e => e.Str("type") is "focus" or "ui.invoke" or "ui.toggle" or "ui.expand" or "ui.selection" && e.Long("t") >= down.Long("t") && e.Long("t") <= st.TTo + 1_000_000);
        var tEl = T?.Obj("el"); var vEl = V?.Obj("el"); var rEl = R?.Obj("el");
        string status; JsonObject? chosen = null; long? fromSeq = null; var alternatives = new JsonArray();
        var corroboration = new JsonArray();
        if (tu && vu && SameElement(tEl, vEl)) { status = "agreed"; chosen = tEl; fromSeq = T!.Long("seq"); reasons.Add("elementAgreed"); }
        else if (tu ^ vu)
        {
            var one = tu ? tEl : vEl; var oneEv = tu ? T : V;
            chosen = one; fromSeq = oneEv!.Long("seq");
            if (rEl != null && SameElement(one, rEl)) { status = "corroborated"; corroboration.Add(R!.Long("seq")); reasons.Add("elementCorroborated"); }
            else { status = "single"; reasons.Add("elementSingle"); }
        }
        else if (tu && vu)
        {
            if (rEl != null && (SameElement(tEl, rEl) || SameElement(vEl, rEl)))
            {
                bool t1 = SameElement(tEl, rEl);
                chosen = t1 ? tEl : vEl; fromSeq = (t1 ? T : V)!.Long("seq");
                alternatives.Add(new JsonObject { ["el"] = (t1 ? vEl : tEl)!.DeepClone(), ["fromSeq"] = (t1 ? V : T)!.Long("seq") });
                status = "corroborated"; corroboration.Add(R!.Long("seq")); reasons.Add("elementCorroborated");
            }
            else
            {
                status = "conflict"; chosen = vEl; fromSeq = V!.Long("seq");
                alternatives.Add(new JsonObject { ["el"] = tEl!.DeepClone(), ["fromSeq"] = T!.Long("seq") });
                reasons.Add("elementConflict");
            }
        }
        else status = st.Win != null ? "position" : "none";
        if (st.Flags.Contains("opaque") && status != "none") { status = "position"; chosen = null; reasons.Add("opaqueSurface"); }
        if (down.Str("hitHwnd") is { } hh && chosen?.Str("hwnd") is { } eh)
            reasons.Add(string.Equals(hh, eh, StringComparison.OrdinalIgnoreCase) ? "hitWindowMatches" : "hitWindowMismatch");
        var target = new JsonObject { ["status"] = status };
        if (chosen != null)
        {
            target["source"] = "uia";
            target["el"] = chosen.DeepClone();
            var path = (fromSeq != null && _bySeq.TryGetValue(fromSeq.Value, out var src)) ? src.Arr("path") : null;
            if (path != null) target["path"] = path.DeepClone();
            st.Prov.Add(new JsonObject { ["path"] = "target.el", ["seq"] = fromSeq, ["field"] = "el", ["copy"] = true });
            // Position im Element
            if (chosen.Obj("rect") is { } r && r.Int("w") > 0 && r.Int("h") > 0 && st.Position != null)
                st.Position["inElement"] = new JsonArray(Math.Round((st.Position.Int("x") - r.Int("x")) / (double)r.Int("w"), 2), Math.Round((st.Position.Int("y") - r.Int("y")) / (double)r.Int("h"), 2));
        }
        var from = new JsonObject();
        if (V != null) from["dwell"] = V.Long("seq");
        if (T != null) from["down"] = T.Long("seq");
        if (corroboration.Count > 0) from["corroboration"] = corroboration;
        target["from"] = from;
        if (alternatives.Count > 0) target["alternatives"] = alternatives;
        st.Target = target;
        AttachWin32(target, T, st, "target.win32");
        st.Evidence["target"] = status;
        st.Evidence["reasons"] = reasons;
        // Fensterposition
        var w = Win(st.Win);
        if (w?.Obj("rect") is { } wr && st.Position != null && wr.Str("m") == st.Position.Str("m"))
            st.Position["inWindow"] = new JsonArray(st.Position.Int("x") - wr.Int("x"), st.Position.Int("y") - wr.Int("y"));
        if (st.Kind == "drag")
        {
            var up = st.Input.Select(s => _bySeq[s]).LastOrDefault(e => e.Str("type") == "mouse.up");
            var U = up != null ? ElementFor("buttonUp", up.Long("seq")) : null;
            if (U?.Str("status") == "ok" && U.Obj("el") is { } uel)
            {
                st.EndTarget = new JsonObject { ["status"] = "single", ["source"] = "uia", ["el"] = uel.DeepClone() };
                st.Prov.Add(new JsonObject { ["path"] = "endTarget.el", ["seq"] = U.Long("seq"), ["field"] = "el", ["copy"] = true });
            }
            if (U != null) { st.EndTarget ??= new JsonObject { ["status"] = "position" }; AttachWin32(st.EndTarget, U, st, "endTarget.win32"); }
        }
    }

    void ScrollTarget(StepDraft st, JsonObject first)
    {
        var W = ElementFor("wheel", first.Long("seq"));
        var reasons = new JsonArray();
        string status = "position";
        string? why = null;
        if (W != null && Usable(W, first, st.Win, out why))
        {
            status = "single";
            st.Target = new JsonObject { ["status"] = "single", ["source"] = "uia", ["el"] = W.Obj("el")!.DeepClone() };
            st.Prov.Add(new JsonObject { ["path"] = "target.el", ["seq"] = W.Long("seq"), ["field"] = "el", ["copy"] = true });
            reasons.Add("elementSingle");
        }
        else
        {
            if (why != null) reasons.Add(why);
            st.Target = new JsonObject { ["status"] = st.Win != null ? "position" : "none" };
            status = st.Target.Str("status")!;
        }
        AttachWin32(st.Target, W, st, "target.win32");
        st.Evidence["target"] = status;
        st.Evidence["reasons"] = reasons;
    }

    void HoverTarget(StepDraft st, JsonObject anchor)
    {
        var V = _ev.FirstOrDefault(e => e.Str("type") == "element" && e.Str("trigger") == "dwell" && e.Long("ref") == anchor.Long("seq"));
        if (V?.Str("status") == "ok" && V.Obj("el") is { } el)
        {
            st.Target = new JsonObject { ["status"] = "single", ["source"] = "uia", ["el"] = el.DeepClone() };
            st.Prov.Add(new JsonObject { ["path"] = "target.el", ["seq"] = V.Long("seq"), ["field"] = "el", ["copy"] = true });
            st.Evidence["target"] = "single";
        }
        else { st.Target = new JsonObject { ["status"] = "position" }; st.Evidence["target"] = "position"; }
        st.Evidence["reasons"] = new JsonArray();
    }

    void KeyTarget(StepDraft st)
    {
        var anchor = _bySeq[st.AnchorSeq];
        long focus = st.InputObj.LongN("focusSeq") ?? anchor.Long("focus");
        st.InputObj.Remove("focusSeq");
        st.InputObj.Remove("ctxAtStart");
        var reasons = new JsonArray();
        var fev = focus != 0 && _bySeq.TryGetValue(focus, out var f) ? f : null;
        var desc = fev != null ? _ev.FirstOrDefault(e => e.Str("type") == "element" && e.Str("trigger") == "focus" && e.Long("ref") == focus) : null;
        if (desc?.Str("status") == "ok" && desc.Obj("el") is { } el)
        {
            st.Target = new JsonObject { ["status"] = "single", ["source"] = "uia", ["el"] = el.DeepClone() };
            if (desc.Arr("path") is { } p) st.Target["path"] = p.DeepClone();
            st.Prov.Add(new JsonObject { ["path"] = "target.el", ["seq"] = desc.Long("seq"), ["field"] = "el", ["copy"] = true });
            reasons.Add("focusFieldKnown");
            st.Evidence["target"] = "single";
        }
        else if (fev?.Obj("el") is { } fel)
        {
            st.Target = new JsonObject { ["status"] = "single", ["source"] = "uia", ["el"] = fel.DeepClone() };
            reasons.Add("focusFieldKnown");
            st.Evidence["target"] = "single";
        }
        else { st.Target = new JsonObject { ["status"] = "none" }; reasons.Add("focusUnknown"); st.Evidence["target"] = "none"; }
        AttachWin32(st.Target, desc, st, "target.win32");
        if (anchor.Str("fg") != null) reasons.Add("foregroundKnown");
        st.Evidence["reasons"] = reasons;
    }

    void ReconcileTyping(StepDraft st)
    {
        var el = st.Target?.Obj("el");
        var reasons = st.Evidence["reasons"] as JsonArray ?? new JsonArray();
        JsonObject? before = null, after = null;
        if (el != null)
        {
            before = _ev.LastOrDefault(e => e.Str("type") == "ui.value" && e.Long("t") <= st.TFrom && SameElement(e.Obj("el"), el));
            // nach dem letzten Tastendruck, vor der nächsten fremden Eingabe
            long tLast = st.Input.Select(s => _bySeq[s].Long("t")).DefaultIfEmpty(st.TTo).Max();
            long nextInput = _ev.Where(e => IsInput(e) && e.Long("t") > tLast && !st.Input.Contains(e.Long("seq"))).Select(e => e.Long("t")).DefaultIfEmpty(long.MaxValue).First();
            after = _ev.LastOrDefault(e => e.Str("type") == "ui.value" && e.Long("t") > st.TFrom && e.Long("t") <= Math.Min(nextInput, tLast + 5_000_000) && SameElement(e.Obj("el"), el));
        }
        static string? Nl(string? x) => x?.Replace("\r\n", "\n").Replace('\r', '\n');
        string? vb = Nl(before?.Str("value")), va = Nl(after?.Str("value"));
        if (vb != null) { st.InputObj["valueBefore"] = vb; st.Prov.Add(new JsonObject { ["path"] = "input.valueBefore", ["seq"] = before!.Long("seq"), ["field"] = "value", ["copy"] = true }); }
        if (va != null) { st.InputObj["valueAfter"] = va; st.Prov.Add(new JsonObject { ["path"] = "input.valueAfter", ["seq"] = after!.Long("seq"), ["field"] = "value", ["copy"] = true }); }
        string? text = Nl(st.InputObj.Str("text"));
        if (text != null)
            st.Prov.Add(new JsonObject { ["path"] = "input.text", ["seqRanges"] = Ranges(st.Input), ["field"] = "ch", ["copy"] = false });
        st.Prov.Add(new JsonObject { ["path"] = "input.tokens", ["seqRanges"] = Ranges(st.Input), ["field"] = "ch", ["copy"] = false });
        string result, source;
        if (va != null && text != null && (va.TrimEnd('\n') == ((vb ?? "") + text).TrimEnd('\n') || va.TrimEnd('\n').EndsWith(text.TrimEnd('\n')))) { result = "valueConfirmed"; source = "value"; reasons.Add("valueMatchesKeys"); }
        else if (va != null && vb != null && va != vb) { result = "valueOnly"; source = "value"; reasons.Add("valueMismatch"); }
        else if (va != null && vb == null && text == null) { result = "valueOnly"; source = "value"; reasons.Add("valueFromField"); }
        else if (text != null) { result = "simulated"; source = "simulated"; if (va == null) reasons.Add("valueUnreadable"); }
        else { result = "none"; source = "none"; }
        if (st.Flags.Contains("opaque") && result == "valueConfirmed") { result = "simulated"; source = "simulated"; }
        st.Evidence["result"] = result;
        st.InputObj["resultSource"] = source;
        st.Evidence["reasons"] = reasons;
        // Fokuselement bestätigt durch Wertänderung
        if (after != null && st.Target != null && st.Target.Str("status") == "single") { st.Target["status"] = "corroborated"; st.Evidence["target"] = "corroborated"; }
    }

    void DragKind(StepDraft st)
    {
        var down = _bySeq[st.AnchorSeq];
        string kind = "unknown";
        var winRect = _ev.Where(e => e.Str("type") == "window.rect" && e.Str("win") == st.Win && e.Long("t") >= st.TFrom && e.Long("t") <= st.TTo + 500_000).ToList();
        var selection = _ev.FirstOrDefault(e => e.Str("type") == "ui.selection" && e.Long("t") >= st.TFrom && e.Long("t") <= st.TTo + 1_000_000);
        var ct = st.Target?.Obj("el").Str("ct");
        if (winRect.Count > 0) kind = "moveWindow";
        else if (selection?.Str("text") is { } sel) { kind = "selectText"; st.InputObj["selectedText"] = sel; }
        else if (_ev.Any(e => e.Str("type") == "window.dragdrop" && e.Long("t") >= st.TFrom && e.Long("t") <= st.TTo)) kind = "dragDrop";
        else if (ct is "Slider" or "Thumb" or "ScrollBar") kind = "slider";
        else if (st.Flags.Contains("opaque") || ct is "Pane" or "Custom" or "Image") kind = "draw";
        else if (ct is "Edit" or "Document" or "Text") kind = "selectText";
        st.InputObj["button"] = down.Str("button") ?? "primary";
        st.InputObj["dragKind"] = kind;
    }

    void BuildAction(StepDraft st)
    {
        var p = st.Position;
        JsonObject? a = null;
        var mods = st.InputObj.Arr("mods");
        switch (st.Kind)
        {
            case "click":
                {
                    string button = st.InputObj.Str("button") ?? "primary";
                    int count = st.InputObj.Int("count", 1);
                    a = button == "middle" && count == 1
                        ? new JsonObject { ["verb"] = "middleClick", ["m"] = p?.Str("m"), ["x"] = p?.Int("x"), ["y"] = p?.Int("y") }
                        : new JsonObject { ["verb"] = "click", ["m"] = p?.Str("m"), ["x"] = p?.Int("x"), ["y"] = p?.Int("y"), ["button"] = button, ["clicks"] = count };
                    break;
                }
            case "doubleClick" or "tripleClick" or "rightClick":
                a = new JsonObject { ["verb"] = st.Kind, ["m"] = p?.Str("m"), ["x"] = p?.Int("x"), ["y"] = p?.Int("y") };
                break;
            case "drag":
                a = new JsonObject { ["verb"] = "dragTo", ["m"] = p?.Str("m"), ["x"] = p?.Int("x"), ["y"] = p?.Int("y"), ["toM"] = st.EndPosition?.Str("m"), ["toX"] = st.EndPosition?.Int("x"), ["toY"] = st.EndPosition?.Int("y"), ["button"] = st.InputObj.Str("button") };
                break;
            case "scroll":
                {
                    double n = st.InputObj.Dbl("notches");
                    bool v = st.InputObj.Str("axis") == "v";
                    a = new JsonObject { ["verb"] = "scroll", ["m"] = p?.Str("m"), ["x"] = p?.Int("x"), ["y"] = p?.Int("y"), ["dx"] = v ? 0 : n, ["dy"] = v ? n : 0 };
                    break;
                }
            case "type":
                if (!st.InputObj.Bool("masked") && st.InputObj.Str("text") is { } text)
                {
                    a = new JsonObject { ["verb"] = "write", ["text"] = text };
                    if (st.InputObj.Obj("terminator") is { } term) a["then"] = new JsonObject { ["verb"] = "press", ["keys"] = new JsonArray(term.Str("key")), ["presses"] = 1 };
                }
                break;
            case "hotkey":
                a = new JsonObject { ["verb"] = "hotkey", ["keys"] = new JsonArray((st.InputObj.Str("combo") ?? "").Split('+').Select(k => (JsonNode)k).ToArray()) };
                break;
            case "key":
                a = new JsonObject { ["verb"] = "press", ["keys"] = new JsonArray(st.InputObj.Str("key")), ["presses"] = st.InputObj.Int("count", 1) };
                break;
            case "hover":
                a = new JsonObject { ["verb"] = "moveTo", ["m"] = p?.Str("m"), ["x"] = p?.Int("x"), ["y"] = p?.Int("y") };
                break;
            case "windowSwitch":
                a = new JsonObject { ["verb"] = "hotkey", ["keys"] = new JsonArray("Alt", "Tab") };
                break;
        }
        if (a != null && mods is { Count: > 0 } && st.Kind is not ("type" or "hotkey" or "key")) a["mods"] = mods.DeepClone();
        st.Action = a?.Clean();
    }

    // =====================================================================================
    // Wirkung
    // =====================================================================================

    static readonly HashSet<string> EffectTypes = new() { "window.open", "window.close", "window.foreground", "window.title", "focus", "ui.value", "ui.toggle", "ui.selection", "ui.expand", "ui.invoke", "ui.scroll", "ui.notification", "clipboard", "mouse.cursor" };

    void BindEffects()
    {
        var actions = _steps.Where(s => s.IsAction).OrderBy(s => s.TFrom).ToList();
        for (int i = 0; i < actions.Count; i++)
        {
            var st = actions[i];
            long start = _bySeq[st.AnchorSeq].Long("t");
            long lastInput = st.TTo;
            long next = i + 1 < actions.Count ? actions[i + 1].TFrom : long.MaxValue;
            var settle = _ev.Where(e => e.Str("type") == "frame" && e.Str("trigger") == "settle" && st.Input.Contains(e.Long("ref"))).Select(e => e.Long("tObs", e.Long("t"))).DefaultIfEmpty(0).Max();
            long end = Math.Min(next, Math.Max(lastInput + EffectWindowMs * 1000L, Math.Min(settle, lastInput + EffectSettleMaxMs * 1000L)));
            var gapStart = _gapSpans.Where(g => g.from > start && g.from < end && g.channels.Any(c => c?.GetValue<string>() is "windows" or "all")).Select(g => g.from).DefaultIfEmpty(long.MaxValue).Min();
            end = Math.Min(end, gapStart);
            var eff = new JsonObject();
            var opened = new JsonArray(); var closed = new JsonArray(); var titles = new JsonArray(); var values = new JsonArray(); var notes = new JsonArray(); var invoked = new JsonArray();
            long? firstEffect = null;
            JsonObject? lastFocus = null;
            foreach (var e in _ev)
            {
                long t = e.Long("t");
                if (t < start) continue;
                if (t > end) break;
                string type = e.Str("type") ?? "";
                if (!EffectTypes.Contains(type) || _consumed.Contains(e.Long("seq"))) continue;
                long after = t - lastInput;
                bool used = true;
                switch (type)
                {
                    case "window.open":
                        {
                            var w = Win(e.Str("win"));
                            opened.Add(new JsonObject { ["win"] = e.Str("win"), ["title"] = w?.Str("title"), ["kind"] = e.Str("kind"), ["afterUs"] = Math.Max(0, t - start) }.Clean());
                            break;
                        }
                    case "window.close":
                        {
                            var w = Win(e.Str("win"));
                            closed.Add(new JsonObject { ["win"] = e.Str("win"), ["title"] = w?.Str("title"), ["kind"] = w?.Str("kind"), ["afterUs"] = Math.Max(0, t - start) }.Clean());
                            break;
                        }
                    case "window.foreground":
                        eff["foreground"] = new JsonObject { ["from"] = e.Str("prev"), ["to"] = e.Str("win"), ["afterUs"] = Math.Max(0, t - start) }.Clean();
                        if (st.Kind == "windowSwitch") { st.InputObj["to"] = e.Str("win"); if (Win(e.Str("win"))?.Str("title") is { Length: > 0 } tt) st.InputObj["toTitle"] = tt; }
                        break;
                    case "window.title":
                        titles.Add(new JsonObject { ["win"] = e.Str("win"), ["to"] = e.Str("title"), ["afterUs"] = Math.Max(0, t - start) }.Clean());
                        break;
                    case "focus":
                        if (t >= lastInput) lastFocus = e; else used = st.Kind is "click" or "doubleClick";
                        break;
                    case "ui.value":
                        if (st.Kind == "type" && SameElement(e.Obj("el"), st.Target?.Obj("el"))) { used = true; break; }
                        if (t >= lastInput) values.Add(new JsonObject { ["el"] = e.Obj("el")?.DeepClone(), ["after"] = e.Str("value") }.Clean());
                        break;
                    case "ui.invoke": invoked.Add(e.Obj("el")?.DeepClone()); break;
                    case "ui.notification": if (e.Str("text") is { } nt) notes.Add(nt); break;
                    case "clipboard":
                        eff["clipboard"] = new JsonObject { ["clip"] = e.Str("clip"), ["kind"] = e.Str("kind") };
                        if (st.Kind == "hotkey") st.Clipboard = new JsonObject { ["op"] = st.InputObj.Str("commonMeaning") == "cut" ? "cut" : "copy", ["clip"] = e.Str("clip") };
                        break;
                    default: used = false; break;
                }
                if (used) { _consumed.Add(e.Long("seq")); st.Related.Add(e.Long("seq")); firstEffect ??= t; }
            }
            if (lastFocus != null) { eff["focus"] = new JsonObject { ["seq"] = lastFocus.Long("seq"), ["el"] = lastFocus.Obj("el")?.DeepClone(), ["ctx"] = lastFocus.Str("ctx") }.Clean(); _consumed.Add(lastFocus.Long("seq")); }
            if (opened.Count > 0) eff["windowsOpened"] = opened;
            if (closed.Count > 0) eff["windowsClosed"] = closed;
            if (titles.Count > 0) eff["titleChanges"] = titles;
            if (values.Count > 0) eff["values"] = values;
            if (invoked.Count > 0) eff["invoked"] = invoked;
            if (notes.Count > 0) eff["notifications"] = notes;
            // Bildschirmänderung aus dem Nachher-Bild
            var post = _ev.LastOrDefault(e => e.Str("type") == "frame" && e.Str("trigger") == "settle" && st.Input.Contains(e.Long("ref")));
            if (post != null)
            {
                double ratio = post.Obj("changed")?.Dbl("ratio") ?? 0;
                eff["screen"] = new JsonObject { ["changedRatio"] = ratio, ["settled"] = post.Obj("settle").Str("reason") != "timeout", ["settleUs"] = Math.Max(0, post.Long("tObs", post.Long("t")) - lastInput), ["settleReason"] = post.Obj("settle").Str("reason") };
                if (ratio > 0.0005) firstEffect ??= post.Long("t");
            }
            if (st.Kind == "hotkey" && st.InputObj.Str("commonMeaning") == "paste")
            {
                var clip = _ev.LastOrDefault(e => e.Str("type") == "clipboard" && e.Long("t") < start);
                if (clip != null) st.Clipboard = new JsonObject { ["op"] = "paste", ["clip"] = clip.Str("clip") };
            }
            bool observed = eff.Count > 0 && (eff.Count > 1 || eff["screen"] == null || (eff["screen"]?.AsObject().Dbl("changedRatio") ?? 0) > 0);
            eff["observed"] = observed;
            if (observed && firstEffect != null)
            {
                eff["latencyUs"] = Math.Max(0, firstEffect.Value - start);
                bool nextSoon = next != long.MaxValue && next - lastInput < EffectUncertainMs * 1000L;
                eff["causality"] = nextSoon || (st.Flags.Contains("opaque") && eff.Count <= 3) ? "plausible" : "direct";
            }
            else eff["causality"] = "none";
            st.Effect = eff;
        }
    }
}
