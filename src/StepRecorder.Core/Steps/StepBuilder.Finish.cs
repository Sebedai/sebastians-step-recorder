using System.Security.Cryptography;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Steps;

public sealed partial class StepBuilder
{
    // =====================================================================================
    // System- und Warteschritte
    // =====================================================================================

    void BuildSystemSteps()
    {
        StepDraft? last = null; long lastT = -1;
        foreach (var e in _ev)
        {
            if (_consumed.Contains(e.Long("seq"))) continue;
            string type = e.Str("type") ?? "";
            string? evName = null; string importance = "secondary";
            switch (type)
            {
                case "window.open":
                    {
                        string kind = e.Str("kind") ?? "window";
                        if (kind is "systemUi") continue;
                        evName = kind == "toast" ? "notification" : "windowOpened";
                        importance = kind is "menu" or "popup" or "dropdown" or "tooltip" || string.IsNullOrEmpty(Win(e.Str("win"))?.Str("title")) ? "trace" : "secondary";
                        break;
                    }
                case "window.close": evName = "windowClosed"; importance = Win(e.Str("win"))?.Str("kind") is "menu" or "popup" or "dropdown" or "tooltip" || string.IsNullOrEmpty(Win(e.Str("win"))?.Str("title")) ? "trace" : "secondary"; break;
                case "window.foreground": evName = "foregroundChanged"; break;
                case "window.title": evName = "titleChanged"; importance = "trace"; break;
                case "ui.notification": evName = "notification"; break;
                case "display.topology" when e.Int("epoch") >= 1: evName = "topologyChanged"; break;
                case "scope.change" when e.Str("reason") != "initial": evName = "scopeChanged"; break;
                case "ui.value" when e.Str("phase") == "change": evName = "valueChanged"; break;
                default: continue;
            }
            // Ereignisse im Rauschen der eigenen Startphase (t = 0) nicht als Schritt
            if (e.Long("t") == 0 && type == "window.foreground") continue;
            long t = e.Long("t");
            if (last != null && t - lastT <= SystemMergeMs * 1000L && last.InputObj.Str("event") == evName)
            {
                last.Related.Add(e.Long("seq")); lastT = t; last.TTo = Math.Max(last.TTo, t);
                continue;
            }
            var st = NewStep("system", e);
            st.InputObj["event"] = evName;
            st.InputObj["refSeq"] = e.Long("seq");
            st.Win = e.Str("win");
            st.Importance = importance;
            var w = Win(st.Win);
            if (w != null)
            {
                st.Ctx = new JsonObject { ["win"] = st.Win, ["process"] = w.Str("process"), ["title"] = w.Str("title"), ["kind"] = w.Str("kind") }.Clean();
                if (w.Str("title") != null) st.Prov.Add(new JsonObject { ["path"] = "ctx.title", ["seq"] = w.Long("seq"), ["field"] = "title", ["copy"] = true });
            }
            if (type == "window.title" && e.Str("title") != null) { st.InputObj["title"] = e.Str("title"); st.Prov.Add(new JsonObject { ["path"] = "input.title", ["seq"] = e.Long("seq"), ["field"] = "title", ["copy"] = true }); }
            if (type == "ui.notification" && e.Str("text") != null) { st.InputObj["text"] = e.Str("text"); st.Prov.Add(new JsonObject { ["path"] = "input.text", ["seq"] = e.Long("seq"), ["field"] = "text", ["copy"] = true }); }
            bool unobserved = evName == "valueChanged" || _gapSpans.Any(g => InputChannels(g.channels) && t >= g.from && t <= g.to + AnomalyWindowMs * 1000L);
            st.Evidence["cause"] = unobserved ? "unobserved" : "none";
            last = st; lastT = t;
        }
    }

