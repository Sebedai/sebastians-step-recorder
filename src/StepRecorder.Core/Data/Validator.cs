using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Steps;

namespace StepRecorder.Data;

/// <summary>Formatprüfung einer Sitzung (04 §Validierung F01–F18, Auswahl) und Vollständigkeitsbericht.</summary>
public static class Validator
{
    public sealed record Issue(string Code, string Severity, string Message, long? Seq = null);

    public static (List<Issue> issues, JsonObject report) Validate(string path, bool thorough = false)
    {
        var issues = new List<Issue>();
        using var s = Session.Open(path, recover: false);
        var m = s.Manifest;
        void Err(string c, string msg, long? seq = null) => issues.Add(new Issue(c, "error", msg, seq));
        void Warn(string c, string msg, long? seq = null) => issues.Add(new Issue(c, "warning", msg, seq));

        // F01
        if (m.Str("format") is not ("steprec" or "sebedais.steprec")) Err("F01", "manifest.format fehlt oder falsch");
        var start = s.Events.FirstOrDefault();
        if (start?.Str("type") != "session.start") Err("F04", "Journal beginnt nicht mit session.start");
        else if (start.Str("sessionId") != m.Str("sessionId")) Err("F01", "sessionId in Manifest und session.start verschieden");
        // F02/F03
        long exp = 1;
        foreach (var e in s.Events)
        {
            if (e.Long("seq") != exp) { Err("F03", $"seq-Lücke bei {exp}", exp); break; }
            if (e["t"] == null || e.Str("type") == null) Err("F02", "Zeile ohne t oder type", e.Long("seq"));
            exp++;
        }
        foreach (var w in s.LoadWarnings) Warn("F02", w);
        // F05/F06/F07
        var wins = new HashSet<string>();
        bool topo = false;
        foreach (var e in s.Events)
        {
            long seq = e.Long("seq");
            string type = e.Str("type") ?? "";
            if (e.LongN("ref") is long r && (r >= seq || s.BySeq(r) == null)) Err("F05", $"ref {r} ungültig", seq);
            if (type == "window.info") wins.Add(e.Str("win") ?? "");
            if (type == "display.topology") topo = true;
            foreach (var k in new[] { "win", "fg", "owner", "prev" })
                if (e.Str(k) is { } w && !wins.Contains(w) && !(type == "window.info" && k == "win")) Err("F06", $"{k}={w} ohne vorheriges window.info", seq);
            if (e["x"] != null && e["m"] != null && (e["ep"] == null || !topo)) Err("F07", "Koordinaten ohne Epoche", seq);
        }
        // F08
        var gaps = new Dictionary<string, (bool begin, bool end)>();
        foreach (var g in s.Events.Where(e => e.Str("type") == "gap"))
        {
            var id = g.Str("gapId") ?? "";
            gaps.TryGetValue(id, out var st);
            if (g.Str("phase") == "end") { if (st.end) Err("F08", $"Lücke {id} doppelt beendet", g.Long("seq")); st.end = true; }
            if (g.Str("phase") == "begin") st.begin = true;
            if (g.LongN("tTo") is long to && to < g.Long("tFrom")) Err("F08", $"Lücke {id}: tTo < tFrom", g.Long("seq"));
            gaps[id] = st;
        }
        // F09
        foreach (var mv in s.Events.Where(e => e.Str("type") == "mouse.move"))
        {
            var pts = StepBuilder.Unpack(mv).ToList();
            if (pts.Count != mv.Int("n", 1) || pts[^1].t != mv.Long("tEnd", pts[^1].t) || pts[^1].x != mv.Int("xEnd", pts[^1].x) || pts[^1].y != mv.Int("yEnd", pts[^1].y))
                Err("F09", "mouse.move-Bündel lässt sich nicht entpacken", mv.Long("seq"));
        }
        // F11
        var frames = new Dictionary<string, JsonObject>();
        foreach (var f in s.Events.Where(e => e.Str("type") == "frame"))
        {
            var st = f.Obj("store");
            string? blob = st.Str("blob");
            if (blob != null)
            {
                var hex = blob[7..];
                var data = s.ReadFile($"frames/{hex[..2]}/{hex}.png");
                if (data == null) Err("F11", $"Blob fehlt für {f.Str("id")}", f.Long("seq"));
                else if (thorough && Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant() != hex) Err("F11", $"Blob-Prüfsumme falsch für {f.Str("id")}", f.Long("seq"));
            }
            foreach (var k in new[] { "base", "as" })
                if (st.Str(k) is { } b && (!frames.TryGetValue(b, out var bf) || bf.Str("m") != f.Str("m"))) Err("F11", $"{k}={b} verweist nicht auf ein früheres Bild desselben Monitors", f.Long("seq"));
            if (f.Str("id") is { } id) frames[id] = f;
        }
        // F13 Prüfsummenkette
        var bytes = s.ReadFile("events.jsonl") ?? Array.Empty<byte>();
        byte[] chain = new byte[32];
        using (var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            h.AppendData(chain);
            int pos = 0;
            while (pos < bytes.Length)
            {
                int nl = Array.IndexOf(bytes, (byte)'\n', pos);
                if (nl < 0) break;
                var line = Encoding.UTF8.GetString(bytes, pos, nl - pos);
                if (line.Contains("\"type\":\"session.checkpoint\""))
                {
                    chain = h.GetHashAndReset();
                    var cp = JsonNode.Parse(line);
                    if (cp.Str("chain") != "sha256:" + Convert.ToHexString(chain).ToLowerInvariant()) { Err("F13", "Prüfsummenkette stimmt nicht", cp.Long("seq")); break; }
                    h.AppendData(chain);
                }
                else h.AppendData(bytes, pos, nl - pos + 1);
                pos = nl + 1;
            }
        }
        // F18 inSeq
        var inSeqs = s.Events.Where(e => StepBuilder.IsInputType(e.Str("type")) && !e.Bool("recovered")).ToList();
        var seen = new HashSet<long>();
        foreach (var e in inSeqs)
        {
            if (e.Arr("redacted") != null && e["inSeq"] == null) continue;
            if (e.LongN("inSeq") is not long n) { Err("F18", "Eingabeereignis ohne inSeq", e.Long("seq")); continue; }
            if (!seen.Add(n)) Err("F18", $"inSeq {n} doppelt", e.Long("seq"));
        }
        // F16 Manifest
        if (m.Str("state") == "recording") Warn("F16", "Sitzung ist nicht abgeschlossen (state=recording)");
        else if (m.Obj("journal")?.Arr("segments")?.FirstOrDefault().Str("sha256") is { } sha && Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != sha)
            Err("F16", "Manifest-Prüfsumme des Journals stimmt nicht");
        // Schritte / Vollständigkeit
        var steps = StepBuilder.Build(s);
        var bk = steps.Obj("completeness").Obj("bookkeeping")!;
        if (!bk.Bool("ok")) Err("S03", $"Buchführung: {bk.Long("orphans")} Eingaben ohne Schritt, unerklärte Zeit {bk.Obj("timeline").Long("unaccountedUs")} µs");
        var report = (JsonObject)steps.Obj("completeness")!.DeepClone();
        report["issues"] = new JsonArray(issues.Select(i => (JsonNode)new JsonObject { ["code"] = i.Code, ["severity"] = i.Severity, ["message"] = i.Message, ["seq"] = i.Seq }.Clean()).ToArray());
        return (issues, report);
    }
}
