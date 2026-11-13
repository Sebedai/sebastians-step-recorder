using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Export;

namespace StepRecorder.Settings;

/// <summary>Eine Änderung beim Import oder Zurücksetzen; schwächende Änderungen tragen eine Kennung, die einzeln bestätigt werden muss.</summary>
public sealed record ImportChange(string Part, string Key, string De, string En, string? WeakeningId = null)
{
    public string Text => Loc.L(De, En);
    public bool Weakens => WeakeningId != null;
}

/// <summary>Ergebnis der Prüfung einer Einstellungsdatei: was sich ändern würde, was abgelehnt wurde.</summary>
public sealed class ImportPlan
{
    public List<ImportChange> Changes { get; } = new();
    /// <summary>Fehler, die die ganze Datei unbrauchbar machen (dann ändert sich nichts).</summary>
    public List<string> Errors { get; } = new();
    /// <summary>Teile, die wegen ungültiger Inhalte übersprungen werden (Text in Oberflächensprache).</summary>
    public List<string> SkippedParts { get; } = new();
    /// <summary>Unbekannte Teile (von einer neueren Version) – werden ignoriert.</summary>
    public List<string> UnknownParts { get; } = new();
    public bool Ok => Errors.Count == 0;
    /// <summary>Einstellungen, wenn alles bestätigt wird.</summary>
    internal AppSettings? Candidate { get; set; }
    public IEnumerable<ImportChange> Weakening => Changes.Where(c => c.Weakens);
}

/// <summary>
/// Einstellungen, Vorlagen und Profile austauschen (07 §13.2, BED-160–163): ohne persönliche Angaben, jede schwächende
/// Änderung einzeln bestätigt (E28, T-17), ungültige Dateien ändern nichts.
/// </summary>
public static class SettingsExchange
{
    public const string Format = "steprecorder.settings-exchange";
    public const int FormatVersion = 1;
    public const int MaxBytes = 32 * 1024 * 1024;
    public static readonly string[] Parts = { "hotkeys", "excluded", "exportTemplates", "textSnippets", "customProfile", "preferences" };

    public static string PartName(string part) => part switch
    {
        "hotkeys" => Loc.L("Tastenkürzel", "Keyboard shortcuts"),
        "excluded" => Loc.L("Ausgeschlossene Programme", "Excluded programs"),
        "exportTemplates" => Loc.L("Dokumentvorlagen", "Document templates"),
        "textSnippets" => Loc.L("Textbausteine", "Text snippets"),
        "customProfile" => Loc.L("Modus „Eigene“", "Custom mode"),
        "preferences" => Loc.L("Allgemeine Einstellungen", "General preferences"),
        _ => part
    };

    static readonly Regex ExeName = new(@"^[\w .\-()+&]{1,96}\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // ---------------- Export ----------------

    public static JsonObject Export(AppSettings s, IEnumerable<string>? parts = null)
    {
        var want = (parts ?? Parts).ToHashSet();
        var p = new JsonObject();
        if (want.Contains("hotkeys")) p["hotkeys"] = new JsonObject(s.EffectiveHotkeys().Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
        if (want.Contains("excluded")) p["excluded"] = new JsonArray(s.Excluded.Select(x => (JsonNode)x).ToArray());
        if (want.Contains("exportTemplates"))
            p["exportTemplates"] = new JsonObject
            {
                ["templates"] = new JsonObject(s.DocTemplates.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value.DeepClone()))),
                ["default"] = s.DefaultTemplate
            };
        if (want.Contains("textSnippets"))
            p["textSnippets"] = new JsonArray(TextSnippet.Clean(s.TextSnippets).Select(t => (JsonNode)new JsonObject { ["name"] = t.Name, ["text"] = t.Text }).ToArray());
        if (want.Contains("customProfile"))
            p["customProfile"] = new JsonObject
            {
                ["record"] = new JsonObject(RecordingOptions.Channels.Select(c => KeyValuePair.Create(c.key, (JsonNode?)s.Records(c.key)))),
                ["changeFrames"] = s.ChangeFrames, ["appContext"] = s.CustomAppContext, ["afterStop"] = s.CustomAfterStop
            };
        if (want.Contains("preferences"))
            p["preferences"] = new JsonObject
            {
                ["language"] = s.Language, ["uiLanguage"] = s.UiLanguage, ["theme"] = s.Theme, ["address"] = s.Address,
                ["countdown"] = s.Countdown, ["soundCues"] = s.SoundCues, ["privacyLevel"] = s.PrivacyLevel
            };
        // Bewusst nie enthalten: Aufnahmeordner, zuletzt verwendete Aufnahmen, Ausweichmodus der Unsichtbarkeit, unbekannte Schlüssel
        return new JsonObject
        {
            ["format"] = Format, ["formatVersion"] = FormatVersion, ["createdUtc"] = Json.UtcNow(),
            ["app"] = new JsonObject { ["name"] = AppInfo.Name, ["version"] = AppInfo.Version },
            ["parts"] = p
        };
    }

