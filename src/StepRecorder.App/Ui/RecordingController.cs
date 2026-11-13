using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using StepRecorder.Capture;
using StepRecorder.Native;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>Verbindet Recorder, Aufnahmeleiste, Rahmen und Tastenkürzel.</summary>
sealed class RecordingController
{
    readonly RecordingOptions _o;
    Recorder _rec = null!;
    RecordingBar? _bar;
    FrameWindow? _frame;
    Hotkeys? _hk;
    TrayIcon? _tray;
    string _trayState = "";
    readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    DateTime _started;
    TimeSpan _pausedTotal; DateTime? _pausedAt;
    public event Action<string?>? Finished;
    bool _discard;
    /// <summary>Ausweichmodus: Leiste und Rahmen bleiben unsichtbar, gesteuert wird nur über Tastenkürzel.</summary>
    bool _hiddenMode;
    /// <summary>Signaltöne bei Start, Stopp, Pause, Marker.</summary>
    bool _sounds;
    /// <summary>Infobereich-Symbol blinkt bis zu diesem Zeitpunkt (Erinnerung).</summary>
    DateTime _remindUntil;

    /// <summary>Tastenkürzel einer Aktion zur Anzeige („Strg+Umschalt+F10“), leer ohne Kürzel.</summary>
    public static string Key(string id) => Loc.Keys(AppSettings.Load().Hotkey(id));
    static string WithKey(string text, string id) => Key(id) is { Length: > 0 } k ? $"{text} ({k})" : text;

    public RecordingController(RecordingOptions o) { _o = o; }

    public bool Start()
    {
        bool selfTest = Invisibility.SelfTest();
        if (!selfTest)
        {
            if (AppSettings.Load().InvisibleFallback == "hidden") _hiddenMode = true;
            else
            {
                var dlg = new HiddenModeDialog();
                if (dlg.ShowDialog() != true) return false;
                _hiddenMode = dlg.Hidden;
            }
        }
        // Countdown vor dem Start (im Ausweichmodus immer, damit klar ist, wann es losgeht)
        var settings = AppSettings.Load();
        _sounds = settings.SoundCues == "always" || (settings.SoundCues != "never" && _hiddenMode);
        int countdown = Math.Max(settings.Countdown, _hiddenMode ? 3 : 0);
        if (countdown > 0)
        {
            var frame = new DispatcherFrame();
            var cd = new CountdownWindow(countdown, Loc.L("Aufnahme startet in", "Recording starts in"),
                _hiddenMode ? Loc.F("Ohne sichtbare Anzeige. Beenden: {0} · Anzeige: {1}", "Without visible display. Stop: {0} · Display: {1}", Key("stop"), Key("bar")) : null);
            bool cancelled = true;
            cd.Finished += () => { cancelled = false; frame.Continue = false; };
            cd.Closed += (_, _) => frame.Continue = false;
            cd.Show();
            Dispatcher.PushFrame(frame);
            if (cancelled) return false;
        }
        _rec = new Recorder(_o) { SelfTestPassed = selfTest, SelfTestMode = selfTest ? "excluded" : _hiddenMode ? "fallbackHidden" : "fallbackVisible" };
        _rec.StateChanged += s => Application.Current.Dispatcher.BeginInvoke(() => OnState(s));
        _rec.ActionLine += t => Application.Current.Dispatcher.BeginInvoke(() => _bar?.Message(t));
        _rec.Warning += t => Application.Current.Dispatcher.BeginInvoke(() => _bar?.Message(t, true));
        _rec.TerminalPromptChanged += on => Application.Current.Dispatcher.BeginInvoke(() => OnTerminalPrompt(on));
        _rec.Reminder += (kind, n) => Application.Current.Dispatcher.BeginInvoke(() => Remind(kind, n));
        _rec.ResumeConfirmationNeeded += () => Application.Current.Dispatcher.BeginInvoke(() =>
            _bar?.Message(Loc.L("Aufnahme pausiert (Sperre/Benutzerwechsel) – mit Fortsetzen weiter.", "Recording paused (lock/user switch) – continue with Resume."), true, sticky: true));
        _bar = new RecordingBar(this);
        if (!_hiddenMode) _bar.Show();
        try
        {
            string exe = Environment.ProcessPath ?? "";
            _rec.Start(exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && System.IO.Path.GetFileName(exe).Contains("StepRecorder", StringComparison.OrdinalIgnoreCase) ? exe : null);
        }
        catch (Exception ex)
        {
            _bar.Close(); _bar = null;
            UiApp.ShowError(ex);
            return false;
        }
        _started = DateTime.Now;
        CreateTray();
        if (_sounds) Tones.Start();
        if (!_hiddenMode) ShowFrame();
        RegisterHotkeys();
        _tick.Tick += (_, _) => UpdateBar();
        _tick.Start();
        string protection = _o.PrivacyLevel == "balanced"
            ? Loc.F("Schutznetz – Passwortfelder werden maskiert; Eingaben im Terminal und in Remote-Desktop müssen vor der Weitergabe durchgesehen werden (Privattaste {0}).", "Safety net – password fields are masked; input in terminals and remote desktops must be reviewed before sharing (privacy key {0}).", Key("privacy"))
            : Loc.L("Streng – Eingaben in unbekannten Feldern, Terminals und Remote-Desktop werden maskiert.", "Strict – input in unknown fields, terminals and remote desktops is masked.");
        // Nie unbemerkt schwächer (10 §4.4): Abweichungen und mitgelesene Quellen in der Startmeldung und dauerhaft in der Leiste
        var weak = _o.Weakens(_o.SettingsPrivacyLevel ?? AppSettings.Load().PrivacyLevel);
        string weakText = weak.Count == 0 ? "" : " · " + Loc.F("Schwächer als die Einstellungen: {0}", "Less protected than the settings: {0}", string.Join(", ", weak.Select(w => RecordingOptions.WeakenName(w, Loc.Lang))))
            + (_rec.LogFiles.Count > 0 ? Loc.F(" ({0})", " ({0})", string.Join(", ", _rec.LogFiles)) : _o.LogFiles.Count > 0 && _o.ContentSources().Contains("logFiles") ? Loc.L(" (keine Protokolldatei gefunden)", " (no log file found)") : "");
        if (_o.ContentSources() is { Count: > 0 } src) _bar.SetSources(Loc.F("Liest mit: {0} – vor der Weitergabe durchsehen", "Reading along: {0} – review before sharing", string.Join(", ", src.Select(w => RecordingOptions.WeakenName(w, Loc.Lang)))));
        _bar.Message((selfTest ? Loc.L("Unsichtbarkeit geprüft · ", "Invisibility checked · ") : _hiddenMode ? Loc.L("Ausgeblendete Anzeige – Achtung: eingeblendet ist die Leiste auf den Bildern zu sehen · ", "Hidden display – note: when shown, the bar appears in the images · ") : Loc.L("Unsichtbarkeit nicht bestätigt · ", "Invisibility not confirmed · ")) + protection + weakText, !selfTest || weak.Count > 0);
        return true;
    }

