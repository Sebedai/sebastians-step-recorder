using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Bild-Editor I (13, Paket 22): Schrittnummer im Bild, Markenstile, Nummern neu zählen, neue Stilschlüssel.</summary>
[Collection("Loc")]
public class MarkStyleTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-marks-" + Guid.NewGuid().ToString("N")[..8]);
    public MarkStyleTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
    string New() => SyntheticSession.Create(Path.Combine(_root, "s" + Guid.NewGuid().ToString("N")[..6]));

    static void Edit(string folder, Action<JsonObject> change)
    {
        using var v = SessionView.Open(folder);
        change(v.Edits);
        Assert.True(v.SaveEdits());
    }

    static JsonObject Auto(JsonObject e)
    {
        var g = e.Obj("guide") ?? new JsonObject(); e["guide"] = g;
        var a = g.Obj("auto") ?? new JsonObject(); g["auto"] = a;
        return a;
    }

    static byte[] Png(string folder, Func<SessionView, ViewStep> pick, bool respectMarks = true, string? label = null, bool authorAnnotations = false)
    {
        using var v = SessionView.Open(folder);
        using var img = StepImages.Render(v, pick(v), "before", "original", annotate: true, respectMarks: respectMarks, stepLabel: label, authorAnnotations: authorAnnotations)!;
        return StepImages.Png(img.Bitmap);
    }

    static Point? MarkerOf(string folder)
    {
        using var v = SessionView.Open(folder);
        using var img = StepImages.Render(v, v.All.First(s => s.Kind == "click"), "before", "original")!;
        return img.Marker;
    }

    static Color PixelAt(byte[] png, int x, int y) { using var ms = new MemoryStream(png); using var b = new Bitmap(ms); return b.GetPixel(x, y); }

    static ViewStep Click(SessionView v) => v.All.First(s => s.Kind == "click");

    [Fact]
    public void StepNumber_OffByDefault_LabelIgnored()
    {
        var folder = New();
        // Ohne Schalter zeichnet auch eine übergebene Nummer nichts – bestehende Aufnahmen bleiben pixelgleich
        Assert.Equal(Png(folder, Click), Png(folder, Click, label: "7"));
        using var v = SessionView.Open(folder);
        Assert.False(v.MarksFor(null).Number);
        Assert.Null(v.MarkStyle());
    }

    [Fact]
    public void StepNumber_DrawnInGuide_NotInAiImages()
    {
        var folder = New();
        var plain = Png(folder, Click, label: "3");
        var ai = Png(folder, Click, respectMarks: false, label: "3");
        Edit(folder, e => Auto(e)["stepNumber"] = true);
        var numbered = Png(folder, Click, label: "3");
        Assert.NotEqual(plain, numbered);
        // KI-Paket und MCP (respectMarks: false) zeichnen die festen Marken – ohne Nummer, ohne Stil
        Assert.Equal(ai, Png(folder, Click, respectMarks: false, label: "3"));
    }

    [Fact]
    public void NumbersPreset_BadgeAtClickPoint_NoRing()
    {
        var folder = New();
        var m = MarkerOf(folder)!.Value;
        Edit(folder, e => { var (style, num) = MarkStyle.PresetJson("numbers"); Auto(e)["style"] = style; Auto(e)["stepNumber"] = num; });
        var with = Png(folder, Click, label: "12");
        var without = Png(folder, Click);
        var c = PixelAt(with, m.X, m.Y - 8);
        Assert.True(c.R > 170 && c.G < 90 && c.B < 90, $"Nummer rot an der Klickstelle erwartet, war {c}");
        // ohne Nummer: kein roter Ring an der Klickstelle (Stil „Nummern“ blendet den Klickpunkt aus)
        var ring = PixelAt(without, m.X + 18, m.Y);
        Assert.False(ring.R > 170 && ring.G < 90 && ring.B < 90);
    }

    [Fact]
    public void Style_ColorsAndWidths_FromOverlay()
    {
        var folder = New();
        var m = MarkerOf(folder)!.Value;
        var classic = Png(folder, Click);
        Assert.True(PixelAt(classic, m.X + 18, m.Y).R > 170);   // roter Ring wie bisher
        Edit(folder, e => Auto(e)["style"] = new JsonObject { ["preset"] = "custom", ["click"] = "ring", ["clickColor"] = "#00A000" });
        var green = PixelAt(Png(folder, Click), m.X + 18, m.Y);
        Assert.True(green.G > 120 && green.R < 80, $"grüner Ring erwartet, war {green}");
        // ungültige Farbe → Standardfarbe, kein Absturz
        Edit(folder, e => Auto(e)["style"] = new JsonObject { ["clickColor"] = "rot; drop" });
        using (var v = SessionView.Open(folder)) Assert.Null(v.MarkStyle()!.ClickColor);
        Assert.Equal(classic, Png(folder, Click));
        Edit(folder, e => Auto(e)["style"] = new JsonObject { ["targetWidth"] = 99 });
        using (var v = SessionView.Open(folder)) Assert.Equal(8, v.MarkStyle()!.TargetWidth);
    }

    [Fact]
    public void StepNumbers_MatchGuideNumbering()
    {
        var folder = New();
        long second;
        using (var v = SessionView.Open(folder)) second = v.GuideSteps()[1].Anchor;
        Edit(folder, e => e["steps"] = new JsonArray(new JsonObject { ["anchor"] = second, ["chapter"] = new JsonObject { ["title"] = "Teil A" } }));
        using var view = SessionView.Open(folder);
        var map = GuideExport.StepNumbers(view);
        var steps = view.GuideSteps().Where(s => s.Kind != "move").ToList();
        Assert.Equal("1", map[steps[0].Anchor]);
        Assert.Equal("1.1", map[steps[1].Anchor]);
        if (steps.Count > 2) Assert.Equal("1.2", map[steps[2].Anchor]);
        Assert.Equal("2", GuideExport.StepNumbers(view, numbering: "flat")[steps[1].Anchor]);
        // dieselbe Nummer steht in der Anleitung
        var md = Path.Combine(_root, "a.md");
        GuideExport.Export(view, new GuideOptions { Format = "md", OutPath = md, Images = false });
        Assert.Contains("1.1", File.ReadAllText(md));
    }

    [Fact]
    public void Renumber_AfterDelete_StartAndCounting()
    {
        var e = new JsonObject
        {
            ["annotations"] = new JsonArray(
                new JsonObject { ["id"] = "a1", ["frame"] = "f1", ["type"] = "badge", ["auto"] = true, ["text"] = "1" },
                new JsonObject { ["id"] = "a2", ["frame"] = "f1", ["type"] = "badge", ["auto"] = true, ["text"] = "2" },
                new JsonObject { ["id"] = "a3", ["frame"] = "f1", ["type"] = "badge", ["text"] = "X" },
                new JsonObject { ["id"] = "a10", ["frame"] = "f1", ["type"] = "badge", ["auto"] = true, ["text"] = "?" },
                new JsonObject { ["id"] = "a4", ["frame"] = "f2", ["type"] = "badge", ["auto"] = true, ["text"] = "9" })
        };
        var arr = e.Arr("annotations")!;
        JsonObject A(string id) => arr.OfType<JsonObject>().First(x => x.Str("id") == id);
        Annotations.Renumber(e, "f1");
        Assert.Equal(new[] { "1", "2", "X", "3" }, new[] { A("a1"), A("a2"), A("a3"), A("a10") }.Select(x => x.Str("text")));
        Assert.Equal("9", A("a4").Str("text"));          // anderes Bild unberührt
        arr.Remove(A("a1"));
        Annotations.Renumber(e, "f1");
        Assert.Equal("1", A("a2").Str("text"));
        Assert.Equal("2", A("a10").Str("text"));
        A("a10")["start"] = 5;
        A("a2")["style"] = new JsonObject { ["seq"] = "roman" };
        Annotations.Renumber(e, "f1");
        Assert.Equal("i", A("a2").Str("text"));
        Assert.Equal("5", A("a10").Str("text"));
        Assert.Equal("X", A("a3").Str("text"));         // feste Beschriftung bleibt
    }

    [Theory]
    [InlineData(1, "num", "1")]
    [InlineData(4, "roman", "iv")]
    [InlineData(1994, "roman", "mcmxciv")]
    [InlineData(3, "alpha", "c")]
    [InlineData(27, "ALPHA", "AA")]
    [InlineData(52, "alpha", "az")]
    public void Seq_Counting(int n, string kind, string expected) => Assert.Equal(expected, MarkStyle.Seq(n, kind));

    [Fact]
    public void ApplyStyle_ToSameType_Only()
    {
        var e = new JsonObject
        {
            ["annotations"] = new JsonArray(
                new JsonObject { ["id"] = "a1", ["type"] = "arrow", ["style"] = new JsonObject { ["color"] = "#00FF00", ["width"] = 7, ["dash"] = "dot", ["heads"] = "both" } },
                new JsonObject { ["id"] = "a2", ["type"] = "arrow", ["style"] = new JsonObject { ["color"] = "#FF0000", ["width"] = 2, ["fontSize"] = 14 } },
                new JsonObject { ["id"] = "a3", ["type"] = "rect", ["style"] = new JsonObject { ["color"] = "#FF0000" } })
        };
        Assert.Equal(1, Annotations.ApplyStyleToSameType(e, "a1"));
        var a2 = e.Arr("annotations")![1]!.Obj("style")!;
        Assert.Equal("#00FF00", a2.Str("color"));
        Assert.Equal("dot", a2.Str("dash"));
        Assert.Null(a2["fontSize"]);
        Assert.Equal("#FF0000", e.Arr("annotations")![2]!.Obj("style")!.Str("color"));
        Assert.Equal(0, Annotations.ApplyStyleToSameType(e, "nope"));
    }

    [Fact]
    public void Highlight_Hue_AndLegacyYellow()
    {
        var folder = New();
        string frame; long anchor;
        using (var v = SessionView.Open(folder)) { var c = Click(v); frame = c.PreFrame!; anchor = c.Anchor; }
        // ein Bereich, in dem das Testbild weiß ist (oben links)
        JsonObject Hl(string id, JsonObject? style) => new() { ["id"] = id, ["frame"] = frame, ["anchor"] = anchor, ["type"] = "highlight", ["geom"] = new JsonArray(2, 2, 20, 10), ["style"] = style ?? new JsonObject { ["color"] = "#4361EE" } };
        Edit(folder, e => e["annotations"] = new JsonArray(Hl("a1", null)));
        var yellow = PixelAt(Png(folder, v => v.All.First(s => s.Anchor == anchor), authorAnnotations: true), 8, 6);
        Assert.True(yellow.B < yellow.R - 25 && yellow.B < yellow.G - 25, $"bisheriges Gelb erwartet (Farbe wird ignoriert), war {yellow}");
        Edit(folder, e => e["annotations"] = new JsonArray(Hl("a1", new JsonObject { ["color"] = "#4361EE", ["hue"] = "#0000FF" })));
        var blue = PixelAt(Png(folder, v => v.All.First(s => s.Anchor == anchor), authorAnnotations: true), 8, 6);
        Assert.True(blue.B > blue.R + 25 && blue.B > blue.G + 25, $"blauer Markierstift erwartet, war {blue}");
    }

    [Fact]
    public void NewStyleKeys_Render_WithoutCrash_AndChangePixels()
    {
        var folder = New();
        string frame; long anchor;
        using (var v = SessionView.Open(folder)) { var c = Click(v); frame = c.PreFrame!; anchor = c.Anchor; }
        JsonObject A(string id, string type, JsonNode geom, JsonObject style, string? text = null)
        {
            var a = new JsonObject { ["id"] = id, ["frame"] = frame, ["anchor"] = anchor, ["type"] = type, ["geom"] = geom, ["style"] = style };
            if (text != null) a["text"] = text;
            return a;
        }
        byte[] With(params JsonObject[] anns)
        {
            Edit(folder, e => e["annotations"] = new JsonArray(anns.Select(x => (JsonNode)x.DeepClone()).ToArray()));
            return Png(folder, v => v.All.First(s => s.Anchor == anchor), authorAnnotations: true);
        }
        var baseRect = With(A("a1", "rect", new JsonArray(10, 10, 60, 30), new JsonObject { ["color"] = "#E5484D", ["width"] = 4 }));
        var filled = With(A("a1", "rect", new JsonArray(10, 10, 60, 30), new JsonObject { ["color"] = "#E5484D", ["width"] = 4, ["fill"] = "#E5484D", ["fillOpacity"] = 0.5 }));
        Assert.NotEqual(baseRect, filled);
        var inside = PixelAt(filled, 35, 25);
        Assert.True(inside.R > inside.G + 40, $"Füllung erwartet, war {inside}");
        var dashed = With(A("a1", "rect", new JsonArray(10, 10, 60, 30), new JsonObject { ["color"] = "#E5484D", ["width"] = 4, ["dash"] = "dash" }));
        Assert.NotEqual(baseRect, dashed);
        // Pfeil mit zwei Spitzen, Nummernformen, Text ohne Hintergrund/farbig, Deckkraft – nichts davon darf abstürzen
        foreach (var shape in new[] { "square", "rounded", "pin", "ring" })
            With(A("b", "badge", new JsonArray(40, 40), new JsonObject { ["color"] = "#4361EE", ["shape"] = shape }, "12"));
        With(A("c", "arrow", new JsonArray(10, 10, 80, 60), new JsonObject { ["color"] = "#1F9D55", ["heads"] = "both", ["opacity"] = 0.5 }),
             A("d", "line", new JsonArray(10, 60, 80, 10), new JsonObject { ["color"] = "#1F9D55", ["heads"] = "start", ["dash"] = "dot" }),
             A("e", "text", new JsonArray(20, 20), new JsonObject { ["color"] = "#1B1C21", ["bg"] = "none" }, "Hallo"),
             A("f", "text", new JsonArray(20, 50), new JsonObject { ["color"] = "#8E4EC6", ["bg"] = "fill" }, "Welt"));
    }
}
