using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace StepRecorder.Native;

/// <summary>
/// Bildschirmaufnahme per DXGI Desktop Duplication (D3D11), deutlich schneller als GDI-BitBlt.
/// Fenster mit WDA_EXCLUDEFROMCAPTURE erscheinen auch hier nicht. Liefert bei jedem Fehler null,
/// damit der Aufrufer auf <see cref="ScreenGrabber.Capture"/> zurückfallen kann.
/// COM-Aufrufe direkt über VTable-Zeiger, ohne NuGet-Pakete.
/// </summary>
/// <remarks>
/// Drehung: 90/180/270° werden per RotateFlip in die angezeigte Ausrichtung gebracht (nur Identität real getestet).
/// HDR: das Bild wird von DXGI nach 8 Bit BGRA gewandelt; Farben können von der Anzeige abweichen.
/// Mehrere GPUs: das Gerät wird auf dem Adapter angelegt, dem der Ausgang gehört; scheitert das
/// (z. B. Hybrid-Grafik), liefert Capture null.
/// </remarks>
public sealed unsafe class DxgiGrabber : IDisposable
{
    // HRESULTs
    const int DXGI_ERROR_ACCESS_LOST = unchecked((int)0x887A0026);
    const int DXGI_ERROR_WAIT_TIMEOUT = unchecked((int)0x887A0027);

    const uint DXGI_FORMAT_B8G8R8A8_UNORM = 87, DXGI_FORMAT_B8G8R8A8_UNORM_SRGB = 91;
    const uint D3D11_USAGE_STAGING = 3, D3D11_CPU_ACCESS_READ = 0x20000, D3D11_MAP_READ = 1;
    const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20, D3D11_SDK_VERSION = 7;
    const int ROT_IDENTITY = 1, ROT_90 = 2, ROT_180 = 3, ROT_270 = 4;
    const int FirstFrameWaitMs = 250;

    static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    static readonly Guid IID_IDXGIOutput1 = new("00cddea8-939b-4b83-a340-a685226666cc");
    static readonly Guid IID_IDXGIOutput5 = new("80a07424-ab52-42eb-833c-0c42fd282d98");
    static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct DXGI_OUTPUT_DESC { public fixed char DeviceName[32]; public RECT DesktopCoordinates; public int AttachedToDesktop, Rotation; public nint Monitor; }

    [StructLayout(LayoutKind.Sequential)]
    struct DXGI_OUTDUPL_DESC { public uint Width, Height, RefreshNum, RefreshDen, Format, ScanlineOrdering, Scaling; public int Rotation, DesktopImageInSystemMemory; }

    [StructLayout(LayoutKind.Sequential)]
    struct DXGI_OUTDUPL_FRAME_INFO
    {
        public long LastPresentTime, LastMouseUpdateTime; public uint AccumulatedFrames; public int RectsCoalesced, ProtectedContentMaskedOut;
        public int PointerX, PointerY, PointerVisible; public uint TotalMetadataBufferSize, PointerShapeBufferSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct D3D11_TEXTURE2D_DESC
    {
        public uint Width, Height, MipLevels, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CPUAccessFlags, MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)] struct D3D11_MAPPED_SUBRESOURCE { public byte* pData; public uint RowPitch, DepthPitch; }

