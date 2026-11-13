using System.Text.Json.Nodes;
using StepRecorder.Capture.Host;
using StepRecorder.Data;
using StepRecorder.Settings;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Capture;

/// <summary>
/// Passwortabfragen im Terminal (10 §6, DS-022/DS-023): In Fenstern der Art „terminal“ liest der Recorder in der Stufe
/// „Schutznetz“ das Ende des sichtbaren Texts über UI Automation (TextPattern, nur auf dem UIA-Arbeiter) – nach der
/// Eingabetaste, nach Beruhigung der Ausgabe, beim Wechsel ins Terminal und zu Beginn jedes neuen Tastenlaufs. Erkennt eine
/// Regel (<see cref="TerminalPrompt"/>) eine Abfrage, schaltet er den Privatmodus mit dem Auslöser <c>terminalPrompt</c> ein:
/// Zeichen- und Bearbeitungstasten werden mit dem Grund <c>terminalPrompt</c>, Regel und Abfragezeile maskiert, bis
/// Eingabetaste, Strg+C, Strg+D, Esc, Fokuswechsel, 60 s ohne Taste oder Stopp die Abfrage beenden (<c>end</c>).
/// Solange über einen Lauf noch nicht entschieden ist, hält die Rückhaltung seine Tasten zurück (höchstens <c>T_max</c>,
/// ohne Ergebnis maskiert mit <c>unresolved</c>). Gelesener Text bleibt im Arbeitsspeicher; gespeichert wird nur die Abfragezeile.
/// Jeder Fehler der Textabfrage heißt „keine Erkennung“ – nie ein Absturz und nie eine verzögerte Eingabe.
/// </summary>
public sealed partial class Recorder
{
    /// <summary>Aktive Maskierung einer erkannten Abfragezeile.</summary>
    sealed class TpActive { public int Id; public string Rule = ""; public string PromptLine = ""; public nint Hwnd; public string GapId = ""; public long TFrom; public long LastKeyT; public string? Tail; }

    /// <summary>Tastenlauf in einem Terminal, über den die Erkennung entscheidet (pending → prompt | plain | unresolved).</summary>
    sealed class TpRun
    {
        public int Id; public nint Hwnd; public long T0, TEnd = long.MaxValue; public int Typed;
        public string[]? Before; public bool ReadA, ReadADone, ReadB, Activated;
        public string Decision = "pending"; public string? Rule, PromptLine, Tail;
        public bool Open => TEnd == long.MaxValue;
    }

    volatile TpActive? _tp;
    readonly List<TpRun> _tpRuns = new();
    readonly HashSet<uint> _arrDown = new();
    readonly Dictionary<nint, int> _tpFailures = new();
    string? _tpEndedTail; nint _tpEndedHwnd;
    int _tpIds;
    const long TpIdleTimeoutUs = 60_000_000;
    static readonly int[] TpAppearDelaysMs = { 300, 1000, 2500 };

    /// <summary>Erkannte Passwortabfrage im Terminal ein/aus (Leiste und Rahmen: „Passwortabfrage erkannt“).</summary>
    public event Action<bool>? TerminalPromptChanged;
    /// <summary>Der Privatmodus mit dem Auslöser <c>terminalPrompt</c> ist aktiv.</summary>
    public bool TerminalPromptActive => _tp != null;
    /// <summary>Privatmodus aus irgendeinem Grund (Privattaste oder erkannte Abfrage): keine Feldinhalte, keine Zwischenablage.</summary>
    bool PrivacyActive => PrivacyMode || _tp != null;

    /// <summary>Erkennung in diesem Fenster möglich: Terminal, nicht „Streng“ (maskiert ohnehin), nicht ausgeschlossen, Textzugriff nicht wiederholt gescheitert.</summary>
    bool TpEligible(WinEntry? fg) => fg != null && !fg.Own && !fg.Excluded && fg.InScope && Options.PrivacyLevel != "strict"
        && WindowRegistry.SurfaceKindOf(fg) == "terminal" && (!_tpFailures.TryGetValue(fg.Hwnd, out var n) || n < 3);

