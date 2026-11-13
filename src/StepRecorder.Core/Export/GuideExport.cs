using System.Drawing;
using System.Runtime.CompilerServices;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export.Formats;
using StepRecorder.ReadLayer;
using StepRecorder.Text;

namespace StepRecorder.Export;

public sealed class GuideOptions
{
    public string Format { get; set; } = "html";       // html | md | docx | pdf
    public string? OutPath { get; set; }
    public string? Language { get; set; }
    public string Importance { get; set; } = "primary"; // primary | secondary | all
    /// <summary>screen | window | crop; null = aus den Dokument-Einstellungen.</summary>
    public string? View { get; set; }
    /// <summary>Übersicht im Ausschnitt; null = aus den Dokument-Einstellungen.</summary>
    public bool? Overview { get; set; }
    /// <summary>Angehängte Aufnahmen (guide.append) mit ausgeben.</summary>
    public bool IncludeAppended { get; set; } = true;
    /// <summary>GIF und Video: Anzeigedauer je Schritt in Sekunden.</summary>
    public double GifSeconds { get; set; } = 2.5;
    public bool Images { get; set; } = true;
    /// <summary>
    /// Zusätzliches Nachher-Bild („Ergebnis“) unter dem Schrittbild: <c>never</c> (Standard), <c>changed</c> = nur wenn sich der
    /// Bildschirm sichtbar geändert hat, <c>always</c> = wann immer eines vorliegt. Entfällt, wenn das Schrittbild selbst schon das Nachher-Bild ist.
    /// </summary>
    public string After { get; set; } = "never";
    public bool Numbering { get; set; } = true;
    public bool ShowEvidence { get; set; }
    public bool Overwrite { get; set; } = true;
    public string ImageProfile { get; set; } = "compatible";
    /// <summary>Mausmarkierungen (Klickpunkt, Zugweg, Zielrahmen, Hinweise) in die Bilder zeichnen.</summary>
    public bool MouseMarks { get; set; } = true;
    /// <summary>Gedrückte Tasten und getippten Text als Tastenkappen in die Bilder zeichnen.</summary>
    public bool KeyMarks { get; set; } = true;
    /// <summary>Dokument-Einstellungen; null = aus der Aufnahme (edits.json, guide.doc).</summary>
    public DocSettings? Doc { get; set; }
    /// <summary>Art der Anleitung: standard | short (Kurzfassung) | checklist (Checkliste).</summary>
    public string? Preset { get; set; }
    /// <summary>Fehlt ein Autorentext in der Exportsprache: auto (automatischer Text) | other (Text der anderen Sprache).</summary>
    public string? MissingTranslation { get; set; }
    /// <summary>Klick-Tour: Hinweisring always | onDemand | never.</summary>
    public string? TourHints { get; set; }
    /// <summary>PowerPoint: Vortragsnotizen mit vollem Text.</summary>
    public bool PptxNotes { get; set; } = true;
    public bool Compact { get; set; }
    public bool Checkboxes { get; set; }
    /// <summary>Bildspeicher der Seitenansicht (nur Arbeitsspeicher); null = jedes Bild neu rendern.</summary>
    public GuideImageCache? ImageCache { get; set; }
    /// <summary>Markdown: Dialekt der Hinweiskästen plain | github | obsidian (09 §6.2).</summary>
    public string MdDialect { get; set; } = "plain";
    /// <summary>Markdown: Kopfdaten als YAML-Block (Docusaurus, MkDocs, Jekyll).</summary>
    public bool MdFrontMatter { get; set; }
    /// <summary>Markdown in der früheren Form (Ordner „bilder“, Überschrift je Schritt) – für eine Übergangszeit.</summary>
    public bool MdLegacy { get; set; }
    /// <summary>GIF: Größenziel in Bytes (0 = ohne); darüber werden Breite und dann Bildzahl verringert.</summary>
    public long GifMaxBytes { get; set; }
    /// <summary>Word/PDF: Seitenformat a4 | letter; null = aus den Dokument-Einstellungen.</summary>
    public string? Page { get; set; }
    /// <summary>Word/PDF: Querformat; null = aus den Dokument-Einstellungen.</summary>
    public bool? Landscape { get; set; }
}

/// <summary>
/// Anleitungs-Exporte (09): HTML (eine Datei), Markdown (Ordner), DOCX, PDF – als Bedienungsanleitung mit
/// optionalem Deckblatt, Inhaltsverzeichnis, Kapiteln, Hinweiskästen, Voraussetzungen und Abschluss.
/// </summary>
public static partial class GuideExport
{
    sealed record Item(ViewStep Step, string Number, string Anchor, string Title, string Text, byte[]? Png, int W, int H, List<(string kind, string text)> Notes, string Size,
        RectangleF? Hot = null, byte[]? AfterPng = null, int AW = 0, int AH = 0, RichText? Body = null, List<RichText?>? NoteRich = null)
    {
        /// <summary>Formatierter Hinweis an Position <paramref name="i"/> oder null.</summary>
        public RichText? NoteBody(int i) => NoteRich != null && i < NoteRich.Count ? NoteRich[i] : null;
    }
    sealed record Chapter(string Number, string Anchor, string Title, string Text, RichText? Body = null);

    /// <summary>Alles, was ein Dokument braucht – einmal gesammelt, für alle Formate gleich.</summary>
    sealed class Doc
    {
        public string Lang = "de", Title = "", Subtitle = "", Intro = "", Outro = "", Date = "";
        public byte[]? Logo;
        public List<string> Prereq = new();
        public List<(string label, string value)> Meta = new();
        public List<object> Blocks = new();   // Chapter | Item
        public DocSettings S = new();
        /// <summary>Kurzfassung: kompakte Schrittdarstellung; Checkliste: Kästchen je Schritt.</summary>
        public bool Compact, Checkboxes;
        /// <summary>Schritte aus angehängten Aufnahmen (ihre Anker gehören zu einer anderen Aufnahme).</summary>
        public HashSet<object> Foreign = new(ReferenceEqualityComparer.Instance);
        /// <summary>Formatierte Einleitung und formatierter Abschluss (13, Paket 21).</summary>
        public RichText? IntroRich, OutroRich;
        /// <summary>Enthält die Anleitung formatierten Text? Dann kommen zusätzliche Stile hinzu.</summary>
        public bool UsesRich => IntroRich != null || OutroRich != null || Blocks.Any(b => b is Chapter { Body: not null } || b is Item it && (it.Body != null || it.NoteRich?.Any(n => n != null) == true));
        public int StepCount => Blocks.OfType<Item>().Count();
        public bool HasChapters => Blocks.OfType<Chapter>().Any();
        public bool ShowToc => S.Toc && (HasChapters ? Blocks.Count >= 3 : StepCount >= 6);
        /// <summary>Rahmentext in der Sprache der Anleitung (Französisch, Spanisch, Italienisch, Niederländisch über <see cref="Settings.OutText.Frame"/>).</summary>
        public string L(string de, string en) => Settings.OutText.T(Lang, de, en);
    }

    public static ExportResult Export(SessionView view, GuideOptions o)
    {
        string lang = o.Language ?? view.Session.Language;
        bool fallbackBefore = view.AuthorTextFallback;
        string langBefore = view.Language;
        // Fehlende Übersetzung: standardmäßig der automatische Text der Exportsprache (09 §10)
        view.AuthorTextFallback = o.MissingTranslation == "other";
        view.Language = lang; view.Rebuild();
        try { return ExportCore(view, o, lang); }
        finally { view.AuthorTextFallback = fallbackBefore; view.Language = langBefore; view.Rebuild(); }
    }

