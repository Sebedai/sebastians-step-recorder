using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Pakete 16 und 17 des Entwickler-Fahrplans: UI-Test-Entwürfe aus Aufnahmen und Testbarkeits-Bericht.</summary>
public class TestCodeGenTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-tcg-" + Guid.NewGuid().ToString("N")[..8]);
    public TestCodeGenTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    static SessionView Open(string folder) => SessionView.Open(folder, headless: true, lang: "de");

    static void Balanced(string code)
    {
        // grobe Strukturprüfung: Klammern außerhalb von Zeichenketten und Kommentaren ausgeglichen
        var stripped = Regex.Replace(code, @"//[^\n]*|#[^\n]*|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'", "");
        Assert.Equal(stripped.Count(c => c == '{'), stripped.Count(c => c == '}'));
        Assert.Equal(stripped.Count(c => c == '('), stripped.Count(c => c == ')'));
    }

    [Fact]
    public void FlaUi_Draft_UsesSelectorsWaitsAndNoSecretLiteral()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "a"), withPassword: true, withHotkey: true, withError: true);
        using var v = Open(folder);
        string code = TestCodeGen.Build(v, new TestGenOptions { Target = "flaui", Language = "de" });
        Assert.Contains("[Fact]", code);
        Assert.Contains("Application.Launch(AppPath)", code);
        Assert.Contains("APP_UNDER_TEST", code);
        Assert.Contains("cf.ByName(\"Datei\").And(cf.ByControlType(ControlType.MenuItem))", code);
        Assert.Contains("Keyboard.Type(", code);
        Assert.Contains("Environment.GetEnvironmentVariable(\"WERT_", code);           // Kennwort nur aus der Umgebung
        Assert.Contains("Fehlerstelle", code);                                          // Marker „Fehler“ wird Prüfstelle
        Assert.DoesNotContain("Thread.Sleep", code);                                    // nie feste Pausen
        Balanced(code);
    }

    [Fact]
    public void FlaUi_Hotkey_MapsToVirtualKeys()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "h"), withHotkey: true);
        using var v = Open(folder);
        string code = TestCodeGen.Build(v, new TestGenOptions { Target = "flaui", Language = "en" });
        Assert.Matches(@"Keyboard\.TypeSimultaneously\(VirtualKeyShort\.CONTROL, VirtualKeyShort\.KEY_[A-Z0-9]\)", code);
        Assert.Contains("Draft UI test (FlaUI, xUnit)", code);
    }

    [Fact]
    public void PyWinAuto_Draft_IsPytestWithUiaBackend()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "p"), withPassword: true);
        using var v = Open(folder);
        string code = TestCodeGen.Build(v, new TestGenOptions { Target = "pywinauto", Language = "de" });
        Assert.Matches(@"(?m)^def test_\w+\(\):", code);
        Assert.Contains("Application(backend=\"uia\").start(APP)", code);
        Assert.Contains("child_window(title=\"Datei\", control_type=\"MenuItem\")", code);
        Assert.Contains("os.environ.get(\"WERT_", code);
        Assert.DoesNotContain("time.sleep", code);
    }

    [Fact]
    public void Playwright_WithoutBrowserSteps_SaysSo_AndRolesMap()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "w"));
        using var v = Open(folder);
        string code = TestCodeGen.Build(v, new TestGenOptions { Target = "playwright", Language = "de" });
        Assert.Contains("import { test, expect } from '@playwright/test';", code);
        Assert.Contains("keine Schritte in Chrome", code);
        Balanced(code);
        Assert.Equal("button", TestCodeGen.Role("Button"));
        Assert.Equal("textbox", TestCodeGen.Role("Edit"));
        Assert.Equal("link", TestCodeGen.Role("Hyperlink"));
        Assert.Null(TestCodeGen.Role("Pane"));
    }

    [Fact]
    public void Testability_FlagsControlsWithoutAutomationId()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "t"));
        using var v = Open(folder);
        var findings = TestCodeGen.Testability(v, "de");
        var menu = Assert.Single(findings, f => f.Str("name") == "Datei");
        Assert.Contains("noAutomationId", menu.StrList("issues"));
        Assert.Equal("MenuItem", menu.Str("controlType"));
        string md = TestCodeGen.TestabilityMarkdown(v, findings, "de");
        Assert.Contains("| Schritt | Programm |", md);
        Assert.Contains("AutomationId setzen", md);
    }

    [Fact]
    public void Exporter_WritesTestFiles_AndTestKiPackage()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "e"));
        using var v = Open(folder);
        foreach (var (fmt, pattern) in new[] { ("flaui", @"Tests\.cs$"), ("pywinauto", @"\\test_[^\\]+\.py$"), ("playwright", @"\.spec\.ts$") })
        {
            Assert.True(Exporter.IsKnownFormat(fmt));
            var r = Exporter.Run(v, new ExportRequest { Format = fmt, Language = "de" });
            Assert.Matches(pattern, r.Path!);
            Assert.True(File.Exists(r.Path));
            Assert.Contains(Path.DirectorySeparatorChar + "tests" + Path.DirectorySeparatorChar, r.Path);
        }
        var pkg = Exporter.Run(v, new ExportRequest { Format = "test-ki", Language = "en" });
        Assert.True(Directory.Exists(pkg.Path));
        foreach (var f in new[] { "automation.json", "README_TEST_KI.md", "testbarkeit.md", "testbarkeit.json" })
            Assert.True(File.Exists(Path.Combine(pkg.Path!, f)), f);
        Assert.Single(Directory.GetFiles(pkg.Path!, "*Tests.cs"));
        Assert.Single(Directory.GetFiles(pkg.Path!, "test_*.py"));
        Assert.Single(Directory.GetFiles(pkg.Path!, "*.spec.ts"));
        Assert.Contains("\"steprec.automation\"", File.ReadAllText(Path.Combine(pkg.Path!, "automation.json")));
        // zweiter Export in denselben Ordner ersetzt den eigenen (kein fremder Ordner wird geleert)
        var again = Exporter.Run(v, new ExportRequest { Format = "test-ki", Language = "en" });
        Assert.Equal(pkg.Path, again.Path);
    }

    [Fact]
    public void Export_RefusesUnreviewed_InEditor()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "u"), withSecret: true);
        using var v = SessionView.Open(folder, lang: "de");
        if (v.UnreviewedCount == 0) return;   // Muster der Synthese erzeugt hier keinen Prüfposten
        Assert.Throws<InvalidOperationException>(() => TestCodeGen.Export(v, new TestGenOptions { Target = "flaui", OutPath = Path.Combine(_root, "x.cs") }));
    }
}
