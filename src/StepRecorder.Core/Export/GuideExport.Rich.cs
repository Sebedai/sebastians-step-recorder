using System.Text;
using StepRecorder.Text;

namespace StepRecorder.Export;

/// <summary>Formatierter Text in HTML, Markdown und als Textzeilen (13, Paket 21). Texte ohne Formatierung laufen weiter über die alten Wege.</summary>
public static partial class GuideExport
{
    /// <summary>Zusätzliche Stile für formatierten Text – nur eingefügt, wenn eine Anleitung ihn enthält (bestehende Ausgaben bleiben gleich).</summary>
    const string RichCss = "kbd{font:600 .82em \"Segoe UI\",system-ui,sans-serif;border:1px solid var(--line);border-bottom-width:2px;border-radius:4px;padding:0 .4em;background:var(--soft);white-space:nowrap}\n" +
        "code{font-family:Consolas,\"Cascadia Mono\",monospace;font-size:.92em;background:var(--soft);border-radius:3px;padding:0 .25em}\n" +
        "b.ui{font-weight:650}\n.rt table{border-collapse:collapse;margin:4px 0 10px;font-size:.95em}.rt th,.rt td{border:1px solid var(--line);padding:4px 8px;text-align:left;vertical-align:top}.rt th{background:var(--soft);font-weight:650}\na.ref{text-decoration:none;border-bottom:1px dotted currentColor}\n.rt p{margin:0 0 6px}.rt p:last-child{margin-bottom:8px}.rt ul,.rt ol{margin:2px 0 8px;padding-left:22px}.rt li{margin:2px 0}\n" +
        ".note .rt p,.box .rt p{margin:0 0 4px}.note .rt p:last-child,.box .rt p:last-child{margin:0}.note .rt ul,.note .rt ol{margin:2px 0 0}\na{color:var(--accent)}\n";

    static string HtmlInline(IEnumerable<RichInline> inl)
    {
        var e = Fmt.HtmlEscape;
        var sb = new StringBuilder();
        foreach (var x in inl)
        {
            string body = x.Kind switch
            {
                RichKind.Text or RichKind.Placeholder => e(x.Text),
                RichKind.Code => "<code>" + e(x.Text) + "</code>",
                RichKind.Ui => "<b class=\"ui\">" + e(x.Text) + "</b>",
                RichKind.Key => string.Join("+", x.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(k => "<kbd>" + e(k) + "</kbd>")),
                RichKind.Link when x.Target != null && RichDoc.IsAllowedLink(x.Target, out var u) => $"<a href=\"{e(u)}\" rel=\"noopener noreferrer\">{e(x.Text)}</a>",
                RichKind.Link => e(x.Text),
                RichKind.Ref when x.Target is { Length: > 1 } t && t[0] == '#' => $"<a class=\"ref\" href=\"{e(t)}\">{e(x.Text)}</a>",
                RichKind.Ref => e(x.Text),
                RichKind.Break => "<br>",
                _ => ""
            };
            if (x.Style.HasFlag(RichStyle.Italic) && x.Kind != RichKind.Break) body = "<em>" + body + "</em>";
            if (x.Style.HasFlag(RichStyle.Bold) && x.Kind != RichKind.Break) body = "<strong>" + body + "</strong>";
            sb.Append(body);
        }
        return sb.ToString();
    }

    /// <summary>Formatierter Text als HTML-Block (<c>div.rt</c> mit Absätzen und Listen).</summary>
    static string HtmlRich(RichText r, string cls = "rt", string? leadHtml = null)
    {
        var sb = new StringBuilder("<div class=\"").Append(cls).Append("\">");
        bool lead = leadHtml != null;
        foreach (var b in r.Doc.Blocks)
        {
            if (b.Kind == RichBlockKind.Paragraph) { sb.Append("<p>"); if (lead) { sb.Append(leadHtml); lead = false; } sb.Append(HtmlInline(b.Inlines!)).Append("</p>"); continue; }
            if (lead) { sb.Append("<p>").Append(leadHtml).Append("</p>"); lead = false; }
            if (b.Kind == RichBlockKind.Table) { sb.Append(HtmlTable(b)); continue; }
            sb.Append(b.Kind == RichBlockKind.Bullet ? "<ul>" : b.Start != 1 ? $"<ol start=\"{b.Start}\">" : "<ol>");
            foreach (var it in b.Items!) sb.Append("<li>").Append(HtmlInline(it)).Append("</li>");
            sb.Append(b.Kind == RichBlockKind.Bullet ? "</ul>" : "</ol>");
        }
        if (lead) sb.Append("<p>").Append(leadHtml).Append("</p>");
        return sb.Append("</div>").ToString();
    }

    static string HtmlTable(RichBlock b)
    {
        var sb = new StringBuilder("<table>");
        int cols = Math.Max(1, b.Columns);
        for (int r = 0; r < b.Rows!.Count; r++)
        {
            bool head = r == 0 && b.Header;
            sb.Append(head ? "<thead><tr>" : "<tr>");
            for (int c = 0; c < cols; c++)
            {
                string cell = c < b.Rows[r].Count ? HtmlInline(b.Rows[r][c]) : "";
                sb.Append(head ? "<th scope=\"col\">" : "<td>").Append(cell).Append(head ? "</th>" : "</td>");
            }
            sb.Append(head ? "</tr></thead><tbody>" : "</tr>");
        }
        if (!b.Header) sb.Insert("<table>".Length, "<tbody>");
        return sb.Append("</tbody></table>").ToString();
    }

