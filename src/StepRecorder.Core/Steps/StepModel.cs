using System.Text.Json.Nodes;

namespace StepRecorder.Steps;

/// <summary>Arbeitsobjekt eines Schritts während der Ableitung; wird als JSON nach steps.json geschrieben.</summary>
public sealed class StepDraft
{
    public string Kind = "";
    public long AnchorSeq;
    public long TFrom, TTo;
    public readonly SortedSet<long> Input = new();
    public readonly SortedSet<long> Approach = new();
    public readonly SortedSet<long> Related = new();
    public JsonObject? Action;
    public string Importance = "primary";
    public JsonObject Evidence = new();
    public JsonObject? Ctx;
    public JsonObject? Target, EndTarget, Position, EndPosition;
    public JsonObject InputObj = new();
    public JsonObject? Effect;
    public JsonObject Frames = new();
    public JsonObject? ApproachObj;
    public JsonObject? Clipboard;
    public string? GapId;
    public readonly SortedSet<string> Flags = new();
    public readonly JsonArray Prov = new();
    public List<long> Comments = new();
    // interne Hilfen
    public string? Monitor;
    public string? Win;
    public long LastInputSeq;
    public long FirstInputSeq;
    public bool IsAction => Kind is "click" or "doubleClick" or "tripleClick" or "rightClick" or "drag" or "scroll" or "type" or "hotkey" or "key" or "hover" or "windowSwitch";
    public bool IsMouse => Kind is "click" or "doubleClick" or "tripleClick" or "rightClick" or "drag" or "scroll" or "hover";
}

public static class StepParams
{
    /// <summary>1.6.0 = Zusammenführung: Terminal-Klartext und UAC-Schritt (1.4/1.5) mit Menüpfaden und Begriffsliste (1.3.1).</summary>
    public const string BuilderVersion = "1.6.0";
}
