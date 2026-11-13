using StepRecorder.Export;

namespace StepRecorder.Tests;

/// <summary>Stapel-Export (13, Paket 38).</summary>
public class BatchExportTests
{
    [Fact]
    public void Plan_SubfoldersPerLanguage()
    {
        var jobs = BatchExport.Plan(new[] { "pdf", "docx" }, new[] { "de", "en" }, @"C:\out");
        Assert.Equal(4, jobs.Count);
        Assert.Contains(new BatchJob("pdf", "en", Path.Combine(@"C:\out", "en")), jobs);
        Assert.All(BatchExport.Plan(new[] { "pdf" }, new[] { "de" }, @"C:\out"), j => Assert.Equal(@"C:\out", j.Folder));
    }

    [Fact]
    public void ParseList_RejectsUnknown()
    {
        Assert.Equal(new[] { "pdf", "docx" }, BatchExport.ParseList("PDF, docx", f => f is "pdf" or "docx"));
        Assert.Null(BatchExport.ParseList("pdf,xyz", f => f is "pdf"));
        Assert.Null(BatchExport.ParseList("", _ => true));
    }
}