    void ShowFrame()
    {
        System.Drawing.Rectangle r;
        if (_o.Scope == ScopeMode.Region && _o.Region is { } reg) r = reg;
        else
        {
            var mons = ScreenGrabber.GetMonitors();
            Win32.GetCursorPos(out var p);
            var m = mons.FirstOrDefault(x => x.Bounds.Contains(p.X, p.Y)) ?? mons[0];
            r = _o.Monitors == "all" ? ScreenGrabber.VirtualScreen(mons) : m.Bounds;
        }
        _frame = new FrameWindow(r);
        _frame.Show();
    }

    /// <summary>Aktion je Tastenkürzel (Kennungen aus <see cref="HotkeyActions"/>).</summary>
    Action ActionFor(string id) => id switch
    {
        "instantPause" => TogglePause, "stop" => Stop, "snapshot" => () => Marker("snapshot"), "comment" => Comment, "privacy" => TogglePrivacy,
        "marker" => () => Marker("note"), "discard" => DiscardPrevious, "bar" => ToggleBar, "markerError" => () => Marker("error"),
        "markerExpected" => () => Marker("expected"), "dictation" => ToggleDictation, "screenshot" => () => Marker("snapshot"), _ => () => { }
    };

    void RegisterHotkeys()
    {
        _hk = new Hotkeys();
        var s = AppSettings.Load();
        foreach (var a in HotkeyActions.All)
            if (s.Hotkey(a.Id) is { Length: > 0 } combo) _hk.Register(a.Id, combo, ActionFor(a.Id));
        if (_hk.Failed.Count > 0)
            _bar?.Message(Loc.L("Tastenkürzel belegt: ", "Shortcuts taken: ") + string.Join(", ", _hk.Failed.Select(id => $"{Key(id)} ({HotkeyActions.Label(HotkeyActions.ById(id)!)})")), true);
    }

    /// <summary>Leiste ein/aus; im Ausweichmodus mit ihr auch den Rahmen (beide wären auf den Bildern zu sehen).</summary>
    void ToggleBar()
    {
        if (_bar == null) return;
        if (!_bar.IsVisible)
        {
            if (!_bar.IsLoaded) _bar.Show(); else _bar.Visibility = Visibility.Visible;
            if (_hiddenMode) { if (_frame == null) ShowFrame(); else _frame.Visibility = Visibility.Visible; _bar.Message(Loc.F("Achtung: Leiste und Rahmen sind jetzt auf den Bildern zu sehen – {0} blendet sie wieder aus.", "Note: bar and frame now appear in the images – {0} hides them again.", Key("bar")), true); }
        }
        else
        {
            _bar.Visibility = Visibility.Hidden;
            if (_hiddenMode && _frame != null) _frame.Visibility = Visibility.Hidden;
        }
    }

    void UpdateBar()
    {
        var elapsed = (_pausedAt ?? DateTime.Now) - _started - _pausedTotal;
        _bar?.SetTime(elapsed, _rec.ActionCount, _rec.State, Private);
        if (_tray != null)
        {
            string state = _rec.State == RecorderState.Paused ? "paused" : Private ? "private" : "rec";
            if (DateTime.Now < _remindUntil && DateTime.Now.Millisecond < 500) state = "remind";
            if (state != _trayState) { _trayState = state; _tray.SetIcon(TrayIcon.Render(state)); }
            string what = _rec.State == RecorderState.Paused ? Loc.L("pausiert", "paused") : Private ? Loc.L("läuft (privat)", "running (private)") : Loc.L("läuft", "running");
            _tray.SetTip(StepRecorder.Data.AppInfo.DisplayName + "\n" + Loc.F("Aufnahme {0} · {1} · {2}", "Recording {0} · {1} · {2}", what, elapsed.ToString("mm\\:ss"), Loc.N(_rec.ActionCount, "1 Schritt", "{0} Schritte", "1 step", "{0} steps")) + "\n" + Loc.L("Klick: Leiste · Rechtsklick: Menü", "Click: bar · Right-click: menu"));
        }
    }

    /// <summary>Erinnerung: bleibende Zeile in der Leiste, blinkendes Symbol, Ton bei ausgeblendeter Leiste. Keine Windows-Benachrichtigung (wäre im Bild).</summary>
    void Remind(ReminderKind kind, int n)
    {
        string text = kind == ReminderKind.StillRecording
            ? Loc.F("Aufnahme läuft noch – seit {0} Minuten keine Eingabe. Beenden: {1}", "Recording is still running – no input for {0} minutes. Stop: {1}", n, Key("stop"))
            : Loc.F("Privatmodus ist noch aktiv – Eingaben werden nicht gespeichert. Ausschalten: {0}", "Private mode is still on – input is not being saved. Turn off: {0}", Key("privacy"));
        _bar?.Message(text, true, sticky: true);
        _remindUntil = DateTime.Now.AddSeconds(10);
        bool barHidden = _bar == null || !_bar.IsVisible;
        if (AppSettings.Load().SoundCues != "never" && (barHidden || _sounds)) Tones.Remind();
        if (barHidden && !_hiddenMode) _frame?.ShowTag(text, 10);
        UiApp.Log("Erinnerung: " + kind);
    }

