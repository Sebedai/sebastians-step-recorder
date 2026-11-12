using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Zoom- und Verschiebefläche: Strg+Mausrad zoomt um den Mauszeiger, mittlere Maustaste oder Leertaste+Ziehen verschiebt,
/// „Einpassen“ passt das Bild in die Fläche (bleibt beim Größenändern eingepasst).
/// </summary>
sealed class ZoomPanel : Grid
{
    readonly ScrollViewer _sv;
    readonly Grid _holder = new();
    readonly ScaleTransform _st = new(1, 1);
    readonly FrameworkElement _content;
    readonly Func<Size> _natural;
    readonly Thickness _pad;
    Point? _panFrom; double _panH, _panV;
    public double Zoom { get; private set; } = 1;
    public bool IsFit { get; private set; } = true;
    public event Action? ZoomChanged;

    public ZoomPanel(FrameworkElement content, Func<Size> naturalSize, Thickness pad)
    {
        _content = content; _natural = naturalSize; _pad = pad;
        content.LayoutTransform = _st;
        content.HorizontalAlignment = HorizontalAlignment.Center; content.VerticalAlignment = VerticalAlignment.Center;
        content.Margin = pad;
        _holder.Children.Add(content);
        _sv = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _holder, Focusable = false, PanningMode = PanningMode.Both };
        Children.Add(_sv);
        _sv.SizeChanged += (_, _) => { UpdateHolder(); if (IsFit) FitNow(); };
        _sv.ScrollChanged += (_, _) => UpdateHolder();
        _sv.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            e.Handled = true;
            SetZoom(Zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), anchor: e.GetPosition(_sv));
        };
        _sv.PreviewMouseDown += (_, e) =>
        {
            bool pan = e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space));
            if (!pan) return;
            _panFrom = e.GetPosition(_sv); _panH = _sv.HorizontalOffset; _panV = _sv.VerticalOffset;
            _sv.CaptureMouse(); _sv.Cursor = Cursors.ScrollAll; e.Handled = true;
        };
        _sv.PreviewMouseMove += (_, e) =>
        {
            if (_panFrom is not { } a) return;
            var p = e.GetPosition(_sv);
            _sv.ScrollToHorizontalOffset(_panH - (p.X - a.X)); _sv.ScrollToVerticalOffset(_panV - (p.Y - a.Y));
            e.Handled = true;
        };
        _sv.PreviewMouseUp += (_, e) =>
        {
            if (_panFrom == null) return;
            _panFrom = null; _sv.ReleaseMouseCapture(); _sv.Cursor = null; e.Handled = true;
        };
    }

    void UpdateHolder() { _holder.MinWidth = _sv.ViewportWidth; _holder.MinHeight = _sv.ViewportHeight; }

    /// <summary>Nach einem neuen Bild: eingepasst bleiben oder die Zoomstufe halten.</summary>
    public void Refresh() => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => { if (IsFit) FitNow(); else ApplyScale(); });

    public void FitNow()
    {
        var n = _natural();
        double w = _sv.ActualWidth - _pad.Left - _pad.Right - 2, h = _sv.ActualHeight - _pad.Top - _pad.Bottom - 2;
        if (n.Width <= 0 || n.Height <= 0 || w <= 0 || h <= 0) return;
        Zoom = Math.Clamp(Math.Min(w / n.Width, h / n.Height), 0.02, 8);
        IsFit = true;
        ApplyScale();
    }

    public void SetZoom(double z, Point? anchor = null)
    {
        z = Math.Clamp(z, 0.05, 8);
        var a = anchor ?? new Point(_sv.ViewportWidth / 2, _sv.ViewportHeight / 2);
        double cx = (_sv.HorizontalOffset + a.X) / Zoom, cy = (_sv.VerticalOffset + a.Y) / Zoom;
        Zoom = z; IsFit = false;
        ApplyScale();
        _sv.UpdateLayout();
        _sv.ScrollToHorizontalOffset(cx * z - a.X); _sv.ScrollToVerticalOffset(cy * z - a.Y);
    }

    public void ZoomIn() => SetZoom(Zoom * 1.25);
    public void ZoomOut() => SetZoom(Zoom / 1.25);
    public void Actual() => SetZoom(1);

    void ApplyScale() { _st.ScaleX = _st.ScaleY = Zoom; UpdateHolder(); ZoomChanged?.Invoke(); }

    /// <summary>Zoomleiste: − · Prozent · + · Einpassen · 100 %.</summary>
    public FrameworkElement Toolbar(Brush? fg = null)
    {
        var pct = new TextBlock { MinWidth = 52, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = Theme.FsHint };
        if (fg != null) pct.Foreground = fg; else pct.Foreground = Theme.Fg2;
        void Upd() => pct.Text = $"{Math.Round(Zoom * 100)} %";
        ZoomChanged += Upd; Upd();
        Button B(string glyph, string? text, Action a, string tip)
        {
            // auf der dunklen Großansicht eigene helle Schaltflächen, sonst Fluent-Textschaltflächen
            var b = fg != null ? U.Btn(glyph, text, a, tip, "BarButton") : U.Ghost(glyph, text, a, tip); b.Margin = new Thickness(1, 0, 1, 0); b.Padding = new Thickness(8, 6, 8, 6);
            if (fg != null) b.Foreground = fg;
            return b;
        }
        return U.Row(B(Theme.IZoomOut, null, ZoomOut, Loc.L("Verkleinern (Strg+Minus, Strg+Mausrad)", "Zoom out (Ctrl+Minus, Ctrl+mouse wheel)")), pct, B(Theme.IZoomIn, null, ZoomIn, Loc.L("Vergrößern (Strg+Plus, Strg+Mausrad)", "Zoom in (Ctrl+Plus, Ctrl+mouse wheel)")),
            B(Theme.IFit, Loc.L("Einpassen", "Fit"), FitNow, Loc.L("Ganzes Bild zeigen (Strg+0)", "Show whole image (Ctrl+0)")), B(Theme.IActual, "100 %", Actual, Loc.L("Originalgröße (Strg+1)", "Actual size (Ctrl+1)")));
    }

    /// <summary>Tastenkürzel für Zoom; true, wenn behandelt.</summary>
    public bool HandleKey(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;
        switch (e.Key)
        {
            case Key.OemPlus or Key.Add: ZoomIn(); return true;
            case Key.OemMinus or Key.Subtract: ZoomOut(); return true;
            case Key.D0 or Key.NumPad0: FitNow(); return true;
            case Key.D1 or Key.NumPad1: Actual(); return true;
        }
        return false;
    }
}

