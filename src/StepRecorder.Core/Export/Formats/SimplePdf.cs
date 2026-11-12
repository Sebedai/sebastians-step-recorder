using System.Globalization;
using System.Text;

namespace StepRecorder.Export.Formats;

/// <summary>
/// Minimaler PDF-Erzeuger ohne Fremdbibliothek: A4-Seiten, Helvetica (WinAnsi), JPEG-Bilder,
/// Rechtecke und Linien. Ausreichend für Schritt-Anleitungen.
/// </summary>
public sealed class SimplePdf
{
    public const double PageW = 595.28, PageH = 841.89;
    public double Margin { get; set; } = 50;

    readonly List<StringBuilder> _pages = new();
    readonly List<(byte[] jpeg, int w, int h)> _images = new();
    readonly List<List<(double x, double y, double w, double h, string uri)>> _links = new();
    StringBuilder _cur = null!;
    public double Y { get; private set; }
    public int PageCount => _pages.Count;
    public void SetY(double y) => Y = y;
    public string Title { get; set; } = "";
    public string Footer { get; set; } = "";

    static readonly int[] HelvWidths =
    {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,
        278,278,584,584,584,556,1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,
        611,722,667,944,667,667,611,278,278,278,469,556,333,556,556,500,556,556,278,556,556,222,222,500,222,833,
        556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
    };

    public SimplePdf() => NewPage();

    public double ContentWidth => PageW - 2 * Margin;

    public void NewPage()
    {
        _cur = new StringBuilder();
        _pages.Add(_cur);
        _links.Add(new());
        Y = PageH - Margin;
    }

    public static double TextWidth(string s, double size, bool bold = false)
    {
        double w = 0;
        foreach (char c in s)
        {
            int cw = c >= 32 && c <= 126 ? HelvWidths[c - 32] : c switch
            {
                'Ä' => 667, 'Ö' => 778, 'Ü' => 722, 'ß' => 611, '€' => 556, '–' => 556, '—' => 1000, '…' => 1000,
                '„' or '“' or '”' => 333, '‘' or '’' => 222, '·' => 278, '•' => 350, _ => 556
            };
            w += cw;
        }
        return w * size / 1000.0 * (bold ? 1.06 : 1.0);
    }

    public void EnsureSpace(double h)
    {
        if (Y - h < Margin + 20) NewPage();
    }

    /// <summary>Schreibt umbrochenen Text ab der aktuellen Position.</summary>
    public void Paragraph(string text, double size = 10.5, bool bold = false, double indent = 0, double spacingAfter = 6, (double r, double g, double b)? color = null, double width = 0)
    {
        double lineH = size * 1.35;
        if (width <= 0) width = ContentWidth - indent;
        foreach (var rawLine in (text ?? "").Replace("\r", "").Split('\n'))
        {
            foreach (var line in Wrap(rawLine, width, size, bold))
            {
                EnsureSpace(lineH);
                Y -= lineH;
                DrawText(Margin + indent, Y + size * 0.25, line, size, bold, color);
            }
        }
        Y -= spacingAfter;
    }

