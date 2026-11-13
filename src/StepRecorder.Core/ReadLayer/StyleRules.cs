using System.Text.RegularExpressions;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Redaktionelle Stilregeln für eigene Texte (13, Paket 29). Geprüft werden nur Texte des Autors, nie automatisch erzeugte.
/// Hinweise, keine Fehler: lange Sätze (Q22), mehrere Handlungen in einem Schritt (Q23), falsche Anrede (Q24), Füllwörter (Q25),
/// Passiv in Anweisungen (Q43). Die Nummer Q21 ist durch „Anwendung reagierte langsam“ belegt.
/// </summary>
public static class StyleRules
{
    public sealed record Hit(string Rule, string Key, string De, string En);

    const int MaxWords = 25;
    static readonly TimeSpan T = TimeSpan.FromMilliseconds(50);
    static readonly Regex Sentence = new(@"(?<=[.!?])\s+|\n+", RegexOptions.Compiled, T);
    static readonly Regex Word = new(@"[\p{L}\p{N}][\p{L}\p{N}'’-]*", RegexOptions.Compiled, T);
    static readonly Regex ThenDe = new(@"\b(und dann|danach|anschließend|im Anschluss)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, T);
    static readonly Regex ThenEn = new(@"\b(and then|afterwards|after that)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, T);
    static readonly Regex Du = new(@"\b(du|dich|dir|dein|deine|deinen|deinem|deiner|deines)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, T);
    // „Sie“ mitten im Satz (am Satzanfang kann es auch „sie“ = Mehrzahl sein)
    static readonly Regex SieMid = new(@"(?<=[\p{Ll},;:]\s)(Sie|Ihr|Ihre|Ihren|Ihrem|Ihrer|Ihnen)\b", RegexOptions.Compiled, T);
    static readonly Regex FillDe = new(@"\b(eigentlich|eventuell|ggf\.|gegebenenfalls|irgendwie|quasi|sozusagen|halt|einfach mal|bitte|usw\.|etc\.)", RegexOptions.Compiled | RegexOptions.IgnoreCase, T);
    static readonly Regex FillEn = new(@"\b(basically|actually|just|simply|maybe|kind of|sort of|please|etc\.)", RegexOptions.Compiled | RegexOptions.IgnoreCase, T);
    static readonly Regex PassiveDe = new(@"\b(wird|werden|wurde|wurden)\b[^.!?\n]{0,50}?\b(ge\p{L}+(t|en)|\p{L}+iert)\b", RegexOptions.Compiled, T);
    static readonly Regex PassiveEn = new(@"\b(is|are|must be|should be|will be)\s+\p{L}+ed\b", RegexOptions.Compiled | RegexOptions.IgnoreCase, T);

    /// <summary>Stilhinweise für einen eigenen Text. <paramref name="address"/>: formal | informal (nur Deutsch).</summary>
    public static List<Hit> Check(string? text, string lang, string address, bool instruction = true)
    {
        var hits = new List<Hit>();
        if (string.IsNullOrWhiteSpace(text)) return hits;
        bool de = lang != "en";
        try
        {
            foreach (var s in Sentence.Split(text))
                if (Word.Matches(s).Count > MaxWords)
                { hits.Add(new("Q22", "long", $"Ein Satz hat mehr als {MaxWords} Wörter – lieber teilen.", $"A sentence has more than {MaxWords} words – better split it.")); break; }
            if (instruction && (de ? ThenDe : ThenEn).IsMatch(text))
                hits.Add(new("Q23", "multi", "Der Text beschreibt mehrere Handlungen („und dann …“) – ein Schritt je Handlung ist leichter zu befolgen.", "The text describes several actions (“and then …”) – one step per action is easier to follow."));
            if (de && address == "formal" && Du.IsMatch(text))
                hits.Add(new("Q24", "du", "Der Text duzt, die Anleitung siezt.", "The text uses “du” while the guide uses “Sie”."));
            if (de && address == "informal" && SieMid.IsMatch(text))
                hits.Add(new("Q24", "sie", "Der Text siezt, die Anleitung duzt.", "The text uses “Sie” while the guide uses “du”."));
            if ((de ? FillDe : FillEn).Match(text) is { Success: true } f)
                hits.Add(new("Q25", "filler", $"Füllwort „{f.Value}“ – meist entbehrlich.", $"Filler word “{f.Value}” – usually unnecessary."));
            if (instruction && (de ? PassiveDe : PassiveEn).IsMatch(text))
                hits.Add(new("Q43", "passive", "Passiv in einer Anweisung – aktiv und direkt ist klarer („Klicken Sie …“).", "Passive voice in an instruction – active and direct is clearer (“Click …”)."));
        }
        catch (RegexMatchTimeoutException) { }
        return hits;
    }
}

/// <summary>
/// Lesbarkeit (13, Paket 29): Deutsch Flesch-Amstad <c>180 − ASL − 58,5·ASW</c>, Englisch Flesch <c>206,835 − 1,015·ASL − 84,6·ASW</c>
/// (ASL = Wörter je Satz, ASW = Silben je Wort). Silben werden über Vokalgruppen geschätzt.
/// </summary>
public static class Readability
{
    public sealed record Result(double Score, int Words, int Sentences, string Level);

    public static Result? Compute(IEnumerable<string> texts, string lang)
    {
        int words = 0, sentences = 0, syllables = 0;
        bool de = lang != "en";
        foreach (var t in texts)
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            foreach (var s in Regex.Split(t, @"(?<=[.!?])\s+|\n+"))
            {
                var ws = Regex.Matches(s, @"\p{L}[\p{L}'’-]*");
                if (ws.Count == 0) continue;
                sentences++;
                foreach (Match w in ws) { words++; syllables += Syllables(w.Value, de); }
            }
        }
        if (words == 0 || sentences == 0) return null;
        double asl = (double)words / sentences, asw = (double)syllables / words;
        double score = de ? 180 - asl - 58.5 * asw : 206.835 - 1.015 * asl - 84.6 * asw;
        score = Math.Round(Math.Clamp(score, 0, 100), 1);
        string level = score < 30 ? "sehr schwer" : score < 50 ? "schwer" : score < 60 ? "mittel" : score < 70 ? "normal" : "leicht";
        return new Result(score, words, sentences, level);
    }

    public static int Syllables(string word, bool de)
    {
        var w = word.ToLowerInvariant();
        if (de) w = w.Replace("qu", "q");
        string vowels = de ? "aeiouyäöü" : "aeiouy";
        int n = 0; bool prev = false;
        foreach (char c in w) { bool v = vowels.IndexOf(c) >= 0; if (v && !prev) n++; prev = v; }
        if (!de && w.EndsWith('e') && n > 1 && !w.EndsWith("le")) n--;
        return Math.Max(1, n);
    }
}
