using System.Text.RegularExpressions;

namespace StepRecorder.Capture;

/// <summary>Treffer einer Terminal-Regel: Regel (<c>T1</c>, <c>T2</c>, <c>T3</c>) und erkannte Abfragezeile (höchstens 120 Zeichen).</summary>
public sealed record TerminalPromptHit(string Rule, string PromptLine);

/// <summary>Schlüsselwort der Regel T1; <see cref="WholeWord"/>: nur als eigenes Wort (sonst auch in Komposita wie „Domänenkennwort“).</summary>
public sealed record PromptKeyword(string Word, bool WholeWord = false);

/// <summary>
/// Erkennung von Passwortabfragen im Terminal (10 §6.2, DS-022/DS-023) – reine Regeln ohne Zugriff auf Fenster:
/// <list type="bullet">
/// <item><b>T1 Mustertreffer:</b> Die letzte nicht leere Zeile enthält ein Schlüsselwort, gefolgt von höchstens 60 weiteren
/// Zeichen und einem Doppelpunkt, Fragezeichen oder Größer-Zeichen am Zeilenende.</item>
/// <item><b>T2 Maskenecho:</b> Getippte Zeichen erscheinen als gleich viele Maskenzeichen (<c>*</c>, <c>•</c>, <c>●</c>).</item>
/// <item><b>T3 Kein Echo an einer Abfrage:</b> mindestens zwei Zeichen getippt, die Zeile ist unverändert und endet auf
/// Doppelpunkt, Fragezeichen oder Größer-Zeichen.</item>
/// </list>
/// Eingabe sind höchstens die letzten drei nicht leeren Zeilen vor und nach dem Tippen; das Ergebnis enthält vom gelesenen
/// Text nur die erkannte Abfragezeile (10 §6.3). Gewöhnliche Eingabeaufforderungen von Shells (<c>PS C:\&gt;</c>,
/// <c>C:\&gt;</c>, <c>&gt;&gt;&gt;</c>) gelten nie als Abfrage – auch dann nicht, wenn ein Verzeichnisname ein Schlüsselwort enthält
/// oder das Echo verspätet ankommt.
/// </summary>
public static class TerminalPrompt
{
    /// <summary>Schlüsselwörter der Regel T1 (10 §6.2) – als Daten hinterlegt und erweiterbar; Groß-/Kleinschreibung egal.</summary>
    public static readonly IReadOnlyList<PromptKeyword> Keywords = new PromptKeyword[]
    {
        new("password"), new("passwort"), new("kennwort"), new("passphrase"), new("passcode"),
        new("pin", WholeWord: true),            // nie in „mapping“, „spinning“, „ping“
        new("secret"), new("token"),
        new("mot de passe"), new("contraseña"), new("senha"), new("wachtwoord"), new("hasło"),
        new("пароль"), new("密码"), new("パスワード"),
    };

    /// <summary>Zeichen, auf die eine Abfragezeile endet: Doppelpunkt, Fragezeichen, Größer-Zeichen (auch in voller Breite für CJK).</summary>
    public const string PromptEnd = ":?>：？＞";
    /// <summary>Maskenzeichen der Regel T2.</summary>
    public const string MaskChars = "*•●";
    /// <summary>Höchstens so viele Zeichen zwischen Schlüsselwort und Zeilenende (T1).</summary>
    public const int MaxTail = 60;
    /// <summary>Höchstlänge der gespeicherten Abfragezeile (DS-023).</summary>
    public const int PromptLineMax = 120;
    /// <summary>Höchstens so viele nicht leere Zeilen werden betrachtet (10 §6.1).</summary>
    public const int MaxLines = 3;

    /// <summary>Gewöhnliche Eingabeaufforderungen von Shells: nie eine Passwortabfrage (gegen Fehlalarme durch Verzeichnisnamen und verspätetes Echo).</summary>
    public static readonly IReadOnlyList<Regex> ShellPrompts = new[]
    {
        new Regex(@"^PS [^\r\n]*>$", RegexOptions.CultureInvariant),                 // PowerShell: PS C:\Users\x>
        new Regex(@"^[A-Za-z]:\\[^\r\n<>]*>$", RegexOptions.CultureInvariant),         // Eingabeaufforderung: C:\Users\x>
        new Regex(@"^(>>>|\.\.\.)$", RegexOptions.CultureInvariant),                  // Python
    };

    static readonly Regex T1Pattern = BuildT1();

    static Regex BuildT1()
    {
        var alts = Keywords.Select(k =>
        {
            string w = string.Join(@"\s+", k.Word.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));
            return k.WholeWord ? @"(?<![\p{L}\p{N}])" + w + @"(?![\p{L}])" : w;
        });
        return new Regex("(?:" + string.Join("|", alts) + ")[^\\r\\n]{0," + MaxTail + "}[" + Regex.Escape(PromptEnd) + "]$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Zeile normalisieren: Leerraum am Ende weg (Konsolen füllen Zeilen mit Leerzeichen auf).</summary>
    static string Norm(string? s) => (s ?? "").TrimEnd(' ', '\t', '\u00A0', '\r', '\n', '\0');

    /// <summary>Letzte nicht leere Zeile.</summary>
    static string? Last(IReadOnlyList<string>? lines)
    {
        if (lines == null) return null;
        for (int i = lines.Count - 1; i >= 0; i--) { var n = Norm(lines[i]); if (n.Trim().Length > 0) return n; }
        return null;
    }

    /// <summary>Höchstens <paramref name="max"/> letzte nicht leere Zeilen eines gelesenen Texts (Zeilenende ohne Leerraum).</summary>
    public static string[] TailLines(string? text, int max = MaxLines)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var res = new List<string>(max);
        for (int i = lines.Length - 1; i >= 0 && res.Count < max; i--)
        {
            var n = Norm(lines[i]);
            if (n.Trim().Length == 0) continue;
            res.Insert(0, n.Length > 1000 ? n[^1000..] : n);
        }
        return res.ToArray();
    }

