using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StepRecorder.Data;

/// <summary>Gemeinsame JSON-Einstellungen und Helfer für tolerantes Lesen.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Compact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    public static readonly JsonSerializerOptions Indented = new(Compact) { WriteIndented = true };

    public static string Line(JsonNode node) => node.ToJsonString(Compact);
    public static string Pretty(JsonNode node) => node.ToJsonString(Indented);

    public static string? Str(this JsonNode? n, string key) => n is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;
    public static long Long(this JsonNode? n, string key, long def = 0)
    {
        if (n is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue jv)
        {
            if (jv.TryGetValue<long>(out var l)) return l;
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<double>(out var d)) return (long)d;
        }
        return def;
    }
    public static long? LongN(this JsonNode? n, string key)
    {
        if (n is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue jv)
        {
            if (jv.TryGetValue<long>(out var l)) return l;
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<double>(out var d)) return (long)d;
        }
        return null;
    }
    public static int Int(this JsonNode? n, string key, int def = 0) => (int)n.Long(key, def);
    public static double Dbl(this JsonNode? n, string key, double def = 0)
    {
        if (n is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue jv)
        {
            if (jv.TryGetValue<double>(out var d)) return d;
            if (jv.TryGetValue<long>(out var l)) return l;
        }
        return def;
    }
    public static bool Bool(this JsonNode? n, string key) => n is JsonObject o && o.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<bool>(out var b) && b;
    public static JsonObject? Obj(this JsonNode? n, string key) => n is JsonObject o && o.TryGetPropertyValue(key, out var v) ? v as JsonObject : null;
    public static JsonArray? Arr(this JsonNode? n, string key) => n is JsonObject o && o.TryGetPropertyValue(key, out var v) ? v as JsonArray : null;
    public static List<string> StrList(this JsonNode? n, string key)
        => n.Arr(key)?.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).Where(s => s != null).Select(s => s!).ToList() ?? new();

    /// <summary>Entfernt null-Werte aus einem Objekt (rekursiv flach).</summary>
    public static JsonObject Clean(this JsonObject o)
    {
        foreach (var k in o.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList()) o.Remove(k);
        return o;
    }

    public static string UtcNow() => Utc(DateTime.UtcNow);
    public static string Utc(DateTime dt) => dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
    public static DateTime ParseUtc(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : DateTime.MinValue;

    public static string Hex(nint h) => "0x" + ((long)h).ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>Schreibt eine Datei atomar (temporär + Umbenennen).</summary>
    public static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var w = new StreamWriter(fs, new System.Text.UTF8Encoding(false)))
        {
            w.Write(content);
            w.Flush();
            fs.Flush(true);
        }
        File.Move(tmp, path, true);
    }

    public static JsonObject? ReadObject(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { MaxDepth = 64 }) as JsonObject; }
        catch { return null; }
    }

    /// <summary>UUID v7 (zeitgeordnet), klein geschrieben.</summary>
    public static string NewUuid7()
    {
        Span<byte> b = stackalloc byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(b);
        long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        b[0] = (byte)(ms >> 40); b[1] = (byte)(ms >> 32); b[2] = (byte)(ms >> 24); b[3] = (byte)(ms >> 16); b[4] = (byte)(ms >> 8); b[5] = (byte)ms;
        b[6] = (byte)(0x70 | (b[6] & 0x0F));
        b[8] = (byte)(0x80 | (b[8] & 0x3F));
        var hex = Convert.ToHexString(b).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}