    static ExportResult ExportCore(SessionView view, GuideOptions o, string lang)
    {
        var res = new ExportResult { Unreviewed = view.UnreviewedCount };
        if (res.Unreviewed > 0 && !view.Headless)
            throw new InvalidOperationException(lang == "de" ? $"{res.Unreviewed} auffällige Eingaben sind noch nicht durchgesehen (Durchsicht der Eingaben)." : $"{res.Unreviewed} flagged inputs are not reviewed yet.");
        GuidePresets.Apply(o.Preset, o, null);
        var doc = Build(view, o, lang);
        string title = view.Title;
        string fmt = o.Format switch { "markdown" => "md", var f => f };
        if (fmt == "bugreport" && view.UnreviewedCount > 0)
            throw new InvalidOperationException(lang == "de" ? $"Fehlerbericht erst nach der Durchsicht: {view.UnreviewedCount} auffällige Eingaben sind ungeprüft." : $"Bug report only after the review: {view.UnreviewedCount} flagged inputs are not reviewed.");
        if (fmt == "tour") o.MouseMarks = false;   // die Klickfläche zeigt die Stelle – keine eingezeichneten Mausmarken
        string ext = fmt switch { "md" or "bugreport" => "md", "docx" => "docx", "pdf" => "pdf", "gif" => "gif", "mp4" => "mp4", "pptx" => "pptx", _ => "html" };
        string sub = fmt switch { "md" => "markdown", "bugreport" => "fehlerbericht", "tour" => "tour", "mp4" => "video", _ => ext };
        string name = fmt == "md" && !o.MdLegacy ? Fmt.AsciiSlug(title, Fmt.AsciiSlug(Settings.OutText.T(lang, "Anleitung", "Guide"), "guide").ToLowerInvariant()) : Fmt.FileSafe(title.Length > 0 ? title : "Anleitung");
        string outPath = o.OutPath ?? Path.Combine(view.Session.Path, "export", sub, name + "." + ext);
        if (Directory.Exists(outPath) || outPath.EndsWith('\\') || outPath.EndsWith('/')) outPath = Path.Combine(outPath, name + "." + ext);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        if (File.Exists(outPath) && !o.Overwrite) throw new IOException("Ziel existiert bereits: " + outPath);
        switch (fmt)
        {
            case "md": Markdown(doc, outPath, o); break;
            case "docx": Docx(doc, outPath); break;
            case "pdf": Pdf(doc, outPath, res); break;
            case "gif": Gif(doc, outPath, o, res); break;
            case "mp4": Video(doc, outPath, o); break;
            case "bugreport": res.Text = BugReport(view, doc, outPath); break;
            case "tour":
                File.WriteAllText(outPath, WithRichCss(Tour(doc, o), doc.UsesRich), new UTF8Encoding(false));
                if (new FileInfo(outPath).Length > 25L << 20) res.Notices.Add(lang == "de" ? "Die Klick-Tour ist größer als 25 MB – zum Verschicken ggf. den Bildausschnitt „Ausschnitt um die Aktion“ oder weniger Schritte wählen." : "The click tour is larger than 25 MB – for sending, consider the \"detail\" view or fewer steps.");
                break;
            case "pptx": Pptx(doc, outPath, o); break;
            default: File.WriteAllText(outPath, WithRichCss(Html(doc, o), doc.UsesRich), new UTF8Encoding(false)); break;
        }
        res.Files.Add(outPath); res.Path = outPath;
        return res;
    }

    // =====================================================================================
    // Inhalt sammeln
    // =====================================================================================

    static Doc Build(SessionView view, GuideOptions o, string lang)
    {
        var guide = view.Edits.Obj("guide");
        var d = new Doc { Lang = lang, S = o.Doc ?? DocSettings.From(guide, lang), Compact = o.Compact, Checkboxes = o.Checkboxes };
        var s = d.S;
        GuidePresets.ApplyDoc(o.Preset, s);
        if (!o.Numbering) s.Numbering = "none";
        if (o.Page is "a4" or "letter") s.Page = o.Page;
        if (o.Landscape is { } land) s.Landscape = land;
        d.Title = view.Clean(DocSettings.GuideText(guide, "title", lang) is { Length: > 0 } lt ? lt : view.Title);
        d.Subtitle = view.Clean(s.Subtitle);
        d.Intro = Intro(view, lang);
        d.Outro = view.Clean(DocSettings.GuideText(guide, "outro", lang) ?? "");
        if (view.GuideRich("intro", lang) is { } ir) d.IntroRich = ir.AppendPlain(GoalLines(view, lang));
        d.OutroRich = view.GuideRich("outro", lang);
        // Formate ohne Formatierung (PowerPoint, GIF, Fehlerbericht) lesen den sichtbaren Text
        if (d.IntroRich != null) d.Intro = PlainLines(d.IntroRich);
        if (d.OutroRich != null) d.Outro = PlainLines(d.OutroRich);
        d.Logo = s.LogoBytes();
        d.Prereq = view.Clean(s.Prerequisites).Replace("\r", "").Split('\n').Select(x => x.Trim().TrimStart('-', '*', '•').Trim()).Where(x => x.Length > 0).ToList();
        string viewMode = o.View ?? s.View;
        bool overview = o.Overview ?? s.Overview;

        int chap = 0, inChap = 0, flat = 0, pre = 0;
        AddSteps(view, null);
        // angehängte Aufnahmen: je Aufnahme ein Kapitel (sofern sie nicht selbst mit einem Kapitel beginnt)
        if (o.IncludeAppended)
            foreach (var path in AppendedPaths(view))
            {
                using var sub = SessionView.Open(path, headless: view.Headless, lang: lang);
                sub.AuthorTextFallback = view.AuthorTextFallback; sub.Rebuild();
                if (sub.UnreviewedCount > 0 && !view.Headless)
                    throw new InvalidOperationException((lang == "de" ? "Angehängte Aufnahme „" + sub.Title + "“: " : "Appended recording \"" + sub.Title + "\": ") + (lang == "de" ? $"{sub.UnreviewedCount} auffällige Eingaben sind noch nicht durchgesehen." : $"{sub.UnreviewedCount} flagged inputs are not reviewed yet."));
                AddSteps(sub, sub.Title);
            }

        void AddSteps(SessionView v, string? autoChapter)
        {
        bool first = true;
        var stepList = v.GuideSteps(o.Importance);
        int tick = 0;
        foreach (var st in stepList)
        {
            ExportJob.Tick(++tick, stepList.Count, "Schritte");
            if (st.Kind == "move") continue;
            if (first && autoChapter != null && st.ChapterTitle == null)
            {
                chap++; inChap = 0;
                d.Blocks.Add(new Chapter(s.Numbering == "none" ? "" : chap.ToString(), $"k{chap}", autoChapter, ""));
            }
            first = false;
            if (st.ChapterTitle is { Length: > 0 } ct)
            {
                chap++; inChap = 0;
                d.Blocks.Add(new Chapter(s.Numbering == "none" ? "" : chap.ToString(), $"k{chap}", ct, st.ChapterText ?? "", st.ChapterRich));
            }
            flat++;
            string number = NumberFor(s.Numbering, chap, ref inChap, flat, ref pre);
            byte[]? png = null, afterPng = null; int w = 0, h = 0, aw = 0, ah = 0; RectangleF? hot = null;
            if (o.Images && !st.Redacted && st.ShowImage != false)
            {
                string variant = StepImages.GuideVariant(st, viewMode);
                var mask = ReadLayer.MouseMarks.Mask(o.MouseMarks, o.KeyMarks);
                string renderView = viewMode == "window" ? "window" : "screen";
                // Ergebnis: Nachher-Bild zusätzlich, wenn gewünscht und nicht schon das Schrittbild
                bool wantAfter = o.After == "always" || (o.After == "changed" && StepImages.ScreenChanged(st, 0.02));
                wantAfter = wantAfter && variant != "after" && !st.Manual && !st.ImageReplaced && st.PostFrame != null && st.PostFrame != st.PreFrame;
                string? cacheKey = o.ImageCache?.KeyFor(v, st, variant, renderView, mask, overview, o.ImageProfile, wantAfter, o.Format == "tour", number);
                if (cacheKey != null && o.ImageCache!.TryGet(cacheKey, out var cached))
                    (png, w, h, hot, afterPng, aw, ah) = cached;
                else
                {
                    // Markierungen des Autors zeichnet Render – jeweils nur die, die auf genau diesem Bild gezeichnet wurden
                    using var img = StepImages.Render(v, st, variant, o.ImageProfile, annotate: true, viewMode: renderView, mask: mask, overview: overview, authorAnnotations: true, stepLabel: number);
                    if (img != null)
                    {
                        png = StepImages.Png(img.Bitmap); w = img.Bitmap.Width; h = img.Bitmap.Height;
                        if (o.Format == "tour") hot = StepImages.HotspotPercent(img, st.Kind);
                        if (wantAfter)
                        {
                            using var after = StepImages.Render(v, st, "after", o.ImageProfile, annotate: true, viewMode: renderView, mask: mask, authorAnnotations: true);
                            if (after != null) { afterPng = StepImages.Png(after.Bitmap); aw = after.Bitmap.Width; ah = after.Bitmap.Height; }
                        }
                    }
                    if (cacheKey != null) o.ImageCache!.Put(cacheKey, (png, w, h, hot, afterPng, aw, ah));
                }
            }
            var shown = st.Comments.Where(c => (c.Str("text") ?? "").Length > 0).ToList();
            var notes = shown.Select(c => (c.Str("kind") ?? "note", c.Str("text") ?? "")).ToList();
            List<RichText?>? noteRich = st.NoteRich == null ? null : shown.Select(c => c.Str("id") is { } id && st.NoteRich.TryGetValue(id, out var r) ? r : null).ToList();
            var item = new Item(st, number, $"s{flat}", st.AuthorTitle ?? st.Title, st.AuthorBody ?? st.Guide, png, w, h, notes, st.ImageSize ?? s.ImageSize, hot, afterPng, aw, ah,
                st.AuthorBody != null && !st.Redacted ? st.BodyRich : null, noteRich);
            d.Blocks.Add(item);
            if (autoChapter != null) d.Foreign.Add(item);
        }
        }

        string date = view.Session.StartUtc.ToLocalTime().ToString(Settings.OutText.DateFormat(lang));
        d.Date = view.Session.StartUtc.ToLocalTime().ToString("yyyy-MM-dd");
        if (s.Version.Length > 0) d.Meta.Add((d.L("Version", "Version"), view.Clean(s.Version)));
        if (s.ShowDate) d.Meta.Add((d.L("Stand", "Date"), date));
        if (s.Author.Length > 0) d.Meta.Add((d.L("Autor", "Author"), view.Clean(s.Author)));
        if (s.Audience.Length > 0) d.Meta.Add((d.L("Zielgruppe", "Audience"), view.Clean(s.Audience)));
        d.Meta.Add((d.L("Umfang", "Length"), $"{d.StepCount} {d.L("Schritte", "steps")}" + (d.HasChapters ? $" · {d.Blocks.OfType<Chapter>().Count()} {d.L("Kapitel", "chapters")}" : "")));
        return d;
    }

