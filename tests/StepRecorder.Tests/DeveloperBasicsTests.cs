using StepRecorder.Capture;

namespace StepRecorder.Tests;

/// <summary>Paket V des Entwickler-Fahrplans (12 §6): Abgleich Grundsätze ↔ Code.</summary>
public class DeveloperBasicsTests
{
    [Theory]
    [InlineData(@"C:\Users\Kanarie\AppData\Local\App\app.exe", @"%USERPROFILE%\AppData\Local\App\app.exe")]
    [InlineData(@"c:\users\kanarie\x.exe", @"%USERPROFILE%\x.exe")]
    [InlineData(@"""C:\Users\Kanarie\tool.exe"" --file C:\Users\Kanarie\a.txt", @"""%USERPROFILE%\tool.exe"" --file %USERPROFILE%\a.txt")]
    [InlineData(@"C:\Program Files\App\app.exe", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Users\Kanarie2\app.exe", @"C:\Users\Kanarie2\app.exe")]   // anderer Benutzer mit gleichem Anfang
    public void ProfilePath_ReplacesUserProfile(string input, string expected)
        => Assert.Equal(expected, Win32Detail.ProfilePath(input, @"C:\Users\Kanarie"));

    [Fact]
    public void Core_DoesNotReadForeignProcessMemory()
    {
        // 10 §2, P9: kein Lesen fremden Prozessspeichers – die Schnittstellen dürfen im Kern nicht mehr vorkommen
        var root = Path.Combine(FindRepoRoot(), "src");
        foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("ReadProcessMemory", text);
            Assert.DoesNotContain("VirtualAllocEx", text);
            Assert.DoesNotContain("WriteProcessMemory", text);
        }
    }

    /// <summary>Wurzel des Repositorys – wie in <see cref="LocalizationTests"/> über die Assembly-Metadaten „TestsDir“.</summary>
    internal static string FindRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string p = "")
    {
        string dir = typeof(DeveloperBasicsTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false).OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "TestsDir")?.Value is { Length: > 0 } d && Directory.Exists(d) ? d : Path.GetDirectoryName(p)!;
        return Path.GetFullPath(Path.Combine(dir, "..", ".."));
    }
}
