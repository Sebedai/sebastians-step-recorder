using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Native;

namespace StepRecorder.Capture;

/// <summary>Monitoranordnung einer Epoche; rechnet virtuelle Punkte in monitorrelative um.</summary>
public sealed class Topology
{
    public int Epoch { get; }
    public List<MonitorInfo> Monitors { get; }
    public Rectangle Virtual { get; }

    public Topology(int epoch, List<MonitorInfo> monitors)
    {
        Epoch = epoch; Monitors = monitors; Virtual = ScreenGrabber.VirtualScreen(monitors);
    }

    public static Topology Read(int epoch) => new(epoch, ScreenGrabber.GetMonitors());

    public string IdOf(MonitorInfo m) => "M" + (m.Index + 1);

    public MonitorInfo? ById(string? id) => Monitors.FirstOrDefault(m => IdOf(m) == id);

    public MonitorInfo MonitorAt(int vx, int vy)
    {
        foreach (var m in Monitors) if (m.Bounds.Contains(vx, vy)) return m;
        // nächster Monitor
        return Monitors.OrderBy(m => Dist(m.Bounds, vx, vy)).First();
    }

    static double Dist(Rectangle r, int x, int y)
    {
        int dx = Math.Max(Math.Max(r.Left - x, 0), x - (r.Right - 1));
        int dy = Math.Max(Math.Max(r.Top - y, 0), y - (r.Bottom - 1));
        return dx * dx + dy * dy;
    }

    /// <summary>(Monitor-Id, x, y) monitorrelativ; Punkt wird auf den virtuellen Desktop begrenzt.</summary>
    public (string m, int x, int y) ToLocal(int vx, int vy)
    {
        var mon = MonitorAt(vx, vy);
        int x = Math.Clamp(vx - mon.Bounds.X, 0, mon.Bounds.Width - 1);
        int y = Math.Clamp(vy - mon.Bounds.Y, 0, mon.Bounds.Height - 1);
        return (IdOf(mon), x, y);
    }

    /// <summary>Rechteck relativ zu dem Monitor mit dem größten sichtbaren Anteil.</summary>
    public JsonObject RectJson(Rectangle r)
    {
        MonitorInfo best = Monitors[0]; long bestArea = -1;
        foreach (var m in Monitors)
        {
            var i = Rectangle.Intersect(m.Bounds, r);
            long a = (long)i.Width * i.Height;
            if (a > bestArea) { bestArea = a; best = m; }
        }
        if (bestArea <= 0) best = MonitorAt(r.X + r.Width / 2, r.Y + r.Height / 2);
        return new JsonObject { ["m"] = IdOf(best), ["x"] = r.X - best.Bounds.X, ["y"] = r.Y - best.Bounds.Y, ["w"] = r.Width, ["h"] = r.Height };
    }

    public JsonObject ToJson(string reason)
    {
        var mons = new JsonArray();
        foreach (var m in Monitors)
            mons.Add(new JsonObject
            {
                ["id"] = IdOf(m), ["name"] = m.Device, ["primary"] = m.Primary, ["vx"] = m.Bounds.X, ["vy"] = m.Bounds.Y,
                ["w"] = m.Bounds.Width, ["h"] = m.Bounds.Height,
                ["work"] = new JsonObject { ["x"] = m.WorkArea.X - m.Bounds.X, ["y"] = m.WorkArea.Y - m.Bounds.Y, ["w"] = m.WorkArea.Width, ["h"] = m.WorkArea.Height },
                ["dpi"] = m.Dpi, ["scale"] = Math.Round(m.Scale, 3), ["rotation"] = 0, ["backend"] = "gdi"
            });
        return new JsonObject
        {
            ["epoch"] = Epoch, ["reason"] = reason,
            ["virtual"] = new JsonObject { ["x"] = Virtual.X, ["y"] = Virtual.Y, ["w"] = Virtual.Width, ["h"] = Virtual.Height },
            ["monitors"] = mons
        };
    }

    public bool SameAs(Topology o)
        => o.Monitors.Count == Monitors.Count && o.Monitors.Zip(Monitors).All(p => p.First.Bounds == p.Second.Bounds && p.First.Dpi == p.Second.Dpi);
}
