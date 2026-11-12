using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StepRecorder.App.Ui;

/// <summary>Kleine Bausteine für Oberflächen im Code.</summary>
static class U
{
    public static Brush Accent => Theme.AccentText;
    public static Brush Muted => Theme.Muted;
    public static Brush Red => Theme.Danger;
    public static Brush Light => Theme.Surface2;
    public static Brush Line => Theme.Line;

    public static TextBlock Text(string s, double size = 13, bool bold = false, Brush? fg = null, Thickness? margin = null)
    {
        var t = new TextBlock { Text = s, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = margin ?? new Thickness(0), TextWrapping = TextWrapping.Wrap };
        if (fg != null) t.Foreground = fg;
        return t;
    }

    public static TextBlock Icon(string glyph, double size = 16, Brush? fg = null)
    {
        var t = new TextBlock { Text = glyph, FontFamily = Theme.Icons, FontSize = size, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (fg != null) t.Foreground = fg;
        return t;
    }

    /// <summary>Inhalt „Symbol + Text“ für Schaltflächen.</summary>
    public static object IconText(string? glyph, string? text, double iconSize = 14, Brush? iconFg = null)
    {
        if (glyph == null) return text ?? "";
        var p = new StackPanel { Orientation = Orientation.Horizontal };
        p.Children.Add(Icon(glyph, iconSize, iconFg));
        if (!string.IsNullOrEmpty(text)) p.Children.Add(new TextBlock { Text = text, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return p;
    }

    public static Button Btn(string s, Action click, string? tip = null, double minW = 0) => Btn(null, s, click, tip, null, minW);

    public static Button Btn(string? glyph, string? text, Action click, string? tip = null, string? style = null, double minW = 0)
    {
        var b = new Button { Content = IconText(glyph, text), Margin = new Thickness(3), MinWidth = minW, ToolTip = tip };
        if (style != null) b.Style = Theme.Style(style);
        b.Click += (_, _) => { try { click(); } catch (Exception ex) { UiApp.ShowError(ex); } };
        return b;
    }

    public static Button Primary(string text, Action click, string? glyph = null) => Btn(glyph, text, click, null, "Primary", 96);
    public static Button Ghost(string? glyph, string? text, Action click, string? tip = null) => Btn(glyph, text, click, tip, "Ghost");

    public static StackPanel Row(params UIElement[] items) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var i in items) p.Children.Add(i); return p; }
    public static StackPanel Col(params UIElement[] items) { var p = new StackPanel(); foreach (var i in items) p.Children.Add(i); return p; }

    public static Border Card(UIElement child, Thickness? pad = null, Thickness? margin = null) => new()
    {
        Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
        Padding = pad ?? new Thickness(18), Margin = margin ?? new Thickness(0), Child = child, SnapsToDevicePixels = true
    };

    /// <summary>Beschriftetes Eingabefeld.</summary>
    public static StackPanel Field(string label, UIElement control, string? hint = null, Thickness? margin = null)
    {
        var p = new StackPanel { Margin = margin ?? new Thickness(0, 0, 0, 14) };
        p.Children.Add(Text(label, 12.5, true, Theme.Fg2, new Thickness(0, 0, 0, 5)));
        p.Children.Add(control);
        if (hint != null) p.Children.Add(Text(hint, 12, fg: Theme.Muted, margin: new Thickness(0, 5, 0, 0)));
        return p;
    }

    public static Border Chip(string text, Brush bg, Brush fg, string? glyph = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph != null) content.Children.Add(new TextBlock { Text = glyph, FontFamily = Theme.Icons, FontSize = 10, Foreground = fg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
        content.Children.Add(new TextBlock { Text = text, FontSize = 11.5, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
        return new Border { Background = bg, CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 1, 7, 2), Margin = new Thickness(0, 0, 5, 0), Child = content, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>Tastenkappen, z. B. „Strg+Umschalt+F9“.</summary>
    public static StackPanel Kbd(string combo, Brush? bg = null, Brush? fg = null, Brush? line = null)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var k in combo.Split('+'))
            p.Children.Add(new Border
            {
                Background = bg ?? Theme.Surface, BorderBrush = line ?? Theme.LineStrong, BorderThickness = new Thickness(1, 1, 1, 2), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 3, 0), MinWidth = 22,
                Child = new TextBlock { Text = k, FontSize = 11, FontFamily = Theme.Mono, Foreground = fg ?? Theme.Fg2, HorizontalAlignment = HorizontalAlignment.Center }
            });
        return p;
    }

    /// <summary>Kreisförmiges Symbol mit getönter Fläche.</summary>
    public static Border IconBadge(string glyph, double size, Brush bg, Brush fg, double? iconSize = null) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.24), Background = bg, Child = Icon(glyph, iconSize ?? size * 0.45, fg)
    };

    public static Border Divider(Thickness? margin = null) => new() { Height = 1, Background = Theme.Line, Margin = margin ?? new Thickness(0, 12, 0, 12) };

