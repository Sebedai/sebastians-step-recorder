using System.Runtime.InteropServices;

namespace StepRecorder.Capture.Host;

/// <summary>Art eines Ringeintrags.</summary>
public enum RingKind : int { None = 0, Mouse = 1, Keyboard = 2, Overflow = 3 }

/// <summary>Fester 64-Byte-Datensatz im gemeinsamen Speicher (keine Zeiger).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
public struct RingRecord
{
    public long Qpc;          // Zeitstempel im Hook gelesen
    public RingKind Kind;
    public int Message;       // WM_* aus wParam
    public int X, Y;          // Maus: Bildschirmpunkt
    public uint Data;         // Maus: mouseData; Tastatur: vkCode
    public uint Scan;         // Tastatur: scanCode
    public uint Flags;        // LL-Flags
    public uint Time;         // Systemzeit in ms
    public long ExtraInfo;
    public long Foreground;   // Fenster-Stempel: Vordergrundfenster zum Hook-Zeitpunkt
    public long Reserved;
}

/// <summary>
/// Gemeinsamer Speicher zwischen Hauptprozess und Capture-Host: Kopf, Steuerblock und
/// SPSC-Ring mit 65 536 Einträgen. Nur unbenannte, vererbte Handles.
/// </summary>
public sealed unsafe class InputRing : IDisposable
{
    public const int Magic = 0x53524331; // "SRC1"
    public const int LayoutVersion = 1;
    public const int Capacity = 65536;
    const int HeaderSize = 4096;
    public static readonly long TotalSize = HeaderSize + (long)Capacity * 64;

    // Kopf-Offsets
    const int OffMagic = 0, OffVersion = 4, OffCapacity = 8, OffParentPid = 12;
    const int OffWrite = 64, OffRead = 128, OffOverflow = 192;
    const int OffTarget = 256, OffAck = 260, OffReady = 264, OffHostPid = 268;
    const int OffHeartbeat = 320, OffHookCalls = 328, OffHookMaxTicks = 336, OffHookOver1ms = 344, OffHookSumTicks = 352;

    public const int TargetIdle = 0, TargetRun = 1, TargetPause = 2, TargetQuit = 3;

    readonly byte* _base;
    readonly nint _mapping;
    readonly bool _owner;

    InputRing(nint mapping, byte* @base, bool owner) { _mapping = mapping; _base = @base; _owner = owner; }

    public nint MappingHandle => _mapping;

    public static InputRing Create(int parentPid)
    {
        var sa = new SECURITY_ATTRIBUTES { nLength = sizeof(SECURITY_ATTRIBUTES), bInheritHandle = 1 };
        nint h = CreateFileMapping(-1, ref sa, 0x04 /*PAGE_READWRITE*/, (uint)(TotalSize >> 32), (uint)TotalSize, null);
        if (h == 0) throw new InvalidOperationException("CreateFileMapping fehlgeschlagen: " + Marshal.GetLastWin32Error());
        byte* p = (byte*)MapViewOfFile(h, 0x0002 | 0x0004, 0, 0, (nuint)TotalSize);
        if (p == null) throw new InvalidOperationException("MapViewOfFile fehlgeschlagen");
        new Span<byte>(p, HeaderSize).Clear();
        *(int*)(p + OffMagic) = Magic; *(int*)(p + OffVersion) = LayoutVersion; *(int*)(p + OffCapacity) = Capacity;
        *(int*)(p + OffParentPid) = parentPid;
        return new InputRing(h, p, true);
    }

    public static InputRing? Open(nint handle, int expectedParent)
    {
        byte* p = (byte*)MapViewOfFile(handle, 0x0002 | 0x0004, 0, 0, (nuint)TotalSize);
        if (p == null) return null;
        if (*(int*)(p + OffMagic) != Magic || *(int*)(p + OffVersion) != LayoutVersion || *(int*)(p + OffParentPid) != expectedParent)
        { UnmapViewOfFile((nint)p); return null; }
        return new InputRing(handle, p, false);
    }

    long* WritePtr => (long*)(_base + OffWrite);
    long* ReadPtr => (long*)(_base + OffRead);
    RingRecord* Records => (RingRecord*)(_base + HeaderSize);

