using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Text;

namespace StepRecorder.Export;

/// <summary>
/// Handbuch-Bausteine (13, Paket 26): Impressum, Änderungsverzeichnis und Glossar. Inhalte stehen in <c>guide.imprint</c>,
/// <c>guide.revisions[]</c>, <c>guide.glossary[]</c> (je Sprache über <c>i18n</c>), werden bereinigt und als formatierter Text
/// (Tabellen) versiegelt. Nichts davon trägt der Editor von selbst ein – kein Benutzer- oder Rechnername.
/// </summary>
public static partial class GuideExport
{
    /// <summary>Ein Zusatzabschnitt vor oder nach den Schritten.</summary>
    sealed record Part(string Anchor, string Title, RichText Body);

    static string? LangStr(JsonObject? o, string key, string lang)
        => lang != "de" && o?.Obj("i18n")?.Obj(lang)?.Str(key) is { Length: > 0 } t ? t : o?.Str(key);

    static void BuildParts(SessionView view, Doc d, string lang)
    {
        var guide = view.Edits.Obj("guide");
        if (guide == null) return;
        var s = d.S;
        // Impressum
        if (guide.Obj("imprint") is { } imp && LangStr(imp, "text", lang) is { Length: > 0 } it && s.Imprint != "off")
        {
            bool rich = (lang != "de" && imp.Obj("i18n")?.Obj(lang)?.Str("text") is { Length: > 0 } ? imp.Obj("i18n")!.Obj(lang)!.Int("fmt") : imp.Int("fmt")) == 1;
            var body = rich ? view.SealRich(it) : RichText.FromPlain(view.CleanVars(it));
            (s.Imprint == "back" ? d.Back : d.Front).Add(new Part("m-imprint", d.L("Impressum", "Imprint"), body));
        }
        // Änderungsverzeichnis: Version | Datum | Autor | Änderung
        var revs = (guide.Arr("revisions") ?? new JsonArray()).OfType<JsonObject>().ToList();
        if (revs.Count > 0 && s.Revisions != "off")
        {
            var rows = new List<IReadOnlyList<string>> { new[] { d.L("Version", "Version"), Settings.OutText.T(lang, "Datum", "Date"), d.L("Autor", "Author"), d.L("Änderung", "Change") } };
            foreach (var r in revs)
                rows.Add(new[] { view.CleanVars(r.Str("version")), view.CleanVars(r.Str("date")), view.CleanVars(r.Str("author")), view.CleanVars(LangStr(r, "text", lang)) });
            (s.Revisions == "back" ? d.Back : d.Front).Add(new Part("m-rev", d.L("Änderungsverzeichnis", "Revision history"), view.SealRich(RichDoc.TableMarkup(rows, header: true))));
        }
        // Glossar: Begriff | Erklärung, alphabetisch
        var gl = (guide.Arr("glossary") ?? new JsonArray()).OfType<JsonObject>()
            .Select(g => (term: view.CleanVars(LangStr(g, "term", lang)), text: view.CleanVars(LangStr(g, "text", lang))))
            .Where(g => g.term.Length > 0).OrderBy(g => g.term, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo(lang == "en" ? "en-US" : "de-DE"), true)).ToList();
        if (gl.Count > 0 && s.Glossary)
        {
            var rows = new List<IReadOnlyList<string>> { new[] { d.L("Begriff", "Term"), d.L("Erklärung", "Explanation") } };
            rows.AddRange(gl.Select(g => (IReadOnlyList<string>)new[] { g.term, g.text }));
            d.Back.Add(new Part("m-glossary", d.L("Glossar", "Glossary"), view.SealRich(RichDoc.TableMarkup(rows, header: true))));
        }
    }

    // ---------------- Schreiber ----------------

    static string HtmlParts(List<Part> parts)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts) sb.Append($"<section class=\"box part\" id=\"{p.Anchor}\"><h2>{Fmt.HtmlEscape(p.Title)}</h2>{HtmlRich(p.Body)}</section>\n");
        return sb.ToString();
    }

    static void MdParts(System.Text.StringBuilder sb, List<Part> parts)
    {
        foreach (var p in parts) { sb.Append("## ").Append(MdText(p.Title)).Append("\n\n"); AppendRichMd(sb, p.Body, ""); sb.Append('\n'); }
    }

    static void DocxParts(Formats.SimpleDocx x, List<Part> parts)
    {
        foreach (var p in parts) { x.Heading(p.Title, 1); x.RichBlocks(p.Body.Doc); }
    }

    static void PdfParts(Formats.SimplePdf pdf, List<Part> parts, (double r, double g, double b) acc, (double r, double g, double b) ink)
    {
        foreach (var p in parts)
        {
            pdf.EnsureSpace(80);
            pdf.Space(8);
            pdf.Dest(p.Anchor);
            pdf.Bookmark(p.Title, 0, p.Anchor);
            pdf.Paragraph(p.Title, 15, true, 0, 8, acc);
            pdf.RichBlocks(p.Body.Doc, 10, 0, 10, ink);
        }
    }
}
