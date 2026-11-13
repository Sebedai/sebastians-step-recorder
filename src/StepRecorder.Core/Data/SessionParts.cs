using System.Text.Json.Nodes;

namespace StepRecorder.Data;

/// <summary>
/// Ein Aufnahmeabschnitt (04 §7.4), abgeleitet aus dem Journal: Er reicht vom <c>session.start</c> bis vor das nächste.
/// <c>Purpose</c>: initial | append | replacement. <c>Replaces</c> nur bei „replacement“.
/// </summary>
public sealed record SessionPart(int Part, string Purpose, long FirstSeq, long LastSeq, long TStart, long TEnd, string? StartUtc, string? EndUtc,
    string StopReason, PartRange? Replaces)
{
    /// <summary>Beim Aufnehmen verworfen (<c>session.stop</c> mit Grund „discarded“); <c>edits.sections</c> kann das überschreiben.</summary>
    public bool StoppedAsDiscarded => StopReason == "discarded";
    public bool Contains(long seq) => seq >= FirstSeq && seq <= LastSeq;
}

/// <summary>Bereich eines Abschnitts, für den neu aufgenommen wurde bzw. der ersetzt wird (<c>section</c>, <c>fromSeq</c>, <c>toSeq</c>).</summary>
public sealed record PartRange(int Section, long FromSeq, long ToSeq)
{
    public JsonObject ToJson() => new() { ["section"] = Section, ["fromSeq"] = FromSeq, ["toSeq"] = ToSeq };
    public static PartRange? From(JsonObject? o) => o == null || o["section"] == null ? null : new PartRange(o.Int("section"), o.Long("fromSeq"), o.Long("toSeq"));
    public bool Contains(long seq) => seq >= FromSeq && seq <= ToSeq;
    public bool Overlaps(PartRange o) => o.Section == Section && FromSeq <= o.ToSeq && o.FromSeq <= ToSeq;
}

public static class SessionParts
{
    /// <summary>Abschnitte in Journal-Reihenfolge. Ein Journal ohne <c>session.start</c> gilt als ein Abschnitt 0.</summary>
    public static List<SessionPart> Read(Session s) => Read(s.Events);

    public static List<SessionPart> Read(IReadOnlyList<JsonObject> events)
    {
        var list = new List<SessionPart>();
        int i = 0;
        while (i < events.Count)
        {
            var start = events[i];
            int j = i + 1;
            while (j < events.Count && events[j].Str("type") != "session.start") j++;
            bool isStart = start.Str("type") == "session.start";
            int part = isStart && start["part"] != null ? start.Int("part") : list.Count;
            string purpose = (isStart ? start.Str("purpose") : null) switch
            {
                "replacement" => "replacement", "append" => "append", "initial" => "initial",
                _ => part == 0 ? "initial" : "append"
            };
            JsonObject? stop = null; long tEnd = 0;
            for (int k = i; k < j; k++)
            {
                if (events[k].Str("type") == "session.stop") stop = events[k];
                tEnd = Math.Max(tEnd, events[k].Long("t"));
            }
            var replaces = purpose == "replacement" ? PartRange.From(start.Obj("replaces")) : null;
            list.Add(new SessionPart(part, purpose, events[i].Long("seq"), events[j - 1].Long("seq"), start.Long("t"), tEnd,
                isStart ? start.Str("utc") : null, stop?.Str("utc"), stop?.Str("reason") ?? "", replaces));
            i = j;
        }
        return list;
    }
}
