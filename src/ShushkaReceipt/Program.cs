using System.Text;
using Microsoft.Extensions.Options;
using ShushkaReceipt;
using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Config search order:
//   1. C:\ProgramData\Shushka\appsettings.json  (production install)
//   2. AppContext.BaseDirectory\appsettings.json  (development / portable)
string programDataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "Shushka");
string configDir = File.Exists(Path.Combine(programDataDir, "appsettings.json"))
    ? programDataDir
    : AppContext.BaseDirectory;
string configPath = Path.Combine(configDir, "appsettings.json");

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile(configPath, optional: false, reloadOnChange: false);

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
    new ThermalPrinterService(sp.GetRequiredService<ShushkaConfig>()));

builder.Services.AddSingleton<AppSettingsWriter>(sp =>
    new AppSettingsWriter(configPath, sp.GetRequiredService<ShushkaConfig>()));

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<TrayAndHotkeyService>();

var host = builder.Build();
host.Run();
