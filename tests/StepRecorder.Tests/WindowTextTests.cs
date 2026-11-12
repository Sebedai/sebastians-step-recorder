using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Mcp;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Paket 11 des Entwickler-Fahrplans: Fehlertext als Text-Schnappschuss (<c>ui.snapshot</c>).</summary>
public class WindowTextTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-snap-" + Guid.NewGuid().ToString("N")[..8]);
    public WindowTextTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void Snapshots_BindToSteps_AndAppearInOutputs()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "s"), withError: true, withSnapshot: true);
        using var v = SessionView.Open(folder, headless: true);
        var click = v.All.First(s => s.Kind == "click");
        Assert.Contains(v.SnapshotsOf(click), x => x.trigger == "dialogStart" && x.text.Contains("Speichern"));
        var marker = v.All.Single(s => s.Kind == "marker" && s.Json.Obj("input")?.Str("markerKind") == "error");
        var snap = Assert.Single(v.SnapshotsOf(marker));
        Assert.Contains("Zugriff verweigert", snap.text);

        var trace = KiPackage.Trace(v, v.All, new(), "de", "standard");
        Assert.Contains("- Text im Fenster „Fehler“ (manual):", trace);
        Assert.Contains("schreibgeschützt", trace);

        Assert.Contains(SessionSearch.Search(SessionSearch.Entries(v), SearchQuery.Parse("schreibgeschützt"), true, new List<string>()), h => h.Item1.Field == "windowText");

        var mcp = new StepRecorderMcp(new McpOptions { Root = _root });
        var server = new McpStdioServer("steprecorder", "test", StepRecorderMcp.Instructions, TextReader.Null, TextWriter.Null, TextWriter.Null);
        mcp.Register(server);
        var r = server.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = "get_failure_context", ["arguments"] = new JsonObject { ["sessionId"] = v.Session.SessionId, ["image"] = false } } });
        var text = r!["result"]!["content"]![0]!["text"]!.GetValue<string>();
        Assert.Contains("schreibgeschützt", text);                                  // windowTexts am Marker und errorTexts im Programm-Kontext
        Assert.Contains("windowTexts", text);
    }

    [Fact]
    public void TextRedaction_AndRangeRedaction_ApplyToSnapshots()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "r"), withError: true, withSnapshot: true);
        using (var v = SessionView.Open(folder))
        {
            var snapEv = v.Session.Events.Last(e => e.Str("type") == "ui.snapshot");
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "range", ["seqRanges"] = new JsonArray(new JsonArray(snapEv.Long("seq"), snapEv.Long("seq"))), ["images"] = false });
            Assert.True(v.SaveEdits("test"));
        }
        using var v2 = SessionView.Open(folder, headless: true);
        var marker = v2.All.Single(s => s.Kind == "marker" && s.Json.Obj("input")?.Str("markerKind") == "error");
        Assert.Empty(v2.SnapshotsOf(marker));
        Assert.DoesNotContain("schreibgeschützt", KiPackage.Trace(v2, v2.All, new(), "de", "full"));
    }
}
