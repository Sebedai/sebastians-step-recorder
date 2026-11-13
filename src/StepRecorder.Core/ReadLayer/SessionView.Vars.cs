using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.Text;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Variablen in Autorentexten (13, Paket 23): <c>{{Produkt}}</c> wird durch den Wert aus <c>guide.vars</c> (je Sprache
/// <c>guide.i18n.&lt;lang&gt;.vars</c>) oder einen eingebauten Wert (Titel, Version, Autor, Datum …) ersetzt – vor Schwärzung und
/// Verallgemeinerung, ein Wert unterliegt also denselben Regeln wie jeder andere Text. Unbekannte Namen bleiben wörtlich stehen.
/// Einfache Klammern (<c>{Benutzer}</c>) sind weiterhin Platzhalter der Verallgemeinerung.
/// </summary>
public sealed partial class SessionView
{
    static readonly Regex VarRx = new(@"\{\{(\p{L}[\p{L}\p{N} _.-]{0,39})\}\}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    /// <summary>Eingebaute Variablen (deutscher und englischer Name, Schlüssel).</summary>
    public static readonly (string de, string en, string key)[] BuiltInVars =
    {
        ("Titel", "Title", "title"), ("Untertitel", "Subtitle", "subtitle"), ("Version", "Version", "version"),
        ("Autor", "Author", "author"), ("Datum", "Date", "date"), ("Zielgruppe", "Audience", "audience"),
    };

    Dictionary<string, string>? _vars;

    /// <summary>Alle Variablen in der Sprache der Ansicht (eingebaute gewinnen gegen gleichnamige eigene).</summary>
    public IReadOnlyDictionary<string, string> Variables()
    {
        if (_vars != null) return _vars;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var guide = Edits.Obj("guide");
        void Add(JsonObject? vars) { foreach (var kv in vars ?? new JsonObject()) if (kv.Value is JsonValue v && v.TryGetValue<string>(out var s) && IsVarName(kv.Key)) map[kv.Key] = s; }
        Add(guide?.Obj("vars"));
        if (Language != "de") Add(guide?.Obj("i18n")?.Obj(Language)?.Obj("vars"));
        var doc = Export.DocSettings.From(guide, Language);
        foreach (var (de, en, key) in BuiltInVars)
        {
            string value = key switch
            {
                "title" => Export.DocSettings.GuideText(guide, "title", Language) is { Length: > 0 } t ? t : Title,
                "subtitle" => doc.Subtitle,
                "version" => doc.Version,
                "author" => doc.Author,
                "audience" => doc.Audience,
                _ => Session.StartUtc.ToLocalTime().ToString(Settings.OutText.DateFormat(Language))
            };
            map[de] = value; map[en] = value;
        }
        return _vars = map;
    }

    public static bool IsVarName(string? n) => n != null && Regex.IsMatch(n, @"^\p{L}[\p{L}\p{N} _.-]{0,39}$");

    /// <summary>Variablen einsetzen; mit <paramref name="escape"/> wird der Wert maskiert (in formatiertem Text bleibt er wörtlich).</summary>
    public string ExpandVars(string? s, bool escape = false)
    {
        if (string.IsNullOrEmpty(s) || !s.Contains("{{")) return s ?? "";
        var vars = Variables();
        try { return VarRx.Replace(s, m => vars.TryGetValue(m.Groups[1].Value.Trim(), out var v) ? (escape ? RichDoc.Escape(v) : v) : m.Value); }
        catch (RegexMatchTimeoutException) { return s; }
    }

    /// <summary>Namen in <c>{{…}}</c>, die keine bekannte Variable sind (Qualitätsregel Q31).</summary>
    public IEnumerable<string> UnknownVars(string? s)
    {
        if (string.IsNullOrEmpty(s) || !s.Contains("{{")) yield break;
        var vars = Variables();
        foreach (Match m in VarRx.Matches(s))
            if (!vars.ContainsKey(m.Groups[1].Value.Trim())) yield return m.Groups[1].Value.Trim();
    }

    /// <summary>Text eines Sprachobjekts <c>{ "de": "…", "en": "…" }</c>: eigene Sprache, sonst Deutsch; bereinigt, leer = null.</summary>
    string? LangText(JsonObject? o)
    {
        if (o == null) return null;
        string? t = o.Str(Language) is { Length: > 0 } own ? own : o.Str("de");
        return string.IsNullOrWhiteSpace(t) ? null : CleanVars(t);
    }

    /// <summary>Variablen einsetzen, dann bereinigen – für reine Autorentexte und Titel.</summary>
    public string CleanVars(string? s) => Clean(ExpandVars(s));
}
