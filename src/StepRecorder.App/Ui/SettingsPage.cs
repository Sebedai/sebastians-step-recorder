using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StepRecorder.Capture;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Seite „Einstellungen“ im Startfenster: Darstellung, Sprachen, Schutzstufe, Erinnerungen, Tastenkürzel, Modus „Eigene“,
/// Speicher, Austausch. Die Bereiche stehen als Auswahlleiste oben, unten „Speichern“ und „Verwerfen“.
/// </summary>
static class SettingsPage
{
    static DockPanel Note(string text)
    {
        var d = new DockPanel();
        var i = U.Icon(Theme.IInfo, 14, Theme.Muted); i.VerticalAlignment = VerticalAlignment.Top; i.Margin = new Thickness(0, 2, 8, 0);
        DockPanel.SetDock(i, Dock.Left); d.Children.Add(i); d.Children.Add(U.Hint(text));
        return d;
    }

    static Grid Two(params UIElement[] items)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        for (int k = 0; k < items.Length; k++)
        {
            if (k % 2 == 0) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(items[k], k / 2); Grid.SetColumn(items[k], k % 2 == 0 ? 0 : 2); g.Children.Add(items[k]);
        }
        return g;
    }

    public static readonly string[] TabIds = { "general", "recording", "keys", "custom", "storage" };

    /// <summary>Baut die Seite. <paramref name="rebuild"/> baut sie mit dem genannten Bereich neu auf (Verwerfen, nach Import oder Zurücksetzen).</summary>
    public static FrameworkElement Build(Window owner, int tab, Action<int> rebuild)
    {
        string Title = Loc.L("Einstellungen", "Settings");
        var s = AppSettings.Load();
        int current = Math.Clamp(tab, 0, TabIds.Length - 1);
        var page = new StackPanel { Width = 880, HorizontalAlignment = HorizontalAlignment.Left };
        // Solange die Einstellungen offen sind, ist das eigene Sofort-Kürzel abgemeldet (sonst gälte es bei der Prüfung als „belegt“)
        bool hkPaused = false;
        page.Loaded += (_, _) => { if (!hkPaused && UiApp.Instance.Recording == null) { hkPaused = true; UiApp.Instance.UnregisterGlobalHotkeys(); } };
        page.Unloaded += (_, _) => { if (hkPaused && UiApp.Instance.Recording == null) UiApp.Instance.RegisterGlobalHotkeys(); hkPaused = false; };

        var uiLang = new ComboBox { Items = { Loc.L("Wie Windows", "Like Windows"), "Deutsch", "English" }, SelectedIndex = s.UiLanguage switch { "de" => 1, "en" => 2, _ => 0 } };
        var lang = new ComboBox { Items = { "Deutsch", "English" }, SelectedIndex = s.Language == "en" ? 1 : 0 };
        var address = new ComboBox { Items = { Loc.L("Sie (förmlich)", "Sie (formal)"), Loc.L("du (persönlich)", "du (informal)") }, SelectedIndex = s.Address == "informal" ? 1 : 0 };
        var level = new ComboBox { Items = { Loc.L("Schutznetz – Passwortfelder maskieren", "Safety net – mask password fields"), Loc.L("Streng – unbekannte Felder, Terminal und Fernzugriff maskieren", "Strict – mask unknown fields, terminals and remote sessions") }, SelectedIndex = s.PrivacyLevel == "strict" ? 1 : 0 };
        var change = new ComboBox { Items = { DetailPresets.ChangeFramesName("few"), DetailPresets.ChangeFramesName("normal"), DetailPresets.ChangeFramesName("many") }, SelectedIndex = s.ChangeFrames switch { "few" => 0, "many" => 2, _ => 1 } };
        var changeHint = U.Hint(DetailPresets.ChangeFramesRule(s.ChangeFrames), new Thickness(0, 6, 0, 0));
        change.SelectionChanged += (_, _) => changeHint.Text = DetailPresets.ChangeFramesRule(change.SelectedIndex switch { 0 => "few", 2 => "many", _ => "normal" });
        var folder = new TextBox { Text = s.SessionsFolder ?? AppPaths.DefaultSessionsFolder() };
        var warn = U.Text("", Theme.FsHint, fg: Theme.Warn, margin: new Thickness(0, 6, 0, 0));
        void CheckSync() { warn.Text = AppPaths.LooksSynced(folder.Text) ? Loc.L("Der Ordner wird synchronisiert – Aufnahmen können dadurch Unbeteiligten zugänglich werden.", "This folder is synced – recordings may become accessible to others.") : ""; warn.Visibility = warn.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed; }
        folder.TextChanged += (_, _) => CheckSync(); CheckSync();
        var browse = U.Btn(Theme.IFolder, null, () =>
        {
            var d = new Microsoft.Win32.OpenFolderDialog { InitialDirectory = Directory.Exists(folder.Text) ? folder.Text : null, Title = Loc.L("Aufnahmeordner wählen", "Choose recordings folder") };
            if (d.ShowDialog(owner) == true) folder.Text = d.FolderName;
        }, Loc.L("Ordner wählen …", "Choose folder …"));
        browse.Margin = new Thickness(8, 0, 0, 0);
        var folderRow = new DockPanel(); DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(folder);
        var excl = new TextBox { Text = string.Join(Environment.NewLine, s.Excluded), AcceptsReturn = true, Height = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top, FontFamily = Theme.Mono, FontSize = Theme.FsHint };
        var theme = new ComboBox { Items = { Loc.L("Dunkel", "Dark"), Loc.L("Hell", "Light"), Loc.L("Wie Windows", "Like Windows") }, SelectedIndex = s.Theme switch { "light" => 1, "system" => 2, _ => 0 } };
        var rec = new Dictionary<string, bool>(s.Record);
        var fallback = new CheckBox { Content = new TextBlock { Text = Loc.L("Ohne Nachfrage mit ausgeblendeter Anzeige aufnehmen, wenn die Unsichtbarkeit nicht bestätigt werden kann", "Record with hidden display without asking if invisibility cannot be confirmed"), TextWrapping = TextWrapping.Wrap }, IsChecked = s.InvisibleFallback == "hidden", Margin = new Thickness(0, 6, 0, 0),
            ToolTip = Loc.L("Z. B. unter Windows Server 2019: Leiste und Rahmen bleiben ausgeblendet, gesteuert wird über Tastenkürzel und das Symbol im Infobereich.", "E.g. on Windows Server 2019: bar and frame stay hidden; control via shortcuts and the notification area icon.") };
        var countdown = new ComboBox { Items = { Loc.L("aus", "off"), Loc.L("3 Sekunden", "3 seconds"), Loc.L("5 Sekunden", "5 seconds") }, SelectedIndex = s.Countdown >= 5 ? 2 : s.Countdown >= 3 ? 1 : 0 };
        var sounds = new ComboBox { Items = { Loc.L("nur bei ausgeblendeter Anzeige", "only with hidden display"), Loc.L("immer", "always"), Loc.L("nie", "never") }, SelectedIndex = s.SoundCues switch { "always" => 1, "never" => 2, _ => 0 } };
        int[] idleValues = { 0, 5, 10, 30 }, privValues = { 30, 60, 120 };
        var idle = new ComboBox { Items = { Loc.L("aus", "off"), Loc.L("nach 5 Minuten", "after 5 minutes"), Loc.L("nach 10 Minuten", "after 10 minutes"), Loc.L("nach 30 Minuten", "after 30 minutes") }, SelectedIndex = Math.Max(0, Array.IndexOf(idleValues, s.IdleReminderMinutes)) };
        var priv = new ComboBox { Items = { Loc.L("nach 30 Sekunden", "after 30 seconds"), Loc.L("nach 1 Minute", "after 1 minute"), Loc.L("nach 2 Minuten", "after 2 minutes") }, SelectedIndex = Math.Max(0, Array.IndexOf(privValues, s.PrivacyReminderSeconds)) };
        var hotkeys = new HotkeyEditor(s.EffectiveHotkeys());
        var glossary = new TextBox { Text = string.Join(Environment.NewLine, s.Glossary), AcceptsReturn = true, Height = 74, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top, FontFamily = Theme.Mono, FontSize = Theme.FsHint,
            ToolTip = Loc.L("Eine Ersetzung je Zeile, z. B.  Text-Editor=Editor  oder  SAP Easy Access=SAP-Startseite", "One replacement per line, e.g.  Text-Editor=Editor  or  SAP Easy Access=SAP-Startseite") };
        var cleanup = new CheckBox { Content = new TextBlock { Text = Loc.L("Neue Aufnahmen beim ersten Öffnen aufräumen (doppelte Klicks, Klick vor der Eingabe, Fensterwechsel hin und zurück)", "Clean up new recordings when first opened (double clicks, click before typing, switching windows back and forth)"), TextWrapping = TextWrapping.Wrap }, IsChecked = s.AutoCleanup, Margin = new Thickness(0, 8, 0, 0),
            ToolTip = Loc.L("Überflüssige Schritte werden nur ausgeblendet, nicht gelöscht – im Editor rückgängig mit Strg+Z oder über ⋯ › Aufräumen.", "Unnecessary steps are only hidden, not deleted – undo in the editor with Ctrl+Z or via ⋯ › Clean up.") };
        var updates = new CheckBox { Content = Loc.L("Beim Start nach einer neuen Version suchen", "Check for a new version at startup"), IsChecked = s.CheckUpdates, VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Loc.L("Fragt nur die öffentliche Release-Seite auf GitHub ab – ohne Kennung oder persönliche Daten. Heruntergeladen wird nichts.", "Only queries the public release page on GitHub – without any identifier or personal data. Nothing is downloaded.") };
        var checkNow = U.Btn(Theme.ISearch, Loc.L("Jetzt suchen", "Check now"), () => _ = UpdateCheck.CheckInteractiveAsync(), Loc.L("Einmalig auf GitHub nach einer neuen Version suchen", "Check GitHub once for a new version"), "Link");
        checkNow.Margin = new Thickness(12, 0, 0, 0);

        // ---------- Reiter ----------
        var general = U.Col(Two(U.Field(Loc.L("Darstellung", "Appearance"), theme, Loc.L("Gilt ab dem nächsten Start.", "Applies after the next start.")),
                                U.Field(Loc.L("Sprache der Oberfläche", "Interface language"), uiLang, Loc.L("Gilt ab dem nächsten Start.", "Applies after the next start.")),
                                U.Field(Loc.L("Sprache der Anleitungstexte", "Language of guide texts"), lang, Loc.L("Für neue Aufnahmen und Exporte; im Editor je Aufnahme umschaltbar.", "For new recordings and exports; switchable per recording in the editor.")),
                                U.Field(Loc.L("Anrede in Anleitungen", "Form of address (German guides)"), address)),
                            U.Field(Loc.L("Begriffe in automatischen Texten", "Terms in automatic texts"), glossary, Loc.L("„Fundstelle=Ersatz“ je Zeile – z. B. für Firmenbegriffe oder sprechende Namen von Programmen.", "\"Match=Replacement\" per line – e.g. for company terms or meaningful program names.")),
                            U.Row(updates, checkNow));
        var recording = U.Col(Two(U.Field(Loc.L("Schutzstufe", "Protection level"), level, Loc.L("Was bei Eingaben maskiert wird.", "What is masked when typing.")),
                                  U.Field(Loc.L("Countdown vor der Aufnahme", "Countdown before recording"), countdown, Loc.L("Bei ausgeblendeter Anzeige mindestens 3 Sekunden.", "At least 3 seconds with hidden display.")),
                                  U.Field(Loc.L("Signaltöne", "Sound cues"), sounds, Loc.L("Bei Start, Stopp, Pause, Markern und Erinnerungen.", "On start, stop, pause, markers and reminders.")),
                                  U.Field(Loc.L("Erinnerung, wenn keine Eingabe kommt", "Reminder when there is no input"), idle, Loc.L("Erinnert nur – die Aufnahme läuft weiter.", "Only reminds – recording continues.")),
                                  U.Field(Loc.L("Erinnerung „Privattaste noch an“", "Reminder \"privacy key still on\""), priv), new Border()), fallback, cleanup);
        var keys = U.Col(U.Hint(Loc.L("Diese Tastenkürzel gelten systemweit während der Aufnahme. „Sofort aufnehmen“ startet auch ohne geöffnetes Fenster eine Aufnahme.", "These shortcuts work system-wide during recording. \"Record now\" also starts a recording without an open window."), new Thickness(0, 0, 0, 12)), hotkeys);
        var custom = U.Col(U.Hint(Loc.L("Gilt nur für den Modus „Eigene“ – die anderen Modi haben einen festen Detailgrad.", "Applies to the \"Custom\" mode only – the other modes have a fixed level of detail."), new Thickness(0, 0, 0, 12)),
                           RecordChoice.Build(rec, null), U.Field(Loc.L("Zusatzbilder ohne Eingabe", "Extra images without input"), U.Col(change, changeHint), margin: new Thickness(0, 10, 0, 0)));

        var exchange = new WrapPanel { Margin = new Thickness(-3, 0, 0, 0) };
        exchange.Children.Add(U.Btn(Theme.IExport, Loc.L("Exportieren …", "Export …"), () => SettingsExchangeDialogs.Export(owner), Loc.L("Einstellungen, Tastenkürzel, Vorlagen und Ausschlussliste als Datei speichern – ohne persönliche Angaben", "Save settings, shortcuts, templates and exclusion list to a file – without personal data")));
        exchange.Children.Add(U.Btn(Theme.IOpen, Loc.L("Importieren …", "Import …"), () => { if (SettingsExchangeDialogs.Import(owner)) rebuild(current); }));
        exchange.Children.Add(U.Btn(Theme.IUndo, Loc.L("Zurücksetzen …", "Reset …"), () => { if (SettingsExchangeDialogs.Reset(owner)) rebuild(current); }, Loc.L("Auslieferungszustand – Aufnahmeordner und Zuletzt-Liste bleiben", "Factory settings – recordings folder and recent list stay")));
        if (SettingsExchange.CanUndo(AppPaths.SettingsFile))
            exchange.Children.Add(U.Btn(Theme.IHistory, Loc.L("Letzten Import zurücknehmen", "Undo last import"), () =>
            {
                if (Msg.Show(Loc.L("Einstellungen vor dem letzten Import oder Zurücksetzen wiederherstellen?", "Restore the settings from before the last import or reset?"), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                if (SettingsExchange.UndoLastImport(AppPaths.SettingsFile)) rebuild(current);
            }, null, "Ghost"));
        exchange.Children.Add(U.Btn(Theme.IWrench, Loc.L("Diagnose-Paket …", "Diagnostics package …"), () => DiagnosticsDialog.Create(owner), Loc.L("Protokoll und Systemangaben für eine Fehlermeldung – ohne Aufnahmen", "Log and system information for a bug report – without recordings"), "Ghost"));
        var storage = U.Col(U.Field(Loc.L("Aufnahmeordner", "Recordings folder"), U.Col(folderRow, warn)),
                            U.Field(Loc.L("Ausgeschlossene Programme", "Excluded programs"), excl, Loc.L("Ein Prozessname je Zeile – von diesen Programmen wird nichts aufgezeichnet.", "One process name per line – nothing is recorded from these programs.")),
                            U.Field(Loc.L("Einstellungen austauschen", "Exchange settings"), exchange),
                            Note(AppPaths.Portable ? Loc.F("Portabler Modus – Einstellungen liegen neben der EXE ({0}).", "Portable mode – settings are stored next to the EXE ({0}).", AppPaths.DataDir) : Loc.F("Einstellungen liegen in {0}.", "Settings are stored in {0}.", AppPaths.DataDir)));
        var pages = new UIElement[] { general, recording, keys, custom, storage };
        var (tabTrack, tabs) = U.Segmented("settab", current, (Theme.ISettings, Loc.L("Allgemein", "General")), (Theme.IRecord, Loc.L("Aufnahme", "Recording")), (Theme.IKeyboard, Loc.L("Tastenkürzel", "Shortcuts")),
            (Theme.IChecklist, Loc.L("Modus „Eigene“", "Custom mode")), (Theme.IFolder, Loc.L("Speicher und Schutz", "Storage and protection")));
        tabTrack.Margin = new Thickness(-4, 0, 0, 20);
        var host = new Grid();
        foreach (var pg in pages) { pg.Visibility = Visibility.Hidden; ((FrameworkElement)pg).VerticalAlignment = VerticalAlignment.Top; host.Children.Add(pg); }   // „Hidden“: alle Reiter gleich hoch
        pages[current].Visibility = Visibility.Visible;
        for (int t = 0; t < tabs.Length; t++) { int k = t; tabs[t].Checked += (_, _) => { foreach (var pg in pages) pg.Visibility = Visibility.Hidden; pages[k].Visibility = Visibility.Visible; current = k; }; }
        var body = U.Col(tabTrack, host);
        if (s.LoadNotice != null) body.Children.Insert(0, new Border { Background = Theme.WarnSoft, CornerRadius = new CornerRadius(Theme.RInset), Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 16), Child = U.Text(s.LoadNotice, Theme.FsHint, fg: Theme.Warn) });
        var saved = U.Text("", Theme.FsHint, fg: Theme.Muted, margin: new Thickness(14, 0, 0, 0)); saved.VerticalAlignment = VerticalAlignment.Center;
        var save = U.Primary(Loc.L("Speichern", "Save"), () =>
        {
            var hkIssues = hotkeys.Issues;
            if (hkIssues.Count > 0) { tabs[2].IsChecked = true; Msg.Show(Loc.L("Tastenkürzel prüfen\n\n", "Check shortcuts\n\n") + string.Join("\n", hkIssues.Select(i => i.Text).Distinct()), Title, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var newExcl = excl.Text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var removed = s.Excluded.Where(x => !newExcl.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
            if (removed.Count > 0 && Msg.Show(Loc.L("Schutz abschwächen?\n\nFolgende Ausschlüsse werden entfernt und deren Eingaben künftig aufgezeichnet:\n", "Weaken protection?\n\nThe following exclusions will be removed and their input recorded from now on:\n") + string.Join(", ", removed), Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (level.SelectedIndex == 0 && s.PrivacyLevel == "strict" && Msg.Show(Loc.L("Schutz abschwächen?\n\nSchutzstufe von „Streng“ auf „Schutznetz“ senken?", "Weaken protection?\n\nLower the protection level from \"strict\" to \"safety net\"?"), Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            string newTheme = theme.SelectedIndex switch { 1 => "light", 2 => "system", _ => "dark" };
            string newUi = uiLang.SelectedIndex switch { 1 => "de", 2 => "en", _ => "system" };
            bool restart = newTheme != s.Theme || newUi != s.UiLanguage;
            s.Language = lang.SelectedIndex == 1 ? "en" : "de"; s.PrivacyLevel = level.SelectedIndex == 1 ? "strict" : "balanced";
            s.UiLanguage = newUi; s.Theme = newTheme;
            s.ChangeFrames = change.SelectedIndex switch { 0 => "few", 2 => "many", _ => "normal" };
            s.SessionsFolder = folder.Text.Trim(); s.Address = address.SelectedIndex == 1 ? "informal" : "formal";
            s.Record = rec;
            s.InvisibleFallback = fallback.IsChecked == true ? "hidden" : "ask";
            s.Countdown = countdown.SelectedIndex switch { 1 => 3, 2 => 5, _ => 0 };
            s.SoundCues = sounds.SelectedIndex switch { 1 => "always", 2 => "never", _ => "hidden" };
            s.IdleReminderMinutes = idleValues[Math.Max(0, idle.SelectedIndex)];
            s.PrivacyReminderSeconds = privValues[Math.Max(0, priv.SelectedIndex)];
            s.Glossary = glossary.Text.Split('\n').Select(x => x.Trim()).Where(x => x.Contains('=') && x.Length > 2).ToList();
            s.AutoCleanup = cleanup.IsChecked == true; s.CheckUpdates = updates.IsChecked == true;
            s.Excluded = newExcl;
            s.Hotkeys = new Dictionary<string, string>(hotkeys.Map);
            s.LoadNotice = null;
            if (!s.Save()) { Msg.Show(Loc.L("Einstellungen nicht gespeichert\n\n", "Settings not saved\n\n") + s.LastSaveError, Title, MessageBoxButton.OK, MessageBoxImage.Error); return; }
            if (restart) Msg.Show(Loc.L("Gilt ab dem nächsten Start\n\nDarstellung und Sprache der Oberfläche ändern sich, wenn das Programm neu gestartet wird.", "Applies after the next start\n\nAppearance and interface language change when the program is restarted."), Title, MessageBoxButton.OK, MessageBoxImage.Information);
            saved.Text = Loc.L("Gespeichert", "Saved");
        });
        var discard = U.Btn(Loc.L("Verwerfen", "Discard"), () => rebuild(current), Loc.L("Nicht gespeicherte Änderungen zurücknehmen", "Undo unsaved changes"));
        var foot = U.Row(save, discard, saved);
        foot.Margin = new Thickness(-3, 8, 0, 0);
        page.Children.Add(U.Heading(Title, Theme.FsHero, new Thickness(0, 0, 0, 16)));
        page.Children.Add(body);
        page.Children.Add(foot);
        return page;
    }
}
