using System.Text.Json.Nodes;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Schritte zusammenfassen (Strg+J) und aufheben (Strg+Umschalt+J): in Anleitungen erscheint die Gruppe als ein Schritt
/// (<c>edits.merges[]</c>); KI-Paket, MCP und AutoHotkey zeigen weiter die Einzelschritte.
/// </summary>
sealed partial class ViewerWindow
{
    JsonObject? MergeEdit(JsonObject edits, string id) => (edits.Arr("merges") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(m => m.Str("id") == id);

    void MergeSelected()
    {
        var items = _list.SelectedItems.OfType<ListBoxItem>().OrderBy(i => _list.Items.IndexOf(i)).ToList();
        var steps = items.Select(i => i.Tag).OfType<ViewStep>().ToList();
        string? problem = null;
        if (steps.Count < 2) problem = Loc.L("Bitte mindestens zwei aufeinanderfolgende Schritte auswählen (Umschalt+Klick oder Strg+Klick).", "Please select at least two consecutive steps (Shift+click or Ctrl+click).");
        else
        {
            var idx = items.Select(i => _list.Items.IndexOf(i)).ToList();
            if (idx.Zip(idx.Skip(1), (a, b) => b - a).Any(d => d != 1)) problem = Loc.L("Zusammenfassen geht nur mit aufeinanderfolgenden Schritten.", "Only consecutive steps can be merged.");
            else if (steps.Any(s => s.Manual)) problem = Loc.L("Eingefügte Schritte lassen sich nicht zusammenfassen.", "Inserted steps cannot be merged.");
            else if (steps.Any(s => s.Kind == "gap")) problem = Loc.L("Lücken in der Aufnahme lassen sich nicht zusammenfassen.", "Gaps in the recording cannot be merged.");
            else if (steps.Any(s => _view.MergeOf(s.Anchor) != null)) problem = Loc.F("Ein Schritt gehört schon zu einer Gruppe – erst aufheben ({0}).", "A step already belongs to a group – dissolve it first ({0}).", Cmd("unmerge").Gesture);
            else if (steps.Skip(1).Any(s => s.ChapterTitle != null)) problem = Loc.L("In der Auswahl beginnt ein Kapitel – bitte nur Schritte desselben Kapitels zusammenfassen.", "A chapter starts within the selection – please merge only steps of the same chapter.");
        }
        if (problem != null) { _status.Text = problem; return; }
        var anchors = steps.Select(s => s.Anchor).ToList();
        Change(e =>
        {
            var arr = e["merges"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject { ["id"] = NextId(e, "merges", "mg"), ["anchors"] = new JsonArray(anchors.Select(a => (JsonNode)a).ToArray()), ["createdUtc"] = Json.UtcNow(), ["by"] = "user" });
            e["merges"] = arr;
        });
        SelectAnchor(anchors[0]);
        _status.Text = Loc.F("{0} Schritte zusammengefasst – in Anleitungen ein Schritt; KI-Paket und AutoHotkey zeigen weiter alle. Aufheben mit {1}.", "{0} steps merged – one step in guides; AI package and AutoHotkey still show all. Dissolve with {1}.", anchors.Count, Cmd("unmerge").Gesture);
    }

    void UnmergeSelected()
    {
        var s = Sel;
        if (s == null || s.Manual || _view.MergeOf(s.Anchor) is not { } mg) { _status.Text = Loc.L("Dieser Schritt ist nicht zusammengefasst.", "This step is not merged."); return; }
        Change(e => { if (e["merges"] is JsonArray arr && MergeEdit(e, mg.id) is { } m) arr.Remove(m); });
        _status.Text = Loc.N(mg.count, "Zusammenfassung aufgehoben", "Zusammenfassung von {0} Schritten aufgehoben", "Merge dissolved", "Merge of {0} steps dissolved");
    }

    /// <summary>Eigener Text eines zusammengefassten Schritts (je Sprache in <c>merges[].text</c>).</summary>
    void SaveMergeText(string mergeId, string title, string body, bool rich = false) => Change(e =>
    {
        if (MergeEdit(e, mergeId) is not { } m) return;
        var text = m["text"] as JsonObject ?? new JsonObject();
        text[_view.Language] = new JsonObject { ["title"] = title, ["body"] = body, ["by"] = "user", ["atUtc"] = Json.UtcNow() };
        if (rich) ((JsonObject)text[_view.Language]!)["fmt"] = 1;
        m["text"] = text;
    });

    void ResetMergeText(string mergeId) => Change(e =>
    {
        if (MergeEdit(e, mergeId) is { } m && m["text"] is JsonObject t) { t.Remove(_view.Language); if (t.Count == 0) m.Remove("text"); }
    });

    bool MergeTextEdited(string mergeId) => MergeEdit(_view.Edits, mergeId)?.Obj("text")?.Obj(_view.Language) != null;
}
