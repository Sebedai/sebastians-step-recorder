using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>Ein Kapitelvorschlag: Kapitel beginnt beim Schritt <see cref="Anchor"/> mit dem Titel; Anzahl der Schritte, Grund.</summary>
public sealed record ChapterSuggestion(long Anchor, string Title, int Steps, string Process);

/// <summary>
/// Kapitelvorschläge (13, Paket 37): Wechselt die Aufnahme das Programm, beginnt dort ein Kapitel. Läufe unter zwei Schritten
/// gehören zum Nachbarn; an Stellen, an denen schon ein Kapitel beginnt (oder in den zwei Schritten danach), wird nichts
/// vorgeschlagen. Titel aus dem gemeinsamen Teil der Fenstertitel nach „ – “, sonst dem Programmnamen; bereinigt.
/// </summary>
public static class ChapterSuggest
{
    static string? Proc(ViewStep s) => s.Json.Obj("ctx")?.Str("process") is { Length: > 0 } p ? p.ToLowerInvariant() : null;

    public static List<ChapterSuggestion> Suggest(SessionView view)
    {
        var steps = view.GuideSteps().Where(s => s.Kind != "move" && !s.TextSection).ToList();
        // Lauf je Programm; Schritte ohne Programm erben das vorige
        var runs = new List<(int first, int count, string proc)>();
        string? cur = null;
        for (int i = 0; i < steps.Count; i++)
        {
            string p = Proc(steps[i]) ?? cur ?? "";
            if (p is "explorer.exe" && cur != null) p = cur;   // Shell-Fenster zählen nicht als Programmwechsel
            if (runs.Count == 0 || runs[^1].proc != p) runs.Add((i, 1, p));
            else runs[^1] = (runs[^1].first, runs[^1].count + 1, p);
            cur = p;
        }
        // kurze Läufe mit dem vorigen verschmelzen
        for (int i = runs.Count - 1; i > 0; i--)
            if (runs[i].count < 2) { runs[i - 1] = (runs[i - 1].first, runs[i - 1].count + runs[i].count, runs[i - 1].proc); runs.RemoveAt(i); }
        for (int i = runs.Count - 1; i > 0; i--)
            if (runs[i].proc == runs[i - 1].proc) { runs[i - 1] = (runs[i - 1].first, runs[i - 1].count + runs[i].count, runs[i - 1].proc); runs.RemoveAt(i); }
        var existing = steps.Select((s, i) => (s, i)).Where(x => x.s.ChapterTitle != null).Select(x => x.i).ToList();
        var res = new List<ChapterSuggestion>();
        if (runs.Count < 2 && existing.Count > 0) return res;
        var seen = new Dictionary<string, int>();
        foreach (var r in runs)
        {
            if (existing.Any(e => r.first >= e && r.first - e <= 2)) continue;
            string title = TitleFor(view, steps.Skip(r.first).Take(r.count).ToList(), r.proc);
            seen[title] = seen.GetValueOrDefault(title) + 1;
            if (seen[title] > 1) title += Settings.OutText.T(view.Language, " (Fortsetzung)", " (continued)");
            res.Add(new ChapterSuggestion(steps[r.first].Anchor, title, r.count, r.proc));
        }
        return res;
    }

    static string TitleFor(SessionView view, List<ViewStep> run, string proc)
    {
        var titles = run.Select(s => s.Json.Obj("ctx")?.Str("title")).Where(t => t is { Length: > 0 }).Select(t => t!).ToList();
        // App-Name: Teil nach dem letzten „ – “ oder „ - “, wenn er in den meisten Titeln gleich ist
        var tails = titles.Select(t => t.Split(new[] { " – ", " - ", " — " }, StringSplitOptions.None).Last().Trim()).Where(t => t.Length > 1).ToList();
        string? common = tails.GroupBy(t => t).OrderByDescending(g => g.Count()).FirstOrDefault(g => g.Count() * 2 >= tails.Count)?.Key;
        string name = common ?? (proc.Length > 0 ? System.IO.Path.GetFileNameWithoutExtension(proc) : Settings.OutText.T(view.Language, "Ablauf", "Procedure"));
        if (common == null && name.Length > 0) name = char.ToUpperInvariant(name[0]) + name[1..];
        return view.Clean(name);
    }

    /// <summary>Vorschläge übernehmen (Titel je Sprache der Ansicht).</summary>
    public static void Apply(System.Text.Json.Nodes.JsonObject edits, IEnumerable<ChapterSuggestion> chosen, string lang)
    {
        var arr = edits["steps"] as System.Text.Json.Nodes.JsonArray ?? new System.Text.Json.Nodes.JsonArray(); edits["steps"] = arr;
        foreach (var c in chosen)
        {
            var se = arr.OfType<System.Text.Json.Nodes.JsonObject>().FirstOrDefault(x => x.Long("anchor") == c.Anchor);
            if (se == null) { se = new System.Text.Json.Nodes.JsonObject { ["anchor"] = c.Anchor }; arr.Add(se); }
            if (se["chapter"] != null) continue;
            se["chapter"] = lang == "de" ? new System.Text.Json.Nodes.JsonObject { ["title"] = c.Title, ["text"] = "" }
                : new System.Text.Json.Nodes.JsonObject { ["title"] = c.Title, [lang] = new System.Text.Json.Nodes.JsonObject { ["title"] = c.Title } };
        }
    }
}