    /// <summary>Ist die Zeile mit der Schreibmarke (letzte Zeile des gelesenen Texts) leer?</summary>
    public static bool CursorLineBlank(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return Norm(lines[^1]).Trim().Length == 0;
    }

    /// <summary>Gewöhnliche Eingabeaufforderung einer Shell (<c>PS C:\&gt;</c>, <c>C:\&gt;</c>, <c>&gt;&gt;&gt;</c>)?</summary>
    public static bool IsShellPrompt(string? line)
    {
        var l = Norm(line).Trim();
        return l.Length > 0 && ShellPrompts.Any(r => r.IsMatch(l));
    }

    static bool EndsLikePrompt(string line) { var l = Norm(line); return l.Length > 0 && PromptEnd.Contains(l[^1]); }

    /// <summary>Regel T1 auf die letzte nicht leere Zeile; liefert die Abfragezeile oder <c>null</c>.</summary>
    public static TerminalPromptHit? MatchT1(IReadOnlyList<string>? lines)
    {
        var last = Last(lines);
        if (last == null || IsShellPrompt(last)) return null;
        // ein schon sichtbares Maskenecho („Password: ***“) gehört nicht zur Abfrage
        last = Norm(StripMaskRun(last, out _));
        var l = last.Length > 1000 ? last[^1000..] : last;
        return l.Length > 0 && T1Pattern.IsMatch(l) ? new TerminalPromptHit("T1", PromptLine(last)) : null;
    }

    /// <summary>Erkennung über alle drei Regeln (genau ein Ergebnis, Vorrang T1 vor T2 vor T3).</summary>
    /// <param name="before">letzte nicht leere Zeilen vor (bzw. zu Beginn) des Tastenlaufs</param>
    /// <param name="after">letzte nicht leere Zeilen nach den ersten Zeichen</param>
    /// <param name="typed">Zahl der bisher getippten Zeichen (Rücktaste zieht ab)</param>
    public static TerminalPromptHit? Detect(IReadOnlyList<string>? before, IReadOnlyList<string>? after, int typed)
        => Detect(before, after, typed, typed);

    /// <summary>
    /// Wie <see cref="Detect(IReadOnlyList{string}?, IReadOnlyList{string}?, int)"/>, aber mit Spanne für die Zahl getippter Zeichen:
    /// Während des Lesens können weitere Tasten eintreffen, deren Echo noch fehlt.
    /// </summary>
    public static TerminalPromptHit? Detect(IReadOnlyList<string>? before, IReadOnlyList<string>? after, int typedMin, int typedMax)
    {
        if (typedMax < typedMin) (typedMin, typedMax) = (typedMax, typedMin);
        // T1: die Abfrage stand schon da – oder steht nach den ersten Zeichen da (ohne Echo)
        if (MatchT1(before) is { } h1) return h1;
        if (after != null && MatchT1(after) is { } h1b) return h1b;
        var b = Last(before);
        var a = Last(after);
        if (a == null || b == null) return null;
        if (IsShellPrompt(b)) return null;
        // T2: die Zeile ist die alte plus genau so viele Maskenzeichen, wie getippt wurden
        if (typedMax >= 1 && MaskEcho(b, a, typedMin, typedMax) is { } prompt) return new TerminalPromptHit("T2", PromptLine(prompt));
        // T3: mindestens zwei Zeichen getippt, nichts geändert, die Zeile endet wie eine Abfrage
        if (typedMin >= 2 && a == b && EndsLikePrompt(b)) return new TerminalPromptHit("T3", PromptLine(b));
        return null;
    }

    /// <summary>T2: Abfragezeile, wenn <paramref name="after"/> = <paramref name="before"/> (ohne vorhandene Maskenzeichen) + Maskenzeichen in getippter Zahl.</summary>
    static string? MaskEcho(string before, string after, int typedMin, int typedMax)
    {
        string baseLine = StripMaskRun(before, out _);
        string afterBase = StripMaskRun(after, out int run);
        if (run < 1 || run < typedMin || run > typedMax) return null;
        if (Norm(afterBase) != Norm(baseLine)) return null;
        return Norm(baseLine).Length > 0 ? Norm(baseLine) : null;
    }

    /// <summary>Entfernt einen Lauf gleicher Maskenzeichen am Zeilenende (davor stehende Leerzeichen bleiben Teil der Abfrage).</summary>
    static string StripMaskRun(string line, out int run)
    {
        var l = Norm(line);
        run = 0;
        if (l.Length == 0 || !MaskChars.Contains(l[^1])) return l;
        char m = l[^1];
        int i = l.Length;
        while (i > 0 && l[i - 1] == m) { i--; run++; }
        return l[..i];
    }

    /// <summary>Abfragezeile für das Protokoll: ohne Leerraum am Rand, höchstens 120 Zeichen (bei Überlänge das Ende mit „…“ davor).</summary>
    public static string PromptLine(string line)
    {
        var l = Norm(line).Trim();
        return l.Length <= PromptLineMax ? l : "…" + l[^(PromptLineMax - 1)..];
    }
}
