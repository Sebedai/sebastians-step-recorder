using System.Text;
using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

/// <summary>
/// Markdown nach 09 §6 (12, Paket 18): eine Datei und ein Ordner <c>&lt;name&gt;-bilder/</c>, ASCII-Namen, Schritte als
/// nummerierte Liste mit eingerückten Bildern, Maskierung nur wo Markdown sie braucht, Hinweiskästen je Dialekt,
/// optional Kopfdaten (YAML). Gleiche Aufnahme → bytegleiche Ausgabe (keine Zeitstempel, feste Bildnamen).
/// </summary>
public static partial class GuideExport
{
    /// <summary>Markdown-Dialekte für Hinweise (09 §6.2): plain | github | obsidian.</summary>
    public static readonly string[] MarkdownDialects = { "plain", "github", "obsidian" };

    static void Markdown(Doc d, string outPath, GuideOptions o)
    {
        if (o.MdLegacy) { MarkdownLegacy(d, outPath); return; }
        string dir = Path.GetDirectoryName(outPath)!;
        string folder = Path.GetFileNameWithoutExtension(outPath) + "-bilder";
        string imgDir = Path.Combine(dir, folder);
        string text = MarkdownText(d, o.MdDialect, o.MdFrontMatter, folder, (file, png) => { Directory.CreateDirectory(imgDir); File.WriteAllBytes(Path.Combine(imgDir, file), png); });
        File.WriteAllText(outPath, text, new UTF8Encoding(false));
    }

    /// <summary>
    /// Markdown einer ganzen Anleitung als Text, ohne Bilder (für „Alle Schritte als Markdown kopieren“). Die Leseschicht
    /// liefert wie bei jedem Export nur bereinigte Inhalte.
    /// </summary>
    public static string MarkdownForClipboard(SessionView view, string? lang = null, string dialect = "github", string importance = "primary")
    {
        lang ??= view.Language;
        bool fb = view.AuthorTextFallback;
        view.Language = lang; view.Rebuild();
        try
        {
            var o = new GuideOptions { Format = "md", Language = lang, Images = false, Importance = importance, MdDialect = dialect };
            var d = Build(view, o, lang);
            return MarkdownText(d, dialect, frontMatter: false, imgFolder: null, writeImage: null);
        }
        finally { view.AuthorTextFallback = fb; view.Rebuild(); }
    }

    /// <summary>Ein einzelner Schritt als Markdown (ohne Bildverweis – das Bild käme beim Kopieren nicht mit).</summary>
    public static string StepMarkdown(SessionView view, ViewStep s, string dialect = "github")
    {
        string lang = view.Language;
        var sb = new StringBuilder();
        sb.Append(s.Number).Append(". **").Append(MdInline(s.AuthorTitle ?? s.Title, s, false)).Append("**\n");
        string body = s.AuthorBody ?? s.Guide;
        if (body.Length > 0 && body != (s.AuthorTitle ?? s.Title)) { sb.Append('\n'); AppendParagraph(sb, body, s, "   "); }
        if (ConsoleBlock(s) is { } cmd) { sb.Append('\n'); AppendFence(sb, cmd, "   "); }
        foreach (var c in s.Comments)
            if (c.Str("text") is { Length: > 0 } t) { sb.Append('\n'); AppendNote(sb, c.Str("kind") ?? "note", t, dialect, lang, "   "); }
        return sb.ToString();
    }

