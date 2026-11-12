using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Native;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Startfenster in zwei Schritten: ① Zweck der Aufnahme (bestimmt nur, was nach dem Stopp passiert),
/// ② Aufnahmebereich (ein Klick startet). Darunter Ziel/Titel, zuletzt verwendet und die wichtigsten Tastenkürzel.
/// </summary>
sealed class StartWindow : Window
{
    readonly RadioButton[] _profiles;
    readonly TextBlock _afterStop = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12.5 };
    readonly TextBox _goal = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60, MaxLength = 2000, VerticalContentAlignment = VerticalAlignment.Top, Tag = "z. B. Neuen Kontakt im Adressbuch anlegen" };
    readonly TextBox _expected = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60, MaxLength = 2000, VerticalContentAlignment = VerticalAlignment.Top, Tag = "z. B. Der Kontakt erscheint in der Liste" };
    readonly TextBox _reference = new() { MaxLength = 200, Tag = "z. B. Ticketnummer" };
    readonly TextBox _title = new() { MaxLength = 120, Tag = "sonst automatisch aus der Aufnahme" };
    readonly Border _goalCard;
    readonly ComboBox _monitor = new() { MinWidth = 240 };
    readonly StackPanel _recent = new();
    static readonly string[] Ids = { "guide", "support", "aiTrace", "custom" };
    readonly StackPanel _customRows = new();
    readonly StackPanel _customMeter = new() { Orientation = Orientation.Horizontal };
    Border _customCard = null!;
    readonly TextBlock _recSummary = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    Button _recLink = null!;

    int ProfileIndex => Math.Max(0, Array.FindIndex(_profiles, r => r.IsChecked == true));

    public StartWindow()
    {
        Title = AppInfo.DisplayName;
        Theme.Prepare(this, Theme.Bg);
        var wa = Theme.ActiveWorkArea();
        Width = Math.Min(1080, wa.Width - 24); SizeToContent = SizeToContent.Height; MaxHeight = wa.Height - 24;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.CanMinimize;
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0 && Session.LooksLikeSession(f[0])) UiApp.Instance.OpenViewer(f[0]); };
        var s = AppSettings.Load();

        // ---------- Kopf ----------
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 30) };
        var actions = U.Row(U.Ghost(Theme.IOpen, "Öffnen", OpenSession, "Aufnahme öffnen (Strg+O)"), U.Ghost(Theme.ISettings, "Einstellungen", ShowSettings), U.Ghost(Theme.IHelp, null, ShowHelp, "Hilfe"));
        actions.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        var logo = Theme.Logo(38); logo.Margin = new Thickness(0, 0, 14, 0);
        var brand = U.Col(Theme.Wordmark(22), U.Text("Arbeitsschritte vorführen – daraus entsteht eine Anleitung mit Bildern", 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 0)));
        brand.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(logo, Dock.Left); header.Children.Add(logo); header.Children.Add(brand);

        // ---------- ① Modus und Detailgrad ----------
        int sel = Math.Max(0, Array.IndexOf(Ids, s.LastProfile));
        var purposes = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new Thickness(-6, 0, -6, 0) };
        _profiles = new[]
        {
            Purpose(Theme.IDoc, "Anleitung", "für Menschen", "Eine bebilderte Schritt-für-Schritt-Anleitung zum Nachmachen.", DetailPresets.Guide, sel == 0),
            Purpose(Theme.IWrench, "Support-Fall", "für den Helpdesk", "Einen Fehler vorführen, damit der Helpdesk ihn nachvollziehen kann.", DetailPresets.Support, sel == 1),
            Purpose(Theme.IRobot, "KI-Spur", "für KI und Skripte", "Ein Ablauf, den eine KI verstehen oder ein Skript automatisieren soll.", DetailPresets.AiTrace, sel == 2),
            CustomPurpose(sel == 3),
        };
        foreach (var r in _profiles) { r.Margin = new Thickness(6, 0, 6, 12); purposes.Children.Add(r); r.Checked += (_, _) => UpdateProfile(); }
        SizeChanged += (_, _) => purposes.Columns = ActualWidth < 900 ? 2 : 4;
        _customCard = BuildCustomCard();
        var afterStopBox = new Border { BorderBrush = Theme.LineStrong, BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(12, 2, 14, 3), Margin = new Thickness(2, 4, 0, 0) };
        var asDock = new DockPanel();
        var asIcon = U.Icon(Theme.IInfo, 13, Theme.Muted); asIcon.VerticalAlignment = VerticalAlignment.Top; asIcon.Margin = new Thickness(0, 1, 10, 0);
        DockPanel.SetDock(asIcon, Dock.Left); asDock.Children.Add(asIcon); asDock.Children.Add(_afterStop);
        afterStopBox.Child = asDock;

        // ---------- ② Bereich ----------
        var tiles = new Grid();
        for (int i = 0; i < 5; i++) tiles.ColumnDefinitions.Add(new ColumnDefinition { Width = i % 2 == 1 ? new GridLength(14) : new GridLength(1, GridUnitType.Star) });
        void Place(UIElement e, int col) { Grid.SetColumn(e, col); tiles.Children.Add(e); }
        var enterHint = U.Row(U.Kbd("Eingabe"), U.Text("startet sofort", 12, fg: Theme.Muted, margin: new Thickness(4, 1, 0, 0)));
        Place(Tile(Theme.IMonitor, "Ganzer Bildschirm", "Alles auf dem Monitor, auf dem gerade gearbeitet wird.", enterHint, () => Begin(ScopeMode.Screen), true), 0);
        Place(Tile(Theme.IApp, "Eine Anwendung", "Nur die Fenster eines Programms – alles andere bleibt außen vor.", Hint(Theme.IPointer, "Danach Fenster anklicken"), () => Begin(ScopeMode.App)), 2);
        Place(Tile(Theme.ICrop, "Bereich", "Ein fester Ausschnitt des Bildschirms, z. B. für kurze Anleitungen.", Hint(Theme.ICrop, "Danach Rechteck aufziehen"), () => Begin(ScopeMode.Region)), 4);

        // ---------- Optionen unter den Kacheln ----------
        _monitor.Items.Add("Monitor mit Aktivität"); _monitor.Items.Add("Alle Monitore");
        foreach (var m in ScreenGrabber.GetMonitors()) _monitor.Items.Add($"Monitor {m.Index + 1} · {m.Bounds.Width} × {m.Bounds.Height}{(m.Primary ? " · Hauptmonitor" : "")}");
        _monitor.SelectedIndex = 0;
        var goalToggle = U.Btn(Theme.IFlag, "Ziel und Titel festlegen", () => { }, "Optional: beschreibt, was die Aufnahme zeigen soll", "Link");
        goalToggle.Click += (_, _) => _goalCard!.Visibility = _goalCard.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        var options = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        DockPanel.SetDock(goalToggle, Dock.Right); options.Children.Add(goalToggle);
        options.Children.Add(U.Row(U.Icon(Theme.IMonitor, 14, Theme.Muted), U.Text("Bildschirm", 13, false, Theme.Fg2, new Thickness(8, 7, 10, 0)), _monitor));

        var goalGrid = new Grid();
        goalGrid.ColumnDefinitions.Add(new ColumnDefinition()); goalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); goalGrid.ColumnDefinitions.Add(new ColumnDefinition());
        goalGrid.RowDefinitions.Add(new RowDefinition()); goalGrid.RowDefinitions.Add(new RowDefinition());
        void G(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); goalGrid.Children.Add(e); }
        G(U.Field("Was wird gezeigt?", _goal), 0, 0); G(U.Field("Erwartetes Ergebnis", _expected), 0, 2);
        G(U.Field("Bezug", _reference, margin: new Thickness(0)), 1, 0); G(U.Field("Titel der Aufnahme", _title, margin: new Thickness(0)), 1, 2);
        _goalCard = U.Card(U.Col(U.Text("Ziel der Aufnahme", 14, true, margin: new Thickness(0, 0, 0, 2)),
            U.Text("Optional. Hilft beim Verstehen der Aufnahme – für Menschen und für KI.", 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 14)), goalGrid), margin: new Thickness(0, 12, 0, 0));
        _goalCard.Visibility = Visibility.Collapsed;

        // ---------- Zusammenfassung: was wird mit dem gewählten Modus aufgezeichnet? ----------
        _recLink = U.Btn(Theme.IEdit, "Anpassen", CustomizeFromPreset, "Diesen Detailgrad als Ausgangspunkt für den Modus „Eigene“ übernehmen", "Link");
        var recRow = new DockPanel();
        DockPanel.SetDock(_recLink, Dock.Right); recRow.Children.Add(_recLink);
        var recIcon = U.Icon(Theme.IRecord, 11, Theme.Accent); recIcon.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(recIcon, Dock.Left); recRow.Children.Add(recIcon); recRow.Children.Add(_recSummary);
        var recBar = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(2, 5, 0, 5), Margin = new Thickness(0, 14, 0, 0), Child = recRow };
        Activated += (_, _) => { if (ProfileIndex == 3) { RefreshCustomCard(); } UpdateCustomCard(); UpdateProfile(); };

        // ---------- Zuletzt verwendet und Tastenkürzel ----------
        var bottom = new Grid { Margin = new Thickness(0, 26, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var recentHead = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var openFolder = U.Btn(Theme.IFolder, "Ordner", () => { var d = AppPaths.DefaultSessionsFolder(); Directory.CreateDirectory(d); Process.Start("explorer.exe", $"\"{d}\""); }, "Aufnahmeordner im Explorer öffnen", "Link");
        DockPanel.SetDock(openFolder, Dock.Right); recentHead.Children.Add(openFolder);
        recentHead.Children.Add(U.Row(U.Icon(Theme.IHistory, 15, Theme.Muted), U.Text("Zuletzt verwendet", 15, true, margin: new Thickness(10, 0, 0, 0))));
        ((FrameworkElement)recentHead.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        var recentCard = U.Card(U.Col(recentHead, _recent), new Thickness(12, 12, 12, 8));
        Grid.SetColumn(recentCard, 0); bottom.Children.Add(recentCard);

        var hk = s.Hotkeys;
        string HK(string key, string def) => De(hk.GetValueOrDefault(key, def));
        var keyGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        keyGrid.ColumnDefinitions.Add(new ColumnDefinition()); keyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        int kr = 0;
        void K(string label, string combo)
        {
            keyGrid.RowDefinitions.Add(new RowDefinition());
            var l = U.Text(label, 12.5, fg: Theme.Fg2, margin: new Thickness(0, 5, 10, 5)); l.VerticalAlignment = VerticalAlignment.Center;
            var k = U.Kbd(combo); Grid.SetRow(l, kr); Grid.SetRow(k, kr); Grid.SetColumn(k, 1);
            keyGrid.Children.Add(l); keyGrid.Children.Add(k); kr++;
        }
        K("Sofort starten · Pause", HK("instantPause", "Ctrl+Shift+F9"));
        K("Beenden", HK("stop", "Ctrl+Shift+F10"));
        K("Fehlerstelle markieren", HK("markerError", "Ctrl+Shift+F1"));
        K("Kommentar", HK("comment", "Ctrl+Shift+F12"));
        K("Privattaste", HK("privacy", "Ctrl+Shift+F8"));
        var keysCard = U.Card(U.Col(U.Row(U.Icon(Theme.IKeyboard, 15, Theme.Muted), U.Text("Während der Aufnahme", 15, true, margin: new Thickness(10, 0, 0, 0))), keyGrid), new Thickness(16, 14, 16, 12));
        Grid.SetColumn(keysCard, 2); bottom.Children.Add(keysCard);

        // ---------- Fuß ----------
        string folder = AppPaths.DefaultSessionsFolder();
        bool strict = s.PrivacyLevel == "strict";
        var foot = new WrapPanel { Margin = new Thickness(2, 18, 0, 0) };
        foot.Children.Add(U.Chip(strict ? "Schutz: streng" : "Schutz: Schutznetz", Theme.SuccessSoft, Theme.Success, Theme.IShield));
        foot.Children.Add(U.Text(strict ? "Eingaben in unbekannten Feldern werden maskiert." : "Passwortfelder werden automatisch maskiert.", 12, fg: Theme.Muted, margin: new Thickness(4, 2, 18, 6)));
        var folderText = U.Text(folder, 12, fg: Theme.Muted, margin: new Thickness(6, 1, 8, 6)); folderText.TextWrapping = TextWrapping.NoWrap; folderText.TextTrimming = TextTrimming.CharacterEllipsis; folderText.MaxWidth = 560; folderText.ToolTip = folder;
        foot.Children.Add(U.Row(U.Icon(Theme.IFolder, 12, Theme.Muted), folderText));
        if (AppPaths.LooksSynced(folder)) foot.Children.Add(U.Chip("synchronisierter Ordner", Theme.WarnSoft, Theme.Warn, Theme.IWarn));

        var root = new StackPanel { Margin = new Thickness(36, 28, 36, 24) };
        root.Children.Add(header);
        root.Children.Add(StepHead(1, "Wofür ist die Aufnahme?", "Der Modus legt fest, wie genau aufgezeichnet wird."));
        root.Children.Add(purposes);
        root.Children.Add(_customCard);
        root.Children.Add(afterStopBox);
        root.Children.Add(StepHead(2, "Was soll aufgenommen werden?", "Ein Klick auf eine Kachel startet die Aufnahme.", new Thickness(0, 28, 0, 12)));
        root.Children.Add(tiles);
        root.Children.Add(options);
        root.Children.Add(recBar);
        root.Children.Add(_goalCard);
        root.Children.Add(bottom);
        root.Children.Add(foot);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        UpdateProfile();
        FillRecent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !(Keyboard.FocusedElement is TextBox or Button or ComboBox)) { Begin(ScopeMode.Screen); e.Handled = true; }
            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { OpenSession(); e.Handled = true; }
        };
        Activated += (_, _) => FillRecent();
    }

    static TextBlock Mono(string text) => new() { Text = text, FontFamily = Theme.Mono, FontSize = 11, Foreground = Theme.Muted, Margin = new Thickness(0, 2, 0, 0) };

    static StackPanel Hint(string glyph, string text) => U.Row(U.Icon(glyph, 12, Theme.Muted), U.Text(text, 12, fg: Theme.Muted, margin: new Thickness(7, 0, 0, 0)));

    static Button Tile(string glyph, string title, string sub, FrameworkElement foot, Action click, bool primary = false)
    {
        var col = new StackPanel();
        var badge = U.Icon(glyph, 24, primary ? Theme.AccentText : Theme.Fg2);
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        col.Children.Add(badge);
        var head = U.Text(title, 17, true, margin: new Thickness(0, 16, 0, 3)); head.FontFamily = Theme.Display;
        col.Children.Add(head);
        col.Children.Add(U.Text(sub, 12.5, fg: Theme.Muted));
        var dock = new DockPanel();
        foot.Margin = new Thickness(0, 16, 0, 0);
        DockPanel.SetDock(foot, Dock.Bottom);
        dock.Children.Add(foot);
        dock.Children.Add(col);
        var b = new Button { Style = Theme.Style("Tile"), Content = dock, MinHeight = 172 };
        if (primary) { b.BorderBrush = Theme.Accent; b.Background = Theme.Surface; }
        b.Click += (_, _) => { try { click(); } catch (Exception ex) { UiApp.ShowError(ex); } };
        return b;
    }

    /// <summary>Überschrift eines nummerierten Schritts im Startfenster.</summary>
    static FrameworkElement StepHead(int n, string title, string sub, Thickness? margin = null) => U.Section(n.ToString("00"), title, sub, margin ?? new Thickness(0, 0, 0, 14));

    /// <summary>Kopf einer Moduskarte: Symbol, Name, Zielgruppe und ein Satz Erklärung.</summary>
    static StackPanel CardHead(string glyph, string title, string audience, string text)
    {
        var col = new StackPanel();
        var name = U.Text(title, 16, true); name.FontFamily = Theme.Display;
        var head = U.Row(U.IconBadge(glyph, 32, Theme.Surface2, Theme.Fg2, 14), U.Col(name, U.Text(audience, 11.5, fg: Theme.Muted)));
        ((FrameworkElement)head.Children[1]).Margin = new Thickness(10, 0, 0, 0); ((FrameworkElement)head.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        head.Margin = new Thickness(0, 0, 26, 0);
        col.Children.Add(head);
        col.Children.Add(U.Text(text, 12.5, fg: Theme.Fg2, margin: new Thickness(0, 10, 0, 0)));
        return col;
    }

    /// <summary>Detailgrad als vier Balken plus Bezeichnung.</summary>
    static void Meter(StackPanel target, int bars, string label)
    {
        target.Children.Clear();
        for (int i = 0; i < 4; i++)
            target.Children.Add(new Border { Width = 16, Height = 6, CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center, Background = i < bars ? Theme.Accent : Theme.LineStrong });
        target.Children.Add(U.Text(label, 12.5, true, Theme.Fg, new Thickness(8, 0, 0, 1)));
    }

    /// <summary>Tabelle „was wird aufgezeichnet“: Klicks, Texteingaben, Mauswege, Zwischenbilder, Programmkontext.</summary>
    static void DetailRows(StackPanel target, ICollection<string> off, string changeFrames, bool appContext)
    {
        target.Children.Clear();
        void Row(string label, string? value, bool on, string tip)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2), ToolTip = tip, Background = Brushes.Transparent };
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(U.Text(label, 12, fg: on ? Theme.Fg2 : Theme.Muted));
            FrameworkElement v = value != null ? U.Text(value, 12, true, Theme.Fg)
                : on ? U.Icon(Theme.ICheck, 11, Theme.AccentText) : U.Text("—", 12, fg: Theme.Muted);
            v.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(v, 1); g.Children.Add(v);
            target.Children.Add(g);
        }
        bool On(params string[] keys) => keys.Any(k => !off.Contains(k));
        Row("Klicks und Scrollen", null, On("clicks", "wheel"), "Mausklicks mit Bild und angeklicktem Element, Scrollen");
        Row("Texteingaben und Tasten", null, On("typing", "hotkeys", "keys"), "Getippter Text, Tastenkombinationen und Einzeltasten (Passwortfelder immer maskiert)");
        Row("Mauswege und Zeigen", null, On("moves"), "Mausbewegungen und Verweilen über Elementen – nützlich für KI und Fehlersuche, für Anleitungen meist überflüssig");
        Row("Zwischenbilder", DetailPresets.ChangeFramesName(changeFrames), true, "Bilder bei Bildschirmänderungen zwischen den Eingaben");
        Row("Programmkontext", appContext ? "erweitert" : "Basis", true, appContext
            ? "Zusätzlich Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument, Statusleiste – für AutoHotkey und KI"
            : "Fenster und angeklicktes Element – reicht für Anleitungen");
    }

    /// <summary>Moduskarte mit festem Detailgrad.</summary>
    static RadioButton Purpose(string glyph, string title, string audience, string text, DetailPreset p, bool selected)
    {
        var col = CardHead(glyph, title, audience, text);
        col.Children.Add(U.Overline("Detailgrad", new Thickness(0, 14, 0, 6)));
        var meter = new StackPanel { Orientation = Orientation.Horizontal }; Meter(meter, p.Bars, p.Level); col.Children.Add(meter);
        var rows = new StackPanel { Margin = new Thickness(0, 8, 0, 0) }; DetailRows(rows, p.Off, p.ChangeFrames, p.AppContext); col.Children.Add(rows);
        return new RadioButton { GroupName = "purpose", Style = Theme.Style("PurposeCard"), Content = col, IsChecked = selected, VerticalContentAlignment = VerticalAlignment.Top };
    }

    /// <summary>Vierter Modus: Detailgrad selbst festlegen (Werte aus den Einstellungen, live aktualisiert).</summary>
    RadioButton CustomPurpose(bool selected)
    {
        var col = CardHead(Theme.ISettings, "Eigene", "selbst festgelegt", "Sie bestimmen selbst, was und wie genau aufgezeichnet wird.");
        col.Children.Add(U.Overline("Detailgrad", new Thickness(0, 14, 0, 6)));
        col.Children.Add(_customMeter);
        _customRows.Margin = new Thickness(0, 8, 0, 0); col.Children.Add(_customRows);
        UpdateCustomCard();
        return new RadioButton { GroupName = "purpose", Style = Theme.Style("PurposeCard"), Content = col, IsChecked = selected, VerticalContentAlignment = VerticalAlignment.Top };
    }

    static readonly (string id, string name, string result)[] AfterStops =
    {
        ("viewer", "Editor öffnen", "Anleitung im Editor – Export in jedes Format"),
        ("export", "KI-Paket exportieren", "KI-Paket: Protokoll, Bilder und Lesehilfe"),
        ("supportPackage", "Support-Paket (ZIP) erstellen", "ZIP-Paket: Anleitung (HTML) + KI-Paket"),
        ("folder", "Aufnahmeordner öffnen", "Aufnahme im Ordner – später im Editor öffnen"),
    };

    void UpdateCustomCard()
    {
        var st = AppSettings.Load();
        var off = RecordingOptions.Channels.Where(c => !st.Records(c.key)).Select(c => c.key).ToList();
        Meter(_customMeter, DetailPresets.BarsFor(off, st.ChangeFrames, st.CustomAppContext), "Eigene");
        DetailRows(_customRows, off, st.ChangeFrames, st.CustomAppContext);
        if (_recSummary != null && _profiles != null && ProfileIndex == 3) UpdateProfile();
    }

    /// <summary>Einstellungen des Modus „Eigene“: Eingabearten, Zwischenbilder, Programmkontext, nach dem Stopp.</summary>
    Border BuildCustomCard()
    {
        var st = AppSettings.Load();
        void Save(Action<AppSettings> change) { var x = AppSettings.Load(); change(x); x.Save(); UpdateCustomCard(); }
        var col = new StackPanel();
        col.Children.Add(U.Text("Eigener Detailgrad", 14, true, margin: new Thickness(0, 0, 0, 2)));
        col.Children.Add(U.Text("Gilt für den Modus „Eigene“ und wird gespeichert. Ausgeschaltetes wird gar nicht erst aufgezeichnet; das Protokoll vermerkt, was fehlt.", 12.5, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 12)));
        col.Children.Add(RecordChoice.Build(st.Record, () => Save(x => x.Record = new Dictionary<string, bool>(st.Record))));

        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(24) : new GridLength(1, GridUnitType.Star) });
        int cf = st.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 };
        var (cfTrack, cfs) = U.Segmented("cfr", cf, (null, "Wenige"), (null, "Normal"), (null, "Viele"));
        for (int i = 0; i < 3; i++) { string v = new[] { "few", "normal", "many" }[i]; cfs[i].Checked += (_, _) => Save(x => x.ChangeFrames = v); }
        var left = U.Field("Zwischenbilder", cfTrack, "Bilder bei Änderungen zwischen den Eingaben – „Viele“ zeigt Ladevorgänge und Animationen, braucht aber mehr Platz.");
        var ctx = new CheckBox { Content = "Erweiterter Programmkontext", IsChecked = st.CustomAppContext, Margin = new Thickness(0, 4, 0, 0) };
        ctx.Checked += (_, _) => Save(x => x.CustomAppContext = true); ctx.Unchecked += (_, _) => Save(x => x.CustomAppContext = false);
        var after = new ComboBox { MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var a in AfterStops) after.Items.Add(a.name);
        after.SelectedIndex = Math.Max(0, Array.FindIndex(AfterStops, a => a.id == st.CustomAfterStop));
        after.SelectionChanged += (_, _) => { string id = AfterStops[Math.Max(0, after.SelectedIndex)].id; Save(x => x.CustomAfterStop = id); };
        var right = U.Col(U.Field("Programmkontext", ctx, "Menüpfade, Listen, Explorer-Ordner, Browser-Adresse, Office-Dokument und Statusleiste – nötig für gute AutoHotkey-Skripte."),
            U.Field("Nach dem Stopp", after, margin: new Thickness(0)));
        Grid.SetColumn(left, 0); Grid.SetColumn(right, 2);
        grid.Children.Add(left); grid.Children.Add(right);
        col.Children.Add(grid);
        var card = U.Card(col, margin: new Thickness(0, 0, 0, 12));
        card.BorderBrush = Theme.Accent;
        card.Visibility = ProfileIndexSafe() == 3 ? Visibility.Visible : Visibility.Collapsed;
        return card;
    }

    /// <summary>Inhalt der Einstellkarte neu aufbauen (Einstellungen können sich außerhalb geändert haben).</summary>
    void RefreshCustomCard()
    {
        var fresh = BuildCustomCard();
        var child = fresh.Child; fresh.Child = null;
        _customCard.Child = child;
    }

    int ProfileIndexSafe() => _profiles == null ? Math.Max(0, Array.IndexOf(Ids, AppSettings.Load().LastProfile)) : ProfileIndex;

    /// <summary>„Anpassen“ bei einem festen Modus: dessen Detailgrad in „Eigene“ übernehmen und dorthin wechseln.</summary>
    void CustomizeFromPreset()
    {
        if (ProfileIndex == 3) { _customCard.Visibility = _customCard.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; return; }
        var p = DetailPresets.For(Ids[ProfileIndex])!;
        var x = AppSettings.Load();
        x.Record = RecordingOptions.Channels.ToDictionary(c => c.key, c => !p.Off.Contains(c.key));
        x.ChangeFrames = p.ChangeFrames; x.CustomAppContext = p.AppContext;
        x.CustomAfterStop = Ids[ProfileIndex] switch { "support" => "supportPackage", "aiTrace" => "export", _ => "viewer" };
        x.Save();
        RefreshCustomCard();
        UpdateCustomCard();
        _profiles[3].IsChecked = true;
    }

    /// <summary>„Ctrl+Shift+F9“ → „Strg+Umschalt+F9“.</summary>
    static string De(string combo) => string.Join("+", combo.Split('+').Select(p => p.Trim() switch { "Ctrl" => "Strg", "Shift" => "Umschalt", var x => x }));

    void UpdateProfile()
    {
        if (_afterStop.Inlines.Count == 0)
        {
            _afterStop.Inlines.Add(new System.Windows.Documents.Run("Bei allen gleich: ") { FontWeight = FontWeights.SemiBold });
            _afterStop.Inlines.Add(new System.Windows.Documents.Run("Bildschirmfotos zu jedem Schritt, Passwortfelder werden maskiert, der Editor steht danach immer zur Verfügung. " +
                "Mehr Detail heißt genauere KI- und AutoHotkey-Ausgaben, aber größere Aufnahmen."));
        }
        if (_profiles == null) return;
        int i = ProfileIndex;
        if (i is 1 or 2 && _goalCard != null) _goalCard.Visibility = Visibility.Visible;
        if (_customCard != null) _customCard.Visibility = i == 3 ? Visibility.Visible : Visibility.Collapsed;
        if (_recLink != null)
        {
            _recLink.Content = U.IconText(Theme.IEdit, i == 3 ? "Einstellungen ein/aus" : "Anpassen …");
            var st = AppSettings.Load();
            var rec = i == 3 ? st.Record : RecordingOptions.Channels.ToDictionary(c => c.key, c => !DetailPresets.For(Ids[i])!.Off.Contains(c.key));
            RecordChoice.Summary(_recSummary, rec);
            string cf = i == 3 ? st.ChangeFrames : DetailPresets.For(Ids[i])!.ChangeFrames;
            bool ctx = i == 3 ? st.CustomAppContext : DetailPresets.For(Ids[i])!.AppContext;
            _recSummary.Inlines.Add(new System.Windows.Documents.Run($"   ·   Zwischenbilder: {DetailPresets.ChangeFramesName(cf)} · Kontext: {(ctx ? "erweitert" : "Basis")}") { Foreground = Theme.Muted });
        }
    }

    void FillRecent()
    {
        _recent.Children.Clear();
        var list = AppSettings.Load().RecentSessions.Where(Session.LooksLikeSession).Take(5).ToList();
        if (list.Count == 0)
        {
            _recent.Children.Add(U.Text("Noch keine Aufnahmen. Wählen Sie oben, was aufgenommen werden soll.", 12.5, fg: Theme.Muted, margin: new Thickness(10, 8, 10, 10)));
            return;
        }
        foreach (var p in list)
        {
            var man = Json.ReadObject(System.IO.Path.Combine(p, "manifest.json"));
            string t = man?.Str("title") is { Length: > 0 } title ? title : System.IO.Path.GetFileName(p);
            var meta = new List<string>();
            if (man != null)
            {
                try { meta.Add(Json.ParseUtc(man.Str("createdUtc")).ToLocalTime().ToString("dd.MM.yyyy HH:mm")); } catch { }
                if (man.Obj("summary")?.LongN("primarySteps") is long n) meta.Add($"{n} Schritte");
                meta.Add(TimeSpan.FromTicks(man.Long("durationUs") * 10).ToString(@"mm\:ss"));
            }
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var badge = U.IconBadge(Theme.IDoc, 34, Theme.Surface2, Theme.Muted, 14); badge.Margin = new Thickness(0, 0, 12, 0);
            var text = U.Col(new TextBlock { Text = t, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }, Mono(string.Join("  ·  ", meta)));
            var chev = U.Icon(Theme.INext, 11, Theme.Muted);
            Grid.SetColumn(text, 1); Grid.SetColumn(chev, 2);
            g.Children.Add(badge); g.Children.Add(text); g.Children.Add(chev);
            var b = new Button { Style = Theme.Style("RowButton"), Content = g, ToolTip = p, Margin = new Thickness(0, 1, 0, 1) };
            b.Click += (_, _) => UiApp.Instance.OpenViewer(p);
            _recent.Children.Add(b);
        }
    }

    RecordingOptions BuildOptions(ScopeMode scope)
    {
        var o = UiApp.OptionsFromSettings(scope);
        UiApp.ApplyProfile(o, Ids[ProfileIndex]);
        o.Scope = scope;
        o.Monitors = _monitor.SelectedIndex switch { 0 => "active", 1 => "all", var i => "M" + (i - 1) };
        o.GoalDescription = _goal.Text; o.GoalExpected = _expected.Text; o.GoalReference = _reference.Text;
        o.Title = _title.Text.Trim();
        var s = AppSettings.Load(); s.LastProfile = o.ProfileId; s.Save();
        return o;
    }

    void Begin(ScopeMode scope)
    {
        var o = BuildOptions(scope);
        if (scope == ScopeMode.App)
        {
            Hide();
            var pick = WindowPicker.Pick();
            if (pick == null) { Show(); return; }
            o.AppProcess = pick.Value.process; o.AppPid = (int)pick.Value.pid; o.AppWindow = 0;
        }
        else if (scope == ScopeMode.Region)
        {
            Hide();
            var r = RegionPicker.Pick();
            if (r == null) { Show(); return; }
            o.Region = r;
        }
        _goal.Clear(); _expected.Clear(); _reference.Clear(); _title.Clear();
        UiApp.Instance.StartRecording(o);
    }

    public void ShowSupport()
    {
        var r = Msg.Show("Problem aufnehmen\n\n1. Aufnahme starten und das Problem vorführen.\n2. Fehlerstelle mit Strg+Umschalt+F1 markieren, mit Strg+Umschalt+F10 beenden.\n3. Eingaben durchsehen und das ZIP erstellen.\n\nDas Programm versendet nichts – Sie geben das ZIP selbst weiter. Aufnahme jetzt starten?",
            "Support-Paket", MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (r != MessageBoxResult.OK) return;
        _profiles[1].IsChecked = true;
        Begin(ScopeMode.Screen);
    }

    void OpenSession()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Aufnahme (manifest.json;*.steprec)|manifest.json;*.steprec", InitialDirectory = AppPaths.DefaultSessionsFolder() };
        if (dlg.ShowDialog(this) == true)
        {
            var p = dlg.FileName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase) ? System.IO.Path.GetDirectoryName(dlg.FileName)! : dlg.FileName;
            UiApp.Instance.OpenViewer(p);
        }
    }

    void ShowSettings() => new SettingsWindow { Owner = this }.ShowDialog();

    void ShowHelp() => Msg.Show(
        "So funktioniert StepRecorder\n\n" +
        "1. Modus wählen: Anleitung (schlank), Support-Fall (ausführlich), KI-Spur (vollständig) oder Eigene (selbst festgelegt). Der Modus bestimmt den Detailgrad – Eingabearten, Zwischenbilder, Programmkontext – und was nach dem Stopp passiert.\n\n" +
        "2. Bereich wählen: ganzer Bildschirm, eine Anwendung oder ein Ausschnitt. Ein Klick startet die Aufnahme; vorführen und mit Strg+Umschalt+F10 beenden.\n\n" +
        "Privattaste Strg+Umschalt+F8: Eingaben werden nur gezählt, nicht gespeichert.\n\n" +
        "Editor: Texte direkt rechts ändern, Kapitel und Hinweiskästen am Schritt anlegen, im Reiter „Dokument“ Deckblatt, Inhaltsverzeichnis, Bildgröße und Farbe festlegen, Werkzeuge links am Bild (V Auswahl, A Pfeil, R Rahmen, E Ellipse, M Hervorheben, T Text, N Nummer, X Schwärzen), Markierungen anklicken und mit Entf löschen, Schritte per Ziehen umsortieren. Strg+C kopiert das Bild, Strg+E exportiert.\n\n" +
        "KI-Zugriff: StepRecorder.exe mcp --root <Aufnahmeordner> startet den MCP-Server (stdio).\n\n" +
        "Portabel, offline, ohne Installation. Das Programm ist in seinen eigenen Bildern nie zu sehen.",
        "Hilfe", MessageBoxButton.OK, MessageBoxImage.Information);

    protected override void OnClosed(EventArgs e) { base.OnClosed(e); UiApp.Instance.MaybeShutdown(); }
}

