using System.Text.Json.Nodes;
using StepRecorder.Mcp;

namespace StepRecorder.Tests;

/// <summary>Agent bittet um Vorführung: Anfragen, Zustände, Missbrauchsschutz, MCP-Werkzeuge.</summary>
[Collection("Requests")]
public class RecordingRequestsTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-rq-" + Guid.NewGuid().ToString("N")[..8]);
    public RecordingRequestsTests() { Directory.CreateDirectory(_root); RecordingRequests.DirOverride = Path.Combine(_root, "requests"); }
    public void Dispose() { RecordingRequests.DirOverride = null; try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void Create_ValidatesAndSanitizes()
    {
        Assert.Throws<ArgumentException>(() => RecordingRequests.Create("zu kurz", null, null, _root));
        Assert.Throws<ArgumentException>(() => RecordingRequests.Create(new string('x', 501), null, null, _root));
        var rq = RecordingRequests.Create("Bitte zeigen‮ Sie\u0007, wie man eine Datei speichert.", "Datei liegt\r\nim Ordner", "Titel\nmit Umbruch", _root);
        Assert.Matches("^rq-[0-9a-f]{12}$", rq.Id);
        Assert.DoesNotContain('‮', rq.Goal);
        Assert.DoesNotContain('\u0007', rq.Goal);
        Assert.Equal("Datei liegt\nim Ordner", rq.Expected);
        Assert.Equal("Titel mit Umbruch", rq.Title);
        Assert.Equal(_root, RecordingRequests.Load(rq.Id)!.Root);
        Assert.Null(RecordingRequests.Load("../evil"));
    }

    [Fact]
    public void Status_TransitionsAndExpiry()
    {
        var t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        var rq = RecordingRequests.Create("Bitte die Anmeldung vorführen.", null, null, _root, t0);
        Assert.Equal("pending", RecordingRequests.Status(rq.Id, t0.AddSeconds(30)).state);
        Assert.Equal("expired", RecordingRequests.Status(rq.Id, t0.AddSeconds(121)).state);
        Assert.True(RecordingRequests.SetStatus(rq.Id, "recording"));
        Assert.False(RecordingRequests.SetStatus(rq.Id, "declined"));               // aus „recording“ nicht zurück
        Assert.True(RecordingRequests.SetStatus(rq.Id, "finished", "0190a3c4-0000-7000-8000-000000000001"));
        Assert.False(RecordingRequests.SetStatus(rq.Id, "discarded"));             // abgeschlossen bleibt abgeschlossen
        var st = RecordingRequests.Status(rq.Id);
        Assert.Equal(("finished", "0190a3c4-0000-7000-8000-000000000001"), (st.state, st.sessionId));
        Assert.False(RecordingRequests.SetStatus(rq.Id, "bogus"));
        Assert.Equal("unknown", RecordingRequests.Status("rq-000000000000").state);
    }

    [Fact]
    public void Policy_OneOpen_60s_FivePerHour_Block()
    {
        var now = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(RequestPolicy.Check(now, Array.Empty<DateTime>(), null, false).ok);
        Assert.Equal("busy", RequestPolicy.Check(now, Array.Empty<DateTime>(), null, true).reason);
        Assert.Equal("tooSoon", RequestPolicy.Check(now, new[] { now.AddSeconds(-30) }, null, false).reason);
        var five = Enumerable.Range(1, 5).Select(i => now.AddMinutes(-i * 5)).ToList();
        Assert.Equal("hourlyLimit", RequestPolicy.Check(now, five, null, false).reason);
        Assert.True(RequestPolicy.Check(now, five.Select(d => d.AddHours(-1)).ToList(), null, false).ok);
        Assert.Equal("blocked", RequestPolicy.Check(now, Array.Empty<DateTime>(), now.AddMinutes(10), false).reason);
        Assert.True(RequestPolicy.Check(now, Array.Empty<DateTime>(), now.AddMinutes(-1), false).ok);
    }

    static (McpStdioServer server, List<string[]> sent) Server(string root, bool allow, bool uiRunning)
    {
        var sent = new List<string[]>();
        var mcp = new StepRecorderMcp(new McpOptions { Root = root, AllowRequests = allow, UiRunning = () => uiRunning, Notify = a => { sent.Add(a); return true; } });
        var server = new McpStdioServer("steprecorder", "test", StepRecorderMcp.Instructions, TextReader.Null, TextWriter.Null, TextWriter.Null);
        mcp.Register(server);
        server.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize", ["params"] = new JsonObject { ["protocolVersion"] = "2025-06-18" } });
        return (server, sent);
    }

    static JsonObject Raw(McpStdioServer s, string tool, JsonObject args)
        => s.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 2, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args } })!.AsObject();

    static string Text(JsonObject r) => r["result"]!["content"]![0]!["text"]!.GetValue<string>();

    [Fact]
    public void Mcp_ToolsOnlyWithSwitch_UnknownKeysRejected_StatusFlow()
    {
        var (off, _) = Server(_root, allow: false, uiRunning: true);
        var tools = off.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 5, ["method"] = "tools/list" })!["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToList();
        Assert.DoesNotContain("request_recording", tools);
        Assert.DoesNotContain("get_recording_request", tools);

        var (down, _) = Server(_root, allow: true, uiRunning: false);
        var r0 = Raw(down, "request_recording", new JsonObject { ["goal"] = "Bitte zeigen Sie den Export als PDF." });
        Assert.True(r0["result"]!["isError"]!.GetValue<bool>());
        Assert.Contains("UI_UNAVAILABLE", Text(r0));

        var (on, sent) = Server(_root, allow: true, uiRunning: true);
        var bad = Raw(on, "request_recording", new JsonObject { ["goal"] = "Bitte zeigen Sie den Export als PDF.", ["scope"] = "region" });
        Assert.True(bad["result"]!["isError"]!.GetValue<bool>());
        var ok = Raw(on, "request_recording", new JsonObject { ["goal"] = "Bitte zeigen Sie den Export als PDF.", ["title"] = "PDF-Export" });
        Assert.False(ok["result"]!["isError"]!.GetValue<bool>(), Text(ok));
        var data = JsonNode.Parse(Text(ok))!["data"]!;
        string id = data["requestId"]!.GetValue<string>();
        Assert.Equal("pending", data["state"]!.GetValue<string>());
        Assert.Equal(120, data["expiresInSeconds"]!.GetValue<int>());
        Assert.Equal(new[] { "--recording-request", id }, sent.Single());

        string State() => JsonNode.Parse(Text(Raw(on, "get_recording_request", new JsonObject { ["requestId"] = id })))!["data"]!["state"]!.GetValue<string>();
        Assert.Equal("pending", State());
        RecordingRequests.SetStatus(id, "recording");
        Assert.Equal("recording", State());
        RecordingRequests.SetStatus(id, "finished", "0190a3c4-0000-7000-8000-000000000002");
        var fin = JsonNode.Parse(Text(Raw(on, "get_recording_request", new JsonObject { ["requestId"] = id, ["waitSeconds"] = 0 })))!["data"]!;
        Assert.Equal("finished", fin["state"]!.GetValue<string>());
        Assert.Equal("0190a3c4-0000-7000-8000-000000000002", fin["sessionId"]!.GetValue<string>());
        Assert.True(Raw(on, "get_recording_request", new JsonObject { ["requestId"] = "rq-ffffffffffff" })["result"]!["isError"]!.GetValue<bool>());
    }
}
