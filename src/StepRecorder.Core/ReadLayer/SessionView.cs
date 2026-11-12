using System.Drawing;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.Steps;

namespace StepRecorder.ReadLayer;

/// <summary>Ein Schritt aus Sicht der Leseschicht (Schwärzungen und Verallgemeinerungen angewendet).</summary>
public sealed class ViewStep
{
    public JsonObject Json = null!;      // bereinigtes Schrittobjekt (ohne prov)
    public string Id = "";
    public long Anchor;
    public int Number;                   // 1-basiert über wirksame Schritte
    public string State = "active";      // active | replaced | unused
    public string Kind = "";
    public string Importance = "primary";
    public string Title = "";            // Vorlage oder Autor
    public string Protocol = "";         // Protokollstil (KI)
    public string Guide = "";            // Anleitungsstil
    public string? AuthorTitle, AuthorBody;
    /// <summary>Vor diesem Schritt beginnt ein Kapitel (Bedienungsanleitung).</summary>
    public string? ChapterTitle, ChapterText;
    /// <summary>Bild im Dokument zeigen (null = Standard) und Bildgröße (s|m|l|full, null = wie im Dokument).</summary>
    public bool? ShowImage;
    public string? ImageSize;
    public bool Hidden, Redacted, Discarded;
    public List<JsonObject> Comments = new();
    public List<JsonObject> Annotations = new();
    public string? PreFrame, PostFrame;
    public string? Monitor;
    public long TFrom, TTo;
    public bool Unreviewed;              // auffällige, ungeprüfte Eingabe
    public string? FlagReason;
    public string EvidenceLevel => Json.Obj("evidence").Str("level") ?? "unknown";
    /// <summary>Vom Autor eingefügter Schritt (Text oder eigenes Bild), nicht aus der Aufnahme.</summary>
    public bool Manual;
    /// <summary>Eingefügt nach diesem Anker (0 = am Anfang); nur bei <see cref="Manual"/>.</summary>
    public long After;
    /// <summary>Bild durch eigenes ersetzt (Bild neu aufgenommen).</summary>
    public bool ImageReplaced => PreFrame?.StartsWith("u:") == true && !Manual;
}

/// <summary>
/// Leseschicht: alle Ausgaben (Viewer, Anleitung, KI-Paket, MCP, Suche) lesen ausschließlich hierüber.
/// Erzwingt Schwärzungen (Bild, Text, Schritt, Zeitraum), Verallgemeinerungen, ausgeblendete Kommentare
/// und die Durchsichtsregel ({unreviewed:N} für Ausgaben ohne Durchsicht).
/// </summary>
/// <summary>Sichtbarkeit der Mausmarkierungen in Schrittbildern.</summary>
/// <summary>Sichtbarkeit der Markierungen in Schrittbildern; <see cref="KeyCaps"/> = gedrückte Tasten und getippter Text als Tastenkappen.</summary>
public sealed record MouseMarks(bool Click, bool Path, bool Target, bool Labels, bool Approach, bool KeyCaps = true)
{
    public static readonly MouseMarks Default = new(true, true, true, true, false, true);
    public static readonly MouseMarks None = new(false, false, false, false, false, false);
    public bool Any => Click || Path || Target || Labels || Approach || KeyCaps;

    /// <summary>Nur was in beiden eingeschaltet ist (z. B. Export-Auswahl über den Einstellungen der Sitzung).</summary>
    public MouseMarks And(MouseMarks o) => new(Click && o.Click, Path && o.Path, Target && o.Target, Labels && o.Labels, Approach && o.Approach, KeyCaps && o.KeyCaps);

    /// <summary>Maske für Exporte: Mausmarkierungen und/oder Tastenkappen.</summary>
    public static MouseMarks Mask(bool mouse, bool keys) => new(mouse, mouse, mouse, mouse, mouse, keys);

    public static MouseMarks From(JsonObject? o, MouseMarks basis) => o == null ? basis : new(
        o["clickMarker"] is JsonValue c ? c.GetValue<bool>() : basis.Click,
        o["dragPath"] is JsonValue p ? p.GetValue<bool>() : basis.Path,
        o["elementFrame"] is JsonValue t ? t.GetValue<bool>() : basis.Target,
        o["labels"] is JsonValue l ? l.GetValue<bool>() : basis.Labels,
        o["approachPath"] is JsonValue a ? a.GetValue<bool>() : basis.Approach,
        o["keyCaps"] is JsonValue k ? k.GetValue<bool>() : basis.KeyCaps);

