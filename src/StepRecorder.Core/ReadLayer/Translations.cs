using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.ReadLayer;

/// <summary>Eine übersetzbare Textstelle: Kennung, Art, Ort, deutscher und englischer Autorentext (bereinigt), Stand.</summary>
public sealed record TextRow(string Id, string Kind, string Location, long? Anchor, string De, string En, string Status);

/// <summary>
/// Übersetzungsübersicht (13, Paket 33): alle eigenen Texte auf Deutsch und Englisch nebeneinander, Zurückschreiben einzelner
/// Zellen und Austausch als CSV. Die Werte sind bereinigt (Schwärzung, Verallgemeinerung); eine Zelle, deren Text einen geschützten
/// Wert verdeckt, wird beim Import nie überschrieben. Zellen, die mit = + - @ beginnen, bekommen in der CSV ein Apostroph.
/// </summary>
public static class Translations
{
    static JsonObject? StepEdit(JsonObject edits, long anchor) => (edits.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(x => x.Long("anchor") == anchor);

    static JsonObject StepEditOrNew(JsonObject edits, long anchor)
    {
        var arr = edits["steps"] as JsonArray ?? new JsonArray(); edits["steps"] = arr;
        var se = arr.OfType<JsonObject>().FirstOrDefault(x => x.Long("anchor") == anchor);
        if (se == null) { se = new JsonObject { ["anchor"] = anchor }; arr.Add(se); }
        return se;
    }

    /// <summary>Rohwert (unbereinigt) einer Zelle aus dem Overlay.</summary>
    static string Raw(JsonObject edits, string id, string lang)
    {
        var p = id.Split(':');
        var guide = edits.Obj("guide");
        switch (p[0])
        {
            case "step" when long.TryParse(p[1], out var a):
                return StepEdit(edits, a)?.Obj("text")?.Obj(lang)?.Str(p[2]) ?? "";
            case "chapter" when long.TryParse(p[1], out var a):
                {
                    var ch = StepEdit(edits, a)?.Obj("chapter");
                    return (lang == "de" ? ch?.Str(p[2]) : ch?.Obj(lang)?.Str(p[2])) ?? "";
                }
            case "note":
                {
                    var c = (edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(x => x.Str("id") == p[1]);
                    return (lang == "de" ? c?.Str("text") : c?.Obj("i18n")?.Obj(lang)?.Str("text")) ?? "";
                }
            case "doc":
                return (lang == "de" ? guide?.Str(p[1]) : guide?.Obj("i18n")?.Obj(lang)?.Str(p[1])) ?? "";
        }
        return "";
    }

    /// <summary>Alle Zeilen in der Reihenfolge der Anleitung.</summary>
    public static List<TextRow> Rows(SessionView view)
    {
        var rows = new List<TextRow>();
        var e = view.Edits;
        TextRow Row(string id, string kind, string loc, long? anchor)
        {
            string de = view.Clean(Raw(e, id, "de")), en = view.Clean(Raw(e, id, "en"));
            string status = de.Length == 0 && en.Length == 0 ? "empty" : en.Length == 0 ? "missingEn" : de.Length == 0 ? "missingDe" : de == en ? "same" : "ok";
            return new TextRow(id, kind, loc, anchor, de, en, status);
        }
        foreach (var k in new[] { "title", "intro", "outro" })
            rows.Add(Row("doc:" + k, "doc", k switch { "title" => Loc.L("Titel", "Title"), "intro" => Loc.L("Einleitung", "Introduction"), _ => Loc.L("Abschluss", "Closing") }, null));
        foreach (var s in view.GuideSteps("all"))
        {
            if (s.Kind == "move") continue;
            string where = Loc.F("Schritt {0}", "Step {0}", s.Number);
            if (StepEdit(e, s.Anchor)?.Obj("chapter") != null)
            {
                rows.Add(Row($"chapter:{s.Anchor}:title", "chapter", where + " · " + Loc.L("Kapitel", "Chapter"), s.Anchor));
                rows.Add(Row($"chapter:{s.Anchor}:text", "chapterText", where + " · " + Loc.L("Kapiteleinleitung", "Chapter introduction"), s.Anchor));
            }
            rows.Add(Row($"step:{s.Anchor}:title", "title", where + " · " + Loc.L("Überschrift", "Heading"), s.Anchor));
            rows.Add(Row($"step:{s.Anchor}:body", "body", where + " · " + Loc.L("Text", "Text"), s.Anchor));
            foreach (var c in (e.Arr("comments") ?? new JsonArray()).OfType<JsonObject>().Where(c => c.Long("anchor") == s.Anchor && c.Str("kind") is not ("internal" or "agent") && !c.Bool("hidden") && c.Str("id") != null))
                rows.Add(Row("note:" + c.Str("id"), "note", where + " · " + Loc.L("Hinweis", "Note"), s.Anchor));
        }
        return rows;
    }

    /// <summary>Wert einer Zelle schreiben (leer entfernt den Eintrag der Sprache).</summary>
    public static void Apply(JsonObject edits, string id, string lang, string value)
    {
        value = value.Replace("\r\n", "\n").Trim();
        var p = id.Split(':');
        switch (p[0])
        {
            case "step" when long.TryParse(p[1], out var a):
                {
                    var se = StepEditOrNew(edits, a);
                    var text = se["text"] as JsonObject ?? new JsonObject(); se["text"] = text;
                    var l = text[lang] as JsonObject ?? new JsonObject(); text[lang] = l;
                    if (value.Length == 0) l.Remove(p[2]); else l[p[2]] = value;
                    if (l.Count == 0 || l.All(kv => kv.Key is "fmt" or "by")) text.Remove(lang);
                    break;
                }
            case "chapter" when long.TryParse(p[1], out var a):
                {
                    var se = StepEditOrNew(edits, a);
                    var ch = se["chapter"] as JsonObject ?? new JsonObject(); se["chapter"] = ch;
                    var h = lang == "de" ? ch : ch[lang] as JsonObject ?? new JsonObject();
                    if (lang != "de") ch[lang] = h;
                    if (value.Length == 0) h.Remove(p[2]); else h[p[2]] = value;
                    break;
                }
            case "note":
                {
                    var c = (edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(x => x.Str("id") == p[1]);
                    if (c == null) return;
                    if (lang == "de") { c["text"] = value; break; }
                    var i18n = c["i18n"] as JsonObject ?? new JsonObject(); c["i18n"] = i18n;
                    if (value.Length == 0) i18n.Remove(lang); else i18n[lang] = new JsonObject { ["text"] = value };
                    break;
                }
            case "doc":
                {
                    var g = edits["guide"] as JsonObject ?? new JsonObject(); edits["guide"] = g;
                    var h = g;
                    if (lang != "de") { var i18n = g["i18n"] as JsonObject ?? new JsonObject(); g["i18n"] = i18n; h = i18n[lang] as JsonObject ?? new JsonObject(); i18n[lang] = h; }
                    if (value.Length == 0) h.Remove(p[1]); else h[p[1]] = value;
                    break;
                }
        }
    }

    // ---------------- CSV ----------------

    static string Cell(string s)
    {
        if (s.Length > 0 && s[0] is '=' or '+' or '-' or '@') s = "'" + s;   // Formelschutz
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    public static string ToCsv(IEnumerable<TextRow> rows)
    {
        var sb = new StringBuilder("﻿id;kind;location;de;en;status\r\n");
        foreach (var r in rows) sb.Append(string.Join(";", new[] { r.Id, r.Kind, r.Location, r.De, r.En, r.Status }.Select(Cell))).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>CSV lesen (Trennzeichen ; , oder Tab, Anführungszeichen nach RFC 4180); liefert id → (de, en).</summary>
    public static Dictionary<string, (string de, string en)> ParseCsv(string text)
    {
        text = text.TrimStart('﻿');
        string firstLine = text.Split('\n')[0];
        char sep = firstLine.Contains(';') ? ';' : firstLine.Contains('\t') ? '\t' : ',';
        var records = new List<List<string>>();
        var cur = new List<string>(); var cell = new StringBuilder(); bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (q) { if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; } else cell.Append(c); continue; }
            if (c == '"') q = true;
            else if (c == sep) { cur.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { cur.Add(cell.ToString().TrimEnd('\r')); cell.Clear(); records.Add(cur); cur = new(); }
            else cell.Append(c);
        }
        if (cell.Length > 0 || cur.Count > 0) { cur.Add(cell.ToString()); records.Add(cur); }
        var res = new Dictionary<string, (string, string)>();
        if (records.Count == 0) return res;
        var head = records[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int iId = head.IndexOf("id"), iDe = head.IndexOf("de"), iEn = head.IndexOf("en");
        if (iId < 0 || iDe < 0 || iEn < 0) return res;
        static string Unprotect(string s) => s.Length > 1 && s[0] == '\'' && s[1] is '=' or '+' or '-' or '@' ? s[1..] : s;
        foreach (var r in records.Skip(1))
            if (r.Count > Math.Max(iId, Math.Max(iDe, iEn)) && r[iId].Length > 0) res[r[iId]] = (Unprotect(r[iDe]), Unprotect(r[iEn]));
        return res;
    }

    /// <summary>
    /// CSV übernehmen: nur geänderte Zellen; eine Zelle, deren gespeicherter Text einen geschützten Wert enthält (bereinigt ≠ roh),
    /// wird nicht überschrieben. Liefert (übernommen, übersprungen).
    /// </summary>
    public static (int applied, int skipped) Import(SessionView view, string csv)
    {
        var data = ParseCsv(csv);
        var current = Rows(view).ToDictionary(r => r.Id);
        int applied = 0, skipped = 0;
        foreach (var (id, (de, en)) in data)
        {
            if (!current.TryGetValue(id, out var row)) { skipped++; continue; }
            foreach (var (lang, val, cur) in new[] { ("de", de, row.De), ("en", en, row.En) })
            {
                if (val.Replace("\r\n", "\n").Trim() == cur) continue;
                if (view.Clean(Raw(view.Edits, id, lang)) != Raw(view.Edits, id, lang)) { skipped++; continue; }
                Apply(view.Edits, id, lang, val);
                applied++;
            }
        }
        return (applied, skipped);
    }
}
