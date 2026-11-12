using System.Text;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Suchanfrage: Leerzeichen verknüpft mit UND, „…“ oder "…" sucht eine Wortfolge; Groß-/Kleinschreibung egal.
/// </summary>
public sealed class SearchQuery
{
    public IReadOnlyList<string> Terms { get; }
    public bool IsEmpty => Terms.Count == 0;

    SearchQuery(List<string> terms) => Terms = terms;

    public static SearchQuery Parse(string? text)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return new SearchQuery(terms);
        var sb = new StringBuilder();
        bool phrase = false;
        void Flush() { var t = Normalize(sb.ToString()); if (t.Length > 0) terms.Add(t); sb.Clear(); }
        foreach (var c in text)
        {
            if (c is '"' or '„' or '“' or '”' or '»' or '«') { Flush(); phrase = !phrase; continue; }
            if (!phrase && char.IsWhiteSpace(c)) { Flush(); continue; }
            sb.Append(c);
        }
        Flush();
        return new SearchQuery(terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>Mehrfache Leerzeichen und Zeilenumbrüche zu einem Leerzeichen.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool space = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
            if (space) { sb.Append(' '); space = false; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    public bool Matches(string haystack)
    {
        if (IsEmpty) return true;
        var h = Normalize(haystack);
        return Terms.All(t => h.Contains(t, StringComparison.CurrentCultureIgnoreCase));
    }
}

/// <summary>
/// Suche in der Schrittliste des Editors: Überschrift, Anleitungs- und Protokolltext, eigener Text, Kapitel, Kommentare und
/// Hinweiskästen. Nur für den Editor – die Suche über alle Aufnahmen und MCP nutzen eigene, schutzgeprüfte Einträge.
/// </summary>
public static class StepSearch
{
    public static string Haystack(ViewStep s)
    {
        var sb = new StringBuilder();
        void Add(string? t) { if (!string.IsNullOrEmpty(t)) sb.Append(t).Append('\n'); }
        Add(s.Title); Add(s.AuthorTitle); Add(s.AuthorBody); Add(s.Guide); Add(s.Protocol);
        Add(s.ChapterTitle); Add(s.ChapterText);
        foreach (var c in s.Comments) Add(c.Str("text"));
        foreach (var c in s.InternalComments) Add(c.Str("text"));
        return sb.ToString();
    }

    public static bool Matches(ViewStep s, SearchQuery q) => q.IsEmpty || q.Matches(Haystack(s));
}
