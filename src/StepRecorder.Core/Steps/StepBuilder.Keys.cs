using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Capture;
using StepRecorder.Data;

namespace StepRecorder.Steps;

public sealed partial class StepBuilder
{
    // Tipplauf-Zustand
    readonly JsonArray _tokens = new();
    readonly StringBuilder _curText = new();     // laufende Zeichenkette (Token)
    readonly StringBuilder _replay = new();      // nachgespieltes Ergebnis
    bool _replayValid;
    int _runChars, _runKeys;
    long _runFirstT;
    readonly JsonArray _pauses = new();

    static readonly Dictionary<string, string> CommonMeaning = new()
    {
        ["Ctrl+S"] = "save", ["Ctrl+C"] = "copy", ["Ctrl+X"] = "cut", ["Ctrl+V"] = "paste", ["Ctrl+Z"] = "undo", ["Ctrl+Y"] = "redo",
        ["Ctrl+A"] = "selectAll", ["Ctrl+F"] = "find", ["Ctrl+P"] = "print", ["Ctrl+N"] = "new", ["Ctrl+O"] = "open", ["Ctrl+W"] = "closeTab",
        ["Ctrl+T"] = "newTab", ["Alt+F4"] = "closeWindow", ["Ctrl+Shift+Escape"] = "taskManager", ["Win+D"] = "showDesktop", ["Win+E"] = "explorer",
        ["Win+L"] = "lock", ["Ctrl+Insert"] = "copy", ["Shift+Insert"] = "paste", ["Shift+Delete"] = "cut", ["Ctrl+Shift+S"] = "saveAs", ["F5"] = "refresh",
    };

    static string ComboName(List<string> mods, string key)
    {
        var parts = new List<string>();
        if (mods.Contains("ctrl")) parts.Add("Ctrl");
        if (mods.Contains("alt")) parts.Add("Alt");
        if (mods.Contains("shift")) parts.Add("Shift");
        if (mods.Contains("win")) parts.Add("Win");
        parts.Add(KeyDisplay(key));
        return string.Join("+", parts);
    }

    static string KeyDisplay(string key) => key switch
    {
        "LeftCtrl" or "RightCtrl" => "Ctrl", "LeftAlt" or "RightAlt" => "Alt", "LeftShift" or "RightShift" => "Shift", "LeftWin" or "RightWin" => "Win",
        "NumPadEnter" => "Enter", _ => key
    };

