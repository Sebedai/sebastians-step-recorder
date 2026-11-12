using System.Runtime.InteropServices;
using System.Text;
using StepRecorder.Settings;

namespace StepRecorder.App;

/// <summary>
/// Rollenweiche: Das erste Argument entscheidet, bevor ein WPF-Typ berührt wird
/// (Capture-Host, MCP-Server, Kommandozeile oder Oberfläche).
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        switch (verb)
        {
            case "--capture-host":
                return Capture.Host.HookHost.HostMain(args);
            case "mcp":
                return McpMode.Run(args);
            case "export" or "validate" or "compare" or "settings" or "--help" or "-h" or "/?" or "--version" or "steps":
                AttachParentConsole();
                return Cli.Run(args);
            case "record" when args.Contains("--stop") || args.Contains("--self-test"):
                AttachParentConsole();
                return Cli.Run(args);
        }
        if (verb is "record" or "support") AttachParentConsole();
        if (verb == "record")
        {
            // falsche Optionen hier melden – nicht erst in der Oberfläche (dort würde z. B. int.Parse werfen)
            try { Settings.Loc.Init(Settings.AppSettings.Load().UiLanguage); } catch { }
            if (Cli.ValidateRecordArgs(args) is { } error)
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine(Settings.Loc.L("Hilfe: SebastiansStepRecorder.exe --help", "Help: SebastiansStepRecorder.exe --help"));
                return 2;
            }
        }
        return UiMain(args);
    }

    // eigene Methode, damit WPF-Assemblys erst hier geladen werden
    static int UiMain(string[] args) => Ui.UiApp.Start(args);

    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] static extern nint GetStdHandle(int n);
    [DllImport("kernel32.dll")] static extern int GetFileType(nint h);

    /// <summary>Konsole des Aufrufers verwenden, falls die Ausgabe nicht ohnehin umgeleitet ist.</summary>
    static void AttachParentConsole()
    {
        nint h = GetStdHandle(-11);
        bool redirected = h != 0 && h != -1 && GetFileType(h) is 1 or 3; // Datei oder Pipe
        if (!redirected && AttachConsole(-1))
        {
            var o = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetOut(o);
            Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
            Console.WriteLine();
        }
        else
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
        }
    }
}

/// <summary>MCP-Betrieb: stdout ausschließlich für Protokollnachrichten, alles andere nach stderr.</summary>
static class McpMode
{
    public static int Run(string[] args)
    {
        var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        var stderr = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true };
        Console.SetOut(stderr); // versehentliche Ausgaben landen nicht im Protokoll
        Console.SetError(stderr);
        try { Settings.Loc.Init(Settings.AppSettings.Load().UiLanguage); } catch { }
        Settings.AppLog.InstallCrashHandlers();
        try
        {
            var o = new Mcp.McpOptions();
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException(Loc.F("Wert fehlt für {0}", "Missing value for {0}", a));
                switch (a)
                {
                    case "--root": o.Root = Next(); break;
                    case "--allow-edit": o.AllowEdit = true; break;
                    case "--allow-requests": o.AllowRequests = true; break;
                    case "--lang": o.Lang = Next(); break;
                    case "--max-result-tokens": o.MaxResultTokens = Math.Clamp(int.Parse(Next()), 2000, 24000); break;
                    case "--max-image-pixels": o.MaxImagePixels = Math.Clamp(int.Parse(Next()), 250_000, 3_750_000); break;
                    case "--max-images": o.MaxImages = Math.Clamp(int.Parse(Next()), 0, 6); break;
                    case "--log" or "--log-level": Next(); break;
                    default: stderr.WriteLine(Loc.F("Unbekannte Option: {0}", "Unknown option: {0}", a)); return 2;
                }
            }
            if (string.IsNullOrEmpty(o.Root)) o.Root = Environment.GetEnvironmentVariable("STEPRECORDER_MCP_ROOT") ?? "";
            if (string.IsNullOrEmpty(o.Root)) o.Root = Settings.AppPaths.DefaultSessionsFolder();
            if (!Directory.Exists(o.Root)) { stderr.WriteLine(Loc.F("Root-Ordner nicht gefunden: {0}", "Root folder not found: {0}", o.Root)); return 3; }
            if (Native.Win32.IsElevated()) { stderr.WriteLine(Loc.L("Der MCP-Server läuft nicht mit Administratorrechten.", "The MCP server does not run with administrator rights.")); return 7; }
            var server = new Mcp.McpStdioServer("steprecorder", Data.AppInfo.Version, Mcp.StepRecorderMcp.Instructions,
                new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)), stdout, stderr);
            new Mcp.StepRecorderMcp(o).Register(server);
            server.Run();
            return 0;
        }
        catch (Exception ex)
        {
            stderr.WriteLine(Loc.F("Fehler: {0}", "Error: {0}", ex.Message));
            return 1;
        }
    }
}
