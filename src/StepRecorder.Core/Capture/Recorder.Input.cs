using System.Text.Json.Nodes;
using StepRecorder.Capture.Host;
using StepRecorder.Data;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Capture;

public sealed partial class Recorder
{
    readonly List<RingRecord> _batch = new(1024);
    WinEntry? _fgWin;

    void InputLoop()
    {
        using var ev = new AutoResetEvent(false) { SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(_host.DataEvent, false) };
        long lastTick = Environment.TickCount64;
        T("input loop started");
        int ticks = 0;
        while (!_stopInput)
        {
            ev.WaitOne(4);
            try
            {
                lock (_gate)
                {
                    if (State == RecorderState.Recording) DrainRing();
                    FlushPendingKeys(force: false);
                    Idle();
                }
                if (Environment.TickCount64 - lastTick >= 250) { lastTick = Environment.TickCount64; if (ticks++ < 3) T("tick " + _w.LastSeq); _w.Tick(Now()); Watch(); }
            }
            catch (Exception ex) { Warning?.Invoke("Eingabeverarbeitung: " + ex.Message); }
        }
    }

    long _lastOverflow;
    void DrainRing()
    {
        _batch.Clear();
        _host.Ring.Drain(_batch);
        long of = _host.Ring.Overflows;
        if (of != _lastOverflow)
        {
            long lost = of - _lastOverflow; _lastOverflow = of;
            _inSeq += lost;
            long t = Now();
            SingleGap("overload", t, t, "certain", new JsonObject { ["dropped"] = lost }, "keyboard", "mouse");
        }
        foreach (var r in _batch)
        {
            if (r.Kind == RingKind.Mouse) OnMouse(r);
            else if (r.Kind == RingKind.Keyboard) OnKey(r);
            // Zustandsprobe 700 ms nach der letzten Aktion (Grundlage für Erfolgsprüfungen)
            if (r.Kind == RingKind.Keyboard || r.Message != WM_MOUSEMOVE) _stateDue = Now() + StateProbeDelayUs;
        }
    }

    // ---------------- Fensterbezogene Lücken (ausgeschlossen, außerhalb, erhöht) ----------------

    void CheckForegroundGaps(WinEntry fg, long t)
    {
        if (fg.Excluded) BeginGap("excludedApp", t, "certain", "keyboard", "mouse", "elements", "clipboard").Detail = new JsonObject { ["rule"] = fg.ExcludedRule };
        else if (_gaps.ContainsKey("excludedApp")) EndGap("excludedApp", t, new JsonObject { ["rule"] = _gaps["excludedApp"].Detail?["rule"]?.GetValue<string>() ?? "" });

        bool elevatedBlocked = fg.Elevated == true && !IsElevated();
        if (elevatedBlocked) BeginGap("elevatedWindow", t, "possible", "keyboard", "mouse", "elements");
        else EndGap("elevatedWindow", t);
    }

    // ---------------- Maus ----------------

    sealed class MoveBundle { public long T, TEnd; public string M = ""; public int X, Y, XEnd, YEnd, N; public long InSeq; public JsonArray D = new(); public long LastT; public int LastX, LastY; public bool Inj; public string HeldKey = ""; }
    MoveBundle? _move;
    long _lastMoveSeq;
    int _lastVx, _lastVy; long _lastMoveT; bool _dwellQueried = true;
    UiaResult? _dwell; long _dwellRefSeq; long _dwellT; int _dwellX, _dwellY; bool _dwellWritten;
    readonly HashSet<string> _held = new();
    long _lastWheelT = -10_000_000;
    bool _swap = GetSystemMetrics(SM_SWAPBUTTON) != 0;
    readonly Dictionary<string, bool> _suppressedUp = new();

