using StepRecorder.Export.Formats;
using StepRecorder.Settings;

namespace StepRecorder.Export;

public static partial class GuideExport
{
    // =====================================================================================
    // Video (MP4, 09 §8e): dieselbe Bildfolge wie das GIF – Titelbild, dann je Schritt Bild mit Nummer und Überschrift –,
    // als H.264 über Windows Media Foundation. Kein Bildschirmvideo: das Video entsteht aus den Schrittbildern.
    // =====================================================================================

    /// <summary>Breite der Videobilder: schärfer als das GIF (1200), bleibt aber in der Pixelgrenze des H.264-Encoders.</summary>
    const int VideoWidth = 1600;

    static void Video(Doc d, string outPath, GuideOptions o)
    {
        int total = d.Blocks.OfType<Item>().Count(i => i.Png != null) + 1, done = 0;
        Mp4Writer? mp4 = null;
        try
        {
            foreach (var (frame, ms) in SlideFrames(d, o, VideoWidth, 1, VideoWidth))
            {
                using (frame)
                {
                    mp4 ??= new Mp4Writer(outPath, frame.Width, frame.Height);
                    mp4.AddFrame(frame, ms);
                }
                ExportJob.Tick(++done, total, Loc.L("Video", "Video"));
            }
            mp4?.Finish();
        }
        finally { mp4?.Dispose(); }
    }
}
