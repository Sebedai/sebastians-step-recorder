using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace StepRecorder.App.Ui;

/// <summary>
/// Symbol im Infobereich der Taskleiste (unten rechts) über Shell_NotifyIcon – ohne WinForms, damit das Menü im
/// eigenen Design erscheint und die Position des Symbols bekannt ist (Klicks darauf werden nicht aufgezeichnet).
/// </summary>
sealed class TrayIcon : IDisposable
{
    const int CallbackMsg = 0x8000 + 0x4D; // WM_APP + 77
    const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    const int NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_SHOWTIP = 0x80;
    const int NIN_SELECT = 0x400, NIN_KEYSELECT = 0x401, WM_MOUSEMOVE = 0x200, WM_LBUTTONUP = 0x202, WM_RBUTTONUP = 0x205, WM_CONTEXTMENU = 0x7B;

    readonly HwndSource _src;
    readonly int _taskbarCreated;
    System.Drawing.Icon? _icon;
    string _tip;
    bool _added, _disposed;
    readonly object _rectLock = new();
    System.Drawing.Rectangle _rect;

    public event Action? LeftClick;
    public event Action? RightClick;

    public nint Handle => _src.Handle;
    public bool Added => _added;

    public TrayIcon(System.Drawing.Icon icon, string tip)
    {
        _icon = icon; _tip = tip;
        _src = new HwndSource(new HwndSourceParameters("StepRecorderTray") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x80 });
        _src.AddHook(Hook);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        Add();
    }

    void Add()
    {
        var d = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        _added = Shell_NotifyIcon(NIM_ADD, ref d);
        d.uVersion = 4;
        Shell_NotifyIcon(NIM_SETVERSION, ref d);
        UpdateRect();
    }

    NOTIFYICONDATA Data(int flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _src.Handle, uID = 1, uFlags = flags, uCallbackMessage = CallbackMsg,
        hIcon = _icon?.Handle ?? 0, szTip = _tip.Length > 127 ? _tip[..127] : _tip, szInfo = "", szInfoTitle = ""
    };

    public void SetTip(string tip)
    {
        tip = Loc.T(tip);
        if (_disposed || tip == _tip) return;
        _tip = tip;
        var d = Data(NIF_TIP | NIF_SHOWTIP);
        Shell_NotifyIcon(NIM_MODIFY, ref d);
    }

    public void SetIcon(System.Drawing.Icon icon)
    {
        if (_disposed) return;
        var old = _icon; _icon = icon;
        var d = Data(NIF_ICON);
        Shell_NotifyIcon(NIM_MODIFY, ref d);
        old?.Dispose();
    }

    /// <summary>Liegt der Bildschirmpunkt (physische Pixel) auf dem Symbol? Wird vom Eingabe-Hook abgefragt.</summary>
    public bool Contains(int x, int y)
    {
        lock (_rectLock) return _rect.Width > 0 && _rect.Contains(x, y);
    }

    public void UpdateRect()
    {
        if (_disposed) return;
        var id = new NOTIFYICONIDENTIFIER { cbSize = Marshal.SizeOf<NOTIFYICONIDENTIFIER>(), hWnd = _src.Handle, uID = 1 };
        if (Shell_NotifyIconGetRect(ref id, out var r) == 0)
            lock (_rectLock) _rect = System.Drawing.Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == CallbackMsg)
        {
            int ev = (int)(lParam.ToInt64() & 0xFFFF);
            switch (ev)
            {
                case WM_MOUSEMOVE: UpdateRect(); break;
                case NIN_SELECT or NIN_KEYSELECT or WM_LBUTTONUP: UpdateRect(); LeftClick?.Invoke(); break;
                case WM_CONTEXTMENU or WM_RBUTTONUP: UpdateRect(); SetForegroundWindow(_src.Handle); RightClick?.Invoke(); break;
            }
            handled = true;
        }
        else if (msg == _taskbarCreated && _taskbarCreated != 0) Add(); // Explorer neu gestartet
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_added) { var d = Data(0); Shell_NotifyIcon(NIM_DELETE, ref d); }
        _src.RemoveHook(Hook);
        _src.Dispose();
        _icon?.Dispose();
    }

    /// <summary>Symbol zeichnen: Aufnahme (roter Punkt), Pause (zwei Balken), Privat (gelber Punkt) auf dunklem Grund mit Indigo-Rand (Farben aus <see cref="Theme.Night"/>).</summary>
    public static System.Drawing.Icon Render(string state)
    {
        int s = Math.Max(16, GetSystemMetrics(49 /*SM_CXSMICON*/));
        using var bmp = new System.Drawing.Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);
            float r = s * 0.28f;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(0, 0, 2 * r, 2 * r, 180, 90); path.AddArc(s - 2 * r - 1, 0, 2 * r, 2 * r, 270, 90);
                path.AddArc(s - 2 * r - 1, s - 2 * r - 1, 2 * r, 2 * r, 0, 90); path.AddArc(0, s - 2 * r - 1, 2 * r, 2 * r, 90, 90); path.CloseFigure();
                using var bg = new System.Drawing.SolidBrush(D(Theme.Night.BgColor));
                g.FillPath(bg, path);
                using var border = new System.Drawing.Pen(D(Theme.Night.AccentColor), Math.Max(1, s / 16f));
                g.DrawPath(border, path);
            }
            if (state == "paused")
            {
                using var w = new System.Drawing.SolidBrush(D(Theme.Night.FgColor));
                float bw = s * 0.15f, bh = s * 0.46f, y = (s - bh) / 2;
                g.FillRectangle(w, s * 0.31f, y, bw, bh); g.FillRectangle(w, s * 0.54f, y, bw, bh);
            }
            else
            {
                var c = D(state == "private" ? Theme.Night.WarnColor : Theme.Night.RecColor);
                using var dot = new System.Drawing.SolidBrush(c);
                float d = s * 0.5f;
                g.FillEllipse(dot, (s - d) / 2, (s - d) / 2, d, d);
            }
        }
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    static System.Drawing.Color D(System.Windows.Media.Color c) => System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public int cbSize; public nint hWnd; public int uID; public int uFlags; public int uCallbackMessage; public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState; public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags; public Guid guidItem; public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NOTIFYICONIDENTIFIER { public int cbSize; public nint hWnd; public int uID; public Guid guidItem; }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(int msg, ref NOTIFYICONDATA data);
    [DllImport("shell32.dll")] static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER id, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint h);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
}