    public static readonly string[] Keys = { "clickMarker", "dragPath", "elementFrame", "labels", "approachPath", "keyCaps" };
}

public sealed class SessionView : IDisposable
{
    public Session Session { get; }
    public JsonObject Steps { get; private set; } = null!;
    public JsonObject Edits { get; private set; } = new();
    public List<ViewStep> All { get; private set; } = new();
    /// <summary>Vom Autor eingefügte Schritte (nur Viewer und Anleitungen; nicht im KI-Paket, AutoHotkey, MCP).</summary>
    public List<ViewStep> Manual { get; private set; } = new();

    /// <summary>Ordner für eigene Bilder (Bild neu aufgenommen, eingefügte Schritte).</summary>
    public string AssetsDir => Path.Combine(Session.Path, "assets");

    /// <summary>Speichert ein eigenes Bild in der Sitzung und liefert dessen Bild-ID („u:datei.png“).</summary>
    public string AddAsset(Bitmap bmp, string prefix = "bild")
    {
        Directory.CreateDirectory(AssetsDir);
        string name = $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png";
        bmp.Save(Path.Combine(AssetsDir, name), System.Drawing.Imaging.ImageFormat.Png);
        return "u:" + name;
    }
    public string Language { get; set; }
    public bool Headless { get; }
    readonly List<(Regex rx, string placeholder)> _subst = new();
    readonly HashSet<string> _redactedStrings = new(StringComparer.Ordinal);
    readonly List<(long from, long to, bool images)> _rangeRedactions = new();
    readonly List<(string m, int fromN, int toN, Rectangle rect, bool pixelate)> _imageRedactions = new();
    public string RedactedText => Language == "de" ? "[geschwärzt]" : "[redacted]";

    SessionView(Session s, bool headless, string? lang)
    {
        Session = s; Headless = headless; Language = lang ?? s.Language;
    }

    public static SessionView Open(string path, bool headless = false, string? lang = null, bool deriveIfMissing = true)
    {
        var s = Session.Open(path);
        var v = new SessionView(s, headless, lang);
        v.Load(deriveIfMissing);
        return v;
    }

    public static SessionView FromSession(Session s, bool headless = false, string? lang = null)
    {
        var v = new SessionView(s, headless, lang);
        v.Load(true);
        return v;
    }

    void Load(bool derive)
    {
        var steps = Session.LoadSteps();
        long lastSeq = Session.Events.Count == 0 ? 0 : Session.Events[^1].Long("seq");
        if (steps == null || steps.Obj("source").Long("lastSeq") != lastSeq || steps.Str("stepBuilderVersion") != StepParams.BuilderVersion)
        {
            if (!derive && steps != null) { }
            else
            {
                steps = StepBuilder.Build(Session);
                if (!Session.ReadOnly && Session.State != "recording") { try { Session.SaveSteps(steps); UpdateManifestSummary(steps); } catch { } }
            }
        }
        Steps = steps!;
        Edits = Session.LoadEdits() ?? new JsonObject();
        Rebuild();
    }

    void UpdateManifestSummary(JsonObject steps)
    {
        var man = (JsonObject)Session.Manifest.DeepClone();
        var arr = steps.Arr("steps") ?? new JsonArray();
        man["rules"] = new JsonObject { ["stepBuilderVersion"] = StepParams.BuilderVersion, ["derivedAtUtc"] = steps.Str("derivedAtUtc"), ["derivedThroughSeq"] = steps.Obj("source").Long("lastSeq") };
        man["summary"] = new JsonObject
        {
            ["steps"] = arr.Count, ["primarySteps"] = arr.Count(s => s.Str("importance") == "primary"),
            ["apps"] = new JsonArray(arr.Where(s => s.Obj("ctx").Str("process") != null).GroupBy(s => s.Obj("ctx").Str("process")!).Select(g => (JsonNode)new JsonObject { ["process"] = g.Key, ["steps"] = g.Count() }).ToArray()),
            ["gaps"] = steps.Arr("gaps")?.Count ?? 0
        };
        var c = steps.Obj("completeness");
        man["completeness"] = new JsonObject
        {
            ["stepBuilderVersion"] = StepParams.BuilderVersion, ["bookkeepingOk"] = c.Obj("bookkeeping").Bool("ok"), ["inputEvents"] = c.Obj("bookkeeping").Long("inputEvents"),
            ["orphanInputEvents"] = c.Obj("bookkeeping").Long("orphans"), ["actionSteps"] = c.Obj("evidence").Long("actionSteps"), ["confirmed"] = c.Obj("evidence").Long("confirmed"),
            ["inputOnly"] = c.Obj("evidence").Long("inputOnly"), ["unknown"] = c.Obj("evidence").Long("unknown"), ["gaps"] = c.Obj("gaps").Long("count"), ["gapsUs"] = c.Obj("gaps").Long("totalUs"),
        };
        var h = man["history"] as JsonArray ?? new JsonArray();
        if (!h.Any(x => x.Str("action") == "derived")) h.Add(new JsonObject { ["atUtc"] = Json.UtcNow(), ["action"] = "derived", ["tool"] = "StepRecorder " + AppInfo.Version });
        man["history"] = h;
        Session.SaveManifest(man);
    }