    void OnKeyDown(JsonObject e)
    {
        string key = e.Str("key") ?? "";
        long vk = e.Long("vk");
        var mods = e.StrList("mods");
        string? ch = e.Str("ch");
        bool dead = e.Bool("dead") || e.Bool("ime");   // IME-Tasten gehören zur Eingabe, ergeben aber selbst kein Zeichen
        if (e.Bool("ime")) _runIme = true;
        bool isMod = KeyNames.IsModifier(key) || KeyNames.IsLock(key) || e.Bool("altGrPart");
        if (isMod)
        {
            if (_run != null) { AddInput(_run, e); _keyOwner[vk] = _run; }
            else if (_altTab != null) { AddInput(_altTab, e); _keyOwner[vk] = _altTab; }
            else _pendingMods.Add(e);
            return;
        }
        _lastClick = null;
        EndWheel();
        bool hasCmd = mods.Contains("ctrl") || mods.Contains("alt") || mods.Contains("win");
        // Fensterwechsel mit Alt+Tab
        if (key == "Tab" && mods.Contains("alt") && !mods.Contains("ctrl"))
        {
            EndRun();
            if (_altTab == null)
            {
                _altTab = NewStep("windowSwitch", e);
                foreach (var m in _pendingMods) AddInput(_altTab, m);
                _pendingMods.Clear();
                _altTab.InputObj["via"] = "altTab"; _altTab.InputObj["count"] = 0;
                _altTab.InputObj["from"] = e.Str("fg");
                _altTab.Win = e.Str("fg");
            }
            AddInput(_altTab, e);
            _altTab.InputObj["count"] = _altTab.InputObj.Int("count") + 1;
            _keyOwner[vk] = _altTab;
            return;
        }
        bool editKey = KeyNames.IsEditKey(key);
        bool charKey = ch != null && !dead && !hasCmd;
        if (hasCmd && !(editKey && _run != null))
        {
            EndRun();
            string combo = ComboName(mods, key);
            if (_lastHotkey != null && _lastCombo == combo && e.Long("t") - _lastHotkeyT <= RepeatMergeGapMs * 1000L && _steps[^1] == _lastHotkey)
            {
                AddInput(_lastHotkey, e);
                _lastHotkey.InputObj["count"] = _lastHotkey.InputObj.Int("count") + 1;
            }
            else
            {
                var st = NewStep("hotkey", e);
                FlushMovesKeepPending();
                foreach (var m in _pendingMods) AddInput(st, m);
                _pendingMods.Clear();
                AddInput(st, e);
                st.Win = e.Str("fg");
                st.InputObj["combo"] = combo;
                st.InputObj["keys"] = new JsonArray(ModKeys(mods).Append(key).Select(k => (JsonNode)k).ToArray());
                st.InputObj["count"] = 1;
                if (CommonMeaning.TryGetValue(combo, out var cm)) st.InputObj["commonMeaning"] = cm;
                if (e.Bool("inj")) st.Flags.Add("injected");
                _lastHotkey = st; _lastCombo = combo;
            }
            _lastHotkeyT = e.Long("t");
            _keyOwner[vk] = _lastHotkey!;
            return;
        }
        if (charKey || dead || (_run != null && editKey))
        {
            long focus = e.Long("focus");
            if (_run != null && ((focus != 0 && _runFocus != 0 && focus != _runFocus && !SameFocusElement(focus, _runFocus)) || e.Str("fg") != _runFg)) EndRun();
            if (_run == null) StartRun(e);
            var run = _run!;
            foreach (var m in _pendingMods) AddInput(run, m);
            _pendingMods.Clear();
            if (e.Long("t") - _runLastT >= TextPauseNoteMs * 1000L && _runLastT > 0)
                _pauses.Add(new JsonObject { ["afterChars"] = _runChars, ["us"] = e.Long("t") - _runLastT });
            AddInput(run, e);
            _keyOwner[vk] = run;
            _runLastT = e.Long("t");
            _runKeys++;
            if (charKey)
            {
                _curText.Append(ch);
                _replay.Append(ch);
                _runChars++;
            }
            else if (editKey)
            {
                FlushCurText();
                AppendKeyToken(key, mods);
                if (key == "Backspace" && mods.Count == 0 && _replay.Length > 0) _replay.Length -= char.IsLowSurrogate(_replay[^1]) && _replay.Length > 1 ? 2 : 1;
                else _replayValid = false;
            }
            if (e.Bool("inj")) run.Flags.Add("injected");
            return;
        }
        // Enter / Tab / Escape
        if (key is "Enter" or "NumPadEnter" or "Tab" or "Escape" && _run != null)
        {
            bool content = key switch
            {
                "Enter" or "NumPadEnter" => _runCtx == "textField" && FocusControlType(_runFocus) == "Document",
                "Tab" => _runCtx == "textField" && !FocusChangeSoon(e.Long("t")) && FocusControlType(_runFocus) == "Document",
                _ => false
            };
            if (_runCtx is "opaque" or "unknown") content = false;
            var run = _run!;
            AddInput(run, e);
            _keyOwner[vk] = run;
            _runKeys++;
            if (content)
            {
                FlushCurText(); AppendKeyToken(key, mods);
                _replay.Append(key == "Tab" ? "\t" : "\n");
                _runLastT = e.Long("t");
                return;
            }
            run.InputObj["terminator"] = new JsonObject { ["key"] = KeyDisplay(key), ["mods"] = new JsonArray(mods.Select(m => (JsonNode)m).ToArray()) };
            EndRun();
            return;
        }
        // Einzeltaste
        EndRun();
        if (_lastKeyStep != null && _lastKeyName == key + "|" + string.Join(",", mods) && (e.Bool("rep") || e.Long("t") - _lastKeyT <= RepeatMergeGapMs * 1000L) && _steps[^1] == _lastKeyStep)
        {
            AddInput(_lastKeyStep, e);
            _lastKeyStep.InputObj["count"] = _lastKeyStep.InputObj.Int("count") + 1;
            if (e.Bool("rep")) _lastKeyStep.InputObj["held"] = true;
        }
        else
        {
            var st = NewStep("key", e);
            FlushMovesKeepPending();
            foreach (var m in _pendingMods) AddInput(st, m);
            _pendingMods.Clear();
            AddInput(st, e);
            st.Win = e.Str("fg");
            st.InputObj["key"] = KeyDisplay(key);
            if (mods.Count > 0) st.InputObj["mods"] = new JsonArray(mods.Select(m => (JsonNode)m).ToArray());
            st.InputObj["count"] = 1;
            st.InputObj["held"] = false;
            if (ch != null && (e.Str("ctx") == "nonText")) st.InputObj["ch"] = ch;
            if (e.Bool("inj")) st.Flags.Add("injected");
            _lastKeyStep = st;
            _lastKeyName = key + "|" + string.Join(",", mods);
        }
        _lastKeyT = e.Long("t");
        _keyOwner[vk] = _lastKeyStep!;
    }