    /// <summary>Symbol im Infobereich: bedienbar wie die Leiste – auch wenn sie ausgeblendet oder minimiert ist.</summary>
    void CreateTray()
    {
        try
        {
            _tray = new TrayIcon(TrayIcon.Render("rec"), StepRecorder.Data.AppInfo.DisplayName + Loc.L(" – Aufnahme läuft", " – recording"));
            _trayState = "rec";
            _tray.LeftClick += ToggleBar;
            _tray.RightClick += ShowTrayMenu;
            _rec.IsRecorderControl = (x, y) => _tray?.Contains(x, y) == true;
            UiApp.Log("Infobereich-Symbol: " + (_tray.Added ? "angelegt" : "nicht angelegt"));
        }
        catch (Exception ex) { UiApp.Log("Infobereich-Symbol: " + ex.Message); _tray = null; }
    }

    void ShowTrayMenu()
    {
        var elapsed = (_pausedAt ?? DateTime.Now) - _started - _pausedTotal;
        bool paused = _rec.State == RecorderState.Paused;
        // dieselben Gruppen wie das Menü „⋯“ der Leiste – und vollständig, damit alles auch ohne Leiste erreichbar ist
        var m = U.MenuScope(new ContextMenu());
        void Add(string head, string id, Action a, string? glyph = null) => m.Items.Add(U.Mi(head, id.Length > 0 ? Key(id) : null, a, glyph));
        m.Items.Add(new MenuItem { Header = Loc.F("Aufnahme {0} · {1} · {2}", "Recording {0} · {1} · {2}", paused ? Loc.L("pausiert", "paused") : Loc.L("läuft", "running"), elapsed.ToString("mm\\:ss"), Loc.N(_rec.ActionCount, "1 Schritt", "{0} Schritte", "1 step", "{0} steps")), IsEnabled = false });
        m.Items.Add(new Separator());
        Add(paused ? Loc.L("Fortsetzen", "Resume") : Loc.L("Pause", "Pause"), "instantPause", TogglePause, paused ? Theme.IPlay : Theme.IPause);
        Add(Loc.L("Aufnahme beenden", "Stop recording"), "stop", Stop, Theme.IStop);
        m.Items.Add(new Separator());
        Add(Loc.L("Kommentar …", "Comment …"), "comment", Comment, Theme.IComment);
        Add(_dict.Active ? Loc.L("Diktat beenden", "Stop dictation") : Loc.L("Diktat starten (Mikrofon)", "Start dictation (microphone)"), "dictation", ToggleDictation, "\uE720");
        m.Items.Add(MarkerMenu());
        Add(Loc.L("Schnappschuss", "Snapshot"), "snapshot", () => Marker("snapshot"), Theme.ICamera);
        m.Items.Add(new Separator());
        Add(_rec.PrivacyMode ? Loc.L("Privattaste aus", "Privacy key off") : Loc.L("Privattaste ein", "Privacy key on"), "privacy", TogglePrivacy, Theme.IUnlock);
        Add(Loc.L("Letzten Schritt zurücknehmen", "Undo last step"), "discard", DiscardPrevious, Theme.IUndo);
        Add(Loc.L("Letzte Eingabe schwärzen", "Redact last input"), "", RedactLastInput, Theme.IShield);
        m.Items.Add(new Separator());
        Add(_bar?.IsVisible == true ? Loc.L("Leiste ausblenden", "Hide bar") : Loc.L("Leiste anzeigen", "Show bar"), "bar", ToggleBar);
        Add(Loc.L("Tastenkürzel …", "Shortcuts …"), "", ShowShortcuts, Theme.IKeyboard);
        m.Items.Add(new Separator());
        Add(Loc.L("Aufnahme verwerfen …", "Discard recording …"), "", Discard, Theme.IDelete);
        m.Placement = PlacementMode.MousePoint;
        m.IsOpen = true;
    }

    /// <summary>Untermenü „Marker setzen“: Notiz, Fehler, Erwartet, Wichtig und Marker mit eigenem Text.</summary>
    internal MenuItem MarkerMenu()
    {
        string? G(string id) => Key(id) is { Length: > 0 } k ? k : null;
        var sub = U.Mi(Loc.L("Marker setzen", "Set marker"), null, null, Theme.IFlag);
        sub.Items.Add(U.Mi(Loc.L("Notiz", "Note"), G("marker"), () => Marker("note")));
        sub.Items.Add(U.Mi(Loc.L("Fehler", "Error"), G("markerError"), () => Marker("error"), Theme.IError));
        sub.Items.Add(U.Mi(Loc.L("Erwartet", "Expected"), G("markerExpected"), () => Marker("expected"), Theme.ICheck));
        sub.Items.Add(U.Mi(Loc.L("Wichtig", "Important"), null, () => Marker("important"), Theme.IImportant));
        sub.Items.Add(new Separator());
        sub.Items.Add(U.Mi(Loc.L("Marker mit Text …", "Marker with text …"), null, () => { var d = new CommentWindow(Loc.L("Marker-Text", "Marker text")); if (d.ShowDialog() == true) Marker("important", d.Text); }));
        return sub;
    }

    /// <summary>Leiste in den Infobereich minimieren.</summary>
    public void MinimizeToTray()
    {
        if (_bar?.IsVisible == true) ToggleBar();
    }

