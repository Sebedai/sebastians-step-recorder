using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace StepRecorder.Export;

/// <summary>
/// Rastert PDF-Seiten mit dem in Windows eingebauten PDF-Renderer (<c>Windows.Data.Pdf</c>, offline, ohne Paketidentität) –
/// für die Seitenansicht im Editor, das Drucken und für Tests, die das erzeugte PDF wirklich öffnen (13, Paket 20, VE-24).
/// Fehlt der Renderer (z. B. Windows Server ohne Desktopdarstellung), ist <see cref="Available"/> falsch.
/// </summary>
public sealed class PdfRaster : IDisposable
{
    readonly PdfDocument _doc;
    readonly InMemoryRandomAccessStream _stream;

    PdfRaster(PdfDocument doc, InMemoryRandomAccessStream stream) { _doc = doc; _stream = stream; }

    static bool? _available;

    /// <summary>Ob der eingebaute Renderer auf diesem Rechner nutzbar ist (einmal geprüft).</summary>
    public static bool Available
    {
        get
        {
            if (_available is bool b) return b;
            if (Environment.GetEnvironmentVariable("STEPRECORDER_FORCE_NOPDF") == "1") return (_available = false).Value;
            try
            {
                using var r = Open(MinimalPdf());
                _available = r.PageCount == 1;
            }
            catch { _available = false; }
            return _available.Value;
        }
    }

    public int PageCount => (int)_doc.PageCount;

    /// <summary>Seitengröße in Punkt (1/72 Zoll).</summary>
    public (double w, double h) PageSize(int index)
    {
        using var p = _doc.GetPage((uint)index);
        // Windows.Data.Pdf liefert die Größe in DIP (1/96 Zoll)
        return (p.Size.Width * 72.0 / 96.0, p.Size.Height * 72.0 / 96.0);
    }

    /// <summary>Öffnet ein PDF aus dem Speicher. Wirft bei unlesbarem PDF.</summary>
    public static PdfRaster Open(byte[] pdf)
    {
        var task = Task.Run(async () =>
        {
            var ras = new InMemoryRandomAccessStream();
            using (var w = new DataWriter(ras)) { w.WriteBytes(pdf); await w.StoreAsync(); w.DetachStream(); }
            ras.Seek(0);
            var doc = await PdfDocument.LoadFromStreamAsync(ras);
            return new PdfRaster(doc, ras);
        });
        if (!task.Wait(30000)) throw new TimeoutException("PDF konnte nicht geöffnet werden.");
        return task.Result;
    }

    /// <summary>
    /// Rendert eine Seite als PNG mit <paramref name="pixelWidth"/> Bildpunkten Breite (Höhe passend), weißer Hintergrund.
    /// Läuft auf einem Pool-Thread; der Aufrufer darf auf jedem Thread sein.
    /// </summary>
    public byte[] RenderPng(int index, int pixelWidth)
    {
        var task = Task.Run(async () =>
        {
            using var page = _doc.GetPage((uint)index);
            double scale = pixelWidth / Math.Max(1.0, page.Size.Width);
            var opt = new PdfPageRenderOptions
            {
                DestinationWidth = (uint)Math.Max(1, pixelWidth),
                DestinationHeight = (uint)Math.Max(1, Math.Round(page.Size.Height * scale)),
                BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255),
                BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId
            };
            using var outStream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(outStream, opt);
            outStream.Seek(0);
            var bytes = new byte[outStream.Size];
            using var reader = new DataReader(outStream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)outStream.Size);
            reader.ReadBytes(bytes);
            return bytes;
        });
        if (!task.Wait(60000)) throw new TimeoutException("PDF-Seite konnte nicht gerendert werden.");
        return task.Result;
    }

    public void Dispose() { _stream.Dispose(); }

    /// <summary>Kleinstes gültiges PDF mit einer leeren Seite (für die Verfügbarkeitsprüfung).</summary>
    static byte[] MinimalPdf()
    {
        var pdf = new Formats.SimplePdf();
        return pdf.ToBytes();
    }
}
