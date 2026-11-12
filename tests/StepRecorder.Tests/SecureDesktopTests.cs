using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Sicherer Desktop (UAC): Zustandslogik und benannter Lückenschritt ohne Bild.</summary>
[Collection("Loc")]
public class SecureDesktopTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-uac-" + Guid.NewGuid().ToString("N")[..8]);
    public SecureDesktopTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void DesktopWatch_EntersImmediately_LeavesAfterTwoDefaultReads()
    {
        var w = new DesktopWatch();
        Assert.Equal(DesktopWatch.Change.None, w.Observe("Default", false));
        Assert.Equal(DesktopWatch.Change.Enter, w.Observe("inaccessible", true));
        Assert.Equal("uac", w.Kind);
        Assert.Equal(DesktopWatch.Change.None, w.Observe("Winlogon", true));
        Assert.Equal(DesktopWatch.Change.None, w.Observe("Default", false));   // eine Lesung reicht nicht
        Assert.Equal(DesktopWatch.Change.None, w.Observe("Winlogon", false));  // Flackern setzt zurück
        Assert.Equal(DesktopWatch.Change.None, w.Observe("Default", false));
        Assert.Equal(DesktopWatch.Change.Leave, w.Observe("Default", false));
        Assert.False(w.OnSecure);
        Assert.Equal(DesktopWatch.Change.Enter, w.Observe("Screen-saver", false));
        Assert.Equal("other", w.Kind);
        w.Reset();
        Assert.False(w.OnSecure);
    }

    [Fact]
    public void UacGap_IsNamedStep_WithoutImage()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"), withUac: true);
        using var v = SessionView.Open(a);
        var gap = v.All.Single(s => s.Kind == "gap" && s.Json.Obj("input")?.Str("cause") == "secureDesktop");
        Assert.Equal("Benutzerkontensteuerung bestätigen", gap.Title);
        Assert.Contains("Benutzerkontensteuerung", gap.Guide);
        Assert.Null(gap.PreFrame);
        Assert.Null(gap.PostFrame);
        Assert.Equal("primary", gap.Importance);
        Assert.Contains(v.GuideSteps(), s => s.Id == gap.Id);
        var (issues, _) = Validator.Validate(a);
        Assert.DoesNotContain(issues, i => i.Severity == "error");
        v.Language = "en"; v.Rebuild();
        Assert.Equal("Confirm User Account Control", v.All.Single(s => s.Id == gap.Id).Title);
    }
}