    /// <summary>Wendet Overlay neu an (nach jeder Bearbeitung aufrufen).</summary>
    public void Rebuild()
    {
        _subst.Clear(); _redactedStrings.Clear(); _rangeRedactions.Clear(); _imageRedactions.Clear();
        foreach (var r in Edits.Arr("redactions") ?? new JsonArray())
        {
            switch (r.Str("kind"))
            {
                case "range":
                    foreach (var a in r.Arr("seqRanges") ?? new JsonArray())
                        _rangeRedactions.Add((a![0]!.GetValue<long>(), a[1]!.GetValue<long>(), r["images"]?.GetValue<bool>() ?? true));
                    break;
                case "text":
                    foreach (var tg in r.Arr("targets") ?? new JsonArray())
                    {
                        var ev = Session.BySeq(tg.Long("seq"));
                        var val = ev != null ? FieldValue(ev, tg.Str("field") ?? "") : null;
                        if (val == null) continue;
                        if (tg.Arr("range") is { Count: 2 } rg) { int a = rg[0]!.GetValue<int>(), b = rg[1]!.GetValue<int>(); if (a >= 0 && b <= val.Length && b > a) val = val[a..b]; }
                        if (val.Length >= 2) _redactedStrings.Add(val);
                    }
                    break;
                case "image":
                    foreach (var a in r.Arr("areas") ?? new JsonArray())
                    {
                        var rc = a.Arr("rect");
                        if (rc == null || rc.Count < 4) continue;
                        _imageRedactions.Add((a.Str("m") ?? "", FrameNo(a.Str("from")), FrameNo(a.Str("to")) is int t && t > 0 ? t : int.MaxValue,
                            new Rectangle(rc[0]!.GetValue<int>(), rc[1]!.GetValue<int>(), rc[2]!.GetValue<int>(), rc[3]!.GetValue<int>()), r.Str("style") == "pixelate"));
                    }
                    break;
            }
        }
        foreach (var sb in Edits.Arr("substitutions") ?? new JsonArray())
        {
            string ph = "{" + (sb.Str("placeholder") ?? "Wert") + "}";
            var m = sb.Obj("match");
            try
            {
                var opts = (sb.Bool("caseSensitive") ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.NonBacktracking;
                if (m.Str("literal") is { Length: >= 3 } lit) _subst.Add((new Regex(@"(?<![\p{L}\p{N}])" + Regex.Escape(lit) + @"(?![\p{L}\p{N}])", opts), ph));
                else if (m.Str("pattern") is { Length: > 0 } pat) _subst.Add((new Regex(pat, opts, TimeSpan.FromMilliseconds(200)), ph));
            }
            catch { }
        }
        BuildSteps();
    }

    static int FrameNo(string? id) => id != null && id.Length > 1 && int.TryParse(id[1..], out var n) ? n : 0;

    public static string? FieldValue(JsonObject ev, string field)
    {
        JsonNode? cur = ev;
        foreach (var p in field.Split('.'))
        {
            int b = p.IndexOf('[');
            if (b > 0) { var arr = cur?[p[..b]] as JsonArray; int idx = int.Parse(p[(b + 1)..^1]); cur = arr != null && idx < arr.Count ? arr[idx] : null; }
            else cur = cur?[p];
        }
        return cur is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    }

    /// <summary>Wendet Textschwärzungen und Verallgemeinerungen auf einen Text an.</summary>
    public string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        foreach (var r in _redactedStrings.OrderByDescending(s => s.Length)) text = text.Replace(r, RedactedText, StringComparison.Ordinal);
        foreach (var (rx, ph) in _subst) { try { text = rx.Replace(text, ph); } catch { } }
        return text;
    }