    /// <summary>Zeichen erzeugende Taste (ohne Strg/Alt/Win): Buchstaben, Ziffern, Leertaste, Ziffernblock, Satzzeichen.</summary>
    static bool PrintableVk(uint vk) => vk == 0x20 || vk is >= 0x30 and <= 0x39 || vk is >= 0x41 and <= 0x5A || vk is >= 0x60 and <= 0x6F
        || vk is >= 0xBA and <= 0xC0 || vk is >= 0xDB and <= 0xDF || vk == 0xE2;

    /// <summary>Gedrückte Tasten beim Eintreffen (für jede Taste, auch in ausgeschlossenen Fenstern – sonst bliebe Strg „hängen“).</summary>
    void TpTrack(in RingRecord r)
    {
        if (r.Message is WM_KEYUP or WM_SYSKEYUP) _arrDown.Remove(r.Data); else _arrDown.Add(r.Data);
    }

    TpRun? TpRunOf(PendingKey pk)
    {
        if (pk.Fg == null || _tpRuns.Count == 0) return null;
        for (int i = _tpRuns.Count - 1; i >= 0; i--)
        {
            var r = _tpRuns[i];
            if (r.Hwnd == pk.Fg.Hwnd && pk.T >= r.T0 && pk.T <= r.TEnd) return r;
        }
        return null;
    }

    /// <summary>Beim Eintreffen einer Taste (vor der Rückhaltung, unter <c>_gate</c>): Läufe abgrenzen und Lesen anstoßen.</summary>
    void TpOnArrival(PendingKey pk)
    {
        uint vk = pk.R.Data;
        if (pk.Self || pk.R.Message is WM_KEYUP or WM_SYSKEYUP) return;
        var fg = pk.Fg;
        if (_tp is { } a && fg != null && fg.Hwnd == a.Hwnd) a.LastKeyT = pk.T;
        var last = _tpRuns.Count > 0 ? _tpRuns[^1] : null;
        if (last is { Open: true } && (fg == null || fg.Hwnd != last.Hwnd)) TpEndRun(last, pk.T);
        if (!TpEligible(fg)) return;

        bool ctrl = _arrDown.Contains(0xA2) || _arrDown.Contains(0xA3) || _arrDown.Contains(0x11);
        bool alt = _arrDown.Contains(0xA4) || _arrDown.Contains(0xA5) || _arrDown.Contains(0x12);
        bool win = _arrDown.Contains(0x5B) || _arrDown.Contains(0x5C);
        bool altGr = _arrDown.Contains(0xA5) && ctrl;
        bool combo = !altGr && (ctrl || alt || win);
        bool terminator = vk is 0x0D or 0x1B || (combo && ctrl && !alt && vk is 0x43 or 0x44);
        var run = last is { Open: true } && last.Hwnd == fg!.Hwnd ? last : null;
        if (terminator)
        {
            if (run != null) TpEndRun(run, pk.T);
            if (vk == 0x0D) TpScheduleAppearReads(fg!.Hwnd);
            return;
        }
        if (_tp is { } act && act.Hwnd == fg!.Hwnd) return;   // Abfrage schon erkannt: kein neuer Lauf bis zu ihrem Ende
        if (vk == 0x08) { if (run != null) run.Typed = Math.Max(0, run.Typed - 1); return; }
        if (combo || !PrintableVk(vk)) return;
        if (run == null)
        {
            if (_tpRuns.Count >= 16) _tpRuns.RemoveAll(r => !r.Open && r.Decision != "pending" && pk.T - r.TEnd > 30_000_000);
            if (_tpRuns.Count >= 16) _tpRuns.RemoveAt(0);
            run = new TpRun { Id = ++_tpIds, Hwnd = fg!.Hwnd, T0 = pk.T };
            _tpRuns.Add(run);
            TpReadA(run);
        }
        run.Typed++;
        if (run.Typed >= 2 && run.ReadADone && !run.ReadB && run.Decision == "pending") TpReadB(run, 120);
    }

