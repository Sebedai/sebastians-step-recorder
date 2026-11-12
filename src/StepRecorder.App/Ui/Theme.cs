using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace StepRecorder.App.Ui;

/// <summary>
/// Gestaltung: neutrale Farbpalette (hell/dunkel nach Windows-Einstellung), Stile aus Theme.xaml,
/// Fenster-Titelleiste in Hintergrundfarbe (Windows 11), Symbole aus Segoe Fluent Icons / MDL2.
/// </summary>
static class Theme
{
    public static bool Dark { get; private set; }
    public static Brush Bg = null!, Surface = null!, Surface2 = null!, HoverBg = null!, Line = null!, LineStrong = null!,
        Fg = null!, Fg2 = null!, Muted = null!, Accent = null!, AccentSoft = null!, AccentText = null!, OnAccent = null!,
        Danger = null!, DangerSoft = null!, Warn = null!, WarnSoft = null!, Success = null!, SuccessSoft = null!, CanvasBg = null!, ScrollThumb = null!;
    public static Color AccentColor;
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");
    /// <summary>Überschriften: engere Display-Variante (Windows 11), sonst Segoe UI.</summary>
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas");
    public const string IArrowUp = "", IArrowDown = "";
    public const string IZoomIn = "", IZoomOut = "", IFit = "", IActual = "", IFullScreen = "", IPixelate = "", IStroke = "";

    // Symbole (Segoe Fluent Icons / MDL2)
    public const string IRecord = "", IStop = "", IPause = "", IPlay = "", ISettings = "", IOpen = "", IFolder = "",
        IHelp = "", IInfo = "", IWarn = "", ILock = "", IUnlock = "", IEye = "", ICopy = "", ISave = "",
        IExport = "", IDelete = "", IEdit = "", IComment = "", ISearch = "", IClose = "", ICheck = "",
        IPrev = "", INext = "", IDown = "", IFlag = "", ICrop = "", ICompare = "", IClock = "", IStopwatch = "",
        IApp = "", IMonitor = "", IWrench = "", IShield = "", ICamera = "", IMouse = "", IMove = "", IScroll = "",
        IKeyboard = "", IHotkey = "", IHover = "", ISystem = "", IError = "", IImportant = "", IQuestion = "",
        IDot = "", IDoc = "", IRobot = "", IUndo = "", IRedo = "", IPointer = "", IHighlight = "", IText = "",
        IArrow = "", IRect = "", IRedact = "", IMore = "", IGrip = "", IGlobe = "", ICode = "", IPdf = "",
        IChecklist = "", IHistory = "", IAdd = "", IScript = "", IWindow = "";

