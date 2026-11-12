using System.Globalization;
using System.Text;

namespace StepRecorder.Export;

/// <summary>Schreibweisen für Ausgaben (06 §5 Notation, Entschärfung E1–E9).</summary>
public static class Fmt
{
    public static CultureInfo Culture(string lang) => lang == "de" ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;

    /// <summary>Zeit seit Aufnahmebeginn: HH:MM:SS,mmm (Englisch mit Punkt).</summary>
    public static string Time(long us, string lang)
    {
        var ts = TimeSpan.FromTicks(Math.Max(0, us) * 10);
        string sep = lang == "de" ? "," : ".";
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}{sep}{ts.Milliseconds:000}";
    }

    public static string Dur(long us, string lang)
    {
        if (us < 1_000_000) return $"{us / 1000} ms";
        if (us < 60_000_000) return (us / 1e6).ToString("0.0", Culture(lang)) + " s";
        var ts = TimeSpan.FromTicks(us * 10);
        return $"{(int)ts.TotalMinutes} min {ts.Seconds} s";
    }

    public static string Pct(double ratio, string lang)
    {
        if (ratio > 0 && ratio < 0.001) return lang == "de" ? "unter 0,1 %" : "under 0.1 %";
        return (ratio * 100).ToString("0.0", Culture(lang)) + " %";
    }

    public static string Num(double v, string lang, string format = "0.##") => v.ToString(format, Culture(lang));

    /// <summary>Aufgezeichneten Text als Codespanne ausgeben (E1–E6): unsichtbare Zeichen sichtbar, Länge begrenzt.</summary>
    public static string Code(string? s, int max = 120, string lang = "de")
    {
        s ??= "";
        var sb = new StringBuilder();
        int count = 0;
        foreach (var ch in s)
        {
            if (count >= max) { sb.Append(lang == "de" ? $"…[+{s.Length - max} Zeichen]" : $"…[+{s.Length - max} chars]"); break; }
            sb.Append(ch switch
            {
                '\n' => "⏎", '\r' => "", '\t' => "⇥",
                _ when char.IsControl(ch) || ch is '​' or '‌' or '‍' or '‎' or '‏' or '‪' or '‫' or '‬' or '‭' or '‮' or '⁦' or '⁧' or '⁨' or '⁩' or '﻿'
                    => $"\\u{{{(int)ch:X4}}}",
                _ => ch.ToString()
            });
            count++;
        }
        var text = sb.ToString();
        int ticks = 1;
        while (text.Contains(new string('`', ticks))) ticks++;
        var fence = new string('`', ticks);
        bool pad = text.StartsWith('`') || text.EndsWith('`');
        return fence + (pad ? " " : "") + text + (pad ? " " : "") + fence;
    }

    /// <summary>
    /// Eine Zeile aufgezeichneten Textes innerhalb eines Codeblocks (Stacktrace, Protokollzeile): Zaun aus Backticks und
    /// unsichtbare Steuer- und Schreibrichtungszeichen entschärft, höchstens <paramref name="max"/> Zeichen.
    /// </summary>
    public static string CodeLine(string? s, int max = 400)
    {
        s = (s ?? "").TrimEnd('\r');
        if (s.Length > max) s = s[..max] + "…";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(ch switch
            {
                '\t' => "    ",
                _ when char.IsControl(ch) || ch is '​' or '‌' or '‍' or '‎' or '‏' or '‪' or '‫' or '‬' or '‭' or '‮' or '⁦' or '⁧' or '⁨' or '⁩' or '﻿' => $"\\u{{{(int)ch:X4}}}",
                _ => ch.ToString()
            });
        return sb.ToString().Replace("```", "ˋˋˋ");
    }

    /// <summary>Text in Anführungszeichen, Markdown-Sonderzeichen erzwingen eine Codespanne.</summary>
    public static string Quote(string? s, string lang, int max = 120)
    {
        s ??= "";
        if (s.IndexOfAny(new[] { '*', '_', '[', ']', '<', '>', '#', '`', '|', '\\', '\n', '!' }) >= 0 || s.Length > max) return Code(s, max);
        return lang == "de" ? $"„{s}“" : $"“{s}”";
    }

    public static string HtmlEscape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    public static string MdEscape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        foreach (var c in s) { if ("\\`*_{}[]()#+-.!|<>".Contains(c)) sb.Append('\\'); sb.Append(c); }
        return sb.ToString();
    }

    /// <summary>
    /// Nur ASCII-Buchstaben, Ziffern und Bindestriche (Umlaute umschrieben, Akzente entfernt) – für Markdown-Dateien und
    /// Bilderordner in Versionsverwaltung und Wikis (09 §6.1, EXP-043).
    /// </summary>
    public static string AsciiSlug(string? s, string fallback = "anleitung", int max = 60)
    {
        s ??= "";
        var map = new Dictionary<char, string> { ['ä'] = "ae", ['ö'] = "oe", ['ü'] = "ue", ['Ä'] = "Ae", ['Ö'] = "Oe", ['Ü'] = "Ue", ['ß'] = "ss", ['æ'] = "ae", ['ø'] = "oe", ['å'] = "aa", ['Æ'] = "Ae", ['Ø'] = "Oe", ['Å'] = "Aa", ['œ'] = "oe", ['Œ'] = "Oe" };
        var sb = new StringBuilder();
        foreach (var c in s) sb.Append(map.TryGetValue(c, out var r) ? r : c.ToString());
        var plain = new StringBuilder();
        foreach (var c in sb.ToString().Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            plain.Append(c < 128 && char.IsLetterOrDigit(c) ? c : '-');
        }
        var t = System.Text.RegularExpressions.Regex.Replace(plain.ToString(), "-{2,}", "-").Trim('-');
        if (t.Length > max) t = t[..max].TrimEnd('-');
        return t.Length == 0 ? fallback : t;
    }

    public static string FileSafe(string s, int max = 40)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var t = new string(s.Select(c => invalid.Contains(c) || c is '„' or '“' or '"' ? '_' : c).ToArray()).Trim(' ', '.', '_');
        return t.Length > max ? t[..max].Trim() : t;
    }
}
