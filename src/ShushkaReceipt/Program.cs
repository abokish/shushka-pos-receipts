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

builder.Services.AddSingleton<ThermalPrinterService>(sp =>
{
    var config = sp.GetRequiredService<ShushkaConfig>();
    return new ThermalPrinterService(config.ThermalPrinterName);
});

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<TrayAndHotkeyService>();

var host = builder.Build();
host.Run();
