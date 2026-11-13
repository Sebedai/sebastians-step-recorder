using System.Text.Json.Nodes;
using StepRecorder.Data;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Nahtstelle zwischen zwei aufeinanderfolgenden Schritten der wirksamen Folge aus verschiedenen Abschnitten (05 §16.4).
/// <c>Kind</c>: replacementStart | replacementEnd | continuation.
/// </summary>
public sealed class Seam
{
    public string Kind = "";
    /// <summary>Abschnitt des Schritts hinter der Naht.</summary>
    public int Section;
    /// <summary>Abschnitt des Schritts vor der Naht (bei replacementStart der ersetzte Abschnitt).</summary>
    public int PrevSection;
    /// <summary>Wanduhr-Beginn des Abschnitts hinter der Naht (UTC, ISO).</summary>
    public string? RecordedUtc;
    /// <summary>Bei continuation: Ende des vorigen Abschnitts (UTC, ISO).</summary>
    public string? PrevEndUtc;
    public string? ReplacementId;
    /// <summary>Bei replacementStart: die ersetzten Schritte (alle Wichtigkeiten, zeitlich).</summary>
    public List<ViewStep> Replaced = new();
    /// <summary>Der Schritt hinter der Naht (null, wenn sie am Ende steht).</summary>
    public ViewStep? Next;
}

/// <summary>Eintrag aus <c>edits.replacements[]</c> mit Prüfergebnis (F22 und 05 §16.1).</summary>
public sealed record ReplacementInfo(string Id, PartRange Replaces, int BySection, string? AtUtc, bool Effective, string? Problem);

public sealed partial class SessionView
{
    /// <summary>Aufnahmeabschnitte der Sitzung (aus dem Journal).</summary>
    public List<SessionPart> Sections { get; private set; } = new();
    /// <summary>Verworfene Abschnitte (edits.sections vor session.stop „discarded“); ihre Schritte liefert die Leseschicht nicht.</summary>
    public HashSet<int> DiscardedSections { get; private set; } = new();
    /// <summary>Ersetzte und nicht verwendete Schritte (Zustand „replaced“/„unused“, ohne Schrittnummer) – nur für den Editor.</summary>
    public List<ViewStep> Inactive { get; private set; } = new();
    /// <summary>Nahtstellen der wirksamen Folge in ihrer Reihenfolge.</summary>
    public List<Seam> Seams { get; private set; } = new();
    /// <summary>Alle Einträge aus edits.replacements mit Prüfergebnis.</summary>
    public List<ReplacementInfo> Replacements { get; private set; } = new();

    readonly Dictionary<string, List<ViewStep>> _blocks = new();
    readonly Dictionary<long, string> _blockOf = new();
    readonly Dictionary<string, List<ViewStep>> _replaced = new();

    public SessionPart? SectionInfo(int section) => Sections.FirstOrDefault(p => p.Part == section);
    /// <summary>Wirksame Schritte, die eine Ersetzung an die Stelle der alten setzt (samt verschachtelter Ersetzungen).</summary>
    public List<ViewStep> BlockOf(string replacementId) => _blocks.TryGetValue(replacementId, out var b) ? b : new List<ViewStep>();
    public bool IsReplacementSection(int section) => SectionInfo(section)?.Purpose == "replacement";
    /// <summary>Schritt aus einer Neuaufnahme (im Editor „neu aufgenommen“).</summary>
    public bool IsNewlyRecorded(ViewStep s) => !s.Manual && IsReplacementSection(s.Section);
    public ViewStep? FindAny(string id) => ById(id) ?? Inactive.FirstOrDefault(s => s.Id == id);
    /// <summary>Wirksame Ersetzung, zu der ein Schritt gehört (als ersetzter Schritt oder als Teil des Ersatzes).</summary>
    public ReplacementInfo? ReplacementOf(ViewStep s) => s.ReplacementId == null ? null : Replacements.FirstOrDefault(r => r.Id == s.ReplacementId);
    /// <summary>Neuaufnahmen, die derzeit nicht verwendet werden und sich (wieder) einsetzen lassen.</summary>
    public List<SessionPart> UnusedReplacementSections()
        => Sections.Where(p => p.Purpose == "replacement" && p.Replaces != null && !DiscardedSections.Contains(p.Part) && !Replacements.Any(r => r.Effective && r.BySection == p.Part)).ToList();

