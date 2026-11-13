using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace StepRecorder.Text;

/// <summary>Darstellung der Auszeichnung im Editor: Farben und Schriften für Eingabe, Tasten, Bedienelemente und Links.</summary>
public sealed record RichFlowStyle(FontFamily Mono, Brush CodeBack, Brush KeyBack, Brush UiFore, Brush LinkFore)
{
    public static RichFlowStyle Default { get; } = new(new FontFamily("Consolas"), Freeze(Color.FromArgb(40, 128, 128, 128)), Freeze(Color.FromArgb(70, 120, 132, 150)), Freeze(Color.FromRgb(0x2F, 0x6F, 0xD1)), Freeze(Color.FromRgb(0x2F, 0x6F, 0xD1)));
    static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}

/// <summary>
/// Formatierter Text ⇄ <see cref="FlowDocument"/> für das Eingabefeld des Editors (13, Paket 21). Jede Zeile der Auszeichnung ist ein
/// <see cref="Paragraph"/> ohne Abstand; eine leere Zeile trennt Absätze. Listen werden <see cref="List"/>. Fett und kursiv sind
/// Schriftschnitt, Eingabe ist die Festbreitenschrift, Tasten haben die Tastenfläche als Hintergrund, Bedienelemente die Farbe
/// <see cref="RichFlowStyle.UiFore"/>, Links sind <see cref="Hyperlink"/> mit dem Ziel in <c>Tag</c>.
/// </summary>
public static class RichFlow
{
    public static FlowDocument ToFlow(RichDoc doc, RichFlowStyle st)
    {
        var fd = new FlowDocument { PagePadding = new Thickness(0) };
        Paragraph Line(IEnumerable<RichInline> inl)
        {
            var p = new Paragraph { Margin = new Thickness(0) };
            foreach (var x in inl) p.Inlines.Add(ToInline(x, st));
            return p;
        }
        for (int b = 0; b < doc.Blocks.Count; b++)
        {
            var bl = doc.Blocks[b];
            if (b > 0) fd.Blocks.Add(new Paragraph { Margin = new Thickness(0) });   // Leerzeile = neuer Absatz
            if (bl.Kind == RichBlockKind.Paragraph)
            {
                foreach (var line in SplitLines(bl.Inlines!)) fd.Blocks.Add(Line(line));
                continue;
            }
            var list = new List { MarkerStyle = bl.Kind == RichBlockKind.Bullet ? TextMarkerStyle.Disc : TextMarkerStyle.Decimal, Margin = new Thickness(0), Padding = new Thickness(22, 0, 0, 0) };
            if (bl.Kind == RichBlockKind.Numbered && bl.Start != 1) list.StartIndex = bl.Start;
            foreach (var item in bl.Items!)
            {
                var p = new Paragraph { Margin = new Thickness(0) };
                foreach (var x in item) p.Inlines.Add(ToInline(x, st));
                list.ListItems.Add(new ListItem(p));
            }
            fd.Blocks.Add(list);
        }
        if (fd.Blocks.Count == 0) fd.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
        return fd;
    }

    static IEnumerable<List<RichInline>> SplitLines(IReadOnlyList<RichInline> inl)
    {
        var cur = new List<RichInline>();
        foreach (var x in inl)
        {
            if (x.Kind == RichKind.Break) { yield return cur; cur = new(); continue; }
            cur.Add(x);
        }
        yield return cur;
    }

    static Inline ToInline(RichInline x, RichFlowStyle st)
    {
        Inline r = x.Kind switch
        {
            RichKind.Code => new Run(x.Text) { FontFamily = st.Mono, Background = st.CodeBack },
            RichKind.Key => new Run(x.Text) { Background = st.KeyBack },
            RichKind.Ui => new Run(x.Text) { Foreground = st.UiFore, FontWeight = FontWeights.SemiBold },
            RichKind.Link => new Hyperlink(new Run(x.Text)) { Tag = x.Target, Foreground = st.LinkFore, ToolTip = x.Target },
            RichKind.Break => new LineBreak(),
            _ => new Run(x.Text)
        };
        if (x.Kind != RichKind.Ui && x.Style.HasFlag(RichStyle.Bold)) r.FontWeight = FontWeights.Bold;
        if (x.Style.HasFlag(RichStyle.Italic)) r.FontStyle = FontStyles.Italic;
        return r;
    }

