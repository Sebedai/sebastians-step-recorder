using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Capture;

/// <summary>Umgebungsdaten für <c>system/environment</c> und das Manifest (ohne Rechner- oder Benutzernamen).</summary>
public static class EnvironmentInfo
{
    public static JsonObject Collect(Topology topo)
    {
        string product = "Windows", display = "";
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            product = k?.GetValue("ProductName") as string ?? product;
            display = k?.GetValue("DisplayVersion") as string ?? "";
            if (Environment.OSVersion.Version.Build >= 22000) product = product.Replace("Windows 10", "Windows 11");
        }
        catch { }
        nint hkl = GetKeyboardLayout(0);
        int lowLevelTimeout = 0;
        try { using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"); lowLevelTimeout = Convert.ToInt32(k?.GetValue("LowLevelHooksTimeout") ?? 0); } catch { }
        string theme = "light";
        try { using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); theme = (k?.GetValue("AppsUseLightTheme") as int?) == 0 ? "dark" : "light"; } catch { }
        var input = new JsonObject
        {
            ["swapMouseButtons"] = GetSystemMetrics(SM_SWAPBUTTON) != 0,
            ["doubleClickTimeMs"] = GetDoubleClickTime(),
            ["doubleClickWidth"] = GetSystemMetrics(SM_CXDOUBLECLK), ["doubleClickHeight"] = GetSystemMetrics(SM_CYDOUBLECLK),
            ["dragWidth"] = GetSystemMetrics(SM_CXDRAG), ["dragHeight"] = GetSystemMetrics(SM_CYDRAG),
            ["wheelScrollLines"] = System.Windows.Forms.SystemInformation.MouseWheelScrollLines,
            ["hoverTimeMs"] = System.Windows.Forms.SystemInformation.MouseHoverTime,
            ["keyboardDelay"] = System.Windows.Forms.SystemInformation.KeyboardDelay,
            ["keyboardSpeed"] = System.Windows.Forms.SystemInformation.KeyboardSpeed,
        };
        if (lowLevelTimeout > 0) input["lowLevelHooksTimeoutMs"] = lowLevelTimeout;
        return new JsonObject
        {
            ["os"] = new JsonObject { ["product"] = product, ["version"] = Environment.OSVersion.Version.ToString(), ["displayVersion"] = display },
            // Windows-Sprache, nicht die (umschaltbare) Sprache der Programmoberfläche
            ["locale"] = new JsonObject { ["ui"] = Settings.Loc.SystemUiCulture.Name, ["formats"] = Settings.Loc.SystemCulture.Name },
            ["keyboardLayouts"] = new JsonArray(new JsonObject { ["hkl"] = KeyTranslator.HklHex(hkl), ["name"] = SafeLayoutName(hkl) }),
            ["inputSettings"] = input,
            ["accessibility"] = new JsonObject { ["highContrast"] = System.Windows.Forms.SystemInformation.HighContrast },
            ["sessionInfo"] = new JsonObject { ["remote"] = System.Windows.Forms.SystemInformation.TerminalServerSession },
            ["theme"] = new JsonObject { ["apps"] = theme },
        };
    }

    static string SafeLayoutName(nint hkl)
    {
        try
        {
            // Anzeigename in der Windows-Sprache, unabhängig von der Programmoberfläche
            var prev = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = Settings.Loc.SystemUiCulture;
            try { return new CultureInfo((int)((long)hkl & 0xFFFF)).DisplayName; }
            finally { CultureInfo.CurrentUICulture = prev; }
        }
        catch { return ""; }
    }
}
