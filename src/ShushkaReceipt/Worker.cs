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
    private readonly ILogger<Worker> _logger;
    private readonly ShushkaConfig _config;
    private readonly AppState _appState;
    private readonly FileJobLogger _fileLogger;
    private readonly ThermalPrinterService _thermal;

    public Worker(
        ILogger<Worker> logger,
        IOptions<ShushkaConfig> config,
        AppState appState,
        FileJobLogger fileLogger,
        ThermalPrinterService thermal)
    {
        _logger   = logger;
        _config   = config.Value;
        _appState = appState;
        _fileLogger = fileLogger;
        _thermal  = thermal;
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
        _logger.LogInformation("Listening for print jobs on 127.0.0.1:{Port}", _config.ListenPort);

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

        string? phone   = ReceiptParser.ExtractCustomerPhone(decoded, _config);
        string message  = ReceiptParser.BuildMessage(decoded, _config);
        string summary  = BuildSummaryLine(decoded);

        _fileLogger.Log($"JOB | bytes={rawBytes.Length} | phone={phone ?? "none"}");

        // Auto-send: phone known + setting on → open WhatsApp silently, no popup
        if (_config.AutoSendIfPhoneKnown && phone is not null)
        {
            WhatsAppService.LaunchDeepLink(
                WhatsAppService.BuildWhatsAppLink(phone, message));
            _fileLogger.Log($"AUTO_WHATSAPP | phone={phone}");
            _logger.LogInformation("Auto-sent WhatsApp for {Phone}", phone);
            return;
        }

        ShowDispatchForm(summary, message, phone, rawBytes);
    }

    private void ShowDispatchForm(
        string summary, string message, string? prefilledPhone, byte[] rawBytes)
    {
        var thread = new Thread(() =>
        {
            using var form = new DispatchForm(
                summary,
                message,
                prefilledPhone,
                _thermal.IsConfigured);

            Application.Run(form);

            // Log the cashier's choice
            switch (form.Result)
            {
                case DispatchForm.Choice.WhatsApp:
                    _fileLogger.Log($"WHATSAPP | phone={form.PhoneE164}");
                    _logger.LogInformation("WhatsApp link launched for {Phone}", form.PhoneE164);
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

    // Builds the one-line summary shown at the top of the dispatch form.
    // Falls back gracefully if parsing can't find the fields.
    private static string BuildSummaryLine(string decoded)
    {
        var lines = decoded.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

        string store = lines.Length > 0 ? lines[0] : "";

        string? order = ExtractFirst(lines, @"מספר הזמנה\s+(\d+)");
        string? total = ExtractFirst(lines, @"לתשלום\s+(\d+\.\d{2})");

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(store)) parts.Add(store);
        if (order is not null) parts.Add($"הזמנה {order}");
        if (total is not null) parts.Add($"₪{total}");

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