    void OnMouse(in RingRecord r)
    {
        long t = TFromQpc(r.Qpc);
        int msg = r.Message;
        bool inj = (r.Flags & LLMHF_INJECTED) != 0;
        var (m, x, y) = _topo.ToLocal(r.X, r.Y);
        _monActivity[m] = t;

        nint hit = msg == WM_MOUSEMOVE ? 0 : WindowFromPoint(new POINT { X = r.X, Y = r.Y });
        nint root = hit != 0 ? WindowRegistry.Root(hit) : 0;
        // Bedienung des Recorders: nicht aufzeichnen
        if (msg != WM_MOUSEMOVE && ((root != 0 && _reg.IsOwn(root)) || IsRecorderControl?.Invoke(r.X, r.Y) == true))
        {
            _inSeq++;
            if (msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN or WM_MOUSEWHEEL or WM_MOUSEHWHEEL)
            {
                FlushMove();
                SingleGap("recorderInteraction", t, t, "none", null, "all");
                if (msg is not (WM_MOUSEWHEEL or WM_MOUSEHWHEEL)) _suppressedUp[ButtonOf(msg, r.Data).phys] = true;
            }
            return;
        }
        if (msg is WM_LBUTTONUP or WM_RBUTTONUP or WM_MBUTTONUP or WM_XBUTTONUP && _suppressedUp.Remove(ButtonOf(msg, r.Data).phys)) { _inSeq++; return; }

        WinEntry? win = root != 0 ? _reg.Ensure(root, t) : null;
        // Ausgeschlossene Anwendung unter dem Zeiger: keine Nummer, nichts aufzeichnen
        if (win is { Excluded: true } || _fgWin is { Excluded: true } && msg == WM_MOUSEMOVE)
        {
            BeginGap("excludedApp", t, "certain", "keyboard", "mouse", "elements", "clipboard").Detail ??= new JsonObject { ["rule"] = (win ?? _fgWin)?.ExcludedRule };
            return;
        }
        // Außerhalb des Aufnahmebereichs: nur zählen
        bool outOfScope = Options.Scope switch
        {
            ScopeMode.Region => Options.Region is { } reg && !reg.Contains(r.X, r.Y),
            ScopeMode.App => msg != WM_MOUSEMOVE && win != null && !win.InScope,
            _ => false
        };
        if (outOfScope)
        {
            _inSeq++;
            var g = BeginGap("outOfScope", t, "certain", "keyboard", "mouse", "elements");
            if (msg == WM_MOUSEMOVE) g.Moves++; else if (msg is WM_MOUSEWHEEL or WM_MOUSEHWHEEL) g.Wheel++; else if (IsDown(msg)) g.Clicks++;
            return;
        }
        if (_gaps.ContainsKey("outOfScope") && msg != WM_MOUSEMOVE) EndGap("outOfScope", t);
        if (_gaps.ContainsKey("excludedApp") && win != null && !win.Excluded && _fgWin is not { Excluded: true }) EndGap("excludedApp", t);

        // Per Einstellung abgeschaltete Eingabearten: gar nicht speichern (Zugwege bei gedrückter Taste bleiben)
        if (msg == WM_MOUSEMOVE ? !Options.Rec("moves") && _held.Count == 0
            : msg is WM_MOUSEWHEEL or WM_MOUSEHWHEEL ? !Options.Rec("wheel")
            : !Options.Rec("clicks")) return;

        long inSeq = ++_inSeq;
        if (msg == WM_MOUSEMOVE)
        {
            AddMove(t, m, x, y, inSeq, inj);
            _lastVx = r.X; _lastVy = r.Y; _lastMoveT = t; _dwellQueried = false;
            return;
        }

        FlushMove();
        FlushPendingKeys(force: true);
        FlushMasked();
        var fg = _fgWin;
        if (msg is WM_MOUSEWHEEL or WM_MOUSEHWHEEL)
        {
            int delta = (short)(r.Data >> 16);
            var ev = new JsonObject { ["t"] = t, ["inSeq"] = inSeq, ["type"] = "mouse.wheel", ["ep"] = _topo.Epoch, ["m"] = m, ["x"] = x, ["y"] = y, ["axis"] = msg == WM_MOUSEWHEEL ? "v" : "h", ["delta"] = delta };
            AddCommon(ev, win, fg, hit, inj);
            long seq = Emit(ev);
            if (t - _lastWheelT >= 200_000) { _frames.PreInput(t, seq, m); QueryElement("wheel", seq, r.X, r.Y, m, x, y, win); }
            _lastWheelT = t;
            _frames.AwaitSettle(t, seq, m);
            ActionLine?.Invoke("Scrollen");
            return;
        }

        var (phys, logical) = ButtonOf(msg, r.Data);
        bool down = IsDown(msg);
        var bev = new JsonObject { ["t"] = t, ["inSeq"] = inSeq, ["type"] = down ? "mouse.down" : "mouse.up", ["ep"] = _topo.Epoch, ["m"] = m, ["x"] = x, ["y"] = y, ["button"] = logical, ["physButton"] = phys };
        var mods = _keys.Mods(0);
        if (mods.Count > 0) bev["mods"] = new JsonArray(mods.Select(s => (JsonNode)s).ToArray());
        var held = _held.Where(h => h != logical).ToList();
        if (held.Count > 0) bev["held"] = new JsonArray(held.Select(s => (JsonNode)s).ToArray());
        AddCommon(bev, win, fg, hit, inj);
        if (down)
        {
            _held.Add(logical);
            // Verweil-Ergebnis vor dem Druck festhalten (wird referenziert)
            long? dwellSeq = WriteDwellIfUsable(r.X, r.Y);
            long seq = Emit(bev);
            _frames.PreInput(t, seq, m);
            QueryElement("buttonDown", seq, r.X, r.Y, m, x, y, win, dwellSeq);
            ActionCount++;
            ActionLine?.Invoke(logical == "secondary" ? "Rechtsklick" : "Klick");
        }
        else
        {
            _held.Remove(logical);
            long seq = Emit(bev);
            _frames.PreInput(t, seq, m);
            _frames.AwaitSettle(t, seq, m);
            if (_lastDownMoved(r.X, r.Y)) QueryElement("buttonUp", seq, r.X, r.Y, m, x, y, win);
        }
        _dwellQueried = true; _dwell = null;
        _lastDownX = r.X; _lastDownY = r.Y;
    }