    void OnState(RecorderState s)
    {
        if (s == RecorderState.Paused) { _pausedAt ??= DateTime.Now; _frame?.SetStyle(Theme.Night.Muted, dashed: true); }
        else if (s == RecorderState.Recording)
        {
            if (_pausedAt != null) { _pausedTotal += DateTime.Now - _pausedAt.Value; _pausedAt = null; }
            _frame?.SetStyle(FrameWindow.RecBrush, dashed: Private);
        }
        UpdateBar();
    }

    public void TogglePause() { if (_sounds) Tones.Pause(); if (_rec.State == RecorderState.Paused) _rec.Resume(); else _rec.Pause(); }

    /// <summary>Privatmodus aus irgendeinem Grund: Privattaste oder erkannte Passwortabfrage im Terminal.</summary>
    bool Private => _rec.PrivacyMode || _rec.TerminalPromptActive;

    /// <summary>Erkannte Passwortabfrage im Terminal (10 §6.2): Leiste und Rahmen wie im Privatmodus, mit „Passwortabfrage erkannt“.</summary>
    void OnTerminalPrompt(bool on)
    {
        _frame?.SetStyle(FrameWindow.RecBrush, dashed: Private);
        string text = on ? Loc.L("Passwortabfrage erkannt – Eingabe wird maskiert bis zur Eingabetaste.", "Password prompt detected – input is masked until Enter.")
                         : Loc.L("Passwortabfrage beendet.", "Password prompt ended.");
        _bar?.Message(text, on, sticky: on);
        if (on && (_bar == null || !_bar.IsVisible) && !_hiddenMode) _frame?.ShowTag(text, 5);
        UpdateBar();
    }

    public void TogglePrivacy()
    {
        _rec.TogglePrivacy();
        _frame?.SetStyle(FrameWindow.RecBrush, dashed: Private);
        _bar?.Message(_rec.PrivacyMode ? Loc.L("Privat – Eingaben werden nicht gespeichert.", "Private – input is not being saved.") : Loc.L("Privatmodus aus.", "Private mode off."), _rec.PrivacyMode);
        UpdateBar();
    }

    int _markers;
    public void Marker(string kind, string? label = null)
    {
        _rec.AddMarker(kind, label);
        if (_sounds) Tones.Tick();
        if (kind != "snapshot") _markers++;
        string kindName = kind switch { "error" => Loc.L("Fehler", "error"), "expected" => Loc.L("Erwartet", "expected"), "important" => Loc.L("Wichtig", "important"), _ => Loc.L("Notiz", "note") };
        _bar?.Message(kind == "snapshot" ? Loc.L("Schnappschuss gespeichert", "Snapshot saved") : Loc.F("Marker {0} ({1}) gesetzt", "Marker {0} ({1}) set", _markers, kindName));
    }

    public void DiscardPrevious() => _rec.DiscardPrevious();
    public void RedactLastInput() => _rec.RedactLastInput();

    public void Comment()
    {
        nint prevFg = Win32.GetForegroundWindow();
        var (t, frame) = _rec.FreezeForComment();
        var dlg = new CommentWindow();
        if (dlg.ShowDialog() == true && dlg.Text.Trim().Length > 0)
        {
            _rec.AddComment(dlg.Text.Trim(), t, frame);
            _bar?.Message(Loc.L("Kommentar gespeichert", "Comment saved"));
        }
        if (prevFg != 0) SetForegroundWindow(prevFg);
    }

    // ---------------- Diktat ----------------
    readonly Dictation _dict = new();
    long _dictT = -1;
    public bool Dictating => _dict.Active;