    static string MarkdownText(Doc d, string dialect, bool frontMatter, string? imgFolder, Action<string, byte[]>? writeImage)
    {
        var sb = new StringBuilder();
        if (frontMatter)
        {
            sb.Append("---\n");
            sb.Append("title: ").Append(Yaml(d.Title)).Append('\n');
            if (d.Subtitle.Length > 0) sb.Append("description: ").Append(Yaml(d.Subtitle)).Append('\n');
            if (d.Date.Length > 0) sb.Append("date: ").Append(d.Date).Append('\n');
            sb.Append("lang: ").Append(d.Lang).Append('\n');
            sb.Append("---\n\n");
        }
        if (d.Logo != null && imgFolder != null && writeImage != null) { writeImage("logo.png", d.Logo); sb.Append("![Logo](").Append(imgFolder).Append("/logo.png)\n\n"); }
        sb.Append("# ").Append(MdText(d.Title)).Append("\n\n");
        if (d.Subtitle.Length > 0) sb.Append('*').Append(MdText(d.Subtitle)).Append("*\n\n");
        if (d.Meta.Count > 0) sb.Append(string.Join(" · ", d.Meta.Select(x => $"{MdText(x.label)}: **{MdText(x.value)}**"))).Append("\n\n");
        if (d.ShowToc && d.HasChapters)
        {
            sb.Append("## ").Append(d.L("Inhalt", "Contents")).Append("\n\n");
            foreach (var c in d.Blocks.OfType<Chapter>()) sb.Append("- ").Append(MdText(Heading(c.Number, c.Title))).Append('\n');
            sb.Append('\n');
        }
        if (d.Intro.Length > 0) { sb.Append("## ").Append(d.L("Über diese Anleitung", "About this guide")).Append("\n\n"); AppendParagraph(sb, d.Intro, null, ""); sb.Append('\n'); }
        if (d.Prereq.Count > 0) sb.Append("## ").Append(d.L("Voraussetzungen", "Prerequisites")).Append("\n\n").Append(string.Concat(d.Prereq.Select(x => "- " + MdText(x) + "\n"))).Append('\n');

        int flat = 0;
        foreach (var b in d.Blocks)
        {
            if (b is Chapter c)
            {
                sb.Append("## ").Append(MdText(Heading(c.Number, c.Title))).Append("\n\n");
                if (c.Text.Length > 0) { AppendParagraph(sb, c.Text, null, ""); sb.Append('\n'); }
                continue;
            }
            var it = (Item)b;
            flat++;
            // Listenmarke: Nummer der Anleitung (bei Kapitelnummerierung die Nummer im Kapitel); ohne Nummerierung Aufzählung
            string marker = d.Checkboxes ? "- [ ]" : it.Number.Length == 0 ? "-" : it.Number.Split('.').Last() + ".";
            string ind = new(' ', marker.Length + 1);
            if (d.Compact || d.Checkboxes)
            {
                // Kurzfassung und Checkliste: ein knapper Listenpunkt je Schritt, Hinweise darunter, keine Bilder
                string body = it.Text.Length > 0 && it.Text != it.Title && !it.Step.Redacted ? " – " + MdInline(Regex.Replace(it.Text, @"\s+", " "), it.Step) : "";
                sb.Append(marker).Append(" **").Append(MdInline(it.Title, it.Step, false)).Append("**").Append(body).Append('\n');
                foreach (var (kind, text) in it.Notes) AppendNote(sb, kind, text, dialect, d.Lang, ind);
                continue;
            }
            sb.Append(marker).Append(" **").Append(MdInline(it.Title, it.Step, false)).Append("**\n");
            void Gap() => sb.Append('\n');
            if (it.Text.Length > 0 && it.Text != it.Title)
            {
                Gap();
                if (it.Step.Redacted) sb.Append(ind).Append('*').Append(MdText(it.Text)).Append("*\n");
                else AppendParagraph(sb, it.Text, it.Step, ind);
            }
            if (!it.Step.Redacted && ConsoleBlock(it.Step) is { } cmd) { Gap(); AppendFence(sb, cmd, ind); }
            foreach (var (kind, text) in it.Notes) { Gap(); AppendNote(sb, kind, text, dialect, d.Lang, ind); }
            if (it.Png != null && imgFolder != null && writeImage != null)
            {
                string file = $"schritt-{flat:000}.png";
                writeImage(file, it.Png);
                Gap(); sb.Append(ind).Append("![").Append(MdText(it.Title)).Append("](").Append(imgFolder).Append('/').Append(file).Append(")\n");
                if (it.AfterPng != null)
                {
                    string af = $"schritt-{flat:000}-nachher.png";
                    writeImage(af, it.AfterPng);
                    Gap(); sb.Append(ind).Append('*').Append(d.L("Ergebnis", "Result")).Append(":*\n");
                    Gap(); sb.Append(ind).Append("![").Append(MdText(it.Title + " – " + d.L("Ergebnis", "Result"))).Append("](").Append(imgFolder).Append('/').Append(af).Append(")\n");
                }
            }
            sb.Append('\n');   // lockere Liste: Leerzeile zwischen den Schritten
        }
        if (d.Compact || d.Checkboxes) sb.Append('\n');
        if (d.Outro.Length > 0) { sb.Append("## ").Append(d.L("Abschluss", "Finally")).Append("\n\n"); AppendParagraph(sb, d.Outro, null, ""); sb.Append('\n'); }
        if (d.S.Footer.Length > 0) sb.Append("---\n\n").Append(MdText(d.S.Footer)).Append('\n');
        // genau ein abschließender Zeilenumbruch
        return sb.ToString().TrimEnd('\n') + "\n";
    }

