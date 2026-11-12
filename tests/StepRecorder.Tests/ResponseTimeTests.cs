using System.Text.Json.Nodes;
using StepRecorder.Compare;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Paket 9 des Entwickler-Fahrplans: Reaktionszeiten.</summary>
public class ResponseTimeTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-timing-" + Guid.NewGuid().ToString("N")[..8]);
    public ResponseTimeTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public void Timing_FromEffectAndSync()
    {
        var step = JsonNode.Parse("""{"effect":{"latencyUs":120000,"screen":{"settled":true}},"sync":{"settleMs":400,"readyMs":2400,"thinkMs":900,"busyMs":0}}""")!.AsObject();
        var t = Timing.Of(step)!;
        Assert.Equal(120, t["reactionMs"]!.GetValue<long>());
        Assert.Equal(2400, Timing.WaitMs(t));
        Assert.Null(t["atLeast"]);
        Assert.Equal("Reaktion 120 ms · bereit nach 2,4 s", Timing.Text(t, "de"));
        var cut = JsonNode.Parse("""{"sync":{"settleMs":3000,"settleReason":"maxWait"}}""")!.AsObject();
        Assert.Contains("mehr als", Timing.Text(Timing.Of(cut)!, "de"));
        Assert.Null(Timing.Of(new JsonObject()));
        var (wait, think, slow) = Timing.Totals(new[] { step, cut });
        Assert.Equal(5400, wait); Assert.Equal(900, think); Assert.Equal(2, slow);
    }

    [Fact]
    public void SlowStep_TraceLine_QualityHint_AndCompare()
    {
        var fast = SyntheticSession.Create(Path.Combine(_root, "fast"));
        var slow = SyntheticSession.Create(Path.Combine(_root, "slow"), enterSettleUs: 4_000_000);
        using var vf = SessionView.Open(fast, headless: true);
        using var vs = SessionView.Open(slow, headless: true);
        var enter = vs.All.First(s => s.Kind == "key" || (s.Kind == "type" && s.Json.Obj("frames")?.Str("post") == "f000005"));
        Assert.True(Timing.WaitMsOf(enter.Json) >= 3000, "Wartezeit nach der Eingabetaste");

        var trace = KiPackage.Trace(vs, vs.All, new(), "de", "standard");
        Assert.Contains("- Reaktion: ", trace);

        var q = QualityCheck.Run(vs, new QualityOptions { Importance = "all" });
        Assert.Contains(q, f => f.Rule == "Q21");
        Assert.DoesNotContain(QualityCheck.Run(vf, new QualityOptions { Importance = "all" }), f => f.Rule == "Q21");

        var r = StepCompare.Compare(vf, vs);
        Assert.Contains(r.Arr("pairs")!, p => p!["differences"]!.AsArray().Any(d => d!.GetValue<string>() == "response"));
        Assert.Contains("Reaktion ", StepCompare.Markdown(r, "de"));
    }
}
