using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;
using StepRecorder.Steps;

namespace StepRecorder.Tests;

public class StepBuilderTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create(withPassword: true);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    JsonObject Build() { using var s = Session.Open(_folder); return StepBuilder.Build(s, "2026-01-01T00:00:00.000000Z"); }

    [Fact]
    public void Bookkeeping_AssignsEveryInputExactlyOnce()
    {
        var steps = Build();
        var bk = steps.Obj("completeness").Obj("bookkeeping")!;
        Assert.Equal(0, bk.Long("orphans"));
        Assert.True(bk.Long("inputEvents") > 10);
        Assert.Equal(0, bk.Obj("timeline").Long("unaccountedUs"));
        Assert.True(bk.Bool("ok"));
    }

    [Fact]
    public void Steps_ClickTypeMaskedGapMarker()
    {
        var steps = Build().Arr("steps")!.Select(s => s!.AsObject()).ToList();
        var kinds = steps.Select(s => s.Str("kind")).ToList();
        var click = steps.First(s => s.Str("kind") == "click");
        Assert.Equal("agreed", click.Obj("target").Str("status"));
        Assert.Equal("confirmed", click.Obj("evidence").Str("level"));
        Assert.Equal("f000002", click.Obj("frames").Str("pre"));
        Assert.Equal("f000003", click.Obj("frames").Str("post"));
        Assert.NotNull(click.Obj("effect").Arr("windowsOpened"));
        var type = steps.First(s => s.Str("kind") == "type" && !s.Obj("input").Bool("masked"));
        Assert.Equal("notiz.txt", type.Obj("input").Str("text"));
        Assert.Equal("Enter", type.Obj("input").Obj("terminator").Str("key"));
        Assert.Equal("valueConfirmed", type.Obj("evidence").Str("result"));
        Assert.Equal("confirmed", type.Obj("evidence").Str("level"));
        var masked = steps.First(s => s.Str("kind") == "type" && s.Obj("input").Bool("masked"));
        Assert.Equal("unknown", masked.Obj("evidence").Str("level"));
        Assert.Contains("gap", kinds);
        Assert.Contains("marker", kinds);
    }

    [Fact]
    public void Derivation_IsDeterministic()
    {
        var a = Json.Line(Build()); var b = Json.Line(Build());
        Assert.Equal(a, b);
    }

    [Fact]
    public void Templates_ProduceGermanGuideText()
    {
        using var v = SessionView.Open(_folder);
        var click = v.All.First(s => s.Kind == "click");
        Assert.Equal("Wählen Sie den Menüpunkt „Datei“.", click.Guide);
        var type = v.All.First(s => s.Kind == "type" && !s.Json.Obj("input").Bool("masked"));
        Assert.Contains("„notiz.txt“", type.Guide);
        Assert.Contains("Eingabetaste", type.Guide);
    }

    [Fact]
    public void TextRedaction_HidesValueEverywhere()
    {
        using (var v = SessionView.Open(_folder))
        {
            var valueEv = v.Session.Events.First(e => e.Str("type") == "ui.value" && e.Str("value") == "notiz.txt");
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "text", ["targets"] = new JsonArray(new JsonObject { ["seq"] = valueEv.Long("seq"), ["field"] = "value" }), ["createdUtc"] = Json.UtcNow(), ["by"] = "user" });
            Assert.True(v.SaveEdits());
        }
        using var v2 = SessionView.Open(_folder, headless: true);
        foreach (var s in v2.All)
        {
            Assert.DoesNotContain("notiz.txt", s.Guide + s.Protocol + s.Title + Json.Line(s.Json));
        }
    }
}

public class HotkeyTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create(withHotkey: true);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    [Fact]
    public void CtrlDigit_IsNamedWithDigit()
    {
        using var v = SessionView.Open(_folder);
        var hk = v.All.First(s => s.Kind == "hotkey");
        Assert.Equal("Ctrl+1", hk.Json.Obj("input").Str("combo"));
        Assert.Contains("Strg+1", hk.Title + " | " + hk.Guide + " | " + hk.Protocol);
        Assert.DoesNotContain("Eingabe", hk.Title + " | " + hk.Guide + " | " + hk.Protocol);
    }
}

public class ImageEditTests : IDisposable
{
    readonly string _folder = SyntheticSession.Create();
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

