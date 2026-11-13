using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace StepRecorder.Export.Formats;

/// <summary>
/// Minimaler PDF-Erzeuger ohne Fremdbibliothek: Seiten in A4 oder Letter (hoch oder quer), JPEG-Bilder, Rechtecke und Linien, interne Verweise und
/// Lesezeichen. Text läuft in Helvetica (WinAnsi), solange die Zeichen dort vorkommen; alles andere (z. B. Kyrillisch,
/// Griechisch, Pfeile, Häkchen) in einer eingebetteten Teilmenge von Segoe UI bzw. Segoe UI Symbol (Type0, Identity-H,
/// mit ToUnicode, damit Kopieren und Suchen funktionieren). Nicht darstellbar bleiben Schriften mit Zeichenformung
/// (Chinesisch/Japanisch/Koreanisch, Arabisch, Hebräisch, Thai) – sie erscheinen als „?“, mit genau einer Warnung.
/// </summary>
public sealed partial class SimplePdf
{
    /// <summary>A4 hoch in Punkt.</summary>
    public const double A4W = 595.28, A4H = 841.89;
    /// <summary>Seitengröße in Punkt (A4, Letter, hoch oder quer; 09 §4.4).</summary>
    public double PageW { get; }
    public double PageH { get; }
    public double Margin { get; set; } = 50;

    readonly List<StringBuilder> _pages = new();
    readonly List<(byte[] jpeg, int w, int h)> _images = new();
    readonly List<List<(double x, double y, double w, double h, string dest)>> _links = new();
    readonly Dictionary<string, (int page, double top)> _dests = new();
    readonly List<(string title, int level, string dest)> _outline = new();
    readonly Dictionary<int, SortedDictionary<int, int>> _used = new();   // Schrift → Glyph → Zeichen
    bool _unsupported;
    StringBuilder _cur = null!;
    public double Y { get; private set; }
    public int PageCount => _pages.Count;
    public void SetY(double y) => Y = y;
    public string Title { get; set; } = "";
    public string Footer { get; set; } = "";
    /// <summary>Sprache des Dokuments (z. B. de-DE) – für Vorlesefunktionen.</summary>
    public string Language { get; set; } = "de-DE";
    /// <summary>Hinweise beim Erzeugen (z. B. nicht darstellbare Zeichen).</summary>
    public List<string> Warnings { get; } = new();

    static readonly int[] HelvWidths =
    {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,
        278,278,584,584,584,556,1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,
        611,722,667,944,667,667,611,278,278,278,469,556,333,556,556,500,556,556,278,556,556,222,222,500,222,833,
        556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
    };

    // ---------------- Schriften ----------------

    static readonly Lazy<TrueTypeFont?> FbRegular = new(() => TrueTypeFont.TryLoadSystem("segoeui.ttf", "SegoeUI") ?? TrueTypeFont.TryLoadSystem("arial.ttf", "ArialMT") ?? TrueTypeFont.TryLoadSystem("tahoma.ttf", "Tahoma"));
    static readonly Lazy<TrueTypeFont?> FbBold = new(() => TrueTypeFont.TryLoadSystem("segoeuib.ttf", "SegoeUI-Bold") ?? TrueTypeFont.TryLoadSystem("arialbd.ttf", "Arial-BoldMT") ?? TrueTypeFont.TryLoadSystem("tahomabd.ttf", "Tahoma-Bold"));
    static readonly Lazy<TrueTypeFont?> FbSymbol = new(() => TrueTypeFont.TryLoadSystem("seguisym.ttf", "SegoeUISymbol"));
    static TrueTypeFont? FontOf(int index) => index switch { 1 => FbRegular.Value, 2 => FbBold.Value, 3 => FbSymbol.Value, _ => null };

