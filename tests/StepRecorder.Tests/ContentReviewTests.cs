using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Mcp;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Pakete 12 und 13 des Entwickler-Fahrplans: prüfbare Programmausgaben, schwächerer Schutz, Protokolldateien.</summary>
public class ContentReviewTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-content-" + Guid.NewGuid().ToString("N")[..8]);
    public ContentReviewTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    const string Canary = "Kanarie-Stack-5T1";

    string Crash(string name) => SyntheticSession.Create(Path.Combine(_root, name), withCrash: true, withEventText: true);

    static string AllText(string folder) => string.Join("\n", Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".png")).Select(File.ReadAllText));

    [Fact]
    public void EventLogText_Unreviewed_AppearsInNoOutputPath()
    {
        var folder = Crash("a");
        using var v = SessionView.Open(folder, headless: true);
        Assert.True(v.UnreviewedCount > 0);
        Assert.Contains(v.ReviewItems, i => i.Source == "eventlog.entry" && i.Required && !i.Reviewed);
        // Rohereignisse, Programm-Kontext, Protokoll
        Assert.DoesNotContain(Canary, string.Join("\n", v.Session.Events.Select(e => v.EventForOutput(e).ToJsonString())));
        Assert.DoesNotContain(Canary, DeveloperContext.Build(v, 0, long.MaxValue, "de")!.ToJsonString());
        Assert.DoesNotContain(Canary, KiPackage.Trace(v, v.All, new(), "de", "full"));
        // KI-Paket mit Rohereignissen
        var ki = KiPackage.Export(v, new KiOptions { OutDir = Path.Combine(_root, "ki"), Detail = "full", IncludeEvents = true });
        Assert.DoesNotContain(Canary, AllText(ki.Path!));
        // Suche
        Assert.Empty(SessionSearch.Search(SessionSearch.Entries(v), SearchQuery.Parse(Canary)));
        // MCP
        var mcp = new StepRecorderMcp(new McpOptions { Root = _root });
        var server = new McpStdioServer("steprecorder", "test", StepRecorderMcp.Instructions, TextReader.Null, TextWriter.Null, TextWriter.Null);
        mcp.Register(server);
        foreach (var (tool, args) in new[] { ("get_failure_context", new JsonObject { ["sessionId"] = v.Session.SessionId, ["image"] = false }), ("get_events", new JsonObject { ["sessionId"] = v.Session.SessionId, ["fromSeq"] = 1 }), ("get_steps", new JsonObject { ["sessionId"] = v.Session.SessionId, ["importance"] = "all", ["detail"] = "full" }) })
        {
            var r = server.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args } });
            Assert.DoesNotContain(Canary, r!.ToJsonString());
        }
        // Bereinigte Kopie: erst nach der Durchsicht
        Assert.Throws<SanitizeException>(() => SessionSanitizer.CreateCopy(folder, Path.Combine(_root, "kopie")));
        // Anleitungen aus dem Editor verlangen die Durchsicht
        using var ed = SessionView.Open(folder);
        Assert.ThrowsAny<Exception>(() => GuideExport.Export(ed, new GuideOptions { Format = "bugreport", OutPath = Path.Combine(_root, "b", "b.md") }));
    }

    [Fact]
    public void EventLogText_AfterReview_KeptOrRedacted()
    {
        var folder = Crash("k");
        string key;
        using (var v = SessionView.Open(folder))
        {
            key = v.ReviewItems.Single(i => i.Source == "eventlog.entry").Key;
            v.Edits["review"] = new JsonObject { ["inputs"] = new JsonArray(new JsonObject { ["kind"] = "content", ["key"] = key, ["status"] = "kept" }) };
            Assert.True(v.SaveEdits("test"));
        }
        using (var v = SessionView.Open(folder, headless: true))
        {
            Assert.Equal(0, v.UnreviewedCount);
            Assert.Contains(Canary, KiPackage.Trace(v, v.All, new(), "de", "full"));
            Assert.Contains(Canary, DeveloperContext.Build(v, 0, long.MaxValue, "de")!.ToJsonString());
        }
        var folder2 = Crash("r");
        using (var v = SessionView.Open(folder2))
        {
            var item = v.ReviewItems.Single(i => i.Source == "eventlog.entry");
            v.Edits["review"] = new JsonObject { ["inputs"] = new JsonArray(new JsonObject { ["kind"] = "content", ["key"] = item.Key, ["status"] = "redacted" }) };
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "range", ["seqRanges"] = new JsonArray(new JsonArray(item.Seq, item.Seq)), ["images"] = false });
            Assert.True(v.SaveEdits("test"));
        }
        using var v2 = SessionView.Open(folder2, headless: true);
        Assert.Equal(0, v2.UnreviewedCount);
        Assert.DoesNotContain(Canary, KiPackage.Trace(v2, v2.All, new(), "de", "full"));
    }

    [Fact]
    public void FlaggedAuthorText_IsReviewItem_AndMaskedHeadless()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "c"));
        const string token = "ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZ0123";
        using (var v = SessionView.Open(folder))
        {
            v.Edits["comments"] = new JsonArray(new JsonObject { ["id"] = "c1", ["anchor"] = v.All.First(s => s.Kind == "click").Anchor, ["kind"] = "note", ["text"] = "Token war " + token });
            Assert.True(v.SaveEdits("test"));
        }
        using var h = SessionView.Open(folder, headless: true);
        Assert.Contains(h.ReviewItems, i => i.Key == "comment:c1" && i.FlagReason == "keyOrToken");
        Assert.True(h.UnreviewedCount > 0);
        Assert.DoesNotContain(token, KiPackage.Trace(h, h.All, new(), "de", "full"));
        using var ed = SessionView.Open(folder);
        Assert.Contains(token, string.Join(" ", ed.All.SelectMany(s => s.Comments).Select(c => c.Str("text"))));   // im Editor sichtbar
    }

    [Fact]
    public void Weakens_AndStrictDisablesContentSources()
    {
        var o = new RecordingOptions { EventLogText = true, ClipboardContent = true, LogFiles = { @"C:\x.log" } };
        Assert.Equal(new[] { "eventLogText", "clipboardContent", "logFiles" }, o.ContentSources());
        Assert.Equal(new[] { "privacyLevel", "eventLogText", "clipboardContent", "logFiles" }, o.Weakens("strict"));
        var p = o.ProfileJson();
        Assert.Equal(3, p["effective"]!["contentSources"]!.AsArray().Count);
        o.PrivacyLevel = "strict";
        Assert.Empty(o.ContentSources());
        Assert.Empty(o.Weakens("strict"));
        Assert.Null(o.ProfileJson()["weakens"]);
        Assert.False(new RecordingOptions().SettingsJson(new JsonObject())["privacy"]!["eventLogText"]!.GetValue<bool>());
    }

    [Fact]
    public void ReaderGuard_RejectsNewerRequirement()
    {
        var folder = SyntheticSession.Create(Path.Combine(_root, "g"));
        var man = Json.ReadObject(Path.Combine(folder, "manifest.json"))!;
        man["requires"] = new JsonObject { ["reader"] = "99.0.0", ["reason"] = "contentEvents" };
        File.WriteAllText(Path.Combine(folder, "manifest.json"), Json.Pretty(man));
        Assert.Throws<NotSupportedException>(() => Session.Open(folder));
        man["requires"] = new JsonObject { ["reader"] = Session.ContentReaderVersion };
        File.WriteAllText(Path.Combine(folder, "manifest.json"), Json.Pretty(man));
        Session.Open(folder);   // eigene Version genügt
    }

    [Fact]
    public void LogWatch_ReadsOnlyAppendedLines_HandlesRotationAndPause()
    {
        string log = Path.Combine(_root, "app.log");
        File.WriteAllText(log, "alt vor der Aufnahme\n");
        var events = new List<JsonObject>();
        bool active = true;
        long t = 0;
        var w = new LogWatch(new[] { Path.Combine(_root, "*.log") }, () => t += 1000, e => events.Add(e), () => active);
        Assert.Equal(new[] { "app.log" }, w.Files);
        w.Start();
        Assert.Contains(events, e => e.Str("type") == "log.source" && e.Str("file") == "app.log");
        File.AppendAllText(log, "INFO Start\nERROR Speichern fehlgeschlagen\nteil");
        w.PollAll();
        var lines = events.Single(e => e.Str("type") == "log.lines");
        Assert.Equal("INFO Start\nERROR Speichern fehlgeschlagen", lines.Str("lines"));
        Assert.DoesNotContain("alt vor der Aufnahme", lines.Str("lines"));
        Assert.Equal("app.log", lines.Str("file"));                                   // nie der Pfad
        active = false;
        File.AppendAllText(log, "weise\nGEHEIM in der Pause\n");
        w.PollAll();
        active = true;
        File.AppendAllText(log, "nach der Pause\n");
        w.PollAll();
        var last = events.Last(e => e.Str("type") == "log.lines");
        Assert.Equal("nach der Pause", last.Str("lines"));
        Assert.True(last.Long("dropped") >= 1);
        Assert.DoesNotContain(events, e => (e.Str("lines") ?? "").Contains("GEHEIM"));
        File.WriteAllText(log, "neu\n");                                             // gedreht
        w.PollAll();
        Assert.True(events.Last().Bool("rotated"));
        w.Dispose();
        Assert.Empty(LogWatch.Resolve(new[] { @"\\server\share\app.log" }));
        Assert.Empty(LogWatch.Resolve(new[] { Path.Combine(_root, "fehlt.log") }));
    }
}
