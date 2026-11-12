using System.Text.Json.Nodes;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

public class CleanupTests
{
    static long _t;
    static ViewStep S(string kind, string? el = "Text-Editor", JsonObject? input = null, long anchor = 0)
    {
        _t += 1_000_000;
        var j = new JsonObject { ["kind"] = kind, ["ctx"] = new JsonObject { ["win"] = "w1" }, ["input"] = input ?? new JsonObject() };
        if (el != null) j["target"] = new JsonObject { ["el"] = new JsonObject { ["name"] = el, ["cls"] = "RichEdit" } };
        return new ViewStep { Json = j, Kind = kind, Id = "s" + _t, Anchor = anchor == 0 ? _t : anchor, TFrom = _t, TTo = _t + 100_000 };
    }

    [Fact]
    public void Suggests_FocusClick_RepeatClick_RoundTrip_EmptyInput()
    {
        var steps = new List<ViewStep>
        {
            S("click"), S("type", input: new JsonObject { ["text"] = "Hallo" }),
            S("click", "OK"), S("click", "OK"),
            S("windowSwitch", null, new JsonObject { ["from"] = "w1", ["to"] = "w2" }), S("windowSwitch", null, new JsonObject { ["from"] = "w2", ["to"] = "w1" }),
            S("type", input: new JsonObject { ["text"] = "" }),
        };
        var sug = StepCleanup.Suggest(steps);
        Assert.Equal(new[] { "focusClick", "repeatClick", "windowRoundTrip", "windowRoundTrip", "emptyInput" }, sug.Select(x => x.Rule));
        Assert.Same(steps[0], sug[0].Step);
        Assert.Same(steps[3], sug[1].Step);   // der zweite Klick wird ausgeblendet, der erste bleibt
    }

    [Fact]
    public void KeepsMeaningfulSteps()
    {
        var steps = new List<ViewStep>
        {
            S("click", "Datei"), S("click", "Speichern"),                              // verschiedene Elemente
            S("click", "Name"), S("hotkey", null), S("type", "Name", new JsonObject { ["text"] = "x" }),   // nicht direkt vor der Eingabe
            S("type", input: new JsonObject { ["masked"] = true }),                    // Passwort: nie ausblenden
        };
        Assert.Empty(StepCleanup.Suggest(steps));
    }

    [Fact]
    public void Apply_HidesWithReason()
    {
        var a = S("click"); var b = S("type", input: new JsonObject { ["text"] = "x" });
        var edits = new JsonObject();
        StepCleanup.Apply(edits, StepCleanup.Suggest(new[] { a, b }));
        var se = (JsonObject)edits["steps"]![0]!;
        Assert.Equal(a.Anchor, (long)se["anchor"]!);
        Assert.True((bool)se["hidden"]!);
        Assert.Equal("focusClick", (string)se["cleanup"]!["rule"]!);
    }
}
