using System.Drawing;
using System.Text.Json.Nodes;
using StepRecorder.Data;
using StepRecorder.Settings;

namespace StepRecorder.ReadLayer;

/// <summary>
/// Sofort-Bildschirmfoto (12, Paket 15): eine Aufnahme mit genau einem eigenen Bildschritt. So gelten Zeichenwerkzeuge,
/// Schwärzen, Kopieren, Export und Bibliothek ohne Sonderweg; das Journal enthält nur Beginn und Ende (keine Eingaben).
/// </summary>
public static class QuickShot
{
    public static string Create(string sessionsFolder, Bitmap image, string lang)
    {
        Directory.CreateDirectory(sessionsFolder);
        var now = DateTime.UtcNow;
        string title = Loc.For(lang, "Bildschirmfoto", "Screenshot") + " " + DateTime.Now.ToString(lang == "de" ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm");
        string baseName = DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + "_" + Loc.For(lang, "Bildschirmfoto", "Screenshot");
        string folder = Path.Combine(sessionsFolder, baseName);
        for (int i = 2; Directory.Exists(folder); i++) folder = Path.Combine(sessionsFolder, baseName + "_" + i);
        string id = Json.NewUuid7();
        var manifest = new JsonObject
        {
            ["format"] = SessionWriter.FormatName, ["schemaVersion"] = SessionWriter.SchemaVersion, ["sessionId"] = id, ["title"] = title, ["state"] = "recording",
            ["createdUtc"] = Json.Utc(now), ["timeZone"] = new JsonObject { ["id"] = TimeZoneInfo.Local.Id, ["offsetMin"] = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes },
            ["parts"] = new JsonArray(new JsonObject { ["part"] = 0, ["startUtc"] = Json.Utc(now), ["tStart"] = 0, ["purpose"] = "initial" }),
            ["recorder"] = new JsonObject { ["name"] = AppInfo.Name, ["version"] = AppInfo.Version, ["arch"] = "x64", ["elevated"] = false, ["captureHost"] = "none" },
            ["environment"] = new JsonObject(), ["topology"] = new JsonObject(), ["settings"] = new JsonObject { ["language"] = lang },
            ["profile"] = new JsonObject { ["name"] = Loc.For(lang, "Bildschirmfoto", "Screenshot"), ["id"] = "screenshot", ["builtIn"] = true },
            ["history"] = new JsonArray(new JsonObject { ["atUtc"] = Json.Utc(now), ["action"] = "created", ["tool"] = "StepRecorder " + AppInfo.Version })
        };
        using (var w = SessionWriter.Create(folder, manifest))
        {
            w.Append(new JsonObject { ["t"] = 0, ["type"] = "session.start", ["part"] = 0, ["schemaVersion"] = SessionWriter.SchemaVersion, ["sessionId"] = id, ["utc"] = Json.Utc(now), ["purpose"] = "screenshot" });
            w.Append(new JsonObject { ["t"] = 1, ["type"] = "session.stop", ["reason"] = "user", ["utc"] = Json.Utc(now) });
            w.Close(1, m => { m["state"] = "complete"; m["durationUs"] = 1; m["endedUtc"] = Json.Utc(now); });
        }
        using var v = SessionView.Open(folder, lang: lang);
        string img = v.AddAsset(image, "bildschirmfoto");
        v.Edits["inserted"] = new JsonArray(new JsonObject { ["anchor"] = -1001, ["after"] = 0, ["title"] = Loc.For(lang, "Bildschirmfoto", "Screenshot"), ["body"] = "", ["image"] = img, ["createdUtc"] = Json.UtcNow() });
        v.SaveEdits("quickShot");
        return folder;
    }
}
