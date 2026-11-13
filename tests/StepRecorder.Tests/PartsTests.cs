using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Impressum, Änderungsverzeichnis, Glossar (13, Paket 26).</summary>
[Collection("Loc")]
public class PartsTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-parts-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    void Edit(Action<JsonObject> edit)
    {
        using var v = SessionView.Open(_folder, headless: true);
        edit(v.Edits);
        Assert.True(v.SaveEdits());
    }

    string Export(string fmt, string lang = "de")
    {
        using var v = SessionView.Open(_folder, headless: true, lang: lang);
        Directory.CreateDirectory(_out);
        string p = Path.Combine(_out, Guid.NewGuid().ToString("N")[..6] + "." + fmt);
        GuideExport.Export(v, new GuideOptions { Format = fmt, OutPath = p, Language = lang, Images = false });
        return fmt == "docx" ? LegacyOutputTests.Normalize(fmt, p) : System.Net.WebUtility.HtmlDecode(File.ReadAllText(p));
    }

    static JsonObject Guide(JsonObject? doc = null) => new()
    {
        ["doc"] = doc ?? new JsonObject { ["toc"] = true },
        ["imprint"] = new JsonObject { ["text"] = "Herausgeber: Beispiel GmbH", ["i18n"] = new JsonObject { ["en"] = new JsonObject { ["text"] = "Publisher: Example Ltd" } } },
        ["revisions"] = new JsonArray(new JsonObject { ["id"] = "rv1", ["version"] = "1.0", ["date"] = "2026-11-01", ["author"] = "S. M.", ["text"] = "Erste Fassung", ["i18n"] = new JsonObject { ["en"] = new JsonObject { ["text"] = "First version" } } }),
        ["glossary"] = new JsonArray(
            new JsonObject { ["id"] = "g1", ["term"] = "Zahlart", ["text"] = "Art der Bezahlung" },
            new JsonObject { ["id"] = "g2", ["term"] = "Abschluss", ["text"] = "Tagesabschluss der Kasse für mmuster" }),
        ["vars"] = new JsonObject(),
    };

    [Fact]
    public void Parts_InAllFormats_Ordered()
    {
        Edit(e =>
        {
            e["guide"] = Guide();
            e["substitutions"] = new JsonArray(new JsonObject { ["id"] = "s1", ["match"] = new JsonObject { ["literal"] = "mmuster" }, ["placeholder"] = "Benutzer" });
        });
        string html = Export("html");
        int imp = html.IndexOf("id=\"m-imprint\"", StringComparison.Ordinal), rev = html.IndexOf("id=\"m-rev\"", StringComparison.Ordinal), gl = html.IndexOf("id=\"m-glossary\"", StringComparison.Ordinal);
        Assert.True(imp > 0 && rev > imp && gl > rev, $"{imp} {rev} {gl}");
        Assert.Contains("Erste Fassung", html);
        Assert.True(html.IndexOf("Abschluss", gl, StringComparison.Ordinal) < html.IndexOf("Zahlart", gl, StringComparison.Ordinal), "Glossar alphabetisch");
        Assert.DoesNotContain("mmuster", html);
        Assert.Contains("{Benutzer}", html);
        string md = Export("md");
        Assert.Contains("## Impressum", md);
        Assert.Contains("## Änderungsverzeichnis", md);
        Assert.Contains("## Glossar", md);
        Assert.Contains("Glossar", Export("docx"));
        Export("pdf");
        string en = Export("html", "en");
        Assert.Contains("Publisher: Example Ltd", en);
        Assert.Contains("First version", en);
        Assert.Contains("Revision history", en);
    }

    [Fact]
    public void Parts_Placement_BackAndOff()
    {
        Edit(e => e["guide"] = Guide(new JsonObject { ["imprint"] = "back", ["revisions"] = "off", ["glossary"] = false }));
        string html = Export("html");
        Assert.DoesNotContain("m-rev", html);
        Assert.DoesNotContain("m-glossary", html);
        Assert.True(html.IndexOf("id=\"m-imprint\"", StringComparison.Ordinal) > html.LastIndexOf("class=\"step", StringComparison.Ordinal));
    }

    [Fact]
    public void NoParts_NoChange()
    {
        string before = Export("html");
        Assert.DoesNotContain("m-imprint", before);
        Assert.DoesNotContain("box part", before);
    }
}
