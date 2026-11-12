using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Ein Prüfposten der Durchsicht außerhalb getippter Eingaben (12, Paket 12; 10 §11.1):
/// inhaltstragende Programmausgaben (Freitext der Ereignisanzeige, Protokollzeilen, Zwischenablage-Inhalt) und
/// auffällige Texte des Autors (Kommentare, Marker-Bezeichnungen, Ziel-Angaben).
/// </summary>
public sealed record ReviewItem(string Key, string Kind, long Seq, string Text, string? FlagReason, string? Status, string Source)
{
    /// <summary>Muss entschieden werden, bevor eine Ausgabe ohne Platzhalter entsteht.</summary>
    public bool Required => Kind == "content" || FlagReason != null;
    public bool Reviewed => Status is "kept" or "redacted";
}

public sealed partial class SessionView
{
    /// <summary>Inhaltstragende Ereignisse und ihr Inhaltsfeld. Sie stehen nie in <see cref="Redactor"/>s strukturellen Arten.</summary>
    public static readonly IReadOnlyDictionary<string, string> ContentFields = new Dictionary<string, string>
    {
        ["eventlog.entry"] = "text", ["log.lines"] = "lines", ["clipboard"] = "content"
    };

    readonly Dictionary<long, ReviewItem> _contentItems = new();
    readonly List<ReviewItem> _authorItems = new();
    /// <summary>Auffällige Stellen in Texten des Autors, die ohne Durchsicht in Ausgaben ohne Oberfläche ersetzt werden.</summary>
    readonly List<string> _unreviewedSpans = new();

    /// <summary>Alle Prüfposten außerhalb getippter Eingaben (Programmausgaben und Texte des Autors).</summary>
    public IReadOnlyList<ReviewItem> ReviewItems => _contentItems.Values.Concat(_authorItems).ToList();

    /// <summary>Noch nicht entschiedene, pflichtige Prüfposten außerhalb getippter Eingaben.</summary>
    public int UnreviewedItems => _contentItems.Values.Count(i => i.Required && !i.Reviewed) + _authorItems.Count(i => i.Required && !i.Reviewed);