/// <summary>Auswahl, welche Eingabearten aufgezeichnet werden (Mausgruppe oben, Tastaturgruppe unten).</summary>
static class RecordChoice
{
    /// <summary>Baut die Auswahl; jede Änderung wird in <paramref name="target"/> geschrieben und <paramref name="changed"/> gemeldet.</summary>
    public static FrameworkElement Build(Dictionary<string, bool> target, Action? changed)
    {
        var col = new StackPanel();
        var warn = U.Text("Alles ausgeschaltet – es entstehen nur noch Bilder bei Bildschirmänderungen.", 12, fg: Theme.Warn, margin: new Thickness(0, 2, 0, 0));
        void UpdWarn() => warn.Visibility = RecordingOptions.Channels.All(c => !target.GetValueOrDefault(c.key, true)) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (group, glyph, keys) in new[] { ("Maus", Theme.IMouse, new[] { "clicks", "wheel", "moves" }), ("Tastatur", Theme.IKeyboard, new[] { "typing", "hotkeys", "keys" }) })
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            var head = U.Row(U.Icon(glyph, 13, Theme.Muted), U.Text(group, 12.5, true, Theme.Fg2, new Thickness(7, 0, 0, 0)));
            head.VerticalAlignment = VerticalAlignment.Top; head.Margin = new Thickness(0, 9, 0, 0);
            grid.Children.Add(head);
            for (int i = 0; i < keys.Length; i++)
            {
                var (key, name, hint) = RecordingOptions.Channels.First(c => c.key == keys[i]);
                var cb = new CheckBox { IsChecked = target.GetValueOrDefault(key, true), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 10, 0) };
                var text = U.Col(U.Text(name, 13, true), U.Text(hint, 11.5, fg: Theme.Muted, margin: new Thickness(0, 2, 0, 0)));
                var item = new DockPanel();
                DockPanel.SetDock(cb, Dock.Left); item.Children.Add(cb); item.Children.Add(text);
                var box = new Border { Child = item, Padding = new Thickness(10, 7, 10, 8), Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = hint };
                box.MouseEnter += (_, _) => box.Background = Theme.HoverBg;
                box.MouseLeave += (_, _) => box.Background = Brushes.Transparent;
                text.MouseLeftButtonUp += (_, _) => cb.IsChecked = cb.IsChecked != true;
                void Set() { target[key] = cb.IsChecked == true; UpdWarn(); changed?.Invoke(); }
                cb.Checked += (_, _) => Set(); cb.Unchecked += (_, _) => Set();
                Grid.SetColumn(box, i + 1); grid.Children.Add(box);
            }
            col.Children.Add(grid);
        }
        col.Children.Add(warn);
        UpdWarn();
        return col;
    }

    /// <summary>Kurzfassung für eine Zeile: „alles“ oder was fehlt.</summary>
    public static void Summary(TextBlock t, Dictionary<string, bool> rec)
    {
        var off = RecordingOptions.Channels.Where(c => !rec.GetValueOrDefault(c.key, true)).Select(c => c.name).ToList();
        var on = RecordingOptions.Channels.Where(c => rec.GetValueOrDefault(c.key, true)).Select(c => c.name).ToList();
        t.Inlines.Clear();
        t.Inlines.Add(new System.Windows.Documents.Run("Aufgezeichnet: ") { FontWeight = FontWeights.SemiBold });
        t.Inlines.Add(new System.Windows.Documents.Run(off.Count == 0 ? "alles – " + string.Join(", ", on) : on.Count == 0 ? "keine Eingaben" : string.Join(", ", on)));
        if (off.Count > 0 && on.Count > 0)
        {
            t.Inlines.Add(new System.Windows.Documents.Run("   ·   Nicht: ") { FontWeight = FontWeights.SemiBold, Foreground = Theme.Warn });
            t.Inlines.Add(new System.Windows.Documents.Run(string.Join(", ", off)) { Foreground = Theme.Warn });
        }
    }
}

