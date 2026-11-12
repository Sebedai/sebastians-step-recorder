using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace StepRecorder.App.Ui;

/// <summary>
/// Gestaltung „Indigo“: Nachtblau und ein Indigo-Akzent, weiche runde Formen (hell/dunkel), Stile aus Theme.xaml,
/// Fenster-Titelleiste in Hintergrundfarbe (Windows 11), Symbole aus Segoe Fluent Icons / MDL2.
/// <para>
/// Ein Maßsystem für alle Fenster: Schriftgrade <see cref="FsCaption"/> … <see cref="FsHero"/> (nichts unter 12 px),
/// Eckenradien <see cref="RSmall"/> … <see cref="RCard"/>, Bedienelemente <see cref="ControlHeight"/> hoch.
/// Dieselben Werte liegen als Ressourcen (z. B. <c>RControl</c>, <c>FsBody</c>) für Theme.xaml bereit.
/// </para>
/// </summary>
static class Theme
{
    public static bool Dark { get; private set; }
    public static Brush Bg = null!, Surface = null!, Surface2 = null!, Raised = null!, HoverBg = null!, Selected = null!, Line = null!, LineStrong = null!,
        Fg = null!, Fg2 = null!, Muted = null!, Accent = null!, AccentSoft = null!, AccentText = null!, OnAccent = null!,
        Danger = null!, DangerSoft = null!, OnDanger = null!, Warn = null!, WarnSoft = null!, Success = null!, SuccessSoft = null!, CanvasBg = null!, ScrollThumb = null!,
        Field = null!, FieldLine = null!;
    public static Color AccentColor;

