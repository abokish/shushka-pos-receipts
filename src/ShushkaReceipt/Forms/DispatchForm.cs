using System.Drawing;
using System.Windows.Forms;
using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Forms;

/// <summary>
/// Per-job dispatch popup. Behaviour depends on <see cref="DispatchMode"/>:
///
///   CustomerNoPhone — receipt arrived with no customer phone:
///     phone input + [שלח ללקוח | שמור מקומית | הדפסה | דלג]
///
///   Order — order (הזמנה) arrived; phone may be pre-filled:
///     phone input + [שלח ללקוח | שלח לחנות | שמור מקומית | הדפסה | דלג]
///
///   Internal — Z report, cashier login, etc.:
///     no phone input + [שלח לבעלים | שלח לחנות | שלח למספר... | שמור מקומית | דלג]
///
/// The form handles on-demand store/owner phone entry internally: if a routing button
/// is clicked and the target phone is not yet configured, a PhonePromptDialog is shown
/// and the number is saved to config before dispatching.
/// </summary>
public sealed class DispatchForm : Form
{
    public enum DispatchMode { CustomerNoPhone, Order, Internal }

    public enum Choice
    {
        ToCustomer,   // WhatsApp to the phone entered in the phone box
        ToStore,      // WhatsApp to StorePhone
        ToOwner,      // WhatsApp to OwnerPhone
        ToNumber,     // WhatsApp to a one-time number (Internal "שלח למספר...")
        SaveLocally,
        Print,
        Skip
    }

    public Choice  Result    { get; private set; } = Choice.Skip;
    public string  PhoneE164 { get; private set; } = "";

    private readonly string           _message;
    private readonly ShushkaConfig    _config;
    private readonly AppSettingsWriter _writer;
    private readonly TextBox?         _phoneBox;

