using System.Text;
using Microsoft.Extensions.Options;
using ShushkaReceipt;
using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
    options.ServiceName = "ShushkaReceipt");

builder.Services.Configure<ShushkaConfig>(
    builder.Configuration.GetSection("Shushka"));

builder.Services.AddSingleton<ShushkaConfig>(sp =>
    sp.GetRequiredService<IOptions<ShushkaConfig>>().Value);

builder.Services.AddSingleton<AppState>();

builder.Services.AddSingleton<FileJobLogger>(sp =>
{
    var config = sp.GetRequiredService<ShushkaConfig>();
    return new FileJobLogger(config.LogFilePath, config.LogMaxSizeBytes);
});

// ThermalPrinterService reads ThermalPrinterName from the live config
// so settings changes take effect immediately.
builder.Services.AddSingleton<ThermalPrinterService>(sp =>
    new ThermalPrinterService(sp.GetRequiredService<ShushkaConfig>()));

// AppSettingsWriter persists settings changes back to appsettings.json.
builder.Services.AddSingleton<AppSettingsWriter>(sp =>
    new AppSettingsWriter(
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        sp.GetRequiredService<ShushkaConfig>()));

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<TrayAndHotkeyService>();

var host = builder.Build();
host.Run();