    void ApplySections(List<ViewStep> list)
    {
        _blocks.Clear(); _blockOf.Clear(); _replaced.Clear();
        Sections = SessionParts.Read(Session);
        // verworfene Abschnitte: Eintrag in edits.sections vor dem Grund im Journal (04 §13.16); Abschnitt 0 nie
        var discarded = Sections.Where(p => p.Part != 0 && p.StoppedAsDiscarded).Select(p => p.Part).ToHashSet();
        foreach (var e in (Edits.Arr("sections") ?? new JsonArray()).OfType<JsonObject>())
        {
            int sec = e.Int("section");
            if (sec == 0 || e["discarded"] == null) continue;
            if (e.Bool("discarded")) discarded.Add(sec); else discarded.Remove(sec);
        }
        DiscardedSections = discarded;
        foreach (var s in list) { s.Section = SectionOfSeq(s.Anchor); s.State = "active"; s.SeamBefore = null; s.ReplacementId = null; }
        var visible = list.Where(s => !discarded.Contains(s.Section)).ToList();

        if (Sections.Count <= 1 && (Edits.Arr("replacements")?.Count ?? 0) == 0)
        {
            Replacements = new(); Inactive = new(); Seams = new();
            SetNumbers(visible);
            return;
        }

        // Ersetzungen prüfen – aufsteigend nach Ersatzabschnitt, damit Ketten eindeutig sind (05 §16.1)
        var infos = new List<ReplacementInfo>();
        var raw = (Edits.Arr("replacements") ?? new JsonArray()).OfType<JsonObject>()
            .Select((o, i) => (o, i, by: o.Obj("by")?.Int("section", -1) ?? -1)).OrderBy(x => x.by).ThenBy(x => x.i).ToList();
        var used = new HashSet<int>();
        var seenBy = new HashSet<int>();
        var effective = new List<ReplacementInfo>();
        foreach (var (o, _, by) in raw)
        {
            var rp = PartRange.From(o.Obj("replaces"));
            string id = o.Str("id") ?? "";
            string? problem = null;
            var rPart = rp == null ? null : SectionInfo(rp.Section);
            var bPart = SectionInfo(by);
            if (rp == null || rPart == null || bPart == null) problem = "unknownSection";
            else if (by <= rp.Section) problem = "order";
            else if (bPart.Purpose != "replacement") problem = "notReplacement";
            else if (seenBy.Contains(by)) problem = "duplicate";
            else if (rp.FromSeq > rp.ToSeq || !rPart.Contains(rp.FromSeq) || !rPart.Contains(rp.ToSeq)) problem = "range";
            else if (effective.Any(x => x.Replaces.Overlaps(rp))) problem = "overlap";
            else if (discarded.Contains(by) || discarded.Contains(rp.Section)) problem = "discarded";
            else if (rPart.Purpose == "replacement" && !used.Contains(rPart.Part)) problem = "inUnused";
            if (by >= 0) seenBy.Add(by);
            var info = new ReplacementInfo(id, rp ?? new PartRange(-1, 0, 0), by, o.Str("atUtc"), problem == null, problem);
            infos.Add(info);
            if (problem == null) { effective.Add(info); used.Add(by); }
        }
        Replacements = infos;

        var groups = visible.GroupBy(s => s.Section).ToDictionary(g => g.Key, g => g.ToList());
        List<object> Expand(int section)
        {
            var items = new List<object>();
            var steps = groups.GetValueOrDefault(section) ?? new List<ViewStep>();
            var reps = effective.Where(r => r.Replaces.Section == section).OrderBy(r => r.Replaces.FromSeq).ToList();
            var done = new HashSet<string>();
            void Insert(ReplacementInfo r, List<ViewStep> replacedSteps)
            {
                _replaced[r.Id] = replacedSteps;
                items.Add(new Seam { Kind = "replacementStart", Section = r.BySection, PrevSection = section, ReplacementId = r.Id, Replaced = replacedSteps, RecordedUtc = SectionInfo(r.BySection)?.StartUtc });
                var inner = Expand(r.BySection);
                var block = inner.OfType<ViewStep>().ToList();
                _blocks[r.Id] = block;
                foreach (var b in block) { b.ReplacementId ??= r.Id; _blockOf.TryAdd(b.Anchor, r.Id); }
                items.AddRange(inner);
                items.Add(new Seam { Kind = "replacementEnd", Section = section, PrevSection = r.BySection, ReplacementId = r.Id, RecordedUtc = SectionInfo(section)?.StartUtc });
            }
            foreach (var s in steps)
            {
                var r = reps.FirstOrDefault(x => x.Replaces.Contains(s.Anchor));
                if (r != null)
                {
                    s.State = "replaced"; s.ReplacementId = r.Id;
                    if (done.Add(r.Id)) Insert(r, steps.Where(x => r.Replaces.Contains(x.Anchor)).ToList());
                    continue;
                }
                // Regel 3: liegt kein Schritt im Bereich, steht der Ersatz vor dem ersten Schritt hinter dem Bereich
                foreach (var e in reps.Where(x => !done.Contains(x.Id) && s.Anchor > x.Replaces.ToSeq && !steps.Any(y => x.Replaces.Contains(y.Anchor))).ToList())
                { done.Add(e.Id); Insert(e, new List<ViewStep>()); }
                items.Add(s);
            }
            foreach (var e in reps.Where(x => !done.Contains(x.Id)).ToList()) { done.Add(e.Id); Insert(e, new List<ViewStep>()); }
            return items;
        }

        var items = new List<object>();
        SessionPart? prevTop = null;
        foreach (var p in Sections.Where(p => p.Purpose != "replacement" && !discarded.Contains(p.Part)).OrderBy(p => p.Part))
        {
            if (prevTop != null) items.Add(new Seam { Kind = "continuation", Section = p.Part, PrevSection = prevTop.Part, RecordedUtc = p.StartUtc, PrevEndUtc = prevTop.EndUtc });
            items.AddRange(Expand(p.Part));
            prevTop = p;
        }
        // Schritte außerhalb aller bekannten Abschnitte (sollte nicht vorkommen): hinten anfügen
        var placed = items.OfType<ViewStep>().ToHashSet();

        // Nahtstellen an die Schritte binden; nur zwischen Schritten verschiedener Abschnitte
        var eff = new List<ViewStep>();
        var seams = new List<Seam>();
        var pending = new List<Seam>();
        foreach (var it in items)
        {
            if (it is Seam sm) { pending.Add(sm); continue; }
            var s = (ViewStep)it;
            if (pending.Count > 0 && (eff.Count == 0 ? pending.Any(x => x.Kind == "replacementStart") : eff[^1].Section != s.Section || pending.Any(x => x.Kind == "replacementStart")))
            {
                var seam = pending.LastOrDefault(x => x.Kind == "replacementStart") ?? pending.FirstOrDefault(x => x.Kind == "continuation") ?? pending[^1];
                if (eff.Count > 0) seam.PrevSection = eff[^1].Section;
                seam.Next = s;
                s.SeamBefore = seam;
                seams.Add(seam);
                // Kapitel, das mit einem ersetzten Schritt begann, beginnt mit dem ersten Schritt des Ersatzes (05 §16.6)
                if (seam.Kind == "replacementStart" && s.ChapterTitle == null && seam.Replaced.FirstOrDefault(x => x.ChapterTitle != null) is { } ch && ch == seam.Replaced[0])
                { s.ChapterTitle = ch.ChapterTitle; s.ChapterText = ch.ChapterText; }
            }
            pending.Clear();
            eff.Add(s);
        }
        // Ersatz ohne Schritte am Ende: Naht ohne folgenden Schritt (nie verschwiegen)
        seams.AddRange(pending.Where(x => x.Kind == "replacementStart" && x.Replaced.Count > 0));
        foreach (var s in visible.Where(s => !placed.Contains(s) && s.State == "active" && !IsReplacementSection(s.Section))) eff.Add(s);
        foreach (var s in visible.Where(s => s.State == "active" && !placed.Contains(s) && IsReplacementSection(s.Section))) s.State = "unused";
        Seams = seams;
        Inactive = visible.Where(s => s.State is "replaced" or "unused").ToList();
        foreach (var s in Inactive) s.Number = 0;
        SetNumbers(eff);
    }

