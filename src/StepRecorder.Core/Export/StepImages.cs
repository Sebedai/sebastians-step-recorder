using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

/// <summary>Ergebnis eines gerenderten Schrittbilds mit Abbildungsangaben.</summary>
public sealed class RenderedImage : IDisposable
{
    public Bitmap Bitmap = null!;
    public string Variant = "";
    public string FrameId = "";
    public string Monitor = "";
    public Rectangle SourceRect;
    public double Scale = 1;
    public Point? Marker;
    public Rectangle? TargetRect;
    public JsonArray Masks = new();
    public int VisualTokens => (int)(Math.Ceiling(Bitmap.Width / 28.0) * Math.Ceiling(Bitmap.Height / 28.0));
    public void Dispose() => Bitmap?.Dispose();

    public JsonObject Meta() => new()
    {
        ["variant"] = Variant, ["frameId"] = FrameId, ["width"] = Bitmap.Width, ["height"] = Bitmap.Height, ["scale"] = Math.Round(Scale, 4),
        ["sourceRect"] = new JsonObject { ["m"] = Monitor, ["x"] = SourceRect.X, ["y"] = SourceRect.Y, ["w"] = SourceRect.Width, ["h"] = SourceRect.Height },
        ["marker"] = Marker is { } p ? new JsonObject { ["x"] = p.X, ["y"] = p.Y } : null,
        ["targetRect"] = TargetRect is { } r ? new JsonObject { ["x"] = r.X, ["y"] = r.Y, ["w"] = r.Width, ["h"] = r.Height } : null,
        ["masks"] = Masks.DeepClone(), ["visualTokens"] = VisualTokens
    };
}

/// <summary>
/// Erzeugt Schrittbilder (Vorher, Ausschnitt, Nachher, Marker) über die Leseschicht, mit festen Einzeichnungen:
/// roter Ring mit Fadenkreuz = Klick, blauer Rahmen = Ziel-Element, orange Linie mit Pfeil = Ziehweg.
/// </summary>
public static class StepImages
{
    public const int CropW = 800, CropH = 600;

    public static int LongEdgeFor(string profile) => profile switch { "original" => int.MaxValue, "highres" or "hochaufloesend" or "hochauflösend" => 2576, _ => 1568 };

    /// <summary>Lage eines Schrittbilds ohne Pixel: Bildgröße, gezeigter Ausschnitt, Klickpunkt und Zielrechteck (Monitorkoordinaten).</summary>
    public sealed record ImageGeometry(string FrameId, string Monitor, Size Full, Rectangle Source, Point? Click, Rectangle? Target);

    public static string? FrameFor(ViewStep step, string variant) => variant switch
    {
        "after" => step.PostFrame,
        "cropEnd" => step.PostFrame ?? step.PreFrame,
        _ => step.PreFrame
    };

    /// <summary>Bildvariante, die Anleitungen für einen Schritt zeigen.</summary>
    public static string GuideVariant(ViewStep st, string viewMode)
        => st.Manual || st.ImageReplaced ? "before" : st.Kind is "type" or "hotkey" or "key" or "system" or "wait" ? (st.PostFrame != null ? "after" : "before") : viewMode == "crop" ? "crop" : "before";

    /// <summary>
    /// Geometrie wie bei <see cref="Render"/> (mit Zuschnitt des Autors), ohne ein Bild zu dekodieren – für Qualitätsprüfung,
    /// Tokenschätzung und Klick-Tour. Null, wenn das Bild fehlt oder geschwärzt ist.
    /// </summary>
    public static ImageGeometry? Geometry(SessionView view, ViewStep step, string variant, string viewMode = "screen", bool applyCrop = true)
    {
        var frameId = FrameFor(step, variant);
        if (frameId == null) return null;
        Size full;
        if (frameId.StartsWith("u:"))
        {
            var file = Path.Combine(view.AssetsDir, Path.GetFileName(frameId[2..]));
            if (!File.Exists(file)) return null;
            try { using var fs = File.OpenRead(file); using var im = Image.FromStream(fs, false, false); full = im.Size; } catch { return null; }
        }
        else
        {
            if (!view.FrameAvailable(frameId)) return null;
            var fr = view.Session.Frame(frameId)!;
            full = new Size(fr.Int("w"), fr.Int("h"));
            if (full.Width <= 0 || full.Height <= 0) return null;
        }
        return GeometryCore(view, step, variant, viewMode, applyCrop, frameId, full);
    }

