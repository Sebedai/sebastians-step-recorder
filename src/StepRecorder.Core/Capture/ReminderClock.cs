namespace StepRecorder.Capture;

public enum ReminderKind { StillRecording, PrivacyStillOn }

/// <summary>
/// Erinnerungen während der Aufnahme (rein, testbar; Zeiten in Mikrosekunden):
/// „Aufnahme läuft noch“ nach N Minuten ohne jede Eingabe (wiederholt alle N, höchstens dreimal je Leerlaufphase) und
/// „Privattaste ist noch an“ einmal je Einschalten nach S Sekunden ohne Taste. Nie während Pause oder Lücke.
/// Erinnert nur – pausiert nichts.
/// </summary>
public sealed class ReminderClock
{
    public const int MaxIdleReminders = 3;
    readonly long _idleUs, _privacyUs;
    long _lastActivity, _lastKey, _privacySince;
    bool _privacy, _privacyFired, _suspended;
    int _idleFired;

    public ReminderClock(int idleMinutes, int privacySeconds, long start = 0)
    {
        _idleUs = Math.Max(0, idleMinutes) * 60_000_000L;
        _privacyUs = Math.Max(0, privacySeconds) * 1_000_000L;
        _lastActivity = _lastKey = start;
    }

    /// <summary>Jede Eingabe (auch Mausbewegung, auch außerhalb des Aufnahmebereichs).</summary>
    public void Activity(long t) { _lastActivity = t; _idleFired = 0; }

    public void Key(long t) { Activity(t); _lastKey = t; }

    public void Privacy(bool on, long t)
    {
        if (on == _privacy) return;
        _privacy = on; _privacyFired = false; _privacySince = t; _lastKey = t;
    }

    /// <summary>Pause, gesperrter Rechner, sicherer Desktop: Uhren stehen; danach beginnen sie neu.</summary>
    public void Suspend(bool on, long t)
    {
        if (on == _suspended) return;
        _suspended = on;
        if (!on) { _lastActivity = t; _idleFired = 0; _lastKey = t; _privacySince = t; }
    }

    /// <summary>Fällige Erinnerung (Art, Minuten bzw. Sekunden) oder null.</summary>
    public (ReminderKind kind, int value)? Poll(long now)
    {
        if (_suspended) return null;
        if (_privacy && !_privacyFired && _privacyUs > 0 && now - Math.Max(_lastKey, _privacySince) >= _privacyUs)
        {
            _privacyFired = true;
            return (ReminderKind.PrivacyStillOn, (int)(_privacyUs / 1_000_000));
        }
        if (_idleUs > 0 && _idleFired < MaxIdleReminders && now - _lastActivity >= _idleUs * (_idleFired + 1))
        {
            _idleFired++;
            return (ReminderKind.StillRecording, (int)((now - _lastActivity) / 60_000_000));
        }
        return null;
    }
}
