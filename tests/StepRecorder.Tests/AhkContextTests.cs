using System.Text.Json.Nodes;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Steps;

namespace StepRecorder.Tests;

/// <summary>
/// Zusatzkontext für AutoHotkey: Pfad-Verallgemeinerung, URL-Bereinigung, Menü-Befehls-IDs, Win32-Listenauswahl,
/// Explorer-Navigation, Office-COM, Eingabefelder, Statusleiste – aus künstlichen Schritten bzw. einer künstlichen Sitzung.
/// (Das Lesen am lebenden Desktop – COM, UIA, Fremdprozess-Nachrichten – lässt sich hier nicht prüfen.)
/// </summary>
public class AhkContextTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-ahkctx-" + Guid.NewGuid().ToString("N")[..8]);
    public AhkContextTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    // ------------------------------------------------------------------ Pfade

    [Theory]
    [InlineData(@"C:\Users\max\Documents\Berichte\a.xlsx", @"A_MyDocuments ""\Berichte\a.xlsx""")]
    [InlineData(@"C:\Users\max\Documents", "A_MyDocuments")]
    [InlineData(@"C:\Users\Max Muster\Desktop\Notiz.txt", @"A_Desktop ""\Notiz.txt""")]
    [InlineData(@"C:\Users\max\AppData\Local\Temp\x.tmp", @"A_Temp ""\x.tmp""")]
    [InlineData(@"C:\Users\max\AppData\Roaming\Tool\cfg.ini", @"A_AppData ""\Tool\cfg.ini""")]
    [InlineData(@"C:\Users\max\AppData\Local\Tool", @"EnvGet(""LOCALAPPDATA"") ""\Tool""")]
    [InlineData(@"C:\Users\max\Downloads\a.zip", @"EnvGet(""USERPROFILE"") ""\Downloads\a.zip""")]
    [InlineData(@"C:\Program Files (x86)\Foo\foo.exe", @"EnvGet(""ProgramFiles(x86)"") ""\Foo\foo.exe""")]
    [InlineData(@"C:\Program Files\Foo\foo.exe", @"A_ProgramFiles ""\Foo\foo.exe""")]
    [InlineData(@"C:\ProgramData\Foo", @"A_AppDataCommon ""\Foo""")]
    [InlineData(@"C:\Windows\System32\notepad.exe", @"A_WinDir ""\System32\notepad.exe""")]
    [InlineData(@"C:\Users\Public\Documents\x.pdf", @"EnvGet(""PUBLIC"") ""\Documents\x.pdf""")]
    [InlineData(@"D:\Daten\a.txt", @"""D:\Daten\a.txt""")]
    [InlineData("notiz.txt", @"""notiz.txt""")]
    [InlineData(@"kopiere C:\Users\max\Desktop\a.txt nach D:\", @"""kopiere "" A_Desktop ""\a.txt nach D:\""")]
    public void Paths_AreGeneralized(string path, string expected) => Assert.Equal(expected, AhkPaths.Expr(path));

    [Fact]
    public void Paths_UserNameElsewhereBecomesA_UserName()
    {
        Assert.Equal(@"""\\server\home\"" A_UserName ""\Berichte""", AhkPaths.Expr(@"\\server\home\max\Berichte", "max"));
        // Benutzername aus dem Profilpfad im selben Text
        Assert.Equal(@"A_Desktop "" und D:\Sicherung\"" A_UserName", AhkPaths.Expr(@"C:\Users\max\Desktop und D:\Sicherung\max"));
        Assert.Equal("max", AhkPaths.UserNameIn(@"Ordner C:\Users\max\Documents\x"));
        Assert.Null(AhkPaths.UserNameIn(@"C:\Users\Public\Documents"));
        Assert.False(AhkPaths.Generalizes(@"D:\Daten"));
        Assert.True(AhkPaths.Generalizes(@"C:\Users\max\Desktop"));
    }

    // ------------------------------------------------------------------ URLs

    [Theory]
    [InlineData("https://example.com/path/x?token=abc#frag", "https://example.com/path/x", false)]
    [InlineData("example.com/a/b?c=1", "https://example.com/a/b", true)]
    [InlineData("https://user:geheim@host.de:8443/p?q=1", "https://host.de:8443/p", false)]
    [InlineData("http://intranet.firma.local/", "http://intranet.firma.local/", false)]
    [InlineData("localhost:3000/app?x=1", "https://localhost:3000/app", true)]
    [InlineData("file:///C:/Users/x/a.html?x=1", "file:///C:/Users/x/a.html", false)]
    [InlineData("about:blank", "about:blank", false)]
    [InlineData("chrome://settings/passwords?search=x", "chrome://settings/passwords", false)]
    public void Url_IsSanitized(string raw, string expected, bool assumed)
    {
        Assert.Equal(expected, AppProbe.SanitizeUrl(raw, out bool a));
        Assert.Equal(assumed, a);
    }

    [Theory]
    [InlineData("wetter morgen")]
    [InlineData("wetter")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("")]
    public void Url_RejectsSearchTermsAndScripts(string raw) => Assert.Null(AppProbe.SanitizeUrl(raw, out _));

    [Fact]
    public void Browser_PrivateWindowsAreRecognized()
    {
        Assert.True(AppProbe.PrivateWindow("Neuer Tab - Google Chrome (Inkognito)", null));
        Assert.True(AppProbe.PrivateWindow("[InPrivate] Bing – Microsoft Edge", null));
        Assert.True(AppProbe.PrivateWindow("Mozilla Firefox — Privater Modus", null));
        Assert.True(AppProbe.PrivateWindow("Seite", "\"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\" --incognito"));
        Assert.True(AppProbe.PrivateWindow("Seite", "firefox.exe -private-window"));
        Assert.False(AppProbe.PrivateWindow("Example Domain - Google Chrome", "\"chrome.exe\" --profile-directory=Default"));
    }

    [Fact]
    public void MenuLabel_StripsAccessKeysAndShortcut()
    {
        Assert.Equal("Speichern unter...", StepBuilder.MenuLabel("Speichern &unter...\tStrg+Umschalt+S"));
        Assert.Equal("Drucken & Vorschau", StepBuilder.MenuLabel("&Drucken && Vorschau"));
        Assert.Equal("", StepBuilder.MenuLabel(null));
    }

    // ------------------------------------------------------------------ AHK-Schnipsel aus künstlichen Schritten

    static JsonObject W32(string cls, string exe, string hwnd = "0x00001000", string title = "Fenster", int x = 40, int y = 60, JsonObject? control = null)
    {
        var o = new JsonObject
        {
            ["src"] = "win32",
            ["window"] = new JsonObject { ["hwnd"] = hwnd, ["title"] = title, ["class"] = cls, ["exe"] = exe },
            ["point"] = new JsonObject { ["window"] = new JsonArray(x, y), ["client"] = new JsonArray(x - 8, y - 30), ["screen"] = new JsonArray(x + 100, y + 100) },
            ["ahk"] = new JsonObject { ["winTitle"] = $"ahk_class {cls} ahk_exe {exe}", ["winTitleFull"] = $"{title} ahk_class {cls} ahk_exe {exe}" }
        };
        if (control != null) o["control"] = control;
        return o;
    }

    static ViewStep Step(string kind, JsonObject json, int n = 1)
    {
        json["kind"] = kind;
        return new ViewStep { Id = "s" + n, Number = n, Kind = kind, Json = json };
    }

    [Fact]
    public void Snippet_MenuCommandUsesMenuSelectAndWmCommand()
    {
        var s = Step("click", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("#32768", "notepad.exe", "0x00003000", "") },
            ["menuPath"] = new JsonObject
            {
                ["items"] = new JsonArray("Datei", "Speichern unter..."), ["win32Menu"] = true, ["commandId"] = 4, ["source"] = "menuBar",
                ["win32Path"] = new JsonArray("Datei", "Speichern unter..."), ["win32Positions"] = new JsonArray(0, 5),
                ["owner"] = new JsonObject { ["hwnd"] = "0x00001000", ["class"] = "Notepad", ["exe"] = "notepad.exe", ["winTitle"] = "ahk_class Notepad ahk_exe notepad.exe" }
            }
        });
        var ahk = AhkExport.Snippet(s);
        Assert.Contains("WinActivate \"ahk_class Notepad ahk_exe notepad.exe\"", ahk);
        Assert.Contains("MenuSelect hwnd,, \"Datei\", \"Speichern unter...\"", ahk);
        Assert.Contains("PostMessage 0x111, 4, 0,, hwnd", ahk);
        Assert.Contains("MenuSelect hwnd,, \"1&\", \"6&\"", ahk);
        Assert.DoesNotContain("#32768", ahk);   // nicht ins Menüfenster klicken
    }

    [Fact]
    public void Snippet_SystemMenuUsesZeroAmpersand()
    {
        var s = Step("click", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("#32768", "notepad.exe", "0x00003000", "") },
            ["menuPath"] = new JsonObject
            {
                ["items"] = new JsonArray("Schließen"), ["win32Menu"] = true, ["commandId"] = 0xF060, ["source"] = "systemMenu",
                ["win32Path"] = new JsonArray("Schließen"), ["win32Positions"] = new JsonArray(6),
                ["owner"] = new JsonObject { ["class"] = "Notepad", ["exe"] = "notepad.exe", ["winTitle"] = "ahk_class Notepad ahk_exe notepad.exe" }
            }
        });
        Assert.Contains("MenuSelect hwnd,, \"0&\", \"Schließen\"", AhkExport.Snippet(s));
    }

    [Fact]
    public void Snippet_ComboBoxUsesControlChooseStringWithClickFallback()
    {
        var s = Step("click", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("#32770", "app.exe", title: "Optionen", control: new JsonObject { ["class"] = "ListBox", ["classNN"] = "ListBox1", ["point"] = new JsonArray(5, 30) }) },
            ["choose"] = new JsonObject
            {
                ["item"] = "Rot", ["win32Control"] = "ListBox1",
                ["win32"] = new JsonObject { ["kind"] = "listBox", ["classNN"] = "ListBox1", ["index"] = 3, ["text"] = "Rot", ["count"] = 5 }
            }
        });
        var ahk = AhkExport.Snippet(s);
        Assert.Contains("try ControlChooseString \"Rot\", \"ListBox1\", hwnd", ahk);
        Assert.Contains("catch {", ahk);
        Assert.Contains("    ControlClick \"ListBox1\", hwnd", ahk);   // Rückfall: aufgezeichneter Klick
        Assert.Contains("ControlChooseIndex 3, \"ListBox1\", hwnd", ahk);
    }

    [Fact]
    public void Snippet_DroppedComboListSelectsInOwnerWindow()
    {
        var s = Step("click", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("ComboLBox", "app.exe", "0x00005000", "") },
            ["choose"] = new JsonObject
            {
                ["item"] = "Grün",
                ["win32"] = new JsonObject { ["kind"] = "comboBox", ["classNN"] = "ComboBox2", ["winTitle"] = "Optionen ahk_class #32770 ahk_exe app.exe", ["index"] = 2, ["text"] = "Grün" }
            }
        });
        var ahk = AhkExport.Snippet(s);
        Assert.Contains("WinActivate \"Optionen ahk_class #32770 ahk_exe app.exe\"", ahk);
        Assert.Contains("ControlChooseString \"Grün\", \"ComboBox2\", hwnd", ahk);
        Assert.DoesNotContain("ComboLBox", ahk);
    }

    [Fact]
    public void Snippet_TabAndListView()
    {
        var tab = Step("click", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("#32770", "app.exe", title: "Eigenschaften") },
            ["choose"] = new JsonObject { ["item"] = "Details", ["win32"] = new JsonObject { ["kind"] = "tab", ["classNN"] = "SysTabControl321", ["index"] = 2, ["text"] = "Details" } }
        });
        Assert.Contains("try ControlChooseIndex 2, \"SysTabControl321\", hwnd", AhkExport.Snippet(tab));
        var lv = Step("doubleClick", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("#32770", "app.exe", title: "Liste") },
            ["choose"] = new JsonObject { ["item"] = "Meier", ["win32"] = new JsonObject { ["kind"] = "listView", ["classNN"] = "SysListView321", ["index"] = 4, ["text"] = "Meier", ["row"] = new JsonArray("Meier", "Berlin") } }
        });
        var ahk = AhkExport.Snippet(lv);
        Assert.Contains("if ListViewWaehle(\"SysListView321\", hwnd, \"Meier\")", ahk);
        Assert.Contains("ControlSend \"{Enter}\", \"SysListView321\", hwnd", ahk);
        Assert.Contains("ListViewWaehle(ctl, hwnd, text) {", ahk);   // Hilfsfunktion im Schnipsel
    }

    [Fact]
    public void Snippet_ExplorerNavigatesToGeneralizedPath()
    {
        var s = Step("doubleClick", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("CabinetWClass", "explorer.exe", title: "Dokumente") },
            ["explorer"] = new JsonObject { ["folder"] = @"C:\Users\max\Documents", ["selected"] = new JsonArray("Projekte"), ["selectedCount"] = 1, ["folderAfter"] = @"C:\Users\max\Documents\Projekte" }
        });
        var ahk = AhkExport.Snippet(s);
        Assert.Contains("ExplorerNavigiere(hwnd, A_MyDocuments \"\\Projekte\")", ahk);
        Assert.Contains("ExplorerNavigiere(hwnd, pfad) {", ahk);
        Assert.DoesNotContain("max", ahk);
    }

    [Fact]
    public void Snippet_TypingIntoKnownEditUsesControlSetTextOrEditPaste()
    {
        JsonObject Edit(JsonObject caret) => new() { ["class"] = "Edit", ["classNN"] = "Edit1", ["textWithheld"] = "editable", ["caret"] = caret };
        var empty = Step("type", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("Notepad", "notepad.exe", control: Edit(new JsonObject { ["line"] = 1, ["col"] = 1, ["selStart"] = 0, ["selLen"] = 0 })) },
            ["input"] = new JsonObject { ["text"] = "Hallo", ["tokens"] = new JsonArray("Hallo"), ["valueBefore"] = "", ["valueAfter"] = "Hallo" }
        });
        var ahk = AhkExport.Snippet(empty);
        Assert.Contains("ControlSetText \"Hallo\", \"Edit1\", hwnd", ahk);
        Assert.Contains("; Alternative: SendText \"Hallo\"", ahk);
        var middle = Step("type", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("Notepad", "notepad.exe", control: Edit(new JsonObject { ["line"] = 2, ["col"] = 4, ["selStart"] = 9, ["selLen"] = 0 })) },
            ["input"] = new JsonObject { ["text"] = "neu", ["tokens"] = new JsonArray("neu"), ["valueBefore"] = "abc\r\ndefgh", ["valueAfter"] = "abc\r\ndefneugh" }
        });
        Assert.Contains("EditPaste \"neu\", \"Edit1\", hwnd", AhkExport.Snippet(middle));
        // ohne Schreibmarke (kein erkanntes Win32-Feld): wie bisher tippen
        var plain = Step("type", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("Notepad", "notepad.exe", control: new JsonObject { ["class"] = "Edit", ["classNN"] = "Edit1" }) },
            ["input"] = new JsonObject { ["text"] = "Hallo", ["tokens"] = new JsonArray("Hallo") }
        });
        Assert.Contains("SendText \"Hallo\"", AhkExport.Snippet(plain));
        Assert.DoesNotContain("ControlSetText", AhkExport.Snippet(plain));
    }

    [Fact]
    public void Snippet_FileDialogSetsFullPath()
    {
        var s = Step("type", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("#32770", "notepad.exe", title: "Speichern unter", control: new JsonObject { ["class"] = "Edit", ["classNN"] = "Edit1" }) },
            ["data"] = new JsonObject { ["fileDialog"] = new JsonObject { ["folder"] = @"C:\Users\max\Desktop", ["isPath"] = true, ["fileNameControl"] = "Edit1" } },
            ["input"] = new JsonObject { ["text"] = "notiz.txt", ["tokens"] = new JsonArray("notiz.txt"), ["terminator"] = new JsonObject { ["key"] = "Enter" } }
        });
        var ahk = AhkExport.Snippet(s);
        Assert.Contains("ControlSetText A_Desktop \"\\\" \"notiz.txt\", \"Edit1\", hwnd", ahk);
        Assert.Contains("Send \"{Enter}\"", ahk);
    }

    [Fact]
    public void Comments_OfficeBrowserExplorer()
    {
        var xl = Step("click", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("XLMAIN", "EXCEL.EXE", title: "Umsatz.xlsx - Excel") },
            ["office"] = new JsonObject { ["app"] = "excel", ["workbook"] = @"C:\Users\max\Documents\Umsatz.xlsx", ["sheet"] = "Tabelle1", ["cell"] = "B3" }
        });
        var c1 = AhkExport.ContextCommentsFor(xl);
        Assert.Contains("ComObjActive(\"Excel.Application\")", c1);
        Assert.Contains("xl.Workbooks.Open(A_MyDocuments \"\\Umsatz.xlsx\")", c1);
        Assert.Contains("xl.Worksheets(\"Tabelle1\").Activate(), xl.Range(\"B3\").Select()", c1);
        var wd = Step("click", new JsonObject { ["office"] = new JsonObject { ["app"] = "word", ["document"] = @"C:\Users\max\Desktop\Brief.docx" } });
        Assert.Contains("wd.Documents.Open(A_Desktop \"\\Brief.docx\")", AhkExport.ContextCommentsFor(wd));
        var br = Step("click", new JsonObject
        {
            ["target"] = new JsonObject { ["win32"] = W32("Chrome_WidgetWin_1", "chrome.exe", title: "Example Domain - Google Chrome") },
            ["browser"] = new JsonObject { ["browser"] = "chrome", ["url"] = "https://example.com/a" }
        });
        var c3 = AhkExport.ContextCommentsFor(br);
        Assert.Contains("; Alternative: Run \"https://example.com/a\"", c3);
        Assert.Contains("WinWait \"Example Domain ahk_exe chrome.exe\",, Wartezeit", c3);
        var ex = Step("click", new JsonObject { ["explorer"] = new JsonObject { ["folder"] = @"C:\Users\max\Desktop", ["selected"] = new JsonArray("a.txt") } });
        var c4 = AhkExport.ContextCommentsFor(ex);
        Assert.Contains("Run 'explorer.exe \"' A_Desktop '\"'", c4);
        Assert.Contains("ausgewählt: a.txt", c4);
    }

    // ------------------------------------------------------------------ Sitzung: von Ereignissen bis zum Skript

    [Fact]
    public void Session_ContextFlowsIntoStepsScriptAndTrace()
    {
        var folder = ContextSession.Create(Path.Combine(_root, "ctx"));
        using var v = SessionView.Open(folder, headless: true);
        var clicks = v.All.Where(x => x.Kind is "click" or "doubleClick").ToList();

        // Menü: Befehls-ID und Pfad aus win32.menu
        var menu = clicks.First(x => x.Json.Obj("menuPath")?.LongN("commandId") != null);
        var mp = menu.Json.Obj("menuPath")!;
        Assert.Equal(4, mp.LongN("commandId"));
        Assert.Equal(new[] { "Datei", "Speichern unter..." }, mp.StrList("win32Path"));
        Assert.Equal("ahk_class Notepad ahk_exe notepad.exe", mp.Obj("owner")?.Str("winTitle"));
        Assert.Contains(menu.Json.Arr("selectors")!, x => x!["kind"]!.GetValue<string>() == "msaa");
        // Statusleiste danach mit Änderung
        var sb = menu.Json.Obj("verify")?.Obj("statusBar");
        Assert.NotNull(sb);
        Assert.Equal("Gespeichert", sb!.Arr("changed")![0]!["to"]!.GetValue<string>());
        // Explorer: Ordner vorher/nachher
        var ex = clicks.First(x => x.Json.Obj("explorer") != null).Json.Obj("explorer")!;
        Assert.Equal(@"C:\Users\max\Documents\Projekte", ex.Str("folderAfter"));
        // Office
        Assert.Contains(clicks, x => x.Json.Obj("office")?.Str("cell") == "B3");
        // Eingabefeld mit Schreibmarke
        var type = v.All.First(x => x.Kind == "type");
        Assert.NotNull(type.Json.Obj("target")?.Obj("win32")?.Obj("control")?.Obj("caret"));

        var r = AhkExport.Export(v, new AhkOptions { Images = false });
        var ahk = File.ReadAllText(r.Path!);        Assert.Contains("MenuSelect hwnd,, \"Datei\", \"Speichern unter...\"", ahk);
        Assert.Contains("PostMessage 0x111, 4, 0,, hwnd", ahk);
        Assert.Contains("Menü öffnen – übernimmt MenuSelect in Schritt", ahk);
        Assert.Contains("StatusBarGetText(1, hwnd), \"Gespeichert\"", ahk);
        Assert.Contains("ExplorerNavigiere(hwnd, A_MyDocuments \"\\Projekte\")", ahk);
        Assert.Contains("ExplorerNavigiere(hwnd, pfad) {", ahk);
        Assert.Contains("ComObjActive(\"Excel.Application\")", ahk);
        Assert.Contains("xl.Workbooks.Open(A_MyDocuments \"\\Umsatz.xlsx\")", ahk);
        Assert.Contains("MsaaStandardAktion(x, y, hwnd) {", ahk);
        Assert.Contains("; Alternative (MSAA, Standardaktion „Auswählen“, Rolle Zelle): MsaaStandardAktion(80, 70, hwnd)", ahk);
        Assert.Contains("ControlSetText ", ahk);
        Assert.Contains(", \"Edit1\", hwnd   ; Feld war leer", ahk);
        // keine benutzerspezifischen Pfade in ausführbaren Zeilen und Alternativen
        var userLines = ahk.Split('\n').Where(l => l.Contains(@"C:\Users\max") && !l.TrimStart().StartsWith("; Explorer-Ordner") && !l.TrimStart().StartsWith("; Excel:") && !l.TrimStart().StartsWith("; Danach")).ToList();
        Assert.True(userLines.Count == 0, string.Join("\n", userLines));

        var ki = KiPackage.Export(v, new KiOptions());
        var trace = File.ReadAllText(Path.Combine(ki.Path, "trace.md"));
        Assert.Contains("Befehls-ID 4", trace);
        Assert.Contains("Explorer-Ordner", trace);
        Assert.Contains("Statusleiste danach", trace);

        var doc = AhkKiPackage.Document(v, new AhkKiOptions { Ocr = false }, null);
        Assert.Contains(doc.Arr("steps")!, x => x!["office"] != null);
        Assert.Contains(doc.Arr("steps")!, x => x!["msaa"] != null);

        var (issues, _) = Validator.Validate(folder, thorough: true);
        Assert.Empty(issues.Where(i => i.Severity == "error"));
    }
}

/// <summary>Künstliche Sitzung mit Menü (Befehls-ID), Statusleiste, Explorer, Excel und Eingabefeld mit Schreibmarke.</summary>
static class ContextSession
{
    static JsonObject Win(string hwnd, string cls, string exe, string title) => new() { ["hwnd"] = hwnd, ["title"] = title, ["class"] = cls, ["pid"] = 10, ["exe"] = exe, ["rect"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["w"] = 200, ["h"] = 100 }, ["state"] = "normal" };
    static JsonObject Ahk(string cls, string exe, string title) => new() { ["winTitle"] = $"ahk_class {cls} ahk_exe {exe}", ["winTitleFull"] = $"{title} ahk_class {cls} ahk_exe {exe}" };

    public static string Create(string folder)
    {
        var manifest = new JsonObject
        {
            ["format"] = "steprec", ["schemaVersion"] = "1.0", ["sessionId"] = Json.NewUuid7(), ["title"] = "Kontext",
            ["state"] = "recording", ["createdUtc"] = "2026-11-11T13:30:05.123456Z", ["timeZone"] = new JsonObject { ["id"] = "W. Europe Standard Time", ["offsetMin"] = 60 },
            ["parts"] = new JsonArray(new JsonObject { ["part"] = 0, ["startUtc"] = "2026-11-11T13:30:05.123456Z", ["tStart"] = 0, ["qpcFrequency"] = 10000000, ["qpcAtStart"] = 1, ["tickCountAtStart"] = 1 }),
            ["recorder"] = new JsonObject { ["name"] = AppInfo.Name, ["version"] = "test", ["arch"] = "x64", ["elevated"] = false, ["captureHost"] = "process" },
            ["environment"] = new JsonObject { ["inputSettings"] = new JsonObject { ["doubleClickTimeMs"] = 500, ["doubleClickWidth"] = 4, ["doubleClickHeight"] = 4, ["dragWidth"] = 4, ["dragHeight"] = 4, ["wheelScrollLines"] = 3, ["hoverTimeMs"] = 400 } },
            ["topology"] = new JsonObject(), ["settings"] = new JsonObject { ["language"] = "de" }, ["history"] = new JsonArray()
        };
        using var w = SessionWriter.Create(folder, manifest);
        long inSeq = 0;
        w.Append(new JsonObject { ["t"] = 0, ["type"] = "session.start", ["part"] = 0, ["schemaVersion"] = "1.0", ["sessionId"] = manifest["sessionId"]!.GetValue<string>(), ["utc"] = "2026-11-11T13:30:05.123456Z" });
        w.Append(new JsonObject { ["t"] = 0, ["type"] = "display.topology", ["epoch"] = 0, ["reason"] = "initial", ["virtual"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["w"] = 200, ["h"] = 100 }, ["monitors"] = new JsonArray(new JsonObject { ["id"] = "M1", ["name"] = "\\\\.\\DISPLAY1", ["primary"] = true, ["vx"] = 0, ["vy"] = 0, ["w"] = 200, ["h"] = 100, ["dpi"] = 96, ["scale"] = 1.0, ["rotation"] = 0, ["backend"] = "gdi" }) });
        w.Append(new JsonObject { ["t"] = 0, ["type"] = "keystate.sync", ["reason"] = "initial", ["down"] = new JsonArray(), ["mouseDown"] = new JsonArray(), ["locks"] = new JsonArray() });
        w.Append(new JsonObject { ["t"] = 0, ["type"] = "scope.change", ["mode"] = "screen", ["monitors"] = "all", ["reason"] = "initial" });
        void WinInfo(string id, string hwnd, string proc, string title, string kind = "window") =>
            w.Append(new JsonObject { ["t"] = 0, ["type"] = "window.info", ["win"] = id, ["hwnd"] = hwnd, ["pid"] = 10, ["process"] = proc, ["title"] = title, ["kind"] = kind, ["ep"] = 0, ["rect"] = new JsonObject { ["m"] = "M1", ["x"] = 0, ["y"] = 0, ["w"] = 200, ["h"] = 100 } });
        WinInfo("w1", "0x00001000", "notepad.exe", "Unbenannt – Editor");
        WinInfo("w2", "0x00003000", "notepad.exe", "", "menu");
        WinInfo("w3", "0x00002000", "explorer.exe", "Dokumente");
        WinInfo("w4", "0x00004000", "EXCEL.EXE", "Umsatz.xlsx - Excel");
        w.Append(new JsonObject { ["t"] = 0, ["type"] = "window.foreground", ["win"] = "w1" });

        void State(long t, string win, JsonObject state) => w.Append(new JsonObject { ["t"] = t, ["type"] = "win32.state", ["win"] = win, ["state"] = state });
        void Click(long t, string win, int x, int y, JsonObject w32, JsonObject el, JsonArray? path = null, int clicks = 1)
        {
            for (int k = 0; k < clicks; k++)
            {
                long td = t + k * 150_000;
                long down = w.Append(new JsonObject { ["t"] = td, ["inSeq"] = ++inSeq, ["type"] = "mouse.down", ["ep"] = 0, ["m"] = "M1", ["x"] = x, ["y"] = y, ["button"] = "primary", ["physButton"] = "left", ["win"] = win, ["fg"] = win });
                if (k == 0)
                {
                    var ev = new JsonObject { ["t"] = td + 10_000, ["tObs"] = td + 30_000, ["type"] = "element", ["trigger"] = "buttonDown", ["ref"] = down, ["status"] = "ok", ["src"] = "uia", ["ms"] = 20, ["ep"] = 0, ["m"] = "M1", ["x"] = x, ["y"] = y, ["win"] = win, ["win32"] = w32, ["el"] = el };
                    if (path != null) ev["path"] = path;
                    w.Append(ev);
                }
                w.Append(new JsonObject { ["t"] = td + 60_000, ["inSeq"] = ++inSeq, ["type"] = "mouse.up", ["ep"] = 0, ["m"] = "M1", ["x"] = x, ["y"] = y, ["button"] = "primary", ["physButton"] = "left", ["win"] = win, ["fg"] = win });
            }
        }
        JsonObject Point(int x, int y) => new() { ["screen"] = new JsonArray(x, y), ["window"] = new JsonArray(x, y), ["client"] = new JsonArray(x - 8, y - 30) };
        JsonObject Rect(int x, int y) => new() { ["m"] = "M1", ["x"] = x - 5, ["y"] = y - 5, ["w"] = 30, ["h"] = 12 };

        // Notepad: Zustand vorher (Statusleiste)
        State(500_000, "w1", new JsonObject { ["hwnd"] = "0x00001000", ["title"] = "Unbenannt – Editor", ["class"] = "Notepad", ["exe"] = "notepad.exe", ["statusBar"] = new JsonObject { ["classNN"] = "msctls_statusbar321", ["parts"] = new JsonArray("Zeile 1, Spalte 1") } });
        // 1) Menüleiste „Datei“
        Click(1_000_000, "w1", 20, 14,
            new JsonObject { ["src"] = "win32", ["window"] = Win("0x00001000", "Notepad", "notepad.exe", "Unbenannt – Editor"), ["point"] = Point(20, 14), ["ahk"] = Ahk("Notepad", "notepad.exe", "Unbenannt – Editor") },
            new JsonObject { ["rid"] = "1.1", ["ct"] = "MenuItem", ["name"] = "Datei", ["fw"] = "Win32", ["rect"] = Rect(20, 14) },
            new JsonArray(new JsonObject { ["ct"] = "Window", ["name"] = "Unbenannt – Editor" }, new JsonObject { ["ct"] = "MenuBar", ["name"] = "Anwendung" }));
        // 2) Popup-Menü „Speichern unter…“ (Befehls-ID 4)
        var menu = new JsonObject
        {
            ["pos"] = 5, ["cmdId"] = 4, ["text"] = "Speichern &unter...\tStrg+Umschalt+S", ["source"] = "menuBar",
            ["path"] = new JsonArray(new JsonObject { ["pos"] = 0, ["text"] = "&Datei" }, new JsonObject { ["pos"] = 5, ["text"] = "Speichern &unter...\tStrg+Umschalt+S" }),
            ["owner"] = new JsonObject { ["hwnd"] = "0x00001000", ["class"] = "Notepad", ["exe"] = "notepad.exe", ["title"] = "Unbenannt – Editor", ["winTitle"] = "ahk_class Notepad ahk_exe notepad.exe" }
        };
        Click(2_000_000, "w2", 40, 60,
            new JsonObject
            {
                ["src"] = "win32", ["window"] = Win("0x00003000", "#32768", "notepad.exe", ""), ["point"] = Point(40, 60), ["ahk"] = Ahk("#32768", "notepad.exe", ""), ["menu"] = menu,
                ["msaa"] = new JsonObject { ["role"] = 12, ["roleText"] = "Menüelement", ["defaultAction"] = "Ausführen", ["childId"] = 6, ["name"] = "Speichern unter..." }
            },
            new JsonObject { ["rid"] = "1.2", ["ct"] = "MenuItem", ["name"] = "Speichern unter...", ["fw"] = "Win32", ["rect"] = Rect(40, 60) },
            new JsonArray(new JsonObject { ["ct"] = "Menu", ["name"] = "Datei" }));
        State(2_800_000, "w1", new JsonObject { ["hwnd"] = "0x00001000", ["title"] = "Unbenannt – Editor", ["class"] = "Notepad", ["exe"] = "notepad.exe", ["statusBar"] = new JsonObject { ["classNN"] = "msctls_statusbar321", ["parts"] = new JsonArray("Gespeichert") } });

        // 3) Explorer: Doppelklick auf „Projekte“
        w.Append(new JsonObject { ["t"] = 3_400_000, ["type"] = "window.foreground", ["win"] = "w3", ["prev"] = "w1" });
        State(3_500_000, "w3", new JsonObject { ["hwnd"] = "0x00002000", ["title"] = "Dokumente", ["class"] = "CabinetWClass", ["exe"] = "explorer.exe", ["explorer"] = new JsonObject { ["folder"] = @"C:\Users\max\Documents" } });
        Click(4_000_000, "w3", 60, 50,
            new JsonObject
            {
                ["src"] = "win32", ["window"] = Win("0x00002000", "CabinetWClass", "explorer.exe", "Dokumente"), ["point"] = Point(60, 50), ["ahk"] = Ahk("CabinetWClass", "explorer.exe", "Dokumente"),
                ["explorer"] = new JsonObject { ["folder"] = @"C:\Users\max\Documents", ["location"] = "Dokumente", ["selected"] = new JsonArray("Projekte"), ["selectedCount"] = 1 }
            },
            new JsonObject { ["rid"] = "2.1", ["ct"] = "ListItem", ["name"] = "Projekte", ["rect"] = Rect(60, 50) }, clicks: 2);
        State(5_000_000, "w3", new JsonObject { ["hwnd"] = "0x00002000", ["title"] = "Projekte", ["class"] = "CabinetWClass", ["exe"] = "explorer.exe", ["explorer"] = new JsonObject { ["folder"] = @"C:\Users\max\Documents\Projekte" } });

        // 4) Excel: Klick in Zelle B3
        w.Append(new JsonObject { ["t"] = 5_900_000, ["type"] = "window.foreground", ["win"] = "w4", ["prev"] = "w3" });
        Click(6_000_000, "w4", 80, 70,
            new JsonObject
            {
                ["src"] = "win32", ["window"] = Win("0x00004000", "XLMAIN", "EXCEL.EXE", "Umsatz.xlsx - Excel"), ["point"] = Point(80, 70), ["ahk"] = Ahk("XLMAIN", "EXCEL.EXE", "Umsatz.xlsx - Excel"),
                ["office"] = new JsonObject { ["app"] = "excel", ["workbook"] = @"C:\Users\max\Documents\Umsatz.xlsx", ["workbookName"] = "Umsatz.xlsx", ["sheet"] = "Tabelle1", ["cell"] = "B3" },
                ["msaa"] = new JsonObject { ["role"] = 29, ["roleText"] = "Zelle", ["defaultAction"] = "Auswählen" }
            },
            new JsonObject { ["rid"] = "4.1", ["ct"] = "DataItem", ["name"] = "B3", ["rect"] = Rect(80, 70) });

        // 5) Editor: Tippen in leeres Edit1 (Schreibmarke bekannt)
        w.Append(new JsonObject { ["t"] = 7_900_000, ["type"] = "window.foreground", ["win"] = "w1", ["prev"] = "w4" });
        long focus = w.Append(new JsonObject { ["t"] = 8_000_000, ["type"] = "focus", ["win"] = "w1", ["ctx"] = "textField", ["ctxSrc"] = "uia", ["el"] = new JsonObject { ["rid"] = "1.9", ["ct"] = "Document", ["name"] = "Textbereich" } });
        w.Append(new JsonObject
        {
            ["t"] = 8_000_000, ["type"] = "element", ["trigger"] = "focus", ["ref"] = focus, ["status"] = "ok", ["src"] = "uia", ["ms"] = 10, ["win"] = "w1",
            ["win32"] = new JsonObject
            {
                ["src"] = "win32", ["window"] = Win("0x00001000", "Notepad", "notepad.exe", "Unbenannt – Editor"),
                ["control"] = new JsonObject { ["hwnd"] = "0x00001010", ["class"] = "Edit", ["classNN"] = "Edit1", ["id"] = 15, ["textWithheld"] = "editable", ["caret"] = new JsonObject { ["line"] = 1, ["col"] = 1, ["selStart"] = 0, ["selLen"] = 0, ["multiline"] = true, ["lines"] = 1 } },
                ["ahk"] = new JsonObject { ["winTitle"] = "ahk_class Notepad ahk_exe notepad.exe", ["control"] = "Edit1" }
            },
            ["el"] = new JsonObject { ["rid"] = "1.9", ["ct"] = "Document", ["name"] = "Textbereich", ["value"] = "" }
        });
        w.Append(new JsonObject { ["t"] = 8_000_100, ["type"] = "ui.value", ["el"] = new JsonObject { ["rid"] = "1.9", ["ct"] = "Document", ["name"] = "Textbereich" }, ["win"] = "w1", ["phase"] = "focusIn", ["value"] = "" });
        long t = 9_000_000;
        foreach (char ch in "Hallo")
        {
            uint vk = char.ToUpperInvariant(ch);
            w.Append(new JsonObject { ["t"] = t, ["inSeq"] = ++inSeq, ["type"] = "key.down", ["vk"] = vk, ["sc"] = 1, ["key"] = char.ToUpperInvariant(ch).ToString(), ["hkl"] = "04070407", ["ch"] = ch.ToString(), ["locks"] = new JsonArray(), ["fg"] = "w1", ["focus"] = focus, ["ctx"] = "textField" });
            w.Append(new JsonObject { ["t"] = t + 40_000, ["inSeq"] = ++inSeq, ["type"] = "key.up", ["vk"] = vk, ["sc"] = 1, ["key"] = char.ToUpperInvariant(ch).ToString(), ["fg"] = "w1" });
            t += 100_000;
        }
        w.Append(new JsonObject { ["t"] = t + 300_000, ["type"] = "ui.value", ["el"] = new JsonObject { ["rid"] = "1.9", ["ct"] = "Document", ["name"] = "Textbereich" }, ["win"] = "w1", ["phase"] = "poll", ["value"] = "Hallo" });
        long end = t + 3_000_000;
        w.Append(new JsonObject { ["t"] = end, ["type"] = "session.stop", ["reason"] = "user", ["utc"] = "2026-11-11T13:30:20.000000Z" });
        w.Close(end, m => { m["state"] = "complete"; m["durationUs"] = end; m["endedUtc"] = "2026-11-11T13:30:20.000000Z"; });
        return folder;
    }
}
