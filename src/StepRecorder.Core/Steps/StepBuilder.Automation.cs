using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Steps;

/// <summary>
/// Angaben für Automatisierung und KI-gestützte Skripterstellung (AutoHotkey):
/// je Schritt <c>sync</c> (Wartesignale), <c>verify</c> (Zustand danach, Änderungen), <c>selectors</c> (Wege zum Element,
/// nach Stabilität geordnet), <c>menuPath</c>, <c>choose</c> (Listenauswahl), <c>data</c> (Datenquellen);
/// für die Aufnahme <c>parameters</c> (veränderliche Werte) und <c>repetitions</c> (wiederholte Schrittfolgen).
/// </summary>
public sealed partial class StepBuilder
{
    static readonly HashSet<string> ActionKinds = new() { "click", "doubleClick", "tripleClick", "rightClick", "drag", "scroll", "type", "hotkey", "key", "hover", "windowSwitch" };

    void Automation(JsonArray steps, JsonObject result)
    {
        var frames = _ev.Where(e => e.Str("type") == "frame" && e.Str("id") != null).GroupBy(e => e.Str("id")!).ToDictionary(g => g.Key, g => g.First());
        var cursor = _ev.Where(e => e.Str("type") == "cursor").ToList();
        var states = _ev.Where(e => e.Str("type") == "win32.state").ToList();
        var clips = _ev.Where(e => e.Str("type") == "clipboard").ToList();
        var actions = steps.OfType<JsonObject>().Where(s => ActionKinds.Contains(s.Str("kind") ?? "")).ToList();
        var parameters = new JsonArray();
        var menuChain = new List<string>(); long menuT = 0;

        for (int i = 0; i < actions.Count; i++)
        {
            var s = actions[i];
            long tTo = s.Long("tTo"), next = i + 1 < actions.Count ? actions[i + 1].Long("tFrom") : long.MaxValue;
            string kind = s.Str("kind")!;

            // ---------- sync: worauf nach dem Schritt gewartet werden muss ----------
            var sync = new JsonObject();
            long ready = tTo;
            if (s.Obj("frames")?.Str("post") is { } pid && frames.TryGetValue(pid, out var pf))
            {
                long settle = Math.Max(0, pf.Long("t") - tTo);
                sync["settleMs"] = settle / 1000;
                if (pf.Obj("settle")?.Str("reason") is { } why) sync["settleReason"] = why;   // quiet | maxWait | nextInput
                ready = Math.Max(ready, pf.Long("t"));
            }
            var busyOn = cursor.FirstOrDefault(c => c.Bool("busy") && c.Long("t") >= s.Long("tFrom") && c.Long("t") < Math.Min(next, tTo + 10_000_000));
            if (busyOn != null)
            {
                var busyOff = cursor.FirstOrDefault(c => !c.Bool("busy") && c.Long("t") > busyOn.Long("t"));
                long end = busyOff?.Long("t") ?? Math.Min(next, busyOn.Long("t") + 30_000_000);
                sync["busyMs"] = (end - busyOn.Long("t")) / 1000;
                sync["busyShape"] = busyOn.Str("shape");
                ready = Math.Max(ready, end);
            }
            if (s.Obj("effect")?.Arr("windowsOpened") is { Count: > 0 } wo && wo.OfType<JsonObject>().FirstOrDefault(w => w.Str("kind") is not ("menu" or "popup" or "dropdown" or "tooltip")) is { } w0)
            {
                sync["waitWindow"] = new JsonObject { ["title"] = w0.Str("title"), ["afterMs"] = w0.Long("afterUs") / 1000 }.Clean();
                ready = Math.Max(ready, tTo + w0.Long("afterUs"));
            }
            if (next != long.MaxValue) { sync["readyMs"] = (ready - tTo) / 1000; sync["thinkMs"] = Math.Max(0, next - ready) / 1000; }
            if (sync.Count > 0) s["sync"] = sync;

            // ---------- verify: Zustand danach und was sich geändert hat ----------
            var before = states.LastOrDefault(e => e.Long("t") <= s.Long("tFrom"))?.Obj("state");
            var after = states.FirstOrDefault(e => e.Long("t") > tTo && e.Long("t") <= Math.Max(next, tTo + 1)) ?.Obj("state");
            if (after != null)
            {
                var v = new JsonObject { ["title"] = after.Str("title"), ["class"] = after.Str("class"), ["exe"] = after.Str("exe"), ["focus"] = after.Str("focus") };
                var tw = s.Obj("target")?.Obj("win32");
                var basis = before ?? (tw != null ? new JsonObject { ["title"] = tw.Obj("window")?.Str("title"), ["hwnd"] = tw.Obj("window")?.Str("hwnd"), ["texts"] = tw.Arr("texts")?.DeepClone() } : null);
                if (basis != null)
                {
                    if (basis.Str("title") != after.Str("title")) v["titleChanged"] = new JsonObject { ["from"] = basis.Str("title"), ["to"] = after.Str("title") };
                    if (basis.Str("hwnd") != null && basis.Str("hwnd") == after.Str("hwnd") || basis.Str("title") == after.Str("title"))
                        if (TextChanges(basis.Arr("texts"), after.Arr("texts")) is { Count: > 0 } ch) v["changes"] = ch;
                }
                ContextVerify(v, before, after);
                s["verify"] = v.Clean();
            }

            // ---------- selectors: Wege zum Element ----------
            if (Selectors(s) is { Count: > 0 } sel) s["selectors"] = sel;

            // ---------- Menüpfad und Listenauswahl ----------
            var el = s.Obj("target")?.Obj("el");
            string ct = el?.Str("ct") ?? "";
            if (kind == "click" && ct == "MenuItem" && el?.Str("name") is { Length: > 0 } mn)
            {
                bool inBar = s.Obj("target")?.Arr("path")?.Any(p => p.Str("ct") == "MenuBar") == true;
                if (inBar || menuChain.Count == 0 || s.Long("tFrom") - menuT > 8_000_000) menuChain.Clear();
                menuChain.Add(mn); menuT = s.Long("tFrom");
                var mp = new JsonObject { ["items"] = new JsonArray(menuChain.Select(x => (JsonNode)x).ToArray()) };
                if (s.Obj("target")?.Obj("win32")?.Obj("window")?.Str("class") == "#32768" || inBar && s.Obj("target")?.Obj("el")?.Str("fw") == "Win32") mp["win32Menu"] = true;
                if (el.Str("accel") is { } acc) mp["accelerator"] = acc;
                s["menuPath"] = mp;
            }
            else if (kind is "click" or "doubleClick" && menuChain.Count > 0) menuChain.Clear();
            if (kind is "click" or "doubleClick" && ct is "ListItem" or "DataItem" or "TreeItem" or "TabItem" or "RadioButton" && el?.Str("name") is { Length: > 0 } item)
            {
                var container = s.Obj("target")?.Arr("path")?.OfType<JsonObject>().LastOrDefault(p => p.Str("ct") is "List" or "ComboBox" or "Tree" or "DataGrid" or "Table" or "Tab" or "Group");
                var ch = new JsonObject { ["item"] = item, ["itemType"] = ct, ["index"] = el.LongN("idx"), ["of"] = el.LongN("sibCount") };
                if (container != null) ch["container"] = new JsonObject { ["ct"] = container.Str("ct"), ["name"] = container.Str("name"), ["aid"] = container.Str("aid"), ["cls"] = container.Str("cls") }.Clean();
                if (s.Obj("target")?.Obj("win32")?.Obj("control") is { } wc && wc.Str("class") is { } wcls && (wcls.Contains("ComboBox", StringComparison.OrdinalIgnoreCase) || wcls.Contains("ListBox", StringComparison.OrdinalIgnoreCase) || wcls.Contains("ComboLBox", StringComparison.OrdinalIgnoreCase)))
                    ch["win32Control"] = wc.Str("classNN");
                s["choose"] = ch.Clean();
            }

            // ---------- Anwendungskontext: Explorer, Browser, Office, Listen, Menü-Befehls-IDs ----------
            ContextKeys(s, kind, before, after);

            // ---------- Datenquellen ----------
            var data = new JsonObject();
            var input = s.Obj("input");
            if (kind == "hotkey" && input?.Str("commonMeaning") == "paste")
            {
                var clip = clips.LastOrDefault(c => c.Long("t") <= s.Long("tFrom"));
                if (clip != null)
                {
                    var owner = Win(clip.Str("ownerWin"));
                    data["pasteFrom"] = new JsonObject { ["process"] = owner?.Str("process"), ["title"] = owner?.Str("title"), ["kind"] = clip.Str("kind"), ["clip"] = clip.Str("clip") }.Clean();
                }
            }
            var stNear = states.LastOrDefault(e => e.Long("t") <= Math.Min(next, tTo + 2_000_000) && e.Obj("state")?.Str("class") == "#32770")?.Obj("state");
            if (stNear?.Arr("texts") is { } dlgTexts && dlgTexts.OfType<JsonObject>().Select(x => x.Str("t") ?? "").FirstOrDefault(x => x.StartsWith("Adresse:") || x.StartsWith("Address:")) is { } addr)
            {
                var fd = new JsonObject { ["folder"] = addr[(addr.IndexOf(':') + 1)..].Trim(), ["dialog"] = stNear.Str("title") };
                if (kind == "type" && input?.Str("text") is { Length: > 0 } fn) fd["fileName"] = fn;
                data["fileDialog"] = fd;
            }
            // Dateidialog direkt aus dem Win32-Kontext (Adressleiste, Dateinamenfeld)
            var fdw = s.Obj("target")?.Obj("win32")?.Obj("fileDialog") ?? (stNear?.Obj("fileDialog"));
            if (fdw != null)
            {
                var fd = data.Obj("fileDialog") ?? new JsonObject { ["dialog"] = s.Obj("target")?.Obj("win32")?.Obj("window")?.Str("title") ?? stNear?.Str("title") };
                if (fd.Str("folder") == null && fdw.Str("folder") is { } fold) fd["folder"] = fold;
                if (fdw.Bool("isPath")) fd["isPath"] = true;
                if (fdw.Str("fileNameControl") is { } fnc) fd["fileNameControl"] = fnc;
                if (kind == "type" && fd.Str("fileName") == null && input?.Str("text") is { Length: > 0 } fn2) fd["fileName"] = fn2;
                if (fd.Str("folder") != null || fd.Str("fileNameControl") != null) data["fileDialog"] = fd.Clean();
            }
            if (data.Count > 0) s["data"] = data;

            // ---------- Parameter ----------
            if (kind == "type" && input != null)
            {
                string field = el?.Str("name") is { Length: > 0 } fname ? fname.TrimEnd(':', ' ') : s.Obj("target")?.Obj("win32")?.Obj("control")?.Str("classNN") ?? "Eingabe";
                var p = new JsonObject { ["name"] = ParamName(field, parameters), ["step"] = s.Str("id"), ["field"] = field };
                if (input.Bool("masked")) p["secret"] = true;
                else
                {
                    p["value"] = input.Str("text");
                    if (data.Obj("fileDialog") != null) p["kind"] = "fileName";
                }
                if (input.Arr("tokens")?.OfType<JsonObject>().Any(t => t.ContainsKey("placeholder")) == true) p["generalized"] = true;
                parameters.Add(p.Clean());
            }
            else if (data.Obj("pasteFrom") is { } pf2)
                parameters.Add(new JsonObject { ["name"] = ParamName("Einfügen", parameters), ["step"] = s.Str("id"), ["field"] = el?.Str("name"), ["source"] = "clipboard", ["from"] = pf2.Str("process") }.Clean());
        }
        if (parameters.Count > 0) result["parameters"] = parameters;
        if (Repetitions(actions) is { Count: > 0 } reps) result["repetitions"] = reps;
    }