    /// <summary>Diktat ein/aus: Gesprochenes wird offline erkannt und als Kommentar an der Stelle gespeichert, an der der Satz begann.</summary>
    public void ToggleDictation()
    {
        if (_dict.Active)
        {
            _dict.Stop();
            _bar?.Message(Loc.L("Diktat beendet – Mikrofon aus.", "Dictation stopped – microphone off."));
            _bar?.SetDictation(false);
            return;
        }
        var disp = Application.Current.Dispatcher;
        var err = _dict.Start(AppSettings.Load().Language,
            onStart: () => disp.BeginInvoke(() => { if (_dictT < 0) _dictT = _rec.Clock; }),
            onText: text => disp.BeginInvoke(() =>
            {
                long t = _dictT >= 0 ? _dictT : _rec.Clock; _dictT = -1;
                if (_rec.State != RecorderState.Recording) return;
                if (_rec.PrivacyMode) { _bar?.Message(Loc.L("Privattaste aktiv – Diktat nicht gespeichert.", "Privacy key active – dictation not saved."), true); return; }
                _rec.AddComment(text, t, null);
                _bar?.Message(Loc.L("Diktiert: ", "Dictated: ") + (text.Length > 70 ? text[..69] + "…" : text));
            }));
        if (err != null) { _bar?.Message(err, true); Msg.Show(Loc.L("Diktat nicht möglich\n\n", "Dictation not possible\n\n") + err, Loc.L("Diktat", "Dictation"), MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (_sounds) Tones.Tick();
        _bar?.SetDictation(true);
        _bar?.Message(Key("dictation") is { Length: > 0 } k
            ? Loc.F("Diktat läuft ({0}) – sprechen Sie; jeder Satz wird ein Kommentar. {1} beendet.", "Dictation running ({0}) – speak; each sentence becomes a comment. {1} stops.", _dict.Culture, k)
            : Loc.F("Diktat läuft ({0}) – sprechen Sie; jeder Satz wird ein Kommentar.", "Dictation running ({0}) – speak; each sentence becomes a comment.", _dict.Culture));
    }

    public void ShowRunningNotice() => _bar?.Message(Loc.L("Es läuft bereits eine Aufnahme.", "A recording is already in progress."), true);

    /// <summary>Fataler Programmfehler: Aufnahme so weit wie möglich abschließen (Recorder.Dispose stoppt mit Grund „error“).</summary>
    public void EmergencyStop() { try { _dict.Stop(); } catch { } try { _rec?.Dispose(); } catch { } }

    /// <summary>Tastenkürzel während der Aufnahme (dieselbe Liste wie auf der Startseite „Hilfe“).</summary>
    public void ShowShortcuts() => Shortcuts.ShowWindow(null, Shortcuts.Recording(), Loc.L("Tastenkürzel während der Aufnahme", "Shortcuts while recording"));

    public void Discard()
    {
        bool bin = _rec.Folder is { } f && RecycleBin.LikelyAvailable(f);
        string text = bin
            ? Loc.L("Aufnahme verwerfen?\n\nDer Aufnahmeordner wird in den Papierkorb verschoben.", "Discard recording?\n\nThe recording folder will be moved to the Recycle Bin.")
            : Loc.L("Aufnahme verwerfen?\n\nDieses Laufwerk hat keinen Papierkorb – die Aufnahme wird endgültig gelöscht. Das lässt sich nicht rückgängig machen.", "Discard recording?\n\nThis drive has no Recycle Bin – the recording will be deleted permanently. This cannot be undone.");
        if (Msg.Show(text, Loc.L("Aufnahme verwerfen", "Discard recording"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _discard = true;
        _allowPermanent = !bin;
        Stop();
    }

    bool _allowPermanent;

    bool _stopping;
    public void Stop()
    {
        if (_stopping) return;
        _stopping = true;
        if (_sounds) Tones.Stop();
        _tick.Stop();
        _hk?.Dispose();
        _dict.Stop();
        _bar?.Message(Loc.L("Wird verarbeitet …", "Processing …"), sticky: true);
        var rec = _rec;
        bool discard = _discard, allowPermanent = _allowPermanent;
        Task.Run(() =>
        {
            string? folder = null;
            try
            {
                if (discard)
                {
                    var r = rec.Discard(allowPermanent, out var kept);
                    if (r != RecycleResult.Recycled)
                        Application.Current.Dispatcher.Invoke(() => Msg.Show(
                            Loc.F("Aufnahme nicht gelöscht\n\nDer Ordner konnte nicht in den Papierkorb verschoben werden und bleibt erhalten:\n{0}", "Recording not deleted\n\nThe folder could not be moved to the Recycle Bin and was kept:\n{0}", kept),
                            Loc.L("Aufnahme verwerfen", "Discard recording"), MessageBoxButton.OK, MessageBoxImage.Warning));
                }
                else
                {
                    folder = rec.Stop();
                    // Schritte sofort ableiten (Viewer öffnet schneller)
                    using var view = ReadLayer.SessionView.Open(folder);
                }
            }
            catch (Exception ex) { Application.Current.Dispatcher.Invoke(() => UiApp.ShowError(ex)); }
            finally { rec.Dispose(); }
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _bar?.Close(); _frame?.Close();
                _tray?.Dispose(); _tray = null;
                Finished?.Invoke(discard ? null : folder);
            });
        });
    }

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint h);
}


/// <summary>Aufnahmeleiste: schwebende Leiste oben mittig, immer im Vordergrund, nimmt nie den Fokus.</summary>
sealed class RecordingBar : Window
{
    // Immer dunkel – Farben aus Theme.Night (Stopp: weiße Schrift 5,3 : 1, gedämpfter Text 7,9 : 1)
    static readonly Brush BarBg = Theme.Night.Bg, BarLine = Theme.Night.Line, BarFg = Theme.Night.Fg, BarMuted = Theme.Night.Muted,
        RecRed = Theme.Night.Rec, WarnFg = Theme.Night.Warn, PrivBg = Theme.Night.PrivBg, StopBg = Theme.Night.StopBg, ChipBg = Theme.Night.Chip;
    readonly RecordingController _c;
    readonly TextBlock _time = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 46 };
    readonly TextBlock _state = new() { FontSize = Theme.FsCaption, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 1, 10, 0), Text = "REC" };
    readonly TextBlock _count = new() { FontSize = Theme.FsHint, VerticalAlignment = VerticalAlignment.Center };
    readonly Ellipse _dot = new() { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _msg = new() { FontSize = Theme.FsHint, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
    readonly Border _msgRow;
    readonly Button _privacy;
    readonly TextBlock _pauseIcon = new(), _privacyIcon = new(), _dictIcon = new();
    readonly Border _sources = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(6, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Dauerhafte Anzeige, solange inhaltstragende Quellen mitgelesen werden (Pakete 12, 13).</summary>
    public void SetSources(string tooltip)
    {
        _sources.Child = new TextBlock { Text = Theme.IEye, FontFamily = Theme.Icons, FontSize = 15, Foreground = WarnFg, VerticalAlignment = VerticalAlignment.Center };
        _sources.ToolTip = tooltip;
        System.Windows.Automation.AutomationProperties.SetName(_sources, tooltip);
        _sources.Visibility = Visibility.Visible;
    }
    readonly Button _dictBtn;

    /// <summary>Diktat-Anzeige: Mikrofon-Symbol rot, solange das Mikrofon offen ist.</summary>
    public void SetDictation(bool on)
    {
        _dictIcon.Foreground = on ? RecRed : BarFg;
        _dictBtn.ToolTip = on ? K(Loc.L("Diktat läuft – klicken zum Beenden", "Dictation running – click to stop"), "dictation")
                              : K(Loc.L("Diktat: Gesprochenes wird offline zu Kommentaren", "Dictation: speech is turned into comments offline"), "dictation");
    }
    readonly DispatcherTimer _msgTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    bool _dragged, _pulsing, _sticky;

    static string K(string text, string id) => RecordingController.Key(id) is { Length: > 0 } k ? $"{text} ({k})" : text;

    public RecordingBar(RecordingController c)
    {
        _c = c;
        Title = StepRecorder.Data.AppInfo.DisplayName + Loc.L(" – Aufnahme", " – recording");
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight; FontFamily = Theme.UiFont; Foreground = BarFg; UseLayoutRounding = true;
        _time.Foreground = BarFg; _state.Foreground = RecRed; _count.Foreground = BarMuted; _dot.Fill = RecRed; _msg.Foreground = BarMuted;
        System.Windows.Documents.Typography.SetNumeralAlignment(_time, FontNumeralAlignment.Tabular);

        Button B(string glyph, string tip, Action a, TextBlock? icon = null, string? text = null)
        {
            icon ??= new TextBlock();
            icon.Text = glyph; icon.FontFamily = Theme.Icons; icon.FontSize = 16; icon.VerticalAlignment = VerticalAlignment.Center;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(icon);
            if (text != null) content.Children.Add(new TextBlock { Text = text, Margin = new Thickness(7, 0, 2, 0), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var b = new Button { Style = Theme.Style("BarButton"), Content = content, ToolTip = tip, Margin = new Thickness(1, 0, 1, 0) };
            b.Click += (_, _) => { try { a(); } catch (Exception ex) { UiApp.ShowError(ex); } };
            return b;
        }
        Border Sep() => new() { Width = 1, Height = 20, Background = BarLine, Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

        var grip = new TextBlock { Text = Theme.IGrip, FontFamily = Theme.Icons, FontSize = 14, Foreground = BarMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0), Cursor = Cursors.SizeAll, ToolTip = Loc.L("Leiste verschieben", "Move bar") };
        grip.MouseLeftButtonDown += (_, _) => { _dragged = true; DragMove(); };
        var status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { _dot, _state, _time } };
        var countChip = new Border { Margin = new Thickness(12, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Child = _count };

        var pause = B(Theme.IPause, K(Loc.L("Pause / Fortsetzen", "Pause / resume"), "instantPause"), c.TogglePause, _pauseIcon);
        var stop = B(Theme.IStop, K(Loc.L("Aufnahme beenden", "Stop recording"), "stop"), c.Stop, text: Loc.L("Stopp", "Stop"));
        stop.Background = StopBg; stop.Foreground = Brushes.White; stop.Padding = new Thickness(10, 6, 12, 6); stop.Margin = new Thickness(3, 0, 1, 0);
        var comment = B(Theme.IComment, K(Loc.L("Kommentar", "Comment"), "comment"), c.Comment);
        _dictBtn = B("\uE720", K(Loc.L("Diktat: Gesprochenes wird offline zu Kommentaren", "Dictation: speech is turned into comments offline"), "dictation"), c.ToggleDictation, _dictIcon);
        var marker = B(Theme.IFlag, K(Loc.L("Marker setzen", "Set marker"), "marker") + Loc.L(" – Rechtsklick: Art wählen", " – right-click: choose type"), () => c.Marker("note"));
        var mm = U.MenuScope(new ContextMenu());
        foreach (var it in c.MarkerMenu().Items.Cast<object>().ToList()) { if (it is FrameworkElement fe) (LogicalTreeHelper.GetParent(fe) as ItemsControl)?.Items.Remove(fe); mm.Items.Add(it); }
        marker.ContextMenu = mm;
        var photo = B(Theme.ICamera, K(Loc.L("Schnappschuss", "Snapshot"), "snapshot"), () => c.Marker("snapshot"));
        _privacy = B(Theme.IUnlock, K(Loc.L("Privattaste: Eingaben nicht speichern", "Privacy key: do not store input"), "privacy"), c.TogglePrivacy, _privacyIcon);
        var discard = B(Theme.IUndo, K(Loc.L("Letzten Schritt zurücknehmen", "Undo last step"), "discard"), c.DiscardPrevious);
        var more = B(Theme.IMore, Loc.L("Weitere Funktionen", "More functions"), () => { });
        var minimize = B("\uE921", K(Loc.L("In den Infobereich minimieren", "Minimise to notification area"), "bar") + Loc.L(" – Symbol unten rechts in der Taskleiste", " – icon at the bottom right of the taskbar"), c.MinimizeToTray);
        var menu = U.MenuScope(new ContextMenu());
        void Add(string h, Action a, string? glyph = null) => menu.Items.Add(U.Mi(h, null, a, glyph));
        menu.Items.Add(c.MarkerMenu());
        Add(Loc.L("Letzte Eingabe schwärzen", "Redact last input"), c.RedactLastInput, Theme.IShield);
        menu.Items.Add(new Separator());
        Add(Loc.L("Tastenkürzel …", "Shortcuts …"), c.ShowShortcuts, Theme.IKeyboard);
        menu.Items.Add(new Separator());
        Add(Loc.L("Aufnahme verwerfen …", "Discard recording …"), c.Discard, Theme.IDelete);
        more.Click += (_, _) => { menu.PlacementTarget = more; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true; };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var e in new UIElement[] { grip, status, countChip, _sources, Sep(), pause, stop, Sep(), comment, _dictBtn, marker, photo, _privacy, discard, more, minimize }) row.Children.Add(e);
        _msgRow = new Border { Child = _msg, Padding = new Thickness(10, 4, 10, 4), Visibility = Visibility.Collapsed };
        _msg.SetBinding(MaxWidthProperty, new System.Windows.Data.Binding("ActualWidth") { Source = row });
        Content = new Border
        {
            Margin = new Thickness(14), CornerRadius = new CornerRadius(Theme.RCard), Background = BarBg, BorderBrush = BarLine, BorderThickness = new Thickness(1), Padding = new Thickness(6),
            Effect = Theme.Shadow(24, 4, 0.45), Child = new StackPanel { Children = { row, _msgRow } }
        };
        _msgTimer.Tick += (_, _) => { if (_sticky) return; _msg.Text = ""; _msgRow.Visibility = Visibility.Collapsed; _msgTimer.Stop(); };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE);
            SetWindowLongPtr(h, Win32.GWL_EXSTYLE, (nint)(ex | 0x08000000 /*NOACTIVATE*/ | Win32.WS_EX_TOOLWINDOW));
            Invisibility.Apply(this);
        };
        Loaded += (_, _) => { Center(); SetPulse(true); if (Content is UIElement ce) Anim.Appear(ce, 0, -6, 200); };
        SizeChanged += (_, _) => { if (!_dragged) Center(); };
        SetTime(TimeSpan.Zero, 0, RecorderState.Recording, false);
    }

    /// <summary>Oben mittig auf dem Monitor, auf dem die Aufnahme begann (physische Pixel, gemischte DPI-Werte).</summary>
    void Center()
    {
        var h = new WindowInteropHelper(this).Handle;
        if (h == 0 || _mon == null) { var wa = SystemParameters.WorkArea; Left = wa.Left + (wa.Width - ActualWidth) / 2; Top = wa.Top - 6; return; }
        double s = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 1;
        int w = (int)(ActualWidth * s);
        SetWindowPos(h, 0, _mon.WorkArea.X + (_mon.WorkArea.Width - w) / 2, _mon.WorkArea.Y - (int)(6 * s), 0, 0, 0x0001 | 0x0004 | 0x0010);
    }

    readonly MonitorInfo? _mon = Theme.ActiveMonitor();
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);

    void SetPulse(bool on)
    {
        if (on == _pulsing) return;
        _pulsing = on;
        // Pulsieren nur, wenn Windows Animationen zeigen soll (Barrierefreiheit, STEPRECORDER_NOANIM)
        if (on && Anim.On) _dot.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
        else { _dot.BeginAnimation(OpacityProperty, null); _dot.Opacity = 1; }
    }

    public void SetTime(TimeSpan t, int count, RecorderState s, bool privacy)
    {
        bool paused = s == RecorderState.Paused;
        _time.Text = $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        _count.Text = Loc.N(count, "1 Schritt", "{0} Schritte", "1 step", "{0} steps");
        _dot.Fill = paused ? BarMuted : privacy ? WarnFg : RecRed;
        _state.Text = paused ? "PAUSE" : privacy ? Loc.L("PRIVAT", "PRIVATE") : "REC";
        _state.Foreground = paused ? BarMuted : privacy ? WarnFg : RecRed;
        SetPulse(!paused && IsLoaded);
        _pauseIcon.Text = paused ? Theme.IPlay : Theme.IPause;
        _privacyIcon.Text = privacy ? Theme.ILock : Theme.IUnlock;
        _privacy.Background = privacy ? PrivBg : Brushes.Transparent;
        _privacy.Foreground = privacy ? WarnFg : BarFg;
    }

    /// <summary>Meldung in der Leiste; <paramref name="sticky"/>: bleibt stehen, bis die nächste Meldung sie ersetzt.</summary>
    public void Message(string text, bool important = false, bool sticky = false)
    {
        _msg.Text = text;
        _msg.Foreground = important ? WarnFg : BarMuted;
        _msgRow.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        _sticky = sticky;
        _msgTimer.Stop(); _msgTimer.Start();
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern nint SetWindowLongPtr(nint h, int idx, nint v);
}

/// <summary>Durchklickbarer Rahmen um den Aufnahmebereich (nie im Bild).</summary>
sealed class FrameWindow : Window
{
    public static Brush RecBrush => Theme.Night.Rec;
    readonly Border _b;
    readonly Border _tag;
    readonly TextBlock _tagText = new() { Foreground = Brushes.White, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
    readonly DispatcherTimer _tagTimer = new();
    readonly System.Drawing.Rectangle _r;
    public FrameWindow(System.Drawing.Rectangle physical)
    {
        _r = physical;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        _b = new Border { BorderBrush = RecBrush, BorderThickness = new Thickness(3) };
        _tag = new Border { Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x17, 0x16, 0x14)), BorderBrush = RecBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 7, 12, 8), Child = _tagText, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 14, 0, 0), Visibility = Visibility.Collapsed };
        Content = new Grid { Children = { _b, _tag } };
        _tagTimer.Tick += (_, _) => { _tag.Visibility = Visibility.Collapsed; _tagTimer.Stop(); };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE);
            SetWindowLongPtr(h, Win32.GWL_EXSTYLE, (nint)(ex | 0x20 /*TRANSPARENT*/ | 0x80000 /*LAYERED*/ | Win32.WS_EX_TOOLWINDOW | 0x08000000));
            Invisibility.Apply(this);
            SetWindowPos(h, -1, _r.X, _r.Y, _r.Width, _r.Height, 0x0010 /*NOACTIVATE*/);
        };
    }

    public void SetStyle(Brush b, bool dashed)
    {
        _b.BorderBrush = b;
        _b.BorderThickness = new Thickness(dashed ? 2 : 3);
        _b.Opacity = dashed ? 0.7 : 1;
    }

    /// <summary>Kurzer Hinweis oben im Rahmen (z. B. Erinnerung bei ausgeblendeter Leiste) – wie der Rahmen nie im Bild.</summary>
    public void ShowTag(string text, int seconds)
    {
        _tagText.Text = text; _tag.Visibility = Visibility.Visible;
        _tagTimer.Interval = TimeSpan.FromSeconds(seconds); _tagTimer.Stop(); _tagTimer.Start();
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern nint SetWindowLongPtr(nint h, int idx, nint v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);
}

