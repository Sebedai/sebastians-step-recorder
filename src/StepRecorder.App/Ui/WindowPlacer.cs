using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StepRecorder.Native;
using StepRecorder.Settings;

namespace StepRecorder.App.Ui;

/// <summary>
/// Merkt sich Lage und Größe eines Fensters in <see cref="AppState"/> (physische Pixel samt DPI) und stellt sie beim
/// nächsten Öffnen wieder her – angepasst an die heutigen Monitore (<see cref="WindowPlacement.Fit"/>).
/// </summary>
static class WindowPlacer
{
    const uint SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;

    public static void Attach(Window w, string key)
    {
        WindowBounds? fit = null;
        try
        {
            if (AppState.Load().Windows.TryGetValue(key, out var saved))
                fit = WindowPlacement.Fit(saved, Monitors(), w.MinWidth, w.MinHeight);
        }
        catch (Exception ex) { AppLog.Write("Fensterlage: " + ex.Message); }
        if (fit != null) w.WindowStartupLocation = WindowStartupLocation.Manual;

        nint hwnd = 0;
        WindowBounds? normal = null;
        void Track()
        {
            if (hwnd == 0 || w.WindowState != WindowState.Normal || !Win32.GetWindowRect(hwnd, out var r) || r.Width <= 0) return;
            normal = new WindowBounds { X = r.Left, Y = r.Top, W = r.Width, H = r.Height, Dpi = (int)Math.Max(96, GetDpiForWindow(hwnd)) };
        }
        w.SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(w).Handle;
            if (fit != null)
            {
                SetWindowPos(hwnd, 0, fit.X, fit.Y, fit.W, fit.H, SWP_NOZORDER | SWP_NOACTIVATE);
                if (fit.Max) w.WindowState = WindowState.Maximized;
            }
            Track();
        };
        w.LocationChanged += (_, _) => Track();
        w.SizeChanged += (_, _) => Track();
        w.Closing += (_, e) =>
        {
            if (e.Cancel) return;
            Track();
            if (normal == null) return;
            normal.Max = w.WindowState == WindowState.Maximized;
            var b = normal;
            AppState.Update(s => s.Windows[key] = b);
        };
    }

    static List<WindowPlacement.Monitor> Monitors()
        => ScreenGrabber.GetMonitors().Select(m => new WindowPlacement.Monitor(m.WorkArea, m.Dpi, m.Primary)).ToList();

    [DllImport("user32.dll")] static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(nint hwnd);
}
