using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Ersteinrichtung beim allerersten Start (noch keine Einstellungsdatei): Sprache der Oberfläche und der Anleitungen,
/// Darstellung, Anrede, Ordner für Aufnahmen, Schutzstufe, Countdown, Töne und einige Verhaltensweisen. Alles lässt sich
/// später unter „Einstellungen“ ändern. Ein Wechsel der Oberflächensprache baut den Dialog sofort neu auf.
/// </summary>
sealed class FirstRunDialog : DialogWindow
{
    readonly AppSettings _s;
    public bool LanguageChanged { get; private set; }

    readonly ComboBox _ui = new(), _theme = new(), _lang = new(), _address = new(), _level = new(), _countdown = new(), _sounds = new();
    readonly TextBox _folder = new();
    readonly CheckBox _cleanup = new(), _keep = new(), _tray = new(), _updates = new();

    public FirstRunDialog(AppSettings s)
    {
        _s = s;
        Title = AppInfo.DisplayName; Width = 640; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.NoResize;
        Theme.Prepare(this, Theme.Surface);

        static ComboBox Fill(ComboBox c, int index, params string[] items) { foreach (var i in items) c.Items.Add(i); c.SelectedIndex = index; c.MinWidth = 220; c.HorizontalAlignment = HorizontalAlignment.Left; return c; }
        Fill(_ui, s.UiLanguage switch { "de" => 1, "en" => 2, _ => 0 }, Loc.L("Wie Windows", "Like Windows"), "Deutsch", "English");
        _ui.SelectionChanged += (_, _) =>
        {
            // Oberfläche sofort in der gewählten Sprache zeigen
            Read();
            LanguageChanged = true;
            DialogResult = false;
        };
        Fill(_theme, s.Theme switch { "light" => 1, "system" => 2, _ => 0 }, Loc.L("Dunkel", "Dark"), Loc.L("Hell", "Light"), Loc.L("Wie Windows", "Like Windows"));
        Fill(_lang, s.Language == "en" ? 1 : 0, "Deutsch", "English");
        Fill(_address, s.Address == "informal" ? 1 : 0, Loc.L("Sie („Klicken Sie auf …“)", "Formal German (“Sie”)"), Loc.L("du („Klicke auf …“)", "Informal German (“du”)"));
        Fill(_level, s.PrivacyLevel == "strict" ? 1 : 0, Loc.L("Schutznetz – Passwortfelder maskieren", "Safety net – mask password fields"), Loc.L("Streng – auch unbekannte Felder, Terminal und Fernzugriff maskieren", "Strict – also mask unknown fields, terminal and remote access"));
        Fill(_countdown, s.Countdown >= 5 ? 2 : s.Countdown >= 3 ? 1 : 0, Loc.L("aus", "off"), Loc.L("3 Sekunden", "3 seconds"), Loc.L("5 Sekunden", "5 seconds"));
        Fill(_sounds, s.SoundCues switch { "always" => 1, "never" => 2, _ => 0 }, Loc.L("nur bei ausgeblendeter Anzeige", "only with hidden display"), Loc.L("immer", "always"), Loc.L("nie", "never"));
        _folder.Text = s.SessionsFolder ?? AppPaths.DefaultSessionsFolder();
        var browse = U.Btn(Theme.IFolder, Loc.L("Wählen …", "Choose …"), () =>
        {
            using var d = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = _folder.Text, UseDescriptionForTitle = true, Description = Loc.L("Ordner für Aufnahmen", "Folder for recordings") };
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) _folder.Text = d.SelectedPath;
        });
        browse.Margin = new Thickness(6, 0, 0, 0);
        var folderRow = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(_folder);

        CheckBox Check(CheckBox c, string text, bool on) { c.Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }; c.IsChecked = on; c.Margin = new Thickness(0, 0, 0, 8); return c; }
        Check(_cleanup, Loc.L("Neue Aufnahmen automatisch aufräumen (doppelte Klicks und Fensterwechsel ausblenden)", "Clean up new recordings automatically (hide double clicks and window switches)"), s.AutoCleanup);
        Check(_keep, Loc.L("Beim Schließen einer neuen Aufnahme fragen, ob sie behalten oder verworfen wird", "When closing a new recording, ask whether to keep or discard it"), s.AskKeepOnClose);
        Check(_tray, Loc.L("Nach dem Schließen im Infobereich bereit bleiben (Sofortaufnahme per Tastenkürzel)", "Stay ready in the notification area after closing (instant recording via shortcut)"), s.StayInTray);
        Check(_updates, Loc.L("Beim Start nach einer neuen Version suchen", "Check for a new version at startup"), s.CheckUpdates);
        if (!UpdateCheck.Available) _updates.Visibility = Visibility.Collapsed;

        Grid Two(UIElement a, UIElement b)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(b, 2); g.Children.Add(a); g.Children.Add(b);
            return g;
        }
        var body = new StackPanel { Children =
        {
            U.Para(Loc.L("Willkommen! Ein paar Einstellungen für den Start – alles lässt sich später unter „Einstellungen“ ändern.", "Welcome! A few settings to get started – everything can be changed later under “Settings”.")),
            new Border { Height = 12 },
            Two(U.Field(Loc.L("Sprache des Programms", "Program language"), _ui), U.Field(Loc.L("Darstellung", "Appearance"), _theme)),
            Two(U.Field(Loc.L("Sprache der Anleitungen", "Language of the guides"), _lang), U.Field(Loc.L("Anrede in deutschen Anleitungen", "Form of address in German guides"), _address)),
            U.Field(Loc.L("Ordner für Aufnahmen", "Folder for recordings"), folderRow, Loc.L("Ein Ordner, der nicht synchronisiert wird (OneDrive, Nextcloud), ist am schnellsten.", "A folder that is not synced (OneDrive, Nextcloud) is fastest.")),
            Two(U.Field(Loc.L("Schutzstufe bei Eingaben", "Protection level for typing"), _level), U.Field(Loc.L("Countdown vor der Aufnahme", "Countdown before recording"), _countdown)),
            U.Field(Loc.L("Signaltöne", "Sound cues"), _sounds),
            _cleanup, _keep, _tray, _updates,
        } };
        U.Dialog(this, Loc.L("Ersteinrichtung", "Initial setup"), body,
            U.Primary(Loc.L("Los geht’s", "Let’s go"), () => { Read(); DialogResult = true; }),
            U.Btn(Loc.L("Standardwerte", "Defaults"), () => DialogResult = true));
    }

    /// <summary>Auswahl in die Einstellungen übernehmen (noch nicht gespeichert).</summary>
    void Read()
    {
        var s = _s;
        s.UiLanguage = _ui.SelectedIndex switch { 1 => "de", 2 => "en", _ => "system" };
        s.Theme = _theme.SelectedIndex switch { 1 => "light", 2 => "system", _ => "dark" };
        s.Language = _lang.SelectedIndex == 1 ? "en" : "de";
        s.Address = _address.SelectedIndex == 1 ? "informal" : "formal";
        s.PrivacyLevel = _level.SelectedIndex == 1 ? "strict" : "balanced";
        s.Countdown = _countdown.SelectedIndex switch { 1 => 3, 2 => 5, _ => 0 };
        s.SoundCues = _sounds.SelectedIndex switch { 1 => "always", 2 => "never", _ => "hidden" };
        string f = _folder.Text.Trim();
        s.SessionsFolder = f.Length == 0 || string.Equals(f, AppPaths.DefaultSessionsFolder(), StringComparison.OrdinalIgnoreCase) ? null : f;
        s.AutoCleanup = _cleanup.IsChecked == true; s.AskKeepOnClose = _keep.IsChecked == true; s.StayInTray = _tray.IsChecked == true; s.CheckUpdates = _updates.IsChecked == true;
    }

    /// <summary>
    /// Beim ersten Start zeigen (keine Einstellungsdatei; nicht für Kommandozeilenbefehle). Speichert immer – auch „Standardwerte“ –,
    /// damit die Frage nicht wiederkommt. Liefert true, wenn sich Darstellung oder Oberflächensprache geändert haben.
    /// </summary>
    public static bool ShowIfFirstRun(bool firstRun)
    {
        if (!firstRun && Environment.GetEnvironmentVariable("STEPRECORDER_FIRSTRUN") != "1") return false;
        var s = AppSettings.Load();
        string theme0 = s.Theme, ui0 = s.UiLanguage;
        while (true)
        {
            var d = new FirstRunDialog(s);
            d.ShowDialog();
            if (!d.LanguageChanged) break;
            Loc.Init(s.UiLanguage, applyToThreads: true);   // neu aufbauen in der gewählten Sprache
        }
        if (s.SessionsFolder is { } f) { try { Directory.CreateDirectory(f); } catch { s.SessionsFolder = null; } }
        s.Save();
        return s.Theme != theme0 || s.UiLanguage != ui0;
    }
}
