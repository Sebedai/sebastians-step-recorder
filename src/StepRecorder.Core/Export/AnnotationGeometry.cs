using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Export;

/// <summary>
/// Geometrie der Markierungen des Autors in Bildkoordinaten (Monitorpixel) – eine Quelle für Editor (Auswahl, Griffe,
/// Verschieben), Export (Zeichnen) und KI-Paket (Beschreibung).
/// Typen: arrow, line, rect, ellipse, highlight, text, badge, freehand, spotlight, magnifier.
/// </summary>
public static class AnnotationGeometry
{
    public const int MaxFreehandPoints = 400;

    static double D(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<double>(out var d) ? d : v.TryGetValue<long>(out var l) ? l : v.TryGetValue<int>(out var i) ? i : 0) : 0;

    /// <summary>Rechteck einer flächigen Markierung (rect, ellipse, highlight, spotlight).</summary>
    public static RectangleF? Rect(JsonObject a) => a["geom"] is JsonArray g && g.Count >= 4 && a.Str("type") is "rect" or "ellipse" or "highlight" or "spotlight"
        ? Norm(new RectangleF((float)D(g[0]), (float)D(g[1]), (float)D(g[2]), (float)D(g[3]))) : null;

    static RectangleF Norm(RectangleF r) => RectangleF.FromLTRB(Math.Min(r.Left, r.Right), Math.Min(r.Top, r.Bottom), Math.Max(r.Left, r.Right), Math.Max(r.Top, r.Bottom));

    /// <summary>Lupe: Quellbereich und Linse (Mittelpunkt „at“, Größe = Quelle × Zoom).</summary>
    public static (RectangleF Source, RectangleF Lens, double Zoom)? MagnifierParts(JsonObject a)
    {
        if (a.Str("type") != "magnifier" || a["geom"] is not JsonObject g || g["src"] is not JsonArray s || s.Count < 4 || g["at"] is not JsonArray at || at.Count < 2) return null;
        var src = Norm(new RectangleF((float)D(s[0]), (float)D(s[1]), (float)D(s[2]), (float)D(s[3])));
        if (src.Width < 2 || src.Height < 2) return null;
        double zoom = Math.Clamp(g.Dbl("zoom", 2), 1.25, 4);
        float w = (float)(src.Width * zoom), h = (float)(src.Height * zoom);
        if (a.Obj("style").Str("shape") == "circle") w = h = Math.Max(w, h);
        var lens = new RectangleF((float)D(at[0]) - w / 2, (float)D(at[1]) - h / 2, w, h);
        return (src, lens, zoom);
    }