    // ---------------- Import ----------------

    public static ImportPlan Plan(string json, AppSettings current, IEnumerable<string>? parts = null)
    {
        var plan = new ImportPlan();
        if (json.Length > MaxBytes) { plan.Errors.Add(Loc.L("Die Datei ist zu groß.", "The file is too large.")); return plan; }
        JsonObject? root;
        try { root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject; }
        catch { root = null; }
        if (root == null) { plan.Errors.Add(Loc.L("Keine gültige JSON-Datei.", "Not a valid JSON file.")); return plan; }
        if (root.Str("format") != Format) { plan.Errors.Add(Loc.L("Keine Einstellungsdatei von StepRecorder.", "Not a StepRecorder settings file.")); return plan; }
        if (root.Int("formatVersion", 0) is var fv && (fv < 1 || fv > FormatVersion)) { plan.Errors.Add(Loc.F("Formatversion {0} wird nicht unterstützt.", "Format version {0} is not supported.", fv)); return plan; }
        var p = root.Obj("parts");
        if (p == null) { plan.Errors.Add(Loc.L("Die Datei enthält keine Einstellungen.", "The file contains no settings.")); return plan; }
        var want = (parts ?? Parts).ToHashSet();
        foreach (var k in p.Select(kv => kv.Key)) if (!Parts.Contains(k)) plan.UnknownParts.Add(k);

        var cand = current.Clone();
        void Skip(string part, string de, string en) => plan.SkippedParts.Add(PartName(part) + ": " + Loc.L(de, en));

        // Tastenkürzel
        if (want.Contains("hotkeys") && p.Obj("hotkeys") is { } hk)
        {
            var map = current.EffectiveHotkeys();
            bool bad = false;
            foreach (var (id, v) in hk)
            {
                if (HotkeyActions.ById(id) == null) continue;
                if (v is not JsonValue jv || !jv.TryGetValue<string>(out var text)) { bad = true; break; }
                if (text.Length > 0 && !HotkeyCombo.TryParse(text, out _)) { bad = true; break; }
                map[id] = text.Length > 0 && HotkeyCombo.TryParse(text, out var c) ? c.ToString() : "";
            }
            var issues = HotkeyRules.Validate(map).Where(i => i.Kind is "required" or "duplicate" or "invalid" or "reserved").ToList();
            if (bad || issues.Count > 0) Skip("hotkeys", "ungültig oder ohne Tastenkürzel zum Beenden – nicht übernommen.", "invalid or missing a stop shortcut – not applied.");
            else
            {
                foreach (var a in HotkeyActions.All)
                    if (current.Hotkey(a.Id) != map[a.Id]) plan.Changes.Add(new("hotkeys", a.Id, $"{a.De}: {Loc.Keys(current.Hotkey(a.Id))} → {Loc.Keys(map[a.Id])}", $"{a.En}: {current.Hotkey(a.Id)} → {map[a.Id]}"));
                cand.Hotkeys = map;
            }
        }

        // Ausschlussliste
        if (want.Contains("excluded") && p.Arr("excluded") is { } ex)
        {
            var list = new List<string>();
            bool bad = ex.Count > 500;
            foreach (var x in ex)
            {
                if (x is not JsonValue v || !v.TryGetValue<string>(out var name) || !ExeName.IsMatch(name.Trim())) { bad = true; break; }
                if (!list.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase)) list.Add(name.Trim());
            }
            if (bad) Skip("excluded", "enthält ungültige Programmnamen – nicht übernommen.", "contains invalid program names – not applied.");
            else
            {
                var removed = current.Excluded.Where(c => !list.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
                var added = list.Where(c => !current.Excluded.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
                if (added.Count > 0) plan.Changes.Add(new("excluded", "added", "Neu ausgeschlossen: " + string.Join(", ", added), "Newly excluded: " + string.Join(", ", added)));
                if (removed.Count > 0) plan.Changes.Add(new("excluded", "removed", "Nicht mehr ausgeschlossen (wird dann aufgezeichnet): " + string.Join(", ", removed), "No longer excluded (will be recorded): " + string.Join(", ", removed), "excluded"));
                cand.Excluded = list;
            }
        }

        // Dokumentvorlagen
        if (want.Contains("exportTemplates") && p.Obj("exportTemplates") is { } et)
        {
            var templates = new Dictionary<string, JsonObject>(current.DocTemplates);
            bool bad = false;
            foreach (var (name, t) in et.Obj("templates") ?? new JsonObject())
            {
                if (name.Trim().Length is 0 or > 80 || t is not JsonObject to) { bad = true; break; }
                var clean = DocSettings.SanitizeTemplate(to, out var problem);
                if (clean == null || problem != null) { bad = true; break; }
                templates[name.Trim()] = clean;
                plan.Changes.Add(new("exportTemplates", name, (current.DocTemplates.ContainsKey(name) ? "Vorlage ersetzt: " : "Vorlage neu: ") + name, (current.DocTemplates.ContainsKey(name) ? "Template replaced: " : "New template: ") + name));
            }
            if (bad) Skip("exportTemplates", "enthält eine ungültige Vorlage – nicht übernommen.", "contains an invalid template – not applied.");
            else
            {
                cand.DocTemplates = templates;
                if (et.Str("default") is { } def && (def.Length == 0 || templates.ContainsKey(def)) && def != current.DefaultTemplate)
                {
                    cand.DefaultTemplate = def;
                    plan.Changes.Add(new("exportTemplates", "default", "Standardvorlage: " + (def.Length == 0 ? "keine" : def), "Default template: " + (def.Length == 0 ? "none" : def)));
                }
            }
        }

        // Textbausteine: werden ergänzt oder (gleicher Name) ersetzt, nie gelöscht – Texte des Autors, schwächen den Schutz nicht
        if (want.Contains("textSnippets") && p["textSnippets"] is { } tsNode)
        {
            var snippets = TextSnippet.Clean(current.TextSnippets);
            bool bad = tsNode is not JsonArray tsArr || tsArr.Count > TextSnippet.MaxCount;
            var incoming = new List<TextSnippet>();
            if (!bad)
                foreach (var x in (JsonArray)tsNode)
                {
                    if (x is not JsonObject o || o["name"] is not JsonValue nv || !nv.TryGetValue<string>(out var name) || o["text"] is not JsonValue tv || !tv.TryGetValue<string>(out var text) || !TextSnippet.Valid(name, text)) { bad = true; break; }
                    incoming.Add(new TextSnippet { Name = name, Text = text });
                }
            if (bad) Skip("textSnippets", "enthält einen ungültigen Textbaustein – nicht übernommen.", "contains an invalid text snippet – not applied.");
            else
            {
                foreach (var t in TextSnippet.Clean(incoming))
                {
                    int i = snippets.FindIndex(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase));
                    if (i >= 0 && snippets[i].Text == t.Text) continue;
                    plan.Changes.Add(new("textSnippets", t.Name, (i >= 0 ? "Textbaustein ersetzt: " : "Textbaustein neu: ") + t.Name, (i >= 0 ? "Text snippet replaced: " : "New text snippet: ") + t.Name));
                    if (i >= 0) snippets[i] = t; else snippets.Add(t);
                }
                cand.TextSnippets = TextSnippet.Clean(snippets);
            }
        }

        // Modus „Eigene“
        if (want.Contains("customProfile") && p.Obj("customProfile") is { } cp)
        {
            bool bad = false;
            var rec = new Dictionary<string, bool>(current.Record);
            foreach (var (key, v) in cp.Obj("record") ?? new JsonObject())
            {
                if (!RecordingOptions.Channels.Any(c => c.key == key) || v is not JsonValue jv || !jv.TryGetValue<bool>(out var on)) { bad = true; break; }
                if (on != current.Records(key))
                {
                    string name = RecordingOptions.ChannelName(key);
                    plan.Changes.Add(on
                        ? new("customProfile", "record:" + key, $"Wird wieder aufgezeichnet: {name}", $"Will be recorded again: {name}", "record:" + key)
                        : new("customProfile", "record:" + key, $"Nicht mehr aufgezeichnet: {name}", $"No longer recorded: {name}"));
                }
                rec[key] = on;
            }
            string? cf = cp.Str("changeFrames"), after = cp.Str("afterStop");
            if (cf != null && cf is not ("few" or "normal" or "many")) bad = true;
            if (after != null && after is not ("viewer" or "export" or "supportPackage" or "folder")) bad = true;
            if (cp["appContext"] != null && !(cp["appContext"] is JsonValue av && av.TryGetValue<bool>(out _))) bad = true;
            if (bad) Skip("customProfile", "enthält ungültige Werte – nicht übernommen.", "contains invalid values – not applied.");
            else
            {
                cand.Record = rec;
                if (cf != null && cf != current.ChangeFrames) { cand.ChangeFrames = cf; plan.Changes.Add(new("customProfile", "changeFrames", $"Zusatzbilder: {cf}", $"Extra images: {cf}")); }
                if (after != null && after != current.CustomAfterStop) { cand.CustomAfterStop = after; plan.Changes.Add(new("customProfile", "afterStop", $"Nach dem Stopp: {after}", $"After stop: {after}")); }
                if (cp["appContext"] != null && cp.Bool("appContext") != current.CustomAppContext) { cand.CustomAppContext = cp.Bool("appContext"); plan.Changes.Add(new("customProfile", "appContext", $"Erweiterter Programmkontext: {(cand.CustomAppContext ? "an" : "aus")}", $"Extended app context: {(cand.CustomAppContext ? "on" : "off")}")); }
            }
        }

        // Allgemeines
        if (want.Contains("preferences") && p.Obj("preferences") is { } pr)
        {
            var allowed = new Dictionary<string, string[]>
            {
                ["language"] = new[] { "de", "en" }, ["uiLanguage"] = new[] { "system", "de", "en" }, ["theme"] = new[] { "dark", "light", "system" },
                ["address"] = new[] { "formal", "informal" }, ["soundCues"] = new[] { "hidden", "always", "never" }, ["privacyLevel"] = new[] { "balanced", "strict" }
            };
            bool bad = false;
            foreach (var (k, vals) in allowed) if (pr[k] != null && !vals.Contains(pr.Str(k))) bad = true;
            if (pr["countdown"] != null && pr.Int("countdown", -1) is not (0 or 3 or 5)) bad = true;
            if (bad) Skip("preferences", "enthält ungültige Werte – nicht übernommen.", "contains invalid values – not applied.");
            else
            {
                void Set(string key, string? now, string? neu, Action<string> apply, string de, string en)
                {
                    if (neu == null || neu == now) return;
                    apply(neu);
                    bool weakens = key == "privacyLevel" && now == "strict" && neu == "balanced";
                    plan.Changes.Add(new("preferences", key, $"{de}: {now} → {neu}", $"{en}: {now} → {neu}", weakens ? "privacyLevel" : null));
                }
                Set("language", current.Language, pr.Str("language"), v => cand.Language = v, "Sprache der Anleitungstexte", "Guide text language");
                Set("uiLanguage", current.UiLanguage, pr.Str("uiLanguage"), v => cand.UiLanguage = v, "Sprache der Oberfläche", "Interface language");
                Set("theme", current.Theme, pr.Str("theme"), v => cand.Theme = v, "Darstellung", "Appearance");
                Set("address", current.Address, pr.Str("address"), v => cand.Address = v, "Anrede", "Form of address");
                Set("soundCues", current.SoundCues, pr.Str("soundCues"), v => cand.SoundCues = v, "Signaltöne", "Sound cues");
                Set("privacyLevel", current.PrivacyLevel, pr.Str("privacyLevel"), v => cand.PrivacyLevel = v, "Schutzstufe", "Protection level");
                if (pr["countdown"] != null && pr.Int("countdown") != current.Countdown) { cand.Countdown = pr.Int("countdown"); plan.Changes.Add(new("preferences", "countdown", $"Countdown: {current.Countdown} → {cand.Countdown} s", $"Countdown: {current.Countdown} → {cand.Countdown} s")); }
            }
        }
        plan.Candidate = cand;
        return plan;
    }

