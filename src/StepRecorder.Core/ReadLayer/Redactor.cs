using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Endgültiges Schwärzen (Ausnahme vom Nur-Anhängen-Prinzip): Zeilen behalten <c>seq</c>, <c>t</c>, <c>type</c>,
/// Inhalte werden entfernt und mit <c>redacted</c> gekennzeichnet; Prüfsummenkette wird neu berechnet.
/// </summary>
public static class Redactor
{
    static readonly HashSet<string> Structural = new() { "session.start", "session.stop", "session.pause", "session.resume", "session.checkpoint", "session.recovered", "system", "display.topology", "scope.change", "keystate.sync", "gap", "frame" };

    /// <summary>Wendet die während der Aufnahme angelegten „Letzte Eingabe schwärzen“-Bereiche an.</summary>
    public static void ApplyLastInputRedactions(string folder, List<JsonObject> redactions)
    {
        var path = Path.Combine(folder, "events.jsonl");
        List<JsonObject> lines;
        using (var fs = File.OpenRead(path)) lines = Session.ReadJournal(fs).events;
        var ranges = redactions.SelectMany(r => r.Arr("seqRanges") ?? new JsonArray())
            .Select(a => (from: a![0]!.GetValue<long>(), to: a[1]!.GetValue<long>())).ToList();
        bool In(long s) => ranges.Any(r => s >= r.from && s <= r.to);
        for (int i = 0; i < lines.Count; i++)
        {
            var e = lines[i];
            long seq = e.Long("seq");
            string type = e.Str("type") ?? "";
            bool hit = In(seq) || (e.LongN("ref") is long rf && In(rf) && !Structural.Contains(type));
            if (!hit) continue;
            if (type == "marker") { e.Remove("label"); continue; }
            if (Structural.Contains(type)) continue;
            var cut = new JsonObject { ["seq"] = seq, ["t"] = e.Long("t"), ["type"] = type };
            foreach (var k in new[] { "inSeq", "inSeqEnd", "n" }) if (e[k] != null) cut[k] = e[k]!.DeepClone();
            cut["redacted"] = new JsonArray("*");
            lines[i] = cut;
        }
        RewriteJournal(folder, lines);
        // Overlay-Eintrag (als angewendet) – die Leseschicht hält Bilder des Zeitraums zurück
        var editsPath = Path.Combine(folder, "edits.json");
        var edits = Json.ReadObject(editsPath) ?? new JsonObject { ["format"] = "steprec.edits", ["schemaVersion"] = "1.0", ["revision"] = 0 };
        var arr = edits["redactions"] as JsonArray ?? new JsonArray();
        foreach (var r in redactions) { r["applied"] = true; r["appliedUtc"] = Json.UtcNow(); arr.Add(r.DeepClone()); }
        edits["redactions"] = arr;
        edits["sessionId"] = Json.ReadObject(Path.Combine(folder, "manifest.json")).Str("sessionId");
        edits["revision"] = edits.Long("revision") + 1;
        edits["savedAtUtc"] = Json.UtcNow();
        edits["savedBy"] = "recorder";
        Json.WriteAtomic(editsPath, Json.Pretty(edits));
        int frames = PurgeRangeFrames(folder, ranges);
        AddHistory(folder, "redactedDestructive", new JsonObject { ["trigger"] = "lastInput", ["ranges"] = ranges.Count, ["frames"] = frames });
    }

