using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Wpf.Ui.Appearance;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;
using WindowCornerPreference = Wpf.Ui.Controls.WindowCornerPreference;
using Wpf.Ui.Markup;

namespace StepRecorder.App.Ui;

/// <summary>
/// Gestaltung im Windows-11-Stil (Fluent) mit der Bibliothek WPF-UI: deren Themen (hell, dunkel, Hochkontrast) und
/// Steuerelemente, Akzent = Windows-Akzentfarbe, Symbole aus Segoe Fluent Icons / MDL2.
/// <para>
/// Die statischen Pinsel (<see cref="Fg"/>, <see cref="Surface"/> …) zeigen auf die WPF-UI-Ressourcen, damit der Code
/// der Fenster sie weiter direkt verwenden kann. Maße: Schriftgrade <see cref="FsCaption"/> … <see cref="FsHero"/>
/// (nichts unter 12 px), Ecken 4 (Bedienelemente) und 8 (Karten, Menüs) wie in Windows.
/// </para>
/// </summary>
static class Theme
{
    public static bool Dark { get; private set; }
    /// <summary>Windows-Kontrastmodus aktiv (WPF-UI-Hochkontrastthema).</summary>
    public static bool HighContrast { get; private set; }
    public static Brush Bg = null!, Surface = null!, Surface2 = null!, Raised = null!, HoverBg = null!, Selected = null!, Line = null!, LineStrong = null!,
        Fg = null!, Fg2 = null!, Muted = null!, Accent = null!, AccentSoft = null!, AccentText = null!, OnAccent = null!,
        Danger = null!, DangerSoft = null!, OnDanger = null!, Warn = null!, WarnSoft = null!, Success = null!, SuccessSoft = null!, CanvasBg = null!, ScrollThumb = null!,
        Field = null!, FieldLine = null!, CardLine = null!;
    public static Color AccentColor;

