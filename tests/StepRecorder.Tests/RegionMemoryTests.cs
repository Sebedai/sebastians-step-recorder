using System.Drawing;
using StepRecorder.Capture;
using StepRecorder.Native;

namespace StepRecorder.Tests;

/// <summary>Letzter Aufnahmebereich: exakt, verschoben, beschnitten, verloren.</summary>
public class RegionMemoryTests
{
    static MonitorInfo Mon(string dev, int x, int y, int w, int h, bool primary = false) => new(0, dev, new Rectangle(x, y, w, h), new Rectangle(x, y, w, h - 40), 96, primary);

    [Fact]
    public void SameMonitor_IsExact()
    {
        var m = Mon(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var s = RegionMemory.Save(new Rectangle(100, 200, 800, 600), m);
        var (r, fit) = RegionMemory.Resolve(s, new[] { m });
        Assert.Equal(RegionFit.Exact, fit);
        Assert.Equal(new Rectangle(100, 200, 800, 600), r);
    }

    [Fact]
    public void MonitorMoved_KeepsRelativePosition()
    {
        var before = Mon(@"\\.\DISPLAY2", 1920, 0, 1920, 1080);
        var s = RegionMemory.Save(new Rectangle(2020, 100, 400, 300), before);
        var after = Mon(@"\\.\DISPLAY2", -1920, 0, 1920, 1080);
        var (r, fit) = RegionMemory.Resolve(s, new[] { Mon(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true), after });
        Assert.Equal(RegionFit.Moved, fit);
        Assert.Equal(new Rectangle(-1820, 100, 400, 300), r);
    }

    [Fact]
    public void SmallerMonitor_ClampsOrLoses()
    {
        var big = Mon(@"\\.\DISPLAY1", 0, 0, 2560, 1440, true);
        var s = RegionMemory.Save(new Rectangle(1700, 900, 800, 400), big);
        var small = Mon(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true);
        var (r, fit) = RegionMemory.Resolve(s, new[] { small });
        Assert.Equal(RegionFit.Clamped, fit);
        Assert.Equal(Rectangle.FromLTRB(1700, 900, 1920, 1080), r);
        var corner = RegionMemory.Save(new Rectangle(2400, 1300, 150, 120), big);
        Assert.Equal(RegionFit.Lost, RegionMemory.Resolve(corner, new[] { small }).fit);
    }

    [Fact]
    public void MissingMonitor_UsesOverlapOrIsLost()
    {
        var gone = Mon(@"\\.\DISPLAY3", 3840, 0, 1920, 1080);
        var s = RegionMemory.Save(new Rectangle(4000, 100, 400, 300), gone);
        Assert.Equal(RegionFit.Lost, RegionMemory.Resolve(s, new[] { Mon(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true) }).fit);
        Assert.Equal(RegionFit.Lost, RegionMemory.Resolve(null, new[] { gone }).fit);
        // anderer Gerätename, aber der Bereich liegt auf einem heutigen Monitor
        var (r, fit) = RegionMemory.Resolve(s, new[] { Mon(@"\\.\DISPLAY9", 3840, 0, 1920, 1080) });
        Assert.Equal(RegionFit.Moved, fit);
        Assert.Equal(new Rectangle(4000, 100, 400, 300), r);
    }
}
