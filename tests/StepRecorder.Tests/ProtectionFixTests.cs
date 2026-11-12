using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.Mcp;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Korrekturen an Schutzfunktionen (Etappe 0.1): Zielordner, Tastenereignisse, interne Kommentare, Terminal.</summary>
public class ProtectionFixTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-prot-" + Guid.NewGuid().ToString("N")[..8]);

    public ProtectionFixTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    string NewSession(bool withSecret = false, bool withComment = false, bool withTerminal = false)
        => SyntheticSession.Create(Path.Combine(_root, "s" + Guid.NewGuid().ToString("N")[..6]), withSecret: withSecret, withComment: withComment, withTerminal: withTerminal);

    [Fact]
    public void KiExport_NeverWipesForeignFolder()
    {
        var folder = NewSession();
        var target = Path.Combine(_root, "fremd");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "wichtig.txt"), "nicht löschen");
        using var v = SessionView.Open(folder, headless: true);
        Assert.Throws<ExportTargetException>(() => KiPackage.Export(v, new KiOptions { OutDir = target }));
        Assert.True(File.Exists(Path.Combine(target, "wichtig.txt")));
        Assert.Throws<ExportTargetException>(() => AhkKiPackage.Export(v, new AhkKiOptions { OutDir = target, Ocr = false }));
        Assert.True(File.Exists(Path.Combine(target, "wichtig.txt")));

        // leerer Ordner und früheres Paket dürfen benutzt bzw. ersetzt werden
        var empty = Path.Combine(_root, "leer"); Directory.CreateDirectory(empty);
        KiPackage.Export(v, new KiOptions { OutDir = empty });
        Assert.True(File.Exists(Path.Combine(empty, "ai-package.json")));
        File.WriteAllText(Path.Combine(empty, "alt.txt"), "x");
        KiPackage.Export(v, new KiOptions { OutDir = empty });
        Assert.False(File.Exists(Path.Combine(empty, "alt.txt")));
    }

    [Fact]
    public void Headless_UnreviewedInput_KeyUpsCarryNoKeyNames()
    {
        var folder = NewSession(withSecret: true);
        using var v = SessionView.Open(folder, headless: true);
        var step = v.All.Single(s => s.Unreviewed);
        var ranges = step.Json.Obj("seq")!.Arr("input")!.Select(r => (r![0]!.GetValue<long>(), r[1]!.GetValue<long>())).ToList();
        var keyEvents = v.Session.Events.Where(e => e.Str("type") is "key.down" or "key.up" && ranges.Any(r => e.Long("seq") >= r.Item1 && e.Long("seq") <= r.Item2)).ToList();
        Assert.Contains(keyEvents, e => e.Str("type") == "key.up");
        foreach (var e in keyEvents)
        {
            var o = v.EventForOutput(e);
            Assert.Equal("Redacted", o.Str("key"));
            Assert.Null(o["ch"]);
        }
    }

    [Fact]
    public void TextRedaction_OfTypedKeys_HidesTextWithoutFinalRedaction()
    {
        var folder = NewSession();
        using (var v = SessionView.Open(folder))
        {
            var type = v.All.Single(s => s.Kind == "type" && !s.Json.Obj("input")!.Bool("masked"));
            var targets = new JsonArray();
            foreach (var rg in type.Json.Obj("seq")!.Arr("input")!)
                for (long q = rg![0]!.GetValue<long>(); q <= rg[1]!.GetValue<long>(); q++)
                    if (v.Session.BySeq(q) is { } ke && ke.Str("ch") != null) targets.Add(new JsonObject { ["seq"] = q, ["field"] = "ch" });
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "text", ["targets"] = targets, ["by"] = "user" });
            Assert.True(v.SaveEdits());
        }
        using var view = SessionView.Open(folder, headless: true);
        var st = view.All.Single(s => s.Kind == "type" && !s.Json.Obj("input")!.Bool("masked"));
        Assert.DoesNotContain("notiz", st.Title + st.Guide + st.Protocol + st.Json.ToJsonString());
        Assert.False(st.Unreviewed);
        foreach (var e in view.Session.Events.Where(e => e.Str("type") is "key.down" or "key.up" or "ui.value"))
            Assert.DoesNotContain("notiz", view.EventForOutput(e).ToJsonString());
        // kein key.up im Tipplauf verrät die Taste
        var ups = view.Session.Events.Where(e => e.Str("type") == "key.up" && e.Str("fg") == "w2" && e.Long("vk") != 13).ToList();
        Assert.NotEmpty(ups);
        Assert.All(ups, e => Assert.Equal("Redacted", view.EventForOutput(e).Str("key")));
    }

    [Fact]
    public void FinalRedaction_NeutersPairedKeyUps()
    {
        var folder = NewSession();
        JsonObject edits;
        using (var v = SessionView.Open(folder))
        {
            var type = v.All.Single(s => s.Kind == "type" && !s.Json.Obj("input")!.Bool("masked"));
            var targets = new JsonArray();
            foreach (var rg in type.Json.Obj("seq")!.Arr("input")!)
                for (long q = rg![0]!.GetValue<long>(); q <= rg[1]!.GetValue<long>(); q++)
                    if (v.Session.BySeq(q) is { } ke && ke.Str("type") == "key.down" && ke.Str("ch") != null) targets.Add(new JsonObject { ["seq"] = q, ["field"] = "ch" });
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "text", ["targets"] = targets, ["by"] = "user" });
            Assert.True(v.SaveEdits());
            edits = (JsonObject)v.Edits.DeepClone();
        }
        Redactor.ApplyTextRedactionsDestructive(folder, edits);
        var journal = File.ReadAllText(Path.Combine(folder, "events.jsonl"));
        foreach (var line in journal.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var e = JsonNode.Parse(line)!.AsObject();
            if (e.Str("type") is "key.up" && e.Str("fg") == "w2" && e.Long("vk") != 13) Assert.Equal("Redacted", e.Str("key"));
        }
        var (issues, _) = Validator.Validate(folder, thorough: true);
        Assert.Empty(issues.Where(i => i.Severity == "error"));
    }

    [Fact]
    public void HiddenRecordedComment_AppearsInNoOutput()
    {
        var folder = NewSession(withComment: true);
        long commentSeq;
        using (var v = SessionView.Open(folder))
        {
            commentSeq = v.Session.Events.Single(e => e.Str("type") == "comment").Long("seq");
            v.Edits["comments"] = new JsonArray(new JsonObject { ["id"] = "rc1", ["origin"] = "recording", ["sourceSeq"] = commentSeq, ["kind"] = "internal", ["hidden"] = true, ["by"] = "user" });
            Assert.True(v.SaveEdits());
            var cs = v.All.Single(s => s.Kind == "comment");
            Assert.True(cs.CommentHidden && cs.Hidden);
            Assert.Contains(SyntheticSession.CommentCanary, v.OriginalCommentText(cs));
            Assert.DoesNotContain(SyntheticSession.CommentCanary, cs.Guide + cs.Protocol + cs.Title);
        }
        using (var hv = SessionView.Open(folder, headless: true))
        {
            Assert.DoesNotContain(SyntheticSession.CommentCanary, hv.EventForOutput(hv.Session.BySeq(commentSeq)!).ToJsonString());
            var r = KiPackage.Export(hv, new KiOptions { Detail = "full" });
            foreach (var f in Directory.GetFiles(r.Path, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".png")))
                Assert.DoesNotContain(SyntheticSession.CommentCanary, File.ReadAllText(f));
        }
        using (var gv = SessionView.Open(folder))
        {
            var html = GuideExport.Export(gv, new GuideOptions { Format = "html", Importance = "all" });
            Assert.DoesNotContain(SyntheticSession.CommentCanary, File.ReadAllText(html.Path));
        }
        // MCP
        var mcp = new StepRecorderMcp(new McpOptions { Root = _root });
        var server = new McpStdioServer("steprecorder", "test", StepRecorderMcp.Instructions, TextReader.Null, TextWriter.Null, TextWriter.Null);
        mcp.Register(server);
        server.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize", ["params"] = new JsonObject { ["protocolVersion"] = "2025-06-18" } });
        var id = Session.Open(folder).SessionId;
        foreach (var (tool, args) in new[] { ("get_steps", new JsonObject { ["sessionId"] = id, ["importance"] = "all", ["detail"] = "full" }), ("get_events", new JsonObject { ["sessionId"] = id, ["limit"] = 500 }), ("search", new JsonObject { ["sessionId"] = id, ["query"] = SyntheticSession.CommentCanary }) })
        {
            var resp = server.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 2, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args } });
            Assert.DoesNotContain(SyntheticSession.CommentCanary, resp!.ToJsonString());
        }
    }

    [Fact]
    public void TerminalTyping_IsPlaintextAndHiddenUntilReviewed()
    {
        var folder = NewSession(withTerminal: true);
        using var v = SessionView.Open(folder, headless: true);
        var st = v.All.Single(s => s.Kind == "type" && s.Json.Obj("ctx")?.Str("process") == "WindowsTerminal.exe");
        Assert.Contains("plaintext", st.Json.StrList("flags"));
        Assert.True(st.Unreviewed);
        Assert.Equal("plaintextWindow", st.FlagReason);
        Assert.DoesNotContain("kanarie42", st.Title + st.Guide + st.Protocol);
        foreach (var e in v.Session.Events) Assert.DoesNotContain("kanarie42", v.EventForOutput(e).ToJsonString());
    }

    [Fact]
    public void BlobPath_RejectsTraversal()
    {
        Assert.Null(Session.BlobPath("sha256:../../windows/x"));
        Assert.Null(Session.BlobPath("sha256:" + new string('A', 64)));
        Assert.NotNull(Session.BlobPath("sha256:" + new string('a', 64)));
    }
}
