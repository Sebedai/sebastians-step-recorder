namespace StepRecorder.Text;

/// <summary>
/// Formatierter Text, den die Leseschicht freigegeben hat (13, Paket 21): Schwärzungen und Verallgemeinerungen sind vor und nach dem
/// Auswerten der Auszeichnung angewendet. Nur diesen Typ nehmen die Schreiber an; erzeugt wird er ausschließlich in
/// <c>SessionView.SealRich</c> oder aus bereits bereinigtem reinem Text (<see cref="FromPlain"/>).
/// </summary>
public sealed class RichText
{
    public RichDoc Doc { get; }
    /// <summary>Sichtbarer Text ohne Auszeichnung – das, was KI-Paket, MCP, Suche und Qualitätsprüfung lesen.</summary>
    public string Plain { get; }
    /// <summary>Die Formatierung wurde verworfen, weil sonst ein geschützter Wert sichtbar geworden wäre.</summary>
    public bool Degraded { get; }
    public bool IsSimple => Doc.IsSimple;

    internal RichText(RichDoc doc, bool degraded) { Doc = doc; Plain = doc.Projection(); Degraded = degraded; }

    /// <summary>Bereits bereinigter reiner Text als ein Absatz mit Zeilenumbrüchen – ohne die Auszeichnung zu deuten.</summary>
    public static RichText FromPlain(string cleanPlain, bool degraded = false)
    {
        var inl = new List<RichInline>();
        var lines = (cleanPlain ?? "").Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) inl.Add(new RichInline(RichKind.Break, "\n"));
            if (lines[i].Length > 0) inl.Add(new RichInline(RichKind.Text, lines[i]));
        }
        return new RichText(new RichDoc(inl.Count == 0 ? Array.Empty<RichBlock>() : new[] { new RichBlock(RichBlockKind.Paragraph, inl) }), degraded);
    }

    /// <summary>Hängt bereinigte reine Zeilen als weiteren Absatz an (z. B. Ziel und erwartetes Ergebnis unter der Einleitung).</summary>
    public RichText AppendPlain(string cleanPlain)
    {
        if (string.IsNullOrEmpty(cleanPlain)) return this;
        var extra = FromPlain(cleanPlain).Doc.Blocks;
        return new RichText(new RichDoc(Doc.Blocks.Concat(extra).ToList()), Degraded);
    }
}