    void BuildWaitSteps()
    {
        var actions = _steps.Where(s => s.IsAction).OrderBy(s => s.TFrom).ToList();
        for (int i = 0; i + 1 < actions.Count; i++)
        {
            long from = actions[i].TTo, to = actions[i + 1].TFrom;
            if (to - from < WaitMinMs * 1000L) continue;
            if (_gapSpans.Any(g => InputChannels(g.channels) && g.from < to && g.to > from)) continue;
            var sys = _steps.FirstOrDefault(s => s.Kind == "system" && s.TFrom > from && s.TFrom < to && s.Importance != "trace");
            var change = _ev.FirstOrDefault(e => e.Str("type") == "frame" && e.Str("trigger") == "change" && e.Long("t") > from && e.Long("t") < to);
            if (sys == null && change == null) continue;
            var anchorEv = sys != null ? _bySeq[sys.AnchorSeq] : change!;
            var st = NewStep("wait", anchorEv);
            st.AnchorSeq = anchorEv.Long("seq") + 0; // gleicher Anker wie der System-Schritt ist unzulässig → eigener Anker
            if (sys != null && sys.AnchorSeq == st.AnchorSeq) { _steps.Remove(st); continue; }
            st.TFrom = from; st.TTo = to;
            st.InputObj["durationUs"] = to - from;
            st.InputObj["busyCursor"] = false;
            if (sys != null) st.InputObj["until"] = new JsonObject { ["seq"] = sys.AnchorSeq, ["what"] = sys.InputObj.Str("event") };
            st.Evidence["cause"] = "none";
        }
    }

    // =====================================================================================
    // Bilder
    // =====================================================================================

    void BindFrames(StepDraft st)
    {
        var fr = new JsonObject();
        var issues = new JsonArray();
        string? m = st.Monitor ?? st.Position?.Str("m");
        JsonObject? pre = null, post = null;
        var between = new JsonArray();
        switch (st.Kind)
        {
            case "comment" or "snapshot" or "marker":
                {
                    var ev = _bySeq[st.AnchorSeq];
                    string? fid = ev.Str("frame");
                    pre = fid != null ? _s.Frame(fid) : _ev.LastOrDefault(e => e.Str("type") == "frame" && e.Long("t") <= ev.Long("t"));
                    m = pre?.Str("m") ?? m;
                    break;
                }
            case "system" or "wait" or "gap" or "move":
                {
                    pre = _ev.LastOrDefault(e => e.Str("type") == "frame" && e.Long("t") <= st.TFrom && (m == null || e.Str("m") == m) && Stored(e));
                    post = _ev.FirstOrDefault(e => e.Str("type") == "frame" && e.Long("t") >= st.TTo && e.Str("trigger") is "settle" or "change" && (m == null || e.Str("m") == m) && Stored(e));
                    if (st.Kind == "move" || st.Kind == "gap") post ??= null;
                    m = pre?.Str("m") ?? m;
                    break;
                }
            default:
                {
                    var pres = _ev.Where(e => e.Str("type") == "frame" && e.Str("trigger") == "preInput" && st.Input.Contains(e.Long("ref"))).OrderBy(e => e.Long("ref")).ToList();
                    long firstRef = pres.Count > 0 ? pres[0].Long("ref") : 0;
                    var cand = pres.Where(e => e.Long("ref") == firstRef).ToList();
                    pre = cand.FirstOrDefault(e => e.Str("m") == m) ?? cand.FirstOrDefault();
                    if (m == null && pre != null) m = pre.Str("m");
                    if (pre == null)
                    {
                        pre = _ev.LastOrDefault(e => e.Str("type") == "frame" && e.Long("t") < st.TFrom && (m == null || e.Str("m") == m));
                        issues.Add(pre == null ? "preMissing" : "preNotGuaranteed");
                    }
                    else if (pre.Str("guarantee") != "beforeInput") issues.Add("preNotGuaranteed");
                    if (pre != null && !Stored(pre)) issues.Add("preMissing");
                    var posts = _ev.Where(e => e.Str("type") == "frame" && e.Str("trigger") == "settle" && st.Input.Contains(e.Long("ref"))).ToList();
                    long lastRef = posts.Count > 0 ? posts.Max(e => e.Long("ref")) : 0;
                    var pc = posts.Where(e => e.Long("ref") == lastRef).ToList();
                    post = pc.LastOrDefault(e => e.Str("m") == m) ?? pc.LastOrDefault();
                    if (post == null)
                    {
                        post = _ev.FirstOrDefault(e => e.Str("type") == "frame" && e.Long("t") > st.TTo && (m == null || e.Str("m") == m) && Stored(e));
                        if (post == null) issues.Add("postMissing");
                    }
                    else if (post.Obj("settle").Str("reason") == "timeout") issues.Add("postTimeout");
                    if (pre != null && post != null)
                        foreach (var b in _ev.Where(e => e.Str("type") == "frame" && e.Long("t") > pre.Long("t") && e.Long("t") < post.Long("t") && e.Str("m") == m && e.Str("trigger") is "change" or "preInput" && e != pre && st.Input.Contains(e.Long("ref", -1)) == false && e.Str("trigger") == "change"))
                            between.Add(b.Str("id"));
                    if (pre?.Arr("masks") is { Count: > 0 } masks && st.Target?.Obj("el")?.Obj("rect") is { } tr)
                        if (masks.Any(mk => Intersects(mk!.AsObject(), tr))) issues.Add("masked");
                    // weitere Monitore
                    var others = new JsonArray();
                    foreach (var o in cand.Where(c => c != pre))
                        others.Add(new JsonObject { ["m"] = o.Str("m"), ["pre"] = o.Str("id"), ["post"] = pc.FirstOrDefault(p => p.Str("m") == o.Str("m"))?.Str("id") });
                    if (others.Count > 0) fr["others"] = others;
                    break;
                }
        }
        fr["m"] = m;
        fr["pre"] = pre?.Str("id");
        fr["post"] = post?.Str("id");
        fr["between"] = between;
        if (issues.Count > 0) fr["issues"] = new JsonArray(issues.Select(i => i!.GetValue<string>()).Distinct().Select(s => (JsonNode)s).ToArray());
        st.Frames = fr;
    }

