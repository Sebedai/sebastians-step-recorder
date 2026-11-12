using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Steps;

namespace StepRecorder.ReadLayer;

/// <summary>Ein durchsuchbarer Text eines Schritts (aus der Ausgabesicht – Schwärzungen und Durchsicht sind schon angewendet).</summary>
public sealed record SearchEntry(string StepId, int Number, long Anchor, long T, string Field, string Text, string? MarkerKind = null);

/// <summary>Treffer der Suche über mehrere Aufnahmen.</summary>
public sealed record SearchHit(string SessionPath, string SessionId, string SessionTitle, SearchEntry Entry, string Snippet);

/// <summary>
/// Suche über Aufnahmen: <see cref="Entries"/> ist die eine Definition von „durchsuchbar“ (Bibliothek und MCP).
/// Einträge stammen nur aus Leseansichten ohne Durchsicht (<see cref="SessionView.Headless"/>): Ungeprüftes ist ein
/// Platzhalter, Geschwärztes, Verallgemeinertes und interne Kommentare sind nicht enthalten. Der Index liegt unter
/// <c>cache\search\</c> und wird bei jeder Änderung des Overlays verworfen.
/// </summary>
public static class SessionSearch
{
    public const int IndexVersion = 1;
    public static readonly string[] Fields = { "stepText", "elements", "windows", "typed", "comments", "markers" };

