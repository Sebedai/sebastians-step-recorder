using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.Mcp;

/// <summary>Bitte eines KI-Agenten um eine Vorführung (Text ungeprüft – er erscheint nur als Hinweis im Dialog).</summary>
public sealed class RecordingRequest
{
    public string Id = "";
    public string Goal = "";
    public string? Expected, Title;
    /// <summary>Prüfaufnahme: Kennung der Ausgangsaufnahme (nur eine Sitzungskennung, kein Pfad).</summary>
    public string? RetestOf;
    /// <summary>Ablageordner des MCP-Servers (vom Benutzer eingerichtet, nie vom Agenten gewählt).</summary>
    public string? Root;
    public DateTime CreatedUtc, ExpiresUtc;

    public JsonObject ToJson() => new JsonObject
    {
        ["id"] = Id, ["goal"] = Goal, ["expected"] = Expected, ["title"] = Title, ["retestOf"] = RetestOf, ["root"] = Root,
        ["createdUtc"] = CreatedUtc.ToString("o"), ["expiresUtc"] = ExpiresUtc.ToString("o")
    }.Clean();

    public static RecordingRequest? FromJson(JsonObject? o)
    {
        if (o?.Str("id") is not { } id) return null;
        DateTime P(string k) => DateTime.TryParse(o.Str(k), null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.MinValue;
        return new RecordingRequest { Id = id, Goal = o.Str("goal") ?? "", Expected = o.Str("expected"), Title = o.Str("title"), RetestOf = o.Str("retestOf"), Root = o.Str("root"), CreatedUtc = P("createdUtc"), ExpiresUtc = P("expiresUtc") };
    }
}

/// <summary>
/// Agent-Anfragen (6.3) über Dateien unter <c>StateDir\requests\</c>: <c>rq-&lt;id&gt;.request.json</c> schreibt der MCP-Server,
/// <c>rq-&lt;id&gt;.status.json</c> die Oberfläche. Zustände: pending · declined · expired · recording · finished · discarded · failed.
/// </summary>
public static class RecordingRequests
{
    public const int TimeoutSeconds = 120;
    public const int GoalMin = 10, GoalMax = 500, ExpectedMax = 500, TitleMax = 120;
    public static readonly string[] States = { "pending", "declined", "expired", "recording", "finished", "discarded", "failed" };
    static readonly HashSet<string> Final = new() { "declined", "expired", "finished", "discarded", "failed" };
    static readonly Regex IdPattern = new("^rq-[0-9a-f]{12}$", RegexOptions.CultureInvariant);

    /// <summary>Nur für Tests.</summary>
    public static string? DirOverride { get; set; }
    public static string Dir => DirOverride ?? Path.Combine(AppPaths.StateDir, "requests");

    static string RequestFile(string id) => Path.Combine(Dir, id + ".request.json");
    static string StatusFile(string id) => Path.Combine(Dir, id + ".status.json");
    public static bool ValidId(string? id) => id != null && IdPattern.IsMatch(id);

    /// <summary>Text des Agenten bereinigen: keine Steuerzeichen (außer Zeilenumbruch), keine Zeichen zur Schreibrichtung, Länge begrenzt.</summary>
    public static string Sanitize(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(Math.Min(s.Length, max + 1));
        foreach (var c in s.Replace("\r\n", "\n"))
        {
            if (c == '\n') { sb.Append('\n'); continue; }
            if (char.IsControl(c) || c is >= '​' and <= '‏' or >= '‪' and <= '‮' or >= '⁦' and <= '⁩' or '﻿') continue;
            sb.Append(c);
        }
        var t = Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n").Trim();
        return t.Length > max ? t[..max] : t;
    }

