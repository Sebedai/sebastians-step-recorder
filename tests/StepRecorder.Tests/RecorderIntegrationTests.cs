using StepRecorder.Capture;
using StepRecorder.Data;

namespace StepRecorder.Tests;

/// <summary>Echte Kurzaufnahme im Testprozess (Hooks im eigenen Prozess). Erzeugt nur harmlose Mausbewegungen.</summary>
public class RecorderIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void ShortRecording_ProducesValidSession()
    {
        string outDir = Path.Combine(Path.GetTempPath(), "steprec-int-" + Guid.NewGuid().ToString("N")[..8]);
        var rec = new Recorder(new RecordingOptions { Title = "Integration", OutputFolder = outDir, Monitors = "all" });
        string folder = rec.Start();
        Thread.Sleep(800);
        Native.Win32.GetCursorPos(out var p);
        for (int i = 0; i < 10; i++) { SetCursorPos(p.X + i * 3, p.Y + i * 2); Thread.Sleep(20); }
        SetCursorPos(p.X, p.Y);
        Thread.Sleep(800);
        rec.Stop();
        rec.Dispose();
        var (issues, report) = Validator.Validate(folder, thorough: true);
        try
        {
            Assert.True(File.Exists(Path.Combine(folder, "events.jsonl")));
            Assert.Empty(issues.Where(i => i.Severity == "error").Select(i => i.Code + " " + i.Message));
            using var s = Session.Open(folder);
            Assert.Equal("complete", s.State);
            Assert.Contains(s.Events, e => e.Str("type") == "frame" && e.Obj("store").Str("kind") == "key");
            Assert.Contains(s.Events, e => e.Str("type") == "session.stop");
        }
        finally { try { Directory.Delete(outDir, true); } catch { } }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
}
