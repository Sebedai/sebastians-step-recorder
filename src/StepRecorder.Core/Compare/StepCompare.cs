using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Compare;

/// <summary>
/// Schrittabgleich zweier Sitzungen (Kernschritte per Sequenzabgleich): gleich, geändert, nur A, nur B.
/// Gleiche Eingabe ergibt gleiches Ergebnis in Viewer, CLI und MCP.
/// </summary>
public static class StepCompare
{
    public const string Version = "1.0";

    static bool Core(ViewStep s) => s.Importance == "primary" && s.Kind != "gap" || s.Kind == "gap";

    static string Key(ViewStep s)
    {
        var el = s.Json.Obj("target")?.Obj("el");
        return s.Kind switch
        {
            "type" => "type|" + (el?.Str("name") ?? "") + "|" + (el?.Str("ct") ?? ""),
            "hotkey" => "hotkey|" + s.Json.Obj("input")?.Str("combo"),
            "key" => "key|" + s.Json.Obj("input")?.Str("key"),
            "gap" => "gap|" + s.Json.Obj("input")?.Str("cause"),
            _ => s.Kind + "|" + (el?.Str("ct") ?? "") + "|" + (el?.Str("name") ?? el?.Str("aid") ?? "") + "|" + s.Json.Obj("ctx")?.Str("process")
        };
    }

    static int Score(ViewStep a, ViewStep b)
    {
        if (a.Kind != b.Kind) return 0;
        int sc = 3;
        var ea = a.Json.Obj("target")?.Obj("el"); var eb = b.Json.Obj("target")?.Obj("el");
        if (ea?.Str("name") != null && ea.Str("name") == eb?.Str("name")) sc += 4;
        if (ea?.Str("aid") != null && ea.Str("aid") == eb?.Str("aid")) sc += 2;
        if (ea?.Str("ct") == eb?.Str("ct")) sc += 1;
        if (a.Json.Obj("ctx")?.Str("process") == b.Json.Obj("ctx")?.Str("process")) sc += 1;
        if (a.Kind == "type" && a.Json.Obj("input")?.Str("text") == b.Json.Obj("input")?.Str("text")) sc += 2;
        if (a.Kind is "hotkey" or "key" && Key(a) == Key(b)) sc += 4;
        return sc;
    }

    public static JsonObject Compare(SessionView a, SessionView b, bool includeMinor = false)
    {
        var sa = a.All.Where(s => includeMinor ? s.Importance != "trace" : Core(s)).Where(s => !s.Discarded).ToList();
        var sb = b.All.Where(s => includeMinor ? s.Importance != "trace" : Core(s)).Where(s => !s.Discarded).ToList();
        if (sa.Count > 5000 || sb.Count > 5000) throw new InvalidOperationException("COMPARISON_TOO_LARGE");
        // Needleman-Wunsch-artiger Abgleich mit Mindestwert 5
        int n = sa.Count, m = sb.Count;
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
            {
                int s = Score(sa[i], sb[j]);
                int match = s >= 5 ? dp[i + 1, j + 1] + s : int.MinValue;
                dp[i, j] = Math.Max(match, Math.Max(dp[i + 1, j], dp[i, j + 1]));
            }
        var pairs = new JsonArray();
        int x = 0, y = 0, same = 0, changed = 0, onlyA = 0, onlyB = 0;
        while (x < n || y < m)
        {
            if (x < n && y < m)
            {
                int s = Score(sa[x], sb[y]);
                if (s >= 5 && dp[x, y] == dp[x + 1, y + 1] + s)
                {
                    var diffs = Differences(sa[x], sb[y]);
                    string st = diffs.Count == 0 ? "same" : "changed";
                    if (st == "same") same++; else changed++;
                    pairs.Add(Pair(st, sa[x], sb[y], s, diffs));
                    x++; y++; continue;
                }
                if (dp[x, y] == dp[x + 1, y]) { pairs.Add(Pair("onlyA", sa[x], null, 0, new())); onlyA++; x++; continue; }
                pairs.Add(Pair("onlyB", null, sb[y], 0, new())); onlyB++; y++; continue;
            }
            if (x < n) { pairs.Add(Pair("onlyA", sa[x], null, 0, new())); onlyA++; x++; }
            else { pairs.Add(Pair("onlyB", null, sb[y], 0, new())); onlyB++; y++; }
        }
        return new JsonObject
        {
            ["compareVersion"] = Version,
            ["a"] = new JsonObject { ["sessionId"] = a.Session.SessionId, ["title"] = a.Title, ["revision"] = a.Revision, ["coreSteps"] = sa.Count },
            ["b"] = new JsonObject { ["sessionId"] = b.Session.SessionId, ["title"] = b.Title, ["revision"] = b.Revision, ["coreSteps"] = sb.Count },
            ["params"] = new JsonObject { ["compareMinScore"] = 5, ["includeMinor"] = includeMinor },
            ["summary"] = new JsonObject { ["same"] = same, ["changed"] = changed, ["onlyA"] = onlyA, ["onlyB"] = onlyB },
            ["pairs"] = pairs
        };
    }

