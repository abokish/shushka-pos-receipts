using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Forms;

public sealed class SettingsForm : Form
{
    private readonly ShushkaConfig     _config;
    private readonly AppSettingsWriter _writer;

    private readonly CheckBox _chkAutoSend;
    private readonly ComboBox _cmbPrinter;
    private readonly TextBox  _txtStorePhone;
    private readonly TextBox  _txtOwnerPhone;
    private readonly TextBox  _txtSavePath;

    public SettingsForm(ShushkaConfig config, AppSettingsWriter writer)
    {
        _config = config;
        _writer = writer;

        Text              = "הגדרות Shushka";
        FormBorderStyle   = FormBorderStyle.FixedDialog;
        MaximizeBox       = false;
        MinimizeBox       = false;
        StartPosition     = FormStartPosition.CenterScreen;
        TopMost           = true;
        Width             = 460;
        Height            = 420;
        RightToLeft       = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font              = new Font("Segoe UI", 10f);

        // ── Section: phones ───────────────────────────────────────────────
        var grpPhones = new GroupBox
        {
            Text     = "מספרי טלפון",
            Location = new Point(10, 10),
            Width    = 420,
            Height   = 100,
        };

        var lblStore = new Label { Text = "חנות:", AutoSize = true, Location = new Point(310, 28) };
        _txtStorePhone = new TextBox
        {
            Width    = 200,
            Location = new Point(100, 25),
            Text     = PhoneInputHelper.FormatForDisplay(config.StorePhone),
        };

        var lblOwner = new Label { Text = "בעלים:", AutoSize = true, Location = new Point(310, 62) };
        _txtOwnerPhone = new TextBox
        {
            Width    = 200,
            Location = new Point(100, 59),
            Text     = PhoneInputHelper.FormatForDisplay(config.OwnerPhone),
        };

        grpPhones.Controls.AddRange([lblStore, _txtStorePhone, lblOwner, _txtOwnerPhone]);

        // ── Section: auto-send ────────────────────────────────────────────
        var grpAuto = new GroupBox
        {
            Text     = "שליחה אוטומטית",
            Location = new Point(10, 118),
            Width    = 420,
            Height   = 58,
        };

        _chkAutoSend = new CheckBox
        {
            Text     = "שלח אוטומטית ל-WhatsApp כשיש מספר טלפון",
            AutoSize = true,
            Location = new Point(10, 22),
            Checked  = config.AutoSendIfPhoneKnown,
        };

        grpAuto.Controls.Add(_chkAutoSend);

        // ── Section: thermal printer ──────────────────────────────────────
        var grpPrinter = new GroupBox
        {
            Text     = "מדפסת תרמית (גיבוי)",
            Location = new Point(10, 184),
            Width    = 420,
            Height   = 58,
        };

        var lblPrinter = new Label { Text = "בחר מדפסת:", AutoSize = true, Location = new Point(10, 25) };

        _cmbPrinter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width         = 270,
            Location      = new Point(130, 22),
        };
        PopulatePrinters(config.ThermalPrinterName);

        grpPrinter.Controls.AddRange([lblPrinter, _cmbPrinter]);

        // ── Section: local save path ──────────────────────────────────────
        var grpSave = new GroupBox
        {
            Text     = "שמירה מקומית",
            Location = new Point(10, 250),
            Width    = 420,
            Height   = 58,
        };

        var lblSave = new Label { Text = "תיקייה:", AutoSize = true, Location = new Point(10, 25) };
        _txtSavePath = new TextBox
        {
            Width    = 270,
            Location = new Point(130, 22),
            Text     = config.LocalSavePath,
        };

        grpSave.Controls.AddRange([lblSave, _txtSavePath]);

        // ── Buttons ───────────────────────────────────────────────────────
        var btnSave = new Button
        {
            Text      = "שמור",
            Width     = 90,
            Height    = 32,
            Location  = new Point(10, 318),
            BackColor = Color.FromArgb(37, 211, 102),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += OnSave;

        var btnCancel = new Button
        {
            Text     = "ביטול",
            Width    = 90,
            Height   = 32,
            Location = new Point(110, 318),
        };
        btnCancel.Click += (_, _) => Close();

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.AddRange([grpPhones, grpAuto, grpPrinter, grpSave, btnSave, btnCancel]);
    }

    private void PopulatePrinters(string currentName)
    {
        _cmbPrinter.Items.Clear();
        _cmbPrinter.Items.Add("(ללא)");

        foreach (string p in PrinterSettings.InstalledPrinters)
            _cmbPrinter.Items.Add(p);

        if (!string.IsNullOrWhiteSpace(currentName) &&
            _cmbPrinter.Items.Contains(currentName))
            _cmbPrinter.SelectedItem = currentName;
        else
            _cmbPrinter.SelectedIndex = 0;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        string printerName = _cmbPrinter.SelectedIndex == 0
            ? ""
            : (_cmbPrinter.SelectedItem as string ?? "");

        // Parse phone fields — empty string is valid (clears the phone)
        string storePhone = ParsePhoneField(_txtStorePhone.Text);
        string ownerPhone = ParsePhoneField(_txtOwnerPhone.Text);
        string savePath   = _txtSavePath.Text.Trim();
        if (string.IsNullOrWhiteSpace(savePath)) savePath = @"C:\קופה\";

        _writer.Save(
            autoSendIfPhoneKnown: _chkAutoSend.Checked,
            thermalPrinterName:   printerName,
            storePhone:           storePhone,
            ownerPhone:           ownerPhone,
            localSavePath:        savePath);

        Close();
    }

    private static string ParsePhoneField(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return PhoneInputHelper.TryParsePhone(text.Trim(), out string e164) ? e164 : "";
    }
}
