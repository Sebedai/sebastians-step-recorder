using System.Text.RegularExpressions;
using StepRecorder.Settings;

namespace StepRecorder.Steps;

/// <summary>
/// Begriffsliste für automatische Texte: ersetzt Fundstellen („Text-Editor=Editor“) in Überschriften und Anleitungstexten,
/// z. B. technische Programmnamen durch Firmenbegriffe. Eigene Texte des Autors bleiben unverändert.
/// Ersetzt wird in einem Durchgang (längere Begriffe zuerst), Ersetzungen werden also nicht erneut ersetzt.
/// </summary>
public sealed class Glossary
{
    readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
    readonly Regex? _rx;
    public static readonly Glossary Empty = new(Array.Empty<string>());
    public int Count => _map.Count;

    public Glossary(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            int i = line.IndexOf('=');
            if (i <= 0) continue;
            string from = line[..i].Trim(), to = line[(i + 1)..].Trim();
            if (from.Length > 0) _map[from] = to;
        }
        if (_map.Count == 0) return;
        var alt = string.Join("|", _map.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape));
        _rx = new Regex(@"(?<![\p{L}\p{N}])(?:" + alt + @")(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    public static Glossary FromSettings()
    {
        try { var g = AppSettings.Load().Glossary; return g.Count == 0 ? Empty : new Glossary(g); } catch { return Empty; }
    }

    public string Apply(string text)
    {
        if (_rx == null || string.IsNullOrEmpty(text)) return text;
        try { return _rx.Replace(text, m => _map.TryGetValue(m.Value, out var to) ? to : m.Value); }
        catch (RegexMatchTimeoutException) { return text; }
    }
}
