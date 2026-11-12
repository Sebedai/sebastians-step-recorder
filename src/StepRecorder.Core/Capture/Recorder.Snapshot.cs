using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.Capture;

/// <summary>
/// Text-Schnappschüsse (03 §7.5; 12, Paket 11): der Wortlaut modaler Fenster (auch WPF-, WinForms- und WinUI-Dialoge,
/// für die <c>window.dialog</c> nicht kommt) und des Vordergrundfensters bei den Markern „Fehler“ und „Erwartet“ sowie
/// beim Schnappschuss. Nur sichtbarer Text innerhalb des Fensterrechtecks – nie mehr, als das Bild zeigt;
/// Werte nur von statischem Text, nie aus Eingabefeldern; nichts im Privatmodus, für ausgeschlossene Programme,
/// außerhalb des Aufnahmebereichs und in Terminal, Remote-Desktop und VM.
/// </summary>
public sealed partial class Recorder
{
    readonly Dictionary<nint, long> _snapped = new();

    /// <summary>Schnappschuss anfordern (darf unter <c>_gate</c> aufgerufen werden; die Arbeit macht der UIA-Arbeiter).</summary>
    void RequestSnapshot(nint hwnd, string trigger, long t, int delayMs = 0)
    {
        hwnd = WindowRegistry.Root(hwnd);
        if (hwnd == 0 || PrivacyMode || State != RecorderState.Recording) return;
        var win = _reg.Peek(hwnd) ?? _reg.Ensure(hwnd, t);
        if (win == null || win.Own || win.Excluded || !win.InScope || WindowRegistry.SurfaceOf(win) != "structured") return;
        lock (_snapped)
        {
            if (trigger != "manual" && _snapped.TryGetValue(hwnd, out var last) && t - last < 2_000_000) return;
            _snapped[hwnd] = t;
        }
        int max = Options.SnapshotMaxNodes > 0 ? Math.Min(Options.SnapshotMaxNodes, 1000) : 300;
        string winId = win.Id;
        void Go() => _uia.Enqueue(() => _uia.Snapshot(hwnd, max, values: Options.PrivacyLevel != "strict"), res =>
        {
            lock (_gate)
            {
                if (_w.Closed || res.Status != "ok" || res.El?.Arr("nodes") is not { Count: > 0 } nodes || PrivacyMode) return;
                var ev = new JsonObject { ["t"] = t, ["tObs"] = res.TEnd, ["type"] = "ui.snapshot", ["trigger"] = trigger, ["win"] = winId, ["title"] = res.El.Str("title"), ["nodes"] = nodes.DeepClone(), ["ms"] = res.Ms };
                if (res.El.Bool("incomplete")) ev["incomplete"] = true;
                Emit(ev.Clean());
            }
        });
        if (delayMs > 0) Task.Delay(delayMs).ContinueWith(_ => { try { if (!_w.Closed) Go(); } catch { } });
        else Go();
    }

    /// <summary>Neues Fenster: Dialog oder modal (Besitzerfenster deaktiviert) → Schnappschuss, sobald es gezeichnet ist.</summary>
    void SnapshotIfModal(nint hwnd, WinEntry e, long t)
    {
        if (e.Kind is "menu" or "popup" or "dropdown" or "tooltip" or "systemUi") return;
        nint owner = GetWindow(hwnd, 4 /*GW_OWNER*/);
        bool modal = e.Kind == "dialog" || owner != 0 && !IsWindowEnabled(owner);
        if (modal) RequestSnapshot(hwnd, "dialogStart", t, 350);
    }

    [DllImport("user32.dll")] static extern nint GetWindow(nint h, uint cmd);
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(nint h);
}
