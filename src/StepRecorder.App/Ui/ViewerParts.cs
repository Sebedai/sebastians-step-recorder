using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Dokumentbereich „Bausteine“ (13, Paket 26): Impressum, Änderungsverzeichnis, Glossar. Texte je Sprache der Ansicht; Version,
/// Datum und Autor eines Änderungseintrags gelten für alle Sprachen. Nichts wird von selbst eingetragen.
/// </summary>
sealed partial class ViewerWindow
{
    void PartsSection(DocSettings d)
    {
        bool ro = _view.Session.ReadOnly;
        string lang = _view.Language;
        var guide = _view.Edits.Obj("guide");
        var box = Section(Loc.L("Bausteine", "Building blocks"));

        ComboBox Place(string current, Action<string> set, bool withFront = true)
        {
            var c = new ComboBox { MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !ro, Margin = new Thickness(0, 0, 0, 6) };
            var ids = new[] { "front", "back", "off" };
            foreach (var n in new[] { Loc.L("Vor dem Inhaltsverzeichnis", "Before the contents"), Loc.L("Am Ende", "At the end"), Loc.L("Nicht ausgeben", "Do not output") }) c.Items.Add(n);
            c.SelectedIndex = Math.Max(0, Array.IndexOf(ids, current));
            c.SelectionChanged += (_, _) => set(ids[Math.Max(0, c.SelectedIndex)]);
            return c;
        }

        // Impressum (formatierbar)
        var imp = guide?.Obj("imprint");
        var impLoc = lang != "de" ? imp?.Obj("i18n")?.Obj(lang) : imp;
        var impBox = RichBox(impLoc?.Str("text") ?? "", impLoc?.Int("fmt") == 1, (v, rich) => QuietChange(e =>
        {
            var g = GuideOf(e);
            var o = g["imprint"] as JsonObject ?? new JsonObject();
            JsonObject h = o;
            if (lang != "de") { var i18n = o["i18n"] as JsonObject ?? new JsonObject(); o["i18n"] = i18n; h = i18n[lang] as JsonObject ?? new JsonObject(); i18n[lang] = h; }
            if (v.Length == 0) { h.Remove("text"); h.Remove("fmt"); } else { h["text"] = v; if (rich) h["fmt"] = 1; else h.Remove("fmt"); }
            if (o.Str("text") == null && o.Obj("i18n") is not { Count: > 0 }) g.Remove("imprint"); else g["imprint"] = o;
        }), Loc.L("Herausgeber, Anschrift, Kontakt, Dokumentnummer, Hinweis zu Marken …", "Publisher, address, contact, document number, trademark notice …"), 64);
        box.Children.Add(U.Field(Loc.L("Impressum", "Imprint"), U.Col(Place(d.Imprint, v => DocChange(x => x.Imprint = v)), impBox)));

        // Änderungsverzeichnis: je Zeile Version | Datum | Autor | Änderung
        var revs = (guide?.Arr("revisions") ?? new JsonArray()).OfType<JsonObject>().ToList();
        string RevLine(JsonObject r) => string.Join(" | ", r.Str("version") ?? "", r.Str("date") ?? "", r.Str("author") ?? "", (lang != "de" ? r.Obj("i18n")?.Obj(lang)?.Str("text") : null) ?? r.Str("text") ?? "");
        var revBox = CommitBox(string.Join("\n", revs.Select(RevLine)), v => QuietChange(e => SetRevisions(e, v)), Loc.L("1.1 | 2026-11-13 | S. Muster | Kapitel „Drucken“ ergänzt", "1.1 | 2026-11-13 | J. Doe | Added chapter “Printing”"), true);
        var addRev = U.Btn(Theme.IAdd, Loc.L("Eintrag mit heutigem Datum", "Entry with today’s date"), () =>
        {
            string line = $"{(d.Version.Length > 0 ? d.Version : "1.0")} | {DateTime.Now:yyyy-MM-dd} |  | ";
            revBox.Text = (revBox.Text.TrimEnd().Length > 0 ? revBox.Text.TrimEnd() + "\n" : "") + line;
            revBox.Focus(); revBox.CaretIndex = revBox.Text.Length;
        }, Loc.L("Version aus „Titelseite“, Datum von heute – Autor und Text tragen Sie selbst ein", "Version from “Title page”, today’s date – you enter author and text yourself"));
        addRev.IsEnabled = !ro; addRev.HorizontalAlignment = HorizontalAlignment.Left; addRev.Margin = new Thickness(0, 6, 0, 0);
        box.Children.Add(U.Field(Loc.L("Änderungsverzeichnis", "Revision history"), U.Col(Place(d.Revisions, v => DocChange(x => x.Revisions = v)), revBox, addRev),
            Loc.L("Je Zeile: Version | Datum | Autor | Änderung. Der Text gilt für die Sprache der Ansicht.", "One per line: version | date | author | change. The text applies to the language of the view.")));

        // Glossar: je Zeile Begriff = Erklärung
        var gl = (guide?.Arr("glossary") ?? new JsonArray()).OfType<JsonObject>().ToList();
        string GlLine(JsonObject g) => ((lang != "de" ? g.Obj("i18n")?.Obj(lang)?.Str("term") : null) ?? g.Str("term") ?? "") + " = " + ((lang != "de" ? g.Obj("i18n")?.Obj(lang)?.Str("text") : null) ?? g.Str("text") ?? "");
        var glBox = CommitBox(string.Join("\n", gl.Select(GlLine)), v => QuietChange(e => SetGlossary(e, v)), Loc.L("Mandant = Abgegrenzter Datenbereich eines Kunden", "Tenant = Separate data area of a customer"), true);
        var glOn = new CheckBox { Content = Loc.L("Glossar am Ende ausgeben", "Output glossary at the end"), IsChecked = d.Glossary, IsEnabled = !ro, Margin = new Thickness(0, 0, 0, 6) };
        glOn.Checked += (_, _) => DocChange(x => x.Glossary = true); glOn.Unchecked += (_, _) => DocChange(x => x.Glossary = false);
        box.Children.Add(U.Field(Loc.L("Glossar", "Glossary"), U.Col(glOn, glBox), Loc.L("Je Zeile: Begriff = Erklärung; erscheint alphabetisch als Tabelle.", "One per line: term = explanation; appears alphabetically as a table.")));
    }

