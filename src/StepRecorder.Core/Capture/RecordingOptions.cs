using System.Drawing;
using System.Text.Json.Nodes;

namespace StepRecorder.Capture;

public enum ScopeMode { Screen, App, Region }

/// <summary>Wirksame Einstellungen einer Aufnahme (Startoption &gt; Profil &gt; Einstellungen).</summary>
public sealed class RecordingOptions
{
    public string Title { get; set; } = "";
    public string Language { get; set; } = "de";
    public ScopeMode Scope { get; set; } = ScopeMode.Screen;
    /// <summary>"active", "all" oder Monitor-Ids (M1, M2 …).</summary>
    public string Monitors { get; set; } = "all";
    public string? AppProcess { get; set; }
    public int? AppPid { get; set; }
    public nint AppWindow { get; set; }
    public bool IncludeRelated { get; set; } = true;
    /// <summary>Rechteck in virtuellen Bildschirmkoordinaten.</summary>
    public Rectangle? Region { get; set; }
    public string PrivacyLevel { get; set; } = "balanced";
    public bool ClipboardContent { get; set; }
    public List<string> ExcludedProcesses { get; set; } = new(DefaultExcluded);
    public string ChangeFrames { get; set; } = "normal"; // few | normal | many
    /// <summary>Erweiterter Programmkontext (Menüpfade, Listen, Explorer, Browser, Office, Statusleiste, MSAA).</summary>
    public bool AppContext { get; set; } = true;
    /// <summary>Name des Detailgrads für Protokoll und Anzeige (Schlank, Ausführlich, Vollständig, Eigene).</summary>
    public string DetailLevel { get; set; } = "";
    public string? GoalDescription { get; set; }
    public string? GoalExpected { get; set; }
    public string? GoalReference { get; set; }
    /// <summary>Nur Oberfläche: der Bereich passt nicht mehr genau und muss vor dem Start bestätigt werden.</summary>
    public bool RegionNeedsConfirm { get; set; }
    public string ProfileName { get; set; } = "Anleitung";
    public string ProfileId { get; set; } = "guide";
    public string AfterStop { get; set; } = "viewer";
    public string OutputFolder { get; set; } = "";
    public int SettleQuietMs { get; set; } = 300;
    public int SettleMaxMs { get; set; } = 3000;
    public int TypingFrameMinGapMs { get; set; } = 250;
    public int MaxDeltaChain { get; set; } = 30;
    public double KeyframeChangeRatio { get; set; } = 0.4;
    public int ValueMaxChars { get; set; } = 2000;
    public int SnapshotMaxNodes { get; set; } = 300;
    public int HoldMaxMs { get; set; } = 1500;
    /// <summary>Erinnerung „Aufnahme läuft noch“ nach so vielen Minuten ohne Eingabe (0 = aus).</summary>
    public int IdleReminderMinutes { get; set; } = 10;
    /// <summary>Erinnerung „Privattaste noch an“ nach so vielen Sekunden ohne Taste.</summary>
    public int PrivacyReminderSeconds { get; set; } = 60;
    /// <summary>Wer das Ziel formuliert hat: author (Person) | agent (über MCP angefragt).</summary>
    public string GoalStatedBy { get; set; } = "author";
    /// <summary>Hotkeys des Recorders als (Modifier, VK) – Tasten werden als <c>self</c> markiert.</summary>
    public List<(uint mods, uint vk)> SelfHotkeys { get; set; } = new();
    /// <summary>Abgeschaltete Eingabearten (Schlüssel aus <see cref="Channels"/>); deren Ereignisse werden nicht gespeichert.</summary>
    public HashSet<string> Off { get; set; } = new();
    public bool Rec(string channel) => !Off.Contains(channel);

    /// <summary>Einstellbare Eingabearten: Schlüssel, Name, Erklärung.</summary>
    public static readonly (string key, string name, string hint)[] Channels =
    {
        ("clicks", "Mausklicks", "Links-, Rechts- und Doppelklicks – mit Bild und angeklicktem Element"),
        ("wheel", "Scrollen", "Mausrad und Scrollen mit dem Touchpad"),
        ("moves", "Mausbewegungen", "Mauswege und Verweilen über Elementen (Zeigen); Zugwege beim Ziehen bleiben erhalten"),
        ("typing", "Texteingaben", "Getippte Zeichen samt Korrekturen; Passwortfelder werden immer maskiert"),
        ("hotkeys", "Tastenkombinationen", "Mit Strg, Alt oder Win, z. B. Strg+C, Alt+Tab, Win+E"),
        ("keys", "Einzeltasten", "Eingabe, Tab, Esc, Pfeiltasten, F-Tasten und ähnliche"),
    };

