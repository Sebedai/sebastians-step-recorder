namespace StepRecorder.Tests;

/// <summary>
/// Nur für die Entwicklung: legt mit STEPRECORDER_DEMO_SESSION=&lt;Ordner&gt; eine synthetische Aufnahme an,
/// die man dann für Oberflächen-Schnappschüsse öffnen kann. Ohne die Variable tut der Test nichts.
/// </summary>
public class DevDemoSession
{
    [Fact]
    public void CreateDemoSessionIfRequested()
    {
        var target = Environment.GetEnvironmentVariable("STEPRECORDER_DEMO_SESSION");
        if (string.IsNullOrWhiteSpace(target)) return;
        if (Directory.Exists(target)) Directory.Delete(target, true);
        SyntheticSession.Create(target, withComment: true, withTerminal: true);
        Assert.True(File.Exists(Path.Combine(target, "manifest.json")));
    }
}
