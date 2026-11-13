namespace StepRecorder.ReadLayer;

/// <summary>Ein Eintrag der Schrittfolge für Kapiteloperationen: Anker und Ebene des hier beginnenden Kapitels (0 = kein Kapitelbeginn).</summary>
public sealed record SeqItem(long Anchor, int ChapterLevel);

/// <summary>Bereich eines Kapitels in der Folge: erster Index, Index nach dem Ende, Ebene.</summary>
public sealed record ChapterRange(long StartAnchor, int Level, int First, int EndExclusive);

/// <summary>
/// Kapitel als Ganzes bearbeiten (13, Paket 24). Ein Kapitel der Ebene L reicht bis zum nächsten Kapitelbeginn mit Ebene ≤ L;
/// Schritte vor dem ersten Kapitel bilden einen Vorspann, der nie mitwandert. Alle Funktionen liefern die vollständige neue
/// Reihenfolge (<c>order[]</c>) oder null, wenn nichts zu tun ist.
/// </summary>
public static class ChapterOps
{
    public static List<ChapterRange> Ranges(IReadOnlyList<SeqItem> seq)
    {
        var res = new List<ChapterRange>();
        for (int i = 0; i < seq.Count; i++)
        {
            int level = seq[i].ChapterLevel;
            if (level <= 0) continue;
            int end = i + 1;
            while (end < seq.Count && (seq[end].ChapterLevel <= 0 || seq[end].ChapterLevel > level)) end++;
            res.Add(new ChapterRange(seq[i].Anchor, level, i, end));
        }
        return res;
    }

    /// <summary>
    /// Kapitel mit seinem ganzen Bereich (samt Unterkapiteln) mit dem Nachbarn gleicher Ebene unter demselben Oberkapitel tauschen.
    /// <paramref name="direction"/> −1 nach oben, +1 nach unten.
    /// </summary>
    public static List<long>? MoveChapter(IReadOnlyList<SeqItem> seq, long chapterStart, int direction)
    {
        var ranges = Ranges(seq);
        var me = ranges.FirstOrDefault(r => r.StartAnchor == chapterStart);
        if (me == null) return null;
        // Nachbar: gleiche Ebene, dazwischen kein Kapitel höherer Ordnung (sonst anderes Oberkapitel)
        ChapterRange? other = null;
        if (direction < 0)
        {
            for (int i = ranges.IndexOf(me) - 1; i >= 0; i--)
            {
                if (ranges[i].Level < me.Level) break;
                if (ranges[i].Level == me.Level) { other = ranges[i]; break; }
            }
        }
        else
        {
            other = ranges.FirstOrDefault(r => r.First == me.EndExclusive && r.Level == me.Level);
        }
        if (other == null) return null;
        var (a, b) = direction < 0 ? (other, me) : (me, other);
        var list = seq.Select(x => x.Anchor).ToList();
        var first = list.GetRange(a.First, a.EndExclusive - a.First);
        var second = list.GetRange(b.First, b.EndExclusive - b.First);
        var res = new List<long>();
        res.AddRange(list.Take(a.First));
        res.AddRange(second);
        res.AddRange(list.Skip(a.EndExclusive).Take(b.First - a.EndExclusive));
        res.AddRange(first);
        res.AddRange(list.Skip(b.EndExclusive));
        return res;
    }

    /// <summary>Anker des Kapitelbeginns, zu dem der Schritt gehört (innerstes Kapitel), oder null im Vorspann.</summary>
    public static long? ChapterOf(IReadOnlyList<SeqItem> seq, long anchor)
    {
        int idx = seq.Select((x, i) => (x, i)).FirstOrDefault(p => p.x.Anchor == anchor).i;
        if (idx < 0 || idx >= seq.Count || seq[idx].Anchor != anchor) return null;
        for (int i = idx; i >= 0; i--) if (seq[i].ChapterLevel > 0) return seq[i].Anchor;
        return null;
    }
}