    public static string ChannelName(string key) => Channels.FirstOrDefault(c => c.key == key).name ?? key;

    public static readonly string[] DefaultExcluded =
    {
        "KeePass.exe", "KeePassXC.exe", "1Password.exe", "Bitwarden.exe", "Dashlane.exe", "LastPass.exe",
        "Enpass.exe", "RoboForm.exe", "PasswordSafe.exe", "pwsafe.exe", "NordPass.exe", "Keeper.exe", "CredentialUIBroker.exe"
    };

    public JsonObject ScopeJson(string reason = "initial")
    {
        var o = new JsonObject { ["mode"] = Scope.ToString().ToLowerInvariant() };
        switch (Scope)
        {
            case ScopeMode.Screen:
                o["monitors"] = Monitors is "active" or "all" ? Monitors : new JsonArray(Monitors.Split(',').Select(s => (JsonNode)s.Trim()).ToArray());
                break;
            case ScopeMode.App:
                var app = new JsonObject { ["process"] = AppProcess ?? "", ["match"] = AppWindow != 0 ? "window" : "process" };
                if (AppPid != null) app["pid"] = AppPid;
                o["apps"] = new JsonArray(app);
                o["includeRelated"] = IncludeRelated;
                break;
            case ScopeMode.Region:
                break;
        }
        o["reason"] = reason;
        return o;
    }

    public JsonObject SettingsJson(JsonObject scope) => new()
    {
        ["scope"] = scope.DeepClone(),
        ["privacy"] = new JsonObject
        {
            ["level"] = PrivacyLevel, ["clipboardContent"] = ClipboardContent,
            ["excluded"] = new JsonArray(ExcludedProcesses.Select(p => (JsonNode)new JsonObject { ["process"] = p }).ToArray()),
            ["recordIdentity"] = false, ["maskPasswordFieldsInFrames"] = true, ["storeMaskedCount"] = true, ["privacyModeSuspendsFrames"] = false
        },
        ["capture"] = new JsonObject
        {
            ["backend"] = "gdi", ["maxDeltaChain"] = MaxDeltaChain, ["keyframeChangeRatio"] = KeyframeChangeRatio,
            ["settleQuietMs"] = SettleQuietMs, ["settleMaxMs"] = SettleMaxMs, ["typingFrameMinGapMs"] = TypingFrameMinGapMs,
            ["valueMaxChars"] = ValueMaxChars, ["snapshotMaxNodes"] = SnapshotMaxNodes, ["changeFrames"] = ChangeFrames, ["appContext"] = AppContext
        },
        ["input"] = new JsonObject(Channels.Select(c => new KeyValuePair<string, JsonNode?>(c.key, Rec(c.key)))),
        ["language"] = Language
    };

    public JsonObject? GoalJson()
    {
        var g = new JsonObject();
        if (!string.IsNullOrWhiteSpace(GoalDescription)) g["description"] = Trunc(GoalDescription.Trim(), 2000);
        if (!string.IsNullOrWhiteSpace(GoalExpected)) g["expected"] = Trunc(GoalExpected.Trim(), 2000);
        if (!string.IsNullOrWhiteSpace(GoalReference)) g["reference"] = Trunc(GoalReference.Replace("\r", " ").Replace("\n", " ").Trim(), 200);
        return g.Count == 0 ? null : g;
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];

    public JsonObject ProfileJson() => new()
    {
        ["name"] = ProfileName, ["id"] = ProfileId, ["builtIn"] = ProfileId is "guide" or "support" or "aiTrace",
        ["effective"] = new JsonObject
        {
            ["scopeMode"] = Scope.ToString().ToLowerInvariant(), ["privacyLevel"] = PrivacyLevel, ["clipboardContent"] = ClipboardContent,
            ["afterStop"] = new JsonObject { ["action"] = AfterStop }, ["showGoalFields"] = ProfileId != "guide",
            ["detail"] = new JsonObject
            {
                ["level"] = DetailLevel, ["changeFrames"] = ChangeFrames, ["appContext"] = AppContext,
                ["off"] = new JsonArray(Off.OrderBy(x => x).Select(x => (JsonNode)x).ToArray())
            }
        }
    };
}
