using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StepRecorder.Capture;
using StepRecorder.Data;
using StepRecorder.Export;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// „Abschnitt neu aufnehmen und ersetzen“ (08 §9.3, V-25): Bereich wählen, sichtbar neu aufnehmen, als Abschnitt anhängen;
/// die neue Fassung tritt an die Stelle der alten. „Ersetzung aufheben“ und „Erneut einsetzen“ ändern nur edits.replacements.
/// </summary>
sealed partial class ViewerWindow
{
    /// <summary>Schrittliste: mit „Alle“ zusätzlich die ersetzten Schritte (vor ihrem Ersatz) und nicht verwendete Neuaufnahmen (am Ende).</summary>
    IEnumerable<ViewStep> WithInactive(List<ViewStep> steps)
    {
        if (_allToggle.IsChecked != true || _view.Inactive.Count == 0) return steps;
        var result = new List<ViewStep>(steps.Count + _view.Inactive.Count);
        var shown = new HashSet<string>();
        foreach (var s in steps)
        {
            if (s.SeamBefore is { Kind: "replacementStart" } seam)
                foreach (var r in seam.Replaced) if (shown.Add(r.Id)) result.Add(r);
            result.Add(s);
        }
        result.AddRange(_view.Inactive.Where(x => shown.Add(x.Id)));
        return result;
    }

    /// <summary>Kennzeichen „ersetzt“, „nicht verwendet“, „neu aufgenommen“ und Nahtstelle in der Schrittliste.</summary>
    void SectionChips(ViewStep s, WrapPanel meta)
    {
        if (s.Manual) return;
        if (s.State == "replaced")
        {
            var block = s.ReplacementId != null ? _view.BlockOf(s.ReplacementId) : new List<ViewStep>();
            var c = U.Chip(Loc.L("ersetzt", "replaced"), Theme.Surface2, Theme.Muted, Theme.IRecord);
            c.ToolTip = block.Count == 0 ? Loc.L("Durch eine Neuaufnahme ersetzt", "Replaced by a re-recording")
                : Loc.F("Ersetzt durch Schritt {0} bis {1} – „Ersetzung aufheben“ stellt ihn wieder her", "Replaced by steps {0} to {1} – “Undo replacement” restores it", block[0].Number, block[^1].Number);
            meta.Children.Add(c);
            return;
        }
        if (s.State == "unused") { meta.Children.Add(U.Chip(Loc.L("nicht verwendet", "unused"), Theme.Surface2, Theme.Muted, Theme.IRecord)); return; }
        if (_view.IsNewlyRecorded(s)) meta.Children.Add(U.Chip(Loc.L("neu aufgenommen", "re-recorded"), Theme.AccentSoft, Theme.AccentText, Theme.IRecord));
        if (s.SeamBefore is { } seam)
        {
            var c = U.Chip(Loc.L("Nahtstelle", "Seam"), Theme.WarnSoft, Theme.Warn);
            c.ToolTip = SeamText.Line(_view, seam, Loc.Lang).TrimStart('>', ' ');
            meta.Children.Add(c);
        }
    }

    /// <summary>Untermenü „Neu aufnehmen“ für Menü „Schritt“ und Rechtsklick.</summary>
    MenuItem ReRecordSubMenu(bool ro)
    {
        var s = Sel;
        var sub = Sub(Loc.L("Neu aufnehmen", "Re-record"), Theme.IRecord,
            Item("rerecord", enabled: !ro && s is { Manual: false, State: "active" }),
            Item("unreplace", enabled: !ro && s?.ReplacementId != null && _view.Replacements.Any(r => r.Effective && r.Id == s.ReplacementId)),
            Item("reapply", enabled: !ro && _view.UnusedReplacementSections().Count > 0));
        return sub;
    }

