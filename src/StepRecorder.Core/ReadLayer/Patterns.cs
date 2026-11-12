using System.Text.RegularExpressions;

namespace StepRecorder.ReadLayer;

/// <summary>Mustersuche für auffällige Eingaben (10 §11.2): Passwörter in Befehlen, Schlüssel, Zahlungsdaten.</summary>
public static partial class Patterns
{
    public const string Version = "1.0";

    [GeneratedRegex(@"://[^/\s:@]+:[^/\s@]+@|password\s*=\s*[^;\s]+|\bpwd\s*=\s*[^;\s]+|/user:\S+\s+\S+|(?:^|\s)-p\S{3,}|--password[ =]\S+", RegexOptions.NonBacktracking | RegexOptions.IgnoreCase)]
    private static partial Regex PasswordInCommand();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_-]{16,}|\bghp_[A-Za-z0-9]{20,}|\bgithub_pat_[A-Za-z0-9_]{20,}|\bAKIA[0-9A-Z]{16}\b|\bxox[abpors]-[A-Za-z0-9-]{10,}|\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}|-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.NonBacktracking)]
    private static partial Regex KeysTokens();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:[ ]?[A-Z0-9]{4}){3,7}(?:[ ]?[A-Z0-9]{1,4})?\b", RegexOptions.NonBacktracking)]
    private static partial Regex IbanCandidate();

    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b", RegexOptions.NonBacktracking)]
    private static partial Regex CardCandidate();

    [GeneratedRegex(@"[A-Za-z0-9+/_\-]{20,}", RegexOptions.NonBacktracking)]
    private static partial Regex LongRandom();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.NonBacktracking)]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<![\w+])(?:\+|00)\d{1,3}[\s/.-]?(?:\(0\)[\s]?)?\d{2,5}[\s/.-]?\d{3,}(?:[\s.-]?\d{1,6})?|(?<![\w+])0\d{2,5}[\s/.-]\d{3,}(?:[\s.-]?\d{1,6})?", RegexOptions.None, 200)]
    private static partial Regex Phone();

    /// <summary>
    /// Stellen mit personenbezogenen oder geheimen Daten in einem Text (z. B. per OCR aus einem Bild):
    /// (Start, Länge, Art) – Art: email, iban, cardNumber, phone, keyOrToken, passwordInCommand.
    /// </summary>
    public static List<(int start, int length, string kind)> SensitiveSpans(string? text)
    {
        var list = new List<(int, int, string)>();
        if (string.IsNullOrEmpty(text)) return list;
        foreach (Match m in Email().Matches(text)) list.Add((m.Index, m.Length, "email"));
        foreach (Match m in IbanCandidate().Matches(text)) if (IbanValid(m.Value)) list.Add((m.Index, m.Length, "iban"));
        foreach (Match m in CardCandidate().Matches(text)) if (Luhn(m.Value)) list.Add((m.Index, m.Length, "cardNumber"));
        foreach (Match m in KeysTokens().Matches(text)) list.Add((m.Index, m.Length, "keyOrToken"));
        foreach (Match m in PasswordInCommand().Matches(text)) list.Add((m.Index, m.Length, "passwordInCommand"));
        foreach (Match m in Phone().Matches(text))
            if (m.Value.Count(char.IsDigit) >= 8 && !list.Any(x => m.Index < x.Item1 + x.Item2 && x.Item1 < m.Index + m.Length)) list.Add((m.Index, m.Length, "phone"));
        return list;
    }

    public static string KindName(string kind, string lang = "de") => kind switch
    {
        "email" => lang == "de" ? "E-Mail-Adresse" : "Email address",
        "iban" => "IBAN",
        "cardNumber" => lang == "de" ? "Kartennummer" : "Card number",
        "phone" => lang == "de" ? "Telefonnummer" : "Phone number",
        "keyOrToken" => lang == "de" ? "Schlüssel/Token" : "Key/token",
        "passwordInCommand" => lang == "de" ? "Passwort im Befehl" : "Password in command",
        "plaintextWindow" => lang == "de" ? "Eingabe in einem Fenster ohne erkennbare Passwortfelder (Terminal, Remote-Desktop, VM)" : "Input in a window without recognisable password fields (terminal, remote desktop, VM)",
        _ => kind
    };

    /// <summary>Gibt die Art des ersten auffälligen Treffers zurück (null = unauffällig).</summary>
    public static string? Flag(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (PasswordInCommand().IsMatch(text)) return "passwordInCommand";
        if (KeysTokens().IsMatch(text)) return "keyOrToken";
        foreach (Match m in IbanCandidate().Matches(text)) if (IbanValid(m.Value)) return "iban";
        foreach (Match m in CardCandidate().Matches(text)) if (Luhn(m.Value)) return "cardNumber";
        return null;
    }

    /// <summary>Nur Hinweise (keine Sperre): lange Zufallsfolgen, E-Mail-Adressen.</summary>
    public static string? Hint(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        foreach (Match m in LongRandom().Matches(text))
            if (m.Value.Distinct().Count() >= 12 && m.Value.Any(char.IsDigit) && m.Value.Any(char.IsLetter)) return "longRandom";
        if (Email().IsMatch(text)) return "email";
        return null;
    }

    static bool IbanValid(string raw)
    {
        var s = raw.Replace(" ", "");
        if (s.Length < 15 || s.Length > 34) return false;
        var r = s[4..] + s[..4];
        int mod = 0;
        foreach (char c in r)
        {
            string d = char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString();
            foreach (char dc in d) mod = (mod * 10 + (dc - '0')) % 97;
        }
        return mod == 1;
    }

    static bool Luhn(string raw)
    {
        var digits = raw.Where(char.IsDigit).Select(c => c - '0').ToArray();
        if (digits.Length < 13 || digits.Length > 19) return false;
        int sum = 0; bool dbl = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int d = digits[i];
            if (dbl) { d *= 2; if (d > 9) d -= 9; }
            sum += d; dbl = !dbl;
        }
        return sum % 10 == 0;
    }
}
