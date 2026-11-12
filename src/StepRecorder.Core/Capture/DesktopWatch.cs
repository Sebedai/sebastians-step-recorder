using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StepRecorder.Capture;

/// <summary>Liest den aktuellen Eingabe-Desktop („Default“, „Winlogon“ bei UAC/Sperre, „Screen-saver“ …).</summary>
public static class DesktopProbe
{
    const uint DESKTOP_READOBJECTS = 0x0001;
    const int UOI_NAME = 2;

    /// <summary>Name des Eingabe-Desktops; „inaccessible“, wenn er sich nicht öffnen lässt (sicherer Desktop).</summary>
    public static string Read()
    {
        nint h = OpenInputDesktop(0, false, DESKTOP_READOBJECTS);
        if (h == 0) return "inaccessible";
        try
        {
            var buf = new byte[512];
            if (!GetUserObjectInformationW(h, UOI_NAME, buf, buf.Length, out int needed) || needed < 2) return "unknown";
            return System.Text.Encoding.Unicode.GetString(buf, 0, Math.Min(needed, buf.Length)).TrimEnd('\0');
        }
        finally { CloseDesktop(h); }
    }

    /// <summary>Läuft gerade eine Abfrage der Benutzerkontensteuerung (consent.exe)?</summary>
    public static bool ConsentRunning()
    {
        try
        {
            var ps = Process.GetProcessesByName("consent");
            foreach (var p in ps) p.Dispose();
            return ps.Length > 0;
        }
        catch { return false; }
    }

    [DllImport("user32.dll", SetLastError = true)] static extern nint OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll", SetLastError = true)] static extern bool GetUserObjectInformationW(nint h, int index, [Out] byte[] info, int length, out int needed);
    [DllImport("user32.dll")] static extern bool CloseDesktop(nint h);
}

/// <summary>
/// Zustandslogik zum sicheren Desktop (rein, testbar): ein anderer Desktop als „Default“ beginnt sofort eine Lücke;
/// zurück geht es erst nach zwei aufeinanderfolgenden „Default“-Lesungen (Flackern beim Umschalten).
/// </summary>
public sealed class DesktopWatch
{
    public enum Change { None, Enter, Leave }
    public bool OnSecure { get; private set; }
    public string Desktop { get; private set; } = "Default";
    /// <summary>uac (Benutzerkontensteuerung) oder other.</summary>
    public string Kind { get; private set; } = "";
    int _defaultReads;

    public Change Observe(string desktop, bool consent)
    {
        bool isDefault = string.Equals(desktop, "Default", StringComparison.OrdinalIgnoreCase);
        if (!OnSecure)
        {
            if (isDefault) return Change.None;
            OnSecure = true; Desktop = desktop; Kind = consent ? "uac" : "other"; _defaultReads = 0;
            return Change.Enter;
        }
        if (!isDefault)
        {
            _defaultReads = 0;
            if (consent) Kind = "uac";
            return Change.None;
        }
        if (++_defaultReads < 2) return Change.None;
        OnSecure = false; _defaultReads = 0;
        return Change.Leave;
    }

    /// <summary>Nach Sperre oder Ruhezustand: Zustand vergessen (die Sperr-Lücke deckt die Zeit ab).</summary>
    public void Reset() { OnSecure = false; _defaultReads = 0; Kind = ""; Desktop = "Default"; }
}