/// <summary>Kommentarfeld (Eingabe speichert, Umschalt+Eingabe neue Zeile, Esc verwirft).</summary>
sealed class CommentWindow : Window
{
    readonly Wpf.Ui.Controls.TextBox _box = new() { ClearButtonEnabled = false, AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MinHeight = 90, MaxLength = 4000, FontSize = 14, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top };
    public string Text => _box.Text;
    public CommentWindow(string? title = null)
    {
        title ??= Loc.L("Kommentar", "Comment");
        _box.PlaceholderText = Loc.L("Was passiert hier? Was ist wichtig?", "What is happening here? What matters?");
        Title = title; Width = 540; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        Theme.Prepare(this, Theme.Surface);
        _box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift) { int i = _box.CaretIndex; _box.Text = _box.Text.Insert(i, "\n"); _box.CaretIndex = i + 1; e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.Enter) { DialogResult = true; e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.Escape) { DialogResult = false; e.Handled = true; }
        };
        var hints = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        void H(string k, string t) { hints.Children.Add(U.Kbd(k)); var ht = U.Text(t, Theme.FsHint, fg: Theme.Muted, margin: new Thickness(2, 0, 16, 6)); ht.VerticalAlignment = VerticalAlignment.Center; hints.Children.Add(ht); }
        H(Loc.Keys("Enter"), Loc.L("speichert", "saves")); H(Loc.Keys("Shift+Enter"), Loc.L("neue Zeile", "new line")); H("Esc", Loc.L("verwirft", "discards"));
        U.Dialog(this, title, U.Col(_box, hints), U.Primary(Loc.L("Speichern", "Save"), () => DialogResult = true), U.Btn(Loc.L("Verwerfen", "Discard"), () => DialogResult = false));
        Loaded += (_, _) => { Activate(); _box.Focus(); };
    }
}

