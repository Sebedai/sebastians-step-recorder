using System.Text;
using StepRecorder.Text;

namespace StepRecorder.Export.Formats;

/// <summary>Formatierter Text in Word (13, Paket 21): fett, kursiv, Eingabe, Tasten, Bedienelemente, Links, Listen.</summary>
public sealed partial class SimpleDocx
{
    readonly List<string> _links = new();

    /// <summary>Beziehung für einen externen Link (nur Ziele, die <see cref="RichDoc.IsAllowedLink"/> erlaubt).</summary>
    string LinkRel(string url)
    {
        int i = _links.IndexOf(url);
        if (i < 0) { _links.Add(url); i = _links.Count - 1; }
        return "rIdLink" + (i + 1);
    }

    static string RunX(string text, bool bold, bool italic, bool mono = false, string? color = null, bool underline = false, bool key = false)
    {
        var rpr = new StringBuilder();
        if (mono || key) rpr.Append("<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\"/>");
        if (bold) rpr.Append("<w:b/>");
        if (italic) rpr.Append("<w:i/>");
        if (color != null) rpr.Append($"<w:color w:val=\"{color}\"/>");
        if (key) rpr.Append("<w:sz w:val=\"19\"/><w:bdr w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"8B93A0\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F1F2F5\"/>");
        else if (mono) rpr.Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F3F3F1\"/>");
        if (underline) rpr.Append("<w:u w:val=\"single\"/>");
        var sb = new StringBuilder("<w:r>");
        if (rpr.Length > 0) sb.Append("<w:rPr>").Append(rpr).Append("</w:rPr>");
        sb.Append("<w:t xml:space=\"preserve\">").Append(X(text)).Append("</w:t></w:r>");
        return sb.ToString();
    }

    /// <summary>Läufe eines formatierten Absatzes.</summary>
    string RichRuns(IEnumerable<RichInline> inl, bool baseItalic = false, string? color = null)
    {
        var sb = new StringBuilder();
        foreach (var x in inl)
        {
            bool b = x.Style.HasFlag(RichStyle.Bold), i = baseItalic || x.Style.HasFlag(RichStyle.Italic);
            switch (x.Kind)
            {
                case RichKind.Text or RichKind.Placeholder: sb.Append(RunX(x.Text, b, i, color: color)); break;
                case RichKind.Code: sb.Append(RunX(x.Text, b, i, mono: true, color: color)); break;
                case RichKind.Ui: sb.Append(RunX(x.Text, true, i, color: color)); break;
                case RichKind.Key:
                {
                    var keys = x.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    for (int k = 0; k < keys.Length; k++) { if (k > 0) sb.Append(RunX("+", false, false, color: color)); sb.Append(RunX(" " + keys[k] + " ", false, false, key: true)); }
                    break;
                }
                case RichKind.Link when x.Target != null && RichDoc.IsAllowedLink(x.Target, out var url):
                    sb.Append($"<w:hyperlink r:id=\"{LinkRel(url)}\" w:history=\"1\">").Append(RunX(x.Text, b, i, color: "1F4E9C", underline: true)).Append("</w:hyperlink>");
                    break;
                case RichKind.Link: sb.Append(RunX(x.Text, b, i, color: color)); break;
                case RichKind.Break: sb.Append("<w:r><w:br/></w:r>"); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Formatierter Text als Absätze und Listen.</summary>
    public void RichBlocks(RichDoc doc, bool italic = false, string? color = null, string? pPr = null)
    {
        foreach (var b in doc.Blocks)
        {
            if (b.Kind == RichBlockKind.Paragraph) { _body.Append("<w:p>").Append(pPr ?? "").Append(RichRuns(b.Inlines!, italic, color)).Append("</w:p>"); continue; }
            for (int k = 0; k < b.Items!.Count; k++)
            {
                string label = b.Kind == RichBlockKind.Bullet ? "•" : (b.Start + k) + ".";
                _body.Append("<w:p><w:pPr><w:pStyle w:val=\"ListBullet\"/></w:pPr>").Append(RunX(label + "\t", false, italic, color: color)).Append(RichRuns(b.Items[k], italic, color)).Append("</w:p>");
            }
        }
    }

    /// <summary>Hinweiskasten mit formatiertem Text: die Kennzeichnung steht vor dem ersten Absatz, alle Absätze tragen Rand und Fläche.</summary>
    public void NoteRich(string label, RichDoc doc, string color, string fill)
    {
        string ppr = $"<w:pPr><w:pStyle w:val=\"Note\"/><w:pBdr><w:left w:val=\"single\" w:sz=\"18\" w:space=\"6\" w:color=\"{color}\"/></w:pBdr><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{fill}\"/></w:pPr>";
        bool first = true;
        foreach (var b in doc.Blocks)
        {
            var lines = b.Kind == RichBlockKind.Paragraph ? new[] { ((string?)null, b.Inlines!) } : b.Items!.Select((it, k) => ((string?)(b.Kind == RichBlockKind.Bullet ? "•" : (b.Start + k) + "."), it)).ToArray();
            foreach (var (lab, inl) in lines)
            {
                _body.Append("<w:p>").Append(ppr);
                if (first) { _body.Append(Run(label + " ", true, false, color)); first = false; }
                if (lab != null) _body.Append(RunX(lab + " ", false, false));
                _body.Append(RichRuns(inl)).Append("</w:p>");
            }
        }
        if (first) _body.Append("<w:p>").Append(ppr).Append(Run(label, true, false, color)).Append("</w:p>");
    }

    /// <summary>Beziehungen der externen Links für <c>document.xml.rels</c>.</summary>
    string LinkRelationships(string idPrefix = "rIdLink")
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _links.Count; i++)
            sb.Append($"<Relationship Id=\"{idPrefix}{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"{X(_links[i])}\" TargetMode=\"External\"/>");
        return sb.ToString();
    }
}
