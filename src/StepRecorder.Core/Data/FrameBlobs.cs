using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace StepRecorder.Data;

/// <summary>Bild-Blobs unter <c>frames\xx\&lt;sha256&gt;.png</c>: schreiben, Verweise sammeln, Verwaiste löschen.</summary>
public static class FrameBlobs
{
    /// <summary>Vollbild als PNG (24 bit, wie die Aufnahme).</summary>
    public static byte[] EncodeKey(Bitmap bmp)
    {
        using var rgb = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(rgb)) g.DrawImageUnscaled(bmp, 0, 0);
        using var ms = new MemoryStream();
        rgb.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>Schreibt den Blob (falls neu) und gibt den Verweis <c>sha256:…</c> zurück.</summary>
    public static string Write(string folder, byte[] png)
    {
        string hex = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        string path = Path.Combine(folder, "frames", hex[..2], hex + ".png");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, png);
            File.Move(tmp, path, true);
        }
        return "sha256:" + hex;
    }

    /// <summary>Alle von Bildereignissen verwendeten Blobs (Hex).</summary>
    public static HashSet<string> Referenced(IEnumerable<JsonObject> events)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in events)
            if (e.Str("type") == "frame" && e.Obj("store")?.Str("blob") is { } b)
                set.Add(b.StartsWith("sha256:") ? b[7..] : b);
        return set;
    }

    /// <summary>Löscht Blobs, die kein Bildereignis mehr verwendet. Gibt die Zahl gelöschter Dateien zurück.</summary>
    public static int DeleteUnreferenced(string folder, HashSet<string> keep)
    {
        var dir = Path.Combine(folder, "frames");
        if (!Directory.Exists(dir)) return 0;
        int n = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*.png", SearchOption.AllDirectories).ToList())
            if (!keep.Contains(Path.GetFileNameWithoutExtension(f)))
                try { File.Delete(f); n++; } catch { }
        foreach (var d in Directory.EnumerateDirectories(dir).ToList())
            try { if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { }
        return n;
    }
}
