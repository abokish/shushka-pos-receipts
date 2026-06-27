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

    public SettingsForm(ShushkaConfig config, AppSettingsWriter writer)
    {
        _config = config;
        _writer = writer;

        // ── Form ──────────────────────────────────────────────────────────
        Text              = "הגדרות Shushka";
        FormBorderStyle   = FormBorderStyle.FixedDialog;
        MaximizeBox       = false;
        MinimizeBox       = false;
        StartPosition     = FormStartPosition.CenterScreen;
        TopMost           = true;
        Width             = 440;
        Height            = 210;
        RightToLeft       = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font              = new Font("Segoe UI", 10f);

        // ── Section: auto-send ────────────────────────────────────────────
        var grpAuto = new GroupBox
        {
            Text     = "שליחה אוטומטית",
            Location = new Point(10, 10),
            Width    = 400,
            Height   = 65,
        };

        _chkAutoSend = new CheckBox
        {
            Text     = "שלח אוטומטית ל-WhatsApp כשיש מספר טלפון",
            AutoSize = true,
            Location = new Point(10, 25),
            Checked  = config.AutoSendIfPhoneKnown,
        };

        grpAuto.Controls.Add(_chkAutoSend);

        // ── Section: thermal printer ──────────────────────────────────────
        var grpPrinter = new GroupBox
        {
            Text     = "מדפסת תרמית (גיבוי)",
            Location = new Point(10, 83),
            Width    = 400,
            Height   = 65,
        };

        var lblPrinter = new Label
        {
            Text     = "בחר מדפסת:",
            AutoSize = true,
            Location = new Point(10, 28),
        };

        _cmbPrinter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width         = 260,
            Location      = new Point(100, 25),
        };
        PopulatePrinters(config.ThermalPrinterName);

        grpPrinter.Controls.AddRange([lblPrinter, _cmbPrinter]);

        // ── Buttons ───────────────────────────────────────────────────────
        var btnSave = new Button
        {
            Text      = "שמור",
            Width     = 90,
            Height    = 32,
            Location  = new Point(10, 158),
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
            Location = new Point(110, 158),
        };
        btnCancel.Click += (_, _) => Close();

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.AddRange([grpAuto, grpPrinter, btnSave, btnCancel]);
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

        _writer.Save(
            autoSendIfPhoneKnown: _chkAutoSend.Checked,
            thermalPrinterName:   printerName);

        Close();
    }
}
