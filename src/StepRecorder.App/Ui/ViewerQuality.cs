using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Qualitätsprüfung im Editor: Befunde (zwischengespeichert bis zur nächsten Änderung), Sprung, Fenster.</summary>
sealed partial class ViewerWindow
{
    List<QualityFinding>? _findings;
    QualityWindow? _qualityWindow;
    int _findingPos = -1;

    /// <summary>Aktuelle Befunde (ohne ignorierte); werden bei jeder Änderung neu berechnet.</summary>
    List<QualityFinding> Findings()
    {
        if (_findings != null) return _findings;
        try { _findings = QualityCheck.Run(_view); }
        catch (Exception ex) { AppLog.Write("Qualitätsprüfung: " + ex.Message); _findings = new(); }
        return _findings;
    }

    /// <summary>Nach jeder Overlay-Änderung: Befunde verwerfen, offenes Fenster und Statuslink aktualisieren.</summary>
    void QualityInvalidate()
    {
        _findings = null;
        _qualityWindow?.Refresh();
        UpdateQualityLink();
    }

    string QualitySummary(List<QualityFinding> f)
    {
        int w = f.Count(x => x.Severity == "warning"), h = f.Count - w;
        if (f.Count == 0) return Loc.L("Qualität: keine Befunde", "Quality: no findings");
        return Loc.L("Qualität: ", "Quality: ") + string.Join(", ", new[] { w > 0 ? Loc.N(w, "1 Warnung", "{0} Warnungen", "1 warning", "{0} warnings") : null, h > 0 ? Loc.N(h, "1 Hinweis", "{0} Hinweise", "1 hint", "{0} hints") : null }.Where(x => x != null));
    }

    void UpdateQualityLink()
    {
        if (_qualityLink == null) return;
        var f = Findings();
        bool warn = f.Any(x => x.Severity == "warning");
        _qualityText.Text = QualitySummary(f);
        _qualityText.Foreground = _qualityIcon!.Foreground = warn ? Theme.Warn : Theme.Muted;
    }

    Button? _qualityLink;
    TextBlock? _qualityIcon;
    readonly TextBlock _qualityText = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };

    Button BuildQualityLink()
    {
        _qualityIcon = U.Icon(Theme.IChecklist, 12, Theme.Muted); _qualityIcon.VerticalAlignment = VerticalAlignment.Center;
        _qualityLink = new Button
        {
            Style = Theme.Style("Link"), Content = U.Row(_qualityIcon, _qualityText), Margin = new Thickness(0), Padding = new Thickness(4, 0, 0, 0),
            ToolTip = Loc.F("Qualitätsprüfung öffnen ({0}) · nächster Befund {1}", "Open quality check ({0}) · next finding {1}", Cmd("quality").Gesture, Cmd("nextFinding").Gesture)
        };
        _qualityLink.Click += (_, _) => ShowQuality();
        return _qualityLink;
    }

    void ShowQuality()
    {
        if (_qualityWindow == null || !_qualityWindow.IsLoaded)
        {
            _qualityWindow = new QualityWindow(this) { Owner = this };
            _qualityWindow.Closed += (_, _) => _qualityWindow = null;
            _qualityWindow.Show();
        }
        else _qualityWindow.Activate();
    }

    /// <summary>F4 / Umschalt+F4: zum nächsten bzw. vorherigen Befund mit Schritt.</summary>
    void NextFinding(int dir)
    {
        var f = Findings().Where(x => x.Anchor != null).ToList();
        if (f.Count == 0) { _status.Text = Loc.L("Keine Befunde der Qualitätsprüfung.", "No quality findings."); return; }
        _findingPos = ((_findingPos + dir) % f.Count + f.Count) % f.Count;
        GoToFinding(f[_findingPos]);
    }

    internal void GoToFinding(QualityFinding f)
    {
        if (f.Anchor is long a && !SelectAnchor(a)) { _search.Text = ""; _allToggle.IsChecked = true; Fill(); SelectAnchor(a); }
        _status.Text = f.Line(Loc.Lang);
    }

    /// <summary>Aktion eines Befunds: erst zum Schritt, dann das passende Werkzeug.</summary>
    internal void RunFindingAction(QualityFinding f)
    {
        GoToFinding(f);
        switch (f.Action)
        {
            case "review": ShowReview(); break;
            case "editText" or "translate": FocusInlineTitle(); break;
            case "hide": ToggleHidden(); break;
            case "adjustCrop": SelectTool("crop"); break;
            case "insertNote": InsertNote(); break;
            case "scanImages": ScanImagesForPersonalData(); break;
            case "replaceImage": if (Sel is { } s) ReplaceImageMenu(s); break;
            case "merge": _status.Text = Loc.F("Schritte auswählen und mit {0} zusammenfassen.", "Select steps and merge them with {0}.", Cmd("merge").Gesture); break;
        }
        Activate();
    }

    internal void SetFindingIgnored(QualityFinding f, bool ignored) => Change(e => QualityCheck.SetIgnored(e, f, ignored), refill: false);

    internal static string ActionLabel(string action) => action switch
    {
        "review" => Loc.L("Durchsicht …", "Review …"), "editText" => Loc.L("Text bearbeiten", "Edit text"), "translate" => Loc.L("Übersetzen", "Translate"),
        "hide" => Loc.L("Ausblenden", "Hide"), "adjustCrop" => Loc.L("Zuschneiden", "Crop"), "insertNote" => Loc.L("Hinweis einfügen", "Insert note"),
        "scanImages" => Loc.L("Bilder prüfen …", "Check images …"), "replaceImage" => Loc.L("Bild ersetzen …", "Replace image …"), "merge" => Loc.L("Zusammenfassen", "Merge"),
        _ => Loc.L("Zum Schritt", "Go to step")
    };

    internal SessionView View => _view;

    /// <summary>Für Entwickler-Schnappschüsse.</summary>
    internal void DevShowQuality() => ShowQuality();
}

