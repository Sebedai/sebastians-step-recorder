using System.Text.Json;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Settings;

/// <summary>Datenverzeichnis (portabel neben der EXE oder Profil) und Standard-Aufnahmeordner (02 §12, E1).</summary>
public static class AppPaths
{
    static string? _dataDir;
    public static string? OverrideDataDir { get; set; }
    public static bool Portable { get; private set; }

    public static string ExeDir => AppContext.BaseDirectory;

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
    public int Version { get; set; } = 1;
    public string Language { get; set; } = "de";
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
    /// <summary>Beim ersten Öffnen einer Aufnahme überflüssige Schritte automatisch ausblenden.</summary>
    public bool AutoCleanup { get; set; } = true;
    /// <summary>Begriffsliste für automatische Texte: „Fundstelle=Ersatz“ je Zeile (z. B. Firmenwörter).</summary>
    public List<string> Glossary { get; set; } = new();
    /// <summary>Beim Start nach einer neuen Version auf GitHub suchen (Standard aus: das Programm bleibt offline).</summary>
    public bool CheckUpdates { get; set; } = false;
    /// <summary>Sprache der Oberfläche: de | en.</summary>
    public string UiLanguage { get; set; } = "de";
    /// <summary>Bildschirmaufnahme: auto (DXGI, Rückfall GDI) | gdi.</summary>
    public string CaptureBackend { get; set; } = "auto";
    /// <summary>KI- und AutoHotkey-Export für Menschen lesbar (ausführlich kommentiert) – zuletzt gewählt.</summary>
    public bool ExportReadable { get; set; }
    public Dictionary<string, string> Hotkeys { get; set; } = new()
    {
        ["instantPause"] = "Ctrl+Shift+F9", ["stop"] = "Ctrl+Shift+F10", ["snapshot"] = "Ctrl+Shift+F11", ["comment"] = "Ctrl+Shift+F12",
        ["privacy"] = "Ctrl+Shift+F8", ["marker"] = "Ctrl+Shift+F7", ["discard"] = "Ctrl+Shift+F6", ["bar"] = "Ctrl+Shift+F4",
        ["markerError"] = "Ctrl+Shift+F1", ["markerExpected"] = "Ctrl+Shift+F2"
    };
    /// <summary>Was aufgezeichnet wird (Schlüssel aus <see cref="Capture.RecordingOptions.Channels"/>); fehlende Schlüssel gelten als eingeschaltet.</summary>
    public Dictionary<string, bool> Record { get; set; } = new();
    public bool Records(string channel) => Record.GetValueOrDefault(channel, true);
    /// <summary>Modus „Eigene“: erweiterter Programmkontext.</summary>
    public bool CustomAppContext { get; set; } = true;
    /// <summary>Modus „Eigene“: was nach dem Stopp passiert (viewer | export | supportPackage | folder).</summary>
    public string CustomAfterStop { get; set; } = "viewer";
    public List<string> RecentSessions { get; set; } = new();
    public JsonObject? Extra { get; set; }

    static AppSettings? _cached;
    static readonly JsonSerializerOptions Opt = new(Json.Indented) { PropertyNameCaseInsensitive = true };

    public static AppSettings Load()
    {
        if (_cached != null) return _cached;
        var path = AppPaths.SettingsFile;
        try
        {
            if (File.Exists(path)) return _cached = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Opt) ?? new AppSettings();
        }
        catch
        {
            try { File.Move(path, path + ".defekt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
        }
        // Zuletzt verwendete Sitzungen liegen im Zustand, nicht in den Einstellungen
        return _cached = new AppSettings();
    }

    public void Save()
    {
        _cached = this;
        try { Json.WriteAtomic(AppPaths.SettingsFile, JsonSerializer.Serialize(this, Opt)); } catch { }
    }

    public void AddRecent(string path)
    {
        RecentSessions.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentSessions.Insert(0, path);
        if (RecentSessions.Count > 15) RecentSessions.RemoveRange(15, RecentSessions.Count - 15);
        Save();
    }
}
