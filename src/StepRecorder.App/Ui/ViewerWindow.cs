using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Viewer und Editor in einem Fenster: Schrittliste, Bildbereich mit Werkzeugen, Detailbereich,
/// Statusleiste. Alle Bearbeitungen landen im Overlay edits.json (automatisch gespeichert, rückgängig machbar).
/// </summary>
sealed partial class ViewerWindow : Window
{
    readonly string _path;
    SessionView _view = null!;
    readonly ListBox _list = new();
    readonly Image _img = new() { Stretch = Stretch.Uniform };
    readonly Canvas _overlay = new() { Background = Brushes.Transparent };
    readonly Grid _imgHost = new();
    readonly StackPanel _details = new() { Margin = new Thickness(22, 20, 22, 24) };
    readonly TextBlock _status = new() { FontSize = Theme.FsHint, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBox _search = new() { Tag = Loc.L("Schritte durchsuchen", "Search steps"), Padding = new Thickness(32, 7, 8, 7), ToolTip = Loc.L("Suche (Strg+F)", "Search (Ctrl+F)") };
    ToggleButton _allToggle = null!;
    ComboBox _dispBox = null!;
    RadioButton _beforeBtn = null!, _afterBtn = null!;
    readonly TextBlock _titleBlock = new() { FontSize = Theme.FsSub, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = Loc.L("Klicken zum Umbenennen", "Click to rename"), TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _metaBlock = new() { FontSize = Theme.FsCaption, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _listCount = new() { FontSize = Theme.FsCaption, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _imgCaption = new() { FontSize = Theme.FsCaption, Foreground = Theme.Night.Fg };
    readonly Border _imgCaptionPill;
    readonly TextBlock _noImage = new() { Text = Loc.L("Für diesen Schritt gibt es kein Bild.", "There is no image for this step."), FontSize = Theme.FsBody, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    readonly TextBlock _reviewCount = new() { FontSize = Theme.FsCaption, FontWeight = FontWeights.SemiBold };
    Border _reviewBadge = null!;
    readonly TextBlock _playIcon = new();
    readonly List<string> _undo = new(), _redo = new();
    bool _showAfter;               // zeigt gerade das Nachher-Bild
    string? _variantOverride;      // vom Benutzer gewählt (sonst Standard des Schritts)
    bool _syncBA;
    string _tool = "select";
    string _display = "screen";
    BitmapSource? _current;
    RenderedImage? _currentMeta;
    Point? _dragStart;
    Shape? _rubber;
    readonly DispatcherTimer _play = new() { Interval = TimeSpan.FromMilliseconds(1600) };

    ViewStep? Sel => _list.SelectedItem is ListBoxItem { Tag: ViewStep s } ? s : null;

    public ViewerWindow(string path)
    {
        _path = path;
        Theme.Prepare(this, Theme.Surface);
        var wa = Theme.ActiveWorkArea();
        Width = Math.Min(1480, wa.Width - 40); Height = Math.Min(920, wa.Height - 40); MinWidth = Math.Min(760, wa.Width - 40); MinHeight = Math.Min(520, wa.Height - 40);
        SizeChanged += (_, _) => Adapt();
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowPlacer.Attach(this, "viewer");
        _status.Foreground = Theme.Muted; _metaBlock.Foreground = Theme.Muted; _listCount.Foreground = Theme.Muted; _noImage.Foreground = Theme.Muted; _reviewCount.Foreground = Theme.Warn;
        _imgCaptionPill = new Border { Background = Theme.Night.Pill, CornerRadius = new CornerRadius(999), Padding = new Thickness(14, 5, 14, 6), Child = _imgCaption, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 0, 0), IsHitTestVisible = false };
        Load();
        BuildLayout();
        RestoreLayoutState();
        Fill();
        try { AutoCleanupOnOpen(); } catch (Exception ex) { UiApp.Log("Aufräumen: " + ex); }
        RestoreStep();
        PreviewKeyDown += OnKey;
        _play.Tick += (_, _) => { if (_list.SelectedIndex < _list.Items.Count - 1) _list.SelectedIndex++; else StopPlay(); };
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0 && Session.LooksLikeSession(f[0])) UiApp.Instance.OpenViewer(f[0]); };
    }

    void Load()
    {
        _view?.Dispose();
        _findings = null;
        _view = SessionView.Open(_path, headless: false, lang: AppSettings.Load().Language);
        if (_view.Edits.Obj("guide")?["address"] == null && AppSettings.Load().Address == "informal")
        { var g = _view.Edits.Obj("guide") ?? new JsonObject(); g["address"] = "informal"; _view.Edits["guide"] = g; _view.Rebuild(); }
        ApplyDefaultTemplate();
        Title = _view.Title + " – " + AppInfo.DisplayName;
        if (!_view.Session.ReadOnly) Exporter.CleanStaleJobs(_view.Session.Path);
    }

    Button MenuButton(string glyph, string text, string tip, string? style, Func<(string label, Action action)[]> items)
    {
        var content = U.MenuContent(glyph, text);
        var b = new Button { Content = content, ToolTip = tip, Margin = new Thickness(2, 0, 2, 0), Style = Theme.Style(style ?? "Ghost") };
        b.Click += (_, _) => ShowMenu(b, items());
        return b;
    }

    static void ShowMenu(FrameworkElement target, params (string label, Action action)[] items)
    {
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        foreach (var (label, action) in items)
        {
            if (label == "-") { m.Items.Add(new Separator()); continue; }
            var (header, gesture) = SplitLabel(label);
            var mi = new MenuItem { Header = header, InputGestureText = gesture };
            mi.Click += (_, _) => { try { action(); } catch (Exception ex) { UiApp.ShowError(ex); } };
            m.Items.Add(mi);
        }
        m.IsOpen = true;
    }

    // =====================================================================================
    // Liste
    // =====================================================================================

    static string KindIcon(ViewStep s) => s.Kind switch
    {
        "click" or "doubleClick" or "tripleClick" or "rightClick" => Theme.IMouse, "drag" => Theme.IMove, "scroll" => Theme.IScroll,
        "type" => s.Json.Obj("input")?.Bool("masked") == true ? Theme.ILock : Theme.IKeyboard, "hotkey" => Theme.IHotkey, "key" => Theme.IKeyboard,
        "hover" => Theme.IHover, "windowSwitch" => Theme.ICompare, "system" => Theme.ISystem, "wait" => Theme.IStopwatch, "comment" => Theme.IComment, "snapshot" => Theme.ICamera,
        "marker" => s.Json.Obj("input")?.Str("markerKind") switch { "error" => Theme.IError, "expected" => Theme.ICheck, "important" => Theme.IImportant, _ => Theme.IFlag },
        "gap" => Theme.IWarn, "move" => Theme.IMove, "manual" => s.PreFrame != null ? Theme.ICamera : Theme.IText, _ => Theme.IDot
    };

    static string KindLabel(string kind) => kind switch
    {
        "click" => Loc.L("Klick", "Click"), "doubleClick" => Loc.L("Doppelklick", "Double-click"), "tripleClick" => Loc.L("Dreifachklick", "Triple-click"), "rightClick" => Loc.L("Rechtsklick", "Right-click"), "drag" => Loc.L("Ziehen", "Drag"), "scroll" => Loc.L("Scrollen", "Scroll"),
        "type" => Loc.L("Eingabe", "Input"), "hotkey" => Loc.L("Tastenkürzel", "Shortcut"), "key" => Loc.L("Taste", "Key"), "hover" => Loc.L("Zeigen", "Point"), "windowSwitch" => Loc.L("Fensterwechsel", "Window switch"), "system" => Loc.L("System", "System"), "wait" => Loc.L("Warten", "Wait"),
        "comment" => Loc.L("Kommentar", "Comment"), "snapshot" => Loc.L("Schnappschuss", "Snapshot"), "marker" => Loc.L("Marker", "Marker"), "gap" => Loc.L("Lücke", "Gap"), "move" => Loc.L("Mausbewegung", "Mouse movement"), "manual" => Loc.L("Eingefügt", "Inserted"), _ => kind
    };

    static string ImportanceLabel(string imp) => imp switch { "primary" => Loc.L("Hauptschritt", "Main step"), "secondary" => Loc.L("Nebenschritt", "Secondary step"), _ => Loc.L("Spur", "Trace") };

    /// <summary>Anführungszeichen je Oberflächensprache („…“ bzw. “…”).</summary>
    static string Q(string? t) => Loc.L($"„{t}“", $"“{t}”");

    void UpdateStatus()
    {
        int redactions = _view.Edits.Arr("redactions")?.Count ?? 0;
        var s = Sel;
        var parts = new List<string> { s != null ? Loc.F("Schritt {0} von {1}", "Step {0} of {1}", s.Number, _view.All.Count) : Loc.N(_view.All.Count, "1 Schritt", "{0} Schritte", "1 step", "{0} steps"), Loc.L("Automatisch gespeichert", "Saved automatically") };
        if (redactions > 0) parts.Add(Loc.N(redactions, "1 Schwärzung", "{0} Schwärzungen", "1 redaction", "{0} redactions"));
        parts.Add(_view.UnreviewedCount > 0 ? Loc.N(_view.UnreviewedCount, "1 Eingabe ungeprüft", "{0} Eingaben ungeprüft", "1 input unreviewed", "{0} inputs unreviewed") : Loc.L("Eingaben geprüft", "Inputs reviewed"));
        parts.Add(_view.Language == "de" ? Loc.L("Deutsch", "German") : Loc.L("English", "English"));
        if (_view.Session.ReadOnly) parts.Add(Loc.L("schreibgeschützt", "read-only"));
        if (_view.Session.State == "recovered") parts.Add(Loc.L("nach Absturz wiederhergestellt", "recovered after crash"));
        _status.Text = string.Join("   ·   ", parts);
        _reviewCount.Text = _view.UnreviewedCount.ToString();
        _reviewBadge.Visibility = _view.UnreviewedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        int primary = _view.All.Count(x => x.Importance == "primary");
        var started = _view.Session.StartUtc.ToLocalTime();
        _metaBlock.Text = Loc.L($"{started:dd.MM.yyyy, HH:mm} Uhr", Loc.DateAndTime(started)) + "   ·   " + Loc.N(primary, "1 Hauptschritt", "{0} Hauptschritte", "1 main step", "{0} main steps") + "   ·   " + Fmt.Time(_view.Session.DurationUs, Loc.Lang);
    }

    // =====================================================================================
    // Bild und Details
    // =====================================================================================

    string? _animStepId;

    void ShowStep()
    {
        var s = Sel;
        _overlay.Children.Clear();
        _currentMeta?.Dispose(); _currentMeta = null;
        _img.Source = null; _current = null;
        if (s == null) { _details.Children.Clear(); _cropOff.Visibility = Visibility.Collapsed; _imgCaptionPill.Visibility = Visibility.Collapsed; _noImage.Visibility = Visibility.Visible; _imgHost.Visibility = Visibility.Collapsed; return; }
        string variant = _display == "crop" && !s.Manual && !s.ImageReplaced ? "crop" : (_variantOverride ?? DefaultVariant(s));
        var img = StepImages.Render(_view, s, variant, "original", annotate: true, viewMode: _display == "window" ? "window" : "screen", applyCrop: _tool != "crop", overview: _display == "crop", authorAnnotations: true);
        if (img == null && variant == "after") { img = StepImages.Render(_view, s, "before", "original", authorAnnotations: true); variant = "before"; }
        _showAfter = variant == "after";
        _syncBA = true; (_showAfter ? _afterBtn : _beforeBtn).IsChecked = true; _syncBA = false;
        if (img != null)
        {
            _current = U.ToSource(img.Bitmap);
            _img.Source = _current;
            if (s.Id != _animStepId) { _animStepId = s.Id; Anim.Refresh(_imgHost, 0.2, 240); Anim.Refresh(_details, 0.35, 220); }
            _imgHost.Width = img.Bitmap.Width; _imgHost.Height = img.Bitmap.Height;
            _overlay.Width = img.Bitmap.Width; _overlay.Height = img.Bitmap.Height;
            _currentMeta = img;
        }
        bool has = img != null;
        _imgHost.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        ((FrameworkElement)((Grid)_imgHost.Parent).Children[0]).Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        _noImage.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        _imgCaptionPill.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        var crop = _view.CropFor(s);
        string variantLabel = variant switch { "after" => Loc.L("Nachher-Bild", "After image"), "crop" => Loc.L("Ausschnitt", "Detail"), _ => Loc.L("Vorher-Bild", "Before image") };
        _imgCaption.Text = Loc.F("Schritt {0}", "Step {0}", s.Number) + "   ·   " + variantLabel + (crop != null && _tool != "crop" ? "   ·   " + Loc.L("zugeschnitten", "cropped") : "") + (img != null ? $"   ·   {img.Bitmap.Width} × {img.Bitmap.Height}" : "");
        _cropOff.Visibility = crop != null ? Visibility.Visible : Visibility.Collapsed;
        // Beim Zuschneiden: bisherigen Zuschnitt gestrichelt zeigen
        if (_tool == "crop" && crop is { } c && _currentMeta != null)
        {
            var tl = ToOverlay(new Point(c.X, c.Y)); var br = ToOverlay(new Point(c.Right, c.Bottom));
            var r = new Rectangle { Stroke = Theme.Accent, StrokeThickness = Px(2), StrokeDashArray = new DoubleCollection { 4, 3 }, Width = Math.Max(4, br.X - tl.X), Height = Math.Max(4, br.Y - tl.Y), IsHitTestVisible = false };
            Canvas.SetLeft(r, tl.X); Canvas.SetTop(r, tl.Y); _overlay.Children.Add(r);
        }
        _zoom.Refresh();
        DrawSelection();
        _stepPos.Text = $"{_list.SelectedIndex + 1} / {_list.Items.Count}";
        ShowDetails(s);
        UpdateStatus();
    }

    static string DefaultVariant(ViewStep s) => s.Kind is "type" or "hotkey" or "key" or "system" or "wait" && s.PostFrame != null ? "after" : "before";

    IEnumerable<JsonObject> AnnotationsFor(string frameId) => (_view.Edits.Arr("annotations") ?? new JsonArray()).OfType<JsonObject>().Where(a => a.Str("frame") == frameId);

    static TextBox ReadOnlyText(string t, Brush? fg = null, double size = Theme.FsBody, FontFamily? font = null) => new()
    {
        Text = t, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(-2, 0, 0, 0), TextWrapping = TextWrapping.Wrap,
        Foreground = fg ?? Theme.Fg, FontSize = size, FontFamily = font ?? Theme.UiFont, VerticalContentAlignment = VerticalAlignment.Top
    };

    /// <summary>Win32-Kontext für AutoHotkey: WinTitle, ClassNN, Koordinaten, Pixel, Fenstertexte, Code zum Kopieren.</summary>
    void Win32Section(ViewStep s, JsonObject w32, Action<string, UIElement> info)
    {
        var head = new DockPanel { Margin = new Thickness(0, 24, 0, 4) };
        var code = AhkExport.Snippet(s);
        if (code.Length > 0)
        {
            var copy = U.Btn(Theme.ICopy, Loc.L("Code kopieren", "Copy code"), () => { var d = new DataObject(); d.SetText(code); SetClipboard(d, Loc.L("AutoHotkey-Code kopiert", "AutoHotkey code copied")); }, Loc.L("AutoHotkey-v2-Code für diesen Schritt kopieren", "Copy AutoHotkey v2 code for this step"), "Link");
            DockPanel.SetDock(copy, Dock.Right); head.Children.Add(copy);
        }
        var ov = U.SectionLabel("AutoHotkey", new Thickness(0)); ov.VerticalAlignment = VerticalAlignment.Center; head.Children.Add(ov);
        _details.Children.Add(head);
        TextBox Mono(string t) => ReadOnlyText(t, Theme.Fg, Theme.FsCaption, Theme.Mono);
        var win = w32.Obj("window");
        if (w32.Obj("ahk")?.Str("winTitle") is { } wt)
            info("WinTitle", U.Col(Mono(wt), ReadOnlyText((win?.Str("title") is { } t ? Q(t) + " · " : "") + (win?.Str("state") ?? "") + (win?.LongN("dpi") is long dpi ? $" · {dpi} dpi" : ""), Theme.Muted, Theme.FsHint)));
        if (w32.Obj("control") is { } ctl)
        {
            var v = U.Col(Mono((ctl.Str("classNN") ?? ctl.Str("class") ?? "") + (ctl.LongN("id") is long id ? $"  · ID {id}" : "")));
            if (ctl.Str("text") is { } ct) v.Children.Add(ReadOnlyText(Loc.F("Text: „{0}“", "Text: “{0}”", ct), Theme.Fg2, Theme.FsHint));
            else if (ctl.Str("textWithheld") is { } tw) v.Children.Add(ReadOnlyText(tw switch { "password" => Loc.L("Passwortfeld – Inhalt nicht gelesen", "Password field – content not read"), "editable" => Loc.L("Eingabefeld – Inhalt nur über UIA", "Input field – content only via UIA"), _ => Loc.L("Text im Privatmodus nicht gelesen", "Text not read in private mode") }, Theme.Muted, Theme.FsHint));
            if (ctl.Obj("rect") is { } r) v.Children.Add(ReadOnlyText($"Client ({r.Int("x")}, {r.Int("y")}) · {r.Int("w")} × {r.Int("h")}", Theme.Muted, Theme.FsHint));
            info("Control", v);
        }
        if (w32.Obj("point") is { } pt)
        {
            string Xy(string k) => pt.Arr(k) is { Count: 2 } a ? $"{a[0]}, {a[1]}" : "–";
            string xyWindow = Xy("window"), xyClient = Xy("client"), xyScreen = Xy("screen");
            var lines = Loc.F("Fenster  {0}\nClient   {1}\nScreen   {2}", "Window  {0}\nClient   {1}\nScreen   {2}", xyWindow, xyClient, xyScreen);
            if (w32.Obj("control")?.Arr("point") is { Count: 2 } cp) lines += $"\nControl  {cp[0]}, {cp[1]}";
            info("Position", Mono(lines));
        }
        if (w32.Str("pixel") is { } px)
        {
            var swatch = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(4), BorderBrush = Theme.LineStrong, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            try { swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + px[2..])); } catch { }
            info("Pixel", U.Row(swatch, Mono(px)));
        }
        if (w32.Obj("focus")?.Str("classNN") is { } fnn) info(Loc.L("Fokus", "Focus"), Mono(fnn));
        if (w32.Str("cursor") is { } cur) info(Loc.L("Zeiger", "Pointer"), Mono(cur));
        var j = s.Json;
        if (j.Arr("selectors") is { Count: > 0 } sel)
        {
            var lines = sel.OfType<JsonObject>().Take(6).Select(x => $"{new string('●', x.Int("score"))}{new string('○', 5 - x.Int("score"))}  {x.Str("kind")}: " + (x.Str("kind") switch
            {
                "uia" => string.Join(" · ", new[] { x.Str("controlType"), x.Str("name") is { } n ? Q(n) : null, x.Str("automationId") is { } aid ? "AutomationId " + aid : null, x.LongN("index") is long ix ? Loc.F("Nr. {0}/{1}", "No. {0}/{1}", ix, x.LongN("of")) : null }.Where(t => t != null)),
                "label" => Loc.F("„{0}“ ({1})", "“{0}” ({1})", x.Str("text"), x.Str("relation") == "rightOf" ? Loc.L("links daneben", "to the left of it") : Loc.L("darüber", "above it")),
                "relative" => Loc.F("{0:0.#} % / {1:0.#} %", "{0:0.#}% / {1:0.#}%", x.Dbl("x") * 100, x.Dbl("y") * 100),
                "window" => $"{x.Int("x")}, {x.Int("y")}",
                "keyboard" => x.Str("accelerator") ?? x.Str("accessKey") ?? "",
                _ => x["value"]?.ToString() ?? ""
            }));
            info(Loc.L("Wege", "Selectors"), ReadOnlyText(string.Join("\n", lines), Theme.Fg, Theme.FsHint));
        }
        if (j.Obj("sync") is { } sy)
        {
            var parts = new List<string>();
            if (sy.LongN("busyMs") is long b) parts.Add(Loc.F("beschäftigt {0} ms ({1})", "busy {0} ms ({1})", b, sy.Str("busyShape")));
            if (sy.Obj("waitWindow")?.Str("title") is { } ww) parts.Add(Loc.F("Fenster „{0}“ nach {1} ms", "Window “{0}” after {1} ms", ww, sy.Obj("waitWindow")!.Long("afterMs")));
            if (sy.LongN("settleMs") is long st) parts.Add(Loc.F("Bild ruhig nach {0} ms", "Image settled after {0} ms", st));
            if (sy.LongN("readyMs") is long rd) parts.Add(Loc.F("bereit nach {0} ms", "ready after {0} ms", rd));
            if (sy.LongN("thinkMs") is long th) parts.Add(Loc.F("Denkpause {0} ms", "Thinking pause {0} ms", th));
            if (parts.Count > 0) info(Loc.L("Warten", "Waiting"), ReadOnlyText(string.Join("\n", parts), Theme.Fg, Theme.FsHint));
        }
        if (j.Obj("verify") is { } vf)
        {
            var parts = new List<string>();
            if (vf.Obj("titleChanged") is { } tc) parts.Add(Loc.F("Titel → „{0}“", "Title → “{0}”", tc.Str("to")));
            foreach (var ch in vf.Arr("changes")?.OfType<JsonObject>().Take(5) ?? Enumerable.Empty<JsonObject>())
                parts.Add(ch.Str("checked") is { } ck ? $"{ch.Str("c")}: {ck}" : ch.Str("to") is { } to ? $"{ch.Str("c")}: " + Q(to) : ch.Str("appeared") is { } ap ? Loc.F("{0} neu: „{1}“", "{0} new: “{1}”", ch.Str("c"), ap) : ch.Str("disappeared") is { } dp ? Loc.F("{0} weg", "{0} gone", ch.Str("c")) : Loc.F("{0} geändert", "{0} changed", ch.Str("c")));
            if (vf.Str("focus") is { } ff) parts.Add(Loc.F("Fokus danach {0}", "Focus afterwards {0}", ff));
            if (parts.Count > 0) info(Loc.L("Prüfung", "Verification"), ReadOnlyText(string.Join("\n", parts), Theme.Fg, Theme.FsHint));
        }
        if (j.Obj("menuPath")?.Arr("items") is { Count: > 0 } mi) info(Loc.L("Menü", "Menu"), ReadOnlyText(string.Join(" › ", mi.Select(x => x?.GetValue<string>())), Theme.Fg, Theme.FsHint));
        if (j.Obj("choose")?.Str("item") is { } chItem) info(Loc.L("Auswahl", "Selection"), ReadOnlyText(Q(chItem) + (j.Obj("choose")!.LongN("index") is long ci ? Loc.F(" (Nr. {0} von {1})", " (No. {0} of {1})", ci, j.Obj("choose")!.LongN("of")) : ""), Theme.Fg, Theme.FsHint));
        if (w32.Obj("window")?.Obj("app") is { } app)
            info(Loc.L("Programm", "Program"), ReadOnlyText(string.Join("\n", new[] { app.Str("product") is { } pr ? pr + (app.Str("version") is { } ver ? " " + ver : "") : null, app["elevated"]?.GetValue<bool>() == true ? Loc.L("läuft als Administrator", "runs as administrator") : null, app.Str("commandLine") }.Where(t => t != null)), Theme.Muted, Theme.FsHint));
        if (w32.Arr("texts") is { Count: > 0 } texts)
        {
            var sb = new StringBuilder();
            foreach (var t in texts) sb.Append(t.Str("c")).Append("  ").Append(t.Str("t")?.Replace("\n", " ")).AppendLine();
            _details.Children.Add(new Expander
            {
                Header = Loc.F("Fenstertexte ({0})", "Window texts ({0})", texts.Count), Margin = new Thickness(0, 10, 0, 0),
                Content = U.Inset(ReadOnlyText(sb.ToString().TrimEnd(), Theme.Fg2, Theme.FsCaption, Theme.Mono), new Thickness(14, 12, 14, 12))
            });
        }
    }

