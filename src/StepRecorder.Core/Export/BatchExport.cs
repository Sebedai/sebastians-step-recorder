namespace StepRecorder.Export;

/// <summary>Ein Auftrag eines Stapel-Exports: Format, Sprache, Zielordner.</summary>
public sealed record BatchJob(string Format, string Language, string Folder);

/// <summary>
/// Stapel-Export (13, Paket 38): mehrere Formate × Sprachen in einem Durchgang. Bei mehreren Sprachen bekommt jede einen
/// Unterordner (<c>de</c>, <c>en</c>), damit gleichnamige Dateien nicht kollidieren.
/// </summary>
public static class BatchExport
{
    public static readonly string[] GuideFormats = { "pdf", "docx", "html", "md", "pptx" };

    /// <summary>Liste „pdf,docx“ zerlegen; null bei unbekanntem Format oder leerer Liste.</summary>
    public static List<string>? ParseList(string? csv, Func<string, bool> known)
    {
        var items = (csv ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).Distinct().ToList();
        return items.Count == 0 || items.Any(i => !known(i)) ? null : items;
    }

    public static List<BatchJob> Plan(IReadOnlyList<string> formats, IReadOnlyList<string> languages, string folder)
    {
        var jobs = new List<BatchJob>();
        foreach (var lang in languages)
        {
            string dir = languages.Count > 1 ? Path.Combine(folder, lang) : folder;
            foreach (var f in formats) jobs.Add(new BatchJob(f, lang, dir));
        }
        return jobs;
    }
}
