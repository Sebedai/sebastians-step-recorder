using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Text;

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

    /// <summary>
    /// Querverweise der Anleitung beschriften (13, Paket 23): <c>s…</c> → „Schritt 2.3“, <c>k…</c> → „Kapitel 2“; ohne Nummerierung
    /// der Titel in Anführungszeichen. Ziele, die nicht im Dokument stehen (ausgeblendet, nicht im Umfang), ergeben „??“ – so kann
    /// kein Titel eines nicht gezeigten Schritts durchsickern. Texte angehängter Aufnahmen verweisen nie in die Hauptaufnahme.
    /// </summary>
    static void ResolveRefs(Doc d, string lang)
    {
        bool de = lang != "en";
        var steps = new Dictionary<long, (string label, string anchor)>();
        var chaps = new Dictionary<long, (string label, string anchor)>();
        Chapter? pending = null;
        string Quote(string t) => de ? "„" + t + "“" : "“" + t + "”";
        foreach (var b in d.Blocks)
        {
            if (b is Chapter c) { pending = c; continue; }
            if (b is not Item it || d.Foreign.Contains(it)) { pending = null; continue; }
            steps[it.Step.Anchor] = (it.Number.Length > 0 ? (de ? "Schritt " : "step ") + it.Number : Quote(it.Title), "#" + it.Anchor);
            foreach (var m in it.Step.Members ?? new List<ViewStep>()) steps.TryAdd(m.Anchor, steps[it.Step.Anchor]);
            if (pending != null)
            {
                chaps[it.Step.Anchor] = (pending.Number.Length > 0 ? (de ? "Kapitel " : "chapter ") + pending.Number : Quote(pending.Title), "#" + pending.Anchor);
                pending = null;
            }
        }
        (string, string?)? Own(string t)
        {
            if (!RichDoc.IsRefTarget(t)) return null;
            long a = long.Parse(t.AsSpan(1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture);
            var map = t[0] == 's' ? steps : chaps;
            return map.TryGetValue(a, out var v) ? (v.label, v.anchor) : null;
        }
        (string, string?)? None(string t) => null;
        RichText? R(RichText? r, bool foreign) => r?.ResolveRefs(foreign ? None : Own);
        for (int i = 0; i < d.Blocks.Count; i++)
        {
            switch (d.Blocks[i])
            {
                case Item it when it.Body != null || it.NoteRich != null:
                    {
                        bool f = d.Foreign.Contains(it);
                        var n = it with { Body = R(it.Body, f), NoteRich = it.NoteRich?.Select(x => R(x, f)).ToList() };
                        d.Blocks[i] = n;
                        if (f) { d.Foreign.Remove(it); d.Foreign.Add(n); }
                        break;
                    }
                case Chapter c when c.Body != null:
                    d.Blocks[i] = c with { Body = R(c.Body, d.Foreign.Contains(c)) };
                    break;
            }
        }
        d.IntroRich = R(d.IntroRich, false);
        d.OutroRich = R(d.OutroRich, false);
    }

    /// <summary>Gedruckte Nummer eines Schritts oder null (nicht in der Anleitung, ohne Nummerierung).</summary>
    public static string? StepNumber(SessionView view, ViewStep step)
        => StepNumbers(view).TryGetValue(step.Anchor, out var n) && n.Length > 0 ? n : null;
}