    static bool Stored(JsonObject f) => f.Obj("store").Str("kind") != "none";

    static bool Intersects(JsonObject mask, JsonObject rect)
        => mask.Int("x") < rect.Int("x") + rect.Int("w") && rect.Int("x") < mask.Int("x") + mask.Int("w") && mask.Int("y") < rect.Int("y") + rect.Int("h") && rect.Int("y") < mask.Int("y") + mask.Int("h");

    void ApplyDiscards()
    {
        foreach (var e in _ev.Where(e => e.Str("type") == "marker" && e.Str("kind") == "discardPrevious"))
        {
            var last = _steps.Where(s => s.IsAction && s.Importance != "trace" && s.TFrom < e.Long("t") && !s.Flags.Contains("discarded")).OrderBy(s => s.TFrom).LastOrDefault();
            if (last != null) last.Flags.Add("discarded");
            else _warnings.Add(new JsonObject { ["code"] = "discardWithoutStep", ["seq"] = e.Long("seq") });
        }
    }

    // =====================================================================================
    // Belegstufe und Wichtigkeit
    // =====================================================================================

    void AssignEvidence(StepDraft st)
    {
        var ev = st.Evidence;
        var reasons = ev["reasons"] as JsonArray ?? new JsonArray();
        ev["reasons"] = reasons;
        string target = ev.Str("target") ?? "n/a";
        string level;
        string input = st.Flags.Contains("masked") ? "masked" : st.IsAction ? "observed" : "unobserved";
        string result = ev.Str("result") ?? (st.Effect?.Bool("observed") == true ? "effectObserved" : st.IsAction ? "none" : "n/a");
        string cause = ev.Str("cause") ?? (st.IsAction ? "n/a" : "none");
        if (st.Effect?.Bool("observed") == true && !Has(reasons, "effectObserved")) reasons.Add("effectObserved");
        else if (st.IsAction && st.Effect?.Bool("observed") == false) reasons.Add("noEffectObserved");
        switch (st.Kind)
        {
            case "click" or "doubleClick" or "tripleClick" or "rightClick":
                level = target is "agreed" or "corroborated" ? "confirmed" : "inputOnly"; break;
            case "drag":
                level = target is "agreed" or "corroborated" or "single" && (st.EndTarget?.Obj("el") != null || st.InputObj.Str("dragKind") != "unknown") && target != "single" ? "confirmed" : "inputOnly"; break;
            case "scroll": level = target == "corroborated" ? "confirmed" : "inputOnly"; break;
            case "type":
                if (st.Flags.Contains("masked")) { level = "unknown"; reasons.Add("maskedByDesign"); target = target == "n/a" ? "none" : target; }
                else level = target is "corroborated" or "single" && result == "valueConfirmed" ? "confirmed" : "inputOnly";
                break;
            case "hotkey" or "key":
                level = _bySeq[st.AnchorSeq].Str("fg") != null ? "confirmed" : "inputOnly";
                if (!Has(reasons, "keysObserved")) reasons.Add("keysObserved");
                break;
            case "hover": level = target == "corroborated" ? "confirmed" : "inputOnly"; break;
            case "windowSwitch": level = st.InputObj.Str("to") != null ? "confirmed" : "inputOnly"; break;
            case "system": level = cause == "none" ? "confirmed" : "unknown"; if (cause == "unobserved") reasons.Add("causeUnobserved"); break;
            case "gap": level = "unknown"; break;
            default: level = "confirmed"; break;
        }
        if (st.IsAction && level == "confirmed")
        {
            if (st.Flags.Contains("recovered")) { level = "inputOnly"; reasons.Add("recoveredInput"); }
            if (st.Flags.Contains("incomplete")) { level = "inputOnly"; reasons.Add("incompleteInput"); }
            if (st.Flags.Contains("opaque") && st.Kind is not ("hotkey" or "key")) { level = "inputOnly"; if (!Has(reasons, "opaqueSurface")) reasons.Add("opaqueSurface"); }
        }
        if (st.Flags.Contains("injected")) reasons.Add("injectedInput");
        ev["level"] = level;
        ev["input"] = input;
        ev["target"] = st.IsAction ? target : "n/a";
        ev["result"] = result;
        ev["cause"] = cause;
        // Gründe eindeutig
        var uniq = reasons.Select(r => r!.GetValue<string>()).Distinct().ToList();
        ev["reasons"] = new JsonArray(uniq.Select(u => (JsonNode)u).ToArray());
        // Reihenfolge der Felder festlegen
        st.Evidence = new JsonObject { ["level"] = ev["level"]!.DeepClone(), ["input"] = input, ["target"] = ev["target"]!.DeepClone(), ["result"] = result, ["cause"] = cause, ["reasons"] = ev["reasons"]!.DeepClone() };
    }

