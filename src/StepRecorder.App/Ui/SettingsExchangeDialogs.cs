using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Einstellungen exportieren, importieren, zurücksetzen – jede schwächende Änderung braucht ein eigenes Häkchen.</summary>
static class SettingsExchangeDialogs
{
    public static void Export(Window owner)
    {
        var w = new DialogWindow { Title = Loc.L("Einstellungen exportieren", "Export settings"), Width = 560, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var boxes = SettingsExchange.Parts.Select(p => new CheckBox { Content = SettingsExchange.PartName(p), IsChecked = true, Tag = p, Margin = new Thickness(0, 4, 0, 4) }).ToList();
        var body = U.Col(U.Text(Loc.L("Was soll in die Datei?", "What should go into the file?"), 13, true, margin: new Thickness(0, 0, 0, 6)));
        foreach (var b in boxes) body.Children.Add(b);
        body.Children.Add(U.Text(Loc.L("Nicht enthalten: Aufnahmeordner, zuletzt verwendete Aufnahmen, Benutzer- und Rechnername. Die Ausschlussliste verrät, welche Programme Sie als vertraulich behandeln.",
            "Not included: recordings folder, recent recordings, user and computer name. The exclusion list reveals which programs you treat as confidential."), 12, fg: Theme.Muted, margin: new Thickness(0, 10, 0, 0)));
        U.Dialog(w, Loc.L("Einstellungen exportieren", "Export settings"), body, U.Primary(Loc.L("Speichern …", "Save …"), () =>
        {
            var parts = boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag).ToList();
            if (parts.Count == 0) return;
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = Loc.F("StepRecorder-Einstellungen-{0:yyyy-MM-dd}.json", "StepRecorder-settings-{0:yyyy-MM-dd}.json", DateTime.Now), Filter = Loc.L("Einstellungen (*.json)|*.json", "Settings (*.json)|*.json") };
            if (dlg.ShowDialog(w) != true) return;
            File.WriteAllText(dlg.FileName, Json.Pretty(SettingsExchange.Export(AppSettings.Load(), parts)));
            w.DialogResult = true;
        }), U.Btn(Loc.L("Abbrechen", "Cancel"), () => w.DialogResult = false));
        w.ShowDialog();
    }

    public static bool Import(Window owner)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.L("Einstellungen (*.json)|*.json", "Settings (*.json)|*.json") };
        if (dlg.ShowDialog(owner) != true) return false;
        string json;
        try { if (new FileInfo(dlg.FileName).Length > SettingsExchange.MaxBytes) throw new IOException(Loc.L("Die Datei ist zu groß.", "The file is too large.")); json = File.ReadAllText(dlg.FileName); }
        catch (Exception ex) { Msg.Show(ex.Message, owner.Title, MessageBoxButton.OK, MessageBoxImage.Error); return false; }
        var plan = SettingsExchange.Plan(json, AppSettings.Load());
        return Confirm(owner, plan, Loc.L("Einstellungen importieren", "Import settings"));
    }

    public static bool Reset(Window owner)
    {
        var current = AppSettings.Load();
        bool templates = false;
        if (current.DocTemplates.Count > 0)
            templates = Msg.Show(Loc.F("Auch die {0} Dokumentvorlagen entfernen?", "Also remove the {0} document templates?", current.DocTemplates.Count), Loc.L("Zurücksetzen", "Reset"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        return Confirm(owner, SettingsExchange.PlanReset(current, templates), Loc.L("Einstellungen zurücksetzen", "Reset settings"));
    }

    /// <summary>Änderungen zeigen; schwächende Änderungen einzeln bestätigen lassen (kein „alles bestätigen“).</summary>
    static bool Confirm(Window owner, ImportPlan plan, string heading)
    {
        if (!plan.Ok) { Msg.Show(string.Join("\n", plan.Errors), heading, MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        var w = new DialogWindow { Title = heading, Width = 640, SizeToContent = SizeToContent.Height, MaxHeight = Theme.ActiveWorkArea().Height - 40, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var body = new StackPanel();
        foreach (var sk in plan.SkippedParts) body.Children.Add(U.Text("⚠ " + sk, 12.5, fg: Theme.Warn, margin: new Thickness(0, 0, 0, 6)));
        var normal = plan.Changes.Where(c => !c.Weakens).ToList();
        if (normal.Count == 0 && !plan.Weakening.Any()) body.Children.Add(U.Text(Loc.L("Keine Änderungen.", "No changes."), 13));
        if (normal.Count > 0)
        {
            body.Children.Add(U.Overline(Loc.L("Wird übernommen", "Will be applied"), new Thickness(0, 4, 0, 6)));
            foreach (var c in normal.Take(40)) body.Children.Add(U.Text("· " + c.Text, 12.5, fg: Theme.Fg2, margin: new Thickness(0, 1, 0, 1)));
            if (normal.Count > 40) body.Children.Add(U.Text(Loc.F("… und {0} weitere", "… and {0} more", normal.Count - 40), 12, fg: Theme.Muted));
        }
        var weakBoxes = new List<CheckBox>();
        if (plan.Weakening.Any())
        {
            body.Children.Add(U.Overline(Loc.L("Schwächt den Schutz – nur mit Häkchen", "Weakens protection – only if ticked"), new Thickness(0, 14, 0, 6)));
            foreach (var c in plan.Weakening)
            {
                var cb = new CheckBox { Content = new TextBlock { Text = c.Text, TextWrapping = TextWrapping.Wrap }, Tag = c.WeakeningId, Margin = new Thickness(0, 3, 0, 3), Foreground = Theme.Warn };
                weakBoxes.Add(cb); body.Children.Add(cb);
            }
            body.Children.Add(U.Text(Loc.L("Ohne Häkchen bleibt der bisherige Schutz bestehen.", "Without a tick, the current protection stays."), 12, fg: Theme.Muted, margin: new Thickness(0, 6, 0, 0)));
        }
        body.Children.Add(U.Text(Loc.L("Die bisherigen Einstellungen werden gesichert und lassen sich zurückholen. Nicht gespeicherte Änderungen im Einstellungsfenster gehen verloren.",
            "The current settings are backed up and can be restored. Unsaved changes in the settings window are lost."), 12, fg: Theme.Muted, margin: new Thickness(0, 12, 0, 0)));
        bool applied = false;
        U.Dialog(w, heading, new ScrollViewer { Content = body, MaxHeight = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, U.Primary(Loc.L("Übernehmen", "Apply"), () =>
        {
            var confirmed = weakBoxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag).ToHashSet();
            var next = SettingsExchange.Apply(plan, AppSettings.Load(), confirmed);
            if (!SettingsExchange.Commit(next, AppPaths.SettingsFile)) { Msg.Show(next.LastSaveError ?? "", heading, MessageBoxButton.OK, MessageBoxImage.Error); return; }
            applied = true;
            UiApp.Instance.RegisterGlobalHotkeys();
            w.DialogResult = true;
        }), U.Btn(Loc.L("Abbrechen", "Cancel"), () => w.DialogResult = false));
        w.ShowDialog();
        return applied;
    }
}

/// <summary>Diagnose-Paket erstellen (Protokoll und Systemangaben, nie Aufnahmen).</summary>
static class DiagnosticsDialog
{
    public static void Create(Window owner)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"StepRecorder-Diagnose-{DateTime.Now:yyyy-MM-dd}.zip", Filter = "ZIP (*.zip)|*.zip" };
        if (dlg.ShowDialog(owner) != true) return;
        try
        {
            var path = Diagnostics.CreatePackage(dlg.FileName, new DiagnosticsExtras(Invisibility.LastResult, Invisibility.LastFailure, UiApp.Instance.HotkeyFailures, StepRecorder.Export.Ocr.Available));
            if (Msg.Show(Loc.F("Diagnose-Paket erstellt\n\n{0}\n\nEnthält: info.txt (Programm und System), settings.json (Einstellungen ohne persönliche Angaben), state.json (Fenstergrößen), log.txt (Protokoll). Keine Aufnahmen, keine Bilder, keine getippten Texte.\n\nDas Programm versendet nichts – Sie geben die Datei selbst weiter. Im Ordner zeigen?",
                    "Diagnostics package created\n\n{0}\n\nContains: info.txt (program and system), settings.json (settings without personal data), state.json (window sizes), log.txt (log). No recordings, no images, no typed text.\n\nThe program sends nothing – you pass the file on yourself. Show in folder?", path),
                    Loc.L("Diagnose-Paket", "Diagnostics package"), MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Shell.Reveal(path);
        }
        catch (Exception ex) { UiApp.ShowError(ex); }
    }
}
