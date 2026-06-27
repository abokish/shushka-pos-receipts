using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Options;
using ShushkaReceipt.Config;

namespace ShushkaReceipt.Services;

/// <summary>
/// Hosts the system tray icon on a dedicated STA thread.
/// Green = listener active, Red = listener down or not yet started.
/// </summary>
public sealed class TrayAndHotkeyService : BackgroundService
{
    private readonly AppState _appState;
    private readonly ILogger<TrayAndHotkeyService> _logger;

    public TrayAndHotkeyService(
        AppState appState,
        IOptions<ShushkaConfig> _,   // kept for DI signature consistency
        ILogger<TrayAndHotkeyService> logger)
    {
        _appState = appState;
        _logger   = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var staThread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var form = new TrayForm(_appState);

            using var reg = stoppingToken.Register(() =>
            {
                try
                {
                    if (form.IsHandleCreated)
                        form.BeginInvoke(Application.ExitThread);
                }
                catch { /* form may have been destroyed */ }
            });

            Application.Run(form);
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.IsBackground = true;
        staThread.Name = "ShushkaTrayThread";
        staThread.Start();

        return Task.CompletedTask;
    }

    // ── Hidden form that owns the NotifyIcon ──────────────────────────────

    private sealed class TrayForm : Form
    {
        private readonly AppState _appState;
        private readonly NotifyIcon _trayIcon;

        internal TrayForm(AppState appState)
        {
            _appState = appState;

            Text              = "ShushkaReceipt";
            FormBorderStyle   = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar     = false;
            WindowState       = FormWindowState.Minimized;
            Width  = 1;
            Height = 1;

            _trayIcon = new NotifyIcon
            {
                Icon    = MakeCircleIcon(Color.Red),
                Text    = "Shushka — ממתין...",
                Visible = true,
            };

            appState.ListenerStatusChanged += OnListenerStatusChanged;
        }

        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);

        private void OnListenerStatusChanged(bool active)
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() =>
            {
                var oldIcon = _trayIcon.Icon;
                _trayIcon.Icon = MakeCircleIcon(active ? Color.LimeGreen : Color.Red);
                _trayIcon.Text = active ? "Shushka — פעיל ✓" : "Shushka — לא פעיל ✗";
                oldIcon?.Dispose();
            });
        }

        private static Icon MakeCircleIcon(Color color)
        {
            using var bmp = new Bitmap(16, 16);
            using var g   = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 14, 14);

            // GetHicon creates a Win32 HICON that must be destroyed separately.
            // Clone() copies it into a managed Icon that owns its own handle,
            // then we destroy the raw HICON immediately.
            IntPtr hIcon = bmp.GetHicon();
            var icon = (Icon)Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            return icon;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _appState.ListenerStatusChanged -= OnListenerStatusChanged;
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
