using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Gliederung (13, Paket 24): Unterkapitel, Zählweisen, ausgeblendete Kapitel, Überschriftenregel, Textabschnitte, Kapitel verschieben.</summary>
[Collection("Loc")]
public class OutlineTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    readonly string _out = Path.Combine(Path.GetTempPath(), "steprec-outline-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } try { Directory.Delete(_out, true); } catch { } }

    SessionView View(Action<JsonObject, List<ViewStep>> edit, string numbering = "chapter")
    {
        var v = SessionView.Open(_folder, headless: true);
        v.Edits["guide"] = new JsonObject { ["doc"] = new JsonObject { ["numbering"] = numbering } };
        edit(v.Edits, v.GuideSteps().Where(s => s.Kind != "move").ToList());
        v.Rebuild();
        return v;
    }

    static JsonObject Chap(long anchor, string title, int level = 1, bool hidden = false, bool hideStep = false)
    {
        var c = new JsonObject { ["title"] = title };
        if (level == 2) c["level"] = 2;
        if (hidden) c["hidden"] = true;
        var o = new JsonObject { ["anchor"] = anchor, ["chapter"] = c };
        if (hideStep) o["hidden"] = true;
        return o;
    }

    string Md(SessionView v)
    {
        Directory.CreateDirectory(_out);
        string p = Path.Combine(_out, Guid.NewGuid().ToString("N")[..6] + ".md");
        GuideExport.Export(v, new GuideOptions { Format = "md", OutPath = p, Images = false });
        return File.ReadAllText(p);
    }

    string Html(SessionView v)
    {
        Directory.CreateDirectory(_out);
        string p = Path.Combine(_out, Guid.NewGuid().ToString("N")[..6] + ".html");
        GuideExport.Export(v, new GuideOptions { Format = "html", OutPath = p, Images = false });
        string h = File.ReadAllText(p);
        Assert.Contains("h2.chapter.sub", h);   // Stil für Unterkapitel nur hier
        return h;
    }

    [Theory]
    [InlineData("chapter", "1|1.1|1.2.1|1.2.2")]
    [InlineData("flat", "1|2|3|4")]
    [InlineData("perChapter", "1|1|1|2")]
    [InlineData("none", "|||")]
    public void Numbering_Matrix_WithSubchapter(string mode, string expected)
    {
        // Schritte: 0 (Vorspann), 1 beginnt Kapitel A, 2 beginnt Unterkapitel A.2, 3 im Unterkapitel
        using var v = View((e, s) => e["steps"] = new JsonArray(Chap(s[1].Anchor, "A"), Chap(s[2].Anchor, "Ax", level: 2)), mode);
        var steps = v.GuideSteps().Where(x => x.Kind != "move").ToList();
        Assert.True(steps.Count >= 4, "Testaufnahme braucht vier Schritte, hat " + steps.Count);
        var nums = GuideExport.StepNumbers(v);
        Assert.Equal(expected, string.Join("|", steps.Take(4).Select(x => nums[x.Anchor])));
    }

    [Fact]
    public void Subchapter_HeadingsInMarkdown()
    {
        using var v = View((e, s) => e["steps"] = new JsonArray(Chap(s[1].Anchor, "Kapitel A"), Chap(s[2].Anchor, "Teil B", level: 2)));
        string md = Md(v);
        Assert.Contains("## 1  Kapitel A", md);
        Assert.Contains("### 1.2  Teil B", md);
        Assert.Contains("1.2.1", Html(v));
    }

    [Fact]
    public void Heading_MovesToNextShownStep_WhenStartHidden()
    {
        // genehmigte Korrektur: erster Schritt des Kapitels ausgeblendet → Überschrift vor dem nächsten gezeigten Schritt
        using var v = View((e, s) => e["steps"] = new JsonArray(Chap(s[1].Anchor, "Bleibt sichtbar", hideStep: true)));
        var steps = v.GuideSteps().Where(x => x.Kind != "move").ToList();
        Assert.DoesNotContain(steps, x => x.Hidden);
        var withChapter = steps.Single(x => x.ChapterTitle != null);
        Assert.Equal("Bleibt sichtbar", withChapter.ChapterTitle);
        Assert.Contains("## 1  Bleibt sichtbar", Md(v));
        // Original-Schritt unverändert (die Ansicht teilt keine Objekte)
        Assert.DoesNotContain(v.All, x => x.ChapterTitle == "Bleibt sichtbar" && x.Anchor == withChapter.Anchor);
    }

    [Fact]
    public void Heading_DroppedWhenRangeEmpty()
    {
        using var v = View((e, s) => e["steps"] = new JsonArray(Chap(s[1].Anchor, "Leer", hideStep: true), Chap(s[2].Anchor, "Danach")));
        string md = Md(v);
        Assert.DoesNotContain("Leer", md);
        Assert.Contains("## 1  Danach", md);
    }

    [Fact]
    public void HiddenChapter_HidesItsSteps_UntilNextChapter()
    {
        using var v = View((e, s) => e["steps"] = new JsonArray(Chap(s[1].Anchor, "Weg", hidden: true), Chap(s[2].Anchor, "Unter", level: 2), Chap(s[3].Anchor, "Wieder da")));
        var shown = v.GuideSteps().Where(x => x.Kind != "move").Select(x => x.Anchor).ToList();
        var all = v.Effective(importance: "all", ordered: true).Where(x => x.Kind != "move").ToList();
        int i1 = all.FindIndex(x => x.Anchor == v.All.First(y => y.ChapterTitle == "Weg").Anchor);
        Assert.DoesNotContain(all[i1].Anchor, shown);
        Assert.DoesNotContain(all[i1 + 1].Anchor, shown);   // Unterkapitel gehört dazu
        Assert.Contains(v.All.First(y => y.ChapterTitle == "Wieder da").Anchor, shown);
        Assert.DoesNotContain("Weg", Md(v));
    }

    [Fact]
    public void TextSection_NoNumber_NoImage()
    {
        using var v = View((e, s) =>
        {
            e["inserted"] = new JsonArray(new JsonObject { ["anchor"] = -1001, ["after"] = s[0].Anchor, ["type"] = "text", ["title"] = "Sicherheitshinweise", ["body"] = "Bitte lesen." });
        });
        var steps = v.GuideSteps().Where(x => x.Kind != "move").ToList();
        var sec = steps.Single(x => x.TextSection);
        Assert.Equal("", GuideExport.StepNumbers(v)[sec.Anchor]);
        Assert.Equal("2", GuideExport.StepNumbers(v)[steps[steps.IndexOf(sec) + 1].Anchor]);   // verbraucht keine Nummer
        string md = Md(v);
        Assert.Contains("Sicherheitshinweise", md);
        Assert.Contains("Bitte lesen.", md);
    }

    [Fact]
    public void ChapterOps_MoveChapter_SwapsWholeRanges()
    {
        // Vorspann 1; Kapitel 10 (11, 12); Unterkapitel 13 (14); Kapitel 20 (21)
        var seq = new List<SeqItem> { new(1, 0), new(10, 1), new(11, 0), new(12, 0), new(13, 2), new(14, 0), new(20, 1), new(21, 0) };
        Assert.Equal(new long[] { 1, 20, 21, 10, 11, 12, 13, 14 }, ChapterOps.MoveChapter(seq, 10, +1));
        Assert.Equal(new long[] { 1, 20, 21, 10, 11, 12, 13, 14 }, ChapterOps.MoveChapter(seq, 20, -1));
        Assert.Null(ChapterOps.MoveChapter(seq, 10, -1));          // schon oben, Vorspann wandert nie
        Assert.Null(ChapterOps.MoveChapter(seq, 13, +1));          // kein Unterkapitel-Nachbar im selben Kapitel
        Assert.Equal(13, ChapterOps.ChapterOf(seq, 14));
        Assert.Null(ChapterOps.ChapterOf(seq, 1));
        var seq2 = new List<SeqItem> { new(10, 1), new(11, 2), new(12, 0), new(13, 2), new(14, 0) };
        Assert.Equal(new long[] { 10, 13, 14, 11, 12 }, ChapterOps.MoveChapter(seq2, 11, +1));
    }
}