    public int Target { get => Volatile.Read(ref *(int*)(_base + OffTarget)); set => Volatile.Write(ref *(int*)(_base + OffTarget), value); }
    public int Ack { get => Volatile.Read(ref *(int*)(_base + OffAck)); set => Volatile.Write(ref *(int*)(_base + OffAck), value); }
    public int Ready { get => Volatile.Read(ref *(int*)(_base + OffReady)); set => Volatile.Write(ref *(int*)(_base + OffReady), value); }
    public int HostPid { get => Volatile.Read(ref *(int*)(_base + OffHostPid)); set => Volatile.Write(ref *(int*)(_base + OffHostPid), value); }
    public long Heartbeat => Volatile.Read(ref *(long*)(_base + OffHeartbeat));
    public void Beat() => Interlocked.Increment(ref *(long*)(_base + OffHeartbeat));
    public long Overflows => Volatile.Read(ref *(long*)(_base + OffOverflow));
    public long HookCalls => Volatile.Read(ref *(long*)(_base + OffHookCalls));
    public long HookMaxTicks => Volatile.Read(ref *(long*)(_base + OffHookMaxTicks));
    public long HookOver1ms => Volatile.Read(ref *(long*)(_base + OffHookOver1ms));
    public long HookSumTicks => Volatile.Read(ref *(long*)(_base + OffHookSumTicks));

    /// <summary>Vom Hook aufgerufen: schreibt ohne Allokation, wartet nie.</summary>
    public bool TryWrite(in RingRecord rec)
    {
        long w = Volatile.Read(ref *WritePtr);
        long r = Volatile.Read(ref *ReadPtr);
        if (w - r >= Capacity) { Interlocked.Increment(ref *(long*)(_base + OffOverflow)); return false; }
        Records[w & (Capacity - 1)] = rec;
        Volatile.Write(ref *WritePtr, w + 1);
        return true;
    }

    public void RecordHookDuration(long ticks, long oneMsTicks)
    {
        *(long*)(_base + OffHookCalls) += 1;
        *(long*)(_base + OffHookSumTicks) += ticks;
        if (ticks > *(long*)(_base + OffHookMaxTicks)) *(long*)(_base + OffHookMaxTicks) = ticks;
        if (ticks > oneMsTicks) *(long*)(_base + OffHookOver1ms) += 1;
    }

    /// <summary>Hauptprozess: liest alle verfügbaren Einträge und nullt sie danach.</summary>
    public int Drain(List<RingRecord> into)
    {
        long r = Volatile.Read(ref *ReadPtr);
        long w = Volatile.Read(ref *WritePtr);
        int n = 0;
        while (r < w)
        {
            ref RingRecord slot = ref Records[r & (Capacity - 1)];
            into.Add(slot);
            slot = default;
            r++; n++;
        }
        Volatile.Write(ref *ReadPtr, r);
        return n;
    }

    public double FillRatio => (Volatile.Read(ref *WritePtr) - Volatile.Read(ref *ReadPtr)) / (double)Capacity;

    public void Dispose()
    {
        if (_base != null)
        {
            if (_owner) new Span<byte>(_base, HeaderSize).Clear();
            UnmapViewOfFile((nint)_base);
        }
        if (_owner && _mapping != 0) CloseHandle(_mapping);
    }

    [StructLayout(LayoutKind.Sequential)] struct SECURITY_ATTRIBUTES { public int nLength; public nint lpSecurityDescriptor; public int bInheritHandle; }
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern nint CreateFileMapping(nint hFile, ref SECURITY_ATTRIBUTES sa, uint protect, uint maxHigh, uint maxLow, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] static extern nint MapViewOfFile(nint h, uint access, uint offHigh, uint offLow, nuint size);
    [DllImport("kernel32.dll")] static extern bool UnmapViewOfFile(nint p);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint h);

    [DllImport("kernel32.dll", SetLastError = true)] static extern nint CreateEvent(ref SECURITY_ATTRIBUTES sa, bool manual, bool initial, string? name);
    [DllImport("kernel32.dll")] public static extern bool SetEvent(nint h);

    /// <summary>Erzeugt ein unbenanntes, vererbbares Auto-Reset-Ereignis.</summary>
    public static nint CreateInheritableEvent()
    {
        var sa = new SECURITY_ATTRIBUTES { nLength = sizeof(SECURITY_ATTRIBUTES), bInheritHandle = 1 };
        return CreateEvent(ref sa, false, false, null);
    }
    public static void CloseEvent(nint h) { if (h != 0) CloseHandle(h); }
}
