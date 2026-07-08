using System.Drawing;
using System.Runtime.InteropServices;
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
///     scrollable text view of the print content +
///     [שלח לבעלים | שלח לחנות | שלח למספר... | שמור מקומית | סגור]
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
        ToCustomer,
        ToStore,
        ToOwner,
        ToNumber,
        SaveLocally,
        Print,
        Skip
    }

    public Choice Result    { get; private set; } = Choice.Skip;
    public string PhoneE164 { get; private set; } = "";

    private readonly string            _message;
    private readonly ShushkaConfig     _config;
    private readonly AppSettingsWriter _writer;
    private readonly TextBox?          _phoneBox;

    public DispatchForm(
        DispatchMode      mode,
        string            summary,
        string            message,
        string?           prefilledPhoneE164,
        bool              thermalConfigured,
        ShushkaConfig     config,
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
        Width             = 480;
        RightToLeft       = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font              = new Font("Segoe UI", 10f);

        // ── Summary label ─────────────────────────────────────────────────
        var summaryLabel = new Label
        {
            Text      = summary,
            AutoSize  = false,
            Width     = 440,
            Height    = 40,
            Location  = new Point(12, 12),
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
        };

        int nextY = 58;

        if (mode == DispatchMode.Internal)
        {
            // ── Scrollable content view ───────────────────────────────────
            var contentBox = new TextBox
            {
                Multiline    = true,
                ReadOnly     = true,
                ScrollBars   = ScrollBars.Vertical,
                Width        = 440,
                Height       = 220,
                Location     = new Point(12, nextY),
                Font         = new Font("Courier New", 9f),
                BackColor    = System.Drawing.SystemColors.Window,
                Text         = message,
                RightToLeft  = RightToLeft.Yes,
            };
            Controls.Add(contentBox);
            nextY += 228;
        }
        else
        {
            // ── Phone row ─────────────────────────────────────────────────
            var phoneLabel = new Label
            {
                Text     = "טלפון:",
                AutoSize = true,
                Location = new Point(12, nextY + 3),
            };

            _phoneBox = new TextBox
            {
                Width    = 240,
                Location = new Point(180, nextY),
                TabStop  = true,
            };
            _phoneBox.TextChanged += (_, _) => UpdateCustomerButton();

            var phoneHint = new Label
            {
                Text      = "לדוגמה: 052-1234567",
                AutoSize  = true,
                ForeColor = Color.Gray,
                Font      = new Font("Segoe UI", 8f),
                Location  = new Point(180, nextY + 24),
                Visible   = prefilledPhoneE164 is null,
            };

            if (prefilledPhoneE164 is not null)
                _phoneBox.Text = PhoneInputHelper.FormatForDisplay(prefilledPhoneE164);

            Controls.AddRange([phoneLabel, _phoneBox, phoneHint]);
            nextY += 52;
        }

        nextY += 6;

        // ── Buttons ───────────────────────────────────────────────────────
        var row1 = BuildRow(MakeRow1(mode, thermalConfigured, nextY));
        nextY += 44;
        var row2 = BuildRow(MakeRow2(mode, thermalConfigured, nextY));
        nextY += 44;

        ClientSize = new Size(ClientSize.Width, nextY + 16);

        Controls.Add(summaryLabel);
        foreach (var b in row1) Controls.Add(b);
        foreach (var b in row2) Controls.Add(b);

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F8 && thermalConfigured)
            {
                Dispatch(Choice.Print);
                e.Handled = true;
            }
        };

        if (mode != DispatchMode.Internal && prefilledPhoneE164 is null)
            ActiveControl = _phoneBox;

        // Phone may have been pre-filled before buttons were added to Controls,
        // so UpdateCustomerButton() missed it. Sync the button state now.
        UpdateCustomerButton();
    }

    // ── Button rows ───────────────────────────────────────────────────────

    private Button[] MakeRow1(DispatchMode mode, bool thermal, int y) => mode switch
    {
        DispatchMode.CustomerNoPhone => [MakeCustomerBtn(y), MakeSaveBtn(y), MakePrintBtn(y, thermal)],
        DispatchMode.Order           => [MakeCustomerBtn(y), MakeStoreBtn(y), MakeSaveBtn(y)],
        DispatchMode.Internal        => [MakeOwnerBtn(y), MakeStoreBtn(y), MakeToNumberBtn(y)],
        _                            => [],
    };

    private Button[] MakeRow2(DispatchMode mode, bool thermal, int y) => mode switch
    {
        DispatchMode.CustomerNoPhone => [MakeSkipBtn(y, "דלג  Esc")],
        DispatchMode.Order           => [MakePrintBtn(y, thermal), MakeSkipBtn(y, "דלג  Esc")],
        DispatchMode.Internal        => [MakeSaveBtn(y), MakePrintBtn(y, thermal), MakeSkipBtn(y, "סגור  Esc")],
        _                            => [],
    };

    // Distributes buttons evenly across the 440 usable px.
    private static Button[] BuildRow(Button[] btns)
    {
        if (btns.Length == 0) return btns;
        int gap   = 8;
        int width = (440 - gap * (btns.Length - 1)) / btns.Length;
        int x     = 12;
        foreach (var b in btns)
        {
            b.Width    = width;
            b.Location = new Point(x, b.Location.Y);
            x         += width + gap;
        }
        return btns;
    }

    // ── Button factories ──────────────────────────────────────────────────

    private Button MakeCustomerBtn(int y)
    {
        var b = MakeBtn("שלח ללקוח", y, Color.FromArgb(37, 211, 102), Color.White);
        b.Enabled = false;
        b.Click  += (_, _) => DispatchToCustomer();
        AcceptButton = b;
        return b;
    }

    private Button MakeStoreBtn(int y)
    {
        bool cfg = !string.IsNullOrEmpty(_config.StorePhone);
        var b    = MakeBtn(cfg ? "שלח לחנות ✓" : "שלח לחנות...", y);
        b.Click += (_, _) => DispatchToStore(b);
        return b;
    }

    private Button MakeOwnerBtn(int y)
    {
        bool cfg = !string.IsNullOrEmpty(_config.OwnerPhone);
        var b    = MakeBtn(cfg ? "שלח לבעלים ✓" : "שלח לבעלים...", y);
        b.Click += (_, _) => DispatchToOwner(b);
        return b;
    }

    private Button MakeToNumberBtn(int y)
    {
        var b = MakeBtn("שלח למספר...", y);
        b.Click += (_, _) => DispatchToCustomNumber();
        return b;
    }

    private Button MakeSaveBtn(int y)
    {
        var b = MakeBtn("שמור מקומית", y);
        b.Click += (_, _) => Dispatch(Choice.SaveLocally);
        return b;
    }

    private Button MakePrintBtn(int y, bool enabled)
    {
        var b = MakeBtn("הדפסה  F8", y);
        b.Enabled = enabled;
        b.TabStop = enabled;
        b.Click  += (_, _) => Dispatch(Choice.Print);
        return b;
    }

    private Button MakeSkipBtn(int y, string label)
    {
        var b = MakeBtn(label, y);
        b.Click += (_, _) => Dispatch(Choice.Skip);
        CancelButton = b;
        return b;
    }

    private static Button MakeBtn(string text, int y,
        Color? backColor = null, Color? foreColor = null)
    {
        var b = new Button
        {
            Text      = text,
            Height    = 38,
            Location  = new Point(0, y),
            TabStop   = true,
            FlatStyle = backColor.HasValue ? FlatStyle.Flat : FlatStyle.Standard,
        };
        if (backColor.HasValue)
        {
            b.BackColor = backColor.Value;
            b.ForeColor = foreColor ?? Color.White;
            b.FlatAppearance.BorderSize = 0;
        }
        return b;
    }

    // ── Dispatch actions ──────────────────────────────────────────────────

    private void UpdateCustomerButton()
    {
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

    // ── Force foreground ──────────────────────────────────────────────────────
    // Windows blocks focus-stealing from background threads. Attach to the
    // foreground window's input queue briefly so SetForegroundWindow works.

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Simulate ALT press/release to acquire the foreground lock, then steal focus.
        // This is the standard workaround for Windows focus-stealing protection.
        keybd_event(0x12, 0, 0, UIntPtr.Zero);       // ALT down
        SetForegroundWindow(Handle);
        keybd_event(0x12, 0, 0x0002, UIntPtr.Zero);  // ALT up
        Activate();
    }

    private static string ModeTitle(DispatchMode mode) => mode switch
    {
        DispatchMode.CustomerNoPhone => "שליחת קבלה",
        DispatchMode.Order           => "שליחת הזמנה",
        DispatchMode.Internal        => "הדפסה פנימית",
        _                            => "שליחת קבלה",
    };
}
