using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Schritt teilen und Ergebnisschritt (13, Paket 34) – als Darstellung der Anleitung, ohne die Aufnahme zu verändern: Der Text des
/// Schritts wird auf ihn und einen neuen, eingefügten Schritt verteilt; der neue Schritt zeigt das Nachher-Bild. KI-Paket, MCP und
/// AutoHotkey behalten den ganzen aufgezeichneten Schritt.
/// </summary>
public static class StepSplit
{
    /// <summary>Neuen eingefügten Schritt hinter <paramref name="after"/> anlegen (auch in einer vorhandenen Reihenfolge); liefert den Anker.</summary>
    public static long InsertAfter(JsonObject edits, ViewStep after, string title, string body, string? image, string? type = null)
    {
        var arr = edits["inserted"] as JsonArray ?? new JsonArray();
        long anchor = -(1000 + arr.OfType<JsonObject>().Select(x => -x.Long("anchor") - 1000).DefaultIfEmpty(0).Max() + 1);
        long afterAnchor = after.Manual ? after.After : after.Anchor;
        var o = new JsonObject { ["anchor"] = anchor, ["after"] = afterAnchor, ["title"] = title, ["body"] = body, ["createdUtc"] = Json.UtcNow() };
        if (image != null) o["image"] = image;
        if (type != null) o["type"] = type;
        arr.Add(o);
        edits["inserted"] = arr;
        if (edits["order"] is JsonArray order && order.Count > 0)
        {
            int i = order.Select((x, ix) => (x, ix)).FirstOrDefault(p => p.x is JsonValue v && v.TryGetValue<long>(out var l) && l == after.Anchor).ix;
            order.Insert(Math.Min(order.Count, i + 1), anchor);
        }
        return anchor;
    }

    /// <summary>Eigenen Text eines Schritts in der Sprache setzen (Titel und Text).</summary>
    public static void SetText(JsonObject edits, long anchor, string lang, string title, string body)
    {
        var arr = edits["steps"] as JsonArray ?? new JsonArray(); edits["steps"] = arr;
        var se = arr.OfType<JsonObject>().FirstOrDefault(x => x.Long("anchor") == anchor);
        if (se == null) { se = new JsonObject { ["anchor"] = anchor }; arr.Add(se); }
        var text = se["text"] as JsonObject ?? new JsonObject(); se["text"] = text;
        text[lang] = new JsonObject { ["title"] = title, ["body"] = body };
    }

    /// <summary>
    /// Teilen: Schritt behält Titel 1 und Text 1, ein neuer Schritt danach bekommt Titel 2, Text 2 und das Nachher-Bild
    /// (sonst das Vorher-Bild). Liefert den Anker des neuen Schritts.
    /// </summary>
    public static long Split(JsonObject edits, ViewStep step, string lang, string title1, string body1, string title2, string body2)
    {
        SetText(edits, step.Anchor, lang, title1, body1);
        return InsertAfter(edits, step, title2, body2, step.PostFrame ?? step.PreFrame);
    }

    /// <summary>Ergebnis als eigener Schritt: Nachher-Bild des Schritts mit Überschrift „Ergebnis“.</summary>
    public static long Result(JsonObject edits, ViewStep step, string title, string body)
        => InsertAfter(edits, step, title, body, step.PostFrame ?? step.PreFrame);

    /// <summary>Vorschlag für die Teilung eines Texts an einer Stelle (an Satz- oder Wortgrenze).</summary>
    public static (string first, string second) SplitText(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        return (text[..caret].TrimEnd(), text[caret..].TrimStart());
    }
}