/// <summary>
/// Unsichtbarkeit nicht bestätigt (z. B. Windows Server 2019 oder ältere Windows-Versionen ohne Ausschluss aus der Bildschirmaufnahme):
/// anbieten, ohne sichtbare Leiste und Rahmen aufzunehmen und nur über Tastenkürzel zu steuern.
/// </summary>
sealed class HiddenModeDialog : Window
{
    readonly CheckBox _remember = new() { Margin = new Thickness(0, 14, 0, 0) };
    public bool Hidden { get; private set; } = true;

    public HiddenModeDialog()
    {
        _remember.Content = Loc.L("Künftig ohne Nachfrage mit ausgeblendeter Anzeige starten", "From now on start with hidden display without asking");
        Title = Loc.L("Aufnahme ohne sichtbare Anzeige", "Recording without visible display"); Width = 600; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true;
        Theme.Prepare(this, Theme.Surface);

        var body = new StackPanel();
        body.Children.Add(U.Text(Loc.L("Dieses Windows kann die Leiste und den roten Rahmen nicht aus den Bildschirmfotos ausschließen – sie wären auf jedem Bild zu sehen. Das betrifft z. B. Windows Server 2019 und Windows 10 vor Version 2004.",
            "This Windows cannot exclude the bar and the red frame from screenshots – they would appear in every image. This affects e.g. Windows Server 2019 and Windows 10 before version 2004."), Theme.FsBody, fg: Theme.Fg2, margin: new Thickness(0, 0, 0, 12)));
        U.Spaced((TextBlock)body.Children[^1]);
        body.Children.Add(U.Text(Loc.L("Empfohlen: mit ausgeblendeter Anzeige aufnehmen. Die Bilder bleiben sauber; gesteuert wird über Tastenkürzel:", "Recommended: record with hidden display. The images stay clean; control via shortcuts:"), Theme.FsBody, true, margin: new Thickness(0, 0, 0, 10)));
        var keys = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        keys.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); keys.ColumnDefinitions.Add(new ColumnDefinition());
        int r = 0;
        void Row(string id, string what)
        {
            var combo = RecordingController.Key(id);
            if (combo.Length == 0) return;
            keys.RowDefinitions.Add(new RowDefinition());
            var k = U.Kbd(combo); k.Margin = new Thickness(0, 3, 14, 3);
            var t = U.Text(what, Theme.FsHint, fg: Theme.Fg2); t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(k, r); Grid.SetRow(t, r); Grid.SetColumn(t, 1);
            keys.Children.Add(k); keys.Children.Add(t); r++;
        }
        Row("stop", Loc.L("Aufnahme beenden", "Stop recording"));
        Row("instantPause", Loc.L("Pause / Fortsetzen", "Pause / resume"));
        Row("bar", Loc.L("Leiste und Rahmen ein- und ausblenden (eingeblendet auf den Bildern sichtbar)", "Show/hide bar and frame (visible in the images when shown)"));
        Row("privacy", Loc.L("Privattaste – Eingaben nicht speichern", "Privacy key – do not store input"));
        Row("markerError", Loc.L("Fehlerstelle markieren", "Mark error"));
        Row("comment", Loc.L("Kommentar", "Comment"));
        Row("dictation", Loc.L("Diktat ein/aus (Mikrofon, offline)", "Dictation on/off (microphone, offline)"));
        body.Children.Add(keys);
        body.Children.Add(U.Text(Loc.L("Alternativ: Rechtsklick auf das Aufnahme-Symbol unten rechts im Infobereich der Taskleiste – dort gibt es alle Befehle als Menü.", "Alternatively: right-click the recording icon in the notification area – all commands are available there."), Theme.FsHint, fg: Theme.Fg2, margin: new Thickness(0, 12, 0, 0)));
        body.Children.Add(_remember);

        var hidden = U.Primary(Loc.L("Ausgeblendet starten", "Start hidden"), () => { Hidden = true; Save(); DialogResult = true; }, Theme.IEye);
        hidden.IsDefault = true;
        var visible = U.Btn(Loc.L("Mit Anzeige starten", "Start visible"), () => { Hidden = false; DialogResult = true; }, Loc.L("Leiste und Rahmen erscheinen auf den Bildern", "Bar and frame appear in the images"));
        var cancel = U.Btn(Loc.L("Abbrechen", "Cancel"), () => DialogResult = false);
        cancel.IsCancel = true;
        U.Dialog(this, Loc.L("Unsichtbarkeit nicht bestätigt", "Invisibility not confirmed"), body, hidden, visible, cancel);
        Loaded += (_, _) => { Activate(); hidden.Focus(); };
    }

    void Save()
    {
        if (_remember.IsChecked != true) return;
        var s = AppSettings.Load(); s.InvisibleFallback = "hidden"; s.Save();
    }
}