    Dictionary<string, string> ItemStatus()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var i in Edits.Obj("review")?.Arr("inputs")?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            if (i.Str("key") is { } k && i.Str("status") is { } st) d[k] = st;
        return d;
    }

    /// <summary>Prüfposten neu bestimmen (nach jeder Bearbeitung, vor dem Aufbau der Schritte).</summary>
    void BuildReviewItems()
    {
        _contentItems.Clear(); _authorItems.Clear(); _unreviewedSpans.Clear();
        var status = ItemStatus();
        foreach (var e in Session.Events)
        {
            string type = e.Str("type") ?? "";
            long seq = e.Long("seq");
            if (ContentFields.TryGetValue(type, out var field) && e.Str(field) is { Length: > 0 } text && !InRedactedRange(seq))
            {
                string key = "seq:" + seq;
                _contentItems[seq] = new ReviewItem(key, "content", seq, text, Patterns.Flag(text), status.GetValueOrDefault(key), type);
            }
            else if (type == "comment" && e.Str("text") is { Length: > 0 } ct && !IsHiddenComment(seq) && !InRedactedRange(seq)) AuthorItem("seq:" + seq, seq, ct, "comment", status);
            else if (type == "marker" && e.Str("label") is { Length: > 0 } ml && !InRedactedRange(seq)) AuthorItem("seq:" + seq, seq, ml, "marker", status);
        }
        foreach (var c in (Edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>().Where(c => c.Str("origin") != "recording" && !IsInternalKind(c.Str("kind")) && !c.Bool("hidden")))
            if (c.Str("id") is { } id && c.Str("text") is { Length: > 0 } t) AuthorItem("comment:" + id, c.Long("anchor"), t, "note", status);
        var goal = Session.Manifest.Obj("goal");
        foreach (var k in new[] { "description", "expected", "reference" })
        {
            string? gv = Edits.Obj("goal")?[k] != null ? Edits.Obj("goal").Str(k) : goal?.Str(k);
            if (!string.IsNullOrEmpty(gv)) AuthorItem("goal:" + k, 0, gv, "goal", status);
        }
    }

    void AuthorItem(string key, long seq, string text, string source, Dictionary<string, string> status)
    {
        string? flag = Patterns.Flag(text);
        var item = new ReviewItem(key, "authorText", seq, text, flag, status.GetValueOrDefault(key), source);
        _authorItems.Add(item);
        if (flag != null && !item.Reviewed)
            foreach (var (start, len, kind) in Patterns.SensitiveSpans(text))
                if (kind is "keyOrToken" or "passwordInCommand" or "iban" or "cardNumber" && len >= 4) _unreviewedSpans.Add(text.Substring(start, len));
    }

    /// <summary>Ungeprüfte auffällige Stellen in Ausgaben ohne Oberfläche durch <c>{unreviewed:N}</c> ersetzen.</summary>
    string MaskUnreviewed(string text)
    {
        if (!Headless || _unreviewedSpans.Count == 0) return text;
        foreach (var s in _unreviewedSpans.OrderByDescending(x => x.Length)) text = text.Replace(s, "{unreviewed:" + s.Length + "}", StringComparison.Ordinal);
        return text;
    }

    /// <summary>Text-Schnappschüsse eines Schritts in Ausgabesicht (Paket 11): Auslöser, Fenstertitel, Wortlaut, unvollständig.</summary>
    public List<(string trigger, string title, string text, bool incomplete)> SnapshotsOf(ViewStep s)
    {
        var list = new List<(string, string, string, bool)>();
        if (s.Redacted) return list;
        foreach (var n in s.Json.Arr("snapshots") ?? new JsonArray())
        {
            if (n is not JsonValue jv || !jv.TryGetValue<long>(out var seq) || Session.BySeq(seq) is not { } ev) continue;
            var o = EventForOutput(ev);
            if (o.Arr("redacted")?.Any(x => x?.GetValue<string>() is "*") == true) continue;
            string text = DeveloperContext.SnapshotText(o);
            if (text.Length > 0) list.Add((o.Str("trigger") ?? "", o.Str("title") ?? "", text, o.Bool("incomplete")));
        }
        return list;
    }

    /// <summary>Inhalt eines inhaltstragenden Ereignisses in Ausgabesicht: geschwärzt → null, ungeprüft ohne Oberfläche → Platzhalter.</summary>
    public string? ContentText(JsonObject outputEvent)
    {
        string type = outputEvent.Str("type") ?? "";
        if (!ContentFields.TryGetValue(type, out var field)) return null;
        if (outputEvent.Arr("redacted")?.Any(x => x?.GetValue<string>() is "*") == true) return null;
        return outputEvent.Str(field);
    }

    /// <summary>Freitext eines Eintrags der Ereignisanzeige (z. B. .NET-Stacktrace) in Ausgabesicht.</summary>
    public string? EventLogText(JsonObject outputEvent) => ContentText(outputEvent);

    /// <summary>Protokollzeilen eines <c>log.lines</c>-Ereignisses in Ausgabesicht.</summary>
    public string? LogLinesText(JsonObject outputEvent) => ContentText(outputEvent);

    /// <summary>Inhaltsfeld eines Rohereignisses für Ausgaben anpassen (in <see cref="EventForOutput"/>).</summary>
    void ContentForOutput(JsonObject c, long seq)
    {
        if (!ContentFields.TryGetValue(c.Str("type") ?? "", out var field) || c.Str(field) is not { } text) return;
        if (Headless && _contentItems.TryGetValue(seq, out var item) && item.Required && !item.Reviewed)
        {
            c[field] = "{unreviewed:" + text.Length + "}";
            c["unreviewed"] = true;
        }
    }
}
