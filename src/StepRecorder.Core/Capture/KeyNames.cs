namespace StepRecorder.Capture;

/// <summary>Kanonische Tastennamen (04 §key.*) und Klassifikation.</summary>
public static class KeyNames
{
    public static string Name(uint vk, bool ext)
    {
        switch (vk)
        {
            case >= 0x41 and <= 0x5A: return ((char)vk).ToString();
            case >= 0x30 and <= 0x39: return ((char)vk).ToString();
            case >= 0x70 and <= 0x87: return "F" + (vk - 0x6F);
            case >= 0x60 and <= 0x69: return "NumPad" + (vk - 0x60);
            case 0x0D: return ext ? "NumPadEnter" : "Enter";
            case 0x09: return "Tab";
            case 0x20: return "Space";
            case 0x08: return "Backspace";
            case 0x1B: return "Escape";
            case 0x2D: return "Insert";
            case 0x2E: return "Delete";
            case 0x24: return "Home";
            case 0x23: return "End";
            case 0x21: return "PageUp";
            case 0x22: return "PageDown";
            case 0x25: return "Left";
            case 0x27: return "Right";
            case 0x26: return "Up";
            case 0x28: return "Down";
            case 0x2C: return "PrintScreen";
            case 0x13: return "Pause";
            case 0x5D: return "Apps";
            case 0xA0: return "LeftShift";
            case 0xA1: return "RightShift";
            case 0xA2: return "LeftCtrl";
            case 0xA3: return "RightCtrl";
            case 0xA4: return "LeftAlt";
            case 0xA5: return "RightAlt";
            case 0x10: return "LeftShift";
            case 0x11: return ext ? "RightCtrl" : "LeftCtrl";
            case 0x12: return ext ? "RightAlt" : "LeftAlt";
            case 0x5B: return "LeftWin";
            case 0x5C: return "RightWin";
            case 0x14: return "CapsLock";
            case 0x90: return "NumLock";
            case 0x91: return "ScrollLock";
            case 0x6B: return "NumPadAdd";
            case 0x6D: return "NumPadSubtract";
            case 0x6A: return "NumPadMultiply";
            case 0x6F: return "NumPadDivide";
            case 0x6E: return "NumPadDecimal";
            case 0xBA: return "Oem1";
            case 0xBF: return "Oem2";
            case 0xC0: return "Oem3";
            case 0xDB: return "Oem4";
            case 0xDC: return "Oem5";
            case 0xDD: return "Oem6";
            case 0xDE: return "Oem7";
            case 0xDF: return "Oem8";
            case 0xE2: return "Oem102";
            case 0xBB: return "OemPlus";
            case 0xBC: return "OemComma";
            case 0xBD: return "OemMinus";
            case 0xBE: return "OemPeriod";
            case 0xAF: return "VolumeUp";
            case 0xAE: return "VolumeDown";
            case 0xAD: return "VolumeMute";
            case 0xB3: return "MediaPlayPause";
            case 0xB0: return "MediaNext";
            case 0xB1: return "MediaPrev";
            case 0xB2: return "MediaStop";
            case 0xA6: return "BrowserBack";
            case 0xA7: return "BrowserForward";
            case 0xA8: return "BrowserRefresh";
            case 0xAC: return "BrowserHome";
            case 0xAA: return "BrowserSearch";
            default: return "Vk" + vk.ToString("X2");
        }
    }

    public static bool IsModifier(string key) => key is "LeftShift" or "RightShift" or "LeftCtrl" or "RightCtrl" or "LeftAlt" or "RightAlt" or "LeftWin" or "RightWin";
    public static bool IsLock(string key) => key is "CapsLock" or "NumLock" or "ScrollLock";
    public static bool IsEditKey(string key) => key is "Backspace" or "Delete" or "Left" or "Right" or "Up" or "Down" or "Home" or "End";

    /// <summary>
    /// Eingabeart einer gedrückten Taste für die Aufnahme-Einstellung: "typing", "hotkeys", "keys" oder für
    /// Umschalter "shift" (Großschreibung und Kombinationen), "altGr" (Sonderzeichen und Kombinationen), "modifier" (Strg/Alt/Win).
    /// </summary>
    public static string Channel(string key, bool combo, string? ctx)
    {
        if (IsLock(key)) return "typing";
        if (key is "LeftShift" or "RightShift") return "shift";
        if (key == "RightAlt") return "altGr";
        if (IsModifier(key)) return "modifier";
        if (combo) return "hotkeys";
        if (key is "Backspace" or "Delete") return ctx == "textField" ? "typing" : "keys";
        if (key == "Space" || key.Length == 1 || key.StartsWith("Oem", StringComparison.Ordinal) || key.StartsWith("NumPad", StringComparison.Ordinal) && key != "NumPadEnter") return "typing";
        return "keys";
    }

    /// <summary>Modifier-Kurzname für <c>mods</c>.</summary>
    public static string? ModOf(string key) => key switch
    {
        "LeftCtrl" or "RightCtrl" => "ctrl",
        "LeftAlt" or "RightAlt" => "alt",
        "LeftShift" or "RightShift" => "shift",
        "LeftWin" or "RightWin" => "win",
        _ => null
    };
}
