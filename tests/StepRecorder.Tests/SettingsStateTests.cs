using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.Tests;

/// <summary>Einstellungen (Migration, Toleranz, unbekannte Schlüssel), Zustand, Tastenkürzel, Protokoll.</summary>
[Collection("Loc")]
public class SettingsStateTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "steprec-set-" + Guid.NewGuid().ToString("N")[..8]);
    public SettingsStateTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    string F(string name) => Path.Combine(_dir, name);

    [Fact]
    public void UnknownKeys_SurviveRoundTrip()
    {
        File.WriteAllText(F("s.json"), """{ "version": 2, "language": "en", "zukunftsSchalter": { "a": 1 }, "countdown": 3 }""");
        var s = AppSettings.Read(F("s.json"));
        Assert.Equal("en", s.Language);
        Assert.Equal(3, s.Countdown);
        Assert.True(s.Write(F("s2.json")));
        var o = Json.ReadObject(F("s2.json"))!;
        Assert.Equal(1, o["zukunftsSchalter"]!["a"]!.GetValue<int>());
    }

    [Fact]
    public void Migration_V1_CanonicalisesHotkeys()
    {
        File.WriteAllText(F("s.json"), """{ "version": 1, "hotkeys": { "stop": "Strg+Umschalt+F10", "bar": "Quatsch+", "comment": "" } }""");
        var s = AppSettings.Read(F("s.json"));
        Assert.Equal(AppSettings.CurrentVersion, s.Version);
        Assert.Equal("Ctrl+Shift+F10", s.Hotkey("stop"));
        Assert.Equal("Ctrl+Shift+F4", s.Hotkey("bar"));       // unlesbar → Standard
        Assert.Equal("", s.Hotkey("comment"));                // bewusst leer
        Assert.Equal("Ctrl+Shift+F11", s.Hotkey("snapshot")); // fehlt → Standard
    }

    [Fact]
    public void NewerVersion_IsNotLowered()
    {
        File.WriteAllText(F("s.json"), """{ "version": 9, "language": "de" }""");
        var s = AppSettings.Read(F("s.json"));
        Assert.Equal(9, s.Version);
        s.Write(F("s.json"));
        Assert.Equal(9, Json.ReadObject(F("s.json"))!["version"]!.GetValue<int>());
    }

    [Fact]
    public void SingleBadValue_DoesNotLoseTheRest()
    {
        File.WriteAllText(F("s.json"), """{ "version": 2, "language": "en", "countdown": "drei", "theme": "light" }""");
        var s = AppSettings.Read(F("s.json"));
        Assert.Equal("en", s.Language);
        Assert.Equal("light", s.Theme);
        Assert.Equal(0, s.Countdown);
        Assert.NotNull(s.LoadNotice);
    }

    [Fact]
    public void CorruptFile_IsQuarantined()
    {
        File.WriteAllText(F("s.json"), "{ das ist kein json");
        var s = AppSettings.Read(F("s.json"));
        Assert.Equal("de", s.Language);
        Assert.False(File.Exists(F("s.json")));
        Assert.Single(Directory.GetFiles(_dir, "s.json.defekt-*"));
    }

    [Fact]
    public void Save_ToUnwritablePath_ReturnsFalse()
    {
        var s = new AppSettings();
        Assert.False(s.Write(Path.Combine(_dir, "gibt-es-nicht", "x", "s.json")));
        Assert.NotNull(s.LastSaveError);
    }

    [Fact]
    public void AppState_RoundTrip_LegacyFile_AndLru()
    {
        File.WriteAllText(F("state.json"), """{ "lastStopped": "C:\\a", "atUtc": "2026-01-01T00:00:00Z" }""");
        var st = AppState.LoadFrom(F("state.json"));
        Assert.Equal("C:\\a", st.LastStopped);
        for (int i = 0; i < AppState.MaxSessions + 1; i++) { st.Session("s" + i).Step = i; Thread.Sleep(1); }
        Assert.Equal(AppState.MaxSessions, st.Sessions.Count);
        Assert.False(st.Sessions.ContainsKey("s0"));
        st.Windows["viewer"] = new WindowBounds { X = 10, Y = 20, W = 800, H = 600, Dpi = 144 };
        Assert.True(st.SaveTo(F("state2.json")));
        var back = AppState.LoadFrom(F("state2.json"));
        Assert.Equal(144, back.Windows["viewer"].Dpi);
        Assert.Equal(50, back.Sessions["s50"].Step);
        File.WriteAllText(F("bad.json"), "[1,2");
        Assert.Null(AppState.LoadFrom(F("bad.json")).LastStopped);
    }

    [Theory]
    [InlineData("Ctrl+Shift+F9", "Ctrl+Shift+F9")]
    [InlineData("Strg+Umschalt+F9", "Ctrl+Shift+F9")]
    [InlineData("alt+pause", "Alt+Pause")]
    [InlineData("Win+Strg+K", "Ctrl+Win+K")]
    public void HotkeyCombo_ParsesAndFormats(string input, string canonical)
    {
        Assert.True(HotkeyCombo.TryParse(input, out var c));
        Assert.Equal(canonical, c.ToString());
        Assert.True(HotkeyCombo.TryParse(c.ToString(), out var c2));
        Assert.Equal(c, c2);
    }

    [Fact]
    public void HotkeyRules_DetectProblems()
    {
        var map = HotkeyActions.Defaults();
        Assert.Empty(HotkeyRules.Validate(map));
        map["stop"] = "";
        Assert.Contains(HotkeyRules.Validate(map), i => i.Id == "stop" && i.Kind == "required");
        map = HotkeyActions.Defaults(); map["bar"] = map["stop"];
        Assert.Contains(HotkeyRules.Validate(map), i => i.Kind == "duplicate");
        map = HotkeyActions.Defaults(); map["marker"] = "K";
        Assert.Contains(HotkeyRules.Validate(map), i => i.Kind == "noModifier");
        map = HotkeyActions.Defaults(); map["marker"] = "Alt+F4";
        Assert.Contains(HotkeyRules.Validate(map), i => i.Kind == "reserved");
        map = HotkeyActions.Defaults(); map["marker"] = "Pause";
        Assert.Empty(HotkeyRules.Validate(map));
    }

    [Fact]
    public void AppLog_RotatesAndHidesArgumentValues()
    {
        AppLog.OverrideDir = _dir;
        try
        {
            Assert.DoesNotContain("Geheim", AppLog.ArgNames(new[] { "record", "--title", "Geheim", "--goal", "Geheimziel" }));
            Assert.Contains("--title", AppLog.ArgNames(new[] { "record", "--title", "Geheim" }));
            Assert.DoesNotContain("Sessions", AppLog.ArgNames(new[] { @"C:\Users\x\Sessions\2026" }));
            var big = new string('x', 10_000);
            for (int i = 0; i < 120; i++) AppLog.Write(big);
            Assert.True(new FileInfo(AppLog.FilePath).Length <= AppLog.MaxBytes + 20_000);
            Assert.True(File.Exists(AppLog.FilePath + ".1"));
        }
        finally { AppLog.OverrideDir = null; }
    }
}
