using System.IO.Compression;
using System.Text.Json.Nodes;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Formatierter Text in allen Anleitungsformaten (13, Paket 21).</summary>
[Collection("Loc")]
public class RichExportTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-richexp-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    const string Body = "Wählen Sie **Speichern unter** und *dann* `notiz.txt`.\nMit [[Strg+S]] geht es schneller – siehe [Hilfe](https://example.org/hilfe).\n\n- erstens [[ui:OK]]\n- zweitens";

    SessionView Prepared()
    {
        var v = SessionView.Open(_folder, headless: true);
        var steps = v.GuideSteps("primary").Where(s => s.Kind != "move").ToList();
        v.Edits["steps"] = new JsonArray(
            new JsonObject { ["anchor"] = steps[0].Anchor, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Speichern", ["body"] = Body, ["fmt"] = 1 } },
                ["chapter"] = new JsonObject { ["title"] = "Teil", ["text"] = "Kapitel mit **fett**", ["fmt"] = 1 } });
        v.Edits["comments"] = new JsonArray(new JsonObject { ["id"] = "uc1", ["anchor"] = steps[0].Anchor, ["kind"] = "tip", ["text"] = "Tipp mit **fett** und\n1. eins\n2. zwei", ["fmt"] = 1 });
        v.Edits["guide"] = new JsonObject { ["title"] = "Formatiert", ["intro"] = "Einleitung *kursiv*", ["outro"] = "Ende **fett**", ["fmt"] = new JsonObject { ["intro"] = 1, ["outro"] = 1 } };
        v.Rebuild();
        return v;
    }

    string Export(SessionView v, string f)
    {
        Directory.CreateDirectory(_out);
        string path = Path.Combine(_out, "g." + (f is "md" ? "md" : f == "tour" ? "html" : f));
        return GuideExport.Export(v, new GuideOptions { Format = f, OutPath = path }).Path!;
    }

    [Fact]
    public void Html_RendersMarkup()
    {
        using var v = Prepared();
        var html = File.ReadAllText(Export(v, "html"));
        Assert.Contains("<strong>Speichern unter</strong>", html);
        Assert.Contains("<em>dann</em>", html);
        Assert.Contains("<code>notiz.txt</code>", html);
        Assert.Contains("<kbd>Strg</kbd>+<kbd>S</kbd>", html);
        Assert.Contains("<a href=\"https://example.org/hilfe\" rel=\"noopener noreferrer\">Hilfe</a>", html);
        Assert.Contains("<ul><li>erstens <b class=\"ui\">OK</b></li><li>zweitens</li></ul>", html);
        Assert.Contains("<ol><li>eins</li><li>zwei</li></ol>", html);
        Assert.Contains("Einleitung <em>kursiv</em>", html);
        Assert.Contains("Ende <strong>fett</strong>", html);
        Assert.Contains("Kapitel mit <strong>fett</strong>", html);
        Assert.Contains("kbd{", html);          // Stile nur bei formatiertem Text
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("**", html);
    }

    [Fact]
    public void Markdown_Docx_Pdf_RenderMarkup()
    {
        using var v = Prepared();
        var md = File.ReadAllText(Export(v, "md"));
        Assert.Contains("**Speichern unter**", md);
        Assert.Contains("`notiz.txt`", md);
        Assert.Contains("[Hilfe](https://example.org/hilfe)", md);
        Assert.Contains("- erstens **OK**", md);

        using (var z = ZipFile.OpenRead(Export(v, "docx")))
        {
            string doc = new StreamReader(z.GetEntry("word/document.xml")!.Open()).ReadToEnd();
            string rels = new StreamReader(z.GetEntry("word/_rels/document.xml.rels")!.Open()).ReadToEnd();
            Assert.Contains("<w:b/></w:rPr><w:t xml:space=\"preserve\">Speichern unter</w:t>", doc);
            Assert.Contains("<w:hyperlink r:id=\"rIdLink1\"", doc);
            Assert.Contains("Target=\"https://example.org/hilfe\" TargetMode=\"External\"", rels);
            Assert.Contains("<w:i/>", doc);
        }

        var pdf = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(Export(v, "pdf")));
        Assert.Contains("/Helvetica-Oblique", pdf);
        Assert.Contains("/Courier", pdf);
        Assert.Contains("/S /URI /URI (https://example.org/hilfe)", pdf);
        Assert.DoesNotContain("**", pdf);
        PdfRasterTests.RenderAll(File.ReadAllBytes(Path.Combine(_out, "g.pdf")), "rich").ForEach(b => b.Dispose());
    }

    [Fact]
    public void Tour_Pptx_KiAndSearch_UsePlainOrRich()
    {
        using var v = Prepared();
        Assert.Contains("<strong>Speichern unter</strong>", File.ReadAllText(Export(v, "tour")));
        using (var z = ZipFile.OpenRead(Export(v, "pptx")))
        {
            string all = string.Concat(z.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide")).Select(e => new StreamReader(e.Open()).ReadToEnd()));
            Assert.Contains("• erstens OK", all);
            Assert.DoesNotContain("**", all);
        }
        var st = v.GuideSteps("primary").First(s => s.BodyRich != null);
        Assert.StartsWith("Wählen Sie Speichern unter und dann notiz.txt.", st.AuthorBody);
    }
}
