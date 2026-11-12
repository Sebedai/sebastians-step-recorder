using System.Diagnostics;
using System.Windows;

namespace StepRecorder.App.Ui;

/// <summary>Explorer und Standardprogramme – Pfade immer in Anführungszeichen.</summary>
static class Shell
{
    /// <summary>Datei mit dem Standardprogramm öffnen (nie für Skripte verwenden).</summary>
    public static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { UiApp.ShowError(ex); }
    }

    /// <summary>Im Explorer zeigen: Datei markiert, Ordner geöffnet.</summary>
    public static void Reveal(string path)
    {
        try
        {
            if (File.Exists(path)) Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (Directory.Exists(path)) Process.Start("explorer.exe", $"\"{path}\"");
            else if (Path.GetDirectoryName(path) is { } dir && Directory.Exists(dir)) Process.Start("explorer.exe", $"\"{dir}\"");
        }
        catch (Exception ex) { UiApp.ShowError(ex); }
    }

    public static void CopyPath(string path)
    {
        for (int i = 0; i < 5; i++)
        {
            try { Clipboard.SetText(path); return; }
            catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(60); }
        }
    }
}
