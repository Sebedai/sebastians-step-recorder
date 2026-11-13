using System.Text;
using StepRecorder.Text;

namespace StepRecorder.Export.Formats;

/// <summary>
/// Formatierter Text im PDF (13, Paket 21): Läufe mit fett, kursiv und Festbreitenschrift, Tastenkappen als Kästchen, Bedienelemente fett,
/// Weblinks als Link-Anmerkung, Aufzählungen und Nummerierung. Zeilenumbruch nur an Leerzeichen; was ohne Leerzeichen
/// zusammensteht („**Speichern**.“), bleibt auf einer Zeile.
/// </summary>
public sealed partial class SimplePdf
{
    readonly List<List<(double x, double y, double w, double h, string url)>> _uris = new();
    readonly HashSet<string> _stdUsed = new();
    static readonly (string res, string name)[] StdFonts = { ("F3", "Helvetica-Oblique"), ("F4", "Helvetica-BoldOblique"), ("F5", "Courier"), ("F6", "Courier-Bold") };

    string StdFont(bool bold, bool italic, bool mono)
    {
        string res = mono ? (bold ? "F6" : "F5") : italic ? (bold ? "F4" : "F3") : bold ? "F2" : "F1";
        if (res is not ("F1" or "F2")) _stdUsed.Add(res);
        return res;
    }

    /// <summary>Breite mit Stil (Courier: 600/1000 je Zeichen; kursiv wie aufrecht).</summary>
    public static double TextWidth(string s, double size, bool bold, bool mono)
    {
        if (!mono) return TextWidth(s, size, bold);
        double w = 0;
        foreach (var r in (s ?? "").EnumerateRunes())
        {
            var (font, code) = Classify(r.Value, bold);
            w += font switch { 0 => 600, -1 => 556, _ => FontOf(font)!.Width1000(code) };
        }
        return w * size / 1000.0;
    }

    /// <summary>Weblink-Fläche auf der aktuellen Seite.</summary>
    public void LinkUri(double x, double y, double w, double h, string url)
    {
        if (RichDoc.IsAllowedLink(url, out var ok)) _uris[^1].Add((x, y, w, h, ok));
    }

    sealed record Piece(string Text, bool Bold, bool Italic, bool Mono, bool Key, string? Url)
    {
        public double Width(double size) => Key ? TextWidth(Text, size * 0.86, false, false) + 6 : TextWidth(Text, size, Bold, Mono);
    }

    /// <summary>Wort (ohne Leerzeichen) aus Stücken verschiedenen Stils; <c>Forced</c> = Zeilenumbruch.</summary>
    sealed record Seg(List<Piece> Pieces, bool Forced)
    {
        public double Width(double size) => Pieces.Sum(p => p.Width(size));
    }

    static List<Seg> Segments(IEnumerable<RichInline> inl)
    {
        var segs = new List<Seg>();
        var cur = new List<Piece>();
        void Close() { if (cur.Count > 0) { segs.Add(new Seg(cur, false)); cur = new(); } }
        foreach (var x in inl)
        {
            bool b = x.Style.HasFlag(RichStyle.Bold), i = x.Style.HasFlag(RichStyle.Italic);
            switch (x.Kind)
            {
                case RichKind.Break: Close(); segs.Add(new Seg(new(), true)); continue;
                case RichKind.Key:
                {
                    var keys = x.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    for (int k = 0; k < keys.Length; k++) { if (k > 0) cur.Add(new Piece("+", false, false, false, false, null)); cur.Add(new Piece(keys[k], false, false, false, true, null)); }
                    continue;
                }
            }
            bool mono = x.Kind == RichKind.Code, bold = b || x.Kind == RichKind.Ui;
            string? url = x.Kind == RichKind.Link && x.Target != null && RichDoc.IsAllowedLink(x.Target, out var u) ? u : null;
            // an Leerzeichen trennen (in Code nicht – dort bleibt der Inhalt zusammen, außer er ist zu lang)
            var parts = mono ? new[] { x.Text } : x.Text.Split(' ');
            for (int k = 0; k < parts.Length; k++)
            {
                if (k > 0) Close();
                if (parts[k].Length > 0) cur.Add(new Piece(parts[k], bold, i, mono, false, url));
            }
        }
        Close();
        return segs;
    }

