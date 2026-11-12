using System.Drawing;
using StepRecorder.Data;
using StepRecorder.Export.Formats;

namespace StepRecorder.Export;

public static partial class GuideExport
{
    /// <summary>
    /// PowerPoint (4.6): Titelfolie · „Über diese Anleitung“ · Kapitel · je Schritt eine Folie (Nummer, Titel als Platzhalter,
    /// Text, Hinweiskästen, eingepasstes Bild) · Abschluss. Vortragsnotizen mit vollem Text und allen Hinweisen.
    /// </summary>
    static void Pptx(Doc d, string outPath, GuideOptions o)
    {
        const long M = 457200;                         // 0,5 Zoll Rand
        long W = SimplePptx.W, H = SimplePptx.H;
        var p = new SimplePptx { Title = d.Title, Language = d.Lang == "de" ? "de-DE" : "en-US", Accent = d.S.AccentHex, Author = d.S.Author };
        string ink = SimplePptx.Ink, muted = SimplePptx.Muted;
        string footer = d.S.Footer.Length > 0 ? d.S.Footer : d.Title;

        // Titelfolie
        var t = p.AddSlide(SimplePptx.Layout.Title);
        t.Rect(0, 0, W, 109728, d.S.AccentHex);
        t.Rect(M + 457200, 2240000, 54864, 2400000, d.S.AccentHex);
        if (d.Logo != null)
        {
            using var lm = new MemoryStream(d.Logo); using var lb = new Bitmap(lm);
            t.Picture(d.Logo, lb.Width, lb.Height, M + 640080, 700000, 3200400, 900000, "Logo", border: false);
        }
        long tx = M + 640080, tw = W - tx - M;
        t.TitleText(d.Title, tx, 2240000, tw, 1300000, SimplePptx.FitFontSize(d.Title, tw, 1300000, 40, 22));
        if (d.Subtitle.Length > 0) t.Text(d.Subtitle, tx, 3620000, tw, 520000, 20, muted);
        t.Text(string.Join("\n", d.Meta.Select(m => m.label + ": " + m.value)), tx, 4250000, tw, 1100000, 13, muted);
        t.Notes = o.PptxNotes ? d.Title + (d.Subtitle.Length > 0 ? "\n" + d.Subtitle : "") : "";

        // Über diese Anleitung
        if (d.Intro.Length > 0 || d.Prereq.Count > 0)
        {
            var a = p.AddSlide();
            a.TitleText(d.L("Über diese Anleitung", "About this guide"), M, 380000, W - 2 * M, 700000, 28);
            string body = d.Intro;
            if (d.Prereq.Count > 0) body += (body.Length > 0 ? "\n\n" : "") + d.L("Voraussetzungen", "Prerequisites") + ":\n" + string.Join("\n", d.Prereq.Select(x => "•  " + x));
            a.Text(body, M, 1300000, W - 2 * M, H - 1300000 - M, SimplePptx.FitFontSize(body, W - 2 * M, H - 1300000 - M, 20, 11));
            a.Notes = o.PptxNotes ? body : "";
        }

        foreach (var b in d.Blocks)
        {
            if (b is Chapter c)
            {
                var s = p.AddSlide();
                s.Rect(M, 2100000, 54864, 2000000, d.S.AccentHex);
                if (c.Number.Length > 0) s.Text(c.Number, M + 320000, 2000000, 2000000, 700000, 40, d.S.AccentHex, bold: true);
                s.TitleText(c.Title, M + 320000, 2700000, W - 2 * M - 320000, 900000, SimplePptx.FitFontSize(c.Title, W - 2 * M - 320000, 900000, 34, 20), anchor: "t");
                if (c.Text.Length > 0) s.Text(c.Text, M + 320000, 3700000, W - 2 * M - 320000, 1900000, SimplePptx.FitFontSize(c.Text, W - 2 * M - 320000, 1900000, 18, 11), muted);
                s.Notes = o.PptxNotes ? (Heading(c.Number, c.Title) + (c.Text.Length > 0 ? "\n" + c.Text : "")) : "";
                continue;
            }
            var it = (Item)b;
            var sl = p.AddSlide();
            long top = 380000, titleH = 700000, titleX = M;
            if (it.Number.Length > 0)
            {
                sl.Badge(it.Number, M, top + (titleH - 420000) / 2, 420000, 16);
                titleX = M + Math.Max(420000, (long)(it.Number.Length * 16 * 0.62 * SimplePptx.Emu + 420000 * 0.6)) + 180000;
            }
            long titleW = W - titleX - M;
            sl.TitleText(it.Title, titleX, top, titleW, titleH, SimplePptx.FitFontSize(it.Title, titleW, titleH, 26, 14), anchor: "ctr");
            sl.Rect(M, top + titleH + 120000, W - 2 * M, 9525, SimplePptx.Line);
            long bodyY = top + titleH + 300000, bodyH = H - bodyY - M - 200000;
            bool img = it.Png != null;
            long textW = img ? (long)((W - 2 * M) * 0.36) : W - 2 * M;
            string text = it.Text.Length > 0 && it.Text != it.Title ? it.Text : "";
            // bis zu zwei Hinweiskästen unter dem Text, weitere hängen am Text
            var boxes = it.Notes.Take(2).ToList();
            foreach (var (kind, nt) in it.Notes.Skip(2)) text += (text.Length > 0 ? "\n" : "") + NoteStyle(kind, d.Lang).label + " " + nt;
            long boxH = 640000, boxesH = boxes.Count * (boxH + 120000);
            long by = bodyY + bodyH - boxesH;                      // Hinweiskästen unten in der Textspalte
            long textH = Math.Max(600000, by - bodyY - 100000);
            if (text.Length > 0) sl.Text(text, M, bodyY, textW, textH, SimplePptx.FitFontSize(text, textW, textH, 18, 10), it.Step.Redacted ? muted : ink);
            foreach (var (kind, nt) in boxes)
            {
                var (label, bar, fill) = NoteStyle(kind, d.Lang);
                double fs = SimplePptx.FitFontSize(label + " " + nt, textW - 200000, boxH - 100000, 13, 9);
                sl.Rich(M, by, textW, boxH, fill, bar, (label + " ", true, bar, fs), (nt.Replace("\n", " "), false, ink, fs));
                by += boxH + 120000;
            }
            if (img)
            {
                long ix = M + textW + 300000;
                sl.Picture(it.Png!, it.W, it.H, ix, bodyY, W - ix - M, bodyH, it.Title);
            }
            sl.Text(footer, M, H - M + 60000, W - 2 * M, 250000, 9, muted);
            if (o.PptxNotes)
                sl.Notes = string.Join("\n", new[] { Heading(it.Number, it.Title), it.Text != it.Title ? it.Text : "" }.Where(x => x.Length > 0)
                    .Concat(it.Notes.Select(n => NoteStyle(n.kind, d.Lang).label + " " + n.text)));
        }

        // Abschluss
        var end = p.AddSlide();
        end.TitleText(d.L("Abschluss", "Finally"), M, 380000, W - 2 * M, 700000, 28);
        string outro = d.Outro.Length > 0 ? d.Outro : d.L("Geschafft – alle Schritte sind beschrieben.", "Done – all steps are described.");
        end.Text(outro, M, 1300000, W - 2 * M, H - 1300000 - M - 400000, SimplePptx.FitFontSize(outro, W - 2 * M, H - 1300000 - M - 400000, 22, 11));
        end.Text(d.L("Erstellt mit ", "Created with ") + AppInfo.DisplayName, M, H - M - 200000, W - 2 * M, 250000, 10, muted);
        end.Notes = o.PptxNotes ? outro : "";
        p.Save(outPath);
    }
}
