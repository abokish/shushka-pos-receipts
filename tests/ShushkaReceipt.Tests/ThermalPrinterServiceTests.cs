using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class ThermalPrinterServiceTests
{
    private static ThermalPrinterService Make(string printerName) =>
        new(new ShushkaConfig { ThermalPrinterName = printerName });

    [Fact]
    public void IsConfigured_EmptyName_ReturnsFalse()
    {
        Assert.False(Make("").IsConfigured);
    }

    [Fact]
    public void IsConfigured_WhitespaceName_ReturnsFalse()
    {
        Assert.False(Make("   ").IsConfigured);
    }

    [Fact]
    public void IsConfigured_NonEmptyName_ReturnsTrue()
    {
        Assert.True(Make("EPSON TM-T20III").IsConfigured);
    }

    [Fact]
    public void PrintRaw_NotConfigured_ReturnsFalseWithoutThrowing()
    {
        Assert.False(Make("").PrintRaw([0x1B, 0x40]));
    }

    [Fact]
    public void PrintRaw_NonExistentPrinter_ReturnsFalseWithoutThrowing()
    {
        Assert.False(Make("__NonExistentPrinter__XYZ__").PrintRaw([0x41, 0x42]));
    }

    [Fact]
    public void IsConfigured_ReflectsLiveConfigChange()
    {
        // Changing ThermalPrinterName on the config should be reflected immediately
        var config = new ShushkaConfig { ThermalPrinterName = "" };
        var svc = new ThermalPrinterService(config);
        Assert.False(svc.IsConfigured);

        config.ThermalPrinterName = "EPSON TM-T20III";
        Assert.True(svc.IsConfigured);

        config.ThermalPrinterName = "";
        Assert.False(svc.IsConfigured);
    }
}
