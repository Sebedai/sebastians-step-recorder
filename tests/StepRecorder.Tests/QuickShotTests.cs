using System.Drawing;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.Tests;

/// <summary>Paket 15 des Entwickler-Fahrplans: Sofort-Bildschirmfoto und Tastenkürzel.</summary>
public class QuickShotTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-shot-" + Guid.NewGuid().ToString("N")[..8]);
    public QuickShotTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void QuickShot_IsSessionWithOneImageStep_Exportable()
    {
        using var bmp = new Bitmap(320, 200);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.CornflowerBlue);
        var folder = QuickShot.Create(_root, bmp, "de");
        Assert.True(Session.LooksLikeSession(folder));
        var (issues, _) = Validator.Validate(folder);
        Assert.DoesNotContain(issues, i => i.Severity == "error");
        using var v = SessionView.Open(folder);
        var step = Assert.Single(v.Manual);
        Assert.Equal("Bildschirmfoto", step.Title);
        Assert.StartsWith("u:", step.PreFrame);
        Assert.Contains(SessionCatalog.Scan(_root), s => s.Path == folder && s.Error == null);
        var r = Exporter.Run(v, new ExportRequest { Format = "md", Target = Path.Combine(_root, "out") + "\\" });
        Assert.True(File.Exists(r.Path));
        Assert.Contains("Bildschirmfoto", File.ReadAllText(r.Path!));
    }

    [Fact]
    public void ScreenshotHotkey_DefaultIsFreeAndValid()
    {
        var a = HotkeyActions.ById("screenshot")!;
        Assert.Equal("Ctrl+Shift+F3", a.Default);
        Assert.Equal(HotkeyActions.All.Length, HotkeyActions.All.Select(x => x.Default).Distinct().Count());   // keine Doppelbelegung
        Assert.Empty(HotkeyRules.Validate(HotkeyActions.Defaults()));
    }
}
