using System.Windows;
using System.Windows.Controls;
using StepRecorder.Export;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Seite und Layout im Dokumentbereich (13, Paket 27): Layoutvorlage, Rand, Kopf- und Fußzeile mit Feldern, Umbrüche.</summary>
sealed partial class ViewerWindow
{
    void PageLayoutFields(StackPanel page, DocSettings d)
    {
        bool ro = _view.Session.ReadOnly;
        // Layoutvorlage
        var presets = new ComboBox { MinWidth = 180, IsEnabled = !ro };
        presets.Items.Add(Loc.L("Vorlage wählen …", "Choose template …"));
        foreach (var (_, name, tip) in DocLayouts.All) presets.Items.Add(new ComboBoxItem { Content = name, ToolTip = tip });
        presets.SelectedIndex = 0;
        presets.SelectionChanged += (_, _) =>
        {
            int i = presets.SelectedIndex - 1;
            if (i < 0) return;
            string id = DocLayouts.All[i].id;
            DocChange(x => DocLayouts.Apply(x, id, _view.Language), quiet: false);
            _status.Text = Loc.F("Layoutvorlage „{0}“ angewendet – rückgängig mit Strg+Z", "Layout template “{0}” applied – undo with Ctrl+Z", DocLayouts.All[i].name);
            Dispatcher.BeginInvoke(ShowCover);
        };
        page.Children.Add(U.Field(Loc.L("Layoutvorlage", "Layout template"), presets, Loc.L("Setzt Rand, Bilder, Umbrüche, Kopf- und Fußzeile – Inhalte bleiben unverändert.", "Sets margins, images, breaks, header and footer – contents stay unchanged.")));

        // Rand
        int[] mm = { 0, 12, 15, 20, 25, 30 };
        var margin = new ComboBox { MinWidth = 140, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !ro };
        foreach (var m in mm) margin.Items.Add(m == 0 ? Loc.L("Standard (2 cm)", "Default (2 cm)") : $"{m} mm");
        margin.SelectedIndex = Math.Max(0, Array.IndexOf(mm, d.MarginMm));
        margin.SelectionChanged += (_, _) => { int v = mm[Math.Max(0, margin.SelectedIndex)]; DocChange(x => x.MarginMm = v); };
        page.Children.Add(U.Field(Loc.L("Rand", "Margin"), margin));

        // Kopf- und Fußzeile
        Grid Band(string l, string c, string r, Action<DocSettings, string>[] set)
        {
            var g = new Grid();
            for (int i = 0; i < 3; i++) g.ColumnDefinitions.Add(new ColumnDefinition());
            string[] vals = { l, c, r };
            string[] tips = { Loc.L("links", "left"), Loc.L("Mitte", "centre"), Loc.L("rechts", "right") };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var tb = CommitBox(vals[i], v => DocChange(x => set[k](x, v)), tips[i]);
                tb.Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0);
                Grid.SetColumn(tb, i); g.Children.Add(tb);
            }
            return g;
        }
        page.Children.Add(U.Field(Loc.L("Kopfzeile", "Header"), Band(d.HeaderLeft, d.HeaderCenter, d.HeaderRight, new Action<DocSettings, string>[] { (x, v) => x.HeaderLeft = v, (x, v) => x.HeaderCenter = v, (x, v) => x.HeaderRight = v })));
        page.Children.Add(U.Field(Loc.L("Fußzeile mit Feldern", "Footer with fields"), Band(d.FooterLeft, d.FooterCenter, d.FooterRight, new Action<DocSettings, string>[] { (x, v) => x.FooterLeft = v, (x, v) => x.FooterCenter = v, (x, v) => x.FooterRight = v }),
            Loc.L("Felder: {{Seite}}, {{Seiten}}, {{Kapitel}} und alle Variablen wie {{Titel}}, {{Version}}, {{Datum}}. Leer = bisherige Fußzeile.", "Fields: {{Page}}, {{Pages}}, {{Chapter}} and all variables such as {{Title}}, {{Version}}, {{Date}}. Empty = previous footer.")));

        // Umbrüche
        CheckBox Check(string text, bool on, Action<bool> set, string tip)
        {
            var c = new CheckBox { Content = text, IsChecked = on, IsEnabled = !ro, Margin = new Thickness(0, 0, 0, 6), ToolTip = tip };
            c.Checked += (_, _) => set(true); c.Unchecked += (_, _) => set(false);
            return c;
        }
        page.Children.Add(Check(Loc.L("Jedes Kapitel auf einer neuen Seite", "Each chapter on a new page"), d.ChapterNewPage, v => DocChange(x => x.ChapterNewPage = v), Loc.L("PDF, Word und Druck", "PDF, Word and print")));
        page.Children.Add(Check(Loc.L("Ein Schritt je Seite", "One step per page"), d.StepPerPage, v => DocChange(x => x.StepPerPage = v), Loc.L("Für Schulungsunterlagen", "For training material")));
        page.Children.Add(Check(Loc.L("„Erstellt mit …“ zeigen", "Show “Created with …”"), d.CreatedWith, v => DocChange(x => x.CreatedWith = v), Loc.L("Hinweis auf das Programm in der HTML-Fußzeile", "Note about the program in the HTML footer")));
    }
}
