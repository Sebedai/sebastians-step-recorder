using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Programm-Kontext eines Zeitraums für Ausgaben (MCP <c>get_failure_context</c>, <c>trace.md</c>, Fehlerbericht):
/// was die Anwendung tat – Programmende, Hänger, Fehlertexte, Einträge der Ereignisanzeige, Protokollzeilen (12, Pakete 10–13).
/// Liest ausschließlich über die Leseschicht: geschwärzte Zeiträume fehlen, ungeprüfte Inhalte erscheinen nicht.
/// </summary>
public static class DeveloperContext
{
    public static JsonObject? Build(SessionView v, long tFrom, long tTo, string lang)
    {
        var o = new JsonObject();
        return o.Count == 0 ? null : o;
    }
}
