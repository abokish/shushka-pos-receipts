using System.Drawing;
using System.Windows.Forms;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Forms;

/// <summary>
/// Per-job dispatch popup. Appears above all windows for every incoming print job.
/// Cashier chooses: send via WhatsApp, print to thermal, or skip.
///
/// Keyboard:
///   Enter — WhatsApp (AcceptButton; works from anywhere on the form, incl. phone field)
///   Esc   — Skip     (CancelButton)
///   F8    — Print    (only special key; greyed out if no thermal printer configured)
///   F6    — WhatsApp (alias, kept for muscle memory)
/// </summary>
public sealed class DispatchForm : Form
{
    // ── Result the caller reads after the form closes ─────────────────────
    public enum Choice { WhatsApp, Print, Skip }
    public Choice Result { get; private set; } = Choice.Skip;
    public string PhoneE164 { get; private set; } = "";

    // ── Controls ──────────────────────────────────────────────────────────
    private readonly TextBox _phoneBox;
    private readonly Button  _btnWhatsApp;
    private readonly Button  _btnPrint;
    private readonly Button  _btnSkip;

    private readonly string _whatsAppMessage;

    public DispatchForm(
        string receiptSummary,
        string whatsAppMessage,
        string? prefilledPhoneE164,
        bool thermalConfigured)
    {
        _whatsAppMessage = whatsAppMessage;

        // ── Form setup ────────────────────────────────────────────────────
        Text              = "Shushka — שליחת קבלה";
        FormBorderStyle   = FormBorderStyle.FixedDialog;
        MaximizeBox       = false;
        MinimizeBox       = false;
        StartPosition     = FormStartPosition.CenterScreen;
        TopMost           = true;
        Width             = 420;
        Height            = 210;
        RightToLeft       = RightToLeft.Yes;
        RightToLeftLayout = true;

        // ── Summary label ─────────────────────────────────────────────────
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
            Width   = 240,
            Location = new Point(130, 67),
            Font    = new Font("Segoe UI", 10f),
            TabStop = true,
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
            Text    = "הדפסה  F8",
            Width   = 110,
            Height  = 38,
            Location = new Point(152, 115),
            Font    = new Font("Segoe UI", 10f),
            Enabled = thermalConfigured,
            TabStop = thermalConfigured,
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

        // ── Wire AcceptButton / CancelButton ──────────────────────────────
        // Enter fires WhatsApp from anywhere (phone field, button focus, etc.)
        // Esc fires Skip from anywhere — no KeyDown handler needed for either.
        AcceptButton = _btnWhatsApp;
        CancelButton = _btnSkip;

        // ── F8 and F6 alias via KeyDown ───────────────────────────────────
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

        // ── Assemble ──────────────────────────────────────────────────────
        Controls.AddRange([summaryLabel, phoneLabel, _phoneBox, phoneHint,
                           _btnWhatsApp, _btnPrint, _btnSkip]);

        // ── Initial state ─────────────────────────────────────────────────
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
    }

    // ── Phone validation ──────────────────────────────────────────────────

    private void UpdateButtonState()
    {
        _btnWhatsApp.Enabled = PhoneInputHelper.TryParsePhone(_phoneBox.Text, out _);
    }

    // ── Dispatch ──────────────────────────────────────────────────────────

    private void Dispatch(Choice choice)
    {
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
}
