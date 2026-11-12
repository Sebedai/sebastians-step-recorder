using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Programm-Kontext für Ausgaben (MCP <c>get_failure_context</c>, <c>get_session</c>, <c>trace.md</c>, Fehlerbericht):
/// was die Anwendung tat – Build-Stand, Programmende, Hänger, Fakten der Ereignisanzeige (12, Paket 10), später
/// Fehlertexte und Protokollzeilen. Liest ausschließlich über die Leseschicht: geschwärzte Zeiträume fehlen.
/// </summary>
public static class DeveloperContext
{
    /// <summary>Ereignisse einer Art in Ausgabesicht (Schwärzungen angewendet, geschwärzte ausgelassen).</summary>
    public static IEnumerable<JsonObject> Events(SessionView v, string type)
    {
        foreach (var e in v.Session.Events)
        {
            if (e.Str("type") != type) continue;
            var o = v.EventForOutput(e);
            if (o.Arr("redacted")?.Any(x => x?.GetValue<string>() == "*") == true) continue;
            yield return o;
        }
    }

    /// <summary>Build-Stand je Programm (aus <c>process.info</c>): Produkt, Version, Build-Kennung, Git-Arbeitsstand.</summary>
    public static JsonArray Builds(SessionView v)
    {
        var arr = new JsonArray();
        foreach (var g in Events(v, "process.info").GroupBy(e => e.Str("process") ?? "", StringComparer.OrdinalIgnoreCase))
        {
            var e = g.First();
            arr.Add(new JsonObject
            {
                ["process"] = e.Str("process"), ["product"] = e.Str("product"), ["version"] = e.Str("version"),
                ["build"] = e.Obj("build")?.DeepClone(), ["git"] = e.Obj("git")?.DeepClone(), ["startedBeforeSession"] = e["startedBeforeSession"]?.DeepClone(),
                ["exitNotObservable"] = e.Str("handle") == "denied" ? true : null
            }.Clean());
        }
        return arr;
    }

    public static JsonObject? Build(SessionView v, long tFrom, long tTo, string lang)
    {
        var o = new JsonObject();
        var exits = Events(v, "process.exit").Where(e => e.Str("class") != "normal").Select(e => (JsonNode)new JsonObject
        {
            ["process"] = e.Str("process"), ["t"] = Export.Fmt.Time(e.Long("t"), "en"), ["class"] = e.Str("class"), ["exitCode"] = e.Str("exitCodeHex"),
            ["meaning"] = e.LongN("exitCode") is long c ? Capture.ExitCodes.Name((uint)c, lang) : null, ["step"] = StepOf(v, e)
        }.Clean()).ToArray();
        if (exits.Length > 0) o["exits"] = new JsonArray(exits);
        var hangs = Events(v, "window.hang").Where(e => e.Str("phase") == "end").Select(e => (JsonNode)new JsonObject
        {
            ["process"] = e.Str("process"), ["t"] = Export.Fmt.Time(e.Long("t"), "en"), ["durationMs"] = e.LongN("durationMs"), ["untilStop"] = e.Bool("untilStop") ? true : null
        }.Clean()).ToArray();
        if (hangs.Length > 0) o["hangs"] = new JsonArray(hangs);
        var logs = Events(v, "eventlog.entry").Select(e => (JsonNode)new JsonObject
        {
            ["process"] = e.Str("process"), ["t"] = Export.Fmt.Time(e.Long("t"), "en"), ["eventId"] = e.Long("eventId"), ["provider"] = e.Str("provider"), ["facts"] = e.Obj("facts")?.DeepClone(),
            ["text"] = v.EventLogText(e)
        }.Clean()).ToArray();
        if (logs.Length > 0) o["eventLog"] = new JsonArray(logs);
        if (Builds(v) is { Count: > 0 } b) o["builds"] = b;
        var snaps = Snapshots(v, tFrom, tTo);
        if (snaps.Count > 0) o["errorTexts"] = snaps;
        var lines = LogLines(v, tFrom, tTo);
        if (lines.Count > 0) o["logLines"] = lines;
        return o.Count == 0 ? null : o;
    }

    static int? StepOf(SessionView v, JsonObject e)
    {
        long seq = e.Long("seq");
        return v.All.FirstOrDefault(s => s.Anchor == seq || (s.Json.Obj("seq")?.Arr("related")?.Any(r => r?.GetValue<long>() == seq) ?? false))?.Number;
    }

    /// <summary>Text-Schnappschüsse (Fehlertexte) im Zeitraum (Paket 11).</summary>
    public static JsonArray Snapshots(SessionView v, long tFrom, long tTo)
    {
        var arr = new JsonArray();
        foreach (var e in Events(v, "ui.snapshot").Where(e => e.Long("t") >= tFrom - 2_000_000 && e.Long("t") <= tTo + 5_000_000).Take(12))
            arr.Add(new JsonObject { ["t"] = Export.Fmt.Time(e.Long("t"), "en"), ["trigger"] = e.Str("trigger"), ["window"] = e.Str("title"), ["text"] = SnapshotText(e), ["incomplete"] = e.Bool("incomplete") ? true : null }.Clean());
        return arr;
    }

    /// <summary>Sichtbarer Wortlaut eines Schnappschusses, Zeile je Knoten (Name und Wert).</summary>
    public static string SnapshotText(JsonObject snap)
    {
        var lines = new List<string>();
        foreach (var n in snap.Arr("nodes")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            string name = n.Str("name") ?? "", val = n.Str("value") ?? "";
            string line = val.Length > 0 && val != name ? (name.Length > 0 ? name + ": " + val : val) : name;
            if (line.Length > 0 && !lines.Contains(line)) lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    /// <summary>Protokollzeilen im Zeitraum (Paket 13).</summary>
    public static JsonArray LogLines(SessionView v, long tFrom, long tTo)
    {
        var arr = new JsonArray();
        foreach (var e in Events(v, "log.lines").Where(e => e.Long("t") >= tFrom - 5_000_000 && e.Long("t") <= tTo + 5_000_000))
        {
            var text = v.LogLinesText(e);
            if (text == null) continue;
            arr.Add(new JsonObject { ["t"] = Export.Fmt.Time(e.Long("t"), "en"), ["file"] = e.Str("file"), ["lines"] = text, ["truncated"] = e.Bool("truncated") ? true : null }.Clean());
            if (arr.Count >= 20) break;
        }
        return arr;
    }
}
