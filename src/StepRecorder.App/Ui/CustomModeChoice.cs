using System.Windows;
using System.Windows.Controls;
using StepRecorder.Capture;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Inhalt „Modus Eigene“: Eingabearten, Zusatzbilder, Programmkontext, nach dem Stopp. Eine Quelle für den Dialog
/// „Eigener Detailgrad“ (speichert sofort) und den Einstellungsreiter (übernimmt erst mit „Speichern“).
/// </summary>
static class CustomModeChoice
{
    /// <summary>Arbeitsstand der vier Angaben; die Bedienelemente schreiben hierhin, gespeichert wird beim Aufrufer.</summary>
    public sealed class Draft
    {
        public Dictionary<string, bool> Record = new();
        public string ChangeFrames = "normal", AfterStop = "viewer";
        public bool AppContext;

        public static Draft From(AppSettings s) => new() { Record = new Dictionary<string, bool>(s.Record), ChangeFrames = s.ChangeFrames, AppContext = s.CustomAppContext, AfterStop = s.CustomAfterStop };

        public void ApplyTo(AppSettings s) { s.Record = new Dictionary<string, bool>(Record); s.ChangeFrames = ChangeFrames; s.CustomAppContext = AppContext; s.CustomAfterStop = AfterStop; }
    }

    static readonly (string id, string name, string result)[] AfterStops =
    {
        ("viewer", Loc.L("Editor öffnen", "Open editor"), Loc.L("Anleitung im Editor – Export in jedes Format", "Guide in the editor – export to any format")),
        ("export", Loc.L("KI-Paket exportieren", "Export AI package"), Loc.L("KI-Paket: Protokoll, Bilder und Lesehilfe", "AI package: log, images and reading guide")),
        ("supportPackage", Loc.L("Support-Paket (ZIP) erstellen", "Create support package (ZIP)"), Loc.L("ZIP-Paket: Anleitung (HTML) + KI-Paket", "ZIP package: guide (HTML) + AI package")),
        ("folder", Loc.L("Aufnahmeordner öffnen", "Open recording folder"), Loc.L("Aufnahme im Ordner – später im Editor öffnen", "Recording in its folder – open it in the editor later")),
    };

    /// <summary>Baut die vier Angaben (ohne Hinweis oben); jede Änderung wird in <paramref name="d"/> geschrieben und <paramref name="changed"/> gemeldet.</summary>
    public static FrameworkElement Build(Draft d, Action? changed)
    {
        var col = new StackPanel();
        col.Children.Add(RecordChoice.Build(d.Record, changed));
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(24) : new GridLength(1, GridUnitType.Star) });
        int cf = d.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 };
        var (cfTrack, cfs) = U.Segmented("cfr", cf, (null, Loc.L("Nur große", "Large only")), (null, Loc.L("Auch kleine", "Small too")), (null, Loc.L("Jede Änderung", "Every change")));
        var cfHint = U.Hint(DetailPresets.ChangeFramesRule(d.ChangeFrames), new Thickness(0, 8, 0, 0));
        for (int i = 0; i < 3; i++) { string v = new[] { "few", "normal", "many" }[i]; cfs[i].Checked += (_, _) => { cfHint.Text = DetailPresets.ChangeFramesRule(v); d.ChangeFrames = v; changed?.Invoke(); }; }
        var left = U.Field(Loc.L("Zusatzbilder ohne Eingabe", "Extra images without input"), U.Col(cfTrack, cfHint));
        var ctx = new CheckBox { Content = Loc.L("Erweiterter Programmkontext", "Extended program context"), IsChecked = d.AppContext, Margin = new Thickness(0, 4, 0, 0), ToolTip = Loc.L("Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument und Statusleiste – nötig für gute AutoHotkey-Skripte", "Menu paths, lists, Explorer folder, browser address, Office document and status bar – needed for good AutoHotkey scripts") };
        void Ctx() { d.AppContext = ctx.IsChecked == true; changed?.Invoke(); }
        ctx.Checked += (_, _) => Ctx(); ctx.Unchecked += (_, _) => Ctx();
        var after = new ComboBox { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var a in AfterStops) after.Items.Add(a.name);
        after.SelectedIndex = Math.Max(0, Array.FindIndex(AfterStops, a => a.id == d.AfterStop));
        after.SelectionChanged += (_, _) => { d.AfterStop = AfterStops[Math.Max(0, after.SelectedIndex)].id; changed?.Invoke(); };
        var right = U.Col(U.Field(Loc.L("Programmkontext", "Program context"), ctx), U.Field(Loc.L("Nach dem Stopp", "After stopping"), after, margin: new Thickness(0)));
        Grid.SetColumn(left, 0); Grid.SetColumn(right, 2);
        grid.Children.Add(left); grid.Children.Add(right);
        col.Children.Add(grid);
        return col;
    }
}
