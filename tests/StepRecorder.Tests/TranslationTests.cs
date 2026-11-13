using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Übersetzungsübersicht und CSV (13, Paket 33).</summary>
[Collection("Loc")]
public class TranslationTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    SessionView View()
    {
        var v = SessionView.Open(_folder, headless: true);
        var st = v.GuideSteps().First(s => s.Kind != "move");
        v.Edits["guide"] = new JsonObject { ["title"] = "Handbuch", ["i18n"] = new JsonObject { ["en"] = new JsonObject { ["title"] = "Manual" } } };
        v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = st.Anchor, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Anmelden als mmuster", ["body"] = "=Formel" } } });
        v.Edits["substitutions"] = new JsonArray(new JsonObject { ["id"] = "s1", ["match"] = new JsonObject { ["literal"] = "mmuster" }, ["placeholder"] = "Benutzer" });
        v.Edits["comments"] = new JsonArray(new JsonObject { ["id"] = "c1", ["anchor"] = st.Anchor, ["kind"] = "tip", ["text"] = "Tipp" }, new JsonObject { ["id"] = "c2", ["anchor"] = st.Anchor, ["kind"] = "internal", ["text"] = "intern" });
        v.Rebuild();
        return v;
    }

    [Fact]
    public void Rows_StatusAndProtection()
    {
        using var v = View();
        var rows = Translations.Rows(v);
        Assert.Equal("ok", rows.Single(r => r.Id == "doc:title").Status);
        var title = rows.First(r => r.Id.EndsWith(":title") && r.Id.StartsWith("step:"));
        Assert.Equal("Anmelden als {Benutzer}", title.De);
        Assert.Equal("missingEn", title.Status);
        Assert.Contains(rows, r => r.Id == "note:c1");
        Assert.DoesNotContain(rows, r => r.Id == "note:c2");   // interne Hinweise nie
    }

    [Fact]
    public void Csv_RoundTrip_FormulaGuard_ProtectedCellKept()
    {
        using var v = View();
        string csv = Translations.ToCsv(Translations.Rows(v));
        Assert.Contains("\"'=Formel\"", csv);
        Assert.DoesNotContain("mmuster", csv);
        var parsed = Translations.ParseCsv(csv);
        var titleId = parsed.Keys.First(k => k.StartsWith("step:") && k.EndsWith(":title"));
        var bodyId = parsed.Keys.First(k => k.StartsWith("step:") && k.EndsWith(":body"));
        Assert.Equal("=Formel", parsed[bodyId].de);
        // übersetzen: Englisch ergänzen, deutschen geschützten Titel „ändern“ (wird nicht übernommen)
        string edited = csv.Replace("\"Anmelden als {Benutzer}\";\"\"", "\"Anmelden als {Benutzer} NEU\";\"Sign in\"");
        var (applied, skipped) = Translations.Import(v, edited);
        Assert.Equal(1, applied);
        Assert.Equal(1, skipped);
        var se = v.Edits.Arr("steps")!.OfType<JsonObject>().First();
        Assert.Equal("Sign in", se.Obj("text")!.Obj("en")!.Str("title"));
        Assert.Equal("Anmelden als mmuster", se.Obj("text")!.Obj("de")!.Str("title"));
    }

    [Fact]
    public void Apply_AllKinds()
    {
        using var v = View();
        Translations.Apply(v.Edits, "note:c1", "en", "Tip");
        Translations.Apply(v.Edits, "doc:intro", "en", "Hello");
        long a = v.GuideSteps().First(s => s.Kind != "move").Anchor;
        Translations.Apply(v.Edits, $"chapter:{a}:title", "en", "Part");
        Assert.Equal("Tip", v.Edits.Arr("comments")![0]!.Obj("i18n")!.Obj("en")!.Str("text"));
        Assert.Equal("Hello", v.Edits.Obj("guide")!.Obj("i18n")!.Obj("en")!.Str("intro"));
        Assert.Equal("Part", v.Edits.Arr("steps")!.OfType<JsonObject>().First().Obj("chapter")!.Obj("en")!.Str("title"));
    }
}