    public static List<SearchEntry> Entries(SessionView view)
    {
        if (!view.Headless) throw new InvalidOperationException("Suchindex nur aus einer Ausgabesicht (headless) bauen.");
        var list = new List<SearchEntry>();
        foreach (var s in view.All)
        {
            if (s.Redacted) continue;
            void Add(string field, string? text)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                list.Add(new SearchEntry(s.Id, s.Number, s.Anchor, s.TFrom, field, SearchQuery.Normalize(text), s.Kind == "marker" ? s.Json.Obj("input")?.Str("markerKind") : null));
            }
            Add("stepText", string.Join(" ", new[] { s.Title, s.Protocol, s.AuthorTitle, s.AuthorBody }.Where(x => !string.IsNullOrWhiteSpace(x))));
            Add("elements", s.Json.Obj("target")?.Obj("el")?.Str("name"));
            Add("windows", s.Json.Obj("ctx")?.Str("title"));
            if (s.Kind == "type" && !s.Unreviewed) Add("typed", s.Json.Obj("input")?.Str("text") ?? TextTemplates.TokensNotation(s.Json.Obj("input") ?? new JsonObject()));
            foreach (var c in s.Comments) Add("comments", c.Str("text"));
            if (s.Kind == "marker") Add("markers", s.Json.Obj("input")?.Str("label"));
            foreach (var sn in view.SnapshotsOf(s)) Add("windowText", sn.text);
        }
        return list;
    }

    static string IndexDir(string folder) => Path.Combine(folder, "cache", "search");

    static string Stamp(string folder, string lang)
    {
        var edits = Json.ReadObject(Path.Combine(folder, "edits.json"));
        long journal = new FileInfo(Path.Combine(folder, "events.jsonl")).Length;
        return $"{IndexVersion}|{edits?.Long("revision") ?? 0}|{journal}|{StepParams.BuilderVersion}|{lang}";
    }

    public static bool IndexValid(string folder, string lang)
    {
        try
        {
            var meta = Json.ReadObject(Path.Combine(IndexDir(folder), "meta.json"));
            return meta != null && meta.Str("stamp") == Stamp(folder, lang) && File.Exists(Path.Combine(IndexDir(folder), "entries.jsonl"));
        }
        catch { return false; }
    }

    /// <summary>Einträge einer Aufnahme aus dem Index oder neu gebaut (und als Index gespeichert, außer bei Archiven und laufenden Aufnahmen).</summary>
    public static List<SearchEntry> LoadOrBuild(string path, string lang = "de")
    {
        bool archive = File.Exists(path);
        if (!archive && IndexValid(path, lang))
        {
            var list = new List<SearchEntry>();
            foreach (var line in File.ReadLines(Path.Combine(IndexDir(path), "entries.jsonl"), Encoding.UTF8))
                if (JsonNode.Parse(line) is JsonObject o)
                    list.Add(new SearchEntry(o.Str("s") ?? "", o.Int("n"), o.Long("a"), o.Long("t"), o.Str("f") ?? "", o.Str("x") ?? "", o.Str("m")));
            return list;
        }
        if (!archive && Json.ReadObject(Path.Combine(path, "manifest.json"))?.Str("state") == "recording") return new List<SearchEntry>();
        using var v = SessionView.Open(path, headless: true, lang: lang);
        var entries = Entries(v);
        if (!archive)
            try
            {
                var dir = IndexDir(path);
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                foreach (var e in entries)
                    sb.Append(Json.Line(new JsonObject { ["s"] = e.StepId, ["n"] = e.Number, ["a"] = e.Anchor, ["t"] = e.T, ["f"] = e.Field, ["x"] = e.Text, ["m"] = e.MarkerKind }.Clean())).Append('\n');
                Json.WriteAtomic(Path.Combine(dir, "entries.jsonl"), sb.ToString());
                Json.WriteAtomic(Path.Combine(dir, "meta.json"), Json.Pretty(new JsonObject { ["format"] = "steprec.search", ["version"] = IndexVersion, ["stamp"] = Stamp(path, lang), ["sessionId"] = v.Session.SessionId, ["entries"] = entries.Count }));
            }
            catch { }
        return entries;
    }

    /// <summary>Treffer: je Schritt höchstens einer (erstes passendes Feld), mit Ausschnitt um den ersten Begriff.</summary>
    public static List<(SearchEntry entry, string snippet)> Search(IEnumerable<SearchEntry> entries, SearchQuery q, bool matchAll = true, ICollection<string>? fields = null)
    {
        var result = new List<(SearchEntry, string)>();
        if (q.IsEmpty) return result;
        var seen = new HashSet<string>();
        foreach (var e in entries)
        {
            if (fields is { Count: > 0 } && !fields.Contains(e.Field)) continue;
            if (seen.Contains(e.StepId)) continue;
            bool ok = matchAll ? q.Matches(e.Text) : q.Terms.Any(t => e.Text.Contains(t, StringComparison.CurrentCultureIgnoreCase));
            if (!ok) continue;
            seen.Add(e.StepId);
            result.Add((e, Snippet(e.Text, q.Terms.FirstOrDefault(t => e.Text.Contains(t, StringComparison.CurrentCultureIgnoreCase)) ?? q.Terms[0])));
        }
        return result;
    }

    /// <summary>Ausschnitt um den Begriff, Fundstelle in «…».</summary>
    public static string Snippet(string text, string term)
    {
        int idx = text.IndexOf(term, StringComparison.CurrentCultureIgnoreCase);
        if (idx < 0) return text.Length <= 80 ? text : text[..80] + "…";
        int from = Math.Max(0, idx - 30), len = Math.Min(text.Length - from, 80 + term.Length);
        string pre = text.Substring(from, idx - from), hit = text.Substring(idx, term.Length), post = text.Substring(idx + term.Length, Math.Max(0, from + len - idx - term.Length));
        return (from > 0 ? "…" : "") + pre + "«" + hit + "»" + post + (from + len < text.Length ? "…" : "");
    }

    /// <summary>
    /// Durchsucht mehrere Aufnahmen nacheinander und meldet Treffer sofort. Nicht lesbare Aufnahmen werden übersprungen.
    /// Gibt die Zahl der durchsuchten Aufnahmen zurück; bricht nach <paramref name="budget"/> ab (null = ohne Grenze).
    /// </summary>
    public static int SearchAll(IEnumerable<SessionInfo> sessions, SearchQuery q, Action<SearchHit> onHit, string lang = "de", TimeSpan? budget = null, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int searched = 0;
        foreach (var s in sessions)
        {
            ct.ThrowIfCancellationRequested();
            if (budget is { } b && sw.Elapsed > b) break;
            if (s.State is "unreadable" or "recording") continue;
            List<SearchEntry> entries;
            try { entries = LoadOrBuild(s.Path, lang); } catch { continue; }
            searched++;
            foreach (var (e, snip) in Search(entries, q)) onHit(new SearchHit(s.Path, s.Id, s.Title, e, snip));
        }
        return searched;
    }
}
