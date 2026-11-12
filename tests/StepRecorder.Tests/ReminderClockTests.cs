using StepRecorder.Capture;

namespace StepRecorder.Tests;

/// <summary>Erinnerungen während der Aufnahme: Leerlauf und Privattaste.</summary>
public class ReminderClockTests
{
    const long Min = 60_000_000L, Sec = 1_000_000L;

    [Fact]
    public void Idle_FiresEveryInterval_AtMostThreeTimes()
    {
        var c = new ReminderClock(10, 60);
        Assert.Null(c.Poll(9 * Min));
        Assert.Equal((ReminderKind.StillRecording, 10), c.Poll(10 * Min));
        Assert.Null(c.Poll(10 * Min + Sec));
        Assert.Equal((ReminderKind.StillRecording, 20), c.Poll(20 * Min));
        Assert.Equal((ReminderKind.StillRecording, 30), c.Poll(30 * Min));
        Assert.Null(c.Poll(40 * Min));
        Assert.Null(c.Poll(500 * Min));
    }

    [Fact]
    public void Activity_RestartsIdlePhase()
    {
        var c = new ReminderClock(5, 60);
        Assert.NotNull(c.Poll(5 * Min));
        c.Activity(6 * Min);
        Assert.Null(c.Poll(10 * Min));
        Assert.Equal((ReminderKind.StillRecording, 5), c.Poll(11 * Min));
    }

    [Fact]
    public void Zero_DisablesReminder()
    {
        var c = new ReminderClock(0, 0);
        c.Privacy(true, 0);
        Assert.Null(c.Poll(1000 * Min));
    }

    [Fact]
    public void Privacy_FiresOncePerSwitchOn_AfterQuietKeys()
    {
        var c = new ReminderClock(0, 60);
        c.Privacy(true, 10 * Sec);
        c.Key(50 * Sec);
        Assert.Null(c.Poll(100 * Sec));
        Assert.Equal((ReminderKind.PrivacyStillOn, 60), c.Poll(110 * Sec));
        Assert.Null(c.Poll(500 * Sec));
        c.Privacy(false, 600 * Sec);
        Assert.Null(c.Poll(1000 * Sec));
        c.Privacy(true, 1000 * Sec);
        Assert.NotNull(c.Poll(1060 * Sec));
    }

    [Fact]
    public void Suspend_StopsClocks_AndRestartsAfterwards()
    {
        var c = new ReminderClock(10, 60);
        c.Suspend(true, 2 * Min);
        Assert.Null(c.Poll(30 * Min));
        c.Suspend(false, 30 * Min);
        Assert.Null(c.Poll(39 * Min));
        Assert.NotNull(c.Poll(40 * Min));
    }
}
