using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Compare;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Paket 19 des Entwickler-Fahrplans: visueller Vorher/Nachher-Vergleich.</summary>
public class VisualCompareTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-vc-" + Guid.NewGuid().ToString("N")[..8]);
    public VisualCompareTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    static Bitmap Canvas(Action<Graphics>? draw = null)
    {
        var b = new Bitmap(400, 300);
        using var g = Graphics.FromImage(b);
        g.Clear(Color.White);
        using (var f = new Font("Segoe UI", 12)) g.DrawString("Datei  Bearbeiten  Ansicht", f, Brushes.Black, 8, 8);
        draw?.Invoke(g);
        return b;
    }

    [Fact]
    public void ImageDiff_FindsChangedRegion_IgnoresIdenticalAndMasked()
    {
        using var a = Canvas();
        using var same = Canvas();
        Assert.Equal(0, ImageDiff.Diff(a, same).ChangedRatio);
        using var b = Canvas(g => g.FillRectangle(Brushes.Red, 200, 150, 60, 40));
        var d = ImageDiff.Diff(a, b);
        Assert.True(d.ChangedRatio > 0.005);
        var r = Assert.Single(d.Regions);
        Assert.True(r.Contains(new Rectangle(200, 150, 60, 40)), r.ToString());
        Assert.True(r.Width < 120 && r.Height < 100, r.ToString());                 // eng um die Änderung
        // Maske über dem geänderten Bereich (z. B. Uhr) → keine Änderung
        var masked = ImageDiff.Diff(a, b, new ImageDiffOptions { Masks = { new RectangleF(0.45f, 0.45f, 0.25f, 0.25f) } });
        Assert.Empty(masked.Regions);
        // Kantenglättung: minimale Farbabweichung unter der Schwelle zählt nicht
        using var faint = Canvas(g => { using var br = new SolidBrush(Color.FromArgb(250, 250, 250)); g.FillRectangle(br, 50, 50, 300, 200); });
        Assert.Empty(ImageDiff.Diff(a, faint).Regions);
        // Ergebnis ist deterministisch
        Assert.Equal(d.ToJson().ToJsonString(), ImageDiff.Diff(a, b).ToJson().ToJsonString());
    }

    [Fact]
    public void SideBySide_HasBothHalvesAndLabels()
    {
        using var a = Canvas();
        using var b = Canvas(g => g.FillRectangle(Brushes.Red, 200, 150, 60, 40));
        var d = ImageDiff.Diff(a, b);
        using var pair = ImageDiff.SideBySide(a, b, "A 1. Datei", "B 1. Datei", d.Regions, d.Size);
        Assert.True(pair.Width >= 800 && pair.Height > 300);
        using var onlyB = ImageDiff.SideBySide(null, b, "A –", "B 2. Neu");
        Assert.True(onlyB.Width > b.Width);
    }

    [Fact]
    public void Compare_WithImages_SameRecording_HasNoImageDifference()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"));
        var b = SyntheticSession.Create(Path.Combine(_root, "b"));
        using var va = SessionView.Open(a, headless: true);
        using var vb = SessionView.Open(b, headless: true);
        var plain = StepCompare.Compare(va, vb);
        var r = StepCompare.Compare(va, vb, images: true);
        Assert.True(r.Obj("params")!.Bool("images"));
        Assert.Equal(plain.Obj("summary")!.ToJsonString(), r.Obj("summary")!.ToJsonString());
        foreach (var p in r.Arr("pairs")!.OfType<JsonObject>().Where(p => p.Str("status") is "same" or "changed"))
        {
            Assert.DoesNotContain("image", p.StrList("differences"));
            Assert.True(p.Obj("imageDiff") != null || p.StrList("unchecked").Contains("image"));
        }
        Assert.Equal(r.ToJsonString(), StepCompare.Compare(va, vb, images: true).ToJsonString());   // gleiche Eingabe, gleiches Ergebnis
    }

    [Fact]
    public void MarkdownWithImages_WritesPairImages_ForDifferences()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a2"));
        var b = SyntheticSession.Create(Path.Combine(_root, "b2"), withHotkey: true);
        using var va = SessionView.Open(a, headless: true);
        using var vb = SessionView.Open(b, headless: true);
        var r = StepCompare.Compare(va, vb, images: true);
        Assert.Contains(r.Arr("pairs")!.OfType<JsonObject>(), p => p.Str("status") == "onlyB");
        string md = Path.Combine(_root, "out", "vergleich.md");
        var files = StepCompare.WriteMarkdownWithImages(r, va, vb, md, "de");
        Assert.Equal(Path.GetFullPath(md), files[0]);
        Assert.True(files.Count >= 2);
        string text = File.ReadAllText(md);
        Assert.Contains("## Bilder", text);
        Assert.Contains("nur in B", text);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\]\((vergleich-bilder/paar-\d{3}\.png)\)"))
            Assert.True(File.Exists(Path.Combine(_root, "out", m.Groups[1].Value.Replace('/', '\\'))));
    }
}
