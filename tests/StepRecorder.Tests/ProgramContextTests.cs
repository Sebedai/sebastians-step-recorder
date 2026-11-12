using System.Text.Json.Nodes;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Mcp;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Paket 10 des Entwickler-Fahrplans: Programmende, Hänger, Build-Stand, Fakten der Ereignisanzeige.</summary>
public class ProgramContextTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-prog-" + Guid.NewGuid().ToString("N")[..8]);
    public ProgramContextTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Theory]
    [InlineData(0u, "normal")]
    [InlineData(1u, "error")]
    [InlineData(0xC0000005u, "crash")]
    [InlineData(0xC0000409u, "crash")]
    [InlineData(0xE0434352u, "crash")]
    [InlineData(0xC000013Au, "error")]   // Strg+C ist kein Absturz
    [InlineData(0x40010004u, "error")]
    public void ExitCodes_Classify(uint code, string expected) => Assert.Equal(expected, ExitCodes.Classify(code));

    [Fact]
    public void ExitCodes_NamesAndDisplay()
    {
        Assert.Equal("Zugriffsverletzung", ExitCodes.Name(0xC0000005, "de"));
        Assert.Equal("unhandled .NET exception", ExitCodes.Name(0xE0434352, "en"));
        Assert.Null(ExitCodes.Name(42, "de"));
        Assert.Equal("1", ExitCodes.Display(1));
        Assert.Equal("0xC0000005", ExitCodes.Display(0xC0000005));
    }

    [Fact]
    public void PeInfo_ReadsTimestampAndPdbIdentity()
    {
        var pe = PeInfo.Read(typeof(ProcessWatch).Assembly.Location)!;
        Assert.True(pe["peTimestampUtc"] != null || pe["peTimestamp"] != null);
        Assert.Matches("^[0-9A-F]{33,}$", pe.Str("pdbId")!);
        Assert.Equal("StepRecorder.Core.pdb", pe.Str("pdbFile"));                   // nur der Dateiname
        Assert.Null(PeInfo.Read(Path.Combine(_root, "fehlt.exe")));
        File.WriteAllText(Path.Combine(_root, "kein.exe"), "kein PE");
        Assert.Null(PeInfo.Read(Path.Combine(_root, "kein.exe")));
    }

    [Fact]
    public void GitInfo_BranchCommitPackedRefsWorktreeDetached()
    {
        string repo = Path.Combine(_root, "repo"), git = Path.Combine(repo, ".git"), bin = Path.Combine(repo, "src", "bin");
        Directory.CreateDirectory(Path.Combine(git, "refs", "heads", "feature")); Directory.CreateDirectory(bin);
        string sha = new string('a', 40), sha2 = new string('b', 40);
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/feature/x\n");
        File.WriteAllText(Path.Combine(git, "refs", "heads", "feature", "x"), sha + "\n");
        var g = GitInfo.Read(Path.Combine(bin, "app.exe"))!;
        Assert.Equal("feature/x", g.Str("branch")); Assert.Equal(sha, g.Str("commit")); Assert.Equal("workingTree", g.Str("basis")); Assert.Equal("notChecked", g.Str("dirty"));
        Assert.DoesNotContain(_root, g.ToJsonString());                              // nie Pfade

        File.Delete(Path.Combine(git, "refs", "heads", "feature", "x"));
        File.WriteAllText(Path.Combine(git, "packed-refs"), "# pack-refs\n" + sha2 + " refs/heads/feature/x\n");
        Assert.Equal(sha2, GitInfo.Read(Path.Combine(bin, "app.exe"))!.Str("commit"));

        // Worktree: .git-Datei mit gitdir und commondir
        string wt = Path.Combine(_root, "wt"), wtGit = Path.Combine(git, "worktrees", "wt");
        Directory.CreateDirectory(wt); Directory.CreateDirectory(wtGit);
        File.WriteAllText(Path.Combine(wt, ".git"), "gitdir: " + wtGit);
        File.WriteAllText(Path.Combine(wtGit, "HEAD"), "ref: refs/heads/feature/x");
        File.WriteAllText(Path.Combine(wtGit, "commondir"), "../..");
        Assert.Equal(sha2, GitInfo.Read(Path.Combine(wt, "app.exe"))!.Str("commit"));

        File.WriteAllText(Path.Combine(git, "HEAD"), sha);
        var d = GitInfo.Read(Path.Combine(bin, "app.exe"))!;
        Assert.True(d.Bool("detached")); Assert.Null(d["branch"]);
        Assert.Null(GitInfo.Read(Path.Combine(_root, "nichts", "app.exe")));
    }

    [Fact]
    public void EventLogFacts_NoPathsNoFreeText()
    {
        var props = new List<object?> { "notepad.exe", "11.0", "65a1b2c3", "C:\\Windows\\System32\\ntdll.dll", "10.0.1", "abc", "c0000005", "00000000000a1b2c", "0x1f40", "x", "C:\\Users\\Kanarie\\app\\notepad.exe" };
        var f = EventLogFacts.FromProperties(1000, props)!;
        Assert.Equal("ntdll.dll", f.Str("module")); Assert.Equal("0xC0000005", f.Str("exceptionCode")); Assert.Equal(8000, f.Long("pid"));
        Assert.DoesNotContain("Kanarie", f.ToJsonString()); Assert.DoesNotContain("System32", f.ToJsonString());
        var net = EventLogFacts.FromProperties(1026, new List<object?> { "Application: app.exe\nCoreCLR Version: 8\nDescription: The process was terminated due to an unhandled exception.\nException Info: System.NullReferenceException: Kanarie geheim\n   at A.B()" })!;
        Assert.Equal("System.NullReferenceException", net.Str("exceptionType"));
        Assert.DoesNotContain("geheim", net.ToJsonString());
    }

    [Fact]
    public void CrashAndHang_BecomeSteps_InTraceBugReportAndMcp()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "c"), withCrash: true);
        using var v = SessionView.Open(folder, headless: true);
        var exit = v.All.Single(s => s.Kind == "system" && s.Json.Obj("input")?.Str("event") == "appExit");
        Assert.Equal("primary", exit.Importance);
        Assert.Equal("„Editor“ stürzt ab", exit.Title);
        Assert.Contains("Zugriffsverletzung", exit.Protocol);
        Assert.Contains("ntdll.dll", exit.Protocol);                                 // Fakten der Ereignisanzeige am Schritt
        var hang = v.All.Single(s => s.Kind == "system" && s.Json.Obj("input")?.Str("event") == "appHang");
        Assert.Contains("6,0 s", hang.Protocol);
        Assert.DoesNotContain(v.All, s => s.Json.Obj("input")?.Str("event") == "appEventLog");   // dem Absturz zugeordnet

        var trace = KiPackage.Trace(v, v.All, new(), "de", "standard");
        Assert.Contains("## Programm-Kontext", trace);
        Assert.Contains("Git-Arbeitsstand (ungeprüft", trace);
        Assert.Contains("feature/save @ 0123456789ab", trace);

        var md = Path.Combine(_root, "bug", "b.md");
        GuideExport.Export(v, new GuideOptions { Format = "bugreport", OutPath = md, Importance = "secondary" });
        var bug = File.ReadAllText(md);
        Assert.Contains("stürzt ab", bug);
        Assert.Contains("PDB 0123456789ABCDEF0123456789ABCDEF1", bug);

        var mcp = new StepRecorderMcp(new McpOptions { Root = _root });
        var server = new McpStdioServer("steprecorder", "test", StepRecorderMcp.Instructions, TextReader.Null, TextWriter.Null, TextWriter.Null);
        mcp.Register(server);
        var r = server.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = "get_failure_context", ["arguments"] = new JsonObject { ["sessionId"] = v.Session.SessionId, ["image"] = false } } });
        var data = JsonNode.Parse(r!["result"]!["content"]![0]!["text"]!.GetValue<string>())!["data"]!;
        Assert.Equal("crash", data["focus"]!["reason"]!.GetValue<string>());
        Assert.Equal("0xC0000005", data["program"]!["exits"]![0]!["exitCode"]!.GetValue<string>());
        Assert.Equal("feature/save", data["apps"]![0]!["git"]!["branch"]!.GetValue<string>());
    }

    [Fact]
    public void RedactedRange_RemovesProgramContext()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "r"), withCrash: true);
        using (var v = SessionView.Open(folder))
        {
            var exit = v.All.Single(s => s.Json.Obj("input")?.Str("event") == "appExit");
            var e = v.Session.Events.Single(x => x.Str("type") == "process.exit");
            var el = v.Session.Events.Single(x => x.Str("type") == "eventlog.entry");
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "range", ["seqRanges"] = new JsonArray(new JsonArray(e.Long("seq"), el.Long("seq"))), ["images"] = true });
            Assert.True(v.SaveEdits("test"));
        }
        using var v2 = SessionView.Open(folder, headless: true);
        Assert.Null(DeveloperContext.Build(v2, 0, long.MaxValue, "de")?["exits"]);
        Assert.Null(DeveloperContext.Build(v2, 0, long.MaxValue, "de")?["eventLog"]);
    }
}
