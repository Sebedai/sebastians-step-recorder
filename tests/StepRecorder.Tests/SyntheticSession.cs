using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Tests;

/// <summary>Erzeugt eine künstliche Sitzung („Datei im Editor speichern“) für Regressionstests.</summary>
public static class SyntheticSession
{
    /// <summary>Win32-Kontext eines Klicks in die Menüleiste des Editors (wie Win32Detail.AtPoint).</summary>
    static JsonObject W32Click() => new()
    {
        ["src"] = "win32",
        ["window"] = new JsonObject { ["hwnd"] = "0x00001000", ["title"] = "Unbenannt – Editor", ["class"] = "Notepad", ["pid"] = 10, ["exe"] = "notepad.exe", ["rect"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["w"] = 800, ["h"] = 600 }, ["state"] = "normal" },
        ["point"] = new JsonObject { ["screen"] = new JsonArray(20, 14), ["window"] = new JsonArray(20, 14), ["client"] = new JsonArray(12, -16) },
        ["pixel"] = "0xF0F0F0",
        ["texts"] = new JsonArray(new JsonObject { ["c"] = "Static1", ["t"] = "Bereit" }),
        ["ahk"] = new JsonObject { ["winTitle"] = "ahk_class Notepad ahk_exe notepad.exe", ["winTitleFull"] = "Unbenannt – Editor ahk_class Notepad ahk_exe notepad.exe" }
    };

    /// <summary>Win32-Kontext des Fokus im Dialog „Speichern unter“.</summary>
    static JsonObject W32Focus() => new()
    {
        ["src"] = "win32",
        ["window"] = new JsonObject { ["hwnd"] = "0x00002000", ["title"] = "Speichern unter", ["class"] = "#32770", ["pid"] = 10, ["exe"] = "notepad.exe" },
        ["control"] = new JsonObject { ["hwnd"] = "0x00002010", ["class"] = "Edit", ["classNN"] = "Edit1", ["id"] = 1001, ["textWithheld"] = "editable" },
        ["ahk"] = new JsonObject { ["winTitle"] = "ahk_class #32770 ahk_exe notepad.exe", ["winTitleFull"] = "Speichern unter ahk_class #32770 ahk_exe notepad.exe", ["control"] = "Edit1" }
    };

    /// <summary>Kanarienwert im aufgenommenen Kommentar (withComment).</summary>
    public const string CommentCanary = "Kanarie-Kommentar-7Q2";
    /// <summary>Im Terminal getippter Befehl (withTerminal).</summary>
    public const string TerminalCanary = "echo kanarie42";
    /// <summary>Freitext eines Eintrags der Ereignisanzeige (withEventText, Paket 12).</summary>
    public const string EventLogCanary = "System.NullReferenceException: Kanarie-Stack-5T1";
    /// <summary>Erkannte Abfragezeile im Terminal (withTerminalPrompt, 10 §6).</summary>
    public const string PromptLine = "[sudo] password for admin:";