    // ---------- Maßsystem ----------
    /// <summary>Schriftgrade: Beschriftung, Hinweis, Text, Zwischentitel, Titel, Seitentitel.</summary>
    public const double FsCaption = 12, FsHint = 13, FsBody = 14, FsSub = 16, FsTitle = 20, FsHero = 28;
    /// <summary>Eckenradien: klein (Tastenkappen, Vorschaubilder), Bedienelemente, Einsätze/Menüs, Karten/Kacheln.</summary>
    public const double RSmall = 4, RControl = 4, RInset = 8, RCard = 8;
    /// <summary>Höhe von Schaltflächen, Eingabefeldern und Auswahllisten.</summary>
    public const double ControlHeight = 32;
    /// <summary>Zeilenhöhe für mehrzeiligen Text (Faktor zur Schriftgröße).</summary>
    public const double LineFactor = 1.45;

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
        // Standard: dunkel. Einstellung „Darstellung“ (dark | light | system); STEPRECORDER_THEME überschreibt.
        var env = Environment.GetEnvironmentVariable("STEPRECORDER_THEME");
        string mode = env is "dark" or "light" or "system" ? env : SettingTheme();
        Dark = mode switch { "light" => false, "system" => SystemUsesDark(), _ => true };
        HighContrast = SystemParameters.HighContrast;
        // WPF-UI: Farben des Themas, Fluent-Steuerelemente, Akzent aus den Windows-Einstellungen.
        // Im Windows-Kontrastmodus wählt WPF-UI selbst das passende Kontrastthema (HC1, HC2, Schwarz, Weiß).
        app.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = Dark ? ApplicationTheme.Dark : ApplicationTheme.Light });
        app.Resources.MergedDictionaries.Add(new ControlsDictionary());
        if (HighContrast)
            try { ApplicationThemeManager.Apply(ApplicationTheme.HighContrast, WindowBackdropType.None, false); } catch { /* dann bleibt hell/dunkel */ }
        else
            try { ApplicationAccentColorManager.ApplySystemAccent(); } catch { /* ohne Systemakzent bleibt das Standardblau */ }

        Brush R(string key, string fallback) => app.TryFindResource(key) as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback));
        Bg = R("ApplicationBackgroundBrush", Dark ? "#202020" : "#FAFAFA");
        Surface = R("CardBackgroundFillColorDefaultBrush", Dark ? "#0DFFFFFF" : "#B3FFFFFF");
        Surface2 = R("CardBackgroundFillColorSecondaryBrush", Dark ? "#08FFFFFF" : "#80F6F6F6");
        Raised = R("SolidBackgroundFillColorQuarternaryBrush", Dark ? "#2C2C2C" : "#FFFFFF");
        HoverBg = R("SubtleFillColorSecondaryBrush", Dark ? "#0FFFFFFF" : "#09000000");
        Selected = R("SubtleFillColorSecondaryBrush", Dark ? "#0FFFFFFF" : "#09000000");
        Line = R("DividerStrokeColorDefaultBrush", Dark ? "#15FFFFFF" : "#0F000000");
        CardLine = R("CardStrokeColorDefaultBrush", Dark ? "#19000000" : "#0F000000");
        LineStrong = R("ControlStrokeColorSecondaryBrush", Dark ? "#18FFFFFF" : "#29000000");
        Fg = R("TextFillColorPrimaryBrush", Dark ? "#FFFFFF" : "#E4000000");
        Fg2 = R("TextFillColorSecondaryBrush", Dark ? "#C5FFFFFF" : "#9E000000");
        // „Tertiary“ erreicht im hellen Thema keine 4,5 : 1 – dort bleibt gedämpfter Text „Secondary“
        Muted = Dark && !HighContrast ? R("TextFillColorTertiaryBrush", "#87FFFFFF") : Fg2;
        Accent = R("AccentFillColorDefaultBrush", Dark ? "#60CDFF" : "#005FB8");
        AccentText = R("AccentTextFillColorPrimaryBrush", Dark ? "#99EBFF" : "#003E92");
        OnAccent = R("TextOnAccentFillColorPrimaryBrush", Dark ? "#000000" : "#FFFFFF");
        AccentColor = (Accent as SolidColorBrush)?.Color ?? Colors.SteelBlue;
        AccentSoft = Frozen(Color.FromArgb((byte)(Dark ? 0x30 : 0x1C), AccentColor.R, AccentColor.G, AccentColor.B));
        Danger = R("SystemFillColorCriticalBrush", Dark ? "#FF99A4" : "#C42B1C");
        DangerSoft = R("SystemFillColorCriticalBackgroundBrush", Dark ? "#442726" : "#FDE7E9");
        OnDanger = Frozen(Dark ? Colors.Black : Colors.White);
        Warn = R("SystemFillColorCautionBrush", Dark ? "#FCE100" : "#9D5D00");
        WarnSoft = R("SystemFillColorCautionBackgroundBrush", Dark ? "#433519" : "#FFF4CE");
        Success = R("SystemFillColorSuccessBrush", Dark ? "#6CCB5F" : "#0F7B0F");
        SuccessSoft = R("SystemFillColorSuccessBackgroundBrush", Dark ? "#393D1B" : "#DFF6DD");
        CanvasBg = R("SolidBackgroundFillColorBaseAltBrush", Dark ? "#0A0A0A" : "#DADADA");
        ScrollThumb = R("ControlStrongFillColorDefaultBrush", Dark ? "#8BFFFFFF" : "#72000000");
        Field = R("ControlFillColorDefaultBrush", Dark ? "#0FFFFFFF" : "#B3FFFFFF");
        FieldLine = R("ControlStrongStrokeColorDefaultBrush", Dark ? "#8BFFFFFF" : "#72000000");
        Night.Init(AccentColor);
        app.Resources["ShadowOpacity"] = Dark ? 0.45 : 0.14;
        // Maße für Theme.xaml (dieselben Werte wie die Konstanten oben)
        app.Resources["RSmall"] = new CornerRadius(RSmall); app.Resources["RControl"] = new CornerRadius(RControl);
        app.Resources["RInner"] = new CornerRadius(RControl - 3); app.Resources["RInset"] = new CornerRadius(RInset); app.Resources["RCard"] = new CornerRadius(RCard);
        app.Resources["RPill"] = new CornerRadius(999);
        app.Resources["FsCaption"] = FsCaption; app.Resources["FsHint"] = FsHint; app.Resources["FsBody"] = FsBody; app.Resources["FsSub"] = FsSub; app.Resources["FsTitle"] = FsTitle;
        app.Resources["ControlHeight"] = ControlHeight;
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/SebastiansStepRecorder;component/Ui/Theme.xaml", UriKind.Relative)));
        // Menüs beim Öffnen ausrichten (Namen gleich breit, Tastenkürzel in einer Spalte)
        void Align(ItemsControl m) => m.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => U.AlignMenu(m));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ContextMenu), System.Windows.Controls.ContextMenu.OpenedEvent, new RoutedEventHandler((s, _) => Align((ItemsControl)s)));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.MenuItem), System.Windows.Controls.MenuItem.SubmenuOpenedEvent, new RoutedEventHandler((s, e) => { if (ReferenceEquals(e.OriginalSource, s)) Align((ItemsControl)s); }));
    }

    static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <summary>Deckende Farbe eines (teil)transparenten Pinsels über <paramref name="under"/> – für DWM-Farben und Bildschirmfotos.</summary>
    public static Color Solid(Brush b, Color under)
    {
        var c = (b as SolidColorBrush)?.Color ?? Colors.Gray;
        double a = c.A / 255.0;
        byte M(byte x, byte y) => (byte)Math.Round(x * a + y * (1 - a));
        return Color.FromRgb(M(c.R, under.R), M(c.G, under.G), M(c.B, under.B));
    }

    /// <summary>
    /// Feste dunkle Farben für alles, was über fremden Programmen schwebt: Aufnahmeleiste, Countdown, Auswahl-Overlays,
    /// Vollbild, Bildunterschrift und Symbol im Infobereich – neutrales Windows-Dunkelgrau, unabhängig von hell/dunkel.
    /// </summary>
    public static class Night
    {
        public static readonly Color BgColor = C(0x20, 0x20, 0x20), RecColor = C(0xFF, 0x4F, 0x58), WarnColor = C(0xFC, 0xE1, 0x00), FgColor = C(0xFF, 0xFF, 0xFF);
        /// <summary>Windows-Akzentfarbe (wird in <see cref="Theme.Init"/> gesetzt).</summary>
        public static Color AccentColor { get; private set; } = C(0x00, 0x78, 0xD4);
        /// <summary>Fläche, Einsatz (z. B. Zähler), Linie.</summary>
        public static readonly Brush Bg = F(BgColor), Chip = F(C(0x2D, 0x2D, 0x2D)), Line = F(C(0x3D, 0x3D, 0x3D));
        /// <summary>Halbtransparente Fläche für Hinweis-Pillen und Bildunterschriften.</summary>
        public static readonly Brush Pill = F(Color.FromArgb(0xF0, 0x20, 0x20, 0x20));
        /// <summary>Text und gedämpfter Text (#CFCFCF: 11,7 : 1 auf <see cref="Bg"/>).</summary>
        public static readonly Brush Fg = F(FgColor), Muted = F(C(0xCF, 0xCF, 0xCF));
        /// <summary>Aufnahme (Punkt, Rahmen), Stopp-Fläche (weiße Schrift 5,9 : 1), Privatmodus.</summary>
        public static readonly Brush Rec = F(RecColor), StopBg = F(C(0xC4, 0x2B, 0x1C)), Warn = F(WarnColor), PrivBg = F(C(0x43, 0x35, 0x19));
        public static Brush Accent { get; private set; } = F(C(0x00, 0x78, 0xD4));
        internal static void Init(Color accent) { AccentColor = accent; Accent = F(accent); }
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

    /// <summary>Leerer Fensterstil: hebt den impliziten WPF-UI-Fensterstil auf (normale WPF-Vorlage).</summary>
    public static Style PlainWindowStyle => _plainWindow ??= new Style(typeof(Window));
    static Style? _plainWindow;

    /// <summary>Hintergrund von Dialogen: eine Ebene über dem Fensterhintergrund (wie WinUI-Dialoge).</summary>
    public static Brush DialogBg => _dialogBg ??= Frozen(Solid(Surface, Solid(Bg, Dark ? Colors.Black : Colors.White)));
    static Brush? _dialogBg;

    /// <summary>
    /// Grundeinstellungen eines Fensters. Fluent-Fenster (Start, Editor) bekommen Mica und die eingebaute Titelleiste;
    /// normale Fenster eine deckende Fläche und eine Windows-Titelleiste in derselben Farbe.
    /// </summary>
    public static void Prepare(Window w, Brush? caption = null)
    {
        w.Foreground = Fg;
        w.FontFamily = UiFont;
        w.FontSize = FsBody;
        w.UseLayoutRounding = true;
        w.SnapsToDevicePixels = true;
        if (w is FluentWindow fw)
        {
            fw.ExtendsContentIntoTitleBar = true;
            fw.WindowCornerPreference = WindowCornerPreference.Round;
            fw.WindowBackdropType = HighContrast ? WindowBackdropType.None : WindowBackdropType.Mica;
            return;
        }
        // WPF-UI bringt einen impliziten Stil für „new Window“ mit (Mindestgröße 460 × 320, eigene Vorlage) – hier nicht gewünscht
        if (w.GetType() == typeof(Window)) w.Style = PlainWindowStyle;
        var bgColor = Solid(caption ?? DialogBg, Solid(Bg, Dark ? Colors.Black : Colors.White));
        w.Background = Frozen(bgColor);
        w.SourceInitialized += (_, _) => Chrome(new WindowInteropHelper(w).Handle, bgColor);
    }

    public static void Chrome(nint h, Color caption)
    {
        if (h == 0) return;
        try
        {
            int dark = Dark ? 1 : 0; DwmSetWindowAttribute(h, 20, ref dark, 4);
            int cap = caption.R | caption.G << 8 | caption.B << 16; DwmSetWindowAttribute(h, 35, ref cap, 4);
            var f = Solid(Fg, caption); int txt = f.R | f.G << 8 | f.B << 16; DwmSetWindowAttribute(h, 36, ref txt, 4);
            var l = Solid(Line, caption); int border = l.R | l.G << 8 | l.B << 16; DwmSetWindowAttribute(h, 34, ref border, 4);
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
        => new() { BlurRadius = blur, ShadowDepth = depth, Direction = 270, Color = Colors.Black, Opacity = opacity ?? (Dark ? 0.45 : 0.14) };

    /// <summary>
    /// Logo: eine Treppenlinie (Schritte), die in einem roten Aufnahmepunkt endet – flach auf dunkler Kachel mit feinem Rand,
    /// funktioniert hell und dunkel. Dieselbe Geometrie wie das Programmsymbol (Assets/app.ico).
    /// </summary>
    public static FrameworkElement Logo(double size)
    {
        var ink = Brushes.White;
        var red = Frozen(Color.FromRgb(0xFF, 0x4F, 0x58));
        var c = new Canvas { Width = size, Height = size };
        double u = size / 24.0;
        var path = new Polyline
        {
            Points = new PointCollection { new(5 * u, 18 * u), new(9.5 * u, 18 * u), new(9.5 * u, 13.5 * u), new(14 * u, 13.5 * u), new(14 * u, 9 * u), new(16 * u, 9 * u) },
            Stroke = ink, StrokeThickness = 2.3 * u, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Flat
        };
        c.Children.Add(path);
        double r = 2.6 * u;
        var dot = new Ellipse { Width = 2 * r, Height = 2 * r, Fill = red };
        Canvas.SetLeft(dot, 18.2 * u - r); Canvas.SetTop(dot, 9 * u - r); c.Children.Add(dot);
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.22), Background = Frozen(Color.FromRgb(0x1F, 0x1F, 0x1F)),
            BorderBrush = Frozen(Color.FromRgb(0x48, 0x48, 0x48)), BorderThickness = new Thickness(Math.Max(1, size / 32)), Child = c };
    }

    /// <summary>Logo als Bild (z. B. für das Symbol in der Titelleiste).</summary>
    public static ImageSource LogoImage(int px)
    {
        var e = Logo(px);
        e.Measure(new Size(px, px)); e.Arrange(new Rect(0, 0, px, px));
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(e); bmp.Freeze();
        return bmp;
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