    static List<PointF> Points(JsonObject a) => a["geom"] is JsonArray g ? g.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new PointF((float)D(p[0]), (float)D(p[1]))).ToList() : new();

    /// <summary>Umriss einer Markierung.</summary>
    public static RectangleF? Bounds(JsonObject a)
    {
        var g = a["geom"];
        switch (a.Str("type"))
        {
            case "arrow" or "line" when g is JsonArray l && l.Count >= 4:
                return Norm(RectangleF.FromLTRB((float)D(l[0]), (float)D(l[1]), (float)D(l[2]), (float)D(l[3])));
            case "rect" or "ellipse" or "highlight" or "spotlight": return Rect(a);
            case "text" or "badge" when g is JsonArray p && p.Count >= 2:
                {
                    float fs = (float)(a.Obj("style")?.Dbl("fontSize", 18) ?? 18);
                    float w = a.Str("type") == "badge" ? fs + 10 : Math.Max(fs, (a.Str("text") ?? "").Length * fs * 0.6f);
                    return a.Str("type") == "badge" ? new RectangleF((float)D(p[0]) - w / 2, (float)D(p[1]) - w / 2, w, w) : new RectangleF((float)D(p[0]), (float)D(p[1]), w, fs * 1.4f);
                }
            case "freehand":
                {
                    var pts = Points(a);
                    if (pts.Count == 0) return null;
                    return RectangleF.FromLTRB(pts.Min(q => q.X), pts.Min(q => q.Y), pts.Max(q => q.X), pts.Max(q => q.Y));
                }
            case "magnifier":
                return MagnifierParts(a) is { } m ? RectangleF.Union(m.Source, m.Lens) : null;
        }
        return null;
    }

    static float DistToSegment(PointF p, PointF a, PointF b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float len = dx * dx + dy * dy;
        float t = len == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len, 0, 1);
        float x = a.X + t * dx - p.X, y = a.Y + t * dy - p.Y;
        return MathF.Sqrt(x * x + y * y);
    }

    /// <summary>Treffer: 0 nein, 1 Markierung, 2 Linse (Lupe), 3 Quellbereich (Lupe).</summary>
    public static int Hit(JsonObject a, PointF p, float tol)
    {
        var g = a["geom"];
        switch (a.Str("type"))
        {
            case "arrow" or "line" when g is JsonArray l && l.Count >= 4:
                return DistToSegment(p, new PointF((float)D(l[0]), (float)D(l[1])), new PointF((float)D(l[2]), (float)D(l[3]))) <= tol ? 1 : 0;
            case "freehand":
                {
                    var pts = Points(a);
                    for (int i = 1; i < pts.Count; i++) if (DistToSegment(p, pts[i - 1], pts[i]) <= tol) return 1;
                    return 0;
                }
            case "rect" or "ellipse" or "spotlight":
                {
                    if (Rect(a) is not { } r) return 0;
                    var outer = RectangleF.Inflate(r, tol, tol); var inner = RectangleF.Inflate(r, -tol, -tol);
                    return outer.Contains(p) && !(inner.Width > 0 && inner.Height > 0 && inner.Contains(p)) ? 1 : 0;
                }
            case "magnifier":
                {
                    if (MagnifierParts(a) is not { } m) return 0;
                    if (RectangleF.Inflate(m.Lens, tol, tol).Contains(p)) return 2;
                    if (RectangleF.Inflate(m.Source, tol, tol).Contains(p)) return 3;
                    return 0;
                }
            default:
                return Bounds(a) is { } b && RectangleF.Inflate(b, tol, tol).Contains(p) ? 1 : 0;
        }
    }

    /// <summary>Griffe zum Ändern der Größe (Ecken; bei der Lupe die Quelle plus ein Zoomgriff an der Linse).</summary>
    public static List<PointF> Handles(JsonObject a)
    {
        var g = a["geom"];
        switch (a.Str("type"))
        {
            case "arrow" or "line" when g is JsonArray l && l.Count >= 4:
                return new() { new((float)D(l[0]), (float)D(l[1])), new((float)D(l[2]), (float)D(l[3])) };
            case "rect" or "ellipse" or "highlight" or "spotlight" when Rect(a) is { } r:
                return new() { new(r.Left, r.Top), new(r.Right, r.Top), new(r.Right, r.Bottom), new(r.Left, r.Bottom) };
            case "magnifier" when MagnifierParts(a) is { } m:
                return new() { new(m.Source.Left, m.Source.Top), new(m.Source.Right, m.Source.Top), new(m.Source.Right, m.Source.Bottom), new(m.Source.Left, m.Source.Bottom), new(m.Lens.Right, m.Lens.Bottom) };
        }
        return new();
    }

    /// <summary>Neue Geometrie nach Ziehen am Griff <paramref name="handle"/> auf <paramref name="p"/>.</summary>
    public static JsonNode? Resized(JsonObject a, int handle, PointF p)
    {
        var g = a["geom"];
        switch (a.Str("type"))
        {
            case "arrow" or "line" when g is JsonArray l && l.Count >= 4:
                return handle == 0 ? new JsonArray(R(p.X), R(p.Y), l[2]!.DeepClone(), l[3]!.DeepClone()) : new JsonArray(l[0]!.DeepClone(), l[1]!.DeepClone(), R(p.X), R(p.Y));
            case "rect" or "ellipse" or "highlight" or "spotlight" when Rect(a) is { } r:
                {
                    var opp = handle switch { 0 => new PointF(r.Right, r.Bottom), 1 => new PointF(r.Left, r.Bottom), 2 => new PointF(r.Left, r.Top), _ => new PointF(r.Right, r.Top) };
                    var n = RectangleF.FromLTRB(Math.Min(opp.X, p.X), Math.Min(opp.Y, p.Y), Math.Max(opp.X, p.X), Math.Max(opp.Y, p.Y));
                    return new JsonArray(R(n.X), R(n.Y), R(Math.Max(4, n.Width)), R(Math.Max(4, n.Height)));
                }
            case "magnifier" when g is JsonObject mg && MagnifierParts(a) is { } m:
                {
                    var c = (JsonObject)mg.DeepClone();
                    if (handle == 4)
                    {
                        // Zoom: Abstand vom Linsenmittelpunkt zur Ecke im Verhältnis zur Quellgröße, in 0,25er-Schritten
                        var center = new PointF(m.Lens.X + m.Lens.Width / 2, m.Lens.Y + m.Lens.Height / 2);
                        double z = Math.Max(Math.Abs(p.X - center.X) * 2 / m.Source.Width, Math.Abs(p.Y - center.Y) * 2 / m.Source.Height);
                        c["zoom"] = Math.Round(Math.Clamp(z, 1.5, 4) * 4) / 4;
                        return c;
                    }
                    var r = m.Source;
                    var opp = handle switch { 0 => new PointF(r.Right, r.Bottom), 1 => new PointF(r.Left, r.Bottom), 2 => new PointF(r.Left, r.Top), _ => new PointF(r.Right, r.Top) };
                    var n = RectangleF.FromLTRB(Math.Min(opp.X, p.X), Math.Min(opp.Y, p.Y), Math.Max(opp.X, p.X), Math.Max(opp.Y, p.Y));
                    c["src"] = new JsonArray(R(n.X), R(n.Y), R(Math.Max(8, n.Width)), R(Math.Max(8, n.Height)));
                    return c;
                }
        }
        return g?.DeepClone();
    }

    /// <summary>Verschobene Geometrie; bei der Lupe nur Linse (part 2) oder nur Quelle (part 3), sonst beides.</summary>
    public static JsonNode? Moved(JsonObject a, float dx, float dy, int part = 1)
    {
        var g = a["geom"];
        switch (g)
        {
            case JsonArray arr when a.Str("type") == "freehand":
                return new JsonArray(arr.OfType<JsonArray>().Select(q => (JsonNode)new JsonArray(R(D(q[0]) + dx), R(D(q[1]) + dy))).ToArray());
            case JsonArray arr when a.Str("type") is "arrow" or "line" && arr.Count >= 4:
                return new JsonArray(R(D(arr[0]) + dx), R(D(arr[1]) + dy), R(D(arr[2]) + dx), R(D(arr[3]) + dy));
            case JsonArray arr when arr.Count >= 2:
                {
                    var c = (JsonArray)arr.DeepClone();
                    c[0] = R(D(arr[0]) + dx); c[1] = R(D(arr[1]) + dy);
                    return c;
                }
            case JsonObject mg when a.Str("type") == "magnifier":
                {
                    var c = (JsonObject)mg.DeepClone();
                    if (part != 3 && mg["at"] is JsonArray at && at.Count >= 2) c["at"] = new JsonArray(R(D(at[0]) + dx), R(D(at[1]) + dy));
                    if (part != 2 && mg["src"] is JsonArray s && s.Count >= 4) c["src"] = new JsonArray(R(D(s[0]) + dx), R(D(s[1]) + dy), s[2]!.DeepClone(), s[3]!.DeepClone());
                    return c;
                }
        }
        return g?.DeepClone();
    }

    static JsonNode R(double v) => JsonValue.Create(Math.Round(v, 1))!;

    /// <summary>Freihandlinie vereinfachen (Ramer-Douglas-Peucker), Endpunkte bleiben, höchstens <paramref name="max"/> Punkte.</summary>
    public static JsonArray Simplify(IReadOnlyList<PointF> pts, float eps = 1.5f, int max = MaxFreehandPoints)
    {
        if (pts.Count <= 2) return new JsonArray(pts.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y))).ToArray());
        List<PointF> result;
        float e = eps;
        do
        {
            var keep = new bool[pts.Count]; keep[0] = keep[^1] = true;
            var stack = new Stack<(int, int)>(); stack.Push((0, pts.Count - 1));
            while (stack.Count > 0)
            {
                var (s, t) = stack.Pop();
                float best = 0; int idx = -1;
                for (int i = s + 1; i < t; i++) { float d = DistToSegment(pts[i], pts[s], pts[t]); if (d > best) { best = d; idx = i; } }
                if (idx >= 0 && best > e) { keep[idx] = true; stack.Push((s, idx)); stack.Push((idx, t)); }
            }
            result = pts.Where((_, i) => keep[i]).ToList();
            e *= 1.6f;
        } while (result.Count > max);
        return new JsonArray(result.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y))).ToArray());
    }

    /// <summary>Neue Lupe: Quelle wie aufgezogen, Linse auf der Seite mit mehr Platz.</summary>
    public static JsonObject NewMagnifier(RectangleF src, Size frame, double zoom = 2)
    {
        float w = (float)(src.Width * zoom), h = (float)(src.Height * zoom);
        float cx = src.X + src.Width / 2, cy = src.Y + src.Height / 2;
        float right = frame.Width - src.Right, left = src.Left, below = frame.Height - src.Bottom, above = src.Top;
        float ax = cx, ay = cy;
        if (Math.Max(right, left) >= Math.Max(below, above)) ax = right >= left ? src.Right + 24 + w / 2 : src.Left - 24 - w / 2;
        else ay = below >= above ? src.Bottom + 24 + h / 2 : src.Top - 24 - h / 2;
        ax = Math.Clamp(ax, w / 2, Math.Max(w / 2, frame.Width - w / 2)); ay = Math.Clamp(ay, h / 2, Math.Max(h / 2, frame.Height - h / 2));
        return new JsonObject { ["src"] = new JsonArray(R(src.X), R(src.Y), R(src.Width), R(src.Height)), ["at"] = new JsonArray(R(ax), R(ay)), ["zoom"] = zoom };
    }

    /// <summary>Beschreibung für das KI-Paket (Markierungen erscheinen dort nicht im Bild).</summary>
    public static string Describe(JsonObject a, string lang)
    {
        bool de = lang == "de";
        string P(double x, double y) => $"({(int)x}, {(int)y})";
        string Box(RectangleF r) => $"({(int)r.X}, {(int)r.Y}, {(int)r.Width}×{(int)r.Height})";
        var g = a["geom"];
        string text = a.Str("text") is { Length: > 0 } t ? $" „{t}“" : "";
        return a.Str("type") switch
        {
            "arrow" when g is JsonArray l && l.Count >= 4 => de ? $"Pfeil von {P(D(l[0]), D(l[1]))} nach {P(D(l[2]), D(l[3]))}" : $"Arrow from {P(D(l[0]), D(l[1]))} to {P(D(l[2]), D(l[3]))}",
            "line" when g is JsonArray l && l.Count >= 4 => de ? $"Linie von {P(D(l[0]), D(l[1]))} nach {P(D(l[2]), D(l[3]))}" : $"Line from {P(D(l[0]), D(l[1]))} to {P(D(l[2]), D(l[3]))}",
            "rect" when Rect(a) is { } r => de ? $"Rahmen {Box(r)}" : $"Box {Box(r)}",
            "ellipse" when Rect(a) is { } r => de ? $"Ellipse {Box(r)}" : $"Ellipse {Box(r)}",
            "highlight" when Rect(a) is { } r => de ? $"Hervorhebung {Box(r)}" : $"Highlight {Box(r)}",
            "spotlight" when Rect(a) is { } r => de ? $"Spotlight auf {Box(r)} – übriges Bild abgedunkelt" : $"Spotlight on {Box(r)} – rest of the image dimmed",
            "text" when g is JsonArray p && p.Count >= 2 => de ? $"Text{text} bei {P(D(p[0]), D(p[1]))}" : $"Text{text} at {P(D(p[0]), D(p[1]))}",
            "badge" when g is JsonArray p && p.Count >= 2 => de ? $"Nummer{text} bei {P(D(p[0]), D(p[1]))}" : $"Number{text} at {P(D(p[0]), D(p[1]))}",
            "freehand" when Bounds(a) is { } b => de ? $"Freihandlinie, {Points(a).Count} Punkte, Bereich {Box(b)}" : $"Freehand line, {Points(a).Count} points, area {Box(b)}",
            "magnifier" when MagnifierParts(a) is { } m => de ? $"Lupe: Bereich {Box(m.Source)} {m.Zoom:0.##}-fach vergrößert bei {P(m.Lens.X + m.Lens.Width / 2, m.Lens.Y + m.Lens.Height / 2)}" : $"Magnifier: area {Box(m.Source)} enlarged {m.Zoom:0.##}× at {P(m.Lens.X + m.Lens.Width / 2, m.Lens.Y + m.Lens.Height / 2)}",
            _ => a.Str("type") ?? ""
        };
    }
}