    /// <summary>Lauf endet (Eingabetaste, Esc, Strg+C/D, anderes Fenster). Ohne Ergebnis von T1 bleibt es beim Klartext.</summary>
    void TpEndRun(TpRun run, long t)
    {
        run.TEnd = t;
        if (run.Decision == "pending" && run.ReadADone && !run.ReadB) { run.Decision = "plain"; FlushPendingKeys(force: false); }
    }

    /// <summary>Lesen zu Beginn des Laufs: „vorher“ für T2/T3, sofortige Prüfung nach T1.</summary>
    void TpReadA(TpRun run)
    {
        run.ReadA = true;
        nint h = run.Hwnd;
        TpRead(h, 0, res =>
        {
            run.ReadADone = true;
            if (res.Status != "ok" || res.Lines is not { Length: > 0 })
            {
                TpFail(h, res.Status);
                if (run.Decision == "pending") { run.Decision = res.Status == "timeout" ? "unresolved" : "plain"; FlushPendingKeys(force: false); }
                return;
            }
            if (_tpFailures.GetValueOrDefault(h) < 3) _tpFailures[h] = 0;
            run.Before = res.Lines;
            if (run.Decision != "pending") return;
            if (TerminalPrompt.MatchT1(res.Lines) is { } hit) { TpDecide(run, hit, res.Lines); return; }
            if (!run.Open) { run.Decision = "plain"; FlushPendingKeys(force: false); return; }
            if (run.Typed >= 2 && !run.ReadB) TpReadB(run, 120);
        });
    }

    /// <summary>Lesen nach den ersten Zeichen: T1 (Abfrage erschien verspätet), T2 (Maskenecho), T3 (kein Echo).</summary>
    void TpReadB(TpRun run, int delayMs)
    {
        run.ReadB = true;
        int typedMin = run.Typed;
        TpRead(run.Hwnd, delayMs, res =>
        {
            if (run.Decision != "pending") return;
            if (res.Status != "ok" || res.Lines == null)
            {
                TpFail(run.Hwnd, res.Status);
                run.Decision = res.Status == "timeout" ? "unresolved" : "plain";
                FlushPendingKeys(force: false);
                return;
            }
            var hit = TerminalPrompt.Detect(run.Before, res.Lines, typedMin, Math.Max(typedMin, run.Typed));
            if (hit != null) TpDecide(run, hit, res.Lines);
            else { run.Decision = "plain"; FlushPendingKeys(force: false); }
        });
    }

    void TpDecide(TpRun run, TerminalPromptHit hit, string[] lines)
    {
        run.Decision = "prompt"; run.Rule = hit.Rule; run.PromptLine = hit.PromptLine; run.Tail = string.Join("\n", lines);
        T("terminal prompt " + hit.Rule);
        FlushPendingKeys(force: false);
    }

    /// <summary>Nach der Eingabetaste und beim Wechsel ins Terminal: erscheint eine Abfrage nach T1, beginnt die Maskierung sofort.</summary>
    void TpScheduleAppearReads(nint h)
    {
        foreach (var d in TpAppearDelaysMs) TpRead(h, d, res => TpAppear(h, res));
    }

    void TpAppear(nint h, UiaResult res)
    {
        if (State != RecorderState.Recording || _tp != null || _fgWin?.Hwnd != h) return;
        if (_tpRuns.Any(r => r.Hwnd == h && (r.Open || r.Decision == "pending"))) return;   // der Lauf entscheidet selbst
        if (res.Status != "ok" || res.Lines is not { Length: > 0 }) { if (res.Status != "ok") TpFail(h, res.Status); return; }
        if (_tpFailures.GetValueOrDefault(h) < 3) _tpFailures[h] = 0;
        if (res.CursorBlank == true) return;                       // Schreibmarke auf leerer Zeile: keine offene Abfrage
        string tail = string.Join("\n", res.Lines);
        if (_tpEndedHwnd == h && _tpEndedTail == tail) return;     // die eben beantwortete Abfrage steht noch da
        if (TerminalPrompt.MatchT1(res.Lines) is { } hit) TpActivate(hit.Rule, hit.PromptLine, h, res.TStart > 0 ? res.TStart : Now(), tail);   // ab Beginn des Lesens: auch Tasten, die währenddessen kamen
    }

