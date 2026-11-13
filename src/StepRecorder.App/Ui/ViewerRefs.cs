using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;
using StepRecorder.Text;

namespace StepRecorder.App.Ui;

/// <summary>Querverweise und Variablen im Editor (13, Paket 23): Ziele und Variablen für das Einfügen-Menü, Variablen im Dokumentbereich.</summary>
sealed partial class ViewerWindow
{
    /// <summary>Beim Aktivieren die Quellen der Textfelder auf dieses Fenster setzen.</summary>
    void InstallTextSources()
    {
        RichEdit.RefSource = RefTargets;
        RichEdit.VarSource = () => _view.Variables().Where(kv => !SessionView.BuiltInVars.Any(b => (_view.Language == "en" ? b.de : b.en) == kv.Key)).Select(kv => (kv.Key, kv.Value)).OrderBy(x => SessionView.BuiltInVars.Any(b => b.de == x.Key || b.en == x.Key) ? 1 : 0).ThenBy(x => x.Key).ToList();
        RichEdit.EditVars = () => { ShowCover(); _status.Text = Loc.L("Variablen stehen unter „Einleitung und Texte“.", "Variables are under “Introduction and texts”."); };
        RichFlow.RefLabel = RefLabel;
    }

    /// <summary>Schritte und Kapitel der Anleitung mit ihren gedruckten Nummern.</summary>
    IReadOnlyList<(string target, string label)> RefTargets()
    {
        var nums = GuideExport.StepNumbers(_view);
        var list = new List<(string, string)>();
        int chap = 0;
        foreach (var s in _view.GuideSteps())
        {
            if (s.Kind == "move") continue;
            if (s.ChapterTitle is { Length: > 0 } ct) list.Add(("k" + s.Anchor, Loc.F("Kapitel {0}: {1}", "Chapter {0}: {1}", ++chap, Short(ct))));
            string n = nums.TryGetValue(s.Anchor, out var x) && x.Length > 0 ? x : "–";
            list.Add(("s" + s.Anchor, Loc.F("Schritt {0}: {1}", "Step {0}: {1}", n, Short(s.AuthorTitle ?? s.Title))));
        }
        return list;
    }

    static string Short(string s) => s.Length > 60 ? s[..60] + "…" : s;

    /// <summary>Beschriftung eines Verweises im Textfeld: wie in der Anleitung, „??“ wenn das Ziel nicht (mehr) gezeigt wird.</summary>
    string? RefLabel(string target)
    {
        if (!RichDoc.IsRefTarget(target)) return null;
        long a = long.Parse(target.AsSpan(1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture);
        if (target[0] == 's')
            return GuideExport.StepNumbers(_view).TryGetValue(a, out var n) ? Loc.For(_view.Language, "Schritt ", "step ") + n : "??";
        int chap = 0;
        foreach (var s in _view.GuideSteps())
            if (s.ChapterTitle is { Length: > 0 }) { chap++; if (s.Anchor == a) return Loc.For(_view.Language, "Kapitel ", "chapter ") + chap; }
        return "??";
    }

    // ---------------- Variablen im Dokumentbereich ----------------

    /// <summary>Eigene Variablen als Zeilen „Name = Wert“ (in der Sprache der Ansicht).</summary>
    string VarsText()
    {
        var g = _view.Edits.Obj("guide");
        var vars = _view.Language != "de" ? g?.Obj("i18n")?.Obj(_view.Language)?.Obj("vars") : g?.Obj("vars");
        return string.Join("\n", (vars ?? new JsonObject()).Select(kv => kv.Key + " = " + (kv.Value is JsonValue v && v.TryGetValue<string>(out var s) ? s : "")));
    }

    void SetVars(JsonObject e, string text)
    {
        var obj = new JsonObject();
        var bad = new List<string>();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.Trim().Length == 0) continue;
            int eq = raw.IndexOf('=');
            string name = (eq > 0 ? raw[..eq] : raw).Trim().Trim('{', '}').Trim();
            string value = eq > 0 ? raw[(eq + 1)..].Trim() : "";
            if (SessionView.IsVarName(name)) obj[name] = value; else bad.Add(name);
        }
        var g = GuideOf(e);
        JsonObject holder = g;
        if (_view.Language != "de")
        {
            var i18n = g["i18n"] as JsonObject ?? new JsonObject(); g["i18n"] = i18n;
            holder = i18n[_view.Language] as JsonObject ?? new JsonObject(); i18n[_view.Language] = holder;
        }
        if (obj.Count == 0) holder.Remove("vars"); else holder["vars"] = obj;
        if (bad.Count > 0) _status.Text = Loc.F("Ungültiger Name übersprungen: {0}", "Invalid name skipped: {0}", string.Join(", ", bad));
    }
}
