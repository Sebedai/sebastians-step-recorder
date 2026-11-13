using System.Text;

namespace StepRecorder.Export.Formats;

/// <summary>
/// Barrierearmes PDF (13, Paket 32): Strukturmarkierung (Tagged PDF). Jede gezeichnete Textzeile wird als Überschrift (H1–H3, nach
/// Größe und Schnitt) oder Absatz markiert, Bilder als Figure mit Alternativtext, Flächen, Linien, Kopf- und Fußzeile und
/// Wasserzeichen als Artefakt. Dazu Strukturbaum, ParentTree, MarkInfo und „Dokumenttitel anzeigen“. Bewusst „barrierearm“:
/// Standardschriften werden nicht eingebettet, eine PDF/UA-Konformität wird nicht behauptet.
/// </summary>
public sealed partial class SimplePdf
{
    /// <summary>Strukturmarkierung schreiben (Standard aus – bestehende PDFs bleiben unverändert).</summary>
    public bool Tagged { get; set; }
    /// <summary>Alternativtext für das nächste Bild.</summary>
    public string? NextAlt { get; set; }

    readonly Dictionary<StringBuilder, List<(string role, string? alt)>> _marks = new(ReferenceEqualityComparer.Instance);
    bool _artifact;

    static string RoleFor(double size, bool bold) => !bold ? "P" : size >= 16 ? "H1" : size >= 13.5 ? "H2" : size >= 12 ? "H3" : "P";

    /// <summary>Beginn eines markierten Inhalts; liefert das Ende („EMC“) oder leer.</summary>
    string BeginMark(StringBuilder sb, string role, string? alt = null)
    {
        if (!Tagged) return "";
        if (_artifact || !_pages.Contains(sb)) { sb.Append("/Artifact BMC "); return "EMC\n"; }
        if (!_marks.TryGetValue(sb, out var list)) _marks[sb] = list = new();
        sb.Append(F($"/{role} <</MCID {list.Count}>> BDC "));
        list.Add((role, alt));
        return "EMC\n";
    }

    string BeginArtifact(StringBuilder sb) { if (!Tagged) return ""; sb.Append("/Artifact BMC "); return "EMC\n"; }

    /// <summary>Objekte des Strukturbaums (nach allen übrigen nummeriert): Wurzel, Dokument, ParentTree, je Markierung ein Element.</summary>
    (int root, int doc, int parentTree, List<List<int>> elems)? AllocateTags(Func<int> next)
    {
        if (!Tagged) return null;
        int root = next(), doc = next(), pt = next();
        var elems = _pages.Select(p => (_marks.TryGetValue(p, out var l) ? l : new()).Select(_ => next()).ToList()).ToList();
        return (root, doc, pt, elems);
    }

    void WriteTags((int root, int doc, int parentTree, List<List<int>> elems) t, List<(int page, int content)> pageObjs, Action<int, string> obj)
    {
        var all = new List<int>();
        for (int i = 0; i < _pages.Count; i++)
        {
            var marks = _marks.TryGetValue(_pages[i], out var l) ? l : new();
            for (int k = 0; k < marks.Count; k++)
            {
                var (role, alt) = marks[k];
                string altPart = alt != null ? $" /Alt {PdfTextString(alt)}" : "";
                obj(t.elems[i][k], $"<< /Type /StructElem /S /{role} /P {t.doc} 0 R /Pg {pageObjs[i].page} 0 R /K {k}{altPart} >>");
                all.Add(t.elems[i][k]);
            }
        }
        obj(t.doc, $"<< /Type /StructElem /S /Document /P {t.root} 0 R /K [{string.Join(" ", all.Select(e => $"{e} 0 R"))}] >>");
        var nums = new StringBuilder();
        for (int i = 0; i < _pages.Count; i++) nums.Append(i).Append(" [").Append(string.Join(" ", t.elems[i].Select(e => $"{e} 0 R"))).Append("] ");
        obj(t.parentTree, $"<< /Nums [{nums}] >>");
        obj(t.root, $"<< /Type /StructTreeRoot /K {t.doc} 0 R /ParentTree {t.parentTree} 0 R /ParentTreeNextKey {_pages.Count} >>");
    }

    /// <summary>Selbstprüfung: jede Textausgabe liegt in einer Markierung, jedes Figure hat einen Alternativtext.</summary>
    public IReadOnlyList<string> Audit()
    {
        var problems = new List<string>();
        if (!Tagged) { problems.Add("not tagged"); return problems; }
        for (int i = 0; i < _pages.Count; i++)
        {
            string c = _pages[i].ToString();
            int bt = CountOf(c, "BT "), marked = CountOf(c, "BDC BT ") + CountOf(c, "BMC BT ");
            if (bt != marked) problems.Add($"page {i + 1}: {bt - marked} unmarked text objects");
            if (_marks.TryGetValue(_pages[i], out var l) && l.Any(m => m.role == "Figure" && string.IsNullOrWhiteSpace(m.alt))) problems.Add($"page {i + 1}: figure without /Alt");
        }
        return problems;
    }

    static int CountOf(string s, string what) { int n = 0, i = 0; while ((i = s.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; } return n; }
}