    /// <summary>Dokument aus dem Eingabefeld lesen (Schriftschnitte werden einschließlich Vererbung gelesen).</summary>
    public static RichDoc FromFlow(FlowDocument fd, RichFlowStyle st)
    {
        var blocks = new List<RichBlock>();
        var para = new List<RichInline>();
        bool havePara = false;
        void Flush()
        {
            if (havePara && para.Any(i => i.Kind != RichKind.Break)) blocks.Add(new RichBlock(RichBlockKind.Paragraph, Merge(TrimBreaks(para))));
            para = new(); havePara = false;
        }
        foreach (var b in fd.Blocks) Visit(b);
        Flush();
        return new RichDoc(blocks);

        void Visit(Block b)
        {
            switch (b)
            {
                case Paragraph p:
                {
                    var inl = new List<RichInline>();
                    foreach (var i in p.Inlines) Collect(i, inl, st, null);
                    if (inl.All(i => i.Kind == RichKind.Break || i.Text.Trim().Length == 0 && i.Kind == RichKind.Text)) { Flush(); return; }   // leere Zeile
                    if (havePara) para.Add(new RichInline(RichKind.Break, "\n"));
                    para.AddRange(inl); havePara = true;
                    return;
                }
                case List l:
                {
                    Flush();
                    var items = new List<IReadOnlyList<RichInline>>();
                    foreach (var li in l.ListItems)
                    {
                        var inl = new List<RichInline>();
                        bool first = true;
                        foreach (var blk in li.Blocks)
                        {
                            if (blk is Paragraph pp)
                            {
                                if (!first) inl.Add(new RichInline(RichKind.Break, "\n"));
                                foreach (var i in pp.Inlines) Collect(i, inl, st, null);
                                first = false;
                            }
                            else if (blk is List nested)   // verschachtelte Listen werden flach
                                foreach (var nli in nested.ListItems) foreach (var np in nli.Blocks.OfType<Paragraph>()) { inl.Add(new RichInline(RichKind.Break, "\n")); foreach (var i in np.Inlines) Collect(i, inl, st, null); }
                        }
                        var m = Merge(TrimBreaks(inl));
                        if (m.Count > 0) items.Add(m);
                    }
                    if (items.Count > 0)
                        blocks.Add(new RichBlock(l.MarkerStyle is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman ? RichBlockKind.Numbered : RichBlockKind.Bullet, Items: items, Start: Math.Max(1, l.StartIndex)));
                    return;
                }
                case Section s: foreach (var c in s.Blocks) Visit(c); return;
                default:
                {
                    // Tabellen, Bilder usw. lässt das Feld nicht zu; falls doch: als Text
                    var t = new TextRange(b.ContentStart, b.ContentEnd).Text;
                    if (t.Trim().Length > 0) { if (havePara) para.Add(new RichInline(RichKind.Break, "\n")); para.Add(new RichInline(RichKind.Text, t.Replace("\r", "").Replace("\n", " "))); havePara = true; }
                    return;
                }
            }
        }
    }

    static void Collect(Inline i, List<RichInline> into, RichFlowStyle st, string? link)
    {
        switch (i)
        {
            case Hyperlink h:
            {
                string? target = h.Tag as string ?? h.NavigateUri?.OriginalString;
                var inner = new List<RichInline>();
                foreach (var c in h.Inlines) Collect(c, inner, st, null);
                string label = string.Concat(inner.Where(x => x.Kind != RichKind.Break).Select(x => x.Text));
                if (label.Length == 0) return;
                var style = inner.FirstOrDefault()?.Style ?? RichStyle.None;
                if (target != null && RichDoc.IsAllowedLink(target, out var ok)) into.Add(new RichInline(RichKind.Link, label, style, ok));
                else into.AddRange(inner);
                return;
            }
            case Span sp: foreach (var c in sp.Inlines) Collect(c, into, st, link); return;
            case LineBreak: into.Add(new RichInline(RichKind.Break, "\n")); return;
            case Run r:
            {
                string text = r.Text.Replace("\r", "").Replace("\n", " ");
                if (text.Length == 0) return;
                var style = RichStyle.None;
                if (r.FontStyle == FontStyles.Italic || r.FontStyle == FontStyles.Oblique) style |= RichStyle.Italic;
                bool ui = SameColor(r.Foreground, st.UiFore) && r.Parent is not Hyperlink;
                if (r.FontWeight.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight() && !ui) style |= RichStyle.Bold;
                if (IsMono(r.FontFamily, st)) into.Add(new RichInline(RichKind.Code, text, style));
                else if (SameColor(r.Background, st.KeyBack)) into.Add(new RichInline(RichKind.Key, text.Trim(), RichStyle.None));
                else if (ui) into.Add(new RichInline(RichKind.Ui, text, style & RichStyle.Italic));
                else into.Add(new RichInline(RichKind.Text, text, style));
                return;
            }
            case InlineUIContainer: return;
        }
    }

    static bool IsMono(FontFamily f, RichFlowStyle st) => f != null && (f.Source == st.Mono.Source || f.Source.Contains("Consolas", StringComparison.OrdinalIgnoreCase) || f.Source.Contains("Cascadia", StringComparison.OrdinalIgnoreCase) || f.Source.Contains("Courier", StringComparison.OrdinalIgnoreCase));

    static bool SameColor(Brush? a, Brush b) => a is SolidColorBrush x && b is SolidColorBrush y && x.Color == y.Color;

    static List<RichInline> TrimBreaks(List<RichInline> l)
    {
        int s = 0, e = l.Count;
        while (s < e && l[s].Kind == RichKind.Break) s++;
        while (e > s && l[e - 1].Kind == RichKind.Break) e--;
        return l.GetRange(s, e - s);
    }

    /// <summary>Nachbarn gleicher Art und gleichen Stils zusammenfassen (aufeinanderfolgende Tasten werden eine Kombination).</summary>
    static List<RichInline> Merge(List<RichInline> list)
    {
        var r = new List<RichInline>();
        foreach (var x in list)
        {
            if (r.Count > 0 && r[^1].Kind == x.Kind && r[^1].Style == x.Style && x.Kind is RichKind.Text or RichKind.Code or RichKind.Ui)
                r[^1] = r[^1] with { Text = r[^1].Text + x.Text };
            else if (r.Count > 0 && r[^1].Kind == RichKind.Key && x.Kind == RichKind.Key)
                r[^1] = r[^1] with { Text = r[^1].Text + "+" + x.Text };
            else r.Add(x);
        }
        // Tasten: „Strg+S“ als Kombination, leere Teile entfallen
        return r.Select(x => x.Kind == RichKind.Key ? x with { Text = string.Join("+", x.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) } : x)
                .Where(x => x.Kind is not (RichKind.Key or RichKind.Code or RichKind.Ui) || x.Text.Length > 0).ToList();
    }
}
