using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Veröffentlichen (13, Paket 38, F12): mehrere Formate × Sprachen in einen Ordner, je Sprache ein Unterordner.</summary>
sealed class BatchExportDialog : DialogWindow
{
    readonly List<(CheckBox box, string id)> _formats = new(), _langs = new();
    readonly TextBox _folder = new();
    public List<string> Formats => _formats.Where(f => f.box.IsChecked == true).Select(f => f.id).ToList();
    public List<string> Languages => _langs.Where(f => f.box.IsChecked == true).Select(f => f.id).ToList();
    public string Folder => _folder.Text.Trim();

    public BatchExportDialog(string defaultFolder, string lang)
    {
        Title = AppInfo.DisplayName; Width = 560; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        var fRow = new WrapPanel();
        foreach (var (id, name) in new[] { ("pdf", "PDF"), ("docx", "Word"), ("html", "HTML"), ("md", "Markdown"), ("pptx", "PowerPoint") })
        { var c = new CheckBox { Content = name, IsChecked = id is "pdf" or "docx", Margin = new Thickness(0, 0, 14, 6) }; _formats.Add((c, id)); fRow.Children.Add(c); }
        var lRow = new WrapPanel();
        foreach (var (id, name) in new[] { ("de", "Deutsch"), ("en", "English") })
        { var c = new CheckBox { Content = name, IsChecked = id == lang, Margin = new Thickness(0, 0, 14, 6) }; _langs.Add((c, id)); lRow.Children.Add(c); }
        _folder.Text = defaultFolder;
        var browse = U.Btn(Theme.IFolder, Loc.L("Wählen …", "Choose …"), () =>
        {
            using var d = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = _folder.Text, UseDescriptionForTitle = true, Description = Loc.L("Zielordner", "Target folder") };
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) _folder.Text = d.SelectedPath;
        });
        browse.Margin = new Thickness(6, 0, 0, 0);
        var row = new DockPanel(); DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse); row.Children.Add(_folder);
        var p = new StackPanel { Children =
        {
            U.Field(Loc.L("Formate", "Formats"), fRow), U.Field(Loc.L("Sprachen", "Languages"), lRow, Loc.L("Mehrere Sprachen: je Sprache ein Unterordner (de, en).", "Several languages: one subfolder per language (de, en).")),
            U.Field(Loc.L("Zielordner", "Target folder"), row),
        } };
        U.Dialog(this, Loc.L("Veröffentlichen", "Publish"), p, U.Primary(Loc.L("Exportieren", "Export"), () =>
        {
            if (Formats.Count == 0 || Languages.Count == 0 || Folder.Length == 0) { Msg.Show(Loc.L("Bitte mindestens ein Format, eine Sprache und einen Ordner wählen.", "Please choose at least one format, one language and a folder.")); return; }
            DialogResult = true;
        }), U.Btn(Loc.L("Abbrechen", "Cancel"), () => DialogResult = false));
    }
}

sealed partial class ViewerWindow
{
    /// <summary>Stapel-Export aus dem Editor.</summary>
    async void PublishBatch()
    {
        InlineCommit(now: true);
        if (_view.UnreviewedCount > 0) { Msg.Show(Loc.L("Bitte zuerst die auffälligen Eingaben durchsehen.", "Please review the flagged inputs first.")); return; }
        string def = Path.Combine(Path.GetDirectoryName(Exporter.DefaultTarget(_view, "pdf", null)) ?? _path, Loc.L("Veröffentlichung", "Publication"));
        var d = new BatchExportDialog(def, _view.Language) { Owner = this };
        if (d.ShowDialog() != true) return;
        var jobs = BatchExport.Plan(d.Formats, d.Languages, d.Folder);
        string path = _path;
        var errors = new List<string>();
        int done = await ExportProgressWindow.Run(this, Loc.L("Wird veröffentlicht …", "Publishing …"), (progress, ct) =>
        {
            int n = 0;
            foreach (var job in jobs)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    Directory.CreateDirectory(job.Folder);
                    using var view = SessionView.Open(path, lang: job.Language);
                    using var ej = ExportJob.Begin(progress, ct);
                    Exporter.Run(view, new ExportRequest { Format = job.Format, Language = job.Language, Target = job.Folder + Path.DirectorySeparatorChar });
                    n++;
                }
                catch (Exception ex) { lock (errors) errors.Add($"{job.Format}/{job.Language}: {ex.Message}"); }
            }
            return n;
        });
        Msg.Show(Loc.F("{0} von {1} Dateien erstellt.", "{0} of {1} files created.", done, jobs.Count) + (errors.Count > 0 ? "\n\n" + string.Join("\n", errors) : ""));
        if (done > 0) Shell.Reveal(d.Folder);
    }
}