/// <summary>Großansicht eines Schrittbilds: Vollbild, Zoom, Verschieben, Blättern mit ← →, Esc schließt.</summary>
sealed class ImageLightbox : Window
{
    readonly Func<int, (BitmapSource img, string caption)?> _get;
    readonly int _count;
    int _index;
    readonly Image _img = new() { Stretch = Stretch.None };
    readonly TextBlock _caption = new() { Foreground = Theme.Night.Fg, FontSize = Theme.FsBody, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly ZoomPanel _zoom;
    public event Action<int>? IndexChanged;

    public ImageLightbox(Func<int, (BitmapSource img, string caption)?> get, int index, int count)
    {
        _get = get; _index = index; _count = count;
        Title = Loc.L("Großansicht", "Full view"); WindowState = WindowState.Maximized; ShowInTaskbar = false;
        var dark = Theme.Night.Bg;
        Theme.Prepare(this, dark);
        Background = dark;
        RenderOptions.SetBitmapScalingMode(_img, BitmapScalingMode.HighQuality);
        var host = new Border { Child = _img, Effect = Theme.Shadow(40, 6, 0.6) };
        _zoom = new ZoomPanel(host, () => _img.Source is BitmapSource b ? new Size(b.PixelWidth, b.PixelHeight) : new Size(0, 0), new Thickness(40, 24, 40, 24));

        var light = Theme.Night.Fg;
        Button Nav(string glyph, int dir, string tip)
        {
            var b = U.Btn(glyph, null, () => Go(_index + dir), tip, "BarButton"); b.Foreground = light; b.Padding = new Thickness(10, 8, 10, 8);
            return b;
        }
        var close = U.Btn(Theme.IClose, Loc.L("Schließen", "Close"), Close, Loc.L("Schließen (Esc)", "Close (Esc)"), "BarButton"); close.Foreground = light; close.Padding = new Thickness(10, 6, 12, 6);
        var bar = new DockPanel { Margin = new Thickness(16, 10, 16, 10) };
        var right = U.Row(_zoom.Toolbar(light), U.VSep(22, Theme.Night.Line), Nav(Theme.IPrev, -1, Loc.L("Vorheriges Bild (←)", "Previous image (←)")), Nav(Theme.INext, +1, Loc.L("Nächstes Bild (→)", "Next image (→)")), close);
        right.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(right, Dock.Right); bar.Children.Add(right); bar.Children.Add(_caption);
        var top = new Border { Background = Theme.Night.Pill, BorderBrush = Theme.Night.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = bar };
        var hint = new TextBlock { Text = Loc.L("Strg+Mausrad zoomen · Leertaste oder mittlere Maustaste + Ziehen verschieben · Doppelklick: Einpassen / 100 % · ← → blättern · Esc schließt", "Ctrl+mouse wheel to zoom · Space or middle mouse button + drag to pan · Double-click: Fit / 100 % · ← → to browse · Esc to close"), Foreground = Theme.Night.Muted, FontSize = Theme.FsCaption, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 12) };
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        DockPanel.SetDock(hint, Dock.Bottom); root.Children.Add(hint);
        root.Children.Add(_zoom);
        Content = root;
        _img.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { if (_zoom.IsFit) _zoom.Actual(); else _zoom.FitNow(); e.Handled = true; } };
        PreviewKeyDown += (_, e) =>
        {
            if (_zoom.HandleKey(e)) { e.Handled = true; return; }
            switch (e.Key)
            {
                case Key.Escape: Close(); break;
                case Key.Left or Key.PageUp: Go(_index - 1); break;
                case Key.Right or Key.PageDown: Go(_index + 1); break;
                case Key.Home: Go(0); break;
                case Key.End: Go(_count - 1); break;
                default: return;
            }
            e.Handled = true;
        };
        Loaded += (_, _) => Go(_index);
    }

    void Go(int i)
    {
        if (i < 0 || i >= _count) return;
        if (_get(i) is not { } r) return;
        _index = i;
        _img.Source = r.img; _img.Width = r.img.PixelWidth; _img.Height = r.img.PixelHeight;
        _caption.Text = r.caption; _caption.ToolTip = r.caption;
        _zoom.FitNow(); _zoom.Refresh();
        IndexChanged?.Invoke(i);
    }
}