/// <summary>Hinweis-Pille für die Auswahl-Overlays.</summary>
static class OverlayUi
{
    public static readonly Brush PillBg = new SolidColorBrush(Color.FromArgb(0xEE, 0x17, 0x16, 0x14));
    public static readonly Brush DarkLine = new SolidColorBrush(Color.FromRgb(0x3B, 0x3A, 0x36));

    public static Border Pill(string glyph, string text)
    {
        var row = U.Row(U.Icon(glyph, 15, Brushes.White), U.Text(text, 14, false, Brushes.White, new Thickness(10, 0, 14, 0)), U.Kbd("Esc", new SolidColorBrush(Color.FromRgb(0x26, 0x25, 0x22)), Brushes.Gainsboro, DarkLine), U.Text("abbrechen", 13, false, Brushes.Gainsboro, new Thickness(4, 0, 0, 0)));
        foreach (var c in row.Children.OfType<FrameworkElement>()) c.VerticalAlignment = VerticalAlignment.Center;
        return new Border
        {
            Background = PillBg, CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 11, 18, 11), Child = row,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 48), Effect = Theme.Shadow(24, 4, 0.4)
        };
    }

    public static Border Label(TextBlock t)
    {
        t.Foreground = Brushes.White; t.FontSize = 13;
        return new Border { Background = PillBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 7), Child = t };
    }
}

