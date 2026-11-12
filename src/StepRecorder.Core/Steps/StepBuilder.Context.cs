using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Steps;

/// <summary>
/// Anwendungskontext je Schritt für AutoHotkey und KI: <c>explorer</c> (Ordner, Auswahl), <c>browser</c> (Adresse ohne
/// Abfrage), <c>office</c> (Mappe/Blatt/Zelle bzw. Dokument), <c>list</c> (Listen-Steuerelement, Auswahl danach),
/// Befehls-IDs klassischer Menüs in <c>menuPath</c>, Win32-Listenauswahl in <c>choose.win32</c> und in <c>verify</c>
/// Statusleiste, Ordner, Adresse und Listenauswahl nach dem Schritt.
/// </summary>
public sealed partial class StepBuilder
{
    /// <summary>Menütext ohne Tastenkürzel-Anhang (nach Tab) und ohne Zugriffstasten-&amp; („&amp;&amp;“ bleibt „&amp;“).</summary>
    public static string MenuLabel(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        int tab = raw.IndexOf('\t');
        var s = tab >= 0 ? raw[..tab] : raw;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '&') { if (i + 1 < s.Length && s[i + 1] == '&') { sb.Append('&'); i++; } continue; }
            sb.Append(s[i]);
        }
        return sb.ToString().Trim();
    }

    /// <summary>Zustand nach dem Schritt: Statusleiste (mit Änderungen), Explorer-Ordner, Browser-Adresse, Office, Listenauswahl.</summary>
    static void ContextVerify(JsonObject v, JsonObject? before, JsonObject after)
    {
        if (after.Obj("statusBar") is { } sbA && sbA.Arr("parts") is { Count: > 0 } pa)
        {
            var sb = new JsonObject { ["classNN"] = sbA.Str("classNN"), ["parts"] = pa.DeepClone() };
            var pb = before?.Obj("statusBar")?.Arr("parts");
            if (pb != null && (before!.Str("hwnd") == null || before.Str("hwnd") == after.Str("hwnd")))
            {
                var ch = new JsonArray();
                for (int i = 0; i < pa.Count; i++)
                {
                    string? from = i < pb.Count ? pb[i]?.GetValue<string>() : null, to = pa[i]?.GetValue<string>();
                    if (from != to) ch.Add(new JsonObject { ["part"] = i + 1, ["from"] = from, ["to"] = to }.Clean());
                }
                if (ch.Count > 0) sb["changed"] = ch;
            }
            v["statusBar"] = sb.Clean();
        }
        if (after.Obj("explorer")?.Str("folder") is { } f) v["explorerFolder"] = f;
        if (after.Obj("browser")?.Str("url") is { } u) v["browserUrl"] = u;
        if (after.Obj("office") is { } of) v["office"] = of.DeepClone();
        if (after.Obj("focusList") is { } fl) v["list"] = fl.DeepClone();
    }

    /// <summary>Kontextschlüssel des Schritts aus dem Win32-Kontext des Ziels, ersatzweise aus dem Zustand davor (gleiches Fenster).</summary>
    static void ContextKeys(JsonObject s, string kind, JsonObject? before, JsonObject? after)
    {
        var w = s.Obj("target")?.Obj("win32");
        string? hwnd = w?.Obj("window")?.Str("hwnd");
        bool sameBefore = before != null && (hwnd == null || before.Str("hwnd") == hwnd);
        bool sameAfter = after != null && (hwnd == null || after.Str("hwnd") == hwnd);
        JsonObject? Pick(string key) => w?.Obj(key) ?? (sameBefore ? before!.Obj(key) : null);

        if (Pick("explorer") is { } ex)
        {
            var o = (JsonObject)ex.DeepClone();
            if (sameAfter && after!.Obj("explorer")?.Str("folder") is { } fa && fa != ex.Str("folder")) o["folderAfter"] = fa;
            s["explorer"] = o;
        }
        if (Pick("browser") is { } br)
        {
            var o = (JsonObject)br.DeepClone();
            if (sameAfter && after!.Obj("browser")?.Str("url") is { } ua && ua != br.Str("url")) o["urlAfter"] = ua;
            s["browser"] = o;
        }
        else if (sameAfter && after!.Obj("browser") is { } ba) s["browser"] = new JsonObject { ["browser"] = ba.Str("browser"), ["urlAfter"] = ba.Str("url") }.Clean();
        if (Pick("office") is { } of)
        {
            var o = (JsonObject)of.DeepClone();
            if (sameAfter && after!.Obj("office") is { } oa)
            {
                var diff = new JsonObject();
                foreach (var k in new[] { "workbook", "sheet", "cell", "selection", "document" })
                    if (oa.Str(k) is { } va && va != of.Str(k)) diff[k] = va;
                if (diff.Count > 0) o["after"] = diff;
            }
            s["office"] = o;
        }
        if (w?.Obj("list") is { } li)
        {
            var o = (JsonObject)li.DeepClone();
            if (sameAfter && after!.Obj("focusList") is { } fl && (fl.Str("classNN") == li.Str("classNN") || li.Str("kind") == "comboList")) o["after"] = fl.DeepClone();
            s["list"] = o;
        }
        else if (kind is "key" or "type" && sameAfter && after!.Obj("focusList") is { } fl2)
            s["list"] = new JsonObject { ["kind"] = fl2.Str("kind"), ["classNN"] = fl2.Str("classNN"), ["after"] = fl2.DeepClone() }.Clean();

        // ---------- klassische Menüs: Befehls-ID und Pfad ----------
        if (kind == "click" && w?.Obj("menu") is { } wm && !wm.Bool("separator"))
        {
            var mp = s.Obj("menuPath") ?? new JsonObject();
            if (mp.Arr("items") is not { Count: > 0 })
            {
                var labels = wm.Arr("path")?.OfType<JsonObject>().Select(p => MenuLabel(p.Str("text"))).Where(x => x.Length > 0).ToList()
                             ?? (wm.Str("text") is { } t ? new List<string> { MenuLabel(t) } : new List<string>());
                if (labels.Count > 0) mp["items"] = new JsonArray(labels.Select(x => (JsonNode)x).ToArray());
            }
            mp["win32Menu"] = true;
            if (wm.LongN("cmdId") is long id) mp["commandId"] = id;
            if (wm.Str("source") is { } src) mp["source"] = src;
            if (wm.Arr("path") is { Count: > 0 } path)
            {
                mp["win32Path"] = new JsonArray(path.OfType<JsonObject>().Select(p => (JsonNode)MenuLabel(p.Str("text"))).ToArray());
                mp["win32Positions"] = new JsonArray(path.OfType<JsonObject>().Select(p => (JsonNode)p.Int("pos")).ToArray());
            }
            if (wm.Bool("submenu")) mp["submenu"] = true;
            if (wm["enabled"] is JsonValue en && !en.GetValue<bool>()) mp["disabled"] = true;
            if (wm.Obj("owner") is { } ow) mp["owner"] = ow.DeepClone();
            if (mp.Arr("items") is { Count: > 0 }) s["menuPath"] = mp;
        }

        // ---------- Win32-Listenauswahl (ControlChooseString/-Index) ----------
        if (kind is "click" or "doubleClick" && w?.Obj("list") is { } wl && wl.Obj("atPoint") is { } ap)
        {
            bool comboList = wl.Str("kind") == "comboList";
            var owner = wl.Obj("owner");
            string? listKind = comboList ? (owner != null ? "comboBox" : "listBox") : wl.Str("kind");
            var wc = new JsonObject
            {
                ["kind"] = listKind, ["classNN"] = comboList ? owner?.Str("classNN") : wl.Str("classNN"),
                ["winTitle"] = comboList ? owner?.Obj("window")?.Str("winTitle") : null, ["index"] = ap.LongN("index"), ["text"] = ap.Str("text"),
                ["count"] = wl.LongN("count"), ["row"] = ap.Arr("row")?.DeepClone(), ["path"] = ap.Arr("path")?.DeepClone(), ["column"] = ap.LongN("column")
            }.Clean();
            var ch = s.Obj("choose") ?? new JsonObject { ["itemType"] = listKind };
            if (ch.Str("item") == null && ap.Str("text") is { } at) ch["item"] = at;
            if (ch["index"] == null && ap.LongN("index") is long ix) { ch["index"] = ix; ch["of"] = wl.LongN("count"); }
            ch["win32"] = wc;
            if (listKind is "comboBox" or "listBox" && wc.Str("classNN") is { } nn) ch["win32Control"] = nn;
            if (ch.Str("item") != null || ch["index"] != null) s["choose"] = ch.Clean();
        }
    }
}