    static ImageGeometry GeometryCore(SessionView view, ViewStep step, string variant, string viewMode, bool applyCrop, string frameId, Size full)
    {
        var f = frameId.StartsWith("u:") ? new JsonObject { ["m"] = frameId } : view.Session.Frame(frameId)!;
        var j = step.Json;
        Point? click = j.Obj("position") is { } p ? new Point(p.Int("x"), p.Int("y")) : null;
        if (variant == "cropEnd" && j.Obj("endPosition") is { } ep) click = new Point(ep.Int("x"), ep.Int("y"));
        if (j.Obj("position")?.Str("m") is { } pm && pm != f.Str("m")) click = null;
        Rectangle? target = j.Obj("target")?.Obj("el")?.Obj("rect") is { } tr && tr.Str("m") == f.Str("m") ? new Rectangle(tr.Int("x"), tr.Int("y"), tr.Int("w"), tr.Int("h")) : null;
        Rectangle src = new(0, 0, full.Width, full.Height);
        if (viewMode == "window" && j.Obj("ctx")?.Str("win") is { } wid && view.Session.Window(wid)?.Obj("rect") is { } wr && wr.Str("m") == f.Str("m"))
        {
            var w0 = Rectangle.Intersect(src, new Rectangle(wr.Int("x"), wr.Int("y"), wr.Int("w"), wr.Int("h")));
            if (w0.Width >= 16 && w0.Height >= 16) src = w0;
        }
        // Zuschnitt des Autors (gilt in Viewer und Anleitungen, nicht im KI-Paket und nicht für Bildsuche)
        if (applyCrop && variant is not ("crop" or "cropEnd") && view.CropFor(step) is { } userCrop)
        {
            var cut = Rectangle.Intersect(src, userCrop);
            if (cut.Width >= 16 && cut.Height >= 16) src = cut;
        }
        if (variant is "crop" or "cropEnd")
        {
            var c = click ?? (target is { } t ? new Point(t.X + t.Width / 2, t.Y + t.Height / 2) : new Point(full.Width / 2, full.Height / 2));
            int w = Math.Min(CropW, full.Width), h = Math.Min(CropH, full.Height);
            if (target is { } tt && tt.Width > w - 40 && tt.Width + 80 <= 1092) w = Math.Min(full.Width, tt.Width + 80);
            if (target is { } th && th.Height > h - 40 && th.Height + 80 <= 1092) h = Math.Min(full.Height, th.Height + 80);
            int x = Math.Clamp(c.X - w / 2, 0, full.Width - w), y = Math.Clamp(c.Y - h / 2, 0, full.Height - h);
            src = new Rectangle(x, y, w, h);
        }
        return new ImageGeometry(frameId, f.Str("m") ?? "", full, src, click, target);
    }

