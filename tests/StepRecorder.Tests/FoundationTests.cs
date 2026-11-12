using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Mcp;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.Tests;

/// <summary>Austausch der Einstellungen, Export-Unterbau, MCP-Einrichtung, Diagnose.</summary>
[Collection("Loc")]
public class FoundationTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-found-" + Guid.NewGuid().ToString("N")[..8]);
    public FoundationTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    // ---------------- Einstellungen austauschen ----------------

    [Fact]
    public void SettingsExport_ContainsNoPersonalData()
    {
        var s = new AppSettings { SessionsFolder = @"C:\Users\Kanarie\Aufnahmen" };
        s.RecentSessions.Add(@"C:\Users\Kanarie\Aufnahmen\2026");
        var json = SettingsExchange.Export(s).ToJsonString();
        Assert.DoesNotContain("Kanarie", json);
        Assert.DoesNotContain(Environment.UserName, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"hotkeys\"", json);
    }

    [Fact]
    public void SettingsImport_WeakeningNeedsIndividualConfirmation()
    {
        var current = new AppSettings { PrivacyLevel = "strict" };
        current.Record["typing"] = false;
        var file = SettingsExchange.Export(new AppSettings { PrivacyLevel = "balanced", Excluded = new() { "Notepad.exe" }, Theme = "light" });
        var plan = SettingsExchange.Plan(file.ToJsonString(), current);
        Assert.True(plan.Ok);
        Assert.Contains(plan.Weakening, w => w.WeakeningId == "privacyLevel");
        Assert.Contains(plan.Weakening, w => w.WeakeningId == "excluded");
        Assert.Contains(plan.Weakening, w => w.WeakeningId == "record:typing");
        // nichts bestätigt: Schutz bleibt, Unkritisches wird übernommen
        var r = SettingsExchange.Apply(plan, current, new HashSet<string>());
        Assert.Equal("strict", r.PrivacyLevel);
        Assert.Contains("KeePass.exe", r.Excluded);
        Assert.Contains("Notepad.exe", r.Excluded);
        Assert.False(r.Records("typing"));
        Assert.Equal("light", r.Theme);
        // einzeln bestätigt
        var r2 = SettingsExchange.Apply(plan, current, new HashSet<string> { "privacyLevel" });
        Assert.Equal("balanced", r2.PrivacyLevel);
        Assert.Contains("KeePass.exe", r2.Excluded);
    }

    [Theory]
    [InlineData("""{ "format": "falsch" }""")]
    [InlineData("""{ "format": "steprecorder.settings-exchange", "formatVersion": 2, "parts": {} }""")]
    [InlineData("kein json")]
    public void SettingsImport_InvalidFile_ChangesNothing(string json)
    {
        var plan = SettingsExchange.Plan(json, new AppSettings());
        Assert.False(plan.Ok);
        var cur = new AppSettings { Theme = "light" };
        Assert.Same(cur, SettingsExchange.Apply(plan, cur, new HashSet<string>()));
    }

    [Fact]
    public void SettingsImport_BadPartsAreSkipped()
    {
        var file = new JsonObject
        {
            ["format"] = SettingsExchange.Format, ["formatVersion"] = 1,
            ["parts"] = new JsonObject
            {
                ["hotkeys"] = new JsonObject { ["stop"] = "" },
                ["preferences"] = new JsonObject { ["theme"] = "neon" },
                ["exportTemplates"] = new JsonObject { ["templates"] = new JsonObject { ["Firma"] = new JsonObject { ["accent"] = "#12345", ["logo"] = "AAAA" } } },
                ["excluded"] = new JsonArray("Notepad.exe", "..\\böse")
            }
        };
        var cur = new AppSettings();
        var plan = SettingsExchange.Plan(file.ToJsonString(), cur);
        Assert.True(plan.Ok);
        Assert.Equal(4, plan.SkippedParts.Count);
        var r = SettingsExchange.Apply(plan, cur, new HashSet<string>());
        Assert.Equal("Ctrl+Shift+F10", r.Hotkey("stop"));
        Assert.Equal("dark", r.Theme);
        Assert.Empty(r.DocTemplates);
    }

    [Fact]
    public void SettingsImport_TemplateLogoIsReencoded()
    {
        using var bmp = new Bitmap(10, 10);
        using var ms = new MemoryStream(); bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        var tpl = new JsonObject { ["accent"] = "#112233", ["logo"] = Convert.ToBase64String(ms.ToArray()), ["fremd"] = "x", ["footer"] = new string('f', 900) };
        var clean = DocSettings.SanitizeTemplate(tpl, out var problem);
        Assert.Null(problem);
        Assert.Null(clean!["fremd"]);
        Assert.Equal(300, clean.Str("footer")!.Length);
        Assert.NotNull(clean.Str("logo"));
    }

    [Fact]
    public void SettingsCommit_And_Undo()
    {
        var path = Path.Combine(_root, "s.json");
        new AppSettings { Theme = "dark" }.Write(path);
        Assert.True(SettingsExchange.Commit(new AppSettings { Theme = "light" }, path));
        Assert.Equal("light", AppSettings.Read(path).Theme);
        Assert.True(SettingsExchange.CanUndo(path));
        Assert.True(SettingsExchange.UndoLastImport(path));
        Assert.Equal("dark", AppSettings.Read(path).Theme);
    }

    [Fact]
    public void SettingsReset_FromStrict_NeedsConfirmation()
    {
        var plan = SettingsExchange.PlanReset(new AppSettings { PrivacyLevel = "strict", Theme = "light" });
        Assert.Contains(plan.Weakening, w => w.WeakeningId == "privacyLevel");
        var r = SettingsExchange.Apply(plan, new AppSettings { PrivacyLevel = "strict", Theme = "light", SessionsFolder = "X" }, new HashSet<string>());
        Assert.Equal("strict", r.PrivacyLevel);
        Assert.Equal("dark", r.Theme);
        Assert.Equal("X", r.SessionsFolder);
    }

    // ---------------- Export-Unterbau ----------------

    [Fact]
    public void Exporter_CancelLeavesNothing()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "s1"));
        using var v = SessionView.Open(folder, headless: true);
        var target = Path.Combine(_root, "out", "a.html");
        using var cts = new CancellationTokenSource();
        int ticks = 0;
        var progress = new SyncProgress(_ => { if (++ticks == 2) cts.Cancel(); });
        using (ExportJob.Begin(progress, cts.Token))
            Assert.Throws<OperationCanceledException>(() => Exporter.Run(v, new ExportRequest { Format = "html", Target = target }));
        Assert.False(File.Exists(target));
        Assert.Empty(Directory.Exists(Path.Combine(folder, "export", ".tmp")) ? Directory.GetDirectories(Path.Combine(folder, "export", ".tmp"), "job-*") : Array.Empty<string>());
    }

    sealed class SyncProgress : IProgress<ExportProgress> { readonly Action<ExportProgress> _a; public SyncProgress(Action<ExportProgress> a) => _a = a; public void Report(ExportProgress v) => _a(v); }

    [Fact]
    public void Exporter_MarkdownAndImagesArriveTogether_AndForeignFilesSurvive()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "s2"));
        using var v = SessionView.Open(folder, headless: true);
        var outDir = Path.Combine(_root, "md");
        Directory.CreateDirectory(Path.Combine(outDir, "a-bilder"));
        File.WriteAllText(Path.Combine(outDir, "a-bilder", "fremd.png"), "x");
        var r = Exporter.Run(v, new ExportRequest { Format = "md", Target = Path.Combine(outDir, "a.md") });
        Assert.Equal(Path.Combine(outDir, "a.md"), r.Path);
        Assert.True(File.Exists(r.Path));
        Assert.True(Directory.GetFiles(Path.Combine(outDir, "a-bilder"), "schritt-*.png").Length > 0);
        Assert.True(File.Exists(Path.Combine(outDir, "a-bilder", "fremd.png")));
    }

    [Fact]
    public void Exporter_KiFolderTarget_ProtectsForeignFolder()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "s3"));
        using var v = SessionView.Open(folder, headless: true);
        var target = Path.Combine(_root, "fremd");
        Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "x.txt"), "x");
        Assert.Throws<ExportTargetException>(() => Exporter.Run(v, new ExportRequest { Format = "ki", Target = target }));
        Assert.True(File.Exists(Path.Combine(target, "x.txt")));
        var ok = Path.Combine(_root, "paket");
        var r = Exporter.Run(v, new ExportRequest { Format = "ki", Target = ok, Zip = true });
        Assert.True(File.Exists(Path.Combine(ok, "trace.md")));
        Assert.True(File.Exists(ok + ".zip"));
        Assert.Equal(ok + ".zip", r.Path);
    }

    [Fact]
    public void ExportRequest_RoundTrip()
    {
        var r = new ExportRequest { Format = "pdf", Target = @"C:\x\y.pdf", Importance = "all", Template = "checklist", MouseMarks = false, GifSeconds = 4 };
        var b = ExportRequest.FromJson(r.ToJson());
        Assert.Equal(r.ToJson().ToJsonString(), b.ToJson().ToJsonString());
    }

    [Fact]
    public void Exporter_DefaultTargets()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "s4"));
        using var v = SessionView.Open(folder);
        Assert.Equal(Path.Combine(folder, "export", "pdf", "Datei im Editor speichern.pdf"), Exporter.DefaultTarget(v, "pdf", null));
        Assert.Equal(Path.Combine(@"D:\Ziel", "Datei-im-Editor-speichern", "Datei-im-Editor-speichern.md"), Exporter.DefaultTarget(v, "md", @"D:\Ziel"));
        Assert.Equal(Path.Combine(@"D:\Ziel", "Datei im Editor speichern", "Datei im Editor speichern.md"), Exporter.DefaultTarget(v, "md", @"D:\Ziel", mdLegacy: true));
        Assert.EndsWith(" – Checkliste.pdf", Exporter.DefaultTarget(v, "pdf", @"D:\Ziel", "checkliste"));
    }

    // ---------------- MCP-Einrichtung ----------------

    [DllImport("shell32.dll", SetLastError = true)] static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string cmd, out int n);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);

    static string[] Split(string cmd)
    {
        var p = CommandLineToArgvW(cmd, out int n);
        try { return Enumerable.Range(0, n).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(p, i * IntPtr.Size))!).ToArray(); }
        finally { LocalFree(p); }
    }

    [Theory]
    [InlineData(@"C:\Program Files\Tools\SebastiansStepRecorder.exe", @"C:\Users\Jörg Müller\Aufnahmen")]
    [InlineData(@"\\server\share\SebastiansStepRecorder.exe", @"D:\")]
    [InlineData(@"C:\Tools\x.exe", @"C:\with trailing\")]
    public void McpConfig_ExactPaths(string exe, string root)
    {
        var o = new McpConfigOptions(exe, root, AllowEdit: true);
        var json = JsonNode.Parse(McpConfig.ProjectJson(o))!;
        var srv = json["mcpServers"]![McpConfig.ServerName]!;
        Assert.Equal(exe, srv["command"]!.GetValue<string>());
        var args = srv["args"]!.AsArray().Select(a => a!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { "mcp", "--root", root, "--allow-edit" }, args);
        var parsed = Split(McpConfig.CommandLine(o));
        Assert.Equal(new[] { exe }.Concat(args).ToArray(), parsed);
    }

    // ---------------- Diagnose ----------------

    [Fact]
    public void Diagnostics_ScrubsPersonalValues()
    {
        var map = new List<(string, string)> { (@"C:\Users\Kanarie", "<Profil>"), ("Kanarie", "<Benutzer>"), ("RECHNER42", "<Rechner>") };
        var text = Diagnostics.Scrub(@"Pfad C:\Users\Kanarie\x und C:\\Users\\Kanarie\\y auf RECHNER42 von kanarie", map);
        Assert.DoesNotContain("Kanarie", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RECHNER42", text);
        var s = new AppSettings { SessionsFolder = @"C:\Users\Kanarie\Geheim" };
        s.Excluded.Add("Geheimprogramm.exe");
        var js = Diagnostics.SanitizedSettings(s).ToJsonString();
        Assert.DoesNotContain("Geheim", js);
    }
}