/// <summary>Fensterauswahl: Bildschirm abgedunkelt, Fenster unter der Maus hervorgehoben; Klick wählt die Anwendung.</summary>
sealed class WindowPicker : Window
{
    readonly Rectangle _hl = new() { StrokeThickness = 3, RadiusX = 8, RadiusY = 8, Fill = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)) };
    readonly TextBlock _labelText = new();
    readonly Border _label;
    readonly Canvas _canvas = new();
    (nint hwnd, uint pid, string process)? _result, _current;
    readonly System.Drawing.Rectangle _virt;
    double _scale = 1;

    WindowPicker()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)); Topmost = true; ShowInTaskbar = false; Cursor = Cursors.Cross;
        FontFamily = Theme.UiFont;
        _hl.Stroke = Theme.Accent;
        _label = OverlayUi.Label(_labelText);
        _virt = ScreenGrabber.VirtualScreen(ScreenGrabber.GetMonitors());
        _canvas.Children.Add(_hl); _canvas.Children.Add(_label);
        Content = new Grid { Children = { _canvas, OverlayUi.Pill(Theme.IPointer, "Anwendung anklicken, die aufgenommen werden soll") } };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowPos(h, -1, _virt.X, _virt.Y, _virt.Width, _virt.Height, 0x0040);
            _scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1;
        };
        MouseMove += (_, _) => Track();
        MouseLeftButtonDown += (_, _) => { _result = _current; DialogResult = _result != null; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
    }

    void Track()
    {
        Win32.GetCursorPos(out var p);
        nint own = new WindowInteropHelper(this).Handle;
        nint found = 0;
        Win32.EnumWindows((h, _) =>
        {
            if (h == own || !Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return true;
            Win32.GetWindowThreadProcessId(h, out uint pid);
            if (pid == Environment.ProcessId) return true;
            if (!Win32.GetWindowRect(h, out var r) || r.Width < 50 || r.Height < 30) return true;
            if (p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom && Win32.GetWindowTitle(h).Length > 0) { found = h; return false; }
            return true;
        }, 0);
        if (found == 0) { _current = null; _hl.Visibility = Visibility.Hidden; _label.Visibility = Visibility.Hidden; return; }
        Win32.GetWindowThreadProcessId(found, out uint fpid);
        string proc = System.IO.Path.GetFileName(Win32.GetProcessPath(fpid) ?? "?");
        _current = (found, fpid, proc);
        Win32.GetWindowRect(found, out var wr);
        _hl.Visibility = Visibility.Visible; _label.Visibility = Visibility.Visible;
        Canvas.SetLeft(_hl, (wr.Left - _virt.X) / _scale); Canvas.SetTop(_hl, (wr.Top - _virt.Y) / _scale);
        _hl.Width = wr.Width / _scale; _hl.Height = wr.Height / _scale;
        bool excluded = RecordingOptions.DefaultExcluded.Contains(proc, StringComparer.OrdinalIgnoreCase);
        _labelText.Text = $"{Win32.GetWindowTitle(found)}\n{proc} · alle Fenster dieser Anwendung{(excluded ? "\nausgeschlossen – wird nicht aufgezeichnet" : "")}";
        Canvas.SetLeft(_label, (wr.Left - _virt.X) / _scale + 12); Canvas.SetTop(_label, (wr.Top - _virt.Y) / _scale + 12);
    }

    public static (nint hwnd, uint pid, string process)? Pick()
    {
        var w = new WindowPicker();
        return w.ShowDialog() == true ? w._result : null;
    }

    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);
}

