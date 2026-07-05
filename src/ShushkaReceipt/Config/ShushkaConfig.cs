namespace ShushkaReceipt.Config;

public sealed class ShushkaConfig
{
    public int ListenPort { get; set; } = 9100;
    public string LogFilePath { get; set; } = @"C:\ProgramData\ShushkaReceipt\receipt-jobs.log";

    // Customer phone extraction anchors
    public string CustomerBlockAnchor { get; set; } = "מספר לקוח";
    public string CustomerPhoneLabel { get; set; } = "טלפון:";
    public string PhoneRegex { get; set; } = @"0\d{1,2}-?\d{7}";

    // Message template
    public string MessageSeparator { get; set; } = "——————————————————————";
    public string MessageClosing { get; set; } = "תודה ולהתראות!";

    // Log rotation: rotate when file exceeds this size (bytes). Default 5 MB.
    public long LogMaxSizeBytes { get; set; } = 5 * 1024 * 1024;

    // Thermal fallback printer — exact Windows printer name (e.g. "EPSON TM-T20III")
    public string ThermalPrinterName { get; set; } = "";

    // Routing phones — set via Settings form or auto-saved on first use
    public string StorePhone { get; set; } = "";  // E.164, e.g. "972501234567"
    public string OwnerPhone { get; set; } = "";  // E.164

    // Folder for locally-saved print jobs (Z reports, orders, etc.)
    public string LocalSavePath { get; set; } = @"C:\קופה\";

    // Legacy: receipts now always auto-send when phone is found.
    public bool AutoSendIfPhoneKnown { get; set; } = false;
}