    /// <summary>Segmentschalter; liefert die Spur und die RadioButtons in Reihenfolge.</summary>
    public static (Border track, RadioButton[] items) Segmented(string group, int selected, params (string? glyph, string text)[] items)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var list = new List<RadioButton>();
        for (int i = 0; i < items.Length; i++)
        {
            var rb = new RadioButton { GroupName = group, Style = Theme.Style("Segment"), IsChecked = i == selected, Content = IconText(items[i].glyph, items[i].text, 13) };
            panel.Children.Add(rb); list.Add(rb);
        }
        var track = new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(2), Child = panel, HorizontalAlignment = HorizontalAlignment.Left };
        return (track, list.ToArray());
    }

    /// <summary>Dialog-Grundgerüst: Inhalt oben, Schaltflächen rechts unten in einer Fußzeile.</summary>
    public static void Dialog(Window w, string? heading, UIElement body, params Button[] buttons)
    {
        var root = new DockPanel();
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in buttons) { b.Margin = new Thickness(8, 0, 0, 0); b.MinWidth = Math.Max(b.MinWidth, 96); footer.Children.Add(b); }
        var foot = new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 12, 20, 12), Child = footer };
        DockPanel.SetDock(foot, Dock.Bottom);
        if (buttons.Length > 0) root.Children.Add(foot);
        var content = new StackPanel { Margin = new Thickness(24, 20, 24, 16) };
        if (heading != null) { var h = Text(heading, 18, true, Theme.Fg, new Thickness(0, 0, 0, 14)); h.FontFamily = Theme.Display; content.Children.Add(h); }
        content.Children.Add(body);
        root.Children.Add(content);
        w.Content = root;
        Anim.Appear(content, 0, 8, 320);
    }

    /// <summary>Kopf eines Abschnitts (klein, gedämpft).</summary>
    public static TextBlock Overline(string text, Thickness? margin = null)
    {
        var t = Text(text.ToUpperInvariant(), 10.5, false, Theme.Muted, margin ?? new Thickness(0, 16, 0, 6));
        t.FontFamily = Theme.Mono;
        return t;
    }

    /// <summary>Abschnittskopf „01  Titel ————“: Nummer in Mono und Markenfarbe, Haarlinie bis zum Rand.</summary>
    public static FrameworkElement Section(string number, string title, string? sub = null, Thickness? margin = null)
    {
        var g = new Grid { Margin = margin ?? new Thickness(0, 0, 0, 12) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.RowDefinitions.Add(new RowDefinition()); g.RowDefinitions.Add(new RowDefinition());
        var num = new TextBlock { Text = number, FontFamily = Theme.Mono, FontSize = 12, Foreground = Theme.AccentText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 12, 0) };
        var head = new TextBlock { Text = title, FontFamily = Theme.Display, FontSize = 19, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var line = new Border { Height = 1, Background = Theme.Line, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 3, 0, 0) };
        Grid.SetColumn(head, 1); Grid.SetColumn(line, 2);
        g.Children.Add(num); g.Children.Add(head); g.Children.Add(line);
        if (sub != null)
        {
            var st = Text(sub, 12.5, false, Theme.Muted, new Thickness(0, 2, 0, 0));
            Grid.SetRow(st, 1); Grid.SetColumn(st, 1); Grid.SetColumnSpan(st, 2); g.Children.Add(st);
        }
        return g;
    }

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
        var w = new Window { Title = title, Width = text.Length > 420 ? 620 : 470, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        Theme.Prepare(w, Theme.Surface);
        w.Background = Theme.Surface;
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
        var badge = U.IconBadge(glyph, 40, bg, fg, 18); badge.VerticalAlignment = VerticalAlignment.Top; badge.Margin = new Thickness(0, 0, 16, 0);
        DockPanel.SetDock(badge, Dock.Left); body.Children.Add(badge);
        var lines = text.Replace("\r", "").Split('\n');
        var col = new StackPanel();
        int first = 0;
        // erste Zeile als Überschrift, wenn sie kurz ist und ein Absatz folgt
        if (lines.Length > 2 && lines[0].Length < 90 && lines[1].Length == 0) { col.Children.Add(U.Text(lines[0], 15, true, Theme.Fg, new Thickness(0, 0, 0, 6))); first = 2; }
        else col.Children.Add(U.Text(title, 15, true, Theme.Fg, new Thickness(0, 0, 0, 6)));
        col.Children.Add(new TextBox
        {
            Text = string.Join("\n", lines.Skip(first)).Trim('\n'), IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(-2, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap, Foreground = Theme.Fg2, FontSize = 13.5, Cursor = Cursors.Arrow, Focusable = false
        });
        body.Children.Add(col);
        Button Make(string label, MessageBoxResult r, bool primary)
        {
            void Click() { result = r; w.DialogResult = true; }
            var b = primary ? U.Primary(label, Click) : U.Btn(label, Click);
            if (primary) b.IsDefault = true;
            return b;
        }
        var btns = buttons switch
        {
            MessageBoxButton.YesNo => new[] { Make("Ja", MessageBoxResult.Yes, true), Make("Nein", MessageBoxResult.No, false) },
            MessageBoxButton.YesNoCancel => new[] { Make("Ja", MessageBoxResult.Yes, true), Make("Nein", MessageBoxResult.No, false), Make("Abbrechen", MessageBoxResult.Cancel, false) },
            MessageBoxButton.OKCancel => new[] { Make("OK", MessageBoxResult.OK, true), Make("Abbrechen", MessageBoxResult.Cancel, false) },
            _ => new[] { Make("OK", MessageBoxResult.OK, true) }
        };
        U.Dialog(w, null, body, btns);
        w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { w.DialogResult = false; e.Handled = true; } };
        w.Loaded += (_, _) => btns[0].Focus();
        w.ShowDialog();
        return result;
    }
}