    public static void Init(Application app)
    {
        var env = Environment.GetEnvironmentVariable("STEPRECORDER_THEME");
        Dark = env == "dark" || (env != "light" && SystemUsesDark());
        var p = Dark ? DarkPalette : LightPalette;
        Brush B(string k) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p[k])); b.Freeze(); app.Resources[k] = b; return b; }
        Bg = B("Bg"); Surface = B("Surface"); Surface2 = B("Surface2"); HoverBg = B("HoverBg"); Line = B("Line"); LineStrong = B("LineStrong");
        Fg = B("Fg"); Fg2 = B("Fg2"); Muted = B("Muted"); Accent = B("Accent"); AccentSoft = B("AccentSoft"); AccentText = B("AccentText"); OnAccent = B("OnAccent");
        Danger = B("Danger"); DangerSoft = B("DangerSoft"); Warn = B("Warn"); WarnSoft = B("WarnSoft"); Success = B("Success"); SuccessSoft = B("SuccessSoft");
        CanvasBg = B("CanvasBg"); ScrollThumb = B("ScrollThumb"); B("AccentHover"); B("SegmentOn");
        AccentColor = ((SolidColorBrush)Accent).Color;
        app.Resources["ShadowOpacity"] = Dark ? 0.45 : 0.10;
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/StepRecorder;component/Ui/Theme.xaml", UriKind.Relative)));
    }

    // Eigene Linie: warmes Graphit statt Blaugrau, Signal-Orange als einzige Markenfarbe, Haarlinien statt Schatten.
    static readonly Dictionary<string, string> LightPalette = new()
    {
        ["Bg"] = "#F5F4F0", ["Surface"] = "#FFFFFF", ["Surface2"] = "#EFEEE9", ["HoverBg"] = "#E9E7E1", ["Line"] = "#E3E1DA", ["LineStrong"] = "#CCC9BF",
        ["Fg"] = "#171614", ["Fg2"] = "#46443E", ["Muted"] = "#7A776E", ["Accent"] = "#F2541B", ["AccentHover"] = "#DB4612", ["AccentSoft"] = "#FCEBE3",
        ["AccentText"] = "#C2410C", ["OnAccent"] = "#FFFFFF", ["Danger"] = "#D6264A", ["DangerSoft"] = "#FBE8EC", ["Warn"] = "#A86400", ["WarnSoft"] = "#FBF1DC",
        ["Success"] = "#2F7D4F", ["SuccessSoft"] = "#E5F2E8", ["CanvasBg"] = "#E7E5DF", ["ScrollThumb"] = "#C9C6BC", ["SegmentOn"] = "#FFFFFF"
    };

    static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["Bg"] = "#121211", ["Surface"] = "#191918", ["Surface2"] = "#20201E", ["HoverBg"] = "#282826", ["Line"] = "#2B2B28", ["LineStrong"] = "#3B3B37",
        ["Fg"] = "#EDECE8", ["Fg2"] = "#BAB8B1", ["Muted"] = "#86847D", ["Accent"] = "#FF6A2B", ["AccentHover"] = "#FF7F47", ["AccentSoft"] = "#2E1C12",
        ["AccentText"] = "#FF9461", ["OnAccent"] = "#141312", ["Danger"] = "#F0556E", ["DangerSoft"] = "#36181E", ["Warn"] = "#E9B14C", ["WarnSoft"] = "#30250F",
        ["Success"] = "#62C28C", ["SuccessSoft"] = "#16291D", ["CanvasBg"] = "#0C0C0B", ["ScrollThumb"] = "#45443F", ["SegmentOn"] = "#2F2F2C"
    };

    static bool SystemUsesDark()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public static Style Style(string key) => (Style)Application.Current.FindResource(key);

    /// <summary>Grundeinstellungen eines Fensters; Titelleiste in der Farbe <paramref name="caption"/>.</summary>
    public static void Prepare(Window w, Brush? caption = null)
    {
        w.Background = caption ?? Bg;
        w.Foreground = Fg;
        w.FontFamily = UiFont;
        w.FontSize = 13;
        w.UseLayoutRounding = true;
        w.SnapsToDevicePixels = true;
        var cap = ((SolidColorBrush)(caption ?? Bg)).Color;
        w.SourceInitialized += (_, _) => Chrome(new WindowInteropHelper(w).Handle, cap);
    }

    public static void Chrome(nint h, Color caption)
    {
        if (h == 0) return;
        try
        {
            int dark = Dark ? 1 : 0; DwmSetWindowAttribute(h, 20, ref dark, 4);
            int cap = caption.R | caption.G << 8 | caption.B << 16; DwmSetWindowAttribute(h, 35, ref cap, 4);
            var f = ((SolidColorBrush)Fg).Color; int txt = f.R | f.G << 8 | f.B << 16; DwmSetWindowAttribute(h, 36, ref txt, 4);
            var l = ((SolidColorBrush)Line).Color; int border = l.R | l.G << 8 | l.B << 16; DwmSetWindowAttribute(h, 34, ref border, 4);
        }
        catch { }
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint h, int attr, ref int value, int size);

    /// <summary>Monitor unter dem Mauszeiger (dort öffnen sich die Fenster).</summary>
    public static StepRecorder.Native.MonitorInfo? ActiveMonitor()
    {
        try
        {
            StepRecorder.Native.Win32.GetCursorPos(out var p);
            var mons = StepRecorder.Native.ScreenGrabber.GetMonitors();
            return mons.FirstOrDefault(x => x.Bounds.Contains(p.X, p.Y)) ?? mons.FirstOrDefault(x => x.Primary) ?? mons.FirstOrDefault();
        }
        catch { return null; }
    }

    /// <summary>Arbeitsbereich des aktiven Monitors in geräteunabhängigen Einheiten.</summary>
    public static Rect ActiveWorkArea()
    {
        var m = ActiveMonitor();
        if (m == null || m.Dpi <= 0) return SystemParameters.WorkArea;
        double s = m.Dpi / 96.0;
        return new Rect(m.WorkArea.X / s, m.WorkArea.Y / s, m.WorkArea.Width / s, m.WorkArea.Height / s);
    }

    public static DropShadowEffect Shadow(double blur = 18, double depth = 2, double? opacity = null)
        => new() { BlurRadius = blur, ShadowDepth = depth, Direction = 270, Color = Colors.Black, Opacity = opacity ?? (Dark ? 0.45 : 0.10) };

    /// <summary>
    /// Logo: eine Treppenlinie (Schritte), die in einem Aufnahmepunkt endet – flach, ohne Verlauf.
    /// Orange Fläche, Linie in Tinte; funktioniert hell und dunkel.
    /// </summary>
    public static FrameworkElement Logo(double size)
    {
        var ink = new SolidColorBrush(Color.FromRgb(0x17, 0x16, 0x14));
        var c = new Canvas { Width = size, Height = size };
        double u = size / 24.0;
        var path = new Polyline
        {
            Points = new PointCollection { new(5 * u, 18 * u), new(9.5 * u, 18 * u), new(9.5 * u, 13.5 * u), new(14 * u, 13.5 * u), new(14 * u, 9 * u), new(16 * u, 9 * u) },
            Stroke = ink, StrokeThickness = 2.3 * u, StrokeLineJoin = PenLineJoin.Miter, StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat
        };
        c.Children.Add(path);
        double r = 2.6 * u;
        var dot = new Ellipse { Width = 2 * r, Height = 2 * r, Fill = ink };
        Canvas.SetLeft(dot, 18.2 * u - r); Canvas.SetTop(dot, 9 * u - r); c.Children.Add(dot);
        var bg = new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x2B));
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.2), Background = bg, Child = c };
    }

    /// <summary>Wortmarke: „Sebastians“ leicht, „Step Recorder“ kräftig.</summary>
    public static TextBlock Wordmark(double size, Brush? fg = null)
    {
        var t = new TextBlock { FontFamily = Display, FontSize = size, VerticalAlignment = VerticalAlignment.Center, Foreground = fg ?? Fg };
        t.Inlines.Add(new System.Windows.Documents.Run("Sebastians ") { FontWeight = FontWeights.Normal, Foreground = Muted });
        t.Inlines.Add(new System.Windows.Documents.Run("Step Recorder") { FontWeight = FontWeights.SemiBold });
        return t;
    }

}
