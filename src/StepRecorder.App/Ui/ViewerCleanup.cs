using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Aufräumen: überflüssige Schritte nach der Aufnahme ausblenden (nichts wird gelöscht, alles rückgängig zu machen).</summary>
sealed partial class ViewerWindow
{
    /// <summary>Beim ersten Öffnen einer Aufnahme einmal automatisch aufräumen (Einstellung „Automatisch aufräumen“).</summary>
    void AutoCleanupOnOpen()
    {
        if (_view.Session.ReadOnly || _view.Edits["cleanup"] != null || !AppSettings.Load().AutoCleanup) return;
        var sug = StepCleanup.Suggest(_view);
        if (sug.Count == 0)
        {
            _view.Edits["cleanup"] = new JsonObject { ["auto"] = true, ["count"] = 0, ["utc"] = Json.UtcNow() };
            _view.SaveEdits();
            return;
        }
        Change(e => { StepCleanup.Apply(e, sug); e["cleanup"] = new JsonObject { ["auto"] = true, ["count"] = sug.Count, ["utc"] = Json.UtcNow() }; });
        _status.Text = sug.Count == 1 ? "1 überflüssiger Schritt wurde ausgeblendet – rückgängig mit Strg+Z, Details unter ⋯ › Aufräumen"
                                       : $"{sug.Count} überflüssige Schritte wurden ausgeblendet – rückgängig mit Strg+Z, Details unter ⋯ › Aufräumen";
    }

    void ShowCleanup() => new CleanupWindow(this, _view) { Owner = this }.ShowDialog();

    internal void ApplyCleanup(List<CleanupSuggestion> hide, List<ViewStep> restore)
    {
        if (hide.Count == 0 && restore.Count == 0) return;
        Change(e =>
        {
            StepCleanup.Apply(e, hide);
            foreach (var s in restore) { var se = StepEdit(e, s.Anchor); se["hidden"] = false; se.Remove("cleanup"); }
            e["cleanup"] ??= new JsonObject { ["auto"] = false, ["utc"] = Json.UtcNow() };
        });
        var parts = new List<string>();
        if (hide.Count > 0) parts.Add($"{hide.Count} ausgeblendet");
        if (restore.Count > 0) parts.Add($"{restore.Count} wieder eingeblendet");
        _status.Text = "Aufgeräumt: " + string.Join(", ", parts) + " – rückgängig mit Strg+Z";
    }
}

/// <summary>Dialog „Aufräumen“: Vorschläge prüfen und übernehmen, aufgeräumte Schritte wieder einblenden.</summary>
sealed class CleanupWindow : Window
{
    readonly ViewerWindow _v;
    readonly List<(CheckBox cb, CleanupSuggestion s)> _sug = new();
    readonly List<(CheckBox cb, ViewStep s)> _done = new();

    public CleanupWindow(ViewerWindow v, SessionView view)
    {
        _v = v;
        Title = "Aufräumen"; Width = 660; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Prepare(this, Theme.Surface);
        var list = new StackPanel();
        var suggestions = StepCleanup.Suggest(view);
        var cleaned = view.All.Where(s => s.Hidden && s.CleanupReason != null).ToList();

        list.Children.Add(U.SectionLabel(suggestions.Count == 0 ? "Keine weiteren Vorschläge" : $"Vorschläge ({suggestions.Count})", new Thickness(0, 0, 0, 10)));
        if (suggestions.Count == 0) list.Children.Add(U.Text("Alle sichtbaren Schritte wirken nötig.", Theme.FsBody, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 8)));
        foreach (var s in suggestions) { var cb = Row(list, s.Step, s.Reason, true); _sug.Add((cb, s)); }

        if (cleaned.Count > 0)
        {
            list.Children.Add(U.SectionLabel($"Bereits aufgeräumt ({cleaned.Count}) – Haken setzen, um wieder einzublenden", new Thickness(0, 20, 0, 10)));
            foreach (var s in cleaned) { var cb = Row(list, s, s.CleanupReason!, false); _done.Add((cb, s)); }
        }

        var rules = U.Text("Regeln: " + string.Join(" · ", StepCleanup.Rules.Select(r => r.name)) + ". Ausgeblendete Schritte bleiben in der Aufnahme erhalten und fehlen nur in Anleitung und Export.", Theme.FsHint, fg: Theme.Muted, margin: new Thickness(0, 14, 0, 0));
        U.Spaced(rules);
        var auto = new CheckBox { Content = "Neue Aufnahmen beim ersten Öffnen automatisch aufräumen", IsChecked = AppSettings.Load().AutoCleanup, Margin = new Thickness(0, 12, 0, 0) };
        auto.Checked += (_, _) => SaveAuto(true); auto.Unchecked += (_, _) => SaveAuto(false);
        var body = U.Col(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 380 }, rules, auto);
        var go = U.Primary("Übernehmen", Apply);
        U.Dialog(this, "Aufräumen", body, go, U.Btn("Schließen", Close));
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    static void SaveAuto(bool on) { var st = AppSettings.Load(); st.AutoCleanup = on; st.Save(); }

    static CheckBox Row(Panel host, ViewStep s, string reason, bool check)
    {
        var cb = new CheckBox { IsChecked = check, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 10, 0) };
        AutomationName(cb, $"Schritt {s.Number}: {s.Title}");
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 8) };
        DockPanel.SetDock(cb, Dock.Left); row.Children.Add(cb);
        var label = new TextBlock { FontSize = Theme.FsHint, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
        label.Inlines.Add(new System.Windows.Documents.Run($"Schritt {s.Number}  ") { FontFamily = Theme.Mono, Foreground = Theme.Muted });
        label.Inlines.Add(new System.Windows.Documents.Run(s.Title) { FontWeight = FontWeights.SemiBold });
        label.Inlines.Add(new System.Windows.Documents.LineBreak());
        label.Inlines.Add(new System.Windows.Documents.Run(reason) { Foreground = Theme.Muted });
        label.MouseLeftButtonUp += (_, _) => cb.IsChecked = cb.IsChecked != true;
        row.Children.Add(label);
        host.Children.Add(row);
        return cb;
    }

    static void AutomationName(DependencyObject d, string name) => System.Windows.Automation.AutomationProperties.SetName(d, name);

    void Apply()
    {
        _v.ApplyCleanup(_sug.Where(x => x.cb.IsChecked == true).Select(x => x.s).ToList(), _done.Where(x => x.cb.IsChecked == true).Select(x => x.s).ToList());
        Close();
    }
}