    int _lastDownX, _lastDownY;
    bool _lastDownMoved(int vx, int vy) => Math.Abs(vx - _lastDownX) > 4 || Math.Abs(vy - _lastDownY) > 4;

    static bool IsDown(int msg) => msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN;

    (string phys, string logical) ButtonOf(int msg, uint data)
    {
        string phys = msg switch
        {
            WM_LBUTTONDOWN or WM_LBUTTONUP => "left",
            WM_RBUTTONDOWN or WM_RBUTTONUP => "right",
            WM_MBUTTONDOWN or WM_MBUTTONUP => "middle",
            _ => (data >> 16) == 1 ? "x1" : "x2"
        };
        string logical = phys switch
        {
            "left" => _swap ? "secondary" : "primary",
            "right" => _swap ? "primary" : "secondary",
            _ => phys
        };
        return (phys, logical);
    }

    void AddCommon(JsonObject ev, WinEntry? win, WinEntry? fg, nint hit, bool inj)
    {
        if (win != null && !win.Own) ev["win"] = win.Id;
        if (hit != 0) ev["hitHwnd"] = Json.Hex(hit);
        if (fg != null && !fg.Own) ev["fg"] = fg.Id;
        if (inj) ev["inj"] = true;
    }

    void AddMove(long t, string m, int x, int y, long inSeq, bool inj)
    {
        var b = _move;
        string held = string.Join(",", _held);
        if (b != null && (b.M != m || b.Inj != inj || b.HeldKey != held || t - b.LastT > 50_000 || t - b.T > 250_000 || b.N >= 256))
        { FlushMove(); b = null; }
        if (b == null)
        {
            _move = new MoveBundle { T = t, TEnd = t, M = m, X = x, Y = y, XEnd = x, YEnd = y, N = 1, InSeq = inSeq, LastT = t, LastX = x, LastY = y, Inj = inj, HeldKey = held };
            return;
        }
        b.D.Add(new JsonArray(t - b.LastT, x - b.LastX, y - b.LastY));
        b.N++; b.LastT = t; b.LastX = x; b.LastY = y; b.TEnd = t; b.XEnd = x; b.YEnd = y;
    }

    void FlushMove()
    {
        var b = _move;
        if (b == null) return;
        _move = null;
        var ev = new JsonObject { ["t"] = b.T, ["inSeq"] = b.InSeq, ["type"] = "mouse.move", ["ep"] = _topo.Epoch, ["m"] = b.M, ["x"] = b.X, ["y"] = b.Y, ["n"] = b.N };
        if (b.N > 1) ev["d"] = b.D;
        ev["tEnd"] = b.TEnd; ev["xEnd"] = b.XEnd; ev["yEnd"] = b.YEnd;
        if (b.HeldKey.Length > 0) ev["held"] = new JsonArray(b.HeldKey.Split(',').Select(s => (JsonNode)s).ToArray());
        if (b.Inj) ev["inj"] = true;
        _lastMoveSeq = Emit(ev);
    }

