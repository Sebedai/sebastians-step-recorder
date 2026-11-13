using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Steps;

namespace StepRecorder.ReadLayer;

/// <summary>Ein Befund der Qualitätsprüfung (09 §12, Regelkatalog Q01–Q20).</summary>
public sealed record QualityFinding(string Rule, string Severity, long? Anchor, string? StepId, int? GuideNumber, string? Key, string De, string En, string Action, bool Ignored)
{
    public string Text(string lang) => lang == "de" ? De : En;
    /// <summary>Eine Zeile für die Kommandozeile: „Q08 Warnung Schritt 12: …“.</summary>
    public string Line(string lang)
    {
        bool de = lang == "de";
        string sev = Severity == "warning" ? (de ? "Warnung" : "Warning") : (de ? "Hinweis" : "Hint");
        string where = GuideNumber is int n ? (de ? $" Schritt {n}" : $" step {n}") : "";
        return $"{Rule} {sev}{where}: {Text(lang)}{(Ignored ? (de ? " (ignoriert)" : " (ignored)") : "")}";
    }
}

public sealed class QualityOptions
{
    public string Importance { get; set; } = "primary";
    /// <summary>Darstellung (screen | window | crop); null = aus den Dokument-Einstellungen.</summary>
    public string? View { get; set; }
    public bool IncludeIgnored { get; set; }
    public int MinW { get; set; } = 400;
    public int MinH { get; set; } = 225;
    public int MaxW { get; set; } = 2560;
    public double MinTargetShare { get; set; } = 0.0005;
    public long LongStepUs { get; set; } = 30_000_000;
    public long LongIdleUs { get; set; } = 60_000_000;
    /// <summary>Persönliche Werte (Art, Wert); null = Benutzer-, Rechner-, Domänenname und Profilpfad dieses Rechners.</summary>
    public IReadOnlyList<(string kind, string value)>? PersonalValues { get; set; }
}

/// <summary>
/// Qualitätsprüfung vor dem Export: Stellen, die eine Anleitung schlechter machen. Liest nur Daten der Leseschicht,
/// dekodiert keine Bilder. Ignorierte Befunde stehen in <c>edits.qualityIgnores[]</c> (04 §edits). Schlüssel enthalten
/// nie aufgezeichnete Inhalte.
/// </summary>
public static class QualityCheck
{
    static readonly Regex StepRef = new(@"(?i)\b(schritt|step)\s*\d+", RegexOptions.CultureInvariant);

