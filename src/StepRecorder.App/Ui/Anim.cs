using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace StepRecorder.App.Ui;

/// <summary>
/// Dezente Animationen: Einblenden mit kleinem Versatz, gestaffelt, weiches Überblenden, ruhiges Pulsieren,
/// kurzes Aufspringen. Abgeschaltet, wenn Windows keine Animationen zeigen soll (Barrierefreiheit) oder
/// STEPRECORDER_NOANIM=1 gesetzt ist.
/// </summary>
static class Anim
{
    public static bool On => SystemParameters.ClientAreaAnimation && Environment.GetEnvironmentVariable("STEPRECORDER_NOANIM") != "1";

    static readonly IEasingFunction Out = new CubicEase { EasingMode = EasingMode.EaseOut };
    static readonly IEasingFunction Back = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };

    static DoubleAnimation A(double? from, double to, int ms, int delay = 0, IEasingFunction? ease = null) => new()
    {
        From = from, To = to, Duration = TimeSpan.FromMilliseconds(ms), BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = ease ?? Out
    };

    /// <summary>Einblenden mit Versatz nach oben (dy &gt; 0) bzw. unten; danach ist das Element wieder ganz normal.</summary>
    public static void Appear(UIElement e, int delayMs = 0, double dy = 10, int ms = 420)
    {
        if (!On) return;
        var tt = new TranslateTransform(0, dy);
        var old = e.RenderTransform;
        e.RenderTransform = old is null || old == Transform.Identity ? tt : new TransformGroup { Children = { old, tt } };
        e.Opacity = 0;
        var fade = A(0, 1, ms, delayMs);
        fade.FillBehavior = FillBehavior.Stop;
        fade.Completed += (_, _) => { e.Opacity = 1; e.RenderTransform = old ?? Transform.Identity; };
        var move = A(dy, 0, ms + 80, delayMs);
        tt.BeginAnimation(TranslateTransform.YProperty, move);
        e.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>Kinder eines Panels nacheinander einblenden.</summary>
    public static void Stagger(Panel p, int startMs = 0, int stepMs = 55, double dy = 8, int max = 14)
    {
        if (!On) return;
        int i = 0;
        foreach (UIElement c in p.Children) { if (i >= max) break; Appear(c, startMs + i++ * stepMs, dy); }
    }

    /// <summary>Weiches Überblenden nach einem Inhaltswechsel (z. B. anderer Schritt).</summary>
    public static void Refresh(UIElement e, double from = 0.25, int ms = 220)
    {
        if (!On) return;
        var a = A(from, 1, ms);
        a.FillBehavior = FillBehavior.Stop;
        e.BeginAnimation(UIElement.OpacityProperty, a);
    }

    /// <summary>Ruhiges „Atmen“ (Deckkraft) – z. B. Aufnahmepunkt.</summary>
    public static void Breathe(UIElement e, double min = 0.35, int periodMs = 1600)
    {
        if (!On) return;
        var a = new DoubleAnimation(1, min, TimeSpan.FromMilliseconds(periodMs / 2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        e.BeginAnimation(UIElement.OpacityProperty, a);
    }

    /// <summary>Kurz aufspringen (Skalierung mit leichtem Überschwingen).</summary>
    public static void Pop(UIElement e, double from = 0.7, int ms = 320)
    {
        if (!On) return;
        if (e is FrameworkElement fe) fe.RenderTransformOrigin = new Point(0.5, 0.5);
        var st = new ScaleTransform(from, from);
        e.RenderTransform = st;
        st.BeginAnimation(ScaleTransform.ScaleXProperty, A(from, 1, ms, 0, Back));
        st.BeginAnimation(ScaleTransform.ScaleYProperty, A(from, 1, ms, 0, Back));
    }

    /// <summary>Weicher Leuchtschein in Akzentfarbe (Schatten ohne Versatz); beim Überfahren etwas kräftiger.</summary>
    public static void Glow(FrameworkElement e, Color color, double rest = 0.32, double hover = 0.6, double blur = 30)
    {
        var fx = new DropShadowEffect { Color = color, BlurRadius = blur, ShadowDepth = 0, Opacity = rest };
        e.Effect = fx;
        if (!On) return;
        e.MouseEnter += (_, _) => fx.BeginAnimation(DropShadowEffect.OpacityProperty, A(null, hover, 220));
        e.MouseLeave += (_, _) => fx.BeginAnimation(DropShadowEffect.OpacityProperty, A(null, rest, 320));
    }

    /// <summary>
    /// Dekorativer, sehr weicher Farbschein in der Akzentfarbe (oben rechts) hinter dem Inhalt, der langsam „atmet“.
    /// Liefert ein Grid, das <paramref name="content"/> umschließt.
    /// </summary>
    public static Grid WithAmbientGlow(UIElement content, Color accent)
    {
        var g = new Grid { ClipToBounds = true };
        byte a1 = (byte)(Theme.Dark ? 46 : 34);
        var brush = new RadialGradientBrush
        {
            GradientStops = { new GradientStop(Color.FromArgb(a1, accent.R, accent.G, accent.B), 0), new GradientStop(Color.FromArgb((byte)(a1 / 3), accent.R, accent.G, accent.B), 0.45), new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1) }
        };
        var glow = new Ellipse { Width = 900, Height = 620, Fill = brush, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -330, -300, 0) };
        var glow2 = new Ellipse
        {
            Width = 620, Height = 460, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(-260, 0, 0, -260),
            Fill = new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromArgb((byte)(a1 / 2), accent.R, accent.G, accent.B), 0), new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1) } }
        };
        g.Children.Add(glow); g.Children.Add(glow2); g.Children.Add(content);
        if (On)
        {
            glow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.65, TimeSpan.FromSeconds(4.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            glow2.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromSeconds(6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }
        return g;
    }
}
