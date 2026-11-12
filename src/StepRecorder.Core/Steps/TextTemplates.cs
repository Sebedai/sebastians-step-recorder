using System.Globalization;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Steps;

/// <summary>Textvorlagen (05 §12): Titel und Text je Schritt in Deutsch/Englisch, Stil Protokoll oder Anleitung.</summary>
public static class TextTemplates
{
    static readonly Dictionary<string, (string de, string deArt, string en)> TypeWords = new()
    {
        ["Button"] = ("Schaltfläche", "die Schaltfläche", "button"), ["SplitButton"] = ("Schaltfläche", "die Schaltfläche", "button"),
        ["Edit"] = ("Feld", "das Feld", "field"), ["Document"] = ("Textbereich", "den Textbereich", "text area"),
        ["CheckBox"] = ("Kontrollkästchen", "das Kontrollkästchen", "checkbox"), ["RadioButton"] = ("Optionsfeld", "das Optionsfeld", "option"),
        ["ComboBox"] = ("Auswahlliste", "die Auswahlliste", "drop-down list"), ["List"] = ("Liste", "die Liste", "list"),
        ["ListItem"] = ("Eintrag", "den Eintrag", "item"), ["TreeItem"] = ("Eintrag", "den Eintrag", "item"), ["DataItem"] = ("Zeile", "die Zeile", "row"),
        ["Menu"] = ("Menü", "das Menü", "menu"), ["MenuBar"] = ("Menüleiste", "die Menüleiste", "menu bar"), ["MenuItem"] = ("Menüpunkt", "den Menüpunkt", "menu item"),
        ["TabItem"] = ("Registerkarte", "die Registerkarte", "tab"), ["Tree"] = ("Baumansicht", "die Baumansicht", "tree"),
        ["Table"] = ("Tabelle", "die Tabelle", "table"), ["DataGrid"] = ("Tabelle", "die Tabelle", "table"), ["HeaderItem"] = ("Spaltenüberschrift", "die Spaltenüberschrift", "column header"),
        ["Hyperlink"] = ("Link", "den Link", "link"), ["Image"] = ("Bild", "das Bild", "image"), ["Slider"] = ("Schieberegler", "den Schieberegler", "slider"),
        ["Spinner"] = ("Drehfeld", "das Drehfeld", "spinner"), ["ScrollBar"] = ("Bildlaufleiste", "die Bildlaufleiste", "scroll bar"),
        ["ToolBar"] = ("Symbolleiste", "die Symbolleiste", "toolbar"), ["StatusBar"] = ("Statusleiste", "die Statusleiste", "status bar"),
        ["TitleBar"] = ("Titelleiste", "die Titelleiste", "title bar"), ["Text"] = ("Text", "den Text", "text"), ["Window"] = ("Fenster", "das Fenster", "window"),
    };

    static readonly Dictionary<string, string> KeyDe = new()
    {
        ["Ctrl"] = "Strg", ["Shift"] = "Umschalt", ["Enter"] = "Eingabetaste", ["Escape"] = "Esc", ["Space"] = "Leertaste", ["Backspace"] = "Rücktaste",
        ["Delete"] = "Entf", ["Insert"] = "Einfg", ["Home"] = "Pos1", ["End"] = "Ende", ["PageUp"] = "Bild auf", ["PageDown"] = "Bild ab",
        ["Left"] = "Pfeil nach links", ["Right"] = "Pfeil nach rechts", ["Up"] = "Pfeil nach oben", ["Down"] = "Pfeil nach unten",
        ["PrintScreen"] = "Druck", ["Apps"] = "Kontextmenütaste", ["Tab"] = "Tab", ["Alt"] = "Alt", ["Win"] = "Win"
    };

    public static string Key(string k, string lang)
    {
        if (lang != "de") return k switch { "Escape" => "Esc", _ => k };
        return string.Join("+", k.Split('+').Select(p => KeyDe.TryGetValue(p, out var d) ? d : p));
    }

    static string Q(string s, string lang) => lang == "de" ? $"„{s}“" : $"“{s}”";

    public static string CleanName(string? n)
    {
        if (string.IsNullOrWhiteSpace(n)) return "";
        var s = n.Replace("\r", " ").Replace("\n", " ").Replace("&", "").Trim();
        if (s.EndsWith(':')) s = s[..^1].TrimEnd();
        return s.Length > 60 ? s[..59] + "…" : s;
    }

