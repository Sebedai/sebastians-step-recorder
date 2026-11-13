using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Export;

/// <summary>
/// Stil der automatischen Mausmarken in Editor und Anleitungen (13, Paket 22; <c>guide.auto.style</c>). Fehlt er, gilt das bisherige
/// Aussehen („Klassisch“). Das KI-Paket zeichnet immer die festen Marken – seine Lesehilfe erklärt genau diese.
/// </summary>
public sealed record MarkStyle(string Click = "ring", string? ClickColor = null, string? TargetColor = null, double TargetWidth = 3, string NumberShape = "circle", string? NumberColor = null, string Preset = "classic")
{
    /// <summary>Mitgelieferte Stile: Klassisch (bisher), Nummern, Dezent, Kräftig.</summary>
    public static readonly (string id, string de, string en)[] Presets =
    {
        ("classic", "Klassisch – roter Ring, blauer Rahmen", "Classic – red ring, blue frame"),
        ("numbers", "Nummern – Schrittnummer an der Stelle, Rahmen ums Ziel", "Numbers – step number at the spot, frame around the target"),
        ("subtle", "Dezent – kleiner Punkt, dünner Rahmen", "Subtle – small dot, thin frame"),
        ("bold", "Kräftig – große Marken, dicker Rahmen", "Bold – large marks, thick frame"),
    };

    /// <summary>Stil aus <c>guide.auto.style</c>; null = Klassisch.</summary>
    public static MarkStyle? From(JsonObject? o)
    {
        if (o == null) return null;
        string click = o.Str("click") is "ring" or "dot" or "target" or "none" ? o.Str("click")! : "ring";
        return new MarkStyle(click, Hex(o.Str("clickColor")), Hex(o.Str("targetColor")), Math.Clamp(o.Dbl("targetWidth", 3), 1, 8),
            o.Str("numberShape") is "circle" or "square" or "rounded" ? o.Str("numberShape")! : "circle", Hex(o.Str("numberColor")), o.Str("preset") ?? "custom");
    }

    static string? Hex(string? s) => s is { Length: 7 } && s[0] == '#' && s[1..].All(Uri.IsHexDigit) ? s : null;

    /// <summary>Overlay-Einträge eines mitgelieferten Stils: (style, stepNumber); „classic“ = kein Stil, keine Nummer.</summary>
    public static (JsonObject? style, bool number) PresetJson(string id) => id switch
    {
        "numbers" => (new JsonObject { ["preset"] = "numbers", ["click"] = "none", ["targetWidth"] = 3 }, true),
        "subtle" => (new JsonObject { ["preset"] = "subtle", ["click"] = "dot", ["targetWidth"] = 1.5 }, false),
        "bold" => (new JsonObject { ["preset"] = "bold", ["click"] = "target", ["targetWidth"] = 5 }, false),
        _ => (null, false)
    };

    static Color ParseColor(string? hex, Color fallback)
    {
        if (hex == null) return fallback;
        try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
    }

    internal Color ClickColorOr(Color c) => ParseColor(ClickColor, c);
    internal Color TargetColorOr(Color c) => ParseColor(TargetColor, c);
    internal Color NumberColorOr(Color c) => ParseColor(NumberColor, c);