    static readonly Dictionary<int, int> WinAnsiExtra = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87, ['ˆ'] = 0x88, ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C,
        ['Ž'] = 0x8E, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98, ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B,
        ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F
    };

    static int WinAnsiCode(int cp) => cp is (>= 32 and <= 126) or (>= 160 and <= 255) ? cp : WinAnsiExtra.TryGetValue(cp, out var b) ? b : -1;

    /// <summary>Schriften mit Zeichenformung oder Bidi bzw. ohne Glyphen in Segoe UI (CJK, Arabisch, Hebräisch, Thai, Emoji u. a.).</summary>
    static bool Unsupported(int cp) => cp is (>= 0x0590 and <= 0x08FF) or (>= 0x0E00 and <= 0x0E7F) or (>= 0x2E80 and <= 0x9FFF) or (>= 0xAC00 and <= 0xD7AF)
                                         or (>= 0xF900 and <= 0xFAFF) or (>= 0xFB1D and <= 0xFDFF) or (>= 0xFE70 and <= 0xFEFF) or >= 0x1F000;

    /// <summary>Schrift (0 Helvetica, 1 Rückfall, 2 Rückfall fett, 3 Symbole, -1 nicht darstellbar) und Code (WinAnsi-Byte bzw. Glyph).</summary>
    static (int font, int code) Classify(int cp, bool bold)
    {
        if (cp < 32) return (0, ' ');
        int w = WinAnsiCode(cp);
        if (w >= 0) return (0, w);
        if (Unsupported(cp)) return (-1, '?');
        if (bold && FbBold.Value is { } fb && fb.GlyphFor(cp) is int gb and > 0) return (2, gb);
        if (FbRegular.Value is { } fr && fr.GlyphFor(cp) is int g and > 0) return (1, g);
        if (FbSymbol.Value is { } fs && fs.GlyphFor(cp) is int gs and > 0) return (3, gs);
        return (-1, '?');
    }

    static double HelvWidth(int cp) => cp >= 32 && cp <= 126 ? HelvWidths[cp - 32] : cp switch
    {
        'Ä' => 667, 'Ö' => 778, 'Ü' => 722, 'ß' => 611, 'é' or 'è' or 'à' or 'á' => 556, '€' => 556, '–' => 556, '—' => 1000, '…' => 1000,
        '„' or '“' or '”' => 333, '‘' or '’' or '‚' => 222, '·' => 278, '•' => 350, '°' => 400, '§' => 556, '©' or '®' => 737, '×' => 584, _ => 556
    };

    public SimplePdf() : this(A4W, A4H) { }

    public SimplePdf(double pageW, double pageH) { PageW = pageW; PageH = pageH; NewPage(); }

    public double ContentWidth => PageW - 2 * Margin;
    /// <summary>Nutzbare Höhe einer Seite (ohne Ränder und Fußzeile).</summary>
    public double ContentHeight => PageH - 2 * Margin - 20;

    public void NewPage()
    {
        _cur = new StringBuilder();
        _pages.Add(_cur);
        _links.Add(new());
        _uris.Add(new());
        Y = PageH - Margin;
    }

    public static double TextWidth(string s, double size, bool bold = false)
    {
        double w = 0;
        foreach (var r in (s ?? "").EnumerateRunes())
        {
            var (font, code) = Classify(r.Value, bold);
            w += font switch { 0 => HelvWidth(r.Value) * (bold ? 1.06 : 1.0), -1 => 556, _ => FontOf(font)!.Width1000(code) };
        }
        return w * size / 1000.0;
    }

    public void EnsureSpace(double h)
    {
        if (Y - h < Margin + 20) NewPage();
    }

    /// <summary>Sprungziel an der aktuellen Stelle (für Inhaltsverzeichnis und Lesezeichen).</summary>
    /// <summary>Gesetzte Sprungziele: Schlüssel → Seite (0-basiert) und Oberkante in Punkt (PDF-Koordinaten, von unten).</summary>
    public IReadOnlyDictionary<string, (int page, double top)> Destinations => _dests;

    public void Dest(string key) => _dests[key] = (_pages.Count - 1, Math.Min(PageH, Y + 24));

    /// <summary>Klickfläche auf der aktuellen Seite, die zu <paramref name="key"/> springt.</summary>
    public void LinkTo(double x, double y, double w, double h, string key) => _links[^1].Add((x, y, w, h, key));

    /// <summary>Lesezeichen (Ebene 0 = Kapitel, 1 = Schritt) zum Sprungziel <paramref name="key"/>.</summary>
    public void Bookmark(string title, int level, string key) => _outline.Add((title, level, key));

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
            // überlanges Wort hart trennen (nie mitten in einem Ersatzzeichenpaar)
            while (TextWidth(w, size, bold) > width && w.Length > 1)
            {
                int n = w.Length;
                while (n > 1 && TextWidth(w[..n], size, bold) > width) n--;
                if (n < w.Length && char.IsHighSurrogate(w[n - 1]) && n > 1) n--;
                yield return w[..n];
                w = w[n..];
            }
            line.Append(w);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    public void DrawText(double x, double y, string text, double size, bool bold = false, (double r, double g, double b)? color = null)
        => DrawTextTo(_cur, x, y, text, size, bold, color);

    /// <summary>Text in Läufen je Schrift: ein BT-Block, mehrere Tf/Tj (Tj rückt selbst weiter).</summary>
    void DrawTextTo(StringBuilder sb, double x, double y, string text, double size, bool bold, (double r, double g, double b)? color)
        => DrawTextTo(sb, x, y, text, size, bold, color, false, false);

    void DrawTextTo(StringBuilder sb, double x, double y, string text, double size, bool bold, (double r, double g, double b)? color, bool italic, bool mono)
    {
        var c = color ?? (0.1, 0.1, 0.1);
        sb.Append(F($"BT {c.r} {c.g} {c.b} rg {x} {y} Td "));
        int curFont = int.MinValue;
        var run = new StringBuilder();
        void Flush()
        {
            if (run.Length == 0) return;
            string res = curFont <= 0 ? StdFont(bold, italic, mono) : "U" + curFont;
            sb.Append(F($"/{res} {size} Tf ")).Append(curFont <= 0 ? "(" + run + ")" : "<" + run + ">").Append(" Tj ");
            run.Clear();
        }
        foreach (var r in (text ?? "").EnumerateRunes())
        {
            var (font, code) = Classify(r.Value, bold);
            if (font == -1) { _unsupported = true; font = 0; code = '?'; }
            int key = font <= 0 ? 0 : font;
            if (key != curFont) { Flush(); curFont = key; }
            if (key == 0)
            {
                if (code is '(' or ')' or '\\') run.Append('\\').Append((char)code);
                else if (code > 126) run.Append('\\').Append(Convert.ToString(code, 8).PadLeft(3, '0'));
                else run.Append((char)code);
            }
            else
            {
                if (!_used.TryGetValue(key, out var map)) _used[key] = map = new SortedDictionary<int, int>();
                map.TryAdd(code, r.Value);
                run.Append(code.ToString("X4"));
            }
        }
        Flush();
        sb.Append("ET\n");
    }

    public void FillRect(double x, double y, double w, double h, (double r, double g, double b) color)
        => _cur.Append(F($"{color.r} {color.g} {color.b} rg {x} {y} {w} {h} re f\n"));

    public void StrokeRect(double x, double y, double w, double h, (double r, double g, double b) color, double lw = 1)
        => _cur.Append(F($"{color.r} {color.g} {color.b} RG {lw} w {x} {y} {w} {h} re S\n"));

    public void Line(double x1, double y1, double x2, double y2, (double r, double g, double b) color, double lw = 0.5)
        => _cur.Append(F($"{color.r} {color.g} {color.b} RG {lw} w {x1} {y1} m {x2} {y2} l S\n"));

    public void Space(double h) => Y -= h;

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

    /// <summary>Fügt ein JPEG-Bild in voller Inhaltsbreite (oder kleiner) ein.</summary>
    public void Image(byte[] jpeg, int pxW, int pxH, double maxW = 0, double maxH = 520, double x = -1, bool border = true)
    {
        if (x < 0) x = Margin;
        if (maxW <= 0) maxW = ContentWidth;
        maxH = Math.Min(maxH, ContentHeight - 4);   // ein Bild wird nie geteilt und passt immer auf eine Seite
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

    /// <summary>Textzeichenkette für Metadaten und Lesezeichen: ASCII literal, sonst UTF-16BE mit BOM.</summary>
    public static string PdfTextString(string s)
    {
        s ??= "";
        if (s.All(c => c >= 32 && c <= 126))
            return "(" + s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ")";
        var b = Encoding.BigEndianUnicode.GetBytes(s);
        return "<FEFF" + Convert.ToHexString(b) + ">";
    }

    static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(data);
        return ms.ToArray();
    }

    /// <summary>Seiten ohne Fußzeile (z. B. Deckblatt), 0-basiert.</summary>
    public HashSet<int> NoFooter { get; } = new();

    public byte[] ToBytes()
    {
        // Fußzeilen (vor den Schriften, damit ihre Zeichen in die Teilmengen kommen)
        for (int i = 0; i < _pages.Count; i++)
        {
            if (NoFooter.Contains(i)) continue;
            string pg = $"{i + 1} / {_pages.Count}";
            DrawTextTo(_pages[i], Margin, 25, Footer, 8, false, (0.5, 0.5, 0.5));
            DrawTextTo(_pages[i], PageW - Margin - TextWidth(pg, 8), 25, pg, 8, false, (0.5, 0.5, 0.5));
        }
        if (_unsupported)
            Warnings.Add(Language.StartsWith("de")
                ? "Einige Zeichen (z. B. Chinesisch, Arabisch, Hebräisch, Thai oder Emoji) kann das PDF nicht darstellen; sie erscheinen als „?“. Für solche Texte bitte HTML oder Word verwenden."
                : "Some characters (e.g. Chinese, Arabic, Hebrew, Thai or emoji) cannot be shown in the PDF; they appear as \"?\". Please use HTML or Word for such texts.");

        // Objektnummern vergeben
        int next = 0;
        int New() => ++next;
        int catalog = New(), pagesObj = New(), info = New(), f1 = New(), f2 = New();
        var fonts = _used.Keys.OrderBy(k => k).ToDictionary(k => k, _ => (type0: New(), cid: New(), desc: New(), file: New(), tu: New()));
        var imgObjs = _images.Select(_ => New()).ToList();
        var pageObjs = _pages.Select(_ => (page: New(), content: New())).ToList();
        var annots = _links.Select(l => l.Where(x => _dests.ContainsKey(x.dest)).Select(x => (x, obj: New())).ToList()).ToList();
        int outlineRoot = _outline.Count > 0 ? New() : 0;
        var outlineObjs = _outline.Where(o => _dests.ContainsKey(o.dest)).Select(o => (o.title, o.level, o.dest, obj: New())).ToList();
        // Neu (13, Paket 21): weitere Standardschriften und Weblinks nur bei Gebrauch, nach allen bisherigen Objekten nummeriert
        var stdObjs = StdFonts.Where(f => _stdUsed.Contains(f.res)).Select(f => (f.res, f.name, obj: New())).ToList();
        var uriObjs = _uris.Select(l => l.Select(x => (x, obj: New())).ToList()).ToList();

        var ms = new MemoryStream();
        var offsets = new Dictionary<int, long>();
        void W(string s) { var b = Encoding.Latin1.GetBytes(s); ms.Write(b); }
        void Obj(int n, string body) { offsets[n] = ms.Position; W($"{n} 0 obj\n{body}\nendobj\n"); }
        void Stream(int n, string dict, byte[] data) { offsets[n] = ms.Position; W($"{n} 0 obj\n<< {dict} /Length {data.Length} >>\nstream\n"); ms.Write(data); W("\nendstream\nendobj\n"); }
        string DestArr(string key) { var (p, top) = _dests[key]; return F($"[{pageObjs[p].page} 0 R /XYZ 0 {Math.Round(top, 2)} null]"); }

        W("%PDF-1.4\n%âãÏÓ\n");

        // Lesezeichen als Baum: Ebene 1 hängt am letzten Kapitel davor
        var tops = new List<int>();
        var children = new Dictionary<int, List<int>>();
        var parent = new Dictionary<int, int>();
        int lastTop = -1;
        for (int i = 0; i < outlineObjs.Count; i++)
        {
            if (outlineObjs[i].level > 0 && lastTop >= 0 && outlineObjs[lastTop].level == 0) { children[lastTop].Add(i); parent[i] = lastTop; }
            else { tops.Add(i); children[i] = new(); lastTop = i; }
        }
        string catalogExtra = outlineObjs.Count > 0 ? $" /Outlines {outlineRoot} 0 R /PageMode /UseOutlines" : "";
        Obj(catalog, $"<< /Type /Catalog /Pages {pagesObj} 0 R /Lang {PdfTextString(Language)}{catalogExtra} >>");
        Obj(pagesObj, $"<< /Type /Pages /Count {_pages.Count} /Kids [{string.Join(" ", pageObjs.Select(p => $"{p.page} 0 R"))}] >>");
        Obj(info, $"<< /Title {PdfTextString(Title)} /Producer (StepRecorder) >>");
        Obj(f1, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Obj(f2, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        foreach (var (key, o) in fonts)
        {
            var font = FontOf(key)!;
            var used = _used[key];
            var raw = font.Subset(used.Keys);
            var hash = System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes(string.Join(",", used.Keys)));
            string tag = new string(hash.Take(6).Select(b => (char)('A' + b % 26)).ToArray());
            string name = tag + "+" + font.PostScriptName;
            double k = 1000.0 / font.UnitsPerEm;
            Obj(o.type0, $"<< /Type /Font /Subtype /Type0 /BaseFont /{name} /Encoding /Identity-H /DescendantFonts [{o.cid} 0 R] /ToUnicode {o.tu} 0 R >>");
            string widths = string.Join(" ", used.Keys.Select(g => F($"{g} [{Math.Round(font.Width1000(g))}]")));
            Obj(o.cid, $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{name} /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {o.desc} 0 R /DW 1000 /W [{widths}] /CIDToGIDMap /Identity >>");
            Obj(o.desc, F($"<< /Type /FontDescriptor /FontName /{name} /Flags 32 /FontBBox [{Math.Round(font.XMin * k)} {Math.Round(font.YMin * k)} {Math.Round(font.XMax * k)} {Math.Round(font.YMax * k)}] /ItalicAngle 0 /Ascent {Math.Round(font.Ascent * k)} /Descent {Math.Round(font.Descent * k)} /CapHeight {Math.Round(font.CapHeight * k)} /StemV 80 /FontFile2 {o.file} 0 R >>"));
            var comp = Deflate(raw);
            Stream(o.file, $"/Filter /FlateDecode /Length1 {raw.Length}", comp);
            var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
            foreach (var chunk in used.Chunk(100))
            {
                cmap.Append(chunk.Length).Append(" beginbfchar\n");
                foreach (var (g, cp) in chunk) cmap.Append('<').Append(g.ToString("X4")).Append("> <").Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(char.ConvertFromUtf32(cp)))).Append(">\n");
                cmap.Append("endbfchar\n");
            }
            cmap.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
            Stream(o.tu, "", Encoding.ASCII.GetBytes(cmap.ToString()));
        }

        for (int i = 0; i < _images.Count; i++)
        {
            var (jpeg, w, h) = _images[i];
            Stream(imgObjs[i], $"/Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", jpeg);
        }
        string xobjs = string.Join(" ", imgObjs.Select((o, i) => $"/Im{i} {o} 0 R"));
        string fontRes = $"/F1 {f1} 0 R /F2 {f2} 0 R" + string.Concat(fonts.Select(f => $" /U{f.Key} {f.Value.type0} 0 R")) + string.Concat(stdObjs.Select(f => $" /{f.res} {f.obj} 0 R"));
        for (int i = 0; i < _pages.Count; i++)
        {
            var (po, co) = pageObjs[i];
            var annIds = annots[i].Select(a => a.obj).Concat(uriObjs[i].Select(a => a.obj)).ToList();
            string ann = annIds.Count > 0 ? $" /Annots [{string.Join(" ", annIds.Select(a => $"{a} 0 R"))}]" : "";
            Obj(po, $"<< /Type /Page /Parent {pagesObj} 0 R /MediaBox [0 0 {F($"{PageW}")} {F($"{PageH}")}] /Resources << /Font << {fontRes} >> /XObject << {xobjs} >> >> /Contents {co} 0 R{ann} >>");
            Stream(co, "", Encoding.Latin1.GetBytes(_pages[i].ToString()));
            foreach (var (l, obj) in annots[i])
                Obj(obj, F($"<< /Type /Annot /Subtype /Link /Rect [{Math.Round(l.x, 2)} {Math.Round(l.y, 2)} {Math.Round(l.x + l.w, 2)} {Math.Round(l.y + l.h, 2)}] /Border [0 0 0] /Dest {DestArr(l.dest)} >>"));
            foreach (var (u, obj) in uriObjs[i])
                Obj(obj, F($"<< /Type /Annot /Subtype /Link /Rect [{Math.Round(u.x, 2)} {Math.Round(u.y, 2)} {Math.Round(u.x + u.w, 2)} {Math.Round(u.y + u.h, 2)}] /Border [0 0 0] /A << /S /URI /URI {PdfTextString(u.url)} >> >>"));
        }

        if (outlineObjs.Count > 0)
        {
            Obj(outlineRoot, $"<< /Type /Outlines /First {outlineObjs[tops[0]].obj} 0 R /Last {outlineObjs[tops[^1]].obj} 0 R /Count {tops.Count} >>");
            void Item(int i, List<int> siblings, int parentObj)
            {
                var it = outlineObjs[i];
                int pos = siblings.IndexOf(i);
                var sb = new StringBuilder($"<< /Title {PdfTextString(it.title)} /Parent {parentObj} 0 R");
                if (pos > 0) sb.Append($" /Prev {outlineObjs[siblings[pos - 1]].obj} 0 R");
                if (pos < siblings.Count - 1) sb.Append($" /Next {outlineObjs[siblings[pos + 1]].obj} 0 R");
                if (children.TryGetValue(i, out var ch) && ch.Count > 0)
                    sb.Append($" /First {outlineObjs[ch[0]].obj} 0 R /Last {outlineObjs[ch[^1]].obj} 0 R /Count -{ch.Count}");
                sb.Append($" /Dest {DestArr(it.dest)} >>");
                Obj(it.obj, sb.ToString());
                if (ch != null) foreach (var c in ch) Item(c, ch, it.obj);
            }
            foreach (var t in tops) Item(t, tops, outlineRoot);
        }

        foreach (var (res, name, obj) in stdObjs) Obj(obj, $"<< /Type /Font /Subtype /Type1 /BaseFont /{name} /Encoding /WinAnsiEncoding >>");
        long xref = ms.Position;
        W($"xref\n0 {next + 1}\n0000000000 65535 f \n");
        for (int n = 1; n <= next; n++) W($"{(offsets.TryGetValue(n, out var off) ? off : 0):D10} 00000 n \n");
        W($"trailer\n<< /Size {next + 1} /Root {catalog} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }
}