    [Fact]
    public void Crop_AppliesToGuideImages_NotToAiImages()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var click = v.All.First(s => s.Kind == "click");
        v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = click.Anchor, ["crop"] = new JsonArray(20, 10, 120, 60) });
        using var guide = StepRecorder.Export.StepImages.Render(v, click, "before", "original");
        Assert.Equal(120, guide!.Bitmap.Width); Assert.Equal(60, guide.Bitmap.Height);
        using var ai = StepRecorder.Export.StepImages.Render(v, click, "before", "original", respectMarks: false);
        Assert.Equal(200, ai!.Bitmap.Width);
    }

    [Fact]
    public void PixelateRedaction_ChangesPixels_AndIsNotBlack()
    {
        using var v = SessionView.Open(_folder, headless: true);
        var click = v.All.First(s => s.Kind == "click");
        using var before = v.Frame(click.PreFrame)!;
        var orig = before.GetPixel(60, 35);
        v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "image", ["style"] = "pixelate", ["areas"] = new JsonArray(new JsonObject { ["m"] = "M1", ["from"] = click.PreFrame, ["to"] = null, ["rect"] = new JsonArray(40, 20, 80, 40) }) });
        v.Rebuild();
        using var after = v.Frame(click.PreFrame)!;
        var px = after.GetPixel(41, 21);
        Assert.False(px.R == 0 && px.G == 0 && px.B == 0);                 // nicht schwarz
        int changed = 0;
        for (int y = 20; y < 60; y++) for (int x = 40; x < 120; x++) if (before.GetPixel(x, y).ToArgb() != after.GetPixel(x, y).ToArgb()) changed++;
        Assert.True(changed > 100, $"nur {changed} Pixel verändert");    // verpixelt (Mischfarben an den Kanten)
        Assert.Equal(before.GetPixel(5, 5), after.GetPixel(5, 5));          // außerhalb unverändert
        _ = orig;
    }
}

public class KeyCapsTests
{
    [Theory]
    [InlineData("LeftCtrl", true, "Strg")]
    [InlineData("Enter", true, "Eingabe")]
    [InlineData("Enter", false, "Enter")]
    [InlineData("Oem7", true, "Ä")]
    [InlineData("NumPad5", true, "Num 5")]
    [InlineData("F5", true, "F5")]
    [InlineData("Down", true, "↓")]
    public void Labels(string key, bool de, string expected) => Assert.Equal(expected, StepRecorder.Export.KeyCaps.Label(key, de));

    [Fact]
    public void Marks_KeyCapsDefaultOn_AndMaskable()
    {
        Assert.True(MouseMarks.Default.KeyCaps);
        var m = MouseMarks.From(new JsonObject { ["keyCaps"] = false }, MouseMarks.Default);
        Assert.False(m.KeyCaps);
        Assert.True(m.Click);
        Assert.False(MouseMarks.Default.And(MouseMarks.Mask(true, false)).KeyCaps);
        Assert.False(MouseMarks.Default.And(MouseMarks.Mask(false, true)).Click);
    }
}

public class RecordChannelTests
{
    [Theory]
    [InlineData("A", false, "textField", "typing")]
    [InlineData("1", false, "nonText", "typing")]
    [InlineData("OemComma", false, "textField", "typing")]
    [InlineData("Space", false, "textField", "typing")]
    [InlineData("Backspace", false, "textField", "typing")]
    [InlineData("Delete", false, "nonText", "keys")]
    [InlineData("Enter", false, "textField", "keys")]
    [InlineData("Escape", false, "unknown", "keys")]
    [InlineData("F5", false, "unknown", "keys")]
    [InlineData("Down", false, "textField", "keys")]
    [InlineData("1", true, "textField", "hotkeys")]
    [InlineData("C", true, "textField", "hotkeys")]
    [InlineData("LeftShift", false, "textField", "shift")]
    [InlineData("RightAlt", false, "textField", "altGr")]
    [InlineData("LeftCtrl", false, "textField", "modifier")]
    [InlineData("CapsLock", false, "textField", "typing")]
    public void KeyChannel(string key, bool combo, string ctx, string expected) => Assert.Equal(expected, StepRecorder.Capture.KeyNames.Channel(key, combo, ctx));

    [Fact]
    public void SettingsJson_ListsChannels()
    {
        var o = new StepRecorder.Capture.RecordingOptions();
        o.Off.Add("moves");
        var input = o.SettingsJson(o.ScopeJson()).Obj("input")!;
        Assert.False(input["moves"]!.GetValue<bool>());
        Assert.True(input["clicks"]!.GetValue<bool>());
        Assert.Equal(6, input.Count);
    }
}

public class PatternTests
{
    [Theory]
    [InlineData("sk-abcdefghijklmnopqrstuvwxyz", "keyOrToken")]
    [InlineData("DE89 3704 0044 0532 0130 00", "iban")]
    [InlineData("4111 1111 1111 1111", "cardNumber")]
    [InlineData("mysql -u root -psecret123", "passwordInCommand")]
    [InlineData("Hallo Welt", null)]
    public void Flags(string text, string? expected) => Assert.Equal(expected, Patterns.Flag(text));
}
