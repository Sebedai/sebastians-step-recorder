using System.IO.Compression;
using System.Security;
using System.Text;

namespace StepRecorder.Export.Formats;

/// <summary>
/// Minimaler DOCX-Erzeuger (Office Open XML) ohne Fremdbibliothek: Überschriften, Absätze,
/// fett/kursiv, Aufzählungen, Bilder (PNG/JPEG), Seitenumbruch. Nutzt echte Formatvorlagen
/// (Überschrift 1/2, Titel), damit Word Navigation und Inhaltsverzeichnis anbieten kann.
/// </summary>
public sealed class SimpleDocx
{
    StringBuilder _body = new();
    readonly List<(string name, byte[] data, string ext)> _media = new();
    public string Title { get; set; } = "";
    public string Language { get; set; } = "de-DE";
    /// <summary>Akzentfarbe für Titel und Kapitelüberschriften (RRGGBB).</summary>
    public string Accent { get; set; } = "1F3864";
    /// <summary>Text der Fußzeile (links); rechts steht die Seitenzahl. null = keine Fußzeile.</summary>
    public string? Footer { get; set; }
    int _docPrId = 1;

    static string X(string s) => SecurityElement.Escape(s ?? "") ?? "";

    static string Run(string text, bool bold = false, bool italic = false, string? color = null, int? sizeHalfPt = null, bool mono = false)
    {
        var rpr = new StringBuilder();
        if (mono) rpr.Append("<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\"/>");
        if (bold) rpr.Append("<w:b/>");
        if (italic) rpr.Append("<w:i/>");
        if (color != null) rpr.Append($"<w:color w:val=\"{color}\"/>");
        if (sizeHalfPt != null) rpr.Append($"<w:sz w:val=\"{sizeHalfPt}\"/>");
        var sb = new StringBuilder("<w:r>");
        if (rpr.Length > 0) sb.Append("<w:rPr>").Append(rpr).Append("</w:rPr>");
        var lines = (text ?? "").Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append("<w:br/>");
            sb.Append("<w:t xml:space=\"preserve\">").Append(X(lines[i])).Append("</w:t>");
        }
        return sb.Append("</w:r>").ToString();
    }

    int _bookmarkId;

    /// <summary>Überschrift; mit <paramref name="bookmark"/> als Sprungziel des Inhaltsverzeichnisses.</summary>
    public void Heading(string text, int level = 1, string? bookmark = null)
    {
        string bs = "", be = "";
        if (bookmark != null) { int id = ++_bookmarkId; bs = $"<w:bookmarkStart w:id=\"{id}\" w:name=\"{X(bookmark)}\"/>"; be = $"<w:bookmarkEnd w:id=\"{id}\"/>"; }
        _body.Append($"<w:p><w:pPr><w:pStyle w:val=\"{(level == 0 ? "Title" : "Heading" + level)}\"/></w:pPr>{bs}{Run(text)}{be}</w:p>");
    }

    /// <summary>Name eines Sprungziels im Stil von Word (versteckt, weil mit „_“ beginnend).</summary>
    public static string TocBookmark(int index) => "_Toc" + (100000000 + index).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Echtes Inhaltsverzeichnis: Feld <c>TOC \o "1-2" \h \z \u</c> in einem Inhaltsverzeichnis-Block. Das Zwischenergebnis
    /// sind die heutigen Einträge als Verweise; Seitenzahlen ergänzt Word bei „Felder aktualisieren“. Bewusst ohne
    /// <c>w:updateFields</c>/<c>w:dirty</c> – beides löst bei jedem Öffnen eine Rückfrage aus.
    /// </summary>
    public void Toc(string heading, IEnumerable<(string text, int level, string bookmark)> entries)
    {
        var list = entries.ToList();
        var sb = new StringBuilder("<w:sdt><w:sdtPr><w:docPartObj><w:docPartGallery w:val=\"Table of Contents\"/><w:docPartUnique/></w:docPartObj></w:sdtPr><w:sdtContent>");
        sb.Append($"<w:p><w:pPr><w:pStyle w:val=\"TOCHeading\"/></w:pPr>{Run(heading)}</w:p>");
        const string fieldBegin = "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> TOC \\o \"1-2\" \\h \\z \\u </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";
        for (int i = 0; i < list.Count; i++)
        {
            var (text, level, bm) = list[i];
            sb.Append($"<w:p><w:pPr><w:pStyle w:val=\"TOC{Math.Clamp(level, 1, 2)}\"/><w:tabs><w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"9628\"/></w:tabs></w:pPr>");
            if (i == 0) sb.Append(fieldBegin);
            sb.Append($"<w:hyperlink w:anchor=\"{X(bm)}\" w:history=\"1\">{Run(text)}</w:hyperlink>");
            if (i == list.Count - 1) sb.Append("<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>");
            sb.Append("</w:p>");
        }
        if (list.Count == 0) sb.Append("<w:p>").Append(fieldBegin).Append("<w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>");
        sb.Append("</w:sdtContent></w:sdt>");
        _body.Append(sb);
    }

    public void Paragraph(string text, bool bold = false, bool italic = false, string? color = null, bool mono = false)
        => _body.Append($"<w:p>{Run(text, bold, italic, color, null, mono)}</w:p>");

    /// <summary>Absatz aus mehreren Läufen (Text, fett).</summary>
    public void Rich(params (string text, bool bold)[] runs)
        => _body.Append("<w:p>").Append(string.Concat(runs.Select(r => Run(r.text, r.bold)))).Append("</w:p>");

    public void Bullet(string text)
        => _body.Append($"<w:p><w:pPr><w:pStyle w:val=\"ListBullet\"/></w:pPr>{Run("•\t" + text)}</w:p>");

    public void Note(string label, string text, string color = "7A4E00", string fill = "FFF8E1")
        => _body.Append($"<w:p><w:pPr><w:pStyle w:val=\"Note\"/><w:pBdr><w:left w:val=\"single\" w:sz=\"18\" w:space=\"6\" w:color=\"{color}\"/></w:pBdr><w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{fill}\"/></w:pPr>{Run(label + " ", true, false, color)}{Run(text)}</w:p>");

    public void Subtitle(string text) => _body.Append($"<w:p><w:pPr><w:pStyle w:val=\"Subtitle\"/></w:pPr>{Run(text)}</w:p>");

    /// <summary>Zeile „Bezeichnung  Wert“ (z. B. auf dem Deckblatt).</summary>
    public void MetaLine(string label, string value) => _body.Append($"<w:p><w:pPr><w:spacing w:after=\"40\"/></w:pPr>{Run(label + "  ", false, false, "7A776E")}{Run(value, true)}</w:p>");

    /// <summary>Eintrag im Inhaltsverzeichnis (Ebene 0 = Kapitel, 1 = Schritt).</summary>
    public void TocEntry(string text, int level) => _body.Append($"<w:p><w:pPr><w:spacing w:after=\"40\"/><w:ind w:left=\"{level * 400}\"/></w:pPr>{Run(text, level == 0)}</w:p>");

    /// <summary>Zweispaltig: links <paramref name="left"/>, rechts <paramref name="right"/> (randlose Tabelle).</summary>
    public void SideBySide(Action left, Action right, int leftPct = 45)
    {
        var outer = _body;
        _body = new StringBuilder(); left(); string l = _body.ToString();
        _body = new StringBuilder(); right(); string r = _body.ToString();
        _body = outer;
        int total = 9638, lw = total * leftPct / 100, rw = total - lw;
        if (l.Length == 0) l = "<w:p/>";
        if (r.Length == 0) r = "<w:p/>";
        _body.Append($"<w:tbl><w:tblPr><w:tblW w:w=\"{total}\" w:type=\"dxa\"/><w:tblLayout w:type=\"fixed\"/><w:tblCellMar><w:left w:w=\"0\" w:type=\"dxa\"/><w:right w:w=\"200\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr><w:tblGrid><w:gridCol w:w=\"{lw}\"/><w:gridCol w:w=\"{rw}\"/></w:tblGrid>" +
                     $"<w:tr><w:trPr><w:cantSplit/></w:trPr><w:tc><w:tcPr><w:tcW w:w=\"{lw}\" w:type=\"dxa\"/></w:tcPr>{l}</w:tc><w:tc><w:tcPr><w:tcW w:w=\"{rw}\" w:type=\"dxa\"/></w:tcPr>{r}</w:tc></w:tr></w:tbl><w:p/>");
    }

    public void PageBreak() => _body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");

    /// <summary>Bild in maximal 16 cm Breite.</summary>
    public void Image(byte[] data, string ext, int pxW, int pxH, string altText, double maxWidthCm = 16, double maxHeightCm = 12, bool border = true)
    {
        string name = $"image{_media.Count + 1}.{ext}";
        _media.Add((name, data, ext));
        string rid = $"rIdImg{_media.Count}";
        const long emuPerCm = 360000;
        double wcm = pxW / 96.0 * 2.54, hcm = pxH / 96.0 * 2.54;
        double s = Math.Min(1, Math.Min(maxWidthCm / wcm, maxHeightCm / hcm));
        long cx = (long)(wcm * s * emuPerCm), cy = (long)(hcm * s * emuPerCm);
        int id = _docPrId++;
        _body.Append($@"<w:p><w:pPr><w:keepNext/></w:pPr><w:r><w:drawing><wp:inline distT=""0"" distB=""0"" distL=""0"" distR=""0""><wp:extent cx=""{cx}"" cy=""{cy}""/><wp:docPr id=""{id}"" name=""Bild {id}"" descr=""{X(altText)}""/><wp:cNvGraphicFramePr><a:graphicFrameLocks xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"" noChangeAspect=""1""/></wp:cNvGraphicFramePr><a:graphic xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main""><a:graphicData uri=""http://schemas.openxmlformats.org/drawingml/2006/picture""><pic:pic xmlns:pic=""http://schemas.openxmlformats.org/drawingml/2006/picture""><pic:nvPicPr><pic:cNvPr id=""{id}"" name=""{name}"" descr=""{X(altText)}""/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=""{rid}""/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=""0"" y=""0""/><a:ext cx=""{cx}"" cy=""{cy}""/></a:xfrm><a:prstGeom prst=""rect""><a:avLst/></a:prstGeom>{(border ? "<a:ln w=\"6350\"><a:solidFill><a:srgbClr val=\"BFBFBF\"/></a:solidFill></a:ln>" : "")}</pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>");
    }

    public void Save(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, string content)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            e.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero); // reproduzierbar
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write(content);
        }
        bool hasPng = _media.Any(m => m.ext == "png"), hasJpg = _media.Any(m => m.ext is "jpg" or "jpeg");
        Add("[Content_Types].xml", $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/><Default Extension=""xml"" ContentType=""application/xml""/>{(hasPng ? "<Default Extension=\"png\" ContentType=\"image/png\"/>" : "")}{(hasJpg ? "<Default Extension=\"jpg\" ContentType=\"image/jpeg\"/>" : "")}<Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/><Override PartName=""/word/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml""/>{(Footer != null ? "<Override PartName=\"/word/footer1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/>" : "")}<Override PartName=""/docProps/core.xml"" ContentType=""application/vnd.openxmlformats-package.core-properties+xml""/></Types>");
        Add("_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/><Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"" Target=""docProps/core.xml""/></Relationships>");
        Add("docProps/core.xml", $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/metadata/core-properties"" xmlns:dc=""http://purl.org/dc/elements/1.1/""><dc:title>{X(Title)}</dc:title><dc:creator>StepRecorder</dc:creator></cp:coreProperties>");
        var rels = new StringBuilder(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rIdStyles"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>");
        for (int i = 0; i < _media.Count; i++)
            rels.Append($@"<Relationship Id=""rIdImg{i + 1}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"" Target=""media/{_media[i].name}""/>");
        if (Footer != null) rels.Append(@"<Relationship Id=""rIdFooter1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer"" Target=""footer1.xml""/>");
        rels.Append("</Relationships>");
        if (Footer != null)
        {
            const string rp = "<w:rPr><w:color w:val=\"7A776E\"/><w:sz w:val=\"16\"/></w:rPr>";
            Add("word/footer1.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<w:ftr xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:p><w:pPr><w:pBdr><w:top w:val=\"single\" w:sz=\"4\" w:space=\"4\" w:color=\"D0CEC6\"/></w:pBdr><w:tabs><w:tab w:val=\"right\" w:pos=\"9638\"/></w:tabs></w:pPr>" +
                Run(Footer, false, false, "7A776E", 16) + $"<w:r>{rp}<w:tab/></w:r><w:r>{rp}<w:fldChar w:fldCharType=\"begin\"/></w:r><w:r>{rp}<w:instrText xml:space=\"preserve\"> PAGE </w:instrText></w:r><w:r>{rp}<w:fldChar w:fldCharType=\"separate\"/></w:r><w:r>{rp}<w:t>1</w:t></w:r><w:r>{rp}<w:fldChar w:fldCharType=\"end\"/></w:r></w:p></w:ftr>");
        }
        Add("word/_rels/document.xml.rels", rels.ToString());
        Add("word/styles.xml", Styles());
        Add("word/document.xml", $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"" xmlns:wp=""http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing""><w:body>{_body}<w:sectPr>{(Footer != null ? "<w:footerReference w:type=\"default\" r:id=\"rIdFooter1\"/>" : "")}<w:pgSz w:w=""11906"" w:h=""16838""/><w:pgMar w:top=""1134"" w:right=""1134"" w:bottom=""1134"" w:left=""1134"" w:header=""567"" w:footer=""567"" w:gutter=""0""/></w:sectPr></w:body></w:document>");
        foreach (var m in _media)
        {
            var e = zip.CreateEntry("word/media/" + m.name, CompressionLevel.NoCompression);
            e.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var s = e.Open(); s.Write(m.data);
        }
    }

    string Styles() => $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:styles xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=""Calibri"" w:hAnsi=""Calibri"" w:eastAsia=""Calibri"" w:cs=""Calibri""/><w:sz w:val=""22""/><w:lang w:val=""{Language}""/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after=""120"" w:line=""264"" w:lineRule=""auto""/></w:pPr></w:pPrDefault></w:docDefaults>
<w:style w:type=""paragraph"" w:default=""1"" w:styleId=""Normal""><w:name w:val=""Normal""/><w:qFormat/></w:style>
<w:style w:type=""paragraph"" w:styleId=""Title""><w:name w:val=""Title""/><w:basedOn w:val=""Normal""/><w:next w:val=""Normal""/><w:qFormat/><w:pPr><w:spacing w:after=""240""/></w:pPr><w:rPr><w:b/><w:color w:val=""{Accent}""/><w:sz w:val=""52""/></w:rPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""Subtitle""><w:name w:val=""Subtitle""/><w:basedOn w:val=""Normal""/><w:next w:val=""Normal""/><w:qFormat/><w:pPr><w:spacing w:after=""360""/></w:pPr><w:rPr><w:color w:val=""5F5D56""/><w:sz w:val=""30""/></w:rPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""Heading1""><w:name w:val=""heading 1""/><w:basedOn w:val=""Normal""/><w:next w:val=""Normal""/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before=""360"" w:after=""120""/><w:outlineLvl w:val=""0""/></w:pPr><w:rPr><w:b/><w:color w:val=""{Accent}""/><w:sz w:val=""32""/></w:rPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""Heading2""><w:name w:val=""heading 2""/><w:basedOn w:val=""Normal""/><w:next w:val=""Normal""/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before=""240"" w:after=""80""/><w:outlineLvl w:val=""1""/></w:pPr><w:rPr><w:b/><w:color w:val=""262522""/><w:sz w:val=""26""/></w:rPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""Heading3""><w:name w:val=""heading 3""/><w:basedOn w:val=""Normal""/><w:next w:val=""Normal""/><w:qFormat/><w:pPr><w:keepNext/><w:outlineLvl w:val=""2""/></w:pPr><w:rPr><w:b/><w:sz w:val=""24""/></w:rPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""TOCHeading""><w:name w:val=""TOC Heading""/><w:basedOn w:val=""Heading1""/><w:next w:val=""Normal""/><w:uiPriority w:val=""39""/><w:unhideWhenUsed/><w:qFormat/><w:pPr><w:outlineLvl w:val=""9""/></w:pPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""TOC1""><w:name w:val=""toc 1""/><w:basedOn w:val=""Normal""/><w:next w:val=""Normal""/><w:uiPriority w:val=""39""/><w:unhideWhenUsed/><w:pPr><w:spacing w:after=""60""/></w:pPr><w:rPr><w:b/></w:rPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""TOC2""><w:name w:val=""toc 2""/><w:basedOn w:val=""Normal""/><w:next w:val=""Normal""/><w:uiPriority w:val=""39""/><w:unhideWhenUsed/><w:pPr><w:spacing w:after=""40""/><w:ind w:left=""400""/></w:pPr></w:style>
<w:style w:type=""character"" w:styleId=""Hyperlink""><w:name w:val=""Hyperlink""/><w:uiPriority w:val=""99""/><w:unhideWhenUsed/><w:rPr><w:color w:val=""1C1B19""/></w:rPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""ListBullet""><w:name w:val=""List Bullet""/><w:basedOn w:val=""Normal""/><w:pPr><w:ind w:left=""360"" w:hanging=""360""/></w:pPr></w:style>
<w:style w:type=""paragraph"" w:styleId=""Note""><w:name w:val=""Note""/><w:basedOn w:val=""Normal""/><w:pPr><w:pBdr><w:left w:val=""single"" w:sz=""18"" w:space=""6"" w:color=""E0A800""/></w:pBdr><w:shd w:val=""clear"" w:color=""auto"" w:fill=""FFF8E1""/><w:ind w:left=""200""/></w:pPr></w:style>
</w:styles>";
}
