using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.ReadLayer;

/// <summary>Die bereinigte Kopie konnte nicht (sicher) erstellt werden; es bleibt nichts zurück.</summary>
public sealed class SanitizeException : Exception { public SanitizeException(string message) : base(message) { } }

/// <summary>
/// Bereinigte Kopie (2.5): eine neue Aufnahme aus Sicht der Leseschicht. Ereignisse laufen durch
/// <see cref="SessionView.EventForOutput"/>, Bilder werden mit allen Schwärzungen neu gespeichert, das Overlay verliert
/// Regelwerte und interne Kommentare, die Kopie bekommt eine neue Kennung. Pflicht-Gegenprobe: jede Textdatei der Kopie
/// (samt neu gebauter Schritte) wird nach allen geschützten Werten durchsucht – ein Treffer löscht die Kopie.
/// </summary>
public static class SessionSanitizer
{
    /// <summary>Erzeugt die Kopie in <paramref name="targetFolder"/> (darf nicht existieren) und gibt sie zurück.</summary>
    public static string CreateCopy(string sessionFolder, string targetFolder, IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        if (Directory.Exists(targetFolder) || File.Exists(targetFolder)) throw new SanitizeException(Loc.L("Das Ziel existiert bereits.", "The target already exists."));
        string tmp = targetFolder.TrimEnd('\\', '/') + ".tmp-" + Guid.NewGuid().ToString("N")[..6];
        Directory.CreateDirectory(tmp);
        try
        {
            List<string> needles;
            using (var v = SessionView.Open(sessionFolder, headless: true))
            {
                if (v.Session.State == "recording") throw new SanitizeException(Loc.L("Die Aufnahme läuft noch.", "The recording is still running."));
                if (v.UnreviewedCount > 0) throw new SanitizeException(Loc.N(v.UnreviewedCount,
                    "1 auffällige Eingabe ist noch nicht durchgesehen. Bitte zuerst die Durchsicht abschließen.", "{0} auffällige Eingaben sind noch nicht durchgesehen. Bitte zuerst die Durchsicht abschließen.",
                    "1 conspicuous input has not been reviewed yet. Please finish the review first.", "{0} conspicuous inputs have not been reviewed yet. Please finish the review first."));
                needles = Needles(v, sessionFolder);
                WriteCopy(v, tmp, progress, ct);
            }
            // Gegenprobe – mit frisch gebauten Schritten der Kopie
            using (var c = SessionView.Open(tmp, headless: false)) { _ = c.All.Count; }
            if (FindLeak(tmp, needles) is { } leak) throw new SanitizeException(Loc.F("Gegenprobe fehlgeschlagen: ein geschützter Wert steht noch in {0}. Die Kopie wurde verworfen.", "Cross-check failed: a protected value is still in {0}. The copy was discarded.", leak));
            Directory.Move(tmp, targetFolder);
            return targetFolder;
        }
        finally { try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } }
    }

    static void WriteCopy(SessionView v, string dst, IProgress<(int, int)>? progress, CancellationToken ct)
    {
        var s = v.Session;
        string newId = Json.NewUuid7();
        // Manifest (alle Texte durch die Leseschicht)
        var man = (JsonObject)v.CleanNode(s.Manifest)!;
        man["sessionId"] = newId;
        man["title"] = v.Title;
        man["state"] = "complete";
        man.Remove("history");
        man["derivation"] = new JsonObject { ["kind"] = "sanitizedCopy", ["atUtc"] = Json.UtcNow(), ["tool"] = "StepRecorder " + AppInfo.Version };
        Json.WriteAtomic(Path.Combine(dst, "manifest.json"), Json.Pretty(man));

        // Tipp-Schritte, deren Text eine Verallgemeinerung oder Schwärzung enthält: Tasten in der Kopie entschärfen,
        // sonst stünde der Wert nach dem Neubau der Schritte wieder im Klartext
        var neuter = new HashSet<long>();
        var rawSteps = (s.LoadSteps()?.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().GroupBy(x => x.Long("anchorSeq")).ToDictionary(g => g.Key, g => g.First());
        foreach (var st in v.AllWithInactive.Where(x => x.Kind == "type"))
        {
            var raw = rawSteps.GetValueOrDefault(st.Anchor);
            string rawText = raw?.Obj("input")?.Str("text") ?? "";
            if (rawText.Length > 0 && v.Clean(rawText) != rawText)
                foreach (var rg in st.Json.Obj("seq")?.Arr("input") ?? raw?.Obj("seq")?.Arr("input") ?? new JsonArray())
                    for (long q = rg![0]!.GetValue<long>(); q <= rg[1]!.GetValue<long>(); q++) neuter.Add(q);
        }

        var lines = new List<JsonObject>(s.Events.Count);
        int i = 0, total = s.Events.Count;
        foreach (var e in s.Events)
        {
            ct.ThrowIfCancellationRequested();
            if (e.Str("type") == "frame" && e.Str("id") is { } fid)
            {
                var f = (JsonObject)v.CleanNode(e)!;
                if (v.FrameAvailable(fid) && v.Frame(fid) is { } bmp)
                    using (bmp) { var png = FrameBlobs.EncodeKey(bmp); f["store"] = new JsonObject { ["kind"] = "key", ["blob"] = FrameBlobs.Write(dst, png), ["bytes"] = png.Length }; }
                else { f["store"] = new JsonObject { ["kind"] = "none", ["reason"] = "redacted" }; f.Remove("cursor"); }
                lines.Add(f);
            }
            else
            {
                var o = v.EventForOutput(e);
                if (neuter.Contains(o.Long("seq")) && o.Str("type") is "key.down" or "key.up") { o["key"] = "Redacted"; o["vk"] = 0; o["sc"] = 0; o.Remove("ch"); o["redacted"] = new JsonArray("ch"); }
                if (o["sessionId"] != null) o["sessionId"] = newId;
                lines.Add(o);
            }
            if (++i % 200 == 0) progress?.Report((i, total));
        }
        Redactor.RewriteJournal(dst, lines);

        // Overlay: ohne Regelwerte (Verallgemeinerungen, Text- und Bildschwärzungen sind angewendet), ohne interne Kommentare
        var ed = (JsonObject)v.CleanNode(v.Edits)!;
        ed.Remove("substitutions");
        if (ed["redactions"] is JsonArray reds)
            foreach (var r in reds.OfType<JsonObject>().Where(r => r.Str("kind") != "range").ToList()) reds.Remove(r);
        if (ed["comments"] is JsonArray cs)
            foreach (var c in cs.OfType<JsonObject>().Where(c => c.Str("origin") != "recording" && (SessionView.IsInternalKind(c.Str("kind")) || c.Bool("hidden"))).ToList()) cs.Remove(c);
        foreach (var a in (ed.Obj("guide")?.Arr("append") ?? new JsonArray()).OfType<JsonObject>()) a.Remove("path");
        ed.Remove("qualityIgnores");
        // Lesezeichen sind Arbeitsnotizen des Autors (wie ignorierte Befunde)
        foreach (var st in (ed.Arr("steps") ?? new JsonArray()).OfType<JsonObject>()) st.Remove("bookmark");
        ed["sessionId"] = newId;
        ed["revision"] = 1;
        ed["savedBy"] = "sanitizer";
        ed["savedAtUtc"] = Json.UtcNow();
        Json.WriteAtomic(Path.Combine(dst, "edits.json"), Json.Pretty(ed));

        // eigene Bilder des Autors: mit Schwärzungen neu speichern
        var assets = Path.Combine(s.Path, "assets");
        if (Directory.Exists(assets))
        {
            Directory.CreateDirectory(Path.Combine(dst, "assets"));
            foreach (var file in Directory.EnumerateFiles(assets))
            {
                using var bmp = v.Frame("u:" + Path.GetFileName(file));
                if (bmp == null) continue;
                bmp.Save(Path.Combine(dst, "assets", Path.GetFileName(file)), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        var cursors = Path.Combine(s.Path, "cursors");
        if (Directory.Exists(cursors))
            foreach (var file in Directory.EnumerateFiles(cursors, "*", SearchOption.AllDirectories))
            {
                var d = Path.Combine(dst, Path.GetRelativePath(s.Path, file));
                Directory.CreateDirectory(Path.GetDirectoryName(d)!);
                File.Copy(file, d);
            }
    }

    /// <summary>Werte, die in der Kopie nirgends stehen dürfen (mindestens 3 Zeichen).</summary>
    static List<string> Needles(SessionView v, string folder)
    {
        var list = new List<string>();
        void Add(string? x) { x = x?.Trim(); if (x is { Length: >= 3 }) list.Add(x); }
        foreach (var sb in v.Edits.Arr("substitutions") ?? new JsonArray()) Add(sb.Obj("match")?.Str("literal"));
        foreach (var r in (v.Edits.Arr("redactions") ?? new JsonArray()).OfType<JsonObject>())
        {
            if (r.Str("kind") == "text")
                foreach (var tg in r.Arr("targets") ?? new JsonArray())
                {
                    var e = v.Session.BySeq(tg.Long("seq"));
                    if (e == null || tg.Arr("range") is not { Count: 2 } rg) continue;
                    var val = FieldValue(e, tg.Str("field") ?? "");
                    int a = rg[0]!.GetValue<int>(), b = rg[1]!.GetValue<int>();
                    if (val != null && a >= 0 && b <= val.Length && b > a) Add(val[a..b]);
                }
            if (r.Str("kind") == "range")
            {
                var rawSteps = (v.Session.LoadSteps()?.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().GroupBy(x => x.Long("anchorSeq")).ToDictionary(g => g.Key, g => g.First());
                foreach (var st in v.AllWithInactive.Where(x => x.Kind == "type" && x.Redacted))
                    if (rawSteps.GetValueOrDefault(st.Anchor)?.Obj("input")?.Str("text") is { Length: >= 4 } t) Add(t);
            }
        }
        foreach (var c in (v.Edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>())
        {
            if (c.Str("origin") == "recording" && (c.Bool("hidden") || c.Str("kind") == "internal") && c.LongN("sourceSeq") is long src)
                Add(v.Session.BySeq(src)?.Str("text"));
            else if (SessionView.IsInternalKind(c.Str("kind")) || c.Bool("hidden")) Add(c.Str("text"));
        }
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    static string? FieldValue(JsonObject e, string path)
    {
        JsonNode? cur = e;
        foreach (var p in path.Split('.'))
        {
            int b = p.IndexOf('[');
            if (b > 0) { var arr = cur?[p[..b]] as JsonArray; int idx = int.TryParse(p[(b + 1)..^1], out var n) ? n : -1; cur = arr != null && idx >= 0 && idx < arr.Count ? arr[idx] : null; }
            else cur = cur?[p];
        }
        return cur is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;
    }

    /// <summary>Erste Textdatei der Kopie, die einen geschützten Wert enthält (relativer Pfad), sonst null.</summary>
    static string? FindLeak(string folder, List<string> needles)
    {
        if (needles.Count == 0) return null;
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            string text;
            try { text = File.ReadAllText(file, Encoding.UTF8); } catch { continue; }
            // JSON kann Zeichen als \uXXXX maskieren: zusätzlich alle Zeichenketten entschlüsselt prüfen
            var sb = new StringBuilder(text);
            void Collect(JsonNode? n)
            {
                switch (n)
                {
                    case JsonObject o: foreach (var kv in o) { sb.Append('\n').Append(kv.Key); Collect(kv.Value); } break;
                    case JsonArray a: foreach (var x in a) Collect(x); break;
                    case JsonValue jv when jv.TryGetValue<string>(out var s): sb.Append('\n').Append(s); break;
                }
            }
            try { Collect(JsonNode.Parse(text)); }
            catch { foreach (var line in text.Split('\n')) try { if (line.Length > 0) Collect(JsonNode.Parse(line)); } catch { } }
            var all = sb.ToString();
            foreach (var n in needles)
                if (all.Contains(n, StringComparison.OrdinalIgnoreCase))
                    return Path.GetRelativePath(folder, file);
        }
        return null;
    }
}
