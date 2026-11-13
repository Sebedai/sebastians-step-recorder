using System.Security.Cryptography;
using System.Text.Json.Nodes;
using StepRecorder.Compare;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;

namespace StepRecorder.Tests;

/// <summary>„Abschnitt neu aufnehmen und ersetzen“ (V-25): Anhängen eines Abschnitts, Ersetzungen, Nahtstellen, Ausgaben.</summary>
public class ReRecordTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "steprec-rerec-" + Guid.NewGuid().ToString("N")[..8]);
    public ReRecordTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    string Target() => SyntheticSession.Create(Path.Combine(_root, "ziel-" + Guid.NewGuid().ToString("N")[..6]));
    /// <summary>Neuaufnahme wie vom Recorder: eigener Ordner unter .pending-section der Zielsitzung.</summary>
    string Source(string target, bool hotkey = true, bool comment = false)
        => SyntheticSession.Create(Path.Combine(SectionAppender.PendingRoot(target), "neu-" + Guid.NewGuid().ToString("N")[..6]), withHotkey: hotkey, withComment: comment);

    static List<long> Anchors(SessionView v) => v.All.Select(s => s.Anchor).ToList();

    /// <summary>Ersetzt den Tipp-Schritt („notiz.txt“) der Zielsitzung.</summary>
    static (PartRange range, List<ViewStep> covered) TypeRange(string target)
    {
        using var v = SessionView.Open(target, headless: true);
        var type = v.All.First(s => s.Kind == "type");
        var (range, covered, err) = v.ReplaceRangeFor(new[] { type });
        Assert.Null(err);
        return (range!, covered);
    }

    [Fact]
    public void Append_Replacement_KeepsOldBytes_ValidatesAndReplacesInView()
    {
        var target = Target();
        List<long> before; int beforeCount;
        using (var v0 = SessionView.Open(target, headless: true)) { before = Anchors(v0); beforeCount = v0.All.Count; }
        var oldBytes = File.ReadAllBytes(Path.Combine(target, "events.jsonl"));
        var (range, covered) = TypeRange(target);
        var source = Source(target);

        var res = SectionAppender.Append(target, source, "replacement", range);
        SectionAppender.DeleteSource(source);
        Assert.Equal(1, res.Part);
        Assert.True(res.CleanStop);
        Assert.Equal("rp1", res.ReplacementId);
        Assert.False(Directory.Exists(SectionAppender.PendingRoot(target)));

        // nur angehängt: die alten Bytes sind unverändert (AUF-102)
        var newBytes = File.ReadAllBytes(Path.Combine(target, "events.jsonl"));
        Assert.True(newBytes.Length > oldBytes.Length);
        Assert.Equal(SHA256.HashData(oldBytes), SHA256.HashData(newBytes[..oldBytes.Length]));

        // Formatprüfung (seq lückenlos, Verweise, Fenster, Epochen, Bilder, Prüfsummenkette, inSeq, Manifest)
        var (issues, _) = Validator.Validate(target, thorough: true);
        Assert.DoesNotContain(issues, i => i.Severity == "error");

        // Sitzungskopf und Manifest nennen denselben Bereich (AUF-107)
        using var s = Session.Open(target);
        var parts = SessionParts.Read(s);
        Assert.Equal(2, parts.Count);
        Assert.Equal("replacement", parts[1].Purpose);
        Assert.Equal(range, parts[1].Replaces);
        Assert.Equal(parts[0].TEnd, parts[1].TStart);          // Sitzungsuhr läuft fort
        var start1 = s.BySeq(parts[1].FirstSeq)!;
        Assert.Equal(s.SessionId, start1.Str("sessionId"));
        Assert.Null(start1["goal"]);
        var man = s.Manifest;
        Assert.Equal(2, man.Arr("parts")!.Count);
        Assert.Equal("replacement", man.Arr("parts")![1].Str("purpose"));
        Assert.Equal(range.FromSeq, man.Arr("parts")![1].Obj("replaces").Long("fromSeq"));
        var rep = s.LoadEdits()!.Arr("replacements")!.Single();
        Assert.Equal(1, rep.Obj("by").Int("section"));
        Assert.Equal(range.ToSeq, rep.Obj("replaces").Long("toSeq"));
        // Kennungen laufen fort: keine Bild- oder Fensterkennung doppelt
        Assert.Equal(s.Events.Count(e => e.Str("type") == "frame"), s.Events.Where(e => e.Str("type") == "frame").Select(e => e.Str("id")).Distinct().Count());
        Assert.Equal(s.Events.Count(e => e.Str("type") == "window.info"), s.Events.Where(e => e.Str("type") == "window.info").Select(e => e.Str("win")).Distinct().Count());
        // kein Differenzbild des neuen Abschnitts verweist in den alten
        foreach (var f in s.Events.Where(e => e.Str("type") == "frame" && e.Long("seq") >= parts[1].FirstSeq))
            foreach (var k in new[] { "base", "as" })
                if (f.Obj("store")?.Str(k) is { } b) Assert.True(s.Frame(b)!.Long("seq") >= parts[1].FirstSeq);

        using var v = SessionView.Open(target, headless: true);
        // ersetzt: der Tipp-Schritt fehlt in der wirksamen Folge, steht aber als „ersetzt“ bereit (VIE-181)
        foreach (var c in covered) Assert.DoesNotContain(v.All, x => x.Anchor == c.Anchor);
        Assert.All(v.Inactive.Where(x => x.Section == 0), x => Assert.Equal("replaced", x.State));
        Assert.Contains(v.Inactive, x => x.Kind == "type" && x.State == "replaced" && x.Number == 0);
        // die neuen Schritte stehen an der Stelle des ersten ersetzten
        int firstNew = v.All.FindIndex(x => x.Section == 1);
        int lastNew = v.All.FindLastIndex(x => x.Section == 1);
        Assert.True(firstNew > 0);
        Assert.True(v.All.Skip(firstNew).Take(lastNew - firstNew + 1).All(x => x.Section == 1));
        Assert.Contains(v.All, x => x.Section == 1 && x.Kind == "hotkey");
        Assert.True(v.All.Take(firstNew).All(x => x.Section == 0 && x.Anchor < range.FromSeq));
        Assert.True(v.All.Skip(lastNew + 1).All(x => x.Section == 0 && x.Anchor > range.ToSeq));
        // fortlaufend nummeriert
        Assert.Equal(Enumerable.Range(1, v.All.Count), v.All.Select(x => x.Number));
        // Nahtstellen: Beginn mit Zahl der ersetzten, Ende danach
        var start = v.All[firstNew].SeamBefore!;
        Assert.Equal("replacementStart", start.Kind);
        Assert.Equal(covered.Count, start.Replaced.Count);
        Assert.Equal("rp1", start.ReplacementId);
        if (lastNew < v.All.Count - 1) Assert.Equal("replacementEnd", v.All[lastNew + 1].SeamBefore!.Kind);
        Assert.Equal(v.All.Count(x => x.SeamBefore != null), v.Seams.Count);
        Assert.True(v.SeamBetween(v.All[firstNew - 1], v.All[firstNew]));
        Assert.True(v.IsNewlyRecorded(v.All[firstNew]));
        Assert.Equal(beforeCount - covered.Count + v.All.Count(x => x.Section == 1), v.All.Count);
        var (rc, uc, sc, dc, ec) = v.SectionStats();
        Assert.Equal(covered.Count, rc); Assert.Equal(0, uc); Assert.Equal(1, ec); Assert.Equal(0, dc); Assert.True(sc >= 1);
        _ = before;
    }

    [Fact]
    public void RemoveReplacement_RestoresOriginal_AndReapplyWorks()
    {
        var target = Target();
        List<long> before;
        using (var v0 = SessionView.Open(target, headless: true)) before = Anchors(v0);
        var (range, _) = TypeRange(target);
        var source = Source(target);
        SectionAppender.Append(target, source, "replacement", range);
        SectionAppender.DeleteSource(source);

        using var v = SessionView.Open(target, headless: true);
        // Bearbeitung am später ersetzten Schritt bleibt erhalten
        long typeAnchor = v.Inactive.First(x => x.Kind == "type").Anchor;
        v.Edits["steps"] = new JsonArray(new JsonObject { ["anchor"] = typeAnchor, ["text"] = new JsonObject { ["de"] = new JsonObject { ["title"] = "Eigener Titel" } } });
        Assert.True(v.SaveEdits());
        Assert.DoesNotContain(v.All, x => x.Anchor == typeAnchor);

        // Ersetzung aufheben (VIE-182): frühere Schritte gelten wieder, mit ihren Bearbeitungen; die Neuaufnahme ist „nicht verwendet“
        Assert.True(SectionEdits.RemoveReplacement(v.Edits, "rp1"));
        Assert.True(v.SaveEdits());
        Assert.Equal(before, Anchors(v));
        Assert.Equal("Eigener Titel", v.All.First(x => x.Anchor == typeAnchor).AuthorTitle);
        Assert.Empty(v.Seams);
        Assert.NotEmpty(v.Inactive);
        Assert.All(v.Inactive, x => { Assert.Equal("unused", x.State); Assert.Equal(1, x.Section); });
        Assert.Single(v.UnusedReplacementSections());
        Assert.Null(v.Edits["replacements"]);

        // erneut einsetzen – Bereich aus dem Sitzungskopf
        var part = v.UnusedReplacementSections().Single();
        SectionEdits.AddReplacement(v.Edits, part.Replaces!, part.Part);
        Assert.True(v.SaveEdits());
        Assert.DoesNotContain(v.All, x => x.Anchor == typeAnchor);
        Assert.Contains(v.All, x => x.Section == 1);
        Assert.Empty(v.UnusedReplacementSections());
    }

    [Fact]
    public void Exports_UseEffectiveSequence_AndShowSeams()
    {
        var target = Target();
        var (range, covered) = TypeRange(target);
        var source = Source(target);
        SectionAppender.Append(target, source, "replacement", range);
        SectionAppender.DeleteSource(source);
        using var v = SessionView.Open(target, headless: true);
        var typed = covered.First(c => c.Kind == "type");

        // Anleitung (EXP-160): neue Schritte, keine ersetzten
        var md = Path.Combine(_root, "out", "a.md");
        GuideExport.Export(v, new GuideOptions { Format = "md", OutPath = md, Importance = "all" });
        var text = File.ReadAllText(md);
        Assert.DoesNotContain(v.GuideSteps("all"), x => x.Anchor == typed.Anchor);
        Assert.All(v.GuideSteps("all").Where(x => !x.Manual), x => Assert.Equal("active", x.State));
        Assert.Contains(v.All.First(x => x.Section == 1 && x.Kind == "hotkey").Title, text);

        // KI-Paket: Nahtstellen im Ablauf und in der Gliederung, Zählung im Kopf und in ai-package.json
        var dir = Path.Combine(_root, "ki");
        KiPackage.Export(v, new KiOptions { OutDir = dir, Detail = "standard" });
        var trace = File.ReadAllText(Path.Combine(dir, "trace.md"));
        Assert.Contains("> Nahtstelle – Beginn einer Neuaufnahme", trace);
        Assert.Contains("Aufnahmeabschnitte: ", trace);
        Assert.Contains(typed.Id, trace);
        Assert.DoesNotContain("## Ersetzter Schritt", trace);
        var kman = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "ai-package.json")))!;
        Assert.Equal(1, kman.Obj("replacements").Int("count"));
        Assert.Equal(covered.Count, kman.Obj("replacements").Int("replacedSteps"));
        var ksteps = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "steps.json")))!;
        Assert.DoesNotContain(ksteps.Arr("steps")!, x => x.Str("id") == typed.Id);
        // „Vollständig“ zeigt die ersetzten Schritte als eigene Blöcke ohne Nummer
        var full = KiPackage.Trace(v, v.Effective(importance: "all"), new(), "de", "full");
        Assert.Contains("## Ersetzter Schritt " + typed.Id, full);
        var en = KiPackage.Trace(v, v.Effective(importance: "all"), new(), "en", "standard");
        Assert.Contains("> Seam – start of a re-recording", en);

        // Vergleich: arbeitet auf der wirksamen Folge
        using var orig = SessionView.Open(Target(), headless: true);
        var cmp = StepCompare.Compare(orig, v);
        Assert.NotNull(cmp);
    }

    [Fact]
    public void Append_Continuation_GivesContinuationSeam_AllActive()
    {
        var target = Target();
        int n0; using (var v0 = SessionView.Open(target, headless: true)) n0 = v0.All.Count;
        var source = Source(target, hotkey: false);
        var res = SectionAppender.Append(target, source, "append");
        Assert.Null(res.ReplacementId);
        var (issues, _) = Validator.Validate(target, thorough: true);
        Assert.DoesNotContain(issues, i => i.Severity == "error");
        using var v = SessionView.Open(target, headless: true);
        Assert.Empty(v.Inactive);
        Assert.True(v.All.Count > n0);
        var seam = Assert.Single(v.Seams);
        Assert.Equal("continuation", seam.Kind);
        Assert.Equal(1, seam.Next!.Section);
        Assert.Equal(0, seam.PrevSection);
    }

    [Fact]
    public void CrashedReplacement_GivesNoEntry_AndIsUnused()
    {
        var target = Target();
        var (range, _) = TypeRange(target);
        var source = Source(target);
        // wie nach einem Absturz: Ende mit Grund „recovered“ (Wiederherstellung)
        var journal = Path.Combine(source, "events.jsonl");
        var lines = File.ReadAllLines(journal).ToList();
        int stopIdx = lines.FindIndex(l => l.Contains("\"type\":\"session.stop\""));
        lines[stopIdx] = lines[stopIdx].Replace("\"reason\":\"user\"", "\"reason\":\"recovered\"");
        File.WriteAllText(journal, string.Join("\n", lines) + "\n");
        var res = SectionAppender.Append(target, source, "replacement", range);
        Assert.False(res.CleanStop);
        Assert.Null(res.ReplacementId);
        using var v = SessionView.Open(target, headless: true);
        Assert.Null(v.Edits["replacements"]);
        Assert.Contains(v.All, x => x.Kind == "type");
        Assert.NotEmpty(v.Inactive);
        Assert.All(v.Inactive, x => Assert.Equal("unused", x.State));
    }

    [Fact]
    public void Precheck_RefusesUnfinishedOrBrokenSessions_WithoutWriting()
    {
        var target = Target();
        var man = Path.Combine(target, "manifest.json");
        var journal = Path.Combine(target, "events.jsonl");
        var m = Json.ReadObject(man)!;
        // laufend / abgestürzt
        m["state"] = "recording"; File.WriteAllText(man, Json.Pretty(m));
        var before = File.ReadAllBytes(journal);
        var source = Source(target);
        Assert.NotNull(SectionAppender.Precheck(target));
        Assert.Throws<InvalidOperationException>(() => SectionAppender.Append(target, source, "append"));
        Assert.Equal(before, File.ReadAllBytes(journal));
        // Journal passt nicht zum Manifest
        m["state"] = "complete"; m["journal"]!["lastSeq"] = 3; File.WriteAllText(man, Json.Pretty(m));
        Assert.NotNull(SectionAppender.Precheck(target));
        // gepackte Sitzung
        Assert.NotNull(SectionAppender.Precheck(target + ".steprec"));
    }

    [Fact]
    public void ReplacementRules_InvalidEntriesHaveNoEffect()
    {
        var target = Target();
        var (range, _) = TypeRange(target);
        var source = Source(target);
        SectionAppender.Append(target, source, "replacement", range);
        using var v = SessionView.Open(target, headless: true);
        var effective = Anchors(v);
        // zweiter Eintrag für denselben Ersatzabschnitt, überlappender Bereich, unbekannter Abschnitt: alle ohne Wirkung (F22)
        var arr = (JsonArray)v.Edits["replacements"]!;
        arr.Add(new JsonObject { ["id"] = "rp2", ["replaces"] = range.ToJson(), ["by"] = new JsonObject { ["section"] = 1 } });
        arr.Add(new JsonObject { ["id"] = "rp3", ["replaces"] = new PartRange(0, 1, 3).ToJson(), ["by"] = new JsonObject { ["section"] = 7 } });
        v.Rebuild();
        Assert.Equal(effective, Anchors(v));
        Assert.Equal(new[] { true, false, false }, v.Replacements.OrderBy(r => r.Id).Select(r => r.Effective));
        // verworfener Ersatzabschnitt (edits.sections): Ersetzung wirkungslos, seine Schritte erscheinen nirgends
        v.Edits["sections"] = new JsonArray(new JsonObject { ["section"] = 1, ["discarded"] = true });
        v.Rebuild();
        Assert.DoesNotContain(v.All, x => x.Section == 1);
        Assert.DoesNotContain(v.Inactive, x => x.Section == 1);
        Assert.Contains(v.All, x => x.Kind == "type");
        Assert.Contains(1, v.DiscardedSections);
    }

    [Fact]
    public void Selection_MustBeOneSection_AndNotOverlapExisting()
    {
        var target = Target();
        var (range, _) = TypeRange(target);
        var source = Source(target);
        SectionAppender.Append(target, source, "replacement", range);
        using var v = SessionView.Open(target, headless: true);
        var a = v.All.First(x => x.Section == 0);
        var b = v.All.First(x => x.Section == 1);
        Assert.NotNull(v.ReplaceRangeFor(new[] { a, b }).error);
        Assert.NotNull(v.ReplaceRangeFor(new[] { v.Inactive.First() }).error);
        // ein Teil der Neuaufnahme lässt sich erneut ersetzen (Kette)
        var (r2, _, err) = v.ReplaceRangeFor(new[] { b });
        Assert.Null(err);
        Assert.Equal(1, r2!.Section);
    }

    [Fact]
    public void ChainedReplacement_ReplacesPartOfTheReplacement()
    {
        var target = Target();
        var (range, _) = TypeRange(target);
        var s1 = Source(target);
        SectionAppender.Append(target, s1, "replacement", range);
        SectionAppender.DeleteSource(s1);
        PartRange r2;
        long hotkeyAnchor;
        using (var v = SessionView.Open(target, headless: true))
        {
            var hk = v.All.First(x => x.Section == 1 && x.Kind == "hotkey");
            hotkeyAnchor = hk.Anchor;
            r2 = v.ReplaceRangeFor(new[] { hk }).range!;
        }
        var s2 = Source(target, hotkey: false, comment: true);
        var res = SectionAppender.Append(target, s2, "replacement", r2);
        Assert.Equal(2, res.Part);
        var (issues, _) = Validator.Validate(target, thorough: true);
        Assert.DoesNotContain(issues, i => i.Severity == "error");
        using var v2 = SessionView.Open(target, headless: true);
        Assert.DoesNotContain(v2.All, x => x.Anchor == hotkeyAnchor);
        Assert.Contains(v2.All, x => x.Section == 2);
        Assert.Contains(v2.All, x => x.Section == 1);
        Assert.True(v2.Replacements.All(r => r.Effective));
        // Abschnitt-2-Schritte stehen innerhalb des Ersatzblocks von Abschnitt 1
        int i2 = v2.All.FindIndex(x => x.Section == 2);
        Assert.True(v2.All.Take(i2).Any(x => x.Section == 1) || v2.All[i2].SeamBefore!.Kind == "replacementStart");
        // Ersetzung 1 aufheben: Abschnitt 1 nicht verwendet → Ersetzung 2 liegt in einer nicht verwendeten Neuaufnahme und ruht
        SectionEdits.RemoveReplacement(v2.Edits, "rp1");
        v2.Rebuild();
        Assert.DoesNotContain(v2.All, x => x.Section > 0);
        Assert.Contains(v2.Replacements, r => r.Id == "rp2" && !r.Effective && r.Problem == "inUnused");
    }

    [Fact]
    public void ReplacedSecret_StillNeedsReview_AndNeverLeaks()
    {
        var target = SyntheticSession.Create(Path.Combine(_root, "geheim"), withSecret: true);
        var (range, _) = TypeRange(target);
        var source = Source(target);
        SectionAppender.Append(target, source, "replacement", range);
        using var v = SessionView.Open(target, headless: true);
        var replaced = v.Inactive.Single(x => x.Kind == "type");
        Assert.True(replaced.Unreviewed);
        Assert.True(v.UnreviewedCount >= 1);                  // Durchsicht gilt unabhängig vom Zustand
        var full = KiPackage.Trace(v, v.Effective(importance: "all"), new(), "de", "full");
        Assert.Contains("## Ersetzter Schritt " + replaced.Id, full);
        Assert.DoesNotContain("abcdefghijklmnop", full);
        Assert.Throws<SanitizeException>(() => SessionSanitizer.CreateCopy(target, Path.Combine(_root, "kopie")));
    }

    [Fact]
    public void Mcp_And_Quality_ShowSeams()
    {
        var target = Target();
        var (range, covered) = TypeRange(target);
        var source = Source(target);
        SectionAppender.Append(target, source, "replacement", range);
        SectionAppender.DeleteSource(source);
        using var v = SessionView.Open(target, headless: true);
        // Qualitätsprüfung: Hinweis Q18 am ersten neuen Schritt
        var q = QualityCheck.Run(v, new QualityOptions { Importance = "all" });
        Assert.Contains(q, f => f.Rule == "Q18" && f.Key == "replacementStart");
        // MCP: Gliederung mit Nahtstellen-Eintrag und Zählung
        var mcp = new Mcp.StepRecorderMcp(new Mcp.McpOptions { Root = Path.GetDirectoryName(target)! });
        var server = new Mcp.McpStdioServer("steprecorder", "test", Mcp.StepRecorderMcp.Instructions, TextReader.Null, TextWriter.Null, TextWriter.Null);
        mcp.Register(server);
        var r = server.Handle(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = "get_session", ["arguments"] = new JsonObject { ["sessionId"] = v.Session.SessionId } } })!.ToJsonString();
        Assert.Contains("replacementStart", r);
        Assert.Contains(covered[0].Id, r);
        Assert.Contains("replacedSteps", r);
    }

    [Fact]
    public void SectionOptions_TakeReferenceSettings_UnionExclusions_AndScopeAtStep()
    {
        var target = Target();
        // Bezugsabschnitt: Schutznetz, eigener Ausschluss, Texteingaben aus; Aufnahmebereich „eine Anwendung“
        var man = Path.Combine(target, "manifest.json");
        var m = Json.ReadObject(man)!;
        m["settings"] = new JsonObject
        {
            ["language"] = "en",
            ["privacy"] = new JsonObject { ["level"] = "balanced", ["excluded"] = new JsonArray(new JsonObject { ["process"] = "alt.exe" }) },
            ["capture"] = new JsonObject { ["changeFrames"] = "few", ["appContext"] = false },
            ["input"] = new JsonObject { ["typing"] = false, ["clicks"] = true }
        };
        File.WriteAllText(man, Json.Pretty(m));
        var journal = Path.Combine(target, "events.jsonl");
        File.WriteAllText(journal, File.ReadAllText(journal).Replace("\"mode\":\"screen\",\"monitors\":\"all\"", "\"mode\":\"app\",\"apps\":[{\"process\":\"notepad.exe\",\"pid\":10,\"match\":\"window\"}],\"includeRelated\":false"));
        using var s = Session.Open(target);
        var current = new Capture.RecordingOptions { PrivacyLevel = "strict", SettingsPrivacyLevel = "strict", ExcludedProcesses = new List<string> { "neu.exe" } };
        var o = Capture.SectionOptions.For(s, 0, s.Events.Last(e => e.Str("type") == "key.down").Long("seq"), current);
        Assert.Equal("balanced", o.PrivacyLevel);
        Assert.Contains("privacyLevel", o.Weakens(o.SettingsPrivacyLevel!));   // schwächer als die Einstellungen → Startmeldung
        Assert.Contains("neu.exe", o.ExcludedProcesses);                       // Ausschlussliste kann nur wachsen
        Assert.Contains("alt.exe", o.ExcludedProcesses);
        Assert.Contains("typing", o.Off);
        Assert.DoesNotContain("clicks", o.Off);
        Assert.Equal("few", o.ChangeFrames);
        Assert.False(o.AppContext);
        Assert.Equal("en", o.Language);
        Assert.Equal(Capture.ScopeMode.App, o.Scope);
        Assert.Equal("notepad.exe", o.AppProcess);
        Assert.Null(o.AppPid);
        Assert.False(o.IncludeRelated);
        Assert.Null(o.GoalJson());
    }

    [Fact]
    public void GuideOrder_PutsReplacementWhereReplacedStepWas()
    {
        var target = Target();
        var (range, _) = TypeRange(target);
        // Reihenfolge des Autors: letzter Schritt zuerst, sonst zeitlich
        using (var v0 = SessionView.Open(target, headless: true))
        {
            var order = v0.All.Select(s => s.Anchor).ToList();
            var last = order[^1]; order.RemoveAt(order.Count - 1); order.Insert(0, last);
            v0.Edits["order"] = new JsonArray(order.Select(a => (JsonNode)a).ToArray());
            Assert.True(v0.SaveEdits());
        }
        var source = Source(target);
        SectionAppender.Append(target, source, "replacement", range);
        using var v = SessionView.Open(target, headless: true);
        var ordered = v.Effective(importance: "all", ordered: true);
        int iNew = ordered.FindIndex(x => x.Section == 1);
        Assert.True(iNew > 0 && iNew < ordered.Count - 1);   // nicht ans Ende gerutscht
        Assert.Equal(v.All[^1].Anchor, ordered[0].Anchor);
    }
}
