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
    readonly TextBlock _status = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBox _search = new() { Tag = "Schritte durchsuchen", Padding = new Thickness(32, 7, 8, 7), ToolTip = "Suche (Strg+F)" };
    ToggleButton _allToggle = null!;
    RadioButton _beforeBtn = null!, _afterBtn = null!;
    readonly TextBlock _titleBlock = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "Klicken zum Umbenennen", TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _metaBlock = new() { FontSize = 12, Margin = new Thickness(0, 1, 0, 0) };
    readonly TextBlock _listCount = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _imgCaption = new() { FontSize = 12, Foreground = Brushes.White };
    readonly Border _imgCaptionPill;
    readonly TextBlock _noImage = new() { Text = "Für diesen Schritt gibt es kein Bild.", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    readonly TextBlock _reviewCount = new() { FontSize = 11, FontWeight = FontWeights.SemiBold };
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
        _status.Foreground = Theme.Muted; _metaBlock.Foreground = Theme.Muted; _listCount.Foreground = Theme.Muted; _noImage.Foreground = Theme.Muted; _reviewCount.Foreground = Theme.Warn;
        _imgCaptionPill = new Border { Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x17, 0x16, 0x14)), CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 5, 12, 6), Child = _imgCaption, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 0, 0), IsHitTestVisible = false };
        Load();
        BuildLayout();
        Fill();
        PreviewKeyDown += OnKey;
        _play.Tick += (_, _) => { if (_list.SelectedIndex < _list.Items.Count - 1) _list.SelectedIndex++; else StopPlay(); };
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0 && Session.LooksLikeSession(f[0])) UiApp.Instance.OpenViewer(f[0]); };
    }

    void Load()
    {
        _view?.Dispose();
        _view = SessionView.Open(_path, headless: false, lang: AppSettings.Load().Language);
        if (_view.Edits.Obj("guide")?["address"] == null && AppSettings.Load().Address == "informal")
        { var g = _view.Edits.Obj("guide") ?? new JsonObject(); g["address"] = "informal"; _view.Edits["guide"] = g; _view.Rebuild(); }
        ApplyDefaultTemplate();
        Title = _view.Title + " – " + AppInfo.DisplayName;
    }

    Button MenuButton(string glyph, string text, string tip, string? style, Func<(string label, Action action)[]> items)
    {
        var content = (StackPanel)U.IconText(glyph, text);
        content.Children.Add(new TextBlock { Text = Theme.IDown, FontFamily = Theme.Icons, FontSize = 9, Margin = new Thickness(8, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 });
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
            var mt = System.Text.RegularExpressions.Regex.Match(label, @"^(.*?)\s*\((K|[^()]*(?:Strg|Alt|Umschalt|Entf|Einfg|F\d)[^()]*)\)$");
            var mi = new MenuItem { Header = mt.Success ? mt.Groups[1].Value : label, InputGestureText = mt.Success ? mt.Groups[2].Value : "" };
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
        "click" => "Klick", "doubleClick" => "Doppelklick", "tripleClick" => "Dreifachklick", "rightClick" => "Rechtsklick", "drag" => "Ziehen", "scroll" => "Scrollen",
        "type" => "Eingabe", "hotkey" => "Tastenkürzel", "key" => "Taste", "hover" => "Zeigen", "windowSwitch" => "Fensterwechsel", "system" => "System", "wait" => "Warten",
        "comment" => "Kommentar", "snapshot" => "Schnappschuss", "marker" => "Marker", "gap" => "Lücke", "move" => "Mausbewegung", "manual" => "Eingefügt", _ => kind
    };

    static string ImportanceLabel(string imp) => imp switch { "primary" => "Hauptschritt", "secondary" => "Nebenschritt", _ => "Spur" };

    void UpdateStatus()
    {
        int redactions = _view.Edits.Arr("redactions")?.Count ?? 0;
        var s = Sel;
        var parts = new List<string> { s != null ? $"Schritt {s.Number} von {_view.All.Count}" : $"{_view.All.Count} Schritte", "Automatisch gespeichert" };
        if (redactions > 0) parts.Add($"{redactions} Schwärzungen");
        parts.Add(_view.UnreviewedCount > 0 ? $"{_view.UnreviewedCount} Eingaben ungeprüft" : "Eingaben geprüft");
        parts.Add(_view.Language == "de" ? "Deutsch" : "English");
        if (_view.Session.ReadOnly) parts.Add("schreibgeschützt");
        if (_view.Session.State == "recovered") parts.Add("nach Absturz wiederhergestellt");
        _status.Text = string.Join("   ·   ", parts);
        _reviewCount.Text = _view.UnreviewedCount.ToString();
        _reviewBadge.Visibility = _view.UnreviewedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        int primary = _view.All.Count(x => x.Importance == "primary");
        _metaBlock.Text = $"{_view.Session.StartUtc.ToLocalTime():dd.MM.yyyy, HH:mm} Uhr   ·   {primary} Hauptschritte   ·   {Fmt.Time(_view.Session.DurationUs, "de")}";
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
        _imgCaption.Text = $"Schritt {s.Number}   ·   {(variant switch { "after" => "Nachher-Bild", "crop" => "Ausschnitt", _ => "Vorher-Bild" })}{(crop != null && _tool != "crop" ? "   ·   zugeschnitten" : "")}{(img != null ? $"   ·   {img.Bitmap.Width} × {img.Bitmap.Height}" : "")}";
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

    static TextBox ReadOnlyText(string t, Brush? fg = null, double size = 13, FontFamily? font = null) => new()
    {
        Text = t, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(-2, 0, 0, 0), TextWrapping = TextWrapping.Wrap,
        Foreground = fg ?? Theme.Fg, FontSize = size, FontFamily = font ?? Theme.UiFont, VerticalContentAlignment = VerticalAlignment.Top
    };

    /// <summary>Win32-Kontext für AutoHotkey: WinTitle, ClassNN, Koordinaten, Pixel, Fenstertexte, Code zum Kopieren.</summary>
    void Win32Section(ViewStep s, JsonObject w32, Action<string, UIElement> info)
    {
        var head = new DockPanel { Margin = new Thickness(0, 22, 0, 2) };
        var code = AhkExport.Snippet(s);
        if (code.Length > 0)
        {
            var copy = U.Btn(Theme.ICopy, "Code kopieren", () => { var d = new DataObject(); d.SetText(code); SetClipboard(d, "AutoHotkey-Code kopiert"); }, "AutoHotkey-v2-Code für diesen Schritt kopieren", "Link");
            DockPanel.SetDock(copy, Dock.Right); head.Children.Add(copy);
        }
        var ov = U.Overline("AutoHotkey", new Thickness(0, 8, 0, 0)); head.Children.Add(ov);
        _details.Children.Add(head);
        TextBox Mono(string t) => ReadOnlyText(t, Theme.Fg, 12, Theme.Mono);
        var win = w32.Obj("window");
        if (w32.Obj("ahk")?.Str("winTitle") is { } wt)
            info("WinTitle", U.Col(Mono(wt), ReadOnlyText((win?.Str("title") is { } t ? $"„{t}“ · " : "") + (win?.Str("state") ?? "") + (win?.LongN("dpi") is long dpi ? $" · {dpi} dpi" : ""), Theme.Muted, 12)));
        if (w32.Obj("control") is { } ctl)
        {
            var v = U.Col(Mono((ctl.Str("classNN") ?? ctl.Str("class") ?? "") + (ctl.LongN("id") is long id ? $"  · ID {id}" : "")));
            if (ctl.Str("text") is { } ct) v.Children.Add(ReadOnlyText($"Text: „{ct}“", Theme.Fg2, 12));
            else if (ctl.Str("textWithheld") is { } tw) v.Children.Add(ReadOnlyText(tw switch { "password" => "Passwortfeld – Inhalt nicht gelesen", "editable" => "Eingabefeld – Inhalt nur über UIA", _ => "Text im Privatmodus nicht gelesen" }, Theme.Muted, 12));
            if (ctl.Obj("rect") is { } r) v.Children.Add(ReadOnlyText($"Client ({r.Int("x")}, {r.Int("y")}) · {r.Int("w")} × {r.Int("h")}", Theme.Muted, 12));
            info("Control", v);
        }
        if (w32.Obj("point") is { } pt)
        {
            string Xy(string k) => pt.Arr(k) is { Count: 2 } a ? $"{a[0]}, {a[1]}" : "–";
            var lines = $"Fenster  {Xy("window")}\nClient   {Xy("client")}\nScreen   {Xy("screen")}";
            if (w32.Obj("control")?.Arr("point") is { Count: 2 } cp) lines += $"\nControl  {cp[0]}, {cp[1]}";
            info("Position", Mono(lines));
        }
        if (w32.Str("pixel") is { } px)
        {
            var swatch = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), BorderBrush = Theme.LineStrong, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            try { swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + px[2..])); } catch { }
            info("Pixel", U.Row(swatch, Mono(px)));
        }
        if (w32.Obj("focus")?.Str("classNN") is { } fnn) info("Fokus", Mono(fnn));
        if (w32.Str("cursor") is { } cur) info("Zeiger", Mono(cur));
        var j = s.Json;
        if (j.Arr("selectors") is { Count: > 0 } sel)
        {
            var lines = sel.OfType<JsonObject>().Take(6).Select(x => $"{new string('●', x.Int("score"))}{new string('○', 5 - x.Int("score"))}  {x.Str("kind")}: " + (x.Str("kind") switch
            {
                "uia" => string.Join(" · ", new[] { x.Str("controlType"), x.Str("name") is { } n ? "„" + n + "“" : null, x.Str("automationId") is { } aid ? "AutomationId " + aid : null, x.LongN("index") is long ix ? $"Nr. {ix}/{x.LongN("of")}" : null }.Where(t => t != null)),
                "label" => $"„{x.Str("text")}“ ({(x.Str("relation") == "rightOf" ? "links daneben" : "darüber")})",
                "relative" => $"{x.Dbl("x") * 100:0.#} % / {x.Dbl("y") * 100:0.#} %",
                "window" => $"{x.Int("x")}, {x.Int("y")}",
                "keyboard" => x.Str("accelerator") ?? x.Str("accessKey") ?? "",
                _ => x["value"]?.ToString() ?? ""
            }));
            info("Wege", ReadOnlyText(string.Join("\n", lines), Theme.Fg, 12));
        }
        if (j.Obj("sync") is { } sy)
        {
            var parts = new List<string>();
            if (sy.LongN("busyMs") is long b) parts.Add($"beschäftigt {b} ms ({sy.Str("busyShape")})");
            if (sy.Obj("waitWindow")?.Str("title") is { } ww) parts.Add($"Fenster „{ww}“ nach {sy.Obj("waitWindow")!.Long("afterMs")} ms");
            if (sy.LongN("settleMs") is long st) parts.Add($"Bild ruhig nach {st} ms");
            if (sy.LongN("readyMs") is long rd) parts.Add($"bereit nach {rd} ms");
            if (sy.LongN("thinkMs") is long th) parts.Add($"Denkpause {th} ms");
            if (parts.Count > 0) info("Warten", ReadOnlyText(string.Join("\n", parts), Theme.Fg, 12));
        }
        if (j.Obj("verify") is { } vf)
        {
            var parts = new List<string>();
            if (vf.Obj("titleChanged") is { } tc) parts.Add($"Titel → „{tc.Str("to")}“");
            foreach (var ch in vf.Arr("changes")?.OfType<JsonObject>().Take(5) ?? Enumerable.Empty<JsonObject>())
                parts.Add(ch.Str("checked") is { } ck ? $"{ch.Str("c")}: {ck}" : ch.Str("to") is { } to ? $"{ch.Str("c")}: „{to}“" : ch.Str("appeared") is { } ap ? $"{ch.Str("c")} neu: „{ap}“" : ch.Str("disappeared") is { } dp ? $"{ch.Str("c")} weg" : $"{ch.Str("c")} geändert");
            if (vf.Str("focus") is { } ff) parts.Add($"Fokus danach {ff}");
            if (parts.Count > 0) info("Prüfung", ReadOnlyText(string.Join("\n", parts), Theme.Fg, 12));
        }
        if (j.Obj("menuPath")?.Arr("items") is { Count: > 0 } mi) info("Menü", ReadOnlyText(string.Join(" › ", mi.Select(x => x?.GetValue<string>())), Theme.Fg, 12));
        if (j.Obj("choose")?.Str("item") is { } chItem) info("Auswahl", ReadOnlyText($"„{chItem}“" + (j.Obj("choose")!.LongN("index") is long ci ? $" (Nr. {ci} von {j.Obj("choose")!.LongN("of")})" : ""), Theme.Fg, 12));
        if (w32.Obj("window")?.Obj("app") is { } app)
            info("Programm", ReadOnlyText(string.Join("\n", new[] { app.Str("product") is { } pr ? pr + (app.Str("version") is { } ver ? " " + ver : "") : null, app["elevated"]?.GetValue<bool>() == true ? "läuft als Administrator" : null, app.Str("commandLine") }.Where(t => t != null)), Theme.Muted, 12));
        if (w32.Arr("texts") is { Count: > 0 } texts)
        {
            var sb = new StringBuilder();
            foreach (var t in texts) sb.Append(t.Str("c")).Append("  ").Append(t.Str("t")?.Replace("\n", " ")).AppendLine();
            _details.Children.Add(new Expander
            {
                Header = $"Fenstertexte ({texts.Count})", Margin = new Thickness(0, 10, 0, 0),
                Content = new Border { Background = Theme.Surface2, CornerRadius = new CornerRadius(5), Padding = new Thickness(12, 10, 12, 10), Child = ReadOnlyText(sb.ToString().TrimEnd(), Theme.Fg2, 11.5, Theme.Mono) }
            });
        }
    }

    static Border Callout(string glyph, Brush bg, Brush fg, string title, string text)
    {
        var dock = new DockPanel();
        var icon = U.Icon(glyph, 14, fg); icon.VerticalAlignment = VerticalAlignment.Top; icon.Margin = new Thickness(0, 2, 10, 0);
        DockPanel.SetDock(icon, Dock.Left); dock.Children.Add(icon);
        dock.Children.Add(U.Col(U.Text(title, 12, true, fg), ReadOnlyText(text, Theme.Fg, 13)));
        return new Border { Background = bg, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 10, 10, 10), Margin = new Thickness(0, 12, 0, 0), Child = dock };
    }

    /// <summary>Für Entwickler-Schnappschüsse: Schritt in der Liste wählen.</summary>
    internal int StepCount => _list.Items.Count;
    internal void SelectStep(int i) { if (i >= 0 && i < _list.Items.Count) _list.SelectedIndex = i; }

    void StopPlay() { _play.Stop(); _playIcon.Text = Theme.IPlay; }

    readonly List<(TextBlock label, int level)> _labels = new();
    ColumnDefinition _leftCol = null!, _rightCol = null!;
    bool? _narrow;

    /// <summary>Passt die Oberfläche an schmale Fenster an: Beschriftungen ausblenden, Seitenleisten schmaler.</summary>
    void Adapt()
    {
        double w = ActualWidth;
        foreach (var (t, lvl) in _labels) t.Visibility = (lvl == 1 && w < 1420) || (lvl == 2 && w < 1200) ? Visibility.Collapsed : Visibility.Visible;
        bool narrow = w < 1180;
        if (_leftCol == null || narrow == _narrow) return;
        _narrow = narrow;
        _leftCol.Width = new GridLength(narrow ? 250 : 310);
        _rightCol.Width = new GridLength(narrow ? 290 : 360);
    }

    static string MouseSummary(string kind, JsonObject mi)
    {
        var lines = new List<string>();
        string button = mi.Str("button") switch { "secondary" => "rechte Taste", "middle" => "mittlere Taste", "x1" or "x2" => "Zusatztaste", _ => "linke Taste" };
        var mods = mi.StrList("mods").Select(x => x switch { "ctrl" => "Strg", "shift" => "Umschalt", "alt" => "Alt", "win" => "Win", _ => x }).ToList();
        lines.Add((mods.Count > 0 ? string.Join("+", mods) + " + " : "") + button);
        if (kind == "drag")
        {
            lines.Add($"gedrückt {mi.Long("durationUs") / 1000} ms · Weg {mi.Long("distancePx")} px");
            if (mi.Long("holdBeforeMoveUs") >= 100_000) lines.Add($"vor der Bewegung gehalten {mi.Long("holdBeforeMoveUs") / 1000} ms");
            if (mi.Long("holdAtEndUs") >= 100_000) lines.Add($"über dem Ziel verweilt {mi.Long("holdAtEndUs") / 1000} ms");
        }
        else if (mi.LongN("pressUs") is long pu) lines.Add($"gedrückt {pu / 1000} ms" + (mi.Bool("longPress") ? " (gedrückt gehalten)" : ""));
        return string.Join("\n", lines);
    }

    // ---------------- Mausmarkierungen ----------------

    static readonly (string key, string label)[] MarkKeys = { ("clickMarker", "Klickpunkt"), ("dragPath", "Zug- und Haltepfad"), ("elementFrame", "Zielrahmen"), ("labels", "Hinweise (Haltedauer, Scrollrichtung)"), ("approachPath", "Mausweg davor"), ("keyCaps", "Tasten und Texteingaben") };

    static bool MarkOf(MouseMarks m, string key) => key switch { "clickMarker" => m.Click, "dragPath" => m.Path, "elementFrame" => m.Target, "labels" => m.Labels, "keyCaps" => m.KeyCaps, _ => m.Approach };

    void ShowMarksMenu(FrameworkElement target)
    {
        var s = Sel;
        var all = _view.MarksFor(null);
        var m = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        void Head(string t) => m.Items.Add(new MenuItem { Header = new TextBlock { Text = t.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Theme.Muted }, IsHitTestVisible = false, Focusable = false });
        void Item(string label, bool on, Action a, string gesture = "")
        {
            var mi = new MenuItem { Header = label, InputGestureText = gesture, Icon = on ? U.Icon(Theme.ICheck, 12, Theme.AccentText) : null };
            mi.Click += (_, _) => { try { a(); } catch (Exception ex) { UiApp.ShowError(ex); } };
            m.Items.Add(mi);
        }
        Head("Alle Schritte");
        foreach (var (key, label) in MarkKeys) { bool on = MarkOf(all, key); Item(label, on, () => SetMark(null, key, !on)); }
        if (s != null)
        {
            m.Items.Add(new Separator());
            Head($"Nur Schritt {s.Number}");
            var cur = _view.MarksFor(s);
            Item(cur.Any ? "Alle Markierungen ausblenden" : "Markierungen einblenden", false, ToggleStepMarks, "H");
            if (StepMarksEdit(s) != null) Item("Wie alle Schritte", false, () => Change(e => StepEdit(e, s.Anchor).Remove("auto"), refill: false));
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
        _status.Text = show ? $"Mausmarkierungen bei Schritt {s.Number} eingeblendet" : $"Mausmarkierungen bei Schritt {s.Number} ausgeblendet";
    }

    static string EffectSummary(JsonObject eff)
    {
        var parts = new List<string>();
        foreach (var w in eff.Arr("windowsOpened") ?? new JsonArray()) parts.Add($"„{w.Str("title")}“ erschien");
        foreach (var w in eff.Arr("windowsClosed") ?? new JsonArray()) parts.Add($"„{w.Str("title")}“ geschlossen");
        foreach (var w in eff.Arr("titleChanges") ?? new JsonArray()) parts.Add($"Titel → „{w.Str("to")}“");
        if (eff.Obj("screen") is { } sc && sc.Dbl("changedRatio") > 0) parts.Add($"Bildschirm {sc.Dbl("changedRatio") * 100:0.0} % geändert");
        return parts.Count == 0 ? "–" : string.Join(" · ", parts);
    }

    // =====================================================================================
    // Overlay-Bearbeitung mit Rückgängig
    // =====================================================================================

    void Change(Action<JsonObject> edit, bool refill = true)
    {
        if (_view.Session.ReadOnly) { Msg.Show("Diese Sitzung ist schreibgeschützt (.steprec). Bitte entpacken, um sie zu bearbeiten."); return; }
        _undo.Add(Json.Line(_view.Edits)); _redo.Clear();
        string ImageEdits() => (_view.Edits["redactions"]?.ToJsonString() ?? "") + (_view.Edits["substitutions"]?.ToJsonString() ?? "");
        string before = ImageEdits();
        edit(_view.Edits);
        if (!_view.SaveEdits()) { _view.ReloadEdits(); Msg.Show("Die Sitzung wurde zwischenzeitlich geändert; bitte erneut versuchen."); }
        bool allImages = before != ImageEdits();
        if (allImages) _thumbs.Clear(); else if (Sel is { } cur) _thumbs.Remove(cur.Id);
        if (refill) Fill();
        else { ShowStep(); if (allImages) InvalidateAllThumbs(); else InvalidateThumb(Sel); }
        UpdateUndo();
    }

    void Undo() { if (_undo.Count == 0) return; _redo.Add(Json.Line(_view.Edits)); Restore(_undo[^1]); _undo.RemoveAt(_undo.Count - 1); UpdateUndo(); _status.Text = "Rückgängig gemacht"; }
    void Redo() { if (_redo.Count == 0) return; _undo.Add(Json.Line(_view.Edits)); Restore(_redo[^1]); _redo.RemoveAt(_redo.Count - 1); UpdateUndo(); _status.Text = "Wiederholt"; }
    void Restore(string json)
    {
        var rev = _view.Edits.Long("revision");
        var restored = (JsonObject)JsonNode.Parse(json)!;
        restored["revision"] = rev;
        foreach (var k in _view.Edits.Select(x => x.Key).ToList()) _view.Edits.Remove(k);
        foreach (var kv in restored.ToList()) { restored.Remove(kv.Key); _view.Edits[kv.Key] = kv.Value; }
        _view.SaveEdits();
        _thumbs.Clear(); _selAnn = null;
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
        var d = new InputDialog("Titel der Anleitung", _view.Title) { Owner = this };
        if (d.ShowDialog() != true) return;
        Change(e => { var g = e.Obj("guide") ?? new JsonObject(); g["title"] = d.Value; e["guide"] = g; });
        _titleBlock.Text = _view.Title;
    }

    void EditText()
    {
        var s = Sel; if (s == null) return;
        var d = new InputDialog("Text des Schritts", s.AuthorBody ?? s.Guide, s.AuthorTitle ?? s.Title) { Owner = this };
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
        var d = new InputDialog("Kommentar", "", null, kinds: true) { Owner = this };
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
        var d = new InputDialog("Hinweis einfügen (als Kommentar am Schritt)", "", null, kinds: true) { Owner = this };
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

    void Move(int dir)
    {
        var s = Sel; if (s == null) return;
        var seq = _view.Effective(guide: true, importance: "all", includeManual: true).Select(x => x.Anchor).ToList();
        int i = seq.IndexOf(s.Anchor), j = i + dir;
        if (i < 0 || j < 0 || j >= seq.Count) return;
        (seq[i], seq[j]) = (seq[j], seq[i]);
        Change(e => e["order"] = new JsonArray(seq.Select(x => (JsonNode)x).ToArray()));
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
        var d = new InputDialog("Welcher Text soll überall geschwärzt werden?", "") { Owner = this };
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
        if (targets.Count == 0) { Msg.Show("Keine Fundstelle."); return; }
        Change(e =>
        {
            var arr = e["redactions"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject { ["id"] = NextId(e, "redactions", "r"), ["kind"] = "text", ["targets"] = targets, ["createdUtc"] = Json.UtcNow(), ["by"] = "user" });
            e["redactions"] = arr;
        });
        Msg.Show($"{targets.Count} Fundstellen geschwärzt (Texte). Im Bild sichtbare Stellen bitte zusätzlich mit dem Werkzeug „Schwärzen“ markieren.", "Schwärzen");
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
        var d = new InputDialog("Persönlichen Wert verallgemeinern – Wert (z. B. Benutzername):", user, "Benutzername") { Owner = this };
        d.TitleLabel = "Platzhalter";
        if (d.ShowDialog() != true || d.Value.Trim().Length < 3) return;
        string ph = new string(d.TitleValue.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-').ToArray()).Trim();
        if (ph.Length == 0) ph = "Wert";
        Change(e =>
        {
            var arr = e["substitutions"] as JsonArray ?? new JsonArray();
            arr.Add(new JsonObject { ["id"] = NextId(e, "substitutions", "sb"), ["match"] = new JsonObject { ["literal"] = d.Value.Trim() }, ["placeholder"] = ph, ["caseSensitive"] = false, ["scope"] = "all", ["targets"] = new JsonArray("text"), ["suggestion"] = d.Value.Trim() == user ? "userName" : null, ["createdUtc"] = Json.UtcNow(), ["by"] = "user" }.Clean());
            e["substitutions"] = arr;
        });
    }

    void FinalRedact()
    {
        if (Msg.Show("Alle Text- und Zeitraum-Schwärzungen jetzt endgültig in die Rohdaten schreiben?\n\nDas lässt sich nicht rückgängig machen.\n\nHinweis: Bildschwärzungen, Verpixelungen und Verallgemeinerungen bleiben eine Bearbeitung; die Bilder eines geschwärzten Zeitraums bleiben in den Rohdaten, werden aber in keiner Ausgabe gezeigt. Für die Weitergabe der ganzen Aufnahme die bereinigte Kopie verwenden.", "Endgültig schwärzen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        int n = Redactor.ApplyTextRedactionsDestructive(_view.Session.Path, _view.Edits);
        _view.SaveEdits();
        var anchor = Sel?.Id;
        Load(); _thumbs.Clear(); Fill(anchor);
        Msg.Show($"{n} Stellen endgültig geschwärzt.");
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

    // =====================================================================================
    // Tastatur
    // =====================================================================================

    void OnKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox && e.Key is not (Key.F2 or Key.F5)) return;
        if (_zoom.HandleKey(e)) { e.Handled = true; return; }
        var m = Keyboard.Modifiers;
        bool handled = true;
        switch (e.Key)
        {
            case Key.C when m == ModifierKeys.Control: CopyImage(); break;
            case Key.C when m == (ModifierKeys.Control | ModifierKeys.Shift): CopyImageWithText(); break;
            case Key.T when m == (ModifierKeys.Control | ModifierKeys.Shift): CopyText(); break;
            case Key.O when m == (ModifierKeys.Control | ModifierKeys.Shift): CopyOriginal(); break;
            case Key.S when m == (ModifierKeys.Control | ModifierKeys.Shift): SaveImage(); break;
            case Key.S when m == ModifierKeys.Control: _status.Text = "Automatisch gespeichert"; break;
            case Key.M when m == ModifierKeys.Control: CopyMarkdown(); break;
            case Key.E when m == ModifierKeys.Control: ShowExportDialog("html"); break;
            case Key.R when m == (ModifierKeys.Control | ModifierKeys.Shift): ShowReview(); break;
            case Key.G when m == (ModifierKeys.Control | ModifierKeys.Shift): Generalize(); break;
            case Key.Z when m == ModifierKeys.Control: Undo(); break;
            case Key.Y when m == ModifierKeys.Control: Redo(); break;
            case Key.F when m == ModifierKeys.Control: _search.Focus(); break;
            case Key.H when m == ModifierKeys.Control: ShowSearchReplace(); break;
            case Key.Insert when m == ModifierKeys.Shift: InsertManual(null); break;
            case Key.N when m == ModifierKeys.Control: UiApp.Instance.ShowStart(); break;
            case Key.W when m == ModifierKeys.Control: Close(); break;
            case Key.Up when m == ModifierKeys.Control: ChangeImportance(+1); break;
            case Key.Down when m == ModifierKeys.Control: ChangeImportance(-1); break;
            case Key.System when e.SystemKey == Key.Up && m == ModifierKeys.Alt: Move(-1); break;
            case Key.System when e.SystemKey == Key.Down && m == ModifierKeys.Alt: Move(+1); break;
            case Key.Delete when m == ModifierKeys.Shift: RedactStep(); break;
            case Key.Delete when _selAnn != null: DeleteSelectedOrAll(); break;
            case Key.Delete: ToggleHidden(); break;
            case Key.F2: FocusInlineTitle(); break;
            case Key.V when m == ModifierKeys.None: SelectTool("select"); break;
            case Key.A when m == ModifierKeys.None: SelectTool("arrow"); break;
            case Key.R when m == ModifierKeys.None: SelectTool("rect"); break;
            case Key.E when m == ModifierKeys.None: SelectTool("ellipse"); break;
            case Key.M when m == ModifierKeys.None: SelectTool("highlight"); break;
            case Key.T when m == ModifierKeys.None: SelectTool("text"); break;
            case Key.N when m == ModifierKeys.None: SelectTool("badge"); break;
            case Key.X when m == ModifierKeys.None: SelectTool("redact"); break;
            case Key.Escape when _selAnn != null: _selAnn = null; DrawSelection(); break;
            case Key.F5: TogglePlay(); break;
            case Key.F11: OpenLightbox(); break;
            case Key.P when m == ModifierKeys.None: SelectTool("pixelate"); break;
            case Key.C when m == ModifierKeys.None: SelectTool("crop"); break;
            case Key.F7: Compare(); break;
            case Key.Insert: InsertNote(); break;
            case Key.K when m == ModifierKeys.None: AddComment(); break;
            case Key.H when m == ModifierKeys.None: ToggleStepMarks(); break;
            case Key.W when m == ModifierKeys.None: ToggleBeforeAfter(); break;
            case Key.Escape: StopPlay(); break;
            case Key.Down or Key.Right when m == ModifierKeys.None && !_list.IsKeyboardFocusWithin: if (_list.SelectedIndex < _list.Items.Count - 1) _list.SelectedIndex++; break;
            case Key.Up or Key.Left when m == ModifierKeys.None && !_list.IsKeyboardFocusWithin: if (_list.SelectedIndex > 0) _list.SelectedIndex--; break;
            default: handled = false; break;
        }
        if (handled) e.Handled = true;
    }

    void ToggleBeforeAfter() => (_showAfter ? _beforeBtn : _afterBtn).IsChecked = true;
    void TogglePlay() { if (_play.IsEnabled) StopPlay(); else { if (_list.SelectedIndex >= _list.Items.Count - 1) _list.SelectedIndex = 0; _play.Start(); _playIcon.Text = Theme.IPause; } }

    protected override void OnClosed(EventArgs e) { base.OnClosed(e); _play.Stop(); _currentMeta?.Dispose(); _view.Dispose(); }
}