    static List<string> Differences(ViewStep a, ViewStep b)
    {
        var d = new List<string>();
        var ea = a.Json.Obj("target")?.Obj("el"); var eb = b.Json.Obj("target")?.Obj("el");
        if (ea?.Str("name") != eb?.Str("name") || ea?.Str("ct") != eb?.Str("ct")) d.Add("target");
        if (a.Kind == "type" && a.Json.Obj("input")?.Str("text") != b.Json.Obj("input")?.Str("text")) d.Add("text");
        var wa = string.Join(",", a.Json.Obj("effect")?.Arr("windowsOpened")?.Select(w => w.Str("title")) ?? Array.Empty<string?>());
        var wb = string.Join(",", b.Json.Obj("effect")?.Arr("windowsOpened")?.Select(w => w.Str("title")) ?? Array.Empty<string?>());
        if (wa != wb) d.Add("effect");
        long da = a.TTo - a.TFrom, db = b.TTo - b.TFrom;
        if (Math.Abs(da - db) > 2_000_000 && Math.Max(da, db) > 2 * Math.Max(1, Math.Min(da, db))) d.Add("duration");
        // Reaktion der Anwendung (12, Paket 9): mehr als 1 s und mindestens doppelt so lang bzw. halb so lang
        if (!a.Redacted && !b.Redacted && ReadLayer.Timing.WaitMsOf(a.Json) is long wa2 && ReadLayer.Timing.WaitMsOf(b.Json) is long wb2
            && Math.Abs(wa2 - wb2) > 1000 && Math.Max(wa2, wb2) >= 2 * Math.Max(1, Math.Min(wa2, wb2))) d.Add("response");
        return d;
    }

    static JsonObject Pair(string status, ViewStep? a, ViewStep? b, int score, List<string> diffs) => new JsonObject
    {
        ["status"] = status, ["kind"] = (a ?? b)!.Kind, ["group"] = (a ?? b)!.Kind == "gap" ? "gap" : "core",
        ["a"] = a?.Number, ["b"] = b?.Number, ["aRef"] = a?.Id, ["bRef"] = b?.Id, ["score"] = score,
        ["differences"] = new JsonArray(diffs.Select(x => (JsonNode)x).ToArray()),
        ["details"] = new JsonObject { ["a"] = a?.Title, ["b"] = b?.Title }.Clean(),
        ["responseMs"] = a == null || b == null || a.Redacted || b.Redacted ? null
            : ReadLayer.Timing.WaitMsOf(a.Json) is long wa && ReadLayer.Timing.WaitMsOf(b.Json) is long wb ? new JsonObject { ["a"] = wa, ["b"] = wb } : null
    }.Clean();

    public static string Markdown(JsonObject r, string lang)
    {
        bool de = lang == "de";
        var sb = new StringBuilder();
        sb.Append(de ? "# Vergleich zweier Aufnahmen\n\n" : "# Comparison of two recordings\n\n");
        sb.Append($"- A: „{r.Obj("a").Str("title")}“ ({r.Obj("a").Long("coreSteps")} {(de ? "Kernschritte" : "core steps")})\n");
        sb.Append($"- B: „{r.Obj("b").Str("title")}“ ({r.Obj("b").Long("coreSteps")} {(de ? "Kernschritte" : "core steps")})\n");
        var s = r.Obj("summary")!;
        sb.Append(de ? $"- Ergebnis: {s.Long("same")} gleich · {s.Long("changed")} geändert · {s.Long("onlyA")} nur in A · {s.Long("onlyB")} nur in B\n\n" : $"- Result: {s.Long("same")} same · {s.Long("changed")} changed · {s.Long("onlyA")} only in A · {s.Long("onlyB")} only in B\n\n");
        sb.Append(de ? "| Status | A | B | Unterschiede |\n|---|---|---|---|\n" : "| Status | A | B | Differences |\n|---|---|---|---|\n");
        foreach (var p in r.Arr("pairs") ?? new JsonArray())
        {
            string st = de ? p.Str("status") switch { "same" => "gleich", "changed" => "geändert", "onlyA" => "nur A", _ => "nur B" } : p.Str("status")!;
            string a = p["a"] != null ? $"{p.Long("a")}. {p.Obj("details").Str("a")}" : "–";
            string b = p["b"] != null ? $"{p.Long("b")}. {p.Obj("details").Str("b")}" : "–";
            var diffs = p.StrList("differences").Select(x => x == "response" && p.Obj("responseMs") is { } rm
                ? (de ? "Reaktion " : "response ") + ReadLayer.Timing.Ms(rm.Long("a"), de) + " → " + ReadLayer.Timing.Ms(rm.Long("b"), de) : x);
            sb.Append($"| {st} | {a.Replace("|", "\\|")} | {b.Replace("|", "\\|")} | {string.Join(", ", diffs)} |\n");
        }
        return sb.ToString();
    }
}