/// <summary>Nicht modales Fenster der Qualitätsprüfung: Befunde mit Sprung, Aktion und „Ignorieren“.</summary>
sealed class QualityWindow : Window
{
    readonly ViewerWindow _owner;
    readonly StackPanel _list = new();
    readonly TextBlock _summary = U.Text("", 12.5, fg: Theme.Muted);
    readonly CheckBox _showIgnored = new() { Content = Loc.L("Ignorierte zeigen", "Show ignored"), VerticalAlignment = VerticalAlignment.Center };

    public QualityWindow(ViewerWindow owner)
    {
        _owner = owner;
        Title = Loc.L("Qualitätsprüfung", "Quality check");
        Width = 560; Height = Math.Min(680, Theme.ActiveWorkArea().Height - 80); ShowInTaskbar = false; ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Theme.Prepare(this, Theme.Surface);
        Loaded += (_, _) =>
        {
            // rechts neben den Editor, sonst rechts im Editor
            var area = Theme.ActiveWorkArea();
            Left = Math.Min(owner.Left + owner.ActualWidth - Width - 24, area.Right - Width); Top = owner.Top + 90;
        };
        var again = U.Btn(Theme.IRedo, Loc.L("Erneut prüfen", "Check again"), Refresh, null, "Ghost");
        _showIgnored.Checked += (_, _) => Refresh(); _showIgnored.Unchecked += (_, _) => Refresh();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var right = U.Row(_showIgnored, again); DockPanel.SetDock(right, Dock.Right); head.Children.Add(right);
        head.Children.Add(_summary); _summary.VerticalAlignment = VerticalAlignment.Center;
        var body = new DockPanel();
        DockPanel.SetDock(head, Dock.Top); body.Children.Add(head);
        body.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var root = new DockPanel { Margin = new Thickness(20, 16, 16, 16) };
        var h = U.Text(Loc.L("Qualitätsprüfung", "Quality check"), 17, true, margin: new Thickness(0, 0, 0, 6)); h.FontFamily = Theme.Display;
        DockPanel.SetDock(h, Dock.Top); root.Children.Add(h);
        root.Children.Add(body);
        Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Refresh();
    }

    public void Refresh()
    {
        _list.Children.Clear();
        List<QualityFinding> all;
        try { all = QualityCheck.Run(_owner.View, new QualityOptions { IncludeIgnored = _showIgnored.IsChecked == true }); }
        catch (Exception ex) { _summary.Text = ex.Message; return; }
        int w = all.Count(x => x.Severity == "warning" && !x.Ignored), hcount = all.Count(x => x.Severity != "warning" && !x.Ignored);
        _summary.Text = all.Count == 0 ? Loc.L("Keine Befunde – die Anleitung ist bereit.", "No findings – the guide is ready.")
            : Loc.N(w, "1 Warnung", "{0} Warnungen", "1 warning", "{0} warnings") + " · " + Loc.N(hcount, "1 Hinweis", "{0} Hinweise", "1 hint", "{0} hints");
        foreach (var f in all.OrderBy(x => x.Severity == "warning" ? 0 : 1).ThenBy(x => x.GuideNumber ?? int.MaxValue))
        {
            bool warn = f.Severity == "warning";
            var icon = U.Icon(warn ? Theme.IWarn : Theme.IInfo, 13, warn ? Theme.Warn : Theme.Muted); icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new Thickness(0, 2, 10, 0);
            var text = U.Text(f.Text(Loc.Lang), 13, fg: f.Ignored ? Theme.Muted : Theme.Fg); text.TextWrapping = TextWrapping.Wrap;
            var where = new TextBlock { Text = (f.GuideNumber is int n ? Loc.F("Schritt {0}", "Step {0}", n) + " · " : "") + f.Rule + (f.Ignored ? Loc.L(" · ignoriert", " · ignored") : ""), FontFamily = Theme.Mono, FontSize = 11, Foreground = Theme.Muted, Margin = new Thickness(0, 0, 0, 3) };
            var actions = new WrapPanel { Margin = new Thickness(-4, 4, 0, 0) };
            var go = U.Btn(null, ViewerWindow.ActionLabel(f.Action), () => _owner.RunFindingAction(f), null, "Link"); actions.Children.Add(go);
            var ign = U.Btn(null, f.Ignored ? Loc.L("Wieder berücksichtigen", "Consider again") : Loc.L("Ignorieren", "Ignore"), () => { _owner.SetFindingIgnored(f, !f.Ignored); }, f.Rule == "Q12" ? Loc.L("Ignorieren hebt die Durchsichtspflicht nicht auf.", "Ignoring does not lift the review requirement.") : null, "Link");
            actions.Children.Add(ign);
            var col = U.Col(where, text, actions);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
            DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon); row.Children.Add(col);
            var card = new Border { Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(6), Background = Theme.Surface2, Margin = new Thickness(0, 0, 0, 6), Child = row, Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is not TextBlock { Parent: StackPanel { Parent: Button } }) _owner.GoToFinding(f); };
            _list.Children.Add(card);
        }
    }
}