    static Border Callout(string glyph, Brush bg, Brush fg, string title, string text)
    {
        var dock = new DockPanel();
        var icon = U.Icon(glyph, 16, fg); icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new Thickness(0, 1, 12, 0);
        DockPanel.SetDock(icon, Dock.Left); dock.Children.Add(icon);
        dock.Children.Add(U.Col(U.Text(title, Theme.FsHint, true, fg, new Thickness(0, 0, 0, 2)), ReadOnlyText(text, Theme.Fg, Theme.FsHint)));
        return new Border { Background = bg, CornerRadius = new CornerRadius(Theme.RInset), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 12, 0, 4), Child = dock };
    }

    /// <summary>Für Entwickler-Schnappschüsse: Schritt in der Liste wählen.</summary>
    internal int StepCount => _list.Items.Count;
    internal void SelectStep(int i) { if (i >= 0 && i < _list.Items.Count) _list.SelectedIndex = i; }

    void StopPlay() { _play.Stop(); _playIcon.Text = Theme.IPlay; }

    readonly List<(TextBlock label, int level)> _labels = new();
    ColumnDefinition _leftCol = null!, _rightCol = null!;
    bool? _narrow;

    /// <summary>Passt die Oberfläche an schmale Fenster an: Beschriftungen ausblenden, Seitenleisten höchstens schmaler als gemerkt.</summary>
    void Adapt()
    {
        double w = ActualWidth;
        // Schwellen passend zur Kopfzeile in 14 px: erst „Abspielen“/„Vorschau“, dann die übrigen Beschriftungen ausblenden
        foreach (var (t, lvl) in _labels) t.Visibility = (lvl == 1 && w < 1400) || (lvl == 2 && w < 1220) || (lvl == 3 && w < 1500) ? Visibility.Collapsed : Visibility.Visible;
        bool narrow = w < 1240;
        if (_leftCol == null || narrow == _narrow) return;
        _narrow = narrow;
        // gemerkte Breiten; im schmalen Fenster höchstens die schmalen Standardbreiten
        _leftCol.Width = new GridLength(narrow ? Math.Min(_prefLeft, LeftNarrow) : _prefLeft);
        _rightCol.Width = new GridLength(narrow ? Math.Min(_prefRight, RightNarrow) : _prefRight);
    }

    static string MouseSummary(string kind, JsonObject mi)
    {
        var lines = new List<string>();
        string button = mi.Str("button") switch { "secondary" => Loc.L("rechte Taste", "right button"), "middle" => Loc.L("mittlere Taste", "middle button"), "x1" or "x2" => Loc.L("Zusatztaste", "extra button"), _ => Loc.L("linke Taste", "left button") };
        var mods = mi.StrList("mods").Select(x => x switch { "ctrl" => Loc.Keys("Ctrl"), "shift" => Loc.Keys("Shift"), "alt" => "Alt", "win" => "Win", _ => x }).ToList();
        lines.Add((mods.Count > 0 ? string.Join("+", mods) + " + " : "") + button);
        if (kind == "drag")
        {
            lines.Add(Loc.F("gedrückt {0} ms · Weg {1} px", "pressed {0} ms · path {1} px", mi.Long("durationUs") / 1000, mi.Long("distancePx")));
            if (mi.Long("holdBeforeMoveUs") >= 100_000) lines.Add(Loc.F("vor der Bewegung gehalten {0} ms", "held {0} ms before moving", mi.Long("holdBeforeMoveUs") / 1000));
            if (mi.Long("holdAtEndUs") >= 100_000) lines.Add(Loc.F("über dem Ziel verweilt {0} ms", "stayed over the target {0} ms", mi.Long("holdAtEndUs") / 1000));
        }
        else if (mi.LongN("pressUs") is long pu) lines.Add(Loc.F("gedrückt {0} ms", "pressed {0} ms", pu / 1000) + (mi.Bool("longPress") ? Loc.L(" (gedrückt gehalten)", " (held down)") : ""));
        return string.Join("\n", lines);
    }

    // ---------------- Mausmarkierungen ----------------

    static (string key, string label)[] MarkKeys => new[] { ("clickMarker", Loc.L("Klickpunkt", "Click point")), ("dragPath", Loc.L("Zug- und Haltepfad", "Drag and hold path")), ("elementFrame", Loc.L("Zielrahmen", "Target frame")), ("labels", Loc.L("Hinweise (Haltedauer, Scrollrichtung)", "Notes (hold time, scroll direction)")), ("approachPath", Loc.L("Mausweg davor", "Mouse path before")), ("keyCaps", Loc.L("Tasten und Texteingaben", "Keys and typed text")) };

    static bool MarkOf(MouseMarks m, string key) => key switch { "clickMarker" => m.Click, "dragPath" => m.Path, "elementFrame" => m.Target, "labels" => m.Labels, "keyCaps" => m.KeyCaps, _ => m.Approach };

    void ShowMarksMenu(FrameworkElement target)
    {
        var s = Sel;
        var all = _view.MarksFor(null);
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        void Head(string t) => m.Items.Add(new MenuItem { Header = new TextBlock { Text = t, FontSize = Theme.FsCaption, FontWeight = FontWeights.SemiBold, Foreground = Theme.Muted }, IsHitTestVisible = false, Focusable = false, Padding = new Thickness(10, 8, 10, 4) });
        void Item(string label, bool on, Action a, string gesture = "")
        {
            var mi = new MenuItem { Header = label, InputGestureText = gesture, Icon = on ? U.Icon(Theme.ICheck, 14, Theme.AccentText) : null };
            mi.Click += (_, _) => { try { a(); } catch (Exception ex) { UiApp.ShowError(ex); } };
            m.Items.Add(mi);
        }
        Head(Loc.L("Alle Schritte", "All steps"));
        foreach (var (key, label) in MarkKeys) { bool on = MarkOf(all, key); Item(label, on, () => SetMark(null, key, !on)); }
        if (s != null)
        {
            m.Items.Add(new Separator());
            Head(Loc.F("Nur Schritt {0}", "Step {0} only", s.Number));
            var cur = _view.MarksFor(s);
            Item(cur.Any ? Loc.L("Alle Markierungen ausblenden", "Hide all markers") : Loc.L("Markierungen einblenden", "Show markers"), false, ToggleStepMarks, "H");
            if (StepMarksEdit(s) != null) Item(Loc.L("Wie alle Schritte", "Same as all steps"), false, () => Change(e => StepEdit(e, s.Anchor).Remove("auto"), refill: false));
        }
        m.IsOpen = true;
    }

    JsonObject? StepMarksEdit(ViewStep s) => (_view.Edits.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(x => x.Long("anchor") == s.Anchor)?.Obj("auto");

    /// <summary>Setzt eine Markierungsart für alle Schritte (<paramref name="step"/> null) oder nur einen Schritt.</summary>
    void SetMark(ViewStep? step, string key, bool on) => Change(e =>
    {
        JsonObject holder;
        if (step == null) { holder = e.Obj("guide") ?? new JsonObject(); if (e.Obj("guide") == null) e["guide"] = holder; }
        else holder = StepEdit(e, step.Anchor);
        var mk = holder.Obj("auto");
        if (mk == null) { mk = new JsonObject(); holder["auto"] = mk; }
        mk[key] = on;
    }, refill: false);

    void ToggleStepMarks()
    {
        var s = Sel; if (s == null) return;
        bool show = !_view.MarksFor(s).Any;
        var all = _view.MarksFor(null);
        Change(e =>
        {
            var se = StepEdit(e, s.Anchor);
            var auto = se.Obj("auto");
            foreach (var k in MouseMarks.Keys) auto?.Remove(k);
            if (auto == null) { auto = new JsonObject(); se["auto"] = auto; }
            if (!show) foreach (var k in MouseMarks.Keys) auto[k] = false;
            else if (!all.Any) foreach (var k in MouseMarks.Keys.Where(k => k != "approachPath")) auto[k] = true;
            if (auto.Count == 0) se.Remove("auto");
        }, refill: false);
        _status.Text = show ? Loc.F("Mausmarkierungen bei Schritt {0} eingeblendet", "Mouse markers shown for step {0}", s.Number) : Loc.F("Mausmarkierungen bei Schritt {0} ausgeblendet", "Mouse markers hidden for step {0}", s.Number);
    }

    static string EffectSummary(JsonObject eff)
    {
        var parts = new List<string>();
        foreach (var w in eff.Arr("windowsOpened") ?? new JsonArray()) parts.Add(Loc.F("„{0}“ erschien", "“{0}” appeared", w.Str("title")));
        foreach (var w in eff.Arr("windowsClosed") ?? new JsonArray()) parts.Add(Loc.F("„{0}“ geschlossen", "“{0}” closed", w.Str("title")));
        foreach (var w in eff.Arr("titleChanges") ?? new JsonArray()) parts.Add(Loc.F("Titel → „{0}“", "Title → “{0}”", w.Str("to")));
        if (eff.Obj("screen") is { } sc && sc.Dbl("changedRatio") > 0) parts.Add(Loc.F("Bildschirm {0:0.0} % geändert", "Screen {0:0.0}% changed", sc.Dbl("changedRatio") * 100));
        return parts.Count == 0 ? "–" : string.Join(" · ", parts);
    }

    // =====================================================================================
    // Overlay-Bearbeitung mit Rückgängig
    // =====================================================================================

    void Change(Action<JsonObject> edit, bool refill = true)
    {
        if (_view.Session.ReadOnly) { Msg.Show(Loc.L("Diese Sitzung ist schreibgeschützt (.steprec). Bitte entpacken, um sie zu bearbeiten.", "This recording is read-only (.steprec). Please extract it to edit it.")); return; }
        _undo.Add(Json.Line(_view.Edits)); _redo.Clear();
        string ImageEdits() => (_view.Edits["redactions"]?.ToJsonString() ?? "") + (_view.Edits["substitutions"]?.ToJsonString() ?? "");
        string before = ImageEdits();
        edit(_view.Edits);
        if (!_view.SaveEdits()) { _view.ReloadEdits(); Msg.Show(Loc.L("Die Sitzung wurde zwischenzeitlich geändert; bitte erneut versuchen.", "The recording was changed in the meantime; please try again.")); }
        bool allImages = before != ImageEdits();
        if (allImages) _thumbs.Clear(); else if (Sel is { } cur) _thumbs.Remove(cur.Id);
        QualityInvalidate();
        if (refill) Fill();
        else { ShowStep(); if (allImages) InvalidateAllThumbs(); else InvalidateThumb(Sel); }
        UpdateUndo();
    }

    void Undo() { if (_undo.Count == 0) return; _redo.Add(Json.Line(_view.Edits)); Restore(_undo[^1]); _undo.RemoveAt(_undo.Count - 1); UpdateUndo(); _status.Text = Loc.L("Rückgängig gemacht", "Undone"); }
    void Redo() { if (_redo.Count == 0) return; _undo.Add(Json.Line(_view.Edits)); Restore(_redo[^1]); _redo.RemoveAt(_redo.Count - 1); UpdateUndo(); _status.Text = Loc.L("Wiederholt", "Redone"); }
    void Restore(string json)
    {
        var rev = _view.Edits.Long("revision");
        var restored = (JsonObject)JsonNode.Parse(json)!;
        restored["revision"] = rev;
        foreach (var k in _view.Edits.Select(x => x.Key).ToList()) _view.Edits.Remove(k);
        foreach (var kv in restored.ToList()) { restored.Remove(kv.Key); _view.Edits[kv.Key] = kv.Value; }
        _view.SaveEdits();
        _thumbs.Clear(); _selAnn = null;
        QualityInvalidate();
        Fill();
    }

    JsonObject StepEdit(JsonObject edits, long anchor)
    {
        var arr = edits["steps"] as JsonArray ?? new JsonArray();
        edits["steps"] = arr;
        var se = arr.OfType<JsonObject>().FirstOrDefault(x => x.Long("anchor") == anchor);
        if (se == null) { se = new JsonObject { ["anchor"] = anchor }; arr.Add(se); }
        return se;
    }

    static string NextId(JsonObject edits, string arrName, string prefix)
        => prefix + ((edits.Arr(arrName) ?? new JsonArray()).Select(x => int.TryParse(x.Str("id")?[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1);

    void Rename()
    {
        var d = new InputDialog(Loc.L("Titel der Anleitung", "Guide title"), _view.Title) { Owner = this };
        if (d.ShowDialog() != true) return;
        Change(e => { var g = e.Obj("guide") ?? new JsonObject(); g["title"] = d.Value; e["guide"] = g; });
        _titleBlock.Text = _view.Title;
    }

    void EditText()
    {
        var s = Sel; if (s == null) return;
        var d = new InputDialog(Loc.L("Text des Schritts", "Step text"),s.AuthorBody ?? s.Guide, s.AuthorTitle ?? s.Title) { Owner = this };
        if (d.ShowDialog() != true) return;
        Change(e =>
        {
            var se = StepEdit(e, s.Anchor);
            var text = se["text"] as JsonObject ?? new JsonObject();
            text[_view.Language] = new JsonObject { ["title"] = d.TitleValue, ["body"] = d.Value, ["by"] = "user", ["atUtc"] = Json.UtcNow() };
            se["text"] = text;
            MarkUnreviewedComment(e);
        });
    }

    void MarkUnreviewedComment(JsonObject e) { }

    void AddComment()
    {
        var s = Sel; if (s == null) return;
        var d = new InputDialog(Loc.L("Kommentar", "Comment"), "", null, kinds: true) { Owner = this };
        if (d.ShowDialog() != true || d.Value.Trim().Length == 0) return;
        Change(e =>
        {
            var arr = e["comments"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = s.Anchor, ["kind"] = d.Kind, ["text"] = d.Value.Trim(), ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" });
            e["comments"] = arr;
        });
    }

    void RemoveComment(string? id) => Change(e => { if (e["comments"] is JsonArray arr) foreach (var c in arr.Where(c => c.Str("id") == id).ToList()) arr.Remove(c); });

    void InsertNote()
    {
        var s = Sel; if (s == null) return;
        var d = new InputDialog(Loc.L("Hinweis einfügen (als Kommentar am Schritt)", "Insert note (as a comment on the step)"),"", null, kinds: true) { Owner = this };
        if (d.ShowDialog() != true) return;
        Change(e => { var arr = e["comments"] as JsonArray ?? new JsonArray(); arr.Add(new JsonObject { ["id"] = NextId(e, "comments", "uc"), ["anchor"] = s.Anchor, ["kind"] = d.Kind, ["text"] = d.Value, ["createdUtc"] = Json.UtcNow(), ["by"] = "user", ["origin"] = "viewer" }); e["comments"] = arr; });
    }

    void ToggleHidden()
    {
        var s = Sel; if (s == null) return;
        if (_list.SelectedItems.Count > 1) { bool hide = !SelSteps[0].Hidden; BulkEdit(SelSteps, se => se["hidden"] = hide); return; }
        Change(e => StepEdit(e, s.Anchor)["hidden"] = !s.Hidden);
    }

    void ChangeImportance(int dir)
    {
        var s = Sel; if (s == null) return;
        string[] order = { "trace", "secondary", "primary" };
        int i = Math.Clamp(Array.IndexOf(order, s.Importance) + dir, 0, 2);
        Change(e => StepEdit(e, s.Anchor)["importance"] = order[i]);
    }

    /// <summary>Um einen sichtbaren Listenplatz verschieben (unsichtbare Spur-Schritte dazwischen zählen nicht).</summary>
    void Move(int dir)
    {
        var s = Sel; if (s == null || _list.SelectedItems.Count > 1) return;
        int j = _list.SelectedIndex + dir;
        if (j < 0 || j >= _list.Items.Count || ((ListBoxItem)_list.Items[j]).Tag is not ViewStep dst) return;
        MoveTo(s, dst);
    }

    void RedactStep()
    {
        var s = Sel; if (s == null) return;
        var ranges = s.Json.Obj("seq")?.Arr("input")?.DeepClone() as JsonArray ?? new JsonArray(new JsonArray(s.Anchor, s.Anchor));
        if (ranges.Count == 0) ranges.Add(new JsonArray(s.Anchor, s.Anchor));
        Change(e =>
        {
            var arr = e["redactions"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject { ["id"] = NextId(e, "redactions", "r"), ["kind"] = "range", ["origin"] = "step", ["anchor"] = s.Anchor, ["seqRanges"] = ranges, ["images"] = true, ["createdUtc"] = Json.UtcNow(), ["by"] = "user" });
            e["redactions"] = arr;
        });
    }

    /// <summary>Text überall schwärzen: sucht alle Fundstellen; gespeichert werden nur Ziele (seq + Feld), nie der Text.</summary>
    void RedactText()
    {
        var d = new InputDialog(Loc.L("Welcher Text soll überall geschwärzt werden?", "Which text should be redacted everywhere?"), "") { Owner = this };
        if (d.ShowDialog() != true || d.Value.Length < 2) return;
        string needle = d.Value;
        var targets = new JsonArray();
        foreach (var ev in _view.Session.Events)
            foreach (var (path, val) in StringFields(ev, ""))
            {
                int idx = val.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) targets.Add(new JsonObject { ["seq"] = ev.Long("seq"), ["field"] = path, ["range"] = new JsonArray(idx, idx + needle.Length) });
            }
        // getippte Zeichen: alle key.down-Ereignisse eines Laufs, deren Zeichenfolge den Text ergibt
        foreach (var st in _view.All.Where(x => x.Kind == "type"))
            if ((st.Json.Obj("input")?.Str("text") ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase))
                foreach (var rg in st.Json.Obj("seq")?.Arr("input") ?? new JsonArray())
                    for (long q = rg![0]!.GetValue<long>(); q <= rg[1]!.GetValue<long>(); q++)
                        if (_view.Session.BySeq(q) is { } ke && ke.Str("ch") != null) targets.Add(new JsonObject { ["seq"] = q, ["field"] = "ch" });
        if (targets.Count == 0) { Msg.Show(Loc.L("Keine Fundstelle.", "No matches found.")); return; }
        Change(e =>
        {
            var arr = e["redactions"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject { ["id"] = NextId(e, "redactions", "r"), ["kind"] = "text", ["targets"] = targets, ["createdUtc"] = Json.UtcNow(), ["by"] = "user" });
            e["redactions"] = arr;
        });
        Msg.Show(Loc.F("{0} Fundstellen geschwärzt (Texte). Im Bild sichtbare Stellen bitte zusätzlich mit dem Werkzeug „Schwärzen“ markieren.", "{0} matches redacted (texts). Please also mark places visible in the image with the “Redact” tool.", targets.Count), Loc.L("Schwärzen", "Redact"));
    }

    static IEnumerable<(string path, string value)> StringFields(JsonNode? n, string prefix)
    {
        switch (n)
        {
            case JsonObject o:
                foreach (var kv in o)
                {
                    if (kv.Key is "type" or "blob" or "id" or "win" or "hwnd" or "rid" or "hkl" or "key" or "ct" or "kind" or "trigger" or "status" or "src" or "phase" or "cause") continue;
                    foreach (var x in StringFields(kv.Value, prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key)) yield return x;
                }
                break;
            case JsonArray a:
                for (int i = 0; i < a.Count; i++) foreach (var x in StringFields(a[i], $"{prefix}[{i}]")) yield return x;
                break;
            case JsonValue v when v.TryGetValue<string>(out var s):
                yield return (prefix, s);
                break;
        }
    }

    void Generalize()
    {
        var user = Environment.UserName;
        var d = new InputDialog(Loc.L("Persönlichen Wert verallgemeinern – Wert (z. B. Benutzername):", "Generalize a personal value – value (e.g. user name):"), user, Loc.L("Benutzername", "User name")) { Owner = this };
        d.TitleLabel = Loc.L("Platzhalter", "Placeholder");
        if (d.ShowDialog() != true || d.Value.Trim().Length < 3) return;
        string ph = new string(d.TitleValue.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-').ToArray()).Trim();
        if (ph.Length == 0) ph = _view.Language == "en" ? "Value" : "Wert";
        Change(e =>
        {
            var arr = e["substitutions"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject { ["id"] = NextId(e, "substitutions", "sb"), ["match"] = new JsonObject { ["literal"] = d.Value.Trim() }, ["placeholder"] = ph, ["caseSensitive"] = false, ["scope"] = "all", ["targets"] = new JsonArray("text"), ["suggestion"] = d.Value.Trim() == user ? "userName" : null, ["createdUtc"] = Json.UtcNow(), ["by"] = "user" }.Clean());
            e["substitutions"] = arr;
        });
    }

    void FinalRedact()
    {
        if (Msg.Show(Loc.L("Alle Text- und Zeitraum-Schwärzungen jetzt endgültig in die Rohdaten schreiben?\n\nDas lässt sich nicht rückgängig machen. Die Bilder geschwärzter Zeiträume werden dabei aus der Aufnahme gelöscht.\n\nHinweis: Bildschwärzungen, Verpixelungen und Verallgemeinerungen bleiben eine Bearbeitung. Für die Weitergabe der ganzen Aufnahme die bereinigte Kopie verwenden (Alle Aufnahmen → Packen).",
                "Write all text and time-range redactions permanently into the raw data now?\n\nThis cannot be undone. Images of redacted time ranges are deleted from the recording.\n\nNote: image redactions, pixelation and generalizations remain edits. To share the whole recording, use the sanitized copy (All recordings → Pack)."),
                Loc.L("Endgültig schwärzen", "Redact permanently"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        int n = Redactor.ApplyTextRedactionsDestructive(_view.Session.Path, _view.Edits);
        _view.SaveEdits();
        var anchor = Sel?.Id;
        Load(); _thumbs.Clear(); Fill(anchor);
        Msg.Show(Loc.N(n, "1 Stelle endgültig geschwärzt.", "{0} Stellen endgültig geschwärzt.", "1 item redacted permanently.", "{0} items redacted permanently."));
    }

    void ClearAnnotations()
    {
        if (_currentMeta == null) return;
        string f = _currentMeta.FrameId;
        Change(e => { if (e["annotations"] is JsonArray a) foreach (var x in a.Where(x => x.Str("frame") == f).ToList()) a.Remove(x); }, refill: false);
    }

    // ---------------- Zeichnen auf dem Bild ----------------

    Point ToImage(Point p) => _currentMeta == null ? p : new Point(p.X / _currentMeta.Scale + _currentMeta.SourceRect.X, p.Y / _currentMeta.Scale + _currentMeta.SourceRect.Y);

    void AddImageRedaction(int x, int y, int w, int h, string style = "black")
    {
        if (_currentMeta == null) return;
        string frame = _currentMeta.FrameId, m = _currentMeta.Monitor;
        Change(e =>
        {
            var arr = e["redactions"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject
            {
                ["id"] = NextId(e, "redactions", "r"), ["kind"] = "image", ["areas"] = new JsonArray(new JsonObject { ["m"] = m, ["from"] = frame, ["to"] = null, ["rect"] = new JsonArray(x, y, w, h) }),
                ["style"] = style, ["rule"] = new JsonObject { ["kind"] = "explicit", ["origin"] = new JsonObject { ["frame"] = frame, ["rect"] = new JsonArray(x, y, w, h) } }, ["createdUtc"] = Json.UtcNow(), ["by"] = "user"
            });
            e["redactions"] = arr;
        }, refill: false);
    }

    void ToggleBeforeAfter() => (_showAfter ? _beforeBtn : _afterBtn).IsChecked = true;
    void TogglePlay() { if (_play.IsEnabled) StopPlay(); else { if (_list.SelectedIndex >= _list.Items.Count - 1) _list.SelectedIndex = 0; _play.Start(); _playIcon.Text = Theme.IPause; } }

    protected override void OnClosed(EventArgs e) { base.OnClosed(e); _play.Stop(); _currentMeta?.Dispose(); _view.Dispose(); }
}
