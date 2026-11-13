using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StepRecorder.Data;

namespace StepRecorder.Settings;

/// <summary>Datenverzeichnis (portabel neben der EXE oder Profil) und Standard-Aufnahmeordner (02 §12, E1).</summary>
public static class AppPaths
{
    static string? _dataDir;
    public static string? OverrideDataDir { get; set; }
    public static bool Portable { get; private set; }

    /// <summary>
    /// Ordner der EXE. Bewusst nicht <c>AppContext.BaseDirectory</c>: Eine Einzeldatei-EXE, die ihren Inhalt in den
    /// Temp-Ordner entpackt (IncludeAllContentForSelfExtract), meldet dort den Entpack-Ordner – der Portabel-Modus muss
    /// aber neben der echten EXE bleiben.
    /// </summary>
    public static string ExeDir => Environment.ProcessPath is { } p && Path.GetDirectoryName(p) is { Length: > 0 } d && !IsDotnetHost(p) ? d + Path.DirectorySeparatorChar : AppContext.BaseDirectory;

    /// <summary>Unter <c>dotnet test</c> o. ä. läuft der Code im dotnet-Host – dann zählt der Ordner der Assemblies.</summary>
    static bool IsDotnetHost(string processPath) => Path.GetFileNameWithoutExtension(processPath) is "dotnet" or "testhost";

    public static string DataDir
    {
        get
        {
            if (_dataDir != null) return _dataDir;
            if (!string.IsNullOrEmpty(OverrideDataDir)) { Directory.CreateDirectory(OverrideDataDir); Portable = true; return _dataDir = OverrideDataDir; }
            if (File.Exists(Path.Combine(ExeDir, "StepRecorder.settings.json")) || IsWritable(ExeDir)) { Portable = true; return _dataDir = ExeDir; }
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StepRecorder");
            Directory.CreateDirectory(d);
            return _dataDir = d;
        }
    }

