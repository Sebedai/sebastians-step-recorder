using System.Drawing;
using StepRecorder.Native;
using StepRecorder.Settings;

namespace StepRecorder.Capture;

/// <summary>Wie gut ein gemerkter Bereich heute noch passt.</summary>
public enum RegionFit { Exact, Moved, Clamped, Lost }

/// <summary>
/// Letzter Aufnahmebereich (5.3): gespeichert relativ zu seinem Monitor; beim Wiederverwenden auf die heutigen Monitore
/// gebracht – gleicher Monitor an gleicher Stelle (exakt), Monitor verschoben (verschoben), teilweise außerhalb
/// (beschnitten) oder nicht mehr auffindbar (verloren).
/// </summary>
public static class RegionMemory
{
    public const int MinSize = 64;

    public static SavedRegion Save(Rectangle region, MonitorInfo monitor) => new()
    {
        X = region.X, Y = region.Y, W = region.Width, H = region.Height, Device = monitor.Device,
        MonX = monitor.Bounds.X, MonY = monitor.Bounds.Y, MonW = monitor.Bounds.Width, MonH = monitor.Bounds.Height, Dpi = monitor.Dpi
    };

    /// <summary>Monitor, auf dem der größte Teil des Bereichs liegt.</summary>
    public static MonitorInfo? MonitorOf(Rectangle r, IReadOnlyList<MonitorInfo> monitors)
        => monitors.Select(m => (m, a: Area(Rectangle.Intersect(m.Bounds, r)))).Where(x => x.a > 0).OrderByDescending(x => x.a).Select(x => x.m).FirstOrDefault();

    static long Area(Rectangle r) => r.Width <= 0 || r.Height <= 0 ? 0 : (long)r.Width * r.Height;

    public static (Rectangle? rect, RegionFit fit) Resolve(SavedRegion? s, IReadOnlyList<MonitorInfo> monitors)
    {
        if (s == null || s.W < MinSize || s.H < MinSize || monitors.Count == 0) return (null, RegionFit.Lost);
        var saved = new Rectangle(s.X, s.Y, s.W, s.H);
        // derselbe Monitor (Gerätename und Größe)?
        var same = monitors.FirstOrDefault(m => m.Device == s.Device && m.Bounds.Width == s.MonW && m.Bounds.Height == s.MonH)
                   ?? monitors.FirstOrDefault(m => m.Device == s.Device);
        if (same != null)
        {
            var r = new Rectangle(same.Bounds.X + (s.X - s.MonX), same.Bounds.Y + (s.Y - s.MonY), s.W, s.H);
            bool moved = same.Bounds.X != s.MonX || same.Bounds.Y != s.MonY;
            var cut = Rectangle.Intersect(r, same.Bounds);
            if (cut == r) return (r, moved ? RegionFit.Moved : RegionFit.Exact);
            return cut.Width >= MinSize && cut.Height >= MinSize ? (cut, RegionFit.Clamped) : (null, RegionFit.Lost);
        }
        // Monitor fehlt: liegt der Bereich trotzdem auf einem heutigen Monitor?
        if (MonitorOf(saved, monitors) is { } other)
        {
            var cut = Rectangle.Intersect(saved, other.Bounds);
            if (cut.Width >= MinSize && cut.Height >= MinSize) return (cut, cut == saved ? RegionFit.Moved : RegionFit.Clamped);
        }
        return (null, RegionFit.Lost);
    }
}
