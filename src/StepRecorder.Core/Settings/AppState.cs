using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StepRecorder.Data;

namespace StepRecorder.Settings;

/// <summary>Fensterlage in physischen Pixeln samt DPI beim Speichern.</summary>
public sealed class WindowBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public bool Max { get; set; }
    public int Dpi { get; set; } = 96;
}

/// <summary>Merkzettel je Aufnahme (Editor): zuletzt gewählter Schritt, Filter, Darstellung, letzter Export.</summary>
public sealed class SessionUiState
{
    public string At { get; set; } = "";
    public long? Step { get; set; }
    public bool All { get; set; }
    public string? Display { get; set; }
    public double? Zoom { get; set; }
    public JsonObject? Export { get; set; }
}

public sealed class ViewerLayoutState
{
    public double? Left { get; set; }
    public double? Right { get; set; }
    public int Tab { get; set; }
}

public sealed class ExportState
{
    public string? LastFormat { get; set; }
    /// <summary>Je Format: zuletzt gewählte Optionen und Zielordner.</summary>
    public Dictionary<string, JsonObject> Formats { get; set; } = new();
}

public sealed class LibraryState
{
    public string Sort { get; set; } = "date";
    public string Mode { get; set; } = "titles";
    public string? PackFolder { get; set; }
}

/// <summary>Zuletzt gewählter Aufnahmebereich (physische Pixel, virtueller Bildschirm) samt Monitor.</summary>
public sealed class SavedRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public string Device { get; set; } = "";
    public int MonX { get; set; }
    public int MonY { get; set; }
    public int MonW { get; set; }
    public int MonH { get; set; }
    public int Dpi { get; set; } = 96;
}

/// <summary>
/// Flüchtiger Oberflächenzustand (StepRecorder.state.json) – keine Vorlieben, keine Inhalte.
/// Nur die Oberfläche schreibt; Kommandozeile und MCP lesen höchstens.
/// </summary>
public sealed class AppState
{
    public const int MaxSessions = 50;

    public int Version { get; set; } = 1;
    /// <summary>Zuletzt beendete Aufnahme (für <c>record --stop</c>).</summary>
    public string? LastStopped { get; set; }
    public string? AtUtc { get; set; }
    public Dictionary<string, WindowBounds> Windows { get; set; } = new();
    public ViewerLayoutState Viewer { get; set; } = new();
    public ExportState Export { get; set; } = new();
    public LibraryState Library { get; set; } = new();
    public SavedRegion? LastRegion { get; set; }
    public Dictionary<string, SessionUiState> Sessions { get; set; } = new();
    /// <summary>Agent-Anfragen: Zeitpunkte und Sperre (06 §21).</summary>
    public JsonObject? Requests { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Unknown { get; set; }

    static AppState? _cached;
    static readonly object Gate = new();
    static readonly JsonSerializerOptions Opt = new(Json.Indented) { PropertyNameCaseInsensitive = true };

    public static AppState Load() { lock (Gate) return _cached ??= LoadFrom(AppPaths.StateFile); }

    /// <summary>Liest tolerant; aus einer unlesbaren Datei wird nur <c>lastStopped</c> gerettet.</summary>
    public static AppState LoadFrom(string path)
    {
        if (!File.Exists(path)) return new AppState();
        try
        {
            var s = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path), Opt) ?? new AppState();
            s.Windows ??= new(); s.Viewer ??= new(); s.Export ??= new(); s.Export.Formats ??= new(); s.Library ??= new(); s.Sessions ??= new();
            return s;
        }
        catch
        {
            var o = Json.ReadObject(path);
            return new AppState { LastStopped = o.Str("lastStopped"), AtUtc = o.Str("atUtc") };
        }
    }

    public bool Save()
    {
        lock (Gate) { _cached = this; return SaveTo(AppPaths.StateFile); }
    }

    public bool SaveTo(string path)
    {
        try { Json.WriteAtomic(path, JsonSerializer.Serialize(this, Opt)); return true; }
        catch (Exception ex) { AppLog.Write("Zustand nicht gespeichert: " + ex.Message); return false; }
    }

    /// <summary>Ändern und speichern in einem Schritt.</summary>
    public static void Update(Action<AppState> change)
    {
        var s = Load();
        lock (Gate) change(s);
        s.Save();
    }

    /// <summary>Merkzettel einer Aufnahme (anlegen, „zuletzt benutzt“ setzen, älteste über <see cref="MaxSessions"/> verwerfen).</summary>
    public SessionUiState Session(string sessionId)
    {
        lock (Gate)
        {
            if (!Sessions.TryGetValue(sessionId, out var st)) Sessions[sessionId] = st = new SessionUiState();
            st.At = Json.UtcNow();
            if (Sessions.Count > MaxSessions)
                foreach (var old in Sessions.OrderBy(kv => kv.Value.At, StringComparer.Ordinal).Take(Sessions.Count - MaxSessions).Select(kv => kv.Key).ToList())
                    Sessions.Remove(old);
            return st;
        }
    }

    /// <summary>Nur lesen, ohne anzulegen.</summary>
    public SessionUiState? PeekSession(string sessionId) { lock (Gate) return Sessions.TryGetValue(sessionId, out var s) ? s : null; }

    /// <summary>Nur für Tests.</summary>
    public static void ResetCache() { lock (Gate) _cached = null; }
}
