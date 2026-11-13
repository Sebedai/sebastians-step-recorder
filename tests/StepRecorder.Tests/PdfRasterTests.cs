using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>
/// VE-24: Der eingebaute PDF-Renderer öffnet die erzeugten PDFs und rastert ihre Seiten. Mit <c>STEPRECORDER_PDF_PNG=&lt;Ordner&gt;</c>
/// werden die Seiten zusätzlich als PNG abgelegt (zum Ansehen beim Entwickeln).
/// </summary>
[Collection("Loc")]
public class PdfRasterTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create(withComment: true);
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-raster-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    /// <summary>Rendert alle Seiten eines PDFs; legt sie bei gesetzter Umgebungsvariable als PNG ab. Liefert die Seitenbilder.</summary>
    public static List<Bitmap> RenderAll(byte[] pdf, string name, int width = 600)
    {
        var list = new List<Bitmap>();
        if (!PdfRaster.Available) return list;
        using var r = PdfRaster.Open(pdf);
        string? dump = Environment.GetEnvironmentVariable("STEPRECORDER_PDF_PNG");
        for (int i = 0; i < r.PageCount; i++)
        {
            var png = r.RenderPng(i, width);
            if (dump is { Length: > 0 }) { Directory.CreateDirectory(dump); File.WriteAllBytes(Path.Combine(dump, $"{name}-{i + 1}.png"), png); }
            using var ms = new MemoryStream(png);
            list.Add(new Bitmap(ms));
        }
        return list;
    }

    [Fact]
    public void RenderPdf_MapsBlocks_UsesCache_AndRespectsRedactions()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var steps = v.GuideSteps("primary").Where(s => s.Kind != "move").ToList();
        v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = steps[1].Anchor, ["chapter"] = new JsonObject { ["title"] = "Teil 2" } });
        v.Rebuild();
        var cache = new GuideImageCache();
        var a = GuideExport.RenderPdf(v, new GuideOptions { ImageCache = cache });
        Assert.Equal(steps.Count, a.Blocks.Count(b => b.Key.StartsWith("s:")));
        Assert.Contains(a.Blocks, b => b.Key == "c:" + steps[1].Anchor);
        Assert.All(a.Blocks, b => Assert.InRange(b.Top, 0, a.PageH));
        Assert.Equal("s:" + steps[0].Anchor, a.BlockAt(a.Blocks.First(b => b.Key == "s:" + steps[0].Anchor).Page, a.Blocks.First(b => b.Key == "s:" + steps[0].Anchor).Top + 5)!.Key);
        int misses = cache.Misses;
        var b2 = GuideExport.RenderPdf(v, new GuideOptions { ImageCache = cache });
        Assert.Equal(misses, cache.Misses);                 // zweiter Aufbau ganz aus dem Speicher
        Assert.Equal(a.Pdf, b2.Pdf);                        // und bytegleich
        // Schwärzung ändert den Fingerabdruck: kein altes Bild wird wiederverwendet
        v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "image", ["style"] = "black", ["areas"] = new JsonArray(new JsonObject { ["m"] = "M1", ["from"] = "f1", ["rect"] = new JsonArray(0, 0, 200, 100) }) });
        v.Rebuild();
        GuideExport.RenderPdf(v, new GuideOptions { ImageCache = cache });
        Assert.True(cache.Misses > misses);
        // Export-Sprache der Ansicht bleibt erhalten
        Assert.Equal("de", v.Language);
    }

    [Fact]
    public void GeneratedPdf_OpensAndRenders()
    {
        Assert.True(PdfRaster.Available, "Windows.Data.Pdf ist auf diesem Rechner nicht verfügbar");
        using var v = SessionView.Open(_folder, headless: true);
        v.Edits["guide"] = new JsonObject { ["title"] = "Raster", ["doc"] = new DocSettings { Cover = true, Footer = "Fuß" }.ToJson() };
        v.Rebuild();
        Directory.CreateDirectory(_out);
        var res = GuideExport.Export(v, new GuideOptions { Format = "pdf", OutPath = Path.Combine(_out, "a.pdf") });
        var bytes = File.ReadAllBytes(res.Path!);
        using var r = PdfRaster.Open(bytes);
        Assert.True(r.PageCount >= 2);
        var (w, h) = r.PageSize(0);
        Assert.InRange(w, 594, 597); Assert.InRange(h, 840, 843);
        var pages = RenderAll(bytes, "raster");
        try
        {
            // Deckblatt: Akzentbalken oben ist farbig, die Mitte weiß
            var cover = pages[0];
            var top = cover.GetPixel(cover.Width / 2, 3);
            Assert.True(top.R > 150 && top.B < 100, "Akzentbalken fehlt: " + top);
            var white = cover.GetPixel(cover.Width - 10, cover.Height / 2);
            Assert.True(white.R > 240 && white.G > 240 && white.B > 240);
        }
        finally { foreach (var p in pages) p.Dispose(); }
    }
}
