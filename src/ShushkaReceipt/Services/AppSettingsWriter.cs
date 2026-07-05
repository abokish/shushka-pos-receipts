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

    /// <summary>Full settings-form save: updates all user-configurable fields.</summary>
    public void Save(
        bool   autoSendIfPhoneKnown,
        string thermalPrinterName,
        string storePhone,
        string ownerPhone,
        string localSavePath)
    {
        _config.AutoSendIfPhoneKnown = autoSendIfPhoneKnown;
        _config.ThermalPrinterName   = thermalPrinterName;
        _config.StorePhone           = storePhone;
        _config.OwnerPhone           = ownerPhone;
        _config.LocalSavePath        = localSavePath;

        PatchJson(node =>
        {
            node["AutoSendIfPhoneKnown"] = autoSendIfPhoneKnown;
            node["ThermalPrinterName"]   = thermalPrinterName;
            node["StorePhone"]           = storePhone;
            node["OwnerPhone"]           = ownerPhone;
            node["LocalSavePath"]        = localSavePath;
        });
    }

    /// <summary>
    /// Saves the store phone on first use (when cashier enters it from the dispatch popup).
    /// </summary>
    public void SaveStorePhone(string phoneE164)
    {
        _config.StorePhone = phoneE164;
        PatchJson(node => node["StorePhone"] = phoneE164);
    }

    /// <summary>
    /// Saves the owner phone on first use (when cashier enters it from the dispatch popup).
    /// </summary>
    public void SaveOwnerPhone(string phoneE164)
    {
        _config.OwnerPhone = phoneE164;
        PatchJson(node => node["OwnerPhone"] = phoneE164);
    }

    private void PatchJson(Action<JsonObject> patch)
    {
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

            patch(shushka);
            File.WriteAllText(_path, root.ToJsonString(JsonOptions));
        }
    }
}
