using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StepRecorder.Mcp;

/// <summary>Ein MCP-Tool: Name, Beschreibung, JSON-Schema der Parameter, Ausführung.</summary>
public sealed record McpTool(string Name, string Description, JsonObject InputSchema, Func<JsonObject, McpToolResult> Handler, bool ReadOnly = true);

/// <summary>Ergebnis eines Tool-Aufrufs: Inhaltsblöcke (Text/Bild) und optional strukturierte Daten.</summary>
public sealed class McpToolResult
{
    public List<JsonObject> Content { get; } = new();
    public bool IsError { get; set; }
    public JsonNode? Structured { get; set; }

    public static McpToolResult Text(string text) { var r = new McpToolResult(); r.AddText(text); return r; }
    public static McpToolResult Json(JsonNode node)
    {
        var r = new McpToolResult { Structured = node };
        r.AddText(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return r;
    }
    public static McpToolResult Error(string code, string message)
    {
        var r = Json(new JsonObject { ["error"] = new JsonObject { ["code"] = code, ["message"] = message } });
        r.IsError = true;
        return r;
    }
    public McpToolResult AddText(string text) { Content.Add(new JsonObject { ["type"] = "text", ["text"] = text }); return this; }
    public McpToolResult AddImage(byte[] data, string mime) { Content.Add(new JsonObject { ["type"] = "image", ["data"] = Convert.ToBase64String(data), ["mimeType"] = mime }); return this; }
}

/// <summary>
/// Schlanker MCP-Server über stdio (JSON-RPC 2.0, eine Nachricht je Zeile). Ohne Netzwerk.
/// stdout enthält ausschließlich Protokollnachrichten; Diagnose geht nach stderr.
/// </summary>
public sealed class McpStdioServer
{
    static readonly string[] SupportedVersions = { "2025-06-18", "2025-03-26", "2024-11-05" };
    readonly Dictionary<string, McpTool> _tools = new();
    readonly string _name, _version, _instructions;
    readonly TextReader _in;
    readonly TextWriter _out;
    readonly TextWriter _log;

    public McpStdioServer(string name, string version, string instructions, TextReader input, TextWriter output, TextWriter log)
    {
        _name = name; _version = version; _instructions = instructions; _in = input; _out = output; _log = log;
    }

    public void Register(McpTool tool) => _tools[tool.Name] = tool;
    public IReadOnlyCollection<McpTool> Tools => _tools.Values;

    public void Run(CancellationToken ct = default)
    {
        string? line;
        while (!ct.IsCancellationRequested && (line = _in.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonNode? msg;
            try { msg = JsonNode.Parse(line); }
            catch (Exception ex) { Send(ErrorResponse(null, -32700, "Parse error: " + ex.Message)); continue; }
            if (msg is JsonArray batch) { foreach (var m in batch) if (m is JsonObject o) HandleSafe(o); }
            else if (msg is JsonObject obj) HandleSafe(obj);
        }
    }

    void HandleSafe(JsonObject msg)
    {
        try { var resp = Handle(msg); if (resp != null) Send(resp); }
        catch (Exception ex)
        {
            _log.WriteLine($"[mcp] Fehler: {ex}");
            if (msg["id"] != null) Send(ErrorResponse(msg["id"], -32603, "Internal error: " + ex.Message));
        }
    }

    public JsonObject? Handle(JsonObject msg)
    {
        var id = msg["id"]?.DeepClone();
        string? method = msg["method"]?.GetValue<string>();
        if (method == null) return null; // Antwort auf eigene Anfrage – ignorieren
        var prm = msg["params"] as JsonObject ?? new JsonObject();
        bool isNotification = id == null;

        switch (method)
        {
            case "initialize":
                {
                    string requested = prm["protocolVersion"]?.GetValue<string>() ?? SupportedVersions[0];
                    string version = SupportedVersions.Contains(requested) ? requested : SupportedVersions[0];
                    return Result(id, new JsonObject
                    {
                        ["protocolVersion"] = version,
                        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
                        ["serverInfo"] = new JsonObject { ["name"] = _name, ["version"] = _version },
                        ["instructions"] = _instructions
                    });
                }
            case "notifications/initialized":
            case "notifications/cancelled":
                return null;
            case "ping":
                return Result(id, new JsonObject());
            case "tools/list":
                {
                    var arr = new JsonArray();
                    foreach (var t in _tools.Values)
                        arr.Add(new JsonObject
                        {
                            ["name"] = t.Name,
                            ["description"] = t.Description,
                            ["inputSchema"] = t.InputSchema.DeepClone(),
                            ["annotations"] = new JsonObject { ["readOnlyHint"] = t.ReadOnly, ["openWorldHint"] = false }
                        });
                    return Result(id, new JsonObject { ["tools"] = arr });
                }
            case "tools/call":
                {
                    string name = prm["name"]?.GetValue<string>() ?? "";
                    var args = prm["arguments"] as JsonObject ?? new JsonObject();
                    if (!_tools.TryGetValue(name, out var tool))
                        return ErrorResponse(id, -32602, $"Unknown tool: {name}");
                    McpToolResult r;
                    try { r = tool.Handler(args); }
                    catch (McpToolException ex) { r = McpToolResult.Error(ex.Code, ex.Message); }
                    catch (Exception ex) { _log.WriteLine($"[mcp] {name}: {ex}"); r = McpToolResult.Error("internal", ex.Message); }
                    var res = new JsonObject { ["content"] = new JsonArray(r.Content.Select(c => (JsonNode)c).ToArray()), ["isError"] = r.IsError };
                    if (r.Structured is JsonObject so) res["structuredContent"] = so.DeepClone();
                    return Result(id, res);
                }
            case "resources/list": return Result(id, new JsonObject { ["resources"] = new JsonArray() });
            case "prompts/list": return Result(id, new JsonObject { ["prompts"] = new JsonArray() });
            default:
                return isNotification ? null : ErrorResponse(id, -32601, $"Method not found: {method}");
        }
    }

    static JsonObject Result(JsonNode? id, JsonObject result) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
    static JsonObject ErrorResponse(JsonNode? id, int code, string message)
        => new() { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

    readonly object _sendLock = new();
    void Send(JsonObject msg)
    {
        var s = msg.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        lock (_sendLock) { _out.Write(s); _out.Write('\n'); _out.Flush(); }
    }
}

public sealed class McpToolException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
