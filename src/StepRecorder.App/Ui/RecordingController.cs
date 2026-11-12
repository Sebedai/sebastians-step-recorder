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
    static string De(string c) => string.Join("+", c.Split('+').Select(p => p.Trim() switch { "Ctrl" => "Strg", "Shift" => "Umschalt", var x => x }));

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
            string stopKey = De(settings.Hotkeys.GetValueOrDefault("stop", "Ctrl+Shift+F10")), barKey = De(settings.Hotkeys.GetValueOrDefault("bar", "Ctrl+Shift+F4"));
            var cd = new CountdownWindow(countdown, "Aufnahme startet in", _hiddenMode ? $"Ohne sichtbare Anzeige. Beenden: {stopKey} · Anzeige: {barKey}" : null);
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
        _rec.ResumeConfirmationNeeded += () => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _bar?.Message("Aufnahme pausiert (Sperre/Benutzerwechsel) – mit Fortsetzen weiter.", true);
        });
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
        string weak = _o.PrivacyLevel == "balanced" ? "Schutznetz – Passwortfelder werden maskiert; in Terminals und Remote-Desktop Privattaste (Strg+Umschalt+F8) nutzen." : "Streng – Eingaben in unbekannten Feldern werden maskiert.";
        _bar.Message((selfTest ? "Unsichtbarkeit geprüft · " : _hiddenMode ? "Ausgeblendete Anzeige – Achtung: eingeblendet ist die Leiste auf den Bildern zu sehen · " : "Unsichtbarkeit nicht bestätigt · ") + weak, !selfTest);
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

    void RegisterHotkeys()
    {
        _hk = new Hotkeys();
        var h = AppSettings.Load().Hotkeys;
        string K(string n, string d) => h.GetValueOrDefault(n, d);
        _hk.Register("pause", K("instantPause", "Ctrl+Shift+F9"), TogglePause);
        _hk.Register("stop", K("stop", "Ctrl+Shift+F10"), Stop);
        _hk.Register("snapshot", K("snapshot", "Ctrl+Shift+F11"), () => Marker("snapshot"));
        _hk.Register("comment", K("comment", "Ctrl+Shift+F12"), Comment);
        _hk.Register("privacy", K("privacy", "Ctrl+Shift+F8"), TogglePrivacy);
        _hk.Register("marker", K("marker", "Ctrl+Shift+F7"), () => Marker("note"));
        _hk.Register("discard", K("discard", "Ctrl+Shift+F6"), DiscardPrevious);
        _hk.Register("bar", K("bar", "Ctrl+Shift+F4"), ToggleBar);
        _hk.Register("markerError", K("markerError", "Ctrl+Shift+F1"), () => Marker("error"));
        _hk.Register("markerExpected", K("markerExpected", "Ctrl+Shift+F2"), () => Marker("expected"));
        _hk.Register("dictation", K("dictation", "Ctrl+Shift+F5"), ToggleDictation);
        if (_hk.Failed.Count > 0) _bar?.Message("Tastenkürzel belegt: " + string.Join(", ", _hk.Failed), true);
    }

    /// <summary>Leiste ein/aus; im Ausweichmodus mit ihr auch den Rahmen (beide wären auf den Bildern zu sehen).</summary>
    void ToggleBar()
    {
        if (_bar == null) return;
        if (!_bar.IsVisible)
        {
            if (!_bar.IsLoaded) _bar.Show(); else _bar.Visibility = Visibility.Visible;
            if (_hiddenMode) { if (_frame == null) ShowFrame(); else _frame.Visibility = Visibility.Visible; _bar.Message("Achtung: Leiste und Rahmen sind jetzt auf den Bildern zu sehen – Strg+Umschalt+F4 blendet sie wieder aus.", true); }
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
        _bar?.SetTime(elapsed, _rec.ActionCount, _rec.State, _rec.PrivacyMode);
        if (_tray != null)
        {
            string state = _rec.State == RecorderState.Paused ? "paused" : _rec.PrivacyMode ? "private" : "rec";
            if (state != _trayState) { _trayState = state; _tray.SetIcon(TrayIcon.Render(state)); }
            string what = state switch { "paused" => "pausiert", "private" => "läuft (privat)", _ => "läuft" };
            _tray.SetTip($"{StepRecorder.Data.AppInfo.DisplayName}\nAufnahme {what} · {elapsed:mm\\:ss} · {_rec.ActionCount} Schritte\nKlick: Leiste · Rechtsklick: Menü");
        }
    }

    /// <summary>Symbol im Infobereich: bedienbar wie die Leiste – auch wenn sie ausgeblendet oder minimiert ist.</summary>
    void CreateTray()
    {
        try
        {
            _tray = new TrayIcon(TrayIcon.Render("rec"), StepRecorder.Data.AppInfo.DisplayName + " – Aufnahme läuft");
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
        var h = AppSettings.Load().Hotkeys;
        string K(string n, string d) => De(h.GetValueOrDefault(n, d));
        var elapsed = (_pausedAt ?? DateTime.Now) - _started - _pausedTotal;
        bool paused = _rec.State == RecorderState.Paused;
        var m = new ContextMenu();
        void Add(string head, string gesture, Action a, bool enabled = true)
        {
            var mi = new MenuItem { Header = head, InputGestureText = gesture, IsEnabled = enabled };
            mi.Click += (_, _) => { try { a(); } catch (Exception ex) { UiApp.ShowError(ex); } };
            m.Items.Add(mi);
        }
        m.Items.Add(new MenuItem { Header = $"Aufnahme {(paused ? "pausiert" : "läuft")} · {elapsed:mm\\:ss} · {_rec.ActionCount} Schritte", IsEnabled = false });
        m.Items.Add(new Separator());
        Add(paused ? "Fortsetzen" : "Pause", K("instantPause", "Ctrl+Shift+F9"), TogglePause);
        Add("Beenden und speichern", K("stop", "Ctrl+Shift+F10"), Stop);
        m.Items.Add(new Separator());
        Add("Fehlerstelle markieren", K("markerError", "Ctrl+Shift+F1"), () => Marker("error"));
        Add("Marker setzen", K("marker", "Ctrl+Shift+F7"), () => Marker("note"));
        Add("Kommentar …", K("comment", "Ctrl+Shift+F12"), Comment);
        Add(_dict.Active ? "Diktat beenden" : "Diktat starten (Mikrofon)", K("dictation", "Ctrl+Shift+F5"), ToggleDictation);
        Add("Schnappschuss", K("snapshot", "Ctrl+Shift+F11"), () => Marker("snapshot"));
        Add(_rec.PrivacyMode ? "Privattaste aus" : "Privattaste ein", K("privacy", "Ctrl+Shift+F8"), TogglePrivacy);
        Add("Letzten Schritt verwerfen", K("discard", "Ctrl+Shift+F6"), DiscardPrevious);
        m.Items.Add(new Separator());
        Add(_bar?.IsVisible == true ? "Leiste ausblenden" : "Leiste anzeigen", K("bar", "Ctrl+Shift+F4"), ToggleBar);
        Add("Aufnahme verwerfen …", "", Discard);
        m.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        m.IsOpen = true;
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
            _frame?.SetStyle(FrameWindow.RecBrush, dashed: _rec.PrivacyMode);
        }
        UpdateBar();
    }

    public void TogglePause() { if (_sounds) Tones.Pause(); if (_rec.State == RecorderState.Paused) _rec.Resume(); else _rec.Pause(); }

    public void TogglePrivacy()
    {
        _rec.TogglePrivacy();
        _frame?.SetStyle(FrameWindow.RecBrush, dashed: _rec.PrivacyMode);
        _bar?.Message(_rec.PrivacyMode ? "Privat – Eingaben werden nicht gespeichert." : "Privatmodus aus.", _rec.PrivacyMode);
        UpdateBar();
    }

    int _markers;
    public void Marker(string kind, string? label = null)
    {
        _rec.AddMarker(kind, label);
        if (_sounds) Tones.Tick();
        if (kind != "snapshot") _markers++;
        _bar?.Message(kind == "snapshot" ? "Schnappschuss gespeichert" : $"Marker {_markers} ({kind switch { "error" => "Fehler", "expected" => "Erwartet", "important" => "Wichtig", _ => "Notiz" }}) gesetzt");
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
            _bar?.Message("Kommentar gespeichert");
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
            _bar?.Message("Diktat beendet – Mikrofon aus.");
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
                if (_rec.PrivacyMode) { _bar?.Message("Privattaste aktiv – Diktat nicht gespeichert.", true); return; }
                _rec.AddComment(text, t, null);
                _bar?.Message("Diktiert: " + (text.Length > 70 ? text[..69] + "…" : text));
            }));
        if (err != null) { _bar?.Message(err, true); Msg.Show("Diktat nicht möglich\n\n" + err, "Diktat", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (_sounds) Tones.Tick();
        _bar?.SetDictation(true);
        _bar?.Message($"Diktat läuft ({_dict.Culture}) – sprechen Sie; jeder Satz wird ein Kommentar. Strg+Umschalt+F5 beendet.");
    }

    public void ShowRunningNotice() => _bar?.Message("Es läuft bereits eine Aufnahme.", true);

    public void Discard()
    {
        if (Msg.Show("Aufnahme verwerfen?\n\nDer Sitzungsordner wird gelöscht. Das lässt sich nicht rückgängig machen.", "Aufnahme verwerfen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _discard = true;
        Stop();
    }

    bool _stopping;
    public void Stop()
    {
        if (_stopping) return;
        _stopping = true;
        if (_sounds) Tones.Stop();
        _tick.Stop();
        _hk?.Dispose();
        _dict.Stop();
        _bar?.Message("Wird verarbeitet …");
        var rec = _rec;
        bool discard = _discard;
        Task.Run(() =>
        {
            string? folder = null;
            try
            {
                if (discard) rec.Discard();
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
    readonly TextBlock _time = new() { FontSize = 15, FontFamily = Theme.Mono, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 46 };
    readonly TextBlock _state = new() { FontSize = Theme.FsCaption, FontFamily = Theme.Mono, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 1, 10, 0), Text = "REC" };
    readonly TextBlock _count = new() { FontSize = Theme.FsHint, VerticalAlignment = VerticalAlignment.Center };
    readonly Ellipse _dot = new() { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _msg = new() { FontSize = Theme.FsHint, TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
    readonly Border _msgRow;
    readonly Button _privacy;
    readonly TextBlock _pauseIcon = new(), _privacyIcon = new(), _dictIcon = new();
    readonly Button _dictBtn;

    /// <summary>Diktat-Anzeige: Mikrofon-Symbol rot, solange das Mikrofon offen ist.</summary>
    public void SetDictation(bool on)
    {
        _dictIcon.Foreground = on ? RecRed : BarFg;
        _dictBtn.ToolTip = on ? "Diktat läuft – klicken zum Beenden (Strg+Umschalt+F5)" : "Diktat: Gesprochenes wird offline zu Kommentaren (Strg+Umschalt+F5)";
    }
    readonly DispatcherTimer _msgTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    bool _dragged, _pulsing;

    public RecordingBar(RecordingController c)
    {
        _c = c;
        Title = StepRecorder.Data.AppInfo.DisplayName + " – Aufnahme";
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

        var grip = new TextBlock { Text = Theme.IGrip, FontFamily = Theme.Icons, FontSize = 14, Foreground = BarMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0), Cursor = Cursors.SizeAll, ToolTip = "Leiste verschieben" };
        grip.MouseLeftButtonDown += (_, _) => { _dragged = true; DragMove(); };
        var status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { _dot, _state, _time } };
        var countChip = new Border { Background = ChipBg, CornerRadius = new CornerRadius(999), Padding = new Thickness(10, 3, 10, 4), Margin = new Thickness(10, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Child = _count };

        var pause = B(Theme.IPause, "Pause / Fortsetzen (Strg+Umschalt+F9)", c.TogglePause, _pauseIcon);
        var stop = B(Theme.IStop, "Aufnahme beenden (Strg+Umschalt+F10)", c.Stop, text: "Stopp");
        stop.Background = StopBg; stop.Foreground = Brushes.White; stop.Padding = new Thickness(10, 6, 12, 6); stop.Margin = new Thickness(3, 0, 1, 0);
        var comment = B(Theme.IComment, "Kommentar (Strg+Umschalt+F12)", c.Comment);
        _dictBtn = B("\uE720", "Diktat: Gesprochenes wird offline zu Kommentaren (Strg+Umschalt+F5)", c.ToggleDictation, _dictIcon);
        var marker = B(Theme.IFlag, "Marker setzen (Strg+Umschalt+F7) – Rechtsklick: Art wählen", () => c.Marker("note"));
        var mm = new ContextMenu();
        foreach (var (k, n, g) in new[] { ("note", "Notiz", "Strg+Umschalt+F7"), ("error", "Fehler", "Strg+Umschalt+F1"), ("expected", "Erwartet", "Strg+Umschalt+F2"), ("important", "Wichtig", "") })
        { var mi = new MenuItem { Header = n, InputGestureText = g }; mi.Click += (_, _) => c.Marker(k); mm.Items.Add(mi); }
        marker.ContextMenu = mm;
        var photo = B(Theme.ICamera, "Schnappschuss (Strg+Umschalt+F11)", () => c.Marker("snapshot"));
        _privacy = B(Theme.IUnlock, "Privattaste: Eingaben nicht speichern (Strg+Umschalt+F8)", c.TogglePrivacy, _privacyIcon);
        var discard = B(Theme.IUndo, "Letzten Schritt verwerfen (Strg+Umschalt+F6)", c.DiscardPrevious);
        var more = B(Theme.IMore, "Weitere Funktionen", () => { });
        var minimize = B("\uE921", "In den Infobereich minimieren (Strg+Umschalt+F4) – Symbol unten rechts in der Taskleiste", c.MinimizeToTray);
        var menu = new ContextMenu();
        void Add(string h, string g, Action a) { var mi = new MenuItem { Header = h, InputGestureText = g }; mi.Click += (_, _) => { try { a(); } catch (Exception ex) { UiApp.ShowError(ex); } }; menu.Items.Add(mi); }
        Add("Letzte Eingabe schwärzen", "", c.RedactLastInput);
        Add("Marker mit Text …", "", () => { var d = new CommentWindow("Marker-Text"); if (d.ShowDialog() == true) c.Marker("important", d.Text); });
        Add("In den Infobereich minimieren", "Strg+Umschalt+F4", c.MinimizeToTray);
        Add("Tastenkürzel …", "", () => Msg.Show("Tastenkürzel während der Aufnahme\n\nStrg+Umschalt+F9  Pause / Fortsetzen\nStrg+Umschalt+F10  Stopp\nStrg+Umschalt+F12  Kommentar\nStrg+Umschalt+F5  Diktat ein/aus\nStrg+Umschalt+F7  Marker\nStrg+Umschalt+F1  Marker „Fehler“\nStrg+Umschalt+F2  Marker „Erwartet“\nStrg+Umschalt+F11  Schnappschuss\nStrg+Umschalt+F8  Privattaste\nStrg+Umschalt+F6  Letzten Schritt verwerfen\nStrg+Umschalt+F4  Leiste ein/aus\n\nAlle Befehle gibt es auch per Rechtsklick auf das Symbol unten rechts im Infobereich der Taskleiste.", "Tastenkürzel"));
        menu.Items.Add(new Separator());
        Add("Aufnahme verwerfen …", "", c.Discard);
        more.Click += (_, _) => { menu.PlacementTarget = more; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true; };

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var e in new UIElement[] { grip, status, countChip, Sep(), pause, stop, Sep(), comment, _dictBtn, marker, photo, _privacy, discard, more, minimize }) row.Children.Add(e);
        _msgRow = new Border { Child = _msg, Padding = new Thickness(10, 4, 10, 4), Visibility = Visibility.Collapsed };
        _msg.SetBinding(MaxWidthProperty, new System.Windows.Data.Binding("ActualWidth") { Source = row });
        Content = new Border
        {
            Margin = new Thickness(14), CornerRadius = new CornerRadius(Theme.RInset + 2), Background = BarBg, BorderBrush = BarLine, BorderThickness = new Thickness(1), Padding = new Thickness(6),
            Effect = Theme.Shadow(24, 4, 0.45), Child = new StackPanel { Children = { row, _msgRow } }
        };
        _msgTimer.Tick += (_, _) => { _msg.Text = ""; _msgRow.Visibility = Visibility.Collapsed; _msgTimer.Stop(); };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            long ex = Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE);
            SetWindowLongPtr(h, Win32.GWL_EXSTYLE, (nint)(ex | 0x08000000 /*NOACTIVATE*/ | Win32.WS_EX_TOOLWINDOW));
            Invisibility.Apply(this);
        };
        Loaded += (_, _) => { Center(); SetPulse(true); if (Content is UIElement ce) Anim.Appear(ce, 0, -14, 380); };
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
        _count.Text = count == 1 ? "1 Schritt" : $"{count} Schritte";
        _dot.Fill = paused ? BarMuted : privacy ? WarnFg : RecRed;
        _state.Text = paused ? "PAUSE" : privacy ? "PRIVAT" : "REC";
        _state.Foreground = paused ? BarMuted : privacy ? WarnFg : RecRed;
        SetPulse(!paused && IsLoaded);
        _pauseIcon.Text = paused ? Theme.IPlay : Theme.IPause;
        _privacyIcon.Text = privacy ? Theme.ILock : Theme.IUnlock;
        _privacy.Background = privacy ? PrivBg : Brushes.Transparent;
        _privacy.Foreground = privacy ? WarnFg : BarFg;
    }

    public void Message(string text, bool important = false)
    {
        _msg.Text = text;
        _msg.Foreground = important ? WarnFg : BarMuted;
        _msgRow.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        _msgTimer.Stop(); _msgTimer.Start();
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern nint SetWindowLongPtr(nint h, int idx, nint v);
}

/// <summary>Durchklickbarer Rahmen um den Aufnahmebereich (nie im Bild).</summary>
sealed class FrameWindow : Window
{
    public static Brush RecBrush => Theme.Night.Rec;
    readonly Border _b;
    readonly System.Drawing.Rectangle _r;
    public FrameWindow(System.Drawing.Rectangle physical)
    {
        _r = physical;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        _b = new Border { BorderBrush = RecBrush, BorderThickness = new Thickness(3) };
        Content = _b;
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

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern nint SetWindowLongPtr(nint h, int idx, nint v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint after, int x, int y, int w, int hh, uint flags);
}

/// <summary>Kommentarfeld (Eingabe speichert, Umschalt+Eingabe neue Zeile, Esc verwirft).</summary>
sealed class CommentWindow : Window
{
    readonly TextBox _box = new() { AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MinHeight = 90, MaxLength = 4000, FontSize = 14, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top, Tag = "Was passiert hier? Was ist wichtig?" };
    public string Text => _box.Text;
    public CommentWindow(string title = "Kommentar")
    {
        Title = title; Width = 540; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        Theme.Prepare(this, Theme.Surface);
        _box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift) { int i = _box.CaretIndex; _box.Text = _box.Text.Insert(i, "\n"); _box.CaretIndex = i + 1; e.Handled = true; }
            else if (e.Key == Key.Enter) { DialogResult = true; e.Handled = true; }
            else if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
        };
        var hints = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        void H(string k, string t) { hints.Children.Add(U.Kbd(k)); var ht = U.Text(t, Theme.FsHint, fg: Theme.Muted, margin: new Thickness(2, 0, 16, 6)); ht.VerticalAlignment = VerticalAlignment.Center; hints.Children.Add(ht); }
        H("Eingabe", "speichert"); H("Umschalt+Eingabe", "neue Zeile"); H("Esc", "verwirft");
        U.Dialog(this, title, U.Col(_box, hints), U.Primary("Speichern", () => DialogResult = true), U.Btn("Verwerfen", () => DialogResult = false));
        Loaded += (_, _) => { Activate(); _box.Focus(); };
    }
}

/// <summary>
/// Unsichtbarkeit nicht bestätigt (z. B. Windows Server 2019 oder ältere Windows-Versionen ohne Ausschluss aus der Bildschirmaufnahme):
/// anbieten, ohne sichtbare Leiste und Rahmen aufzunehmen und nur über Tastenkürzel zu steuern.
/// </summary>
sealed class HiddenModeDialog : Window
{
    readonly CheckBox _remember = new() { Content = "Künftig ohne Nachfrage mit ausgeblendeter Anzeige starten", Margin = new Thickness(0, 14, 0, 0) };
    public bool Hidden { get; private set; } = true;

    public HiddenModeDialog()
    {
        Title = "Aufnahme ohne sichtbare Anzeige"; Width = 600; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true;
        Theme.Prepare(this, Theme.Surface);
        var h = AppSettings.Load().Hotkeys;
        static string De(string c) => string.Join("+", c.Split('+').Select(p => p.Trim() switch { "Ctrl" => "Strg", "Shift" => "Umschalt", var x => x }));
        string K(string n, string d) => De(h.GetValueOrDefault(n, d));

        var body = new StackPanel();
        body.Children.Add(U.Text("Dieses Windows kann die Leiste und den roten Rahmen nicht aus den Bildschirmfotos ausschließen – sie wären auf jedem Bild zu sehen. " +
            "Das betrifft z. B. Windows Server 2019 und Windows 10 vor Version 2004.", Theme.FsBody, fg: Theme.Fg2, margin: new Thickness(0, 0, 0, 12)));
        U.Spaced((TextBlock)body.Children[^1]);
        body.Children.Add(U.Text("Empfohlen: mit ausgeblendeter Anzeige aufnehmen. Die Bilder bleiben sauber; gesteuert wird über Tastenkürzel:", Theme.FsBody, true, margin: new Thickness(0, 0, 0, 10)));
        var keys = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        keys.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); keys.ColumnDefinitions.Add(new ColumnDefinition());
        int r = 0;
        void Row(string combo, string what)
        {
            keys.RowDefinitions.Add(new RowDefinition());
            var k = U.Kbd(combo); k.Margin = new Thickness(0, 3, 14, 3);
            var t = U.Text(what, Theme.FsHint, fg: Theme.Fg2); t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(k, r); Grid.SetRow(t, r); Grid.SetColumn(t, 1);
            keys.Children.Add(k); keys.Children.Add(t); r++;
        }
        Row(K("stop", "Ctrl+Shift+F10"), "Aufnahme beenden");
        Row(K("instantPause", "Ctrl+Shift+F9"), "Pause / Fortsetzen");
        Row(K("bar", "Ctrl+Shift+F4"), "Leiste und Rahmen ein- und ausblenden (eingeblendet auf den Bildern sichtbar)");
        Row(K("privacy", "Ctrl+Shift+F8"), "Privattaste – Eingaben nicht speichern");
        Row(K("markerError", "Ctrl+Shift+F1"), "Fehlerstelle markieren");
        Row(K("comment", "Ctrl+Shift+F12"), "Kommentar");
        Row(K("dictation", "Ctrl+Shift+F5"), "Diktat ein/aus (Mikrofon, offline)");
        body.Children.Add(keys);
        body.Children.Add(U.Text("Alternativ: Rechtsklick auf das Aufnahme-Symbol unten rechts im Infobereich der Taskleiste – dort gibt es alle Befehle als Menü.", Theme.FsHint, fg: Theme.Fg2, margin: new Thickness(0, 12, 0, 0)));
        body.Children.Add(_remember);

        var hidden = U.Primary("Ausgeblendet starten", () => { Hidden = true; Save(); DialogResult = true; }, Theme.IEye);
        hidden.IsDefault = true;
        var visible = U.Btn("Mit Anzeige starten", () => { Hidden = false; DialogResult = true; }, "Leiste und Rahmen erscheinen auf den Bildern");
        var cancel = U.Btn("Abbrechen", () => DialogResult = false);
        cancel.IsCancel = true;
        U.Dialog(this, "Unsichtbarkeit nicht bestätigt", body, hidden, visible, cancel);
        Loaded += (_, _) => { Activate(); hidden.Focus(); };
    }

    void Save()
    {
        if (_remember.IsChecked != true) return;
        var s = AppSettings.Load(); s.InvisibleFallback = "hidden"; s.Save();
    }
}
