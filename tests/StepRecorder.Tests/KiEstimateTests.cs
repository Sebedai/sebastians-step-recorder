using System.Security.Cryptography;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Tokenschätzung vor dem KI-Export: Maße wie beim Rendern, Schätzung wie Export, nichts geschrieben.</summary>
[Collection("Loc")]
public class KiEstimateTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-est-" + Guid.NewGuid().ToString("N")[..8]);
    public KiEstimateTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void Measure_EqualsRender_ForAllStepsAndVariants()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"));
        using var v = SessionView.Open(a, headless: true);
        int compared = 0;
        foreach (var s in v.All)
            foreach (var variant in new[] { "before", "after", "crop", "cropEnd" })
                foreach (var profile in new[] { "compatible", "original" })
                {
                    var m = StepImages.Measure(v, s, variant, profile, "screen", annotate: true, respectMarks: false);
                    using var r = StepImages.Render(v, s, variant, profile, annotate: true, viewMode: "screen", respectMarks: false);
                    Assert.Equal(r == null, m == null);
                    if (r == null) continue;
                    Assert.Equal((r.Bitmap.Width, r.Bitmap.Height, r.VisualTokens), (m!.Width, m.Height, m.VisualTokens));
                    compared++;
                }
        Assert.True(compared > 5);
    }

    static string Snapshot(string folder) => string.Join("\n", Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f)
        .Select(f => f + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))));

    [Theory]
    [InlineData("compact")]
    [InlineData("standard")]
    [InlineData("full")]
    public void Estimate_MatchesExport_AndWritesNothing(string detail)
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"));
        using (SessionView.Open(a)) { }                                // Schritte einmal ableiten (steps.json)
        string before = Snapshot(a);
        KiEstimate est;
        using (var v = SessionView.Open(a, headless: true)) est = KiPackage.Estimate(v, new KiOptions { Detail = detail });
        Assert.Equal(before, Snapshot(a));
        ExportResult r;
        using (var v = SessionView.Open(a, headless: true)) r = KiPackage.Export(v, new KiOptions { Detail = detail, OutDir = Path.Combine(_root, "ki-" + detail) });
        Assert.Equal(r.ImageTokens, est.ImageTokens);
        Assert.Equal(Directory.GetFiles(Path.Combine(r.Path, "images")).Length, est.Images);
        Assert.InRange(est.TextTokens, r.TextTokens * 0.8, r.TextTokens * 1.25);
    }
}
