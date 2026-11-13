using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

/// <summary>Ein Block der Seitenansicht: Schritt (<c>s:</c>) oder Kapitelbeginn (<c>c:</c>), Seite (0-basiert) und Oberkante in Punkt von oben.</summary>
public sealed record PdfBlock(string Key, long Anchor, bool Foreign, int Page, double Top);

/// <summary>Ergebnis der Seitenansicht: PDF im Speicher, Seitengröße, Lage der Blöcke, Hinweise.</summary>
public sealed record PdfPreview(byte[] Pdf, int PageCount, double PageW, double PageH, IReadOnlyList<PdfBlock> Blocks, IReadOnlyList<string> Warnings, int Unreviewed)
{
    /// <summary>Block an einer Stelle der Seite (Punkt von oben) – der letzte, der darüber beginnt.</summary>
    public PdfBlock? BlockAt(int page, double y) => Blocks.Where(b => b.Page < page || (b.Page == page && b.Top <= y + 2)).OrderBy(b => b.Page).ThenBy(b => b.Top).LastOrDefault();
}

/// <summary>
/// Bildspeicher der Seitenansicht (13, Paket 20): gerenderte Schrittbilder, nur im Arbeitsspeicher, nie auf der Platte. Der Schlüssel enthält
/// alles, was die Pixel bestimmt – auch einen Fingerabdruck aller Schwärzungen, Verallgemeinerungen und der Durchsicht; ändert sich einer davon,
/// passt kein alter Eintrag mehr (10 §10.6).
/// </summary>
public sealed class GuideImageCache
{
    readonly Dictionary<string, (byte[]? png, int w, int h, RectangleF? hot, byte[]? after, int aw, int ah)> _items = new();
    long _bytes;
    /// <summary>Obergrenze; darüber wird der Speicher geleert.</summary>
    public long MaxBytes { get; set; } = 400L << 20;
    public int Count { get { lock (_items) return _items.Count; } }
    public int Hits, Misses;

    public void Clear() { lock (_items) { _items.Clear(); _bytes = 0; } }

    internal string KeyFor(SessionView v, ViewStep st, string variant, string renderView, MouseMarks mask, bool overview, string profile, bool after, bool tour, string? label = null)
    {
        var sb = new StringBuilder();
        sb.Append(label).Append('|');
        sb.Append(v.Session.Path).Append('|').Append(v.Language).Append('|').Append(st.Anchor).Append('|').Append(st.MergeId).Append('|').Append(variant).Append('|').Append(renderView)
          .Append('|').Append(mask).Append('|').Append(overview).Append('|').Append(profile).Append('|').Append(after).Append('|').Append(tour)
          .Append('|').Append(st.PreFrame).Append('|').Append(st.PostFrame).Append('|').Append(v.CropFor(st)).Append('|').Append(v.MarksFor(st))
          .Append('|').Append(st.Redacted).Append('|').Append(st.Unreviewed).Append('|').Append(st.Kind);
        foreach (var f in new[] { st.PreFrame, st.PostFrame })
            if (f != null) foreach (var a in v.AnnotationsFor(st, f)) sb.Append('|').Append(a.ToJsonString());
        sb.Append('|').Append(v.Edits["redactions"]?.ToJsonString()).Append('|').Append(v.Edits["substitutions"]?.ToJsonString())
          .Append('|').Append(v.Edits["review"]?.ToJsonString()).Append('|').Append(v.Edits["guide"]?["auto"]?.ToJsonString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    internal bool TryGet(string key, out (byte[]? png, int w, int h, RectangleF? hot, byte[]? after, int aw, int ah) v)
    {
        lock (_items)
        {
            bool ok = _items.TryGetValue(key, out v);
            if (ok) Hits++; else Misses++;
            return ok;
        }
    }

    internal void Put(string key, (byte[]? png, int w, int h, RectangleF? hot, byte[]? after, int aw, int ah) v)
    {
        lock (_items)
        {
            long size = (v.png?.Length ?? 0) + (v.after?.Length ?? 0);
            if (_bytes + size > MaxBytes) { _items.Clear(); _bytes = 0; }
            _items[key] = v; _bytes += size;
        }
    }
}

public static partial class GuideExport
{
    /// <summary>
    /// PDF der Anleitung im Speicher für die Seitenansicht und das Drucken (13, Paket 20): derselbe Aufbau wie der PDF-Export, dazu die Lage
    /// jedes Schritts und Kapitels. Wirft nicht wegen ungeprüfter Eingaben, wenn die Ansicht ohne Oberfläche geöffnet ist – dann stehen dort
    /// Platzhalter (<see cref="PdfPreview.Unreviewed"/> nennt die Zahl).
    /// </summary>
    public static PdfPreview RenderPdf(SessionView view, GuideOptions o)
    {
        string lang = o.Language ?? view.Session.Language;
        bool fallbackBefore = view.AuthorTextFallback;
        string langBefore = view.Language;
        view.AuthorTextFallback = o.MissingTranslation == "other";
        view.Language = lang; view.Rebuild();
        try
        {
            int unreviewed = view.UnreviewedCount;
            if (unreviewed > 0 && !view.Headless)
                throw new InvalidOperationException(lang == "de" ? $"{unreviewed} auffällige Eingaben sind noch nicht durchgesehen (Durchsicht der Eingaben)." : $"{unreviewed} flagged inputs are not reviewed yet.");
            o.Format = "pdf";
            GuidePresets.Apply(o.Preset, o, null);
            var doc = Build(view, o, lang);
            var pdf = PdfDocument(doc);
            var bytes = pdf.ToBytes();
            var blocks = new List<PdfBlock>();
            for (int i = 0; i < doc.Blocks.Count; i++)
            {
                switch (doc.Blocks[i])
                {
                    case Item it when pdf.Destinations.TryGetValue(it.Anchor, out var dst):
                        blocks.Add(new PdfBlock("s:" + it.Step.Anchor, it.Step.Anchor, doc.Foreign.Contains(it), dst.page, Math.Max(0, pdf.PageH - dst.top)));
                        break;
                    case Chapter c when pdf.Destinations.TryGetValue(c.Anchor, out var cd):
                        var next = doc.Blocks.Skip(i + 1).OfType<Item>().FirstOrDefault();
                        if (next != null) blocks.Add(new PdfBlock("c:" + next.Step.Anchor, next.Step.Anchor, doc.Foreign.Contains(next), cd.page, Math.Max(0, pdf.PageH - cd.top)));
                        break;
                }
            }
            return new PdfPreview(bytes, pdf.PageCount, pdf.PageW, pdf.PageH, blocks, pdf.Warnings.ToList(), unreviewed);
        }
        finally { view.AuthorTextFallback = fallbackBefore; view.Language = langBefore; view.Rebuild(); }
    }
}
