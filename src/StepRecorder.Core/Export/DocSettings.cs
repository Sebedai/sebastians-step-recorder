using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.Export;

/// <summary>
/// Dokument-Einstellungen für Bedienungsanleitungen (in edits.json unter <c>guide.doc</c>):
/// Deckblatt, Inhaltsverzeichnis, Kapitelnummerierung, Bildgröße und -anordnung, Akzentfarbe, Fußzeile.
/// </summary>
public sealed class DocSettings
{
    public string Subtitle = "", Author = "", Version = "", Audience = "", Prerequisites = "", Footer = "";
    public bool Cover, Toc = true, ShowDate = true;
    /// <summary>chapter (1.1, 1.2 …) | flat (1, 2, 3 …) | none</summary>
    public string Numbering = "chapter";
    /// <summary>s | m | l | full</summary>
    public string ImageSize = "m";
    /// <summary>below (Bild unter dem Text) | side (Text links, Bild rechts)</summary>
    public string Layout = "below";
    public string Accent = "#C2410C";
    /// <summary>Logo als PNG (Base64), höchstens 600 px Kantenlänge; leer = kein Logo.</summary>
    public string Logo = "";
    /// <summary>Bildausschnitt: screen (ganzer Bildschirm) | window (Anwendungsfenster) | crop (Bereich um die Aktion).</summary>
    public string View = "screen";
    /// <summary>Im Ausschnitt eine kleine Übersicht des ganzen Bildschirms einblenden.</summary>
    public bool Overview = true;

    /// <summary>Felder, die je Sprache gepflegt werden (guide.i18n.&lt;lang&gt; überschreibt für andere Sprachen als Deutsch).</summary>
    public static readonly string[] LocalizedKeys = { "subtitle", "audience", "prerequisites", "footer" };

    /// <summary>Felder, die eine Vorlage überträgt (nicht: Untertitel, Version, Voraussetzungen – die gehören zur einzelnen Anleitung).</summary>
    public static readonly string[] TemplateKeys = { "author", "audience", "footer", "cover", "toc", "showDate", "numbering", "imageSize", "layout", "accent", "logo", "view", "overview" };

    /// <summary>
    /// Vorlage aus fremder Quelle prüfen und bereinigen: nur bekannte Schlüssel, Längen begrenzt, Farbe #RRGGBB,
    /// Logo neu kodiert (entfernt alles, was kein Bild ist). Null bei unbrauchbarer Vorlage.
    /// </summary>
    public static JsonObject? SanitizeTemplate(JsonObject? t, out string? problem)
    {
        problem = null;
        if (t == null) { problem = Loc.L("keine Vorlage", "no template"); return null; }
        var r = new JsonObject();
        foreach (var k in TemplateKeys)
        {
            var v = t[k];
            if (v == null) continue;
            switch (k)
            {
                case "cover" or "toc" or "showDate" or "overview":
                    if (v is JsonValue bv && bv.TryGetValue<bool>(out var b)) r[k] = b; break;
                case "numbering": if (t.Str(k) is "chapter" or "flat" or "none") r[k] = t.Str(k); break;
                case "imageSize": if (t.Str(k) is "s" or "m" or "l" or "full") r[k] = t.Str(k); break;
                case "layout": if (t.Str(k) is "below" or "side") r[k] = t.Str(k); break;
                case "view": if (t.Str(k) is "screen" or "window" or "crop") r[k] = t.Str(k); break;
                case "accent":
                    if (t.Str(k) is { Length: 7 } a && a[0] == '#' && a[1..].All(Uri.IsHexDigit)) r[k] = a;
                    else problem = Loc.L("Akzentfarbe ungültig", "invalid accent colour");
                    break;
                case "logo":
                    if (t.Str(k) is { Length: > 0 } logo)
                    {
                        if (logo.Length > 4_000_000) { problem = Loc.L("Logo zu groß", "logo too large"); break; }
                        try
                        {
                            using var ms = new MemoryStream(Convert.FromBase64String(logo));
                            using var img = System.Drawing.Image.FromStream(ms);
                            r[k] = LogoFrom(img);
                        }
                        catch { problem = Loc.L("Logo ist kein gültiges Bild", "logo is not a valid image"); }
                    }
                    break;
                default:
                    if (t.Str(k) is { } s) r[k] = s.Length > 300 ? s[..300] : s; break;
            }
        }
        if (t.Str("address") is "formal" or "informal") r["address"] = t.Str("address");
        return r;
    }

    public static (string hex, string name)[] Accents => new[]
        { ("#C2410C", Loc.L("Orange", "Orange")), ("#1F4E9C", Loc.L("Blau", "Blue")), ("#1F7A4D", Loc.L("Grün", "Green")), ("#6B3FA0", Loc.L("Violett", "Violet")), ("#B4233C", Loc.L("Rot", "Red")), ("#2E2D2A", Loc.L("Graphit", "Graphite")) };

