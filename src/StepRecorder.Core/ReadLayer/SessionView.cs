using System.Drawing;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Data;
using StepRecorder.Settings;
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
    /// <summary>Formatierter Anleitungstext (13, Paket 21); <see cref="AuthorBody"/> enthält dann den sichtbaren Text ohne Auszeichnung.</summary>
    public StepRecorder.Text.RichText? BodyRich;
    /// <summary>Formatierte Kapiteleinleitung; <see cref="ChapterText"/> enthält den sichtbaren Text.</summary>
    public StepRecorder.Text.RichText? ChapterRich;
    /// <summary>Formatierte Hinweise je Kennung (<c>comments[].id</c>); der Text im Hinweis ist dann der sichtbare Text.</summary>
    public Dictionary<string, StepRecorder.Text.RichText>? NoteRich;
    /// <summary>Vor diesem Schritt beginnt ein Kapitel (Bedienungsanleitung).</summary>
    public string? ChapterTitle, ChapterText;
    /// <summary>Ebene des hier beginnenden Kapitels: 1 = Kapitel, 2 = Unterkapitel (13, Paket 24).</summary>
    public int ChapterLevel = 1;
    /// <summary>Das hier beginnende Kapitel ist samt seinen Schritten aus der Anleitung ausgeblendet.</summary>
    public bool ChapterHidden;
    /// <summary>Eingefügter Textabschnitt (<c>inserted[].type: "text"</c>): Überschrift und Text ohne Schrittnummer und ohne Bild.</summary>
    public bool TextSection;
    /// <summary>Bild im Dokument zeigen (null = Standard) und Bildgröße (s|m|l|full, null = wie im Dokument).</summary>
    public bool? ShowImage;
    public string? ImageSize;
    public bool Hidden, Redacted, Discarded;
    public List<JsonObject> Comments = new();
    /// <summary>Interne und ausgeblendete Hinweise des Autors – nur für den Editor, erscheinen in keiner Ausgabe.</summary>
    public List<JsonObject> InternalComments = new();
    /// <summary>Aufgenommener Kommentar, den der Autor als intern markiert hat (Text in allen Ausgaben geschwärzt).</summary>
    public bool CommentHidden;
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
    /// <summary>Eigener Text existiert nur in der anderen Sprache (Qualitätsprüfung Q13).</summary>
    public bool TranslationMissing;
    /// <summary>Zusammengefasster Schritt (nur in Anleitungen): Kennung und Mitglieder in Aufnahmereihenfolge.</summary>
    public string? MergeId;
    public List<ViewStep>? Members;
    /// <summary>Beim Aufräumen ausgeblendet: Grund (null = nicht aufgeräumt).</summary>
    public string? CleanupReason;
    /// <summary>Lesezeichen des Autors (<c>edits.steps[].bookmark</c>) – nur für den Editor (Stern, Filter), erscheint in keiner Ausgabe.</summary>
    public bool Bookmarked;
    /// <summary>Aufnahmeabschnitt des Ankerereignisses (05 §16.1).</summary>
    public int Section;
    /// <summary>Nahtstelle unmittelbar vor diesem Schritt der wirksamen Folge (05 §16.4), sonst null.</summary>
    public Seam? SeamBefore;
    /// <summary>Ersetzte Schritte: Kennung der Ersetzung, die sie ersetzt; Schritte eines Ersatzblocks: Kennung der Ersetzung, zu der sie gehören.</summary>
    public string? ReplacementId;

    public ViewStep Clone() => (ViewStep)MemberwiseClone();
}

/// <summary>
/// Leseschicht: alle Ausgaben (Viewer, Anleitung, KI-Paket, MCP, Suche) lesen ausschließlich hierüber.
/// Erzwingt Schwärzungen (Bild, Text, Schritt, Zeitraum), Verallgemeinerungen, ausgeblendete Kommentare
/// und die Durchsichtsregel ({unreviewed:N} für Ausgaben ohne Durchsicht).
/// </summary>
/// <summary>Sichtbarkeit der Mausmarkierungen in Schrittbildern.</summary>
/// <summary>Sichtbarkeit der Markierungen in Schrittbildern; <see cref="KeyCaps"/> = gedrückte Tasten und getippter Text als Tastenkappen.</summary>
public sealed record MouseMarks(bool Click, bool Path, bool Target, bool Labels, bool Approach, bool KeyCaps = true, bool Number = false)
{
    public static readonly MouseMarks Default = new(true, true, true, true, false, true);
    public static readonly MouseMarks None = new(false, false, false, false, false, false);
    public bool Any => Click || Path || Target || Labels || Approach || KeyCaps || Number;

    /// <summary>Nur was in beiden eingeschaltet ist (z. B. Export-Auswahl über den Einstellungen der Sitzung).</summary>
    public MouseMarks And(MouseMarks o) => new(Click && o.Click, Path && o.Path, Target && o.Target, Labels && o.Labels, Approach && o.Approach, KeyCaps && o.KeyCaps, Number && o.Number);

    /// <summary>Maske für Exporte: Mausmarkierungen (samt Schrittnummer) und/oder Tastenkappen.</summary>
    public static MouseMarks Mask(bool mouse, bool keys) => new(mouse, mouse, mouse, mouse, mouse, keys, mouse);