/// <summary>Bereichsauswahl: Rechteck aufziehen (mindestens 64×64, auf einem Monitor).</summary>
sealed class RegionPicker : Window
{
    readonly Rectangle _sel = new() { StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), Visibility = Visibility.Hidden };
    readonly TextBlock _sizeText = new();
    readonly Border _size;
    readonly System.Drawing.Rectangle _virt;
    Win32.POINT _start; bool _drag; double _scale = 1;
    System.Drawing.Rectangle? _result;

    RegionPicker()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)); Topmost = true; ShowInTaskbar = false; Cursor = Cursors.Cross;
        FontFamily = Theme.UiFont;
        _sel.Stroke = Theme.Accent;
        _size = OverlayUi.Label(_sizeText); _size.Visibility = Visibility.Hidden;
        _virt = ScreenGrabber.VirtualScreen(ScreenGrabber.GetMonitors());
        var c = new Canvas { Children = { _sel, _size } };
        Content = new Grid { Children = { c, OverlayUi.Pill(Theme.ICrop, "Rechteck aufziehen, das aufgenommen werden soll") } };
        SourceInitialized += (_, _) =>
        {
            SetWindowPos(new WindowInteropHelper(this).Handle, -1, _virt.X, _virt.Y, _virt.Width, _virt.Height, 0x0040);
            _scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1;
        };
        MouseLeftButtonDown += (_, _) => { Win32.GetCursorPos(out _start); _drag = true; CaptureMouse(); };
        MouseMove += (_, _) =>
        {
            if (!_drag) return;
            var r = Current();
            _sel.Visibility = Visibility.Visible; _size.Visibility = Visibility.Visible;
            Canvas.SetLeft(_sel, (r.X - _virt.X) / _scale); Canvas.SetTop(_sel, (r.Y - _virt.Y) / _scale); _sel.Width = r.Width / _scale; _sel.Height = r.Height / _scale;
            _sizeText.Text = $"{r.Width} × {r.Height} px"; Canvas.SetLeft(_size, (r.X - _virt.X) / _scale); Canvas.SetTop(_size, Math.Max(0, (r.Y - _virt.Y) / _scale - 36));
        };
        MouseLeftButtonUp += (_, _) =>
        {
            if (!_drag) return;
            _drag = false; ReleaseMouseCapture();
            var r = Current();
            if (r.Width < 64 || r.Height < 64) { _sel.Visibility = Visibility.Hidden; _sizeText.Text = "mindestens 64 × 64 px"; return; }
            var mon = ScreenGrabber.GetMonitors().OrderByDescending(m => { var i = System.Drawing.Rectangle.Intersect(m.Bounds, r); return (long)i.Width * i.Height; }).First();
            _result = System.Drawing.Rectangle.Intersect(r, mon.Bounds);
            DialogResult = true;
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) DialogResult = false; };
    }

    System.Drawing.Rectangle Current()
    {
        Win32.GetCursorPos(out var p);
        return System.Drawing.Rectangle.FromLTRB(Math.Min(p.X, _start.X), Math.Min(p.Y, _start.Y), Math.Max(p.X, _start.X), Math.Max(p.Y, _start.Y));
    }

    public static System.Drawing.Rectangle? Pick()
    {
        var w = new RegionPicker();
        return w.ShowDialog() == true ? w._result : null;
    }

    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);
}

