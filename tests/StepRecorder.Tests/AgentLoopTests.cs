using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Mcp;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Paket 8 des Entwickler-Fahrplans: Agenten-Schleife (MCP) und Kommandozeile für Skripte.</summary>
public class AgentLoopTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-agent-" + Guid.NewGuid().ToString("N")[..8]);
    public AgentLoopTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    (McpStdioServer server, string id) Server(bool allowEdit = false, bool withError = true, bool withSecret = false)
    {
        SyntheticSession.Create(Path.Combine(_root, "s" + Guid.NewGuid().ToString("N")[..6]), withError: withError, withSecret: withSecret);
        var mcp = new StepRecorderMcp(new McpOptions { Root = _root, AllowEdit = allowEdit });
        var server = new McpStdioServer("steprecorder", "test", StepRecorderMcp.Instructions, TextReader.Null, TextWriter.Null, TextWriter.Null);
        mcp.Register(server);
        server.Handle(Rpc("initialize", new JsonObject { ["protocolVersion"] = "2025-06-18" }));
        var id = Call(server, "list_sessions", new JsonObject())["data"]!["sessions"]![0]!["id"]!.GetValue<string>();
        return (server, id);
    }

    static JsonObject Rpc(string method, JsonObject prm) => new() { ["jsonrpc"] = "2.0", ["id"] = 7, ["method"] = method, ["params"] = prm };

    static JsonObject Call(McpStdioServer s, string tool, JsonObject args) => CallRaw(s, tool, args).data;

    static (JsonObject data, JsonArray content) CallRaw(McpStdioServer s, string tool, JsonObject args)
    {
        var r = s.Handle(Rpc("tools/call", new JsonObject { ["name"] = tool, ["arguments"] = args }));
        var content = r!["result"]!["content"]!.AsArray();
        var text = content[0]!["text"]!.GetValue<string>();
        Assert.False(r["result"]!["isError"]!.GetValue<bool>(), text);
        return (JsonNode.Parse(text)!.AsObject(), content);
    }

    [Fact]
    public void FailureContext_FocusesErrorMarker_WithAutomationAndImage()
    {
        var (server, id) = Server();
        var (d, content) = CallRaw(server, "get_failure_context", new JsonObject { ["sessionId"] = id });
        var data = d["data"]!.AsObject();
        Assert.Equal("firstErrorMarker", data["focus"]!["reason"]!.GetValue<string>());
        Assert.Equal("error", data["focus"]!["markerKind"]!.GetValue<string>());
        var steps = data["steps"]!.AsArray();
        Assert.True(steps.Count >= 2);
        Assert.Contains(steps, s => s!["selectors"] != null);                 // Automationsfelder enthalten
        Assert.Contains(data["markers"]!.AsArray(), m => m!["kind"]!.GetValue<string>() == "expected");
        Assert.Contains(content, c => c!["type"]!.GetValue<string>() == "image");
    }

    [Fact]
    public void FailureContext_WithoutErrorMarker_UsesLastStep()
    {
        var (server, id) = Server(withError: false);
        var data = Call(server, "get_failure_context", new JsonObject { ["sessionId"] = id, ["image"] = false })["data"]!;
        Assert.Equal("lastStep", data["focus"]!["reason"]!.GetValue<string>());
        Assert.NotNull(data["hints"]);
    }

    [Fact]
    public void FailureContext_UnreviewedSecret_IsNeverReturned()
    {
        var (server, id) = Server(withSecret: true);
        var (d, _) = CallRaw(server, "get_failure_context", new JsonObject { ["sessionId"] = id, ["image"] = false, ["before"] = 40 });
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz", d.ToJsonString());
    }

    [Fact]
    public void GetSteps_Include_AddsAutomationFields()
    {
        var (server, id) = Server();
        var plain = Call(server, "get_steps", new JsonObject { ["sessionId"] = id })["data"]!["items"]!.AsArray();
        Assert.DoesNotContain(plain, s => s!["selectors"] != null);
        var rich = Call(server, "get_steps", new JsonObject { ["sessionId"] = id, ["include"] = new JsonArray("selectors", "sync", "timing") })["data"]!["items"]!.AsArray();
        Assert.Contains(rich, s => s!["selectors"] != null);
        Assert.Contains(rich, s => s!["timing"] != null);
    }

    [Fact]
    public void Prompts_ListAndGet_ContainOnlyInstructions()
    {
        var (server, id) = Server();
        var list = server.Handle(Rpc("prompts/list", new JsonObject()))!["result"]!["prompts"]!.AsArray();
        Assert.Equal(new[] { "analyze_bug", "verify_fix", "write_guide", "write_ui_test" }, list.Select(p => p!["name"]!.GetValue<string>()).OrderBy(x => x));
        var init = server.Handle(Rpc("initialize", new JsonObject { ["protocolVersion"] = "2025-06-18" }))!["result"]!["capabilities"]!;
        Assert.NotNull(init["prompts"]);
        var msg = server.Handle(Rpc("prompts/get", new JsonObject { ["name"] = "analyze_bug", ["arguments"] = new JsonObject { ["sessionId"] = id } }))!["result"]!["messages"]![0]!["content"]!["text"]!.GetValue<string>();
        Assert.Contains("get_failure_context", msg);
        Assert.Contains(id, msg);
        Assert.DoesNotContain("notiz.txt", msg);                                 // keine aufgezeichneten Inhalte
        var bad = server.Handle(Rpc("prompts/get", new JsonObject { ["name"] = "analyze_bug", ["arguments"] = new JsonObject { ["sessionId"] = "../../etc" } }));
        Assert.NotNull(bad!["error"]);
        var missing = server.Handle(Rpc("prompts/get", new JsonObject { ["name"] = "verify_fix", ["arguments"] = new JsonObject() }));
        Assert.NotNull(missing!["error"]);
    }

    [Fact]
    public void AgentNote_OnlyWithAllowEdit_VisibleInMcp_NotInOutputs()
    {
        var (ro, _) = Server();
        var tools = ro.Handle(Rpc("tools/list", new JsonObject()))!["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToList();
        Assert.DoesNotContain("add_step_note", tools);

        var (server, id) = Server(allowEdit: true);
        var r = Call(server, "add_step_note", new JsonObject { ["sessionId"] = id, ["step"] = 1, ["text"] = "Agentenbefund-K9 Ursache vermutlich im Speichern-Dialog" });
        Assert.True(r["data"]!["applied"]!.GetValue<bool>());
        var step = Call(server, "get_step", new JsonObject { ["sessionId"] = id, ["step"] = 1, ["images"] = new JsonArray() })["data"]!;
        Assert.Contains("Agentenbefund-K9", step["agentNotes"]![0]!["text"]!.GetValue<string>());

        var folder = Directory.GetDirectories(_root).Single(d => Json.ReadObject(Path.Combine(d, "manifest.json"))!.Str("sessionId") == id);
        using var v = SessionView.Open(folder, headless: true);
        Assert.Empty(v.All.SelectMany(s => s.Comments));
        var trace = KiPackage.Trace(v, v.All, new(), "de", "full");
        Assert.DoesNotContain("Agentenbefund-K9", trace);
        var md = Path.Combine(_root, "out", "a.md");
        GuideExport.Export(v, new GuideOptions { Format = "md", OutPath = md, Importance = "all" });
        Assert.DoesNotContain("Agentenbefund-K9", File.ReadAllText(md));
    }

    [Fact]
    public void RequestRecording_RetestOf_MustBeKnownSession()
    {
        Assert.Throws<ArgumentException>(() => RecordingRequests.Create("Bitte den Ablauf erneut zeigen", null, null, _root, retestOf: "..\\x"));
        RecordingRequests.DirOverride = Path.Combine(_root, "rq");
        try
        {
            var rq = RecordingRequests.Create("Bitte den Ablauf erneut zeigen", null, null, _root, retestOf: "0192a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");
            Assert.Equal("0192a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b", RecordingRequests.Load(rq.Id)!.RetestOf);
        }
        finally { RecordingRequests.DirOverride = null; }
    }

    [Fact]
    public void Latest_ResolvesNewestCompleteSession()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"));
        Assert.Equal(a, SessionCatalog.Resolve("latest", _root));
        Assert.Equal(@"C:\x", SessionCatalog.Resolve(@"C:\x", _root));
        Assert.Throws<DirectoryNotFoundException>(() => SessionCatalog.Resolve("latest", Path.Combine(_root, "leer")));
    }

    [Fact]
    public void Export_StepRange_LimitsGuideAndAiPackage()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "r"));
        using var v = SessionView.Open(folder, headless: true);
        int total = v.GuideSteps("all").Count;
        Assert.True(total >= 3);
        var res = Exporter.Run(v, new ExportRequest { Format = "md", Importance = "all", FromStep = 1, ToStep = 1, Target = Path.Combine(_root, "md") + "\\" });
        string md = File.ReadAllText(res.Path!);
        var headings = md.Split('\n').Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^#{2,3} \d+\\?\.")).ToList();
        Assert.Single(headings);
        Assert.StartsWith("## 1", headings[0]);
        Assert.Null(v.Range);                                                    // nach dem Export wieder alle Schritte
        var ki = Exporter.Run(v, new ExportRequest { Format = "ki", FromStep = 2, ToStep = 3, Target = Path.Combine(_root, "ki") });
        var steps = Json.ReadObject(Path.Combine(ki.Path!, "steps.json"))!.Arr("steps")!;
        Assert.All(steps, s => Assert.InRange(s!["number"]!.GetValue<int>(), 2, 3));
    }

    [Fact]
    public void McpConfig_MoreClients()
    {
        var o = new McpConfigOptions(@"C:\Tools\SebastiansStepRecorder.exe", @"C:\Tools\Sessions");
        var vs = JsonNode.Parse(McpConfig.VsCodeJson(o))!;
        Assert.Equal("stdio", vs["servers"]!["steprecorder"]!["type"]!.GetValue<string>());
        Assert.Equal(@"C:\Tools\SebastiansStepRecorder.exe", vs["servers"]!["steprecorder"]!["command"]!.GetValue<string>());
        var toml = McpConfig.CodexToml(o);
        Assert.Contains("[mcp_servers.steprecorder]", toml);
        Assert.Contains(@"command = 'C:\Tools\SebastiansStepRecorder.exe'", toml);
        Assert.Contains(@"args = ['mcp', '--root', 'C:\Tools\Sessions']", toml);
        var quoted = McpConfig.CodexToml(o with { Root = @"C:\O'Brien" });
        Assert.Contains(@"""C:\\O'Brien""", quoted);
        Assert.Equal(5, McpConfig.Clients.Length);
    }
}
