using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Stilregeln, Lesbarkeit und vollständige du-Form (13, Paket 29).</summary>
[Collection("Loc")]
public class StyleRulesTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    static IEnumerable<string> Rules(string text, string lang = "de", string address = "formal") => StyleRules.Check(text, lang, address).Select(h => h.Rule);

    [Fact]
    public void Rules_DetectTypicalProblems()
    {
        Assert.Contains("Q22", Rules(string.Join(" ", Enumerable.Repeat("Wort", 30)) + "."));
        Assert.Contains("Q23", Rules("Klicken Sie auf OK und dann auf Speichern."));
        Assert.Contains("Q24", Rules("Klicke auf deinen Namen."));
        Assert.Contains("Q24", Rules("Dann klicke auf Ihren Namen.", address: "informal"));
        Assert.DoesNotContain("Q24", Rules("Sie sehen das Fenster.", address: "informal"));   // Satzanfang: mehrdeutig
        Assert.Contains("Q25", Rules("Klicken Sie eigentlich auf OK."));
        Assert.Contains("Q43", Rules("Die Datei wird anschließend gespeichert."));
        Assert.Contains("Q43", Rules("The file is saved.", "en"));
        Assert.Empty(Rules("Klicken Sie auf Speichern."));
        Assert.Empty(Rules("Click Save.", "en"));
    }

    [Fact]
    public void Readability_Scores()
    {
        var easy = Readability.Compute(new[] { "Klicken Sie auf OK. Das Fenster geht zu. Fertig." }, "de")!;
        var hard = Readability.Compute(new[] { "Die Konfigurationsparametrisierung der Anwendungsinfrastrukturkomponenten erfordert umfangreiche Berechtigungsüberprüfungsmaßnahmen." }, "de")!;
        Assert.True(easy.Score > hard.Score);
        Assert.Equal(3, easy.Sentences);
        Assert.Equal(3, Readability.Syllables("Fenster", true) + 1);
    }

    [Fact]
    public void AutomaticTexts_TriggerNoStyleRule_InAnyAddress()
    {
        foreach (var address in new[] { "formal", "informal" })
        {
            using var v = SessionView.Open(_folder, headless: true);
            v.Edits["guide"] = new JsonObject { ["address"] = address };
            v.Rebuild();
            foreach (var s in v.GuideSteps())
            {
                var hits = StyleRules.Check(s.Guide, "de", address).Where(h => h.Rule is "Q24").ToList();
                Assert.True(hits.Count == 0, $"{address}: „{s.Guide}“");
                if (address == "informal") Assert.DoesNotMatch(@"\b(Sie|Ihr|Ihre)\b", s.Guide);
            }
        }
    }

    [Fact]
    public void QualityCheck_ReportsStyle_OnlyForAuthorTexts()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var st = v.GuideSteps().First(s => s.Kind != "move");
        v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = st.Anchor, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "T", ["body"] = "Klicke auf deinen Namen und dann auf Speichern." } } });
        v.Rebuild();
        var f = QualityCheck.Run(v);
        Assert.Contains(f, x => x.Rule == "Q23" && x.Anchor == st.Anchor);
        Assert.Contains(f, x => x.Rule == "Q24" && x.Anchor == st.Anchor);
        Assert.DoesNotContain(f, x => x.Rule is "Q22" or "Q23" or "Q24" or "Q25" or "Q43" && x.Anchor != st.Anchor);
        Assert.DoesNotContain(QualityCheck.Run(v, new QualityOptions { Style = false }), x => x.Rule == "Q23");
    }
}
