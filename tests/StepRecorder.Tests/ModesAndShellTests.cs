using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.Tests;

/// <summary>Paket 14 des Entwickler-Fahrplans: Modusliste, Modus „Fehler nachstellen“, Kurzbefehl für Konsolen.</summary>
public class ModesAndShellTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-modes-" + Guid.NewGuid().ToString("N")[..8]);
    public ModesAndShellTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Theory]
    [InlineData("guide", "guide")]
    [InlineData("Anleitung", "guide")]
    [InlineData("ki-spur", "aiTrace")]
    [InlineData("Support-Fall", "support")]
    [InlineData("fehler-nachstellen", "bugRepro")]
    [InlineData("BugRepro", "bugRepro")]
    [InlineData("Reproduce bug", "bugRepro")]
    [InlineData("eigene", "custom")]
    public void Modes_FindByIdNameOrAlias(string input, string id) => Assert.Equal(id, RecordingModes.Find(input)!.Id);

    [Fact]
    public void Modes_OneListForAllUses()
    {
        Assert.Null(RecordingModes.Find("unbekannt"));
        Assert.Equal(5, RecordingModes.All.Length);
        Assert.All(RecordingModes.All.Where(m => m.Id != "custom"), m => Assert.NotNull(DetailPresets.For(m.Id)));
        var o = new RecordingOptions { ProfileId = "bugRepro" };
        var p = o.ProfileJson();
        Assert.True(p["builtIn"]!.GetValue<bool>());
        Assert.True(p["effective"]!["showGoalFields"]!.GetValue<bool>());
        Assert.True(DetailPresets.For("bugRepro")!.AppContext);
        Assert.Equal("viewerError", RecordingModes.Find("bugRepro")!.AfterStop);
        Assert.Contains("fehler-nachstellen", RecordingModes.CliNames("de"));
    }

    [Fact]
    public void ConsoleShim_WaitsAndPassesExitCode()
    {
        var path = ConsoleShim.Write(_root, "SebastiansStepRecorder.exe");
        var text = File.ReadAllText(path);
        Assert.Equal(Path.Combine(_root, "steprec.cmd"), path);
        Assert.Contains("start \"\" /wait /b \"%~dp0SebastiansStepRecorder.exe\" %*", text);
        Assert.Contains("exit /b %ERRORLEVEL%", text);
        Assert.Contains("\r\n", text);                                                // Stapeldateien mit CRLF
        Assert.False(File.Exists(path + ".tmp"));
    }
}