    public static List<QualityFinding> Run(SessionView view, QualityOptions? o = null)
    {
        o ??= new QualityOptions();
        string lang = view.Language;
        var ignores = (view.Edits.Arr("qualityIgnores") ?? new JsonArray()).OfType<JsonObject>()
            .Select(x => (x.Str("rule") ?? "", x.LongN("anchor"), x.Str("key"))).ToHashSet();
        var list = new List<QualityFinding>();
        var steps = view.GuideSteps(o.Importance).Where(s => s.Kind != "move").ToList();
        var numbers = new Dictionary<long, int>();
        for (int i = 0; i < steps.Count; i++) numbers[steps[i].Anchor] = i + 1;
        string viewMode = o.View ?? DocSettings.From(view.Edits.Obj("guide")).View;

        void Add(string rule, string sev, ViewStep? s, string? key, string de, string en, string action)
        {
            bool ign = ignores.Contains((rule, s?.Anchor, key)) || ignores.Contains((rule, s?.Anchor, null));
            if (ign && !o.IncludeIgnored) return;
            list.Add(new QualityFinding(rule, sev, s?.Anchor, s?.Id, s != null && numbers.TryGetValue(s.Anchor, out var n) ? n : null, key, de, en, action, ign));
        }

        // Q12 über alle Schritte (nicht nur den Umfang): ungeprüfte Eingaben blockieren jede Weitergabe
        foreach (var s in view.AllWithInactive.Where(s => s.Unreviewed))
            Add("Q12", "warning", s, "typed", "Eine auffällige Eingabe ist ungeprüft.", "A flagged input has not been reviewed.", "review");

        ViewStep? prev = null, prevRec = null;
        long firstT = steps.Count > 0 ? steps.Min(s => s.TFrom) : 0, lastT = steps.Count > 0 ? steps.Max(s => s.TTo) : 0;
        bool reordered = (view.Edits.Arr("order")?.Count ?? 0) > 0 || view.All.Any(s => s.Hidden) || view.Manual.Count > 0 || (view.Edits.Arr("merges")?.Count ?? 0) > 0 || (view.Edits.Obj("guide")?.Arr("append")?.Count ?? 0) > 0;
        var timeline = view.Steps.Arr("timeline") ?? new JsonArray();
        var personal = o.PersonalValues ?? DefaultPersonalValues();

        foreach (var s in steps)
        {
            bool action = s.Kind is "click" or "doubleClick" or "tripleClick" or "rightClick" or "drag" or "scroll" or "hover" or "type" or "hotkey" or "key";
            bool authored = s.AuthorTitle != null || s.AuthorBody != null;
            // Q01
            if (s.Importance == "primary" && !s.Manual && !authored && s.Kind is "click" or "doubleClick" or "tripleClick" or "rightClick" or "drag" or "type" or "hover"
                && TextTemplates.TargetRule(s.Json) >= 6)
                Add("Q01", "hint", s, null, "Das Ziel dieses Schritts hat keinen Namen.", "The target of this step has no name.", "editText");
            // Q02 / Q03
            if (action && !s.Manual && !s.Redacted)
            {
                if (s.EvidenceLevel == "inputOnly") Add("Q02", "hint", s, null, "Dieser Schritt ist nur durch die Eingabe belegt.", "This step is only evidenced by the input.", "checkImage");
                else if (s.EvidenceLevel == "unknown" && s.Importance == "primary" && !(s.Json.Obj("input")?.Bool("masked") ?? false))
                    Add("Q03", "hint", s, null, "Dieser Schritt ist nicht belegt.", "This step has no evidence.", "hide");
            }
            // Q04
            if (s.Kind == "gap" && s.TFrom > firstT && s.TTo < lastT)
                Add("Q04", "hint", s, s.Json.Obj("input")?.Str("cause") ?? "gap", "Hier fehlt ein Stück der Aufzeichnung.", "A part of the recording is missing here.", "insertNote");
            // Q05
            if (action && s.TTo - s.TFrom > o.LongStepUs)
                Add("Q05", "hint", s, null, "Dieser Schritt dauert sehr lange.", "This step takes very long.", "insertNote");
            // Q18: Nahtstelle vor diesem Schritt (Neuaufnahme oder Fortsetzung, 05 §16.4) – über sie hinweg keine Abstände (Q06)
            var seams = s.Manual ? new List<Seam>() : view.SeamsBetween(prevRec, s);
            if (seams.Count > 0)
                Add("Q18", "hint", s, seams[0].Kind, "Vor diesem Schritt passt der Zustand nicht nahtlos.", "The state before this step may not continue seamlessly.", "insertNote");
            if (!s.Manual) prevRec = s;
            // Q06
            if (prev != null && seams.Count == 0 && prev.Kind is not ("wait" or "comment"))
            {
                long idle = timeline.OfType<JsonObject>().Where(x => x.Str("kind") == "idle" && x.Long("tTo") <= s.TFrom + 1000 && x.Long("tTo") >= prev.TTo).Select(x => x.Long("tTo") - x.Long("tFrom")).DefaultIfEmpty(0).Max();
                if (idle == 0 && s.TFrom - prev.TTo > o.LongIdleUs) idle = s.TFrom - prev.TTo;
                if (idle > o.LongIdleUs) Add("Q06", "hint", s, null, "Vor diesem Schritt vergeht viel Zeit.", "A lot of time passes before this step.", "insertNote");
            }
            // Bildbezogene Regeln
            if (!s.Redacted && s.ShowImage != false && s.Kind != "gap")
            {
                string variant = StepImages.GuideVariant(s, viewMode);
                string key = variant == "after" ? "post" : "pre";
                var geo = StepImages.Geometry(view, s, variant, viewMode == "window" ? "window" : "screen");
                if (geo == null)
                {
                    bool expected = s.Manual ? s.PreFrame != null : (variant == "after" ? s.PostFrame : s.PreFrame) != null || action;
                    if (expected && !s.Manual)
                        Add("Q16", "warning", s, key == "post" ? "postMissing" : "preMissing", "Zu diesem Schritt fehlt ein Bild.", "This step has no image.", "replaceImage");
                }
                else
                {
                    var src = geo.Source;
                    if (src.Width < o.MinW || src.Height < o.MinH)
                        Add("Q07", "hint", s, "tooSmall", "Das Bild ist sehr klein.", "The image is very small.", "adjustCrop");
                    else if (src.Width > o.MaxW || (geo.Target is { } tg && (double)tg.Width * tg.Height / Math.Max(1.0, (double)src.Width * src.Height) < o.MinTargetShare))
                        Add("Q07", "hint", s, "tooLarge", "Das Ziel ist im Bild schwer zu finden.", "The target is hard to find in the image.", "adjustCrop");
                    if (geo.Target is { } t && t.Width > 0 && t.Height > 0 && !src.Contains(t) && src.IntersectsWith(t) && variant != "after")
                        Add("Q08", "warning", s, key, "Das Bild zeigt das Ziel nicht vollständig.", "The image does not show the whole target.", "adjustCrop");
                    if (geo.Click is { } c && !src.Contains(c) && variant != "after")
                        Add("Q09", "warning", s, key, "Die Klickmarke liegt außerhalb des Bildes.", "The click marker lies outside the image.", "adjustCrop");
                }
                // Q16/Q17 aus den Bildangaben des Schritts
                foreach (var issue in s.Json.Obj("frames")?.StrList("issues") ?? new())
                {
                    bool shown = issue.StartsWith(key, StringComparison.OrdinalIgnoreCase);
                    if (issue is "preMissing" or "postMissing" && shown && geo != null) Add("Q16", "warning", s, issue, "Zu diesem Schritt fehlt ein Bild.", "This step has no image.", "replaceImage");
                    if (issue is "preNotGuaranteed" or "postTimeout" or "masked" && (shown || issue == "masked"))
                        Add("Q17", "hint", s, issue, "Das Bild dieses Schritts ist nicht verlässlich.", "The image of this step is not reliable.", "checkImage");
                }
            }
            // Q10 / Q11
            string title = (s.AuthorTitle ?? s.Title).Trim(), body = (s.AuthorBody ?? s.Guide).Trim();
            if (!s.Redacted && (title.Length == 0 || (body.Length == 0 && s.AuthorTitle == null && s.Kind is not ("gap" or "marker" or "snapshot"))))
                Add("Q10", "warning", s, lang, "Dieser Schritt hat keinen Text.", "This step has no text.", "editText");
            if (prev != null && body.Length > 0 && body == (prev.AuthorBody ?? prev.Guide).Trim() && s.Kind != "gap")
                Add("Q11", "hint", s, lang, "Dieser Schritt hat denselben Text wie der vorige.", "This step has the same text as the previous one.", "merge");
            // Q13
            if (s.TranslationMissing)
                Add("Q13", "warning", s, lang, "Ein eigener Text fehlt in der Exportsprache.", "An author text is missing in the export language.", "translate");
            // Q14
            string texts = title + "\n" + body + "\n" + string.Join("\n", s.Comments.Select(c => c.Str("text")));
            foreach (var (kind, value) in personal)
                if (value.Length >= 3 && Regex.IsMatch(texts, @"(?<![\p{L}\p{N}])" + Regex.Escape(value) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase))
                { Add("Q14", "hint", s, kind, "Ein persönlicher Wert ist nicht verallgemeinert.", "A personal value has not been generalised.", "generalize"); break; }
            // Q19
            if (s.Importance == "primary" && s.Kind is "click" or "doubleClick" or "rightClick" or "hotkey" && s.Json.Obj("effect") is { } eff && eff["observed"] is JsonValue ov && ov.TryGetValue<bool>(out var observed) && !observed)
                Add("Q19", "hint", s, null, "Für diesen Schritt wurde keine Wirkung beobachtet.", "No effect was observed for this step.", "hide");
            // Q21: die Anwendung reagierte langsam (Hinweis; 12, Paket 9)
            if (!s.Redacted && s.Importance == "primary" && Timing.WaitMsOf(s.Json) is long waitMs && waitMs >= Timing.SlowMs)
                Add("Q21", "hint", s, null, $"Die Anwendung brauchte nach diesem Schritt {Timing.Ms(waitMs, true)}, bis sie bereit war.", $"The application needed {Timing.Ms(waitMs, false)} after this step to be ready.", "none");
            // Q20
            if (reordered && s.Comments.Any(c => StepRef.IsMatch(c.Str("text") ?? "")))
                Add("Q20", "hint", s, null, "Ein Kommentar nennt eine Schrittnummer, die sich verschoben haben kann.", "A comment mentions a step number that may have shifted.", "editText");
            prev = s;
        }

        // Q13 für Dokumenttexte
        var guide = view.Edits.Obj("guide");
        if (guide != null)
        {
            string other = lang == "de" ? "en" : "de";
            foreach (var field in new[] { "title", "intro", "outro" })
            {
                bool hasOwn = lang == "de" ? guide.Str(field) is { Length: > 0 } : guide.Obj("i18n")?.Obj(lang)?.Str(field) is { Length: > 0 };
                bool hasOther = other == "de" ? guide.Str(field) is { Length: > 0 } : guide.Obj("i18n")?.Obj(other)?.Str(field) is { Length: > 0 };
                if (!hasOwn && hasOther && field != "title")
                    Add("Q13", "warning", null, $"{lang}:{field}", "Ein Dokumenttext fehlt in der Exportsprache.", "A document text is missing in the export language.", "translate");
            }
        }
        // Q15 (teilweise): Verallgemeinerungen ohne Bildflächen – der Wert kann im Bild noch zu sehen sein
        foreach (var sb in (view.Edits.Arr("substitutions") ?? new JsonArray()).OfType<JsonObject>())
            if ((sb.Obj("images")?.Arr("areas")?.Count ?? 0) == 0)
                Add("Q15", "warning", null, sb.Str("id"), "Ein verallgemeinerter Wert kann im Bild noch zu sehen sein.", "A generalised value may still be visible in an image.", "scanImages");
        // Q18 (teilweise): angehängte Aufnahmen sind Nahtstellen
        foreach (var a in (guide?.Arr("append") ?? new JsonArray()).OfType<JsonObject>())
            Add("Q18", "hint", null, "continuation:" + (a.Str("sessionId") ?? a.Str("rel") ?? "?")[..Math.Min(8, (a.Str("sessionId") ?? a.Str("rel") ?? "?").Length)], "Zwischen den Aufnahmen passt der Zustand nicht nahtlos.", "The state may not continue seamlessly between recordings.", "insertNote");
        return list.OrderBy(f => f.GuideNumber ?? int.MaxValue).ThenBy(f => f.Rule, StringComparer.Ordinal).ToList();
    }

