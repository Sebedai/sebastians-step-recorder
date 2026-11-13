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

    /// <summary>
    /// Zeichenformatvorlagen für formatierten Text (13, Paket 31): Eingabe, Taste, Bedienelement, Link. Die Läufe tragen nur den
    /// Verweis auf die Vorlage – eine eigene Word-Vorlage kann <c>SREingabe</c>, <c>SRTaste</c>, <c>SRElement</c>, <c>SRLink</c>
    /// selbst gestalten. Angelegt werden nur benutzte Vorlagen.
    /// </summary>
    readonly SortedSet<string> _charStyles = new(StringComparer.Ordinal);

    static readonly Dictionary<string, (string name, string rpr)> CharStyleDefs = new()
    {
        ["SREingabe"] = ("SR Eingabe", "<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F3F3F1\"/>"),
        ["SRTaste"] = ("SR Taste", "<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\"/><w:sz w:val=\"19\"/><w:bdr w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"8B93A0\"/><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"F1F2F5\"/>"),
        ["SRElement"] = ("SR Bedienelement", "<w:b/>"),
        ["SRLink"] = ("SR Link", "<w:color w:val=\"1F4E9C\"/><w:u w:val=\"single\"/>"),
    };

    /// <summary>Definitionen der benutzten Zeichenformatvorlagen für <c>styles.xml</c>.</summary>
    string CharStylesXml() => string.Concat(_charStyles.Select(id => $"<w:style w:type=\"character\" w:customStyle=\"1\" w:styleId=\"{id}\"><w:name w:val=\"{CharStyleDefs[id].name}\"/><w:rPr>{CharStyleDefs[id].rpr}</w:rPr></w:style>"));

    string RunX(string text, bool bold, bool italic, bool mono = false, string? color = null, bool underline = false, bool key = false, bool ui = false)
    {
        var rpr = new StringBuilder();
        string? cs = ui ? "SRElement" : key ? "SRTaste" : mono ? "SREingabe" : underline && color == "1F4E9C" ? "SRLink" : null;
        if (cs != null) { _charStyles.Add(cs); rpr.Append($"<w:rStyle w:val=\"{cs}\"/>"); }
        if (bold) rpr.Append("<w:b/>");
        if (italic) rpr.Append("<w:i/>");
        if (color != null && cs != "SRLink") rpr.Append($"<w:color w:val=\"{color}\"/>");
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
                case RichKind.Ui: sb.Append(RunX(x.Text, false, i, color: color, ui: true)); break;
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
                case RichKind.Ref: sb.Append(RunX(x.Text, b, i, color: color)); break;
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
            if (b.Kind == RichBlockKind.Table) { TableX(b); continue; }
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
            if (b.Kind == RichBlockKind.Table) { if (first) { _body.Append("<w:p>").Append(ppr).Append(Run(label, true, false, color)).Append("</w:p>"); first = false; } TableX(b); continue; }
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

    /// <summary>Tabelle mit Rahmen; die Kopfzeile wiederholt sich auf jeder Seite und ist schattiert.</summary>
    void TableX(RichBlock b)
    {
        int cols = Math.Max(1, b.Columns);
        int tw = 9000, cw = tw / cols;
        var sb = new StringBuilder("<w:tbl><w:tblPr><w:tblW w:w=\"" + tw + "\" w:type=\"dxa\"/><w:tblBorders>");
        foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" }) sb.Append($"<w:{side} w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B8BEC8\"/>");
        sb.Append("</w:tblBorders><w:tblCellMar><w:left w:w=\"80\" w:type=\"dxa\"/><w:right w:w=\"80\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid>");
        for (int c = 0; c < cols; c++) sb.Append($"<w:gridCol w:w=\"{cw}\"/>");
        sb.Append("</w:tblGrid>");
        for (int r = 0; r < b.Rows!.Count; r++)
        {
            bool head = r == 0 && b.Header;
            sb.Append("<w:tr>");
            if (head) sb.Append("<w:trPr><w:tblHeader/></w:trPr>");
            for (int c = 0; c < cols; c++)
            {
                sb.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{cw}\" w:type=\"dxa\"/>");
                if (head) sb.Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"EEF0F3\"/>");
                sb.Append("</w:tcPr><w:p><w:pPr><w:spacing w:before=\"40\" w:after=\"40\"/></w:pPr>");
                if (c < b.Rows[r].Count) sb.Append(head ? RichRuns(b.Rows[r][c].Select(x => x.Kind is RichKind.Text or RichKind.Placeholder ? x with { Style = x.Style | RichStyle.Bold } : x)) : RichRuns(b.Rows[r][c]));
                sb.Append("</w:p></w:tc>");
            }
            sb.Append("</w:tr>");
        }
        sb.Append("</w:tbl><w:p><w:pPr><w:spacing w:after=\"60\"/></w:pPr></w:p>");
        _body.Append(sb);
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