    public static RenderedImage? Render(SessionView view, ViewStep step, string variant, string profile = "compatible", bool annotate = true, string viewMode = "screen", bool respectMarks = true, MouseMarks? mask = null, bool applyCrop = true, bool overview = false, bool authorAnnotations = false)
    {
        string? frameId = FrameFor(step, variant);
        if (frameId == null) return null;
        var full = view.Frame(frameId);
        if (full == null) return null;
        var f = frameId.StartsWith("u:") ? new JsonObject { ["m"] = frameId } : view.Session.Frame(frameId)!;
        var j = step.Json;
        var geo = GeometryCore(view, step, variant, viewMode, applyCrop && annotate && respectMarks, frameId, full.Size);
        Point? click = geo.Click;
        Rectangle? target = geo.Target;
        Rectangle src = geo.Source;
        var img = new RenderedImage { Variant = variant, FrameId = frameId, Monitor = f.Str("m") ?? "" };
        foreach (var mk in f.Arr("masks") ?? new JsonArray()) img.Masks.Add(mk!.DeepClone());
        int longEdge = LongEdgeFor(profile);
        double scale = Math.Min(1.0, (double)longEdge / Math.Max(src.Width, src.Height));
        int outW = Math.Max(1, (int)Math.Round(src.Width * scale)), outH = Math.Max(1, (int)Math.Round(src.Height * scale));
        var bmp = new Bitmap(outW, outH, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = scale < 1 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(full, new Rectangle(0, 0, outW, outH), src, GraphicsUnit.Pixel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Point Map(Point q) => new((int)Math.Round((q.X - src.X) * scale), (int)Math.Round((q.Y - src.Y) * scale));
            if (target is { } tr2)
            {
                var a = Map(tr2.Location); var b = Map(new Point(tr2.Right, tr2.Bottom));
                img.TargetRect = Rectangle.FromLTRB(a.X, a.Y, b.X, b.Y);
            }
            if (click is { } cp && src.Contains(cp)) img.Marker = Map(cp);
            var marks = !annotate ? MouseMarks.None : respectMarks ? view.MarksFor(step) : MouseMarks.Default;
            if (mask != null) marks = marks.And(mask);
            if (marks.Any && variant is "before" or "crop" or "cropEnd")
                DrawMouseMarks(g, j, marks, img, Map, f.Str("m"), bmp.Size, scale, view.Language);
            if (marks.KeyCaps) KeyCaps.Draw(g, step, img, bmp.Size, view);
            if (overview && variant is "crop" or "cropEnd" && (src.Width < full.Width * 0.8 || src.Height < full.Height * 0.8))
                DrawOverview(g, full, src, bmp.Size, img.Marker);
            // Markierungen des Autors: nur die dieses Bildes, solange das Vollbild noch da ist (Lupe braucht die Pixel)
            if (authorAnnotations && view.AnnotationsFor(step, frameId) is { Count: > 0 } anns)
                DrawAuthorAnnotations(g, anns, src, scale, full, bmp.Size);
        }
        full.Dispose();
        img.Bitmap = bmp; img.SourceRect = src; img.Scale = scale;
        return img;
    }


    /// <summary>
    /// Klickfläche für die Klick-Tour in Prozent des Bildes: Zielrechteck (mit Rand), sonst ein 56-px-Feld um den Klickpunkt,
    /// mindestens <paramref name="minPx"/> groß. Null bei Schritten ohne Mausklick oder ohne bekannte Stelle („Weiter“-Knopf).
    /// </summary>
    public static RectangleF? HotspotPercent(RenderedImage img, string kind, int minPx = 32, int padPx = 6)
    {
        if (kind is not ("click" or "doubleClick" or "tripleClick" or "rightClick" or "drag")) return null;
        int W = img.Bitmap.Width, H = img.Bitmap.Height;
        if (W <= 0 || H <= 0) return null;
        RectangleF r;
        if (img.TargetRect is { } t && t.Width > 0 && t.Height > 0 && (img.Marker is not { } m0 || Rectangle.Inflate(t, 4, 4).Contains(m0)))
            r = RectangleF.Inflate(t, padPx, padPx);
        else if (img.Marker is { } m) r = new RectangleF(m.X - 28, m.Y - 28, 56, 56);
        else return null;
        if (r.Width < minPx) r = RectangleF.Inflate(r, (minPx - r.Width) / 2, 0);
        if (r.Height < minPx) r = RectangleF.Inflate(r, 0, (minPx - r.Height) / 2);
        r = RectangleF.Intersect(r, new RectangleF(0, 0, W, H));
        if (r.Width <= 0 || r.Height <= 0) return null;
        return new RectangleF(r.X * 100f / W, r.Y * 100f / H, r.Width * 100f / W, r.Height * 100f / H);
    }

    /// <summary>Übersicht im Ausschnitt: kleines Gesamtbild in einer Ecke (weg vom Klickpunkt), Ausschnitt orange umrandet.</summary>
    static void DrawOverview(Graphics g, Bitmap full, Rectangle src, Size size, Point? marker)
    {
        int w = Math.Clamp(size.Width / 4, 120, 260);
        int h = (int)Math.Round(w * (double)full.Height / full.Width);
        if (h > size.Height / 3) { h = size.Height / 3; w = (int)Math.Round(h * (double)full.Width / full.Height); }
        int pad = 10;
        var mk = marker ?? new Point(size.Width / 2, size.Height / 2);
        bool right = mk.X < size.Width / 2, bottom = mk.Y < size.Height / 2;
        var box = new Rectangle(right ? size.Width - w - pad : pad, bottom ? size.Height - h - pad : pad, w, h);
        var sm = g.SmoothingMode; var im = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillRectangle(shadow, box.X + 2, box.Y + 3, box.Width, box.Height);
        g.DrawImage(full, box);
        using (var frame = new Pen(Color.FromArgb(230, 255, 255, 255), 3)) g.DrawRectangle(frame, box);
        using (var frame2 = new Pen(Color.FromArgb(160, 0, 0, 0), 1)) g.DrawRectangle(frame2, Rectangle.Inflate(box, 2, 2));
        double s = (double)w / full.Width;
        var r = new Rectangle(box.X + (int)(src.X * s), box.Y + (int)(src.Y * s), Math.Max(4, (int)(src.Width * s)), Math.Max(4, (int)(src.Height * s)));
        using (var pen = new Pen(Color.FromArgb(242, 84, 27), 2)) g.DrawRectangle(pen, r);
        g.SmoothingMode = sm; g.InterpolationMode = im;
    }

    static readonly Color DragColor = Color.FromArgb(245, 130, 20), ClickColor = Color.FromArgb(220, 30, 30), TargetColor = Color.FromArgb(30, 100, 230);

    /// <summary>
    /// Mausmarkierungen: Zielrahmen (blau), Mausweg davor (grau gestrichelt), Zug-/Haltepfad (orange: gefüllter Punkt = gedrückt,
    /// Pfeil = Bewegung, Ring = losgelassen), Klickpunkt (roter Ring mit Fadenkreuz), Hinweise (Haltedauer, Taste, Modifikatoren).
    /// </summary>
    static void DrawMouseMarks(Graphics g, JsonObject j, MouseMarks marks, RenderedImage img, Func<Point, Point> map, string? monitor, Size size, double scale, string lang)
    {
        bool de = lang != "en";
        string kind = j.Str("kind") ?? "";
        var input = j.Obj("input");
        Point P(JsonNode? pt) => map(new Point(pt![0]!.GetValue<int>(), pt[1]!.GetValue<int>()));
        string Sec(long us) => (us / 1_000_000.0).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo(de ? "de-DE" : "en-US")) + " s";

        if (marks.Target && img.TargetRect is { } r2) using (var pen = new Pen(TargetColor, 3)) g.DrawRectangle(pen, r2);

        if (marks.Approach && j.Obj("approach") is { } ap && ap.Arr("path") is { Count: >= 2 } apath && (ap.Obj("from")?.Str("m") ?? monitor) == monitor)
        {
            var pts = apath.Select(P).ToArray();
            using var pen = new Pen(Color.FromArgb(200, 110, 110, 120), 2) { DashStyle = DashStyle.Dash };
            g.DrawLines(pen, pts);
            using var dot = new SolidBrush(Color.FromArgb(200, 110, 110, 120));
            foreach (var d in ap.Arr("dwells") ?? new JsonArray())
            {
                var q = map(new Point(d.Int("x"), d.Int("y")));
                g.FillEllipse(dot, q.X - 4, q.Y - 4, 8, 8);
            }
        }

        bool sameMonitor = j.Obj("position")?.Str("m") == monitor;
        if (marks.Path && kind == "drag" && sameMonitor && input?.Arr("path") is { Count: >= 2 } path)
        {
            var pts = path.Select(P).ToArray();
            using (var halo = new Pen(Color.FromArgb(150, 255, 255, 255), 8) { LineJoin = LineJoin.Round }) g.DrawLines(halo, pts);
            using (var pen = new Pen(DragColor, 4) { CustomEndCap = new AdjustableArrowCap(5, 5), LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
            var a = pts[0]; var b = pts[^1];
            using (var fill = new SolidBrush(DragColor)) g.FillEllipse(fill, a.X - 8, a.Y - 8, 16, 16);
            g.DrawEllipse(Pens.White, a.X - 8, a.Y - 8, 16, 16);
            using (var ring = new Pen(DragColor, 3)) g.DrawEllipse(ring, b.X - 13, b.Y - 13, 26, 26);
        }

        bool mouseKind = kind is "click" or "doubleClick" or "tripleClick" or "rightClick" or "scroll" or "hover";
        if (marks.Click && img.Marker is { } mp && mouseKind)
        {
            using var pen = new Pen(ClickColor, 3);
            g.DrawEllipse(pen, mp.X - 18, mp.Y - 18, 36, 36);
            using var thin = new Pen(ClickColor, 1.5f);
            g.DrawLine(thin, mp.X - 26, mp.Y, mp.X - 8, mp.Y); g.DrawLine(thin, mp.X + 8, mp.Y, mp.X + 26, mp.Y);
            g.DrawLine(thin, mp.X, mp.Y - 26, mp.X, mp.Y - 8); g.DrawLine(thin, mp.X, mp.Y + 8, mp.X, mp.Y + 26);
            if (input?.Bool("longPress") == true)
                using (var hold = new Pen(Color.FromArgb(160, ClickColor), 2) { DashStyle = DashStyle.Dot }) g.DrawEllipse(hold, mp.X - 26, mp.Y - 26, 52, 52);
        }

        if (!marks.Labels || input == null) return;
        var parts = new List<string>();
        var mods = input.StrList("mods");
        if (mods.Count > 0) parts.Add(string.Join("+", mods.Select(m => m switch { "ctrl" => de ? "Strg" : "Ctrl", "shift" => de ? "Umschalt" : "Shift", "alt" => "Alt", "win" => "Win", "altGr" => "AltGr", _ => m })));
        if (kind == "scroll" && input.Dbl("notches") is var n && n != 0)
        {
            bool v = input.Str("axis") != "h";
            string dir = v ? (n > 0 ? "↑" : "↓") : (n > 0 ? "→" : "←");
            string what = input.Str("semantic") == "zoom" ? (de ? (n > 0 ? "vergrößern" : "verkleinern") : (n > 0 ? "zoom in" : "zoom out")) : (de ? "scrollen" : "scroll");
            parts.Add($"{dir} {what} · {Math.Abs(n).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo(de ? "de-DE" : "en-US"))} {(de ? "Rasten" : "notches")}");
        }
        if (input.Str("button") is "secondary" && kind == "drag") parts.Add(de ? "rechte Taste" : "right button");
        if (input.Str("button") is "middle" or "x1" or "x2") parts.Add(de ? "mittlere/Zusatztaste" : "middle/extra button");
        if (kind == "drag" && input.Long("holdBeforeMoveUs") >= 300_000) parts.Add((de ? "erst " : "hold ") + Sec(input.Long("holdBeforeMoveUs")) + (de ? " halten" : ""));
        else if (kind is "click" or "rightClick" && input.Bool("longPress")) parts.Add(Sec(input.Long("pressUs")) + (de ? " gedrückt halten" : " press and hold"));
        Point? anchor = kind == "drag" && sameMonitor && input.Arr("path") is { Count: >= 2 } p0 ? P(p0[0]) : img.Marker;
        if (parts.Count > 0 && anchor is { } an) Pill(g, string.Join(" · ", parts), new PointF(an.X + 22, an.Y + 16), size, scale);
        if (kind == "drag" && sameMonitor && input.Arr("path") is { Count: >= 2 } p1)
        {
            var end = P(p1[^1]);
            var endParts = new List<string> { (de ? "loslassen" : "release") };
            if (input.Long("holdAtEndUs") >= 500_000) endParts.Insert(0, Sec(input.Long("holdAtEndUs")) + (de ? " über dem Ziel" : " over target"));
            if (input.Long("durationUs") > 0) endParts.Add(Sec(input.Long("durationUs")));
            Pill(g, string.Join(" · ", endParts), new PointF(end.X + 20, end.Y + 14), size, scale);
        }
    }

    /// <summary>Dunkle Hinweis-Pille, innerhalb des Bildes gehalten.</summary>
    static void Pill(Graphics g, string text, PointF at, Size bounds, double scale)
    {
        float px = (float)(14 * Math.Max(scale, 0.75));
        using var font = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
        var sz = g.MeasureString(text, font);
        float w = sz.Width + 14, h = sz.Height + 6;
        float x = Math.Clamp(at.X, 2, Math.Max(2, bounds.Width - w - 2)), y = Math.Clamp(at.Y, 2, Math.Max(2, bounds.Height - h - 2));
        using var gp = new GraphicsPath();
        float r = h / 2;
        gp.AddArc(x, y, h, h, 90, 180); gp.AddArc(x + w - h, y, h, h, 270, 180); gp.CloseFigure();
        using (var bg = new SolidBrush(Color.FromArgb(232, 30, 31, 36))) g.FillPath(bg, gp);
        g.DrawString(text, font, Brushes.White, x + 7, y + 3);
    }

    /// <summary>Wendet die Markierungen des Autors (Pfeile, Rahmen, Texte, Nummern) für Anleitungen an.</summary>
    public static void DrawAuthorAnnotations(Graphics g, IEnumerable<JsonObject> anns, Rectangle src, double scale)
        => DrawAuthorAnnotations(g, anns, src, scale, null, Size.Truncate(g.VisibleClipBounds.Size));

    /// <summary>
    /// Markierungen des Autors. <paramref name="full"/> ist das (bereits geschwärzte) Vollbild – Quelle der Lupe; ohne Vollbild entfällt die Lupe.
    /// Reihenfolge: Spotlight-Abdunklung, Lupen, übrige Markierungen.
    /// </summary>
    public static void DrawAuthorAnnotations(Graphics g, IEnumerable<JsonObject> anns, Rectangle src, double scale, Bitmap? full, Size outSize)
    {
        var list = anns.ToList();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float S(double v) => (float)(v * scale);
        RectangleF Map(RectangleF r) => new(S(r.X - src.X), S(r.Y - src.Y), S(r.Width), S(r.Height));

        // 1 Spotlight: alles außerhalb aller Spotlight-Rechtecke einmal abdunkeln
        var spots = list.Where(a => a.Str("type") == "spotlight" && AnnotationGeometry.Rect(a) is { }).ToList();
        if (spots.Count > 0)
        {
            double dim = spots.Max(a => Math.Clamp(a.Obj("style")?.Dbl("dim", 0.55) ?? 0.55, 0.1, 0.9));
            using var region = new Region(new Rectangle(Point.Empty, outSize));
            foreach (var s in spots) region.Exclude(Map(AnnotationGeometry.Rect(s)!.Value));
            using var shade = new SolidBrush(Color.FromArgb((int)(dim * 255), 0, 0, 0));
            g.FillRegion(shade, region);
        }
        // 2 Lupen
        if (full != null)
            foreach (var a in list.Where(a => a.Str("type") == "magnifier"))
                DrawMagnifier(g, a, src, scale, full);

        foreach (var a in list)
        {
            var geom = a["geom"];
            Color color;
            try { color = ColorTranslator.FromHtml(a.Obj("style").Str("color") ?? "#D32F2F"); } catch { color = Color.FromArgb(0xD3, 0x2F, 0x2F); }
            float width = (float)(a.Obj("style")?.Dbl("width", 4) ?? 4);
            using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            switch (a.Str("type"))
            {
                case "arrow" or "line" when geom is JsonArray g4 && g4.Count >= 4:
                    if (a.Str("type") == "arrow") pen.CustomEndCap = new AdjustableArrowCap(4, 4);
                    g.DrawLine(pen, S(g4[0]!.GetValue<double>() - src.X), S(g4[1]!.GetValue<double>() - src.Y), S(g4[2]!.GetValue<double>() - src.X), S(g4[3]!.GetValue<double>() - src.Y));
                    break;
                case "rect" or "ellipse" or "highlight" when geom is JsonArray r && r.Count >= 4:
                    {
                        var rect = new RectangleF(S(r[0]!.GetValue<double>() - src.X), S(r[1]!.GetValue<double>() - src.Y), S(r[2]!.GetValue<double>()), S(r[3]!.GetValue<double>()));
                        if (a.Str("type") == "highlight") using (var b = new SolidBrush(Color.FromArgb(90, 255, 235, 59))) g.FillRectangle(b, rect);
                        else if (a.Str("type") == "ellipse") g.DrawEllipse(pen, rect);
                        else g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                        break;
                    }
                case "text" or "badge" when geom is JsonArray pt && pt.Count >= 2:
                    {
                        float x = S(pt[0]!.GetValue<double>() - src.X), y = S(pt[1]!.GetValue<double>() - src.Y);
                        string text = a.Str("text") ?? "";
                        using var font = new Font("Segoe UI", (float)(a.Obj("style")?.Dbl("fontSize", 18) ?? 18) * (float)scale, FontStyle.Bold, GraphicsUnit.Pixel);
                        if (a.Str("type") == "badge")
                        {
                            float d = Math.Max(font.Height + 6, 24);
                            using var b = new SolidBrush(color);
                            g.FillEllipse(b, x - d / 2, y - d / 2, d, d);
                            g.DrawString(text, font, Brushes.White, new RectangleF(x - d / 2, y - d / 2, d, d), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        }
                        else
                        {
                            var size = g.MeasureString(text, font);
                            using var bg = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
                            g.FillRectangle(bg, x - 2, y - 2, size.Width + 4, size.Height + 4);
                            using var b = new SolidBrush(color);
                            g.DrawString(text, font, b, x, y);
                        }
                        break;
                    }
                case "freehand" when geom is JsonArray pts && pts.Count >= 2:
                    g.DrawLines(pen, pts.Select(q => new PointF(S(q![0]!.GetValue<double>() - src.X), S(q[1]!.GetValue<double>() - src.Y))).ToArray());
                    break;
            }
        }
    }

    /// <summary>
    /// Lupe: Quellbereich vergrößert an anderer Stelle. Pixel kommen nur aus dem übergebenen (geschwärzten) Vollbild und nur aus
    /// dem gezeigten Ausschnitt – eine Lupe kann weder Schwärzungen noch einen Zuschnitt des Autors umgehen.
    /// </summary>
    static void DrawMagnifier(Graphics g, JsonObject a, Rectangle src, double scale, Bitmap full)
    {
        if (AnnotationGeometry.MagnifierParts(a) is not { } m) return;
        var visible = Rectangle.Intersect(Rectangle.Round(m.Source), src);
        visible.Intersect(new Rectangle(0, 0, full.Width, full.Height));
        if (visible.Width < 2 || visible.Height < 2) return;
        float S(double v) => (float)(v * scale);
        var lens = new RectangleF(S(m.Lens.X - src.X), S(m.Lens.Y - src.Y), S(m.Lens.Width), S(m.Lens.Height));
        var srcOut = new RectangleF(S(m.Source.X - src.X), S(m.Source.Y - src.Y), S(m.Source.Width), S(m.Source.Height));
        Color color;
        try { color = ColorTranslator.FromHtml(a.Obj("style").Str("color") ?? "#4361EE"); } catch { color = Color.FromArgb(0x43, 0x61, 0xEE); }
        float width = (float)Math.Max(2, a.Obj("style")?.Dbl("width", 3) ?? 3);
        bool circle = a.Obj("style").Str("shape") == "circle";
        using var path = new GraphicsPath();
        if (circle) path.AddEllipse(lens);
        else
        {
            float r = Math.Min(16, Math.Min(lens.Width, lens.Height) / 6);
            path.AddArc(lens.X, lens.Y, 2 * r, 2 * r, 180, 90); path.AddArc(lens.Right - 2 * r, lens.Y, 2 * r, 2 * r, 270, 90);
            path.AddArc(lens.Right - 2 * r, lens.Bottom - 2 * r, 2 * r, 2 * r, 0, 90); path.AddArc(lens.X, lens.Bottom - 2 * r, 2 * r, 2 * r, 90, 90); path.CloseFigure();
        }
        // Verbindungslinie und gestrichelter Quellrahmen
        using (var dash = new Pen(Color.FromArgb(200, color), Math.Max(1.5f, width / 2)) { DashStyle = DashStyle.Dash })
        {
            g.DrawRectangle(dash, srcOut.X, srcOut.Y, srcOut.Width, srcOut.Height);
            var c1 = new PointF(srcOut.X + srcOut.Width / 2, srcOut.Y + srcOut.Height / 2);
            var c2 = new PointF(lens.X + lens.Width / 2, lens.Y + lens.Height / 2);
            g.DrawLine(dash, c1, c2);
        }
        using (var shadow = new GraphicsPath()) { shadow.AddPath(path, false); using var m2 = new Matrix(); m2.Translate(2, 3); shadow.Transform(m2); using var sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0)); g.FillPath(sb, shadow); }
        var state = g.Save();
        g.SetClip(path);
        using (var bg = new SolidBrush(Color.White)) g.FillPath(bg, path);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        // nur der sichtbare Teil der Quelle wird vergrößert (Seitenverhältnis wie Quelle)
        float fx = lens.Width / (float)m.Source.Width, fy = lens.Height / (float)m.Source.Height;
        var dest = new RectangleF(lens.X + (float)(visible.X - m.Source.X) * fx, lens.Y + (float)(visible.Y - m.Source.Y) * fy, visible.Width * fx, visible.Height * fy);
        g.DrawImage(full, dest, visible, GraphicsUnit.Pixel);
        g.Restore(state);
        using var border = new Pen(color, width);
        g.DrawPath(border, path);
    }

    public static byte[] Png(Bitmap b) { using var ms = new MemoryStream(); b.Save(ms, ImageFormat.Png); return ms.ToArray(); }

    public static byte[] Jpeg(Bitmap b, long quality = 85)
    {
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var p = new EncoderParameters(1);
        p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        using var ms = new MemoryStream();
        b.Save(ms, codec, p);
        return ms.ToArray();
    }
}