    void SetNumbers(List<ViewStep> eff)
    {
        int n = 0;
        foreach (var v in eff) v.Number = ++n;
        All = eff;
    }

    int SectionOfSeq(long seq)
    {
        for (int i = Sections.Count - 1; i >= 0; i--) if (seq >= Sections[i].FirstSeq) return Sections[i].Part;
        return 0;
    }

    /// <summary>Position in <c>order</c>; Schritte eines Ersatzes erben die Position des ersten ersetzten Schritts (05 §16.2).</summary>
    int OrderKey(ViewStep s, Dictionary<long, int> pos)
    {
        long anchor = s.Anchor;
        for (int guard = 0; guard < 32; guard++)
        {
            if (pos.TryGetValue(anchor, out var p)) return p;
            if (!_blockOf.TryGetValue(anchor, out var rid) || !_replaced.TryGetValue(rid, out var rep) || rep.Count == 0) break;
            anchor = rep[0].Anchor;
        }
        return int.MaxValue;
    }

    /// <summary>Eigener Schritt hinter einem ersetzten Schritt: steht hinter dem letzten Schritt des Ersatzes.</summary>
    long MapAfter(long after)
    {
        for (int guard = 0; guard < 32 && after > 0; guard++)
        {
            var rs = Inactive.FirstOrDefault(x => x.Anchor == after && x.State == "replaced");
            if (rs?.ReplacementId is not { } rid || !_blocks.TryGetValue(rid, out var block) || block.Count == 0) break;
            after = block[^1].Anchor;
        }
        return after;
    }

