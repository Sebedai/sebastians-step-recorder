using System.Text;

namespace StepRecorder.Settings;

/// <summary>
/// Kurzbefehl <c>steprec.cmd</c> neben der EXE (02 §8.3, Punkt 4; 12 Paket 14): Konsolen warten nicht auf ein
/// GUI-Programm – der Kurzbefehl startet die EXE mit <c>start /wait /b</c> in derselben Konsole und reicht den
/// Exit-Code durch. Entsteht nur auf Befehl in den Einstellungen; die EXE selbst bleibt unverändert.
/// </summary>
public static class ConsoleShim
{
    public const string FileName = "steprec.cmd";

    public static string Content(string exeName) =>
        "@echo off\r\n" +
        "rem " + StepRecorder.Data.AppInfo.DisplayName + " fuer Konsolen und Skripte: wartet auf das Ende und gibt den Exit-Code weiter.\r\n" +
        "rem Beispiel: steprec export latest --format md\r\n" +
        $"start \"\" /wait /b \"%~dp0{exeName}\" %*\r\n" +
        "exit /b %ERRORLEVEL%\r\n";

    /// <summary>Schreibt den Kurzbefehl in <paramref name="dir"/> (atomar); gibt den Pfad zurück.</summary>
    public static string Write(string dir, string exeName)
    {
        string path = Path.Combine(dir, FileName);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, Content(exeName), new UTF8Encoding(false));
        File.Move(tmp, path, true);
        return path;
    }
}