    // ---------------- Verweilen und Elementermittlung ----------------

    void Idle()
    {
        long now = Now();
        // Bewegungsbündel nach 50 ms Ruhe schließen
        if (_move != null && now - _move.LastT > 50_000) FlushMove();
        // Verweilen: 80 ms ohne Bewegung → Elementermittlung (verwerfbar)
        if (!_dwellQueried && _held.Count == 0 && now - _lastMoveT >= 80_000 && State == RecorderState.Recording)
        {
            _dwellQueried = true;
            int vx = _lastVx, vy = _lastVy;
            var root = WindowRegistry.Root(WindowFromPoint(new POINT { X = vx, Y = vy }));
            var e = root != 0 ? _reg.Peek(root) : null;
            if (root != 0 && !_reg.IsOwn(root) && e is not { Excluded: true } && (e == null || e.InScope))
            {
                long refSeq = _lastMoveSeq;
                _uia.Enqueue(() => _uia.AtPoint(vx, vy), res =>
                {
                    lock (_gate) { _dwell = res; _dwellRefSeq = refSeq; _dwellT = res.TStart; _dwellX = vx; _dwellY = vy; _dwellWritten = false; }
                }, droppable: true);
            }
        }
        // Verweilen ≥ Hover-Zeit: Ergebnis schreiben (Grundlage für Hover-Schritte)
        if (_dwell != null && !_dwellWritten && now - _dwellT >= 450_000 && _held.Count == 0) WriteDwellIfUsable(_dwellX, _dwellY);
        if (State == RecorderState.Recording)
        {
            PollCursor(now);
            if (_stateDue > 0 && now >= _stateDue) { _stateDue = 0; ProbeState(); }
        }
        // Feldinhalt 300 ms nach der letzten Taste eines Laufs
        if (_valuePollDue && now - _lastKeyT >= 300_000 && _pendingKeys.Count == 0) { _valuePollDue = false; PollValue("poll"); }
    }

    // ---------------- Wartesignale und Zustand nach Aktionen ----------------

    const long StateProbeDelayUs = 700_000;
    long _stateDue, _lastCursorPoll;
    bool _cursorBusy;

    /// <summary>Zeiger „beschäftigt“ (Sanduhr/AppStarting) ein und aus – Wartesignal für Automatisierung.</summary>
    void PollCursor(long now)
    {
        if (now - _lastCursorPoll < 100_000) return;
        _lastCursorPoll = now;
        var shape = Win32Detail.CursorShape();
        bool busy = shape is "wait" or "appStarting";
        if (busy == _cursorBusy) return;
        _cursorBusy = busy;
        Emit(new JsonObject { ["t"] = now, ["type"] = "cursor", ["shape"] = shape, ["busy"] = busy });
    }

    /// <summary>Titel, Fokus und sichtbare Texte (mit Häkchen) des Vordergrundfensters nach einer Aktion.</summary>
    void ProbeState()
    {
        nint root = WindowRegistry.Root(GetForegroundWindow());
        if (root == 0 || _reg.IsOwn(root)) return;
        var win = _reg.Peek(root);
        if (win is { Excluded: true } || win is { InScope: false }) return;
        var level = Win32Level; string? wid = win?.Id;
        _uia.Enqueue(() =>
        {
            var r = new UiaResult { Status = "ok", Src = "win32" };
            try { r.Win32 = Win32Detail.State(root, level); } catch { }
            // Explorer-Ordner, Browser-Adresse, Office-Zelle nach der Aktion (eigene Zeitlimits)
            if (Options.AppContext) try { AppProbe.EnrichState(r.Win32, root, level); } catch { }
            return r;
        }, res =>
        {
            lock (_gate)
            {
                if (_w.Closed || res.Win32 == null) return;
                Emit(new JsonObject { ["t"] = res.TEnd, ["type"] = "win32.state", ["win"] = wid, ["state"] = res.Win32 }.Clean());
            }
        });
    }