    public static RecordingRequest Create(string goal, string? expected, string? title, string? root, DateTime? nowUtc = null, string? retestOf = null)
    {
        if (retestOf != null && !Regex.IsMatch(retestOf, "^[0-9a-f-]{36}$")) throw new ArgumentException("retestOf must be a session id from list_sessions.");
        string g = Sanitize(goal, GoalMax + 1);
        if (g.Length < GoalMin || g.Length > GoalMax) throw new ArgumentException($"goal must be {GoalMin}–{GoalMax} characters.");
        string? e = expected == null ? null : Sanitize(expected, ExpectedMax + 1);
        if (e is { Length: > ExpectedMax }) throw new ArgumentException($"expected must be at most {ExpectedMax} characters.");
        string? t = title == null ? null : Sanitize(title, TitleMax + 1).Replace('\n', ' ');
        if (t is { Length: > TitleMax }) throw new ArgumentException($"title must be at most {TitleMax} characters.");
        var now = nowUtc ?? DateTime.UtcNow;
        var rq = new RecordingRequest
        {
            Id = "rq-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant(), Goal = g,
            Expected = string.IsNullOrEmpty(e) ? null : e, Title = string.IsNullOrEmpty(t) ? null : t, Root = root,
            CreatedUtc = now, ExpiresUtc = now.AddSeconds(TimeoutSeconds), RetestOf = retestOf
        };
        Directory.CreateDirectory(Dir);
        Json.WriteAtomic(RequestFile(rq.Id), Json.Pretty(rq.ToJson()));
        return rq;
    }

    public static RecordingRequest? Load(string? id) => ValidId(id) ? RecordingRequest.FromJson(Json.ReadObject(RequestFile(id!))) : null;

    /// <summary>Zustand einer Anfrage (unknown, wenn es sie nicht gibt).</summary>
    public static (string state, string? sessionId, string? reason) Status(string id, DateTime? nowUtc = null)
    {
        if (!ValidId(id)) return ("unknown", null, null);
        if (Json.ReadObject(StatusFile(id)) is { } s && s.Str("state") is { } st) return (st, s.Str("sessionId"), s.Str("reason"));
        var rq = Load(id);
        if (rq == null) return ("unknown", null, null);
        return (nowUtc ?? DateTime.UtcNow) > rq.ExpiresUtc ? ("expired", null, null) : ("pending", null, null);
    }

    /// <summary>Zustand setzen (Oberfläche). Abgeschlossene Zustände werden nie überschrieben.</summary>
    public static bool SetStatus(string id, string state, string? sessionId = null, string? reason = null)
    {
        if (!ValidId(id) || !States.Contains(state)) return false;
        var cur = Json.ReadObject(StatusFile(id))?.Str("state");
        if (cur != null && Final.Contains(cur)) return false;
        if (cur == "recording" && state is "pending" or "declined" or "expired") return false;
        Directory.CreateDirectory(Dir);
        Json.WriteAtomic(StatusFile(id), Json.Pretty(new JsonObject { ["id"] = id, ["state"] = state, ["sessionId"] = sessionId, ["reason"] = reason, ["atUtc"] = Json.UtcNow() }.Clean()));
        return true;
    }

    /// <summary>Alte Anfragen (älter als ein Tag) entfernen.</summary>
    public static void Cleanup(DateTime? nowUtc = null)
    {
        try
        {
            if (!Directory.Exists(Dir)) return;
            var limit = (nowUtc ?? DateTime.UtcNow).AddDays(-1);
            foreach (var f in Directory.EnumerateFiles(Dir, "rq-*.json"))
                if (File.GetLastWriteTimeUtc(f) < limit) try { File.Delete(f); } catch { }
        }
        catch { }
    }
}

/// <summary>
/// Missbrauchsschutz für Agent-Anfragen (von der Oberfläche erzwungen): höchstens eine offene Anfrage, 60 s Abstand,
/// fünf je Stunde, auf Wunsch 15 Minuten Sperre. Kein „immer erlauben“.
/// </summary>
public static class RequestPolicy
{
    public const int MaxPerHour = 5;
    public static readonly TimeSpan MinGap = TimeSpan.FromSeconds(60), BlockFor = TimeSpan.FromMinutes(15);

    public static (bool ok, string reason) Check(DateTime nowUtc, IReadOnlyList<DateTime> recentUtc, DateTime? blockedUntilUtc, bool anyOpen)
    {
        if (blockedUntilUtc is { } b && b > nowUtc) return (false, "blocked");
        if (anyOpen) return (false, "busy");
        if (recentUtc.Any(r => nowUtc - r < MinGap)) return (false, "tooSoon");
        if (recentUtc.Count(r => nowUtc - r < TimeSpan.FromHours(1)) >= MaxPerHour) return (false, "hourlyLimit");
        return (true, "");
    }
}
