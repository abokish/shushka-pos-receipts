using System.Drawing;
using System.Windows.Forms;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Forms;

/// <summary>
/// Per-job dispatch popup. Appears above all windows for every incoming print job.
///
/// Keyboard:
///   Enter — WhatsApp (AcceptButton; works from anywhere, including the phone field)
///   Esc   — Skip     (CancelButton)
///   F8    — Print    (greyed out when no thermal printer configured)
///   F6    — WhatsApp alias
///
/// Optional countdown: when countdownSeconds > 0 the form auto-dispatches to WhatsApp
/// after the timer reaches zero unless the cashier interacts first.
/// </summary>
public sealed class DispatchForm : Form
{
    public enum Choice { WhatsApp, Print, Skip }
    public Choice Result   { get; private set; } = Choice.Skip;
    public string PhoneE164 { get; private set; } = "";

    private readonly TextBox _phoneBox;
    private readonly Button  _btnWhatsApp;
    private readonly Button  _btnPrint;
    private readonly Button  _btnSkip;
    private readonly string  _whatsAppMessage;

    // Countdown
    private System.Windows.Forms.Timer? _timer;
    private int     _secondsLeft;
    private Label?  _countdownLabel;

    public DispatchForm(
        string  receiptSummary,
        string  whatsAppMessage,
        string? prefilledPhoneE164,
        bool    thermalConfigured,
        int?    countdownSeconds = null)
    {
        _whatsAppMessage = whatsAppMessage;

        bool hasCountdown = countdownSeconds is > 0;
        _secondsLeft = countdownSeconds ?? 0;

        // ── Form ──────────────────────────────────────────────────────────
        Text              = "Shushka — שליחת קבלה";
        FormBorderStyle   = FormBorderStyle.FixedDialog;
        MaximizeBox       = false;
        MinimizeBox       = false;
        StartPosition     = FormStartPosition.CenterScreen;
        TopMost           = true;
        Width             = 420;
        Height            = hasCountdown ? 240 : 210;
        RightToLeft       = RightToLeft.Yes;
        RightToLeftLayout = true;

        // ── Summary ───────────────────────────────────────────────────────
        var summaryLabel = new Label
        {
            Text      = receiptSummary,
            AutoSize  = false,
            Width     = 380,
            Height    = 44,
            Location  = new Point(12, 12),
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
        };

        // ── Phone row ─────────────────────────────────────────────────────
        var phoneLabel = new Label
        {
            Text     = "טלפון:",
            AutoSize = true,
            Location = new Point(12, 70),
            Font     = new Font("Segoe UI", 10f),
        };

        _phoneBox = new TextBox
        {
            Width    = 240,
            Location = new Point(130, 67),
            Font     = new Font("Segoe UI", 10f),
            TabStop  = true,
        };
        _phoneBox.TextChanged += (_, _) => UpdateButtonState();

        var phoneHint = new Label
        {
            Text      = "לדוגמה: 052-1234567",
            AutoSize  = true,
            ForeColor = Color.Gray,
            Font      = new Font("Segoe UI", 8f),
            Location  = new Point(130, 92),
            Visible   = prefilledPhoneE164 is null,
        };

        // ── Buttons ───────────────────────────────────────────────────────
        _btnWhatsApp = new Button
        {
            Text      = "WhatsApp  ↵",
            Width     = 130,
            Height    = 38,
            Location  = new Point(12, 115),
            Font      = new Font("Segoe UI", 10f),
            BackColor = Color.FromArgb(37, 211, 102),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            TabStop   = true,
        };
        _btnWhatsApp.FlatAppearance.BorderSize = 0;
        _btnWhatsApp.Click += (_, _) => Dispatch(Choice.WhatsApp);

        _btnPrint = new Button
        {
            Text     = "הדפסה  F8",
            Width    = 110,
            Height   = 38,
            Location = new Point(152, 115),
            Font     = new Font("Segoe UI", 10f),
            Enabled  = thermalConfigured,
            TabStop  = thermalConfigured,
        };
        _btnPrint.Click += (_, _) => Dispatch(Choice.Print);

        _btnSkip = new Button
        {
            Text     = "דלג  Esc",
            Width    = 100,
            Height   = 38,
            Location = new Point(272, 115),
            Font     = new Font("Segoe UI", 10f),
            TabStop  = true,
        };
        _btnSkip.Click += (_, _) => Dispatch(Choice.Skip);

        AcceptButton = _btnWhatsApp;
        CancelButton = _btnSkip;

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F8 && _btnPrint.Enabled)
            {
                Dispatch(Choice.Print);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F6 && _btnWhatsApp.Enabled)
            {
                Dispatch(Choice.WhatsApp);
                e.Handled = true;
            }
        };

        // ── Countdown label ───────────────────────────────────────────────
        if (hasCountdown)
        {
            _countdownLabel = new Label
            {
                Text      = CountdownText(),
                AutoSize  = false,
                Width     = 380,
                Height    = 22,
                Location  = new Point(12, 163),
                ForeColor = Color.DarkOrange,
                Font      = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleRight,
            };
        }

        // ── Assemble ──────────────────────────────────────────────────────
        Controls.AddRange([summaryLabel, phoneLabel, _phoneBox, phoneHint,
                           _btnWhatsApp, _btnPrint, _btnSkip]);
        if (_countdownLabel is not null) Controls.Add(_countdownLabel);

        // ── Initial focus ─────────────────────────────────────────────────
        if (prefilledPhoneE164 is not null)
        {
            _phoneBox.Text = PhoneInputHelper.FormatForDisplay(prefilledPhoneE164);
            ActiveControl  = _btnWhatsApp;
        }
        else
        {
            ActiveControl = _phoneBox;
        }

        UpdateButtonState();

        // ── Start countdown after handle is created ───────────────────────
        if (hasCountdown)
        {
            HandleCreated += (_, _) =>
            {
                _timer = new System.Windows.Forms.Timer { Interval = 1000 };
                _timer.Tick += OnTick;
                _timer.Start();
            };
        }
    }

    // ── Countdown ─────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        _secondsLeft--;
        if (_countdownLabel is not null)
            _countdownLabel.Text = CountdownText();

        if (_secondsLeft <= 0)
            Dispatch(Choice.WhatsApp);
    }

    private string CountdownText() =>
        $"שולח אוטומטית ל-WhatsApp בעוד {_secondsLeft} שניות... (Esc לביטול)";

    // ── State ──────────────────────────────────────────────────────────────

    private void UpdateButtonState()
    {
        _btnWhatsApp.Enabled = PhoneInputHelper.TryParsePhone(_phoneBox.Text, out _);
    }

    // ── Dispatch ──────────────────────────────────────────────────────────

    private void Dispatch(Choice choice)
    {
        _timer?.Stop();

        if (choice == Choice.WhatsApp)
        {
            if (!PhoneInputHelper.TryParsePhone(_phoneBox.Text, out string e164)) return;
            PhoneE164 = e164;
            WhatsAppService.LaunchDeepLink(
                WhatsAppService.BuildWhatsAppLink(e164, _whatsAppMessage));
        }

        Result = choice;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer?.Dispose();
        base.Dispose(disposing);
    }
}
