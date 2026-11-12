using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Navigation und Seiten des Startfensters: Neue Aufnahme · Aufnahmen · Einstellungen · Hilfe
/// (linke Spalte wie in den Windows-Einstellungen; unter 900 px nur Symbole).
/// </summary>
sealed partial class StartWindow
{
    const double NavWidth = 232, NavCompact = 60;
    string _page = "new";
    readonly Dictionary<string, RadioButton> _nav = new();
    readonly List<TextBlock> _navLabels = new();
    readonly ContentControl _pageHost = new() { Focusable = false };
    FrameworkElement _newPage = null!;
    DockPanel _navPanel = null!;
    static readonly Dictionary<string, BitmapSource?> ThumbCache = new(StringComparer.OrdinalIgnoreCase);

    FrameworkElement BuildNav()
    {
        var top = new StackPanel { Margin = new Thickness(8, 6, 8, 0) };
        var bottom = new StackPanel { Margin = new Thickness(8, 0, 8, 12) };
        void Item(string id, string glyph, string text, string tip, Panel host)
        {
            var icon = U.Icon(glyph, 16); icon.VerticalAlignment = VerticalAlignment.Center;
            var label = U.Text(text, Theme.FsBody, margin: new Thickness(14, 0, 0, 0)); label.VerticalAlignment = VerticalAlignment.Center;
            _navLabels.Add(label);
            var rb = new RadioButton { GroupName = "startnav", Style = Theme.Style("NavItem"), Content = U.Row(icon, label), ToolTip = tip, Margin = new Thickness(0, 0, 0, 4) };
            System.Windows.Automation.AutomationProperties.SetName(rb, Loc.T(text));
            rb.Checked += (_, _) => { if (_page != id) ShowPage(id); };
            _nav[id] = rb;
            host.Children.Add(rb);
        }
        Item("new", Theme.IRecord, "Neue Aufnahme", "Zweck und Bereich wählen – ein Klick startet die Aufnahme", top);
        Item("sessions", Theme.IHistory, "Aufnahmen", "Alle Aufnahmen durchsuchen und öffnen (Strg+O öffnet eine Datei)", top);
        Item("settings", Theme.ISettings, "Einstellungen", "Darstellung, Sprache, Aufnahme, Speicher und Schutz", bottom);
        Item("help", Theme.IHelp, "Hilfe", "So funktioniert es, alle Tastenkürzel, Version und Lizenz", bottom);
        _navPanel = new DockPanel { Width = NavWidth };
        DockPanel.SetDock(bottom, Dock.Bottom); _navPanel.Children.Add(bottom); _navPanel.Children.Add(top);
        return _navPanel;
    }