    /// <summary>Text lesen – nur auf dem UIA-Arbeiter (Zeitlimit des Arbeiters); Ergebnis unter <c>_gate</c>.</summary>
    void TpRead(nint h, int delayMs, Action<UiaResult> done)
    {
        void Go()
        {
            try
            {
                _uia.Enqueue(() => _uia.TerminalTail(h), res =>
                {
                    lock (_gate)
                    {
                        if (_w.Closed) return;
                        try { done(res); } catch (Exception ex) { T("terminal: " + ex.Message); }
                    }
                });
            }
            catch { }
        }
        if (delayMs > 0) Task.Delay(delayMs).ContinueWith(_ => { try { if (!_w.Closed && State is RecorderState.Recording or RecorderState.Paused) Go(); } catch { } });
        else Go();
    }

    /// <summary>Textzugriff gescheitert: nach drei Fehlschlägen in Folge (oder einer Zeitüberschreitung) wird in diesem Fenster nicht mehr gelesen (Schutznetz N1, N2, N5 bleiben).</summary>
    void TpFail(nint h, string status)
    {
        if (status == "skipped") return;
        // Zeitüberschreitung: sofort aufgeben – ein hängender Arbeiter wird ersetzt, aber nur wenige Male je Aufnahme
        int n = status == "timeout" ? 3 : _tpFailures.TryGetValue(h, out var c) ? c + 1 : 1;
        _tpFailures[h] = n;
        if (n == 3) T("terminal text not available: " + status);
    }