    /// <summary>Stile für formatierten Text vor <c>&lt;/style&gt;</c> einsetzen, wenn die Anleitung welchen enthält.</summary>
    /// <summary>Unterkapitel (13, Paket 24): kleinere Überschrift, im Verzeichnis eingerückt – nur in Anleitungen mit Unterkapiteln.</summary>
    const string SubChapterCss = "h2.chapter.sub{font-size:1.2em;margin:28px 0 6px;padding-bottom:5px;border-bottom-width:1px}\nnav.toc li.sub{padding-left:16px}\n";

    static string WithCss(string html, bool uses, string css)
    {
        if (!uses) return html;
        int i = html.IndexOf("</style>", StringComparison.Ordinal);
        return i < 0 ? html : html[..i] + css + html[i..];
    }

    static string WithRichCss(string html, bool uses)
    {
        if (!uses) return html;
        int i = html.IndexOf("</style>", StringComparison.Ordinal);
        return i < 0 ? html : html[..i] + RichCss + html[i..];
    }

    static string MdInlineRich(IEnumerable<RichInline> inl)
    {
        var sb = new StringBuilder();
        foreach (var x in inl)
        {
            string body = x.Kind switch
            {
                RichKind.Text or RichKind.Placeholder => MdText(x.Text),
                RichKind.Code => MdCode(x.Text),
                RichKind.Ui => "**" + MdText(x.Text) + "**",
                RichKind.Key => string.Join("+", x.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(k => "<kbd>" + Fmt.HtmlEscape(k) + "</kbd>")),
                RichKind.Link when x.Target != null && RichDoc.IsAllowedLink(x.Target, out var u) => "[" + MdText(x.Text) + "](" + u + ")",
                RichKind.Link => MdText(x.Text),
                RichKind.Ref when x.Target is { Length: > 1 } t && t[0] == '#' => "[" + MdText(x.Text) + "](" + t + ")",
                RichKind.Ref => MdText(x.Text),
                RichKind.Break => "\\\n",
                _ => ""
            };
            if (x.Kind is RichKind.Text or RichKind.Placeholder or RichKind.Code or RichKind.Link && x.Style != RichStyle.None && body.Trim().Length > 0)
            {
                string d = x.Style switch { RichStyle.Bold => "**", RichStyle.Italic => "*", _ => "***" };
                string lead = body[..(body.Length - body.TrimStart().Length)], trail = body[body.TrimEnd().Length..];
                body = lead + d + body.Trim() + d + trail;
            }
            sb.Append(body);
        }
        return sb.ToString();
    }

    static string MdCode(string code)
    {
        int longest = 0, run = 0;
        foreach (char c in code) { run = c == '`' ? run + 1 : 0; longest = Math.Max(longest, run); }
        string d = new('`', longest + 1);
        return d + (code.StartsWith('`') || code.EndsWith('`') ? " " + code + " " : code) + d;
    }

    /// <summary>Formatierter Text als Markdown-Zeilen mit Einrückung (Absätze durch Leerzeile getrennt).</summary>
    static void AppendRichMd(StringBuilder sb, RichText r, string ind)
    {
        bool first = true;
        foreach (var b in r.Doc.Blocks)
        {
            if (!first) sb.Append('\n');
            first = false;
            if (b.Kind == RichBlockKind.Paragraph)
            {
                foreach (var line in MdInlineRich(b.Inlines!).Split('\n')) sb.Append(ind).Append(line).Append('\n');
                continue;
            }
            if (b.Kind == RichBlockKind.Table)
            {
                int cols = Math.Max(1, b.Columns);
                string Row(IReadOnlyList<IReadOnlyList<RichInline>> row) => "| " + string.Join(" | ", Enumerable.Range(0, cols).Select(c => c < row.Count ? MdInlineRich(row[c]).Replace("\\\n", " ").Replace("|", "\\|") : "")) + " |";
                int start = 0;
                if (b.Header) { sb.Append(ind).Append(Row(b.Rows![0])).Append('\n'); start = 1; }
                else sb.Append(ind).Append("| " + string.Join(" | ", Enumerable.Repeat(" ", cols)) + " |").Append('\n');
                sb.Append(ind).Append("|" + string.Concat(Enumerable.Repeat("---|", cols))).Append('\n');
                for (int ri = start; ri < b.Rows!.Count; ri++) sb.Append(ind).Append(Row(b.Rows[ri])).Append('\n');
                continue;
            }
            for (int k = 0; k < b.Items!.Count; k++)
            {
                string m = b.Kind == RichBlockKind.Bullet ? "- " : (b.Start + k) + ". ";
                var lines = MdInlineRich(b.Items[k]).Split('\n');
                sb.Append(ind).Append(m).Append(lines[0]).Append('\n');
                foreach (var l in lines.Skip(1)) sb.Append(ind).Append(new string(' ', m.Length)).Append(l).Append('\n');
            }
        }
    }

    /// <summary>Formatierter Text als Zeilen ohne Auszeichnung, Listen mit „•“/„1.“ (PowerPoint, Fehlerbericht).</summary>
    public static string PlainLines(RichText r)
    {
        var lines = new List<string>();
        foreach (var b in r.Doc.Blocks)
        {
            if (b.Kind == RichBlockKind.Paragraph) { lines.Add(RichDoc.InlineText(b.Inlines!)); continue; }
            if (b.Kind == RichBlockKind.Table) { foreach (var row in b.Rows!) lines.Add(string.Join("  |  ", row.Select(RichDoc.InlineText))); continue; }
            for (int k = 0; k < b.Items!.Count; k++) lines.Add((b.Kind == RichBlockKind.Bullet ? "• " : (b.Start + k) + ". ") + RichDoc.InlineText(b.Items[k]));
        }
        return string.Join("\n", lines);
    }
}