    /// <summary>Wendet Text-Schwärzungen aus dem Overlay endgültig auf das Journal an.</summary>
    public static int ApplyTextRedactionsDestructive(string folder, JsonObject edits)
    {
        var path = Path.Combine(folder, "events.jsonl");
        List<JsonObject> lines;
        using (var fs = File.OpenRead(path)) lines = Session.ReadJournal(fs).events;
        var bySeq = lines.ToDictionary(l => l.Long("seq"));
        int n = 0;
        foreach (var r in edits.Arr("redactions") ?? new JsonArray())
        {
            if (r.Str("kind") == "text" && !r.Bool("applied"))
            {
                var targets = (r.Arr("targets") ?? new JsonArray()).Where(tg => bySeq.ContainsKey(tg.Long("seq"))).ToList();
                // zugehörige key.up-Ereignisse tragen Taste und Scancode ebenfalls – vor dem Entschärfen bestimmen
                var ups = PairedKeyUps(lines, targets.Select(tg => tg.Long("seq")).Where(q => bySeq[q].Str("type") == "key.down").ToHashSet());
                foreach (var tg in targets)
                {
                    var e = bySeq[tg.Long("seq")];
                    string field = tg.Str("field") ?? "";
                    if (e.Str("type") is "key.down" or "key.up") NeuterKey(e);
                    else SetPath(e, field, "[redacted]");
                    var red = e["redacted"] as JsonArray ?? new JsonArray();
                    red.Add(field); e["redacted"] = red;
                    n++;
                }
                foreach (var up in ups.Where(u => u.Str("key") != "Redacted")) { NeuterKey(up); up["redacted"] = new JsonArray("ch"); n++; }
                r.AsObject()["applied"] = true; r.AsObject()["appliedUtc"] = Json.UtcNow();
            }
            else if (r.Str("kind") == "range" && !r.Bool("applied"))
            {
                var ranges = (r.Arr("seqRanges") ?? new JsonArray()).Select(a => (a![0]!.GetValue<long>(), a[1]!.GetValue<long>())).ToList();
                bool In(long q) => ranges.Any(x => q >= x.Item1 && q <= x.Item2);
                for (int i = 0; i < lines.Count; i++)
                {
                    long s = lines[i].Long("seq");
                    string type = lines[i].Str("type") ?? "";
                    // auch Ereignisse außerhalb des Bereichs, die sich darauf beziehen (Element, Feldwert, Zustand)
                    bool hit = In(s) || (lines[i].LongN("ref") is long rf && In(rf));
                    if (!hit || Structural.Contains(type)) continue;
                    var cut = new JsonObject { ["seq"] = s, ["t"] = lines[i].Long("t"), ["type"] = type };
                    foreach (var k in new[] { "inSeq", "inSeqEnd", "n" }) if (lines[i][k] != null) cut[k] = lines[i][k]!.DeepClone();
                    cut["redacted"] = new JsonArray("*");
                    lines[i] = cut; n++;
                }
                r.AsObject()["applied"] = true; r.AsObject()["appliedUtc"] = Json.UtcNow();
            }
        }
        RewriteJournal(folder, lines);
        // Bilder aller geschwärzten Zeiträume (auch früher angewendeter) aus den Rohdaten entfernen – wiederholbar
        var imageRanges = (edits.Arr("redactions") ?? new JsonArray()).OfType<JsonObject>()
            .Where(r => r.Str("kind") == "range" && (r["images"]?.GetValue<bool>() ?? true))
            .SelectMany(r => r.Arr("seqRanges") ?? new JsonArray()).Select(a => (a![0]!.GetValue<long>(), a[1]!.GetValue<long>())).ToList();
        int frames = PurgeRangeFrames(folder, imageRanges);
        AddHistory(folder, "redactedDestructive", new JsonObject { ["trigger"] = "viewer", ["fields"] = n, ["frames"] = frames });
        try { var cache = Path.Combine(folder, "cache"); if (Directory.Exists(cache)) Directory.Delete(cache, true); } catch { }
        return n + frames;
    }

    static void NeuterKey(JsonObject e) { e["key"] = "Redacted"; e["vk"] = 0; e["sc"] = 0; e.Remove("ch"); }

    /// <summary>
    /// Entfernt die Bilder geschwärzter Zeiträume aus den Rohdaten: Bildereignisse im Zeitraum (oder mit Bezug darauf) werden
    /// zu <c>store: none</c>; spätere Bilder, die als Änderung darauf aufbauen, werden vorher als Schlüsselbild neu gespeichert;
    /// nicht mehr verwendete Blobs werden gelöscht. Gibt die Zahl entfernter Bilder zurück.
    /// </summary>
    public static int PurgeRangeFrames(string folder, IReadOnlyCollection<(long from, long to)> ranges)
    {
        if (ranges.Count == 0) return 0;
        var path = Path.Combine(folder, "events.jsonl");
        List<JsonObject> lines;
        using (var fs = File.OpenRead(path)) lines = Session.ReadJournal(fs).events;
        bool In(long q) => ranges.Any(r => q >= r.from && q <= r.to);
        var frames = new Dictionary<string, JsonObject>();
        foreach (var l in lines) if (l.Str("type") == "frame" && l.Str("id") is { } id) frames[id] = l;
        var purge = frames.Values.Where(f => f.Obj("store").Str("kind") is not (null or "none") && (In(f.Long("seq")) || (f.LongN("ref") is long r && In(r))))
                                 .Select(f => f.Str("id")!).ToHashSet();
        if (purge.Count == 0) return 0;
        bool Depends(string id, int depth)
        {
            if (depth > 64 || !frames.TryGetValue(id, out var f)) return false;
            var st = f.Obj("store");
            string? baseId = st.Str("kind") switch { "same" => st.Str("as"), "delta" => st.Str("base"), _ => null };
            return baseId != null && (purge.Contains(baseId) || Depends(baseId, depth + 1));
        }
        using (var s = Session.Open(folder, recover: false))
        {
            foreach (var (id, f) in frames)
            {
                if (purge.Contains(id) || !Depends(id, 0)) continue;
                using var bmp = s.RestoreFrameCopy(id);
                f["store"] = bmp == null
                    ? new JsonObject { ["kind"] = "none", ["reason"] = "redacted" }
                    : FrameStore(folder, FrameBlobs.EncodeKey(bmp));
            }
        }
        foreach (var id in purge) { frames[id]["store"] = new JsonObject { ["kind"] = "none", ["reason"] = "redacted" }; frames[id].Remove("cursor"); }
        RewriteJournal(folder, lines);
        FrameBlobs.DeleteUnreferenced(folder, FrameBlobs.Referenced(lines));
        return purge.Count;
    }

