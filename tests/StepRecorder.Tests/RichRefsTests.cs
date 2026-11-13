using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Text;

namespace StepRecorder.Tests;

/// <summary>Querverweise, Variablen und Tabellen (13, Paket 23).</summary>
[Collection("Loc")]
public class RichRefsTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-refs-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    // ---------------- Tabellen ----------------

    [Fact]
    public void Table_Parse_HeaderCellsAndEscapedPipe()
    {
        var d = RichDoc.Parse("Vorher\n\n| Taste | Wirkung |\n|---|---|\n| [[Strg+S]] | **speichert** |\n| a \\| b | `x` |\n\nNachher");
        Assert.Equal(3, d.Blocks.Count);
        var t = d.Blocks[1];
        Assert.Equal(RichBlockKind.Table, t.Kind);
        Assert.True(t.Header);
        Assert.Equal(3, t.Rows!.Count);
        Assert.Equal(2, t.Columns);
        Assert.Equal(RichKind.Key, t.Rows[1][0][0].Kind);
        Assert.Equal(RichStyle.Bold, t.Rows[1][1][0].Style);
        Assert.Equal("a | b", RichDoc.InlineText(t.Rows[2][0]));
        Assert.Contains("Taste\tWirkung", d.Projection());
        // kanonische Auszeichnung ergibt wieder dieselbe Tabelle
        var again = RichDoc.Parse(d.ToMarkup());
        Assert.Equal(d.Projection(), again.Projection());
        Assert.True(again.Blocks[1].Header);
        Assert.False(d.IsSimple);
    }

    [Fact]
    public void Table_WithoutSeparator_NoHeader_AndColumnLimit()
    {
        var d = RichDoc.Parse("| a | b |\n| c |");
        Assert.False(d.Blocks[0].Header);
        Assert.Equal(2, d.Blocks[0].Columns);
        var wide = RichDoc.Parse("|" + string.Concat(Enumerable.Range(1, 20).Select(i => " " + i + " |")));
        Assert.Equal(RichDoc.MaxColumns, wide.Blocks[0].Columns);
        Assert.Contains("20", wide.Projection());   // nichts geht verloren
    }

    [Theory]
    [InlineData("| kein Tisch")]
    [InlineData("a | b | c")]
    [InlineData("Pfad: C:\\Daten | Ordner")]
    public void Escape_KeepsPipesAsText(string plain)
    {
        var d = RichDoc.Parse(RichDoc.Escape(plain));
        Assert.Equal(plain, d.Projection());
        Assert.All(d.Blocks, b => Assert.Equal(RichBlockKind.Paragraph, b.Kind));
    }

    [Fact]
    public void TableMarkup_FromPlainCells()
    {
        string m = RichDoc.TableMarkup(new[] { new[] { "Feld", "Inhalt" }, new[] { "Name", "**nicht fett** | Strich" } }, header: true);
        var t = RichDoc.Parse(m).Blocks[0];
        Assert.Equal(RichBlockKind.Table, t.Kind);
        Assert.Equal("**nicht fett** | Strich", RichDoc.InlineText(t.Rows![1][1]));
        Assert.All(t.Rows[1][1], x => Assert.Equal(RichStyle.None, x.Style));
    }

    // ---------------- Querverweise ----------------

    [Fact]
    public void Ref_ParseAndRoundTrip()
    {
        var d = RichDoc.Parse("Siehe [[ref:s1203]] und [[ref:k-4]], aber [[ref:x1]] ist eine Taste.");
        var refs = d.References().ToList();
        Assert.Equal(new[] { "s1203", "k-4" }, refs);
        Assert.Contains("[[ref:x1]]", d.Projection());   // ungültiges Ziel bleibt Text
        Assert.Equal(d.ToMarkup(), RichDoc.Parse(d.ToMarkup()).ToMarkup());
        Assert.Contains("[[ref:s1203]]", d.ToMarkup());
    }

    SessionView View(Action<JsonObject, List<ViewStep>> edit)
    {
        var v = SessionView.Open(_folder, headless: true);
        edit(v.Edits, v.GuideSteps().Where(s => s.Kind != "move").ToList());
        v.Rebuild();
        return v;
    }

    static JsonObject Text(long anchor, string body, bool rich = true)
    {
        var t = new JsonObject { ["title"] = "T" + anchor, ["body"] = body };
        if (rich) t["fmt"] = 1;
        return new JsonObject { ["anchor"] = anchor, ["text"] = new JsonObject { ["de"] = t } };
    }

    string Export(SessionView v, string fmt)
    {
        Directory.CreateDirectory(_out);
        string path = Path.Combine(_out, "x." + (fmt == "tour" ? "html" : fmt));
        GuideExport.Export(v, new GuideOptions { Format = fmt, OutPath = path, Images = false });
        return fmt is "docx" or "pptx" ? LegacyOutputTests.Normalize(fmt, path) : File.ReadAllText(path);
    }

    [Fact]
    public void Ref_ShowsCurrentStepNumber_InGuide()
    {
        using var v = View((e, steps) =>
            e["steps"] = new JsonArray(Text(steps[0].Anchor, $"Weiter mit [[ref:s{steps[2].Anchor}]]."), new JsonObject { ["anchor"] = steps[1].Anchor, ["chapter"] = new JsonObject { ["title"] = "Teil" } }));
        string md = Export(v, "md");
        Assert.Contains("Weiter mit [Schritt 1.2](#s3).", md);
        string html = Export(v, "html");
        Assert.Contains("<a class=\"ref\" href=\"#s3\">Schritt 1.2</a>", html);
        // im KI-Paket/MCP nur ohne Nummer
        Assert.Contains(RichDoc.RefPending, v.GuideSteps().First(s => s.BodyRich != null).AuthorBody);
    }

    [Fact]
    public void Ref_HiddenTarget_NeverShowsTitle()
    {
        using var v = View((e, steps) => e["steps"] = new JsonArray(
            Text(steps[0].Anchor, $"Siehe [[ref:s{steps[1].Anchor}]]."),
            new JsonObject { ["anchor"] = steps[1].Anchor, ["hidden"] = true, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "GEHEIMER TITEL" } } }));
        foreach (var f in new[] { "md", "html", "docx" })
        {
            string o = Export(v, f);
            Assert.DoesNotContain("GEHEIMER TITEL", o);
            Assert.Contains(f == "docx" ? "??" : "Siehe ??.", o);
        }
    }

    // ---------------- Variablen ----------------

    [Fact]
    public void Vars_ExpandInPlainAndRich_UnknownStays()
    {
        using var v = View((e, steps) =>
        {
            e["guide"] = new JsonObject { ["title"] = "Kassenhandbuch", ["vars"] = new JsonObject { ["Produkt"] = "Kasse **3000**", ["bad name!"] = "x" }, ["i18n"] = new JsonObject { ["en"] = new JsonObject { ["vars"] = new JsonObject { ["Produkt"] = "Till" } } } };
            e["steps"] = new JsonArray(Text(steps[0].Anchor, "Starten Sie {{Produkt}} – {{Unbekannt}} – {{Titel}}", rich: false), Text(steps[1].Anchor, "**{{Produkt}}** öffnen"));
        });
        var st = v.GuideSteps().Where(s => s.Kind != "move").ToList();
        Assert.Equal("Starten Sie Kasse **3000** – {{Unbekannt}} – Kassenhandbuch", st[0].AuthorBody);
        // im formatierten Text bleibt der Wert wörtlich (seine Sterne sind keine Auszeichnung)
        Assert.Equal("Kasse **3000** öffnen", st[1].BodyRich!.Plain);
        Assert.Equal(new[] { "Unbekannt" }, v.UnknownVars("{{Produkt}} {{Unbekannt}}"));
        Assert.False(v.Variables().ContainsKey("bad name!"));
        Assert.True(v.SaveEdits());
        using var en = SessionView.Open(_folder, headless: true, lang: "en");
        Assert.Equal("Till", en.Variables()["Produkt"]);
        Assert.Equal("Kassenhandbuch", en.Variables()["Title"]);
    }

    [Fact]
    public void Vars_ValueIsProtected()
    {
        using var v = View((e, steps) =>
        {
            e["guide"] = new JsonObject { ["vars"] = new JsonObject { ["Login"] = "Anmeldung als mmuster" } };
            e["substitutions"] = new JsonArray(new JsonObject { ["id"] = "s1", ["match"] = new JsonObject { ["literal"] = "mmuster" }, ["placeholder"] = "Benutzer" });
            e["steps"] = new JsonArray(Text(steps[0].Anchor, "{{Login}}", rich: false), Text(steps[1].Anchor, "*{{Login}}*"));
        });
        var st = v.GuideSteps().Where(s => s.Kind != "move").ToList();
        Assert.Equal("Anmeldung als {Benutzer}", st[0].AuthorBody);
        Assert.DoesNotContain("mmuster", st[1].BodyRich!.Plain);
        foreach (var f in new[] { "md", "html", "docx", "pdf" }) Assert.DoesNotContain("mmuster", Export(v, f));
    }

    [Fact]
    public void Quality_Q31_Q32()
    {
        using var v = View((e, steps) => e["steps"] = new JsonArray(
            Text(steps[0].Anchor, "Starten Sie {{Fehlt}}.", rich: false),
            Text(steps[1].Anchor, "TODO: Bild prüfen", rich: false),
            Text(steps[2].Anchor, "Siehe [[ref:s999999]] und [[ref:s" + steps[0].Anchor + "]]")));
        var f = QualityCheck.Run(v);
        var st = v.GuideSteps().Where(s => s.Kind != "move").ToList();
        Assert.Contains(f, x => x.Rule == "Q31" && x.Key == "var" && x.Anchor == st[0].Anchor);
        Assert.Contains(f, x => x.Rule == "Q31" && x.Key == "todo" && x.Anchor == st[1].Anchor);
        Assert.Contains(f, x => x.Rule == "Q32" && x.Anchor == st[2].Anchor);
        Assert.DoesNotContain(f, x => x.Rule is "Q31" or "Q32" && x.Anchor == st[3].Anchor);
    }

    [Fact]
    public void Table_InAllFormats()
    {
        using var v = View((e, steps) => e["steps"] = new JsonArray(Text(steps[0].Anchor, "Tasten:\n\n| Taste | Wirkung |\n|---|---|\n| [[F5]] | Neu laden |\n| [[Strg+P]] | Drucken |")));
        Assert.Contains("<th scope=\"col\">Taste</th>", Export(v, "html"));
        string md = Export(v, "md");
        Assert.Contains("| Taste | Wirkung |", md);
        Assert.Contains("|---|---|", md);
        string docx = Export(v, "docx");
        Assert.Contains("<w:tbl>", docx);
        Assert.Contains("<w:tblHeader/>", docx);
        var pdfPath = Path.Combine(_out, "t.pdf");
        GuideExport.Export(v, new GuideOptions { Format = "pdf", OutPath = pdfPath, Images = false });
        Assert.True(new FileInfo(pdfPath).Length > 1000);
        Export(v, "pptx"); Export(v, "tour");
    }
}