    /// <summary>Zeilen aus Wörtern: gierig füllen, Leerzeichen zwischen Wörtern, zu lange Wörter hart trennen.</summary>
    static List<List<Seg>> LayoutLines(List<Seg> segs, double width, double size)
    {
        var lines = new List<List<Seg>> { new() };
        double used = 0, space = TextWidth(" ", size);
        foreach (var s in segs)
        {
            if (s.Forced) { lines.Add(new()); used = 0; continue; }
            double w = s.Width(size);
            if (lines[^1].Count > 0 && used + space + w > width) { lines.Add(new()); used = 0; }
            if (w > width && s.Pieces.Count == 1 && !s.Pieces[0].Key)
            {
                // überlanges Wort: in Teile, die je eine Zeile füllen
                var p = s.Pieces[0];
                string rest = p.Text;
                while (rest.Length > 0)
                {
                    int n = rest.Length;
                    while (n > 1 && new Piece(rest[..n], p.Bold, p.Italic, p.Mono, false, p.Url).Width(size) > width - (lines[^1].Count > 0 ? used + space : 0)) n--;
                    if (n < rest.Length && char.IsHighSurrogate(rest[n - 1]) && n > 1) n--;
                    lines[^1].Add(new Seg(new() { p with { Text = rest[..n] } }, false));
                    rest = rest[n..];
                    if (rest.Length > 0) { lines.Add(new()); used = 0; }
                    else used += new Piece(lines[^1][^1].Pieces[0].Text, p.Bold, p.Italic, p.Mono, false, null).Width(size);
                }
                continue;
            }
            used += (lines[^1].Count > 0 ? space : 0) + w;
            lines[^1].Add(s);
        }
        return lines;
    }

    static double LineHeight(List<Seg> line, double size) => size * (line.Any(s => s.Pieces.Any(p => p.Key)) ? 1.55 : 1.35);

    /// <summary>Höhe eines formatierten Absatzes.</summary>
    public static double MeasureRich(IEnumerable<RichInline> inl, double width, double size)
        => LayoutLines(Segments(inl), width, size).Sum(l => LineHeight(l, size));

    /// <summary>Höhe eines ganzen formatierten Texts (Absätze und Listen), wie <see cref="RichText"/> ihn zeichnet.</summary>
    public static double MeasureRichDoc(RichDoc doc, double width, double size, double paraGap = 4)
    {
        double h = 0;
        foreach (var b in doc.Blocks)
        {
            if (b.Kind == RichBlockKind.Paragraph) h += MeasureRich(b.Inlines!, width, size) + paraGap;
            else foreach (var it in b.Items!) h += MeasureRich(it, width - 14, size) + 2;
        }
        return h;
    }

    void DrawLine(List<Seg> line, double x, double baseY, double size, (double r, double g, double b) color)
    {
        double space = TextWidth(" ", size);
        bool first = true;
        foreach (var s in line)
        {
            if (!first) x += space;
            first = false;
            foreach (var p in s.Pieces)
            {
                double w = p.Width(size);
                if (p.Key)
                {
                    double ks = size * 0.86, bh = size * 1.25;
                    FillRect(x, baseY - size * 0.33, w, bh, (0.95, 0.95, 0.96));
                    StrokeRect(x, baseY - size * 0.33, w, bh, (0.55, 0.58, 0.63), 0.6);
                    DrawTextTo(_cur, x + 3, baseY, p.Text, ks, false, (0.11, 0.12, 0.15), false, false);
                }
                else
                {
                    var c = p.Url != null ? (0.12, 0.31, 0.61) : color;
                    if (p.Mono) FillRect(x - 0.5, baseY - size * 0.25, w + 1, size * 1.1, (0.95, 0.95, 0.94));
                    DrawTextTo(_cur, x, baseY, p.Text, size, p.Bold, c, p.Italic, p.Mono);
                    if (p.Url != null)
                    {
                        Line(x, baseY - 1.5, x + w, baseY - 1.5, c, 0.5);
                        LinkUri(x, baseY - size * 0.3, w, size * 1.2, p.Url);
                    }
                }
                x += w;
            }
        }
    }

