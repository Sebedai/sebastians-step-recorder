using System.Windows;
using System.Windows.Controls;
using StepRecorder.Data;
using StepRecorder.Native;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Beim Schließen einer frisch aufgenommenen Aufnahme fragen: behalten oder verwerfen (Papierkorb) – damit sich keine
/// Probeaufnahmen ansammeln. Abschaltbar („Nicht mehr fragen“, Einstellung <see cref="AppSettings.AskKeepOnClose"/>).
/// </summary>
sealed partial class ViewerWindow
{
    /// <summary>Gerade aufgenommen (direkt nach dem Stopp geöffnet).</summary>
    public bool FreshRecording { get; set; }
    bool _keepAsked, _discard;

    void AskKeepOnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!FreshRecording || _keepAsked || _view.Session.ReadOnly || !AppSettings.Load().AskKeepOnClose) return;
        var d = new KeepDialog { Owner = this };
        d.ShowDialog();
        if (d.Choice == null) { e.Cancel = true; return; }
        _keepAsked = true;
        if (d.DontAskAgain) { var s = AppSettings.Load(); s.AskKeepOnClose = false; s.Save(); }
        _discard = d.Choice == "discard";
    }

    /// <summary>Nach dem Schließen (Ansicht freigegeben): verworfene Aufnahme in den Papierkorb.</summary>
    void DiscardAfterClose()
    {
        if (!_discard) return;
        string path = _view.Session.Path;
        var r = RecycleBin.Delete(new[] { path }, out var error);
        if (r == RecycleResult.NoRecycleBin
            && Msg.Show(Loc.L("Auf diesem Laufwerk gibt es keinen Papierkorb. Aufnahme endgültig löschen?", "There is no recycle bin on this drive. Delete the recording permanently?"), AppInfo.DisplayName, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            try { Directory.Delete(path, true); r = RecycleResult.Recycled; } catch (Exception ex) { error = ex.Message; }
        }
        if (r != RecycleResult.Recycled) { if (error != null) Msg.Show(Loc.L("Die Aufnahme konnte nicht gelöscht werden.\n\n", "The recording could not be deleted.\n\n") + error); return; }
        AppSettings.Load().RemoveRecent(path);
        AppLog.Write("Aufnahme beim Schließen verworfen");
    }
}

/// <summary>Frage „Aufnahme behalten?“ mit Behalten, Verwerfen und Abbrechen.</summary>
sealed class KeepDialog : DialogWindow
{
    readonly CheckBox _dont = new() { Content = Loc.L("Nicht mehr fragen (in den Einstellungen wieder einschaltbar)", "Don't ask again (can be turned on again in the settings)"), Margin = new Thickness(0, 14, 0, 0) };
    public string? Choice { get; private set; }
    public bool DontAskAgain => _dont.IsChecked == true;

    public KeepDialog()
    {
        Title = AppInfo.DisplayName; Width = 520; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Theme.Prepare(this, Theme.Surface);
        var p = new StackPanel { Children =
        {
            U.Para(Loc.L("Soll die Aufnahme gespeichert bleiben? Verworfene Aufnahmen kommen in den Papierkorb – exportierte Dateien (PDF, Word …) bleiben erhalten.",
                "Should the recording be kept? Discarded recordings go to the recycle bin – exported files (PDF, Word …) are kept.")),
            _dont,
        } };
        U.Dialog(this, Loc.L("Aufnahme behalten?", "Keep the recording?"), p,
            U.Primary(Loc.L("Behalten", "Keep"), () => { Choice = "keep"; DialogResult = true; }),
            U.Btn(Loc.L("Verwerfen", "Discard"), () => { Choice = "discard"; DialogResult = true; }),
            U.Btn(Loc.L("Abbrechen", "Cancel"), () => { Choice = null; DialogResult = false; }));
    }
}
