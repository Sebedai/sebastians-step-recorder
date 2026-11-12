using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StepRecorder.App.Ui;

/// <summary>
/// Zurückhaltende Animationen wie in Windows: kurzes Einblenden eines Fensterinhalts und weiches Überblenden
/// nach einem Inhaltswechsel. Abgeschaltet, wenn Windows keine Animationen zeigen soll (Barrierefreiheit) oder
/// STEPRECORDER_NOANIM=1 gesetzt ist.
/// </summary>
static class Anim
{
    public static bool On => SystemParameters.ClientAreaAnimation && Environment.GetEnvironmentVariable("STEPRECORDER_NOANIM") != "1";

    static readonly IEasingFunction Out = new CubicEase { EasingMode = EasingMode.EaseOut };

    static DoubleAnimation A(double? from, double to, int ms, int delay = 0) => new()
    {
        From = from, To = to, Duration = TimeSpan.FromMilliseconds(ms), BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = Out
    };

    /// <summary>Einblenden mit kleinem Versatz nach oben (dy &gt; 0) bzw. unten; danach ist das Element wieder ganz normal.</summary>
    public static void Appear(UIElement e, int delayMs = 0, double dy = 4, int ms = 200)
    {
        if (!On) return;
        var tt = new TranslateTransform(0, dy);
        var old = e.RenderTransform;
        e.RenderTransform = old is null || old == Transform.Identity ? tt : new TransformGroup { Children = { old, tt } };
        e.Opacity = 0;
        var fade = A(0, 1, ms, delayMs);
        fade.FillBehavior = FillBehavior.Stop;
        fade.Completed += (_, _) => { e.Opacity = 1; e.RenderTransform = old ?? Transform.Identity; };
        var move = A(dy, 0, ms + 60, delayMs);
        tt.BeginAnimation(TranslateTransform.YProperty, move);
        e.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>Weiches Überblenden nach einem Inhaltswechsel (z. B. anderer Schritt).</summary>
    public static void Refresh(UIElement e, double from = 0.25, int ms = 220)
    {
        if (!On) return;
        var a = A(from, 1, ms);
        a.FillBehavior = FillBehavior.Stop;
        e.BeginAnimation(UIElement.OpacityProperty, a);
    }
}
