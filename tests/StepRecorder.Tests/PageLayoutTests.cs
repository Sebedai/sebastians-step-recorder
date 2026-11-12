using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Export.Formats;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Seitenformat A4/Letter, hoch/quer (09 §4.4, EXP-027) und eigene Word-Vorlage (09 §7.3, EXP-052).</summary>
[Collection("Loc")]
public class PageLayoutTests : IDisposable
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-page-" + Guid.NewGuid().ToString("N")[..8]);
    public PageLayoutTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    string New() => SyntheticSession.Create(Path.Combine(_root, "s" + Guid.NewGuid().ToString("N")[..6]));

    string Export(string folder, string format, DocSettings? doc = null, string? page = null, bool? landscape = null)
    {
        using var v = SessionView.Open(folder);
        string outPath = Path.Combine(_root, Guid.NewGuid().ToString("N")[..6] + "." + format);
        return GuideExport.Export(v, new GuideOptions { Format = format, OutPath = outPath, Doc = doc, Page = page, Landscape = landscape }).Path!;
    }

    static XDocument Part(ZipArchive zip, string name) { using var s = zip.GetEntry(name)!.Open(); return XDocument.Load(s); }

    static (double w, double h) MediaBox(string pdfPath)
    {
        string text = Encoding.Latin1.GetString(File.ReadAllBytes(pdfPath));
        var boxes = Regex.Matches(text, @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]").Select(m => (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))).Distinct().ToList();
        Assert.Single(boxes);
        // Kein Bild ragt über die Seite hinaus
        foreach (Match m in Regex.Matches(text, @"q ([\d.]+) 0 0 ([\d.]+) ([\d.-]+) ([\d.-]+) cm /Im"))
        {
            double iw = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), ih = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            double x = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), y = double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
            Assert.True(x >= 0 && y >= 0 && x + iw <= boxes[0].Item1 + 0.01 && y + ih <= boxes[0].Item2 + 0.01, $"Bild außerhalb der Seite: {m.Value}");
        }
        return boxes[0];
    }

    static XElement SectPr(ZipArchive zip) => Part(zip, "word/document.xml").Root!.Element(W + "body")!.Elements(W + "sectPr").Last();

    /// <summary>Jede Beziehung zeigt auf ein vorhandenes Teil, jedes Teil hat einen Inhaltstyp.</summary>
    static void AssertPackageConsistent(ZipArchive zip)
    {
        var names = zip.Entries.Select(e => e.FullName).ToHashSet();
        foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels"))) Part(zip, e.FullName);
        var ct = Part(zip, "[Content_Types].xml").Root!;
        XNamespace ctn = ct.Name.Namespace;
        var overrides = ct.Elements(ctn + "Override").Select(o => ((string)o.Attribute("PartName")!).TrimStart('/')).ToHashSet();
        var defaults = ct.Elements(ctn + "Default").Select(o => ((string)o.Attribute("Extension")!).ToLowerInvariant()).ToHashSet();
        foreach (var n in names.Where(n => n != "[Content_Types].xml"))
            Assert.True(overrides.Contains(n) || defaults.Contains(Path.GetExtension(n).TrimStart('.').ToLowerInvariant()), "ohne Inhaltstyp: " + n);
        foreach (var rel in names.Where(n => n.EndsWith(".rels")))
        {
            string baseDir = rel == "_rels/.rels" ? "" : Path.GetDirectoryName(Path.GetDirectoryName(rel))!.Replace('\\', '/');
            foreach (var r in Part(zip, rel).Root!.Elements())
            {
                if ((string?)r.Attribute("TargetMode") == "External") continue;
                string target = (string)r.Attribute("Target")!;
                string full = Path.GetFullPath(Path.Combine("/x", baseDir, target)).Replace('\\', '/');
                full = full[(full.IndexOf("/x/", StringComparison.Ordinal) + 3)..];
                Assert.True(names.Contains(full), $"{rel}: Ziel fehlt {target}");
            }
        }
    }

    // ---------------- Seitenformat ----------------

    [Fact]
    public void Default_IsA4Portrait_InDocxAndPdf()
    {
        var a = New();
        using (var zip = ZipFile.OpenRead(Export(a, "docx")))
        {
            var sz = SectPr(zip).Element(W + "pgSz")!;
            Assert.Equal("11906", (string?)sz.Attribute(W + "w"));
            Assert.Equal("16838", (string?)sz.Attribute(W + "h"));
            Assert.Null(sz.Attribute(W + "orient"));
        }
        var (pw, ph) = MediaBox(Export(a, "pdf"));
        Assert.Equal(595.28, pw, 2); Assert.Equal(841.89, ph, 2);
    }

    [Theory]
    [InlineData("a4", false, 11906, 16838, 595.28, 841.89)]
    [InlineData("a4", true, 16838, 11906, 841.89, 595.28)]
    [InlineData("letter", false, 12240, 15840, 612, 792)]
    [InlineData("letter", true, 15840, 12240, 792, 612)]
    public void PageFormat_FromDocSettings_DocxAndPdf(string page, bool landscape, int tw, int th, double pw, double ph)
    {
        var a = New();
        var doc = new DocSettings { Page = page, Landscape = landscape, ImageSize = "full", Cover = true };
        using (var zip = ZipFile.OpenRead(Export(a, "docx", doc)))
        {
            var sect = SectPr(zip);
            var sz = sect.Element(W + "pgSz")!;
            Assert.Equal(tw.ToString(), (string?)sz.Attribute(W + "w"));
            Assert.Equal(th.ToString(), (string?)sz.Attribute(W + "h"));
            Assert.Equal(landscape ? "landscape" : null, (string?)sz.Attribute(W + "orient"));
            // Bilder passen in den Satzspiegel (Seite minus 2 × 20 mm), Fußzeile vorhanden
            var mar = sect.Element(W + "pgMar")!;
            long textW = (tw - int.Parse((string)mar.Attribute(W + "left")!) - int.Parse((string)mar.Attribute(W + "right")!)) * 635L;
            long textH = (th - int.Parse((string)mar.Attribute(W + "top")!) - int.Parse((string)mar.Attribute(W + "bottom")!)) * 635L;
            var extents = Part(zip, "word/document.xml").Descendants(Wp + "extent").ToList();
            Assert.NotEmpty(extents);
            foreach (var e in extents)
            {
                Assert.True(long.Parse((string)e.Attribute("cx")!) <= textW, "Bild breiter als der Satzspiegel");
                Assert.True(long.Parse((string)e.Attribute("cy")!) <= textH, "Bild höher als der Satzspiegel");
            }
            Assert.NotNull(sect.Element(W + "footerReference"));
            Assert.Contains($"w:pos=\"{tw - 2268}\"", new StreamReader(zip.GetEntry("word/footer1.xml")!.Open()).ReadToEnd());
        }
        var (w, h) = MediaBox(Export(a, "pdf", doc));
        Assert.Equal(pw, w, 2); Assert.Equal(ph, h, 2);
    }

    [Fact]
    public void PageFormat_StoredInEditsJson_AndOverriddenPerExport()
    {
        var a = New();
        using (var v = SessionView.Open(a))
        {
            var guide = v.Edits["guide"] as JsonObject ?? new JsonObject(); v.Edits["guide"] = guide;
            guide["doc"] = new DocSettings { Page = "letter", Landscape = true }.ToJson();
            Assert.True(v.SaveEdits());
        }
        using (var v = SessionView.Open(a))
        {
            var d = DocSettings.From(v.Edits.Obj("guide"));
            Assert.Equal("letter", d.Page); Assert.True(d.Landscape);
            Assert.Equal((792.0, 612.0), d.PagePoints());
            Assert.Equal((15840, 12240), d.PageTwips());
        }
        // Über den Exportauftrag (Kommandozeile): Einstellung der Aufnahme gilt, --page/--portrait überschreiben
        using (var v = SessionView.Open(a))
        {
            var r = Exporter.Run(v, new ExportRequest { Format = "pdf", Target = Path.Combine(_root, "x.pdf") });
            Assert.Equal((792.0, 612.0), MediaBox(r.Path));
            var r2 = Exporter.Run(v, new ExportRequest { Format = "pdf", Target = Path.Combine(_root, "y.pdf"), Page = "a4", Landscape = false });
            Assert.Equal((595.28, 841.89), MediaBox(r2.Path));
        }
        // Der Auftrag behält die Angaben beim Klonen; ohne Angabe bleiben sie leer
        var req = new ExportRequest { Format = "docx", Page = "letter", Landscape = true }.Clone();
        Assert.Equal("letter", req.Page); Assert.True(req.Landscape);
        var plain = new ExportRequest().Clone();
        Assert.Null(plain.Page); Assert.Null(plain.Landscape);
        Assert.Null(new ExportRequest().ToJson()["page"]);
    }

    [Fact]
    public void PageFormat_InCorporateTemplate_IsSanitized()
    {
        var clean = DocSettings.SanitizeTemplate(new JsonObject { ["page"] = "letter", ["landscape"] = true }, out var problem);
        Assert.Null(problem);
        Assert.Equal("letter", clean!.Str("page")); Assert.True(clean.Bool("landscape"));
        var bad = DocSettings.SanitizeTemplate(new JsonObject { ["page"] = "a3" }, out _);
        Assert.Null(bad!["page"]);
    }

    // ---------------- Eigene Word-Vorlage ----------------

    const string CtTemplate = "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml";
    const string CtMacro = "application/vnd.ms-word.template.macroEnabledTemplate.main+xml";

    /// <summary>Kleinste Word-Vorlage: deutsche Formatvorlagenkennungen, Kopfzeile, eigene Ränder, Titelfeld, Marke.</summary>
    static byte[] BuildTemplate(bool marker = true, bool macro = false, bool titleField = true)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string name, string content) { using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(content); }
            const string ns = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
            Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                (macro ? "<Default Extension=\"bin\" ContentType=\"application/vnd.ms-office.vbaProject\"/>" : "") +
                $"<Override PartName=\"/word/document.xml\" ContentType=\"{(macro ? CtMacro : CtTemplate)}\"/>" +
                "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>" +
                "<Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/>" +
                "<Override PartName=\"/word/header1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml\"/></Types>");
            Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Add("word/_rels/document.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/header\" Target=\"header1.xml\"/>" +
                "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings\" Target=\"settings.xml\"/>" +
                (macro ? "<Relationship Id=\"rId9\" Type=\"http://schemas.microsoft.com/office/2006/relationships/vbaProject\" Target=\"vbaProject.bin\"/>" : "") + "</Relationships>");
            Add("word/_rels/settings.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/attachedTemplate\" Target=\"file:///C:/Firma/Vorlage.dotx\" TargetMode=\"External\"/></Relationships>");
            Add("word/settings.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:settings {ns}><w:attachedTemplate r:id=\"rId1\"/></w:settings>");
            Add("word/styles.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:styles {ns}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Georgia\" w:hAnsi=\"Georgia\"/></w:rPr></w:rPrDefault></w:docDefaults>" +
                "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Standard\"><w:name w:val=\"Normal\"/></w:style>" +
                "<w:style w:type=\"paragraph\" w:styleId=\"berschrift1\"><w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Standard\"/><w:rPr><w:rFonts w:ascii=\"Georgia\" w:hAnsi=\"Georgia\"/><w:color w:val=\"112233\"/></w:rPr></w:style>" +
                "<w:style w:type=\"paragraph\" w:styleId=\"Titel\"><w:name w:val=\"Title\"/><w:basedOn w:val=\"Standard\"/></w:style></w:styles>");
            Add("word/header1.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:hdr {ns}><w:p><w:r><w:t>Firmenkopf Muster GmbH</w:t></w:r></w:p></w:hdr>");
            string title = titleField ? "<w:p><w:sdt><w:sdtPr><w:tag w:val=\"SR.Title\"/><w:showingPlcHdr/></w:sdtPr><w:sdtContent><w:r><w:t>Titel hier</w:t></w:r></w:sdtContent></w:sdt></w:p>" : "";
            string mark = marker ? "<w:p><w:bookmarkStart w:id=\"0\" w:name=\"StepRecorderContent\"/><w:bookmarkEnd w:id=\"0\"/></w:p>" : "";
            Add("word/document.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document {ns}><w:body>{title}{mark}<w:p><w:r><w:t>Ende der Vorlage</w:t></w:r></w:p>" +
                "<w:sectPr><w:headerReference w:type=\"default\" r:id=\"rId2\"/><w:pgSz w:w=\"12240\" w:h=\"15840\"/><w:pgMar w:top=\"1000\" w:right=\"1500\" w:bottom=\"1000\" w:left=\"2000\" w:header=\"500\" w:footer=\"500\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>");
            if (macro) { using var s = zip.CreateEntry("word/vbaProject.bin").Open(); s.Write(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }); }
        }
        return ms.ToArray();
    }

    static string Text(XElement e) => string.Concat(e.Descendants(W + "t").Select(t => t.Value));

    [Fact]
    public void WordTemplate_TakesHeaderMarginsStyles_ContentAtMarker()
    {
        var a = New();
        var doc = new DocSettings { WordTemplate = Convert.ToBase64String(BuildTemplate()), WordTemplateName = "Firma.dotx", ImageSize = "full" };
        string path = Export(a, "docx", doc);
        using var zip = ZipFile.OpenRead(path);
        AssertPackageConsistent(zip);

        // Hauptteil ist ein Dokument, keine Vorlage mehr; keine Makros, kein Verweis auf die Vorlagendatei
        string ct = new StreamReader(zip.GetEntry("[Content_Types].xml")!.Open()).ReadToEnd();
        Assert.Contains("wordprocessingml.document.main+xml", ct);
        Assert.DoesNotContain("template.main+xml", ct);
        Assert.Empty(Part(zip, "word/settings.xml").Descendants(W + "attachedTemplate"));
        Assert.DoesNotContain(Part(zip, "word/_rels/settings.xml.rels").Root!.Elements(), r => (string?)r.Attribute("TargetMode") == "External");

        // Kopfzeile und Seitenränder der Vorlage
        var sect = SectPr(zip);
        Assert.NotNull(sect.Element(W + "headerReference"));
        Assert.Contains("Firmenkopf Muster GmbH", Text(Part(zip, "word/header1.xml").Root!));
        Assert.Equal("2000", (string?)sect.Element(W + "pgMar")!.Attribute(W + "left"));
        Assert.Equal("12240", (string?)sect.Element(W + "pgSz")!.Attribute(W + "w"));
        Assert.Null(sect.Element(W + "footerReference"));   // die Fußzeile kommt aus der Vorlage (hier: keine)

        // Formatvorlagen über den Namen gefunden (deutsche Kennungen), fehlende angelegt, Schrift der Vorlage bleibt
        var styles = Part(zip, "word/styles.xml");
        var ids = styles.Descendants(W + "style").Select(s => (string)s.Attribute(W + "styleId")!).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Contains("Note", ids);
        Assert.DoesNotContain("Heading1", ids);
        Assert.DoesNotContain("Normal", ids);
        Assert.Contains(styles.Descendants(W + "rFonts"), f => (string?)f.Attribute(W + "ascii") == "Georgia");
        var heading2 = styles.Descendants(W + "style").Single(s => (string?)s.Attribute(W + "styleId") == "Heading2");
        Assert.Equal("Standard", (string?)heading2.Element(W + "basedOn")!.Attribute(W + "val"));
        var document = Part(zip, "word/document.xml");
        var used = document.Descendants(W + "pStyle").Select(s => (string)s.Attribute(W + "val")!).ToHashSet();
        Assert.All(used, u => Assert.Contains(u, ids));

        // Titelfeld gefüllt, Inhalt hinter der Marke und vor dem Rest der Vorlage
        var body = document.Root!.Element(W + "body")!.Elements().ToList();
        var sdt = document.Descendants(W + "sdt").First(s => s.Descendants(W + "tag").Any(t => (string?)t.Attribute(W + "val") == "SR.Title"));
        using (var v = SessionView.Open(a)) Assert.Equal(v.Title, Text(sdt));
        Assert.Null(sdt.Element(W + "sdtPr")!.Element(W + "showingPlcHdr"));
        int markIdx = body.FindIndex(e => e.Descendants(W + "bookmarkStart").Any(b => (string?)b.Attribute(W + "name") == "StepRecorderContent"));
        int endIdx = body.FindIndex(e => Text(e) == "Ende der Vorlage");
        int firstHeading = body.FindIndex(e => e.Descendants(W + "pStyle").Any(s => (string?)s.Attribute(W + "val") == "Heading2"));
        Assert.True(markIdx >= 0 && firstHeading > markIdx && endIdx > firstHeading, $"Marke {markIdx}, Inhalt {firstHeading}, Ende {endIdx}");
        // Mit Titelfeld bringt die Vorlage den Titelblock mit – kein zweiter Titel im Inhalt
        Assert.DoesNotContain(document.Descendants(W + "pStyle"), s => (string?)s.Attribute(W + "val") == "Titel");

        // Bilder: eigene Teile, eindeutige Kennungen, höchstens so breit wie der Satzspiegel der Vorlage
        Assert.Contains(zip.Entries, e => e.FullName.StartsWith("word/media/sr-image"));
        var docPr = document.Descendants(Wp + "docPr").Select(e => (string)e.Attribute("id")!).ToList();
        Assert.Equal(docPr.Count, docPr.Distinct().Count());
        var bms = document.Descendants(W + "bookmarkStart").Select(e => (string)e.Attribute(W + "id")!).ToList();
        Assert.Equal(bms.Count, bms.Distinct().Count());
        foreach (var e in document.Descendants(Wp + "extent")) Assert.True(long.Parse((string)e.Attribute("cx")!) <= (12240 - 2000 - 1500) * 635L);
        Assert.Contains("<dc:title>", new StreamReader(zip.GetEntry("docProps/core.xml")!.Open()).ReadToEnd());
    }

    [Fact]
    public void WordTemplate_WithoutMarker_ContentAtEnd_TitleBlockKept()
    {
        var a = New();
        var doc = new DocSettings { WordTemplate = Convert.ToBase64String(BuildTemplate(marker: false, titleField: false)) };
        using var zip = ZipFile.OpenRead(Export(a, "docx", doc));
        AssertPackageConsistent(zip);
        var body = Part(zip, "word/document.xml").Root!.Element(W + "body")!.Elements().ToList();
        Assert.Equal(W + "sectPr", body[^1].Name);
        int endIdx = body.FindIndex(e => Text(e) == "Ende der Vorlage");
        Assert.Equal(0, endIdx);
        // eigener Titelblock mit der Titel-Formatvorlage der Vorlage („Titel“, über den Namen „Title“ gefunden)
        Assert.Contains(body, e => e.Descendants(W + "pStyle").Any(s => (string?)s.Attribute(W + "val") == "Titel"));
    }

    [Fact]
    public void WordTemplate_WithMacros_IsRejected()
    {
        var dotm = BuildTemplate(macro: true);
        Assert.NotNull(SimpleDocx.CheckTemplate(dotm));
        Assert.Null(SimpleDocx.CheckTemplate(BuildTemplate()));
        Assert.NotNull(SimpleDocx.CheckTemplate(Encoding.UTF8.GetBytes("kein zip")));
        var a = New();
        Assert.Throws<InvalidOperationException>(() => Export(a, "docx", new DocSettings { WordTemplate = Convert.ToBase64String(dotm) }));
        // auch nicht über eine weitergegebene Vorlage (Einstellungsaustausch)
        var clean = DocSettings.SanitizeTemplate(new JsonObject { ["wordTemplate"] = Convert.ToBase64String(dotm) }, out var problem);
        Assert.NotNull(problem);
        Assert.Null(clean!["wordTemplate"]);
        var ok = DocSettings.SanitizeTemplate(new JsonObject { ["wordTemplate"] = Convert.ToBase64String(BuildTemplate()), ["wordTemplateName"] = "Firma.dotx" }, out var none);
        Assert.Null(none);
        Assert.NotNull(ok!.Str("wordTemplate"));
    }

    [Fact]
    public void WordTemplate_StoredInDocSettings_RoundTrip()
    {
        var tpl = BuildTemplate();
        var d = new DocSettings { WordTemplate = Convert.ToBase64String(tpl), WordTemplateName = "Firma.dotx" };
        var back = DocSettings.From(new JsonObject { ["doc"] = d.ToJson() });
        Assert.Equal("Firma.dotx", back.WordTemplateName);
        Assert.Equal(tpl, back.WordTemplateBytes());
        Assert.Null(new DocSettings().WordTemplateBytes());
    }
}
