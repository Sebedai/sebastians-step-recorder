using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.ReadLayer;

/// <summary>Vorschlag des Aufräumens: Schritt ausblenden, mit Regel und verständlichem Grund.</summary>
public sealed record CleanupSuggestion(ViewStep Step, string Rule, string Reason);

/// <summary>
/// Aufräumen nach der Aufnahme: findet Schritte, die in einer Anleitung nur stören – ohne etwas zu löschen
/// (Vorschläge werden als „ausgeblendet“ mit Grund gespeichert und lassen sich jederzeit wieder einblenden).
/// </summary>
public static class StepCleanup
{
    const long FocusClickGapUs = 3_000_000, RepeatClickGapUs = 3_000_000, RoundTripUs = 10_000_000;

    public static (string rule, string name)[] Rules => new[]
    {
        ("focusClick", Loc.L("Klick ins Feld direkt vor der Eingabe", "Click into the field right before typing")),
        ("repeatClick", Loc.L("Wiederholter Klick auf dasselbe Element", "Repeated click on the same element")),
        ("windowRoundTrip", Loc.L("Fensterwechsel hin und gleich wieder zurück", "Window switch there and straight back")),
        ("emptyInput", Loc.L("Eingabe ohne Ergebnis", "Input without result")),
    };

    /// <summary>Verständlicher Grund je Regel in der Oberflächensprache (gespeicherte Gründe älterer Aufnahmen werden darüber neu angezeigt).</summary>
    public static string? ReasonFor(string? rule) => rule switch
    {
        "emptyInput" => Loc.L("Eingabe ohne Ergebnis – es wurde nur getippt und wieder gelöscht.", "Input without result – text was only typed and deleted again."),
        "focusClick" => Loc.L("Klick ins Feld vor der Eingabe – der Eingabe-Schritt nennt das Feld bereits.", "Click into the field before typing – the typing step already names the field."),
        "repeatClick" => Loc.L("Wiederholter Klick auf dasselbe Element.", "Repeated click on the same element."),
        "windowRoundTrip" => Loc.L("Fensterwechsel hin und gleich wieder zurück, ohne Aktion dazwischen.", "Window switch there and straight back, with no action in between."),
        _ => null
    };

    public static List<CleanupSuggestion> Suggest(SessionView view) => Suggest(view.All);

    public static List<CleanupSuggestion> Suggest(IEnumerable<ViewStep> all)
    {
        var seq = all.Where(s => !s.Hidden && !s.Discarded && !s.Manual && !s.Redacted && s.Importance != "trace"
                                  && s.Kind is not ("gap" or "marker" or "comment" or "snapshot" or "system" or "wait" or "move"))
                     .OrderBy(s => s.TFrom).ToList();
        var result = new List<CleanupSuggestion>();
        var used = new HashSet<string>();
        void Add(ViewStep s, string rule, string reason) { if (used.Add(s.Id)) result.Add(new(s, rule, reason)); }

        for (int i = 0; i < seq.Count; i++)
        {
            var a = seq[i];
            var b = i + 1 < seq.Count ? seq[i + 1] : null;
            var input = a.Json.Obj("input");

            // leere Eingabe: nur Korrekturen, kein Text, keine abschließende Taste
            if (a.Kind == "type" && input?.Bool("masked") != true && string.IsNullOrEmpty(input?.Str("text")) && input?.Obj("terminator") == null
                && input?.Str("inputMethod") != "ime" && string.IsNullOrEmpty(input?.Str("valueAfter")))
                Add(a, "emptyInput", ReasonFor("emptyInput")!);

            if (b == null) continue;
            long gap = b.TFrom - a.TTo;

            // Klick ins Feld unmittelbar vor der Eingabe in dasselbe Feld
            if (a.Kind == "click" && IsPlainClick(a) && b.Kind == "type" && SameElement(a, b) && gap < FocusClickGapUs)
                Add(a, "focusClick", ReasonFor("focusClick")!);

            // wiederholter Klick auf dasselbe Element ohne sichtbare Wirkung dazwischen
            if (a.Kind == "click" && b.Kind == "click" && IsPlainClick(a) && IsPlainClick(b) && SameElement(a, b) && gap < RepeatClickGapUs && !OpenedSomething(a))
                Add(b, "repeatClick", ReasonFor("repeatClick")!);

            // Fensterwechsel hin und sofort zurück
            if (a.Kind == "windowSwitch" && b.Kind == "windowSwitch" && gap < RoundTripUs)
            {
                string? from = input?.Str("from"), to2 = b.Json.Obj("input")?.Str("to");
                if (from != null && from == to2)
                {
                    Add(a, "windowRoundTrip", ReasonFor("windowRoundTrip")!);
                    Add(b, "windowRoundTrip", ReasonFor("windowRoundTrip")!);
                }
            }
        }
        return result;
    }

    static bool IsPlainClick(ViewStep s)
    {
        var i = s.Json.Obj("input");
        return (i?.Str("button") ?? "primary") == "primary" && i?.Int("count") is 0 or 1 && (i?.Arr("mods")?.Count ?? 0) == 0;
    }

    static bool OpenedSomething(ViewStep s) => (s.Json.Obj("effect")?.Arr("windowsOpened")?.Count ?? 0) > 0;

    static bool SameElement(ViewStep a, ViewStep b)
    {
        var ea = a.Json.Obj("target")?.Obj("el"); var eb = b.Json.Obj("target")?.Obj("el");
        if (ea == null || eb == null) return false;
        if (a.Json.Obj("ctx")?.Str("win") != b.Json.Obj("ctx")?.Str("win")) return false;
        string? na = ea.Str("name"), nb = eb.Str("name"), ca = ea.Str("cls") ?? ea.Str("autoId"), cb = eb.Str("cls") ?? eb.Str("autoId");
        if (string.IsNullOrEmpty(na) && string.IsNullOrEmpty(ca)) return false;
        return na == nb && ca == cb;
    }

    /// <summary>Vorschläge als Bearbeitung übernehmen: ausblenden und Grund vermerken.</summary>
    public static void Apply(JsonObject edits, IEnumerable<CleanupSuggestion> suggestions)
    {
        var arr = edits["steps"] as JsonArray ?? new JsonArray();
        edits["steps"] = arr;
        foreach (var s in suggestions)
        {
            var se = arr.OfType<JsonObject>().FirstOrDefault(x => x.Long("anchor") == s.Step.Anchor);
            if (se == null) { se = new JsonObject { ["anchor"] = s.Step.Anchor }; arr.Add(se); }
            se["hidden"] = true;
            se["cleanup"] = new JsonObject { ["rule"] = s.Rule, ["reason"] = s.Reason };
        }
    }
}
