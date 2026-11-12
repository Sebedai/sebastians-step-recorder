using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Bearbeitungsstände: Intervall, Deduplizierung, Obergrenzen, Wiederherstellen mit/ohne Schutz, Löschen.</summary>
[Collection("Loc")]
public class EditHistoryTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-hist-" + Guid.NewGuid().ToString("N")[..8]);
    public EditHistoryTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    static JsonObject E(long rev, string? text = null) => new() { ["revision"] = rev, ["guide"] = text == null ? null : new JsonObject { ["title"] = text } };

    [Fact]
    public void Snapshot_IntervalDedupeAndForce()
    {
        var t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(EditHistory.MaybeSnapshot(_root, E(0), "x", force: true, t0));                     // nichts gespeichert
        Assert.True(EditHistory.MaybeSnapshot(_root, E(1), "open", force: false, t0));
        Assert.False(EditHistory.MaybeSnapshot(_root, E(1), "auto", force: true, t0.AddMinutes(1)));     // gleicher Stand
        Assert.False(EditHistory.MaybeSnapshot(_root, E(2), "auto", force: false, t0.AddMinutes(5)));    // zu früh
        Assert.True(EditHistory.MaybeSnapshot(_root, E(2), "auto", force: false, t0.AddMinutes(11)));
        Assert.True(EditHistory.MaybeSnapshot(_root, E(3), "replaceAll", force: true, t0.AddMinutes(12)));
        var list = EditHistory.List(_root);
        Assert.Equal(new long[] { 3, 2, 1 }, list.Select(s => s.Revision));
        Assert.Equal("replaceAll", list[0].Reason);
    }

    [Fact]
    public void Snapshot_RespectsCountAndSizeLimits()
    {
        var t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        for (int i = 1; i <= 45; i++) EditHistory.MaybeSnapshot(_root, E(i), "auto", force: true, t0.AddMinutes(i));
        var list = EditHistory.List(_root);
        Assert.Equal(EditHistory.MaxCount, list.Count);
        Assert.Equal(45, list[0].Revision);                                                              // die ältesten fliegen raus
        var big = new string('x', 1_000_000);
        for (int i = 100; i < 120; i++) EditHistory.MaybeSnapshot(_root, E(i, big), "auto", force: true, t0.AddHours(1).AddMinutes(i));
        Assert.True(EditHistory.List(_root).Sum(s => s.Bytes) <= EditHistory.MaxBytes);
    }

    [Fact]
    public void Restore_KeepsProtectionByDefault()
    {
        var current = new JsonObject { ["revision"] = 9, ["sessionId"] = "s", ["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1" }), ["review"] = new JsonObject { ["inputs"] = new JsonArray() }, ["guide"] = new JsonObject { ["title"] = "Neu" } };
        var old = new JsonObject { ["revision"] = 3, ["sessionId"] = "s", ["guide"] = new JsonObject { ["title"] = "Alt" }, ["annotations"] = new JsonArray(new JsonObject { ["id"] = "a1" }) };
        var keep = EditHistory.Restore(current, old);
        Assert.Equal("Alt", keep.Obj("guide")!.Str("title"));
        Assert.Equal(9, keep.Long("revision"));
        Assert.Single(keep.Arr("redactions")!);
        Assert.NotNull(keep.Obj("review"));
        Assert.Single(keep.Arr("annotations")!);
        var raw = EditHistory.Restore(current, old, keepProtection: false);
        Assert.Null(raw["redactions"]);
        Assert.NotEmpty(EditHistory.Diff(current, keep));
        Assert.Contains("1", EditHistory.Summarize(keep));
    }

    [Fact]
    public void View_SnapshotsOnSave_RevisionMonotonic_PurgedByFinalRedaction()
    {
        var a = SyntheticSession.Create(Path.Combine(_root, "a"));
        long lastRev;
        using (var v = SessionView.Open(a))
        {
            v.Edits["guide"] = new JsonObject { ["title"] = "Eins" }; Assert.True(v.SaveEdits());   // auf der Platte noch nichts → kein Stand
            v.Edits["guide"] = new JsonObject { ["title"] = "Zwei" }; Assert.True(v.SaveEdits());   // sichert Revision 1
            v.SnapshotBefore = "replaceAll";
            v.Edits["guide"] = new JsonObject { ["title"] = "Drei" }; Assert.True(v.SaveEdits());   // erzwungen: Revision 2
            lastRev = v.Edits.Long("revision");
        }
        var list = EditHistory.List(a);
        Assert.Equal(new long[] { 2, 1 }, list.Select(s => s.Revision));
        // Wiederherstellen ist ein normales Speichern: Revision steigt weiter
        using (var v = SessionView.Open(a))
        {
            var restored = EditHistory.Restore(v.Edits, EditHistory.Load(list[^1])!);
            foreach (var k in v.Edits.Select(x => x.Key).ToList()) v.Edits.Remove(k);
            foreach (var kv in restored.ToList()) { restored.Remove(kv.Key); v.Edits[kv.Key] = kv.Value; }
            Assert.True(v.SaveEdits());
            Assert.True(v.Edits.Long("revision") > lastRev);
            Assert.Equal("Eins", v.Title);
        }
        Assert.Equal(3, EditHistory.List(a).Count);                                               // erstes Speichern nach dem Öffnen
        using (var v = SessionView.Open(a)) Redactor.ApplyTextRedactionsDestructive(a, v.Edits);
        Assert.Empty(EditHistory.List(a));
        Assert.False(Directory.Exists(Path.Combine(a, "history")));
    }
}
