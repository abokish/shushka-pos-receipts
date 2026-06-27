using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class ThermalPrinterServiceTests
{
    [Fact]
    public void IsConfigured_EmptyName_ReturnsFalse()
    {
        var svc = new ThermalPrinterService("");
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void IsConfigured_WhitespaceName_ReturnsFalse()
    {
        var svc = new ThermalPrinterService("   ");
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void IsConfigured_NonEmptyName_ReturnsTrue()
    {
        var svc = new ThermalPrinterService("EPSON TM-T20III");
        Assert.True(svc.IsConfigured);
    }

    [Fact]
    public void PrintRaw_NotConfigured_ReturnsFalseWithoutThrowing()
    {
        var svc = new ThermalPrinterService("");
        bool result = svc.PrintRaw([0x1B, 0x40]); // ESC @ init
        Assert.False(result);
    }

    [Fact]
    public void PrintRaw_NonExistentPrinter_ReturnsFalseWithoutThrowing()
    {
        // Win32 OpenPrinter fails for a name that doesn't exist → PrintRaw returns false
        var svc = new ThermalPrinterService("__NonExistentPrinter__XYZ__");
        bool result = svc.PrintRaw([0x41, 0x42]); // "AB"
        Assert.False(result);
    }
}
