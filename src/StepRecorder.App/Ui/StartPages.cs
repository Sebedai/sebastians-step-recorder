using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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
    Grid _navPanel = null!;

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
            System.Windows.Automation.AutomationProperties.SetName(rb, text);
            rb.Checked += (_, _) => { if (_page != id) ShowPage(id); };
            _nav[id] = rb;
            host.Children.Add(rb);
        }
        Item("new", Theme.IRecord, Loc.L("Neue Aufnahme", "New recording"), Loc.L("Zweck und Bereich wählen – ein Klick startet die Aufnahme", "Choose purpose and area – one click starts recording"), top);
        Item("sessions", Theme.IHistory, Loc.L("Aufnahmen", "Recordings"), Loc.L("Alle Aufnahmen: suchen, öffnen, umbenennen, packen, löschen (Strg+L)", "All recordings: search, open, rename, pack, delete (Ctrl+L)"), top);
        Item("settings", Theme.ISettings, Loc.L("Einstellungen", "Settings"), Loc.L("Darstellung, Sprache, Aufnahme, Tastenkürzel, Speicher und Schutz", "Appearance, language, recording, shortcuts, storage and protection"), bottom);
        Item("help", Theme.IHelp, Loc.L("Hilfe", "Help"), Loc.L("So funktioniert es, Tastenkürzel, KI-Zugriff, Version und Lizenz", "How it works, shortcuts, AI access, version and licence"), bottom);
        // oben die Seiten, unten Einstellungen und Hilfe – die Tab-Taste geht in dieser Reihenfolge
        _navPanel = U.Stack(top, bottom);
        _navPanel.Width = NavWidth;
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
            "sessions" => new LibraryPage(),
            "settings" => BuildSettingsPage(0),
            "help" => BuildHelpPage(),
            _ => _newPage,
        };
        if (page == "new") { UpdateProfile(); FillRecent(); UpdateLastRegionLink(); }
        if (_pageHost.Content is UIElement ue && page != "new") Anim.Appear(ue, 0, 4, 160);
    }

    /// <summary>Seite „Aufnahmen“ neu einlesen, falls sie offen ist (z. B. nach dem Schließen des Editors).</summary>
    public void RescanLibrary() { if (_page == "sessions" && _pageHost.Content is LibraryPage lp) lp.Rescan(); }

    // =====================================================================================
    // Einstellungen
    // =====================================================================================

    FrameworkElement BuildSettingsPage(int tab)
    {
        var page = SettingsPage.Build(this, tab, t => _pageHost.Content = BuildSettingsPage(t));
        // bei wenig Platz scrollen statt verkleinern – die Schrift bleibt gut lesbar
        return new ScrollViewer { Content = new Border { Child = page, Padding = new Thickness(28, 8, 32, 16) }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
    }

    // =====================================================================================
    // Hilfe
    // =====================================================================================

    FrameworkElement BuildHelpPage()
    {
        var col = new StackPanel { Margin = new Thickness(28, 8, 32, 24), MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Left };
        col.Children.Add(U.Heading(Loc.L("Hilfe", "Help"), Theme.FsHero, new Thickness(0, 0, 0, 4)));
        col.Children.Add(U.Hint(Loc.F("{0} zeichnet Arbeitsschritte auf und macht daraus Anleitungen, Support-Pakete und Unterlagen für KI – portabel, offline, ohne Installation.",
            "{0} records work steps and turns them into guides, support packages and material for AI – portable, offline, no installation.", AppInfo.DisplayName), new Thickness(0, 0, 0, 4)));

        // So funktioniert es
        col.Children.Add(U.SectionLabel(Loc.L("So funktioniert es", "How it works")));
        var steps = new UniformGrid { Columns = 4 };
        var s = AppSettings.Load();
        string stop = Loc.Keys(s.Hotkey("stop")), privacy = Loc.Keys(s.Hotkey("privacy"));
        int n = 0;
        foreach (var (head, text) in new[]
        {
            (Loc.L("Zweck wählen", "Choose a purpose"), Loc.L("Anleitung (schlank), Support-Fall (ausführlich), KI-Spur (vollständig) oder Eigene. Der Zweck bestimmt, wie genau aufgezeichnet wird und was nach dem Stopp passiert.",
                "Guide (lean), Support case (detailed), AI trace (complete) or Custom. The purpose sets how precisely input is recorded and what happens after stopping.")),
            (Loc.L("Bereich anklicken", "Click an area"), Loc.L("Ganzer Bildschirm, eine Anwendung oder ein fester Bereich – ein Klick startet die Aufnahme.",
                "Full screen, one application or a fixed area – one click starts recording.")),
            (Loc.L("Vorführen und beenden", "Demonstrate and stop"), Loc.F("Einfach arbeiten wie immer. {0} beendet die Aufnahme, {1} schaltet die Privattaste (Eingaben werden nur gezählt). Das Programm ist in seinen eigenen Bildern nie zu sehen.",
                "Just work as usual. {0} stops recording, {1} toggles the privacy key (input is only counted). The program never appears in its own images.", stop, privacy)),
            (Loc.L("Nachbearbeiten und weitergeben", "Edit and share"), Loc.L("Im Editor Texte anpassen, Hinweise ergänzen, schwärzen – dann als HTML, Word, PDF, KI-Paket oder Skript exportieren. F1 zeigt dort alle Tastenkürzel.",
                "In the editor, adjust texts, add notes, redact – then export as HTML, Word, PDF, AI package or script. F1 shows all shortcuts there.")),
        })
        {
            var card = U.Card(U.Col(U.Text($"{++n}.", Theme.FsSub, true, Theme.AccentText, new Thickness(0, 0, 0, 6)), U.Text(head, Theme.FsBody, true, margin: new Thickness(0, 0, 0, 4)), U.Para(text)), new Thickness(16, 14, 16, 16), new Thickness(0, 0, 8, 0));
            steps.Children.Add(card);
        }
        col.Children.Add(steps);

        // Tastenkürzel während der Aufnahme
        col.Children.Add(U.SectionLabel(Loc.L("Tastenkürzel", "Shortcuts")));
        col.Children.Add(U.Hint(Loc.L("Während der Aufnahme gelten die Kürzel überall – auch wenn ein anderes Programm vorn ist. Ändern lassen sie sich in den Einstellungen. Alle Befehle der Aufnahme gibt es auch per Rechtsklick auf das Symbol im Infobereich. Im Editor zeigt F1 alle Kürzel.",
            "While recording, shortcuts work everywhere – even when another program is in front. Change them in the settings. All recording commands are also available by right-clicking the icon in the notification area. In the editor, F1 shows all shortcuts."), new Thickness(0, 0, 0, 12)));
        col.Children.Add(U.Card(Shortcuts.View(Shortcuts.Recording(), 1), new Thickness(18, 16, 0, 0)));

        // Für KI-Agenten
        col.Children.Add(U.SectionLabel(Loc.L("Für KI-Agenten", "For AI agents")));
        var aiBtn = U.Btn(Theme.IRobot, Loc.L("Mit KI verbinden …", "Connect to AI …"), () => AiConnect.Show(this), Loc.L("Fertige Einrichtung für Claude & Co. anzeigen", "Show the ready-made setup for Claude & co."));
        aiBtn.HorizontalAlignment = HorizontalAlignment.Left; aiBtn.Margin = new Thickness(-3, 10, 0, 0);
        col.Children.Add(U.Card(U.Col(U.Para(Loc.L("Aufnahmen lassen sich direkt an KI-Assistenten geben: als KI-Paket (Exportieren › KI-Paket) oder über den eingebauten MCP-Server.",
            "Recordings can be handed straight to AI assistants: as an AI package (Export › AI package) or via the built-in MCP server.")), aiBtn), new Thickness(16, 14, 16, 14)));

        // Über
        col.Children.Add(U.SectionLabel(Loc.L("Über", "About")));
        var about = U.Col(
            U.Text(AppInfo.DisplayName, Theme.FsBody, true),
            U.Meta(Loc.F("Version {0} · offline · kostenlos, auch beruflich", "Version {0} · offline · free, also commercially", AppInfo.Version), new Thickness(0, 2, 0, 8)),
#if ANONYMOUS
            U.Para(Loc.L("Lizenz: MIT. Enthält Bestandteile unter MIT-Lizenz (.NET, WPF, WPF UI).", "Licence: MIT. Contains components under the MIT licence (.NET, WPF, WPF UI).")));
#else
            U.Para(Loc.L("Lizenz: MIT. © 2026 Sebedai. Enthält Bestandteile unter MIT-Lizenz (.NET, WPF, WPF UI) – siehe THIRD-PARTY-NOTICES.",
                "Licence: MIT. © 2026 Sebedai. Contains components under the MIT licence (.NET, WPF, WPF UI) – see THIRD-PARTY-NOTICES.")));
#endif
        var links = new WrapPanel { Margin = new Thickness(-3, 10, 0, 0) };
#if !ANONYMOUS
        links.Children.Add(U.Btn(Theme.ISearch, Loc.L("Nach Updates suchen", "Check for updates"), () => _ = UpdateCheck.CheckInteractiveAsync(), Loc.L("Einmalig nach einer neuen Version suchen – ohne Kennung, heruntergeladen wird nichts", "Check once for a new version – no identifier, nothing is downloaded")));
        links.Children.Add(U.Btn(Theme.IGlobe, Loc.L("Projektseite", "Project page"), UpdateCheck.OpenReleases, UpdateCheck.ReleasesPage));
#endif
        links.Children.Add(U.Btn(Theme.IWrench, Loc.L("Diagnose-Paket erstellen …", "Create diagnostics package …"), () => DiagnosticsDialog.Create(this), Loc.L("Für eine Fehlermeldung: Protokoll und Systemangaben, ohne Aufnahmen", "For a bug report: log and system information, without recordings")));
        links.Children.Add(U.Btn(Theme.IFolder, Loc.L("Protokollordner", "Log folder"), () => Shell.Reveal(AppLog.FilePath)));
        about.Children.Add(links);
        col.Children.Add(U.Card(about, new Thickness(16, 14, 16, 14)));

        return new ScrollViewer { Content = col, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Nur für Entwickler-Schnappschüsse: alle Seiten nacheinander zeigen und abbilden.</summary>
    internal void DevPages(Action save)
    {
        void Flush() { for (int i = 0; i < 8; i++) Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () => { }); }
        ShowPage("sessions"); Flush(); System.Threading.Thread.Sleep(400); Flush(); save();
        for (int t = 0; t < SettingsPage.TabIds.Length; t++) { _page = "settings"; _nav["settings"].IsChecked = true; _pageHost.Content = BuildSettingsPage(t); Flush(); save(); }
        ShowPage("help"); Flush(); save();
        ShowPage("new"); Flush();
    }
}
