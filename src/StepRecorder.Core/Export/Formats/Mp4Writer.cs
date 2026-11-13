using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using StepRecorder.Settings;

namespace StepRecorder.Export.Formats;

/// <summary>
/// H.264-Video im MP4-Container über Windows Media Foundation (Sink Writer, 09 §8e) – ohne fremde Bibliotheken.
/// Eingang sind RGB32-Bilder (von oben nach unten); jedes Bild wird mit fester Bildrate so oft wiederholt, wie seine
/// Anzeigedauer verlangt. Breite und Höhe werden gerade gemacht (H.264), höchstens 1920 Pixel breit und höchstens
/// 1920 × 1088 Bildpunkte groß (passt in jede H.264-Stufe des Windows-Encoders).
/// Die COM-Aufrufe gehen direkt über die V-Tabelle – zeigergroß (nint), damit gleich auf x64 und ARM64.
/// </summary>
public sealed unsafe class Mp4Writer : IDisposable
{
    public const int DefaultFps = 15, MaxWidth = 1920, MaxPixels = 1920 * 1088;

    public int Width { get; }
    public int Height { get; }
    public int Fps { get; }
    /// <summary>Geschriebene Bilder (einschließlich Wiederholungen).</summary>
    public long FrameCount => _frame;

    nint _writer;
    uint _stream;
    long _frame, _ms;
    bool _mfStarted, _comInit, _finished;

    /// <param name="width">Breite der Bilder, die mit <see cref="AddFrame"/> kommen (wird bei Bedarf verkleinert und gerade gemacht).</param>
    /// <exception cref="ExportPrerequisiteException">Media Foundation oder der H.264-Encoder fehlt (z. B. Windows N ohne Media Feature Pack).</exception>
    public Mp4Writer(string path, int width, int height, int fps = DefaultFps)
    {
        (Width, Height) = EncodedSize(width, height);
        Fps = Math.Clamp(fps, 1, 60);
        try { Open(Path.GetFullPath(path)); }
        catch { Dispose(); throw; }
    }

    /// <summary>Kodiergröße: höchstens <see cref="MaxWidth"/> breit und <see cref="MaxPixels"/> groß, beide Seiten gerade.</summary>
    public static (int w, int h) EncodedSize(int w, int h)
    {
        w = Math.Max(2, w); h = Math.Max(2, h);
        double s = Math.Min(1, Math.Min((double)MaxWidth / w, Math.Sqrt((double)MaxPixels / ((double)w * h))));
        if (s < 1) { w = (int)Math.Floor(w * s); h = (int)Math.Floor(h * s); }
        return (w & ~1, h & ~1);
    }

    static ExportPrerequisiteException Missing(Exception? inner = null, int hr = 0) => new(
        Loc.L("Video-Export nicht möglich: Windows Media Foundation mit H.264-Encoder fehlt. Unter Windows N bitte das „Media Feature Pack“ installieren (Einstellungen → Apps → Optionale Features)."
              + (hr != 0 ? $" (0x{hr:X8})" : ""),
              "Video export not possible: Windows Media Foundation with an H.264 encoder is missing. On Windows N, please install the \"Media Feature Pack\" (Settings → Apps → Optional features)."
              + (hr != 0 ? $" (0x{hr:X8})" : "")), inner);

