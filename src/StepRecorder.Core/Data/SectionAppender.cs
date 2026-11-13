using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Settings;

namespace StepRecorder.Data;

/// <summary>Ergebnis von <see cref="SectionAppender.Append"/>.</summary>
public sealed record SectionAppendResult(int Part, long FirstSeq, long LastSeq, string? ReplacementId, bool CleanStop);

/// <summary>
/// Weitere Aufnahmeabschnitte (03 §1.7, 04 §7.4): Eine eigenständig und sichtbar mit dem normalen Recorder gemachte Aufnahme
/// wird nach ihrem Stopp als neuer Abschnitt an eine abgeschlossene Sitzung <b>angehängt</b>. Vorhandene Zeilen und Bilder bleiben
/// bytegleich; <c>seq</c>, Sitzungsuhr, Eingabenummer, Epochen und Kennungen laufen fort, die Prüfsummenkette wird fortgesetzt.
/// Bei <c>purpose: "replacement"</c> und sauberem Stopp entsteht zugleich der Eintrag in <c>edits.replacements</c> (04 §13.13).
/// </summary>
public static partial class SectionAppender
{
    /// <summary>Unterordner der Sitzung, in dem die Neuaufnahme bis zum Anhängen liegt (wird nie gepackt, danach gelöscht).</summary>
    public const string PendingFolder = ".pending-section";

    public static string PendingRoot(string sessionFolder) => Path.Combine(sessionFolder, PendingFolder);

    static readonly string[] TimeKeys = { "t", "tObs", "tEnd", "tFrom", "tTo" };
    static readonly string[] SeqRefKeys = { "ref", "dwellRef", "focus", "markerSeq", "refSeq" };
    static readonly string[] IdKeys = { "id", "frame", "win", "fg", "owner", "prev", "ownerWin", "gapId", "proc", "clip" };

    [GeneratedRegex(@"^([A-Za-z]+)(\d+)$")] private static partial Regex IdPattern();

