using System.Text;
using System.Text.RegularExpressions;

namespace StepRecorder.Export;

/// <summary>
/// Verallgemeinert Pfade für AutoHotkey-Skripte, damit sie auf anderen PCs laufen: benutzerspezifische Ordner werden
/// durch eingebaute Variablen ersetzt (A_MyDocuments, A_Desktop, A_AppData, A_Temp, A_ProgramFiles, A_WinDir,
/// A_AppDataCommon, EnvGet("USERPROFILE"), EnvGet("LOCALAPPDATA") …), weitere Vorkommen des Benutzernamens durch A_UserName.
/// Ergebnis ist ein AHK-v2-Ausdruck (Verkettung mit Leerzeichen), z. B. <c>A_MyDocuments "\Berichte\a.xlsx"</c>.
/// Rein exportseitig – die Aufnahme bleibt unverändert.
/// </summary>
public static class AhkPaths
{
    const string End = @"(?=[\\/""'\s;,|]|$)";
    // Profilordner C:\Users\<Name> – Namen mit Leerzeichen nur, wenn ein weiterer Pfadteil folgt
    const string User = @"[A-Za-z]:\\Users\\(?<u>(?!(?:Public|Default|Default User|All Users)(?:\\|$))(?:[^\\/:*?""<>|\r\n]+?(?=\\)|[^\\/:*?""<>|\r\n\s]+))";

    static readonly (Regex Rx, string Expr)[] Rules =
    {
        (R(User + @"\\AppData\\Local\\Temp" + End), "A_Temp"),
        (R(User + @"\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup" + End), "A_Startup"),
        (R(User + @"\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs" + End), "A_Programs"),
        (R(User + @"\\AppData\\Roaming" + End), "A_AppData"),
        (R(User + @"\\AppData\\Local" + End), "EnvGet(\"LOCALAPPDATA\")"),
        (R(User + @"\\(?:Documents|Eigene Dateien|My Documents)" + End), "A_MyDocuments"),
        (R(User + @"\\Desktop" + End), "A_Desktop"),
        (R(User + @"\\OneDrive" + End), "EnvGet(\"OneDrive\")"),
        (R(User + End), "EnvGet(\"USERPROFILE\")"),
        (R(@"[A-Za-z]:\\Users\\Public" + End), "EnvGet(\"PUBLIC\")"),
        (R(@"[A-Za-z]:\\Program Files \(x86\)" + End), "EnvGet(\"ProgramFiles(x86)\")"),
        (R(@"[A-Za-z]:\\Program Files" + End), "A_ProgramFiles"),
        (R(@"[A-Za-z]:\\ProgramData" + End), "A_AppDataCommon"),
        (R(@"[A-Za-z]:\\Windows" + End), "A_WinDir"),
    };

    static Regex R(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    static readonly Regex UserIn = R(User + @"(?=\\|$)");

    /// <summary>Benutzername aus dem ersten Profilpfad (C:\Users\Name\…) im Text, sonst null.</summary>
    public static string? UserNameIn(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        try { var m = UserIn.Match(text); return m.Success ? m.Groups["u"].Value : null; } catch { return null; }
    }

    /// <summary>Wurde im Text etwas verallgemeinert?</summary>
    public static bool Generalizes(string? text, string? userName = null) => text != null && Expr(text, userName) != AhkExport.Q(text);

    /// <summary>
    /// AHK-Ausdruck für den Text: Pfadanfänge durch Built-ins ersetzt, übrige Vorkommen des Benutzernamens als
    /// eigener Pfadteil durch A_UserName. Ohne Treffer: die Zeichenkette in Anführungszeichen.
    /// </summary>
    public static string Expr(string text, string? userName = null)
    {
        if (string.IsNullOrEmpty(text)) return AhkExport.Q(text ?? "");
        var parts = new List<(bool expr, string s)>();
        int pos = 0;
        string? user = userName;
        try
        {
            while (pos < text.Length)
            {
                Match? best = null; string? bestExpr = null;
                foreach (var (rx, ex) in Rules)
                {
                    var m = rx.Match(text, pos);
                    if (!m.Success) continue;
                    if (best == null || m.Index < best.Index || (m.Index == best.Index && m.Length > best.Length)) { best = m; bestExpr = ex; }
                }
                if (best == null) break;
                // nur am Anfang eines Pfads (nicht mitten in einem Wort, z. B. „XC:\…“)
                if (best.Index > 0 && char.IsLetterOrDigit(text[best.Index - 1])) { Lit(parts, text[pos..(best.Index + 1)]); pos = best.Index + 1; continue; }
                if (best.Index > pos) Lit(parts, text[pos..best.Index]);
                parts.Add((true, bestExpr!));
                if (user == null && best.Groups["u"].Success) user = best.Groups["u"].Value;
                pos = best.Index + best.Length;
            }
        }
        catch (RegexMatchTimeoutException) { return AhkExport.Q(text); }
        if (pos < text.Length) Lit(parts, text[pos..]);

        // weitere Vorkommen des Benutzernamens als Pfadteil (z. B. \\server\home\<Name>\…)
        if (user is { Length: >= 3 })
        {
            var rx = new Regex(@"(?<=\\)" + Regex.Escape(user) + @"(?=\\|$)", RegexOptions.IgnoreCase);
            var split = new List<(bool expr, string s)>();
            foreach (var p in parts)
            {
                if (p.expr) { split.Add(p); continue; }
                int last = 0;
                foreach (Match m in rx.Matches(p.s))
                {
                    if (m.Index > last) split.Add((false, p.s[last..m.Index]));
                    split.Add((true, "A_UserName"));
                    last = m.Index + m.Length;
                }
                if (last < p.s.Length) split.Add((false, p.s[last..]));
            }
            parts = split;
        }
        if (!parts.Any(p => p.expr)) return AhkExport.Q(text);
        var sb = new StringBuilder();
        foreach (var (expr, s) in parts)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(expr ? s : AhkExport.Q(s));
        }
        return sb.ToString();
    }

    static void Lit(List<(bool expr, string s)> parts, string s)
    {
        if (s.Length == 0) return;
        if (parts.Count > 0 && !parts[^1].expr) parts[^1] = (false, parts[^1].s + s);
        else parts.Add((false, s));
    }
}