    void Open(string path)
    {
        int hr = CoInitializeEx(0, 0 /* COINIT_MULTITHREADED */);
        _comInit = hr >= 0;   // S_OK oder S_FALSE; RPC_E_CHANGED_MODE (STA-Thread) ist ebenfalls brauchbar
        try { hr = MFStartup(MF_VERSION, 1 /* MFSTARTUP_LITE */); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { throw Missing(ex); }
        if (hr < 0) throw Missing(null, hr);
        _mfStarted = true;

        nint attrs = 0, outType = 0, inType = 0, enc = 0;
        try
        {
            // Container ausdrücklich MP4 – unabhängig von der Dateiendung des Ziels
            Check(MFCreateAttributes(out attrs, 2));
            SetGuid(attrs, MF_TRANSCODE_CONTAINERTYPE, MFTranscodeContainerType_MPEG4);
            SetUInt32(attrs, MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 0);   // Software-Encoder: überall gleich und vorhersehbar
            try { hr = MFCreateSinkWriterFromURL(path, 0, attrs, out _writer); }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { throw Missing(ex); }
            Check(hr);

            Check(MFCreateMediaType(out outType));
            SetGuid(outType, MF_MT_MAJOR_TYPE, MFMediaType_Video);
            SetGuid(outType, MF_MT_SUBTYPE, MFVideoFormat_H264);
            SetUInt32(outType, MF_MT_AVG_BITRATE, (uint)Math.Clamp((long)Width * Height * Fps / 5, 1_000_000, 16_000_000));
            SetUInt32(outType, MF_MT_INTERLACE_MODE, 2 /* progressiv */);
            SetUInt64(outType, MF_MT_FRAME_SIZE, Pack((uint)Width, (uint)Height));
            SetUInt64(outType, MF_MT_FRAME_RATE, Pack((uint)Fps, 1));
            SetUInt64(outType, MF_MT_PIXEL_ASPECT_RATIO, Pack(1, 1));
            hr = SinkWriter_AddStream(_writer, outType, out _stream);
            if (hr < 0) throw Missing(Marshal.GetExceptionForHR(hr), hr);

            Check(MFCreateMediaType(out inType));
            SetGuid(inType, MF_MT_MAJOR_TYPE, MFMediaType_Video);
            SetGuid(inType, MF_MT_SUBTYPE, MFVideoFormat_RGB32);
            SetUInt32(inType, MF_MT_INTERLACE_MODE, 2);
            SetUInt64(inType, MF_MT_FRAME_SIZE, Pack((uint)Width, (uint)Height));
            SetUInt64(inType, MF_MT_FRAME_RATE, Pack((uint)Fps, 1));
            SetUInt64(inType, MF_MT_PIXEL_ASPECT_RATIO, Pack(1, 1));
            SetUInt32(inType, MF_MT_DEFAULT_STRIDE, (uint)(Width * 4));   // positiv = von oben nach unten

            // Qualitätsgesteuert statt fester Bitrate (Bildschirmtext bleibt lesbar, Standbilder kosten kaum etwas),
            // Schlüsselbild alle fünf Sekunden zum Spulen (Schlüsselbilder machen bei Standbildern den Großteil der Datei aus).
            // Lehnt der Encoder das ab: ohne Parameter.
            Check(MFCreateAttributes(out enc, 3));
            SetUInt32(enc, CODECAPI_AVEncCommonRateControlMode, 3 /* Quality */);
            SetUInt32(enc, CODECAPI_AVEncCommonQuality, 75);
            SetUInt32(enc, CODECAPI_AVEncMPVGOPSize, (uint)(Fps * 5));
            hr = SinkWriter_SetInputMediaType(_writer, _stream, inType, enc);
            if (hr < 0) hr = SinkWriter_SetInputMediaType(_writer, _stream, inType, 0);
            if (hr < 0) throw Missing(Marshal.GetExceptionForHR(hr), hr);

            Check(SinkWriter_BeginWriting(_writer));
        }
        finally { Release(attrs); Release(outType); Release(inType); Release(enc); }
    }

    /// <summary>Ein Bild für <paramref name="durationMs"/> Millisekunden zeigen (wird mit der Bildrate wiederholt).</summary>
    public void AddFrame(Bitmap frame, int durationMs)
    {
        if (_writer == 0 || _finished) throw new InvalidOperationException("Mp4Writer: closed");
        nint buf = FillBuffer(frame);
        try
        {
            // Bildzahl aus der aufsummierten Zeit – kein Auseinanderlaufen bei Bildraten, die nicht aufgehen
            _ms += Math.Max(1, durationMs);
            long end = Math.Max(_frame + 1, (_ms * Fps + 500) / 1000);
            for (; _frame < end; _frame++)
            {
                long t0 = _frame * 10_000_000L / Fps, t1 = (_frame + 1) * 10_000_000L / Fps;   // 100-ns-Einheiten
                Check(MFCreateSample(out var sample));
                try
                {
                    // Alle Wiederholungen teilen sich den (nur gelesenen) Puffer
                    Check(Sample_AddBuffer(sample, buf));
                    Check(Sample_SetSampleTime(sample, t0));
                    Check(Sample_SetSampleDuration(sample, t1 - t0));
                    Check(SinkWriter_WriteSample(_writer, _stream, sample));
                }
                finally { Release(sample); }
            }
        }
        finally { Release(buf); }
    }

    /// <summary>Datei abschließen (schreibt den MP4-Index). Ohne diesen Aufruf ist die Datei unbrauchbar.</summary>
    public void Finish()
    {
        if (_writer == 0 || _finished) return;
        Check(SinkWriter_Finalize(_writer));
        _finished = true;
    }

    /// <summary>Bild als RGB32 (BGRX, von oben nach unten, Zeilenlänge Breite × 4) in einen Media-Foundation-Puffer.</summary>
    nint FillBuffer(Bitmap frame)
    {
        int stride = Width * 4, len = stride * Height;
        Bitmap? scaled = null;
        var src = frame;
        if (frame.Width > Width + 1 || frame.Height > Height + 1)
        {
            // zu groß: verkleinern (sonst nur auf gerade Größe beschneiden oder auffüllen)
            double s = Math.Min((double)Width / frame.Width, (double)Height / frame.Height);
            scaled = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
            using var g = Graphics.FromImage(scaled);
            g.Clear(Color.Black);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(frame, 0, 0, (int)Math.Round(frame.Width * s), (int)Math.Round(frame.Height * s));
            src = scaled;
        }
        nint buf = 0;
        try
        {
            Check(MFCreateMemoryBuffer((uint)len, out buf));
            byte* dst; uint max, cur;
            Check(Buffer_Lock(buf, &dst, &max, &cur));
            try
            {
                new Span<byte>(dst, len).Clear();   // Rand (bei ungerader Quellgröße) schwarz
                int w = Math.Min(src.Width, Width), h = Math.Min(src.Height, Height);
                var bd = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                try
                {
                    for (int y = 0; y < h; y++)
                        new ReadOnlySpan<byte>((byte*)bd.Scan0 + (long)y * bd.Stride, w * 4).CopyTo(new Span<byte>(dst + (long)y * stride, w * 4));
                }
                finally { src.UnlockBits(bd); }
            }
            finally { Buffer_Unlock(buf); }
            Check(Buffer_SetCurrentLength(buf, (uint)len));
            return buf;
        }
        catch { Release(buf); throw; }
        finally { scaled?.Dispose(); }
    }

    public void Dispose()
    {
        if (_writer != 0) { Release(_writer); _writer = 0; }
        if (_mfStarted) { MFShutdown(); _mfStarted = false; }
        if (_comInit) { CoUninitialize(); _comInit = false; }
    }

    // ---------------- Media Foundation ----------------

    const uint MF_VERSION = 0x00020070;
    static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    static readonly Guid MF_MT_AVG_BITRATE = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    static readonly Guid MF_MT_INTERLACE_MODE = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4012-b834-72030849a37d");
    static readonly Guid MF_MT_FRAME_RATE = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    static readonly Guid MF_MT_PIXEL_ASPECT_RATIO = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    static readonly Guid MF_MT_DEFAULT_STRIDE = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00aa00389b71");
    static readonly Guid MFVideoFormat_H264 = new("34363248-0000-0010-8000-00aa00389b71");
    static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00aa00389b71");
    static readonly Guid MF_TRANSCODE_CONTAINERTYPE = new("150ff23f-4abc-478b-ac4f-e1916fba1cca");
    static readonly Guid MFTranscodeContainerType_MPEG4 = new("dc6cd05d-b9d0-40ef-bd35-fa622c1ab28a");
    static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = new("a634a91c-822b-41b9-a494-4de4643612b0");
    static readonly Guid CODECAPI_AVEncCommonRateControlMode = new("1c0608e9-370c-4710-8a58-cb6181c42423");
    static readonly Guid CODECAPI_AVEncCommonQuality = new("fcbf57a3-7ea5-4b0c-9644-69b40c39c391");
    static readonly Guid CODECAPI_AVEncMPVGOPSize = new("95f31b26-95a4-41aa-9303-246a7fc6eef1");

    static ulong Pack(uint hi, uint lo) => ((ulong)hi << 32) | lo;

    static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }

    [DllImport("ole32.dll")] static extern int CoInitializeEx(nint reserved, uint coInit);
    [DllImport("ole32.dll")] static extern void CoUninitialize();
    [DllImport("mfplat.dll")] static extern int MFStartup(uint version, uint flags);
    [DllImport("mfplat.dll")] static extern int MFShutdown();
    [DllImport("mfplat.dll")] static extern int MFCreateAttributes(out nint attributes, uint initialSize);
    [DllImport("mfplat.dll")] static extern int MFCreateMediaType(out nint mediaType);
    [DllImport("mfplat.dll")] static extern int MFCreateMemoryBuffer(uint maxLength, out nint buffer);
    [DllImport("mfplat.dll")] static extern int MFCreateSample(out nint sample);
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)] static extern int MFCreateSinkWriterFromURL(string url, nint byteStream, nint attributes, out nint sinkWriter);

    // V-Tabellen (mfobjects.h, mfreadwrite.h): IUnknown 0–2; IMFAttributes 3–32; IMFSample ab 33; IMFMediaBuffer 3–7; IMFSinkWriter 3–13
    static nint Fn(nint obj, int slot) => (*(nint**)obj)[slot];

    static void Release(nint obj) { if (obj != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Fn(obj, 2))(obj); }

    static void SetUInt32(nint attrs, Guid key, uint value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)Fn(attrs, 21))(attrs, &key, value));
    static void SetUInt64(nint attrs, Guid key, ulong value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, ulong, int>)Fn(attrs, 22))(attrs, &key, value));
    static void SetGuid(nint attrs, Guid key, Guid value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Fn(attrs, 24))(attrs, &key, &value));

    static int Sample_SetSampleTime(nint s, long t) => ((delegate* unmanaged[Stdcall]<nint, long, int>)Fn(s, 36))(s, t);
    static int Sample_SetSampleDuration(nint s, long d) => ((delegate* unmanaged[Stdcall]<nint, long, int>)Fn(s, 38))(s, d);
    static int Sample_AddBuffer(nint s, nint b) => ((delegate* unmanaged[Stdcall]<nint, nint, int>)Fn(s, 42))(s, b);

    static int Buffer_Lock(nint b, byte** data, uint* max, uint* cur) => ((delegate* unmanaged[Stdcall]<nint, byte**, uint*, uint*, int>)Fn(b, 3))(b, data, max, cur);
    static int Buffer_Unlock(nint b) => ((delegate* unmanaged[Stdcall]<nint, int>)Fn(b, 4))(b);
    static int Buffer_SetCurrentLength(nint b, uint len) => ((delegate* unmanaged[Stdcall]<nint, uint, int>)Fn(b, 6))(b, len);

    static int SinkWriter_AddStream(nint w, nint type, out uint index) { uint i; int hr = ((delegate* unmanaged[Stdcall]<nint, nint, uint*, int>)Fn(w, 3))(w, type, &i); index = i; return hr; }
    static int SinkWriter_SetInputMediaType(nint w, uint stream, nint type, nint encParams) => ((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)Fn(w, 4))(w, stream, type, encParams);
    static int SinkWriter_BeginWriting(nint w) => ((delegate* unmanaged[Stdcall]<nint, int>)Fn(w, 5))(w);
    static int SinkWriter_WriteSample(nint w, uint stream, nint sample) => ((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Fn(w, 6))(w, stream, sample);
    static int SinkWriter_Finalize(nint w) => ((delegate* unmanaged[Stdcall]<nint, int>)Fn(w, 11))(w);
}