    static IEnumerable<string> ModKeys(List<string> mods)
    {
        if (mods.Contains("ctrl")) yield return "LeftCtrl";
        if (mods.Contains("alt")) yield return "LeftAlt";
        if (mods.Contains("shift")) yield return "LeftShift";
        if (mods.Contains("win")) yield return "LeftWin";
    }

    /// <summary>Bewegungen vor Tastaturschritten bleiben für den nächsten Mausschritt liegen.</summary>
    void FlushMovesKeepPending() { }

    bool SameFocusElement(long a, long b)
    {
        string? ra = _bySeq.TryGetValue(a, out var ea) ? ea.Obj("el").Str("rid") : null;
        string? rb = _bySeq.TryGetValue(b, out var eb) ? eb.Obj("el").Str("rid") : null;
        return ra != null && ra == rb;
    }

    string? FocusControlType(long focusSeq) => focusSeq != 0 && _bySeq.TryGetValue(focusSeq, out var f) ? f.Obj("el").Str("ct") : null;

    bool FocusChangeSoon(long t) => _ev.Any(e => e.Str("type") == "focus" && e.Long("t") > t && e.Long("t") <= t + TabFocusMs * 1000L);

    void StartRun(JsonObject e)
    {
        var st = NewStep("type", e);
        _run = st;
        _runFocus = e.Long("focus");
        _runFg = e.Str("fg");
        _runCtx = e.Str("ctx") ?? "unknown";
        _runLastT = 0; _runFirstT = e.Long("t");
        _tokens.Clear(); _curText.Clear(); _replay.Clear(); _replayValid = true; _runChars = 0; _runKeys = 0;
        _pauses.Clear();
        st.Win = _runFg;
        st.InputObj["ctxAtStart"] = _runCtx;
        if (_runFocus != 0) st.InputObj["focusSeq"] = _runFocus;
    }

    void FlushCurText()
    {
        if (_curText.Length == 0) return;
        _tokens.Add(_curText.ToString());
        _curText.Clear();
    }

    void AppendKeyToken(string key, List<string> mods)
    {
        var last = _tokens.Count > 0 ? _tokens[^1] as JsonObject : null;
        string mk = string.Join(",", mods);
        if (last != null && last.Str("key") == KeyDisplay(key) && string.Join(",", last.StrList("mods")) == mk)
        { last["n"] = last.Int("n", 1) + 1; return; }
        var tok = new JsonObject { ["key"] = KeyDisplay(key) };
        if (mods.Count > 0) tok["mods"] = new JsonArray(mods.Select(m => (JsonNode)m).ToArray());
        tok["n"] = 1;
        _tokens.Add(tok);
    }

    bool _runIme;