    /// <summary>
    /// Vorprüfung (03 §1.7, Voraussetzungen 1–4): null, wenn ein weiterer Abschnitt angehängt werden darf; sonst der Grund.
    /// Schreibt nichts in die Sitzung.
    /// </summary>
    public static string? Precheck(string folder)
    {
        if (File.Exists(folder) && folder.EndsWith(".steprec", StringComparison.OrdinalIgnoreCase))
            return Loc.L("Die Aufnahme ist gepackt (.steprec) – bitte zuerst entpacken.", "The recording is packed (.steprec) – please extract it first.");
        if (!Directory.Exists(folder) || !File.Exists(Path.Combine(folder, "manifest.json")))
            return Loc.L("Die Aufnahme wurde nicht gefunden.", "The recording was not found.");
        var man = Json.ReadObject(Path.Combine(folder, "manifest.json"));
        if (man == null) return Loc.L("manifest.json ist ungültig.", "manifest.json is invalid.");
        if ((man.Str("state") ?? "complete") != "complete")
            return Loc.L("Die Aufnahme ist nicht sauber abgeschlossen (sie läuft noch oder wurde nach einem Absturz wiederhergestellt).", "The recording was not finished cleanly (it is still running or was recovered after a crash).");
        if (Session.IsLockAlive(folder)) return Loc.L("Die Aufnahme wird gerade von einem anderen Vorgang geschrieben.", "The recording is currently being written by another process.");
        if ((man.Str("schemaVersion") ?? "1.0") != SessionWriter.SchemaVersion)
            return Loc.F("Die Aufnahme hat die Formatversion {0}; dieses Programm schreibt {1}.", "The recording has format version {0}; this program writes {1}.", man.Str("schemaVersion") ?? "?", SessionWriter.SchemaVersion);
        var journal = Path.Combine(folder, "events.jsonl");
        if (!File.Exists(journal)) return Loc.L("events.jsonl fehlt.", "events.jsonl is missing.");
        try { if (new FileInfo(journal).IsReadOnly) return Loc.L("Die Aufnahme liegt schreibgeschützt vor.", "The recording is read-only."); }
        catch (Exception ex) { return ex.Message; }
        List<JsonObject> events;
        try { using var fs = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); var r = Session.ReadJournal(fs); if (r.invalidAtByte >= 0) return Loc.L("Das Journal ist beschädigt.", "The journal is damaged."); events = r.events; }
        catch (Exception ex) { return ex.Message; }
        if (events.LastOrDefault(e => e.Str("type") != "session.checkpoint")?.Str("type") != "session.stop")
            return Loc.L("Das Journal endet nicht mit dem Ende einer Aufnahme.", "The journal does not end with the end of a recording.");
        if (man.Obj("journal")?.LongN("lastSeq") is long ls && ls != events[^1].Long("seq"))
            return Loc.L("Journal und Manifest passen nicht zusammen.", "Journal and manifest do not match.");
        return null;
    }

    /// <summary>
    /// Hängt die Aufnahme <paramref name="source"/> (eigener Sitzungsordner, ein Abschnitt) als neuen Abschnitt an <paramref name="target"/> an.
    /// <paramref name="purpose"/>: „append“ oder „replacement“ (dann mit <paramref name="replaces"/>). Die Quelle bleibt unverändert;
    /// der Aufrufer löscht sie danach. Wirft bei nicht erfüllten Voraussetzungen, bevor irgendetwas geschrieben wird.
    /// </summary>
    public static SectionAppendResult Append(string target, string source, string purpose = "append", PartRange? replaces = null)
    {
        if (purpose is not ("append" or "replacement")) throw new ArgumentException("purpose");
        if (purpose == "replacement" && replaces == null) throw new ArgumentException("replaces");
        if (Precheck(target) is { } err) throw new InvalidOperationException(err);

        using var src = Session.Open(source, recover: true);
        var srcParts = SessionParts.Read(src);
        if (srcParts.Count != 1) throw new InvalidOperationException(Loc.L("Die neue Aufnahme besteht selbst aus mehreren Abschnitten.", "The new recording itself consists of several parts."));
        if (srcParts[0].StoppedAsDiscarded) throw new InvalidOperationException(Loc.L("Die neue Aufnahme wurde verworfen.", "The new recording was discarded."));
        bool clean = srcParts[0].StopReason == "user" && src.State == "complete";

        using var tgt = Session.Open(target, recover: false);
        var parts = SessionParts.Read(tgt);
        if (replaces != null)
        {
            var rp = parts.FirstOrDefault(p => p.Part == replaces.Section);
            if (rp == null || replaces.FromSeq > replaces.ToSeq || !rp.Contains(replaces.FromSeq) || !rp.Contains(replaces.ToSeq))
                throw new InvalidOperationException(Loc.L("Der zu ersetzende Bereich liegt nicht in einem Abschnitt der Aufnahme.", "The range to replace does not lie within one part of the recording."));
        }
        int newPart = parts.Count == 0 ? 1 : parts.Max(p => p.Part) + 1;

        // Fortlaufende Größen der Zielsitzung
        long seqBase = tgt.Events[^1].Long("seq");
        long tBase = Math.Max(tgt.Manifest.Long("durationUs"), tgt.Events.Max(e => e.Long("t")));
        long inBase = 0, epBase = -1;
        var idMax = new Dictionary<string, long>(StringComparer.Ordinal);
        var redIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in tgt.Events)
        {
            inBase = Math.Max(inBase, Math.Max(e.Long("inSeq"), e.Long("inSeqEnd")));
            if (e.LongN("epoch") is long ep1) epBase = Math.Max(epBase, ep1);
            if (e.LongN("ep") is long ep2) epBase = Math.Max(epBase, ep2);
            foreach (var k in IdKeys) NoteId(idMax, e.Str(k));
            NoteId(idMax, e.Obj("store")?.Str("base")); NoteId(idMax, e.Obj("store")?.Str("as"));
            if (e.Str("redactionId") is { } rid) redIds.Add(rid);
        }
        epBase += 1;
        var tgtEdits = tgt.LoadEdits() ?? new JsonObject();
        foreach (var r in tgtEdits.Arr("redactions")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>()) if (r.Str("id") is { } rid) redIds.Add(rid);

        // Redaktionskennungen der Quelle („Letzte Eingabe schwärzen“) eindeutig machen
        var srcEdits = src.LoadEdits();
        var redMap = new Dictionary<string, string>(StringComparer.Ordinal);
        int rn = 0;
        string NewRedId() { string id; do id = "r" + ++rn; while (redIds.Contains(id)); redIds.Add(id); return id; }
        foreach (var r in srcEdits?.Arr("redactions")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            if (r.Str("id") is { } rid && !redMap.ContainsKey(rid)) redMap[rid] = NewRedId();

        string MapId(string v)
        {
            var m = IdPattern().Match(v);
            if (!m.Success) return v + "-p" + newPart;
            long n = long.Parse(m.Groups[2].Value) + (idMax.TryGetValue(m.Groups[1].Value, out var mx) ? mx : 0);
            return m.Groups[1].Value + n.ToString().PadLeft(m.Groups[2].Value.Length, '0');
        }

        // Ereignisse der Quelle (ohne ihre Checkpoints – die Kette wird neu fortgeschrieben)
        var srcEvents = src.Events.Where(e => e.Str("type") != "session.checkpoint").ToList();
        var seqMap = new Dictionary<long, long>();
        for (int i = 0; i < srcEvents.Count; i++) seqMap[srcEvents[i].Long("seq")] = seqBase + 1 + i;
        var lines = new List<JsonObject>(srcEvents.Count + 1);
        foreach (var e in srcEvents)
        {
            var o = (JsonObject)e.DeepClone();
            o["seq"] = seqMap[e.Long("seq")];
            foreach (var k in TimeKeys) if (IsNumber(o[k])) o[k] = o.Long(k) + tBase;
            foreach (var k in SeqRefKeys)
                if (IsNumber(o[k])) { if (seqMap.TryGetValue(o.Long(k), out var ns)) o[k] = ns; else o.Remove(k); }
            foreach (var k in new[] { "inSeq", "inSeqEnd" }) if (IsNumber(o[k])) o[k] = o.Long(k) + inBase;
            foreach (var k in new[] { "ep", "epoch" }) if (IsNumber(o[k])) o[k] = o.Long(k) + epBase;
            foreach (var k in IdKeys) if (o.Str(k) is { } v) o[k] = MapId(v);
            if (o.Str("redactionId") is { } red) o["redactionId"] = redMap.TryGetValue(red, out var nr) ? nr : MapId(red);
            if (o.Obj("store") is { } st) { if (st.Str("base") is { } b) st["base"] = MapId(b); if (st.Str("as") is { } a) st["as"] = MapId(a); }
            switch (o.Str("type"))
            {
                case "session.start":
                    o["part"] = newPart; o["purpose"] = purpose; o["sessionId"] = tgt.SessionId;
                    if (replaces != null) o["replaces"] = replaces.ToJson(); else o.Remove("replaces");
                    o.Remove("goal"); o.Remove("profile");   // weitere Abschnitte: ohne Ziel-Angaben und ohne Profil (03 §1.7)
                    break;
                case "display.topology" when o.Str("reason") == "initial":
                    o["reason"] = "partStart";
                    break;
            }
            lines.Add(o);
        }
        long firstSeq = seqBase + 1, lastT = lines.Count > 0 ? lines.Max(l => l.Long("t")) : tBase;
        long srcDuration = Math.Max(src.DurationUs, src.Events.Count > 0 ? src.Events.Max(e => e.Long("t")) : 0);
        long tEnd = Math.Max(tBase + srcDuration, lastT);

        // 1. Bilder (inhaltsadressiert – gleiche Inhalte teilen sich den Blob)
        CopyTree(Path.Combine(source, "frames"), Path.Combine(target, "frames"));
        CopyTree(Path.Combine(source, "assets"), Path.Combine(target, "assets"));

        // 2. Journal: nur anhängen; Prüfsummenkette über die vorhandenen Bytes fortsetzen
        string journalPath = Path.Combine(target, "events.jsonl");
        var existing = File.ReadAllBytes(journalPath);
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        h.AppendData(new byte[32]);
        long linesSince = 0;
        for (int pos = 0; pos < existing.Length;)
        {
            int nl = Array.IndexOf(existing, (byte)'\n', pos);
            if (nl < 0) break;
            if (Encoding.UTF8.GetString(existing, pos, nl - pos).Contains("\"type\":\"session.checkpoint\"")) { var c = h.GetHashAndReset(); h.AppendData(c); linesSince = 0; }
            else { h.AppendData(existing, pos, nl - pos + 1); linesSince++; }
            pos = nl + 1;
        }
        var buf = new MemoryStream();
        foreach (var l in lines)
        {
            var b = Encoding.UTF8.GetBytes(Json.Line(l) + "\n");
            buf.Write(b); h.AppendData(b); linesSince++;
        }
        long cpSeq = seqBase + lines.Count + 1;
        var chain = h.GetHashAndReset();
        var cp = new JsonObject { ["seq"] = cpSeq, ["t"] = tEnd, ["type"] = "session.checkpoint", ["utc"] = Json.UtcNow(), ["lines"] = linesSince, ["chain"] = "sha256:" + Convert.ToHexString(chain).ToLowerInvariant() };
        buf.Write(Encoding.UTF8.GetBytes(Json.Line(cp) + "\n"));
        using (var fs = new FileStream(journalPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            buf.Position = 0; buf.CopyTo(fs); fs.Flush(true);
        }

        // 3. Manifest
        var all = File.ReadAllBytes(journalPath);
        var man = (JsonObject)tgt.Manifest.DeepClone();
        var srcPart0 = src.Manifest.Arr("parts")?.FirstOrDefault() as JsonObject;
        var partsArr = man["parts"] as JsonArray ?? new JsonArray(new JsonObject { ["part"] = 0, ["startUtc"] = man.Str("createdUtc"), ["tStart"] = 0, ["purpose"] = "initial" });
        man["parts"] = partsArr;
        if (partsArr.Count > 0 && partsArr[^1] is JsonObject prevPart)
        {
            prevPart["tEnd"] ??= tBase;
            prevPart["lastSeq"] ??= seqBase;
        }
        var partJson = new JsonObject
        {
            ["part"] = newPart, ["startUtc"] = srcPart0?.Str("startUtc") ?? src.Manifest.Str("createdUtc"), ["endUtc"] = src.Manifest.Str("endedUtc"),
            ["tStart"] = tBase, ["tEnd"] = tEnd,
            ["qpcFrequency"] = srcPart0?["qpcFrequency"]?.DeepClone(), ["qpcAtStart"] = srcPart0?["qpcAtStart"]?.DeepClone(), ["tickCountAtStart"] = srcPart0?["tickCountAtStart"]?.DeepClone(),
            ["firstSeq"] = firstSeq, ["lastSeq"] = cpSeq, ["purpose"] = purpose, ["replaces"] = replaces?.ToJson()
        };
        partsArr.Add(partJson.Clean());
        man["state"] = "complete";
        man["durationUs"] = tEnd;
        if (src.Manifest.Str("endedUtc") is { } ended) man["endedUtc"] = ended;
        man["journal"] = new JsonObject
        {
            ["segments"] = new JsonArray(new JsonObject { ["file"] = "events.jsonl", ["firstSeq"] = 1, ["lastSeq"] = cpSeq, ["bytes"] = all.LongLength, ["sha256"] = Convert.ToHexString(SHA256.HashData(all)).ToLowerInvariant() }),
            ["events"] = cpSeq, ["lastSeq"] = cpSeq, ["lastT"] = tEnd
        };
        if (man.Obj("frames") is { } fr && src.Manifest.Obj("frames") is { } sfr)
            foreach (var k in new[] { "records", "blobs", "keyframes", "bytes" }) fr[k] = fr.Long(k) + sfr.Long(k);
        if (src.Manifest.Obj("requires") is { } req && man["requires"] == null) man["requires"] = req.DeepClone();
        var perf = man["performance"] as JsonArray ?? new JsonArray();
        foreach (var p in src.Manifest.Arr("performance")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>()) { var pc = (JsonObject)p.DeepClone(); pc["part"] = newPart; perf.Add(pc); }
        man["performance"] = perf;
        var hist = man["history"] as JsonArray ?? new JsonArray();
        hist.Add(new JsonObject { ["atUtc"] = Json.UtcNow(), ["action"] = "partAppended", ["part"] = newPart, ["purpose"] = purpose, ["tool"] = "StepRecorder " + AppInfo.Version });
        man["history"] = hist;
        Json.WriteAtomic(Path.Combine(target, "manifest.json"), Json.Pretty(man));

        // 4. Bearbeitungs-Overlay: Schwärzungen der Neuaufnahme übernehmen, Ersetzung eintragen
        string? replacementId = null;
        var srcRed = srcEdits?.Arr("redactions")?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        bool addReplacement = purpose == "replacement" && clean && replaces != null;
        if (srcRed.Count > 0 || addReplacement)
        {
            long rev = tgtEdits.Long("revision");
            var reds = tgtEdits["redactions"] as JsonArray ?? new JsonArray();
            foreach (var r in srcRed)
            {
                var c = (JsonObject)r.DeepClone();
                if (c.Str("id") is { } rid && redMap.TryGetValue(rid, out var nid)) c["id"] = nid;
                if (IsNumber(c["markerSeq"])) { if (seqMap.TryGetValue(c.Long("markerSeq"), out var ms)) c["markerSeq"] = ms; else c.Remove("markerSeq"); }
                if (c["seqRanges"] is JsonArray ranges)
                    c["seqRanges"] = new JsonArray(ranges.OfType<JsonArray>().Where(a => a.Count == 2)
                        .Select(a => (JsonNode)new JsonArray(MapSeq(seqMap, a[0]!.GetValue<long>()), MapSeq(seqMap, a[1]!.GetValue<long>()))).ToArray());
                reds.Add(c);
            }
            if (srcRed.Count > 0) tgtEdits["redactions"] = reds;
            if (addReplacement) replacementId = SectionEdits.AddReplacement(tgtEdits, replaces!, newPart);
            if (!tgt.SaveEdits(tgtEdits, rev, "recorder")) throw new InvalidOperationException(Loc.L("Die Bearbeitungen wurden zwischenzeitlich geändert.", "The edits were changed in the meantime."));
        }

        // 5. Abgeleitetes verwerfen (Suchindex, Vorschaubilder); steps.json wird beim nächsten Öffnen neu abgeleitet
        try { var cache = Path.Combine(target, "cache"); if (Directory.Exists(cache)) Directory.Delete(cache, true); } catch { }
        return new SectionAppendResult(newPart, firstSeq, cpSeq, replacementId, clean);
    }

    /// <summary>Seq der Quelle → Seq im Ziel; ein weggefallener Checkpoint nimmt das nächste erhaltene Ereignis.</summary>
    static long MapSeq(Dictionary<long, long> map, long seq)
    {
        if (map.TryGetValue(seq, out var n)) return n;
        foreach (var kv in map.OrderBy(k => k.Key)) if (kv.Key > seq) return kv.Value;
        return map.Count > 0 ? map.Values.Max() : seq;
    }

    static bool IsNumber(JsonNode? n) => n is JsonValue v && (v.TryGetValue<long>(out _) || v.TryGetValue<int>(out _) || v.TryGetValue<double>(out _));

    static void NoteId(Dictionary<string, long> max, string? v)
    {
        if (v == null) return;
        var m = IdPattern().Match(v);
        if (!m.Success || !long.TryParse(m.Groups[2].Value, out var n)) return;
        var p = m.Groups[1].Value;
        max[p] = Math.Max(max.TryGetValue(p, out var x) ? x : 0, n);
    }

    static void CopyTree(string from, string to)
    {
        if (!Directory.Exists(from)) return;
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var dst = Path.Combine(to, Path.GetRelativePath(from, f));
            if (File.Exists(dst)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            string tmp = dst + ".tmp";
            File.Copy(f, tmp, true);
            File.Move(tmp, dst, true);
        }
    }

    /// <summary>Löscht die (angehängte oder verworfene) Neuaufnahme samt leerem Zwischenordner.</summary>
    public static void DeleteSource(string source)
    {
        try { if (Directory.Exists(source)) Directory.Delete(source, true); } catch { }
        var parent = Path.GetDirectoryName(source.TrimEnd('\\', '/'));
        if (parent != null && Path.GetFileName(parent) == PendingFolder) RemoveEmptyPending(Path.GetDirectoryName(parent)!);
    }

    /// <summary>Entfernt den Zwischenordner der Sitzung, wenn er leer ist (etwa nach einer verworfenen Neuaufnahme).</summary>
    public static void RemoveEmptyPending(string sessionFolder)
    {
        try { var p = PendingRoot(sessionFolder); if (Directory.Exists(p) && !Directory.EnumerateFileSystemEntries(p).Any()) Directory.Delete(p); } catch { }
    }
}

