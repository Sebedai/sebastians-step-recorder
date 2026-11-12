using System.Globalization;
using System.Speech.Recognition;

namespace StepRecorder.App.Ui;

/// <summary>
/// Diktat während der Aufnahme: gesprochene Sätze werden offline (Windows-Spracherkennung, ohne Internet) zu Kommentaren.
/// Das Mikrofon ist nur offen, solange das Diktat eingeschaltet ist; Ton wird nicht gespeichert, nur der erkannte Text.
/// </summary>
sealed class Dictation : IDisposable
{
    SpeechRecognitionEngine? _eng;
    public bool Active => _eng != null;
    public string Culture { get; private set; } = "";

    /// <summary>Installierte Erkennung für die Sprache (de bevorzugt de-DE, sonst jede deutsche; en analog), null wenn keine.</summary>
    public static RecognizerInfo? Find(string lang)
    {
        try
        {
            var all = SpeechRecognitionEngine.InstalledRecognizers();
            return all.FirstOrDefault(r => r.Culture.Name.Equals(lang == "en" ? "en-US" : "de-DE", StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName == lang);
        }
        catch { return null; }
    }

    public static string Installed()
    {
        try { var l = SpeechRecognitionEngine.InstalledRecognizers().Select(r => r.Culture.Name).ToList(); return l.Count == 0 ? "keine" : string.Join(", ", l); }
        catch { return "keine"; }
    }

    /// <summary>Starten. onStart: Sprechbeginn (Zeitpunkt merken), onText: erkannter Satz. Fehlertext oder null.</summary>
    public string? Start(string lang, Action onStart, Action<string> onText)
    {
        if (_eng != null) return null;
        var ri = Find(lang) ?? Find(lang == "de" ? "en" : "de");
        if (ri == null) return $"Auf diesem Rechner ist keine Windows-Spracherkennung installiert (gefunden: {Installed()}). Sie lässt sich unter Einstellungen › Zeit und Sprache › Sprache › Sprachpaket › „Spracherkennung“ hinzufügen.";
        try
        {
            var eng = new SpeechRecognitionEngine(ri);
            eng.LoadGrammar(new DictationGrammar());
            eng.SetInputToDefaultAudioDevice();
            eng.SpeechDetected += (_, _) => onStart();
            eng.SpeechRecognized += (_, e) => { if (e.Result?.Text is { Length: > 0 } t && e.Result.Confidence >= 0.25f) onText(Sentence(t, ri.Culture)); };
            eng.RecognizeAsync(RecognizeMode.Multiple);
            _eng = eng; Culture = ri.Culture.Name;
            return null;
        }
        catch (Exception ex) { return "Das Mikrofon oder die Spracherkennung konnte nicht gestartet werden: " + ex.Message; }
    }

    static string Sentence(string t, CultureInfo c)
    {
        t = t.Trim();
        if (t.Length == 0) return t;
        t = char.ToUpper(t[0], c) + t[1..];
        return ".!?".Contains(t[^1]) ? t : t + ".";
    }

    public void Stop()
    {
        var e = _eng; _eng = null;
        if (e == null) return;
        try { e.RecognizeAsyncCancel(); } catch { }
        try { e.Dispose(); } catch { }
    }

    public void Dispose() => Stop();
}