    public DispatchForm(
        DispatchMode    mode,
        string          summary,
        string          message,
        string?         prefilledPhoneE164,
        bool            thermalConfigured,
        ShushkaConfig   config,
        AppSettingsWriter writer)
    {
        _message = message;
        _config  = config;
        _writer  = writer;

        // ── Form ──────────────────────────────────────────────────────────
        Text              = "Shushka — " + ModeTitle(mode);
        FormBorderStyle   = FormBorderStyle.FixedDialog;
        MaximizeBox       = false;
        MinimizeBox       = false;
        StartPosition     = FormStartPosition.CenterScreen;
        TopMost           = true;
        Width             = 460;
        RightToLeft       = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font              = new Font("Segoe UI", 10f);

        // ── Summary label ─────────────────────────────────────────────────
        var summaryLabel = new Label
        {
            Text      = summary,
            AutoSize  = false,
            Width     = 420,
            Height    = 44,
            Location  = new Point(12, 12),
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
        };

        int nextY = 64;

        // ── Phone row (not shown for Internal mode) ───────────────────────
        if (mode != DispatchMode.Internal)
        {
            var phoneLabel = new Label
            {
                Text     = "טלפון:",
                AutoSize = true,
                Location = new Point(12, nextY + 3),
            };

            _phoneBox = new TextBox
            {
                Width    = 230,
                Location = new Point(165, nextY),
                TabStop  = true,
            };
            _phoneBox.TextChanged += (_, _) => UpdateCustomerButton();

            var phoneHint = new Label
            {
                Text      = "לדוגמה: 052-1234567",
                AutoSize  = true,
                ForeColor = Color.Gray,
                Font      = new Font("Segoe UI", 8f),
                Location  = new Point(165, nextY + 24),
                Visible   = prefilledPhoneE164 is null,
            };

            if (prefilledPhoneE164 is not null)
                _phoneBox.Text = PhoneInputHelper.FormatForDisplay(prefilledPhoneE164);

            Controls.AddRange([phoneLabel, _phoneBox, phoneHint]);
            nextY += 50;
        }

        nextY += 8;

        // ── Buttons — row 1 ───────────────────────────────────────────────
        var row1Btns = BuildRow1Buttons(mode, thermalConfigured, nextY);
        nextY += 44;

        // ── Buttons — row 2 ───────────────────────────────────────────────
        var row2Btns = BuildRow2Buttons(mode, thermalConfigured, nextY);
        nextY += 44;

        Height = nextY + 20;

        Controls.Add(summaryLabel);
        foreach (var b in row1Btns) Controls.Add(b);
        foreach (var b in row2Btns) Controls.Add(b);

        // Keyboard shortcuts
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F8 && thermalConfigured)
            {
                Dispatch(Choice.Print);
                e.Handled = true;
            }
        };

        // Focus
        if (mode == DispatchMode.Internal)
        {
            // No phone box — nothing to focus; first button gets tab focus
        }
        else if (prefilledPhoneE164 is not null)
        {
            UpdateCustomerButton();
            // focus is set after controls are added; handled below
        }
        else
        {
            ActiveControl = _phoneBox;
        }
    }

    // ── Button factory helpers ─────────────────────────────────────────────

    private Button[] BuildRow1Buttons(DispatchMode mode, bool thermalConfigured, int y)
    {
        return mode switch
        {
            DispatchMode.CustomerNoPhone => BuildRow([
                MakeCustomerBtn(y),
                MakeSaveBtn(y),
                MakePrintBtn(y, thermalConfigured),
            ]),
            DispatchMode.Order => BuildRow([
                MakeCustomerBtn(y),
                MakeStoreBtn(y),
                MakeSaveBtn(y),
            ]),
            DispatchMode.Internal => BuildRow([
                MakeOwnerBtn(y),
                MakeStoreBtn(y),
                MakeToNumberBtn(y),
            ]),
            _ => []
        };
    }

    private Button[] BuildRow2Buttons(DispatchMode mode, bool thermalConfigured, int y)
    {
        return mode switch
        {
            DispatchMode.CustomerNoPhone => BuildRow([
                MakeSkipBtn(y),
            ]),
            DispatchMode.Order => BuildRow([
                MakePrintBtn(y, thermalConfigured),
                MakeSkipBtn(y),
            ]),
            DispatchMode.Internal => BuildRow([
                MakeSaveBtn(y),
                MakePrintBtn(y, thermalConfigured),
                MakeSkipBtn(y),
            ]),
            _ => []
        };
    }

    // Lays out buttons evenly across the form width (420 usable px, 12 left margin).
    private static Button[] BuildRow(Button[] btns)
    {
        if (btns.Length == 0) return btns;
        int total   = 420;
        int gap     = 8;
        int width   = (total - gap * (btns.Length - 1)) / btns.Length;
        int x       = 12;
        foreach (var b in btns)
        {
            b.Width    = width;
            b.Location = new Point(x, b.Location.Y);
            x         += width + gap;
        }
        return btns;
    }

    private Button MakeCustomerBtn(int y)
    {
        var b = new Button
        {
            Text      = "שלח ללקוח",
            Height    = 38,
            Location  = new Point(0, y),
            BackColor = Color.FromArgb(37, 211, 102),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            TabStop   = true,
            Enabled   = false,
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => DispatchToCustomer();
        AcceptButton = b;
        return b;
    }

    private Button MakeStoreBtn(int y)
    {
        bool configured = !string.IsNullOrEmpty(_config.StorePhone);
        var b = new Button
        {
            Text     = configured ? "שלח לחנות ✓" : "שלח לחנות...",
            Height   = 38,
            Location = new Point(0, y),
            TabStop  = true,
        };
        b.Click += (_, _) => DispatchToStore(b);
        return b;
    }

    private Button MakeOwnerBtn(int y)
    {
        bool configured = !string.IsNullOrEmpty(_config.OwnerPhone);
        var b = new Button
        {
            Text     = configured ? "שלח לבעלים ✓" : "שלח לבעלים...",
            Height   = 38,
            Location = new Point(0, y),
            TabStop  = true,
        };
        b.Click += (_, _) => DispatchToOwner(b);
        return b;
    }

    private Button MakeToNumberBtn(int y)
    {
        var b = new Button
        {
            Text    = "שלח למספר...",
            Height  = 38,
            Location = new Point(0, y),
            TabStop  = true,
        };
        b.Click += (_, _) => DispatchToCustomNumber();
        return b;
    }

    private Button MakeSaveBtn(int y)
    {
        var b = new Button
        {
            Text    = "שמור מקומית",
            Height  = 38,
            Location = new Point(0, y),
            TabStop  = true,
        };
        b.Click += (_, _) => Dispatch(Choice.SaveLocally);
        return b;
    }

    private Button MakePrintBtn(int y, bool enabled)
    {
        var b = new Button
        {
            Text    = "הדפסה  F8",
            Height  = 38,
            Location = new Point(0, y),
            Enabled = enabled,
            TabStop = enabled,
        };
        b.Click += (_, _) => Dispatch(Choice.Print);
        return b;
    }

    private Button MakeSkipBtn(int y)
    {
        var b = new Button
        {
            Text    = "דלג  Esc",
            Height  = 38,
            Location = new Point(0, y),
            TabStop  = true,
        };
        b.Click += (_, _) => Dispatch(Choice.Skip);
        CancelButton = b;
        return b;
    }

    // ── Dispatch actions ──────────────────────────────────────────────────

    private void UpdateCustomerButton()
    {
        // Find the שלח ללקוח button and en/disable it based on phone validity
        foreach (Control c in Controls)
        {
            if (c is Button b && b.Text.StartsWith("שלח ללקוח"))
            {
                b.Enabled = _phoneBox is not null &&
                            PhoneInputHelper.TryParsePhone(_phoneBox.Text, out _);
                break;
            }
        }
    }

    private void DispatchToCustomer()
    {
        if (_phoneBox is null) return;
        if (!PhoneInputHelper.TryParsePhone(_phoneBox.Text, out string e164)) return;
        PhoneE164 = e164;
        WhatsAppService.LaunchDeepLink(WhatsAppService.BuildWhatsAppLink(e164, _message));
        Result = Choice.ToCustomer;
        Close();
    }

    private void DispatchToStore(Button btn)
    {
        string phone = _config.StorePhone;
        if (string.IsNullOrEmpty(phone))
        {
            using var dlg = new PhonePromptDialog("הזן מספר טלפון החנות:");
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            phone = dlg.PhoneE164;
            _writer.SaveStorePhone(phone);
            btn.Text = "שלח לחנות ✓";
        }
        WhatsAppService.LaunchDeepLink(WhatsAppService.BuildWhatsAppLink(phone, _message));
        PhoneE164 = phone;
        Result    = Choice.ToStore;
        Close();
    }

    private void DispatchToOwner(Button btn)
    {
        string phone = _config.OwnerPhone;
        if (string.IsNullOrEmpty(phone))
        {
            using var dlg = new PhonePromptDialog("הזן מספר טלפון הבעלים:");
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            phone = dlg.PhoneE164;
            _writer.SaveOwnerPhone(phone);
            btn.Text = "שלח לבעלים ✓";
        }
        WhatsAppService.LaunchDeepLink(WhatsAppService.BuildWhatsAppLink(phone, _message));
        PhoneE164 = phone;
        Result    = Choice.ToOwner;
        Close();
    }

    private void DispatchToCustomNumber()
    {
        using var dlg = new PhonePromptDialog("הזן מספר טלפון:");
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        WhatsAppService.LaunchDeepLink(WhatsAppService.BuildWhatsAppLink(dlg.PhoneE164, _message));
        PhoneE164 = dlg.PhoneE164;
        Result    = Choice.ToNumber;
        Close();
    }

    private void Dispatch(Choice choice)
    {
        Result = choice;
        Close();
    }

    private static string ModeTitle(DispatchMode mode) => mode switch
    {
        DispatchMode.CustomerNoPhone => "שליחת קבלה",
        DispatchMode.Order           => "שליחת הזמנה",
        DispatchMode.Internal        => "דוח פנימי",
        _                            => "שליחת קבלה",
    };
}
