using System.Windows;
using System.Windows.Controls;
using StepRecorder.ReadLayer;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Was sich der Editor merkt (<see cref="AppState"/>): Spaltenbreiten und Reiter für alle Aufnahmen,
/// je Aufnahme den zuletzt gewählten Schritt, den Filter „Alle“ und die Bildansicht.
/// </summary>
sealed partial class ViewerWindow
{
    double _prefLeft = 320, _prefRight = 370;

    string SessionKey => _view.Session.SessionId is { Length: > 0 } id ? id : _path;

    /// <summary>Vor dem ersten Füllen der Liste: Breiten, Reiter, Filter, Ansicht.</summary>
    void RestoreLayoutState()
    {
        try
        {
            var st = AppState.Load();
            if (st.Viewer.Left is double l && l >= _leftCol.MinWidth) _prefLeft = l;
            if (st.Viewer.Right is double r && r >= _rightCol.MinWidth) _prefRight = r;
            _leftCol.Width = new GridLength(_prefLeft); _rightCol.Width = new GridLength(_prefRight);
            if (st.Viewer.Tab is > 0 and < 3) _tabBtns[st.Viewer.Tab].IsChecked = true;
            if (st.PeekSession(SessionKey) is { } ss)
            {
                if (ss.All) _allToggle.IsChecked = true;
                _dispBox.SelectedIndex = ss.Display switch { "window" => 1, "crop" => 2, _ => 0 };
            }
        }
        catch (Exception ex) { AppLog.Write("Editorzustand: " + ex.Message); }
    }

    /// <summary>Nach dem ersten Füllen: zuletzt gewählten Schritt wieder auswählen.</summary>
    void RestoreStep()
    {
        if (AppState.Load().PeekSession(SessionKey)?.Step is long a) SelectAnchor(a);
    }

    public bool SelectAnchor(long anchor)
    {
        var it = _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is ViewStep s && s.Anchor == anchor);
        if (it == null) return false;
        _list.SelectedItems.Clear();
        _list.SelectedItem = it;
        _list.ScrollIntoView(it);
        return true;
    }

    void SaveUiState()
    {
        try
        {
            AppState.Update(st =>
            {
                st.Viewer.Left = Math.Round(_prefLeft); st.Viewer.Right = Math.Round(_prefRight); st.Viewer.Tab = _detailTab;
                var ss = st.Session(SessionKey);
                ss.Step = Sel?.Anchor; ss.All = _allToggle.IsChecked == true; ss.Display = _display;
            });
        }
        catch (Exception ex) { AppLog.Write("Editorzustand: " + ex.Message); }
    }
}