    long? WriteDwellIfUsable(int vx, int vy)
    {
        var d = _dwell;
        if (d == null) return null;
        if (_dwellWritten) return _dwellSeqWritten;
        if (Math.Abs(vx - _dwellX) > 5 || Math.Abs(vy - _dwellY) > 5 || Now() - _dwellT > 1_500_000) { _dwell = null; return null; }
        var (m, x, y) = _topo.ToLocal(_dwellX, _dwellY);
        var ev = ElementEvent("dwell", _dwellRefSeq, d, m, x, y, null);
        _dwellSeqWritten = Emit(ev);
        _dwellWritten = true;
        return _dwellSeqWritten;
    }
    long _dwellSeqWritten;

    JsonObject ElementEvent(string trigger, long refSeq, UiaResult r, string? m, int x, int y, WinEntry? win)
    {
        var ev = new JsonObject { ["t"] = r.TStart, ["tObs"] = r.TEnd, ["type"] = "element", ["trigger"] = trigger, ["ref"] = refSeq, ["status"] = r.Status, ["src"] = r.Status == "ok" ? "uia" : "none", ["ms"] = r.Ms };
        if (m != null) { ev["ep"] = _topo.Epoch; ev["m"] = m; ev["x"] = x; ev["y"] = y; }
        if (win != null && !win.Own) ev["win"] = win.Id;
        if (r.Win32 != null) ev["win32"] = r.Win32.DeepClone();
        if (r.El != null && r.Status == "ok")
        {
            var el = (JsonObject)r.El.DeepClone();
            if (win != null && WindowRegistry.SurfaceOf(win) == "opaque") el["opaqueHint"] = true;
            ev["el"] = el;
            if (r.Path != null) ev["path"] = r.Path.DeepClone();
        }
        return ev;
    }

    /// <summary>Wie viel Text der Win32-Kontext enthalten darf (Privatmodus: keiner; streng: nur das Steuerelement).</summary>
    Win32Detail.TextLevel Win32Level => PrivacyMode ? Win32Detail.TextLevel.None : Options.PrivacyLevel == "strict" ? Win32Detail.TextLevel.Control : Win32Detail.TextLevel.All;

    void QueryElement(string trigger, long refSeq, int vx, int vy, string m, int x, int y, WinEntry? win, long? dwellSeq = null)
    {
        if (win is { Excluded: true }) return;
        bool dwellValid = dwellSeq != null;
        var level = Win32Level;
        _uia.Enqueue(() =>
        {
            JsonObject? w32 = null;
            try { w32 = Win32Detail.AtPoint(vx, vy, level); } catch { }
            var r = UiaService.Safe(() => _uia.AtPoint(vx, vy));
            // erst nach UIA: MSAA-Standardaktion, Explorer, Browser, Office (Privatmodus: nichts; kein URL bei Passwortfeldern)
            if (Options.AppContext) try { AppProbe.Enrich(w32, level, r.IsPassword || r.Ctx == "passwordField", vx, vy); } catch { }
            r.Win32 = w32;
            return r;
        }, res =>
        {
            lock (_gate)
            {
                if (_w.Closed) return;
                var ev = ElementEvent(trigger, refSeq, res, m, x, y, win);
                if (trigger == "buttonDown" && dwellSeq != null) { ev["dwellRef"] = dwellSeq; ev["dwellValid"] = dwellValid; }
                Emit(ev);
            }
        });
    }

    // ---------------- Tastatur ----------------

    sealed class PendingKey { public RingRecord R; public long T; public long InSeq; public WinEntry? Fg; public bool Self; }
    readonly List<PendingKey> _pendingKeys = new();
    long _lastKeyT; bool _valuePollDue;
    long _lastKeyFrameT = -10_000_000; string? _lastKeyFrameWin;
    readonly HashSet<uint> _maskedDown = new();

    sealed class MaskedRun { public long T, TEnd, InSeq, InSeqEnd; public int Count, Events; public string Reason = ""; public string? Fg; public long? Focus; }
    MaskedRun? _masked;
    /// <summary>Tasten, deren Druck per Einstellung nicht gespeichert wurde – das Loslassen wird ebenfalls verworfen.</summary>
    readonly HashSet<uint> _offDown = new();

