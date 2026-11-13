using System.IO.Compression;
using System.Text.Json.Nodes;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

public class DetailPresetTests
{
    [Fact]
    public void Presets_DifferInDetail()
    {
        Assert.Contains("moves", DetailPresets.Guide.Off);
        Assert.Equal("few", DetailPresets.Guide.ChangeFrames);
        Assert.False(DetailPresets.Guide.AppContext);
        Assert.Empty(DetailPresets.AiTrace.Off);
        Assert.Equal("many", DetailPresets.AiTrace.ChangeFrames);
        Assert.True(DetailPresets.Guide.Bars < DetailPresets.Support.Bars && DetailPresets.Support.Bars < DetailPresets.AiTrace.Bars);
    }

    [Fact]
    public void Apply_SetsOptions_AndProfileJsonRecordsDetail()
    {
        var o = new RecordingOptions();
        DetailPresets.Apply(o, DetailPresets.Guide);
        Assert.False(o.Rec("moves"));
        Assert.True(o.Rec("clicks"));
        Assert.False(o.AppContext);
        var detail = o.ProfileJson().Obj("effective").Obj("detail")!;
        Assert.Equal("Schlank", detail.Str("level"));
        Assert.Equal("few", detail.Str("changeFrames"));
        Assert.Contains("moves", detail.StrList("off"));
    }

    [Fact]
    public void Bars_ForCustom_InRange()
    {
        Assert.Equal(4, DetailPresets.BarsFor(Array.Empty<string>(), "many", true));
        Assert.InRange(DetailPresets.BarsFor(RecordingOptions.Channels.Select(c => c.key), "few", false), 1, 2);
    }
}

// Nicht parallel zu den Sprachtests (Loc.Set schaltet die Oberflächensprache kurz um)
[Collection("Loc")]
public class ManualExportTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-manual-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    SessionView Prepared(string layout = "below")
    {
        var v = SessionView.Open(_folder, headless: true);
        var first = v.Effective(guide: true, importance: "primary").First(s => s.Kind != "move");
        v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = first.Anchor, ["chapter"] = new JsonObject { ["title"] = "Vorbereitung", ["text"] = "Zuerst das Menü öffnen." } });
        v.Edits["comments"] = new JsonArray(new JsonObject { ["id"] = "uc1", ["anchor"] = first.Anchor, ["kind"] = "important", ["text"] = "Vorher speichern!" });
        v.Edits["guide"] = new JsonObject
        {
            ["title"] = "Handbuch Test", ["intro"] = "Diese Anleitung zeigt den Ablauf.", ["outro"] = "Fertig.",
            ["doc"] = new DocSettings { Subtitle = "Kurzanleitung", Version = "1.2", Author = "IT", Prerequisites = "Zugang\nRechte", Cover = true, Toc = true, Layout = layout, Footer = "Intern" }.ToJson()
        };
        v.Rebuild();
        return v;
    }

    [Fact]
    public void Html_HasCoverChaptersCalloutsAndPrerequisites()
    {
        using var v = Prepared();
        var r = GuideExport.Export(v, new GuideOptions { Format = "html", OutPath = Path.Combine(_out, "a.html") });
        var html = File.ReadAllText(r.Path!);
        Assert.Contains("class=\"cover\"", html);
        Assert.Contains("Kurzanleitung", html);
        Assert.Contains("class=\"chapter\"", html);
        Assert.Contains("Vorbereitung", html);
        Assert.Contains("Wichtig:", html);
        Assert.Contains("<li>Zugang</li>", html);
        Assert.Contains(">1.1<", html);
        Assert.Contains("Intern", html);
    }

    [Fact]
    public void Steps_CanHideImages()
    {
        using var v = Prepared();
        var first = v.Effective(guide: true, importance: "primary").First(s => s.Kind != "move");
        ((JsonArray)v.Edits["steps"]!)[0]!["showImage"] = false;
        v.Rebuild();
        Assert.False(v.All.First(s => s.Anchor == first.Anchor).ShowImage);
    }

    [Fact]
    public void Pdf_Docx_Md_AreWritten()
    {
        using var v = Prepared(layout: "side");
        var pdf = GuideExport.Export(v, new GuideOptions { Format = "pdf", OutPath = Path.Combine(_out, "a.pdf") });
        var bytes = File.ReadAllBytes(pdf.Path!);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        var docx = GuideExport.Export(v, new GuideOptions { Format = "docx", OutPath = Path.Combine(_out, "a.docx") });
        using (var z = ZipFile.OpenRead(docx.Path!))
        {
            Assert.NotNull(z.GetEntry("word/footer1.xml"));
            using var sr = new StreamReader(z.GetEntry("word/document.xml")!.Open());
            var xml = sr.ReadToEnd();
            Assert.Contains("Vorbereitung", xml);
            Assert.Contains("<w:tbl>", xml);   // Bild neben dem Text
            System.Xml.Linq.XDocument.Parse(xml);
        }
        var md = GuideExport.Export(v, new GuideOptions { Format = "md", OutPath = Path.Combine(_out, "md", "a.md") });
        var text = File.ReadAllText(md.Path!);
        Assert.Contains("## 1  Vorbereitung", text);
        Assert.Contains("**Wichtig:**", text);
    }
}
