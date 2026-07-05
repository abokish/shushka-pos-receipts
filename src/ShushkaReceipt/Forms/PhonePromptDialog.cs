using System.Drawing;
using System.Windows.Forms;

namespace ShushkaReceipt.Forms;

/// <summary>
/// Minimal dialog that asks for a single phone number.
/// Used for on-demand store/owner phone entry from the dispatch popup.
/// </summary>
public sealed class PhonePromptDialog : Form
{
    public string PhoneE164 { get; private set; } = "";

    private readonly TextBox _phoneBox;

    public PhonePromptDialog(string prompt, string? prefilledE164 = null)
    {
        Text              = "Shushka";
        FormBorderStyle   = FormBorderStyle.FixedDialog;
        MaximizeBox       = false;
        MinimizeBox       = false;
        StartPosition     = FormStartPosition.CenterScreen;
        TopMost           = true;
        Width             = 360;
        Height            = 160;
        RightToLeft       = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font              = new Font("Segoe UI", 10f);

        var lblPrompt = new Label
        {
            Text      = prompt,
            AutoSize  = false,
            Width     = 320,
            Height    = 32,
            Location  = new Point(12, 12),
            TextAlign = ContentAlignment.MiddleRight,
        };

        _phoneBox = new TextBox
        {
            Width    = 200,
            Location = new Point(12, 52),
            Font     = new Font("Segoe UI", 10f),
        };

        if (prefilledE164 is not null)
            _phoneBox.Text = PhoneInputHelper.FormatForDisplay(prefilledE164);

        var btnOk = new Button
        {
            Text      = "אישור",
            Width     = 80,
            Height    = 30,
            Location  = new Point(12, 88),
            BackColor = Color.FromArgb(37, 211, 102),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += OnOk;

        var btnCancel = new Button
        {
            Text     = "ביטול",
            Width    = 80,
            Height   = 30,
            Location = new Point(102, 88),
        };
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange([lblPrompt, _phoneBox, btnOk, btnCancel]);
        ActiveControl = _phoneBox;
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (!PhoneInputHelper.TryParsePhone(_phoneBox.Text, out string e164))
        {
            MessageBox.Show("מספר טלפון לא תקין. לדוגמה: 052-1234567",
                "שגיאה", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        PhoneE164    = e164;
        DialogResult = DialogResult.OK;
        Close();
    }
}
