using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Export.Formats;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Barrierearmes PDF (13, Paket 32): Strukturmarkierung, Alternativtexte, Artefakte.</summary>
[Collection("Loc")]
public class TaggedPdfTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-tag-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    [Fact]
    public void SimplePdf_Tagged_StructureAndAudit()
    {
        var pdf = new SimplePdf { Title = "T", Language = "de-DE", Tagged = true, Footer = "Fuß", Watermark = "ENTWURF" };
        pdf.Paragraph("Überschrift", 17, true, 0, 4, (0, 0, 0));
        pdf.Paragraph("Ein Absatz.", 10.5, false, 0, 4, (0, 0, 0));
        pdf.FillRect(50, 50, 10, 10, (1, 0, 0));
        Assert.Empty(pdf.Audit());
        string s = System.Text.Encoding.Latin1.GetString(pdf.ToBytes());
        Assert.Contains("/MarkInfo << /Marked true >>", s);
        Assert.Contains("/StructTreeRoot", s);
        Assert.Contains("/S /H1", s);
        Assert.Contains("/S /P", s);
        Assert.Contains("/StructParents 0", s);
        Assert.Contains("/Artifact BMC", s);
        Assert.Contains("/DisplayDocTitle true", s);
    }

    [Fact]
    public void Guide_Tagged_FiguresHaveAlt_UntaggedUnchanged()
    {
        Directory.CreateDirectory(_out);
        string plain = Path.Combine(_out, "a.pdf"), tagged = Path.Combine(_out, "b.pdf");
        using (var v = SessionView.Open(_folder, headless: true)) GuideExport.Export(v, new GuideOptions { Format = "pdf", OutPath = plain });
        Assert.DoesNotContain("/StructTreeRoot", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(plain)));
        using (var v = SessionView.Open(_folder, headless: true))
        {
            var st = v.GuideSteps().First(s => s.Kind != "move");
            v.Edits["guide"] = new JsonObject { ["doc"] = new JsonObject { ["pdfTagged"] = true } };
            v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = st.Anchor, ["alt"] = new JsonObject { ["de"] = "Menü Datei geöffnet" } });
            v.Rebuild();
            GuideExport.Export(v, new GuideOptions { Format = "pdf", OutPath = tagged });
        }
        var bytes = File.ReadAllBytes(tagged);
        string s = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("/S /Figure", s);
        Assert.Contains("/Alt " + SimplePdf.PdfTextString("Menü Datei geöffnet"), s);
        PdfRasterTests.RenderAll(bytes, "tagged").ForEach(b => b.Dispose());
    }
}
