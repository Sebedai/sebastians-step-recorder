namespace StepRecorder.Settings;

/// <summary>
/// Programmprotokoll ohne Inhalte: Abläufe, Fehler, Selbsttest. Höchstens <see cref="MaxBytes"/> je Datei plus ein Vorgänger.
/// Nie protokolliert: Titel, Ziele, Texte, Suchbegriffe, Werte von Kommandozeilen-Optionen.
/// </summary>
public static class AppLog
{
    public const long MaxBytes = 1_000_000;
    static readonly object Gate = new();
    /// <summary>Nur für Tests: anderes Verzeichnis.</summary>
    public static string? OverrideDir { get; set; }

    public static string FilePath => Path.Combine(OverrideDir ?? AppPaths.StateDir, "StepRecorder.log");

    public static void Write(string text)
    {
        try
        {
            lock (Gate)
            {
                var path = FilePath;
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    var old = path + ".1";
                    File.Move(path, old, true);
                }
                File.AppendAllText(path, $"{DateTime.Now:O} {text}{Environment.NewLine}");
            }
        }
        catch { }
    }

    public static void Error(string where, Exception ex) => Write($"FEHLER {where}: {ex}");

    /// <summary>Befehl und Optionsnamen, nie deren Werte: „record --title … --goal …“.</summary>
    public static string ArgNames(string[] args)
        => string.Join(" ", args.Select((a, i) => i == 0 && !a.StartsWith('-') && !a.Contains('\\') && !a.Contains('/') && a.Length < 20 ? a : a.StartsWith("--") || (a.StartsWith('-') && a.Length <= 3) ? a : "…"));

    /// <summary>Ende des Protokolls (für das Diagnose-Paket).</summary>
    public static string ReadTail(int maxBytes = 1_000_000)
    {
        try
        {
            lock (Gate)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var p in new[] { FilePath + ".1", FilePath })
                    if (File.Exists(p)) sb.Append(File.ReadAllText(p));
                var s = sb.ToString();
                return s.Length > maxBytes ? s[^maxBytes..] : s;
            }
        }
        catch { return ""; }
    }

    static bool _installed;

    /// <summary>Unbehandelte Ausnahmen aus allen Threads protokollieren; <paramref name="onFatal"/> versucht z. B. eine laufende Aufnahme zu sichern.</summary>
    public static void InstallCrashHandlers(Action? onFatal = null)
    {
        if (_installed) return;
        _installed = true;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Error("unbehandelt" + (e.IsTerminating ? " (Programmende)" : ""), ex);
            if (e.IsTerminating) try { onFatal?.Invoke(); } catch { }
        };
        TaskScheduler.UnobservedTaskException += (_, e) => { Error("Task", e.Exception); e.SetObserved(); };
    }
}