    /// <summary>Wendet Clean rekursiv auf alle Zeichenketten eines JSON-Knotens an.</summary>
    public JsonNode? CleanNode(JsonNode? n)
    {
        switch (n)
        {
            case JsonObject o:
                var r = new JsonObject();
                foreach (var kv in o) r[kv.Key] = CleanNode(kv.Value);
                return r;
            case JsonArray a:
                return new JsonArray(a.Select(CleanNode).ToArray());
            case JsonValue v when v.TryGetValue<string>(out var s):
                return Clean(s);
            default:
                return n?.DeepClone();
        }
    }

    public bool InRedactedRange(long seq) => _rangeRedactions.Any(r => seq >= r.from && seq <= r.to);

    bool StepRedacted(JsonObject step)
    {
        foreach (var rg in step.Obj("seq")?.Arr("input") ?? new JsonArray())
        {
            long a = rg![0]!.GetValue<long>(), b = rg[1]!.GetValue<long>();
            if (_rangeRedactions.Any(r => a <= r.to && b >= r.from)) return true;
        }
        if (InRedactedRange(step.Long("anchorSeq"))) return true;
        return step.StrList("flags").Contains("redacted") || (step.Str("kind") == "gap" && step.Obj("input").Str("cause") == "removedByAuthor");
    }

