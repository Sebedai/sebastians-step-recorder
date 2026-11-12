using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using StepRecorder.Data;

using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Optionale Suche nach einer neuen Version (öffentliche GitHub-Releases). Nur auf Wunsch oder wenn in den Einstellungen eingeschaltet;
/// gesendet wird nur eine anonyme Abfrage ohne Kennung, Benutzer- oder Rechnerdaten. Heruntergeladen wird nichts – es öffnet sich nur die Release-Seite.
/// </summary>
static class UpdateCheck
{
    public const string ReleasesPage = "https://github.com/Sebedai/sebastians-step-recorder/releases/latest";
    const string Api = "https://api.github.com/repos/Sebedai/sebastians-step-recorder/releases/latest";

    public sealed record Result(bool Ok, string? Latest, bool Newer, string? Error);

    public static async Task<Result> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SebastiansStepRecorder-UpdateCheck");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var json = JsonNode.Parse(await http.GetStringAsync(Api)) as JsonObject;
            var tag = json?["tag_name"]?.GetValue<string>()?.TrimStart('v', 'V');
            if (tag == null) return new(false, null, false, Loc.L("Keine Versionsangabe gefunden.", "No version information found."));
            return new(true, tag, IsNewer(tag, AppInfo.Version), null);
        }
        catch (Exception ex) { return new(false, null, false, ex is HttpRequestException or TaskCanceledException ? Loc.L("Keine Verbindung zu GitHub.", "No connection to GitHub.") : ex.Message); }
    }

    public static bool IsNewer(string latest, string current)
        => Version.TryParse(Norm(latest), out var l) && Version.TryParse(Norm(current), out var c) && l > c;

    static string Norm(string v)
    {
        var core = new string(v.TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray()).Trim('.');
        return core.Count(ch => ch == '.') == 0 ? core + ".0" : core;
    }

    public static void OpenReleases() { try { Process.Start(new ProcessStartInfo(ReleasesPage) { UseShellExecute = true }); } catch { } }

    /// <summary>Manuell (Hilfe/Einstellungen): Ergebnis immer melden.</summary>
    public static async Task CheckInteractiveAsync()
    {
        var r = await CheckAsync();
        if (!r.Ok) { Msg.Show(Loc.L("Suche nach Updates fehlgeschlagen\n\n", "Update check failed\n\n") + r.Error, "Updates", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information); return; }
        if (!r.Newer) { Msg.Show(Loc.F("Sie verwenden die aktuelle Version ({0}).", "You are using the current version ({0}).", AppInfo.Version), "Updates", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information); return; }
        if (Msg.Show(Loc.F("Version {0} ist verfügbar (installiert: {1}).\n\nRelease-Seite auf GitHub öffnen?", "Version {0} is available (installed: {1}).\n\nOpen the release page on GitHub?", r.Latest, AppInfo.Version), "Updates", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.Yes)
            OpenReleases();
    }
}