    /// <summary>Bezeichnung und Farben eines Hinweiskastens: (Label, Balken, Fläche).</summary>
    public static (string label, string bar, string fill) NoteStyle(string kind, string lang) => kind switch
    {
        "tip" => (Settings.OutText.For(lang, "Tipp:", "Tip:", "Astuce :", "Consejo:", "Suggerimento:", "Tip:"), "2F7D4F", "E8F4EC"),
        "warning" => (Settings.OutText.For(lang, "Achtung:", "Warning:", "Attention :", "Advertencia:", "Attenzione:", "Waarschuwing:"), "B26A00", "FFF4DC"),
        "important" => (Settings.OutText.For(lang, "Wichtig:", "Important:", "Important :", "Importante:", "Importante:", "Belangrijk:"), "B4233C", "FBE9EC"),
        "result" => (Settings.OutText.For(lang, "Ergebnis:", "Result:", "Résultat :", "Resultado:", "Risultato:", "Resultaat:"), "3F3D38", "F1F0EC"),
        _ => (Settings.OutText.For(lang, "Hinweis:", "Note:", "Remarque :", "Nota:", "Nota:", "Opmerking:"), "2F5C9C", "EAF0F9")
    };

    static (double, double, double) Rgb(string hex) => (Convert.ToInt32(hex[..2], 16) / 255.0, Convert.ToInt32(hex[2..4], 16) / 255.0, Convert.ToInt32(hex[4..6], 16) / 255.0);

    /// <summary>Ziel und erwartetes Ergebnis als Zeilen unter der Einleitung.</summary>
    static string GoalLines(SessionView view, string lang)
    {
        var parts = new List<string>();
        var goal = view.Goal();
        if (goal?.Str("description") is { } d) parts.Add(Settings.OutText.T(lang, "Ziel: ", "Goal: ") + d);
        if (goal?.Str("expected") is { } e) parts.Add(Settings.OutText.T(lang, "Erwartetes Ergebnis: ", "Expected result: ") + e);
        return string.Join("\n", parts);
    }

    static string Intro(SessionView view, string lang)
    {
        var parts = new List<string>();
        if (DocSettings.GuideText(view.Edits.Obj("guide"), "intro", lang) is { Length: > 0 } intro) parts.Add(view.Clean(intro));
        var goal = view.Goal();
        if (goal?.Str("description") is { } d) parts.Add(Settings.OutText.T(lang, "Ziel: ", "Goal: ") + d);
        if (goal?.Str("expected") is { } e) parts.Add(Settings.OutText.T(lang, "Erwartetes Ergebnis: ", "Expected result: ") + e);
        return string.Join("\n", parts);
    }

