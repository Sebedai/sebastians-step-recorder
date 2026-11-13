using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Kapitelvorschläge (13, Paket 37).</summary>
[Collection("Loc")]
public class ChapterSuggestTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    [Fact]
    public void Suggests_AtProgramChange_WithTitles_AndApplies()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var steps = v.GuideSteps().Where(s => s.Kind != "move").ToList();
        Assert.True(steps.Count >= 4);
        for (int i = 0; i < steps.Count; i++)
            steps[i].Json["ctx"] = i < 2
                ? new JsonObject { ["process"] = "notepad.exe", ["title"] = "Unbenannt - Editor" }
                : new JsonObject { ["process"] = "excel.exe", ["title"] = "Mappe1 - Excel" };
        var sug = ChapterSuggest.Suggest(v);
        Assert.Equal(2, sug.Count);
        Assert.Equal("Editor", sug[0].Title);
        Assert.Equal("Excel", sug[1].Title);
        Assert.Equal(steps[2].Anchor, sug[1].Anchor);
        ChapterSuggest.Apply(v.Edits, sug, "de");
        v.Rebuild();
        Assert.Equal(2, v.GuideSteps().Count(s => s.ChapterTitle != null));
    }

    [Fact]
    public void SingleProgram_NoChapters_SuggestsOne_AndShortRunsMerge()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var steps = v.GuideSteps().Where(s => s.Kind != "move").ToList();
        for (int i = 0; i < steps.Count; i++)
            steps[i].Json["ctx"] = new JsonObject { ["process"] = i == 1 ? "calc.exe" : "notepad.exe" };
        var sug = ChapterSuggest.Suggest(v);
        Assert.Single(sug);   // der Ein-Schritt-Ausflug zählt nicht
        Assert.Equal("Notepad", sug[0].Title);
    }
}
