using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

public class EditorFeatureTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _folder2 = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-feat-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { foreach (var d in new[] { _folder, _folder2, _out }) try { Directory.Delete(d, true); } catch { } }

    static Bitmap Solid(Color c, int w = 120, int h = 80) { var b = new Bitmap(w, h); using var g = Graphics.FromImage(b); g.Clear(c); return b; }

    [Fact]
    public void ManualSteps_AppearAfterAnchor_OnlyWhenIncluded()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var first = v.Effective(guide: true, importance: "primary").First();
        string img; using (var b = Solid(Color.Red)) img = v.AddAsset(b);
        v.Edits["inserted"] = new JsonArray(new JsonObject { ["anchor"] = -1001, ["after"] = first.Anchor, ["title"] = "Eigener Schritt", ["body"] = "Text", ["image"] = img });
        v.Rebuild();
        Assert.DoesNotContain(v.Effective(guide: true, importance: "primary"), s => s.Manual);
        var list = v.Effective(guide: true, importance: "primary", includeManual: true);
        int i = list.FindIndex(s => s.Anchor == first.Anchor);
        Assert.True(list[i + 1].Manual);
        Assert.Equal("Eigener Schritt", list[i + 1].Title);
        Assert.DoesNotContain(v.All, s => s.Manual);   // KI, AutoHotkey, MCP sehen eingefügte Schritte nicht
        using var r = StepImages.Render(v, list[i + 1], "before", "original");
        Assert.NotNull(r);
        Assert.Equal(Color.Red.ToArgb(), r!.Bitmap.GetPixel(10, 10).ToArgb());
    }

    [Fact]
    public void ReplacedImage_IsUsed_AndRedactionOnlyHitsThatImage()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var click = v.All.First(s => s.Kind == "click");
        string img; using (var b = Solid(Color.Blue)) img = v.AddAsset(b);
        v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = click.Anchor, ["frameChoice"] = new JsonObject { ["pre"] = img, ["post"] = img } });
        v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "image", ["areas"] = new JsonArray(new JsonObject { ["m"] = img, ["from"] = img, ["to"] = img, ["rect"] = new JsonArray(0, 0, 20, 20) }) });
        v.Rebuild();
        var s = v.All.First(x => x.Anchor == click.Anchor);
        Assert.True(s.ImageReplaced);
        using var bmp = v.Frame(s.PreFrame)!;
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(5, 5).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bmp.GetPixel(60, 60).ToArgb());
    }

    [Fact]
    public void CropWithOverview_DrawsInset()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var click = v.All.First(s => s.Kind == "click");
        using var plain = StepImages.Render(v, click, "crop", "original");
        using var withOv = StepImages.Render(v, click, "crop", "original", overview: true);
        Assert.NotNull(plain); Assert.NotNull(withOv);
        Assert.Equal(plain!.Bitmap.Size, withOv!.Bitmap.Size);
    }

    [Fact]
    public void SensitiveSpans_FindsEmailIbanPhone()
    {
        var spans = Patterns.SensitiveSpans("Kontakt: max.mustermann@firma.de, Tel. +49 30 1234567, IBAN DE89 3704 0044 0532 0130 00");
        Assert.Contains(spans, x => x.kind == "email");
        Assert.Contains(spans, x => x.kind == "phone");
        Assert.Contains(spans, x => x.kind == "iban");
        Assert.Empty(Patterns.SensitiveSpans("Version 1.2.3 vom 12.11.2026"));
    }

    [Fact]
    public void Export_IncludesManualStep_Appended_Logo_AndEnglishTexts()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var first = v.Effective(guide: true, importance: "primary").First();
        string logo; using (var b = Solid(Color.Orange, 60, 30)) logo = DocSettings.LogoFrom(b);
        v.Edits["inserted"] = new JsonArray(new JsonObject { ["anchor"] = -1001, ["after"] = first.Anchor, ["title"] = "Warten Sie auf die Mail" });
        v.Edits["guide"] = new JsonObject
        {
            ["title"] = "Anleitung", ["intro"] = "Deutsche Einleitung",
            ["i18n"] = new JsonObject { ["en"] = new JsonObject { ["title"] = "Manual", ["intro"] = "English intro", ["subtitle"] = "Quick guide" } },
            ["doc"] = new DocSettings { Logo = logo, Subtitle = "Kurzanleitung" }.ToJson(),
            ["append"] = new JsonArray(new JsonObject { ["path"] = _folder2 })
        };
        v.Rebuild();
        var de = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "html", OutPath = Path.Combine(_out, "de.html") }).Path!);
        Assert.Contains("Warten Sie auf die Mail", de);
        Assert.Contains("class=\"logo\"", de);
        Assert.Contains("Deutsche Einleitung", de);
        Assert.Contains("class=\"chapter\"", de);           // angehängte Aufnahme als Kapitel
        var en = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "html", Language = "en", OutPath = Path.Combine(_out, "en.html") }).Path!);
        Assert.Contains("English intro", en);
        Assert.Contains("Quick guide", en);
        Assert.Contains("<h1>Manual</h1>", en);
    }

    [Fact]
    public void Ocr_FindsEmailInScreenshotText()
    {
        if (!Ocr.Available) return;   // ohne OCR-Sprachpaket nicht prüfbar
        using var bmp = new Bitmap(700, 120);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var f = new Font("Segoe UI", 15, GraphicsUnit.Pixel);
            g.DrawString("Absender: max.mustermann@firma.de", f, Brushes.Black, 20, 30);
            g.DrawString("Betreff: Rechnung", f, Brushes.Black, 20, 70);
        }
        var hits = Ocr.FindSensitive(bmp);
        Assert.Contains(hits, h => h.kind == "email" && h.rect.X > 60 && h.rect.Y < 60);
        Assert.DoesNotContain(hits, h => h.rect.Y > 60);
    }

    [Fact]
    public void Gif_IsWritten_WithTitleAndStepFrames()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var r = GuideExport.Export(v, new GuideOptions { Format = "gif", OutPath = Path.Combine(_out, "a.gif"), GifSeconds = 1 });
        using var img = Image.FromFile(r.Path!);
        int frames = img.GetFrameCount(System.Drawing.Imaging.FrameDimension.Time);
        Assert.True(frames >= 2, $"nur {frames} Bilder");
    }
}