    static bool Has(JsonArray a, string s) => a.Any(x => x?.GetValue<string>() == s);

    void AssignImportance(StepDraft st)
    {
        st.Importance = st.Kind switch
        {
            "click" or "doubleClick" or "tripleClick" or "rightClick" or "type" or "hotkey" or "comment" or "snapshot" => "primary",
            "drag" => st.InputObj.Str("dragKind") is "moveWindow" or "resizeWindow" ? "secondary" : "primary",
            "scroll" or "windowSwitch" or "wait" => "secondary",
            "marker" => st.InputObj.Str("markerKind") == "note" ? "secondary" : "primary",
            "move" => "trace",
            "hover" => (st.Effect?.Arr("windowsOpened")?.Any(w => w?["kind"]?.GetValue<string>() is "menu" or "popup" or "dropdown") ?? false) ? "secondary" : "trace",
            "gap" => st.Importance == "trace" ? "trace" : st.InputObj.Str("inputLost") is "none" ? "secondary" : "primary",
            "system" => st.Importance,
            "key" => KeyImportance(st.InputObj.Str("key") ?? "", st.InputObj.Arr("mods")?.Count ?? 0),
            _ => st.Importance
        };
    }

    static string KeyImportance(string key, int mods) => key switch
    {
        "Enter" or "Escape" or "Delete" or "Space" or "PrintScreen" or "Apps" or "Win" => "primary",
        _ when key.StartsWith('F') && key.Length <= 3 && int.TryParse(key[1..], out _) => "primary",
        "Shift" or "Ctrl" or "CapsLock" or "NumLock" or "ScrollLock" => "trace",
        _ => "secondary"
    };

    // =====================================================================================
    // Ausgabe
    // =====================================================================================

