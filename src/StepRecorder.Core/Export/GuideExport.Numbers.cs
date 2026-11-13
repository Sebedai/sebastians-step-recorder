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
        var num = new Numbering(mode);
        foreach (var st in view.GuideSteps(importance ?? "primary"))
        {
            if (st.Kind == "move") continue;
            if (st.ChapterTitle is { Length: > 0 }) num.Chapter(st.ChapterLevel);
            map[st.Anchor] = st.TextSection ? "" : num.Step();
        }
        return map;
    }

    /// <summary>
    /// Zählung der Anleitung (13, Paket 24). Ohne Unterkapitel genau wie bisher: <c>chapter</c> „2.3“ (vor dem ersten Kapitel 1, 2, …),
    /// <c>flat</c> fortlaufend, <c>none</c> keine. Neu: <c>perChapter</c> zählt je Kapitel ab 1; Unterkapitel teilen in <c>chapter</c> den
    /// Zähler der zweiten Ebene mit losen Schritten („1.2“ ist nie zugleich Schritt und Unterkapitel), ihre Schritte heißen „1.2.1“.
    /// </summary>
    sealed class Numbering(string mode)
    {
        int _chap, _l2, _l3, _scope, _flat, _pre;
        bool _inSub;
        public int Level { get; private set; } = 1;
        public string ChapterAnchor => Level == 2 && _chap > 0 ? $"k{_chap}-{_l2}" : $"k{_chap}";

        public string Chapter(int level)
        {
            if (level == 2 && _chap > 0)
            {
                _inSub = true; _l3 = 0; _scope = 0; Level = 2;
                return mode == "none" ? "" : $"{_chap}.{++_l2}";
            }
            _chap++; _l2 = _l3 = _scope = 0; _inSub = false; Level = 1;
            return mode == "none" ? "" : _chap.ToString();
        }

        public string Step()
        {
            _flat++;
            return mode switch
            {
                "none" => "",
                "flat" => _flat.ToString(),
                "perChapter" => (++_scope).ToString(),
                _ => _chap == 0 ? (++_pre).ToString() : _inSub ? $"{_chap}.{_l2}.{++_l3}" : $"{_chap}.{++_l2}"
            };
        }
    }

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
