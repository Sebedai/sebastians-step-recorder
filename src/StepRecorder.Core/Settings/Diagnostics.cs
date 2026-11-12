using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Capture;
using StepRecorder.Data;

namespace StepRecorder.Settings;

/// <summary>Angaben, die nur die Oberfläche kennt (Selbsttest, belegte Tastenkürzel).</summary>
public sealed record DiagnosticsExtras(bool? SelfTestPassed = null, string? SelfTestFailure = null, IReadOnlyList<string>? HotkeysTaken = null, bool? OcrAvailable = null);

/// <summary>
/// Diagnose-Paket für Fehlermeldungen: Protokoll, Programm- und Systemangaben, bereinigte Einstellungen.
/// Nie Aufnahmeinhalte, nie Pfade mit Benutzernamen, nie Rechner- oder Domänennamen. Die Person gibt die Datei selbst weiter.
/// </summary>
public static class Diagnostics
{
    /// <summary>Einstellungen nach Positivliste: Vorlieben im Klartext, Listen nur als Anzahl.</summary>
    public static JsonObject SanitizedSettings(AppSettings s) => new()
    {
        ["version"] = s.Version, ["language"] = s.Language, ["uiLanguage"] = s.UiLanguage, ["theme"] = s.Theme, ["address"] = s.Address,
        ["privacyLevel"] = s.PrivacyLevel, ["countdown"] = s.Countdown, ["soundCues"] = s.SoundCues, ["invisibleFallback"] = s.InvisibleFallback,
        ["changeFrames"] = s.ChangeFrames, ["lastProfile"] = s.LastProfile, ["customAppContext"] = s.CustomAppContext, ["customAfterStop"] = s.CustomAfterStop,
        ["record"] = new JsonObject(RecordingOptions.Channels.Select(c => KeyValuePair.Create(c.key, (JsonNode?)s.Records(c.key)))),
        ["hotkeys"] = new JsonObject(s.EffectiveHotkeys().Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
        ["excludedCount"] = s.Excluded.Count, ["excludedIsDefault"] = s.Excluded.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(RecordingOptions.DefaultExcluded),
        ["docTemplates"] = s.DocTemplates.Count, ["recentSessions"] = s.RecentSessions.Count,
        ["sessionsFolder"] = string.IsNullOrWhiteSpace(s.SessionsFolder) ? "default" : "custom",
        ["unknownKeys"] = s.Unknown?.Count ?? 0
    };

    /// <summary>Zustand: nur Fenstergeometrie.</summary>
    public static JsonObject SanitizedState(AppState st) => new()
    {
        ["windows"] = new JsonObject(st.Windows.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonObject { ["w"] = kv.Value.W, ["h"] = kv.Value.H, ["max"] = kv.Value.Max, ["dpi"] = kv.Value.Dpi }))),
        ["sessionsRemembered"] = st.Sessions.Count
    };

    public static string SystemInfo(DiagnosticsExtras x)
    {
        var sb = new StringBuilder();
        void L(string k, object? v) => sb.Append(k).Append(": ").AppendLine(v?.ToString() ?? "–");
        L("Programm", $"{AppInfo.DisplayName} {AppInfo.Version}");
        L("Betrieb", AppPaths.Portable ? "portabel (Daten neben der EXE)" : "Benutzerprofil");
        L("Windows", $"{RuntimeInformation.OSDescription} ({Environment.OSVersion.Version})");
        L(".NET", RuntimeInformation.FrameworkDescription);
        L("Architektur", RuntimeInformation.ProcessArchitecture);
        L("Administratorrechte", Native.Win32.IsElevated() ? "ja" : "nein");
        L("Sprache (Windows)", $"{Loc.SystemUiCulture.Name} / Format {Loc.SystemCulture.Name}");
        L("Sprache (Oberfläche)", Loc.Lang);
        try
        {
            foreach (var m in Native.ScreenGrabber.GetMonitors())
                L($"Monitor M{m.Index + 1}", $"{m.Bounds.Width} × {m.Bounds.Height} bei ({m.Bounds.X}, {m.Bounds.Y}), Arbeitsbereich {m.WorkArea.Width} × {m.WorkArea.Height}, {(int)Math.Round(m.Scale * 100)} %{(m.Primary ? ", Hauptmonitor" : "")}");
        }
        catch { }
        L("Remote-Sitzung", System.Windows.Forms.SystemInformation.TerminalServerSession ? "ja" : "nein");
        L("Hoher Kontrast", System.Windows.Forms.SystemInformation.HighContrast ? "ja" : "nein");
        L("Selbsttest Unsichtbarkeit", x.SelfTestPassed switch { true => "bestanden", false => "nicht bestanden: " + x.SelfTestFailure, _ => "nicht gelaufen" });
        L("Texterkennung (OCR)", x.OcrAvailable switch { true => "verfügbar", false => "nicht verfügbar", _ => "unbekannt" });
        if (x.HotkeysTaken is { Count: > 0 } hk) L("Tastenkürzel belegt", string.Join(", ", hk));
        try
        {
            var folder = AppPaths.DefaultSessionsFolder();
            L("Aufnahmeordner", $"{(AppPaths.IsWritable(Directory.Exists(folder) ? folder : Path.GetDirectoryName(folder) ?? folder) ? "beschreibbar" : "nicht beschreibbar")}{(AppPaths.LooksSynced(folder) ? ", synchronisiert" : "")}");
            var root = Path.GetPathRoot(Path.GetFullPath(folder));
            if (root != null) L("Freier Speicher", Loc.Bytes(new DriveInfo(root).AvailableFreeSpace));
        }
        catch { }
        return sb.ToString();
    }

    /// <summary>Ersetzt persönliche Angaben (Profilpfad, Benutzer, Rechner, Domäne, Ordner) – auch in JSON-maskierter Form.</summary>
    public static string Scrub(string text, IEnumerable<(string value, string token)>? map = null)
    {
        var pairs = (map ?? DefaultScrubMap()).Where(p => p.value.Length >= 3).OrderByDescending(p => p.value.Length).ToList();
        foreach (var (value, token) in pairs)
        {
            text = text.Replace(value, token, StringComparison.OrdinalIgnoreCase);
            var esc = value.Replace("\\", "\\\\");
            if (esc != value) text = text.Replace(esc, token, StringComparison.OrdinalIgnoreCase);
        }
        return text;
    }

    public static List<(string value, string token)> DefaultScrubMap()
    {
        var l = new List<(string, string)>();
        void Add(Func<string?> f, string token) { try { if (f() is { Length: >= 3 } v) l.Add((v.TrimEnd('\\'), token)); } catch { } }
        Add(() => AppPaths.DefaultSessionsFolder(), "<Aufnahmeordner>");
        Add(() => AppPaths.DataDir, "<Datenordner>");
        Add(() => AppPaths.StateDir, "<Zustandsordner>");
        Add(() => AppContext.BaseDirectory, "<Programmordner>");
        Add(() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "<Profil>");
        Add(() => Environment.UserName, "<Benutzer>");
        Add(() => Environment.MachineName, "<Rechner>");
        Add(() => Environment.UserDomainName, "<Domäne>");
        return l;
    }

    /// <summary>Erstellt das ZIP: info.txt, settings.json, state.json, log.txt, LIESMICH.txt.</summary>
    public static string CreatePackage(string zipPath, DiagnosticsExtras x)
    {
        var map = DefaultScrubMap();
        var tmp = zipPath + ".part";
        if (File.Exists(tmp)) File.Delete(tmp);
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            void Add(string name, string content)
            {
                var e = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                w.Write(Scrub(content, map));
            }
            Add("info.txt", SystemInfo(x));
            Add("settings.json", Json.Pretty(SanitizedSettings(AppSettings.Load())));
            Add("state.json", Json.Pretty(SanitizedState(AppState.Load())));
            Add("log.txt", AppLog.ReadTail());
            Add("LIESMICH.txt", Loc.L(
                "Diagnose-Paket von Sebastians Step Recorder\r\n\r\nEnthält: Programm- und Systemangaben (info.txt), Einstellungen ohne persönliche Angaben (settings.json),\r\nFenstergrößen (state.json) und das Programmprotokoll (log.txt). Benutzer-, Rechner- und Ordnernamen sind ersetzt.\r\nEnthält keine Aufnahmen, keine Bilder, keine getippten Texte.\r\n\r\nDas Programm hat nichts verschickt – Sie geben diese Datei selbst weiter.\r\n",
                "Diagnostics package of Sebastians Step Recorder\r\n\r\nContains: program and system information (info.txt), settings without personal data (settings.json),\r\nwindow sizes (state.json) and the program log (log.txt). User, computer and folder names are replaced.\r\nContains no recordings, no images, no typed text.\r\n\r\nThe program has sent nothing – you pass this file on yourself.\r\n"));
        }
        File.Move(tmp, zipPath, true);
        AppLog.Write("Diagnose-Paket erstellt");
        return zipPath;
    }
}
