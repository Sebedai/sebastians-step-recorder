using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using InfoBarSeverity = Wpf.Ui.Controls.InfoBarSeverity;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Kleine Bausteine für Oberflächen im Code – auf den Fluent-Steuerelementen von WPF-UI
/// (Schaltflächen mit Appearance, Felder mit Platzhalter, InfoBar) und dem Maßsystem aus <see cref="Theme"/>.
/// </summary>
static class U
{
    public static Brush Accent => Theme.AccentText;
    public static Brush Muted => Theme.Muted;
    public static Brush Red => Theme.Danger;
    public static Brush Light => Theme.Surface2;
    public static Brush Line => Theme.Line;

    public static TextBlock Text(string s, double size = Theme.FsBody, bool bold = false, Brush? fg = null, Thickness? margin = null)
    {
        var t = new TextBlock { Text = s, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = margin ?? new Thickness(0), TextWrapping = TextWrapping.Wrap };
        if (fg != null) t.Foreground = fg;
        return t;
    }

    /// <summary>Mehrzeiliger Text mit ruhigem Zeilenabstand (1,45 × Schriftgröße).</summary>
    public static TextBlock Para(string s, double size = Theme.FsBody, Brush? fg = null, Thickness? margin = null) => Spaced(Text(s, size, false, fg, margin));

    /// <summary>Zeilenabstand für einen vorhandenen Text setzen.</summary>
    public static TextBlock Spaced(TextBlock t)
    {
        t.LineHeight = Math.Round(t.FontSize * Theme.LineFactor);
        t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        return t;
    }

    /// <summary>Hinweis unter oder neben einem Element: 13 px, gedämpft, mit Zeilenabstand.</summary>
    public static TextBlock Hint(string s, Thickness? margin = null) => Spaced(Text(s, Theme.FsHint, false, Theme.Muted, margin));