    static string ParamName(string field, JsonArray existing)
    {
        var baseName = new string(field.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray()).Trim();
        baseName = string.Concat(baseName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        if (baseName.Length == 0 || char.IsDigit(baseName[0])) baseName = "Wert" + baseName;
        if (baseName.Length > 30) baseName = baseName[..30];
        string name = baseName; int n = 2;
        while (existing.Any(p => p.Str("name") == name)) name = baseName + n++;
        return name;
    }

    /// <summary>Unterschiede der Fenstertexte (nach ClassNN): geänderter Text, Häkchen, neu, verschwunden – höchstens 12.</summary>
    static JsonArray TextChanges(JsonArray? a, JsonArray? b)
    {
        var res = new JsonArray();
        if (a == null || b == null) return res;
        var ma = a.OfType<JsonObject>().Where(x => x.Str("c") != null).GroupBy(x => x.Str("c")!).ToDictionary(g => g.Key, g => g.First());
        var mb = b.OfType<JsonObject>().Where(x => x.Str("c") != null).GroupBy(x => x.Str("c")!).ToDictionary(g => g.Key, g => g.First());
        foreach (var (c, nb) in mb)
        {
            if (res.Count >= 12) break;
            if (!ma.TryGetValue(c, out var na)) { res.Add(new JsonObject { ["c"] = c, ["appeared"] = nb.Str("t") }); continue; }
            if (na.Str("t") != nb.Str("t")) res.Add(new JsonObject { ["c"] = c, ["from"] = na.Str("t"), ["to"] = nb.Str("t") });
            if (na.Str("checked") != nb.Str("checked") && nb.Str("checked") != null) res.Add(new JsonObject { ["c"] = c, ["text"] = nb.Str("t"), ["checked"] = nb.Str("checked") });
            if (na["enabled"]?.GetValue<bool>() != nb["enabled"]?.GetValue<bool>()) res.Add(new JsonObject { ["c"] = c, ["text"] = nb.Str("t"), ["enabled"] = nb["enabled"]?.GetValue<bool>() ?? true });
        }
        foreach (var (c, na) in ma) { if (res.Count >= 12) break; if (!mb.ContainsKey(c)) res.Add(new JsonObject { ["c"] = c, ["disappeared"] = na.Str("t") }); }
        return res;
    }

    static bool VolatileClass(string? cls) => cls != null && (cls.StartsWith("WindowsForms10.", StringComparison.Ordinal) || cls.StartsWith("HwndWrapper[", StringComparison.Ordinal)
        || cls.StartsWith("Afx:", StringComparison.Ordinal) || cls.StartsWith("ATL:", StringComparison.Ordinal));

    /// <summary>Wege zum Element, nach Stabilität geordnet (score 5 = sehr stabil, 1 = nur Notbehelf).</summary>
    static JsonArray Selectors(JsonObject s)
    {
        var list = new List<JsonObject>();
        var tg = s.Obj("target");
        var w = tg?.Obj("win32");
        var el = tg?.Obj("el");
        var ctl = w?.Obj("control");
        var texts = w?.Arr("texts")?.OfType<JsonObject>().ToList() ?? new();
        void Add(int score, JsonObject o) { o["score"] = score; list.Add(o.Clean()); }

        if (ctl?.Str("classNN") is { } nn)
        {
            bool vol = VolatileClass(ctl.Str("class"));
            Add(vol ? 1 : 5, new JsonObject { ["kind"] = "classNN", ["value"] = nn, ["stable"] = !vol, ["note"] = vol ? "Klassenname enthält Laufzeitanteil" : null });
            if (ctl.LongN("id") is long id && id > 0 && !vol && id < 65536) Add(4, new JsonObject { ["kind"] = "controlId", ["value"] = id });
            if (ctl.Str("text") is { Length: > 0 } t && !t.Contains('\n'))
            {
                int same = texts.Count(x => x.Str("t") == t);
                Add(same <= 1 ? 4 : 2, new JsonObject { ["kind"] = "controlText", ["value"] = t, ["unique"] = same <= 1 });
            }
        }
        if (el != null && el.Str("ct") != null)
        {
            bool aidStable = el.Str("aid") is { Length: > 0 } aid && !aid.All(char.IsDigit);   // rein numerische IDs sind oft Fensterhandles
            bool unique = el.LongN("nameDup") == null;
            int score = aidStable ? 5 : !string.IsNullOrEmpty(el.Str("name")) && unique ? 4 : el.LongN("idx") != null ? 3 : 2;
            var uia = new JsonObject
            {
                ["kind"] = "uia", ["controlType"] = el.Str("ct"), ["name"] = el.Str("name"), ["automationId"] = aidStable ? el.Str("aid") : null, ["className"] = el.Str("cls"),
                ["index"] = el.LongN("idx"), ["of"] = el.LongN("sibCount"), ["nameUnique"] = string.IsNullOrEmpty(el.Str("name")) ? null : unique
            };
            if (tg!.Arr("path") is { Count: > 0 } path)
                uia["path"] = new JsonArray(path.OfType<JsonObject>().TakeLast(5).Select(p => (JsonNode)new JsonObject { ["ct"] = p.Str("ct"), ["name"] = p.Str("name"), ["aid"] = p.Str("aid") }.Clean()).ToArray());
            Add(score, uia);
            if (el.Str("accel") is { } accel || el.Str("access") is { }) Add(4, new JsonObject { ["kind"] = "keyboard", ["accelerator"] = el.Str("accel"), ["accessKey"] = el.Str("access") });
        }
        // MSAA (IAccessible): Rolle und Standardaktion (DoDefaultAction)
        if (w?.Obj("msaa") is { } ms && ms.Str("defaultAction") is { Length: > 0 } da)
            Add(3, new JsonObject { ["kind"] = "msaa", ["role"] = ms.Str("roleText"), ["name"] = ms.Str("name"), ["defaultAction"] = da, ["childId"] = ms.LongN("childId") });
        // Beschriftung daneben (links oder darüber) – für Felder ohne eigenen Namen
        if (ctl?.Obj("rect") is { } cr && texts.Count > 0)
        {
            int cx = cr.Int("x"), cy = cr.Int("y"), ch = cr.Int("h");
            JsonObject? best = null; double bestD = double.MaxValue; string rel = "";
            foreach (var t in texts)
            {
                if (t.Arr("r") is not { Count: 4 } r || t.Str("c") == ctl.Str("classNN")) continue;
                int tx = r[0]!.GetValue<int>(), ty = r[1]!.GetValue<int>(), tw = r[2]!.GetValue<int>(), th = r[3]!.GetValue<int>();
                bool leftOf = tx + tw <= cx + 4 && Math.Abs(ty + th / 2 - (cy + ch / 2)) <= Math.Max(ch, th);
                bool above = ty + th <= cy + 4 && Math.Abs(tx - cx) <= 40;
                if (!leftOf && !above) continue;
                double d = leftOf ? cx - (tx + tw) : (cy - (ty + th)) * 1.5;
                if (d < bestD && d < 200) { bestD = d; best = t; rel = leftOf ? "rightOf" : "below"; }
            }
            if (best != null) Add(3, new JsonObject { ["kind"] = "label", ["text"] = best.Str("t"), ["relation"] = rel, ["labelControl"] = best.Str("c") });
        }
        if (w?.Obj("point") is { } pt && w.Obj("window")?.Obj("client") is { } cl && pt.Arr("client") is { Count: 2 } cp && cl.Int("w") > 0 && cl.Int("h") > 0)
            Add(2, new JsonObject { ["kind"] = "relative", ["x"] = Math.Round(cp[0]!.GetValue<int>() / (double)cl.Int("w"), 4), ["y"] = Math.Round(cp[1]!.GetValue<int>() / (double)cl.Int("h"), 4), ["note"] = "Anteil am Clientbereich" });
        if (w?.Obj("point")?.Arr("window") is { Count: 2 } wp)
            Add(1, new JsonObject { ["kind"] = "window", ["x"] = wp[0]!.GetValue<int>(), ["y"] = wp[1]!.GetValue<int>(), ["pixel"] = w.Str("pixel") });
        return new JsonArray(list.OrderByDescending(x => x.Int("score")).Select(x => (JsonNode)x).ToArray());
    }

    /// <summary>Unmittelbar wiederholte Schrittfolgen (z. B. dieselben Schritte für mehrere Datensätze) mit den Werten, die sich ändern.</summary>
    static JsonArray Repetitions(List<JsonObject> actions)
    {
        string Sig(JsonObject s)
        {
            var w = s.Obj("target")?.Obj("win32");
            var el = s.Obj("target")?.Obj("el");
            string win = w?.Obj("ahk")?.Str("winTitle") ?? s.Obj("ctx")?.Str("process") ?? "";
            string what = w?.Obj("control")?.Str("classNN") ?? (el != null ? el.Str("ct") + "/" + (el.Str("aid") ?? "") : "");
            string extra = s.Str("kind") is "hotkey" ? s.Obj("input")?.Str("combo") ?? "" : s.Str("kind") is "key" ? s.Obj("input")?.Str("key") ?? "" : "";
            return s.Str("kind") + "|" + win + "|" + what + "|" + extra;
        }
        string Val(JsonObject s) => s.Str("kind") == "type" ? s.Obj("input")?.Str("text") ?? "" : s.Obj("target")?.Obj("el")?.Str("name") ?? "";
        var sig = actions.Select(Sig).ToList();
        var res = new JsonArray();
        int i = 0;
        while (i < sig.Count)
        {
            int bestL = 0, bestK = 0;
            for (int L = 1; L <= 12 && i + 2 * L <= sig.Count; L++)
            {
                int k = 1;
                while (i + (k + 1) * L <= sig.Count && Enumerable.Range(0, L).All(j => sig[i + j] == sig[i + k * L + j])) k++;
                if (k >= 2 && L * k > bestL * bestK && (L > 1 || k >= 3)) { bestL = L; bestK = k; }
            }
            if (bestK >= 2)
            {
                var varying = new JsonArray();
                for (int j = 0; j < bestL; j++)
                {
                    var vals = Enumerable.Range(0, bestK).Select(k => Val(actions[i + k * bestL + j])).ToList();
                    if (vals.Distinct().Count() > 1) varying.Add(new JsonObject { ["position"] = j + 1, ["kind"] = actions[i + j].Str("kind"), ["values"] = new JsonArray(vals.Select(v => (JsonNode)v).ToArray()) });
                }
                res.Add(new JsonObject
                {
                    ["steps"] = new JsonArray(Enumerable.Range(0, bestL * bestK).Select(x => (JsonNode)actions[i + x].Str("id")).ToArray()),
                    ["length"] = bestL, ["repeats"] = bestK, ["varying"] = varying
                });
                i += bestL * bestK;
            }
            else i++;
        }
        return res;
    }
}
