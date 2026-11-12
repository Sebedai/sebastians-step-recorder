using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Neue Ausgabeformate: PowerPoint, Klick-Tour, Fehlerbericht.</summary>
[Collection("Loc")]
public class ExportFormatsTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-fmt-" + Guid.NewGuid().ToString("N")[..8]);
    public ExportFormatsTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    string New(bool withSecret = false) => SyntheticSession.Create(Path.Combine(_root, "s" + Guid.NewGuid().ToString("N")[..6]), withSecret: withSecret);

    ExportResult Export(string folder, string format, string? outName = null)
    {
        using var v = SessionView.Open(folder);
        return Exporter.Run(v, new ExportRequest { Format = format, Target = outName == null ? null : Path.Combine(_root, outName) });
    }

    // ---------------- PowerPoint ----------------

    [Fact]
    public void Pptx_PackageIsConsistent_AndDeterministic()
    {
        var a = New();
        var r1 = Export(a, "pptx", "a.pptx");
        var r2 = Export(a, "pptx", "b.pptx");
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(r1.Path))), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(r2.Path))));

        using var zip = ZipFile.OpenRead(r1.Path);
        Assert.Equal("[Content_Types].xml", zip.Entries[0].FullName);
        var names = zip.Entries.Select(e => e.FullName).ToHashSet();
        XDocument Load(string n) { using var s = zip.GetEntry(n)!.Open(); return XDocument.Load(s); }
        // jedes XML-Teil ist lesbar
        foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels"))) Load(e.FullName);
        // Inhaltstypen: jedes Teil hat einen (Override oder Default nach Endung)
        var ct = Load("[Content_Types].xml").Root!;
        XNamespace ctn = ct.Name.Namespace;
        var overrides = ct.Elements(ctn + "Override").Select(o => ((string)o.Attribute("PartName")!).TrimStart('/')).ToHashSet();
        var defaults = ct.Elements(ctn + "Default").Select(o => (string)o.Attribute("Extension")!).ToHashSet();
        foreach (var n in names.Where(n => n != "[Content_Types].xml"))
            Assert.True(overrides.Contains(n) || defaults.Contains(Path.GetExtension(n).TrimStart('.')), "ohne Inhaltstyp: " + n);
        foreach (var o in overrides) Assert.Contains(o, names);
        // jede Beziehung zeigt auf ein vorhandenes Teil
        foreach (var rel in names.Where(n => n.EndsWith(".rels")))
        {
            string baseDir = rel == "_rels/.rels" ? "" : Path.GetDirectoryName(Path.GetDirectoryName(rel))!.Replace('\\', '/');
            foreach (var r in Load(rel).Root!.Elements())
            {
                string target = (string)r.Attribute("Target")!;
                string full = Path.GetFullPath(Path.Combine("/x", baseDir, target)).Replace('\\', '/');
                full = full[(full.IndexOf("/x/", StringComparison.Ordinal) + 3)..];
                Assert.True(names.Contains(full), $"{rel}: Ziel fehlt {target}");
            }
        }
        // Folienzahl: Titel + Schritte + Abschluss (ohne Einleitung/Voraussetzungen in der Testaufnahme)
        int slides = names.Count(n => n.StartsWith("ppt/slides/slide") && n.EndsWith(".xml"));
        using (var v = SessionView.Open(a)) Assert.Equal(v.GuideSteps().Count(s => s.Kind != "move") + 2 + (v.Goal() != null ? 1 : 0), slides);
        // Shape-IDs je Folie eindeutig; Bilder im Seitenverhältnis
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main", ad = "http://schemas.openxmlformats.org/drawingml/2006/main";
        foreach (var n in names.Where(n => n.StartsWith("ppt/slides/slide")))
        {
            var doc = Load(n);
            var ids = doc.Descendants(p + "cNvPr").Select(e => (string)e.Attribute("id")!).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.NotNull(doc.Descendants(p + "ph").FirstOrDefault(e => (string?)e.Attribute("type") is "title" or "ctrTitle"));
        }
        Assert.Contains(names, n => n.StartsWith("ppt/media/"));
        Assert.Contains(names, n => n.StartsWith("ppt/notesSlides/"));
        var theme = Load("ppt/theme/theme1.xml");
        foreach (var lst in new[] { "fillStyleLst", "lnStyleLst", "effectStyleLst", "bgFillStyleLst" })
            Assert.Equal(3, theme.Descendants(ad + lst).Single().Elements().Count());
    }

    // ---------------- Word: echtes Inhaltsverzeichnis ----------------

    [Fact]
    public void Docx_TocIsField_WithBookmarkLinks_NoUpdatePrompt()
    {
        var a = New();
        using (var v = SessionView.Open(a))
        {
            long first = v.GuideSteps().First().Anchor;
            v.Edits["steps"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["anchor"] = first, ["chapter"] = new System.Text.Json.Nodes.JsonObject { ["title"] = "Vorbereitung" } });
            Assert.True(v.SaveEdits());
        }
        var r = Export(a, "docx", "a.docx");
        using var zip = ZipFile.OpenRead(r.Path);
        Assert.DoesNotContain(zip.Entries, e => e.FullName == "word/settings.xml");          // kein updateFields
        string xml; using (var sr = new StreamReader(zip.GetEntry("word/document.xml")!.Open())) xml = sr.ReadToEnd();
        var doc = XDocument.Parse(xml);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        Assert.Contains(doc.Descendants(w + "docPartGallery"), e => (string?)e.Attribute(w + "val") == "Table of Contents");
        Assert.Contains(doc.Descendants(w + "instrText"), e => e.Value.Contains("TOC \\o \"1-2\" \\h \\z \\u"));
        var anchors = doc.Descendants(w + "hyperlink").Select(e => (string)e.Attribute(w + "anchor")!).ToList();
        var marks = doc.Descendants(w + "bookmarkStart").Select(e => (string)e.Attribute(w + "name")!).ToHashSet();
        Assert.NotEmpty(anchors);
        Assert.All(anchors, x => Assert.Contains(x, marks));
        Assert.DoesNotContain("w:dirty", xml);
        Assert.Equal(1, doc.Descendants(w + "fldChar").Count(e => (string?)e.Attribute(w + "fldCharType") == "begin" && e.Parent!.ElementsAfterSelf().Any(x => x.Value.Contains("TOC"))));
    }

    // ---------------- Klick-Tour ----------------

    [Fact]
    public void Tour_SingleFile_NoScript_NoStyleAttributes_HasHotspots()
    {
        var a = New();
        var r = Export(a, "tour", "tour.html");
        var html = File.ReadAllText(r.Path);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=\"", html);
        Assert.Contains("id=\"start\"", html);
        Assert.Contains("id=\"end\"", html);
        Assert.Contains("class=\"hot\"", html);
        Assert.Matches(@"#h\d+\{left:[\d.]+%;top:[\d.]+%;width:[\d.]+%;height:[\d.]+%\}", html);
        using var v = SessionView.Open(a);
        Assert.Equal(v.GuideSteps().Count(s => s.Kind != "move"), System.Text.RegularExpressions.Regex.Matches(html, "<section class=\"slide\" id=\"s\\d+\"").Count);
    }

    // ---------------- Fehlerbericht ----------------

    [Fact]
    public void BugReport_HasSections_NoPathsOrPids_RequiresReview()
    {
        var a = New();
        var r = Export(a, "bugreport");
        var md = File.ReadAllText(r.Path);
        Assert.Equal(md, r.Text);
        foreach (var h in new[] { "## Zusammenfassung", "## Umgebung", "## Schritte zum Nachstellen", "## Erwartetes Ergebnis", "## Tatsächliches Ergebnis" }) Assert.Contains(h, md);
        Assert.Contains("`notepad.exe`", md);
        Assert.DoesNotContain("pid", md, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@":\", md);
        Assert.True(Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(r.Path)!, "bilder")).Any());

        var b = New(withSecret: true);
        Assert.Throws<InvalidOperationException>(() => { using var v = SessionView.Open(b, headless: true); GuideExport.Export(v, new GuideOptions { Format = "bugreport", OutPath = Path.Combine(_root, "x.md") }); });
    }
}
