using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.Tests;

/// <summary>Alltagskomfort: Fensterlage, Suche im Editor, Reihenfolge der Schrittliste.</summary>
public class ComfortTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-comfort-" + Guid.NewGuid().ToString("N")[..8]);
    public ComfortTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    static readonly WindowPlacement.Monitor Primary = new(new Rectangle(0, 0, 1920, 1040), 96, true);
    static readonly WindowPlacement.Monitor Right150 = new(new Rectangle(1920, 0, 2560, 1400), 144, false);

    // ---------------- Fensterlage ----------------

    [Fact]
    public void Placement_SameMonitor_Unchanged()
    {
        var b = WindowPlacement.Fit(new WindowBounds { X = 100, Y = 80, W = 1200, H = 800, Dpi = 96 }, new[] { Primary, Right150 })!;
        Assert.Equal((100, 80, 1200, 800, 96), (b.X, b.Y, b.W, b.H, b.Dpi));
    }

    [Fact]
    public void Placement_PartlyOutside_ClampedIntoWorkArea()
    {
        var b = WindowPlacement.Fit(new WindowBounds { X = 1500, Y = 700, W = 800, H = 600, Dpi = 96 }, new[] { Primary })!;
        Assert.Equal(1920 - 800, b.X);
        Assert.Equal(1040 - 600, b.Y);
    }

    [Fact]
    public void Placement_MonitorGone_CenteredOnPrimary()
    {
        var b = WindowPlacement.Fit(new WindowBounds { X = 3000, Y = 100, W = 1000, H = 600, Dpi = 144, Max = true }, new[] { Primary })!;
        Assert.Equal(96, b.Dpi);
        Assert.Equal(667, b.W);                    // 1000 px bei 150 % = 667 px bei 100 %
        Assert.Equal((1920 - b.W) / 2, b.X);
        Assert.True(b.Max);
    }

    [Fact]
    public void Placement_LargestOverlapWins_AndScalesWithDpi()
    {
        // überwiegend auf dem rechten Monitor, gespeichert noch mit 100 %
        var b = WindowPlacement.Fit(new WindowBounds { X = 1800, Y = 100, W = 1000, H = 600, Dpi = 96 }, new[] { Primary, Right150 })!;
        Assert.Equal(144, b.Dpi);
        Assert.Equal(1500, b.W);
        Assert.Equal(900, b.H);
        Assert.True(b.X >= 1920);
    }

    [Fact]
    public void Placement_TooLarge_ShrinksToWorkArea_ButKeepsMinimum()
    {
        var b = WindowPlacement.Fit(new WindowBounds { X = -50, Y = -50, W = 5000, H = 3000, Dpi = 96 }, new[] { Primary }, 760, 520)!;
        Assert.Equal((0, 0, 1920, 1040), (b.X, b.Y, b.W, b.H));
        var tiny = WindowPlacement.Fit(new WindowBounds { X = 10, Y = 10, W = 100, H = 80, Dpi = 96 }, new[] { Primary }, 760, 520)!;
        Assert.Equal((760, 520), (tiny.W, tiny.H));
        Assert.Null(WindowPlacement.Fit(null, new[] { Primary }));
        Assert.Null(WindowPlacement.Fit(new WindowBounds { W = 0, H = 0 }, new[] { Primary }));
    }

    // ---------------- Suche ----------------

    [Fact]
    public void SearchQuery_SplitsWordsAndPhrases()
    {
        Assert.Equal(new[] { "datei", "im editor", "speichern" }, SearchQuery.Parse("datei „im   editor“ speichern").Terms);
        Assert.Equal(new[] { "a b" }, SearchQuery.Parse("\"a b\"").Terms);
        Assert.True(SearchQuery.Parse("   ").IsEmpty);
    }

    [Fact]
    public void StepSearch_AllTermsMustMatch_AcrossFields()
    {
        var s = new ViewStep { Title = "Klicken Sie auf „Speichern“.", Comments = { new JsonObject { ["text"] = "Achtung: Netzlaufwerk" } }, ChapterTitle = "Einrichten" };
        Assert.True(StepSearch.Matches(s, SearchQuery.Parse("speichern netzlaufwerk")));
        Assert.True(StepSearch.Matches(s, SearchQuery.Parse("einrichten")));
        Assert.False(StepSearch.Matches(s, SearchQuery.Parse("speichern drucken")));
        Assert.True(StepSearch.Matches(s, SearchQuery.Parse("")));
        Assert.True(StepSearch.Matches(s, SearchQuery.Parse("\"klicken sie\"")));
        Assert.False(StepSearch.Matches(s, SearchQuery.Parse("\"sie klicken\"")));
    }

    // ---------------- Reihenfolge ----------------

    [Fact]
    public void Ordered_FollowsGuideOrder_AndKeepsHiddenSteps()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "s1"));
        long a, b;
        using (var v = SessionView.Open(folder))
        {
            var all = v.Effective(importance: "all");
            Assert.True(all.Count >= 3);
            a = all[0].Anchor; b = all[1].Anchor;
            var order = new JsonArray(all.Select(x => (JsonNode)x.Anchor).ToArray());
            order.RemoveAt(0); order.Insert(1, a);                                  // a hinter b
            v.Edits["order"] = order;
            v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = b, ["hidden"] = true });
            Assert.True(v.SaveEdits());
        }
        using (var v = SessionView.Open(folder))
        {
            var chrono = v.Effective(importance: "all");
            var ordered = v.Effective(importance: "all", ordered: true);
            var guide = v.Effective(guide: true, importance: "all");
            Assert.Equal(a, chrono[0].Anchor);                                    // ohne Reihenfolge: Aufnahme
            Assert.Equal(new[] { b, a }, ordered.Take(2).Select(x => x.Anchor));  // Liste: wie die Anleitung, Ausgeblendete bleiben
            Assert.True(ordered[0].Hidden);
            Assert.Equal(a, guide[0].Anchor);                                     // Anleitung: ausgeblendeter Schritt fehlt
            Assert.Equal(chrono.Count, ordered.Count);
        }
    }
}