    /// <summary>Zurücksetzen auf Auslieferungszustand – über dieselben Bestätigungen wie ein Import. Aufnahmeordner und Zuletzt-Liste bleiben.</summary>
    public static ImportPlan PlanReset(AppSettings current, bool includeTemplates = false)
    {
        var d = new AppSettings();
        var parts = includeTemplates ? Parts : Parts.Where(p => p != "exportTemplates");
        var json = Export(d, parts);
        if (includeTemplates) json["parts"]!["exportTemplates"] = new JsonObject { ["templates"] = new JsonObject(), ["default"] = "" };
        var plan = Plan(json.ToJsonString(), current, parts);
        if (includeTemplates && plan.Candidate != null && current.DocTemplates.Count > 0)
        {
            plan.Candidate.DocTemplates = new(); plan.Candidate.DefaultTemplate = "";
            plan.Changes.Add(new("exportTemplates", "all", $"{current.DocTemplates.Count} Vorlagen entfernt", $"{current.DocTemplates.Count} templates removed"));
        }
        // Zurücksetzen der Tastenkürzel: Standardbelegung ist immer gültig
        return plan;
    }

    /// <summary>Wendet einen Plan an; nicht bestätigte schwächende Änderungen bleiben auf dem bisherigen Stand.</summary>
    public static AppSettings Apply(ImportPlan plan, AppSettings current, ISet<string> confirmedWeakening)
    {
        if (!plan.Ok || plan.Candidate == null) return current;
        var r = plan.Candidate.Clone();
        foreach (var w in plan.Weakening)
        {
            if (confirmedWeakening.Contains(w.WeakeningId!)) continue;
            switch (w.WeakeningId)
            {
                case "privacyLevel": r.PrivacyLevel = current.PrivacyLevel; break;
                case "excluded":
                    foreach (var c in current.Excluded) if (!r.Excluded.Contains(c, StringComparer.OrdinalIgnoreCase)) r.Excluded.Add(c);
                    break;
                case { } id when id.StartsWith("record:"):
                    r.Record[id[7..]] = current.Records(id[7..]);
                    break;
            }
        }
        // nie über den Austausch: Ordner, Zuletzt-Liste, Ausweichmodus
        r.SessionsFolder = current.SessionsFolder; r.RecentSessions = new(current.RecentSessions); r.InvisibleFallback = current.InvisibleFallback; r.LastProfile = current.LastProfile;
        return r;
    }

    public static string BackupPath(string settingsPath) => settingsPath + ".vor-import";

    /// <summary>Sichert die bisherige Datei und speichert die neuen Einstellungen.</summary>
    public static bool Commit(AppSettings next, string settingsPath)
    {
        try { if (File.Exists(settingsPath)) File.Copy(settingsPath, BackupPath(settingsPath), true); } catch { }
        bool ok = next.Write(settingsPath, setCache: settingsPath == AppPaths.SettingsFile);
        if (ok) AppLog.Write("Einstellungen importiert/zurückgesetzt");
        return ok;
    }

    public static bool CanUndo(string settingsPath) => File.Exists(BackupPath(settingsPath));

    public static bool UndoLastImport(string settingsPath)
    {
        try
        {
            if (!File.Exists(BackupPath(settingsPath))) return false;
            File.Copy(BackupPath(settingsPath), settingsPath, true);
            File.Delete(BackupPath(settingsPath));
            if (settingsPath == AppPaths.SettingsFile) AppSettings.Reload();
            return true;
        }
        catch { return false; }
    }
}