    static void AppendParagraph(StringBuilder sb, string text, ViewStep? step, string ind)
    {
        var lines = text.Replace("\r", "").Split('\n').Select(l => l.Trim()).ToList();
        // leere Zeilen trennen Absätze, sonst harter Zeilenumbruch mit „\“
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length == 0) { if (i > 0 && lines[i - 1].Length > 0) sb.Append('\n'); continue; }
            sb.Append(ind).Append(MdInline(lines[i], step));
            bool nextText = i + 1 < lines.Count && lines[i + 1].Length > 0;
            sb.Append(nextText ? "\\\n" : "\n");
        }
    }

    static void AppendNote(StringBuilder sb, string kind, string text, string dialect, string lang, string ind)
    {
        var (label, _, _) = NoteStyle(kind, lang);
        var lines = text.Replace("\r", "").Split('\n').Select(l => l.Trim()).ToList();
        switch (dialect)
        {
            case "github":
                string gh = kind switch { "tip" => "TIP", "warning" => "WARNING", "important" => "IMPORTANT", _ => "NOTE" };
                sb.Append(ind).Append("> [!").Append(gh).Append("]\n");
                if (kind == "result") lines[0] = label + " " + lines[0];
                break;
            case "obsidian":
                string ob = kind switch { "tip" => "tip", "warning" => "warning", "important" => "important", "result" => "success", _ => "note" };
                sb.Append(ind).Append("> [!").Append(ob).Append("] ").Append(MdText(label.TrimEnd(':').TrimEnd())).Append('\n');
                break;
            default:
                lines[0] = "\u0001" + lines[0];   // Platzhalter für die fett gesetzte Kennzeichnung
                break;
        }
        foreach (var l in lines)
        {
            if (l.StartsWith('\u0001')) sb.Append(ind).Append("> **").Append(MdText(label)).Append("** ").Append(MdInline(l[1..], null)).Append('\n');
            else sb.Append(ind).Append(l.Length == 0 ? ">" : "> " + MdInline(l, null)).Append('\n');
        }
    }

    static void AppendFence(StringBuilder sb, string code, string ind)
    {
        string fence = code.Contains("```") ? "~~~" : "```";
        sb.Append(ind).Append(fence).Append("console\n");
        foreach (var l in code.Replace("\r", "").Split('\n')) sb.Append(ind).Append(l).Append('\n');
        sb.Append(ind).Append(fence).Append('\n');
    }

    /// <summary>Im Terminal getippter Befehl (nach der Durchsicht) – als Codeblock statt Fließtext.</summary>
    static string? ConsoleBlock(ViewStep s)
    {
        if (s.Kind != "type" || s.Json.Obj("ctx")?.Str("surfaceKind") != "terminal") return null;
        var input = s.Json.Obj("input");
        if (input == null || input.Bool("masked")) return null;
        string? text = input.Str("text");
        if (string.IsNullOrWhiteSpace(text) || text.Contains("{unreviewed:")) return null;
        return text.TrimEnd();
    }

    // -------------------------------------------------------------------------------------
    // Maskierung: nur wo CommonMark sie braucht
    // -------------------------------------------------------------------------------------

    static readonly Regex KeyCombo = new(@"\b(?:Strg|Ctrl|Alt|AltGr|Umschalt|Shift|Maj|Mayús|Maiusc|Win|Windows)(?:\s*\+\s*(?:Strg|Ctrl|Alt|AltGr|Umschalt|Shift|Maj|Mayús|Maiusc|Win|F\d{1,2}|Entf|Del|Suppr|Supr|Canc|Einfg|Eingabe|Enter|Entrée|Intro|Invio|Tab|Esc|Échap|Pos1|Ende|Bild\S*|[A-Za-z0-9ÄÖÜäöü]))+", RegexOptions.Compiled);
    /// <summary>Angeführte Namen: „…“ (Deutsch), "…", « … »/«…» (Französisch, Spanisch, Italienisch), ‘…’ (Niederländisch).</summary>
    static readonly Regex Quoted = new("„([^“\n]{1,200})“|\"([^\"\n]{1,200})\"|(« ?)([^»\n]{1,200}?)( ?»)|(‘)([^’\n]{1,200})(’)", RegexOptions.Compiled);

    /// <summary>Text mit Auszeichnung: Elementnamen fett in Anführungszeichen, getippter Text und Tastenkürzel als Code-Spanne.</summary>
    static string MdInline(string text, ViewStep? step, bool emphasis = true)
    {
        string? typed = step?.Kind == "type" && step.Json.Obj("input") is { } inp && !inp.Bool("masked") ? inp.Str("text") : null;
        var sb = new StringBuilder();
        int pos = 0;
        foreach (Match m in Quoted.Matches(text))
        {
            sb.Append(PlainWithKeys(text[pos..m.Index], sb.Length == 0 && pos == 0));
            string inner = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[4].Success ? m.Groups[4].Value : m.Groups[7].Value;
            if (typed != null && inner == typed && !inner.Contains("{unreviewed:")) sb.Append(Code(inner));
            else if (m.Groups[1].Success) sb.Append(emphasis ? "**„" : "„").Append(MdText(inner, false)).Append(emphasis ? "“**" : "“");
            else if (m.Groups[4].Success || m.Groups[7].Success)
            {
                string open = m.Groups[4].Success ? m.Groups[3].Value : m.Groups[6].Value, close = m.Groups[4].Success ? m.Groups[5].Value : m.Groups[8].Value;
                sb.Append(emphasis ? "**" + open : open).Append(MdText(inner, false)).Append(emphasis ? close + "**" : close);
            }
            else sb.Append('"').Append(MdText(inner, false)).Append('"');
            pos = m.Index + m.Length;
        }
        sb.Append(PlainWithKeys(text[pos..], sb.Length == 0 && pos == 0));
        return sb.ToString();
    }

    static string PlainWithKeys(string s, bool lineStart)
    {
        var sb = new StringBuilder();
        int pos = 0;
        foreach (Match m in KeyCombo.Matches(s))
        {
            sb.Append(MdText(s[pos..m.Index], lineStart && pos == 0));
            sb.Append(Code(Regex.Replace(m.Value, @"\s*\+\s*", "+")));
            pos = m.Index + m.Length;
        }
        sb.Append(MdText(s[pos..], lineStart && pos == 0 && sb.Length == 0));
        return sb.ToString();
    }

    static string Code(string s)
    {
        int run = 0, max = 0;
        foreach (var c in s) { run = c == '`' ? run + 1 : 0; max = Math.Max(max, run); }
        string ticks = new('`', max + 1);
        bool pad = s.StartsWith('`') || s.EndsWith('`') || s.StartsWith(' ') && s.EndsWith(' ') && s.Trim().Length > 0;
        return ticks + (pad ? " " + s + " " : s) + ticks;
    }

    /// <summary>
    /// Maskiert nur, was in CommonMark/GFM Bedeutung hat: <c>\ ` * _ [ ] &lt; ~</c> überall, Entitäten mit <c>&amp;</c>,
    /// am Zeilenanfang Überschrift, Zitat, Listenmarke, Trennlinie und nummerierte Liste.
    /// </summary>
    public static string MdText(string? s, bool lineStart = true)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 8);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '~') sb.Append('\\');
            else if (c == '&' && Regex.IsMatch(s[(i + 1)..], @"^(#\d+|#x[0-9A-Fa-f]+|[A-Za-z][A-Za-z0-9]*);")) sb.Append('\\');
            else if (c == '!' && i + 1 < s.Length && s[i + 1] == '[') sb.Append('\\');
            sb.Append(c);
        }
        string r = sb.ToString();
        if (!lineStart) return r;
        // Zeilenanfang: # > + - = | sowie „1.“ oder „1)“ gefolgt von Leerzeichen
        if (Regex.IsMatch(r, @"^(#{1,6}(\s|$)|>|[+\-=](\s|$)|\|)")) r = "\\" + r;
        else if (Regex.Match(r, @"^(\d{1,9})([.)])(\s|$)") is { Success: true } m) r = m.Groups[1].Value + "\\" + r[m.Groups[1].Length..];
        return r;
    }

    /// <summary>Einfache YAML-Zeichenkette in doppelten Anführungszeichen.</summary>
    static string Yaml(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ") + "\"";
}