    void ReRecordSelected()
    {
        InlineCommit(now: true);
        if (_view.Session.ReadOnly) { Msg.Show(Loc.L("Diese Aufnahme ist gepackt (.steprec) – bitte zuerst entpacken.", "This recording is packed (.steprec) – please extract it first.")); return; }
        if (SectionAppender.Precheck(_path) is { } why) { Msg.Show(Loc.L("Neu aufnehmen ist nicht möglich\n\n", "Re-recording is not possible\n\n") + why, Loc.L("Neu aufnehmen und ersetzen", "Re-record and replace"), MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var (range, covered, error) = _view.ReplaceRangeFor(SelSteps);
        if (range == null) { Msg.Show(error ?? "", Loc.L("Neu aufnehmen und ersetzen", "Re-record and replace"), MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var st = AppSettings.Load();
        var current = UiApp.OptionsFromSettings(ScopeMode.Screen);
        var o = SectionOptions.For(_view.Session, range.Section, range.FromSeq, current);
        if (!ConfirmReRecord(covered, o)) return;
        // Aufnahmebereich herstellen – nie stillschweigend ein anderer (AUF-104)
        if (o.Scope == ScopeMode.Region)
        {
            var picked = RegionPicker.Pick(null);
            if (picked == null) return;
            o.Region = picked; o.RegionNeedsConfirm = false;
        }
        else if (o.Scope == ScopeMode.App && !string.IsNullOrEmpty(o.AppProcess)
                 && System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(o.AppProcess)).Length == 0)
        {
            Msg.Show(Loc.F("Die Anwendung {0} läuft nicht.\n\nBitte starten Sie sie und bringen Sie sie in den Zustand vor dem ersten zu ersetzenden Schritt.", "The application {0} is not running.\n\nPlease start it and bring it into the state before the first step to replace.", o.AppProcess),
                Loc.L("Neu aufnehmen und ersetzen", "Re-record and replace"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        o.OutputFolder = SectionAppender.PendingRoot(_path);
        o.Title = Loc.For(_view.Language, "Neuaufnahme", "Re-recording");
        o.SettingsPrivacyLevel = st.PrivacyLevel;
        UiApp.Instance.StartRecording(o, folder => FinishReRecord(folder, range));
    }

    bool ConfirmReRecord(List<ViewStep> covered, RecordingOptions o)
    {
        var w = new Window { Title = Loc.L("Neu aufnehmen und ersetzen", "Re-record and replace"), Width = 620, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Theme.Prepare(w, Theme.Surface);
        var body = new StackPanel();
        var first = covered.FirstOrDefault();
        string which = covered.Count <= 1 ? Loc.F("Ersetzt wird: Schritt {0}", "To be replaced: step {0}", first?.Number ?? 0)
            : Loc.F("Ersetzt werden: Schritt {0} bis {1} ({2} Schritte)", "To be replaced: steps {0} to {1} ({2} steps)", covered[0].Number, covered[^1].Number, covered.Count);
        body.Children.Add(U.Text(which, Theme.FsBody, true, margin: new Thickness(0, 0, 0, 10)));
        body.Children.Add(U.Text(Loc.L("Bringen Sie die Anwendung in diesen Zustand, bevor Sie starten:", "Bring the application into this state before you start:"), Theme.FsBody, fg: Theme.Fg2, margin: new Thickness(0, 0, 0, 6)));
        if (first != null)
            try
            {
                using var img = StepImages.Render(_view, first, "before", "original", annotate: false);
                if (img != null) body.Children.Add(new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 10), Child = new Image { Source = U.ToSource(img.Bitmap), MaxHeight = 260, Stretch = Stretch.Uniform } });
            }
            catch (Exception ex) { UiApp.Log("Neuaufnahme-Vorschau: " + ex.Message); }
        body.Children.Add(U.Text(Loc.F("Aufgenommen wird: {0}", "Recording: {0}", SectionOptions.ScopeLabel(o)), Theme.FsBody, fg: Theme.Fg2, margin: new Thickness(0, 0, 0, 6)));
        int notes = covered.Sum(s => s.Comments.Count + s.InternalComments.Count), marks = covered.Sum(s => s.Annotations.Count), edited = covered.Count(s => s.AuthorTitle != null || s.AuthorBody != null);
        if (notes + marks + edited > 0)
            body.Children.Add(U.Text(Loc.F("An den ersetzten Schritten hängen {0} Hinweise, {1} Markierungen und {2} eigene Texte. Sie bleiben bei der bisherigen Fassung und kommen zurück, wenn die Ersetzung aufgehoben wird.",
                "The replaced steps carry {0} notes, {1} markups and {2} own texts. They stay with the previous version and return if the replacement is undone.", notes, marks, edited), Theme.FsHint, fg: Theme.Muted, margin: new Thickness(0, 0, 0, 6)));
        body.Children.Add(U.Text(Loc.L("Die bisherigen Schritte werden nicht gelöscht. „Aufnahme verwerfen“ in der Leiste lässt alles, wie es war.", "The previous steps are not deleted. “Discard recording” in the bar leaves everything as it was."), Theme.FsHint, fg: Theme.Muted));
        bool ok = false;
        var start = U.Primary(Loc.L("Aufnahme starten", "Start recording"), () => { ok = true; w.Close(); }, Theme.IRecord);
        start.IsDefault = true;
        var cancel = U.Btn(Loc.L("Abbrechen", "Cancel"), () => w.Close());
        cancel.IsCancel = true;
        U.Dialog(w, Loc.L("Neu aufnehmen und ersetzen", "Re-record and replace"), body, start, cancel);
        w.ShowDialog();
        return ok;
    }

    /// <summary>Nach dem Stopp der Neuaufnahme: als Abschnitt anhängen (samt Ersetzung), Editor neu laden, Strg+Z hebt die Ersetzung auf.</summary>
    void FinishReRecord(string? folder, PartRange range)
    {
        Show(); Activate();
        if (folder == null)
        {
            SectionAppender.RemoveEmptyPending(_path);
            _status.Text = Loc.L("Neuaufnahme verworfen – die Aufnahme ist unverändert.", "Re-recording discarded – the recording is unchanged.");
            return;
        }
        SectionAppendResult res;
        try { res = SectionAppender.Append(_path, folder, "replacement", range); }
        catch (Exception ex)
        {
            UiApp.Log("Neuaufnahme anhängen: " + ex);
            Msg.Show(Loc.F("Die Neuaufnahme konnte nicht angehängt werden:\n{0}\n\nSie liegt weiter unter:\n{1}", "The re-recording could not be appended:\n{0}\n\nIt is still stored at:\n{1}", ex.Message, folder),
                Loc.L("Neu aufnehmen und ersetzen", "Re-record and replace"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        SectionAppender.DeleteSource(folder);
        Load();
        _thumbs.Clear();
        QualityInvalidate();
        // Strg+Z: Stand ohne die eben angelegte Ersetzung
        if (res.ReplacementId != null)
        {
            var without = (JsonObject)_view.Edits.DeepClone();
            SectionEdits.RemoveReplacement(without, res.ReplacementId);
            _undo.Add(Json.Line(without)); _redo.Clear();
        }
        UpdateUndo();
        Fill();
        var block = res.ReplacementId != null ? _view.BlockOf(res.ReplacementId) : new List<ViewStep>();
        if (block.Count > 0) SelectAnchor(block[0].Anchor);
        _status.Text = res.ReplacementId == null
            ? Loc.L("Die Neuaufnahme endete nicht sauber – sie ist als nicht verwendete Neuaufnahme gespeichert („Ersetzung erneut einsetzen“).", "The re-recording did not end cleanly – it is stored as an unused re-recording (“Reapply replacement”).")
            : block.Count == 0 ? Loc.F("Ersetzt – die Neuaufnahme enthält keine Schritte. Rückgängig mit {0}.", "Replaced – the re-recording contains no steps. Undo with {0}.", Cmd("undo").Gesture)
            : Loc.F("Neu aufgenommen: Schritt {0} bis {1} ersetzen die bisherigen. Rückgängig mit {2} oder „Ersetzung aufheben“.", "Re-recorded: steps {0} to {1} replace the previous ones. Undo with {2} or “Undo replacement”.", block[0].Number, block[^1].Number, Cmd("undo").Gesture);
    }

    void UndoReplacement()
    {
        var s = Sel;
        var rep = s == null ? null : _view.ReplacementOf(s);
        if (rep is not { Effective: true }) { _status.Text = Loc.L("Dieser Schritt gehört zu keiner Ersetzung.", "This step does not belong to a replacement."); return; }
        Change(e => SectionEdits.RemoveReplacement(e, rep.Id));
        _thumbs.Clear();
        _status.Text = Loc.F("Ersetzung aufgehoben – die früheren Schritte gelten wieder. Rückgängig mit {0}.", "Replacement undone – the previous steps apply again. Undo with {0}.", Cmd("undo").Gesture);
    }

    void ReapplyReplacement()
    {
        var unused = _view.UnusedReplacementSections();
        if (unused.Count == 0) { _status.Text = Loc.L("Es gibt keine nicht verwendete Neuaufnahme.", "There is no unused re-recording."); return; }
        // gewählter Schritt aus einer Neuaufnahme zuerst, sonst die jüngste
        var part = Sel is { State: "unused" } s ? unused.FirstOrDefault(p => p.Part == s.Section) ?? unused[^1] : unused[^1];
        if (_view.Replacements.Any(r => r.Effective && r.Replaces.Overlaps(part.Replaces!)))
        { Msg.Show(Loc.L("Der Bereich dieser Neuaufnahme ist inzwischen anders ersetzt. Heben Sie diese Ersetzung zuerst auf.", "The range of this re-recording has since been replaced differently. Undo that replacement first.")); return; }
        Change(e => SectionEdits.AddReplacement(e, part.Replaces!, part.Part));
        _thumbs.Clear();
        var rep = _view.Replacements.LastOrDefault(r => r.BySection == part.Part);
        if (rep is { Effective: false })
            _status.Text = Loc.L("Die Ersetzung ist eingetragen, wirkt aber nicht (der Bereich liegt in einer nicht verwendeten Neuaufnahme).", "The replacement is recorded but has no effect (its range lies in an unused re-recording).");
        else _status.Text = Loc.F("Ersetzung wieder eingesetzt. Rückgängig mit {0}.", "Replacement reapplied. Undo with {0}.", Cmd("undo").Gesture);
    }
}
