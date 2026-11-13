using System.Windows;
using System.Windows.Controls;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Übersicht der Tastenkürzel während der Aufnahme (aus <see cref="HotkeyActions"/> und den Einstellungen) –
/// für die Startseite „Hilfe“ und die Aufnahmeleiste. Die Editor-Kürzel zeigt <see cref="ShortcutsWindow"/> (F1)
/// aus der Befehlstabelle des Editors.
/// </summary>
static class Shortcuts
{
    public sealed record Group(string Title, List<(string label, string keys)> Items);

    public static List<Group> Recording()
    {
        AppSettings s;
        try { s = AppSettings.Load(); } catch { s = new AppSettings(); }
        var items = HotkeyActions.All.Select(a => (HotkeyActions.Label(a), Loc.Keys(s.Hotkey(a.Id)) is { Length: > 0 } k ? k : Loc.L("nicht belegt", "not assigned"))).ToList();
        return new() { new(Loc.L("Während der Aufnahme (überall)", "While recording (everywhere)"), items) };
    }

    /// <summary>Kompakte Tastenkappe mit dem ganzen Kürzel – überall dieselbe (Hilfe, F1, Aufnahmeleiste).</summary>
    public static Border Keycap(string keys) => new()
    {
        Background = Theme.Field, BorderBrush = Theme.CardLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RSmall), Padding = new Thickness(6, 1, 6, 2),
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
        Child = new TextBlock { Text = keys, FontSize = Theme.FsCaption, Foreground = Theme.Fg }
    };

    /// <summary>Übersicht als Oberfläche: Gruppen in Spalten, je Zeile Befehl und Tastenkappe.</summary>
    public static FrameworkElement View(IEnumerable<Group> groups, int columns = 2)
    {
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = columns };
        foreach (var g in groups)
        {
            var col = new StackPanel { Margin = new Thickness(0, 0, 28, 20) };
            col.Children.Add(U.Text(g.Title, Theme.FsBody, true, margin: new Thickness(0, 0, 0, 6)));
            var rows = new Grid();
            rows.ColumnDefinitions.Add(new ColumnDefinition());
            rows.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int r = 0;
            foreach (var (label, keys) in g.Items)
            {
                rows.RowDefinitions.Add(new RowDefinition());
                var l = U.Text(label, Theme.FsHint, fg: Theme.Fg2, margin: new Thickness(0, 3, 12, 3)); l.VerticalAlignment = VerticalAlignment.Center; l.TextWrapping = TextWrapping.Wrap;
                var k = Keycap(keys); k.Margin = new Thickness(0, 2, 0, 2);
                Grid.SetRow(l, r); Grid.SetRow(k, r); Grid.SetColumn(k, 1);
                rows.Children.Add(l); rows.Children.Add(k); r++;
            }
            col.Children.Add(rows);
            grid.Children.Add(col);
        }
        return grid;
    }

    /// <summary>Eigenes Fenster mit der Übersicht (Aufnahmeleiste, Infobereich).</summary>
    public static void ShowWindow(Window? owner, IEnumerable<Group> groups, string title)
    {
        var w = new DialogWindow { Title = title, Width = 620, SizeToContent = SizeToContent.Height, WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = owner, Topmost = owner == null };
        Theme.Prepare(w, Theme.Surface);
        var list = groups.ToList();
        var body = U.Col(View(list, 1), U.Hint(Loc.L("Alle Befehle der Aufnahme gibt es auch per Rechtsklick auf das Symbol im Infobereich. Ändern: Einstellungen → Tastenkürzel.", "All recording commands are also available by right-clicking the icon in the notification area. Change: Settings → Shortcuts.")));
        U.Dialog(w, title, body, U.Primary(Loc.L("Schließen", "Close"), w.Close));
        w.ShowDialog();
    }
}