    void OnKey(in RingRecord r)
    {
        long t = TFromQpc(r.Qpc);
        bool up = r.Message is WM_KEYUP or WM_SYSKEYUP;
        var fg = r.Foreground != 0 ? _reg.Ensure((nint)r.Foreground, t) : null;
        if (fg != null && !fg.Own && fg != _fgWin) { /* Vordergrund wird über WinEvents geführt */ }
        // Ausgeschlossene Anwendung im Vordergrund: keine Nummer, nichts
        if (fg is { Excluded: true })
        {
            if (up) _keys.Up(r.Data); else _keys.Down(r.Data);
            BeginGap("excludedApp", t, "certain", "keyboard", "mouse", "elements", "clipboard").Detail ??= new JsonObject { ["rule"] = fg.ExcludedRule };
            return;
        }
        long inSeq = ++_inSeq;
        if (Options.Scope == ScopeMode.App && fg != null && !fg.InScope && !fg.Own)
        {
            if (up) _keys.Up(r.Data); else _keys.Down(r.Data);
            BeginGap("outOfScope", t, "certain", "keyboard", "mouse", "elements").Keys += up ? 0 : 1;
            return;
        }
        if (_gaps.ContainsKey("outOfScope") && fg is { InScope: true }) EndGap("outOfScope", t);

        bool self = IsSelfHotkey(r.Data, up) || (fg?.Own ?? false);
        _pendingKeys.Add(new PendingKey { R = r, T = t, InSeq = inSeq, Fg = fg, Self = self });
        _lastKeyT = t;
        if (!up && !self) RequestFocusProbe(t);
        FlushPendingKeys(force: false);
    }

    bool IsSelfHotkey(uint vk, bool up)
    {
        foreach (var (mods, hvk) in Options.SelfHotkeys)
        {
            if (hvk != vk) continue;
            bool c = (mods & MOD_CONTROL) != 0, a = (mods & MOD_ALT) != 0, s = (mods & MOD_SHIFT) != 0, w = (mods & MOD_WIN) != 0;
            if (c == _keys.Ctrl && a == _keys.Alt && s == _keys.Shift && w == _keys.Win) return true;
        }
        return false;
    }

    /// <summary>Rückhaltepuffer: Tasten werden erst freigegeben, wenn der Fokuszustand nach dem Tastendruck bekannt ist.</summary>
    void FlushPendingKeys(bool force)
    {
        long now = Now();
        while (_pendingKeys.Count > 0)
        {
            var pk = _pendingKeys[0];
            var f = _focus;
            bool focusKnown = f != null && f.TObs >= pk.T;
            bool expired = now - pk.T >= Options.HoldMaxMs * 1000L;
            if (!pk.Self && !focusKnown && !expired && !force && !PrivacyMode) return;
            _pendingKeys.RemoveAt(0);
            ProcessKey(pk, focusKnown ? f : (f != null && f.Hwnd == pk.Fg?.Hwnd ? f : null));
        }
    }