    public static string Create(string? folder = null, bool withPassword = false, bool withSecret = false, bool withHotkey = false, bool withComment = false, bool withTerminal = false, bool withUac = false, bool withError = false, long enterSettleUs = 400_000, bool withCrash = false, bool withEventText = false, bool withSnapshot = false, bool withTerminalPrompt = false)
    {
        folder ??= Path.Combine(Path.GetTempPath(), "steprec-test-" + Guid.NewGuid().ToString("N")[..8]);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        var manifest = new JsonObject
        {
            ["format"] = "steprec", ["schemaVersion"] = "1.0", ["sessionId"] = Json.NewUuid7(), ["title"] = "Datei im Editor speichern",
            ["state"] = "recording", ["createdUtc"] = "2026-11-11T13:30:05.123456Z", ["timeZone"] = new JsonObject { ["id"] = "W. Europe Standard Time", ["offsetMin"] = 60 },
            ["parts"] = new JsonArray(new JsonObject { ["part"] = 0, ["startUtc"] = "2026-11-11T13:30:05.123456Z", ["tStart"] = 0, ["qpcFrequency"] = 10000000, ["qpcAtStart"] = 1, ["tickCountAtStart"] = 1 }),
            ["recorder"] = new JsonObject { ["name"] = AppInfo.Name, ["version"] = "test", ["arch"] = "x64", ["elevated"] = false, ["captureHost"] = "process" },
            ["environment"] = new JsonObject { ["inputSettings"] = new JsonObject { ["doubleClickTimeMs"] = 500, ["doubleClickWidth"] = 4, ["doubleClickHeight"] = 4, ["dragWidth"] = 4, ["dragHeight"] = 4, ["wheelScrollLines"] = 3, ["hoverTimeMs"] = 400 } },
            ["topology"] = new JsonObject(), ["settings"] = new JsonObject { ["language"] = "de" },
            ["history"] = new JsonArray()
        };
        using var w = SessionWriter.Create(folder, manifest);
        long inSeq = 0;
        void E(JsonObject o) => w.Append(o);
        E(new JsonObject { ["t"] = 0, ["type"] = "session.start", ["part"] = 0, ["schemaVersion"] = "1.0", ["sessionId"] = manifest["sessionId"]!.GetValue<string>(), ["utc"] = "2026-11-11T13:30:05.123456Z" });
        E(new JsonObject { ["t"] = 0, ["type"] = "system", ["kind"] = "environment" });
        E(new JsonObject { ["t"] = 0, ["type"] = "display.topology", ["epoch"] = 0, ["reason"] = "initial", ["virtual"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["w"] = 200, ["h"] = 100 }, ["monitors"] = new JsonArray(new JsonObject { ["id"] = "M1", ["name"] = "\\\\.\\DISPLAY1", ["primary"] = true, ["vx"] = 0, ["vy"] = 0, ["w"] = 200, ["h"] = 100, ["dpi"] = 96, ["scale"] = 1.0, ["rotation"] = 0, ["backend"] = "gdi" }) });
        E(new JsonObject { ["t"] = 0, ["type"] = "keystate.sync", ["reason"] = "initial", ["down"] = new JsonArray(), ["mouseDown"] = new JsonArray(), ["locks"] = new JsonArray() });
        E(new JsonObject { ["t"] = 0, ["type"] = "scope.change", ["mode"] = "screen", ["monitors"] = "all", ["reason"] = "initial" });
        E(new JsonObject { ["t"] = 0, ["type"] = "window.info", ["win"] = "w1", ["hwnd"] = "0x00001000", ["pid"] = 10, ["process"] = "notepad.exe", ["title"] = "Unbenannt – Editor", ["kind"] = "window", ["ep"] = 0, ["rect"] = new JsonObject { ["m"] = "M1", ["x"] = 0, ["y"] = 0, ["w"] = 200, ["h"] = 100 } });
        E(new JsonObject { ["t"] = 0, ["type"] = "window.foreground", ["win"] = "w1" });
        string Frame(long t, string id, string trigger, long? refSeq, Color c, string? baseId = null)
        {
            using var bmp = new Bitmap(200, 100, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.White); g.FillRectangle(new SolidBrush(c), 50, 30, 60, 20); }
            using var ms = new MemoryStream(); bmp.Save(ms, ImageFormat.Png);
            var blob = w.WriteBlob(ms.ToArray());
            var f = new JsonObject { ["t"] = t, ["type"] = "frame", ["id"] = id, ["ep"] = 0, ["m"] = "M1", ["w"] = 200, ["h"] = 100, ["trigger"] = trigger, ["store"] = new JsonObject { ["kind"] = "key", ["blob"] = blob, ["bytes"] = ms.Length } };
            if (refSeq != null) f["ref"] = refSeq;
            if (trigger == "preInput") f["guarantee"] = "beforeInput";
            if (trigger == "settle") f["settle"] = new JsonObject { ["reason"] = "quiet", ["quietMs"] = 300 };
            E(f);
            return id;
        }
        Frame(0, "f000001", "sessionStart", null, Color.Gray);
        // Mausweg + Klick auf „Datei“
        long mv = w.Append(new JsonObject { ["t"] = 1_000_000, ["inSeq"] = ++inSeq, ["type"] = "mouse.move", ["ep"] = 0, ["m"] = "M1", ["x"] = 10, ["y"] = 10, ["n"] = 3, ["d"] = new JsonArray(new JsonArray(10000, 5, 2), new JsonArray(10000, 5, 2)), ["tEnd"] = 1_020_000, ["xEnd"] = 20, ["yEnd"] = 14 });
        inSeq += 2;
        long dwell = w.Append(new JsonObject { ["t"] = 1_100_000, ["tObs"] = 1_130_000, ["type"] = "element", ["trigger"] = "dwell", ["ref"] = mv, ["status"] = "ok", ["src"] = "uia", ["ms"] = 30, ["ep"] = 0, ["m"] = "M1", ["x"] = 20, ["y"] = 14, ["win"] = "w1", ["el"] = new JsonObject { ["rid"] = "42.1", ["ct"] = "MenuItem", ["name"] = "Datei", ["rect"] = new JsonObject { ["m"] = "M1", ["x"] = 10, ["y"] = 8, ["w"] = 30, ["h"] = 12 } } });
        long down = w.LastSeq + 1;
        E(new JsonObject { ["t"] = 1_500_000, ["inSeq"] = ++inSeq, ["type"] = "mouse.down", ["ep"] = 0, ["m"] = "M1", ["x"] = 20, ["y"] = 14, ["button"] = "primary", ["physButton"] = "left", ["win"] = "w1", ["fg"] = "w1" });
        Frame(1_450_000, "f000002", "preInput", down, Color.Gray);
        E(new JsonObject { ["t"] = 1_510_000, ["tObs"] = 1_540_000, ["type"] = "element", ["trigger"] = "buttonDown", ["ref"] = down, ["status"] = "ok", ["src"] = "uia", ["ms"] = 30, ["ep"] = 0, ["m"] = "M1", ["x"] = 20, ["y"] = 14, ["win"] = "w1", ["win32"] = W32Click(), ["dwellRef"] = dwell, ["dwellValid"] = true, ["el"] = new JsonObject { ["rid"] = "42.1", ["ct"] = "MenuItem", ["name"] = "Datei", ["rect"] = new JsonObject { ["m"] = "M1", ["x"] = 10, ["y"] = 8, ["w"] = 30, ["h"] = 12 } } });
        long up = w.LastSeq + 1;
        E(new JsonObject { ["t"] = 1_580_000, ["inSeq"] = ++inSeq, ["type"] = "mouse.up", ["ep"] = 0, ["m"] = "M1", ["x"] = 20, ["y"] = 14, ["button"] = "primary", ["physButton"] = "left", ["win"] = "w1", ["fg"] = "w1" });
        E(new JsonObject { ["t"] = 1_600_000, ["type"] = "window.info", ["win"] = "w2", ["hwnd"] = "0x00002000", ["pid"] = 10, ["process"] = "notepad.exe", ["title"] = "Speichern unter", ["kind"] = "dialog", ["owner"] = "w1" });
        E(new JsonObject { ["t"] = 1_600_000, ["type"] = "window.open", ["win"] = "w2", ["kind"] = "dialog", ["first"] = true });
        if (withSnapshot)
            E(new JsonObject { ["t"] = 1_600_000, ["tObs"] = 1_950_000, ["type"] = "ui.snapshot", ["trigger"] = "dialogStart", ["win"] = "w2", ["title"] = "Speichern unter", ["ms"] = 40,
                ["nodes"] = new JsonArray(new JsonObject { ["ct"] = "Text", ["name"] = "Dateiname:" }, new JsonObject { ["ct"] = "Button", ["name"] = "Speichern" }, new JsonObject { ["ct"] = "Button", ["name"] = "Abbrechen" }) });
        Frame(1_900_000, "f000003", "settle", up, Color.Red);
        // Fokus im Dialog, Tippen „notiz.txt“ + Enter
        long focus = w.Append(new JsonObject { ["t"] = 2_000_000, ["type"] = "focus", ["win"] = "w2", ["ctx"] = "textField", ["ctxSrc"] = "uia", ["el"] = new JsonObject { ["rid"] = "42.2", ["ct"] = "Edit", ["name"] = "Dateiname:" } });
        w.Append(new JsonObject { ["t"] = 2_000_000, ["type"] = "element", ["trigger"] = "focus", ["ref"] = focus, ["win32"] = W32Focus(), ["status"] = "ok", ["src"] = "uia", ["ms"] = 10, ["win"] = "w2", ["el"] = new JsonObject { ["rid"] = "42.2", ["ct"] = "Edit", ["name"] = "Dateiname:", ["value"] = "" } });
        w.Append(new JsonObject { ["t"] = 2_000_100, ["type"] = "ui.value", ["el"] = new JsonObject { ["rid"] = "42.2", ["ct"] = "Edit", ["name"] = "Dateiname:" }, ["win"] = "w2", ["phase"] = "focusIn", ["value"] = "" });
        long t = 3_000_000;
        string text = withSecret ? "sk-abcdefghijklmnopqrstuvwxyz" : "notiz.txt";
        long firstKey = 0;
        foreach (char c in text)
        {
            uint vk = char.IsLetter(c) ? char.ToUpperInvariant(c) : (uint)0xBE;
            long s = w.Append(new JsonObject { ["t"] = t, ["inSeq"] = ++inSeq, ["type"] = "key.down", ["vk"] = vk, ["sc"] = 1, ["key"] = char.IsLetter(c) ? char.ToUpperInvariant(c).ToString() : "OemPeriod", ["hkl"] = "04070407", ["ch"] = c.ToString(), ["locks"] = new JsonArray(), ["fg"] = "w2", ["focus"] = focus, ["ctx"] = "textField" });
            if (firstKey == 0) { firstKey = s; Frame(t - 50_000, "f000004", "preInput", s, Color.Red); }
            w.Append(new JsonObject { ["t"] = t + 40_000, ["inSeq"] = ++inSeq, ["type"] = "key.up", ["vk"] = vk, ["sc"] = 1, ["key"] = char.IsLetter(c) ? char.ToUpperInvariant(c).ToString() : "OemPeriod", ["fg"] = "w2" });
            t += 100_000;
        }
        w.Append(new JsonObject { ["t"] = t + 300_000, ["type"] = "ui.value", ["el"] = new JsonObject { ["rid"] = "42.2", ["ct"] = "Edit", ["name"] = "Dateiname:" }, ["win"] = "w2", ["phase"] = "poll", ["value"] = text });
        t += 500_000;
        long enter = w.Append(new JsonObject { ["t"] = t, ["inSeq"] = ++inSeq, ["type"] = "key.down", ["vk"] = 13, ["sc"] = 28, ["key"] = "Enter", ["hkl"] = "04070407", ["locks"] = new JsonArray(), ["fg"] = "w2", ["focus"] = focus, ["ctx"] = "textField" });
        w.Append(new JsonObject { ["t"] = t + 50_000, ["inSeq"] = ++inSeq, ["type"] = "key.up", ["vk"] = 13, ["sc"] = 28, ["key"] = "Enter", ["fg"] = "w2" });
        w.Append(new JsonObject { ["t"] = t + 100_000, ["type"] = "window.close", ["win"] = "w2", ["destroyed"] = true });
        w.Append(new JsonObject { ["t"] = t + 120_000, ["type"] = "window.title", ["win"] = "w1", ["title"] = "notiz.txt – Editor" });
        Frame(t + enterSettleUs, "f000005", "settle", enter, Color.Green);
        if (enterSettleUs > 400_000) t += enterSettleUs;
        if (withError)
        {
            w.Append(new JsonObject { ["t"] = t + 600_000, ["type"] = "marker", ["id"] = "mk0", ["kind"] = "error", ["label"] = "Fehlermeldung erscheint", ["frame"] = "f000005" });
            if (withSnapshot)
                w.Append(new JsonObject { ["t"] = t + 600_000, ["tObs"] = t + 700_000, ["type"] = "ui.snapshot", ["trigger"] = "manual", ["win"] = "w1", ["title"] = "Fehler", ["ms"] = 30,
                    ["nodes"] = new JsonArray(new JsonObject { ["ct"] = "Text", ["name"] = "Zugriff verweigert: notiz.txt ist schreibgeschützt (Fehler 0x80070005)" }, new JsonObject { ["ct"] = "Button", ["name"] = "OK" }) });
        }
        if (withPassword)
        {
            t += 1_000_000;
            w.Append(new JsonObject { ["t"] = t, ["tEnd"] = t + 800_000, ["type"] = "text.masked", ["count"] = 8, ["inSeq"] = ++inSeq, ["inSeqEnd"] = inSeq + 15, ["events"] = 16, ["reason"] = "passwordField", ["fg"] = "w1" });
            inSeq += 15;
        }
        if (withHotkey)
        {
            // Strg+1 im Editor
            t += 1_000_000;
            w.Append(new JsonObject { ["t"] = t, ["inSeq"] = ++inSeq, ["type"] = "key.down", ["vk"] = 0xA2, ["sc"] = 29, ["key"] = "LeftCtrl", ["hkl"] = "04070407", ["locks"] = new JsonArray(), ["fg"] = "w1", ["ctx"] = "textField" });
            w.Append(new JsonObject { ["t"] = t + 100_000, ["inSeq"] = ++inSeq, ["type"] = "key.down", ["vk"] = 0x31, ["sc"] = 2, ["key"] = "1", ["hkl"] = "04070407", ["mods"] = new JsonArray("ctrl"), ["locks"] = new JsonArray(), ["fg"] = "w1", ["ctx"] = "textField" });
            w.Append(new JsonObject { ["t"] = t + 150_000, ["inSeq"] = ++inSeq, ["type"] = "key.up", ["vk"] = 0x31, ["sc"] = 2, ["key"] = "1", ["fg"] = "w1" });
            w.Append(new JsonObject { ["t"] = t + 200_000, ["inSeq"] = ++inSeq, ["type"] = "key.up", ["vk"] = 0xA2, ["sc"] = 29, ["key"] = "LeftCtrl", ["fg"] = "w1" });
        }
        if (withComment)
        {
            t += 1_000_000;
            w.Append(new JsonObject { ["t"] = t, ["tObs"] = t + 10_000, ["type"] = "comment", ["id"] = "c1", ["text"] = CommentCanary + " – nur intern" });
        }
        if (withTerminal)
        {
            // Befehl in Windows Terminal (kein Passwortfeld erkennbar → Klartext, Durchsicht nötig)
            t += 1_000_000;
            w.Append(new JsonObject { ["t"] = t, ["type"] = "window.info", ["win"] = "w3", ["hwnd"] = "0x00003000", ["pid"] = 30, ["process"] = "WindowsTerminal.exe", ["class"] = "CASCADIA_HOSTING_WINDOW_CLASS", ["title"] = "PowerShell", ["kind"] = "window", ["surface"] = "partial", ["surfaceKind"] = "terminal" });
            w.Append(new JsonObject { ["t"] = t, ["type"] = "window.foreground", ["win"] = "w3" });
            long tf = w.Append(new JsonObject { ["t"] = t + 10_000, ["type"] = "focus", ["win"] = "w3", ["ctx"] = "nonText", ["ctxSrc"] = "uia", ["el"] = new JsonObject { ["rid"] = "77.1", ["ct"] = "Text", ["name"] = "PowerShell" } });
            t += 100_000;
            foreach (char c in TerminalCanary)
            {
                uint vk = char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : (uint)0x20;
                string kname = char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c).ToString() : "Space";
                w.Append(new JsonObject { ["t"] = t, ["inSeq"] = ++inSeq, ["type"] = "key.down", ["vk"] = vk, ["sc"] = 1, ["key"] = kname, ["hkl"] = "04070407", ["ch"] = c.ToString(), ["locks"] = new JsonArray(), ["fg"] = "w3", ["focus"] = tf, ["ctx"] = "nonText" });
                w.Append(new JsonObject { ["t"] = t + 30_000, ["inSeq"] = ++inSeq, ["type"] = "key.up", ["vk"] = vk, ["sc"] = 1, ["key"] = kname, ["fg"] = "w3" });
                t += 80_000;
            }
            w.Append(new JsonObject { ["t"] = t, ["inSeq"] = ++inSeq, ["type"] = "key.down", ["vk"] = 13, ["sc"] = 28, ["key"] = "Enter", ["hkl"] = "04070407", ["locks"] = new JsonArray(), ["fg"] = "w3", ["focus"] = tf, ["ctx"] = "nonText" });
            w.Append(new JsonObject { ["t"] = t + 30_000, ["inSeq"] = ++inSeq, ["type"] = "key.up", ["vk"] = 13, ["sc"] = 28, ["key"] = "Enter", ["fg"] = "w3" });
        }
        if (withTerminalPrompt)
        {
            // Passwortabfrage in Windows Terminal, erkannt nach Regel T1: Privatmodus „terminalPrompt“, maskierter Lauf, Ende mit der Eingabetaste
            t += 1_000_000;
            w.Append(new JsonObject { ["t"] = t, ["type"] = "window.info", ["win"] = "w4", ["hwnd"] = "0x00004000", ["pid"] = 40, ["process"] = "WindowsTerminal.exe", ["class"] = "CASCADIA_HOSTING_WINDOW_CLASS", ["title"] = "Ubuntu", ["kind"] = "window", ["surface"] = "partial", ["surfaceKind"] = "terminal" });
            w.Append(new JsonObject { ["t"] = t, ["type"] = "window.foreground", ["win"] = "w4" });
            long tf = w.Append(new JsonObject { ["t"] = t + 10_000, ["type"] = "focus", ["win"] = "w4", ["ctx"] = "nonText", ["ctxSrc"] = "uia", ["el"] = new JsonObject { ["rid"] = "88.1", ["ct"] = "Text", ["name"] = "Ubuntu" } });
            t += 300_000;
            w.Append(new JsonObject { ["t"] = t, ["type"] = "gap", ["gapId"] = "gtp", ["phase"] = "begin", ["cause"] = "privacyMode", ["tFrom"] = t, ["channels"] = new JsonArray("keyboard", "clipboard", "fieldValues"), ["inputLost"] = "none" });
            w.Append(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "privacyMode", ["active"] = true, ["trigger"] = "terminalPrompt", ["rule"] = "T1", ["gapId"] = "gtp" });
            long tStart = t + 200_000;
            w.Append(new JsonObject { ["t"] = tStart, ["tEnd"] = tStart + 900_000, ["type"] = "text.masked", ["count"] = 9, ["inSeq"] = ++inSeq, ["inSeqEnd"] = inSeq + 17, ["events"] = 18, ["reason"] = "terminalPrompt", ["rule"] = "T1", ["promptLine"] = PromptLine, ["fg"] = "w4", ["focus"] = tf });
            inSeq += 17;
            t = tStart + 1_000_000;
            w.Append(new JsonObject { ["t"] = t, ["type"] = "gap", ["gapId"] = "gtp", ["phase"] = "end", ["cause"] = "privacyMode", ["tFrom"] = tStart - 200_000, ["tTo"] = t, ["channels"] = new JsonArray("keyboard", "clipboard", "fieldValues"), ["inputLost"] = "none", ["detail"] = new JsonObject { ["trigger"] = "terminalPrompt", ["rule"] = "T1", ["end"] = "enter" } });
            w.Append(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "privacyMode", ["active"] = false, ["trigger"] = "terminalPrompt", ["rule"] = "T1", ["end"] = "enter", ["gapId"] = "gtp" });
            w.Append(new JsonObject { ["t"] = t, ["inSeq"] = ++inSeq, ["type"] = "key.down", ["vk"] = 13, ["sc"] = 28, ["key"] = "Enter", ["hkl"] = "04070407", ["locks"] = new JsonArray(), ["fg"] = "w4", ["focus"] = tf, ["ctx"] = "nonText" });
            w.Append(new JsonObject { ["t"] = t + 30_000, ["inSeq"] = ++inSeq, ["type"] = "key.up", ["vk"] = 13, ["sc"] = 28, ["key"] = "Enter", ["fg"] = "w4" });
        }
        if (withUac)
        {
            // Abfrage der Benutzerkontensteuerung: sicherer Desktop, danach Tastenstatus und Schlüsselbild
            t += 1_000_000;
            w.Append(new JsonObject { ["t"] = t, ["type"] = "system", ["kind"] = "desktopSwitch", ["to"] = "secure", ["desktop"] = "inaccessible" });
            w.Append(new JsonObject { ["t"] = t, ["type"] = "gap", ["gapId"] = "gu", ["phase"] = "begin", ["cause"] = "secureDesktop", ["tFrom"] = t, ["channels"] = new JsonArray("all"), ["inputLost"] = "possible" });
            w.Append(new JsonObject { ["t"] = t + 3_000_000, ["type"] = "gap", ["gapId"] = "gu", ["phase"] = "end", ["cause"] = "secureDesktop", ["tFrom"] = t, ["tTo"] = t + 3_000_000, ["channels"] = new JsonArray("all"), ["inputLost"] = "possible", ["detail"] = new JsonObject { ["desktop"] = "inaccessible", ["kind"] = "uac" } });
            w.Append(new JsonObject { ["t"] = t + 3_000_000, ["type"] = "system", ["kind"] = "desktopSwitch", ["to"] = "default" });
            w.Append(new JsonObject { ["t"] = t + 3_000_000, ["type"] = "keystate.sync", ["reason"] = "secureDesktop", ["gapId"] = "gu", ["down"] = new JsonArray(), ["mouseDown"] = new JsonArray(), ["locks"] = new JsonArray() });
            Frame(t + 3_050_000, "f000099", "resume", null, Color.Gray);
            t += 3_000_000;
        }
        if (withCrash)
        {
            // Programm-Kontext (Paket 10): Start mit Build-Kennung, Hänger, Absturz, Fakten der Ereignisanzeige
            w.Append(new JsonObject { ["t"] = 100_000, ["type"] = "process.info", ["proc"] = "p1", ["pid"] = 10, ["process"] = "notepad.exe", ["win"] = "w1", ["startedBeforeSession"] = false, ["product"] = "Editor", ["version"] = "11.2410.21.0",
                ["build"] = new JsonObject { ["peTimestampUtc"] = "2026-10-01T08:00:00Z", ["pdbId"] = "0123456789ABCDEF0123456789ABCDEF1", ["pdbFile"] = "notepad.pdb" },
                ["git"] = new JsonObject { ["basis"] = "workingTree", ["dirty"] = "notChecked", ["branch"] = "feature/save", ["commit"] = "0123456789abcdef0123456789abcdef01234567" } });
            t += 1_000_000;
            w.Append(new JsonObject { ["t"] = t, ["type"] = "window.hang", ["phase"] = "start", ["win"] = "w1", ["proc"] = "p1", ["process"] = "notepad.exe" });
            w.Append(new JsonObject { ["t"] = t + 6_000_000, ["type"] = "window.hang", ["phase"] = "end", ["win"] = "w1", ["proc"] = "p1", ["process"] = "notepad.exe", ["durationMs"] = 6000 });
            t += 7_000_000;
            w.Append(new JsonObject { ["t"] = t, ["type"] = "process.exit", ["proc"] = "p1", ["pid"] = 10, ["process"] = "notepad.exe", ["win"] = "w1", ["exitCode"] = 0xC0000005L, ["exitCodeHex"] = "0xC0000005", ["class"] = "crash" });
            w.Append(new JsonObject { ["t"] = t + 500_000, ["type"] = "eventlog.entry", ["proc"] = "p1", ["process"] = "notepad.exe", ["win"] = "w1", ["eventId"] = 1000, ["recordId"] = 4711, ["provider"] = "Application Error",
                ["facts"] = new JsonObject { ["app"] = "notepad.exe", ["appVersion"] = "11.2410.21.0", ["module"] = "ntdll.dll", ["moduleVersion"] = "10.0.26100.1", ["exceptionCode"] = "0xC0000005", ["offset"] = "0x00000000000a1b2c" },
                ["text"] = withEventText ? EventLogCanary + "\n   at Notepad.Save()" : null }.Clean());
            t += 1_000_000;
        }
        // Lücke (Pause)
        t += 1_000_000;
        w.Append(new JsonObject { ["t"] = t, ["type"] = "gap", ["gapId"] = "g1", ["phase"] = "begin", ["cause"] = "paused", ["tFrom"] = t, ["channels"] = new JsonArray("all"), ["inputLost"] = "possible" });
        w.Append(new JsonObject { ["t"] = t, ["type"] = "session.pause", ["reason"] = "user", ["gapId"] = "g1" });
        w.Append(new JsonObject { ["t"] = t + 2_000_000, ["type"] = "gap", ["gapId"] = "g1", ["phase"] = "end", ["cause"] = "paused", ["tFrom"] = t, ["tTo"] = t + 2_000_000, ["channels"] = new JsonArray("all"), ["inputLost"] = "possible" });
        w.Append(new JsonObject { ["t"] = t + 2_000_000, ["type"] = "session.resume", ["gapId"] = "g1" });
        long end = t + 5_000_000;
        w.Append(new JsonObject { ["t"] = end - 1_000_000, ["type"] = "marker", ["id"] = "mk1", ["kind"] = "expected", ["label"] = "So soll es sein", ["frame"] = "f000005" });
        w.Append(new JsonObject { ["t"] = end, ["type"] = "session.stop", ["reason"] = "user", ["utc"] = "2026-11-11T13:30:20.000000Z" });
        w.Close(end, m => { m["state"] = "complete"; m["durationUs"] = end; m["endedUtc"] = "2026-11-11T13:30:20.000000Z"; });
        return folder;
    }
}
