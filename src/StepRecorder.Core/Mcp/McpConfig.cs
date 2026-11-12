using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.Mcp;

public sealed record McpConfigOptions(string ExePath, string Root, bool AllowEdit = false, bool AllowRequests = false, string? Lang = null);

/// <summary>
/// Fertige MCP-Einrichtung zum Kopieren („Mit KI verbinden …“): Projektdatei <c>.mcp.json</c>, Claude Desktop, Kommandozeile.
/// Erzeugt nur Text – schreibt keine fremde Datei und startet nichts.
/// </summary>
public static class McpConfig
{
    public const string ServerName = "steprecorder";

    public static string[] Args(McpConfigOptions o)
    {
        var l = new List<string> { "mcp", "--root", o.Root };
        if (o.AllowEdit) l.Add("--allow-edit");
        if (o.AllowRequests) l.Add("--allow-requests");
        if (o.Lang is "de" or "en") { l.Add("--lang"); l.Add(o.Lang); }
        return l.ToArray();
    }

    static JsonObject Server(McpConfigOptions o) => new()
    {
        ["command"] = o.ExePath,
        ["args"] = new JsonArray(Args(o).Select(a => (JsonNode)a).ToArray())
    };

    /// <summary>Für die Projektdatei <c>.mcp.json</c> (z. B. Claude Code).</summary>
    public static string ProjectJson(McpConfigOptions o) => Json.Pretty(new JsonObject { ["mcpServers"] = new JsonObject { [ServerName] = Server(o) } });

    /// <summary>Für <c>claude_desktop_config.json</c> (gleiche Form; dort mit vorhandenen Servern zusammenführen).</summary>
    public static string DesktopJson(McpConfigOptions o) => ProjectJson(o);

    /// <summary>Befehl für Claude Code: <c>claude mcp add …</c>.</summary>
    public static string ClaudeAddCommand(McpConfigOptions o)
        => "claude mcp add --transport stdio --scope user " + ServerName + " -- " + string.Join(" ", new[] { o.ExePath }.Concat(Args(o)).Select(QuoteArg));

    /// <summary>Aufruf für die Eingabeaufforderung.</summary>
    public static string CommandLine(McpConfigOptions o) => string.Join(" ", new[] { o.ExePath }.Concat(Args(o)).Select(QuoteArg));

    /// <summary>Ein Argument nach den Regeln von CommandLineToArgvW einschließen (abschließende Backslashes verdoppelt).</summary>
    public static string QuoteArg(string arg)
    {
        if (arg.Length > 0 && !arg.Any(c => c is ' ' or '\t' or '"' or '\n')) return arg;
        var sb = new StringBuilder("\"");
        int bs = 0;
        foreach (char c in arg)
        {
            if (c == '\\') { bs++; continue; }
            if (c == '"') { sb.Append('\\', bs * 2 + 1).Append('"'); bs = 0; continue; }
            sb.Append('\\', bs).Append(c); bs = 0;
        }
        sb.Append('\\', bs * 2).Append('"');
        return sb.ToString();
    }

    /// <summary>Hinweise zur Einrichtung (Ort der EXE, synchronisierter Ordner, Sonderzeichen).</summary>
    public static List<string> Warnings(McpConfigOptions o)
    {
        var w = new List<string>();
        string exeLower = o.ExePath.ToLowerInvariant();
        if (exeLower.Contains(@"\downloads\") || exeLower.Contains(@"\temp\") || exeLower.Contains(@"\appdata\local\temp"))
            w.Add(Loc.L("Die EXE liegt in Downloads oder einem temporären Ordner – für eine dauerhafte Einrichtung besser an einen festen Ort legen.", "The EXE is in Downloads or a temporary folder – move it to a permanent location for a lasting setup."));
        try
        {
            var root = Path.GetPathRoot(o.ExePath);
            if (root != null && new DriveInfo(root).DriveType == DriveType.Removable)
                w.Add(Loc.L("Die EXE liegt auf einem Wechseldatenträger – die Verbindung funktioniert nur, solange er unter diesem Laufwerksbuchstaben steckt.", "The EXE is on a removable drive – the connection only works while it is plugged in under this drive letter."));
        }
        catch { }
        if (AppPaths.LooksSynced(o.Root)) w.Add(Loc.L("Der Aufnahmeordner wird synchronisiert.", "The recordings folder is synced."));
        if ((o.ExePath + o.Root).Any(c => c is '%' or '$' or '`')) w.Add(Loc.L("Der Pfad enthält %, $ oder `; in der Kommandozeile können diese Zeichen umgedeutet werden.", "The path contains %, $ or `; these characters may be interpreted by the shell."));
        if (!Path.GetFileName(o.ExePath).Equals(AppInfo.ExeName, StringComparison.OrdinalIgnoreCase))
            w.Add(Loc.L("Das Programm läuft nicht als SebastiansStepRecorder.exe (Entwicklerstart) – bitte den Pfad der veröffentlichten EXE eintragen.", "The program is not running as SebastiansStepRecorder.exe (developer start) – please use the path of the released EXE."));
        return w;
    }
}
