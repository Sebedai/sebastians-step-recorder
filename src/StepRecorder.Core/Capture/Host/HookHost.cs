using System.Diagnostics;
using System.Runtime.InteropServices;
using StepRecorder.Native;
using static StepRecorder.Native.Win32;

namespace StepRecorder.Capture.Host;

/// <summary>
/// Führt die Low-Level-Hooks aus und schreibt Rohdatensätze in den Ring. Läuft im Capture-Host-Prozess
/// (Normalfall) oder als Rückfall auf einem eigenen Thread im Hauptprozess. Der Hook-Aufruf allokiert
/// nicht, sperrt nicht, schreibt nichts auf Platte und kehrt immer über CallNextHookEx zurück.
/// </summary>
public sealed unsafe class HookHost
{
    const uint WM_APP_COMMAND = 0x8001;
    readonly InputRing _ring;
    readonly nint _dataEvent;
    nint _kbHook, _msHook;
    uint _threadId;
    // Delegaten als Felder halten, damit der GC sie nicht einsammelt
    readonly HookProc _kbProc, _msProc;
    readonly long _oneMs = Stopwatch.Frequency / 1000;
    public volatile bool Ready;

    public HookHost(InputRing ring, nint dataEvent)
    {
        _ring = ring; _dataEvent = dataEvent;
        _kbProc = KeyboardProc; _msProc = MouseProc;
    }

    /// <summary>Hook-Thread: Nachrichtenschleife bis zum Beenden-Befehl.</summary>
    public void Run(nint commandEvent, nint parentProcess)
    {
        _threadId = GetCurrentThreadId();
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        // Warmlaufen des Hook-Pfads (JIT) mit Dummy-Daten, ohne zu schreiben
        var dummy = new RingRecord { Qpc = Stopwatch.GetTimestamp() };
        _ = dummy.Qpc + _ring.FillRatio;
        PeekMessage(out _, 0, 0, 0, 0); // Nachrichtenwarteschlange anlegen

        // Befehlsüberwachung: Steuerblock auswerten und an den Hook-Thread weiterreichen
        var watcher = new Thread(() =>
        {
            var handles = new List<WaitHandle>();
            using var cmd = commandEvent != 0 ? new AutoResetEvent(false) { SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(commandEvent, false) } : null;
            int last = -1;
            while (true)
            {
                if (cmd != null) cmd.WaitOne(100); else Thread.Sleep(50);
                _ring.Beat();
                if (parentProcess != 0 && WaitForSingleObject(parentProcess, 0) == 0) { PostThreadMessage(_threadId, WM_QUIT, 0, 0); return; }
                int target = _ring.Target;
                if (target != last) { last = target; PostThreadMessage(_threadId, WM_APP_COMMAND, target, 0); }
                if (target == InputRing.TargetQuit) return;
            }
        }) { IsBackground = true, Name = "HookHost.Command" };
        watcher.Start();

        _ring.Ready = 1;
        Ready = true;
        while (GetMessage(out var msg, 0, 0, 0) > 0)
        {
            if (msg.message == WM_APP_COMMAND)
            {
                int target = (int)msg.wParam;
                if (target == InputRing.TargetRun) Install();
                else Uninstall();
                _ring.Ack = target;
                if (target == InputRing.TargetQuit) break;
                continue;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
        Uninstall();
    }

    void Install()
    {
        nint mod = GetModuleHandle(null);
        if (_kbHook == 0) _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, mod, 0);
        if (_msHook == 0) _msHook = SetWindowsHookEx(WH_MOUSE_LL, _msProc, mod, 0);
    }

    void Uninstall()
    {
        if (_kbHook != 0) { UnhookWindowsHookEx(_kbHook); _kbHook = 0; }
        if (_msHook != 0) { UnhookWindowsHookEx(_msHook); _msHook = 0; }
    }

    nint KeyboardProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            long q = Stopwatch.GetTimestamp();
            var k = (KBDLLHOOKSTRUCT*)lParam;
            var rec = new RingRecord
            {
                Qpc = q, Kind = RingKind.Keyboard, Message = (int)wParam, Data = k->vkCode, Scan = k->scanCode,
                Flags = k->flags, Time = k->time, ExtraInfo = k->dwExtraInfo, Foreground = GetForegroundWindow()
            };
            _ring.TryWrite(rec);
            InputRing.SetEvent(_dataEvent);
            _ring.RecordHookDuration(Stopwatch.GetTimestamp() - q, _oneMs);
        }
        return CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    nint MouseProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            long q = Stopwatch.GetTimestamp();
            var m = (MSLLHOOKSTRUCT*)lParam;
            var rec = new RingRecord
            {
                Qpc = q, Kind = RingKind.Mouse, Message = (int)wParam, X = m->pt.X, Y = m->pt.Y, Data = m->mouseData,
                Flags = m->flags, Time = m->time, ExtraInfo = m->dwExtraInfo, Foreground = GetForegroundWindow()
            };
            _ring.TryWrite(rec);
            if ((int)wParam != WM_MOUSEMOVE) InputRing.SetEvent(_dataEvent);
            _ring.RecordHookDuration(Stopwatch.GetTimestamp() - q, _oneMs);
        }
        return CallNextHookEx(_msHook, nCode, wParam, lParam);
    }

    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(nint h, uint ms);

    /// <summary>Einstieg der Rolle <c>--capture-host map data cmd parent parentPid</c>. Rückgabe = Exit-Code.</summary>
    public static int HostMain(string[] args)
    {
        try
        {
            if (args.Length < 6) return 7;
            nint map = (nint)long.Parse(args[1]), data = (nint)long.Parse(args[2]), cmd = (nint)long.Parse(args[3]), parent = (nint)long.Parse(args[4]);
            int parentPid = int.Parse(args[5]);
            using var ring = InputRing.Open(map, parentPid);
            if (ring == null) return 7;
            ring.HostPid = Environment.ProcessId;
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch { }
            var host = new HookHost(ring, data);
            host.Run(cmd, parent);
            return 0;
        }
        catch { return 1; }
    }
}