/// <summary>Einstellungen: Sprache, Anrede, Schutzstufe, Zwischenbilder, Aufnahmeordner, Ausschlussliste.</summary>
sealed class SettingsWindow : Window
{
    static DockPanel Note(string text)
    {
        var d = new DockPanel();
        var i = U.Icon(Theme.IInfo, 12, Theme.Muted); i.VerticalAlignment = VerticalAlignment.Top; i.Margin = new Thickness(0, 2, 8, 0);
        DockPanel.SetDock(i, Dock.Left); d.Children.Add(i); d.Children.Add(U.Text(text, 12, fg: Theme.Muted));
        return d;
    }

    public SettingsWindow()
    {
        Title = "Einstellungen"; Width = 800; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        var s = AppSettings.Load();
        var lang = new ComboBox { Items = { "Deutsch", "English" }, SelectedIndex = s.Language == "en" ? 1 : 0 };
        var address = new ComboBox { Items = { "Sie (förmlich)", "du (persönlich)" }, SelectedIndex = s.Address == "informal" ? 1 : 0 };
        var level = new ComboBox { Items = { "Schutznetz – Passwortfelder maskieren", "Streng – unbekannte Felder maskieren" }, SelectedIndex = s.PrivacyLevel == "strict" ? 1 : 0 };
        var change = new ComboBox { Items = { "wenige", "normal", "viele" }, SelectedIndex = s.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 } };
        var folder = new TextBox { Text = s.SessionsFolder ?? AppPaths.DefaultSessionsFolder() };
        var warn = U.Text("", 12, fg: Theme.Warn, margin: new Thickness(0, 5, 0, 0));
        void CheckSync() { warn.Text = AppPaths.LooksSynced(folder.Text) ? "Der Ordner wird synchronisiert – Aufnahmen können dadurch Unbeteiligten zugänglich werden." : ""; warn.Visibility = warn.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed; }
        folder.TextChanged += (_, _) => CheckSync(); CheckSync();
        var browse = U.Btn(Theme.IFolder, null, () =>
        {
            using var d = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = folder.Text, Description = "Aufnahmeordner wählen", UseDescriptionForTitle = true };
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) folder.Text = d.SelectedPath;
        }, "Ordner wählen …");
        browse.Margin = new Thickness(8, 0, 0, 0);
        var folderRow = new DockPanel(); DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(folder);
        var excl = new TextBox { Text = string.Join(Environment.NewLine, s.Excluded), AcceptsReturn = true, Height = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top, FontFamily = Theme.Mono, FontSize = 12.5 };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        void G(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); grid.Children.Add(e); }
        G(U.Field("Sprache der Texte", lang), 0, 0); G(U.Field("Anrede in Anleitungen", address), 0, 2);
        G(U.Field("Schutzstufe", level), 1, 0); G(U.Field("Zwischenbilder · Modus „Eigene“", change, "Bilder bei Änderungen zwischen den Eingaben. Anleitung, Support-Fall und KI-Spur legen das selbst fest."), 1, 2);

        var rec = new Dictionary<string, bool>(s.Record);
        var fallback = new CheckBox { Content = "Ohne Nachfrage mit ausgeblendeter Anzeige aufnehmen, wenn die Unsichtbarkeit nicht bestätigt werden kann", IsChecked = s.InvisibleFallback == "hidden", Margin = new Thickness(0, 2, 0, 4),
            ToolTip = "Z. B. unter Windows Server 2019: Leiste und Rahmen bleiben ausgeblendet, gesteuert wird über Tastenkürzel (Strg+Umschalt+F4 blendet ein)." };
        var countdown = new ComboBox { Items = { "aus", "3 Sekunden", "5 Sekunden" }, SelectedIndex = s.Countdown >= 5 ? 2 : s.Countdown >= 3 ? 1 : 0, MinWidth = 160 };
        var sounds = new ComboBox { Items = { "nur bei ausgeblendeter Anzeige", "immer", "nie" }, SelectedIndex = s.SoundCues switch { "always" => 1, "never" => 2, _ => 0 }, MinWidth = 220 };
        var cues = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        cues.ColumnDefinitions.Add(new ColumnDefinition()); cues.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); cues.ColumnDefinitions.Add(new ColumnDefinition());
        var fCd = U.Field("Countdown vor der Aufnahme", countdown, "Bei ausgeblendeter Anzeige immer mindestens 3 Sekunden.");
        var fSnd = U.Field("Signaltöne bei Start, Stopp, Pause, Marker", sounds, "Kurze Töne – hilfreich, wenn keine Leiste zu sehen ist.");
        Grid.SetColumn(fSnd, 2); cues.Children.Add(fCd); cues.Children.Add(fSnd);
        var body = U.Col(
            U.Overline("Allgemein", new Thickness(0, 0, 0, 10)), grid, fallback, cues,
            U.Overline("Was wird aufgezeichnet? · Modus „Eigene“", new Thickness(0, 6, 0, 4)),
            U.Text("Gilt für den Modus „Eigene“ im Startfenster – die anderen Modi haben einen festen Detailgrad. Ausgeschaltetes wird gar nicht erst gespeichert; das Protokoll vermerkt, was fehlt.", 12, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 6)),
            RecordChoice.Build(rec, null),
            U.Overline("Speicherort", new Thickness(0, 6, 0, 10)), U.Field("Aufnahmeordner", U.Col(folderRow, warn)),
            U.Overline("Ausschlüsse", new Thickness(0, 6, 0, 10)), U.Field("Ausgeschlossene Programme", excl, "Ein Prozessname je Zeile – von diesen Programmen wird nichts aufgezeichnet."),
            Note(AppPaths.Portable ? $"Portabler Modus – Einstellungen liegen neben der EXE ({AppPaths.DataDir})." : $"Profilmodus – Einstellungen liegen in {AppPaths.DataDir}."));
        U.Dialog(this, null, body, U.Primary("Speichern", () =>
        {
            var removed = s.Excluded.Except(excl.Text.Split('\n').Select(x => x.Trim())).ToList();
            if (removed.Count > 0 && Msg.Show("Schutz abschwächen?\n\nFolgende Ausschlüsse werden entfernt und deren Eingaben künftig aufgezeichnet:\n" + string.Join(", ", removed), "Einstellungen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (level.SelectedIndex == 0 && s.PrivacyLevel == "strict" && Msg.Show("Schutz abschwächen?\n\nSchutzstufe von „Streng“ auf „Schutznetz“ senken?", "Einstellungen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            s.Language = lang.SelectedIndex == 1 ? "en" : "de"; s.PrivacyLevel = level.SelectedIndex == 1 ? "strict" : "balanced";
            s.ChangeFrames = change.SelectedIndex switch { 0 => "few", 2 => "many", _ => "normal" };
            s.SessionsFolder = folder.Text.Trim(); s.Address = address.SelectedIndex == 1 ? "informal" : "formal";
            s.Record = rec;
            s.InvisibleFallback = fallback.IsChecked == true ? "hidden" : "ask";
            s.Countdown = countdown.SelectedIndex switch { 1 => 3, 2 => 5, _ => 0 };
            s.SoundCues = sounds.SelectedIndex switch { 1 => "always", 2 => "never", _ => "hidden" };
            s.Excluded = excl.Text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            s.Save(); Close();
        }), U.Btn("Abbrechen", Close));
    }
}
