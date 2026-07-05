using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using Microsoft.Extensions.Options;
using ShushkaReceipt.Config;
using ShushkaReceipt.Forms;
using ShushkaReceipt.Services;

namespace ShushkaReceipt;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker>   _logger;
    private readonly ShushkaConfig     _config;
    private readonly AppState          _appState;
    private readonly FileJobLogger     _fileLogger;
    private readonly ThermalPrinterService _thermal;
    private readonly AppSettingsWriter _writer;

    public Worker(
        ILogger<Worker>    logger,
        IOptions<ShushkaConfig> config,
        AppState           appState,
        FileJobLogger      fileLogger,
        ThermalPrinterService thermal,
        AppSettingsWriter  writer)
    {
        _logger     = logger;
        _config     = config.Value;
        _appState   = appState;
        _fileLogger = fileLogger;
        _thermal    = thermal;
        _writer     = writer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var listener = new TcpListener(IPAddress.Loopback, _config.ListenPort);
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot bind to 127.0.0.1:{Port}", _config.ListenPort);
            _appState.SetListenerActive(false);
            return;
        }

        _appState.SetListenerActive(true);
        _logger.LogInformation("Listening on 127.0.0.1:{Port}", _config.ListenPort);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(stoppingToken); }
                catch (OperationCanceledException) { break; }

                await HandleJobAsync(client, stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
            _appState.SetListenerActive(false);
            _logger.LogInformation("Listener stopped");
        }
    }

    private async Task HandleJobAsync(TcpClient client, CancellationToken ct)
    {
        using var _ = client;

        byte[] rawBytes;
        try
        {
            using var ms = new MemoryStream();
            await client.GetStream().CopyToAsync(ms, ct);
            rawBytes = ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading print job stream");
            return;
        }

        _logger.LogInformation("Print job received: {Bytes} bytes", rawBytes.Length);

        string decoded;
        try { decoded = ReceiptDecoder.DecodeReceipt(rawBytes); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode receipt");
            _fileLogger.Log($"DECODE_ERROR | bytes={rawBytes.Length} | {ex.Message}");
            return;
        }

        var docType = ReceiptParser.GetDocumentType(decoded);
        string message = ReceiptParser.BuildMessage(decoded, _config);
        string summary = BuildSummaryLine(decoded, docType);

        _fileLogger.Log($"JOB | type={docType} | bytes={rawBytes.Length}");

        switch (docType)
        {
            case DocumentType.Receipt:
                HandleReceipt(decoded, message, summary, rawBytes);
                break;

            case DocumentType.Order:
                string? orderPhone = ReceiptParser.ExtractCustomerPhone(decoded, _config);
                ShowDispatchForm(DispatchForm.DispatchMode.Order, summary, message, orderPhone, rawBytes, docType);
                break;

            case DocumentType.Internal:
                ShowDispatchForm(DispatchForm.DispatchMode.Internal, summary, message, null, rawBytes, docType);
                break;
        }
    }

    // ── Receipt: auto-send if phone found; prompt if not ──────────────────

    private void HandleReceipt(string decoded, string message, string summary, byte[] rawBytes)
    {
        string? phone = ReceiptParser.ExtractCustomerPhone(decoded, _config);

        if (phone is not null)
        {
            WhatsAppService.LaunchDeepLink(WhatsAppService.BuildWhatsAppLink(phone, message));
            _fileLogger.Log($"RECEIPT_AUTO | phone={phone}");
            _logger.LogInformation("Receipt sent to {Phone}", phone);
            return;
        }

        // No customer phone on the receipt — ask cashier
        ShowDispatchForm(DispatchForm.DispatchMode.CustomerNoPhone, summary, message, null, rawBytes, DocumentType.Receipt);
    }

    // ── Shared dispatch popup ─────────────────────────────────────────────

    private void ShowDispatchForm(
        DispatchForm.DispatchMode mode,
        string    summary,
        string    message,
        string?   prefilledPhone,
        byte[]    rawBytes,
        DocumentType docType)
    {
        var thread = new Thread(() =>
        {
            using var form = new DispatchForm(
                mode, summary, message, prefilledPhone,
                _thermal.IsConfigured, _config, _writer);

            Application.Run(form);

            switch (form.Result)
            {
                case DispatchForm.Choice.ToCustomer:
                    _fileLogger.Log($"WHATSAPP_CUSTOMER | phone={form.PhoneE164}");
                    _logger.LogInformation("Sent to customer {Phone}", form.PhoneE164);
                    break;

                case DispatchForm.Choice.ToStore:
                    _fileLogger.Log($"WHATSAPP_STORE | phone={form.PhoneE164}");
                    _logger.LogInformation("Sent to store {Phone}", form.PhoneE164);
                    break;

                case DispatchForm.Choice.ToOwner:
                    _fileLogger.Log($"WHATSAPP_OWNER | phone={form.PhoneE164}");
                    _logger.LogInformation("Sent to owner {Phone}", form.PhoneE164);
                    break;

                case DispatchForm.Choice.ToNumber:
                    _fileLogger.Log($"WHATSAPP_NUMBER | phone={form.PhoneE164}");
                    _logger.LogInformation("Sent to {Phone}", form.PhoneE164);
                    break;

                case DispatchForm.Choice.SaveLocally:
                    string? path = LocalSaveService.Save(message, docType, _config);
                    _fileLogger.Log(path is not null ? $"SAVED | {path}" : "SAVE_FAILED");
                    if (path is null) _logger.LogWarning("Local save failed");
                    break;

                case DispatchForm.Choice.Print:
                    bool ok = _thermal.PrintRaw(rawBytes);
                    _fileLogger.Log(ok ? "PRINTED" : "PRINT_FAILED");
                    if (!ok) _logger.LogWarning("Thermal print failed");
                    break;

                case DispatchForm.Choice.Skip:
                    _fileLogger.Log("SKIPPED");
                    break;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    // ── Summary line for the top of the popup ─────────────────────────────

    private static string BuildSummaryLine(string decoded, DocumentType docType)
    {
        var lines = decoded.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        string store = lines.Length > 0 ? lines[0] : "";

        if (docType == DocumentType.Internal)
        {
            // Try to identify the type from first few lines
            string header = string.Join(" ", lines.Take(3));
            string label  = header.Contains("Z") || header.Contains("יומי") ? "דוח יומי" :
                            header.Contains("קופאי") || header.Contains("פתיחה") ? "פתיחת קופאי" :
                            "דוח פנימי";
            return string.IsNullOrEmpty(store) ? label : $"{store}  |  {label}";
        }

        string? order = ExtractFirst(lines, @"מספר הזמנה\s+(\d+)");
        string? inv   = ExtractFirst(lines, @"חשבונית עסקה\s+([\d/]+)");
        string? total = ExtractFirst(lines, @"לתשלום\s+(\d+\.\d{2})");

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(store))  parts.Add(store);
        if (order is not null)             parts.Add($"הזמנה {order}");
        else if (inv is not null)          parts.Add($"חשבונית {inv}");
        if (total is not null)             parts.Add($"₪{total}");

        return string.Join("  |  ", parts);
    }

    private static string? ExtractFirst(string[] lines, string pattern)
    {
        foreach (var l in lines)
        {
            var m = System.Text.RegularExpressions.Regex.Match(l, pattern);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }
}
