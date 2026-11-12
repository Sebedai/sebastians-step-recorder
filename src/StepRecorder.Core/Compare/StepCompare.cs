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

    /// <param name="images">Bilder vergleichen (08 §5.9, Schalter): „Bild“ wird erst dann als Unterschied gemeldet.</param>
    public static JsonObject Compare(SessionView a, SessionView b, bool includeMinor = false, bool images = false, ImageDiffOptions? imageOptions = null)
    {
        imageOptions ??= new ImageDiffOptions();
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
                    JsonObject? img = null; var unchecked_ = new List<string>();
                    if (images)
                    {
                        var (res, why) = CompareImages(a, sa[x], b, sb[y], imageOptions);
                        if (res == null) unchecked_.Add("image");
                        else { img = res.ToJson(); if (res.ChangedRatio >= imageOptions.MinChangedRatio || res.SizeDiffers) diffs.Add("image"); }
                        if (why != null && img == null) img = new JsonObject { ["unchecked"] = why };
                    }
                    if (sa[x].Redacted || sb[y].Redacted) unchecked_.Add("content");
                    string st = diffs.Count == 0 ? "same" : "changed";
                    if (st == "same") same++; else changed++;
                    var pj = Pair(st, sa[x], sb[y], s, diffs);
                    if (img != null) pj["imageDiff"] = img;
                    if (unchecked_.Count > 0) pj["unchecked"] = new JsonArray(unchecked_.Select(u => (JsonNode)u).ToArray());
                    pairs.Add(pj);
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
            ["params"] = new JsonObject { ["compareMinScore"] = 5, ["includeMinor"] = includeMinor, ["images"] = images, ["pixelThreshold"] = images ? imageOptions.PixelThreshold : null, ["minChangedPct"] = images ? imageOptions.MinChangedRatio * 100 : null }.Clean(),
            ["summary"] = new JsonObject { ["same"] = same, ["changed"] = changed, ["onlyA"] = onlyA, ["onlyB"] = onlyB },
            ["pairs"] = pairs
        };
    }

    /// <summary>Bild eines Schritts, wie Anleitungen es zeigen – ohne Einzeichnungen; null, wenn es fehlt oder zurückgehalten ist.</summary>
    public static System.Drawing.Bitmap? StepBitmap(SessionView v, ViewStep s)
    {
        if (s.Redacted || s.ShowImage == false) return null;
        try
        {
            using var img = Export.StepImages.Render(v, s, Export.StepImages.GuideVariant(s, "screen"), "compatible", annotate: false);
            return img == null ? null : new System.Drawing.Bitmap(img.Bitmap);
        }
        catch { return null; }
    }

    static (ImageDiffResult? res, string? why) CompareImages(SessionView a, ViewStep sa, SessionView b, ViewStep sb, ImageDiffOptions o)
    {
        // Was zurückgehalten ist, geht nicht in den Vergleich ein (08 §5.9) – kein Orakel für geschwärzte Inhalte
        if (sa.Redacted || sb.Redacted) return (null, "redacted");
        using var ia = StepBitmap(a, sa);
        using var ib = StepBitmap(b, sb);
        if (ia == null || ib == null) return (null, "noImage");
        return (ImageDiff.Diff(ia, ib, o), null);
    }

    /// <summary>
    /// Vergleich als Markdown mit Bildpaaren für Reviews (12, Paket 19): je geändertem oder einseitigem Paar ein Vorher/Nachher-Bild
    /// (A links, B rechts mit hervorgehobenen Bereichen) im Ordner <c>&lt;name&gt;-bilder/</c> neben der Datei.
    /// </summary>
    public static List<string> WriteMarkdownWithImages(JsonObject r, SessionView a, SessionView b, string mdPath, string lang)
    {
        bool de = lang == "de";
        var files = new List<string>();
        string dir = Path.GetDirectoryName(Path.GetFullPath(mdPath))!;
        string folder = Path.GetFileNameWithoutExtension(mdPath) + "-bilder";
        Directory.CreateDirectory(dir);
        var sb = new StringBuilder(Markdown(r, lang));
        sb.Append('\n').Append(de ? "## Bilder\n\n" : "## Images\n\n");
        int k = 0;
        foreach (var p in (r.Arr("pairs") ?? new JsonArray()).OfType<JsonObject>())
        {
            k++;
            if (p.Str("status") == "same") continue;
            var stA = p["a"] != null ? a.All.FirstOrDefault(s => s.Number == p.Int("a")) : null;
            var stB = p["b"] != null ? b.All.FirstOrDefault(s => s.Number == p.Int("b")) : null;
            using var ba = stA != null ? StepBitmap(a, stA) : null;
            using var bb = stB != null ? StepBitmap(b, stB) : null;
            if (ba == null && bb == null) continue;
            var regions = new List<System.Drawing.Rectangle>(); System.Drawing.Size? size = null;
            if (p.Obj("imageDiff") is { } idf && idf.Arr("regions") is { } rg)
            {
                regions = rg.OfType<JsonObject>().Select(x => new System.Drawing.Rectangle(x.Int("x"), x.Int("y"), x.Int("w"), x.Int("h"))).ToList();
                size = new System.Drawing.Size(idf.Int("width"), idf.Int("height"));
            }
            else if (ba != null && bb != null) { var d = ImageDiff.Diff(ba, bb); regions = d.Regions; size = d.Size; }
            string la = stA != null ? $"A {stA.Number}. {stA.AuthorTitle ?? stA.Title}" : (de ? "A –" : "A –");
            string lb = stB != null ? $"B {stB.Number}. {stB.AuthorTitle ?? stB.Title}" : (de ? "B –" : "B –");
            using var pair = ImageDiff.SideBySide(ba, bb, la, lb, regions, size);
            string file = $"paar-{k:000}.png";
            Directory.CreateDirectory(Path.Combine(dir, folder));
            string full = Path.Combine(dir, folder, file);
            pair.Save(full, System.Drawing.Imaging.ImageFormat.Png);
            files.Add(full);
            string status = de ? p.Str("status") switch { "changed" => "geändert", "onlyA" => "nur in A", _ => "nur in B" } : p.Str("status") switch { "changed" => "changed", "onlyA" => "only in A", _ => "only in B" };
            sb.Append($"### {(de ? "Paar" : "Pair")} {k} · {status}\n\n");
            var diffs = p.StrList("differences");
            if (diffs.Count > 0) sb.Append((de ? "Unterschiede: " : "Differences: ") + string.Join(", ", diffs.Select(x => DiffName(x, de))) + "\n\n");
            sb.Append($"![{(de ? "Paar" : "Pair")} {k}]({folder}/{file})\n\n");
        }
        File.WriteAllText(mdPath, sb.ToString().TrimEnd('\n') + "\n", new UTF8Encoding(false));
        files.Insert(0, Path.GetFullPath(mdPath));
        return files;
    }

    public static string DiffName(string d, bool de) => d switch
    {
        "target" => de ? "Ziel" : "target", "text" => de ? "Text" : "text", "effect" => de ? "Wirkung" : "effect", "duration" => de ? "Dauer" : "duration",
        "response" => de ? "Reaktion" : "response", "image" => de ? "Bild" : "image", _ => d
    };

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
                ? (de ? "Reaktion " : "response ") + ReadLayer.Timing.Ms(rm.Long("a"), de) + " → " + ReadLayer.Timing.Ms(rm.Long("b"), de)
                : x == "image" && p.Obj("imageDiff") is { } idf ? (de ? "Bild " : "image ") + idf.Dbl("changedPct").ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " %" : x)
                .Concat(p.StrList("unchecked").Select(u => (de ? "unverglichen: " : "not compared: ") + (u == "image" ? (de ? "Bild" : "image") : (de ? "Inhalt" : "content"))));
            sb.Append($"| {st} | {a.Replace("|", "\\|")} | {b.Replace("|", "\\|")} | {string.Join(", ", diffs)} |\n");
        }
        return sb.ToString();
    }
}
