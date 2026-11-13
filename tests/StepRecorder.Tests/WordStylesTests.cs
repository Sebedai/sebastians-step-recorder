using System.IO.Compression;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Word-Profil (13, Paket 31): Zeichenformatvorlagen für Eingabe, Taste, Bedienelement und Link.</summary>
[Collection("Loc")]
public class WordStylesTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-wstyle-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    static string Entry(string docx, string name) { using var z = ZipFile.OpenRead(docx); using var r = new StreamReader(z.GetEntry(name)!.Open()); return r.ReadToEnd(); }

    [Fact]
    public void RichRuns_UseCharacterStyles_OnlyWhenUsed()
    {
        Directory.CreateDirectory(_out);
        string plain = Path.Combine(_out, "a.docx"), rich = Path.Combine(_out, "b.docx");
        using (var v = SessionView.Open(_folder, headless: true)) GuideExport.Export(v, new GuideOptions { Format = "docx", OutPath = plain, Images = false });
        Assert.DoesNotContain("SREingabe", Entry(plain, "word/styles.xml"));
        using (var v = SessionView.Open(_folder, headless: true))
        {
            var st = v.GuideSteps().First(s => s.Kind != "move");
            v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = st.Anchor, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "T", ["body"] = "Geben Sie `abc` ein, drücken Sie [[Strg+S]] und klicken Sie [[ui:OK]] – [Hilfe](https://example.org).", ["fmt"] = 1 } } });
            v.Rebuild();
            GuideExport.Export(v, new GuideOptions { Format = "docx", OutPath = rich, Images = false });
        }
        string doc = Entry(rich, "word/document.xml"), styles = Entry(rich, "word/styles.xml");
        foreach (var id in new[] { "SREingabe", "SRTaste", "SRElement", "SRLink" })
        {
            Assert.Contains($"<w:rStyle w:val=\"{id}\"/>", doc);
            Assert.Contains($"w:styleId=\"{id}\"", styles);
        }
        Assert.DoesNotContain("w:fill=\"F3F3F1\"", doc);   // Gestaltung steckt in der Vorlage, nicht im Lauf
    }
}