    /// <summary>Nahtstellen zwischen zwei angezeigten Schritten (prev ausschließlich, cur einschließlich) – auch wenn dazwischen Schritte weggelassen wurden.</summary>
    public List<Seam> SeamsBetween(ViewStep? prev, ViewStep cur)
    {
        if (cur.Manual || cur.Number <= 0 || Seams.Count == 0) return new();
        if (prev is { Manual: true }) return cur.SeamBefore != null ? new List<Seam> { cur.SeamBefore } : new();
        int from = prev?.Number ?? 0;
        return All.Where(s => s.Number > from && s.Number <= cur.Number && s.SeamBefore != null).Select(s => s.SeamBefore!).ToList();
    }

    /// <summary>Liegt zwischen zwei Schritten der wirksamen Folge eine Nahtstelle? Dann gibt es dort keinen Abstand (05 §16.4).</summary>
    public bool SeamBetween(ViewStep? prev, ViewStep cur) => prev != null && SeamsBetween(prev, cur).Count > 0;

    /// <summary>Kennzahlen der wirksamen Folge (05 §16.6): ersetzt, nicht verwendet, Nahtstellen, verworfene Abschnitte.</summary>
    public (int replaced, int unused, int seams, int discardedSections, int effectiveReplacements) SectionStats()
        => (Inactive.Count(s => s.State == "replaced"), Inactive.Count(s => s.State == "unused"), Seams.Count, DiscardedSections.Count, Replacements.Count(r => r.Effective));

    /// <summary>
    /// Bereich für „Neu aufnehmen und ersetzen“ aus gewählten Schritten (08 §9.3): ein zusammenhängender Bereich innerhalb eines Abschnitts,
    /// vom ersten Ereignis des ersten bis zum letzten Ereignis des letzten gewählten Schritts. Liefert sonst den Grund.
    /// </summary>
    public (PartRange? range, List<ViewStep> steps, string? error) ReplaceRangeFor(IEnumerable<ViewStep> selected)
    {
        var sel = selected.Where(s => !s.Manual).Distinct().ToList();
        string Err(string de, string en) => Settings.Loc.L(de, en);
        if (sel.Count == 0) return (null, sel, Err("Bitte zuerst aufgenommene Schritte wählen.", "Please select recorded steps first."));
        if (sel.Any(s => s.State != "active" || s.Number <= 0)) return (null, sel, Err("Ersetzte oder nicht verwendete Schritte lassen sich nicht ersetzen.", "Replaced or unused steps cannot be replaced."));
        if (sel.Select(s => s.Section).Distinct().Count() > 1) return (null, sel, Err("Die Schritte stammen aus verschiedenen Aufnahmeabschnitten – ersetzt wird nur innerhalb eines Abschnitts.", "The steps come from different recording parts – only a range within one part can be replaced."));
        int section = sel[0].Section;
        long from = long.MaxValue, to = long.MinValue;
        foreach (var s in sel)
        {
            from = Math.Min(from, s.Anchor); to = Math.Max(to, s.Anchor);
            foreach (var key in new[] { "input", "approach" })
                foreach (var rg in s.Json.Obj("seq")?.Arr(key)?.OfType<JsonArray>() ?? Enumerable.Empty<JsonArray>())
                    if (rg.Count == 2 && rg[0] is JsonValue a && rg[1] is JsonValue b) { from = Math.Min(from, a.GetValue<long>()); to = Math.Max(to, b.GetValue<long>()); }
        }
        if (SectionInfo(section) is { } part) { from = Math.Max(from, part.FirstSeq); to = Math.Min(to, part.LastSeq); }
        var range = new PartRange(section, from, to);
        if (Replacements.Any(r => r.Effective && r.Replaces.Overlaps(range)))
            return (null, sel, Err("Der Bereich überschneidet sich mit einer bestehenden Ersetzung.", "The range overlaps an existing replacement."));
        var covered = All.Where(s => s.Section == section && range.Contains(s.Anchor)).ToList();
        return (range, covered, null);
    }
}