    void EndRun()
    {
        var st = _run;
        if (st == null) return;
        _run = null;
        FlushCurText();
        st.InputObj["tokens"] = _tokens.DeepClone();
        st.InputObj["masked"] = false;
        st.InputObj["count"] = _runKeys;
        if (_replayValid && _replay.Length > 0) st.InputObj["text"] = _replay.ToString();
        st.InputObj["inputMethod"] = _runIme ? "ime" : "keyboard";
        _runIme = false;
        st.InputObj["durationUs"] = Math.Max(0, _runLastT - _runFirstT);
        if (_pauses.Count > 0) st.InputObj["pauses"] = _pauses.DeepClone();
        if (_runCtx == "opaque") st.Flags.Add("plaintext");
        // In Nicht-Text-Kontext: ein einzelnes Zeichen ist eine Taste (Listen-Schnellsuche erst ab zwei)
        if (_runCtx == "nonText" && _runChars == 1 && _tokens.Count == 1 && _tokens[0] is JsonValue && st.InputObj["terminator"] == null)
        {
            var downs = st.Input.Select(s => _bySeq[s]).Where(x => x.Str("type") == "key.down" && x.Str("ch") != null).ToList();
            if (downs.Count == 1)
            {
                st.Kind = "key";
                st.InputObj = new JsonObject { ["key"] = KeyDisplay(downs[0].Str("key") ?? ""), ["ch"] = downs[0].Str("ch"), ["count"] = 1, ["held"] = false };
            }
        }
        _tokens.Clear();
    }

    void OnKeyUp(JsonObject e)
    {
        long vk = e.Long("vk");
        if (_keyOwner.Remove(vk, out var owner)) { AddInput(owner, e); if (owner == _altTab && e.Str("key") is "LeftAlt" or "RightAlt") _altTab = null; return; }
        // Modifier allein gedrückt und losgelassen
        var down = _pendingMods.LastOrDefault(m => m.Long("vk") == vk);
        if (down != null)
        {
            var st = LoneModifier(down);
            AddInput(st, e);
            return;
        }
        if (e.Str("key") is "LeftAlt" or "RightAlt") _altTab = null;
        var last = _steps.LastOrDefault(s => s.Kind is "type" or "hotkey" or "key" or "windowSwitch");
        if (last != null) AddInput(last, e);
        else
        {
            _warnings.Add(new JsonObject { ["code"] = "unpairedKeyUp", ["seq"] = e.Long("seq"), ["message"] = e.Str("key") });
            var st = NewStep("key", e); AddInput(st, e);
            st.InputObj["key"] = KeyDisplay(e.Str("key") ?? ""); st.InputObj["count"] = 0; st.Flags.Add("incomplete");
        }
    }

    StepDraft LoneModifier(JsonObject down)
    {
        _pendingMods.Remove(down);
        var st = NewStep("key", down);
        AddInput(st, down);
        st.Win = down.Str("fg");
        st.InputObj["key"] = KeyDisplay(down.Str("key") ?? "");
        st.InputObj["count"] = 1;
        st.InputObj["held"] = false;
        _keyOwner.Remove(down.Long("vk"));
        return st;
    }

    void OnMasked(JsonObject e)
    {
        EndRun();
        var st = NewStep("type", e);
        foreach (var m in _pendingMods) AddInput(st, m);
        _pendingMods.Clear();
        AddInput(st, e);
        st.Win = e.Str("fg");
        long? count = e.LongN("count");
        st.InputObj["tokens"] = new JsonArray(new JsonObject { ["masked"] = count == null ? null : JsonValue.Create(count.Value) });
        st.InputObj["masked"] = true;
        if (count != null) st.InputObj["count"] = count;
        st.InputObj["maskReason"] = e.Str("reason") ?? "unresolved";
        if (e.Str("rule") != null) st.InputObj["rule"] = e.Str("rule");
        if (e.Str("promptLine") != null) st.InputObj["promptLine"] = e.Str("promptLine");
        st.InputObj["inputMethod"] = "keyboard";
        st.InputObj["resultSource"] = "none";
        st.InputObj["durationUs"] = e.Long("tEnd", e.Long("t")) - e.Long("t");
        if (e.LongN("focus") is long f) st.InputObj["focusSeq"] = f;
        st.Flags.Add("masked");
    }
}
