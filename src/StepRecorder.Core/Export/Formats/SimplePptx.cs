using System.IO.Compression;
using System.Text;

namespace StepRecorder.Export.Formats;

/// <summary>
/// Minimaler PowerPoint-Schreiber (PresentationML, 16:9) ohne Fremdbibliothek: Folien mit Titel-Platzhalter, Textfeldern,
/// Marken und Bildern, Vortragsnotizen. Vollständiges Theme (je drei Füll-, Linien-, Effekt- und Hintergrundstile), damit
/// PowerPoint die Datei ohne Reparatur öffnet. Feste Zeitstempel: zwei Exporte desselben Inhalts sind bytegleich.
/// </summary>
public sealed class SimplePptx
{
    public const long W = 12192000, H = 6858000;   // 13,333 × 7,5 Zoll
    public const long Emu = 12700;                  // EMU je Punkt
    public string Title { get; set; } = "";
    public string Language { get; set; } = "de-DE";
    /// <summary>Akzentfarbe als RRGGBB.</summary>
    public string Accent { get; set; } = "E85D04";
    public string Author { get; set; } = "";
    public DateTime Created { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public const string Ink = "1C1B19", Muted = "6B6962", Line = "DEDCD5", Soft = "F6F5F1";

    readonly List<Slide> _slides = new();
    public IReadOnlyList<Slide> Slides => _slides;

    public enum Layout { Title = 1, TitleOnly = 2, Blank = 3 }

    public Slide AddSlide(Layout layout = Layout.TitleOnly) { var s = new Slide(this, layout); _slides.Add(s); return s; }

    public sealed class Slide
    {
        readonly SimplePptx _p;
        internal readonly Layout Layout;
        internal readonly StringBuilder Shapes = new();
        internal readonly List<byte[]> Media = new();
        int _id = 1;
        /// <summary>Vortragsnotizen (Absätze durch \n getrennt); leer = keine Notizfolie.</summary>
        public string Notes { get; set; } = "";
        internal Slide(SimplePptx p, Layout l) { _p = p; Layout = l; }
        int Next() => ++_id;
        static string Xf(long x, long y, long w, long h) => $"<a:xfrm><a:off x=\"{x}\" y=\"{y}\"/><a:ext cx=\"{Math.Max(1, w)}\" cy=\"{Math.Max(1, h)}\"/></a:xfrm>";
        string Run(string text, double size, bool bold, string color, string? font = null)
            => $"<a:r><a:rPr lang=\"{_p.Language}\" sz=\"{(int)Math.Round(size * 100)}\"{(bold ? " b=\"1\"" : "")} dirty=\"0\"><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill>{(font != null ? $"<a:latin typeface=\"{X(font)}\"/>" : "")}</a:rPr><a:t>{X(text)}</a:t></a:r>";
        string Paras(string text, double size, bool bold, string color, string algn = "l", double spaceAfter = 0)
        {
            var sb = new StringBuilder();
            foreach (var line in text.Replace("\r", "").Split('\n'))
                sb.Append($"<a:p><a:pPr algn=\"{algn}\">{(spaceAfter > 0 ? $"<a:spcAft><a:spcPts val=\"{(int)(spaceAfter * 100)}\"/></a:spcAft>" : "")}</a:pPr>")
                  .Append(line.Length > 0 ? Run(line, size, bold, color) : $"<a:endParaRPr lang=\"{_p.Language}\" sz=\"{(int)Math.Round(size * 100)}\" dirty=\"0\"/>").Append("</a:p>");
            return sb.ToString();
        }

        /// <summary>Titel als echter Platzhalter (Gliederungsansicht, Barrierefreiheit), Lage und Größe frei.</summary>
        public void TitleText(string text, long x, long y, long w, long h, double size, string color = Ink, string anchor = "b", bool center = false)
        {
            int id = Next();
            string ph = Layout == Layout.Title ? "<p:ph type=\"ctrTitle\"/>" : "<p:ph type=\"title\"/>";
            Shapes.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"Titel {id}\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr>{ph}</p:nvPr></p:nvSpPr>")
                  .Append($"<p:spPr>{Xf(x, y, w, h)}</p:spPr><p:txBody><a:bodyPr wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"{anchor}\"><a:normAutofit/></a:bodyPr><a:lstStyle/>")
                  .Append(Paras(text, size, true, color, center ? "ctr" : "l")).Append("</p:txBody></p:sp>");
        }

        /// <summary>Textfeld (Absätze durch \n).</summary>
        public void Text(string text, long x, long y, long w, long h, double size, string color = Ink, bool bold = false, string anchor = "t", string algn = "l")
        {
            int id = Next();
            Shapes.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"Text {id}\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr>")
                  .Append($"<p:spPr>{Xf(x, y, w, h)}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/></p:spPr>")
                  .Append($"<p:txBody><a:bodyPr wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"{anchor}\"><a:normAutofit/></a:bodyPr><a:lstStyle/>")
                  .Append(Paras(text, size, bold, color, algn, size * 0.4)).Append("</p:txBody></p:sp>");
        }

        /// <summary>Textfeld mit Läufen in einer Zeile (z. B. „Hinweis:“ fett, Rest normal), auf farbiger Fläche.</summary>
        public void Rich(long x, long y, long w, long h, string? fill, string? bar, params (string text, bool bold, string color, double size)[] runs)
        {
            int id = Next();
            string geom = "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>" + (fill != null ? $"<a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill>" : "<a:noFill/>");
            string ln = bar != null ? "<a:ln><a:noFill/></a:ln>" : "";
            Shapes.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"Text {id}\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr><p:spPr>{Xf(x, y, w, h)}{geom}{ln}</p:spPr>")
                  .Append($"<p:txBody><a:bodyPr wrap=\"square\" lIns=\"{(bar != null ? 137160 : 0)}\" tIns=\"{(fill != null ? 64008 : 0)}\" rIns=\"{(fill != null ? 91440 : 0)}\" bIns=\"{(fill != null ? 64008 : 0)}\" anchor=\"ctr\"><a:normAutofit/></a:bodyPr><a:lstStyle/><a:p>")
                  .Append(string.Concat(runs.Select(r => Run(r.text, r.size, r.bold, r.color)))).Append("</a:p></p:txBody></p:sp>");
            if (bar != null) Rect(x, y, 54864, h, bar);
        }

        /// <summary>Gefüllte Fläche (Balken, Akzentstreifen).</summary>
        public void Rect(long x, long y, long w, long h, string fill, bool round = false)
        {
            int id = Next();
            Shapes.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"Fläche {id}\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr>{Xf(x, y, w, h)}<a:prstGeom prst=\"{(round ? "roundRect" : "rect")}\"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill><a:ln><a:noFill/></a:ln></p:spPr>")
                  .Append($"<p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:endParaRPr lang=\"{_p.Language}\" dirty=\"0\"/></a:p></p:txBody></p:sp>");
        }

        /// <summary>Nummernmarke (abgerundetes Rechteck in Akzentfarbe, weiße Zahl).</summary>
        public void Badge(string number, long x, long y, long h, double size)
        {
            int id = Next();
            long w = Math.Max(h, (long)(number.Length * size * 0.62 * Emu + h * 0.6));
            Shapes.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"Nummer {id}\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr>{Xf(x, y, w, h)}<a:prstGeom prst=\"roundRect\"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val=\"{_p.Accent}\"/></a:solidFill><a:ln><a:noFill/></a:ln></p:spPr>")
                  .Append($"<p:txBody><a:bodyPr wrap=\"none\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"ctr\"/><a:lstStyle/><a:p><a:pPr algn=\"ctr\"/>{Run(number, size, true, "FFFFFF")}</a:p></p:txBody></p:sp>");
        }

        /// <summary>Bild in einen Rahmen eingepasst (Seitenverhältnis bleibt, mittig). Gibt die tatsächliche Lage zurück.</summary>
        public (long x, long y, long w, long h) Picture(byte[] png, int pxW, int pxH, long boxX, long boxY, long boxW, long boxH, string alt, bool border = true)
        {
            var (w, h) = Fit(pxW, pxH, boxW, boxH);
            long x = boxX + (boxW - w) / 2, y = boxY + (boxH - h) / 2;
            Media.Add(png);
            int id = Next();
            string rid = "rId" + (2 + Media.Count);
            Shapes.Append($"<p:pic><p:nvPicPr><p:cNvPr id=\"{id}\" name=\"Bild {id}\" descr=\"{X(alt)}\"/><p:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></p:cNvPicPr><p:nvPr/></p:nvPicPr>")
                  .Append($"<p:blipFill><a:blip r:embed=\"{rid}\"/><a:stretch><a:fillRect/></a:stretch></p:blipFill>")
                  .Append($"<p:spPr>{Xf(x, y, w, h)}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>{(border ? $"<a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"{Line}\"/></a:solidFill></a:ln>" : "")}</p:spPr></p:pic>");
            return (x, y, w, h);
        }
    }

    /// <summary>Größe eines Bildes von <paramref name="pxW"/>×<paramref name="pxH"/> in einem Rahmen (EMU), Seitenverhältnis bleibt.</summary>
    public static (long w, long h) Fit(int pxW, int pxH, long boxW, long boxH)
    {
        if (pxW <= 0 || pxH <= 0) return (boxW, boxH);
        double s = Math.Min((double)boxW / pxW, (double)boxH / pxH);
        return ((long)(pxW * s), (long)(pxH * s));
    }

    /// <summary>Schriftgröße, bei der <paramref name="text"/> grob in den Rahmen passt (Schätzung: 0,52 em je Zeichen, 1,25 Zeilenhöhe).</summary>
    public static double FitFontSize(string text, long boxW, long boxH, double max, double min)
    {
        for (double s = max; s > min; s -= 1)
        {
            double charsPerLine = Math.Max(1, boxW / (s * 0.52 * Emu));
            int lines = text.Replace("\r", "").Split('\n').Sum(l => Math.Max(1, (int)Math.Ceiling(l.Length / charsPerLine)));
            if (lines * s * 1.25 * Emu <= boxH) return s;
        }
        return min;
    }

    /// <summary>XML-Text: Sonderzeichen maskiert, in XML unzulässige Steuerzeichen entfernt.</summary>
    public static string X(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c < 0x20 && c is not ('\t' or '\n' or '\r')) continue;
            if (c is '￾' or '￿') continue;
            sb.Append(c switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", '"' => "&quot;", '\'' => "&apos;", _ => c.ToString() });
        }
        return sb.ToString();
    }

    const string NsA = "http://schemas.openxmlformats.org/drawingml/2006/main", NsR = "http://schemas.openxmlformats.org/officeDocument/2006/relationships", NsP = "http://schemas.openxmlformats.org/presentationml/2006/main";
    const string Ns = $"xmlns:a=\"{NsA}\" xmlns:r=\"{NsR}\" xmlns:p=\"{NsP}\"";
    const string Hdr = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";
    const string RelT = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    const string Grp = "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>";
    const string ClrMap = "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>";

    static string Rels(params (string id, string type, string target)[] rels)
        => Hdr + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + string.Concat(rels.Select(r => $"<Relationship Id=\"{r.id}\" Type=\"{(r.type.StartsWith("http") ? r.type : RelT + r.type)}\" Target=\"{r.target}\"/>")) + "</Relationships>";

    string PlaceholderSp(int id, string name, string ph, long x, long y, long w, long h, double size, bool bold, string anchor)
        => $"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"{name}\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr>{ph}</p:nvPr></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"{x}\" y=\"{y}\"/><a:ext cx=\"{w}\" cy=\"{h}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr>" +
           $"<p:txBody><a:bodyPr vert=\"horz\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" rtlCol=\"0\" anchor=\"{anchor}\"><a:normAutofit/></a:bodyPr><a:lstStyle/><a:p><a:r><a:rPr lang=\"{Language}\" sz=\"{(int)(size * 100)}\"{(bold ? " b=\"1\"" : "")} dirty=\"0\"/><a:t>{X(name)}</a:t></a:r></a:p></p:txBody></p:sp>";

    string Theme(string name) => Hdr + $"<a:theme xmlns:a=\"{NsA}\" name=\"{name}\"><a:themeElements>" +
        $"<a:clrScheme name=\"{name}\"><a:dk1><a:srgbClr val=\"{Ink}\"/></a:dk1><a:lt1><a:srgbClr val=\"FFFFFF\"/></a:lt1><a:dk2><a:srgbClr val=\"3F3D38\"/></a:dk2><a:lt2><a:srgbClr val=\"{Soft}\"/></a:lt2>" +
        $"<a:accent1><a:srgbClr val=\"{Accent}\"/></a:accent1><a:accent2><a:srgbClr val=\"2F5C9C\"/></a:accent2><a:accent3><a:srgbClr val=\"2F7D4F\"/></a:accent3><a:accent4><a:srgbClr val=\"B26A00\"/></a:accent4><a:accent5><a:srgbClr val=\"B4233C\"/></a:accent5><a:accent6><a:srgbClr val=\"6B6962\"/></a:accent6>" +
        "<a:hlink><a:srgbClr val=\"2F5C9C\"/></a:hlink><a:folHlink><a:srgbClr val=\"6B4C9A\"/></a:folHlink></a:clrScheme>" +
        $"<a:fontScheme name=\"{name}\"><a:majorFont><a:latin typeface=\"Segoe UI Semibold\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont><a:minorFont><a:latin typeface=\"Segoe UI\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>" +
        $"<a:fmtScheme name=\"{name}\"><a:fillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"><a:tint val=\"50000\"/></a:schemeClr></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"><a:shade val=\"80000\"/></a:schemeClr></a:solidFill></a:fillStyleLst>" +
        "<a:lnStyleLst>" + string.Concat(new[] { 6350, 12700, 19050 }.Select(w => $"<a:ln w=\"{w}\" cap=\"flat\" cmpd=\"sng\" algn=\"ctr\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:prstDash val=\"solid\"/><a:miter lim=\"800000\"/></a:ln>")) + "</a:lnStyleLst>" +
        "<a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>" +
        "<a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"><a:tint val=\"95000\"/></a:schemeClr></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"><a:shade val=\"90000\"/></a:schemeClr></a:solidFill></a:bgFillStyleLst>" +
        "</a:fmtScheme></a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>";

    string Master()
    {
        string tx(string kind, int sz, string font) => $"<a:lvl1pPr marL=\"0\" indent=\"0\" algn=\"l\" defTabSz=\"914400\" rtl=\"0\" eaLnBrk=\"1\" latinLnBrk=\"0\" hangingPunct=\"1\">{(kind == "body" ? "<a:buNone/>" : "")}<a:defRPr sz=\"{sz}\" kern=\"1200\"><a:solidFill><a:schemeClr val=\"tx1\"/></a:solidFill><a:latin typeface=\"{font}\"/><a:ea typeface=\"+mn-ea\"/><a:cs typeface=\"+mn-cs\"/></a:defRPr></a:lvl1pPr>";
        return Hdr + $"<p:sldMaster {Ns}><p:cSld><p:bg><p:bgRef idx=\"1001\"><a:schemeClr val=\"bg1\"/></p:bgRef></p:bg><p:spTree>{Grp}" +
               PlaceholderSp(2, "Titelplatzhalter 1", "<p:ph type=\"title\"/>", 685800, 457200, 10820400, 914400, 28, true, "b") +
               PlaceholderSp(3, "Textplatzhalter 2", "<p:ph type=\"body\" idx=\"1\"/>", 685800, 1600200, 10820400, 4572000, 18, false, "t") +
               $"</p:spTree></p:cSld>{ClrMap}<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rId1\"/><p:sldLayoutId id=\"2147483650\" r:id=\"rId2\"/><p:sldLayoutId id=\"2147483651\" r:id=\"rId3\"/></p:sldLayoutIdLst>" +
               $"<p:txStyles><p:titleStyle>{tx("title", 2800, "+mj-lt")}</p:titleStyle><p:bodyStyle>{tx("body", 1800, "+mn-lt")}</p:bodyStyle><p:otherStyle><a:defPPr><a:defRPr lang=\"{Language}\"/></a:defPPr></p:otherStyle></p:txStyles></p:sldMaster>";
    }

    string LayoutXml(Layout l)
    {
        string name = l switch { Layout.Title => "Titelfolie", Layout.TitleOnly => "Nur Titel", _ => "Leer" };
        string type = l switch { Layout.Title => "title", Layout.TitleOnly => "titleOnly", _ => "blank" };
        string shapes = l switch
        {
            Layout.Title => PlaceholderSp(2, "Titel 1", "<p:ph type=\"ctrTitle\"/>", 914400, 2286000, 10363200, 1371600, 36, true, "b") +
                            PlaceholderSp(3, "Untertitel 2", "<p:ph type=\"subTitle\" idx=\"1\"/>", 914400, 3810000, 10363200, 914400, 20, false, "t"),
            Layout.TitleOnly => PlaceholderSp(2, "Titel 1", "<p:ph type=\"title\"/>", 685800, 457200, 10820400, 914400, 28, true, "b"),
            _ => ""
        };
        return Hdr + $"<p:sldLayout {Ns} type=\"{type}\" preserve=\"1\"><p:cSld name=\"{name}\"><p:spTree>{Grp}{shapes}</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>";
    }

    string NotesMaster() => Hdr + $"<p:notesMaster {Ns}><p:cSld><p:bg><p:bgRef idx=\"1001\"><a:schemeClr val=\"bg1\"/></p:bgRef></p:bg><p:spTree>{Grp}" +
        "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Folienbildplatzhalter 1\"/><p:cNvSpPr><a:spLocks noGrp=\"1\" noRot=\"1\" noChangeAspect=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"sldImg\" idx=\"2\"/></p:nvPr></p:nvSpPr>" +
        "<p:spPr><a:xfrm><a:off x=\"685800\" y=\"1143000\"/><a:ext cx=\"5486400\" cy=\"3086100\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/><a:ln w=\"12700\"><a:solidFill><a:prstClr val=\"black\"/></a:solidFill></a:ln></p:spPr></p:sp>" +
        "<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Notizenplatzhalter 2\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"body\" sz=\"quarter\" idx=\"3\"/></p:nvPr></p:nvSpPr>" +
        $"<p:spPr><a:xfrm><a:off x=\"685800\" y=\"4400550\"/><a:ext cx=\"5486400\" cy=\"3600450\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr><p:txBody><a:bodyPr vert=\"horz\" lIns=\"91440\" tIns=\"45720\" rIns=\"91440\" bIns=\"45720\" rtlCol=\"0\"/><a:lstStyle/><a:p><a:endParaRPr lang=\"{Language}\" dirty=\"0\"/></a:p></p:txBody></p:sp>" +
        $"</p:spTree></p:cSld>{ClrMap}<p:notesStyle><a:lvl1pPr marL=\"0\" algn=\"l\" defTabSz=\"914400\" rtl=\"0\" eaLnBrk=\"1\" latinLnBrk=\"0\" hangingPunct=\"1\"><a:defRPr sz=\"1200\" kern=\"1200\"><a:solidFill><a:schemeClr val=\"tx1\"/></a:solidFill><a:latin typeface=\"+mn-lt\"/><a:ea typeface=\"+mn-ea\"/><a:cs typeface=\"+mn-cs\"/></a:defRPr></a:lvl1pPr></p:notesStyle></p:notesMaster>";

    string NotesSlide(Slide s)
    {
        var paras = new StringBuilder();
        foreach (var line in s.Notes.Replace("\r", "").Split('\n'))
            paras.Append("<a:p>").Append(line.Length > 0 ? $"<a:r><a:rPr lang=\"{Language}\" dirty=\"0\"/><a:t>{X(line)}</a:t></a:r>" : $"<a:endParaRPr lang=\"{Language}\" dirty=\"0\"/>").Append("</a:p>");
        return Hdr + $"<p:notes {Ns}><p:cSld><p:spTree>{Grp}" +
            "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Folienbildplatzhalter 1\"/><p:cNvSpPr><a:spLocks noGrp=\"1\" noRot=\"1\" noChangeAspect=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"sldImg\"/></p:nvPr></p:nvSpPr><p:spPr/></p:sp>" +
            $"<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Notizenplatzhalter 2\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph type=\"body\" idx=\"1\"/></p:nvPr></p:nvSpPr><p:spPr/><p:txBody><a:bodyPr/><a:lstStyle/>{paras}</p:txBody></p:sp>" +
            "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:notes>";
    }

    public void Save(string path)
    {
        string tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create))
            Write(fs);
        File.Move(tmp, path, true);
    }

    public void Write(Stream output)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var stamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        void Put(string name, string text) { var e = zip.CreateEntry(name, CompressionLevel.Optimal); e.LastWriteTime = stamp; using var w = new StreamWriter(e.Open(), new UTF8Encoding(false)); w.Write(text); }
        void PutBytes(string name, byte[] data) { var e = zip.CreateEntry(name, CompressionLevel.NoCompression); e.LastWriteTime = stamp; using var s = e.Open(); s.Write(data); }

        int n = _slides.Count;
        bool anyNotes = _slides.Any(s => s.Notes.Length > 0);
        var ct = new StringBuilder(Hdr + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/>" +
            "<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>" +
            "<Override PartName=\"/ppt/slideMasters/slideMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml\"/>");
        for (int i = 1; i <= 3; i++) ct.Append($"<Override PartName=\"/ppt/slideLayouts/slideLayout{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml\"/>");
        ct.Append("<Override PartName=\"/ppt/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/>");
        if (anyNotes) ct.Append("<Override PartName=\"/ppt/theme/theme2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/><Override PartName=\"/ppt/notesMasters/notesMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.notesMaster+xml\"/>");
        for (int i = 1; i <= n; i++)
        {
            ct.Append($"<Override PartName=\"/ppt/slides/slide{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>");
            if (_slides[i - 1].Notes.Length > 0) ct.Append($"<Override PartName=\"/ppt/notesSlides/notesSlide{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.notesSlide+xml\"/>");
        }
        ct.Append("<Override PartName=\"/ppt/presProps.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presProps+xml\"/>" +
                  "<Override PartName=\"/ppt/viewProps.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.viewProps+xml\"/>" +
                  "<Override PartName=\"/ppt/tableStyles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.tableStyles+xml\"/>" +
                  "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
                  "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/></Types>");
        Put("[Content_Types].xml", ct.ToString());
        Put("_rels/.rels", Rels(("rId1", "officeDocument", "ppt/presentation.xml"), ("rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml"), ("rId3", "extended-properties", "docProps/app.xml")));
        string iso = Created.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        Put("docProps/core.xml", Hdr + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            $"<dc:title>{X(Title)}</dc:title><dc:creator>{X(Author)}</dc:creator><dc:language>{Language}</dc:language><dcterms:created xsi:type=\"dcterms:W3CDTF\">{iso}</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">{iso}</dcterms:modified></cp:coreProperties>");
        Put("docProps/app.xml", Hdr + $"<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>{X(Data.AppInfo.DisplayName)}</Application><PresentationFormat>Breitbild</PresentationFormat><Slides>{n}</Slides><Notes>{_slides.Count(s => s.Notes.Length > 0)}</Notes></Properties>");

        var presRels = new List<(string, string, string)> { ("rId1", "slideMaster", "slideMasters/slideMaster1.xml"), ("rId3", "theme", "theme/theme1.xml"), ("rId4", "presProps", "presProps.xml"), ("rId5", "viewProps", "viewProps.xml"), ("rId6", "tableStyles", "tableStyles.xml") };
        if (anyNotes) presRels.Add(("rId2", "notesMaster", "notesMasters/notesMaster1.xml"));
        for (int i = 1; i <= n; i++) presRels.Add(($"rId{100 + i}", "slide", $"slides/slide{i}.xml"));
        Put("ppt/_rels/presentation.xml.rels", Rels(presRels.ToArray()));
        Put("ppt/presentation.xml", Hdr + $"<p:presentation {Ns} saveSubsetFonts=\"1\"><p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst>" +
            (anyNotes ? "<p:notesMasterIdLst><p:notesMasterId r:id=\"rId2\"/></p:notesMasterIdLst>" : "") +
            "<p:sldIdLst>" + string.Concat(Enumerable.Range(1, n).Select(i => $"<p:sldId id=\"{255 + i}\" r:id=\"rId{100 + i}\"/>")) + "</p:sldIdLst>" +
            $"<p:sldSz cx=\"{W}\" cy=\"{H}\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/><p:defaultTextStyle><a:defPPr><a:defRPr lang=\"{Language}\"/></a:defPPr></p:defaultTextStyle></p:presentation>");
        Put("ppt/presProps.xml", Hdr + $"<p:presentationPr {Ns}/>");
        Put("ppt/viewProps.xml", Hdr + $"<p:viewPr {Ns}><p:normalViewPr><p:restoredLeft sz=\"15620\"/><p:restoredTop sz=\"94660\"/></p:normalViewPr><p:gridSpacing cx=\"72008\" cy=\"72008\"/></p:viewPr>");
        Put("ppt/tableStyles.xml", Hdr + $"<a:tblStyleLst xmlns:a=\"{NsA}\" def=\"{{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}}\"/>");
        Put("ppt/theme/theme1.xml", Theme("StepRecorder"));
        Put("ppt/slideMasters/slideMaster1.xml", Master());
        Put("ppt/slideMasters/_rels/slideMaster1.xml.rels", Rels(("rId1", "slideLayout", "../slideLayouts/slideLayout1.xml"), ("rId2", "slideLayout", "../slideLayouts/slideLayout2.xml"), ("rId3", "slideLayout", "../slideLayouts/slideLayout3.xml"), ("rId4", "theme", "../theme/theme1.xml")));
        foreach (var l in new[] { Layout.Title, Layout.TitleOnly, Layout.Blank })
        {
            Put($"ppt/slideLayouts/slideLayout{(int)l}.xml", LayoutXml(l));
            Put($"ppt/slideLayouts/_rels/slideLayout{(int)l}.xml.rels", Rels(("rId1", "slideMaster", "../slideMasters/slideMaster1.xml")));
        }
        if (anyNotes)
        {
            Put("ppt/theme/theme2.xml", Theme("StepRecorderNotes"));
            Put("ppt/notesMasters/notesMaster1.xml", NotesMaster());
            Put("ppt/notesMasters/_rels/notesMaster1.xml.rels", Rels(("rId1", "theme", "../theme/theme2.xml")));
        }
        int media = 0;
        for (int i = 1; i <= n; i++)
        {
            var s = _slides[i - 1];
            Put($"ppt/slides/slide{i}.xml", Hdr + $"<p:sld {Ns}><p:cSld><p:spTree>{Grp}{s.Shapes}</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>");
            var rels = new List<(string, string, string)> { ("rId1", "slideLayout", $"../slideLayouts/slideLayout{(int)s.Layout}.xml") };
            if (s.Notes.Length > 0) rels.Add(("rId2", "notesSlide", $"../notesSlides/notesSlide{i}.xml"));
            for (int k = 0; k < s.Media.Count; k++)
            {
                string file = $"image{++media}.png";
                PutBytes($"ppt/media/{file}", s.Media[k]);
                rels.Add(($"rId{3 + k}", "image", $"../media/{file}"));
            }
            Put($"ppt/slides/_rels/slide{i}.xml.rels", Rels(rels.ToArray()));
            if (s.Notes.Length > 0)
            {
                Put($"ppt/notesSlides/notesSlide{i}.xml", NotesSlide(s));
                Put($"ppt/notesSlides/_rels/notesSlide{i}.xml.rels", Rels(("rId1", "notesMaster", "../notesMasters/notesMaster1.xml"), ("rId2", "slide", $"../slides/slide{i}.xml")));
            }
        }
    }
}
