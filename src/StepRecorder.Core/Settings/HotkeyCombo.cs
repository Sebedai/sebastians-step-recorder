namespace StepRecorder.Settings;

/// <summary>Globale Tastenkombination (Modifier nach RegisterHotKey: Alt 1, Strg 2, Umschalt 4, Win 8).</summary>
public readonly record struct HotkeyCombo(uint Mods, uint Vk)
{
    public const uint Alt = 1, Ctrl = 2, Shift = 4, Win = 8;

    public bool IsEmpty => Vk == 0;

    /// <summary>Liest „Ctrl+Shift+F9“, „Strg+Umschalt+F9“, „Alt+Pause“ …</summary>
    public static bool TryParse(string? text, out HotkeyCombo combo)
    {
        combo = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        uint mods = 0, vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = raw.ToLowerInvariant();
            switch (p)
            {
                case "ctrl" or "strg" or "control": mods |= Ctrl; continue;
                case "shift" or "umschalt" or "umsch": mods |= Shift; continue;
                case "alt": mods |= Alt; continue;
                case "win" or "windows": mods |= Win; continue;
            }
            if (vk != 0) return false;
            vk = KeyToVk(p);
            if (vk == 0) return false;
        }
        if (vk == 0) return false;
        combo = new HotkeyCombo(mods, vk);
        return true;
    }

    static uint KeyToVk(string p)
    {
        if (p.Length > 1 && p[0] == 'f' && int.TryParse(p[1..], out int f) && f is >= 1 and <= 24) return (uint)(0x70 + f - 1);
        if (p.Length == 1 && char.IsAsciiLetterOrDigit(p[0])) return char.ToUpperInvariant(p[0]);
        return p switch
        {
            "pause" => 0x13, "scroll" or "rollen" => 0x91, "space" or "leertaste" => 0x20,
            "ins" or "insert" or "einfg" => 0x2D, "del" or "delete" or "entf" => 0x2E,
            "home" or "pos1" => 0x24, "end" or "ende" => 0x23, "pageup" or "bildauf" => 0x21, "pagedown" or "bildab" => 0x22,
            "up" => 0x26, "down" => 0x28, "left" => 0x25, "right" => 0x27,
            "numpad0" => 0x60, "numpad1" => 0x61, "numpad2" => 0x62, "numpad3" => 0x63, "numpad4" => 0x64,
            "numpad5" => 0x65, "numpad6" => 0x66, "numpad7" => 0x67, "numpad8" => 0x68, "numpad9" => 0x69,
            _ => 0
        };
    }

    static string VkName(uint vk) => vk switch
    {
        >= 0x70 and <= 0x87 => "F" + (vk - 0x70 + 1),
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        0x13 => "Pause", 0x91 => "Scroll", 0x20 => "Space", 0x2D => "Ins", 0x2E => "Del", 0x24 => "Home", 0x23 => "End",
        0x21 => "PageUp", 0x22 => "PageDown", 0x26 => "Up", 0x28 => "Down", 0x25 => "Left", 0x27 => "Right",
        >= 0x60 and <= 0x69 => "NumPad" + (vk - 0x60),
        _ => "VK" + vk.ToString("X2")
    };

    /// <summary>Kanonische Schreibweise für die Einstellungsdatei.</summary>
    public override string ToString()
    {
        if (IsEmpty) return "";
        var parts = new List<string>();
        if ((Mods & Ctrl) != 0) parts.Add("Ctrl");
        if ((Mods & Shift) != 0) parts.Add("Shift");
        if ((Mods & Alt) != 0) parts.Add("Alt");
        if ((Mods & Win) != 0) parts.Add("Win");
        parts.Add(VkName(Vk));
        return string.Join("+", parts);
    }

    /// <summary>Anzeige in der Oberflächensprache („Strg+Umschalt+F9“).</summary>
    public string Display() => Loc.Keys(ToString());

    /// <summary>Kombinationen ohne Modifier sind nur für Tasten erlaubt, die beim Arbeiten nicht vorkommen.</summary>
    public bool NeedsNoModifier => Vk is 0x13 or 0x91 || Vk is >= 0x7C and <= 0x87;
}