    /// <summary>Privatmodus mit dem Auslöser <c>terminalPrompt</c> einschalten (eigene Lücke, unabhängig von der Privattaste).</summary>
    void TpActivate(string rule, string promptLine, nint hwnd, long t, string? tail)
    {
        if (_tp != null) return;
        FlushMove();
        string gid = GapIds.Next();
        Emit(new JsonObject { ["t"] = t, ["type"] = "gap", ["gapId"] = gid, ["phase"] = "begin", ["cause"] = "privacyMode", ["tFrom"] = t,
            ["channels"] = new JsonArray("keyboard", "clipboard", "fieldValues"), ["inputLost"] = "none" });
        Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "privacyMode", ["active"] = true, ["trigger"] = "terminalPrompt", ["rule"] = rule, ["gapId"] = gid });
        _tp = new TpActive { Id = ++_tpIds, Rule = rule, PromptLine = promptLine, Hwnd = hwnd, GapId = gid, TFrom = t, LastKeyT = Math.Max(t, Now()), Tail = tail };
        _tpEndedTail = null;
        ActionLine?.Invoke(Loc.L("Passwortabfrage erkannt", "Password prompt detected"));
        try { TerminalPromptChanged?.Invoke(true); } catch { }
    }

    /// <summary>
    /// Abfrage beenden. <paramref name="end"/> nach 04 (<c>enter</c>, <c>cancelCtrlC</c>, <c>cancelCtrlD</c>, <c>cancelEscape</c>,
    /// <c>focusChange</c>, <c>timeout</c>, <c>stopped</c>). Bei Gründen ohne Taste werden zuvor alle zurückgehaltenen Tasten
    /// entschieden – sonst gingen Tasten der Abfrage nach ihrem Ende im Klartext durch.
    /// </summary>
    void TpEnd(string end, long t, bool flushPending)
    {
        if (_tp == null) return;
        if (flushPending) FlushPendingKeys(force: true);
        var a = _tp;
        if (a == null) return;   // beim Abarbeiten schon beendet (z. B. Eingabetaste in der Warteschlange)
        if (_masked?.Reason == "terminalPrompt") FlushMasked();
        t = Math.Max(t, a.TFrom);
        Emit(new JsonObject { ["t"] = t, ["type"] = "gap", ["gapId"] = a.GapId, ["phase"] = "end", ["cause"] = "privacyMode", ["tFrom"] = a.TFrom, ["tTo"] = t,
            ["channels"] = new JsonArray("keyboard", "clipboard", "fieldValues"), ["inputLost"] = "none",
            ["detail"] = new JsonObject { ["trigger"] = "terminalPrompt", ["rule"] = a.Rule, ["end"] = end } });
        Emit(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "privacyMode", ["active"] = false, ["trigger"] = "terminalPrompt", ["rule"] = a.Rule, ["end"] = end, ["gapId"] = a.GapId });
        _tpEndedTail = a.Tail; _tpEndedHwnd = a.Hwnd;
        _tp = null;
        try { TerminalPromptChanged?.Invoke(false); } catch { }
    }

    /// <summary>Rückhaltung: Tasten eines Laufs, über den noch nicht entschieden ist, warten (true = warten).</summary>
    bool TpHold(PendingKey pk, long now, bool force)
    {
        if (pk.Self || TpRunOf(pk) is not { Decision: "pending" } run) return false;
        if (!force && now - pk.T < Options.HoldMaxMs * 1000L) return true;
        run.Decision = "unresolved";   // ohne Ergebnis rechtzeitig: maskieren (10 §6.2 „Schnelle Folgeabfrage“)
        T("terminal: no decision in time");
        return false;
    }

    /// <summary>
    /// Schutzentscheidung im Terminal beim Verarbeiten einer gedrückten Taste: aktiviert die Maskierung eines erkannten Laufs,
    /// beendet sie mit Eingabetaste, Esc, Strg+C oder Strg+D und liefert den Maskierungsgrund (oder <c>null</c>).
    /// </summary>
    string? TpKeyDown(PendingKey pk, string key, uint vk, bool isMod, bool combo, bool terminatorKey)
    {
        if (pk.Self || pk.Fg == null) return null;
        var run = TpRunOf(pk);
        if (run is { Decision: "prompt", Activated: false } && _tp == null)
        {
            run.Activated = true;
            TpActivate(run.Rule ?? "T1", run.PromptLine ?? "", run.Hwnd, pk.T, run.Tail);
        }
        // Tasten von vor der Erkennung (z. B. die Eingabetaste des Befehls, der die Abfrage auslöste) gehören nicht dazu
        if (_tp is { } a && a.Hwnd == pk.Fg.Hwnd && pk.T >= a.TFrom)
        {
            string? end = key is "Enter" or "NumPadEnter" ? "enter" : key == "Escape" ? "cancelEscape"
                : _keys.Ctrl && !_keys.Alt && vk == 0x43 ? "cancelCtrlC" : _keys.Ctrl && !_keys.Alt && vk == 0x44 ? "cancelCtrlD" : null;
            if (end != null) { FlushMasked(); TpEnd(end, pk.T, flushPending: false); return null; }
            return !isMod && !combo && !terminatorKey ? "terminalPrompt" : null;
        }
        if (run is { Decision: "unresolved" } && !isMod && !combo && !terminatorKey && !PrivacyMode) return "unresolved";
        return null;
    }

    /// <summary>Im Leerlauf: Zeitüberschreitung der Abfrage (60 s ohne Taste) und spätestes Lesen „nachher“ für langsam Tippende.</summary>
    void TpIdle(long now)
    {
        if (State != RecorderState.Recording) return;
        if (_tp is { } a && now - a.LastKeyT >= TpIdleTimeoutUs) TpEnd("timeout", now, flushPending: true);
        if (_tpRuns.Count == 0) return;
        long deadline = Math.Max(200_000, Options.HoldMaxMs * 1000L - 500_000);
        foreach (var r in _tpRuns)
            if (r.Decision == "pending" && r.Open && r.ReadADone && !r.ReadB && now - r.T0 >= deadline) TpReadB(r, 0);
    }

    /// <summary>Vordergrund wechselt: Abfrage endet (Fokuswechsel), offene Läufe enden; im neuen Terminal nach einer Abfrage sehen.</summary>
    void TpForeground(WinEntry e, long t)
    {
        if (_tp is { } a && a.Hwnd != e.Hwnd) TpEnd("focusChange", t, flushPending: true);
        foreach (var r in _tpRuns) if (r.Open && r.Hwnd != e.Hwnd) TpEndRun(r, t);
        if (TpEligible(e)) TpScheduleAppearReads(e.Hwnd);
    }
}
