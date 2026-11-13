using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Fertigstellung, Freigabe und Wasserzeichen (13, Paket 30).</summary>
[Collection("Loc")]
public class GuideStatusTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-status-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    string Pdf(SessionView v)
    {
        Directory.CreateDirectory(_out);
        string p = Path.Combine(_out, Guid.NewGuid().ToString("N")[..6] + ".pdf");
        GuideExport.Export(v, new GuideOptions { Format = "pdf", OutPath = p, Images = false });
        return System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(p));
    }

    [Fact]
    public void NoStatus_NoWatermark_DraftHasOne()
    {
        using var v = SessionView.Open(_folder, headless: true);
        Assert.Equal("none", GuideStatus.State(v));
        Assert.DoesNotContain("(ENTWURF)", Pdf(v));
        GuideStatus.SetDraft(v.Edits); v.Rebuild();
        Assert.Equal("draft", GuideStatus.State(v));
        string draft = Pdf(v);
        Assert.Contains("(ENTWURF)", draft);
        using (var pages = new DisposableList(PdfRasterTests.RenderAll(System.Text.Encoding.Latin1.GetBytes(draft), "watermark"))) Assert.NotEmpty(pages.Items);
        v.Edits.Obj("guide")!["doc"] = new JsonObject { ["watermark"] = "off" };
        Assert.DoesNotContain("(ENTWURF)", Pdf(v));
    }

    [Fact]
    public void Release_RequiresMandatoryItems_AndResetsOnChange()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var items = GuideStatus.Readiness(v);
        Assert.False(items.Single(i => i.Id == "title").Done);
        Assert.False(GuideStatus.Release(v, items));
        v.Edits["guide"] = new JsonObject { ["title"] = "Fertige Anleitung" };
        // ungeprüfte Eingaben gelten als erledigt, sobald durchgesehen; hier alle Pflichtpunkte erzwingen
        var forced = GuideStatus.Readiness(v).Select(i => i with { Done = true }).ToList();
        Assert.True(GuideStatus.Release(v, forced));
        Assert.Equal("released", GuideStatus.State(v));
        Assert.DoesNotContain("(ENTWURF)", Pdf(v));
        v.Edits["guide"]!["intro"] = "Neu";
        Assert.Equal("changed", GuideStatus.State(v));
        Assert.True(GuideStatus.Watermark(v));
        // flüchtige Schlüssel ändern nichts
        v.Edits["guide"]!.AsObject().Remove("intro");
        v.Edits["revision"] = 99; v.Edits["savedAtUtc"] = "x";
        Assert.Equal("released", GuideStatus.State(v));
    }

    sealed class DisposableList(List<System.Drawing.Bitmap> items) : IDisposable
    {
        public List<System.Drawing.Bitmap> Items => items;
        public void Dispose() { foreach (var b in items) b.Dispose(); }
    }
}
