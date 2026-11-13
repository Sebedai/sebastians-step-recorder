using System.IO.Compression;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Seite und Layout (13, Paket 27): Rand, Kopf- und Fußzeile mit Feldern, Umbrüche, Layoutvorlagen.</summary>
[Collection("Loc")]
public class LayoutBandTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-page-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    void Edit(Action<JsonObject, List<ViewStep>> edit)
    {
        using var v = SessionView.Open(_folder, headless: true);
        edit(v.Edits, v.GuideSteps().Where(s => s.Kind != "move").ToList());
        Assert.True(v.SaveEdits());
    }

    string Path_(string fmt)
    {
        using var v = SessionView.Open(_folder, headless: true);
        Directory.CreateDirectory(_out);
        string p = Path.Combine(_out, Guid.NewGuid().ToString("N")[..6] + "." + fmt);
        GuideExport.Export(v, new GuideOptions { Format = fmt, OutPath = p, Images = false });
        return p;
    }

    static string Entry(string docx, string name)
    {
        using var z = ZipFile.OpenRead(docx);
        var e = z.GetEntry(name);
        if (e == null) return "";
        using var r = new StreamReader(e.Open());
        return r.ReadToEnd();
    }

    [Fact]
    public void HeaderFooter_FieldsAndVariables()
    {
        Edit((e, s) => e["guide"] = new JsonObject
        {
            ["doc"] = new JsonObject { ["version"] = "2.1", ["headerLeft"] = "{{Version}}", ["footerRight"] = "Seite {{Seite}} von {{Seiten}}", ["footerLeft"] = "Kapitel: {{Kapitel}}" },
            ["vars"] = new JsonObject()
        });
        var pdf = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(Path_("pdf")));
        Assert.Contains("(Seite 1 von ", pdf);
        Assert.Contains("(2.1)", pdf);
        string docx = Path_("docx");
        string ftr = Entry(docx, "word/footer1.xml"), hdr = Entry(docx, "word/header1.xml");
        Assert.Contains("NUMPAGES", ftr);
        Assert.Contains(" PAGE ", ftr);
        Assert.Contains("STYLEREF", ftr);
        Assert.Contains("2.1", hdr);
        Assert.Contains("headerReference", Entry(docx, "word/document.xml"));
    }

    [Fact]
    public void Margin_InWordAndPdf()
    {
        Edit((e, s) => e["guide"] = new JsonObject { ["doc"] = new JsonObject { ["marginMm"] = 15 } });
        Assert.Contains("w:top=\"850\" w:right=\"850\"", Entry(Path_("docx"), "word/document.xml"));
        Path_("pdf");
    }

    [Fact]
    public void PageBreaks_StepAndChapter()
    {
        Edit((e, s) => e["steps"] = new JsonArray(new JsonObject { ["anchor"] = s[1].Anchor, ["pageBreak"] = true }, new JsonObject { ["anchor"] = s[2].Anchor, ["chapter"] = new JsonObject { ["title"] = "K" } }));
        string html = File.ReadAllText(Path_("html"));
        Assert.Contains("id=\"s2\" style=\"break-before:page\"", html);
        int before = PdfPages(Path_("pdf"));
        Edit((e, s) => e["guide"] = new JsonObject { ["doc"] = new JsonObject { ["chapterNewPage"] = true, ["stepPerPage"] = true } });
        int after = PdfPages(Path_("pdf"));
        Assert.True(after > before, $"{before} → {after}");
        Assert.Contains("w:type=\"page\"", Entry(Path_("docx"), "word/document.xml"));
    }

    static int PdfPages(string p) => System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(p)), @"/Type /Page\b").Count;

    [Fact]
    public void LayoutTemplates_SetOnlyLayout()
    {
        var d = new DocSettings { Version = "3.0", Subtitle = "Bleibt" };
        DocLayouts.Apply(d, "manual", "de");
        Assert.True(d.ChapterNewPage && d.Figures && d.Captions == "auto");
        Assert.Contains("{{Seiten}}", d.FooterRight);
        Assert.Equal("3.0", d.Version);
        Assert.Equal("Bleibt", d.Subtitle);
        DocLayouts.Apply(d, "standard", "de");
        Assert.False(d.ChapterNewPage);
        Assert.Equal("", d.FooterRight);
        Assert.Equal(0, d.MarginMm);
        var j = d.ToJson();
        Assert.Null(j["marginMm"]); Assert.Null(j["footerRight"]);
    }

    [Fact]
    public void Defaults_NoNewKeys()
    {
        var j = new DocSettings().ToJson();
        foreach (var k in new[] { "marginMm", "headerLeft", "footerLeft", "chapterNewPage", "stepPerPage", "createdWith" }) Assert.Null(j[k]);
    }
}
