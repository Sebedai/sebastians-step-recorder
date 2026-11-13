using System.Text.Json.Nodes;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Rückgängig und Wiederholen über das Schließen und Neustarten hinweg: die Stapel der Bearbeitungsstände (nur das Overlay
/// <c>edits.json</c>, nie Bilder – die aufgenommenen Bilder werden durch Bearbeiten nie verändert) liegen in
/// <c>history/undo.json</c> der Aufnahme. Höchstens 200 Schritte und 20 MB; „Endgültig schwärzen“ löscht den Ordner mit.
/// </summary>
sealed partial class ViewerWindow
{
    const int MaxUndo = 200;
    const long MaxUndoBytes = 20_000_000;
    DispatcherTimer? _undoTimer;
    bool _undoLoaded;

    string UndoFile => Path.Combine(_view.Session.Path, "history", "undo.json");

    /// <summary>Gespeicherte Stapel beim Öffnen übernehmen.</summary>
    void LoadUndo()
    {
        if (_undoLoaded) return;
        _undoLoaded = true;
        if (_view.Session.ReadOnly || !File.Exists(UndoFile)) return;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(UndoFile)) is not JsonObject o) return;
            var undo = (o["undo"] as JsonArray ?? new JsonArray()).Select(x => x?.GetValue<string>()).OfType<string>().ToList();
            var redo = (o["redo"] as JsonArray ?? new JsonArray()).Select(x => x?.GetValue<string>()).OfType<string>().ToList();
            // Wiederholen nur, wenn seither niemand anders das Overlay geändert hat
            bool same = o["revision"] is JsonValue rv && rv.TryGetValue<long>(out var r) && r == _view.Edits.Long("revision");
            _undo.InsertRange(0, undo);
            if (same) _redo.AddRange(redo);
            Trim();
        }
        catch (Exception ex) { AppLog.Write("Rückgängig-Verlauf nicht gelesen: " + ex.Message); }
    }

    void Trim()
    {
        while (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        while (_redo.Count > MaxUndo) _redo.RemoveAt(0);
        long size = _undo.Sum(s => (long)s.Length) + _redo.Sum(s => (long)s.Length);
        while (size > MaxUndoBytes && _undo.Count > 0) { size -= _undo[0].Length; _undo.RemoveAt(0); }
    }

    /// <summary>Nach jeder Änderung der Stapel: gebündelt nach einer Sekunde speichern.</summary>
    void ScheduleUndoSave()
    {
        if (_view.Session.ReadOnly || !_undoLoaded) return;
        _undoTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => { _undoTimer!.Stop(); SaveUndoNow(); }, Dispatcher);
        _undoTimer.Stop(); _undoTimer.Start();
    }

    void SaveUndoNow()
    {
        if (_view.Session.ReadOnly || !_undoLoaded) return;
        try
        {
            Trim();
            if (_undo.Count == 0 && _redo.Count == 0) { if (File.Exists(UndoFile)) File.Delete(UndoFile); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(UndoFile)!);
            var o = new JsonObject
            {
                ["revision"] = _view.Edits.Long("revision"),
                ["undo"] = new JsonArray(_undo.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
                ["redo"] = new JsonArray(_redo.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
            };
            string tmp = UndoFile + ".tmp";
            File.WriteAllText(tmp, o.ToJsonString());
            File.Move(tmp, UndoFile, true);
        }
        catch (Exception ex) { AppLog.Write("Rückgängig-Verlauf nicht gespeichert: " + ex.Message); }
    }
}
