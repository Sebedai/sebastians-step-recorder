using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Präsentationsansicht (13, Paket 38, Umschalt+F5): Titel-, Kapitel-, Schritt- und Schlussfolie im Vollbild; Pfeiltasten,
/// Leertaste, Bild↑/↓, Pos1/Ende, Esc. Entscheidung des Auftraggebers: bei Bildschirmfreigabe sichtbar, solange keine Aufnahme
/// läuft – beim Start einer Aufnahme schließt sie sich (sonst stünde sie in den Bildern).
/// </summary>
sealed class PresentationWindow : Window
{
    static readonly List<PresentationWindow> Open = new();
    abstract record Slide;
    sealed record TitleSlide(string Title, string Sub) : Slide;
    sealed record ChapterSlide(string Number, string Title) : Slide;
    sealed record StepSlide(ViewStep Step, string Number) : Slide;
    sealed record EndSlide(string Text) : Slide;

    readonly SessionView _view;
    readonly List<Slide> _slides = new();
    readonly Grid _stage = new();
    readonly TextBlock _counter = new() { Foreground = Brushes.Gray, FontSize = 14, Margin = new Thickness(24, 0, 24, 16), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
    int _i;

    public PresentationWindow(SessionView view, long? startAnchor)
    {
        _view = view;
        Title = AppInfo.DisplayName; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized; Topmost = true;
        Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)); Foreground = Brushes.White;
        var guide = view.Edits.Obj("guide");
        string title = guide?.Str("title") is { Length: > 0 } t ? view.CleanVars(t) : view.Title;
        _slides.Add(new TitleSlide(title, DocSettings.From(guide, view.Language).Subtitle));
        var nums = GuideExport.StepNumbers(view);
        int chap = 0;
        foreach (var s in view.GuideSteps())
        {
            if (s.Kind == "move") continue;
            if (s.ChapterTitle is { Length: > 0 } ct) _slides.Add(new ChapterSlide((++chap).ToString(), ct));
            _slides.Add(new StepSlide(s, nums.TryGetValue(s.Anchor, out var n) ? n : ""));
            if (startAnchor == s.Anchor) _i = _slides.Count - 1;
        }
        _slides.Add(new EndSlide(Loc.For(view.Language, "Fertig!", "Done!")));
        var root = new Grid();
        root.Children.Add(_stage); root.Children.Add(_counter);
        Content = root;
        KeyDown += OnKey;
        MouseLeftButtonUp += (_, _) => Go(_i + 1);
        MouseRightButtonUp += (_, _) => Go(_i - 1);
        IntPtr hwnd = IntPtr.Zero;
        SourceInitialized += (_, _) => { hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle; Invisibility.Exempt(hwnd, true); };
        Closed += (_, _) => { Open.Remove(this); if (hwnd != IntPtr.Zero) Invisibility.Exempt(hwnd, false); };
        Open.Add(this);
        Show_();
    }

    /// <summary>Beim Start einer Aufnahme alle Präsentationen schließen.</summary>
    public static void CloseAll() { foreach (var w in Open.ToList()) w.Close(); }

    void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right or Key.Down or Key.Space or Key.PageDown or Key.Enter: Go(_i + 1); break;
            case Key.Left or Key.Up or Key.PageUp or Key.Back: Go(_i - 1); break;
            case Key.Home: Go(0); break;
            case Key.End: Go(_slides.Count - 1); break;
            case Key.Escape: Close(); break;
            default: return;
        }
        e.Handled = true;
    }

    void Go(int i) { _i = Math.Clamp(i, 0, _slides.Count - 1); Show_(); }

    void Show_()
    {
        _stage.Children.Clear();
        _counter.Text = $"{_i + 1} / {_slides.Count}";
        var accent = new SolidColorBrush(DocSettings.From(_view.Edits.Obj("guide")).AccentRgb() is var (r, g, b) ? Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)) : Colors.OrangeRed);
        TextBlock T(string text, double size, bool bold = false, Brush? fg = null) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = fg ?? Brushes.White, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        switch (_slides[_i])
        {
            case TitleSlide ts:
                _stage.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { T(ts.Title, 56, true), T(ts.Sub, 26, false, Brushes.Gray) } });
                break;
            case ChapterSlide cs:
                _stage.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { T(cs.Number, 72, true, accent), T(cs.Title, 48, true) } });
                break;
            case EndSlide es:
                _stage.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { T(es.Text, 56, true) } });
                break;
            case StepSlide ss:
                {
                    var s = ss.Step;
                    var grid = new Grid { Margin = new Thickness(48, 32, 48, 48) };
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var head = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
                    if (ss.Number.Length > 0) head.Children.Add(new Border { Background = accent, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 14, 0), Child = T(ss.Number, 30, true) });
                    head.Children.Add(T(s.AuthorTitle ?? s.Title, 34, true));
                    grid.Children.Add(head);
                    if (!s.Redacted && s.ShowImage != false && !s.TextSection)
                    {
                        try
                        {
                            using var img = StepImages.Render(_view, s, StepImages.GuideVariant(s, "screen"), "original", annotate: true, authorAnnotations: true, stepLabel: _view.MarksFor(s).Number ? ss.Number : null);
                            if (img != null)
                            {
                                var image = new Image { Source = U.ToSource(img.Bitmap), Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 16) };
                                Grid.SetRow(image, 1); grid.Children.Add(image);
                            }
                        }
                        catch (Exception ex) { AppLog.Write("Präsentation: " + ex.Message); }
                    }
                    var body = T(s.AuthorBody ?? s.Guide, 24, false, new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)));
                    body.MaxWidth = 1400;
                    Grid.SetRow(body, 2); grid.Children.Add(body);
                    _stage.Children.Add(grid);
                    break;
                }
        }
    }
}