    /// <summary>Pfade der angehängten Aufnahmen (guide.append: absolut oder relativ zum übergeordneten Ordner), nur vorhandene.</summary>
    public static List<string> AppendedPaths(SessionView view)
    {
        var list = new List<string>();
        // Aus einem .steprec-Archiv (fremde Herkunft) keine anderen Aufnahmen dieses Rechners in den Export ziehen
        if (view.Session.ReadOnly) return list;
        string parent = Path.GetDirectoryName(view.Session.Path.TrimEnd('\\', '/')) ?? "";
        foreach (var a in (view.Edits.Obj("guide")?.Arr("append") ?? new JsonArray()).OfType<JsonObject>())
        {
            string? p = a.Str("path");
            if (p == null || !Session.LooksLikeSession(p)) p = a.Str("rel") is { Length: > 0 } rel ? Path.GetFullPath(Path.Combine(parent, rel)) : null;
            if (p != null && Session.LooksLikeSession(p) && !string.Equals(Path.GetFullPath(p).TrimEnd('\\'), Path.GetFullPath(view.Session.Path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) list.Add(p);
        }
        return list;
    }

    static string Heading(string number, string title) => number.Length > 0 ? number + "  " + title : title;

    // =====================================================================================
    // HTML
    // =====================================================================================

    static string Html(Doc d, GuideOptions o)
    {
        var e = Fmt.HtmlEscape;
        string P(string text) => e(text).Replace("\n", "<br>");
        var sb = new StringBuilder();
        sb.Append($"<!DOCTYPE html>\n<html lang=\"{d.Lang}\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\">\n<title>{e(d.Title)}</title>\n");
        sb.Append("<style>\n:root{--accent:" + d.S.Accent + "}\n");
        sb.Append("""
:root{--fg:#1c1b19;--muted:#6b6962;--line:#dedcd5;--bg:#fff;--soft:#f6f5f1}
@media (prefers-color-scheme:dark){:root{--fg:#ecebe7;--muted:#a3a19a;--line:#36352f;--bg:#161615;--soft:#1f1f1d;--accent:color-mix(in srgb,var(--accent) 65%,white)}}
*{box-sizing:border-box}
body{font-family:"Segoe UI",system-ui,sans-serif;max-width:1000px;margin:0 auto;padding:32px 20px 48px;color:var(--fg);background:var(--bg);line-height:1.55}
h1{font-size:2em;line-height:1.2;margin:0 0 6px;color:var(--accent)} .sub{font-size:1.2em;color:var(--muted);margin:0 0 18px}
.cover{min-height:88vh;display:flex;flex-direction:column;justify-content:center;border-left:6px solid var(--accent);padding:0 0 0 28px;margin-bottom:32px}
.cover h1{font-size:2.6em} img.logo{max-height:64px;max-width:240px;display:block;margin:0 0 22px} header img.logo{max-height:44px;margin-bottom:14px}
.cover img.logo{max-height:90px;max-width:320px;margin-bottom:40px}
.meta{display:flex;flex-wrap:wrap;gap:6px 22px;color:var(--muted);font-size:.9em;margin:0 0 26px}
.meta b{color:var(--fg);font-weight:600;margin-left:6px}
.box{background:var(--soft);border-radius:6px;padding:14px 18px;margin:0 0 22px}
.box h2{font-size:1em;margin:0 0 6px;text-transform:uppercase;letter-spacing:.06em;color:var(--muted)}
.box ul{margin:4px 0 0;padding-left:20px}
nav.toc ol{list-style:none;padding:0;margin:4px 0 0} nav.toc li{margin:3px 0} nav.toc ol ol{padding-left:22px}
nav.toc a{color:var(--fg);text-decoration:none} nav.toc a:hover{color:var(--accent)} nav.toc .n{display:inline-block;min-width:3em;color:var(--accent);font-variant-numeric:tabular-nums}
h2.chapter{font-size:1.5em;margin:40px 0 6px;padding-bottom:8px;border-bottom:2px solid var(--accent)} h2.chapter .n{color:var(--accent);margin-right:12px}
.chtext{color:var(--fg);margin:0 0 6px}
.step{border-top:1px solid var(--line);padding:20px 0;break-inside:avoid}
.step h3{font-size:1.12em;margin:0 0 6px;display:flex;gap:12px;align-items:baseline}
.step h3 .n{flex:none;min-width:2.2em;height:1.9em;line-height:1.9em;padding:0 .5em;border-radius:5px;background:var(--accent);color:#fff;font-size:.85em;text-align:center;font-variant-numeric:tabular-nums}
.step p{margin:0 0 8px}
.step.side{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1.35fr);gap:24px;align-items:start}
figure{margin:10px 0 0} figure img{max-width:100%;height:auto;border:1px solid var(--line);border-radius:4px;display:block}
figcaption{color:var(--muted);font-size:.85em;margin:0 0 4px}
.note{border-left:4px solid var(--c);background:color-mix(in srgb,var(--c) 10%,var(--bg));padding:8px 12px;margin:8px 0;border-radius:3px}
.note b{color:var(--c)}
.ev{color:var(--muted);font-size:.85em} .redacted{color:var(--muted);font-style:italic}
.outro{margin-top:28px}
footer{margin-top:36px;padding-top:12px;border-top:1px solid var(--line);color:var(--muted);font-size:.85em;display:flex;justify-content:space-between;gap:12px;flex-wrap:wrap}
.zoom{display:block;position:relative;cursor:zoom-in}
.zoom input{position:absolute;opacity:0;pointer-events:none;width:0;height:0}
.zoom .zh{display:block;color:var(--muted);font-size:.8em;margin-top:3px}
.zoom input:checked+img{position:fixed;inset:0;margin:auto;max-width:96vw;max-height:94vh;width:auto;height:auto;z-index:11;border:0;border-radius:6px;cursor:zoom-out}
.zoom:has(input:checked){position:fixed;inset:0;z-index:10;background:rgba(10,11,14,.88);cursor:zoom-out;margin:0}
.zoom .zc{display:none}
.zoom:has(input:checked) .zo{display:none}
.zoom:has(input:checked) .zc{display:block;position:fixed;left:0;right:0;bottom:12px;text-align:center;color:#ddd}
body.compact .step{padding:8px 0} body.compact .step h3{font-size:1em;margin:0 0 2px} body.compact .step p{margin:0 0 4px}
.step h3 input.done{flex:none;width:1.15em;height:1.15em;margin:.15em 0 0;accent-color:var(--accent)}
@page{margin:16mm 15mm}
@media print{body{max-width:none;padding:0}.cover{min-height:auto;height:250mm;page-break-after:always;margin:0}.step{page-break-inside:avoid}.zoom .zh{display:none}nav.toc{page-break-after:always}h2.chapter{page-break-after:avoid}}
</style>
</head>

""");
        sb.Append(d.Compact ? "<body class=\"compact\">\n" : "<body>\n");
        string metaHtml = string.Concat(d.Meta.Select(m => $"<span>{e(m.label)}<b>{e(m.value)}</b></span>"));
        string logoHtml = d.Logo != null ? $"<img class=\"logo\" alt=\"Logo\" src=\"data:image/png;base64,{Convert.ToBase64String(d.Logo)}\">" : "";
        if (d.S.Cover)
            sb.Append($"<header class=\"cover\">{logoHtml}<h1>{e(d.Title)}</h1>{(d.Subtitle.Length > 0 ? $"<p class=\"sub\">{e(d.Subtitle)}</p>" : "")}<div class=\"meta\">{metaHtml}</div></header>\n");
        else
            sb.Append($"<header>{logoHtml}<h1>{e(d.Title)}</h1>{(d.Subtitle.Length > 0 ? $"<p class=\"sub\">{e(d.Subtitle)}</p>" : "")}<div class=\"meta\">{metaHtml}</div></header>\n");

        if (d.ShowToc)
        {
            sb.Append($"<nav class=\"toc box\"><h2>{d.L("Inhalt", "Contents")}</h2><ol>\n");
            bool open = false;
            foreach (var b in d.Blocks)
            {
                if (b is Chapter c) { if (open) sb.Append("</ol></li>\n"); sb.Append($"<li><a href=\"#{c.Anchor}\"><span class=\"n\">{e(c.Number)}</span>{e(c.Title)}</a><ol>\n"); open = true; }
                else if (b is Item it) sb.Append($"<li><a href=\"#{it.Anchor}\"><span class=\"n\">{e(it.Number)}</span>{e(it.Title)}</a></li>\n");
            }
            if (open) sb.Append("</ol></li>\n");
            sb.Append("</ol></nav>\n");
        }
        if (d.IntroRich != null) sb.Append($"<section class=\"box\"><h2>{d.L("Über diese Anleitung", "About this guide")}</h2>{HtmlRich(d.IntroRich)}</section>\n");
        else if (d.Intro.Length > 0) sb.Append($"<section class=\"box\"><h2>{d.L("Über diese Anleitung", "About this guide")}</h2><p style=\"margin:0\">{P(d.Intro)}</p></section>\n");
        if (d.Prereq.Count > 0) sb.Append($"<section class=\"box\"><h2>{d.L("Voraussetzungen", "Prerequisites")}</h2><ul>{string.Concat(d.Prereq.Select(x => $"<li>{e(x)}</li>"))}</ul></section>\n");

        string enlarge = d.L("Klicken zum Vergrößern", "Click to enlarge"), close = d.L("Klicken zum Schließen", "Click to close");
        foreach (var b in d.Blocks)
        {
            if (b is Chapter c)
            {
                sb.Append($"<h2 class=\"chapter\" id=\"{c.Anchor}\">{(c.Number.Length > 0 ? $"<span class=\"n\">{e(c.Number)}</span>" : "")}{e(c.Title)}</h2>\n");
                if (c.Body != null) sb.Append(HtmlRich(c.Body, "rt chtext")).Append('\n');
                else if (c.Text.Length > 0) sb.Append($"<p class=\"chtext\">{P(c.Text)}</p>\n");
                continue;
            }
            var it = (Item)b;
            bool side = d.S.Layout == "side" && it.Png != null;
            sb.Append($"<section class=\"step{(side ? " side" : "")}\" id=\"{it.Anchor}\">\n<div>\n");
            sb.Append($"<h3>{(d.Checkboxes ? $"<input type=\"checkbox\" class=\"done\" aria-label=\"{d.L("erledigt", "done")}\">" : "")}{(it.Number.Length > 0 ? $"<span class=\"n\">{e(it.Number)}</span>" : "")}<span>{e(it.Title)}</span></h3>\n");
            if (it.Step.Redacted) sb.Append($"<p class=\"redacted\">{e(it.Text)}</p>\n");
            else if (it.Body != null && it.Text.Length > 0) sb.Append(HtmlRich(it.Body)).Append('\n');
            else if (it.Text.Length > 0 && it.Text != it.Title) sb.Append($"<p>{P(it.Text)}</p>\n");
            for (int ni = 0; ni < it.Notes.Count; ni++)
            {
                var (kind, text) = it.Notes[ni];
                var (label, bar, _) = NoteStyle(kind, d.Lang);
                if (it.NoteBody(ni) is { } nr) sb.Append($"<div class=\"note\" style=\"--c:#{bar}\">{HtmlRich(nr, "rt", $"<b>{e(label)}</b> ")}</div>\n");
                else sb.Append($"<div class=\"note\" style=\"--c:#{bar}\"><b>{e(label)}</b> {P(text)}</div>\n");
            }
            if (o.ShowEvidence) sb.Append($"<div class=\"ev\">{d.L("Beleg", "Evidence")}: {e(it.Step.EvidenceLevel)}</div>\n");
            if (!side) sb.Append("</div>\n");
            if (it.Png != null)
            {
                string style = side ? "" : $" style=\"max-width:{(int)(DocSettings.Factor(it.Size) * 100)}%\"";
                string Fig(byte[] png, int w, int h, string alt, string? caption) =>
                    $"<figure{style}>{(caption != null ? $"<figcaption>{e(caption)}</figcaption>" : "")}<label class=\"zoom\" title=\"{enlarge}\"><input type=\"checkbox\"><img alt=\"{e(alt)}\" width=\"{w}\" height=\"{h}\" src=\"data:image/png;base64,{Convert.ToBase64String(png)}\">" +
                    $"<span class=\"zh zo\">{enlarge}</span><span class=\"zh zc\">{close}</span></label></figure>\n";
                if (side) sb.Append("</div>\n");
                bool two = it.AfterPng != null;
                if (side && two) sb.Append("<div>\n");
                sb.Append(Fig(it.Png, it.W, it.H, it.Title, null));
                if (two) sb.Append(Fig(it.AfterPng!, it.AW, it.AH, it.Title + " – " + d.L("Ergebnis", "Result"), d.L("Ergebnis", "Result")));
                if (side && two) sb.Append("</div>\n");
            }
            else if (side) sb.Append("</div>\n");
            sb.Append("</section>\n");
        }
        if (d.OutroRich != null) sb.Append($"<section class=\"box outro\"><h2>{d.L("Abschluss", "Finally")}</h2>{HtmlRich(d.OutroRich)}</section>\n");
        else if (d.Outro.Length > 0) sb.Append($"<section class=\"box outro\"><h2>{d.L("Abschluss", "Finally")}</h2><p style=\"margin:0\">{P(d.Outro)}</p></section>\n");
        sb.Append($"<footer><span>{e(d.S.Footer.Length > 0 ? d.S.Footer : d.Title)}</span><span>{d.L("Erstellt mit", "Created with")} {AppInfo.DisplayName}</span></footer>\n</body>\n</html>\n");
        return sb.ToString();
    }

    // =====================================================================================
    // Markdown
    // =====================================================================================

    /// <summary>Frühere Markdown-Form (Ordner „bilder“, Überschrift je Schritt, alles maskiert) – Exportoption für eine Übergangszeit.</summary>
    static void MarkdownLegacy(Doc d, string outPath)
    {
        string dir = Path.GetDirectoryName(outPath)!;
        string imgDir = Path.Combine(dir, "bilder");
        Directory.CreateDirectory(imgDir);
        var m = Fmt.MdEscape;
        var sb = new StringBuilder();
        if (d.Logo != null) { File.WriteAllBytes(Path.Combine(imgDir, "logo.png"), d.Logo); sb.Append("![Logo](bilder/logo.png)\n\n"); }
        sb.Append("# ").Append(m(d.Title)).Append("\n\n");
        if (d.Subtitle.Length > 0) sb.Append("*").Append(m(d.Subtitle)).Append("*\n\n");
        sb.Append(string.Join(" · ", d.Meta.Select(x => $"{x.label}: **{m(x.value)}**"))).Append("\n\n");
        if (d.ShowToc)
        {
            sb.Append("## ").Append(d.L("Inhalt", "Contents")).Append("\n\n");
            foreach (var b in d.Blocks)
                if (b is Chapter c) sb.Append("- **").Append(m(Heading(c.Number, c.Title))).Append("**\n");
                else if (b is Item it) sb.Append(d.HasChapters ? "  - " : "- ").Append(m(Heading(it.Number, it.Title))).Append('\n');
            sb.Append('\n');
        }
        if (d.Intro.Length > 0) sb.Append("## ").Append(d.L("Über diese Anleitung", "About this guide")).Append("\n\n").Append(m(d.Intro)).Append("\n\n");
        if (d.Prereq.Count > 0) sb.Append("## ").Append(d.L("Voraussetzungen", "Prerequisites")).Append("\n\n").Append(string.Concat(d.Prereq.Select(x => "- " + m(x) + "\n"))).Append('\n');
        int img = 0;
        foreach (var b in d.Blocks)
        {
            if (b is Chapter c)
            {
                sb.Append("## ").Append(m(Heading(c.Number, c.Title))).Append("\n\n");
                if (c.Text.Length > 0) sb.Append(m(c.Text)).Append("\n\n");
                continue;
            }
            var it = (Item)b;
            if (d.Checkboxes || d.Compact)
            {
                // Kurzfassung/Checkliste: ein Listenpunkt je Schritt
                string body = it.Text.Length > 0 && it.Text != it.Title ? " – " + m(it.Text.Replace("\n", " ")) : "";
                sb.Append(d.Checkboxes ? "- [ ] " : (it.Number.Length > 0 ? it.Number + ". " : "- ")).Append("**").Append(m(it.Title)).Append("**").Append(body).Append('\n');
                foreach (var (kind, text) in it.Notes) sb.Append("  > **").Append(NoteStyle(kind, d.Lang).label).Append("** ").Append(m(text)).Append('\n');
                continue;
            }
            sb.Append(d.HasChapters ? "### " : "## ").Append(m(Heading(it.Number.Length > 0 ? it.Number + "." : "", it.Title))).Append("\n\n");
            if (it.Text.Length > 0 && it.Text != it.Title) sb.Append(m(it.Text)).Append("\n\n");
            foreach (var (kind, text) in it.Notes) sb.Append("> **").Append(NoteStyle(kind, d.Lang).label).Append("** ").Append(m(text)).Append("\n\n");
            if (it.Png != null)
            {
                string file = $"schritt-{++img:000}.png";
                File.WriteAllBytes(Path.Combine(imgDir, file), it.Png);
                sb.Append("![").Append(m(it.Title)).Append("](bilder/").Append(file).Append(")\n\n");
                if (it.AfterPng != null)
                {
                    string af = $"schritt-{img:000}-nachher.png";
                    File.WriteAllBytes(Path.Combine(imgDir, af), it.AfterPng);
                    sb.Append('*').Append(d.L("Ergebnis", "Result")).Append(":*\n\n![").Append(m(it.Title + " – " + d.L("Ergebnis", "Result"))).Append("](bilder/").Append(af).Append(")\n\n");
                }
            }
        }
        if (d.Checkboxes || d.Compact) sb.Append('\n');
        if (d.Outro.Length > 0) sb.Append("## ").Append(d.L("Abschluss", "Finally")).Append("\n\n").Append(m(d.Outro)).Append("\n\n");
        if (d.S.Footer.Length > 0) sb.Append("---\n\n").Append(m(d.S.Footer)).Append('\n');
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
    }

    // =====================================================================================
    // Word
    // =====================================================================================

    static void Docx(Doc d, string outPath)
    {
        var x = new SimpleDocx { Title = d.Title, Language = Settings.OutText.Tag(d.Lang), Accent = d.S.AccentHex, Footer = d.S.Footer.Length > 0 ? d.S.Footer : d.Title };
        var (pw, ph) = d.S.PageTwips();
        x.SetPage(pw, ph);
        // Eigene Word-Vorlage (09 §7.3): ihre Seiteneinrichtung, Formatvorlagen, Kopf- und Fußzeilen; .dotm wird abgelehnt
        bool templateTitle = false;
        if (d.S.WordTemplateBytes() is { } tpl)
        {
            x.UseTemplate(tpl);
            string Meta(string de, string en) => d.Meta.FirstOrDefault(m => m.label == d.L(de, en)).value ?? "";
            x.Fields["SR.Title"] = d.Title; x.Fields["SR.Subtitle"] = d.Subtitle; x.Fields["SR.Author"] = Meta("Autor", "Author"); x.Fields["SR.Version"] = Meta("Version", "Version");
            x.Fields["SR.Date"] = DateTime.TryParseExact(d.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt) ? dt.ToString(d.Lang == "de" ? "dd.MM.yyyy" : "yyyy-MM-dd") : d.Date;
            // Hat die Vorlage ein Titelfeld, bringt sie Titelblock bzw. Deckblatt selbst mit
            templateTitle = x.TemplateHasField("SR.Title");
        }
        if (!templateTitle)
        {
            if (d.Logo != null) { using var lm = new MemoryStream(d.Logo); using var lb = new Bitmap(lm); x.Image(d.Logo, "png", lb.Width, lb.Height, "Logo", d.S.Cover ? 6 : 4, d.S.Cover ? 2.6 : 1.6, border: false); }
            x.Heading(d.Title, 0);
            if (d.Subtitle.Length > 0) x.Subtitle(d.Subtitle);
            foreach (var (label, value) in d.Meta) x.MetaLine(label, value);
            if (d.S.Cover) x.PageBreak();
        }
        // Bildgrößen wachsen mit dem Satzspiegel (A4 hoch: 17 cm breit) und bleiben auf einer Seite
        double cwCm = x.TextWidthCm, maxHCm = x.TextHeightCm - 4;
        double Wd(double cm) => cwCm == 17 ? cm : Math.Round(cm * cwCm / 17, 2);
        // Sprungziele je Block (Kapitel = Überschrift 1, Schritt = Überschrift 2) – das Inhaltsverzeichnis verweist darauf
        var marks = new Dictionary<object, string>();
        for (int i = 0; i < d.Blocks.Count; i++) marks[d.Blocks[i]] = SimpleDocx.TocBookmark(i + 1);
        if (d.ShowToc)
        {
            x.Toc(d.L("Inhalt", "Contents"), d.Blocks.Select(b => b is Chapter c ? (Heading(c.Number, c.Title), 1, marks[b]) : (Heading(((Item)b).Number, ((Item)b).Title), 2, marks[b])));
            if (d.S.Cover) x.PageBreak();
        }
        if (d.IntroRich != null) { x.Heading(d.L("Über diese Anleitung", "About this guide"), 1); x.RichBlocks(d.IntroRich.Doc); }
        else if (d.Intro.Length > 0) { x.Heading(d.L("Über diese Anleitung", "About this guide"), 1); x.Paragraph(d.Intro); }
        if (d.Prereq.Count > 0) { x.Heading(d.L("Voraussetzungen", "Prerequisites"), 1); foreach (var p in d.Prereq) x.Bullet(p); }
        foreach (var b in d.Blocks)
        {
            if (b is Chapter c)
            {
                x.Heading(Heading(c.Number, c.Title), 1, marks[b]);
                if (c.Body != null) x.RichBlocks(c.Body.Doc);
                else if (c.Text.Length > 0) x.Paragraph(c.Text);
                continue;
            }
            var it = (Item)b;
            void Text()
            {
                if (d.Compact || d.Checkboxes)
                {
                    x.Rich((d.Checkboxes ? "☐  " : it.Number.Length > 0 ? it.Number + "  " : "", false), (it.Title, true), (it.Text.Length > 0 && it.Text != it.Title ? " – " + it.Text.Replace("\n", " ") : "", false));
                    foreach (var (kind, text) in it.Notes) { var (label, bar, fill) = NoteStyle(kind, d.Lang); x.Note(label, text, bar, fill); }
                    return;
                }
                x.Heading(Heading(it.Number, it.Title), 2, marks[b]);
                if (it.Body != null && it.Text.Length > 0) x.RichBlocks(it.Body.Doc);
                else if (it.Text.Length > 0 && it.Text != it.Title) x.Paragraph(it.Text, italic: it.Step.Redacted);
                for (int ni = 0; ni < it.Notes.Count; ni++)
                {
                    var (kind, text) = it.Notes[ni];
                    var (label, bar, fill) = NoteStyle(kind, d.Lang);
                    if (it.NoteBody(ni) is { } nr) x.NoteRich(label, nr.Doc, bar, fill); else x.Note(label, text, bar, fill);
                }
            }
            void After(double maxW, double maxH)
            {
                if (it.AfterPng == null) return;
                x.Paragraph(d.L("Ergebnis", "Result"), italic: true, color: "666666");
                x.Image(it.AfterPng, "png", it.AW, it.AH, it.Title + " – " + d.L("Ergebnis", "Result"), maxW, maxH);
            }
            if (it.Png != null && d.S.Layout == "side")
                x.SideBySide(Text, () => { x.Image(it.Png, "png", it.W, it.H, it.Title, Wd(9.2), Math.Min(9, maxHCm)); After(Wd(9.2), Math.Min(9, maxHCm)); }, 42);
            else
            {
                Text();
                double mw = cwCm * DocSettings.Factor(it.Size), mh = Math.Min(cwCm * DocSettings.Factor(it.Size) * 0.8 + 3, maxHCm);
                if (it.Png != null) x.Image(it.Png, "png", it.W, it.H, it.Title, mw, mh);
                After(mw, mh);
            }
        }
        if (d.OutroRich != null) { x.Heading(d.L("Abschluss", "Finally"), 1); x.RichBlocks(d.OutroRich.Doc); }
        else if (d.Outro.Length > 0) { x.Heading(d.L("Abschluss", "Finally"), 1); x.Paragraph(d.Outro); }
        x.Save(outPath);
    }

    // =====================================================================================
    // PDF (zwei Durchläufe: der erste ermittelt die Seitenzahlen fürs Inhaltsverzeichnis)
    // =====================================================================================

    static void Pdf(Doc d, string outPath, ExportResult res)
    {
        var pdf = PdfDocument(d);
        File.WriteAllBytes(outPath, pdf.ToBytes());
        res.Warnings.AddRange(pdf.Warnings);
    }

    /// <summary>PDF im Speicher aufbauen (zwei Durchläufe für die Seitenzahlen im Inhaltsverzeichnis); Bytes über <see cref="SimplePdf.ToBytes"/>.</summary>
    static SimplePdf PdfDocument(Doc d)
    {
        var jpegs = new Dictionary<Item, (byte[] jpg, int w, int h)>();
        var afterJpegs = new Dictionary<Item, (byte[] jpg, int w, int h)>();
        (byte[] jpg, int w, int h)? logo = null;
        if (d.Logo != null)
        {
            using var lm = new MemoryStream(d.Logo); using var lb = new Bitmap(lm);
            using var flat = new Bitmap(lb.Width, lb.Height); using (var g = Graphics.FromImage(flat)) { g.Clear(Color.White); g.DrawImage(lb, 0, 0, lb.Width, lb.Height); }
            logo = (StepImages.Jpeg(flat, 92), lb.Width, lb.Height);
        }
        foreach (var it in d.Blocks.OfType<Item>())
            if (it.Png != null)
            {
                jpegs[it] = JpegOf(it.Png);
                if (it.AfterPng != null) afterJpegs[it] = JpegOf(it.AfterPng);
            }
        var pages = new Dictionary<string, int>();
        PdfRender(d, jpegs, afterJpegs, logo, pages, null);
        return PdfRender(d, jpegs, afterJpegs, logo, new Dictionary<string, int>(), pages);
    }

    /// <summary>JPEG eines Schrittbilds; je PNG-Instanz zwischengespeichert (der Bildspeicher der Seitenansicht liefert dieselbe Instanz).</summary>
    static (byte[] jpg, int w, int h) JpegOf(byte[] png)
    {
        if (JpegMemo.TryGetValue(png, out var hit)) return hit.Value;
        using var ms = new MemoryStream(png);
        using var bmp = new Bitmap(ms);
        var r = (StepImages.Jpeg(bmp, 88), bmp.Width, bmp.Height);
        JpegMemo.AddOrUpdate(png, new StrongBox<(byte[] jpg, int w, int h)>(r));
        return r;
    }
    static readonly ConditionalWeakTable<byte[], StrongBox<(byte[] jpg, int w, int h)>> JpegMemo = new();

    static SimplePdf PdfRender(Doc d, Dictionary<Item, (byte[] jpg, int w, int h)> jpegs, Dictionary<Item, (byte[] jpg, int w, int h)> afterJpegs, (byte[] jpg, int w, int h)? logo, Dictionary<string, int> record, Dictionary<string, int>? known)
    {
        var acc = d.S.AccentRgb();
        var ink = (0.11, 0.11, 0.10); var muted = (0.42, 0.41, 0.38); var line = (0.85, 0.84, 0.81);
        var (pw, ph) = d.S.PagePoints();
        var pdf = new SimplePdf(pw, ph) { Title = d.Title, Footer = d.S.Footer.Length > 0 ? d.S.Footer : d.Title, Language = Settings.OutText.Tag(d.Lang) };
        double cw = pdf.ContentWidth;
        // Bildhöhe höchstens Satzspiegel abzüglich Platz für den Schritttext (A4 hoch: wie bisher 560 · Faktor + 60)
        double maxImgH = pdf.ContentHeight - 100;
        // Lagen auf dem Deckblatt sind für A4 hoch festgelegt und wachsen mit der Seitenhöhe mit
        double Vy(double y) => Math.Round(y * pdf.PageH / SimplePdf.A4H, 2);

        // Deckblatt oder Kopf
        if (d.S.Cover)
        {
            pdf.NoFooter.Add(0);
            pdf.FillRect(0, pdf.PageH - 14, pdf.PageW, 14, acc);
            pdf.FillRect(pdf.Margin, Vy(250), 5, Vy(580) - Vy(250), acc);
            if (logo is { } lg) { pdf.SetY(Vy(700)); pdf.Image(lg.jpg, lg.w, lg.h, 200, 70, pdf.Margin + 22, border: false); }
            pdf.SetY(Vy(560));
            pdf.Paragraph(d.Title, 28, true, 22, 10, ink);
            if (d.Subtitle.Length > 0) pdf.Paragraph(d.Subtitle, 15, false, 22, 22, muted);
            foreach (var (label, value) in d.Meta) pdf.Paragraph(label + ":  " + value, 10.5, false, 22, 3, muted);
            pdf.NewPage();
        }
        else
        {
            if (logo is { } lg) pdf.Image(lg.jpg, lg.w, lg.h, 150, 44, border: false);
            pdf.Paragraph(d.Title, 22, true, 0, 4, acc);
            if (d.Subtitle.Length > 0) pdf.Paragraph(d.Subtitle, 13, false, 0, 6, muted);
            pdf.Paragraph(string.Join("   ·   ", d.Meta.Select(m => m.label + ": " + m.value)), 9, false, 0, 16, muted);
        }

        // Inhaltsverzeichnis mit Seitenzahlen
        if (d.ShowToc)
        {
            pdf.Paragraph(d.L("Inhalt", "Contents"), 15, true, 0, 8, acc);
            foreach (var b in d.Blocks)
            {
                string key = b is Chapter c ? c.Anchor : ((Item)b).Anchor;
                string num = b is Chapter c1 ? c1.Number : ((Item)b).Number, title = b is Chapter c2 ? c2.Title : ((Item)b).Title;
                bool isChap = b is Chapter;
                double size = isChap ? 11 : 10, indent = isChap || !d.HasChapters ? 0 : 18, lh = size * 1.6;
                pdf.EnsureSpace(lh);
                pdf.SetY(pdf.Y - lh);
                string pg = known != null && known.TryGetValue(key, out var p) ? p.ToString() : "00";
                double numW = num.Length > 0 ? 34 : 0;
                pdf.DrawText(pdf.Margin + indent, pdf.Y + 3, num, size, isChap, acc);
                string t = title;
                double maxT = cw - indent - numW - 40;
                while (t.Length > 4 && SimplePdf.TextWidth(t, size, isChap) > maxT) t = t[..^2];
                if (t != title) t = t.TrimEnd() + "…";
                pdf.DrawText(pdf.Margin + indent + numW, pdf.Y + 3, t, size, isChap, ink);
                pdf.DrawText(pdf.Margin + cw - SimplePdf.TextWidth(pg, size), pdf.Y + 3, pg, size, false, muted);
                pdf.LinkTo(pdf.Margin + indent, pdf.Y, cw - indent, lh, key);
            }
            pdf.NewPage();
        }

        void Box(string heading, string text, IEnumerable<string>? bullets = null, RichText? rich = null)
        {
            pdf.Paragraph(heading.ToUpperInvariant(), 9, true, 0, 4, muted);
            if (rich != null) pdf.RichBlocks(rich.Doc, 10.5, 0, 4, ink);
            else if (text.Length > 0) pdf.Paragraph(text, 10.5, false, 0, 4, ink);
            if (bullets != null) foreach (var x in bullets) pdf.Paragraph("•  " + x, 10.5, false, 6, 2, ink);
            pdf.Space(12);
        }
        if (d.Intro.Length > 0 || d.IntroRich != null) Box(d.L("Über diese Anleitung", "About this guide"), d.Intro, rich: d.IntroRich);
        if (d.Prereq.Count > 0) Box(d.L("Voraussetzungen", "Prerequisites"), "", d.Prereq);

        foreach (var b in d.Blocks)
        {
            if (b is Chapter c)
            {
                pdf.EnsureSpace(120);
                pdf.Space(10);
                record[c.Anchor] = pdf.PageCount;
                pdf.Dest(c.Anchor);
                pdf.Bookmark(Heading(c.Number, c.Title), 0, c.Anchor);
                pdf.Paragraph(Heading(c.Number, c.Title), 17, true, 0, 4, acc);
                pdf.Line(pdf.Margin, pdf.Y, pdf.Margin + cw, pdf.Y, acc, 1.2);
                pdf.Space(10);
                if (c.Body != null) pdf.RichBlocks(c.Body.Doc, 10.5, 0, 10, ink);
                else if (c.Text.Length > 0) pdf.Paragraph(c.Text, 10.5, false, 0, 10, ink);
                continue;
            }
            var it = (Item)b;
            if (d.Compact || d.Checkboxes)
            {
                // Kurzfassung/Checkliste: eine Zeile je Schritt, Kästchen statt Nummernmarke
                double lead = d.Checkboxes ? 20 : it.Number.Length > 0 ? Math.Max(18, SimplePdf.TextWidth(it.Number + ".", 10.5, true) + 8) : 0;
                double th = SimplePdf.MeasureText(it.Title, cw - lead, 10.5, true);
                pdf.EnsureSpace(th + 14);
                record[it.Anchor] = pdf.PageCount;
                pdf.Dest(it.Anchor);
                pdf.Bookmark(Heading(it.Number, it.Title), d.HasChapters ? 1 : 0, it.Anchor);
                double top2 = pdf.Y;
                if (d.Checkboxes) pdf.StrokeRect(pdf.Margin + 1, top2 - 12, 10, 10, ink, 0.8);
                else if (it.Number.Length > 0) pdf.DrawText(pdf.Margin, top2 - 10, it.Number + ".", 10.5, true, acc);
                pdf.SetY(top2 + 1);
                pdf.Paragraph(it.Title, 10.5, true, lead, 1, ink, cw - lead);
                if (it.Text.Length > 0 && it.Text != it.Title) pdf.Paragraph(it.Text, 9.5, false, lead, 2, muted, cw - lead);
                foreach (var (kind, text) in it.Notes) { var (label, bar, fill) = NoteStyle(kind, d.Lang); pdf.Callout(label, text, Rgb(bar), Rgb(fill)); }
                pdf.Space(5);
                continue;
            }
            bool hasImg = jpegs.TryGetValue(it, out var jp);
            bool side = hasImg && d.S.Layout == "side";
            double numW = it.Number.Length > 0 ? Math.Max(22, SimplePdf.TextWidth(it.Number, 10, true) + 10) : 0;
            double textW = side ? cw * 0.42 : cw;
            double titleH = SimplePdf.MeasureText(it.Title, textW - numW - 8, 12.5, true);
            pdf.EnsureSpace(hasImg && !side ? Math.Min(260, titleH + 200) : titleH + 60);
            pdf.Line(pdf.Margin, pdf.Y - 2, pdf.Margin + cw, pdf.Y - 2, line);
            pdf.Space(10);
            record[it.Anchor] = pdf.PageCount;

            double top = pdf.Y;
            pdf.Bookmark(Heading(it.Number, it.Title), d.HasChapters ? 1 : 0, it.Anchor);
            double imgH = 0, imgW = 0;
            if (side)
            {
                double maxW = cw - textW - 16;
                double scale = Math.Min(Math.Min(maxW / jp.w, 360.0 / jp.h), 0.75);
                imgW = jp.w * scale; imgH = jp.h * scale;
                double textH = titleH + (it.Body != null ? SimplePdf.MeasureRichDoc(it.Body.Doc, textW, 10.5) : SimplePdf.MeasureText(it.Text, textW, 10.5)) + 20;
                if (top - Math.Max(imgH, textH) < pdf.Margin + 20) { pdf.NewPage(); top = pdf.Y; record[it.Anchor] = pdf.PageCount; }
            }
            pdf.SetY(top); pdf.Dest(it.Anchor);
            // Nummer als Marke, Überschrift daneben
            if (numW > 0)
            {
                pdf.FillRect(pdf.Margin, top - 17, numW - 6, 17, acc);
                pdf.DrawText(pdf.Margin + (numW - 6 - SimplePdf.TextWidth(it.Number, 10, true)) / 2, top - 12.5, it.Number, 10, true, (1, 1, 1));
            }
            pdf.SetY(top + 2);
            pdf.Paragraph(it.Title, 12.5, true, numW, 4, ink, textW - numW);
            if (it.Body != null && it.Text.Length > 0) pdf.RichBlocks(it.Body.Doc, 10.5, 0, 6, ink, textW);
            else if (it.Text.Length > 0 && it.Text != it.Title) pdf.Paragraph(it.Text, 10.5, false, 0, 6, it.Step.Redacted ? muted : ink, textW);
            if (side)
            {
                double afterText = pdf.Y;
                pdf.SetY(top);
                pdf.Image(jp.jpg, jp.w, jp.h, imgW, imgH, pdf.Margin + textW + 16);
                pdf.SetY(Math.Min(afterText, top - imgH - 8));
            }
            for (int ni = 0; ni < it.Notes.Count; ni++)
            {
                var (kind, text) = it.Notes[ni];
                var (label, bar, fill) = NoteStyle(kind, d.Lang);
                if (it.NoteBody(ni) is { } nr) pdf.CalloutRich(label, nr.Doc, Rgb(bar), Rgb(fill));
                else pdf.Callout(label, text, Rgb(bar), Rgb(fill));
            }
            if (hasImg && !side)
            {
                double f = DocSettings.Factor(it.Size);
                pdf.Image(jp.jpg, jp.w, jp.h, cw * f, Math.Min(560 * f + 60, maxImgH));
            }
            if (afterJpegs.TryGetValue(it, out var aj))
            {
                // Ergebnis: in der Breite des Schrittbilds, bei „nebeneinander“ in der Bildspalte
                double f = DocSettings.Factor(it.Size);
                double ax = side ? pdf.Margin + textW + 16 : -1, amaxW = side ? imgW : cw * f, amaxH = side ? 360 : Math.Min(560 * f + 60, maxImgH);
                pdf.EnsureSpace(Math.Min(amaxH, aj.h * Math.Min(1, amaxW / aj.w)) + 24);
                pdf.Paragraph(d.L("Ergebnis", "Result"), 9.5, false, side ? textW + 16 : 0, 3, muted);
                pdf.Image(aj.jpg, aj.w, aj.h, amaxW, amaxH, ax);
            }
            pdf.Space(6);
        }
        if (d.Outro.Length > 0 || d.OutroRich != null) { pdf.Space(10); Box(d.L("Abschluss", "Finally"), d.Outro, rich: d.OutroRich); }
        return pdf;
    }

    // =====================================================================================
    // Animiertes GIF: Titelbild, dann je Schritt Bild mit Bildunterschrift (Nummer + Überschrift)
    // =====================================================================================

    static void Gif(Doc d, string outPath, GuideOptions o, ExportResult res)
    {
        // Größenziel (z. B. für Pull Requests): erst die Breite verringern, dann jeden zweiten, dritten … Schritt zeigen
        int width = 1200, stride = 1;
        byte[] data;
        for (int attempt = 0; ; attempt++)
        {
            data = GifBytes(d, o, width, stride);
            if (o.GifMaxBytes <= 0 || data.LongLength <= o.GifMaxBytes || attempt >= 10) break;
            if (width > 480) width = Math.Max(480, (int)(width * 0.8)); else stride++;
        }
        File.WriteAllBytes(outPath, data);
        if (width < 1200 || stride > 1)
            res.Notices.Add(d.L($"GIF verkleinert, um das Größenziel einzuhalten: höchstens {width} Pixel breit" + (stride > 1 ? $", jeder {stride}. Schritt" : "") + $" ({data.LongLength / 1024.0 / 1024.0:0.0} MB).",
                                $"GIF reduced to meet the size target: at most {width} pixels wide" + (stride > 1 ? $", every {stride}th step" : "") + $" ({data.LongLength / 1024.0 / 1024.0:0.0} MB)."));
        if (o.GifMaxBytes > 0 && data.LongLength > o.GifMaxBytes)
            res.Warnings.Add(d.L("Das GIF bleibt über dem Größenziel.", "The GIF stays above the size target."));
    }

    static byte[] GifBytes(Doc d, GuideOptions o, int maxWidth, int stride)
    {
        var frames = new List<Bitmap>(); var delays = new List<int>();
        try
        {
            foreach (var (frame, ms) in SlideFrames(d, o, maxWidth, stride)) { frames.Add(frame); delays.Add(ms); }
            using var fs = new MemoryStream();
            GifWriter.Write(fs, frames, delays);
            return fs.ToArray();
        }
        finally { foreach (var f in frames) f.Dispose(); }
    }

    /// <summary>
    /// Bildfolge für GIF und Video (09 §8d/§8e): Titelbild, dann je Schritt Bild mit Nummer und Überschrift, jeweils mit Anzeigedauer in ms.
    /// Liefert die Bilder einzeln (der Aufrufer gibt sie frei) und lädt Schrittbilder erst, wenn sie dran sind.
    /// </summary>
    static IEnumerable<(Bitmap frame, int ms)> SlideFrames(Doc d, GuideOptions o, int maxWidth, int stride, int widthCap = 1200)
    {
        var items = d.Blocks.OfType<Item>().Where(i => i.Png != null).Where((_, i) => i % stride == 0).ToList();
        var sizes = items.Select(i => PngSize(i.Png!)).ToList();
        {
            int maxW = sizes.Count > 0 ? sizes.Max(b => b.Width) : 1000;
            int W = Math.Min(Math.Clamp(maxW, 640, widthCap), maxWidth);
            double maxAspect = sizes.Count > 0 ? sizes.Max(b => (double)b.Height / Math.Max(1, b.Width)) : 0.6;
            int imgH = (int)Math.Round(W * Math.Min(maxAspect, 0.8));
            const int bar = 64;
            int H = imgH + bar;
            var acc = System.Drawing.ColorTranslator.FromHtml(d.S.Accent);
            var bg = Color.FromArgb(0x17, 0x16, 0x14);
            int ms1 = (int)Math.Round(Math.Clamp(o.GifSeconds, 0.5, 20) * 1000);
            using var titleFont = new Font("Segoe UI Semibold", 30, GraphicsUnit.Pixel);
            using var subFont = new Font("Segoe UI", 18, GraphicsUnit.Pixel);
            using var capFont = new Font("Segoe UI Semibold", 19, GraphicsUnit.Pixel);
            using var numFont = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };

            // Titelbild
            var title = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(title))
            {
                g.Clear(bg); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var ab = new SolidBrush(acc)) g.FillRectangle(ab, 48, H / 2 - 70, 6, 140);
                float y = H / 2 - 60;
                if (d.Logo != null) { using var lm = new MemoryStream(d.Logo); using var lb = new Bitmap(lm); double s = Math.Min(220.0 / lb.Width, 60.0 / lb.Height); g.DrawImage(lb, 72, (int)(y - 80), (int)(lb.Width * s), (int)(lb.Height * s)); }
                g.DrawString(d.Title, titleFont, Brushes.White, new RectangleF(72, y, W - 120, 44), fmt);
                using var mb = new SolidBrush(Color.FromArgb(0xBA, 0xB8, 0xB1));
                if (d.Subtitle.Length > 0) g.DrawString(d.Subtitle, subFont, mb, new RectangleF(72, y + 50, W - 120, 30), fmt);
                g.DrawString($"{items.Count} {d.L("Schritte", "steps")}", subFont, mb, new RectangleF(72, y + 86, W - 120, 30), fmt);
            }
            yield return (title, Math.Max(1500, ms1));

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                using var srcMs = new MemoryStream(it.Png!); using var src = new Bitmap(srcMs);
                var f = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(f))
                {
                    g.Clear(bg);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    double s = Math.Min((double)W / src.Width, (double)imgH / src.Height);
                    int w = (int)(src.Width * s), h = (int)(src.Height * s);
                    g.DrawImage(src, (W - w) / 2, (imgH - h) / 2, w, h);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    float x = 20;
                    if (it.Number.Length > 0)
                    {
                        var sz = g.MeasureString(it.Number, numFont);
                        var badge = new RectangleF(x, imgH + (bar - 30) / 2f, Math.Max(30, sz.Width + 14), 30);
                        using (var ab = new SolidBrush(acc)) g.FillRectangle(ab, badge);
                        g.DrawString(it.Number, numFont, Brushes.White, badge, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        x += badge.Width + 14;
                    }
                    g.DrawString(it.Title, capFont, Brushes.White, new RectangleF(x, imgH, W - x - 20, bar), fmt);
                }
                yield return (f, ms1);
            }
        }
    }

    /// <summary>Breite und Höhe aus dem PNG-Kopf (IHDR), ohne das Bild zu dekodieren; sonst dekodieren.</summary>
    static Size PngSize(byte[] png)
    {
        if (png.Length >= 24 && png[0] == 0x89 && png[1] == (byte)'P' && png[12] == (byte)'I' && png[13] == (byte)'H')
            return new Size(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
        using var ms = new MemoryStream(png); using var b = new Bitmap(ms);
        return b.Size;
    }

    /// <summary>
    /// Support-Paket: ZIP mit HTML-Anleitung und KI-Paket aus der bereinigten Sicht. Verlangt vollständige
    /// Durchsicht; das Programm versendet nichts.
    /// </summary>
    public static ExportResult SupportPackage(SessionView view, string? outZip = null)
    {
        if (view.UnreviewedCount > 0) throw new InvalidOperationException("Support-Paket erst nach der Durchsicht aller auffälligen Eingaben.");
        string tmp = Path.Combine(view.Session.Path, "export", ".tmp", "support-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
        Directory.CreateDirectory(tmp);
        try
        {
            string title = Fmt.FileSafe(view.Title.Length > 0 ? view.Title : "Aufzeichnung");
            Export(view, new GuideOptions { Format = "html", OutPath = Path.Combine(tmp, title + ".html"), Importance = "secondary" });
            KiPackage.Export(view, new KiOptions { OutDir = Path.Combine(tmp, title + ".ki-paket"), Detail = "standard", ImageProfile = "compatible", IncludeEvents = false });
            outZip ??= Path.Combine(view.Session.Path, "export", $"Support-Paket-{DateTime.Now:yyyyMMdd-HHmm}.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(outZip)!);
            if (File.Exists(outZip)) File.Delete(outZip);
            ZipFile.CreateFromDirectory(tmp, outZip, CompressionLevel.Optimal, false);
            var r = new ExportResult { Path = outZip };
            r.Files.Add(outZip);
            return r;
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }
}
