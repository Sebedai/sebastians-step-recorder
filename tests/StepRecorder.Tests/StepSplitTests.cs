using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Schritt teilen und Ergebnisschritt (13, Paket 34).</summary>
[Collection("Loc")]
public class StepSplitTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    [Fact]
    public void Split_KeepsRecording_NewStepAfterWithAfterImage()
    {
        using var v = SessionView.Open(_folder, headless: true);
        int before = v.GuideSteps().Count;
        var st = v.GuideSteps().First(s => s.Kind == "type");
        int aiSteps = v.Effective().Count;
        long a = StepSplit.Split(v.Edits, st, "de", "Name eingeben", "Geben Sie den Namen ein.", "Bestätigen", "Drücken Sie die Eingabetaste.");
        v.Rebuild();
        var steps = v.GuideSteps().Where(s => s.Kind != "move").ToList();
        Assert.Equal(before + 1, v.GuideSteps().Count);
        int i = steps.FindIndex(s => s.Anchor == st.Anchor);
        Assert.Equal(a, steps[i + 1].Anchor);
        Assert.Equal("Name eingeben", steps[i].AuthorTitle);
        Assert.Equal("Bestätigen", steps[i + 1].Title);
        Assert.Equal(st.PostFrame ?? st.PreFrame, steps[i + 1].PreFrame);
        Assert.Equal(aiSteps, v.Effective().Count);   // KI-Sicht: aufgezeichnete Schritte unverändert
        // Nummern rücken nach, Bild vorhanden
        var nums = GuideExport.StepNumbers(v);
        Assert.Equal((i + 2).ToString(), nums[a]);
        using var img = StepImages.Render(v, steps[i + 1], "before", "compatible");
        Assert.NotNull(img);
    }

    [Fact]
    public void Result_InsertsAfterImageStep()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var st = v.GuideSteps().First(s => s.Kind == "click" && s.PostFrame != null);
        long a = StepSplit.Result(v.Edits, st, "Ergebnis", "");
        v.Rebuild();
        var r = v.GuideSteps().Single(s => s.Anchor == a);
        Assert.Equal(st.PostFrame, r.PreFrame);
        Assert.True(r.Manual);
        Assert.Equal(("eins.", "zwei"), StepSplit.SplitText("eins. zwei", 5));
    }
}