    [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1(Guid* riid, nint* factory);
    [DllImport("d3d11.dll")]
    static extern int D3D11CreateDevice(nint adapter, int driverType, nint software, uint flags, int* featureLevels, uint numLevels,
        uint sdkVersion, nint* device, int* featureLevel, nint* context);

    /// <summary>Zustand je DXGI-Ausgang (Monitor).</summary>
    sealed class OutputState
    {
        public Rectangle Bounds;
        public nint Device;   // ID3D11Device
        public nint Context;  // ID3D11DeviceContext
        public nint Dupl;     // IDXGIOutputDuplication
        public nint Staging;  // ID3D11Texture2D, CPU-lesbar; hält zugleich das letzte Bild
        public uint TexW, TexH, TexFormat;
        public int Rotation = ROT_IDENTITY;
        public bool HasFrame;
    }

    readonly object _gate = new();
    readonly Dictionary<Rectangle, OutputState> _outputs = new();
    readonly Dictionary<Rectangle, long> _retryAt = new(); // nach Fehlschlag erst ab diesem Zeitpunkt erneut versuchen
    bool _disposed;

    /// <summary>Letzter Fehler (Diagnose), null wenn keiner.</summary>
    public string? LastError { get; private set; }
    /// <summary>Hochformat-Monitore (90/270°) per DXGI aufnehmen – aus, solange nicht auf echter Hardware geprüft.</summary>
    public static bool AllowRotated = Environment.GetEnvironmentVariable("STEPRECORDER_DXGI_ROTATED") == "1";

    DxgiGrabber() { }

    ~DxgiGrabber() => ReleaseAll();

    /// <summary>Legt den Grabber an; null, wenn Desktop Duplication auf keinem Monitor verfügbar ist.</summary>
    /// <remarks>Je Prozess ist nur eine Duplizierung pro Monitor möglich, daher nur eine Instanz verwenden.</remarks>
    public static DxgiGrabber? TryCreate()
    {
        DxgiGrabber? g = null;
        try
        {
            g = new DxgiGrabber();
            bool any = false;
            // Alle Ausgänge vorab öffnen: das erste echte Bild kommt erst einige ms nach DuplicateOutput.
            foreach (var bounds in EnumOutputs(Rectangle.Empty, null, null))
                if (g.GetState(bounds) != null) any = true;
            if (any) return g;
        }
        catch { }
        g?.Dispose();
        return null;
    }

    /// <summary>Nimmt den ganzen Monitor mit diesen physischen Grenzen auf; null bei Fehler (→ GDI verwenden).</summary>
    public Bitmap? Capture(Rectangle monitorBounds)
    {
        lock (_gate)
        {
            if (_disposed) return null;
            try
            {
                var st = GetState(monitorBounds);
                return st == null ? null : Grab(st);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseAll();
        }
        GC.SuppressFinalize(this);
    }

    void ReleaseAll()
    {
        foreach (var st in _outputs.Values) FreeState(st);
        _outputs.Clear();
    }

    // ---------- Ausgänge finden und Duplizierung anlegen ----------

    /// <summary>
    /// Liefert den Zustand für diese Grenzen oder legt ihn neu an (frische DXGI-Factory, D3D11-Gerät auf dem
    /// Adapter des Ausgangs, Duplizierung). Nach einem Fehlschlag erst nach einer Pause erneut versuchen.
    /// </summary>
    OutputState? GetState(Rectangle bounds)
    {
        if (_outputs.TryGetValue(bounds, out var st)) return st;
        long now = Environment.TickCount64;
        if (_retryAt.TryGetValue(bounds, out long retry) && now < retry) return null;

        nint adapter = 0, output = 0, o1 = 0;
        st = new OutputState { Bounds = bounds };
        try
        {
            var current = EnumOutputs(bounds, &adapter, &output);
            // Zustände zu nicht mehr vorhandenen Grenzen freigeben (Monitor verschoben/entfernt): sie blockieren
            // sonst den Ausgang, denn je Prozess ist nur eine Duplizierung pro Ausgang erlaubt.
            foreach (var old in _outputs.Values.Where(o => !current.Contains(o.Bounds)).ToList()) Drop(old);

            if (output == 0) { LastError = $"Kein DXGI-Ausgang für {bounds}"; _retryAt[bounds] = now + 3000; return null; }

            Guid iid = IID_IDXGIOutput1;
            if (QI(output, &iid, &o1) < 0) { LastError = "IDXGIOutput1 fehlt"; _retryAt[bounds] = now + 3000; return null; }

            nint dev = 0, ctx = 0; int level;
            int hr = D3D11CreateDevice(adapter, 0 /*UNKNOWN*/, 0, D3D11_CREATE_DEVICE_BGRA_SUPPORT, null, 0, D3D11_SDK_VERSION, &dev, &level, &ctx);
            if (hr < 0) { LastError = $"D3D11CreateDevice 0x{hr:X8}"; _retryAt[bounds] = now + 3000; return null; }
            st.Device = dev; st.Context = ctx;

            if (!Duplicate(st, o1)) { _retryAt[bounds] = now + 1000; return null; }
            _retryAt.Remove(bounds);
            _outputs[bounds] = st;
            var result = st;
            st = null;
            return result;
        }
        finally
        {
            if (st != null) FreeState(st);
            Release(ref o1);
            Release(ref output);
            Release(ref adapter);
        }
    }

    /// <summary>
    /// Grenzen aller an den Desktop angeschlossenen DXGI-Ausgänge. Passt einer zu <paramref name="want"/>,
    /// werden Adapter und Ausgang (je mit eigener Referenz) zurückgegeben.
    /// </summary>
    static List<Rectangle> EnumOutputs(Rectangle want, nint* adapterOut, nint* outputOut)
    {
        var list = new List<Rectangle>();
        nint factory = 0;
        Guid iid = IID_IDXGIFactory1;
        if (CreateDXGIFactory1(&iid, &factory) < 0) return list;
        try
        {
            for (uint a = 0; ; a++)
            {
                nint adapter = 0;
                if (((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Vt(factory, 12))(factory, a, &adapter) < 0) break; // EnumAdapters1
                for (uint o = 0; ; o++)
                {
                    nint output = 0;
                    if (((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Vt(adapter, 7))(adapter, o, &output) < 0) break; // EnumOutputs
                    DXGI_OUTPUT_DESC d;
                    if (OutputDesc(output, &d) && d.AttachedToDesktop != 0)
                    {
                        var r = ToRect(d.DesktopCoordinates);
                        list.Add(r);
                        if (outputOut != null && *outputOut == 0 && r == want)
                        {
                            *outputOut = output; output = 0;
                            AddRef(adapter); *adapterOut = adapter;
                        }
                    }
                    Release(ref output);
                }
                Release(ref adapter);
            }
        }
        finally { Release(ref factory); }
        return list;
    }

    /// <summary>Legt die Duplizierung für den Ausgang an.</summary>
    bool Duplicate(OutputState st, nint output1)
    {
        nint dupl = 0;
        int hr = -1;
        // IDXGIOutput5::DuplicateOutput1 zuerst: vorgesehen für PerMonitorV2-Prozesse, wandelt HDR nach BGRA.
        Guid iid5 = IID_IDXGIOutput5;
        nint o5 = 0;
        if (QI(output1, &iid5, &o5) >= 0)
        {
            uint fmt = DXGI_FORMAT_B8G8R8A8_UNORM;
            hr = ((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint*, nint*, int>)Vt(o5, 26))(o5, st.Device, 0, 1, &fmt, &dupl);
            Release(ref o5);
        }
        if (hr < 0)
        {
            dupl = 0;
            hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Vt(output1, 22))(output1, st.Device, &dupl); // IDXGIOutput1::DuplicateOutput
        }
        if (hr < 0 || dupl == 0)
        {
            LastError = $"DuplicateOutput 0x{hr:X8}";
            return false;
        }

        DXGI_OUTDUPL_DESC dd;
        ((delegate* unmanaged[Stdcall]<nint, DXGI_OUTDUPL_DESC*, void>)Vt(dupl, 7))(dupl, &dd); // GetDesc
        st.Dupl = dupl;
        st.Rotation = dd.Rotation;
        st.HasFrame = false;
        return true;
    }

    // ---------- Aufnahme ----------

    Bitmap? Grab(OutputState st)
    {
        DXGI_OUTDUPL_FRAME_INFO info;
        nint res = 0;
        int hr;
        // Mit gültigem letzten Bild nicht warten. Direkt nach dem Anlegen liefert DXGI zunächst ein leeres
        // (schwarzes) Frame mit LastPresentTime 0; solche Frames überspringen, bis ein echtes Bild kommt.
        long deadline = Environment.TickCount64 + FirstFrameWaitMs;
        while (true)
        {
            uint timeout = st.HasFrame ? 0u : 20u;
            hr = ((delegate* unmanaged[Stdcall]<nint, uint, DXGI_OUTDUPL_FRAME_INFO*, nint*, int>)Vt(st.Dupl, 8))(st.Dupl, timeout, &info, &res);
            bool empty = hr == DXGI_ERROR_WAIT_TIMEOUT || (hr >= 0 && info.LastPresentTime == 0);
            if (st.HasFrame || !empty || Environment.TickCount64 >= deadline) break;
            if (hr >= 0) { Release(ref res); ReleaseFrame(st); }
        }
        if (hr >= 0 && !st.HasFrame && info.LastPresentTime == 0)
        {
            Release(ref res);
            ReleaseFrame(st);
            hr = DXGI_ERROR_WAIT_TIMEOUT;
        }

        if (hr == DXGI_ERROR_WAIT_TIMEOUT)
            return st.HasFrame ? ReadStaging(st, false) : null;
        if (hr == DXGI_ERROR_ACCESS_LOST)
        {
            // Desktopwechsel, UAC, Moduswechsel: alles freigeben, beim nächsten Aufruf neu suchen und anlegen
            LastError = "ACCESS_LOST";
            Drop(st);
            return null;
        }
        if (hr < 0)
        {
            LastError = $"AcquireNextFrame 0x{hr:X8}";
            Drop(st);
            return null;
        }

        bool frameHeld = true;
        try
        {
            // LastPresentTime == 0: nur der Mauszeiger hat sich bewegt, das Bild in der Staging-Textur ist aktuell.
            if (info.LastPresentTime != 0 || !st.HasFrame)
            {
                Guid iid = IID_ID3D11Texture2D;
                nint tex = 0;
                if (QI(res, &iid, &tex) < 0) { LastError = "Kein ID3D11Texture2D"; return null; }
                try
                {
                    D3D11_TEXTURE2D_DESC td;
                    ((delegate* unmanaged[Stdcall]<nint, D3D11_TEXTURE2D_DESC*, void>)Vt(tex, 10))(tex, &td); // GetDesc
                    if (td.Format != DXGI_FORMAT_B8G8R8A8_UNORM && td.Format != DXGI_FORMAT_B8G8R8A8_UNORM_SRGB)
                    {
                        LastError = $"Format {td.Format} nicht unterstützt";
                        return null;
                    }
                    if (!EnsureStaging(st, td)) return null;
                    ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)Vt(st.Context, 47))(st.Context, st.Staging, tex); // CopyResource
                    st.HasFrame = true;
                }
                finally { Release(ref tex); }
            }
            // Map wartet auf die Kopie; danach Frame sofort freigeben
            frameHeld = false;
            return ReadStaging(st, true);
        }
        finally
        {
            Release(ref res);
            if (frameHeld) ReleaseFrame(st);
        }
    }

    bool EnsureStaging(OutputState st, D3D11_TEXTURE2D_DESC src)
    {
        if (st.Staging != 0 && st.TexW == src.Width && st.TexH == src.Height && st.TexFormat == src.Format) return true;
        Release(ref st.Staging);
        st.HasFrame = false;
        var d = new D3D11_TEXTURE2D_DESC
        {
            Width = src.Width, Height = src.Height, MipLevels = 1, ArraySize = 1, Format = src.Format,
            SampleCount = 1, SampleQuality = 0, Usage = D3D11_USAGE_STAGING, BindFlags = 0, CPUAccessFlags = D3D11_CPU_ACCESS_READ, MiscFlags = 0,
        };
        nint tex = 0;
        int hr = ((delegate* unmanaged[Stdcall]<nint, D3D11_TEXTURE2D_DESC*, void*, nint*, int>)Vt(st.Device, 5))(st.Device, &d, null, &tex); // CreateTexture2D
        if (hr < 0) { LastError = $"CreateTexture2D 0x{hr:X8}"; return false; }
        st.Staging = tex; st.TexW = src.Width; st.TexH = src.Height; st.TexFormat = src.Format;
        return true;
    }

    /// <summary>Kopiert die Staging-Textur in ein neues Bitmap (BGRA → 32bppRgb) und dreht es in die angezeigte Ausrichtung.</summary>
    Bitmap? ReadStaging(OutputState st, bool releaseFrameAfterMap)
    {
        D3D11_MAPPED_SUBRESOURCE m;
        int hr = ((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, D3D11_MAPPED_SUBRESOURCE*, int>)Vt(st.Context, 14))(st.Context, st.Staging, 0, D3D11_MAP_READ, 0, &m); // Map
        if (releaseFrameAfterMap) ReleaseFrame(st);
        if (hr < 0) { LastError = $"Map 0x{hr:X8}"; return null; }

        Bitmap? bmp = null;
        try
        {
            int w = (int)st.TexW, h = (int)st.TexH;
            bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                long row = (long)w * 4;
                byte* src = m.pData, dst = (byte*)bd.Scan0;
                if (m.RowPitch == row && bd.Stride == row)
                    Buffer.MemoryCopy(src, dst, row * h, row * h);
                else
                    for (int y = 0; y < h; y++)
                        Buffer.MemoryCopy(src + (long)y * m.RowPitch, dst + (long)y * bd.Stride, row, row);
            }
            finally { bmp.UnlockBits(bd); }
        }
        catch
        {
            bmp?.Dispose();
            throw;
        }
        finally
        {
            ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)Vt(st.Context, 15))(st.Context, st.Staging, 0); // Unmap
        }

        // Gedrehte Monitore sind nicht auf echter Hardware geprüft: lieber auf GDI zurückfallen als ein verdrehtes Bild speichern
        if (st.Rotation is ROT_90 or ROT_270 && !AllowRotated) { LastError = "gedrehter Monitor – GDI"; bmp.Dispose(); return null; }
        // Die Textur liegt in der Ausrichtung des Panels vor; zurückdrehen ergibt das angezeigte Bild.
        switch (st.Rotation)
        {
            case ROT_90: bmp.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
            case ROT_180: bmp.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
            case ROT_270: bmp.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
        }
        if (bmp.Width != st.Bounds.Width || bmp.Height != st.Bounds.Height)
        {
            LastError = $"Größe {bmp.Width}×{bmp.Height} passt nicht zu {st.Bounds}";
            bmp.Dispose();
            return null;
        }
        return bmp;
    }

    // ---------- Aufräumen ----------

    static void ReleaseFrame(OutputState st)
    {
        if (st.Dupl != 0) ((delegate* unmanaged[Stdcall]<nint, int>)Vt(st.Dupl, 14))(st.Dupl);
    }

    /// <summary>Verwirft den Zustand eines Ausgangs ganz; beim nächsten Aufruf wird neu gesucht.</summary>
    void Drop(OutputState st)
    {
        _outputs.Remove(st.Bounds);
        FreeState(st);
    }

    static void FreeState(OutputState st)
    {
        Release(ref st.Dupl);
        Release(ref st.Staging);
        Release(ref st.Context);
        Release(ref st.Device);
        st.HasFrame = false;
    }

    // ---------- COM-Hilfen ----------

    static nint Vt(nint p, int index) => (*(nint**)p)[index];

    static int QI(nint p, Guid* iid, nint* result) =>
        ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Vt(p, 0))(p, iid, result);

    static void AddRef(nint p) => ((delegate* unmanaged[Stdcall]<nint, uint>)Vt(p, 1))(p);

    static void Release(ref nint p)
    {
        nint q = p;
        p = 0;
        if (q != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Vt(q, 2))(q);
    }

    static bool OutputDesc(nint output, DXGI_OUTPUT_DESC* d) =>
        ((delegate* unmanaged[Stdcall]<nint, DXGI_OUTPUT_DESC*, int>)Vt(output, 7))(output, d) >= 0;

    static Rectangle ToRect(RECT r) => Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
}
