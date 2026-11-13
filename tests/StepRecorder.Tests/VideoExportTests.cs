using System.Buffers.Binary;
using StepRecorder.Export;
using StepRecorder.Export.Formats;
using StepRecorder.ReadLayer;
using Xunit.Abstractions;

namespace StepRecorder.Tests;

/// <summary>Video-Export (MP4, 09 §8e): H.264 über Media Foundation aus derselben Bildfolge wie das GIF.</summary>
[Collection("Loc")]
public class VideoExportTests : IDisposable
{
    readonly ITestOutputHelper _out;
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-mp4-" + Guid.NewGuid().ToString("N")[..8]);
    public VideoExportTests(ITestOutputHelper output) { _out = output; Directory.CreateDirectory(_root); }
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    /// <summary>Oberste MP4-Boxen: (Typ, Anfang, Länge).</summary>
    static List<(string type, long start, long size)> Boxes(byte[] data, long from = 0, long to = -1)
    {
        var list = new List<(string, long, long)>();
        long p = from, end = to < 0 ? data.LongLength : to;
        while (p + 8 <= end)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan((int)p));
            string type = System.Text.Encoding.ASCII.GetString(data, (int)p + 4, 4);
            int header = 8;
            if (size == 1) { size = (long)BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan((int)p + 8)); header = 16; }
            else if (size == 0) size = end - p;
            if (size < header || p + size > end) break;
            list.Add((type, p, size));
            p += size;
        }
        return list;
    }

    [Fact]
    public void Mp4_IsValidH264Container_WithSlideDurations()
    {
        string folder = SyntheticSession.Create(Path.Combine(_root, "s"));
        ExportResult r;
        try
        {
            using var v = SessionView.Open(folder);
            r = Exporter.Run(v, new ExportRequest { Format = "mp4", Target = Path.Combine(_root, "video.mp4"), GifSeconds = 1.5 });
        }
        catch (ExportPrerequisiteException ex)
        {
            _out.WriteLine("Media Foundation/H.264 fehlt auf diesem Rechner – Test übersprungen: " + ex.Message);
            return;
        }
        Assert.Equal(Path.Combine(_root, "video.mp4"), r.Path);
        var data = File.ReadAllBytes(r.Path);
        Assert.True(data.Length > 1000, $"nur {data.Length} Bytes");
        var top = Boxes(data);
        Assert.Equal("ftyp", top[0].type);
        var moov = Assert.Single(top, b => b.type == "moov");
        Assert.Contains(top, b => b.type == "mdat" && b.size > 100);

        // Gesamtdauer aus mvhd: Titelbild (mindestens 1,5 s) plus 1,5 s je Schritt
        var mvhd = Assert.Single(Boxes(data, moov.start + 8, moov.start + moov.size), b => b.type == "mvhd");
        int p = (int)mvhd.start + 8;
        bool v1 = data[p] == 1;
        uint timescale = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(p + (v1 ? 20 : 12)));
        ulong duration = v1 ? BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(p + 24)) : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(p + 16));
        double seconds = (double)duration / timescale;
        _out.WriteLine($"{data.Length} Bytes, {seconds:0.00} s");
        Assert.True(seconds >= 2.9, $"zu kurz: {seconds:0.00} s");
        double steps = (seconds - 1.5) / 1.5;
        Assert.True(Math.Abs(steps - Math.Round(steps)) < 0.1, $"Dauer passt nicht zu 1,5 s je Bild: {seconds:0.00} s");

        // Standardort: Unterordner „video“, Endung .mp4
        using var v2 = SessionView.Open(folder);
        Assert.EndsWith(Path.Combine("export", "video", Path.GetFileNameWithoutExtension(Exporter.DefaultTarget(v2, "mp4", null)) + ".mp4"), Exporter.DefaultTarget(v2, "mp4", null));
    }

    [Theory]
    [InlineData(1201, 965, 1200, 964)]
    [InlineData(1600, 964, 1600, 964)]
    [InlineData(3840, 2160, 1920, 1080)]
    [InlineData(1600, 1344, 1576, 1324)]
    public void EncodedSize_IsEven_AndWithinEncoderLimits(int w, int h, int ew, int eh)
    {
        var (W, H) = Mp4Writer.EncodedSize(w, h);
        Assert.Equal((ew, eh), (W, H));
        Assert.True(W % 2 == 0 && H % 2 == 0 && W <= Mp4Writer.MaxWidth && W * H <= Mp4Writer.MaxPixels);
    }

    [Fact]
    public void Mp4_IsKnownGuideFormat()
    {
        Assert.True(Exporter.IsKnownFormat("mp4"));
        Assert.True(Exporter.IsGuideFormat("MP4"));
        Assert.Equal("mp4", Exporter.Extension("mp4"));
        Assert.Equal("video", Exporter.SubFolder("mp4"));
        Assert.False(Exporter.IsFolderOutput("mp4"));
    }
}