    void BuildSteps()
    {
        var list = new List<ViewStep>();
        var stepEdits = (Edits.Arr("steps") ?? new JsonArray()).Where(x => x is JsonObject).ToDictionary(x => x.Long("anchor"), x => x!.AsObject());
        var comments = (Edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>().Where(c => c.Str("kind") != "internal" && !c.Bool("hidden")).ToList();
        var anns = (Edits.Arr("annotations") ?? new JsonArray()).OfType<JsonObject>().ToList();
        var review = ReviewState();
        foreach (var sNode in Steps.Arr("steps") ?? new JsonArray())
        {
            if (sNode is not JsonObject s) continue;
            var vs = new ViewStep { Id = s.Str("id") ?? "", Anchor = s.Long("anchorSeq"), Kind = s.Str("kind") ?? "", Importance = s.Str("importance") ?? "primary", TFrom = s.Long("tFrom"), TTo = s.Long("tTo") };
            vs.Redacted = StepRedacted(s) && vs.Kind != "gap";
            var clean = (JsonObject)CleanNode(s)!;
            clean.Remove("prov");
            vs.Json = clean;
            vs.Discarded = s.StrList("flags").Contains("discarded");
            if (stepEdits.TryGetValue(vs.Anchor, out var se))
            {
                if (se["hidden"] != null) vs.Hidden = se.Bool("hidden");
                if (se["discarded"] != null) vs.Discarded = se.Bool("discarded");
                if (se.Str("importance") is { } imp) vs.Importance = imp;
                var txt = se.Obj("text")?.Obj(Language) ?? se.Obj("text")?.Obj(Language == "de" ? "en" : "de");
                if (txt != null) { vs.AuthorTitle = Clean(txt.Str("title")); vs.AuthorBody = Clean(txt.Str("body")); }
                if (se.Obj("frameChoice") is { } fc) { vs.PreFrame = fc.Str("pre"); vs.PostFrame = fc.Str("post"); }
                ApplyChapter(vs, se);
                if (se["showImage"] != null) vs.ShowImage = se.Bool("showImage");
                if (se.Str("imageSize") is { Length: > 0 } isz) vs.ImageSize = isz;
            }
            vs.PreFrame ??= s.Obj("frames").Str("pre");
            vs.PostFrame ??= s.Obj("frames").Str("post");
            vs.Monitor = s.Obj("frames").Str("m");
            vs.Comments = comments.Where(c => c.Long("anchor") == vs.Anchor).Select(c => (JsonObject)CleanNode(c)!).ToList();
            vs.Annotations = anns.Where(a => a.Long("anchor") == vs.Anchor || (a.Str("frame") is { } f && (f == vs.PreFrame || f == vs.PostFrame))).ToList();
            // Durchsicht: auffällige getippte Eingaben
            if (vs.Kind == "type" && !s.Obj("input").Bool("masked"))
            {
                string text = (s.Obj("input").Str("text") ?? TextTemplates.TokensNotation(s.Obj("input")!)) + " " + s.Obj("input").Str("valueAfter");
                vs.FlagReason = Patterns.Flag(text) ?? (s.StrList("flags").Contains("plaintext") ? "plaintextWindow" : null);
                if (vs.FlagReason != null && !review.Contains(vs.Anchor)) vs.Unreviewed = true;
            }
            if (vs.Redacted)
            {
                vs.Title = Language == "de" ? "Schritt geschwärzt" : "Step redacted";
                vs.Protocol = vs.Guide = vs.Title;
                vs.Json = new JsonObject { ["id"] = vs.Id, ["kind"] = vs.Kind, ["anchorSeq"] = vs.Anchor, ["tFrom"] = vs.TFrom, ["tTo"] = vs.TTo, ["importance"] = vs.Importance, ["redacted"] = true, ["evidence"] = new JsonObject { ["level"] = "unknown" } };
                vs.PreFrame = vs.PostFrame = null;
            }
            else
            {
                string? commentText = vs.Kind == "comment" ? Clean(Session.BySeq(s.Obj("input").Long("refSeq"))?.Str("text")) : null;
                var forText = vs.Json;
                if (vs.Unreviewed && Headless) forText = WithUnreviewed(vs.Json);
                vs.Title = Clean(TextTemplates.Title(forText, Language));
                vs.Protocol = Clean(TextTemplates.Body(forText, Language, "protocol", "formal", commentText));
                vs.Guide = Clean(TextTemplates.Body(forText, Language, "guide", AppAddress(), commentText));
                if (vs.Unreviewed && Headless) vs.Json = forText;
            }
            list.Add(vs);
        }
        int n = 0;
        foreach (var v in list) v.Number = ++n;
        All = list;

        // vom Autor eingefügte Schritte
        var manual = new List<ViewStep>();
        foreach (var ins in (Edits.Arr("inserted") ?? new JsonArray()).OfType<JsonObject>())
        {
            long anchor = ins.Long("anchor");
            if (anchor >= 0) continue;
            var vs = new ViewStep
            {
                Manual = true, Id = "m" + (-anchor), Anchor = anchor, After = ins.Long("after"), Kind = "manual", Importance = "primary",
                Title = Clean(ins.Str("title") ?? (Language == "de" ? "Hinweis" : "Note")), Guide = Clean(ins.Str("body") ?? ""),
                PreFrame = ins.Str("image"), TFrom = list.FirstOrDefault(x => x.Anchor == ins.Long("after"))?.TTo ?? 0
            };
            vs.TTo = vs.TFrom;
            vs.Protocol = vs.Guide;
            vs.Json = new JsonObject { ["id"] = vs.Id, ["kind"] = "manual", ["anchorSeq"] = anchor, ["importance"] = "primary", ["evidence"] = new JsonObject { ["level"] = "author" } };
            if (stepEdits.TryGetValue(anchor, out var se))
            {
                if (se["hidden"] != null) vs.Hidden = se.Bool("hidden");
                var txt = se.Obj("text")?.Obj(Language) ?? se.Obj("text")?.Obj(Language == "de" ? "en" : "de");
                if (txt != null) { vs.AuthorTitle = Clean(txt.Str("title")); vs.AuthorBody = Clean(txt.Str("body")); }
                ApplyChapter(vs, se);
                if (se["showImage"] != null) vs.ShowImage = se.Bool("showImage");
                if (se.Str("imageSize") is { Length: > 0 } isz) vs.ImageSize = isz;
            }
            vs.Comments = comments.Where(c => c.Long("anchor") == anchor).Select(c => (JsonObject)CleanNode(c)!).ToList();
            vs.Annotations = anns.Where(a => a.Long("anchor") == anchor || (a.Str("frame") is { } f && f == vs.PreFrame)).ToList();
            manual.Add(vs);
        }
        Manual = manual;
    }

    /// <summary>Kapitel am Schritt; Titel und Text je Sprache (chapter.en überschreibt für Englisch).</summary>
    void ApplyChapter(ViewStep vs, JsonObject se)
    {
        if (se.Obj("chapter") is not { } ch) return;
        var loc = Language != "de" ? ch.Obj(Language) : null;
        string? title = loc?.Str("title") is { Length: > 0 } lt ? lt : ch.Str("title");
        if (title is not { Length: > 0 }) return;
        vs.ChapterTitle = Clean(title);
        vs.ChapterText = Clean(loc?.Str("text") is { Length: > 0 } ltx ? ltx : ch.Str("text"));
    }

    string AppAddress() => Edits.Obj("guide").Str("address") ?? "formal";

    /// <summary>Ersetzt den getippten Text einer ungeprüften Eingabe durch {unreviewed:N}.</summary>
    static JsonObject WithUnreviewed(JsonObject step)
    {
        var c = (JsonObject)step.DeepClone();
        var input = c.Obj("input");
        if (input == null) return c;
        int n = input.Int("count");
        input["tokens"] = new JsonArray(new JsonObject { ["unreviewed"] = n.ToString() });
        input.Remove("text"); input.Remove("valueAfter"); input.Remove("valueBefore");
        input["unreviewed"] = n;
        c["action"] = null;
        return c;
    }

    HashSet<long> ReviewState()
    {
        var set = new HashSet<long>();
        foreach (var i in Edits.Obj("review")?.Arr("inputs") ?? new JsonArray())
            if (i.Str("status") is "kept" or "redacted")
            {
                if (i.LongN("anchor") is long a) set.Add(a);
                if (i.Arr("seqRange") is { Count: 2 } r) set.Add(r[0]!.GetValue<long>());
            }
        return set;
    }

    // ---------------- Sichten ----------------

    /// <summary>Wirksame Schritte (ohne verworfene Abschnitte). guide=true wendet Reihenfolge und Ausblenden an.</summary>
    public List<ViewStep> Effective(bool guide = false, string importance = "secondary", bool includeManual = false)
    {
        IEnumerable<ViewStep> q = All;
        Dictionary<long, int>? pos = null;
        var order = Edits.Arr("order");
        if (order is { Count: > 0 })
            pos = order.Select((o, i) => (key: o is JsonValue v && v.TryGetValue<long>(out var l) ? l : long.MinValue, i)).Where(x => x.key != long.MinValue).GroupBy(x => x.key).ToDictionary(g => g.Key, g => g.First().i);
        if (guide)
        {
            q = q.Where(s => !s.Hidden && !s.Discarded);
            if (pos != null) q = q.OrderBy(s => pos.TryGetValue(s.Anchor, out var p) ? p : int.MaxValue).ThenBy(s => s.Number);
        }
        if (includeManual && Manual.Count > 0)
        {
            var list = q.ToList();
            var man = Manual.Where(m => !guide || !m.Hidden).ToList();
            // in der Reihenfolge-Liste geführte eigene Schritte dort einsortieren, die übrigen nach ihrem Bezugsschritt
            if (guide && pos != null)
            {
                var ordered = man.Where(m => pos.ContainsKey(m.Anchor)).ToList();
                if (ordered.Count > 0)
                {
                    list.AddRange(ordered);
                    list = list.OrderBy(s => pos.TryGetValue(s.Anchor, out var p) ? p : int.MaxValue).ThenBy(s => s.Manual ? 1 : 0).ToList();
                    man = man.Except(ordered).ToList();
                }
            }
            foreach (var m in man)
            {
                int i = m.After == 0 ? 0 : list.FindIndex(x => x.Anchor == m.After) + 1;
                if (i <= 0 && m.After != 0) i = list.Count;
                while (i < list.Count && list[i].Manual && list[i].After == m.After) i++;   // mehrere nach demselben Schritt: in Einfügereihenfolge
                list.Insert(Math.Clamp(i, 0, list.Count), m);
            }
            q = list;
        }
        q = importance switch
        {
            "primary" => q.Where(s => s.Importance == "primary" || s.Kind == "gap" || s.Manual),
            "all" => q,
            _ => q.Where(s => s.Importance != "trace" || s.Kind == "gap")
        };
        return q.ToList();
    }

    public ViewStep? ByNumber(int n) => All.FirstOrDefault(s => s.Number == n);
    public ViewStep? ById(string id) => All.FirstOrDefault(s => s.Id == id);

    public int UnreviewedCount => All.Count(s => s.Unreviewed);

    public JsonObject? Goal()
    {
        var g = (Session.Manifest.Obj("goal")?.DeepClone() as JsonObject) ?? new JsonObject();
        if (Edits.Obj("goal") is { } eg)
            foreach (var k in new[] { "description", "expected", "reference" })
                if (eg[k] != null) { var v = eg.Str(k); if (string.IsNullOrEmpty(v)) g.Remove(k); else g[k] = v; }
        if (g.Count == 0) return null;
        return (JsonObject)CleanNode(g)!;
    }

    public string Title => Clean(Edits.Obj("guide").Str("title") ?? Session.Title);

    public string Revision
    {
        get
        {
            long rev = Edits.Long("revision");
            var h = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(StepParams.BuilderVersion + "|" + Steps.Obj("source").Long("lastSeq")));
            return $"{rev}-{Convert.ToHexString(h)[..4].ToLowerInvariant()}";
        }
    }