    void SetRevisions(JsonObject e, string text)
    {
        var g = GuideOf(e);
        var old = (g.Arr("revisions") ?? new JsonArray()).OfType<JsonObject>().ToList();
        var arr = new JsonArray();
        string lang = _view.Language;
        int i = 0;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.Trim().Length == 0) continue;
            var p = raw.Split('|').Select(x => x.Trim()).ToArray();
            var r = i < old.Count ? (JsonObject)old[i].DeepClone() : new JsonObject { ["id"] = "rv" + (i + 1) };
            r["version"] = p.ElementAtOrDefault(0) ?? ""; r["date"] = p.ElementAtOrDefault(1) ?? ""; r["author"] = p.ElementAtOrDefault(2) ?? "";
            string t = p.Length > 3 ? string.Join(" | ", p.Skip(3)) : "";
            if (lang == "de") r["text"] = t;
            else { var i18n = r["i18n"] as JsonObject ?? new JsonObject(); r["i18n"] = i18n; i18n[lang] = new JsonObject { ["text"] = t }; if (r.Str("text") == null) r["text"] = t; }
            arr.Add(r); i++;
        }
        if (arr.Count == 0) g.Remove("revisions"); else g["revisions"] = arr;
    }

    void SetGlossary(JsonObject e, string text)
    {
        var g = GuideOf(e);
        var old = (g.Arr("glossary") ?? new JsonArray()).OfType<JsonObject>().ToList();
        var arr = new JsonArray();
        string lang = _view.Language;
        int i = 0;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.Trim().Length == 0) continue;
            int eq = raw.IndexOf('=');
            string term = (eq > 0 ? raw[..eq] : raw).Trim(), expl = eq > 0 ? raw[(eq + 1)..].Trim() : "";
            var r = i < old.Count ? (JsonObject)old[i].DeepClone() : new JsonObject { ["id"] = "gl" + (i + 1) };
            if (lang == "de") { r["term"] = term; r["text"] = expl; }
            else
            {
                var i18n = r["i18n"] as JsonObject ?? new JsonObject(); r["i18n"] = i18n; i18n[lang] = new JsonObject { ["term"] = term, ["text"] = expl };
                if (r.Str("term") == null) { r["term"] = term; r["text"] = expl; }
            }
            arr.Add(r); i++;
        }
        if (arr.Count == 0) g.Remove("glossary"); else g["glossary"] = arr;
    }
}
