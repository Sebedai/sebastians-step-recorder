using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Befehlspalette des Editors (Strg+Umschalt+P; 08 §12, Paket 15): findet jeden Befehl der Befehlstabelle über Namen und
/// Gruppe, zuletzt benutzte zuerst, mit Kürzel daneben. Pfeiltasten wählen, Eingabe führt aus, Esc schließt.
/// </summary>
static class CommandPalette
{
    /// <summary>Zuletzt ausgeführte Befehle (für diese Programmsitzung).</summary>
    static readonly List<string> Recent = new();

    public static void Show(Window owner, IReadOnlyList<ViewerCmd> commands)
    {
        var groups = ViewerCommands.Groups().ToDictionary(g => g.id, g => g.label);
        var all = commands.Where(c => c.Listed && c.Id != "palette").GroupBy(c => c.Id).Select(g => g.First()).ToList();
        var w = new Window
        {
            Title = Loc.L("Befehlspalette", "Command palette"), Width = 560, Height = 440, WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        Theme.Prepare(w, Theme.Surface);
        var search = new TextBox { Padding = new Thickness(10, 7, 8, 7), Margin = new Thickness(14, 14, 14, 8), Tag = Loc.L("Befehl suchen …", "Search command …") };
        System.Windows.Automation.AutomationProperties.SetName(search, Loc.L("Befehl suchen", "Search command"));
        var list = new ListBox { Margin = new Thickness(14, 0, 14, 14), BorderThickness = new Thickness(0), Background = Theme.Surface };
        void Fill()
        {
            list.Items.Clear();
            var q = StepRecorder.ReadLayer.SearchQuery.Parse(search.Text);
            var hits = all.Where(c => q.Matches(c.Label + " " + (groups.GetValueOrDefault(c.Group) ?? "") + " " + c.Id))
                          .OrderBy(c => Recent.IndexOf(c.Id) is int i && i >= 0 ? i : int.MaxValue).ThenBy(c => c.Label, StringComparer.CurrentCultureIgnoreCase);
            foreach (var c in hits)
            {
                var row = new DockPanel { Margin = new Thickness(4, 3, 4, 3) };
                if (c.Gesture.Length > 0)
                {
                    var k = new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 1, 8, 2), Child = new TextBlock { Text = c.Gesture, FontFamily = Theme.Mono, FontSize = 12, Foreground = Theme.Fg } };
                    DockPanel.SetDock(k, Dock.Right); row.Children.Add(k);
                }
                row.Children.Add(new TextBlock { Text = c.Label, FontSize = 13, Foreground = Theme.Fg, VerticalAlignment = VerticalAlignment.Center });
                var sub = new TextBlock { Text = groups.GetValueOrDefault(c.Group) ?? "", FontSize = 11, Foreground = Theme.Muted, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(sub, Dock.Right); row.Children.Insert(row.Children.Count - 1, sub);
                list.Items.Add(new ListBoxItem { Content = row, Tag = c });
            }
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }
        void RunSelected()
        {
            if (list.SelectedItem is not ListBoxItem { Tag: ViewerCmd c }) return;
            Recent.Remove(c.Id); Recent.Insert(0, c.Id);
            if (Recent.Count > 12) Recent.RemoveAt(Recent.Count - 1);
            w.Close();
            owner.Dispatcher.BeginInvoke(() => { if (c.When == null || c.When()) try { c.Run(); } catch (Exception ex) { UiApp.ShowError(ex); } });
        }
        search.TextChanged += (_, _) => Fill();
        search.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && list.SelectedIndex < list.Items.Count - 1) { list.SelectedIndex++; list.ScrollIntoView(list.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Up && list.SelectedIndex > 0) { list.SelectedIndex--; list.ScrollIntoView(list.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Enter) { RunSelected(); e.Handled = true; }
        };
        list.MouseDoubleClick += (_, _) => RunSelected();
        list.KeyDown += (_, e) => { if (e.Key == Key.Enter) { RunSelected(); e.Handled = true; } };
        w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
        var body = new DockPanel();
        DockPanel.SetDock(search, Dock.Top); body.Children.Add(search); body.Children.Add(list);
        w.Content = body;
        Fill();
        w.Loaded += (_, _) => search.Focus();
        w.ShowDialog();
    }
}
