using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Exportieren aus dem Editor: Dialog mit gemerkten Optionen, Export im Hintergrund (eigene Leseansicht, Fortschritt,
/// „Abbrechen“), Ergebnisfenster; Strg+Umschalt+E wiederholt den letzten Export dieser Aufnahme ohne Rückfrage.
/// </summary>
sealed partial class ViewerWindow
{
    bool _exporting;

    /// <summary>Exportmenü: nur Formate, die dieses Programm erzeugen kann.</summary>
    (string, Action)[] ExportMenuItems()
    {
        var items = new List<(string, Action)>();
        foreach (var (f, label) in new[]
        {
            ("html", "HTML …"), ("md", "Markdown …"), ("docx", "Word (DOCX) …"), ("pdf", "PDF …"), ("pptx", "PowerPoint …"),
            ("tour", Loc.L("Klick-Tour …", "Click tour …")), ("bugreport", Loc.L("Fehlerbericht …", "Bug report …")), ("gif", "GIF …")
        })
            if (Exporter.IsKnownFormat(f)) items.Add((label, () => ShowExportDialog(f)));
        items.Add(("-", () => { }));
        items.Add((Loc.L("KI-Paket …", "AI package …"), () => ShowExportDialog("ki")));
        items.Add((Loc.L("AutoHotkey-Skript …", "AutoHotkey script …"), () => ShowExportDialog("ahk")));
        items.Add((Loc.L("Support-Paket (ZIP) …", "Support package (ZIP) …"), StartSupportFlow));
        items.Add(("-", () => { }));
        items.Add(M("repeatExport"));
        return items.ToArray();
    }

    /// <summary>Ausgaben ohne Platzhalter für ungeprüfte Eingaben verlangen vorher die Durchsicht.</summary>
    bool ReviewedFor(string format)
    {
        if (_view.UnreviewedCount == 0 || Exporter.Normalize(format) == "ki") return true;
        if (Msg.Show(Loc.N(_view.UnreviewedCount,
                "1 auffällige Eingabe ist noch nicht durchgesehen. Die Ausgabe kann erst nach der Durchsicht erstellt werden.\n\nJetzt prüfen?",
                "{0} auffällige Eingaben sind noch nicht durchgesehen. Die Ausgabe kann erst nach der Durchsicht erstellt werden.\n\nJetzt prüfen?",
                "1 conspicuous input has not been reviewed yet. The output can only be created after the review.\n\nReview now?",
                "{0} conspicuous inputs have not been reviewed yet. The output can only be created after the review.\n\nReview now?"),
                Loc.L("Durchsicht", "Review"), MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            ShowReview();
        return _view.UnreviewedCount == 0;
    }

    public void ShowExportDialog(string format)
    {
        if (_exporting) return;
        format = Exporter.Normalize(format);
        if (!ReviewedFor(format)) return;
        var st = AppState.Load();
        var initial = InitialRequest(format, st);
        var last = st.PeekSession(SessionKey)?.Export;
        string? lastTarget = last != null && Exporter.Normalize(last.Str("format") ?? "") == format ? last.Str("target") : null;
        var d = new ExportDialog(initial, DefaultTargetFor, lastTarget) { Owner = this };
        var findings = Findings();
        if (findings.Count > 0) d.ShowQuality(QualitySummary(findings), findings.Any(f => f.Severity == "warning"), ShowQuality);
        if (d.ShowDialog() != true) return;
        var req = d.Request;
        Remember(req, d.FolderChoice);
        RunExport(req);
    }

    /// <summary>Vorbelegung: zuletzt gewählte Optionen dieses Formats, Sprache wie beim letzten Export dieser Aufnahme.</summary>
    ExportRequest InitialRequest(string format, AppState st)
    {
        ExportRequest r;
        if (st.Export.Formats.TryGetValue(format, out var o)) r = ExportRequest.FromJson(o);
        else
        {
            r = new ExportRequest();
            var doc = DocSettings.From(_view.Edits.Obj("guide"));
            r.View = doc.View; r.Overview = doc.Overview;
        }
        r.Format = format; r.Target = null;
        r.Language = st.PeekSession(SessionKey)?.Export?.Str("language") ?? _view.Language;
        return r;
    }

    /// <param name="folder">null = zuletzt genutzter Ordner dieses Formats, "" = export\ der Aufnahme, sonst dieser Ordner.</param>
    string DefaultTargetFor(ExportRequest r, string? folder)
    {
        if (folder == null && AppState.Load().Export.Formats.TryGetValue(Exporter.Normalize(r.Format), out var o)) folder = o.Str("folder");
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) folder = null;
        return Exporter.DefaultTarget(_view, r.Format, folder, r.Template, r.Language);
    }

    void Remember(ExportRequest req, string? folderChoice)
    {
        try
        {
            string key = Exporter.Normalize(req.Format);
            string sessionExport = Path.GetFullPath(Exporter.SessionExportDir(_view));
            AppState.Update(st =>
            {
                st.Export.LastFormat = key;
                var o = req.ToJson(); o.Remove("target"); o.Remove("language");
                string? prev = st.Export.Formats.TryGetValue(key, out var p) ? p.Str("folder") : null;
                string? folder = folderChoice == null ? prev : folderChoice.Length == 0 ? null : folderChoice;
                // Ordner innerhalb einer Aufnahme nicht als Vorgabe für andere Aufnahmen merken
                if (folder != null && (Path.GetFullPath(folder) + "\\").StartsWith(sessionExport + "\\", StringComparison.OrdinalIgnoreCase)) folder = null;
                if (folder != null) o["folder"] = folder;
                st.Export.Formats[key] = o;
                st.Session(SessionKey).Export = req.ToJson();
            });
        }
        catch (Exception ex) { AppLog.Write("Exportoptionen nicht gemerkt: " + ex.Message); }
    }

    /// <summary>Strg+Umschalt+E: letzten Export dieser Aufnahme mit denselben Optionen und demselben Ziel wiederholen.</summary>
    void RepeatLastExport()
    {
        if (_exporting) return;
        var o = AppState.Load().PeekSession(SessionKey)?.Export;
        if (o == null) { ShowExportDialog(AppState.Load().Export.LastFormat ?? "html"); return; }
        var r = ExportRequest.FromJson(o);
        if (!ReviewedFor(r.Format)) return;
        if (string.IsNullOrWhiteSpace(r.Target)) r.Target = null;
        RunExport(r);
    }

    /// <summary>
    /// Export im Hintergrund mit eigener Leseansicht. Der Editor ist währenddessen gesperrt; dauert es länger als
    /// 0,7 s, erscheint ein Fortschrittsfenster mit „Abbrechen“. Ein Abbruch hinterlässt nichts am Ziel.
    /// </summary>
    async void RunExport(ExportRequest req)
    {
        if (_exporting) return;
        _exporting = true;
        var root = Content as UIElement;
        if (root != null) root.IsHitTestVisible = false;
        Cursor = Cursors.Wait;
        using var cts = new CancellationTokenSource();
        var prog = new ExportProgressWindow(req.Format) { Owner = this };
        prog.CancelRequested += () => { try { cts.Cancel(); } catch (ObjectDisposedException) { } };
        var progress = new Progress<ExportProgress>(prog.Report);
        string path = _path, lang = req.Language ?? _view.Language;
        bool headless = Exporter.Normalize(req.Format) == "ki";
        var task = Task.Run(() =>
        {
            using var view = SessionView.Open(path, headless: headless, lang: lang);
            using var job = ExportJob.Begin(progress, cts.Token);
            return Exporter.Run(view, req);
        });
        try
        {
            if (await Task.WhenAny(task, Task.Delay(700)) != task)
            {
                _ = task.ContinueWith(_ => Dispatcher.InvokeAsync(prog.Finish), TaskScheduler.Default);
                prog.ShowDialog();
            }
            var r = await task;
            _status.Text = Loc.F("Exportiert: {0}", "Exported: {0}", r.Path);
            ExportResultDialog.Show(this, r, req.Format);
        }
        catch (OperationCanceledException) { _status.Text = Loc.L("Export abgebrochen – am Ziel wurde nichts verändert.", "Export cancelled – nothing was changed at the target."); }
        catch (ExportTargetException ex) { Msg.Show(ex.Message, Loc.L("Exportieren", "Export"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        catch (Exception ex) { UiApp.ShowError(ex); }
        finally
        {
            prog.Finish();
            _exporting = false;
            if (root != null) root.IsHitTestVisible = true;
            Cursor = null;
        }
    }
}
