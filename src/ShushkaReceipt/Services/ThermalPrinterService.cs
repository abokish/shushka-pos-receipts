using System.Runtime.InteropServices;
using ShushkaReceipt.Config;

namespace ShushkaReceipt.Services;

/// <summary>
/// Forwards raw ESC/POS bytes directly to a Windows printer via Win32.
/// No GDI rendering — identical to what WritePrinter-based POS software does.
/// Reads ThermalPrinterName from the live ShushkaConfig so settings changes
/// take effect immediately without restarting.
/// </summary>
public sealed class ThermalPrinterService
{
    // Win32 raw-printing API (same pattern as Microsoft KB322090)
    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DocInfo1 pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DocInfo1
    {
        public string pDocName;
        public string? pOutputFile;
        public string pDataType;
    }

    private readonly ShushkaConfig _config;

    public ThermalPrinterService(ShushkaConfig config)
    {
        _config = config;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config.ThermalPrinterName);

    /// <summary>
    /// Sends raw ESC/POS bytes to the configured thermal printer.
    /// Returns true on success, false if the printer name is not configured or Win32 fails.
    /// </summary>
    public bool PrintRaw(byte[] data)
    {
        string printerName = _config.ThermalPrinterName;
        if (string.IsNullOrWhiteSpace(printerName)) return false;

        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            return false;

        try
        {
            var docInfo = new DocInfo1
            {
                pDocName   = "Shushka Receipt",
                pOutputFile = null,
                pDataType  = "RAW"
            };

            if (StartDocPrinter(hPrinter, 1, ref docInfo) == 0) return false;
            try
            {
                if (!StartPagePrinter(hPrinter)) return false;
                try
                {
                    var ptr = Marshal.AllocHGlobal(data.Length);
                    try
                    {
                        Marshal.Copy(data, 0, ptr, data.Length);
                        return WritePrinter(hPrinter, ptr, data.Length, out _);
                    }
                    finally { Marshal.FreeHGlobal(ptr); }
                }
                finally { EndPagePrinter(hPrinter); }
            }
            finally { EndDocPrinter(hPrinter); }
        }
        finally { ClosePrinter(hPrinter); }
    }
}