    /// <summary>Schmale Fenster: Navigation nur mit Symbolen.</summary>
    void AdaptNav()
    {
        if (_navPanel == null) return;
        bool compact = ActualWidth < 900;
        _navPanel.Width = compact ? NavCompact : NavWidth;
        foreach (var l in _navLabels) l.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Seite öffnen (new | sessions | settings | help).</summary>
    public void ShowPage(string page)
    {
        if (!_nav.ContainsKey(page)) page = "new";
        _page = page;
        if (_nav[page].IsChecked != true) _nav[page].IsChecked = true;
        _pageHost.Content = page switch
        {
            "sessions" => BuildSessionsPage(),
            "settings" => BuildSettingsPage(0),
            "help" => BuildHelpPage(),
            _ => _newPage,
        };
        if (page == "new") { UpdateProfile(); FillRecent(); }
        if (_pageHost.Content is UIElement ue && page != "new") Anim.Appear(ue, 0, 4, 160);
    }

    // =====================================================================================
    // Aufnahmen
    // =====================================================================================

    sealed record SessionInfo(string Path, string Title, DateTime? Created, long? Steps, string? Profile);

    static List<SessionInfo> ReadSessions()
    {
        var paths = new List<string>();
        try { var d = AppPaths.DefaultSessionsFolder(); if (Directory.Exists(d)) paths.AddRange(Directory.EnumerateDirectories(d).Where(Session.LooksLikeSession)); } catch { }
        try { paths.AddRange(AppSettings.Load().RecentSessions.Where(Session.LooksLikeSession)); } catch { }
        var list = new List<SessionInfo>();
        foreach (var p in paths.Select(x => System.IO.Path.GetFullPath(x).TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var man = Json.ReadObject(System.IO.Path.Combine(p, "manifest.json"));
            DateTime? created = null;
            try { if (man?.Str("createdUtc") is { } c) created = Json.ParseUtc(c).ToLocalTime(); } catch { }
            list.Add(new SessionInfo(p, man?.Str("title") is { Length: > 0 } t ? t : System.IO.Path.GetFileName(p), created,
                man?.Obj("summary")?.LongN("primarySteps"), man?.Obj("profile")?.Str("name")));
        }
        return list.OrderByDescending(x => x.Created ?? DateTime.MinValue).ToList();
    }

    FrameworkElement BuildSessionsPage()
    {
        string folder = AppPaths.DefaultSessionsFolder();
        var search = new Wpf.Ui.Controls.TextBox { PlaceholderText = "Aufnahmen durchsuchen", Width = 280, Icon = new Wpf.Ui.Controls.FontIcon { Glyph = Theme.ISearch, FontFamily = Theme.Icons, FontSize = 14 } };
        var open = U.Btn(Theme.IOpen, "Öffnen …", OpenSession, "Aufnahme aus einem anderen Ordner oder eine .steprec-Datei öffnen (Strg+O)");
        var openFolder = U.Btn(Theme.IFolder, "Aufnahmeordner", () => { Directory.CreateDirectory(folder); Process.Start("explorer.exe", $"\"{folder}\""); }, "Aufnahmeordner im Explorer öffnen");
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var cmds = U.Row(search, open, openFolder); cmds.VerticalAlignment = VerticalAlignment.Center;
        search.Margin = new Thickness(0, 0, 6, 0);
        DockPanel.SetDock(cmds, Dock.Right); head.Children.Add(cmds);
        head.Children.Add(U.Heading("Aufnahmen", Theme.FsHero, new Thickness(0)));
        var folderLine = U.Row(U.Icon(Theme.IFolder, 14, Theme.Muted), U.Text(folder, Theme.FsCaption, fg: Theme.Muted, margin: new Thickness(8, 0, 0, 0)));
        ((TextBlock)folderLine.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        if (AppPaths.LooksSynced(folder)) { var sync = U.Chip("synchronisierter Ordner", Theme.WarnSoft, Theme.Warn, Theme.IWarn); sync.Margin = new Thickness(10, 0, 0, 0); folderLine.Children.Add(sync); }
        folderLine.Margin = new Thickness(0, 0, 0, 16);

        var list = new ListBox { ItemContainerStyle = Theme.Style("StepItem"), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(list, true);
        var empty = U.Hint("Noch keine Aufnahmen. Unter „Neue Aufnahme“ starten – oder eine vorhandene Aufnahme mit „Öffnen …“ laden.", new Thickness(0, 8, 0, 0));
        var all = ReadSessions();

        void Fill()
        {
            list.Items.Clear();
            string q = search.Text.Trim();
            var hits = all.Where(x => q.Length == 0 || x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Path.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            string? group = null;
            foreach (var si in hits)
            {
                string g = GroupOf(si.Created);
                if (g != group)
                {
                    group = g;
                    list.Items.Add(new ListBoxItem { Content = U.Text(g, Theme.FsHint, true, Theme.Fg2, new Thickness(0, 10, 0, 0)), IsEnabled = false, Focusable = false, IsHitTestVisible = false });
                }
                list.Items.Add(SessionRow(si));
            }
            empty.Visibility = hits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (hits.Count == 0 && q.Length > 0) empty.Text = Loc.T("Keine Aufnahme passt zur Suche.");
        }
        search.TextChanged += (_, _) => Fill();
        list.KeyDown += (_, e) => { if (e.Key == Key.Enter && list.SelectedItem is ListBoxItem { Tag: string p }) { UiApp.Instance.OpenViewer(p); e.Handled = true; } };
        Fill();

        var page = new DockPanel { Margin = new Thickness(28, 8, 32, 16) };
        DockPanel.SetDock(head, Dock.Top); page.Children.Add(head);
        DockPanel.SetDock(folderLine, Dock.Top); page.Children.Add(folderLine);
        DockPanel.SetDock(empty, Dock.Top); page.Children.Add(empty);
        page.Children.Add(list);
        return page;
    }

    static string GroupOf(DateTime? t)
    {
        if (t == null) return "Ohne Datum";
        var d = (DateTime.Today - t.Value.Date).TotalDays;
        return d <= 0 ? "Heute" : d < 2 ? "Gestern" : d < 7 ? "Diese Woche" : d < 31 ? "Diesen Monat" : "Früher";
    }

    ListBoxItem SessionRow(SessionInfo si)
    {
        var thumb = new Image { Stretch = Stretch.UniformToFill, Width = 80, Height = 50, Clip = new RectangleGeometry(new Rect(0, 0, 80, 50), Theme.RSmall - 1, Theme.RSmall - 1) };
        RenderOptions.SetBitmapScalingMode(thumb, BitmapScalingMode.HighQuality);
        var thumbBox = new Border { Width = 82, Height = 52, CornerRadius = new CornerRadius(Theme.RSmall), Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 16, 0),
            Child = new Grid { Children = { U.Icon(Theme.IDoc, 16, Theme.Muted), thumb } } };
        // Vorschaubild erst laden, wenn die Zeile sichtbar wird (die Liste ist virtualisiert)
        thumb.Loaded += (_, _) =>
        {
            if (thumb.Source != null) return;
            if (ThumbCache.TryGetValue(si.Path, out var cached)) { thumb.Source = cached; return; }
            LoadThumb(si.Path, thumb);
        };
        var meta = new List<string>();
        if (si.Created is { } c) meta.Add(Relative(c));
        if (si.Steps is long n) meta.Add(n == 1 ? "1 Schritt" : $"{n} Schritte");
        if (si.Profile is { Length: > 0 } pr) meta.Add(pr);
        var title = new TextBlock { Text = si.Title, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        var info = U.Col(title, U.Meta(string.Join(" · ", meta.Select(Loc.T)), new Thickness(0, 3, 0, 0)));
        info.VerticalAlignment = VerticalAlignment.Center;
        var g = new DockPanel();
        DockPanel.SetDock(thumbBox, Dock.Left); g.Children.Add(thumbBox); g.Children.Add(info);
        var item = new ListBoxItem { Content = g, Tag = si.Path, ToolTip = si.Path, Cursor = Cursors.Hand };
        System.Windows.Automation.AutomationProperties.SetName(item, si.Title);
        item.MouseLeftButtonUp += (_, _) => UiApp.Instance.OpenViewer(si.Path);
        var menu = U.MenuScope(new ContextMenu());
        menu.Items.Add(U.Mi("Öffnen", null, () => UiApp.Instance.OpenViewer(si.Path), Theme.IOpen));
        menu.Items.Add(U.Mi("Im Explorer zeigen", null, () => Process.Start("explorer.exe", $"\"{si.Path}\""), Theme.IFolder));
        item.ContextMenu = menu;
        return item;
    }

    // =====================================================================================
    // Einstellungen
    // =====================================================================================

    FrameworkElement BuildSettingsPage(int tab)
    {
        var page = SettingsPage.Build(tab, t => _pageHost.Content = BuildSettingsPage(t));
        // ohne Scrollen: bei wenig Platz gleichmäßig verkleinern
        return new Viewbox { Child = page, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(28, 8, 32, 16) };
    }

    // =====================================================================================
    // Hilfe
    // =====================================================================================

    FrameworkElement BuildHelpPage()
    {
        var col = new StackPanel { Margin = new Thickness(28, 8, 32, 24), MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Left };
        col.Children.Add(U.Heading("Hilfe", Theme.FsHero, new Thickness(0, 0, 0, 4)));
        col.Children.Add(U.Hint($"{AppInfo.DisplayName} zeichnet Arbeitsschritte auf und macht daraus Anleitungen, Support-Pakete und Unterlagen für KI – portabel, offline, ohne Installation.", new Thickness(0, 0, 0, 4)));

        // So funktioniert es
        col.Children.Add(U.SectionLabel("So funktioniert es"));
        var steps = new UniformGrid { Columns = 4 };
        string stop = Shortcuts.De(AppSettings.Load().Hotkeys.GetValueOrDefault("stop", "Ctrl+Shift+F10"));
        int n = 0;
        foreach (var (head, text) in new[]
        {
            ("Zweck wählen", "Anleitung (schlank), Support-Fall (ausführlich), KI-Spur (vollständig) oder Eigene. Der Zweck bestimmt, wie genau aufgezeichnet wird."),
            ("Bereich anklicken", "Ganzer Bildschirm, eine Anwendung oder ein fester Bereich – ein Klick startet die Aufnahme."),
            ("Vorführen und beenden", $"Einfach arbeiten wie immer. {stop} beendet die Aufnahme; das Programm ist in seinen eigenen Bildern nie zu sehen."),
            ("Nachbearbeiten und weitergeben", "Im Editor Texte anpassen, Hinweise ergänzen, schwärzen – dann als HTML, Word, PDF, KI-Paket oder Skript exportieren."),
        })
        {
            var card = U.Card(U.Col(U.Text($"{++n}.", Theme.FsSub, true, Theme.AccentText, new Thickness(0, 0, 0, 6)), U.Text(head, Theme.FsBody, true, margin: new Thickness(0, 0, 0, 4)), U.Para(text)), new Thickness(16, 14, 16, 16), new Thickness(0, 0, 8, 0));
            steps.Children.Add(card);
        }
        col.Children.Add(steps);

        // Tastenkürzel
        col.Children.Add(U.SectionLabel("Tastenkürzel"));
        col.Children.Add(U.Hint("Während der Aufnahme gelten die Kürzel überall – auch wenn ein anderes Programm vorn ist. Ändern lassen sie sich in den Einstellungen. Alle Befehle der Aufnahme gibt es auch per Rechtsklick auf das Symbol im Infobereich.", new Thickness(0, 0, 0, 12)));
        col.Children.Add(U.Card(Shortcuts.View(Shortcuts.Recording().Concat(Shortcuts.Editor()), 3), new Thickness(18, 16, 0, 0)));

        // Für KI-Agenten
        col.Children.Add(U.SectionLabel("Für KI-Agenten"));
        col.Children.Add(U.Card(U.Col(U.Para("Aufnahmen lassen sich direkt an KI-Assistenten geben: als KI-Paket (Exportieren › KI-Paket) oder über den eingebauten MCP-Server:"),
            U.CodeBox("SebastiansStepRecorder.exe mcp --root <Aufnahmeordner>")), new Thickness(16, 14, 16, 16)));

        // Über
        col.Children.Add(U.SectionLabel("Über"));
        var about = U.Col(
            U.Text(AppInfo.DisplayName, Theme.FsBody, true),
            U.Meta($"Version {AppInfo.Version} · offline · kostenlos, auch beruflich", new Thickness(0, 2, 0, 8)),
            U.Para("Lizenz: CC BY-ND 4.0 (Namensnennung – keine Bearbeitungen). © 2026 Sebedai. Enthält Bestandteile unter MIT-Lizenz (.NET, WPF, WPF UI) – siehe THIRD-PARTY-NOTICES."));
        var links = new WrapPanel { Margin = new Thickness(-3, 10, 0, 0) };
        links.Children.Add(U.Btn(Theme.ISearch, "Nach Updates suchen", () => _ = UpdateCheck.CheckInteractiveAsync(), "Einmalig auf GitHub nach einer neuen Version suchen – ohne Kennung, heruntergeladen wird nichts"));
        links.Children.Add(U.Btn(Theme.IGlobe, "Projektseite auf GitHub", UpdateCheck.OpenReleases, "github.com/Sebedai/sebastians-step-recorder"));
        about.Children.Add(links);
        col.Children.Add(U.Card(about, new Thickness(16, 14, 16, 14)));

        return new ScrollViewer { Content = col, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Nur für Entwickler-Schnappschüsse: alle Seiten nacheinander zeigen und abbilden.</summary>
    internal void DevPages(Action save)
    {
        void Flush() { for (int i = 0; i < 8; i++) Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { }); }
        ShowPage("sessions"); Flush(); save();
        for (int t = 0; t < SettingsPage.Tabs.Length; t++) { _page = "settings"; _nav["settings"].IsChecked = true; _pageHost.Content = BuildSettingsPage(t); Flush(); save(); }
        ShowPage("help"); Flush(); save();
        ShowPage("new"); Flush();
    }
}
