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
    /// <summary>Seitenformat für Word und PDF: a4 | letter (09 §4.4).</summary>
    public string Page = "a4";
    /// <summary>Bildunterschriften: none (bisher), auto (Abb. n: Titel), custom (eigener Text, sonst Titel); Abbildungsverzeichnis; Warnhinweise vor dem Schritttext (13, Paket 25).</summary>
    public string Captions = "none";
    public bool Figures, WarnFirst;
    /// <summary>Impressum und Änderungsverzeichnis: front (vor dem Inhalt), back (am Ende), off; Glossar am Ende (13, Paket 26).</summary>
    public string Imprint = "front", Revisions = "front";
    public bool Glossary = true;
    /// <summary>Seite und Layout (13, Paket 27): Rand in mm (0 = bisher), Kopf- und Fußzeile je links/Mitte/rechts mit Feldern
    /// ({{Seite}}, {{Seiten}}, {{Kapitel}} und alle Variablen), Umbrüche, „Erstellt mit“.</summary>
    public int MarginMm;
    public string HeaderLeft = "", HeaderCenter = "", HeaderRight = "", FooterLeft = "", FooterCenter = "", FooterRight = "";
    public bool ChapterNewPage, StepPerPage, CreatedWith = true;
    public bool HasHeader => HeaderLeft.Length + HeaderCenter.Length + HeaderRight.Length > 0;
    public bool HasFooterParts => FooterLeft.Length + FooterCenter.Length + FooterRight.Length > 0;
    /// <summary>Querformat für Word und PDF.</summary>
    public bool Landscape;
    /// <summary>Eigene Word-Vorlage (.dotx/.docx, Base64; 09 §7.3); leer = keine. Höchstens <see cref="WordTemplateMaxBytes"/>.</summary>
    public string WordTemplate = "";
    /// <summary>Dateiname der Word-Vorlage (nur zur Anzeige).</summary>
    public string WordTemplateName = "";
    public const int WordTemplateMaxBytes = 5 * 1024 * 1024;

    /// <summary>Felder, die je Sprache gepflegt werden (guide.i18n.&lt;lang&gt; überschreibt für andere Sprachen als Deutsch).</summary>
    public static readonly string[] LocalizedKeys = { "subtitle", "audience", "prerequisites", "footer" };

    /// <summary>Felder, die eine Vorlage überträgt (nicht: Untertitel, Version, Voraussetzungen – die gehören zur einzelnen Anleitung).</summary>
    public static readonly string[] TemplateKeys = { "author", "audience", "footer", "cover", "toc", "showDate", "numbering", "imageSize", "layout", "accent", "logo", "view", "overview", "page", "landscape", "wordTemplate", "wordTemplateName" };

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
                case "cover" or "toc" or "showDate" or "overview" or "landscape":
                    if (v is JsonValue bv && bv.TryGetValue<bool>(out var b)) r[k] = b; break;
                case "page": if (t.Str(k) is "a4" or "letter") r[k] = t.Str(k); break;
                case "wordTemplate":
                    if (t.Str(k) is { Length: > 0 } wt)
                    {
                        // Word-Vorlage: nur ohne Makros und ohne Verweise nach außen (09 §9.2, EXP-073)
                        byte[]? raw = null;
                        try { raw = Convert.FromBase64String(wt); } catch { }
                        string? why = raw == null || raw.Length > WordTemplateMaxBytes ? Loc.L("Word-Vorlage ungültig oder zu groß", "Word template invalid or too large") : Formats.SimpleDocx.CheckTemplate(raw);
                        if (why != null) problem = why; else r[k] = wt;
                    }
                    break;
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
            foreach (var k in new[] { "headerLeft", "headerCenter", "headerRight", "footerLeft", "footerCenter", "footerRight" })
                if (loc.Str(k) is { Length: > 0 } hv) d.SetBand(k, hv);
            if (loc.Str("subtitle") is { Length: > 0 } a) d.Subtitle = a;
            if (loc.Str("audience") is { Length: > 0 } b) d.Audience = b;
            if (loc.Str("prerequisites") is { Length: > 0 } c) d.Prerequisites = c;
            if (loc.Str("footer") is { Length: > 0 } e) d.Footer = e;
        }
        return d;
    }

    void SetBand(string key, string v)
    {
        switch (key)
        {
            case "headerLeft": HeaderLeft = v; break; case "headerCenter": HeaderCenter = v; break; case "headerRight": HeaderRight = v; break;
            case "footerLeft": FooterLeft = v; break; case "footerCenter": FooterCenter = v; break; case "footerRight": FooterRight = v; break;
        }
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
        d.Numbering = j.Str("numbering") is "flat" or "none" or "chapter" or "perChapter" ? j.Str("numbering")! : "chapter";
        d.ImageSize = j.Str("imageSize") is "s" or "m" or "l" or "full" ? j.Str("imageSize")! : "m";
        d.Layout = j.Str("layout") == "side" ? "side" : "below";
        if (j.Str("accent") is { Length: 7 } a && a[0] == '#') d.Accent = a;
        d.Logo = j.Str("logo") ?? "";
        d.View = j.Str("view") is "window" or "crop" ? j.Str("view")! : "screen";
        if (j["overview"] != null) d.Overview = j.Bool("overview");
        d.Page = j.Str("page") == "letter" ? "letter" : "a4";
        d.Landscape = j.Bool("landscape");
        d.WordTemplate = j.Str("wordTemplate") ?? ""; d.WordTemplateName = j.Str("wordTemplateName") ?? "";
        d.Captions = j.Str("captions") is "auto" or "custom" ? j.Str("captions")! : "none";
        d.Figures = j.Bool("figures"); d.WarnFirst = j.Bool("warnFirst");
        d.Imprint = j.Str("imprint") is "back" or "off" ? j.Str("imprint")! : "front";
        d.Revisions = j.Str("revisions") is "back" or "off" ? j.Str("revisions")! : "front";
        if (j["glossary"] != null) d.Glossary = j.Bool("glossary");
        d.MarginMm = Math.Clamp(j.Int("marginMm"), 0, 40);
        d.HeaderLeft = j.Str("headerLeft") ?? ""; d.HeaderCenter = j.Str("headerCenter") ?? ""; d.HeaderRight = j.Str("headerRight") ?? "";
        d.FooterLeft = j.Str("footerLeft") ?? ""; d.FooterCenter = j.Str("footerCenter") ?? ""; d.FooterRight = j.Str("footerRight") ?? "";
        d.ChapterNewPage = j.Bool("chapterNewPage"); d.StepPerPage = j.Bool("stepPerPage");
        if (j["createdWith"] != null) d.CreatedWith = j.Bool("createdWith");
    }

    public static (string id, string name)[] Pages => new[] { ("a4", "A4"), ("letter", "Letter") };

    /// <summary>Seitengröße in Punkt (1/72 Zoll), Querformat berücksichtigt.</summary>
    public (double w, double h) PagePoints()
    {
        var (w, h) = Page == "letter" ? (612.0, 792.0) : (Formats.SimplePdf.A4W, Formats.SimplePdf.A4H);
        return Landscape ? (h, w) : (w, h);
    }

    /// <summary>Seitengröße in Twips (1/20 Punkt) für Word, Querformat berücksichtigt.</summary>
    public (int w, int h) PageTwips()
    {
        var (w, h) = Page == "letter" ? (12240, 15840) : (11906, 16838);
        return Landscape ? (h, w) : (w, h);
    }

    /// <summary>Bytes der Word-Vorlage oder null.</summary>
    public byte[]? WordTemplateBytes() { try { return WordTemplate.Length > 0 ? Convert.FromBase64String(WordTemplate) : null; } catch { return null; } }

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

    public JsonObject ToJson()
    {
        var o = BaseJson();
        // neue Schlüssel nur, wenn gesetzt (ältere Versionen und Vorlagen bleiben unverändert)
        if (Captions != "none") o["captions"] = Captions;
        if (Figures) o["figures"] = true;
        if (WarnFirst) o["warnFirst"] = true;
        if (Imprint != "front") o["imprint"] = Imprint;
        if (Revisions != "front") o["revisions"] = Revisions;
        if (!Glossary) o["glossary"] = false;
        if (MarginMm > 0) o["marginMm"] = MarginMm;
        foreach (var (k, v) in new[] { ("headerLeft", HeaderLeft), ("headerCenter", HeaderCenter), ("headerRight", HeaderRight), ("footerLeft", FooterLeft), ("footerCenter", FooterCenter), ("footerRight", FooterRight) })
            if (v.Length > 0) o[k] = v;
        if (ChapterNewPage) o["chapterNewPage"] = true;
        if (StepPerPage) o["stepPerPage"] = true;
        if (!CreatedWith) o["createdWith"] = false;
        return o;
    }

    JsonObject BaseJson() => new()
    {
        ["subtitle"] = Subtitle, ["author"] = Author, ["version"] = Version, ["audience"] = Audience, ["prerequisites"] = Prerequisites, ["footer"] = Footer,
        ["cover"] = Cover, ["toc"] = Toc, ["showDate"] = ShowDate, ["numbering"] = Numbering, ["imageSize"] = ImageSize, ["layout"] = Layout, ["accent"] = Accent,
        ["logo"] = Logo, ["view"] = View, ["overview"] = Overview,
        ["page"] = Page, ["landscape"] = Landscape, ["wordTemplate"] = WordTemplate, ["wordTemplateName"] = WordTemplateName
    };

    public (double r, double g, double b) AccentRgb()
    {
        try { var c = System.Drawing.ColorTranslator.FromHtml(Accent); return (c.R / 255.0, c.G / 255.0, c.B / 255.0); }
        catch { return (0.76, 0.25, 0.05); }
    }

    public string AccentHex => Accent.TrimStart('#').ToUpperInvariant();
}
