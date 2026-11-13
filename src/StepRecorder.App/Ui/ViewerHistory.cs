using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Bearbeitungsstände: frühere Stände des Overlays ansehen und wiederherstellen (ein Schritt, Strg+Z macht ihn rückgängig).</summary>
sealed partial class ViewerWindow
{
    void ShowHistory()
    {
        if (_view.Session.ReadOnly) { Msg.Show(Loc.L("Ein .steprec-Archiv hat keine Bearbeitungsstände.", "A .steprec archive has no edit history.")); return; }
        var list = EditHistory.List(_path);
        var w = new DialogWindow { Title = Loc.L("Bearbeitungsstände", "Edit history"), Width = 640, Height = Math.Min(620, Theme.ActiveWorkArea().Height - 80), ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var keep = new CheckBox { Content = Loc.L("Schwärzungen, Verallgemeinerungen und Durchsicht wie jetzt behalten", "Keep redactions, generalizations and review as they are now"), IsChecked = true, Margin = new Thickness(0, 10, 0, 0) };
        var items = new StackPanel();
        if (list.Count == 0)
            items.Children.Add(U.Text(Loc.L("Noch keine Stände. Gesichert wird beim ersten Speichern nach dem Öffnen, danach höchstens alle 10 Minuten und vor großen Änderungen wie „Alle ersetzen“.",
                "No snapshots yet. A snapshot is taken on the first save after opening, then at most every 10 minutes, and before large changes such as \"Replace all\"."), 13, fg: Theme.Muted));
        foreach (var s in list)
        {
            var snap = EditHistory.Load(s);
            if (snap == null) continue;
            var diff = EditHistory.Diff(_view.Edits, snap);
            var head = new TextBlock { Text = Loc.Relative(s.Utc.ToLocalTime()) + "  ·  " + ReasonLabel(s.Reason) + "  ·  r" + s.Revision, FontFamily = Theme.Mono, FontSize = 11.5, Foreground = Theme.Muted };
            var sum = U.Text(EditHistory.Summarize(snap), 13, fg: Theme.Fg); sum.TextWrapping = TextWrapping.Wrap;
            var d = U.Text(diff.Count == 0 ? Loc.L("entspricht dem jetzigen Stand", "same as now") : Loc.L("gegenüber jetzt: ", "compared to now: ") + string.Join(" · ", diff), 12, fg: Theme.Fg2); d.TextWrapping = TextWrapping.Wrap;
            var restore = U.Btn(Theme.IUndo, Loc.L("Wiederherstellen", "Restore"), () =>
            {
                RestoreSnapshot(snap, keep.IsChecked == true);
                w.Close();
            }, null, "Ghost");
            restore.IsEnabled = diff.Count > 0;
            var row = new DockPanel();
            DockPanel.SetDock(restore, Dock.Right); row.Children.Add(restore);
            row.Children.Add(U.Col(head, sum, d));
            items.Children.Add(new Border { Background = Theme.Surface2, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 8, 8), Margin = new Thickness(0, 0, 0, 6), Child = row });
        }
        var body = new DockPanel();
        DockPanel.SetDock(keep, Dock.Bottom); body.Children.Add(keep);
        body.Children.Add(new ScrollViewer { Content = items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = Math.Max(200, w.Height - 230) });
        U.Dialog(w, Loc.L("Bearbeitungsstände", "Edit history"), body, U.Btn(Loc.L("Schließen", "Close"), () => w.Close()));
        w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
        w.ShowDialog();
    }

    static string ReasonLabel(string r) => r switch
    {
        "open" => Loc.L("beim Öffnen", "on opening"), "auto" => Loc.L("automatisch", "automatic"), "replaceAll" => Loc.L("vor „Alle ersetzen“", "before \"Replace all\""),
        "cropAll" => Loc.L("vor „Zuschnitt auf alle“", "before \"Crop all\""), "template" => Loc.L("vor „Vorlage anwenden“", "before \"Apply template\""), "restore" => Loc.L("vor dem Wiederherstellen", "before restoring"), _ => r
    };

    /// <summary>Ein Stand wird wie jede Bearbeitung übernommen: ein Rückgängig-Schritt, die Revision steigt weiter.</summary>
    void RestoreSnapshot(JsonObject snapshot, bool keepProtection)
    {
        var restored = EditHistory.Restore(_view.Edits, snapshot, keepProtection);
        _view.SnapshotBefore = "restore";
        Change(e =>
        {
            foreach (var k in e.Select(x => x.Key).ToList()) e.Remove(k);
            foreach (var kv in restored) e[kv.Key] = kv.Value?.DeepClone();
        });
        _titleBlock.Text = _view.Title;
        _thumbs.Clear(); InvalidateAllThumbs();
        _status.Text = Loc.F("Bearbeitungsstand wiederhergestellt – rückgängig mit {0}.", "Edit snapshot restored – undo with {0}.", Cmd("undo").Gesture);
    }
}
