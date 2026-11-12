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
    public static string Code(string? s, int max = 120)
    {
        s ??= "";
        var sb = new StringBuilder();
        int count = 0;
        foreach (var ch in s)
        {
            if (count >= max) { sb.Append($"…[+{s.Length - max} Zeichen]"); break; }
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

    public static string FileSafe(string s, int max = 40)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var t = new string(s.Select(c => invalid.Contains(c) || c is '„' or '“' or '"' ? '_' : c).ToArray()).Trim(' ', '.', '_');
        return t.Length > max ? t[..max].Trim() : t;
    }
}