    static bool B(JsonNode? n, bool basis) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : basis;

    public static MouseMarks From(JsonObject? o, MouseMarks basis) => o == null ? basis : new(
        B(o["clickMarker"], basis.Click), B(o["dragPath"], basis.Path), B(o["elementFrame"], basis.Target), B(o["labels"], basis.Labels),
        B(o["approachPath"], basis.Approach), B(o["keyCaps"], basis.KeyCaps), B(o["stepNumber"], basis.Number));

    /// <summary>Schlüssel in <c>guide.auto</c>/<c>steps[].auto</c>; <c>stepNumber</c> (13, Paket 22) ist standardmäßig aus.</summary>
    public static readonly string[] Keys = { "clickMarker", "dragPath", "elementFrame", "labels", "approachPath", "keyCaps", "stepNumber" };
}

public sealed partial class SessionView : IDisposable
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
    /// <summary>
    /// Fehlt ein Autorentext in <see cref="Language"/>, den der anderen Sprache zeigen (Editor). Anleitungs-Exporte schalten das
    /// standardmäßig ab und nehmen den automatischen Text der Exportsprache (09 §10).
    /// </summary>
    public bool AuthorTextFallback { get; set; } = true;
    /// <summary>Sprache, deren Autorentext bei fehlender Übersetzung einspringt: Deutsch ↔ Englisch; weitere Sprachen nehmen Englisch, sonst Deutsch.</summary>
    string OtherLanguage => Language == "de" ? "en" : Language == "en" ? "de" : "en";
    readonly List<(Regex rx, string placeholder)> _subst = new();
    readonly HashSet<string> _redactedStrings = new(StringComparer.Ordinal);
    readonly List<(long from, long to, bool images)> _rangeRedactions = new();
    readonly List<(string m, int fromN, int toN, Rectangle rect, bool pixelate)> _imageRedactions = new();
    /// <summary>Tastenereignisse (key.down samt zugehörigem key.up), deren Zeichen geschwärzt sind.</summary>
    readonly HashSet<long> _redactedKeySeqs = new();
    /// <summary>Anker der Tipp-Schritte, deren Eingabe (ganz oder teilweise) geschwärzt ist.</summary>
    readonly HashSet<long> _redactedInputAnchors = new();
    /// <summary>seq aufgenommener Kommentare, die der Autor als intern markiert hat.</summary>
    readonly HashSet<long> _hiddenComments = new();
    public string RedactedText => OutText.T(Language, "[geschwärzt]", "[redacted]");

    SessionView(Session s, bool headless, string? lang)
    {
        Session = s; Headless = headless; Language = lang ?? s.Language;
    }

    /// <summary>
    /// Kommentararten, die in keiner Ausgabe erscheinen: „internal“ (Autor) und „agent“ (Notiz eines KI-Agenten über MCP,
    /// sichtbar nur im Editor und in MCP – Entscheidung O-19).
    /// </summary>
    public static bool IsInternalKind(string? kind) => kind is "internal" or "agent";

    /// <summary>
    /// Schrittbereich für Ausgaben (<c>export --from/--to</c>, Nummern wie in der Schrittliste); null = alle.
    /// Eigene Schritte gehören zum Bereich ihres Bezugsschritts.
    /// </summary>
    public (int from, int to)? Range { get; set; }

    public bool InRange(ViewStep s)
    {
        if (Range is not { } r) return true;
        if (s.Manual)
        {
            if (s.After == 0) return r.from <= 1;
            var basis = All.FirstOrDefault(x => x.Anchor == s.After);
            return basis == null || basis.Number >= r.from && basis.Number <= r.to;
        }
        return s.Number >= r.from && s.Number <= r.to;
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
        _vars = null;
        _subst.Clear(); _redactedStrings.Clear(); _rangeRedactions.Clear(); _imageRedactions.Clear();
        _redactedKeySeqs.Clear(); _redactedInputAnchors.Clear(); _hiddenComments.Clear();
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
                        if (ev != null && ev.Str("type") is "key.down" or "key.up") { _redactedKeySeqs.Add(tg.Long("seq")); continue; }
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
            // Platzhaltername geprüft (04 §13.14): ein fremder, unzulässiger Name wird nie Auszeichnung oder Ersetzungsmuster
            string phName = (sb.Str("placeholder") ?? "Wert").Trim('{', '}', ' ');
            string ph = PlaceholderName.IsMatch(phName) ? "{" + phName + "}" : RedactedText;
            var m = sb.Obj("match");
            var opts = sb.Bool("caseSensitive") ? RegexOptions.None : RegexOptions.IgnoreCase;
            var timeout = TimeSpan.FromMilliseconds(200);
            try
            {
                // Wörtlicher Wert als ganzes Wort. Lookarounds kann die NonBacktracking-Engine nicht – ein maskierter
                // Literalwert kann aber nicht ausufern, darum die normale Engine mit Zeitgrenze.
                if (m.Str("literal") is { Length: >= 3 } lit) _subst.Add((new Regex(@"(?<![\p{L}\p{N}])" + Regex.Escape(lit) + @"(?![\p{L}\p{N}])", opts, timeout), ph));
                else if (m.Str("pattern") is { Length: > 0 } pat)
                {
                    Regex rx;
                    try { rx = new Regex(pat, opts | RegexOptions.NonBacktracking); }
                    catch (NotSupportedException) { rx = new Regex(pat, opts, timeout); }
                    _subst.Add((rx, ph));
                }
            }
            catch (ArgumentException) { }   // ungültiges Muster: bleibt wirkungslos (der Editor prüft beim Anlegen)
        }
        _mergeOf = null;
        if (_redactedKeySeqs.Count > 0) ApplyKeyRedactions();
        foreach (var c in (Edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>())
            if (c.Str("origin") == "recording" && (c.Bool("hidden") || c.Str("kind") == "internal") && c.LongN("sourceSeq") is long src) _hiddenComments.Add(src);
        BuildReviewItems();
        BuildSteps();
    }

    /// <summary>
    /// Geschwärzte Tastenanschläge: das zugehörige key.up gilt mit, und der getippte Text der betroffenen Schritte
    /// ist überall geschwärzt (auch in Feldwerten, die ihn wiederholen).
    /// </summary>
    void ApplyKeyRedactions()
    {
        foreach (var up in Redactor.PairedKeyUps(Session.Events, new HashSet<long>(_redactedKeySeqs))) _redactedKeySeqs.Add(up.Long("seq"));
        foreach (var s in (Steps.Arr("steps") ?? new JsonArray()).OfType<JsonObject>())
        {
            if (s.Str("kind") != "type") continue;
            bool hit = false;
            foreach (var rg in s.Obj("seq")?.Arr("input") ?? new JsonArray())
            {
                long a = rg![0]!.GetValue<long>(), b = rg[1]!.GetValue<long>();
                if (_redactedKeySeqs.Any(q => q >= a && q <= b)) { hit = true; break; }
            }
            if (!hit) continue;
            _redactedInputAnchors.Add(s.Long("anchorSeq"));
            foreach (var f in new[] { "text", "valueAfter" })
                if (s.Obj("input")?.Str(f) is { Length: >= 2 } v && v.Trim().Length >= 2) _redactedStrings.Add(v);
        }
    }

    /// <summary>Getippten Inhalt eines Schritts durch den Schwärzungsplatzhalter ersetzen.</summary>
    JsonObject WithRedactedInput(JsonObject step)
    {
        var c = (JsonObject)step.DeepClone();
        var input = c.Obj("input");
        if (input == null) return c;
        input["tokens"] = new JsonArray(RedactedText);
        input["text"] = RedactedText;
        input.Remove("valueAfter"); input.Remove("valueBefore");
        c["action"] = null;
        return c;
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

    static readonly Regex PlaceholderName = new(@"^[\p{L}\p{N}][\p{L}\p{N} _.\-]{0,39}$", RegexOptions.Compiled);

    /// <summary>Wendet Textschwärzungen und Verallgemeinerungen auf einen Text an.</summary>
    public string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        foreach (var r in _redactedStrings.OrderByDescending(s => s.Length)) text = text.Replace(r, RedactedText, StringComparison.Ordinal);
        foreach (var (rx, ph) in _subst) { try { text = rx.Replace(text, _ => ph); } catch { } }
        return MaskUnreviewed(text);
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
        var allComments = (Edits.Arr("comments") ?? new JsonArray()).OfType<JsonObject>().Where(c => c.Str("origin") != "recording").ToList();
        var comments = allComments.Where(c => !IsInternalKind(c.Str("kind")) && !c.Bool("hidden")).ToList();
        var internalComments = allComments.Where(c => IsInternalKind(c.Str("kind")) || c.Bool("hidden")).ToList();
        var anns = (Edits.Arr("annotations") ?? new JsonArray()).OfType<JsonObject>().ToList();
        var review = ReviewState();
        foreach (var sNode in Steps.Arr("steps") ?? new JsonArray())
        {
            if (sNode is not JsonObject s) continue;
            var vs = new ViewStep { Id = s.Str("id") ?? "", Anchor = s.Long("anchorSeq"), Kind = s.Str("kind") ?? "", Importance = s.Str("importance") ?? "primary", TFrom = s.Long("tFrom"), TTo = s.Long("tTo") };
            vs.Redacted = StepRedacted(s) && vs.Kind != "gap";
            var clean = (JsonObject)CleanNode(s)!;
            clean.Remove("prov");
            bool inputRedacted = _redactedInputAnchors.Contains(vs.Anchor);
            vs.Json = inputRedacted ? WithRedactedInput(clean) : clean;
            vs.Discarded = s.StrList("flags").Contains("discarded");
            if (stepEdits.TryGetValue(vs.Anchor, out var se))
            {
                if (se["hidden"] != null) vs.Hidden = se.Bool("hidden");
                if (se["discarded"] != null) vs.Discarded = se.Bool("discarded");
                vs.Bookmarked = se.Bool("bookmark");
                if (vs.Hidden && se.Obj("cleanup")?.Str("reason") is { } cr) vs.CleanupReason = StepCleanup.ReasonFor(se.Obj("cleanup")?.Str("rule")) ?? cr;
                if (se.Str("importance") is { } imp) vs.Importance = imp;
                ApplyAuthorText(vs, se.Obj("text"));
                if (se.Obj("frameChoice") is { } fc) { vs.PreFrame = fc.Str("pre"); vs.PostFrame = fc.Str("post"); }
                ApplyChapter(vs, se);
                if (se["showImage"] != null) vs.ShowImage = se.Bool("showImage");
                if (se.Str("imageSize") is { Length: > 0 } isz) vs.ImageSize = isz;
            }
            vs.PreFrame ??= s.Obj("frames").Str("pre");
            vs.PostFrame ??= s.Obj("frames").Str("post");
            vs.Monitor = s.Obj("frames").Str("m");
            vs.Comments = comments.Where(c => c.Long("anchor") == vs.Anchor).Select(c => CleanComment(c, vs)).ToList();
            vs.InternalComments = internalComments.Where(c => c.Long("anchor") == vs.Anchor).Select(c => (JsonObject)CleanNode(c)!).ToList();
            vs.Annotations = anns.Where(a => a.Long("anchor") == vs.Anchor || (a.Str("frame") is { } f && (f == vs.PreFrame || f == vs.PostFrame))).ToList();
            if (vs.Kind == "comment" && IsHiddenComment(s.Obj("input").Long("refSeq"))) { vs.CommentHidden = true; vs.Hidden = true; }
            // Durchsicht: auffällige getippte Eingaben (geschwärzte Eingaben brauchen keine Durchsicht mehr)
            if (vs.Kind == "type" && !s.Obj("input").Bool("masked") && !inputRedacted)
            {
                string text = (s.Obj("input").Str("text") ?? TextTemplates.TokensNotation(s.Obj("input")!)) + " " + s.Obj("input").Str("valueAfter");
                vs.FlagReason = Patterns.Flag(text) ?? (s.StrList("flags").Contains("plaintext") ? "plaintextWindow" : null);
                if (vs.FlagReason != null && !review.Contains(vs.Anchor)) vs.Unreviewed = true;
            }
            if (vs.Redacted)
            {
                vs.Title = OutText.T(Language, "Schritt geschwärzt", "Step redacted");
                vs.Protocol = vs.Guide = vs.Title;
                vs.Json = new JsonObject { ["id"] = vs.Id, ["kind"] = vs.Kind, ["anchorSeq"] = vs.Anchor, ["tFrom"] = vs.TFrom, ["tTo"] = vs.TTo, ["importance"] = vs.Importance, ["redacted"] = true, ["evidence"] = new JsonObject { ["level"] = "unknown" } };
                vs.PreFrame = vs.PostFrame = null;
            }
            else
            {
                string? commentText = vs.Kind != "comment" ? null : vs.CommentHidden ? RedactedText : Clean(Session.BySeq(s.Obj("input").Long("refSeq"))?.Str("text"));
                var forText = vs.Json;
                if (vs.Unreviewed && Headless) forText = WithUnreviewed(vs.Json);
                _glossary ??= Glossary.FromSettings();
                vs.Title = _glossary.Apply(Clean(TextTemplates.Title(forText, Language)));
                vs.Protocol = Clean(TextTemplates.Body(forText, Language, "protocol", "formal", commentText));
                vs.Guide = _glossary.Apply(Clean(TextTemplates.Body(forText, Language, "guide", AppAddress(), commentText)));
                if (vs.Unreviewed && Headless) vs.Json = forText;
            }
            list.Add(vs);
        }
        // Abschnitte, Ersetzungen und Nahtstellen: All ist die wirksame Schrittfolge (05 §16)
        ApplySections(list);

        // vom Autor eingefügte Schritte
        var manual = new List<ViewStep>();
        foreach (var ins in (Edits.Arr("inserted") ?? new JsonArray()).OfType<JsonObject>())
        {
            long anchor = ins.Long("anchor");
            if (anchor >= 0) continue;
            var vs = new ViewStep
            {
                Manual = true, Id = "m" + (-anchor), Anchor = anchor, After = ins.Long("after"), Kind = "manual", Importance = "primary", TextSection = ins.Str("type") == "text",
                Title = Clean(ins.Str("title") ?? OutText.T(Language, "Hinweis", "Note")), Guide = Clean(ins.Str("body") ?? ""),
                PreFrame = ins.Str("image"), TFrom = list.FirstOrDefault(x => x.Anchor == ins.Long("after"))?.TTo ?? 0
            };
            vs.TTo = vs.TFrom;
            vs.Protocol = vs.Guide;
            vs.Json = new JsonObject { ["id"] = vs.Id, ["kind"] = "manual", ["anchorSeq"] = anchor, ["importance"] = "primary", ["evidence"] = new JsonObject { ["level"] = "author" } };
            if (stepEdits.TryGetValue(anchor, out var se))
            {
                if (se["hidden"] != null) vs.Hidden = se.Bool("hidden");
                vs.Bookmarked = se.Bool("bookmark");
                ApplyAuthorText(vs, se.Obj("text"));
                ApplyChapter(vs, se);
                if (se["showImage"] != null) vs.ShowImage = se.Bool("showImage");
                if (se.Str("imageSize") is { Length: > 0 } isz) vs.ImageSize = isz;
            }
            vs.Comments = comments.Where(c => c.Long("anchor") == anchor).Select(c => CleanComment(c, vs)).ToList();
            vs.InternalComments = internalComments.Where(c => c.Long("anchor") == anchor).Select(c => (JsonObject)CleanNode(c)!).ToList();
            vs.Annotations = anns.Where(a => a.Long("anchor") == anchor || (a.Str("frame") is { } f && f == vs.PreFrame)).ToList();
            manual.Add(vs);
        }
        Manual = manual;
    }

    /// <summary>Autorentext der Sprache übernehmen; fehlt er, je nach <see cref="AuthorTextFallback"/> den der anderen Sprache.</summary>
    void ApplyAuthorText(ViewStep vs, JsonObject? text)
    {
        var own = text?.Obj(Language);
        var other = text?.Obj(OtherLanguage) ?? (Language is "de" or "en" ? null : text?.Obj("de"));
        vs.TranslationMissing = own == null && other != null;
        var txt = own ?? (AuthorTextFallback ? other : null);
        if (txt != null)
        {
            vs.AuthorTitle = CleanVars(txt.Str("title"));
            if (IsRich(txt) && txt.Str("body") is { } rb) { vs.BodyRich = SealRich(rb); vs.AuthorBody = vs.BodyRich.Plain; }
            else vs.AuthorBody = CleanVars(txt.Str("body"));
        }
    }

    /// <summary>Kapitel am Schritt; Titel und Text je Sprache (chapter.en überschreibt für Englisch).</summary>
    void ApplyChapter(ViewStep vs, JsonObject se)
    {
        if (se.Obj("chapter") is not { } ch) return;
        var loc = Language != "de" ? ch.Obj(Language) : null;
        string? title = loc?.Str("title") is { Length: > 0 } lt ? lt : ch.Str("title");
        if (title is not { Length: > 0 }) return;
        vs.ChapterTitle = CleanVars(title);
        vs.ChapterLevel = ch.Int("level") == 2 ? 2 : 1;
        vs.ChapterHidden = ch.Bool("hidden");
        bool own = loc?.Str("text") is { Length: > 0 };
        string? text = own ? loc!.Str("text") : ch.Str("text");
        if (IsRich(own ? loc : ch) && text != null) { vs.ChapterRich = SealRich(text); vs.ChapterText = vs.ChapterRich.Plain; }
        else vs.ChapterText = CleanVars(text);
    }

    Glossary? _glossary;
    /// <summary>Begriffsliste neu laden (nach Änderung in den Einstellungen).</summary>
    public void ReloadGlossary() { _glossary = null; Rebuild(); }
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

    /// <summary>
    /// Wirksame Schritte (ohne verworfene Abschnitte). guide=true wendet Reihenfolge und Ausblenden an;
    /// ordered=true nur die Reihenfolge (Schrittliste des Editors: Ausgeblendete bleiben sichtbar).
    /// </summary>
    public List<ViewStep> Effective(bool guide = false, string importance = "secondary", bool includeManual = false, bool ordered = false)
    {
        IEnumerable<ViewStep> q = All;
        Dictionary<long, int>? pos = null;
        var order = Edits.Arr("order");
        if (order is { Count: > 0 })
            pos = order.Select((o, i) => (key: o is JsonValue v && v.TryGetValue<long>(out var l) ? l : long.MinValue, i)).Where(x => x.key != long.MinValue).GroupBy(x => x.key).ToDictionary(g => g.Key, g => g.First().i);
        bool hide = guide, applyOrder = guide || ordered;
        if (hide) q = q.Where(s => !s.Hidden && !s.Discarded);
        if (applyOrder && pos != null) q = q.OrderBy(s => OrderKey(s, pos)).ThenBy(s => s.Number);
        if (includeManual && Manual.Count > 0)
        {
            var list = q.ToList();
            var man = Manual.Where(m => !hide || !m.Hidden).ToList();
            // in der Reihenfolge-Liste geführte eigene Schritte dort einsortieren, die übrigen nach ihrem Bezugsschritt
            if (applyOrder && pos != null)
            {
                var inOrder = man.Where(m => pos.ContainsKey(m.Anchor)).ToList();
                if (inOrder.Count > 0)
                {
                    list.AddRange(inOrder);
                    list = list.OrderBy(s => OrderKey(s, pos)).ThenBy(s => s.Manual ? 1 : 0).ToList();
                    man = man.Except(inOrder).ToList();
                }
            }
            foreach (var m in man)
            {
                long after = MapAfter(m.After);   // Bezugsschritt ersetzt: hinter dem Ersatz
                int i = after == 0 ? 0 : list.FindIndex(x => x.Anchor == after) + 1;
                if (i <= 0 && after != 0) i = list.Count;
                while (i < list.Count && list[i].Manual && MapAfter(list[i].After) == after) i++;   // mehrere nach demselben Schritt: in Einfügereihenfolge
                list.Insert(Math.Clamp(i, 0, list.Count), m);
            }
            q = list;
        }
        if (Range != null) q = q.Where(InRange);
        q = importance switch
        {
            "primary" => q.Where(s => s.Importance == "primary" || s.Kind == "gap" || s.Manual),
            "all" => q,
            _ => q.Where(s => s.Importance != "trace" || s.Kind == "gap")
        };
        return q.ToList();
    }

    // ---------------- Zusammengefasste Schritte (nur Anleitungen) ----------------

    sealed record MergeInfo(string Id, List<long> Anchors, JsonObject? Text);
    Dictionary<long, MergeInfo>? _mergeOf;

    /// <summary>
    /// <c>edits.merges[]</c>: Gruppen aufeinanderfolgender Schritte, die in Anleitungen als ein Schritt erscheinen.
    /// Der erste Anker führt; ein Schritt gehört höchstens zu einer Gruppe; unbekannte Anker werden übergangen.
    /// </summary>
    Dictionary<long, MergeInfo> Merges()
    {
        if (_mergeOf != null) return _mergeOf;
        var map = new Dictionary<long, MergeInfo>();
        var known = All.ToDictionary(s => s.Anchor);
        var inactive = Inactive.Select(s => s.Anchor).ToHashSet();
        foreach (var m in (Edits.Arr("merges") ?? new JsonArray()).OfType<JsonObject>())
        {
            var raw = (m.Arr("anchors") ?? new JsonArray()).Select(a => a is JsonValue v && v.TryGetValue<long>(out var l) ? l : long.MinValue).ToList();
            if (raw.Any(inactive.Contains)) continue;   // Gruppen mit ersetzten Schritten ruhen (05 §16.6)
            var anchors = raw.Where(a => known.ContainsKey(a) && !map.ContainsKey(a)).Distinct().ToList();
            if (anchors.Count < 2) continue;
            var info = new MergeInfo(m.Str("id") ?? "", anchors, m.Obj("text"));
            foreach (var a in anchors) map[a] = info;
        }
        return _mergeOf = map;
    }

    /// <summary>Zugehörigkeit eines Schritts zu einer Gruppe: Kennung, ob führend, Größe, Nummer des führenden Schritts.</summary>
    public (string id, bool lead, int count, int leadNumber, List<int> numbers)? MergeOf(long anchor)
    {
        if (!Merges().TryGetValue(anchor, out var m)) return null;
        var nums = m.Anchors.Select(a => All.First(s => s.Anchor == a).Number).OrderBy(n => n).ToList();
        return (m.Id, m.Anchors[0] == anchor, m.Anchors.Count, All.First(s => s.Anchor == m.Anchors[0]).Number, nums);
    }

    /// <summary>
    /// Schritte, wie Anleitungen sie zeigen: <see cref="Effective"/> mit Reihenfolge, Ausblenden und eigenen Schritten,
    /// zusammengefasste Gruppen als ein Schritt. KI-Paket, MCP, Vergleich und AutoHotkey zeigen weiter die Einzelschritte.
    /// </summary>
    public List<ViewStep> GuideSteps(string importance = "primary", bool includeManual = true)
    {
        var merges = Merges();
        var list = Effective(guide: true, importance: "all", includeManual: includeManual);
        var result = new List<ViewStep>();
        foreach (var s in list)
        {
            if (!s.Manual && merges.TryGetValue(s.Anchor, out var m))
            {
                if (m.Anchors[0] != s.Anchor) continue;   // Mitglied: im führenden Schritt enthalten
                result.Add(Composite(s, m));
            }
            else result.Add(s);
        }
        // Mitglieder ohne sichtbaren führenden Schritt bleiben verborgen (führender Schritt ausgeblendet = Gruppe ausgeblendet)
        IEnumerable<ViewStep> q = result;
        q = importance switch
        {
            "primary" => q.Where(s => s.Importance == "primary" || s.Kind == "gap" || s.Manual),
            "all" => q,
            _ => q.Where(s => s.Importance != "trace" || s.Kind == "gap")
        };
        return ApplyOutline(q.ToList(), includeManual);
    }

    /// <summary>
    /// Gliederung der Anleitung (13, Paket 24): Schritte ausgeblendeter Kapitel entfallen; eine Kapitelüberschrift, deren erster Schritt
    /// nicht gezeigt wird (ausgeblendet, Nebenschritt, Mitglied einer Zusammenfassung), steht vor dem nächsten gezeigten Schritt ihres
    /// Bereichs – sie entfällt nur, wenn der Bereich keinen gezeigten Schritt hat (genehmigte Korrektur; früher ging sie verloren).
    /// </summary>
    List<ViewStep> ApplyOutline(List<ViewStep> shown, bool includeManual)
    {
        bool anyChapter = All.Any(s => s.ChapterTitle != null) || Manual.Any(s => s.ChapterTitle != null);
        if (!anyChapter) return shown;
        var byAnchor = new Dictionary<long, int>();
        for (int i = 0; i < shown.Count; i++) byAnchor.TryAdd(shown[i].Anchor, i);
        var full = Effective(importance: "all", includeManual: includeManual, ordered: true).Where(s => !s.Discarded).ToList();
        var drop = new HashSet<long>();
        var result = new List<ViewStep>(shown);
        ViewStep? pending = null;
        int? hiddenLevel = null;
        foreach (var s in full)
        {
            if (s.ChapterTitle is { Length: > 0 })
            {
                if (hiddenLevel != null && s.ChapterLevel <= hiddenLevel) hiddenLevel = null;
                if (hiddenLevel == null && s.ChapterHidden) hiddenLevel = s.ChapterLevel;
                pending = hiddenLevel == null ? s : null;   // eine neue Überschrift ersetzt eine noch nicht gesetzte (ihr Bereich blieb leer)
            }
            if (hiddenLevel != null) { drop.Add(s.Anchor); continue; }
            if (!byAnchor.TryGetValue(s.Anchor, out int idx)) continue;
            if (pending != null && pending.Anchor != s.Anchor)
            {
                var c = result[idx] == shown[idx] ? result[idx].Clone() : result[idx];
                c.ChapterTitle = pending.ChapterTitle; c.ChapterText = pending.ChapterText; c.ChapterRich = pending.ChapterRich;
                c.ChapterLevel = pending.ChapterLevel; c.ChapterHidden = false;
                result[idx] = c;
            }
            pending = null;
        }
        // Schritte, deren eigene Überschrift ein ausgeblendetes Kapitel beginnt, sind schon in drop; gezeigte Schritte ohne Eintrag in full (Bereich) bleiben
        return drop.Count == 0 ? result : result.Where(s => !drop.Contains(s.Anchor)).ToList();
    }

    /// <summary>Der zusammengefasste Schritt eines führenden Schritts (null, wenn er keine Gruppe führt).</summary>
    public ViewStep? GuideStepFor(ViewStep lead) => !lead.Manual && Merges().TryGetValue(lead.Anchor, out var m) && m.Anchors[0] == lead.Anchor ? Composite(lead, m) : null;

    ViewStep Composite(ViewStep lead, MergeInfo m)
    {
        var members = m.Anchors.Select(a => All.First(s => s.Anchor == a)).OrderBy(s => s.Number).ToList();
        var visible = members.Where(s => !s.Redacted && (s == lead || !s.Hidden)).ToList();
        var c = lead.Clone();
        c.MergeId = m.Id;
        c.Members = members;
        c.TFrom = members.Min(s => s.TFrom); c.TTo = members.Max(s => s.TTo);
        c.PreFrame = visible.FirstOrDefault(s => s.PreFrame != null)?.PreFrame ?? lead.PreFrame;
        c.PostFrame = visible.LastOrDefault(s => s.PostFrame != null)?.PostFrame ?? lead.PostFrame;
        c.Importance = members.Any(s => s.Importance == "primary") ? "primary" : members.Any(s => s.Importance == "secondary") ? "secondary" : "trace";
        c.Unreviewed = members.Any(s => s.Unreviewed);
        c.Comments = members.SelectMany(s => s.Comments).ToList();
        c.Guide = string.Join(" ", visible.Select(s => (s.AuthorBody ?? s.Guide).Trim()).Where(t => t.Length > 0));
        c.Protocol = string.Join(" ", visible.Select(s => s.Protocol.Trim()).Where(t => t.Length > 0));
        c.AuthorTitle = null; c.AuthorBody = null; c.BodyRich = null; c.TranslationMissing = false;
        if (members.Any(x => x.NoteRich != null)) c.NoteRich = members.Where(x => x.NoteRich != null).SelectMany(x => x.NoteRich!).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.First().Value);
        var own = m.Text?.Obj(Language);
        var other = m.Text?.Obj(OtherLanguage) ?? (Language is "de" or "en" ? null : m.Text?.Obj("de"));
        var txt = own ?? (AuthorTextFallback ? other : null);
        c.TranslationMissing = own == null && other != null;
        if (txt != null)
        {
            c.AuthorTitle = CleanVars(txt.Str("title"));
            if (IsRich(txt) && txt.Str("body") is { } rb) { c.BodyRich = SealRich(rb); c.AuthorBody = c.BodyRich.Plain; }
            else c.AuthorBody = CleanVars(txt.Str("body"));
        }
        else c.AuthorTitle = lead.AuthorTitle;
        return c;
    }

    public ViewStep? ByNumber(int n) => All.FirstOrDefault(s => s.Number == n);
    public ViewStep? ById(string id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>Ungeprüfte auffällige Eingaben und pflichtige Prüfposten (Programmausgaben, auffällige Texte des Autors).</summary>
    /// <remarks>Zählt auch ersetzte und nicht verwendete Schritte: Die Durchsicht gilt unabhängig vom Zustand (05 §16.5).</remarks>
    public int UnreviewedCount => All.Count(s => s.Unreviewed) + Inactive.Count(s => s.Unreviewed) + UnreviewedItems;
    /// <summary>Wirksame Schritte und dahinter ersetzte/nicht verwendete (für Durchsicht und bereinigte Kopie).</summary>
    public IEnumerable<ViewStep> AllWithInactive => All.Concat(Inactive);

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

    /// <summary>Stil der Mausmarken (<c>guide.auto.style</c>, 13 Paket 22) oder null = Klassisch.</summary>
    public Export.MarkStyle? MarkStyle() => Export.MarkStyle.From(Edits.Obj("guide")?.Obj("auto")?.Obj("style"));

    public bool SaveEdits(string savedBy = "viewer")
    {
        long rev = Edits.Long("revision");
        var current = Session.LoadEdits();
        if ((current?.Long("revision") ?? 0) != rev) return false;
        // Bearbeitungsstand sichern: erstes Speichern nach dem Öffnen, sonst höchstens alle 10 Minuten, vor großen Änderungen immer
        if (!Session.ReadOnly)
        {
            try { EditHistory.MaybeSnapshot(Session.Path, current, SnapshotBefore ?? (_historySaved ? "auto" : "open"), force: SnapshotBefore != null || !_historySaved); }
            catch (Exception ex) { Settings.AppLog.Write("Bearbeitungsstand nicht gesichert: " + ex.Message); }
            _historySaved = true;
        }
        SnapshotBefore = null;
        // Abgeleitetes (Suchindex, Vorschaubilder) zuerst löschen: stürzt das Programm zwischen den beiden Schritten ab,
        // darf kein Zwischenspeicher übrig bleiben, der eine gerade geschwärzte Stelle noch enthält
        ClearCache();
        bool ok = Session.SaveEdits(Edits, rev, savedBy);
        if (ok)
        {
            Edits = Session.LoadEdits() ?? Edits;
            ClearCache();
            Rebuild();
        }
        return ok;
    }

    public void ReloadEdits() { Edits = Session.LoadEdits() ?? new JsonObject(); Rebuild(); }

    bool _historySaved;
    /// <summary>Vor einer großen Änderung setzen (z. B. „replaceAll“): der nächste Speichervorgang sichert den bisherigen Stand.</summary>
    public string? SnapshotBefore { get; set; }

    void ClearCache()
    {
        if (Session.ReadOnly) return;
        try { var cache = Path.Combine(Session.Path, "cache"); if (Directory.Exists(cache)) Directory.Delete(cache, true); } catch { }
    }

    /// <summary>Rohereignisse für Ausgaben (get_events): Schwärzungen, Durchsicht, ausgeblendete Kommentare.</summary>
    public JsonObject EventForOutput(JsonObject e)
    {
        long seq = e.Long("seq");
        if (InRedactedRange(seq) || (e.Arr("redacted")?.Any(x => x?.GetValue<string>() == "*") ?? false))
            return new JsonObject { ["seq"] = seq, ["t"] = e.Long("t"), ["type"] = e.Str("type"), ["redacted"] = new JsonArray("*") };
        var c = (JsonObject)CleanNode(e)!;
        if (c.Str("type") == "comment" && c.Str("text") is { } && IsHiddenComment(seq)) c["text"] = RedactedText;
        ContentForOutput(c, seq);
        bool key = c.Str("type") is "key.down" or "key.up";
        if (key && _redactedKeySeqs.Contains(seq))
        {
            c.Remove("ch"); c["key"] = "Redacted"; c["vk"] = 0; c["sc"] = 0; c["redacted"] = new JsonArray("ch");
            return c;
        }
        if (Headless && key && All.FirstOrDefault(s => s.Unreviewed && InStep(s, seq)) is { })
        { c.Remove("ch"); c["key"] = "Redacted"; c["vk"] = 0; c["sc"] = 0; c["unreviewed"] = true; }
        if (Headless && c.Str("type") is "ui.value" && All.Any(s => s.Unreviewed && s.Json.Obj("target").Obj("el").Str("rid") == c.Obj("el").Str("rid")))
            c["value"] = "{unreviewed}";
        return c;
    }

    /// <summary>
    /// Markierungen des Autors, die auf genau diesem Bild gezeichnet wurden (bereinigt: Texte mit Schwärzungen und Platzhaltern).
    /// Gilt gleich für Editor, Vorschaubilder und Export – Markierungen des Nachher-Bilds erscheinen nicht auf dem Vorher-Bild.
    /// </summary>
    public List<JsonObject> AnnotationsFor(ViewStep step, string frameId)
        => (Edits.Arr("annotations") ?? new JsonArray()).OfType<JsonObject>().Where(a => a.Str("frame") == frameId).Select(a => (JsonObject)CleanNode(a)!).ToList();

    /// <summary>Aufgenommener Kommentar, den der Autor als intern markiert hat (erscheint in keiner Ausgabe).</summary>
    public bool IsHiddenComment(long seq) => _hiddenComments.Contains(seq);

    /// <summary>Originaltext eines aufgenommenen Kommentars – nur für die Anzeige im Editor, nie für Ausgaben.</summary>
    public string? OriginalCommentText(ViewStep s) => s.Kind == "comment" ? Clean(Session.BySeq(s.Json.Obj("input").Long("refSeq"))?.Str("text")) : null;

    static bool InStep(ViewStep s, long seq)
    {
        foreach (var rg in s.Json.Obj("seq")?.Arr("input") ?? new JsonArray())
            if (seq >= rg![0]!.GetValue<long>() && seq <= rg[1]!.GetValue<long>()) return true;
        return false;
    }

    public void Dispose() => Session.Dispose();
}
