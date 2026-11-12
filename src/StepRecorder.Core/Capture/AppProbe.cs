using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using StepRecorder.Data;
using StepRecorder.Native;

namespace StepRecorder.Capture;

/// <summary>
/// Anwendungskontext für AutoHotkey-Skripte: Explorer-Ordner und Auswahl (Shell.Application), Adresse des aktiven
/// Browser-Tabs (UIA, ohne Abfrage und Fragment), Excel/Word über COM (Mappe, Blatt, aktive Zelle bzw. Dokument – nie
/// Zellinhalte) und die MSAA-Standardaktion des Elements am Klickpunkt.
/// Läuft auf dem UIA-Arbeiter; COM-Aufrufe in Fremdprozesse zusätzlich auf einem STA-Hilfsthread mit Zeitlimit.
/// Datenschutz: nichts im Privatmodus (Stufe None); Explorer, Browser und Office nur bei voller Stufe; keine Adresse
/// bei Passwortfeldern oder privaten Browserfenstern. Ausgeschlossene Anwendungen erreichen diese Stelle nicht.
/// </summary>
public static class AppProbe
{
    const int ComTimeoutMs = 350, MaxSel = 50, MaxText = 300;

    /// <summary>Ergänzt den Win32-Kontext eines Klicks (bzw. Fokus) um Explorer, Browser, Office und MSAA.</summary>
    public static void Enrich(JsonObject? w32, Win32Detail.TextLevel level, bool password, int? vx = null, int? vy = null)
    {
        if (w32 == null || level == Win32Detail.TextLevel.None) return;
        try
        {
            var win = w32.Obj("window");
            nint top = Hwnd(win?.Str("hwnd"));
            if (top == 0) return;
            if (vx is int x && vy is int y && Msaa(x, y, level, password) is { } msaa) w32["msaa"] = msaa;
            if (level != Win32Detail.TextLevel.All) return;
            AddApp(w32, top, win!.Str("class") ?? "", win.Str("exe") ?? "", win.Str("title"), win.Obj("app")?.Str("commandLine") ?? (win.Obj("app")?.Bool("privateArgs") == true ? "--private" : null), password, withSelection: true);
        }
        catch { }
    }

    /// <summary>Ergänzt den Zustand nach einer Aktion (win32.state) um Explorer-Ordner, Browser-Adresse und Office.</summary>
    public static void EnrichState(JsonObject? state, nint top, Win32Detail.TextLevel level)
    {
        if (state == null || level != Win32Detail.TextLevel.All || top == 0) return;
        try
        {
            bool password = false;
            string exe = state.Str("exe") ?? "";
            if (BrowserName(exe) != null) { try { password = AutomationElement.FocusedElement?.Current.IsPassword == true; } catch { } }
            AddApp(state, top, state.Str("class") ?? "", exe, state.Str("title"), null, password, withSelection: true);
        }
        catch { }
    }

    static void AddApp(JsonObject o, nint top, string cls, string exe, string? title, string? cmdLine, bool password, bool withSelection)
    {
        if (cls is "CabinetWClass" or "ExploreWClass" && Explorer(top, title, withSelection) is { } ex) o["explorer"] = ex;
        else if (BrowserName(exe) is { } bn && !password && !PrivateWindow(title, cmdLine) && Browser(top, bn) is { } br) o["browser"] = br;
        else if (OfficeApp(exe) is { } app && Office(top, app) is { } of) o["office"] = of;
    }

    static nint Hwnd(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return 0;
        try { return (nint)Convert.ToInt64(hex, 16); } catch { return 0; }
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];

    // ------------------------------------------------------------------------------------------
    // STA-Hilfsthread mit Zeitlimit (COM in Explorer/Office kann hängen)
    // ------------------------------------------------------------------------------------------

    static int _staBusy;

    static T? Sta<T>(Func<T?> work, int timeoutMs) where T : class
    {
        if (Interlocked.Increment(ref _staBusy) > 2) { Interlocked.Decrement(ref _staBusy); return null; }   // hängende Aufrufe nicht stapeln
        T? result = null;
        var th = new Thread(() =>
        {
            try { result = work(); } catch { }
            finally { Interlocked.Decrement(ref _staBusy); }
        }) { IsBackground = true, Name = "AppProbe.COM" };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();
        return th.Join(timeoutMs) ? result : null;
    }

