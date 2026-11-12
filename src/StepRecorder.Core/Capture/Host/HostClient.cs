using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StepRecorder.Capture.Host;

/// <summary>
/// Hauptprozess-Seite des Capture-Hosts: legt Ring und Ereignisse an, startet den Host-Prozess
/// (gleiche EXE, Rolle <c>--capture-host</c>) in einem Job-Objekt und steuert ihn. Fällt bei Problemen
/// auf einen Hook-Thread im eigenen Prozess zurück.
/// </summary>
public sealed class HostClient : IDisposable
{
    public InputRing Ring { get; }
    public nint DataEvent { get; }
    readonly nint _cmdEvent;
    nint _parentHandle, _job;
    Process? _proc;
    Thread? _inProcThread;
    public bool InProcess { get; private set; }
    public string? HostExe { get; set; }
    public int Restarts { get; private set; }

    public HostClient()
    {
        Ring = InputRing.Create(Environment.ProcessId);
        DataEvent = InputRing.CreateInheritableEvent();
        _cmdEvent = InputRing.CreateInheritableEvent();
    }

    /// <summary>Startet den Host (ohne Hooks). true = bereit.</summary>
    public bool Start(bool allowProcess = true)
    {
        if (allowProcess && TryStartProcess()) return true;
        StartInProcess();
        return true;
    }

    bool TryStartProcess()
    {
        try
        {
            string exe = HostExe ?? Environment.ProcessPath ?? "";
            if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(exe)) return false;
            nint self = GetCurrentProcess();
            if (!DuplicateHandle(self, self, self, out _parentHandle, 0x00100000 /*SYNCHRONIZE*/, true, 0)) return false;
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false, CreateNoWindow = true,
                Arguments = $"--capture-host {(long)Ring.MappingHandle} {(long)DataEvent} {(long)_cmdEvent} {(long)_parentHandle} {Environment.ProcessId}",
            };
            psi.Environment["DOTNET_gcConcurrent"] = "0";
            psi.Environment["DOTNET_TieredCompilation"] = "0";
            _proc = Process.Start(psi);
            if (_proc == null) return false;
            AttachJob(_proc);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3000)
            {
                if (Ring.Ready == 1) return true;
                if (_proc.HasExited) return false;
                Thread.Sleep(10);
            }
            try { _proc.Kill(); } catch { }
            return false;
        }
        catch { return false; }
    }

    void StartInProcess()
    {
        InProcess = true;
        var host = new HookHost(Ring, DataEvent);
        _inProcThread = new Thread(() => host.Run(_cmdEvent, 0)) { IsBackground = true, Name = "HookHost.InProcess" };
        _inProcThread.Start();
        var sw = Stopwatch.StartNew();
        while (!host.Ready && sw.ElapsedMilliseconds < 3000) Thread.Sleep(5);
    }

    void AttachJob(Process p)
    {
        try
        {
            _job = CreateJobObject(0, null);
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = 0x2000; // KILL_ON_JOB_CLOSE
            int len = Marshal.SizeOf(info);
            nint ptr = Marshal.AllocHGlobal(len);
            try { Marshal.StructureToPtr(info, ptr, false); SetInformationJobObject(_job, 9, ptr, (uint)len); }
            finally { Marshal.FreeHGlobal(ptr); }
            AssignProcessToJobObject(_job, p.Handle);
        }
        catch { }
    }

    /// <summary>Setzt den Sollzustand und wartet auf die Bestätigung (Hooks gesetzt/entfernt).</summary>
    public bool Command(int target, int timeoutMs = 1000)
    {
        Ring.Target = target;
        InputRing.SetEvent(_cmdEvent);
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (Ring.Ack == target) return true;
            Thread.Sleep(2);
        }
        return false;
    }

    public bool HostAlive => InProcess ? _inProcThread?.IsAlive == true : _proc is { HasExited: false };

    public void Stop()
    {
        Command(InputRing.TargetQuit, 500);
        if (_proc != null)
        {
            try { if (!_proc.WaitForExit(500)) _proc.Kill(); } catch { }
        }
        _inProcThread?.Join(500);
    }

    public void Dispose()
    {
        try { Stop(); } catch { }
        _proc?.Dispose();
        if (_job != 0) CloseHandle(_job);
        if (_parentHandle != 0) CloseHandle(_parentHandle);
        InputRing.CloseEvent(_cmdEvent);
        InputRing.CloseEvent(DataEvent);
        Ring.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public nuint MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit; public nuint Affinity; public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)] struct IO_COUNTERS { public ulong a, b, c, d, e, f; }
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern nint CreateJobObject(nint sa, string? name);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(nint job, int cls, nint info, uint len);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint h);
    [DllImport("kernel32.dll")] static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool DuplicateHandle(nint srcProc, nint src, nint dstProc, out nint dst, uint access, bool inherit, uint options);
}