    // ---------- Maßsystem ----------
    /// <summary>Schriftgrade: Beschriftung, Hinweis, Text, Zwischentitel, Titel, Begrüßung.</summary>
    public const double FsCaption = 12, FsHint = 13, FsBody = 14, FsSub = 16, FsTitle = 20, FsHero = 28;
    /// <summary>Eckenradien: klein (Tastenkappen, Vorschaubilder), Bedienelemente, Einsätze/Menüs, Karten/Kacheln.</summary>
    public const double RSmall = 6, RControl = 10, RInset = 12, RCard = 16;
    /// <summary>Höhe von Schaltflächen, Eingabefeldern und Auswahllisten.</summary>
    public const double ControlHeight = 34;
    /// <summary>Zeilenhöhe für mehrzeiligen Text (Faktor zur Schriftgröße).</summary>
    public const double LineFactor = 1.45;

    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly FontFamily UiFont = new FontFamily("Segoe UI Variable Text, Segoe UI");
    /// <summary>Überschriften: engere Display-Variante (Windows 11), sonst Segoe UI.</summary>
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas");
    public const string IArrowUp = "", IArrowDown = "";
    /// <summary>Freihand (Stift), Spotlight (Helligkeit), Lupe.</summary>
    public const string IInk = "", ISpot = "", IMagnifier = "";
    public const string IZoomIn ="", IZoomOut = "", IFit = "", IActual = "", IFullScreen = "", IPixelate = "", IStroke = "";

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
        // Standard: dunkel. Einstellung „Darstellung“ (dark | light | system); STEPRECORDER_THEME überschreibt.
        var env = Environment.GetEnvironmentVariable("STEPRECORDER_THEME");
        string mode = env is "dark" or "light" or "system" ? env : SettingTheme();
        Dark = mode switch { "light" => false, "system" => SystemUsesDark(), _ => true };
        var p = Dark ? DarkPalette : LightPalette;
        Brush B(string k) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p[k])); b.Freeze(); app.Resources[k] = b; return b; }
        Bg = B("Bg"); Surface = B("Surface"); Surface2 = B("Surface2"); Raised = B("Raised"); HoverBg = B("HoverBg"); Selected = B("Selected"); Line = B("Line"); LineStrong = B("LineStrong");
        Fg = B("Fg"); Fg2 = B("Fg2"); Muted = B("Muted"); Accent = B("Accent"); AccentSoft = B("AccentSoft"); AccentText = B("AccentText"); OnAccent = B("OnAccent");
        Danger = B("Danger"); DangerSoft = B("DangerSoft"); OnDanger = B("OnDanger"); Warn = B("Warn"); WarnSoft = B("WarnSoft"); Success = B("Success"); SuccessSoft = B("SuccessSoft");
        CanvasBg = B("CanvasBg"); ScrollThumb = B("ScrollThumb"); Field = B("Field"); FieldLine = B("FieldLine");
        B("AccentHover"); B("DangerHover"); B("SegmentOn"); B("ControlBg"); B("ControlLine");
        AccentColor = ((SolidColorBrush)Accent).Color;
        app.Resources["ShadowOpacity"] = Dark ? 0.5 : 0.12;
        // Maße für Theme.xaml (dieselben Werte wie die Konstanten oben)
        app.Resources["RSmall"] = new CornerRadius(RSmall); app.Resources["RControl"] = new CornerRadius(RControl);
        app.Resources["RInner"] = new CornerRadius(RControl - 3); app.Resources["RInset"] = new CornerRadius(RInset); app.Resources["RCard"] = new CornerRadius(RCard);
        app.Resources["RPill"] = new CornerRadius(999);
        app.Resources["FsCaption"] = FsCaption; app.Resources["FsHint"] = FsHint; app.Resources["FsBody"] = FsBody; app.Resources["FsSub"] = FsSub; app.Resources["FsTitle"] = FsTitle;
        app.Resources["ControlHeight"] = ControlHeight;
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/SebastiansStepRecorder;component/Ui/Theme.xaml", UriKind.Relative)));
    }

    // „Indigo“: kühles Nachtblau, Indigo als einzige Markenfarbe; Rot, Gelb und Grün bleiben Statusfarben.
    // Alle Textpaare erreichen mindestens 4,5 : 1 (WCAG AA), Feldränder und Bildlaufleisten mindestens 3 : 1.
    static readonly Dictionary<string, string> LightPalette = new()
    {
        ["Bg"] = "#F4F5FA", ["Surface"] = "#FFFFFF", ["Surface2"] = "#ECEEF6", ["Raised"] = "#FFFFFF", ["HoverBg"] = "#E3E6F2", ["Selected"] = "#E2E5FB",
        ["Line"] = "#DCDFEA", ["LineStrong"] = "#C3C8D9", ["Fg"] = "#12141C", ["Fg2"] = "#3D4253", ["Muted"] = "#5C6275",
        ["Accent"] = "#4B55E0", ["AccentHover"] = "#3F49D1", ["AccentSoft"] = "#E7E9FD", ["AccentText"] = "#3B44C4", ["OnAccent"] = "#FFFFFF",
        ["Danger"] = "#C01F45", ["DangerHover"] = "#A81A3C", ["DangerSoft"] = "#FCE8ED", ["OnDanger"] = "#FFFFFF", ["Warn"] = "#875000", ["WarnSoft"] = "#FBF1DC",
        ["Success"] = "#1F6B45", ["SuccessSoft"] = "#E3F3EA", ["CanvasBg"] = "#E4E6EF", ["ScrollThumb"] = "#8389A0",
        ["Field"] = "#FFFFFF", ["FieldLine"] = "#81879C", ["SegmentOn"] = "#FFFFFF", ["ControlBg"] = "#FFFFFF", ["ControlLine"] = "#C3C8D9"
    };

    static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["Bg"] = "#0E1016", ["Surface"] = "#161923", ["Surface2"] = "#1E2230", ["Raised"] = "#1D2131", ["HoverBg"] = "#272C3D", ["Selected"] = "#252A42",
        ["Line"] = "#2B3042", ["LineStrong"] = "#3A4058", ["Fg"] = "#F1F3F9", ["Fg2"] = "#C3C8D8", ["Muted"] = "#9BA2B8",
        ["Accent"] = "#5B66F0", ["AccentHover"] = "#4E59E6", ["AccentSoft"] = "#1D2142", ["AccentText"] = "#A5ADFF", ["OnAccent"] = "#FFFFFF",
        ["Danger"] = "#FF6B81", ["DangerHover"] = "#FF8193", ["DangerSoft"] = "#3A1A24", ["OnDanger"] = "#1A0A0F", ["Warn"] = "#F2B84B", ["WarnSoft"] = "#33280F",
        ["Success"] = "#5CCB95", ["SuccessSoft"] = "#13291F", ["CanvasBg"] = "#0A0C11", ["ScrollThumb"] = "#606886",
        ["Field"] = "#10131B", ["FieldLine"] = "#6B7391", ["SegmentOn"] = "#2C3247", ["ControlBg"] = "#222738", ["ControlLine"] = "#343A50"
    };

    /// <summary>
    /// Feste dunkle Farben für alles, was über fremden Programmen schwebt: Aufnahmeleiste, Countdown, Auswahl-Overlays,
    /// Vollbild, Bildunterschrift und Symbol im Infobereich – unabhängig von hell/dunkel, damit es auf jedem Hintergrund trägt.
    /// </summary>
    public static class Night
    {
        public static readonly Color BgColor = C(0x12, 0x14, 0x1C), AccentColor = C(0x5B, 0x66, 0xF0), RecColor = C(0xFF, 0x54, 0x68), WarnColor = C(0xFF, 0xC5, 0x3D), FgColor = C(0xF1, 0xF3, 0xF9);
        /// <summary>Fläche, Einsatz (z. B. Zähler), Linie.</summary>
        public static readonly Brush Bg = F(BgColor), Chip = F(C(0x22, 0x27, 0x38)), Line = F(C(0x2E, 0x34, 0x47));
        /// <summary>Halbtransparente Fläche für Hinweis-Pillen und Bildunterschriften.</summary>
        public static readonly Brush Pill = F(Color.FromArgb(0xEE, 0x12, 0x14, 0x1C));
        /// <summary>Text (16,6 : 1) und gedämpfter Text (7,9 : 1 auf <see cref="Bg"/>).</summary>
        public static readonly Brush Fg = F(FgColor), Muted = F(C(0xA3, 0xAA, 0xC0));
        /// <summary>Aufnahme (Punkt, Rahmen), Stopp-Fläche (weiße Schrift 5,3 : 1), Privatmodus, Akzent.</summary>
        public static readonly Brush Rec = F(RecColor), StopBg = F(C(0xCC, 0x2A, 0x45)), Warn = F(WarnColor), PrivBg = F(C(0x45, 0x34, 0x0C)), Accent = F(AccentColor), AccentText = F(C(0xA5, 0xAD, 0xFF));
        static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
        static Brush F(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    }

    static string SettingTheme()
    {
        try { return StepRecorder.Settings.AppSettings.Load().Theme; } catch { return "dark"; }
    }

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
        w.FontSize = FsBody;
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
        => new() { BlurRadius = blur, ShadowDepth = depth, Direction = 270, Color = Colors.Black, Opacity = opacity ?? (Dark ? 0.5 : 0.12) };

    /// <summary>Indigo-Verlauf (leicht ins Violette) für hervorgehobene Flächen: Logo, Hauptkachel. Weiße Schrift darauf ≥ 4,5 : 1.</summary>
    public static LinearGradientBrush AccentGradient(double violet = 1)
    {
        var a = Dark ? Color.FromRgb(0x5B, 0x66, 0xF0) : Color.FromRgb(0x4B, 0x55, 0xE0);
        var v = Dark ? Color.FromRgb(0x6E, 0x5B, 0xEF) : Color.FromRgb(0x5E, 0x4B, 0xE0);
        Color Mix(Color x, Color y, double t) => Color.FromRgb((byte)(x.R + (y.R - x.R) * t), (byte)(x.G + (y.G - x.G) * t), (byte)(x.B + (y.B - x.B) * t));
        var g = new LinearGradientBrush(a, Mix(a, v, violet), new Point(0, 1), new Point(1, 0));
        g.Freeze();
        return g;
    }

    /// <summary>
    /// Logo: eine Treppenlinie (Schritte), die in einem Aufnahmepunkt endet. Indigo-Fläche, Linie und Punkt weiß;
    /// funktioniert hell und dunkel. Dieselbe Geometrie wie das Programmsymbol (Assets/app.ico).
    /// </summary>
    public static FrameworkElement Logo(double size)
    {
        var ink = Brushes.White;
        var c = new Canvas { Width = size, Height = size };
        double u = size / 24.0;
        var path = new Polyline
        {
            Points = new PointCollection { new(5 * u, 18 * u), new(9.5 * u, 18 * u), new(9.5 * u, 13.5 * u), new(14 * u, 13.5 * u), new(14 * u, 9 * u), new(16 * u, 9 * u) },
            Stroke = ink, StrokeThickness = 2.3 * u, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Flat
        };
        c.Children.Add(path);
        double r = 2.6 * u;
        var dot = new Ellipse { Width = 2 * r, Height = 2 * r, Fill = ink };
        Canvas.SetLeft(dot, 18.2 * u - r); Canvas.SetTop(dot, 9 * u - r); c.Children.Add(dot);
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.28), Background = AccentGradient(), Child = c };
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