    static List<(string kind, string value)> DefaultPersonalValues()
    {
        var l = new List<(string, string)>();
        try { if (Environment.UserName is { Length: >= 3 } u) l.Add(("userName", u)); } catch { }
        try { if (Environment.MachineName is { Length: >= 3 } m) l.Add(("machineName", m)); } catch { }
        try { if (Environment.UserDomainName is { Length: >= 3 } d && !string.Equals(d, Environment.MachineName, StringComparison.OrdinalIgnoreCase)) l.Add(("domain", d)); } catch { }
        try { if (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: >= 3 } p) l.Add(("profilePath", p)); } catch { }
        return l;
    }

    /// <summary>Befund ignorieren (oder wieder berücksichtigen) – gespeichert in <c>edits.qualityIgnores</c>.</summary>
    public static void SetIgnored(JsonObject edits, QualityFinding f, bool ignored)
    {
        var arr = edits["qualityIgnores"] as JsonArray ?? new JsonArray();
        foreach (var x in arr.OfType<JsonObject>().Where(x => x.Str("rule") == f.Rule && x.LongN("anchor") == f.Anchor && x.Str("key") == f.Key).ToList()) arr.Remove(x);
        if (ignored) arr.Add(new JsonObject { ["rule"] = f.Rule, ["anchor"] = f.Anchor, ["key"] = f.Key, ["atUtc"] = Json.UtcNow(), ["by"] = "user" }.Clean());
        edits["qualityIgnores"] = arr;
    }

    public static JsonArray ToJson(IEnumerable<QualityFinding> list, string lang)
        => new(list.Select(f => (JsonNode)new JsonObject
        {
            ["rule"] = f.Rule, ["severity"] = f.Severity, ["step"] = f.GuideNumber, ["stepId"] = f.StepId, ["key"] = f.Key,
            ["message"] = f.Text(lang), ["action"] = f.Action, ["ignored"] = f.Ignored ? true : null
        }.Clean()).ToArray());
}
