using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Kapitelvorschläge prüfen (13, Paket 37): je Vorschlag ankreuzen und den Titel ändern.</summary>
sealed class ChapterSuggestWindow : DialogWindow
{
    readonly List<(CheckBox use, TextBox title, ChapterSuggestion s)> _rows = new();
    public List<ChapterSuggestion> Chosen => _rows.Where(r => r.use.IsChecked == true && r.title.Text.Trim().Length > 0).Select(r => r.s with { Title = r.title.Text.Trim() }).ToList();

    public ChapterSuggestWindow(List<ChapterSuggestion> list, Func<long, string> stepLabel)
    {
        Title = AppInfo.DisplayName; Width = 620; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        var p = new StackPanel();
        p.Children.Add(U.Hint(Loc.L("Vorgeschlagen ist ein Kapitel bei jedem Wechsel des Programms. Titel lassen sich ändern.", "A chapter is suggested at every change of program. Titles can be changed."), new Thickness(0, 0, 0, 10)));
        foreach (var s in list)
        {
            var use = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var title = new TextBox { Text = s.Title, MinWidth = 260 };
            var meta = U.Text(Loc.F("ab {0} · {1} Schritte", "from {0} · {1} steps", stepLabel(s.Anchor), s.Steps), Theme.FsHint, false, Theme.Muted);
            meta.VerticalAlignment = VerticalAlignment.Center; meta.Margin = new Thickness(10, 0, 0, 0);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(use, Dock.Left); DockPanel.SetDock(meta, Dock.Right);
            row.Children.Add(use); row.Children.Add(meta); row.Children.Add(title);
            p.Children.Add(row);
            _rows.Add((use, title, s));
        }
        U.Dialog(this, Loc.L("Kapitel vorschlagen", "Suggest chapters"), p, U.Primary(Loc.L("Übernehmen", "Apply"), () => DialogResult = true), U.Btn(Loc.L("Abbrechen", "Cancel"), () => DialogResult = false));
    }
}
