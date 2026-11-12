using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StepRecorder.App.Ui;

//// <summary>
/// Kleine Bausteine für Oberflächen im Code – alle auf dem Maßsystem aus <see cref="Theme"/>
/// (Schriftgrade 12/13/14/16/20, Ecken 6/10/12/16, Abstände in Vielfachen von 4).
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

    /// <summary>Text in Festbreitenschrift (Tastenkappen, Code, Zeitangaben).</summary>
    public static TextBlock Mono(string s, double size = Theme.FsCaption, Brush? fg = null, Thickness? margin = null)
    {
        var t = new TextBlock { Text = s, FontFamily = Theme.Mono, FontSize = size, Margin = margin ?? new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        if (fg != null) t.Foreground = fg;
        return t;
    }

    /// <summary>Überschrift (Dialog, Fensterbereich): Display-Schrift, halbfett.</summary>
    public static TextBlock Heading(string s, double size = Theme.FsTitle, Thickness? margin = null)
    {
        var t = Text(s, size, true, Theme.Fg, margin ?? new Thickness(0, 0, 0, 16));
        t.FontFamily = Theme.Display;
        return t;
    }

    /// <summary>Kopf eines Abschnitts: 13 px halbfett in normaler Schreibweise; oben mehr Luft als unten.</summary>
    public static TextBlock SectionLabel(string text, Thickness? margin = null)
        => Text(text, Theme.FsHint, true, Theme.Fg2, margin ?? new Thickness(0, 24, 0, 10));

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
        content.Children.Add(new TextBlock { Text = Theme.IDown, FontFamily = Theme.Icons, FontSize = 10, Margin = new Thickness(8, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.75 });
        return content;
    }

    public static Button Btn(string s, Action click, string? tip = null, double minW = 0) => Btn(null, s, click, tip, null, minW);

    public static Button Btn(string? glyph, string? text, Action click, string? tip = null, string? style = null, double minW = 0)
    {
        var b = new Button { Content = IconText(glyph, text), Margin = new Thickness(3), MinWidth = minW, ToolTip = tip };
        if (style != null) b.Style = Theme.Style(style);
        b.Click += (_, _) => { try { click(); } catch (Exception ex) { UiApp.ShowError(ex); } };
        return b;
    }

    public static Button Primary(string text, Action click, string? glyph = null) => Btn(glyph, text, click, null, "Primary", 100);
    public static Button Ghost(string? glyph, string? text, Action click, string? tip = null) => Btn(glyph, text, click, tip, "Ghost");

    public static StackPanel Row(params UIElement[] items) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var i in items) p.Children.Add(i); return p; }
    public static StackPanel Col(params UIElement[] items) { var p = new StackPanel(); foreach (var i in items) p.Children.Add(i); return p; }

    public static Border Card(UIElement child, Thickness? pad = null, Thickness? margin = null) => new()
    {
        Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RCard),
        Padding = pad ?? new Thickness(20), Margin = margin ?? new Thickness(0), Child = child, SnapsToDevicePixels = true
    };

    /// <summary>Eingelassene Fläche (Infokasten, Detailstreifen): zweite Flächenfarbe, Radius 12.</summary>
    public static Border Inset(UIElement child, Thickness? pad = null, Thickness? margin = null) => new()
    {
        Background = Theme.Surface2, CornerRadius = new CornerRadius(Theme.RInset), Padding = pad ?? new Thickness(16, 12, 16, 12),
        Margin = margin ?? new Thickness(0), Child = child, SnapsToDevicePixels = true
    };

    /// <summary>Kasten mit Code oder Protokolltext (Festbreitenschrift, auswählbar).</summary>
    public static Border CodeBox(string text, double maxHeight = double.PositiveInfinity)
    {
        var box = new TextBox
        {
            Text = text, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), FontFamily = Theme.Mono, FontSize = Theme.FsCaption,
            Foreground = Theme.Fg2, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = maxHeight
        };
        TextBlock.SetLineHeight(box, 18);
        return Inset(box, new Thickness(14, 12, 14, 12));
    }

    /// <summary>Beschriftetes Eingabefeld.</summary>
    public static StackPanel Field(string label, UIElement control, string? hint = null, Thickness? margin = null)
    {
        var p = new StackPanel { Margin = margin ?? new Thickness(0, 0, 0, 16) };
        p.Children.Add(Text(label, Theme.FsHint, true, Theme.Fg2, new Thickness(0, 0, 0, 6)));
        p.Children.Add(control);
        if (hint != null) p.Children.Add(Hint(hint, new Thickness(0, 6, 0, 0)));
        return p;
    }

    /// <summary>Kleine Marke in Pillenform (Art, Status, Anzahl).</summary>
    public static Border Chip(string text, Brush bg, Brush fg, string? glyph = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph != null) content.Children.Add(new TextBlock { Text = glyph, FontFamily = Theme.Icons, FontSize = 11, Foreground = fg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
        content.Children.Add(new TextBlock { Text = text, FontSize = Theme.FsCaption, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
        return new Border { Background = bg, CornerRadius = new CornerRadius(999), Padding = new Thickness(9, 2, 10, 3), Margin = new Thickness(0, 0, 6, 0), Child = content, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>Tastenkappen, z. B. „Strg+Umschalt+F9“.</summary>
    public static StackPanel Kbd(string combo, Brush? bg = null, Brush? fg = null, Brush? line = null)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var k0 in combo.Split('+'))
        {
            var k = Loc.En ? (k0 == "Eingabe" ? "Enter" : Loc.T(k0)) : k0;
            p.Children.Add(new Border
            {
                Background = bg ?? Theme.Surface, BorderBrush = line ?? Theme.LineStrong, BorderThickness = new Thickness(1, 1, 1, 2), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 1, 7, 2), Margin = new Thickness(0, 0, 4, 0), MinWidth = 24,
                Child = new TextBlock { Text = k, FontSize = Theme.FsCaption, FontFamily = Theme.Mono, Foreground = fg ?? Theme.Fg2, HorizontalAlignment = HorizontalAlignment.Center }
            });
        }
        return p;
    }

    /// <summary>Rundes Symbol mit getönter Fläche.</summary>
    public static Border IconBadge(string glyph, double size, Brush bg, Brush fg, double? iconSize = null) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = bg, Child = Icon(glyph, iconSize ?? Math.Round(size * 0.45), fg)
    };

    /// <summary>Runde Nummernmarke („1“, „2“ …) in zarter Akzentfläche.</summary>
    public static Border NumberBadge(string n, double size = 24) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = Theme.AccentSoft, VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = n, FontSize = Theme.FsCaption, FontWeight = FontWeights.SemiBold, Foreground = Theme.AccentText, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
    };

    public static Border Divider(Thickness? margin = null) => new() { Height = 1, Background = Theme.Line, Margin = margin ?? new Thickness(0, 16, 0, 16) };

    /// <summary>Senkrechter Trennstrich zwischen Gruppen von Schaltflächen.</summary>
    public static Border VSep(double height = 22, Brush? brush = null) => new() { Width = 1, Height = height, Background = brush ?? Theme.Line, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Segmentschalter; liefert die Spur und die RadioButtons in Reihenfolge.</summary>
    public static (Border track, RadioButton[] items) Segmented(string group, int selected, params (string? glyph, string text)[] items)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var list = new List<RadioButton>();
        for (int i = 0; i < items.Length; i++)
        {
            var rb = new RadioButton { GroupName = group, Style = Theme.Style("Segment"), IsChecked = i == selected, Content = IconText(items[i].glyph, items[i].text, 14) };
            panel.Children.Add(rb); list.Add(rb);
        }
        var track = new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.RControl), Padding = new Thickness(3), Child = panel, HorizontalAlignment = HorizontalAlignment.Left };
        return (track, list.ToArray());
    }

    /// <summary>Fußzeile eines Dialogs: Schaltflächen rechts, zweite Flächenfarbe, Haarlinie oben.</summary>
    public static Border Footer(params Button[] buttons)
    {
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in buttons) { b.Margin = new Thickness(8, 0, 0, 0); b.MinWidth = Math.Max(b.MinWidth, 100); footer.Children.Add(b); }
        return new Border { Background = Theme.Surface2, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(24, 14, 24, 14), Child = footer };
    }

    /// <summary>Dialog-Grundgerüst: Inhalt oben, Schaltflächen rechts unten in einer Fußzeile.</summary>
    public static void Dialog(Window w, string? heading, UIElement body, params Button[] buttons)
    {
        var root = new DockPanel();
        if (buttons.Length > 0) { var foot = Footer(buttons); DockPanel.SetDock(foot, Dock.Bottom); root.Children.Add(foot); }
        var content = new StackPanel { Margin = new Thickness(28, 24, 28, 20) };
        if (heading != null) content.Children.Add(Heading(heading));
        content.Children.Add(body);
        root.Children.Add(content);
        w.Content = root;
        Anim.Appear(content, 0, 8, 320);
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
        text = Loc.T(text); title = Loc.T(title);
        var w = new Window { Title = title, Width = text.Length > 420 ? 640 : 500, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
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
        var badge = U.IconBadge(glyph, 40, bg, fg, 18); badge.VerticalAlignment = VerticalAlignment.Top; badge.Margin = new Thickness(0, 0, 18, 0);
        DockPanel.SetDock(badge, Dock.Left); body.Children.Add(badge);
        var lines = text.Replace("\r", "").Split('\n');
        var col = new StackPanel();
        int first = 0;
        // erste Zeile als Überschrift, wenn sie kurz ist und ein Absatz folgt
        if (lines.Length > 2 && lines[0].Length < 90 && lines[1].Length == 0) { col.Children.Add(U.Text(lines[0], Theme.FsSub, true, Theme.Fg, new Thickness(0, 2, 0, 8))); first = 2; }
        else col.Children.Add(U.Text(title, Theme.FsSub, true, Theme.Fg, new Thickness(0, 2, 0, 8)));
        var msg = new TextBox
        {
            Text = string.Join("\n", lines.Skip(first)).Trim('\n'), IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), Margin = new Thickness(-2, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap, Foreground = Theme.Fg2, FontSize = Theme.FsBody, Cursor = Cursors.Arrow, Focusable = false
        };
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
