using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Übersetzungsübersicht (13, Paket 33): alle eigenen Texte Deutsch | Englisch nebeneinander, direkt bearbeitbar, Filter „nur
/// fehlende“, Austausch als CSV (Export verlangt die abgeschlossene Durchsicht).
/// </summary>
sealed class TranslationWindow : DialogWindow
{
    public sealed class RowVm
    {
        public string Id { get; init; } = "";
        public string Location { get; init; } = "";
        public string De { get; set; } = "";
        public string En { get; set; } = "";
        public string Status { get; set; } = "";
        public long? Anchor { get; init; }
        public string OrigDe = "", OrigEn = "";
    }

    readonly SessionView _view;
    readonly Action<Action<System.Text.Json.Nodes.JsonObject>> _change;
    readonly ObservableCollection<RowVm> _rows = new();
    readonly DataGrid _grid = new() { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, MinHeight = 380, MaxHeight = 620 };
    readonly CheckBox _missing = new() { Content = Loc.L("Nur fehlende Übersetzungen", "Missing translations only"), Margin = new Thickness(0, 0, 0, 8) };

    public TranslationWindow(SessionView view, Action<Action<System.Text.Json.Nodes.JsonObject>> change)
    {
        _view = view; _change = change;
        Title = Loc.L("Übersetzungen", "Translations"); Width = 1100; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Prepare(this, Theme.Surface);
        _grid.Columns.Add(new DataGridTextColumn { Header = Loc.L("Ort", "Location"), Binding = new Binding(nameof(RowVm.Location)), IsReadOnly = true, Width = new DataGridLength(200) });
        _grid.Columns.Add(Edit("Deutsch", nameof(RowVm.De)));
        _grid.Columns.Add(Edit("English", nameof(RowVm.En)));
        _grid.Columns.Add(new DataGridTextColumn { Header = Loc.L("Stand", "Status"), Binding = new Binding(nameof(RowVm.Status)), IsReadOnly = true, Width = new DataGridLength(110) });
        _grid.ItemsSource = _rows;
        _grid.CellEditEnding += (_, e) => { if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is RowVm r) Dispatcher.BeginInvoke(() => Save(r)); };
        _missing.Checked += (_, _) => Reload(); _missing.Unchecked += (_, _) => Reload();
        _grid.IsReadOnly = view.Session.ReadOnly;
        Reload();
        var body = new StackPanel { Children = { U.Hint(Loc.L("Leere Zellen erscheinen in dieser Sprache als automatischer Text bzw. Text der anderen Sprache. Geschwärzte Stellen zeigen den Platzhalter und werden beim CSV-Import nicht überschrieben.", "Empty cells appear in that language as automatic text or the other language's text. Redacted places show the placeholder and are not overwritten by a CSV import."), new Thickness(0, 0, 0, 8)), _missing, _grid } };
        U.Dialog(this, Loc.L("Übersetzungen", "Translations"), body,
            U.Btn(Theme.IExport, Loc.L("CSV exportieren …", "Export CSV …"), ExportCsv),
            U.Btn(Theme.IOpen, Loc.L("CSV importieren …", "Import CSV …"), ImportCsv),
            U.Primary(Loc.L("Schließen", "Close"), () => Close()));
    }

    static DataGridTextColumn Edit(string header, string path)
    {
        var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        var edit = new Style(typeof(TextBox)); edit.Setters.Add(new Setter(TextBox.TextWrappingProperty, TextWrapping.Wrap)); edit.Setters.Add(new Setter(TextBox.AcceptsReturnProperty, true));
        return new DataGridTextColumn { Header = header, Binding = new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(1, DataGridLengthUnitType.Star), ElementStyle = style, EditingElementStyle = edit };
    }

    void Reload()
    {
        _rows.Clear();
        foreach (var r in Translations.Rows(_view))
        {
            if (_missing.IsChecked == true && r.Status is not ("missingEn" or "missingDe")) continue;
            _rows.Add(new RowVm { Id = r.Id, Location = r.Location, De = r.De, En = r.En, OrigDe = r.De, OrigEn = r.En, Anchor = r.Anchor, Status = StatusText(r.Status) });
        }
    }

    static string StatusText(string s) => s switch
    {
        "missingEn" => Loc.L("Englisch fehlt", "English missing"), "missingDe" => Loc.L("Deutsch fehlt", "German missing"),
        "same" => Loc.L("gleich", "identical"), "empty" => Loc.L("automatisch", "automatic"), _ => "✓"
    };

    void Save(RowVm r)
    {
        bool de = r.De != r.OrigDe, en = r.En != r.OrigEn;
        if (!de && !en) return;
        _change(e => { if (de) Translations.Apply(e, r.Id, "de", r.De); if (en) Translations.Apply(e, r.Id, "en", r.En); });
        r.OrigDe = r.De; r.OrigEn = r.En;
    }

    void ExportCsv()
    {
        if (_view.UnreviewedCount > 0) { Msg.Show(Loc.L("Bitte zuerst die auffälligen Eingaben durchsehen – die CSV ist eine Ausgabe.", "Please review the flagged inputs first – the CSV is an output.")); return; }
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv", FileName = (_view.Title.Length > 0 ? _view.Title : "Uebersetzung") + ".csv" };
        if (d.ShowDialog(this) != true) return;
        File.WriteAllText(d.FileName, Translations.ToCsv(Translations.Rows(_view)), new System.Text.UTF8Encoding(false));
        Msg.Show(Loc.L("CSV gespeichert. Spalten: id; kind; location; de; en; status – nur „de“ und „en“ ändern.", "CSV saved. Columns: id; kind; location; de; en; status – only change “de” and “en”."));
    }

    void ImportCsv()
    {
        if (_view.Session.ReadOnly) return;
        var d = new Microsoft.Win32.OpenFileDialog { Filter = "CSV|*.csv;*.txt" };
        if (d.ShowDialog(this) != true) return;
        string csv = File.ReadAllText(d.FileName);
        (int applied, int skipped) res = (0, 0);
        _change(e => res = Translations.Import(_view, csv));
        Reload();
        Msg.Show(Loc.F("{0} Zellen übernommen, {1} übersprungen.", "{0} cells applied, {1} skipped.", res.applied, res.skipped));
    }
}