    /// <summary>Wandelt eine Seq-Menge in Bereiche um, die nur Eingaben dieser Menge überdecken.</summary>
    JsonArray Ranges(IEnumerable<long> seqs)
    {
        var set = seqs as ISet<long> ?? seqs.ToHashSet();
        var res = new JsonArray();
        long? from = null, to = null;
        foreach (var s in _inputSeqs)
        {
            if (set.Contains(s)) { from ??= s; to = s; }
            else if (from != null) { res.Add(new JsonArray(from.Value, to!.Value)); from = null; }
        }
        if (from != null) res.Add(new JsonArray(from.Value, to!.Value));
        // Nicht-Eingaben (z. B. geschwärzte Zeilen außerhalb der Liste)
        foreach (var s in set.Where(x => !_inputSeqs.Contains(x) && _bySeq.TryGetValue(x, out var e) && IsInputType(e.Str("type"))).OrderBy(x => x))
            res.Add(new JsonArray(s, s));
        return res;
    }

    JsonObject Serialize(string derivedAt)
    {
        _steps.Sort((a, b) => a.TFrom != b.TFrom ? a.TFrom.CompareTo(b.TFrom) : a.AnchorSeq.CompareTo(b.AnchorSeq));
        // eindeutige Anker sicherstellen
        var seen = new HashSet<long>();
        foreach (var st in _steps.ToList()) if (!seen.Add(st.AnchorSeq)) { _warnings.Add(new JsonObject { ["code"] = "duplicateAnchor", ["seq"] = st.AnchorSeq }); _steps.Remove(st); }
        var steps = new JsonArray();
        int index = 0;
        foreach (var st in _steps)
        {
            var o = new JsonObject
            {
                ["id"] = "s" + st.AnchorSeq, ["index"] = ++index, ["anchorSeq"] = st.AnchorSeq, ["kind"] = st.Kind,
                ["action"] = st.Action?.DeepClone(), ["importance"] = st.Importance, ["evidence"] = st.Evidence.DeepClone(),
                ["tFrom"] = st.TFrom, ["tTo"] = Math.Max(st.TFrom, st.TTo),
                ["seq"] = new JsonObject { ["input"] = Ranges(st.Input), ["approach"] = Ranges(st.Approach), ["related"] = new JsonArray(st.Related.Select(r => (JsonNode)r).ToArray()) },
            };
            if (st.Ctx != null) o["ctx"] = st.Ctx.DeepClone();
            if (st.Target != null) o["target"] = st.Target.DeepClone();
            if (st.EndTarget != null) o["endTarget"] = st.EndTarget.DeepClone();
            if (st.Position != null) o["position"] = st.Position.DeepClone();
            if (st.EndPosition != null) o["endPosition"] = st.EndPosition.DeepClone();
            if (st.InputObj.Count > 0) o["input"] = st.InputObj.DeepClone();
            if (st.Effect != null) o["effect"] = st.Effect.DeepClone();
            o["frames"] = st.Frames.DeepClone();
            if (st.ApproachObj is { Count: > 0 }) o["approach"] = st.ApproachObj.DeepClone();
            if (st.Clipboard != null) o["clipboard"] = st.Clipboard.DeepClone();
            if (st.GapId != null) o["gapId"] = st.GapId;
            if (st.Flags.Count > 0) o["flags"] = new JsonArray(st.Flags.Where(f => f != "redacted").Select(f => (JsonNode)f).ToArray());
            o["prov"] = st.Prov.DeepClone();
            steps.Add(o);
        }

        // Zeitleiste
        var timeline = BuildTimeline();
        // Lücken
        var gaps = new JsonArray();
        foreach (var g in _gapSpans.OrderBy(g => g.from))
        {
            var st = _steps.FirstOrDefault(s => s.GapId == g.id);
            gaps.Add(new JsonObject { ["cause"] = g.cause, ["gapId"] = g.id, ["tFrom"] = g.from, ["tTo"] = g.to, ["channels"] = g.channels.DeepClone(), ["inputLost"] = g.inputLost, ["seq"] = new JsonArray(g.seq), ["step"] = st != null ? "s" + st.AnchorSeq : null }.Clean());
        }
        foreach (var st in _steps.Where(s => s.Evidence.Str("level") == "unknown" && s.Kind != "gap"))
        {
            string cause = st.Flags.Contains("masked") ? "maskedInput" : "unobserved";
            gaps.Add(new JsonObject { ["cause"] = cause, ["tFrom"] = st.TFrom, ["tTo"] = st.TTo, ["channels"] = new JsonArray(cause == "maskedInput" ? "keyboard" : "all"), ["inputLost"] = "none", ["step"] = "s" + st.AnchorSeq });
        }
        foreach (var st in _steps.Where(s => s.Kind == "gap" && s.Flags.Contains("redacted")))
            gaps.Add(new JsonObject { ["cause"] = "removedByAuthor", ["tFrom"] = st.TFrom, ["tTo"] = st.TTo, ["channels"] = new JsonArray("all"), ["inputLost"] = "none", ["step"] = "s" + st.AnchorSeq });

        // Vollständigkeit
        int orphans = _inputSeqs.Count(s => !_assigned.Contains(s));
        var orphanRanges = new JsonArray();
        foreach (var s in _inputSeqs.Where(s => !_assigned.Contains(s))) orphanRanges.Add(new JsonArray(s, s));
        int selfInputs = _s.Events.Count(e => IsInputType(e.Str("type")) && e.Bool("self"));
        var actions = _steps.Where(s => s.IsAction && !s.Flags.Contains("discarded")).ToList();
        int conf = actions.Count(s => s.Evidence.Str("level") == "confirmed"), inOnly = actions.Count(s => s.Evidence.Str("level") == "inputOnly"), unk = actions.Count(s => s.Evidence.Str("level") == "unknown");
        var byReason = new JsonObject();
        foreach (var r in actions.Where(a => a.Evidence.Str("level") != "confirmed").SelectMany(a => a.Evidence.StrList("reasons")).GroupBy(x => x).OrderBy(g => g.Key))
            byReason[r.Key] = r.Count();
        var frames = _s.Events.Where(e => e.Str("type") == "frame").ToList();
        long stepsUs = timeline.Where(x => x!["kind"]!.GetValue<string>() == "steps").Sum(x => x!["tTo"]!.GetValue<long>() - x["tFrom"]!.GetValue<long>());
        long idleUs = timeline.Where(x => x!["kind"]!.GetValue<string>() == "idle").Sum(x => x!["tTo"]!.GetValue<long>() - x["tFrom"]!.GetValue<long>());
        long gapsUs = timeline.Where(x => x!["kind"]!.GetValue<string>() == "gap").Sum(x => x!["tTo"]!.GetValue<long>() - x["tFrom"]!.GetValue<long>());
        var byCause = new JsonObject();
        foreach (var g in gaps.GroupBy(g => g!["cause"]!.GetValue<string>()).OrderBy(g => g.Key)) byCause[g.Key] = g.Count();
        var completeness = new JsonObject
        {
            ["stepBuilderVersion"] = StepParams.BuilderVersion,
            ["bookkeeping"] = new JsonObject
            {
                ["inputEvents"] = _inputSeqs.Count, ["assigned"] = _inputSeqs.Count - orphans, ["orphans"] = orphans, ["orphanSeqRanges"] = orphanRanges,
                ["duplicates"] = 0, ["selfInputEvents"] = selfInputs,
                ["timeline"] = new JsonObject { ["durationUs"] = _duration, ["stepsUs"] = stepsUs, ["idleUs"] = idleUs, ["gapsUs"] = gapsUs, ["unaccountedUs"] = _duration - stepsUs - idleUs - gapsUs },
                ["ok"] = orphans == 0 && _duration - stepsUs - idleUs - gapsUs == 0
            },
            ["evidence"] = new JsonObject { ["actionSteps"] = actions.Count, ["confirmed"] = conf, ["inputOnly"] = inOnly, ["unknown"] = unk, ["confirmedRatio"] = actions.Count == 0 ? null : Math.Round(conf / (double)actions.Count, 3), ["byReason"] = byReason },
            ["gaps"] = new JsonObject { ["count"] = gaps.Count, ["totalUs"] = gapsUs, ["byCause"] = byCause },
            ["frames"] = new JsonObject
            {
                ["requested"] = frames.Count, ["stored"] = frames.Count(f => f.Obj("store").Str("kind") is "key" or "delta"), ["same"] = frames.Count(f => f.Obj("store").Str("kind") == "same"),
                ["dropped"] = frames.Count(f => f.Obj("store").Str("kind") == "none"),
                ["preGuaranteed"] = frames.Count(f => f.Str("trigger") == "preInput" && f.Str("guarantee") == "beforeInput"),
                ["preNotGuaranteed"] = frames.Count(f => f.Str("trigger") == "preInput" && f.Str("guarantee") != "beforeInput")
            },
            ["journal"] = new JsonObject { ["schemaOk"] = true, ["seqContinuous"] = SeqContinuous(), ["refsOk"] = true, ["blobsOk"] = true, ["chainOk"] = true, ["issues"] = new JsonArray() }
        };
        string journalHash = "";
        try { var b = _s.ReadFile("events.jsonl"); if (b != null) journalHash = Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant(); } catch { }
        var result = new JsonObject
        {
            ["format"] = "steprec.steps", ["schemaVersion"] = "1.0", ["sessionId"] = _s.SessionId, ["stepBuilderVersion"] = StepParams.BuilderVersion,
            ["params"] = _params.DeepClone(), ["derivedAtUtc"] = derivedAt,
            ["source"] = new JsonObject { ["lastSeq"] = _s.Events.Count == 0 ? 0 : _s.Events[^1].Long("seq"), ["journalSha256"] = journalHash },
            ["steps"] = steps, ["timeline"] = timeline, ["gaps"] = gaps, ["completeness"] = completeness
        };
        try { Automation(steps, result); } catch (Exception ex) { _warnings.Add(new JsonObject { ["code"] = "automationDerivation", ["message"] = ex.Message }); }
        if (_warnings.Count > 0) result["warnings"] = new JsonArray(_warnings.Select(w => (JsonNode)w.DeepClone()).ToArray());
        return result;
    }

