using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using StepRecorder.Export;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Seitenansicht (13, Paket 20): zeigt die Seiten des PDFs, wie es exportiert würde. Seiten werden erst gerastert, wenn sie sichtbar
/// werden, und weit entfernte wieder freigegeben. Ein Klick meldet den Block (Schritt oder Kapitel) unter dem Mauszeiger.
/// </summary>
sealed class PageView : Grid
{
    readonly ScrollViewer _sv = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = true, PanningMode = PanningMode.VerticalOnly };
    readonly StackPanel _stack = new() { Margin = new Thickness(24, 20, 24, 28) };
    readonly Border _banner = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(14, 7, 14, 8), CornerRadius = new CornerRadius(8), Visibility = Visibility.Collapsed };
    readonly TextBlock _bannerText = new() { FontSize = Theme.FsCaption, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
    readonly TextBlock _empty = new() { FontSize = Theme.FsBody, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 420 };
    readonly List<Grid> _slots = new();
    readonly Dictionary<int, byte[]> _png = new();
    readonly HashSet<int> _pending = new();
    readonly SemaphoreSlim _renderLock = new(1, 1);
    PdfPreview? _doc;
    PdfRaster? _raster;
    int _generation;
    double _zoom = 1;          // 1 = Seitenbreite
    bool _fitPage;
    public event Action<PdfBlock, bool>? BlockClicked;
    public PdfPreview? Document => _doc;

    public PageView()
    {
        Background = Theme.CanvasBg;
        _sv.Content = _stack;
        Children.Add(_sv);
        _empty.Foreground = Theme.Muted;
        Children.Add(_empty);
        _banner.Child = _bannerText;
        Children.Add(_banner);
        System.Windows.Automation.AutomationProperties.SetName(this, Loc.L("Seitenansicht", "Page view"));
        _sv.ScrollChanged += (_, e) => { if (e.VerticalChange != 0 || e.ViewportHeightChange != 0 || e.ExtentHeightChange != 0) RenderVisible(); };
        SizeChanged += (_, e) => { if (Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 4) Relayout(); };
        PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            Zoom(e.Delta > 0 ? 1.15 : 1 / 1.15); e.Handled = true;
        };
    }

    /// <summary>Hinweis über den Seiten (z. B. „wird aktualisiert“, „ungeprüfte Eingaben als Platzhalter“); null blendet ihn aus.</summary>
    public void Banner(string? text, bool warn = false)
    {
        if (string.IsNullOrEmpty(text)) { _banner.Visibility = Visibility.Collapsed; return; }
        _bannerText.Text = text;
        _banner.Background = warn ? Theme.WarnSoft : Theme.Raised;
        _bannerText.Foreground = warn ? Theme.Warn : Theme.Fg2;
        _banner.Visibility = Visibility.Visible;
    }

    /// <summary>Text statt der Seiten (keine Seiten, Renderer fehlt, Fehler).</summary>
    public void Message(string? text) { _empty.Text = text ?? ""; _empty.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible; }

    public void Zoom(double factor) { _fitPage = false; _zoom = Math.Clamp(_zoom * factor, 0.3, 4); Relayout(); }
    public void ZoomFitWidth() { _fitPage = false; _zoom = 1; Relayout(); }
    public void ZoomFitPage() { _fitPage = true; _zoom = 1; Relayout(); }

    /// <summary>Neues Dokument zeigen; die Lage bleibt am Block, der oben sichtbar war.</summary>
    public void SetDocument(PdfPreview doc, PdfRaster raster)
    {
        string? anchorKey = TopBlockKey(out double offsetInBlock);
        var oldRaster = _raster;
        _generation++;
        _doc = doc; _raster = raster;
        _png.Clear(); _pending.Clear();
        Message(doc.PageCount == 0 ? Loc.L("Keine Seiten.", "No pages.") : null);
        BuildSlots();
        if (anchorKey != null) Dispatcher.BeginInvoke(() => ScrollToKey(anchorKey, false, offsetInBlock), System.Windows.Threading.DispatcherPriority.Loaded);
        Dispatcher.BeginInvoke(RenderVisible, System.Windows.Threading.DispatcherPriority.Loaded);
        // alten Renderer erst freigeben, wenn er nicht mehr rendert
        if (oldRaster != null) _ = Task.Run(async () => { await _renderLock.WaitAsync(); try { oldRaster.Dispose(); } finally { _renderLock.Release(); } });
    }

    double PageWidthDip
    {
        get
        {
            double avail = Math.Max(240, (ActualWidth > 0 ? ActualWidth : 800) - 48 - SystemParameters.VerticalScrollBarWidth);
            if (_doc == null) return Math.Min(860, avail);
            double w = Math.Min(avail, 900);
            if (_fitPage)
            {
                double availH = Math.Max(200, (ActualHeight > 0 ? ActualHeight : 600) - 48);
                w = Math.Min(w, availH * _doc.PageW / _doc.PageH);
            }
            return Math.Round(w * _zoom);
        }
    }

    double PageHeightDip => _doc == null ? 0 : Math.Round(PageWidthDip * _doc.PageH / _doc.PageW);

    void BuildSlots()
    {
        _stack.Children.Clear(); _slots.Clear();
        if (_doc == null) return;
        for (int i = 0; i < _doc.PageCount; i++)
        {
            int index = i;
            var img = new Image { Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var num = new TextBlock { Text = Loc.F("Seite {0} von {1}", "Page {0} of {1}", i + 1, _doc.PageCount), FontSize = Theme.FsCaption, Foreground = Theme.Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            var paper = new Border { Background = Brushes.White, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Child = img };
            var overlay = new Canvas { IsHitTestVisible = false };
            var slot = new Grid { Margin = new Thickness(0, 0, 0, 18), HorizontalAlignment = HorizontalAlignment.Center, Cursor = Cursors.Hand, Tag = index };
            slot.RowDefinitions.Add(new RowDefinition()); slot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var paperHost = new Grid();
            paperHost.Children.Add(paper); paperHost.Children.Add(overlay);
            slot.Children.Add(paperHost);
            Grid.SetRow(num, 1); slot.Children.Add(num);
            slot.MouseLeftButtonUp += (_, e) => Clicked(index, e.GetPosition(paperHost).Y, e.ClickCount);
            slot.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Clicked(index, e.GetPosition(paperHost).Y, 2); };
            System.Windows.Automation.AutomationProperties.SetName(slot, num.Text);
            _slots.Add(slot); _stack.Children.Add(slot);
        }
        Relayout();
    }

    void Relayout()
    {
        if (_doc == null) return;
        double w = PageWidthDip, h = PageHeightDip;
        foreach (var s in _slots) { var host = (Grid)s.Children[0]; host.Width = w; host.Height = h; }
        // neue Breite: Bilder neu rastern, sobald sichtbar
        foreach (var s in _slots) if (s.Children[0] is Grid host && host.Children[0] is Border { Child: Image img } && img.Source is BitmapSource bs && Math.Abs(bs.PixelWidth - PixelWidth) > PixelWidth * 0.25) { img.Source = null; _png.Remove((int)s.Tag); }
        Dispatcher.BeginInvoke(RenderVisible, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    int PixelWidth
    {
        get
        {
            double scale = 1;
            try { scale = VisualTreeHelper.GetDpi(this).DpiScaleX; } catch { }
            return (int)Math.Clamp(PageWidthDip * scale, 200, 3000);
        }
    }

    /// <summary>Sichtbare Seiten (und je eine davor und danach) rastern, weit entfernte freigeben.</summary>
    void RenderVisible()
    {
        if (_doc == null || _raster == null || _slots.Count == 0) return;
        double pitch = PageHeightDip + 18 + 22;
        double top = _sv.VerticalOffset - 20, bottom = top + Math.Max(_sv.ViewportHeight, 400);
        int first = Math.Max(0, (int)(top / pitch) - 1), last = Math.Min(_slots.Count - 1, (int)(bottom / pitch) + 1);
        for (int i = 0; i < _slots.Count; i++)
        {
            var img = ImageOf(i);
            if (i >= first && i <= last)
            {
                if (img.Source == null) Load(i);
            }
            else if (Math.Abs(i - first) > 6 && Math.Abs(i - last) > 6 && img.Source != null) img.Source = null;   // Speicher frei
        }
        // zwischengespeicherte PNGs begrenzen
        if (_png.Count > 60) foreach (var k in _png.Keys.Where(k => k < first - 20 || k > last + 20).ToList()) _png.Remove(k);
    }

    Image ImageOf(int i) => (Image)((Border)((Grid)_slots[i].Children[0]).Children[0]).Child;

    void Load(int i)
    {
        if (_png.TryGetValue(i, out var cached)) { ImageOf(i).Source = Decode(cached); return; }
        if (!_pending.Add(i)) return;
        int gen = _generation, px = PixelWidth;
        var raster = _raster!;
        _ = Task.Run(async () =>
        {
            byte[]? png = null;
            await _renderLock.WaitAsync();
            try { if (gen == _generation) png = raster.RenderPng(i, px); }
            catch (Exception ex) { AppLog.Write("Seitenansicht: " + ex.Message); }
            finally { _renderLock.Release(); }
            await Dispatcher.InvokeAsync(() =>
            {
                if (gen != _generation) return;
                _pending.Remove(i);
                if (png == null || i >= _slots.Count) return;
                _png[i] = png;
                ImageOf(i).Source = Decode(png);
            });
        });
    }

    static BitmapSource Decode(byte[] png)
    {
        var bi = new BitmapImage();
        bi.BeginInit(); bi.StreamSource = new MemoryStream(png); bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
        return bi;
    }

    void Clicked(int page, double yDip, int clicks)
    {
        if (_doc == null) return;
        double yPt = yDip / Math.Max(1, PageHeightDip) * _doc.PageH;
        if (_doc.BlockAt(page, yPt) is { } b) BlockClicked?.Invoke(b, clicks >= 2);
    }

    /// <summary>Block oben im Sichtbereich (für das Wiederherstellen der Lage nach einer Aktualisierung).</summary>
    string? TopBlockKey(out double offset)
    {
        offset = 0;
        if (_doc == null || _slots.Count == 0) return null;
        double pitch = PageHeightDip + 18 + 22;
        int page = Math.Clamp((int)(_sv.VerticalOffset / pitch), 0, _slots.Count - 1);
        double yPt = (_sv.VerticalOffset - page * pitch) / Math.Max(1, PageHeightDip) * _doc.PageH;
        var b = _doc.BlockAt(page, yPt);
        if (b == null) return null;
        offset = (page - b.Page) * _doc.PageH + yPt - b.Top;
        return b.Key;
    }

    /// <summary>Zum Block springen (Schlüssel „s:&lt;Anker&gt;“ oder „c:&lt;Anker&gt;“); <paramref name="flash"/> umrandet ihn kurz.</summary>
    public bool ScrollToKey(string key, bool flash, double offsetPt = -18)
    {
        if (_doc == null) return false;
        var b = _doc.Blocks.FirstOrDefault(x => x.Key == key && !x.Foreign);
        if (b == null) return false;
        double pitch = PageHeightDip + 18 + 22, k = PageHeightDip / _doc.PageH;
        double y = b.Page * pitch + (b.Top + offsetPt) * k;
        _sv.ScrollToVerticalOffset(Math.Max(0, y));
        if (flash) Flash(b);
        return true;
    }

    /// <summary>Ist der Block gerade (teilweise) sichtbar?</summary>
    public bool IsVisible(string key)
    {
        if (_doc == null) return false;
        var b = _doc.Blocks.FirstOrDefault(x => x.Key == key && !x.Foreign);
        if (b == null) return false;
        double pitch = PageHeightDip + 18 + 22, k = PageHeightDip / _doc.PageH;
        double y = b.Page * pitch + b.Top * k;
        return y >= _sv.VerticalOffset && y <= _sv.VerticalOffset + _sv.ViewportHeight - 60;
    }

    void Flash(PdfBlock b)
    {
        if (_doc == null || b.Page >= _slots.Count) return;
        var canvas = (Canvas)((Grid)_slots[b.Page].Children[0]).Children[1];
        double k = PageHeightDip / _doc.PageH;
        var next = _doc.Blocks.Where(x => x.Page == b.Page && x.Top > b.Top + 4).OrderBy(x => x.Top).FirstOrDefault();
        double bottom = next?.Top ?? _doc.PageH - 40;
        var r = new Rectangle { Stroke = Theme.Accent, StrokeThickness = 2, RadiusX = 4, RadiusY = 4, Width = PageWidthDip - 24, Height = Math.Max(16, (bottom - b.Top) * k - 4), Fill = new SolidColorBrush(Color.FromArgb(18, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B)) };
        Canvas.SetLeft(r, 12); Canvas.SetTop(r, Math.Max(0, (b.Top - 4) * k));
        canvas.Children.Clear(); canvas.Children.Add(r);
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(900)) { BeginTime = TimeSpan.FromMilliseconds(900) };
        fade.Completed += (_, _) => canvas.Children.Remove(r);
        r.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Zoom-Tasten wie in der Bildansicht (Strg+Plus/Minus/0/1).</summary>
    public bool HandleKey(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;
        switch (e.Key)
        {
            case Key.OemPlus or Key.Add: Zoom(1.15); return true;
            case Key.OemMinus or Key.Subtract: Zoom(1 / 1.15); return true;
            case Key.D0 or Key.NumPad0: ZoomFitPage(); return true;
            case Key.D1 or Key.NumPad1: ZoomFitWidth(); return true;
        }
        return false;
    }
}