    static JsonObject FrameStore(string folder, byte[] png) => new() { ["kind"] = "key", ["blob"] = FrameBlobs.Write(folder, png), ["bytes"] = png.Length };

    /// <summary>Zu jedem key.down das nächste key.up derselben Taste (vor dem Entschärfen aufrufen).</summary>
    public static List<JsonObject> PairedKeyUps(IReadOnlyList<JsonObject> lines, HashSet<long> downs)
    {
        var result = new List<JsonObject>();
        if (downs.Count == 0) return result;
        var pending = new Dictionary<long, int>();
        foreach (var e in lines)
        {
            string? type = e.Str("type");
            long vk = e.Long("vk");
            if (type == "key.down" && downs.Contains(e.Long("seq"))) pending[vk] = pending.GetValueOrDefault(vk) + 1;
            else if (type == "key.up" && pending.TryGetValue(vk, out int c) && c > 0)
            {
                result.Add(e);
                if (c == 1) pending.Remove(vk); else pending[vk] = c - 1;
            }
        }
        return result;
    }

    static void SetPath(JsonObject e, string path, string value)
    {
        var parts = path.Split('.');
        JsonNode? cur = e;
        for (int i = 0; i < parts.Length - 1 && cur != null; i++)
        {
            var p = parts[i];
            int b = p.IndexOf('[');
            if (b > 0) { var arr = cur[p[..b]] as JsonArray; int idx = int.Parse(p[(b + 1)..^1]); cur = arr != null && idx < arr.Count ? arr[idx] : null; }
            else cur = cur[p];
        }
        if (cur is JsonObject o && o[parts[^1]] != null) o[parts[^1]] = value;
    }

    /// <summary>Schreibt das Journal neu und berechnet Checkpoint-Kette und Manifest-Prüfsumme.</summary>
    public static void RewriteJournal(string folder, List<JsonObject> lines)
    {
        var path = Path.Combine(folder, "events.jsonl");
        var tmp = path + ".tmp";
        byte[] chain = new byte[32];
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        using (var fs = new FileStream(tmp, FileMode.Create))
        {
            hash.AppendData(chain);
            long count = 0;
            foreach (var l in lines)
            {
                if (l.Str("type") == "session.checkpoint")
                {
                    chain = hash.GetHashAndReset();
                    l["chain"] = "sha256:" + Convert.ToHexString(chain).ToLowerInvariant();
                    l["lines"] = count;
                    count = 0;
                    var cb = Encoding.UTF8.GetBytes(Json.Line(l) + "\n");
                    fs.Write(cb);
                    hash.AppendData(chain);
                    continue;
                }
                var b = Encoding.UTF8.GetBytes(Json.Line(l) + "\n");
                fs.Write(b);
                hash.AppendData(b);
                count++;
            }
            fs.Flush(true);
        }
        File.Move(tmp, path, true);
        var man = Json.ReadObject(Path.Combine(folder, "manifest.json"));
        if (man?["journal"]?["segments"] is JsonArray segs && segs.Count > 0 && segs[0] is JsonObject s0)
        {
            var bytes = File.ReadAllBytes(path);
            s0["bytes"] = bytes.Length;
            s0["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Json.WriteAtomic(Path.Combine(folder, "manifest.json"), Json.Pretty(man));
        }
        // steps.json ist danach veraltet
        try { File.Delete(Path.Combine(folder, "steps.json")); } catch { }
    }

    static void AddHistory(string folder, string action, JsonObject detail)
    {
        var mp = Path.Combine(folder, "manifest.json");
        var man = Json.ReadObject(mp);
        if (man == null) return;
        var h = man["history"] as JsonArray ?? new JsonArray();
        h.Add(new JsonObject { ["atUtc"] = Json.UtcNow(), ["action"] = action, ["tool"] = "StepRecorder " + AppInfo.Version, ["detail"] = detail });
        man["history"] = h;
        var priv = man["privacy"] as JsonObject ?? new JsonObject();
        priv["destructiveRedactions"] = priv.Long("destructiveRedactions") + 1;
        man["privacy"] = priv;
        Json.WriteAtomic(mp, Json.Pretty(man));
    }
}
