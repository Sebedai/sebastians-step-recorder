using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Zusammenfassen, Markierungen (Linie, Freihand, Spotlight, Lupe), Qualitätsprüfung, Reihenfolge, Ausgabe-Arten.</summary>
[Collection("Loc")]
public class GuideFeatureTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-guide-" + Guid.NewGuid().ToString("N")[..8]);
    public GuideFeatureTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
    string New(bool withSecret = false, bool withHotkey = false) => SyntheticSession.Create(Path.Combine(_root, "s" + Guid.NewGuid().ToString("N")[..6]), withSecret: withSecret, withHotkey: withHotkey);

    static void Edit(string folder, Action<JsonObject> change)
    {
        using var v = SessionView.Open(folder);
        change(v.Edits);
        Assert.True(v.SaveEdits());
    }

    // ---------------- Zusammenfassen ----------------

    [Fact]
    public void Merge_ClickAndTyping_OneGuideStep_TraceUnchanged()
    {
        var folder = New();
        long click, type;
        string ahkBefore;
        using (var v = SessionView.Open(folder, headless: true))
        {
            click = v.All.First(s => s.Kind == "click").Anchor;
            type = v.All.First(s => s.Kind == "type" && !s.Json.Obj("input")!.Bool("masked")).Anchor;
            ahkBefore = AhkExport.Build(v, new AhkOptions { Images = false });
        }
        int before;
        using (var v = SessionView.Open(folder)) before = v.GuideSteps().Count;
        Edit(folder, e => e["merges"] = new JsonArray(new JsonObject { ["id"] = "mg1", ["anchors"] = new JsonArray(click, type) }));
        using (var v = SessionView.Open(folder))
        {
            var gs = v.GuideSteps();
            Assert.Equal(before - 1, gs.Count);
            var m = gs.Single(s => s.MergeId == "mg1");
            var c = v.All.First(s => s.Anchor == click); var t = v.All.First(s => s.Anchor == type);
            Assert.Equal(c.PreFrame, m.PreFrame);
            Assert.Equal(t.PostFrame, m.PostFrame);
            Assert.Contains(c.Guide.Trim(), m.Guide);
            Assert.Contains(t.Guide.Trim(), m.Guide);
            Assert.Equal(v.All.Count, v.Effective(guide: false, importance: "all").Count);
            Assert.NotNull(v.MergeOf(type));
        }
        using (var v = SessionView.Open(folder, headless: true))
        {
            Assert.Equal(ahkBefore, AhkExport.Build(v, new AhkOptions { Images = false }));
            var r = KiPackage.Export(v, new KiOptions());
            var trace = File.ReadAllText(Path.Combine(r.Path, "trace.md"));
            Assert.Contains("zusammengefasst", trace);
        }
        // eigener Text je Sprache
        Edit(folder, e => ((JsonObject)e["merges"]![0]!)["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Datei speichern", ["body"] = "Speichern Sie die Datei." } });
        using (var v = SessionView.Open(folder))
        {
            var m = v.GuideSteps().Single(s => s.MergeId == "mg1");
            Assert.Equal("Datei speichern", m.AuthorTitle);
            var html = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "html" }).Path);
            Assert.Contains("Speichern Sie die Datei.", html);
        }
        // aufheben
        Edit(folder, e => e.Remove("merges"));
        using (var v = SessionView.Open(folder)) Assert.Equal(before, v.GuideSteps().Count);
    }

    [Fact]
    public void Merge_InvalidEntries_AreIgnored()
    {
        var folder = New();
        long a;
        using (var v = SessionView.Open(folder)) a = v.All.First(s => s.Kind == "click").Anchor;
        Edit(folder, e => e["merges"] = new JsonArray(new JsonObject { ["id"] = "x", ["anchors"] = new JsonArray(a, 999999) }, new JsonObject { ["id"] = "y", ["anchors"] = new JsonArray(a) }));
        using var v2 = SessionView.Open(folder);
        Assert.DoesNotContain(v2.GuideSteps(), s => s.MergeId != null);
    }

    // ---------------- Reihenfolge ----------------

    [Fact]
    public void Order_IsVisibleInEditorList()
    {
        var folder = New();
        List<long> anchors;
        using (var v = SessionView.Open(folder)) anchors = v.Effective(guide: false, importance: "all").Select(s => s.Anchor).ToList();
        var reversed = anchors.AsEnumerable().Reverse().ToList();
        Edit(folder, e => e["order"] = new JsonArray(reversed.Select(x => (JsonNode)x).ToArray()));
        using var v2 = SessionView.Open(folder);
        Assert.Equal(reversed, v2.Effective(importance: "all", ordered: true).Select(s => s.Anchor).ToList());
        Assert.Equal(anchors, v2.Effective(importance: "all").Select(s => s.Anchor).ToList());
    }

    // ---------------- Markierungen ----------------

    [Fact]
    public void AnnotationGeometry_Basics()
    {
        var pts = Enumerable.Range(0, 2000).Select(i => new PointF(i, (float)Math.Sin(i / 10.0) * 20)).ToList();
        var s = AnnotationGeometry.Simplify(pts);
        Assert.True(s.Count <= AnnotationGeometry.MaxFreehandPoints);
        Assert.Equal(0, s[0]![0]!.GetValue<double>());
        Assert.Equal(1999, s[^1]![0]!.GetValue<double>());
        var line = new JsonObject { ["type"] = "line", ["geom"] = new JsonArray(0, 0, 100, 0) };
        Assert.Equal(1, AnnotationGeometry.Hit(line, new PointF(50, 2), 4));
        Assert.Equal(0, AnnotationGeometry.Hit(line, new PointF(50, 20), 4));
        var moved = AnnotationGeometry.Moved(line, 10, 5)!.AsArray();
        Assert.Equal(10, moved[0]!.GetValue<double>());
        var mag = new JsonObject { ["type"] = "magnifier", ["geom"] = AnnotationGeometry.NewMagnifier(new RectangleF(10, 10, 40, 20), new Size(400, 200)) };
        var parts = AnnotationGeometry.MagnifierParts(mag)!.Value;
        Assert.Equal(80, parts.Lens.Width, 1);
        var zoomed = new JsonObject { ["type"] = "magnifier", ["geom"] = AnnotationGeometry.Resized(mag, 4, new PointF(parts.Lens.X + parts.Lens.Width / 2 + 200, parts.Lens.Y)) };
        Assert.Equal(4, AnnotationGeometry.MagnifierParts(zoomed)!.Value.Zoom);
        Assert.Contains("Lupe", AnnotationGeometry.Describe(mag, "de"));
        Assert.Contains("Magnifier", AnnotationGeometry.Describe(mag, "en"));
    }

    static Color PixelAt(byte[] png, int x, int y) { using var ms = new MemoryStream(png); using var b = new Bitmap(ms); return b.GetPixel(x, y); }

    [Fact]
    public void Spotlight_DarkensOutside()
    {
        var folder = New();
        string frame; long anchor;
        using (var v = SessionView.Open(folder)) { var c = v.All.First(s => s.Kind == "click"); frame = c.PreFrame!; anchor = c.Anchor; }
        Edit(folder, e => e["annotations"] = new JsonArray(new JsonObject { ["id"] = "a1", ["frame"] = frame, ["anchor"] = anchor, ["type"] = "spotlight", ["geom"] = new JsonArray(40, 20, 80, 40), ["style"] = new JsonObject { ["dim"] = 0.6 } }));
        using var v2 = SessionView.Open(folder);
        var st = v2.All.First(s => s.Anchor == anchor);
        using var img = StepImages.Render(v2, st, "before", "original", annotate: false, authorAnnotations: true)!;
        var inside = img.Bitmap.GetPixel(45, 25); var outside = img.Bitmap.GetPixel(5, 5);
        Assert.True(inside.R > 100);           // Grau bzw. Weiß bleibt hell
        Assert.True(outside.R < 140);          // Weiß ist abgedunkelt
    }

    [Fact]
    public void Magnifier_ShowsSource_ButNeverRedactedPixels()
    {
        var folder = New();
        string frame; long anchor;
        using (var v = SessionView.Open(folder)) { var c = v.All.First(s => s.Kind == "click"); frame = c.PreFrame!; anchor = c.Anchor; }
        // Quelle über der grauen Fläche (50,30,60,20), Linse rechts oben
        var mag = new JsonObject { ["id"] = "m1", ["frame"] = frame, ["anchor"] = anchor, ["type"] = "magnifier", ["geom"] = new JsonObject { ["src"] = new JsonArray(60, 35, 20, 10), ["at"] = new JsonArray(160, 20), ["zoom"] = 2 }, ["style"] = new JsonObject { ["color"] = "#4361EE", ["width"] = 2 } };
        Edit(folder, e => e["annotations"] = new JsonArray(mag));
        using (var v = SessionView.Open(folder))
        {
            using var img = StepImages.Render(v, v.All.First(s => s.Anchor == anchor), "before", "original", annotate: false, authorAnnotations: true)!;
            var c = img.Bitmap.GetPixel(160, 20);
            Assert.True(c.R < 200 && Math.Abs(c.R - c.G) < 20);   // Grau aus der Quelle, nicht Weiß
        }
        // Schwärzung über der Quelle → Linse schwarz
        Edit(folder, e => e["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "image", ["areas"] = new JsonArray(new JsonObject { ["m"] = "M1", ["from"] = "f000001", ["rect"] = new JsonArray(50, 30, 60, 20) }) }));
        using (var v = SessionView.Open(folder))
        {
            using var img = StepImages.Render(v, v.All.First(s => s.Anchor == anchor), "before", "original", annotate: false, authorAnnotations: true)!;
            var c = img.Bitmap.GetPixel(160, 20);
            Assert.True(c.R < 30 && c.G < 30 && c.B < 30);
        }
    }

    [Fact]
    public void AnnotationOnAfterImage_NotOnBeforeExport()
    {
        var folder = New();
        long anchor; string pre, post;
        using (var v = SessionView.Open(folder)) { var c = v.All.First(s => s.Kind == "click" && s.PostFrame != null && s.PostFrame != s.PreFrame); anchor = c.Anchor; pre = c.PreFrame!; post = c.PostFrame!; }
        byte[] Render()
        {
            using var v = SessionView.Open(folder);
            using var img = StepImages.Render(v, v.All.First(s => s.Anchor == anchor), "before", "original", annotate: false, authorAnnotations: true)!;
            return StepImages.Png(img.Bitmap);
        }
        var a = Render();
        Edit(folder, e => e["annotations"] = new JsonArray(new JsonObject { ["id"] = "a1", ["frame"] = post, ["anchor"] = anchor, ["type"] = "rect", ["geom"] = new JsonArray(5, 5, 50, 50), ["style"] = new JsonObject { ["color"] = "#00FF00", ["width"] = 4 } }));
        Assert.Equal(a, Render());
    }

    // ---------------- Qualitätsprüfung ----------------

    [Fact]
    public void Quality_FindsTypicalProblems_AndIgnoresPersist()
    {
        var folder = New(withSecret: true);
        using (var v = SessionView.Open(folder))
        {
            var list = QualityCheck.Run(v, new QualityOptions { MinW = 10, MinH = 10, PersonalValues = new[] { ("userName", "notiz") } });
            Assert.Contains(list, f => f.Rule == "Q12");
            Assert.Contains(list, f => f.Rule == "Q04");
        }
        long click;
        using (var v = SessionView.Open(folder)) click = v.All.First(s => s.Kind == "click").Anchor;
        // Q10 leerer Titel, Q13 nur englischer Text, Q09 Zuschnitt schneidet Klick ab
        Edit(folder, e => e["steps"] = new JsonArray(new JsonObject { ["anchor"] = click, ["text"] = new JsonObject { ["en"] = new JsonObject { ["title"] = "Open file menu", ["body"] = "Click File." } }, ["crop"] = new JsonArray(100, 50, 90, 40) }));
        using (var v = SessionView.Open(folder))
        {
            var list = QualityCheck.Run(v, new QualityOptions { MinW = 10, MinH = 10 });
            Assert.Contains(list, f => f.Rule == "Q13" && f.Anchor == click);
            Assert.Contains(list, f => f.Rule == "Q09" && f.Anchor == click);
            var q9 = list.First(f => f.Rule == "Q09");
            QualityCheck.SetIgnored(v.Edits, q9, true);
            ((JsonArray)v.Edits["qualityIgnores"]!).Add(new JsonObject { ["rule"] = "Q99", ["anchor"] = 1 });
            Assert.True(v.SaveEdits());
        }
        using (var v = SessionView.Open(folder))
        {
            Assert.DoesNotContain(QualityCheck.Run(v, new QualityOptions { MinW = 10, MinH = 10 }), f => f.Rule == "Q09");
            Assert.Contains(QualityCheck.Run(v, new QualityOptions { MinW = 10, MinH = 10, IncludeIgnored = true }), f => f.Rule == "Q09" && f.Ignored);
            Assert.Contains(v.Edits.Arr("qualityIgnores")!, x => x.Str("rule") == "Q99");
            // ignoriertes Q12 hebt die Durchsicht nicht auf
            foreach (var f in QualityCheck.Run(v).Where(f => f.Rule == "Q12")) QualityCheck.SetIgnored(v.Edits, f, true);
            Assert.True(v.SaveEdits());
        }
        using (var v = SessionView.Open(folder))
            Assert.Throws<InvalidOperationException>(() => GuideExport.Export(v, new GuideOptions { Format = "html" }));
    }

    [Fact]
    public void MissingTranslation_DefaultUsesAutomaticText()
    {
        var folder = New();
        long click;
        using (var v = SessionView.Open(folder)) click = v.All.First(s => s.Kind == "click").Anchor;
        Edit(folder, e => e["steps"] = new JsonArray(new JsonObject { ["anchor"] = click, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Eigener Titel Kanarie", ["body"] = "Text" } } }));
        using var v2 = SessionView.Open(folder);
        var auto = File.ReadAllText(GuideExport.Export(v2, new GuideOptions { Format = "html", Language = "en", OutPath = Path.Combine(_root, "a.html") }).Path);
        Assert.DoesNotContain("Kanarie", auto);
        var other = File.ReadAllText(GuideExport.Export(v2, new GuideOptions { Format = "html", Language = "en", OutPath = Path.Combine(_root, "b.html"), MissingTranslation = "other" }).Path);
        Assert.Contains("Kanarie", other);
    }

    // ---------------- Ausgabe-Arten ----------------

    [Fact]
    public void Presets_ShortAndChecklist()
    {
        var o = new GuideOptions(); var d = new DocSettings { Cover = true, Toc = true };
        GuidePresets.Apply("Checkliste", o, d);
        Assert.False(o.Images); Assert.True(o.Checkboxes); Assert.False(d.Cover); Assert.Equal("flat", d.Numbering);
        var folder = New();
        using var v = SessionView.Open(folder);
        var html = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "html", Preset = "checklist", OutPath = Path.Combine(_root, "c.html") }).Path);
        Assert.Contains("type=\"checkbox\"", html);
        Assert.DoesNotContain("<img src=\"data:image/png", html.Replace("class=\"logo\"", ""));
        var md = File.ReadAllText(GuideExport.Export(v, new GuideOptions { Format = "md", Preset = "checklist", OutPath = Path.Combine(_root, "c", "c.md") }).Path);
        Assert.Contains("- [ ]", md);
    }
}