    /// <summary>Bezeichnung des Ziels (Rückfallkette: Name → beschriftet durch → Wert → Hilfe → Vorfahre → Position).</summary>
    public static string TargetLabel(JsonObject step, string lang, bool article, string which = "target")
    {
        var t = step.Obj(which);
        var el = t?.Obj("el");
        var ctx = step.Obj("ctx");
        string ct = el.Str("ct") ?? "Unknown";
        TypeWords.TryGetValue(ct, out var tw);
        string word = lang == "de" ? (article ? (tw.deArt ?? "das Element") : (tw.de ?? "Element")) : (tw.en ?? "element");
        if (tw == default) word = lang == "de" ? (article ? "das Element" : "Element") : "element";
        string name = CleanName(el.Str("name"));
        if (name.Length == 0) name = CleanName(el.Obj("labeledBy").Str("name"));
        if (name.Length > 0) return $"{word} {Q(name, lang)}";
        if (el.Str("value") is { Length: > 0 } v && ct is "ComboBox" or "Edit" or "Spinner")
            return lang == "de" ? $"{word} mit dem Wert {Q(CleanName(v), lang)}" : $"{word} with the value {Q(CleanName(v), lang)}";
        if (CleanName(el.Str("help")) is { Length: > 0 } h) return $"{word} ({h})";
        var path = t?.Arr("path");
        var anc = path?.Reverse().Select(p => CleanName(p.Str("name"))).FirstOrDefault(n => n.Length > 0);
        if (el != null && anc != null) return lang == "de" ? $"{word} im Bereich {Q(anc, lang)}" : $"{word} in {Q(anc, lang)}";
        var pos = which == "endTarget" ? step.Obj("endPosition") : step.Obj("position");
        string title = CleanName(ctx.Str("title"));
        if (pos != null)
            return lang == "de" ? $"die Stelle bei ({pos.Int("x")}, {pos.Int("y")}){(title.Length > 0 ? " im Fenster " + Q(title, lang) : "")}"
                                : $"the spot at ({pos.Int("x")}, {pos.Int("y")}){(title.Length > 0 ? " in the window " + Q(title, lang) : "")}";
        return lang == "de" ? "ein unbekanntes Element" : "an unknown element";
    }

    static string N(JsonObject step) => CleanName(step.Obj("target").Obj("el").Str("name")) is { Length: > 0 } n ? n : CleanName(step.Obj("target").Obj("el").Obj("labeledBy").Str("name"));
    static string F(JsonObject step, string lang) => CleanName(step.Obj("ctx").Str("title")) is { Length: > 0 } t ? Q(t, lang) : (lang == "de" ? "(ohne Titel)" : "(untitled)");

    public static string Duration(long us, string lang)
    {
        var c = lang == "de" ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;
        if (us < 1_000_000) return $"{us / 1000} ms";
        if (us < 60_000_000) return (us / 1e6).ToString("0.0", c) + " s";
        var ts = TimeSpan.FromTicks(us * 10);
        return lang == "de" ? $"{(int)ts.TotalMinutes} min {ts.Seconds} s" : $"{(int)ts.TotalMinutes} min {ts.Seconds} s";
    }

    public static string CauseText(string cause, string lang) => lang == "de" ? cause switch
    {
        "paused" => "Die Aufnahme war angehalten.",
        "privacyMode" => "Privatmodus: Eingaben wurden nicht aufgezeichnet.",
        "excludedApp" => "Eine ausgeschlossene Anwendung war im Vordergrund; davon wurde nichts aufgezeichnet.",
        "outOfScope" => "Arbeit außerhalb des Aufnahmebereichs; nicht aufgezeichnet.",
        "secureDesktop" => "Sicherer Desktop (z. B. Abfrage der Benutzerkontensteuerung): Eingaben und Bilder konnten nicht aufgezeichnet werden.",
        "sessionLocked" or "sessionDisconnected" or "suspend" => "Der Rechner war gesperrt, getrennt oder im Ruhezustand.",
        "elevatedWindow" => "Ein Fenster mit Administratorrechten war im Vordergrund: Eingaben waren nicht sichtbar, Bilder wurden aufgezeichnet.",
        "hookLost" or "inputMismatch" or "overload" => "Eingaben wurden möglicherweise unvollständig aufgezeichnet.",
        "captureLost" or "frameDropped" => "Für diesen Zeitraum fehlen Bilder.",
        "protectedContent" => "Geschützter Inhalt wurde vom System ausgeblendet.",
        "diskFull" => "Der Datenträger war voll; die Aufnahme war angehalten.",
        "crash" => "Der Recorder wurde unerwartet beendet; ab hier fehlt die Aufzeichnung.",
        "unsupportedInput" => "Eine nicht aufzeichenbare Eingabe (z. B. Mehrfinger-Geste) wurde erkannt.",
        "relativeMouse" => "Der Mauszeiger war von der Anwendung gefangen; Positionen sind ohne Aussage.",
        "maskedInput" => "Eine Tasteneingabe wurde bewusst nicht aufgezeichnet.",
        "unobserved" => "Die Ursache wurde nicht beobachtet.",
        "removedByAuthor" => "Dieser Zeitraum wurde vom Autor entfernt.",
        _ => "Die Aufzeichnung ist hier unvollständig."
    } : cause switch
    {
        "paused" => "Recording was paused.",
        "privacyMode" => "Private mode: input was not recorded.",
        "excludedApp" => "An excluded application was in the foreground; nothing of it was recorded.",
        "outOfScope" => "Work outside the capture area; not recorded.",
        "secureDesktop" => "Secure desktop (e.g. a User Account Control prompt): input and images could not be recorded.",
        "sessionLocked" or "sessionDisconnected" or "suspend" => "The computer was locked, disconnected or asleep.",
        "elevatedWindow" => "A window with administrator rights was in the foreground: input was not visible, images were recorded.",
        "hookLost" or "inputMismatch" or "overload" => "Input may have been recorded incompletely.",
        "captureLost" or "frameDropped" => "Images are missing for this period.",
        "protectedContent" => "Protected content was hidden by the system.",
        "diskFull" => "The disk was full; recording was paused.",
        "crash" => "The recorder ended unexpectedly; the recording is missing from here on.",
        "unsupportedInput" => "An input that cannot be recorded (e.g. a multi-finger gesture) was detected.",
        "relativeMouse" => "The pointer was captured by the application; positions are meaningless.",
        "maskedInput" => "A keyboard input was deliberately not recorded.",
        "unobserved" => "The cause was not observed.",
        "removedByAuthor" => "This period was removed by the author.",
        _ => "The recording is incomplete here."
    };

