using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Bild-Editor II (13, Paket 28): Sprechblase, Stempel, Weichzeichnen.</summary>
[Collection("Loc")]
public class ImageEditor2Tests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    [Fact]
    public void Callout_Geometry_KeepsTip()
    {
        var a = new JsonObject { ["type"] = "callout", ["geom"] = new JsonArray(10, 10, 100, 40, 0, 90) };
        Assert.Equal(new PointF(0, 90), AnnotationGeometry.Tip(a));
        var moved = new JsonObject { ["type"] = "callout", ["geom"] = AnnotationGeometry.Moved(a, 5, 5) };
        Assert.Equal(new PointF(5, 95), AnnotationGeometry.Tip(moved));
        Assert.Equal(new RectangleF(15, 15, 100, 40), AnnotationGeometry.Rect(moved));
        var resized = new JsonObject { ["type"] = "callout", ["geom"] = AnnotationGeometry.Resized(a, 2, new PointF(200, 80)) };
        Assert.Equal(new PointF(0, 90), AnnotationGeometry.Tip(resized));
        Assert.Equal(190, AnnotationGeometry.Rect(resized)!.Value.Width);
        var tipMoved = new JsonObject { ["type"] = "callout", ["geom"] = AnnotationGeometry.Resized(a, 4, new PointF(150, 0)) };
        Assert.Equal(new PointF(150, 0), AnnotationGeometry.Tip(tipMoved));
        Assert.Equal(5, AnnotationGeometry.Handles(a).Count);
        Assert.Contains("Sprechblase", AnnotationGeometry.Describe(new JsonObject { ["type"] = "callout", ["geom"] = new JsonArray(1, 2, 3, 4), ["text"] = "Hi" }, "de"));
    }

    [Fact]
    public void Stamp_BoundsAndDescribe()
    {
        var s = new JsonObject { ["type"] = "stamp", ["geom"] = new JsonArray(50, 50, 40), ["text"] = "warn" };
        Assert.Equal(new RectangleF(30, 30, 40, 40), AnnotationGeometry.Bounds(s));
        var bigger = new JsonObject { ["type"] = "stamp", ["geom"] = AnnotationGeometry.Resized(s, 0, new PointF(90, 50)) };
        Assert.Equal(80, AnnotationGeometry.Bounds(bigger)!.Value.Width);
        Assert.Contains("Warnung", AnnotationGeometry.Describe(s, "de"));
    }

    [Fact]
    public void Render_CalloutAndStamps()
    {
        string frame; long anchor;
        using (var v = SessionView.Open(_folder)) { var c = v.All.First(s => s.Kind == "click"); frame = c.PreFrame!; anchor = c.Anchor; }
        var anns = new JsonArray(new JsonObject { ["id"] = "a1", ["frame"] = frame, ["anchor"] = anchor, ["type"] = "callout", ["geom"] = new JsonArray(20, 10, 90, 30, 10, 70), ["text"] = "Hier klicken", ["style"] = new JsonObject { ["color"] = "#4361EE" } });
        int i = 2;
        foreach (var k in AnnotationGeometry.Stamps) anns.Add(new JsonObject { ["id"] = "a" + i, ["frame"] = frame, ["anchor"] = anchor, ["type"] = "stamp", ["geom"] = new JsonArray(10 + 20 * (i++ % 8), 80, 18), ["text"] = k, ["style"] = new JsonObject { ["color"] = "#1F9D55" } });
        using (var v = SessionView.Open(_folder)) { v.Edits["annotations"] = anns; Assert.True(v.SaveEdits()); }
        using var v2 = SessionView.Open(_folder);
        using var img = StepImages.Render(v2, v2.All.First(s => s.Anchor == anchor), "before", "original", annotate: false, authorAnnotations: true)!;
        var px = img.Bitmap.GetPixel(25, 15);
        Assert.True(px.B > 180 && px.R < 120, $"blaue Sprechblase erwartet, war {px}");
    }

    [Fact]
    public void Blur_DestroysDetail()
    {
        string frame; long anchor; byte[] plain;
        using (var v = SessionView.Open(_folder))
        {
            var c = v.All.First(s => s.Kind == "click"); frame = c.PreFrame!; anchor = c.Anchor;
            using var img = StepImages.Render(v, c, "before", "original", annotate: false)!;
            plain = StepImages.Png(img.Bitmap);
        }
        using (var v = SessionView.Open(_folder))
        {
            string m = v.Session.Frame(frame)!.Str("m") ?? "";
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "image", ["style"] = "blur", ["areas"] = new JsonArray(new JsonObject { ["m"] = m, ["from"] = frame, ["rect"] = new JsonArray(0, 0, 200, 100) }) });
            Assert.True(v.SaveEdits());
        }
        using var v2 = SessionView.Open(_folder);
        using var b = StepImages.Render(v2, v2.All.First(s => s.Anchor == anchor), "before", "original", annotate: false)!;
        Assert.NotEqual(plain, StepImages.Png(b.Bitmap));
        // keine scharfen Kanten mehr: benachbarte Bildpunkte unterscheiden sich kaum
        int maxJump = 0;
        for (int x = 1; x < b.Bitmap.Width; x++) maxJump = Math.Max(maxJump, Math.Abs(b.Bitmap.GetPixel(x, 50).G - b.Bitmap.GetPixel(x - 1, 50).G));
        Assert.True(maxJump < 60, $"Sprung {maxJump}");
    }
}