    /// <summary>Metaangaben (Zeit, Anzahl, Art): 12 px, gedämpft, einzeilig.</summary>
    public static TextBlock Meta(string s, Thickness? margin = null)
        => new() { Text = s, FontSize = Theme.FsCaption, Foreground = Theme.Muted, Margin = margin ?? new Thickness(0), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Text in Festbreitenschrift (Tastenkappen, Code, Protokoll).</summary>
    public static TextBlock Mono(string s, double size = Theme.FsCaption, Brush? fg = null, Thickness? margin = null)
    {
        var t = new TextBlock { Text = s, FontFamily = Theme.Mono, FontSize = size, Margin = margin ?? new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        if (fg != null) t.Foreground = fg;
        return t;
    }

    /// <summary>Überschrift (Seite, Dialog): Windows-Titelstil – Display-Schrift, halbfett.</summary>
    public static TextBlock Heading(string s, double size = Theme.FsTitle, Thickness? margin = null)
    {
        var t = Text(s, size, true, Theme.Fg, margin ?? new Thickness(0, 0, 0, 16));
        t.FontFamily = Theme.Display;
        return t;
    }

    /// <summary>Kopf eines Abschnitts: 14 px halbfett („Body Strong“ wie in den Windows-Einstellungen).</summary>
    public static TextBlock SectionLabel(string text, Thickness? margin = null)
        => Text(text, Theme.FsBody, true, Theme.Fg, margin ?? new Thickness(0, 24, 0, 8));

    public static TextBlock Icon(string glyph, double size = 16, Brush? fg = null)
    {
        var t = new TextBlock { Text = glyph, FontFamily = Theme.Icons, FontSize = size, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (fg != null) t.Foreground = fg;
        return t;
    }

    /// <summary>Inhalt „Symbol + Text“ für Schaltflächen.</summary>
    public static object IconText(string? glyph, string? text, double iconSize = 16, Brush? iconFg = null)
    {
        if (glyph == null) return text ?? "";
        var p = new StackPanel { Orientation = Orientation.Horizontal };
        p.Children.Add(Icon(glyph, iconSize, iconFg));
        if (!string.IsNullOrEmpty(text)) p.Children.Add(new TextBlock { Text = text, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return p;
    }

    /// <summary>Inhalt „Symbol + Text + Pfeil“ für Schaltflächen, die ein Menü öffnen. Kind 1 ist immer der Text.</summary>
    public static StackPanel MenuContent(string glyph, string text)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(Icon(glyph, 16));
        content.Children.Add(new TextBlock { Text = text, Margin = new Thickness(text.Length > 0 ? 8 : 0, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        content.Children.Add(new TextBlock { Text = Theme.IDown, FontFamily = Theme.Icons, FontSize = 10, Margin = new Thickness(8, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return content;
    }

    /// <summary>Fluent-Schaltfläche: Stil „Primary“ (Akzent), „Ghost“ (transparent), „Danger“, „Link“ (Akzent-Text) oder Standard.</summary>
    public static Button Make(string? style)
    {
        var b = new Wpf.Ui.Controls.Button();
        switch (style)
        {
            case "Primary": b.Appearance = ControlAppearance.Primary; break;
            case "Ghost": Subtle(b); break;
            case "Danger": b.Appearance = ControlAppearance.Danger; b.Foreground = Brushes.White; break;
            case "Link": Subtle(b); b.Foreground = Theme.AccentText; b.Padding = new Thickness(8, 4, 8, 4); b.MinHeight = 0; break;
            case null: break;
            default: b.Style = Theme.Style(style); break;
        }
        return b;
    }

    /// <summary>Eigenschaften setzen und das Element zurückgeben (statt Objekt-Initialisierer bei Fabrik-Methoden).</summary>
    public static T Set<T>(this T e, Action<T> init) where T : DependencyObject { init(e); return e; }

    /// <summary>Schlichte Schaltfläche wie in Windows-Befehlsleisten: ohne Fläche und Rand, beim Zeigen leicht hinterlegt.</summary>
    static void Subtle(Wpf.Ui.Controls.Button b)
    {
        b.Appearance = ControlAppearance.Transparent;
        b.Background = Brushes.Transparent; b.BorderBrush = Brushes.Transparent;
        b.MouseOverBorderBrush = Brushes.Transparent; b.PressedBorderBrush = Brushes.Transparent;
        b.MouseOverBackground = Theme.HoverBg;
        b.PressedBackground = Application.Current.TryFindResource("SubtleFillColorTertiaryBrush") as Brush ?? Theme.HoverBg;
        // gesperrt (z. B. Rückgängig ohne Verlauf): nur blasser Inhalt, keine graue Fläche
        b.Resources["ButtonBackgroundDisabled"] = Brushes.Transparent;
        b.Resources["ButtonBorderBrushDisabled"] = Brushes.Transparent;
    }

    /// <summary>
    /// Menüeintrag mit gut lesbarem Tastenkürzel rechts (WPF-UI zeigt Kürzel sonst klein und blass). Die Spalten
    /// richten sich über <see cref="MenuScope"/> am Menü aus.
    /// </summary>
    public static MenuItem Mi(string label, string? gesture = null, Action? click = null, string? glyph = null)
    {
        var g = new Grid { Tag = MenuGridTag };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var k = new TextBlock { Text = gesture ?? "", Foreground = Theme.Muted, FontSize = Theme.FsCaption, Margin = new Thickness(32, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(k, 1); g.Children.Add(k);
        // Symbolspalte immer belegen, damit Einträge mit und ohne Symbol bündig stehen
        var mi = new MenuItem { Header = g, Icon = glyph != null ? Icon(glyph, 16) : new Border { Width = 16, Height = 16 } };
        System.Windows.Automation.AutomationProperties.SetName(mi, label);
        if (!string.IsNullOrEmpty(gesture)) System.Windows.Automation.AutomationProperties.SetAcceleratorKey(mi, gesture);
        if (click != null) mi.Click += (_, _) => { try { click(); } catch (Exception ex) { UiApp.ShowError(ex); } };
        return mi;
    }

    const string MenuGridTag = "mi";

    /// <summary>Kontextmenü mit ausgerichteten Spalten (Ausrichtung übernimmt <see cref="AlignMenu"/> beim Öffnen).</summary>
    public static ContextMenu MenuScope(ContextMenu m) => m;

    /// <summary>
    /// Richtet die Einträge eines geöffneten Menüs aus: alle Namen gleich breit, Tastenkürzel rechtsbündig in einer Spalte.
    /// Wird beim Öffnen jedes Menüs aufgerufen (Klassenereignisse in <see cref="Theme.Init"/>).
    /// </summary>
    public static void AlignMenu(ItemsControl menu)
    {
        var grids = menu.Items.OfType<MenuItem>().Select(m => m.Header as Grid).Where(g => g?.Tag as string == MenuGridTag).Cast<Grid>().ToList();
        if (grids.Count == 0) return;
        double label = 0, key = 0;
        foreach (var g in grids)
        {
            foreach (var t in g.Children.OfType<TextBlock>()) t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            label = Math.Max(label, g.Children[0].DesiredSize.Width);
            key = Math.Max(key, g.Children[1].DesiredSize.Width);
        }
        foreach (var g in grids) { g.ColumnDefinitions[0].Width = new GridLength(Math.Ceiling(label)); g.ColumnDefinitions[1].Width = new GridLength(Math.Ceiling(key)); }
    }

    public static Button Btn(string s, Action click, string? tip = null, double minW = 0) => Btn(null, s, click, tip, null, minW);

    public static Button Btn(string? glyph, string? text, Action click, string? tip = null, string? style = null, double minW = 0)
    {
        var b = Make(style);
        b.Content = IconText(glyph, text); b.Margin = new Thickness(3); b.MinWidth = minW; b.ToolTip = tip;
        b.Click += (_, _) => { try { click(); } catch (Exception ex) { UiApp.ShowError(ex); } };
        return b;
    }

    public static Button Primary(string text, Action click, string? glyph = null) => Btn(glyph, text, click, null, "Primary", 100);
    public static Button Ghost(string? glyph, string? text, Action click, string? tip = null) => Btn(glyph, text, click, tip, "Ghost");

    /// <summary>Eingabefeld mit Platzhalter (WPF-UI).</summary>
    public static Wpf.Ui.Controls.TextBox Input(string? placeholder = null, string text = "")
        => new() { Text = text, PlaceholderText = placeholder ?? "", PlaceholderEnabled = placeholder != null, ClearButtonEnabled = false };

    /// <summary>Fluent-Hinweisleiste (Warnung oder Information), dauerhaft sichtbar.</summary>
    public static Wpf.Ui.Controls.InfoBar InfoBar(string title, string message, bool warning, Thickness? margin = null)
        => new() { Title = title, Message = message, Severity = warning ? InfoBarSeverity.Warning : InfoBarSeverity.Informational, IsOpen = true, IsClosable = false, Margin = margin ?? new Thickness(0, 12, 0, 4) };

    public static StackPanel Row(params UIElement[] items) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var i in items) p.Children.Add(i); return p; }
    public static StackPanel Col(params UIElement[] items) { var p = new StackPanel(); foreach (var i in items) p.Children.Add(i); return p; }

    /// <summary>Fluent-Karte: Kartenfläche mit feinem Rand, Radius 8.</summary>
    public static Border Card(UIElement child, Thickness? pad = null, Thickness? margin = null) => new()
    {
        Background = Theme.Surface, BorderBrush = Theme.CardLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RCard),
        Padding = pad ?? new Thickness(16), Margin = margin ?? new Thickness(0), Child = child, SnapsToDevicePixels = true
    };

    /// <summary>Eingelassene Fläche (Infokasten, Detailzeile): zweite Kartenfläche, Radius 8.</summary>
    public static Border Inset(UIElement child, Thickness? pad = null, Thickness? margin = null) => new()
    {
        Background = Theme.Surface2, BorderBrush = Theme.CardLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RInset), Padding = pad ?? new Thickness(16, 12, 16, 12),
        Margin = margin ?? new Thickness(0), Child = child, SnapsToDevicePixels = true
    };

    /// <summary>Text, der wie normaler Text aussieht, aber markierbar ist (Werte, Meldungen).</summary>
    public static TextBox PlainText(string text, Brush? fg = null, double size = Theme.FsBody, FontFamily? font = null) => new()
    {
        Style = Theme.Style("PlainTextBox"), Text = text, TextWrapping = TextWrapping.Wrap, Foreground = fg ?? Theme.Fg, FontSize = size, FontFamily = font ?? Theme.UiFont, Cursor = Cursors.IBeam
    };

    /// <summary>Kasten mit Code oder Protokolltext (Festbreitenschrift, markierbar).</summary>
    public static Border CodeBox(string text, double maxHeight = double.PositiveInfinity)
    {
        var box = PlainText(text, Theme.Fg2, Theme.FsCaption, Theme.Mono);
        box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; box.MaxHeight = maxHeight;
        TextBlock.SetLineHeight(box, 18);
        return Inset(box, new Thickness(14, 12, 14, 12));
    }

    /// <summary>Beschriftetes Eingabefeld.</summary>
    public static StackPanel Field(string label, UIElement control, string? hint = null, Thickness? margin = null)
    {
        var p = new StackPanel { Margin = margin ?? new Thickness(0, 0, 0, 16) };
        p.Children.Add(Text(label, Theme.FsBody, false, Theme.Fg, new Thickness(0, 0, 0, 6)));
        p.Children.Add(control);
        if (hint != null) p.Children.Add(Hint(hint, new Thickness(0, 6, 0, 0)));
        return p;
    }

    /// <summary>
    /// Kleine Statusangabe (Art, Status, Anzahl): Symbol und Text in einer Zeile wie in Windows. Nur Warnungen und Fehler
    /// bekommen eine getönte Fläche, alles andere bleibt schlichter Text.
    /// </summary>
    public static Border Chip(string text, Brush bg, Brush fg, string? glyph = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph != null) content.Children.Add(new TextBlock { Text = glyph, FontFamily = Theme.Icons, FontSize = 12, Foreground = fg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
        content.Children.Add(new TextBlock { Text = text, FontSize = Theme.FsCaption, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
        bool tinted = ReferenceEquals(bg, Theme.WarnSoft) || ReferenceEquals(bg, Theme.DangerSoft);
        return new Border
        {
            Background = tinted ? bg : Brushes.Transparent, CornerRadius = new CornerRadius(Theme.RSmall), Padding = tinted ? new Thickness(6, 1, 7, 2) : new Thickness(0, 1, 0, 2),
            Margin = new Thickness(0, 0, tinted ? 8 : 12, 0), Child = content, VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>Tastenkappen, z. B. „Strg+Umschalt+F9“.</summary>
    public static StackPanel Kbd(string combo, Brush? bg = null, Brush? fg = null, Brush? line = null)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var k0 in combo.Split('+'))
        {
            var k = Loc.Keys(k0);
            p.Children.Add(new Border
            {
                Background = bg ?? Theme.Field, BorderBrush = line ?? Theme.LineStrong, BorderThickness = new Thickness(1, 1, 1, 2), CornerRadius = new CornerRadius(Theme.RSmall),
                Padding = new Thickness(7, 1, 7, 2), Margin = new Thickness(0, 0, 4, 0), MinWidth = 24,
                Child = new TextBlock { Text = k, FontSize = Theme.FsCaption, FontFamily = Theme.Mono, Foreground = fg ?? Theme.Fg2, HorizontalAlignment = HorizontalAlignment.Center }
            });
        }
        return p;
    }

    /// <summary>Symbol in einer getönten Fläche (rund).</summary>
    public static Border IconBadge(string glyph, double size, Brush bg, Brush fg, double? iconSize = null) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = bg, Child = Icon(glyph, iconSize ?? Math.Round(size * 0.45), fg)
    };

    public static Border Divider(Thickness? margin = null) => new() { Height = 1, Background = Theme.Line, Margin = margin ?? new Thickness(0, 16, 0, 16) };

    /// <summary>Senkrechter Trennstrich zwischen Gruppen von Schaltflächen.</summary>
    public static Border VSep(double height = 22, Brush? brush = null) => new() { Width = 1, Height = height, Background = brush ?? Theme.Line, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Auswahlleiste (wie WinUI „SelectorBar“); liefert die Leiste und die RadioButtons in Reihenfolge.</summary>
    public static (Border track, RadioButton[] items) Segmented(string group, int selected, params (string? glyph, string text)[] items)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var list = new List<RadioButton>();
        for (int i = 0; i < items.Length; i++)
        {
            var rb = new RadioButton { GroupName = group, Style = Theme.Style("Segment"), IsChecked = i == selected, Content = IconText(items[i].glyph, items[i].text, 14) };
            panel.Children.Add(rb); list.Add(rb);
        }
        var track = new Border { Background = Brushes.Transparent, Child = panel, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-4, 0, 0, 0) };
        return (track, list.ToArray());
    }

    /// <summary>Fußzeile eines Dialogs wie in WinUI: Schaltflächen rechts, etwas dunklere Fläche, Haarlinie oben.</summary>
    public static Border Footer(params Button[] buttons)
    {
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in buttons) { b.Margin = new Thickness(8, 0, 0, 0); b.MinWidth = Math.Max(b.MinWidth, 110); footer.Children.Add(b); }
        return new Border { Background = Theme.Bg, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 16, 24, 16), Child = footer };
    }

    /// <summary>Dialog-Grundgerüst: Inhalt oben, Schaltflächen rechts unten in einer Fußzeile.</summary>
    public static void Dialog(Window w, string? heading, UIElement body, params Button[] buttons)
    {
        var root = new DockPanel();
        if (buttons.Length > 0) { var foot = Footer(buttons); DockPanel.SetDock(foot, Dock.Bottom); root.Children.Add(foot); }
        var content = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };
        if (heading != null) content.Children.Add(Heading(heading));
        content.Children.Add(body);
        root.Children.Add(content);
        w.Content = root;
        Anim.Appear(content, 0, 4, 200);
    }

    /// <summary>
    /// Dialoginhalt immer ganz zeigen, ohne Scrollen: Ist er höher als <see cref="Window.MaxHeight"/> erlaubt, wird er gleichmäßig
    /// verkleinert – und vorher so verbreitert, dass er nach dem Verkleinern wieder die ganze Fensterbreite füllt.
    /// </summary>
    public static void FitDialog(Window w)
    {
        if (w.Content is not FrameworkElement dlg) return;
        w.Content = null;
        double design = Math.Max(200, w.Width - 16);
        dlg.Width = design;
        var box = new Viewbox { Child = dlg, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, VerticalAlignment = VerticalAlignment.Top };
        w.Content = box;
        w.Loaded += (_, _) =>
        {
            // Maße der Client-Fläche (der Viewbox selbst meldet schon die verkleinerte Größe)
            if (VisualTreeHelper.GetChildrenCount(w) == 0 || VisualTreeHelper.GetChild(w, 0) is not FrameworkElement client) return;
            double availW = client.ActualWidth, availH = client.ActualHeight;
            if (availW <= 0 || availH <= 0) return;
            double width = design;
            for (int i = 0; i < 3; i++)
            {
                dlg.Width = width;
                dlg.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double h = dlg.DesiredSize.Height;
                if (h <= availH + 0.5) break;
                double wanted = Math.Min(availW * h / availH, design * 1.6);
                if (Math.Abs(wanted - width) < 2) break;
                width = wanted;
            }
            dlg.Width = width;
        };
    }

    /// <summary>Kopf eines Abschnitts (alter Name; gleiche Gestaltung wie <see cref="SectionLabel"/>).</summary>
    public static TextBlock Overline(string text, Thickness? margin = null) => SectionLabel(text, margin);

    public static System.Windows.Media.Imaging.BitmapSource ToSource(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
        ms.Position = 0;
        var bi = new System.Windows.Media.Imaging.BitmapImage();
        bi.BeginInit(); bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit();
        bi.Freeze();
        return bi;
    }
}

/// <summary>Moderner Ersatz für MessageBox (gleiche Aufrufform).</summary>
static class Msg
{
    public static MessageBoxResult Show(string text, string title = StepRecorder.Data.AppInfo.DisplayName, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
    {
        // Texte kommen schon in der Oberflächensprache (Loc.L beim Aufrufer)
        var w = new Window { Title = title, Width = text.Length > 420 ? 640 : 500, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        Theme.Prepare(w);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive && x.IsVisible && x is not RecordingBar and not FrameWindow && x != w);
        if (owner != null) { w.Owner = owner; w.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        else { w.WindowStartupLocation = WindowStartupLocation.CenterScreen; w.Topmost = true; w.ShowInTaskbar = true; }
        var result = buttons switch { MessageBoxButton.YesNo => MessageBoxResult.No, MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel, _ => MessageBoxResult.OK };
        var (glyph, bg, fg) = icon switch
        {
            MessageBoxImage.Warning => (Theme.IWarn, Theme.WarnSoft, Theme.Warn),
            MessageBoxImage.Error => (Theme.IError, Theme.DangerSoft, Theme.Danger),
            MessageBoxImage.Question => (Theme.IQuestion, Theme.AccentSoft, Theme.AccentText),
            _ => (Theme.IInfo, Theme.AccentSoft, Theme.AccentText)
        };
        var body = new DockPanel();
        // Symbol wie bei Windows-Meldungen: farbiges Zeichen ohne Fläche
        var badge = U.Icon(glyph, 26, fg); badge.VerticalAlignment = VerticalAlignment.Top; badge.Margin = new Thickness(0, 2, 16, 0);
        DockPanel.SetDock(badge, Dock.Left); body.Children.Add(badge);
        var lines = text.Replace("\r", "").Split('\n');
        var col = new StackPanel();
        int first = 0;
        // erste Zeile als Überschrift, wenn sie kurz ist und ein Absatz folgt
        if (lines.Length > 2 && lines[0].Length < 90 && lines[1].Length == 0) { col.Children.Add(U.Heading(lines[0], Theme.FsTitle, new Thickness(0, 0, 0, 10))); first = 2; }
        else col.Children.Add(U.Heading(title, Theme.FsTitle, new Thickness(0, 0, 0, 10)));
        var msg = U.PlainText(string.Join("\n", lines.Skip(first)).Trim('\n'), Theme.Fg);
        msg.Cursor = Cursors.Arrow; msg.Focusable = false;
        TextBlock.SetLineHeight(msg, 21);
        col.Children.Add(msg);
        body.Children.Add(col);
        Button Make(string label, MessageBoxResult r, bool primary)
        {
            void Click() { result = r; w.DialogResult = true; }
            var b = primary ? U.Primary(label, Click) : U.Btn(label, Click);
            if (primary) b.IsDefault = true;
            return b;
        }
        string yes = Settings.Loc.L("Ja", "Yes"), no = Settings.Loc.L("Nein", "No"), cancel = Settings.Loc.L("Abbrechen", "Cancel");
        var btns = buttons switch
        {
            MessageBoxButton.YesNo => new[] { Make(yes, MessageBoxResult.Yes, true), Make(no, MessageBoxResult.No, false) },
            MessageBoxButton.YesNoCancel => new[] { Make(yes, MessageBoxResult.Yes, true), Make(no, MessageBoxResult.No, false), Make(cancel, MessageBoxResult.Cancel, false) },
            MessageBoxButton.OKCancel => new[] { Make("OK", MessageBoxResult.OK, true), Make(cancel, MessageBoxResult.Cancel, false) },
            _ => new[] { Make("OK", MessageBoxResult.OK, true) }
        };
        U.Dialog(w, null, body, btns);
        w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { w.DialogResult = false; e.Handled = true; } };
        w.Loaded += (_, _) => btns[0].Focus();
        w.ShowDialog();
        return result;
    }
}