    public static IEnumerable<string> Wrap(string text, double width, double size, bool bold)
    {
        if (string.IsNullOrEmpty(text)) { yield return ""; yield break; }
        var words = text.Split(' ');
        var line = new StringBuilder();
        foreach (var w0 in words)
        {
            var w = w0;
            string candidate = line.Length == 0 ? w : line + " " + w;
            if (TextWidth(candidate, size, bold) <= width) { line.Clear().Append(candidate); continue; }
            if (line.Length > 0) { yield return line.ToString(); line.Clear(); }
            // überlanges Wort hart trennen
            while (TextWidth(w, size, bold) > width && w.Length > 1)
            {
                int n = w.Length;
                while (n > 1 && TextWidth(w[..n], size, bold) > width) n--;
                yield return w[..n];
                w = w[n..];
            }
            line.Append(w);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    public void DrawText(double x, double y, string text, double size, bool bold = false, (double r, double g, double b)? color = null)
    {
        var c = color ?? (0.1, 0.1, 0.1);
        _cur.Append(F($"BT {c.r} {c.g} {c.b} rg /{(bold ? "F2" : "F1")} {size} Tf {x} {y} Td ")).Append(PdfString(text)).Append(" Tj ET\n");
    }

    public void FillRect(double x, double y, double w, double h, (double r, double g, double b) color)
        => _cur.Append(F($"{color.r} {color.g} {color.b} rg {x} {y} {w} {h} re f\n"));

    public void StrokeRect(double x, double y, double w, double h, (double r, double g, double b) color, double lw = 1)
        => _cur.Append(F($"{color.r} {color.g} {color.b} RG {lw} w {x} {y} {w} {h} re S\n"));

    public void Line(double x1, double y1, double x2, double y2, (double r, double g, double b) color, double lw = 0.5)
        => _cur.Append(F($"{color.r} {color.g} {color.b} RG {lw} w {x1} {y1} m {x2} {y2} l S\n"));

    public void Space(double h) => Y -= h;

    /// <summary>Fügt ein JPEG-Bild in voller Inhaltsbreite (oder kleiner) ein.</summary>
    /// <summary>Höhe eines umbrochenen Absatzes (ohne Abstand danach).</summary>
    public static double MeasureText(string text, double width, double size, bool bold = false)
        => (text ?? "").Replace("\r", "").Split('\n').Sum(l => Wrap(l, width, size, bold).Count()) * size * 1.35;

    /// <summary>Hinweiskasten: farbiger Balken links, getönte Fläche, fette Bezeichnung vor dem Text.</summary>
    public void Callout(string label, string text, (double r, double g, double b) bar, (double r, double g, double b) bg, double size = 10)
    {
        double pad = 7, w = ContentWidth - 2 * pad - 4;
        string full = label + " " + text;
        var lines = full.Replace("\r", "").Split('\n').SelectMany(l => Wrap(l, w, size, false)).ToList();
        double lineH = size * 1.35, h = lines.Count * lineH + 2 * pad;
        EnsureSpace(h + 4);
        FillRect(Margin, Y - h, ContentWidth, h, bg);
        FillRect(Margin, Y - h, 3, h, bar);
        double y = Y - pad;
        bool first = true;
        foreach (var line in lines)
        {
            y -= lineH;
            double x = Margin + 4 + pad;
            if (first && line.StartsWith(label))
            {
                DrawText(x, y + size * 0.25, label, size, true, bar);
                x += TextWidth(label + " ", size, true);
                DrawText(x, y + size * 0.25, line[label.Length..].TrimStart(), size);
            }
            else DrawText(x, y + size * 0.25, line, size);
            first = false;
        }
        Y -= h + 6;
    }

    public void Image(byte[] jpeg, int pxW, int pxH, double maxW = 0, double maxH = 520, double x = -1, bool border = true)
    {
        if (x < 0) x = Margin;
        if (maxW <= 0) maxW = ContentWidth;
        double scale = Math.Min(maxW / pxW, maxH / pxH);
        scale = Math.Min(scale, 0.75); // höchstens 96 dpi → 72 pt
        double w = pxW * scale, h = pxH * scale;
        EnsureSpace(h + 4);
        Y -= h;
        int idx = _images.Count;
        _images.Add((jpeg, pxW, pxH));
        _cur.Append(F($"q {w} 0 0 {h} {x} {Y} cm /Im{idx} Do Q\n"));
        if (border) StrokeRect(x, Y, w, h, (0.75, 0.75, 0.75), 0.5);
        Y -= 8;
    }

    static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    static string PdfString(string s)
    {
        var sb = new StringBuilder("(");
        var enc = Encoding.Latin1;
        foreach (char c0 in s)
        {
            // WinAnsiEncoding: typografische Zeichen liegen auf 0x80–0x9F
            char c = c0 switch { '„' => '\u0084', '“' => '\u0093', '”' => '\u0094', '‚' => '\u0082', '‘' => '\u0091', '’' => '\u0092', '–' => '\u0096', '—' => '\u0097', '…' => '\u0085', '•' => '\u0095', '€' => '\u0080', '→' => '>', _ => c0 };
            if (c == '(' || c == ')' || c == '\\') sb.Append('\\').Append(c);
            else if (c < 32) sb.Append(' ');
            else if (c > 255) sb.Append('?');
            else if (c > 126) sb.Append('\\').Append(Convert.ToString(c, 8).PadLeft(3, '0'));
            else sb.Append(c);
        }
        return sb.Append(')').ToString();
    }

    /// <summary>Seiten ohne Fußzeile (z. B. Deckblatt), 0-basiert.</summary>
    public HashSet<int> NoFooter { get; } = new();

    public byte[] ToBytes()
    {
        // Fußzeilen
        for (int i = 0; i < _pages.Count; i++)
        {
            if (NoFooter.Contains(i)) continue;
            var p = _pages[i];
            string pg = $"{i + 1} / {_pages.Count}";
            p.Append(F($"BT 0.5 0.5 0.5 rg /F1 8 Tf {Margin} 25 Td ")).Append(PdfString(Footer)).Append(" Tj ET\n");
            p.Append(F($"BT 0.5 0.5 0.5 rg /F1 8 Tf {PageW - Margin - TextWidth(pg, 8)} 25 Td ")).Append(PdfString(pg)).Append(" Tj ET\n");
        }

        var ms = new MemoryStream();
        var offsets = new List<long>();
        void W(string s) { var b = Encoding.Latin1.GetBytes(s); ms.Write(b); }
        void Obj(int n) { while (offsets.Count < n) offsets.Add(0); offsets[n - 1] = ms.Position; W($"{n} 0 obj\n"); }

        W("%PDF-1.4\n%âãÏÓ\n");
        // 1 Katalog, 2 Seitenbaum, 3 F1, 4 F2, 5 Info, dann Bilder, dann Seiten + Inhalte
        int firstImg = 6;
        int firstPage = firstImg + _images.Count;
        var pageObjs = Enumerable.Range(0, _pages.Count).Select(i => firstPage + i * 2).ToList();

        Obj(1); W("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Obj(2); W($"<< /Type /Pages /Count {_pages.Count} /Kids [{string.Join(" ", pageObjs.Select(p => $"{p} 0 R"))}] >>\nendobj\n");
        Obj(3); W("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        Obj(4); W("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");
        Obj(5); W($"<< /Title {PdfString(Title)} /Producer (StepRecorder) >>\nendobj\n");
        for (int i = 0; i < _images.Count; i++)
        {
            var (jpeg, w, h) = _images[i];
            Obj(firstImg + i);
            W($"<< /Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            ms.Write(jpeg); W("\nendstream\nendobj\n");
        }
        string xobjs = string.Join(" ", Enumerable.Range(0, _images.Count).Select(i => $"/Im{i} {firstImg + i} 0 R"));
        for (int i = 0; i < _pages.Count; i++)
        {
            int po = pageObjs[i];
            var content = Encoding.Latin1.GetBytes(_pages[i].ToString());
            Obj(po);
            W($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F($"{PageW}")} {F($"{PageH}")}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> /XObject << {xobjs} >> >> /Contents {po + 1} 0 R >>\nendobj\n");
            Obj(po + 1);
            W($"<< /Length {content.Length} >>\nstream\n"); ms.Write(content); W("\nendstream\nendobj\n");
        }
        long xref = ms.Position;
        W($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W($"{o:D10} 00000 n \n");
        W($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info 5 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }
}
