using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Packen als <c>.steprec</c>: „Vollständig“ (alle Rohdaten, der Empfänger kann Schutzmaßnahmen aufheben – mit zweiter
/// Bestätigung) oder „Bereinigte Kopie“ (Schwärzungen und Verallgemeinerungen fest angewendet).
/// </summary>
static class PackDialog
{
    public static void Show(Window owner, string folder)
    {
        var info = SessionCatalog.Read(folder);
        if (info.Recording) { Msg.Show(Loc.L("Die Aufnahme läuft noch.", "The recording is still running."), Loc.L("Packen", "Pack")); return; }
        var edits = Json.ReadObject(Path.Combine(folder, "edits.json")) ?? new JsonObject();
        int redactions = edits.Arr("redactions")?.Count ?? 0, generalizations = edits.Arr("substitutions")?.Count ?? 0;
        int internalComments = (edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>().Count(c => StepRecorder.ReadLayer.SessionView.IsInternalKind(c.Str("kind")) || c.Bool("hidden"));
        int unreviewed = SessionCatalog.Details(folder)?.Unreviewed ?? 0;
        int protections = redactions + generalizations + internalComments + unreviewed;

        var w = new Window { Title = Loc.L("Packen", "Pack"), Width = 640, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        RadioButton Card(string glyph, string title, string text, IEnumerable<string> notes, bool enabled)
        {
            var col = U.Col(U.Row(U.Icon(glyph, 16), U.Text(title, 14, true, margin: new Thickness(10, 0, 0, 0))), U.Text(text, 12.5, fg: Theme.Fg2, margin: new Thickness(0, 6, 0, 0)));
            foreach (var n in notes) col.Children.Add(U.Row(U.Icon(Theme.IDot, 8, Theme.Muted), U.Text(n, 12, fg: Theme.Muted, margin: new Thickness(8, 0, 0, 0))));
            foreach (var tb in col.Children.OfType<TextBlock>()) tb.TextWrapping = TextWrapping.Wrap;
            var rb = new RadioButton { GroupName = "pack", Style = Theme.Style("Choice"), Content = col, IsEnabled = enabled, Margin = new Thickness(0, 0, 0, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(16, 12, 16, 12) };
            return rb;
        }
        var fullNotes = new List<string>();
        if (redactions > 0) fullNotes.Add(Loc.N(redactions, "1 Schwärzung – der Empfänger kann sie aufheben", "{0} Schwärzungen – der Empfänger kann sie aufheben", "1 redaction – the recipient can undo it", "{0} redactions – the recipient can undo them"));
        if (generalizations > 0) fullNotes.Add(Loc.N(generalizations, "1 Verallgemeinerung – der Originalwert ist enthalten", "{0} Verallgemeinerungen – die Originalwerte sind enthalten", "1 generalization – the original value is included", "{0} generalizations – the original values are included"));
        if (internalComments > 0) fullNotes.Add(Loc.N(internalComments, "1 interner Kommentar – lesbar", "{0} interne Kommentare – lesbar", "1 internal comment – readable", "{0} internal comments – readable"));
        if (unreviewed > 0) fullNotes.Add(Loc.N(unreviewed, "1 ungeprüfte Eingabe – im Klartext", "{0} ungeprüfte Eingaben – im Klartext", "1 unreviewed input – in plain text", "{0} unreviewed inputs – in plain text"));
        var full = Card(Theme.IDoc, Loc.L("Vollständig – zum Weiterbearbeiten", "Complete – for further editing"),
            Loc.L("Alle Rohdaten der Aufnahme, auch was im Editor geschwärzt, verallgemeinert oder ausgeblendet ist. Nur an Personen geben, die alles sehen dürfen.", "All raw data of the recording, including what is redacted, generalized or hidden in the editor. Only give it to people who may see everything."),
            fullNotes, true);
        var clean = Card(Theme.IShield, Loc.L("Bereinigte Kopie – zum Weitergeben", "Sanitized copy – for sharing"),
            unreviewed > 0 ? Loc.L("Erst nach der Durchsicht möglich: ungeprüfte Eingaben könnten Passwörter enthalten.", "Only possible after the review: unreviewed input could contain passwords.")
                           : Loc.L("Schwärzungen und Verallgemeinerungen werden fest angewendet, interne Kommentare entfernt, Bilder geschwärzter Zeiträume gelöscht. Bekommt eine neue Kennung.", "Redactions and generalizations are applied permanently, internal comments removed, images of redacted time ranges deleted. Gets a new id."),
            Array.Empty<string>(), unreviewed == 0);
        (unreviewed == 0 ? clean : full).IsChecked = true;
        bool ok = false;
        U.Dialog(w, Loc.F("„{0}“ packen", "Pack \"{0}\"", info.Title), U.Col(clean, full),
            U.Primary(Loc.L("Speichern unter …", "Save as …"), () => { ok = true; w.Close(); }, Theme.IExport), U.Btn(Loc.L("Abbrechen", "Cancel"), () => w.Close()));
        w.ShowDialog();
        if (!ok) return;
        bool sanitized = clean.IsChecked == true;
        if (!sanitized && protections > 0 &&
            Msg.Show(Loc.L("Die vollständige Datei enthält alles, was Sie geschützt haben, in lesbarer Form:\n\n", "The complete file contains everything you protected in readable form:\n\n") + string.Join("\n", fullNotes.Select(n => "• " + n)) + Loc.L("\n\nTrotzdem vollständig packen?", "\n\nPack completely anyway?"),
                Loc.L("Vollständig packen", "Pack completely"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var lib = AppState.Load().Library;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = Fmt.FileSafe(info.Title, 60) + (sanitized ? Loc.L(" (bereinigt)", " (sanitized)") : "") + ".steprec",
            Filter = Loc.L("Aufnahme-Archiv (*.steprec)|*.steprec", "Recording archive (*.steprec)|*.steprec"), DefaultExt = ".steprec", OverwritePrompt = true
        };
        if (lib.PackFolder is { } pf && Directory.Exists(pf)) dlg.InitialDirectory = pf;
        else dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (dlg.ShowDialog(owner) != true) return;
        string target = dlg.FileName;
        AppState.Update(st => st.Library.PackFolder = Path.GetDirectoryName(target));
        _ = Run(owner, folder, target, sanitized);
    }

    static async Task Run(Window owner, string folder, string target, bool sanitized)
    {
        try
        {
            await ExportProgressWindow.Run(owner, sanitized ? Loc.L("Bereinigte Kopie wird erstellt …", "Creating sanitized copy …") : Loc.L("Wird gepackt …", "Packing …"), (progress, ct) =>
            {
                var p = new Progress<(int done, int total)>(x => ((IProgress<ExportProgress>)progress).Report(new ExportProgress(x.done, x.total, "")));
                if (!sanitized) { SessionPackage.Pack(folder, target, p, ct); return 0; }
                string tmp = Path.Combine(Path.GetDirectoryName(target)!, ".steprec-tmp-" + Guid.NewGuid().ToString("N")[..8]);
                try
                {
                    var copy = SessionSanitizer.CreateCopy(folder, Path.Combine(tmp, "copy"), p, ct);
                    SessionPackage.Pack(copy, target, p, ct);
                }
                finally { try { Directory.Delete(tmp, true); } catch { } }
                return 0;
            });
            var r = new ExportResult { Path = target };
            r.Notices.Add(sanitized ? Loc.L("Bereinigte Kopie – Schwärzungen und Verallgemeinerungen sind fest angewendet.", "Sanitized copy – redactions and generalizations are applied permanently.")
                                    : Loc.L("Vollständig – enthält alle Rohdaten.", "Complete – contains all raw data."));
            ExportResultDialog.Show(owner, r, "steprec");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is SanitizeException or PackageException) { Msg.Show(ex.Message, Loc.L("Packen", "Pack"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        catch (Exception ex) { UiApp.ShowError(ex); }
    }
}

/// <summary>Entpacken einer <c>.steprec</c>-Datei in den Aufnahmeordner (mit Rückfrage bei gleicher Aufnahme) und öffnen.</summary>
static class SessionImport
{
    public static async Task<string?> Unpack(Window owner, string file, Action? done = null)
    {
        var man = SessionPackage.PeekManifest(file);
        if (man == null) { Msg.Show(Loc.L("Diese Datei ist kein gültiges Aufnahme-Archiv.", "This file is not a valid recording archive."), Loc.L("Entpacken", "Unpack")); return null; }
        string parent = AppPaths.DefaultSessionsFolder();
        string title = man.Str("title") is { Length: > 0 } t ? t : Path.GetFileNameWithoutExtension(file);
        if (SessionPackage.FindExisting(parent, man.Str("sessionId") ?? "") is { } existing)
        {
            var r = Msg.Show(Loc.F("„{0}“ ist bereits im Aufnahmeordner.\n\nJa: trotzdem als weitere Kopie entpacken\nNein: die vorhandene öffnen", "\"{0}\" is already in the recordings folder.\n\nYes: unpack as another copy anyway\nNo: open the existing one", title),
                Loc.L("Entpacken", "Unpack"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.No) { UiApp.Instance.OpenViewer(existing); return existing; }
            if (r != MessageBoxResult.Yes) return null;
        }
        try
        {
            var dir = await ExportProgressWindow.Run(owner, Loc.L("Wird entpackt …", "Unpacking …"), (_, ct) => SessionPackage.Unpack(file, parent, null, ct));
            done?.Invoke();
            UiApp.Instance.OpenViewer(dir);
            return dir;
        }
        catch (OperationCanceledException) { return null; }
        catch (PackageException ex) { Msg.Show(ex.Message, Loc.L("Entpacken", "Unpack"), MessageBoxButton.OK, MessageBoxImage.Warning); return null; }
        catch (Exception ex) { UiApp.ShowError(ex); return null; }
    }
}