    static void Release(object? o) { try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); } catch { } }

    // ------------------------------------------------------------------------------------------
    // Explorer
    // ------------------------------------------------------------------------------------------

    /// <summary>Ordner und ausgewählte Elemente eines Explorer-Fensters über Shell.Application.Windows().</summary>
    static JsonObject? Explorer(nint top, string? title, bool withSelection) => Sta(() =>
    {
        var t = Type.GetTypeFromProgID("Shell.Application");
        if (t == null) return null;
        object? shellObj = Activator.CreateInstance(t);
        dynamic? shell = shellObj, wins = null;
        try
        {
            wins = shell!.Windows();
            int n = Math.Min((int)wins.Count, 60);
            JsonObject? best = null;
            for (int i = 0; i < n; i++)
            {
                dynamic? w = null;
                try
                {
                    w = wins.Item(i);
                    if (w == null) continue;
                    long h = Convert.ToInt64((object)w.HWND);
                    if ((nint)h != top) continue;
                    var o = new JsonObject();
                    string? loc = null;
                    try { loc = (string?)w.LocationName; } catch { }
                    if (loc != null) o["location"] = Trunc(loc, MaxText);
                    dynamic? doc = null;
                    try
                    {
                        doc = w.Document;
                        string? path = (string?)doc.Folder.Self.Path;
                        if (!string.IsNullOrEmpty(path)) o["folder"] = Trunc(path, MaxText);
                        if (withSelection)
                        {
                            dynamic items = doc.SelectedItems();
                            int c = (int)items.Count;
                            o["selectedCount"] = c;
                            var names = new JsonArray();
                            for (int k = 0; k < Math.Min(c, MaxSel); k++) { try { names.Add((JsonNode)Trunc((string)items.Item(k).Name, 200)); } catch { } }
                            if (names.Count > 0) o["selected"] = names;
                            Release(items);
                        }
                    }
                    catch
                    {
                        try { if (Uri.TryCreate((string)w.LocationURL, UriKind.Absolute, out var u) && u.IsFile) o["folder"] = Trunc(u.LocalPath, MaxText); } catch { }
                    }
                    finally { Release(doc); }
                    if (o.Str("folder") is { } f && f.StartsWith("::", StringComparison.Ordinal)) o["virtual"] = true;
                    // Registerkarten (Windows 11): alle teilen das Fenster – die zum Titel passende ist die aktive
                    bool active = loc != null && title != null && title.StartsWith(loc, StringComparison.OrdinalIgnoreCase);
                    if (best == null || active) best = o;
                    if (active) break;
                }
                catch { }
                finally { Release(w); }
            }
            return best;
        }
        finally { Release(wins); Release(shellObj); }
    }, ComTimeoutMs);

    // ------------------------------------------------------------------------------------------
    // Browser
    // ------------------------------------------------------------------------------------------

    internal static string? BrowserName(string exe) => exe.ToLowerInvariant() switch
    {
        "chrome.exe" => "chrome", "msedge.exe" => "edge", "firefox.exe" => "firefox", "brave.exe" => "brave", _ => null
    };

    static readonly Regex PrivateTitle = new(@"(?i)(inkognito|incognito|inprivate|private browsing|privater modus|privates fenster|private window|privates surfen|navigation privée)", RegexOptions.Compiled);
    static readonly Regex PrivateArgs = new(@"(?i)(^|\s)(--?incognito|--?inprivate|--?private(-window)?)(\s|=|$)", RegexOptions.Compiled);

    /// <summary>Privates Browserfenster (Inkognito, InPrivate, privater Modus) am Titel oder an der Befehlszeile erkennbar.</summary>
    public static bool PrivateWindow(string? title, string? commandLine)
        => (title != null && PrivateTitle.IsMatch(title)) || (commandLine != null && PrivateArgs.IsMatch(commandLine));

    static readonly Dictionary<nint, AutomationElement> AddressBars = new();

    /// <summary>Adresse des aktiven Tabs aus der Adressleiste (UIA ValuePattern), bereinigt.</summary>
    static JsonObject? Browser(nint top, string browser)
    {
        AutomationElement? bar;
        lock (AddressBars) AddressBars.TryGetValue(top, out bar);
        bool privateHint = false;
        if (bar == null)
        {
            bar = FindAddressBar(top, browser, out privateHint);
            if (privateHint) return null;
            if (bar == null) return null;
            lock (AddressBars) { if (AddressBars.Count > 32) AddressBars.Clear(); AddressBars[top] = bar; }
        }
        try
        {
            var c = bar.Current;
            if (c.HasKeyboardFocus) return null;   // Nutzer tippt gerade in die Adressleiste – kein fertiger Wert
            if (!bar.TryGetCurrentPattern(ValuePattern.Pattern, out var p)) return null;
            var raw = ((ValuePattern)p).Current.Value;
            var url = SanitizeUrl(raw, out bool assumed);
            if (url == null) return null;
            var o = new JsonObject { ["browser"] = browser, ["url"] = url };
            if (assumed) o["schemeAssumed"] = true;
            return o;
        }
        catch
        {
            lock (AddressBars) AddressBars.Remove(top);
            return null;
        }
    }

    /// <summary>Begrenzte Tiefensuche ohne Webinhalt (Document): Chromium „OmniboxViewViews“, Firefox „urlbar-input“.</summary>
    static AutomationElement? FindAddressBar(nint top, string browser, out bool privateHint)
    {
        privateHint = false;
        var root = AutomationElement.FromHandle(top);
        if (root == null) return null;
        var walker = TreeWalker.ControlViewWalker;
        var sw = Stopwatch.StartNew();
        var stack = new Stack<(AutomationElement el, int depth)>();
        stack.Push((root, 0));
        int visited = 0;
        AutomationElement? firstEdit = null;
        while (stack.Count > 0 && visited < 600 && sw.ElapsedMilliseconds < 250)
        {
            var (el, depth) = stack.Pop();
            visited++;
            AutomationElement.AutomationElementInformation c;
            try { c = el.Current; } catch { continue; }
            if (c.ControlType == ControlType.Document) continue;   // Webinhalt nicht durchsuchen
            if (c.Name is { Length: > 0 } nm && nm.Length < 60 && PrivateTitle.IsMatch(nm)) { privateHint = true; return null; }
            if (c.ControlType == ControlType.Edit)
            {
                if (c.ClassName == "OmniboxViewViews" || c.AutomationId == "urlbar-input") return el;
                firstEdit ??= el;
            }
            if (depth >= 14) continue;
            var kids = new List<AutomationElement>();
            try { for (var ch = walker.GetFirstChild(el); ch != null && kids.Count < 60; ch = walker.GetNextSibling(ch)) kids.Add(ch); } catch { }
            for (int i = kids.Count - 1; i >= 0; i--) stack.Push((kids[i], depth + 1));
        }
        return firstEdit;
    }

    /// <summary>
    /// URL ohne Abfrage (?…), Fragment (#…) und Anmeldedaten: nur scheme://host[:port]/pfad. Ohne Schema (die Adressleiste
    /// blendet „https://“ oft aus) wird https angenommen. Suchbegriffe, data:/javascript: und Ungültiges ergeben null.
    /// </summary>
    public static string? SanitizeUrl(string? raw, out bool schemeAssumed)
    {
        schemeAssumed = false;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (s.Length > 4000 || s.Any(char.IsWhiteSpace)) return null;
        if (Regex.IsMatch(s, @"^(?i)(data|javascript|blob|vbscript):")) return null;
        if (Regex.IsMatch(s, @"^(?i)(about|chrome|edge|brave|opera|vivaldi):[^/]")) { int cut = s.IndexOfAny(new[] { '?', '#' }); return Trunc(cut >= 0 ? s[..cut] : s, MaxText); }
        if (!Regex.IsMatch(s, @"^[A-Za-z][A-Za-z0-9+.-]*://"))
        {
            // nur Hostnamen mit Punkt (oder localhost) – sonst ist es ein Suchbegriff
            if (!Regex.IsMatch(s, @"^(localhost|[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+|\[[0-9A-Fa-f:]+\])(:\d+)?([/?#]|$)")) return null;
            s = "https://" + s; schemeAssumed = true;
        }
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return null;
        string res;
        if (u.IsFile) res = "file:///" + u.AbsolutePath.TrimStart('/');
        else
        {
            if (string.IsNullOrEmpty(u.Host)) return null;
            res = u.Scheme + "://" + u.Host + (u.IsDefaultPort ? "" : ":" + u.Port) + u.AbsolutePath;
        }
        return Trunc(res, MaxText);
    }

    // ------------------------------------------------------------------------------------------
    // Office (Excel, Word) über COM
    // ------------------------------------------------------------------------------------------

    internal static string? OfficeApp(string exe) => exe.ToLowerInvariant() switch { "excel.exe" => "excel", "winword.exe" => "word", _ => null };

    /// <summary>
    /// Excel: Mappe (FullName), Blatt, aktive Zelle, Auswahl; Word: Dokument (FullName). Zuerst über das Dokumentfenster
    /// (AccessibleObjectFromWindow, OBJID_NATIVEOM – genau diese Instanz), sonst GetActiveObject, falls dessen Hauptfenster passt.
    /// </summary>
    static JsonObject? Office(nint top, string app) => Sta(() =>
    {
        object? appObj = null, winObj = null;
        try
        {
            nint doc = FindChild(top, app == "excel" ? "EXCEL7" : "_WwG");
            if (doc != 0)
            {
                var iid = new Guid("00020400-0000-0000-C000-000000000046");   // IDispatch
                if (AccessibleObjectFromWindow(doc, 0xFFFFFFF0 /*OBJID_NATIVEOM*/, ref iid, out winObj) == 0 && winObj != null)
                    appObj = ((dynamic)winObj).Application;
            }
            if (appObj == null)
            {
                CLSIDFromProgID(app == "excel" ? "Excel.Application" : "Word.Application", out var clsid);
                if (GetActiveObject(ref clsid, 0, out var unk) == 0 && unk != null)
                {
                    long h = 0;
                    try { h = app == "excel" ? Convert.ToInt64((object)((dynamic)unk).Hwnd) : Convert.ToInt64((object)((dynamic)unk).ActiveWindow.Hwnd); } catch { }
                    if ((nint)h == top) appObj = unk; else Release(unk);
                }
            }
            if (appObj == null) return null;
            dynamic a = appObj;
            var o = new JsonObject { ["app"] = app };
            if (app == "excel")
            {
                try { o["workbook"] = Trunc((string)a.ActiveWorkbook.FullName, MaxText); } catch { }
                try { o["workbookName"] = Trunc((string)a.ActiveWorkbook.Name, 200); } catch { }
                try { o["sheet"] = Trunc((string)a.ActiveSheet.Name, 100); } catch { }
                try { o["cell"] = ((string)a.ActiveCell.Address).Replace("$", ""); } catch { }
                try { var sel = ((string)a.Selection.Address).Replace("$", ""); if (sel != o.Str("cell")) o["selection"] = Trunc(sel, 100); } catch { }
            }
            else
            {
                try { o["document"] = Trunc((string)a.ActiveDocument.FullName, MaxText); } catch { }
                try { o["documentName"] = Trunc((string)a.ActiveDocument.Name, 200); } catch { }
            }
            return o.Count > 1 ? o : null;
        }
        catch { return null; }   // z. B. Excel im Bearbeitungsmodus (RPC_E_CALL_REJECTED)
        finally { Release(appObj); Release(winObj); }
    }, ComTimeoutMs);

    static nint FindChild(nint top, string cls)
    {
        nint found = 0;
        EnumChildWindows(top, (h, _) => { if (Win32.GetClassNameOf(h) == cls) { found = h; return false; } return true; }, 0);
        return found;
    }

    // ------------------------------------------------------------------------------------------
    // MSAA (IAccessible): Rolle, Zustand, Standardaktion am Klickpunkt
    // ------------------------------------------------------------------------------------------

    static JsonObject? Msaa(int vx, int vy, Win32Detail.TextLevel level, bool password)
    {
        IAccessible? acc = null;
        try
        {
            if (AccessibleObjectFromPoint(new Win32.POINT { X = vx, Y = vy }, out acc, out var child) != 0 || acc == null) return null;
            int childId = child is int ci ? ci : 0;
            object cv = childId;
            var o = new JsonObject();
            if (childId != 0) o["childId"] = childId;
            try
            {
                if (acc.get_accRole(cv) is int role)
                {
                    o["role"] = role;
                    var sb = new StringBuilder(64);
                    if (GetRoleText((uint)role, sb, (uint)sb.Capacity) > 0) o["roleText"] = sb.ToString();
                }
            }
            catch { }
            int st = 0;
            try
            {
                if (acc.get_accState(cv) is int sv)
                {
                    st = sv;
                    o["state"] = "0x" + st.ToString("X");
                    var names = new JsonArray();
                    foreach (var (bit, name) in StateNames) if ((st & bit) != 0) names.Add((JsonNode)name);
                    if (names.Count > 0) o["states"] = names;
                }
            }
            catch { }
            try { if (acc.get_accDefaultAction(cv) is { Length: > 0 } da) o["defaultAction"] = Trunc(da, 100); } catch { }
            if (level != Win32Detail.TextLevel.None && !password && (st & 0x20000000 /*STATE_SYSTEM_PROTECTED: Passwort*/) == 0)
                try { if (acc.get_accName(cv) is { Length: > 0 } nm) o["name"] = Trunc(nm, 200); } catch { }
            return o.Count > 0 ? o : null;
        }
        catch { return null; }
        finally { Release(acc); }
    }

    static readonly (int bit, string name)[] StateNames =
    {
        (0x1, "unavailable"), (0x2, "selected"), (0x4, "focused"), (0x8, "pressed"), (0x10, "checked"), (0x20, "mixed"), (0x40, "readOnly"),
        (0x200, "expanded"), (0x400, "collapsed"), (0x8000, "invisible"), (0x100000, "focusable"), (0x200000, "selectable"), (0x40000000, "hasPopup")
    };

    /// <summary>IAccessible (duale Schnittstelle) – nur die benötigten Methoden in Vtable-Reihenfolge.</summary>
    [ComImport, Guid("618736E0-3C3D-11CF-810C-00AA00389B71"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    interface IAccessible
    {
        [return: MarshalAs(UnmanagedType.IDispatch)] object get_accParent();
        int get_accChildCount();
        [return: MarshalAs(UnmanagedType.IDispatch)] object get_accChild(object varChild);
        [return: MarshalAs(UnmanagedType.BStr)] string get_accName(object varChild);
        [return: MarshalAs(UnmanagedType.BStr)] string get_accValue(object varChild);
        [return: MarshalAs(UnmanagedType.BStr)] string get_accDescription(object varChild);
        object get_accRole(object varChild);
        object get_accState(object varChild);
        [return: MarshalAs(UnmanagedType.BStr)] string get_accHelp(object varChild);
        int get_accHelpTopic([MarshalAs(UnmanagedType.BStr)] out string helpFile, object varChild);
        [return: MarshalAs(UnmanagedType.BStr)] string get_accKeyboardShortcut(object varChild);
        object get_accFocus();
        object get_accSelection();
        [return: MarshalAs(UnmanagedType.BStr)] string get_accDefaultAction(object varChild);
    }

    delegate bool EnumProc(nint h, nint l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(nint parent, EnumProc proc, nint l);
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromPoint(Win32.POINT pt, [MarshalAs(UnmanagedType.Interface)] out IAccessible? acc, [MarshalAs(UnmanagedType.Struct)] out object? child);
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(nint hwnd, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object? obj);
    [DllImport("oleacc.dll", CharSet = CharSet.Unicode, EntryPoint = "GetRoleTextW")] static extern uint GetRoleText(uint role, StringBuilder sb, uint max);
    [DllImport("oleaut32.dll")] static extern int GetActiveObject(ref Guid clsid, nint reserved, [MarshalAs(UnmanagedType.IUnknown)] out object? unk);
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)] static extern int CLSIDFromProgID(string progId, out Guid clsid);
}