    /// <summary>Formatierter Absatz ab der aktuellen Position.</summary>
    public void RichParagraph(IEnumerable<RichInline> inl, double size = 10.5, double indent = 0, double spacingAfter = 6, (double r, double g, double b)? color = null, double width = 0)
    {
        if (width <= 0) width = ContentWidth - indent;
        foreach (var line in LayoutLines(Segments(inl), width, size))
        {
            double lh = LineHeight(line, size);
            EnsureSpace(lh);
            Y -= lh;
            DrawLine(line, Margin + indent, Y + size * 0.25 + (lh - size * 1.35) / 2, size, color ?? (0.1, 0.1, 0.1));
        }
        Y -= spacingAfter;
    }

    /// <summary>Formatierter Text: Absätze und Listen (Marke links, Text eingerückt).</summary>
    public void RichBlocks(RichDoc doc, double size = 10.5, double indent = 0, double spacingAfter = 6, (double r, double g, double b)? color = null, double width = 0)
    {
        if (width <= 0) width = ContentWidth - indent;
        for (int bi = 0; bi < doc.Blocks.Count; bi++)
        {
            var b = doc.Blocks[bi];
            bool last = bi == doc.Blocks.Count - 1;
            if (b.Kind == RichBlockKind.Paragraph) { RichParagraph(b.Inlines!, size, indent, last ? spacingAfter : 4, color, width); continue; }
            for (int k = 0; k < b.Items!.Count; k++)
            {
                string label = b.Kind == RichBlockKind.Bullet ? "•" : (b.Start + k) + ".";
                double top = Y;
                EnsureSpace(size * 1.35);
                if (Y > top) top = Y;   // neue Seite
                top = Y;
                DrawTextTo(_cur, Margin + indent + 2, Y - size * 1.35 + size * 0.25, label, size, false, color ?? (0.1, 0.1, 0.1), false, false);
                RichParagraph(b.Items[k], size, indent + 14, last && k == b.Items.Count - 1 ? spacingAfter : 2, color, width - 14);
            }
        }
    }

    /// <summary>Hinweiskasten mit formatiertem Text (wie <see cref="Callout"/>).</summary>
    public void CalloutRich(string label, RichDoc doc, (double r, double g, double b) bar, (double r, double g, double b) bg, double size = 10)
    {
        double pad = 7, w = ContentWidth - 2 * pad - 4;
        // Kennzeichnung als erstes, fettes Wort des ersten Absatzes
        var blocks = doc.Blocks.ToList();
        var labelInl = new RichInline(RichKind.Text, label + " ", RichStyle.Bold);
        if (blocks.Count == 0 || blocks[0].Kind != RichBlockKind.Paragraph) blocks.Insert(0, new RichBlock(RichBlockKind.Paragraph, new[] { labelInl }));
        else blocks[0] = blocks[0] with { Inlines = new[] { labelInl }.Concat(blocks[0].Inlines!).ToList() };
        var d2 = new RichDoc(blocks);
        double h = MeasureRichDoc(d2, w, size, 2) + 2 * pad - 2;
        EnsureSpace(h + 4);
        double top = Y;
        FillRect(Margin, Y - h, ContentWidth, h, bg);
        FillRect(Margin, Y - h, 3, h, bar);
        Y -= pad;
        double savedMargin = Margin;
        // innerhalb des Kastens zeichnen: Rand vorübergehend verschieben
        Margin = savedMargin + 4 + pad;
        try { RichBlocks(d2, size, 0, 0, null, w); }
        finally { Margin = savedMargin; }
        Y = top - h - 6;
    }
}
