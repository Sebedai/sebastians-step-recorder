using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

public static partial class GuideExport
{
    /// <summary>
    /// Gedruckte Schrittnummern der Anleitung (Anker → „2.3“) mit den Dokumenteinstellungen der Aufnahme – dieselbe Zählung wie
    /// <see cref="Build"/>. Der Editor zeigt damit die Schrittnummer im Bild genau wie in der Anleitung (13, Paket 22).
    /// </summary>
    public static Dictionary<long, string> StepNumbers(SessionView view, string? importance = null, string? numbering = null)
    {
        var guide = view.Edits.Obj("guide");
        var s = DocSettings.From(guide, view.Language);
        string mode = numbering ?? s.Numbering;
        var map = new Dictionary<long, string>();
        int chap = 0, inChap = 0, flat = 0, pre = 0;
        foreach (var st in view.GuideSteps(importance ?? "primary"))
        {
            if (st.Kind == "move") continue;
            if (st.ChapterTitle is { Length: > 0 }) { chap++; inChap = 0; }
            flat++;
            map[st.Anchor] = NumberFor(mode, chap, ref inChap, flat, ref pre);
        }
        return map;
    }

    /// <summary>Nummer eines Schritts nach Zählweise: keine, fortlaufend oder je Kapitel („2.3“).</summary>
    static string NumberFor(string mode, int chap, ref int inChap, int flat, ref int pre) => mode switch
    {
        "none" => "",
        "flat" => flat.ToString(),
        _ => chap == 0 ? (++pre).ToString() : $"{chap}.{++inChap}"
    };

    /// <summary>Gedruckte Nummer eines Schritts oder null (nicht in der Anleitung, ohne Nummerierung).</summary>
    public static string? StepNumber(SessionView view, ViewStep step)
        => StepNumbers(view).TryGetValue(step.Anchor, out var n) && n.Length > 0 ? n : null;
}
