using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

/// <summary>Wortlaut der Nahtstellen (06 §7.7) und der Abschnitts-Kopfzeilen – in der Sprache der Ausgabe.</summary>
public static class SeamText
{
    static DateTime Local(string? utc) => Json.ParseUtc(utc).ToLocalTime();

    /// <summary>Hinweiszeile „&gt; Nahtstelle – …“ (ohne Zeilenende).</summary>
    public static string Line(SessionView view, Seam seam, string lang)
    {
        bool de = lang == "de";
        var at = Local(seam.RecordedUtc);
        switch (seam.Kind)
        {
            case "replacementStart":
            {
                var block = seam.ReplacementId != null ? view.BlockOf(seam.ReplacementId) : new List<ViewStep>();
                string nums = block.Count == 0 ? "" : block.Count == 1
                    ? (de ? $"Schritt {block[0].Number} wurde" : $"Step {block[0].Number} was")
                    : (de ? $"Die Schritte {block[0].Number} bis {block[^1].Number} wurden" : $"Steps {block[0].Number} to {block[^1].Number} were");
                string head = block.Count == 0
                    ? (de ? $"Am {at:yyyy-MM-dd} ab {at:HH:mm:ss} wurde neu aufgenommen (Aufnahmeabschnitt {seam.Section + 1}), ohne Schritte." : $"Re-recorded on {at:yyyy-MM-dd} from {at:HH:mm:ss} (recording part {seam.Section + 1}), without steps.")
                    : (de ? $"{nums} am {at:yyyy-MM-dd} ab {at:HH:mm:ss} neu aufgenommen (Aufnahmeabschnitt {seam.Section + 1})." : $"{nums} re-recorded on {at:yyyy-MM-dd} from {at:HH:mm:ss} (recording part {seam.Section + 1}).");
                var r = seam.Replaced;
                string ids = r.Count == 0 ? "" : r.Count > 5 ? (de ? $"Kennungen {r[0].Id} bis {r[^1].Id}" : $"ids {r[0].Id} to {r[^1].Id}") : (de ? "Kennungen " : "ids ") + string.Join(", ", r.Select(x => x.Id));
                string tail = r.Count == 0 ? ""
                    : de ? $" Sie ersetzen {(r.Count == 1 ? "1 früheren Schritt" : $"{r.Count} frühere Schritte")} ({ids}; {Fmt.Time(r.Min(x => x.TFrom), lang)} – {Fmt.Time(r.Max(x => x.TTo), lang)})."
                         : $" They replace {(r.Count == 1 ? "1 earlier step" : $"{r.Count} earlier steps")} ({ids}; {Fmt.Time(r.Min(x => x.TFrom), lang)} – {Fmt.Time(r.Max(x => x.TTo), lang)}).";
                return (de ? "> Nahtstelle – Beginn einer Neuaufnahme: " : "> Seam – start of a re-recording: ") + head + tail;
            }
            case "replacementEnd":
                return de ? $"> Nahtstelle – Ende der Neuaufnahme: Ab hier wieder Aufnahmeabschnitt {seam.Section + 1}, aufgenommen am {at:yyyy-MM-dd}."
                          : $"> Seam – end of the re-recording: From here on recording part {seam.Section + 1} again, recorded on {at:yyyy-MM-dd}.";
            default:
            {
                var end = seam.PrevEndUtc != null ? Local(seam.PrevEndUtc) : (DateTime?)null;
                return de ? $"> Nahtstelle – Fortsetzung: Aufnahmeabschnitt {seam.Section + 1}, aufgenommen am {at:yyyy-MM-dd} ab {at:HH:mm:ss}." + (end is { } e1 ? $" Abschnitt {seam.PrevSection + 1} endete am {e1:yyyy-MM-dd} um {e1:HH:mm:ss}." : "")
                          : $"> Seam – continuation: recording part {seam.Section + 1}, recorded on {at:yyyy-MM-dd} from {at:HH:mm:ss}." + (end is { } e2 ? $" Part {seam.PrevSection + 1} ended on {e2:yyyy-MM-dd} at {e2:HH:mm:ss}." : "");
            }
        }
    }

    /// <summary>Kopfzeilen zu Abschnitten und wirksamer Folge; leer bei einer Aufnahme aus einem Abschnitt ohne Ersetzung.</summary>
    public static List<string> HeaderLines(SessionView view, string lang)
    {
        bool de = lang == "de";
        var l = new List<string>();
        if (view.Sections.Count <= 1 && view.Replacements.Count == 0) return l;
        var parts = view.Sections.Where(p => !view.DiscardedSections.Contains(p.Part)).Select(p =>
        {
            var at = Local(p.StartUtc);
            string what = p.Purpose switch
            {
                "replacement" => de ? "Neuaufnahme" : "re-recording",
                "append" => de ? "Fortsetzung" : "continuation",
                _ => de ? "Erstaufnahme" : "initial recording"
            };
            return $"{p.Part + 1} ({what}, {at:yyyy-MM-dd HH:mm:ss})";
        });
        l.Add((de ? "- Aufnahmeabschnitte: " : "- Recording parts: ") + string.Join(" · ", parts));
        var (replaced, unused, seams, discarded, _) = view.SectionStats();
        l.Add(de ? $"- Wirksame Schrittfolge: {view.All.Count} Schritte · {replaced} ersetzt, {unused} nicht verwendet · {seams} Nahtstellen" + (discarded > 0 ? $" · {discarded} verworfene Abschnitte" : "")
                 : $"- Effective step sequence: {view.All.Count} steps · {replaced} replaced, {unused} unused · {seams} seams" + (discarded > 0 ? $" · {discarded} discarded parts" : ""));
        return l;
    }
}