/// <summary>Die eine Tabelle der globalen Aufnahme-Tastenkürzel (Registrierung, Menüs, Hilfe, Editor).</summary>
public static class HotkeyActions
{
    public sealed record Action(string Id, string De, string En, string Default, bool Required);

    public static readonly Action[] All =
    {
        new("instantPause", "Sofort aufnehmen · Pause / Fortsetzen", "Record now · pause / resume", "Ctrl+Shift+F9", false),
        new("stop", "Aufnahme beenden und speichern", "Stop and save recording", "Ctrl+Shift+F10", true),
        new("markerError", "Fehlerstelle markieren", "Mark error", "Ctrl+Shift+F1", false),
        new("markerExpected", "Marker „Erwartet“", "Marker \"expected\"", "Ctrl+Shift+F2", false),
        new("marker", "Marker setzen", "Set marker", "Ctrl+Shift+F7", false),
        new("comment", "Kommentar eingeben", "Enter comment", "Ctrl+Shift+F12", false),
        new("dictation", "Diktat ein/aus (Mikrofon, offline)", "Dictation on/off (microphone, offline)", "Ctrl+Shift+F5", false),
        new("snapshot", "Schnappschuss", "Snapshot", "Ctrl+Shift+F11", false),
        new("privacy", "Privattaste (Eingaben nur zählen)", "Privacy key (count input only)", "Ctrl+Shift+F8", false),
        new("discard", "Letzten Schritt verwerfen", "Discard last step", "Ctrl+Shift+F6", false),
        new("bar", "Leiste ein-/ausblenden", "Show/hide bar", "Ctrl+Shift+F4", false),
    };

    public static Action? ById(string id) => All.FirstOrDefault(a => a.Id == id);
    public static string Label(Action a) => Loc.L(a.De, a.En);
    public static Dictionary<string, string> Defaults() => All.ToDictionary(a => a.Id, a => a.Default);
}

public sealed record HotkeyIssue(string Id, string Kind, string De, string En)
{
    public string Text => Loc.L(De, En);
}

/// <summary>Regeln für eigene Belegungen (07 §8.4): Modifier, keine Doppelbelegung, Stopp ist Pflicht, reservierte Kombinationen.</summary>
public static class HotkeyRules
{
    static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ctrl+Alt+Del", "Ctrl+Shift+Esc", "Alt+F4", "Alt+Tab", "Ctrl+Esc", "Win+L", "Ctrl+C", "Ctrl+V", "Ctrl+X", "Ctrl+Z", "Ctrl+Y", "Ctrl+S", "Ctrl+A", "Ctrl+F",
    };

    public static List<HotkeyIssue> Validate(IReadOnlyDictionary<string, string> map)
    {
        var issues = new List<HotkeyIssue>();
        var seen = new Dictionary<HotkeyCombo, string>();
        foreach (var a in HotkeyActions.All)
        {
            map.TryGetValue(a.Id, out var text);
            if (string.IsNullOrWhiteSpace(text))
            {
                if (a.Required) issues.Add(new(a.Id, "required", "Für „Aufnahme beenden“ ist ein Tastenkürzel Pflicht.", "\"Stop recording\" needs a shortcut."));
                continue;
            }
            if (!HotkeyCombo.TryParse(text, out var c)) { issues.Add(new(a.Id, "invalid", $"„{text}“ ist keine gültige Kombination.", $"\"{text}\" is not a valid combination.")); continue; }
            if (c.Mods == 0 && !c.NeedsNoModifier) issues.Add(new(a.Id, "noModifier", "Bitte mit Strg, Alt, Umschalt oder Win kombinieren.", "Please combine with Ctrl, Alt, Shift or Win."));
            if (Reserved.Contains(c.ToString())) issues.Add(new(a.Id, "reserved", $"{c.Display()} ist von Windows oder für Standardbefehle belegt.", $"{c.Display()} is reserved by Windows or common commands."));
            if (seen.TryGetValue(c, out var other)) issues.Add(new(a.Id, "duplicate", $"{c.Display()} ist schon „{HotkeyActions.Label(HotkeyActions.ById(other)!)}“ zugeordnet.", $"{c.Display()} is already used for \"{HotkeyActions.Label(HotkeyActions.ById(other)!)}\"."));
            else seen[c] = a.Id;
        }
        return issues;
    }
}
