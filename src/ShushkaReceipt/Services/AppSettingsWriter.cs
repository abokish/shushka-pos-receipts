using System.Text.Json;
using System.Text.Json.Nodes;
using ShushkaReceipt.Config;

namespace ShushkaReceipt.Services;

/// <summary>
/// Writes updated values back to appsettings.json so settings survive restarts.
/// Also updates the live ShushkaConfig singleton so changes take effect immediately.
/// </summary>
public sealed class AppSettingsWriter
{
    private readonly string _path;
    private readonly ShushkaConfig _config;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettingsWriter(string path, ShushkaConfig config)
    {
        _path   = path;
        _config = config;
    }

    public void Save(bool autoSendIfPhoneKnown, string thermalPrinterName)
    {
        _config.AutoSendIfPhoneKnown = autoSendIfPhoneKnown;
        _config.ThermalPrinterName   = thermalPrinterName;

        lock (_lock)
        {
            JsonObject root;
            try   { root = JsonNode.Parse(File.ReadAllText(_path))!.AsObject(); }
            catch { root = new JsonObject(); }

            if (root["Shushka"] is not JsonObject shushka)
            {
                shushka = new JsonObject();
                root["Shushka"] = shushka;
            }

            shushka["AutoSendIfPhoneKnown"] = autoSendIfPhoneKnown;
            shushka["ThermalPrinterName"]   = thermalPrinterName;

            File.WriteAllText(_path, root.ToJsonString(JsonOptions));
        }
    }
}
