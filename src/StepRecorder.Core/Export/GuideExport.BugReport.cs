using System.Text;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.ReadLayer;

namespace StepRecorder.Export;

public static partial class GuideExport
{
    /// <summary>
    /// Fehlerbericht (4.4): Markdown für Ticket oder Issue plus <c>bilder\</c>. Abschnitte: Zusammenfassung, Umgebung,
    /// Schritte zum Nachstellen, erwartetes und tatsächliches Ergebnis, Anhänge. Nie enthalten: Programmpfad,
    /// Kommandozeile, Prozess-ID. Gibt den Text zurück (für die Zwischenablage).
    /// </summary>
    static string BugReport(SessionView view, Doc d, string outPath)
    {
        string dir = Path.GetDirectoryName(outPath)!;
        string imgDir = Path.Combine(dir, "bilder");
        var m = Fmt.MdEscape;
        string Todo() => "_" + d.L("(bitte ergänzen)", "(please add)") + "_";
        var sb = new StringBuilder();
        sb.Append("# ").Append(m(d.Title)).Append("\n\n");

        // Zusammenfassung
        var goal = view.Goal();
        sb.Append("## ").Append(d.L("Zusammenfassung", "Summary")).Append("\n\n");
        string summary = goal?.Str("description") is { Length: > 0 } gd ? view.Clean(gd) : DocSettings.GuideText(view.Edits.Obj("guide"), "intro", d.Lang) is { Length: > 0 } intro ? view.Clean(intro) : "";
        sb.Append(summary.Length > 0 ? m(summary) : Todo()).Append("\n\n");

        // Umgebung (ohne Pfade, Kommandozeilen, Prozess-IDs)
        sb.Append("## ").Append(d.L("Umgebung", "Environment")).Append("\n\n");
        var man = view.Session.Manifest;
        if (man.Obj("environment")?.Obj("os") is { } os)
            sb.Append("- Windows: ").Append(m(view.Clean(os.Str("product") ?? "Windows"))).Append(os.Str("displayVersion") is { Length: > 0 } dv ? " " + m(dv) : "").Append(os.Str("version") is { Length: > 0 } v ? $" ({m(v)})" : "").Append('\n');
        if (man.Obj("environment")?.Obj("locale")?.Str("ui") is { Length: > 0 } ui) sb.Append("- ").Append(d.L("Sprache", "Language")).Append(": ").Append(m(ui)).Append('\n');
        var mons = view.Session.Events.LastOrDefault(e => e.Str("type") == "display.topology")?.Arr("monitors") ?? man.Obj("topology")?.Arr("monitors") ?? new JsonArray();
        foreach (var mon in mons.OfType<JsonObject>())
            sb.Append("- ").Append(d.L("Monitor", "Monitor")).Append(' ').Append(m(mon.Str("id") ?? "")).Append(": ").Append(mon.Int("w")).Append(" × ").Append(mon.Int("h"))
              .Append(", ").Append(Math.Round(mon.Dbl("scale", 1) * 100)).Append(" %").Append(mon.Bool("primary") ? d.L(" (Hauptmonitor)", " (primary)") : "").Append('\n');
        var apps = new List<string>();
        foreach (var it in d.Blocks.OfType<Item>())
        {
            var j = it.Step.Json;
            if (j.Obj("ctx")?.Str("process") is not { Length: > 0 } proc) continue;
            var app = j.Obj("target")?.Obj("win32")?.Obj("window")?.Obj("app");
            string label = proc + (app?.Str("product") is { Length: > 0 } pr ? $" ({pr}{(app.Str("version") is { Length: > 0 } ver ? " " + ver : "")})" : "");
            label = view.Clean(label);
            if (!apps.Contains(label, StringComparer.OrdinalIgnoreCase)) apps.Add(label);
        }
        if (apps.Count > 0) sb.Append("- ").Append(d.L("Programme", "Applications")).Append(": ").Append(string.Join(", ", apps.Select(a => "`" + a.Replace("`", "'") + "`"))).Append('\n');
        // Build-Stand (12, Paket 10): Kennung der Programmdatei, Git-Arbeitsstand nur als ungeprüfte Zusatzangabe
        foreach (var b in DeveloperContext.Builds(view).OfType<JsonObject>())
        {
            var bits = new List<string>();
            if (b.Obj("build")?.Str("pdbId") is { } pdb) bits.Add("PDB " + pdb);
            if (b.Obj("build")?.Str("peTimestampUtc") is { } pts) bits.Add(d.L("erstellt ", "linked ") + pts);
            if (b.Obj("git") is { } git) bits.Add(d.L("Git-Arbeitsstand (ungeprüft): ", "Git working tree (unchecked): ") + (git.Str("branch") ?? d.L("losgelöst", "detached")) + (git.Str("commit") is { } cm ? " @ " + cm[..Math.Min(12, cm.Length)] : ""));
            if (bits.Count > 0) sb.Append("- Build ").Append(m(view.Clean(b.Str("process") ?? ""))).Append(": ").Append(m(view.Clean(string.Join(" · ", bits)))).Append('\n');
        }
        sb.Append("- ").Append(d.L("Aufgenommen", "Recorded")).Append(": ").Append(view.Session.StartUtc.ToLocalTime().ToString(Settings.OutText.DateFormat(d.Lang) + " HH:mm"))
          .Append(d.L(" mit ", " with ")).Append(AppInfo.DisplayName).Append(man.Obj("recorder")?.Str("version") is { Length: > 0 } rv ? " " + m(rv) : "").Append("\n\n");

        // Schritte zum Nachstellen (Beobachtungen „Fehler“/„Erwartet“ kommen in eigene Abschnitte)
        sb.Append("## ").Append(d.L("Schritte zum Nachstellen", "Steps to reproduce")).Append("\n\n");
        int n = 0, img = 0;
        var attachments = new List<string>();
        foreach (var it in d.Blocks.OfType<Item>())
        {
            if (it.Step.Kind == "marker" && it.Step.Json.Obj("input")?.Str("markerKind") is "error" or "expected") continue;
            sb.Append(++n).Append(". **").Append(m(it.Title)).Append("**");
            if (it.Text.Length > 0 && it.Text != it.Title) sb.Append(" – ").Append(m(it.Text.Replace("\n", " ")));
            sb.Append('\n');
            foreach (var (kind, nt) in it.Notes) sb.Append("   > **").Append(NoteStyle(kind, d.Lang).label).Append("** ").Append(m(nt.Replace("\n", " "))).Append('\n');
            if (it.Png != null)
            {
                Directory.CreateDirectory(imgDir);
                string file = $"schritt-{++img:000}.png";
                File.WriteAllBytes(Path.Combine(imgDir, file), it.Png);
                attachments.Add(file);
                sb.Append("   ![").Append(d.L("Schritt ", "Step ")).Append(n).Append("](bilder/").Append(file).Append(")\n");
            }
        }
        if (n == 0) sb.Append(Todo()).Append('\n');
        sb.Append('\n');

        string Markers(string kind) => string.Join("\n", view.All.Where(s => s.Kind == "marker" && !s.Redacted && s.Json.Obj("input")?.Str("markerKind") == kind)
            .Select(s => "- " + m(s.Json.Obj("input")?.Str("label") is { Length: > 0 } l ? l : s.AuthorTitle ?? s.Title) + $" ({d.L("bei", "at")} {Fmt.Time(s.TFrom, d.Lang).Split(',')[0]})"));
        sb.Append("## ").Append(d.L("Erwartetes Ergebnis", "Expected result")).Append("\n\n");
        string expected = string.Join("\n", new[] { goal?.Str("expected") is { Length: > 0 } ge ? m(view.Clean(ge)) : null, Markers("expected") is { Length: > 0 } me ? me : null }.Where(x => x != null));
        sb.Append(expected.Length > 0 ? expected : Todo()).Append("\n\n");
        sb.Append("## ").Append(d.L("Tatsächliches Ergebnis", "Actual result")).Append("\n\n");
        string actual = Markers("error");
        // beobachtetes Programmende, Hänger und Fehlermeldungen von Windows (12, Paket 10)
        var observed = view.All.Where(s => !s.Redacted && s.Kind == "system" && s.Json.Obj("input")?.Str("event") is "appExit" or "appHang" or "appEventLog" && s.Json.Obj("input")?.Str("class") != "normal")
            .Select(s => "- " + m(s.Protocol.Length > 0 ? s.Protocol : s.Title) + $" ({d.L("Schritt", "step")} {s.Number}, {d.L("beobachtet", "observed")})");
        actual = string.Join("\n", new[] { actual }.Concat(observed).Where(x => x.Length > 0));
        sb.Append(actual.Length > 0 ? actual : Todo()).Append("\n\n");

        if (attachments.Count > 0)
        {
            sb.Append("## ").Append(d.L("Anhänge", "Attachments")).Append("\n\n");
            foreach (var a in attachments) sb.Append("- bilder/").Append(a).Append('\n');
            sb.Append('\n');
        }
        sb.Append("---\n").Append(d.L("Erstellt mit ", "Created with ")).Append(AppInfo.DisplayName).Append('\n');
        string text = sb.ToString();
        File.WriteAllText(outPath, text, new UTF8Encoding(false));
        return text;
    }
}
