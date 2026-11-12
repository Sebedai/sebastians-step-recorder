using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>Aufnahme-Bibliothek: Katalog, Umbenennen, .steprec packen/entpacken, Suche über alle Aufnahmen (T-15).</summary>
[Collection("Loc")]
public class LibraryTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-lib-" + Guid.NewGuid().ToString("N")[..8]);
    public LibraryTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    string New(string name, bool withSecret = false, bool withComment = false) => SyntheticSession.Create(Path.Combine(_root, name), withSecret: withSecret, withComment: withComment);

    // ---------------- Katalog ----------------

    [Fact]
    public void Catalog_FindsFoldersAndArchives_UsesOverlayTitle()
    {
        var a = New("a");
        Directory.CreateDirectory(Path.Combine(_root, "Projekt"));
        var b = SyntheticSession.Create(Path.Combine(_root, "Projekt", "b"));
        Directory.CreateDirectory(Path.Combine(_root, "export"));                 // kein Aufnahmeordner, wird übergangen
        SessionPackage.Pack(a, Path.Combine(_root, "a.steprec"));
        Assert.True(SessionEdits.SetTitle(b, "Neuer Titel"));

        var list = SessionCatalog.Scan(_root);
        Assert.Equal(3, list.Count);
        Assert.Single(list, x => x.Archive);
        var bi = list.Single(x => x.Path == b);
        Assert.Equal("Neuer Titel", bi.Title);
        Assert.True(bi.Edited);
        Assert.Equal("Datei im Editor speichern", list.Single(x => x.Path == a).Title);
        Assert.False(string.IsNullOrEmpty(bi.Id));
        // Ordnername bleibt
        Assert.True(Directory.Exists(b));
    }

    [Fact]
    public void Catalog_DetailsAndThumbnail_AreCached_AndRespectRedactions()
    {
        var a = New("a", withSecret: true);
        var d = SessionCatalog.Details(a)!;
        Assert.Equal(1, d.Unreviewed);
        Assert.True(File.Exists(Path.Combine(a, "cache", "catalog.json")));
        var png = SessionCatalog.Thumbnail(a);
        Assert.NotNull(png);
        Assert.True(File.Exists(Path.Combine(a, "cache", "thumbs", "list-192.png")));
        // jede Overlay-Änderung verwirft den Zwischenspeicher
        Assert.True(SessionEdits.SetTitle(a, "X"));
        Assert.False(Directory.Exists(Path.Combine(a, "cache")));
    }

    // ---------------- Packen und Entpacken ----------------

    static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

    [Fact]
    public void Package_RoundTrip_SameFiles_NoCacheOrExports()
    {
        var a = New("a");
        Directory.CreateDirectory(Path.Combine(a, "cache")); File.WriteAllText(Path.Combine(a, "cache", "x.txt"), "x");
        Directory.CreateDirectory(Path.Combine(a, "export")); File.WriteAllText(Path.Combine(a, "export", "y.html"), "y");
        Directory.CreateDirectory(Path.Combine(a, "history")); File.WriteAllText(Path.Combine(a, "history", "z.json"), "{}");
        File.WriteAllText(Path.Combine(a, "session.lock"), "{}");
        var file = Path.Combine(_root, "a.steprec");
        SessionPackage.Pack(a, file);
        using (var zip = ZipFile.OpenRead(file))
        {
            Assert.Equal("manifest.json", zip.Entries[0].FullName);
            Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("cache") || e.FullName.StartsWith("export") || e.FullName.StartsWith("history") || e.FullName == "session.lock");
        }
        var target = Path.Combine(_root, "entpackt");
        var dir = SessionPackage.Unpack(file, target);
        foreach (var rel in SessionPackage.PackList(a))
            Assert.Equal(Hash(Path.Combine(a, rel)), Hash(Path.Combine(dir, rel)));
        Assert.Equal(Session.Open(a, recover: false).SessionId, SessionPackage.FindExisting(target, Session.Open(dir, recover: false).SessionId) is { } ex ? Session.Open(ex, recover: false).SessionId : null);
        // zweites Entpacken: neuer Ordnername
        var dir2 = SessionPackage.Unpack(file, target);
        Assert.NotEqual(dir, dir2);
    }

    [Fact]
    public void Package_RemovesAbsoluteAppendPaths()
    {
        var a = New("a");
        using (var v = SessionView.Open(a))
        {
            v.Edits["guide"] = new JsonObject { ["append"] = new JsonArray(new JsonObject { ["path"] = @"C:\Users\Kanarie\Aufnahmen\b", ["rel"] = "b" }) };
            Assert.True(v.SaveEdits());
        }
        var file = Path.Combine(_root, "a.steprec");
        SessionPackage.Pack(a, file);
        using var zip = ZipFile.OpenRead(file);
        using var r = new StreamReader(zip.GetEntry("edits.json")!.Open());
        Assert.DoesNotContain("Kanarie", r.ReadToEnd());
    }

    static string Zip(string dir, params (string name, string content)[] entries)
    {
        var file = Path.Combine(dir, "bad-" + Guid.NewGuid().ToString("N")[..6] + ".steprec");
        using var fs = File.Create(file);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var e = zip.CreateEntry(name);
            using var s = e.Open();
            s.Write(Encoding.UTF8.GetBytes(content));
        }
        return file;
    }

    const string Man = "{\"format\":\"steprec\",\"schemaVersion\":\"1.0\",\"sessionId\":\"0190a3c4-0000-7000-8000-000000000001\",\"title\":\"T\"}";

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("frames/../../evil.png")]
    [InlineData("/abs.json")]
    [InlineData("C:/x/manifest2.json")]
    [InlineData("frames\\a.png")]
    [InlineData("evil.exe")]
    [InlineData("frames/CON")]
    [InlineData("assets/nul.png")]
    [InlineData("cache/x.json")]
    public void Package_Unpack_RejectsDangerousNames_LeavesNothing(string bad)
    {
        var src = Path.Combine(_root, "src"); Directory.CreateDirectory(src);
        var file = Zip(src, ("manifest.json", Man), ("events.jsonl", ""), (bad, "x"));
        var target = Path.Combine(_root, "ziel");
        Assert.Throws<PackageException>(() => SessionPackage.Unpack(file, target));
        Assert.True(!Directory.Exists(target) || !Directory.EnumerateFileSystemEntries(target).Any());
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
    }

    [Fact]
    public void Package_Unpack_EnforcesLimits_AndNeedsManifest()
    {
        var src = Path.Combine(_root, "src"); Directory.CreateDirectory(src);
        var target = Path.Combine(_root, "ziel");
        Assert.Throws<PackageException>(() => SessionPackage.Unpack(Zip(src, ("events.jsonl", "")), target));
        var many = Zip(src, new[] { ("manifest.json", Man), ("events.jsonl", "") }.Concat(Enumerable.Range(0, 20).Select(i => ($"frames/{i:00}.png", "x"))).ToArray());
        Assert.Throws<PackageException>(() => SessionPackage.Unpack(many, target, new UnpackLimits(MaxEntries: 10)));
        var big = Zip(src, ("manifest.json", Man), ("events.jsonl", new string('a', 5000)));
        Assert.Throws<PackageException>(() => SessionPackage.Unpack(big, target, new UnpackLimits(MaxEntryBytes: 1000)));
        Assert.Throws<PackageException>(() => SessionPackage.Unpack(big, target, new UnpackLimits(MaxTotalBytes: 2000)));
        Assert.True(!Directory.Exists(target) || !Directory.EnumerateFileSystemEntries(target).Any());
    }

    // ---------------- Suche (T-15) ----------------

    const string InternalCanary = "Interner-Kanarie-55";

    [Fact]
    public void Search_IndexContainsNoProtectedValues_FindsReviewedText()
    {
        var a = New("a", withSecret: true, withComment: true);
        using (var v = SessionView.Open(a))
        {
            long commentSeq = v.Session.Events.Single(e => e.Str("type") == "comment").Long("seq");
            var title = v.Session.Events.First(e => e.Str("type") == "window.title" && (e.Str("title") ?? "").Contains("notiz.txt"));
            long anchor = v.All.First(s => s.Kind == "click").Anchor;
            v.Edits["comments"] = new JsonArray(
                new JsonObject { ["id"] = "rc1", ["origin"] = "recording", ["sourceSeq"] = commentSeq, ["kind"] = "internal", ["hidden"] = true, ["by"] = "user" },
                new JsonObject { ["id"] = "uc1", ["anchor"] = anchor, ["kind"] = "internal", ["text"] = InternalCanary, ["by"] = "user", ["origin"] = "viewer" });
            v.Edits["substitutions"] = new JsonArray(new JsonObject { ["id"] = "sb1", ["match"] = new JsonObject { ["literal"] = "Unbenannt" }, ["placeholder"] = "Dokument", ["caseSensitive"] = false, ["scope"] = "all", ["targets"] = new JsonArray("text") });
            v.Edits["redactions"] = new JsonArray(new JsonObject { ["id"] = "r1", ["kind"] = "text", ["targets"] = new JsonArray(new JsonObject { ["seq"] = title.Long("seq"), ["field"] = "title", ["range"] = new JsonArray(0, 9) }) });
            Assert.True(v.SaveEdits());
        }
        var info = SessionCatalog.Read(a);
        var hits = new List<SearchHit>();
        SessionSearch.SearchAll(new[] { info }, SearchQuery.Parse("Editor"), hits.Add);
        Assert.NotEmpty(hits);
        Assert.True(SessionSearch.IndexValid(a, "de"));
        string Bytes() => string.Join("\n", Directory.EnumerateFiles(Path.Combine(a, "cache", "search"), "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        var idx = Bytes();
        foreach (var canary in new[] { "sk-abcdefghijklmnopqrstuvwxyz", SyntheticSession.CommentCanary, InternalCanary, "Unbenannt", "notiz.txt" })
        {
            Assert.DoesNotContain(canary, idx, StringComparison.OrdinalIgnoreCase);
            var h = new List<SearchHit>();
            SessionSearch.SearchAll(new[] { info }, SearchQuery.Parse(canary), h.Add);
            Assert.Empty(h);
        }
        // nach der Durchsicht („in Ordnung“) wird der getippte Text gefunden
        using (var v = SessionView.Open(a))
        {
            var typed = v.All.Single(s => s.Unreviewed);
            v.Edits["review"] = new JsonObject { ["inputs"] = new JsonArray(new JsonObject { ["anchor"] = typed.Anchor, ["status"] = "kept" }) };
            Assert.True(v.SaveEdits());
        }
        Assert.False(SessionSearch.IndexValid(a, "de"));
        var after = new List<SearchHit>();
        SessionSearch.SearchAll(new[] { SessionCatalog.Read(a) }, SearchQuery.Parse("sk-abcdefghijklmnopqrstuvwxyz"), after.Add);
        Assert.Single(after);
        Assert.Contains("«sk-abcdefghijklmnopqrstuvwxyz»", after[0].Snippet);
    }

    /// <summary>Regression: wörtliche Verallgemeinerungen wurden mit der NonBacktracking-Engine nie angewendet (Lookarounds).</summary>
    [Fact]
    public void Generalization_Literal_AppliesToTextsAndTitles()
    {
        var a = New("a");
        using (var v = SessionView.Open(a))
        {
            v.Edits["substitutions"] = new JsonArray(
                new JsonObject { ["id"] = "sb1", ["match"] = new JsonObject { ["literal"] = "Unbenannt" }, ["placeholder"] = "Dokument" },
                new JsonObject { ["id"] = "sb2", ["match"] = new JsonObject { ["pattern"] = @"(?<=notiz)\.txt" }, ["placeholder"] = "Endung" });
            Assert.True(v.SaveEdits());
        }
        using (var v = SessionView.Open(a, headless: true))
        {
            Assert.Equal("{Dokument} – Editor", v.Clean("Unbenannt – Editor"));
            Assert.Equal("Unbenannte", v.Clean("Unbenannte"));                       // nur ganze Wörter
            Assert.Equal("notiz{Endung}", v.Clean("notiz.txt"));                     // Muster mit Lookbehind fällt nicht still weg
            var all = string.Join("\n", v.All.Select(s => s.Json.ToJsonString() + s.Title + s.Guide + s.Protocol));
            Assert.DoesNotContain("Unbenannt", all);
        }
    }

    [Fact]
    public void Search_Snippet_MarksTerm()
    {
        Assert.Equal("Datei «speichern» unter", SessionSearch.Snippet("Datei speichern unter", "Speichern").Replace("«Speichern»", "«speichern»"));
        Assert.StartsWith("…", SessionSearch.Snippet(new string('a', 60) + " Ziel " + new string('b', 200), "Ziel"));
    }
}