    public static (string id, string name)[] Sizes => new[] { ("s", Loc.L("klein", "small")), ("m", Loc.L("mittel", "medium")), ("l", Loc.L("groß", "large")), ("full", Loc.L("volle Breite", "full width")) };

    /// <summary>Anteil der Inhaltsbreite für Bilder.</summary>
    public static double Factor(string? size) => size switch { "s" => 0.5, "l" => 0.85, "full" => 1.0, _ => 0.68 };

    public static DocSettings From(JsonObject? guide, string lang = "de")
    {
        var d = new DocSettings();
        var j = guide?.Obj("doc");
        if (j == null) return d;
        d.ReadFrom(j);
        // Übersetzungen
        if (lang != "de" && guide?.Obj("i18n")?.Obj(lang) is { } loc)
        {
            if (loc.Str("subtitle") is { Length: > 0 } a) d.Subtitle = a;
            if (loc.Str("audience") is { Length: > 0 } b) d.Audience = b;
            if (loc.Str("prerequisites") is { Length: > 0 } c) d.Prerequisites = c;
            if (loc.Str("footer") is { Length: > 0 } e) d.Footer = e;
        }
        return d;
    }

    /// <summary>Text aus guide (title, intro, outro) in der gewünschten Sprache.</summary>
    public static string? GuideText(JsonObject? guide, string key, string lang)
    {
        if (lang != "de" && guide?.Obj("i18n")?.Obj(lang)?.Str(key) is { Length: > 0 } loc) return loc;
        return guide?.Str(key);
    }

    /// <summary>Liest Einstellungen aus einem doc-Objekt (auch aus Vorlagen).</summary>
    public void ReadFrom(JsonObject j)
    {
        var d = this;
        d.Subtitle = j.Str("subtitle") ?? ""; d.Author = j.Str("author") ?? ""; d.Version = j.Str("version") ?? "";
        d.Audience = j.Str("audience") ?? ""; d.Prerequisites = j.Str("prerequisites") ?? ""; d.Footer = j.Str("footer") ?? "";
        if (j["cover"] != null) d.Cover = j.Bool("cover");
        if (j["toc"] != null) d.Toc = j.Bool("toc");
        if (j["showDate"] != null) d.ShowDate = j.Bool("showDate");
        d.Numbering = j.Str("numbering") is "flat" or "none" or "chapter" ? j.Str("numbering")! : "chapter";
        d.ImageSize = j.Str("imageSize") is "s" or "m" or "l" or "full" ? j.Str("imageSize")! : "m";
        d.Layout = j.Str("layout") == "side" ? "side" : "below";
        if (j.Str("accent") is { Length: 7 } a && a[0] == '#') d.Accent = a;
        d.Logo = j.Str("logo") ?? "";
        d.View = j.Str("view") is "window" or "crop" ? j.Str("view")! : "screen";
        if (j["overview"] != null) d.Overview = j.Bool("overview");
    }

    public static (string id, string name)[] Views => new[] { ("screen", Loc.L("Ganzer Bildschirm", "Full screen")), ("window", Loc.L("Anwendungsfenster", "Application window")), ("crop", Loc.L("Bereich um die Aktion", "Area around the action")) };

    /// <summary>Logo-Bytes (PNG) oder null.</summary>
    public byte[]? LogoBytes() { try { return Logo.Length > 0 ? Convert.FromBase64String(Logo) : null; } catch { return null; } }

    /// <summary>Bild für das Logo vorbereiten: auf 600 px Kantenlänge begrenzen, als PNG (Base64).</summary>
    public static string LogoFrom(System.Drawing.Image img)
    {
        double s = Math.Min(1.0, 600.0 / Math.Max(img.Width, img.Height));
        int w = Math.Max(1, (int)(img.Width * s)), h = Math.Max(1, (int)(img.Height * s));
        using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(img, 0, 0, w, h); }
        using var ms = new MemoryStream(); bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    public JsonObject ToJson() => new()
    {
        ["subtitle"] = Subtitle, ["author"] = Author, ["version"] = Version, ["audience"] = Audience, ["prerequisites"] = Prerequisites, ["footer"] = Footer,
        ["cover"] = Cover, ["toc"] = Toc, ["showDate"] = ShowDate, ["numbering"] = Numbering, ["imageSize"] = ImageSize, ["layout"] = Layout, ["accent"] = Accent,
        ["logo"] = Logo, ["view"] = View, ["overview"] = Overview
    };

    public (double r, double g, double b) AccentRgb()
    {
        try { var c = System.Drawing.ColorTranslator.FromHtml(Accent); return (c.R / 255.0, c.G / 255.0, c.B / 255.0); }
        catch { return (0.76, 0.25, 0.05); }
    }

    public string AccentHex => Accent.TrimStart('#').ToUpperInvariant();
}
