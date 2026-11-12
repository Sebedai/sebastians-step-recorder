using System.Globalization;
using System.Text;
using StepRecorder.Data;

namespace StepRecorder.Export;

public static partial class GuideExport
{
    /// <summary>
    /// Klick-Tour (4.5): eine HTML-Datei ohne Skript. Je Schritt eine Folie (<c>section.slide</c>), sichtbar über
    /// <c>:target</c>; die Klickfläche liegt über dem Ziel und führt zum nächsten Schritt. Alle Maße stehen im einen
    /// <c>&lt;style&gt;</c>-Block. Hinweisring nach <see cref="GuideOptions.TourHints"/> (always | onDemand | never).
    /// Die Druckansicht zeigt alle Schritte untereinander.
    /// </summary>
    static string NoteKind(string kind) => kind is "tip" or "warning" or "important" or "result" ? kind : "note";

    static string Tour(Doc d, GuideOptions o)
    {
        var e = Fmt.HtmlEscape;
        string P(string t) => e(t).Replace("\n", "<br>");
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var items = d.Blocks.OfType<Item>().ToList();
        string hints = o.TourHints is "always" or "never" ? o.TourHints : "onDemand";
        var css = new StringBuilder();
        css.Append(":root{--accent:").Append(d.S.Accent).Append("}\n");
        css.Append("""
:root{--fg:#ecebe7;--muted:#a3a19a;--bg:#161615;--panel:#1f1f1d;--line:#36352f}
*{box-sizing:border-box}
html,body{margin:0;background:var(--bg);color:var(--fg);font-family:"Segoe UI",system-ui,sans-serif;line-height:1.5}
.slide{display:none;max-width:1280px;margin:0 auto;padding:22px 20px 28px}
.slide:target{display:block}
body:not(:has(.slide:target)) #start{display:block}
.slide header{display:flex;gap:14px;align-items:baseline;margin:0 0 6px}
.slide header .n{flex:none;background:var(--accent);color:#fff;border-radius:5px;padding:0 .55em;font-size:.85em;font-variant-numeric:tabular-nums}
.slide h1{font-size:2.1em;margin:18vh 0 8px;color:var(--accent)} .slide h2{font-size:1.25em;margin:0}
.count{color:var(--muted);font-size:.85em;font-variant-numeric:tabular-nums;margin-left:auto}
.text{margin:0 0 12px;color:var(--fg)} .sub{color:var(--muted);font-size:1.15em}
.note{border-left:4px solid var(--c);background:var(--panel);padding:6px 12px;margin:6px 0;border-radius:3px} .note b{color:var(--c)}
.stage{position:relative;display:inline-block;max-width:100%;margin:6px 0 0}
.stage img{display:block;max-width:100%;height:auto;border:1px solid var(--line);border-radius:4px}
a.hot{position:absolute;display:block;border-radius:6px;cursor:pointer;outline:none}
a.hot:focus-visible{box-shadow:0 0 0 3px #fff,0 0 0 6px var(--accent)}
@keyframes pulse{0%,100%{box-shadow:0 0 0 2px var(--accent),0 0 0 6px color-mix(in srgb,var(--accent) 35%,transparent)}50%{box-shadow:0 0 0 3px var(--accent),0 0 0 14px color-mix(in srgb,var(--accent) 0%,transparent)}}
nav{display:flex;gap:10px;margin:16px 0 0;align-items:center}
nav a{color:var(--fg);text-decoration:none;border:1px solid var(--line);border-radius:6px;padding:7px 14px;background:var(--panel)}
nav a.next{background:var(--accent);border-color:var(--accent);color:#fff} nav a:focus-visible{outline:2px solid #fff}
nav .hint{color:var(--muted);font-size:.85em;margin-left:6px}
.box{background:var(--panel);border-radius:6px;padding:12px 16px;margin:12px 0;max-width:760px} .box ul{margin:4px 0 0;padding-left:20px}
footer{color:var(--muted);font-size:.8em;margin-top:28px}
@media print{.slide{display:block!important;page-break-after:always;max-width:none}a.hot,nav{display:none}html,body{background:#fff;color:#000}.stage img{border-color:#ccc}}

""");
        foreach (var k in new[] { "note", "tip", "warning", "important", "result" }) css.Append(".note.k-").Append(k).Append("{--c:#").Append(NoteStyle(k, d.Lang).bar).Append("}\n");
        css.Append(hints switch
        {
            "always" => "a.hot{animation:pulse 1.8s ease-in-out infinite}\n",
            "never" => "",
            _ => ".stage:hover a.hot,a.hot:focus{animation:pulse 1.8s ease-in-out infinite}\n"
        });
        for (int i = 0; i < items.Count; i++)
            if (items[i].Hot is { } h)
                css.Append('#').Append("h").Append(i + 1).Append("{left:").Append(F(h.X)).Append("%;top:").Append(F(h.Y)).Append("%;width:").Append(F(h.Width)).Append("%;height:").Append(F(h.Height)).Append("%}\n");

        var b = new StringBuilder();
        string first = items.Count > 0 ? "#" + items[0].Anchor : "#end";
        // Startseite
        b.Append("<section class=\"slide\" id=\"start\">\n<h1>").Append(e(d.Title)).Append("</h1>\n");
        if (d.Subtitle.Length > 0) b.Append("<p class=\"sub\">").Append(e(d.Subtitle)).Append("</p>\n");
        b.Append("<p class=\"text\">").Append(e(items.Count == 1 ? d.L("1 Schritt", "1 step") : $"{items.Count} {d.L("Schritte", "steps")}")).Append(" · ")
         .Append(e(d.L("Klicken Sie im Bild auf die markierte Stelle, um weiterzugehen.", "Click the marked spot in the image to continue."))).Append("</p>\n");
        if (d.Intro.Length > 0) b.Append("<div class=\"box\">").Append(P(d.Intro)).Append("</div>\n");
        if (d.Prereq.Count > 0) b.Append("<div class=\"box\"><b>").Append(e(d.L("Voraussetzungen", "Prerequisites"))).Append("</b><ul>").Append(string.Concat(d.Prereq.Select(x => $"<li>{e(x)}</li>"))).Append("</ul></div>\n");
        b.Append("<nav><a class=\"next\" href=\"").Append(first).Append("\" accesskey=\"n\">").Append(e(d.L("Tour starten →", "Start tour →"))).Append("</a></nav>\n</section>\n");

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            string prev = i == 0 ? "#start" : "#" + items[i - 1].Anchor, next = i == items.Count - 1 ? "#end" : "#" + items[i + 1].Anchor;
            b.Append("<section class=\"slide\" id=\"").Append(it.Anchor).Append("\">\n<header>");
            if (it.Number.Length > 0) b.Append("<span class=\"n\">").Append(e(it.Number)).Append("</span>");
            b.Append("<h2>").Append(e(it.Title)).Append("</h2><span class=\"count\">").Append(e(d.L($"{i + 1} von {items.Count}", $"{i + 1} of {items.Count}"))).Append("</span></header>\n");
            if (it.Text.Length > 0 && it.Text != it.Title) b.Append("<p class=\"text\">").Append(P(it.Text)).Append("</p>\n");
            foreach (var (kind, text) in it.Notes)
            {
                var (label, _, _) = NoteStyle(kind, d.Lang);
                b.Append("<div class=\"note k-").Append(NoteKind(kind)).Append("\"><b>").Append(e(label)).Append("</b> ").Append(P(text)).Append("</div>\n");
            }
            if (it.Png != null)
            {
                b.Append("<div class=\"stage\"><img alt=\"").Append(e(it.Title)).Append("\" width=\"").Append(it.W).Append("\" height=\"").Append(it.H).Append("\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(it.Png)).Append("\">");
                if (it.Hot != null) b.Append("<a class=\"hot\" id=\"h").Append(i + 1).Append("\" href=\"").Append(next).Append("\" aria-label=\"").Append(e(d.L("Hier klicken – weiter", "Click here – next"))).Append("\" title=\"").Append(e(d.L("Hier klicken", "Click here"))).Append("\"></a>");
                b.Append("</div>\n");
            }
            b.Append("<nav><a href=\"").Append(prev).Append("\" accesskey=\"p\">").Append(e(d.L("← Zurück", "← Back"))).Append("</a><a class=\"next\" href=\"").Append(next).Append("\" accesskey=\"n\">").Append(e(d.L("Weiter →", "Next →"))).Append("</a>");
            if (it.Hot != null) b.Append("<span class=\"hint\">").Append(e(d.L("oder im Bild auf die Stelle klicken", "or click the spot in the image"))).Append("</span>");
            b.Append("</nav>\n</section>\n");
        }

        // Endseite
        b.Append("<section class=\"slide\" id=\"end\">\n<h1>").Append(e(d.L("Geschafft", "Done"))).Append("</h1>\n");
        if (d.Outro.Length > 0) b.Append("<div class=\"box\">").Append(P(d.Outro)).Append("</div>\n");
        b.Append("<nav><a class=\"next\" href=\"").Append(first).Append("\">").Append(e(d.L("Von vorn", "Start again"))).Append("</a><a href=\"#start\">").Append(e(d.L("Übersicht", "Overview"))).Append("</a></nav>\n");
        b.Append("<footer>").Append(e(d.S.Footer.Length > 0 ? d.S.Footer : d.Title)).Append(" · ").Append(e(d.L("Erstellt mit ", "Created with "))).Append(e(AppInfo.DisplayName)).Append("</footer>\n</section>\n");

        return $"<!DOCTYPE html>\n<html lang=\"{d.Lang}\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n" +
               "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\">\n" +
               $"<title>{e(d.Title)}</title>\n<style>\n{css}</style>\n</head>\n<body>\n{b}</body>\n</html>\n";
    }
}