    void ProcessKey(PendingKey pk, FocusInfo? focus)
    {
        var r = pk.R;
        uint vk = r.Data;
        bool up = r.Message is WM_KEYUP or WM_SYSKEYUP;
        bool ext = (r.Flags & LLKHF_EXTENDED) != 0;
        bool inj = (r.Flags & LLKHF_INJECTED) != 0;
        string key = KeyNames.Name(vk, ext);
        // AltGr: vom System erzeugtes LCtrl vor RAlt
        bool altGrPart = vk == 0xA2 && r.Scan == 0x21D;
        string ctx = focus?.Ctx ?? (pk.Fg != null && WindowRegistry.SurfaceOf(pk.Fg) == "opaque" ? "opaque" : "unknown");

        if (up)
        {
            if (_offDown.Remove(vk)) { _keys.Up(vk); return; }
            bool wasRepeatMasked = _maskedDown.Remove(vk);
            _keys.Up(vk);
            if (wasRepeatMasked && _masked != null) { _masked.Events++; _masked.InSeqEnd = pk.InSeq; _masked.TEnd = pk.T; return; }
            var ev = new JsonObject { ["t"] = pk.T, ["inSeq"] = pk.InSeq, ["type"] = "key.up", ["vk"] = vk, ["sc"] = r.Scan, ["key"] = key };
            if (ext) ev["ext"] = true;
            if (pk.Fg != null && !pk.Fg.Own) ev["fg"] = pk.Fg.Id;
            if (altGrPart) ev["altGrPart"] = true;
            if (inj) ev["inj"] = true;
            if (pk.Self) ev["self"] = true;
            Emit(ev);
            return;
        }

        bool rep = _keys.IsDown(vk);
        var mods = _keys.Mods(vk);
        bool isMod = KeyNames.IsModifier(key) || KeyNames.IsLock(key);
        bool combo = !isMod && (_keys.Ctrl || _keys.Alt || _keys.Win) && !_keys.AltGr;
        bool terminatorKey = key is "Enter" or "NumPadEnter" or "Tab" or "Escape";

        // Per Einstellung abgeschaltete Eingabearten: Tastenzustand weiterführen, aber nichts speichern
        if (!pk.Self && Options.Off.Count > 0)
        {
            bool keep = KeyNames.Channel(key, combo, ctx) switch
            {
                "shift" or "altGr" => Options.Rec("typing") || Options.Rec("hotkeys"),
                "modifier" => Options.Rec("hotkeys") || (altGrPart && Options.Rec("typing")),
                var channel => Options.Rec(channel)
            };
            if (!keep) { _keys.Down(vk); _offDown.Add(vk); return; }
        }

        // Schutzentscheidung
        string? maskReason = null;
        if (!pk.Self)
        {
            if (PrivacyMode && !isMod) maskReason = "privacyMode";
            else if (ctx == "passwordField" && !isMod && !combo && !terminatorKey) maskReason = "passwordField";
            else if (Options.PrivacyLevel == "strict" && ctx == "unknown" && !isMod && !combo && !terminatorKey) maskReason = "strictUnknown";
        }
        if (maskReason != null)
        {
            if (_masked != null && _masked.Reason != maskReason) FlushMasked();
            _masked ??= new MaskedRun { T = pk.T, InSeq = pk.InSeq, Reason = maskReason, Fg = pk.Fg?.Id, Focus = _focusSeq == 0 ? null : _focusSeq };
            _masked.Count++; _masked.Events++; _masked.TEnd = pk.T; _masked.InSeqEnd = pk.InSeq;
            _maskedDown.Add(vk);
            _keys.Down(vk);
            _valuePollDue = false;
            RememberRun(pk.T, 0);
            ActionLine?.Invoke("Eingabe (maskiert)");
            return;
        }
        // Modifier-Ereignisse innerhalb eines maskierten Laufs zählen mit
        if (_masked != null && isMod && !pk.Self) { _masked.Events++; _masked.InSeqEnd = pk.InSeq; _keys.Down(vk); _maskedDown.Add(vk); return; }
        FlushMasked();

        nint hkl = GetKeyboardLayout(pk.Fg != null ? GetWindowThreadProcessId(pk.Fg.Hwnd, out _) : 0);
        var (ch, dead) = _keys.Translate(vk, r.Scan, hkl);
        _keys.Down(vk);

        var ev2 = new JsonObject { ["t"] = pk.T, ["inSeq"] = pk.InSeq, ["type"] = "key.down", ["vk"] = vk, ["sc"] = r.Scan, ["key"] = key };
        if (ext) ev2["ext"] = true;
        ev2["hkl"] = KeyTranslator.HklHex(hkl);
        if (ch != null) ev2["ch"] = ch;
        if (dead) ev2["dead"] = true;
        if (rep) ev2["rep"] = true;
        if (mods.Count > 0) ev2["mods"] = new JsonArray(mods.Select(s => (JsonNode)s).ToArray());
        ev2["locks"] = new JsonArray(_keys.Locks().Select(s => (JsonNode)s).ToArray());
        if (altGrPart) ev2["altGrPart"] = true;
        if (_keys.Alt) ev2["altDown"] = true;
        if (inj) ev2["inj"] = true;
        if (pk.Fg != null && !pk.Fg.Own) ev2["fg"] = pk.Fg.Id;
        if (_focusSeq != 0 && focus != null) ev2["focus"] = _focusSeq;
        ev2["ctx"] = ctx;
        if (pk.Self) ev2["self"] = true;
        long seq = Emit(ev2);
        if (pk.Self || altGrPart || isMod && !rep) return;

        // Bilder: jede Nicht-Wiederholung; beim Tippen höchstens alle 250 ms
        bool printable = ch != null && !combo;
        bool typing = printable && pk.T - _lastKeyFrameT < 500_000 && _lastKeyFrameWin == pk.Fg?.Id;
        if (!rep && (!typing || pk.T - _lastKeyFrameT >= Options.TypingFrameMinGapMs * 1000L))
        {
            _frames.PreInput(pk.T, seq, CurrentMonitor());
            _lastKeyFrameT = pk.T; _lastKeyFrameWin = pk.Fg?.Id;
        }
        _frames.AwaitSettle(pk.T, seq, CurrentMonitor());
        if (printable || KeyNames.IsEditKey(key)) { _valuePollDue = ctx == "textField"; RememberRun(pk.T, seq); }
        if (combo) ActionLine?.Invoke("Tastenkombination " + string.Join("+", mods.Select(Cap).Append(key)));
        else if (printable) ActionLine?.Invoke("Texteingabe");
        else if (!isMod) ActionLine?.Invoke("Taste " + key);
        if (combo || terminatorKey) ActionCount++;
    }

    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    void RememberRun(long t, long seq)
    {
        if (_lastRunFromSeq == 0 || t - _lastRunEndT > 2_000_000) { _lastRunFromSeq = seq != 0 ? seq : _w.LastSeq + 1; _lastRunFromT = t; }
        _lastRunToSeq = seq != 0 ? seq : _w.LastSeq + 1;
        _lastRunEndT = t;
    }
    long _lastRunEndT;