    /// <summary>Ist ein Bild verfügbar (nicht in einem geschwärzten Zeitraum)?</summary>
    public bool FrameAvailable(string frameId)
    {
        var f = Session.Frame(frameId);
        if (f == null) return false;
        if (f.Obj("store").Str("kind") == "none") return false;
        long refSeq = f.Long("ref");
        if (_rangeRedactions.Any(r => r.images && ((refSeq >= r.from && refSeq <= r.to) || (f.Long("seq") >= r.from && f.Long("seq") <= r.to)))) return false;
        return true;
    }

    /// <summary>Bild mit angewendeten Bildschwärzungen und Verallgemeinerungsflächen. Aufrufer gibt es frei.</summary>
    public Bitmap? Frame(string? frameId)
    {
        if (frameId == null) return null;
        Bitmap? bmp;
        string m; int no;
        if (frameId.StartsWith("u:"))
        {
            // eigenes Bild des Autors: Schwärzungen gelten nur für genau dieses Bild (m = Bild-ID)
            var file = Path.Combine(AssetsDir, Path.GetFileName(frameId[2..]));
            if (!File.Exists(file)) return null;
            try { using var tmp = new Bitmap(file); bmp = new Bitmap(tmp.Width, tmp.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb); using var gg = Graphics.FromImage(bmp); gg.Clear(Color.White); gg.DrawImage(tmp, 0, 0, tmp.Width, tmp.Height); }
            catch { return null; }
            m = frameId; no = 0;
        }
        else
        {
            if (!FrameAvailable(frameId)) return null;
            bmp = Session.RestoreFrameCopy(frameId);
            if (bmp == null) return null;
            m = Session.Frame(frameId)!.Str("m") ?? "";
            no = FrameNo(frameId);
        }
        using var g = Graphics.FromImage(bmp);
        foreach (var r in _imageRedactions.Where(r => r.m == m && no >= r.fromN && no <= r.toN))
        {
            if (r.pixelate) Pixelate(bmp, g, r.rect); else g.FillRectangle(Brushes.Black, r.rect);
        }
        foreach (var sb in Edits.Arr("substitutions") ?? new JsonArray())
        {
            string ph = "{" + (sb.Str("placeholder") ?? "") + "}";
            foreach (var a in sb.Obj("images")?.Arr("areas") ?? new JsonArray())
            {
                if (a.Str("m") != m || no < FrameNo(a.Str("from")) || no > (FrameNo(a.Str("to")) is int t && t > 0 ? t : int.MaxValue)) continue;
                var rc = a.Arr("rect"); if (rc == null || rc.Count < 4) continue;
                var rect = new Rectangle(rc[0]!.GetValue<int>(), rc[1]!.GetValue<int>(), rc[2]!.GetValue<int>(), rc[3]!.GetValue<int>());
                g.FillRectangle(Brushes.Gainsboro, rect);
                using var font = new Font("Segoe UI", Math.Max(7, rect.Height * 0.5f), GraphicsUnit.Pixel);
                g.DrawString(ph, font, Brushes.Black, rect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
        }
        return bmp;
    }

    /// <summary>Bereich grob verpixeln (Blöcke ≥ 12 px – Text wird unlesbar).</summary>
    static void Pixelate(Bitmap bmp, Graphics g, Rectangle r)
    {
        r = Rectangle.Intersect(r, new Rectangle(0, 0, bmp.Width, bmp.Height));
        if (r.Width < 2 || r.Height < 2) return;
        int block = Math.Max(12, Math.Min(r.Width, r.Height) / 5);
        int sw = Math.Max(1, r.Width / block), sh = Math.Max(1, r.Height / block);
        using var part = bmp.Clone(r, bmp.PixelFormat);
        using var small = new Bitmap(sw, sh);
        using (var gs = Graphics.FromImage(small)) { gs.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear; gs.DrawImage(part, new Rectangle(0, 0, sw, sh)); }
        var im = g.InterpolationMode; var pm = g.PixelOffsetMode;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.DrawImage(small, r);
        g.InterpolationMode = im; g.PixelOffsetMode = pm;
    }

    /// <summary>Zuschnitt des Autors für die Bilder eines Schritts (Bildkoordinaten des Monitors) oder null.</summary>
    public Rectangle? CropFor(ViewStep step)
    {
        var se = (Edits.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(x => x.Long("anchor") == step.Anchor);
        return se?["crop"] is JsonArray c && c.Count >= 4 && c[2]!.GetValue<int>() >= 16 && c[3]!.GetValue<int>() >= 16
            ? new Rectangle(c[0]!.GetValue<int>(), c[1]!.GetValue<int>(), c[2]!.GetValue<int>(), c[3]!.GetValue<int>()) : null;
    }

    /// <summary>Speichert das Overlay (mit Revisionsprüfung) und wendet es neu an.</summary>
    /// <summary>
    /// Welche Mausmarkierungen in Bildern erscheinen: Voreinstellung der Sitzung (<c>guide.auto</c>), je Schritt
    /// überschreibbar (<c>steps[].auto</c>). Schlüssel: clickMarker, dragPath, elementFrame, labels, approachPath.
    /// </summary>
    public MouseMarks MarksFor(ViewStep? step)
    {
        var m = MouseMarks.From(Edits.Obj("guide")?.Obj("auto"), MouseMarks.Default);
        if (step != null && (Edits.Arr("steps") ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(x => x.Long("anchor") == step.Anchor)?.Obj("auto") is { } sm)
            m = MouseMarks.From(sm, m);
        return m;
    }

    public bool SaveEdits(string savedBy = "viewer")
    {
        long rev = Edits.Long("revision");
        var current = Session.LoadEdits();
        if ((current?.Long("revision") ?? 0) != rev) return false;
        bool ok = Session.SaveEdits(Edits, rev, savedBy);
        if (ok)
        {
            Edits = Session.LoadEdits() ?? Edits;
            try { var cache = Path.Combine(Session.Path, "cache"); if (Directory.Exists(cache)) Directory.Delete(cache, true); } catch { }
            Rebuild();
        }
        return ok;
    }

    public void ReloadEdits() { Edits = Session.LoadEdits() ?? new JsonObject(); Rebuild(); }

    /// <summary>Rohereignisse für Ausgaben (get_events): Schwärzungen, Durchsicht, ausgeblendete Kommentare.</summary>
    public JsonObject EventForOutput(JsonObject e)
    {
        long seq = e.Long("seq");
        if (InRedactedRange(seq) || (e.Arr("redacted")?.Any(x => x?.GetValue<string>() == "*") ?? false))
            return new JsonObject { ["seq"] = seq, ["t"] = e.Long("t"), ["type"] = e.Str("type"), ["redacted"] = new JsonArray("*") };
        var c = (JsonObject)CleanNode(e)!;
        if (c.Str("type") == "comment" && c.Str("text") is { } && IsHiddenComment(seq)) c["text"] = "[redacted]";
        if (Headless && c.Str("type") is "key.down" && All.FirstOrDefault(s => s.Unreviewed && InStep(s, seq)) is { })
        { c.Remove("ch"); c["key"] = "Redacted"; c["vk"] = 0; c["sc"] = 0; c["unreviewed"] = true; }
        if (Headless && c.Str("type") is "ui.value" && All.Any(s => s.Unreviewed && s.Json.Obj("target").Obj("el").Str("rid") == c.Obj("el").Str("rid")))
            c["value"] = "{unreviewed}";
        return c;
    }

    bool IsHiddenComment(long seq) => false;

    static bool InStep(ViewStep s, long seq)
    {
        foreach (var rg in s.Json.Obj("seq")?.Arr("input") ?? new JsonArray())
            if (seq >= rg![0]!.GetValue<long>() && seq <= rg[1]!.GetValue<long>()) return true;
        return false;
    }

    public void Dispose() => Session.Dispose();
}
