using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Bildunterschriften, Abbildungsverzeichnis, Alternativtext, Warnhinweise mit Signalwort, Hinweise je Sprache (13, Paket 25).</summary>
[Collection("Loc")]
public class FiguresTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-fig-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    SessionView View(Action<JsonObject, List<ViewStep>> edit, string lang = "de")
    {
        using (var v0 = SessionView.Open(_folder, headless: true))
        {
            edit(v0.Edits, v0.GuideSteps().Where(s => s.Kind != "move").ToList());
            Assert.True(v0.SaveEdits());
        }
        return SessionView.Open(_folder, headless: true, lang: lang);
    }

    string Export(SessionView v, string fmt)
    {
        Directory.CreateDirectory(_out);
        string p = Path.Combine(_out, Guid.NewGuid().ToString("N")[..6] + "." + fmt);
        GuideExport.Export(v, new GuideOptions { Format = fmt, OutPath = p, Language = v.Language });
        return fmt == "docx" ? LegacyOutputTests.Normalize(fmt, p) : System.Net.WebUtility.HtmlDecode(File.ReadAllText(p));
    }

    [Fact]
    public void Captions_AutoAndCustom_WithFigureList()
    {
        long a0 = 0;
        using var v = View((e, s) =>
        {
            a0 = s[0].Anchor;
            e["guide"] = new JsonObject { ["doc"] = new JsonObject { ["captions"] = "custom", ["figures"] = true } };
            e["steps"] = new JsonArray(new JsonObject { ["anchor"] = s[0].Anchor, ["caption"] = new JsonObject { ["de"] = "Das Startmenü", ["en"] = "The start menu" }, ["alt"] = new JsonObject { ["de"] = "Startmenü mit Suchfeld" } });
        });
        string html = Export(v, "html");
        var caps = string.Join("\n", System.Text.RegularExpressions.Regex.Matches(html, "<figcaption[^<]*</figcaption>|<h3>.*?</h3>").Select(m => m.Value));
        Assert.True(html.Contains("Abb. 1: Das Startmenü</figcaption>"), caps);
        Assert.Contains("alt=\"Startmenü mit Suchfeld\"", html);
        Assert.Contains("id=\"m-figures\"", html);
        Assert.Contains("Abb. 2: ", html);   // ohne eigenen Text: Überschrift
        string md = Export(v, "md");
        Assert.Contains("*Abb. 1: Das Startmenü*", md);
        Assert.Contains("## Abbildungsverzeichnis", md);
        Assert.Contains("Abbildungsverzeichnis", Export(v, "docx"));
        using var en = SessionView.Open(_folder, headless: true, lang: "en");
        Assert.Contains("Fig. 1: The start menu", Export(en, "html"));
        Assert.Contains("alt=\"Startmenü mit Suchfeld\"", Export(en, "html"));   // fehlt Englisch: Deutsch
    }

    [Fact]
    public void Captions_Off_ByDefault_NoChange()
    {
        using var v = View((e, s) => e["steps"] = new JsonArray(new JsonObject { ["anchor"] = s[0].Anchor, ["caption"] = new JsonObject { ["de"] = "Unsichtbar" } }));
        Assert.DoesNotContain("Unsichtbar", Export(v, "html"));
        Assert.DoesNotContain("m-figures", Export(v, "html"));
        var ds = new DocSettings();
        Assert.Null(ds.ToJson()["captions"]);
        Assert.Null(ds.ToJson()["warnFirst"]);
    }

    [Fact]
    public void Signal_LabelsAndWarnFirst()
    {
        using var v = View((e, s) =>
        {
            e["guide"] = new JsonObject { ["doc"] = new JsonObject { ["warnFirst"] = true } };
            e["comments"] = new JsonArray(
                new JsonObject { ["id"] = "c1", ["anchor"] = s[0].Anchor, ["kind"] = "tip", ["text"] = "Ein Tipp", ["by"] = "user" },
                new JsonObject { ["id"] = "c2", ["anchor"] = s[0].Anchor, ["kind"] = "warning", ["signal"] = "danger", ["text"] = "Stromschlag", ["by"] = "user" },
                new JsonObject { ["id"] = "c3", ["anchor"] = s[1].Anchor, ["kind"] = "warning", ["text"] = "Alt", ["by"] = "user" });
        });
        string html = Export(v, "html");
        Assert.Contains("<b>GEFAHR:</b>", html);
        Assert.Contains("<b>Achtung:</b>", html);    // ohne Signal wie bisher
        int warn = html.IndexOf("Stromschlag", StringComparison.Ordinal), tip = html.IndexOf("Ein Tipp", StringComparison.Ordinal);
        int stepText = html.IndexOf("<p>", html.LastIndexOf("<h3>", warn, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.True(warn < tip, "Warnhinweis vor dem Tipp");
        Assert.True(stepText < 0 || warn < stepText || stepText > tip, "Warnhinweis vor dem Schritttext");
        Assert.Contains("GEFAHR:", Export(v, "md"));
        Assert.Contains("GEFAHR:", Export(v, "docx"));
    }

    [Fact]
    public void Notes_PerLanguage()
    {
        using var v = View((e, s) => e["comments"] = new JsonArray(
            new JsonObject { ["id"] = "c1", ["anchor"] = s[0].Anchor, ["kind"] = "tip", ["text"] = "Deutscher Tipp", ["i18n"] = new JsonObject { ["en"] = new JsonObject { ["text"] = "**English** tip", ["fmt"] = 1 } }, ["by"] = "user" }), "en");
        string html = Export(v, "html");
        Assert.Contains("<strong>English</strong> tip", html);
        Assert.DoesNotContain("Deutscher Tipp", html);
        using var de = SessionView.Open(_folder, headless: true, lang: "de");
        Assert.Contains("Deutscher Tipp", Export(de, "html"));
    }
}
