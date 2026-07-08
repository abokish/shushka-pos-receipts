using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Options;
using ShushkaReceipt.Config;
using ShushkaReceipt.Forms;

namespace ShushkaReceipt.Services;

/// <summary>
/// Hosts the system tray icon on a dedicated STA thread.
/// Green = listener active, Red = listener down or not yet started.
/// Right-click menu: הגדרות (Settings) and יציאה (Exit).
/// </summary>
public sealed class TrayAndHotkeyService : BackgroundService
{
    private readonly AppState          _appState;
    private readonly ShushkaConfig     _config;
    private readonly AppSettingsWriter _settingsWriter;
    private readonly ILogger<TrayAndHotkeyService> _logger;

    public TrayAndHotkeyService(
        AppState appState,
        IOptions<ShushkaConfig> config,
        AppSettingsWriter settingsWriter,
        ILogger<TrayAndHotkeyService> logger)
    {
        _appState       = appState;
        _config         = config.Value;
        _settingsWriter = settingsWriter;
        _logger         = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var staThread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var form = new TrayForm(_appState, _config, _settingsWriter);

            using var reg = stoppingToken.Register(() =>
            {
                try
                {
                    if (form.IsHandleCreated)
                        form.BeginInvoke(Application.ExitThread);
                }
                catch { }
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
        private readonly AppState          _appState;
        private readonly ShushkaConfig     _config;
        private readonly AppSettingsWriter _settingsWriter;
        private readonly NotifyIcon        _trayIcon;

        internal TrayForm(AppState appState, ShushkaConfig config, AppSettingsWriter settingsWriter)
        {
            _appState       = appState;
            _config         = config;
            _settingsWriter = settingsWriter;

            Text            = "ShushkaReceipt";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar   = false;
            WindowState     = FormWindowState.Minimized;
            Width  = 1;
            Height = 1;

            // ── Context menu ──────────────────────────────────────────────
            var menu = new ContextMenuStrip();
            menu.RightToLeft = RightToLeft.Yes;

            var itemSettings = new ToolStripMenuItem("הגדרות...");
            itemSettings.Click += (_, _) => OpenSettings();

            var itemSep = new ToolStripSeparator();

            var itemExit = new ToolStripMenuItem("יציאה");
            itemExit.Click += (_, _) => Application.ExitThread();

            menu.Items.AddRange([itemSettings, itemSep, itemExit]);

            // ── Tray icon ─────────────────────────────────────────────────
            _trayIcon = new NotifyIcon
            {
                Icon             = MakeCircleIcon(Color.Red),
                Text             = "Shushka — ממתין...",
                Visible          = true,
                ContextMenuStrip = menu,
            };

            // Double-click opens settings too
            _trayIcon.DoubleClick += (_, _) => OpenSettings();

            appState.ListenerStatusChanged += OnListenerStatusChanged;

            // Catch the case where the listener started before the handle existed.
            HandleCreated += (_, _) => OnListenerStatusChanged(_appState.ListenerActive);
        }

        private void OpenSettings()
        {
            // Run on the current STA thread — no new thread needed
            using var form = new SettingsForm(_config, _settingsWriter);
            form.ShowDialog(this);
        }

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

        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);

        private static Icon MakeCircleIcon(Color color)
        {
            using var bmp = new Bitmap(16, 16);
            using var g   = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 14, 14);
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