    public static string ShortCause(string cause, string lang) => lang == "de" ? cause switch
    {
        "paused" => "Pause", "privacyMode" => "Privatmodus", "excludedApp" => "ausgeschlossene Anwendung", "outOfScope" => "außerhalb des Aufnahmebereichs",
        "secureDesktop" => "sicherer Desktop", "sessionLocked" => "Rechner gesperrt", "elevatedWindow" => "Administratorfenster", "removedByAuthor" => "vom Autor entfernt",
        "crash" => "Absturz", "diskFull" => "Datenträger voll", _ => "unvollständig"
    } : cause switch
    {
        "paused" => "pause", "privacyMode" => "private mode", "excludedApp" => "excluded application", "outOfScope" => "outside capture area",
        "secureDesktop" => "secure desktop", "sessionLocked" => "computer locked", "elevatedWindow" => "administrator window", "removedByAuthor" => "removed by author",
        "crash" => "crash", "diskFull" => "disk full", _ => "incomplete"
    };

    /// <summary>Getippte Token in Notation: <c>notiz.txr{Backspace}t</c>, <c>{masked:12}</c>, <c>{Enter}</c>.</summary>
    public static string TokensNotation(JsonObject input)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var tok in input.Arr("tokens") ?? new JsonArray())
        {
            if (tok is JsonValue v && v.TryGetValue<string>(out var s)) sb.Append(s.Replace("{", "{{").Replace("}", "}}"));
            else if (tok is JsonObject o)
            {
                if (o.ContainsKey("masked")) sb.Append(o["masked"] is null ? "{masked}" : $"{{masked:{o.Long("masked")}}}");
                else if (o.Str("placeholder") is { } p) sb.Append('{').Append(p).Append('}');
                else if (o.Str("uncertain") is { } u) sb.Append($"{{uncertain:{u}}}");
                else if (o.Str("unreviewed") is { } ur) sb.Append($"{{unreviewed:{ur}}}");
                else if (o.Str("key") is { } k)
                {
                    var mods = o.StrList("mods");
                    string name = (mods.Count > 0 ? string.Join("+", mods.Select(Cap)) + "+" : "") + k;
                    for (int i = 0; i < Math.Max(1, o.Int("n", 1)); i++) sb.Append('{').Append(name).Append('}');
                }
            }
        }
        if (input.Obj("terminator") is { } term) sb.Append('{').Append(term.Str("key")).Append('}');
        return sb.ToString();
    }

    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Titel (≤60 Zeichen) eines Schritts.</summary>
    public static string Title(JsonObject step, string lang)
    {
        bool de = lang == "de";
        string kind = step.Str("kind") ?? "";
        var input = step.Obj("input") ?? new JsonObject();
        string n = N(step);
        string ct = step.Obj("target").Obj("el").Str("ct") ?? "";
        string t = kind switch
        {
            "click" or "doubleClick" or "tripleClick" or "rightClick" when n.Length == 0 => de ? $"Klick im Fenster {F(step, lang)}" : $"Click in window {F(step, lang)}",
            "click" when input.Bool("longPress") && step.Obj("input").StrList("mods").Count == 0 => de ? $"{Q(n, lang)} gedrückt halten" : $"Press and hold {Q(n, lang)}",
            "click" => ct switch
            {
                "CheckBox" => step.Obj("target").Obj("el").Str("toggle") == "on" ? (de ? $"{Q(n, lang)} deaktivieren" : $"Clear {Q(n, lang)}") : (de ? $"{Q(n, lang)} aktivieren" : $"Select {Q(n, lang)}"),
                "ComboBox" => de ? $"{Q(n, lang)} öffnen" : $"Open {Q(n, lang)}",
                "ListItem" or "TreeItem" or "DataItem" or "RadioButton" => de ? $"{Q(n, lang)} auswählen" : $"Select {Q(n, lang)}",
                "MenuItem" => de ? $"Menüpunkt {Q(n, lang)} wählen" : $"Choose {Q(n, lang)}",
                "TabItem" => de ? $"Zu {Q(n, lang)} wechseln" : $"Switch to {Q(n, lang)}",
                "Edit" or "Document" => de ? $"In {Q(n, lang)} klicken" : $"Click in {Q(n, lang)}",
                _ => de ? $"{Q(n, lang)} anklicken" : $"Click {Q(n, lang)}"
            },
            "doubleClick" => de ? $"Doppelklick auf {Q(n, lang)}" : $"Double-click {Q(n, lang)}",
            "tripleClick" => de ? $"Dreifachklick auf {Q(n, lang)}" : $"Triple-click {Q(n, lang)}",
            "rightClick" => de ? $"Rechtsklick auf {Q(n, lang)}" : $"Right-click {Q(n, lang)}",
            "drag" => input.Str("dragKind") switch
            {
                "selectText" => de ? "Text markieren" : "Select text",
                "moveWindow" => de ? "Fenster verschieben" : "Move window",
                "resizeWindow" => de ? "Fenstergröße ändern" : "Resize window",
                _ => n.Length > 0 ? (de ? $"{Q(n, lang)} ziehen" : $"Drag {Q(n, lang)}") : (de ? "Ziehen" : "Drag")
            },
            "scroll" => input.Str("semantic") == "zoom" ? (de ? "Ansicht zoomen" : "Zoom view") : n.Length > 0 ? (de ? $"In {Q(n, lang)} scrollen" : $"Scroll in {Q(n, lang)}") : (de ? "Scrollen" : "Scroll"),
            "type" when input.Bool("masked") => input.Str("maskReason") == "privacyMode" ? (de ? "Vertrauliche Eingabe" : "Confidential input") : (de ? "Kennwort eingeben" : "Enter password"),
            "type" when input.Str("text") is { Length: > 0 and <= 30 } tx && !tx.Contains('\n') => de ? $"{Q(tx, lang)} eingeben" : $"Type {Q(tx, lang)}",
            "type" => n.Length > 0 ? (de ? $"Text in {Q(n, lang)} eingeben" : $"Enter text in {Q(n, lang)}") : (de ? "Text eingeben" : "Enter text"),
            "hotkey" => de ? $"{Key(input.Str("combo") ?? "", lang)} drücken" : $"Press {input.Str("combo")}",
            "key" => input.Int("count", 1) > 1 ? (de ? $"{Key(input.Str("key") ?? "", lang)} {input.Int("count")}-mal drücken" : $"Press {input.Str("key")} {input.Int("count")} times") : (de ? $"{Key(input.Str("key") ?? "", lang)} drücken" : $"Press {input.Str("key")}"),
            "hover" => de ? $"Auf {Q(n.Length > 0 ? n : "?", lang)} zeigen" : $"Point to {Q(n.Length > 0 ? n : "?", lang)}",
            "move" => de ? "Mausbewegung" : "Mouse movement",
            "windowSwitch" => de ? $"Zu {WinTitle(step, input.Str("to"), lang)} wechseln" : $"Switch to {WinTitle(step, input.Str("to"), lang)}",
            "system" => input.Str("event") switch
            {
                "windowOpened" => de ? $"{F(step, lang)} erscheint" : $"{F(step, lang)} appears",
                "windowClosed" => de ? $"{F(step, lang)} schließt sich" : $"{F(step, lang)} closes",
                "foregroundChanged" => de ? $"{F(step, lang)} im Vordergrund" : $"{F(step, lang)} in front",
                "titleChanged" => de ? "Fenstertitel ändert sich" : "Window title changes",
                "valueChanged" => de ? "Inhalt ändert sich" : "Content changes",
                "notification" => de ? "Mitteilung" : "Notification",
                "topologyChanged" => de ? "Monitor-Anordnung geändert" : "Monitor layout changed",
                "scopeChanged" => de ? "Aufnahmebereich gewechselt" : "Capture area changed",
                _ => de ? "Systemereignis" : "System event"
            },
            "wait" => de ? "Warten" : "Wait",
            "comment" => de ? "Kommentar" : "Comment",
            "snapshot" => de ? "Schnappschuss" : "Snapshot",
            "marker" => input.Str("markerKind") switch { "error" => de ? "Fehler tritt auf" : "Error occurs", "expected" => de ? "Soll-Zustand" : "Expected state", "important" => de ? "Wichtige Stelle" : "Important point", _ => "Marker" },
            "gap" => (de ? "Lücke: " : "Gap: ") + ShortCause(input.Str("cause") ?? "unknown", lang),
            _ => kind
        };
        return t.Length > 60 ? t[..59] + "…" : t;
    }

    static string WinTitle(JsonObject step, string? win, string lang) => F(step, lang);

    /// <summary>Text eines Schritts. style: "protocol" oder "guide".</summary>
    public static string Body(JsonObject step, string lang, string style = "guide", string address = "formal", string? commentText = null)
    {
        bool de = lang == "de", guide = style == "guide";
        string kind = step.Str("kind") ?? "";
        var input = step.Obj("input") ?? new JsonObject();
        var mods = input.StrList("mods");
        string Z(bool art = true) => TargetLabel(step, lang, art);
        bool posOnly = step.Obj("target").Str("status") is "position" or "none" || step.Obj("target").Obj("el") == null;
        string modText = string.Join("+", mods.Select(m => Key(Cap(m), lang)));
        string ct = step.Obj("target").Obj("el").Str("ct") ?? "";
        string r = kind switch
        {
            "click" or "doubleClick" or "tripleClick" or "rightClick" when posOnly && step.Obj("position") is { } p =>
                guide ? (de ? $"Klicken Sie im Fenster {F(step, lang)} auf die im Bild markierte Stelle." : $"In the window {F(step, lang)}, click the spot marked in the image.")
                      : (de ? $"Linksklick an Position ({p.Int("x")}, {p.Int("y")}) im Fenster {F(step, lang)}" : $"Left click at position ({p.Int("x")}, {p.Int("y")}) in window {F(step, lang)}"),
            "click" when guide && mods.Count == 0 && ct == "CheckBox" => de ? $"{(step.Obj("target").Obj("el").Str("toggle") == "on" ? "Deaktivieren" : "Aktivieren")} Sie {Z()}." : $"{(step.Obj("target").Obj("el").Str("toggle") == "on" ? "Clear" : "Select")} {Z()}.",
            "click" when guide && mods.Count == 0 && ct is "RadioButton" or "MenuItem" => de ? $"Wählen Sie {Z()}." : $"Choose {Z()}.",
            "click" when guide && mods.Count == 0 && ct is "ListItem" or "TreeItem" or "DataItem" => de ? $"Wählen Sie {Z()} aus." : $"Select {Z()}.",
            "click" when guide && mods.Count == 0 && ct == "TabItem" => de ? $"Wechseln Sie zu {Z()}." : $"Switch to {Z()}.",
            "click" when guide && mods.Count == 0 && ct == "ComboBox" => de ? $"Öffnen Sie {Z()}." : $"Open {Z()}.",
            "click" when guide && mods.Count == 0 && ct is "Edit" or "Document" => de ? $"Klicken Sie in {Z()}." : $"Click in {Z()}.",
            "click" when mods.Count == 0 && input.Bool("longPress") && !posOnly => guide ? (de ? $"Halten Sie die Maustaste auf {Z()} etwa {Seconds(input.Long("pressUs"), lang)} gedrückt." : $"Press and hold {Z()} for about {Seconds(input.Long("pressUs"), lang)}.") : (de ? $"Linke Maustaste {Seconds(input.Long("pressUs"), lang)} gedrückt gehalten auf {Z(false)}" : $"Left button held for {Seconds(input.Long("pressUs"), lang)} on {Z(false)}"),
            "click" when mods.Count > 0 => guide ? (de ? $"Halten Sie {modText} gedrückt und klicken Sie auf {Z()}." : $"Hold {modText} and click {Z()}.") : (de ? $"{modText}+Linksklick auf {Z(false)}" : $"{modText}+left click on {Z(false)}"),
            "click" => guide ? (de ? $"Klicken Sie auf {Z()}." : $"Click {Z()}.") : (de ? $"Linksklick auf {Z(false)}" : $"Left click on {Z(false)}"),
            "doubleClick" => guide ? (de ? $"Doppelklicken Sie auf {Z()}." : $"Double-click {Z()}.") : (de ? $"Doppelklick auf {Z(false)}" : $"Double click on {Z(false)}"),
            "tripleClick" => guide ? (de ? $"Klicken Sie dreimal schnell auf {Z()}." : $"Triple-click {Z()}.") : (de ? $"Dreifachklick auf {Z(false)}" : $"Triple click on {Z(false)}"),
            "rightClick" => guide ? (de ? $"Klicken Sie mit der rechten Maustaste auf {Z()}." : $"Right-click {Z()}.") : (de ? $"Rechtsklick auf {Z(false)}" : $"Right click on {Z(false)}"),
            "drag" => input.Str("dragKind") switch
            {
                "selectText" when input.Str("selectedText") is { } st => guide ? (de ? $"Markieren Sie den Text {Q(st, lang)}." : $"Select the text {Q(st, lang)}.") : (de ? $"Text markiert durch Ziehen: {Q(st, lang)}" : $"Text selected by dragging: {Q(st, lang)}"),
                "moveWindow" => guide ? (de ? $"Verschieben Sie das Fenster {F(step, lang)}." : $"Move the window {F(step, lang)}.") : (de ? $"Fenster {F(step, lang)} verschoben" : $"Window {F(step, lang)} moved"),
                "resizeWindow" => guide ? (de ? $"Ändern Sie die Größe des Fensters {F(step, lang)}." : $"Resize the window {F(step, lang)}.") : (de ? $"Fenster {F(step, lang)} in der Größe geändert" : $"Window {F(step, lang)} resized"),
                _ => guide ? (de ? $"Ziehen Sie {Z()} auf {TargetLabel(step, lang, true, "endTarget")}." : $"Drag {Z()} onto {TargetLabel(step, lang, true, "endTarget")}.") : (de ? $"Ziehen von {Z(false)} nach {TargetLabel(step, lang, false, "endTarget")}" : $"Drag from {Z(false)} to {TargetLabel(step, lang, false, "endTarget")}")
            },
            "scroll" when input.Str("semantic") == "zoom" => guide ? (de ? "Vergrößern oder verkleinern Sie die Ansicht mit Strg+Mausrad." : "Zoom with Ctrl+mouse wheel.") : (de ? $"Zoom mit Strg+Mausrad ({Math.Abs(input.Dbl("notches")):0.#} Rasten)" : $"Zoom with Ctrl+wheel ({Math.Abs(input.Dbl("notches")):0.#} notches)"),
            "scroll" => ScrollText(step, input, lang, guide),
            "type" when input.Bool("masked") => MaskedText(step, input, lang, guide),
            "type" => TypeText(step, input, lang, guide),
            "hotkey" => guide ? (de ? $"Drücken Sie {Key(input.Str("combo") ?? "", lang)}." : $"Press {input.Str("combo")}.") : (de ? $"Tastenkombination {Key(input.Str("combo") ?? "", lang)}" : $"Keyboard shortcut {input.Str("combo")}") + Meaning(input, lang, guide),
            "key" => KeyText(input, lang, guide),
            "hover" => guide ? (de ? $"Zeigen Sie auf {Z()}." : $"Point to {Z()}.") : (de ? $"Mauszeiger verweilt auf {Z(false)}" : $"Pointer rests on {Z(false)}"),
            "move" => guide ? "" : (de ? "Mausbewegung ohne folgende Aktion" : "Mouse movement without a following action"),
            "windowSwitch" => guide ? (de ? $"Wechseln Sie mit Alt+Tab zum Fenster {F(step, lang)}." : $"Press Alt+Tab to switch to the window {F(step, lang)}.") : (de ? $"Fensterwechsel mit Alt+Tab zu {F(step, lang)}" : $"Window switch with Alt+Tab to {F(step, lang)}"),
            "system" => SystemText(step, input, lang, guide),
            "wait" => guide ? (input.Obj("until")?.Str("what") == "windowOpened" ? (de ? $"Warten Sie, bis das Fenster erscheint." : "Wait until the window appears.") : (de ? "Warten Sie, bis der Vorgang abgeschlossen ist." : "Wait for the operation to finish.")) : (de ? $"Wartezeit von {Duration(input.Long("durationUs"), lang)}" : $"Waiting time of {Duration(input.Long("durationUs"), lang)}"),
            "comment" => guide ? (commentText ?? "") : (de ? $"Kommentar des Autors: {commentText}" : $"Author's comment: {commentText}"),
            "snapshot" => guide ? "" : (de ? "Schnappschuss" : "Snapshot"),
            "marker" => MarkerText(input, lang, guide),
            "gap" => guide ? (de ? "Hinweis: " : "Note: ") + CauseText(input.Str("cause") ?? "unknown", lang)
                           : (de ? $"Lücke von {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}: " : $"Gap of {Duration(step.Long("tTo") - step.Long("tFrom"), lang)}: ") + CauseText(input.Str("cause") ?? "unknown", lang),
            _ => ""
        };
        if (kind == "drag" && r.Length > 0) r += DragDetails(input, lang, guide);
        if (!guide)
        {
            if (step.Obj("target").Str("status") == "single" && kind is "click" or "doubleClick" or "rightClick" or "drag" or "type") r += de ? " (Ziel nicht bestätigt)" : " (target not confirmed)";
            if (step.Obj("target").Str("status") == "conflict") r += de ? " (Ziel uneindeutig)" : " (target ambiguous)";
            var flags = step.StrList("flags");
            if (flags.Contains("injected")) r += de ? " (automatisch erzeugte Eingabe)" : " (synthetic input)";
            if (flags.Contains("discarded")) r += de ? " (vom Autor verworfen)" : " (discarded by author)";
            if (flags.Contains("incomplete")) r += de ? " (unvollständig aufgezeichnet)" : " (incompletely recorded)";
        }
        if (guide && address == "informal" && de) r = Informal(r);
        else if (guide && address == "neutral" && de) r = Neutral(r);
        return r;
    }

    static string Seconds(long us, string lang) => (us / 1_000_000.0).ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo(lang == "de" ? "de-DE" : "en-US")) + " s";

    /// <summary>Gedrückt halten beim Ziehen: Taste, Modifikatoren, Haltezeit vor der Bewegung und über dem Ziel.</summary>
    static string DragDetails(JsonObject input, string lang, bool guide)
    {
        bool de = lang == "de";
        var parts = new List<string>();
        string button = input.Str("button") ?? "primary";
        var mods = input.StrList("mods");
        long holdBefore = input.Long("holdBeforeMoveUs"), holdEnd = input.Long("holdAtEndUs");
        if (guide)
        {
            if (button == "secondary") parts.Add(de ? "Verwenden Sie dabei die rechte Maustaste." : "Use the right mouse button.");
            if (mods.Count > 0) parts.Add(de ? $"Halten Sie dabei {string.Join("+", mods.Select(m => Key(Cap(m), lang)))} gedrückt." : $"Hold {string.Join("+", mods.Select(Cap))} while dragging.");
            if (holdBefore >= 500_000) parts.Add(de ? $"Halten Sie die Maustaste zuerst etwa {Seconds(holdBefore, lang)} gedrückt, bevor Sie ziehen." : $"Hold the button for about {Seconds(holdBefore, lang)} before dragging.");
            if (holdEnd >= 800_000) parts.Add(de ? $"Warten Sie über dem Ziel etwa {Seconds(holdEnd, lang)}, bevor Sie loslassen." : $"Wait over the target for about {Seconds(holdEnd, lang)} before releasing.");
            return parts.Count == 0 ? "" : " " + string.Join(" ", parts);
        }
        string bname = button switch { "secondary" => de ? "rechte Taste" : "right button", "middle" => de ? "mittlere Taste" : "middle button", "primary" => de ? "linke Taste" : "left button", _ => button };
        parts.Add((mods.Count > 0 ? string.Join("+", mods.Select(m => Key(Cap(m), lang))) + "+" : "") + bname);
        parts.Add((de ? "gedrückt " : "held ") + Seconds(input.Long("durationUs"), lang));
        if (holdBefore >= 300_000) parts.Add((de ? "davon vor der Bewegung " : "before moving ") + Seconds(holdBefore, lang));
        if (holdEnd >= 300_000) parts.Add((de ? "über dem Ziel " : "over target ") + Seconds(holdEnd, lang));
        if (input.Long("distancePx") > 0) parts.Add($"{input.Long("distancePx")} px");
        return " (" + string.Join(", ", parts) + ")";
    }

    static string Meaning(JsonObject input, string lang, bool guide)
    {
        if (guide || input.Str("commonMeaning") is not { } m) return "";
        string? d = lang == "de" ? m switch { "save" => "Speichern", "copy" => "Kopieren", "cut" => "Ausschneiden", "paste" => "Einfügen", "undo" => "Rückgängig", "selectAll" => "Alles markieren", "find" => "Suchen", "print" => "Drucken", "closeWindow" => "Fenster schließen", "new" => "Neu", "open" => "Öffnen", _ => null } : m;
        return d != null ? (lang == "de" ? $" (üblich: {d})" : $" (usually: {d})") : "";
    }

    static string ScrollText(JsonObject step, JsonObject input, string lang, bool guide)
    {
        bool de = lang == "de";
        double n = input.Dbl("notches");
        bool v = input.Str("axis") != "h";
        string dir = v ? (n < 0 ? (de ? "nach unten" : "down") : (de ? "nach oben" : "up")) : (n > 0 ? (de ? "nach rechts" : "right") : (de ? "nach links" : "left"));
        string z = TargetLabel(step, lang, false);
        if (guide) return de ? $"Scrollen Sie in {TargetLabel(step, lang, true)} {dir}." : $"Scroll {dir} in {z}.";
        return de ? $"Scrollen {dir} um {Math.Abs(n):0.#} Rasten in {z}" : $"Scroll {dir} {Math.Abs(n):0.#} notches in {z}";
    }

    static string TypeText(JsonObject step, JsonObject input, string lang, bool guide)
    {
        bool de = lang == "de";
        string z = TargetLabel(step, lang, guide);
        bool noTarget = step.Obj("target").Obj("el") == null;
        string text = input.Str("text") ?? TokensNotation(new JsonObject { ["tokens"] = input["tokens"]?.DeepClone() });
        string? term = input.Obj("terminator").Str("key");
        if (guide)
        {
            string core = noTarget ? (de ? $"Geben Sie {Q(text, lang)} ein" : $"Type {Q(text, lang)}") : (de ? $"Geben Sie in {z} {Q(text, lang)} ein" : $"Type {Q(text, lang)} in {z}");
            if (term != null) core += de ? (term == "Enter" ? " und drücken Sie die Eingabetaste" : $" und drücken Sie die Taste {Key(term, lang)}") : $" and press {term}";
            return core + ".";
        }
        string r = noTarget ? (de ? "Text eingegeben" : "Text entered") : (de ? $"Text in {z} eingegeben" : $"Text entered in {z}");
        if (term != null) r += de ? $" – abgeschlossen mit {Key(term, lang)}" : $" – finished with {term}";
        return r;
    }

    static string MaskedText(JsonObject step, JsonObject input, string lang, bool guide)
    {
        bool de = lang == "de";
        bool priv = input.Str("maskReason") == "privacyMode";
        if (guide) return priv ? (de ? "Geben Sie hier Ihre vertraulichen Daten ein." : "Enter your confidential data here.") : (de ? "Geben Sie Ihr Kennwort ein." : "Enter your password.");
        long? n = input.LongN("count");
        string z = step.Obj("target").Obj("el") != null ? TargetLabel(step, lang, false) : (de ? "Feld" : "field");
        if (priv) return de ? (n != null ? $"Eingabe: {n} Tasten, nicht aufgezeichnet (Privatmodus)" : "Eingabe: nicht aufgezeichnet (Privatmodus)") : (n != null ? $"Input: {n} keys, not recorded (private mode)" : "Input: not recorded (private mode)");
        return de ? (n != null ? $"Eingabe in {z}: {n} Tasten, nicht aufgezeichnet (maskiert)" : $"Eingabe in {z}: nicht aufgezeichnet (maskiert)") : (n != null ? $"Input in {z}: {n} keys, not recorded (masked)" : $"Input in {z}: not recorded (masked)");
    }

    static string KeyText(JsonObject input, string lang, bool guide)
    {
        bool de = lang == "de";
        string k = Key(input.Str("key") ?? "", lang);
        int n = input.Int("count", 1);
        bool endsTaste = k.EndsWith("taste");
        if (guide) return n > 1 ? (de ? $"Drücken Sie {n}-mal {(endsTaste ? "die " + k : "die Taste " + k)}." : $"Press {k} {n} times.") : (de ? (endsTaste ? $"Drücken Sie die {k}." : $"Drücken Sie die Taste {k}.") : $"Press {k}.");
        return n > 1 ? (de ? $"{n}× Taste {k}" : $"{k} pressed {n} times") : (de ? $"Taste {k}" : $"Key {k}");
    }

    static string SystemText(JsonObject step, JsonObject input, string lang, bool guide)
    {
        bool de = lang == "de";
        string f = F(step, lang);
        return input.Str("event") switch
        {
            "windowOpened" => guide ? (de ? $"Das Fenster {f} wird angezeigt." : $"The window {f} appears.") : (de ? $"Fenster {f} erschien" : $"Window {f} appeared"),
            "windowClosed" => guide ? (de ? $"Das Fenster {f} schließt sich." : $"The window {f} closes.") : (de ? $"Fenster {f} wurde geschlossen" : $"Window {f} closed"),
            "foregroundChanged" => guide ? (de ? $"Das Fenster {f} tritt in den Vordergrund." : $"The window {f} comes to the front.") : (de ? $"Fenster {f} kam in den Vordergrund" : $"Window {f} came to the foreground"),
            "titleChanged" => de ? $"Fenstertitel geändert: {Q(input.Str("title") ?? "", lang)}" : $"Window title changed: {Q(input.Str("title") ?? "", lang)}",
            "notification" => guide ? (de ? $"Es erscheint die Mitteilung {Q(input.Str("text") ?? "", lang)}." : $"The notification {Q(input.Str("text") ?? "", lang)} appears.") : (de ? $"Mitteilung: {Q(input.Str("text") ?? "", lang)}" : $"Notification: {Q(input.Str("text") ?? "", lang)}"),
            "valueChanged" => de ? "Inhalt änderte sich ohne beobachtete Eingabe" : "Content changed without observed input",
            "topologyChanged" => de ? "Monitor-Anordnung geändert" : "Monitor layout changed",
            "scopeChanged" => de ? "Aufnahmebereich gewechselt" : "Capture area changed",
            _ => ""
        };
    }

    static string MarkerText(JsonObject input, string lang, bool guide)
    {
        bool de = lang == "de";
        string label = input.Str("label") is { } l ? " – " + l : "";
        return input.Str("markerKind") switch
        {
            "error" => (guide ? (de ? "Hinweis: Hier tritt der Fehler auf." : "Note: The error occurs here.") : (de ? "Marker des Autors: Hier tritt der Fehler auf" : "Author's marker: The error occurs here")) + label,
            "expected" => (guide ? (de ? "Hinweis: So sollte es aussehen." : "Note: This is how it should look.") : (de ? "Marker des Autors: So sollte es aussehen" : "Author's marker: This is how it should look")) + label,
            "important" => (guide ? (de ? "Wichtig: Beachten Sie diese Stelle." : "Important: Note this point.") : (de ? "Marker des Autors: Wichtig" : "Author's marker: Important")) + label,
            _ => guide ? (input.Str("label") ?? "") : "Marker" + label
        };
    }

    static string Informal(string s) => s
        .Replace("Klicken Sie", "Klicke").Replace("Doppelklicken Sie", "Doppelklicke").Replace("Geben Sie", "Gib").Replace("Drücken Sie", "Drücke")
        .Replace("Wählen Sie", "Wähle").Replace("Öffnen Sie", "Öffne").Replace("Wechseln Sie", "Wechsle").Replace("Ziehen Sie", "Ziehe").Replace("Zeigen Sie", "Zeige")
        .Replace("Scrollen Sie", "Scrolle").Replace("Aktivieren Sie", "Aktiviere").Replace("Deaktivieren Sie", "Deaktiviere").Replace("Markieren Sie", "Markiere")
        .Replace("Verschieben Sie", "Verschiebe").Replace("Warten Sie", "Warte").Replace("Halten Sie", "Halte").Replace("Beachten Sie", "Beachte")
        .Replace(" Ihr ", " dein ").Replace(" Ihre ", " deine ").Replace("drücken Sie", "drücke").Replace("klicken Sie", "klicke");

    static string Neutral(string s) => s; // Neutrale Form: Vorlagen bleiben in der Sie-Form, wenn keine eigene Vorlage existiert
}