/// <summary>Bearbeitungen über ganze Abschnitte: <c>edits.replacements[]</c> (04 §13.13).</summary>
public static class SectionEdits
{
    /// <summary>Legt eine Ersetzung an und gibt ihre Kennung zurück („rp1“ …).</summary>
    public static string AddReplacement(JsonObject edits, PartRange replaces, int bySection)
    {
        var arr = edits["replacements"] as JsonArray ?? new JsonArray();
        edits["replacements"] = arr;
        int n = arr.OfType<JsonObject>().Select(x => x.Str("id") is { } id && id.StartsWith("rp") && int.TryParse(id[2..], out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1;
        string newId = "rp" + n;
        arr.Add(new JsonObject { ["id"] = newId, ["replaces"] = replaces.ToJson(), ["by"] = new JsonObject { ["section"] = bySection }, ["atUtc"] = Json.UtcNow() });
        return newId;
    }

    /// <summary>Entfernt eine Ersetzung („Ersetzung aufheben“); die Neuaufnahme gilt danach als nicht verwendet.</summary>
    public static bool RemoveReplacement(JsonObject edits, string id)
    {
        if (edits["replacements"] is not JsonArray arr) return false;
        var hit = arr.OfType<JsonObject>().FirstOrDefault(x => x.Str("id") == id);
        if (hit == null) return false;
        arr.Remove(hit);
        if (arr.Count == 0) edits.Remove("replacements");
        return true;
    }
}