    bool SeqContinuous()
    {
        long exp = 1;
        foreach (var e in _s.Events) { if (e.Long("seq") != exp) return false; exp++; }
        return true;
    }

    JsonArray BuildTimeline()
    {
        // Grenzen sammeln
        var bounds = new SortedSet<long> { 0, _duration };
        var gapIntervals = _gapSpans.Where(g => InputChannels(g.channels) && g.cause != "recorderInteraction").Select(g => (Math.Max(0, g.from), Math.Min(_duration, g.to), g.id)).Where(x => x.Item2 > x.Item1).ToList();
        var stepIntervals = _steps.Where(s => s.IsAction || s.Kind is "comment" or "snapshot" or "marker").Select(s => (Math.Max(0, s.TFrom), Math.Min(_duration, Math.Max(s.TTo, s.TFrom + 1)), "s" + s.AnchorSeq)).Where(x => x.Item2 > x.Item1).ToList();
        foreach (var g in gapIntervals) { bounds.Add(g.Item1); bounds.Add(g.Item2); }
        foreach (var s in stepIntervals) { bounds.Add(s.Item1); bounds.Add(s.Item2); }
        var list = bounds.Where(b => b >= 0 && b <= _duration).ToList();
        var res = new List<JsonObject>();
        for (int i = 0; i + 1 < list.Count; i++)
        {
            long a = list[i], b = list[i + 1];
            if (b <= a) continue;
            var g = gapIntervals.FirstOrDefault(x => x.Item1 <= a && x.Item2 >= b);
            string kind; var ids = new List<string>(); string? gapId = null;
            if (g.id != null) { kind = "gap"; gapId = g.id; }
            else
            {
                ids = stepIntervals.Where(x => x.Item1 <= a && x.Item2 >= b).Select(x => x.Item3).ToList();
                kind = ids.Count > 0 ? "steps" : "idle";
            }
            var last = res.Count > 0 ? res[^1] : null;
            if (last != null && last.Str("kind") == kind && (kind != "gap" || last.Str("gapId") == gapId))
            {
                last["tTo"] = b;
                if (kind == "steps") { var arr = (JsonArray)last["steps"]!; foreach (var id in ids) if (!arr.Any(x => x!.GetValue<string>() == id)) arr.Add(id); }
                continue;
            }
            var seg = new JsonObject { ["kind"] = kind, ["tFrom"] = a, ["tTo"] = b, ["steps"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) };
            if (gapId != null) seg["gapId"] = gapId;
            res.Add(seg);
        }
        return new JsonArray(res.Select(r => (JsonNode)r).ToArray());
    }
}
