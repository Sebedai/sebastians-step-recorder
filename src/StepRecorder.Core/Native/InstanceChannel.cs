using System.Runtime.InteropServices;

namespace StepRecorder.Native;

/// <summary>
/// Kanal zur laufenden Oberfläche (eine Instanz je Windows-Sitzung): Erkennung über den benannten Mutex, Zustellung per
/// WM_COPYDATA an ein unsichtbares Nachrichtenfenster. Genutzt von zweiten Programmstarts, der Kommandozeile und dem
/// MCP-Server (Agent-Anfragen). Startet nie selbst etwas.
/// </summary>
public static class InstanceChannel
{
    public const string MutexName = @"Local\StepRecorder.UI";
    public const string WindowTitle = "StepRecorder.Instance";
    public const char Separator = '\u001F';

    /// <summary>Läuft die Oberfläche bereits (in dieser Windows-Sitzung)?</summary>
    public static bool IsUiRunning()
    {
        if (Mutex.TryOpenExisting(MutexName, out var m)) { m.Dispose(); return true; }
        return false;
    }

    /// <summary>Argumente an die laufende Oberfläche senden; <paramref name="result"/> ist ihre Antwort.</summary>
    public static bool Send(string[] args, out long result)
    {
        result = 0;
        var h = FindWindowEx(new IntPtr(-3), IntPtr.Zero, null, WindowTitle);
        if (h == IntPtr.Zero) return false;
        var text = string.Join(Separator, args);
        var ptr = Marshal.StringToHGlobalUni(text);
        try
        {
            var cds = new COPYDATASTRUCT { dwData = 1, cbData = (text.Length + 1) * 2, lpData = ptr };
            bool ok = SendMessageTimeout(h, 0x004A, IntPtr.Zero, ref cds, 2, 5000, out var res) != IntPtr.Zero;
            result = (long)res;
            return ok;
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    [StructLayout(LayoutKind.Sequential)] struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h, int msg, IntPtr w, ref COPYDATASTRUCT l, int flags, int timeout, out IntPtr result);
}
