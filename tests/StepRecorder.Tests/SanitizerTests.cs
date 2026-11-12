using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Bereinigte Kopie und endgültiges Entfernen der Bilder geschwärzter Zeiträume.</summary>
[Collection("Loc")]
public class SanitizerTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-san-" + Guid.NewGuid().ToString("N")[..8]);
    public SanitizerTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    const string InternalCanary = "Interner-Kanarie-77";

    static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

    [Fact]
    public void Copy_HasNoProtectedValues_RedactedPixelsBlack_Validates_OriginalUnchanged()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"), withComment: true);
        string frameId, m;
        using (var v = SessionView.Open(a))
        {
            long commentSeq = v.Session.Events.Single(e => e.Str("type") == "comment").Long("seq");
            var title = v.Session.Events.First(e => e.Str("type") == "window.title" && (e.Str("title") ?? "").Contains("notiz.txt"));
            long anchor = v.All.First(s => s.Kind == "click").Anchor;
            frameId = v.All.First(s => s.PreFrame != null && !s.PreFrame.StartsWith("u:")).PreFrame!;
            m = v.Session.Frame(frameId)!.Str("m") ?? "";
            v.Edits["comments"] = new JsonArray(
                new JsonObject { ["id"] = "rc1", ["origin"] = "recording", ["sourceSeq"] = commentSeq, ["kind"] = "internal", ["hidden"] = true, ["by"] = "user" },
                new JsonObject { ["id"] = "uc1", ["anchor"] = anchor, ["kind"] = "internal", ["text"] = InternalCanary, ["by"] = "user", ["origin"] = "viewer" });
            v.Edits["substitutions"] = new JsonArray(new JsonObject { ["id"] = "sb1", ["match"] = new JsonObject { ["literal"] = "Unbenannt" }, ["placeholder"] = "Dokument" });
            v.Edits["redactions"] = new JsonArray(
                new JsonObject { ["id"] = "r1", ["kind"] = "text", ["targets"] = new JsonArray(new JsonObject { ["seq"] = title.Long("seq"), ["field"] = "title", ["range"] = new JsonArray(0, 9) }) },
                new JsonObject { ["id"] = "r2", ["kind"] = "image", ["areas"] = new JsonArray(new JsonObject { ["m"] = m, ["from"] = frameId, ["to"] = null, ["rect"] = new JsonArray(10, 10, 30, 20) }), ["style"] = "black" });
            Assert.True(v.SaveEdits());
        }
        string evHash = Hash(Path.Combine(a, "events.jsonl")), edHash = Hash(Path.Combine(a, "edits.json"));
        var copy = SessionSanitizer.CreateCopy(a, Path.Combine(_root, "kopie"));

        var text = string.Join("\n", Directory.EnumerateFiles(copy, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".png")).Select(File.ReadAllText));
        foreach (var canary in new[] { SyntheticSession.CommentCanary, InternalCanary, "Unbenannt", "notiz.txt" })
            Assert.DoesNotContain(canary, text, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(copy, "steps.json")));

        var (issues, _) = Validator.Validate(copy, thorough: true);
        Assert.DoesNotContain(issues, i => i.Severity == "error");

        using (var c = Session.Open(copy, recover: false))
        using (var o = Session.Open(a, recover: false))
        {
            Assert.NotEqual(o.SessionId, c.SessionId);
            using var red = c.RestoreFrameCopy(frameId)!;
            var px = red.GetPixel(15, 15);
            Assert.Equal((0, 0, 0), (px.R, px.G, px.B));
            using var orig = o.RestoreFrameCopy(frameId)!;
            Assert.NotEqual(Color.Black.ToArgb(), orig.GetPixel(15, 15).ToArgb());
        }
        Assert.Equal(evHash, Hash(Path.Combine(a, "events.jsonl")));
        Assert.Equal(edHash, Hash(Path.Combine(a, "edits.json")));
    }

    [Fact]
    public void Copy_RefusedWhileInputUnreviewed_LeavesNothing()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"), withSecret: true);
        var target = Path.Combine(_root, "kopie");
        Assert.Throws<SanitizeException>(() => SessionSanitizer.CreateCopy(a, target));
        Assert.False(Directory.Exists(target));
        Assert.DoesNotContain(Directory.EnumerateDirectories(_root), d => Path.GetFileName(d).StartsWith("kopie"));
    }

    [Fact]
    public void FinalRedaction_RemovesFramesOfRange_KeepsLaterFramesIntact()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"));
        JsonObject edits;
        (long from, long to) range;
        using (var v = SessionView.Open(a))
        {
            var click = v.All.First(s => s.Kind == "click");
            var rg = click.Json.Obj("seq")!.Arr("input")![0]!;
            range = (rg[0]!.GetValue<long>(), rg[1]!.GetValue<long>());
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "range", ["seqRanges"] = new JsonArray(new JsonArray(range.from, range.to)), ["images"] = true });
            Assert.True(v.SaveEdits());
            edits = v.Edits;
        }
        bool In(long q) => q >= range.from && q <= range.to;
        HashSet<string> purged;
        Dictionary<string, string> before = new();
        using (var s = Session.Open(a, recover: false))
        {
            purged = s.Frames.Values.Where(f => f.Obj("store").Str("kind") != "none" && (In(f.Long("seq")) || (f.LongN("ref") is long r && In(r)))).Select(f => f.Str("id")!).ToHashSet();
            foreach (var id in s.Frames.Keys.Where(id => !purged.Contains(id)))
                using (var b = s.RestoreFrameCopy(id)) if (b != null) before[id] = Pixels(b);
        }
        Assert.NotEmpty(purged);
        Assert.NotEmpty(before);

        Redactor.ApplyTextRedactionsDestructive(a, edits);

        using (var s = Session.Open(a, recover: false))
        {
            foreach (var id in purged) Assert.Equal("none", s.Frame(id)!.Obj("store").Str("kind"));
            foreach (var (id, px) in before)
                using (var b = s.RestoreFrameCopy(id)) Assert.Equal(px, b == null ? null : Pixels(b));
            var used = FrameBlobs.Referenced(s.Events);
            var files = Directory.EnumerateFiles(Path.Combine(a, "frames"), "*.png", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.True(files.SetEquals(used));
        }
        var (issues, _) = Validator.Validate(a, thorough: true);
        Assert.DoesNotContain(issues, i => i.Severity == "error");
    }

    static string Pixels(Bitmap b)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < b.Height; y += 7) for (int x = 0; x < b.Width; x += 7) sb.Append(b.GetPixel(x, y).ToArgb()).Append(',');
        return sb.ToString();
    }
}