    /// <summary>Schrittnummer als Marke (Kreis, Quadrat, abgerundet) mit weißem Rand.</summary>
    internal static void DrawBadge(Graphics g, string text, PointF center, double scale, Color fill, string shape, Size bounds)
    {
        float px = (float)(15 * Math.Max(scale, 0.8));
        using var font = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
        var sz = g.MeasureString(text, font);
        float h = Math.Max(px * 1.75f, 22), w = Math.Max(h, sz.Width + px * 0.7f);
        float x = Math.Clamp(center.X - w / 2, 2, Math.Max(2, bounds.Width - w - 2)), y = Math.Clamp(center.Y - h / 2, 2, Math.Max(2, bounds.Height - h - 2));
        var r = new RectangleF(x, y, w, h);
        using var path = new GraphicsPath();
        if (shape == "square") path.AddRectangle(r);
        else
        {
            float rad = shape == "rounded" ? h / 4 : h / 2;
            path.AddArc(r.X, r.Y, 2 * rad, 2 * rad, 180, 90); path.AddArc(r.Right - 2 * rad, r.Y, 2 * rad, 2 * rad, 270, 90);
            path.AddArc(r.Right - 2 * rad, r.Bottom - 2 * rad, 2 * rad, 2 * rad, 0, 90); path.AddArc(r.X, r.Bottom - 2 * rad, 2 * rad, 2 * rad, 90, 90); path.CloseFigure();
        }
        using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) { var st = g.Save(); g.TranslateTransform(1, 2); g.FillPath(shadow, path); g.Restore(st); }
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        using (var pen = new Pen(Color.White, Math.Max(1.5f, h / 12))) g.DrawPath(pen, path);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, Brushes.White, r, fmt);
    }

    /// <summary>Zählweise einer Nummer: 1, 2, 3 · a, b, c · A, B, C · i, ii, iii.</summary>
    public static string Seq(int n, string? kind) => kind switch
    {
        "alpha" => Alpha(n).ToLowerInvariant(),
        "ALPHA" => Alpha(n),
        "roman" => Roman(n),
        _ => n.ToString()
    };

    static string Alpha(int n) { var s = ""; while (n > 0) { n--; s = (char)('A' + n % 26) + s; n /= 26; } return s; }

    static string Roman(int n)
    {
        if (n <= 0 || n > 3999) return n.ToString();
        var map = new (int v, string s)[] { (1000, "m"), (900, "cm"), (500, "d"), (400, "cd"), (100, "c"), (90, "xc"), (50, "l"), (40, "xl"), (10, "x"), (9, "ix"), (5, "v"), (4, "iv"), (1, "i") };
        var r = "";
        foreach (var (v, s) in map) while (n >= v) { r += s; n -= v; }
        return r;
    }
}

/// <summary>Bearbeitungen an Zeichnungen im Overlay (13, Paket 22): Nummern neu zählen, Stil übertragen.</summary>
public static class Annotations
{
    static int IdNo(JsonObject a) => int.TryParse(a.Str("id")?.TrimStart('a'), out var n) ? n : 0;

    /// <summary>
    /// Automatische Nummern (<c>auto: true</c>) eines Bildes in der Reihenfolge ihres Anlegens neu zählen – nach Einfügen und Löschen
    /// stimmen sie wieder. Eine Nummer mit <c>start</c> zählt ab diesem Wert weiter. Feste Beschriftungen bleiben.
    /// </summary>
    public static void Renumber(JsonObject edits, string frame)
    {
        var badges = (edits.Arr("annotations") ?? new JsonArray()).OfType<JsonObject>()
            .Where(a => a.Str("type") == "badge" && a.Str("frame") == frame && a.Bool("auto")).OrderBy(IdNo).ToList();
        int n = 0;
        foreach (var b in badges)
        {
            if (b["start"] is JsonValue sv && sv.TryGetValue<int>(out var s) && s > 0) n = s; else n++;
            b["text"] = MarkStyle.Seq(n, b.Obj("style")?.Str("seq"));
        }
    }

    /// <summary>Stil einer Zeichnung auf alle Zeichnungen derselben Art übertragen (Farbe, Stärke, Linienart, Spitzen, Füllung, Form). Liefert die Anzahl.</summary>
    public static int ApplyStyleToSameType(JsonObject edits, string id)
    {
        var all = (edits.Arr("annotations") ?? new JsonArray()).OfType<JsonObject>().ToList();
        var src = all.FirstOrDefault(a => a.Str("id") == id);
        if (src?.Obj("style") is not { } st) return 0;
        int n = 0;
        foreach (var a in all.Where(a => a != src && a.Str("type") == src.Str("type")))
        {
            var cur = a.Obj("style") ?? new JsonObject();
            foreach (var k in new[] { "color", "width", "fontSize", "dash", "heads", "fill", "hue", "shape", "seq", "bg", "dim" })
                if (st[k] != null) cur[k] = st[k]!.DeepClone(); else cur.Remove(k);
            a["style"] = cur;
            n++;
        }
        return n;
    }
}