    public static string StateDir
    {
        get
        {
            if (Portable || DataDir == ExeDir) return DataDir;
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StepRecorder");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public static string SettingsFile => Path.Combine(DataDir, "StepRecorder.settings.json");
    public static string StateFile => Path.Combine(StateDir, "StepRecorder.state.json");

    /// <summary><c>Sessions</c> neben der EXE, falls beschreibbar und nicht synchronisiert; sonst %LOCALAPPDATA%.</summary>
    public static string DefaultSessionsFolder()
    {
        var s = AppSettings.Load().SessionsFolder;
        if (!string.IsNullOrWhiteSpace(s)) return Path.IsPathRooted(s) ? s : Path.Combine(DataDir, s);
        if (IsWritable(ExeDir) && !LooksSynced(ExeDir)) return Path.Combine(ExeDir, "Sessions");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StepRecorder", "Sessions");
    }

    public static bool IsWritable(string dir)
    {
        try
        {
            var p = Path.Combine(dir, ".write-test-" + Environment.ProcessId);
            File.WriteAllText(p, "");
            File.Delete(p);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Heuristik für Sync-Ordner (OneDrive, Nextcloud/ownCloud, Dropbox).</summary>
    public static bool LooksSynced(string dir)
    {
        try
        {
            var full = Path.GetFullPath(dir);
            foreach (var v in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" })
            {
                var od = Environment.GetEnvironmentVariable(v);
                if (!string.IsNullOrEmpty(od) && full.StartsWith(od, StringComparison.OrdinalIgnoreCase)) return true;
            }
            for (var d = new DirectoryInfo(full); d != null; d = d.Parent)
            {
                if (d.GetFiles(".sync_*.db").Length > 0 || d.GetFiles("._sync_*.db").Length > 0 || d.GetFiles(".owncloudsync.log").Length > 0) return true;
                if (Directory.Exists(Path.Combine(d.FullName, ".dropbox.cache")) || File.Exists(Path.Combine(d.FullName, ".dropbox"))) return true;
            }
        }
        catch { }
        return false;
    }
}

/// <summary>Programmeinstellungen (versioniert, tolerant gelesen, atomar geschrieben; unbekannte Schlüssel bleiben erhalten).</summary>
public sealed class AppSettings
{
    /// <summary>Aktuelle Version der Einstellungsdatei; ältere werden beim Lesen schrittweise angehoben.</summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    /// <summary>Sprache der automatisch erzeugten Anleitungstexte (de | en).</summary>
    public string Language { get; set; } = "de";
    /// <summary>Sprache der Programmoberfläche: system (wie Windows) | de | en. Gilt ab dem nächsten Start.</summary>
    public string UiLanguage { get; set; } = "system";
    public string? SessionsFolder { get; set; }
    public string PrivacyLevel { get; set; } = "balanced";
    public List<string> Excluded { get; set; } = new(Capture.RecordingOptions.DefaultExcluded);
    public string LastProfile { get; set; } = "guide";
    public string ChangeFrames { get; set; } = "normal";
    public int Countdown { get; set; } = 0;
    public string Address { get; set; } = "formal";
    /// <summary>Wenn die Unsichtbarkeit nicht bestätigt werden kann: ask (nachfragen) | hidden (immer mit ausgeblendeter Anzeige).</summary>
    public string InvisibleFallback { get; set; } = "ask";
    /// <summary>Dokumentvorlagen: Name → doc-Objekt (Logo, Farbe, Fußzeile …) samt Anrede.</summary>
    public Dictionary<string, JsonObject> DocTemplates { get; set; } = new();
    /// <summary>Vorlage, die neue Aufnahmen automatisch erhalten (leer = keine).</summary>
    public string DefaultTemplate { get; set; } = "";
    /// <summary>Töne bei Start, Stopp, Pause, Marker: hidden (nur bei ausgeblendeter Anzeige) | always | never.</summary>
    public string SoundCues { get; set; } = "hidden";
    /// <summary>Farbschema der Oberfläche: dark (Standard) | light | system (wie Windows).</summary>
    public string Theme { get; set; } = "dark";
    /// <summary>Erinnerung „Aufnahme läuft noch“ nach so vielen Minuten ohne Eingabe (0 = aus).</summary>
    public int IdleReminderMinutes { get; set; } = 10;
    /// <summary>Erinnerung „Privattaste ist noch an“ nach so vielen Sekunden ohne Taste.</summary>
    public int PrivacyReminderSeconds { get; set; } = 60;
    /// <summary>Beim ersten Öffnen einer Aufnahme überflüssige Schritte automatisch ausblenden.</summary>
    public bool AutoCleanup { get; set; } = true;
    /// <summary>Beim Schließen einer frisch aufgenommenen Aufnahme fragen: behalten oder verwerfen.</summary>
    public bool AskKeepOnClose { get; set; } = true;
    /// <summary>Begriffsliste für automatische Texte: „Fundstelle=Ersatz“ je Zeile (z. B. Firmenwörter).</summary>
    public List<string> Glossary { get; set; } = new();
    /// <summary>Beim Start nach einer neuen Version auf GitHub suchen (Standard aus: das Programm bleibt offline).</summary>
    public bool CheckUpdates { get; set; } = false;
    /// <summary>Bildschirmaufnahme: auto (DXGI, Rückfall GDI) | gdi.</summary>
    public string CaptureBackend { get; set; } = "auto";
    /// <summary>KI- und AutoHotkey-Export für Menschen lesbar (ausführlich kommentiert) – zuletzt gewählt.</summary>
    public bool ExportReadable { get; set; }
    public Dictionary<string, string> Hotkeys { get; set; } = HotkeyActions.Defaults();

    /// <summary>Wirksames Tastenkürzel einer Aktion: gespeichert, sonst Standard; leer = bewusst ohne Kürzel.</summary>
    public string Hotkey(string id) => Hotkeys.TryGetValue(id, out var v) ? v ?? "" : HotkeyActions.ById(id)?.Default ?? "";

    /// <summary>Alle wirksamen Kürzel (auch nicht gespeicherte Standardwerte).</summary>
    public Dictionary<string, string> EffectiveHotkeys() => HotkeyActions.All.ToDictionary(a => a.Id, a => Hotkey(a.Id));
    /// <summary>Was aufgezeichnet wird (Schlüssel aus <see cref="Capture.RecordingOptions.Channels"/>); fehlende Schlüssel gelten als eingeschaltet.</summary>
    public Dictionary<string, bool> Record { get; set; } = new();
    public bool Records(string channel) => Record.GetValueOrDefault(channel, true);
    /// <summary>Entwickler-Kontext (Paket 12): Freitext der Ereignisanzeige mitlesen – ab Werk aus, schwächt den Schutz.</summary>
    public bool EventLogText { get; set; }
    /// <summary>Inhalt der Zwischenablage (nur Text, gekürzt) aufzeichnen – ab Werk aus (10 §9), schwächt den Schutz.</summary>
    public bool ClipboardContent { get; set; }
    /// <summary>Protokolldateien, die während der Aufnahme mitgelesen werden (Paket 13) – leer = keine.</summary>
    public List<string> LogFiles { get; set; } = new();
    /// <summary>Im Infobereich bereit bleiben, wenn das letzte Fenster schließt (Paket 14) – ab Werk aus.</summary>
    public bool StayInTray { get; set; }
    /// <summary>Modus „Eigene“: erweiterter Programmkontext.</summary>
    public bool CustomAppContext { get; set; } = true;
    /// <summary>Modus „Eigene“: was nach dem Stopp passiert (viewer | export | supportPackage | folder).</summary>
    public string CustomAfterStop { get; set; } = "viewer";
    public List<string> RecentSessions { get; set; } = new();
    /// <summary>Textbausteine für Hinweise und Schritttexte (Name und Text), im Editor per Strg+Umschalt+B einfügbar.</summary>
    public List<TextSnippet> TextSnippets { get; set; } = new();
    public JsonObject? Extra { get; set; }

    /// <summary>Schlüssel, die diese Version nicht kennt (z. B. von einer neueren) – bleiben beim Speichern erhalten.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }

    /// <summary>Hinweis vom Laden (beschädigte Datei beiseitegelegt, einzelne Werte verworfen) – einmal anzeigen.</summary>
    [JsonIgnore] public string? LoadNotice { get; set; }
    /// <summary>Fehler beim letzten Speichern (null = gespeichert).</summary>
    [JsonIgnore] public string? LastSaveError { get; private set; }

    static AppSettings? _cached;
    static readonly JsonSerializerOptions Opt = new(Json.Indented) { PropertyNameCaseInsensitive = true };

    public static AppSettings Load() => _cached ??= Read(AppPaths.SettingsFile);

    /// <summary>Verwirft den Zwischenspeicher (nach Import oder Zurücksetzen von außen).</summary>
    public static void Reload() => _cached = null;

    /// <summary>Liest eine Einstellungsdatei tolerant: Migration, einzelne ungültige Werte werden verworfen statt der ganzen Datei.</summary>
    public static AppSettings Read(string path)
    {
        if (!File.Exists(path)) return new AppSettings();
        JsonObject? raw;
        try { raw = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { MaxDepth = 64, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject; }
        catch { raw = null; }
        if (raw == null)
        {
            string bak = path + ".defekt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            try { File.Move(path, bak, true); } catch { }
            AppLog.Write("Einstellungen beschädigt – beiseitegelegt: " + Path.GetFileName(bak));
            return new AppSettings { LoadNotice = Loc.L("Die Einstellungsdatei war beschädigt und wurde beiseitegelegt; es gelten die Standardwerte.", "The settings file was damaged and has been set aside; defaults apply.") };
        }
        int fileVersion = (int)(Key(raw, "version")?.GetValueKind() == JsonValueKind.Number ? Key(raw, "version")!.GetValue<int>() : 1);
        SettingsMigrations.Run(raw, fileVersion);
        var dropped = new List<string>();
        for (int attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                var s = JsonSerializer.Deserialize<AppSettings>(raw.ToJsonString(), Opt) ?? new AppSettings();
                s.Version = Math.Max(fileVersion, CurrentVersion);
                s.Hotkeys ??= HotkeyActions.Defaults();
                s.Excluded ??= new(Capture.RecordingOptions.DefaultExcluded);
                s.DocTemplates ??= new(); s.Record ??= new(); s.RecentSessions ??= new();
                s.TextSnippets = TextSnippet.Clean(s.TextSnippets);
                if (dropped.Count > 0)
                {
                    AppLog.Write("Einstellungen: ungültige Werte verworfen: " + string.Join(", ", dropped));
                    s.LoadNotice = Loc.F("Einige Einstellungen waren ungültig und wurden zurückgesetzt: {0}", "Some settings were invalid and have been reset: {0}", string.Join(", ", dropped));
                }
                return s;
            }
            catch (JsonException ex) when (ex.Path is { Length: > 1 })
            {
                dropped.Add(ex.Path!.TrimStart('$', '.'));
                if (!RemovePath(raw, ex.Path!)) break;
            }
            catch { break; }
        }
        AppLog.Write("Einstellungen nicht lesbar – Standardwerte");
        return new AppSettings { LoadNotice = Loc.L("Die Einstellungen konnten nicht gelesen werden; es gelten die Standardwerte.", "Settings could not be read; defaults apply.") };
    }

    static JsonNode? Key(JsonObject o, string name) => o.FirstOrDefault(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Entfernt den Wert an einem JSON-Pfad wie „$.countdown“ oder „$.hotkeys.stop“.</summary>
    static bool RemovePath(JsonObject root, string path)
    {
        var segs = path.TrimStart('$').Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length == 0) return false;
        JsonNode? cur = root;
        for (int i = 0; i < segs.Length; i++)
        {
            string seg = segs[i];
            int b = seg.IndexOf('[');
            string name = b >= 0 ? seg[..b] : seg;
            bool last = i == segs.Length - 1;
            if (cur is not JsonObject o) return false;
            var key = o.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
            if (key == null) return false;
            if (last && b < 0) { o.Remove(key); return true; }
            if (b >= 0 && o[key] is JsonArray arr && int.TryParse(seg[(b + 1)..^1], out int idx) && idx < arr.Count)
            {
                if (last) { arr.RemoveAt(idx); return true; }
                cur = arr[idx];
            }
            else if (b >= 0) { o.Remove(key); return true; }
            else cur = o[key];
        }
        return false;
    }

    public bool Save() => Write(AppPaths.SettingsFile, setCache: true);

    public JsonObject ToJsonObject() => JsonNode.Parse(JsonSerializer.Serialize(this, Opt))!.AsObject();
    public static AppSettings FromJsonObject(JsonObject o) => JsonSerializer.Deserialize<AppSettings>(o.ToJsonString(), Opt) ?? new AppSettings();
    public AppSettings Clone() => FromJsonObject(ToJsonObject());

    /// <summary>Speichert atomar; Fehler werden protokolliert und in <see cref="LastSaveError"/> gemeldet.</summary>
    public bool Write(string path, bool setCache = false)
    {
        if (setCache) _cached = this;
        try
        {
            Version = Math.Max(Version, CurrentVersion);
            Json.WriteAtomic(path, JsonSerializer.Serialize(this, Opt));
            LastSaveError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastSaveError = ex.Message;
            AppLog.Write("Einstellungen nicht gespeichert: " + ex.GetType().Name + " " + ex.Message);
            return false;
        }
    }

    public void AddRecent(string path)
    {
        RecentSessions.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentSessions.Insert(0, path);
        if (RecentSessions.Count > 15) RecentSessions.RemoveRange(15, RecentSessions.Count - 15);
        Save();
    }

    public void RemoveRecent(string path)
    {
        if (RecentSessions.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0) Save();
    }
}

/// <summary>Textbaustein: wiederverwendbarer Text für Hinweise und Schritttexte (08 §12, Teil <c>textSnippets</c> im Einstellungs-Austausch).</summary>
public sealed class TextSnippet
{
    public const int MaxName = 60, MaxText = 4000, MaxCount = 200;
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";

    /// <summary>Gültig: Name 1–60 Zeichen (ohne Zeilenumbruch), Text 1–4000 Zeichen.</summary>
    public static bool Valid(string? name, string? text)
        => name != null && text != null && name.Trim().Length is > 0 and <= MaxName && !name.Contains('\n') && !name.Contains('\r') && text.Trim().Length > 0 && text.Length <= MaxText;

    /// <summary>Bereinigt eine Liste: ungültige Einträge fallen weg, Namen eindeutig (ohne Groß-/Kleinschreibung, der letzte gewinnt), höchstens 200.</summary>
    public static List<TextSnippet> Clean(IEnumerable<TextSnippet?>? list)
    {
        var result = new List<TextSnippet>();
        foreach (var s in list ?? Enumerable.Empty<TextSnippet?>())
        {
            if (s == null || !Valid(s.Name, s.Text)) continue;
            var t = new TextSnippet { Name = s.Name.Trim(), Text = s.Text.TrimEnd() };
            int i = result.FindIndex(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) result[i] = t; else result.Add(t);
        }
        return result.Take(MaxCount).ToList();
    }

    /// <summary>Fügt <paramref name="snippet"/> an der Schreibmarke ein (ersetzt eine Markierung); liefert neuen Text und neue Schreibmarke.</summary>
    public static (string text, int caret) Insert(string text, int selStart, int selLength, string snippet)
    {
        text ??= "";
        selStart = Math.Clamp(selStart, 0, text.Length);
        selLength = Math.Clamp(selLength, 0, text.Length - selStart);
        return (text[..selStart] + snippet + text[(selStart + selLength)..], selStart + snippet.Length);
    }
}

/// <summary>Schrittweise Anhebung älterer Einstellungsdateien (vor dem Einlesen, auf dem rohen JSON).</summary>
public static class SettingsMigrations
{
    static readonly (int From, Action<JsonObject> Step)[] Steps =
    {
        // 1 → 2: Tastenkürzel in kanonische Schreibweise, unlesbare entfernen (dann gilt der Standard)
        (1, raw =>
        {
            var key = raw.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, "hotkeys", StringComparison.OrdinalIgnoreCase));
            if (key == null || raw[key] is not JsonObject hk) return;
            foreach (var (id, val) in hk.ToList())
            {
                string? text = val is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
                if (text is { Length: 0 }) continue;
                if (text != null && HotkeyCombo.TryParse(text, out var c)) hk[id] = c.ToString();
                else hk.Remove(id);
            }
        }),
    };

    public static void Run(JsonObject raw, int fromVersion)
    {
        foreach (var (from, step) in Steps)
            if (fromVersion <= from) { try { step(raw); } catch { } }
        var vk = raw.Select(kv => kv.Key).FirstOrDefault(k => string.Equals(k, "version", StringComparison.OrdinalIgnoreCase));
        if (vk != null) raw.Remove(vk);
        raw["version"] = Math.Max(fromVersion, AppSettings.CurrentVersion);
    }
}