    void FlushMasked()
    {
        var mr = _masked;
        if (mr == null) return;
        _masked = null;
        var ev = new JsonObject
        {
            ["t"] = mr.T, ["tEnd"] = mr.TEnd, ["type"] = "text.masked", ["count"] = mr.Count, ["inSeq"] = mr.InSeq, ["inSeqEnd"] = mr.InSeqEnd,
            ["events"] = mr.Events, ["reason"] = mr.Reason
        };
        if (mr.Fg != null) ev["fg"] = mr.Fg;
        if (mr.Focus != null) ev["focus"] = mr.Focus;
        long seq = Emit(ev);
        RememberRun(mr.T, seq);
        _lastRunToSeq = seq;
    }

    void PollValue(string phase)
    {
        if (PrivacyMode) return;
        _uia.Enqueue(() => _uia.Focused(false), res =>
        {
            lock (_gate)
            {
                if (_w.Closed || res.Status != "ok" || res.El == null || res.IsPassword) return;
                if (res.Value == null) return;
                var root = WindowRegistry.Root((nint)(res.El.Str("hwnd") is { } h ? Convert.ToInt64(h, 16) : 0));
                var win = root != 0 ? _reg.Peek(root) : _fgWin;
                if (win is { Excluded: true }) return;
                var ev = new JsonObject { ["t"] = res.TEnd, ["tSrc"] = "obs", ["type"] = "ui.value", ["el"] = ElementRef(res.El), ["phase"] = phase, ["value"] = res.Value };
                if (win != null) ev["win"] = win.Id;
                if (res.ValueLen > res.Value.Length) { ev["len"] = res.ValueLen; ev["truncated"] = true; }
                Emit(ev);
            }
        });
    }

    static JsonObject ElementRef(JsonObject el)
    {
        var o = new JsonObject();
        foreach (var k in new[] { "rid", "ct", "name", "aid" }) if (el[k] != null) o[k] = el[k]!.DeepClone();
        return o;
    }

    // ---------------- Wächter ----------------

    long _lastHostBeat; int _missedBeats;
    void Watch()
    {
        long beat = _host.Ring.Heartbeat;
        if (beat == _lastHostBeat && State == RecorderState.Recording && !_host.InProcess)
        {
            if (++_missedBeats == 8) { Warning?.Invoke("Capture-Host reagiert nicht."); SingleGap("hookLost", Now(), Now(), "possible", new JsonObject { ["channel"] = "keyboard" }, "keyboard", "mouse"); }
        }
        else { _missedBeats = 0; _lastHostBeat = beat; }
        // Speicherplatz
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_w.Folder)!);
            if (drive.AvailableFreeSpace < 200L * 1024 * 1024 && State == RecorderState.Recording)
            {
                _w.ReleaseReserve();
                Emit(new JsonObject { ["t"] = Now(), ["type"] = "system", ["kind"] = "disk", ["state"] = "full", ["freeBytes"] = drive.AvailableFreeSpace });
                Warning?.Invoke("Datenträger fast voll – Aufnahme angehalten.");
                Pause("diskFull");
            }
        }
        catch { }
    }
}
