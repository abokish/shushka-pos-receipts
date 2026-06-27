using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class BuildMessageTests
{
    private static readonly ShushkaConfig DefaultConfig = new();

    // Representative decoded text matching the proven sample from the spec
    private const string SampleDecoded = """
        טבע בוקיש
        מספר הזמנה 1504
        תאריך 24/06/26
        שעה 21:08
        001תות שדה קפוא 2 קילו 42.00*
        002אננס קפוא 2 קילו 52.00*
        9משלוח בערבה 10.00*
        לתשלום 104.00
        """;

    [Fact]
    public void ContainsStoreName()
    {
        string msg = ReceiptParser.BuildMessage(SampleDecoded, DefaultConfig);
        Assert.StartsWith("טבע בוקיש", msg.TrimStart());
    }

    [Fact]
    public void ContainsOrderNumber()
    {
        string msg = ReceiptParser.BuildMessage(SampleDecoded, DefaultConfig);
        Assert.Contains("הזמנה 1504", msg);
    }

    [Fact]
    public void ContainsDateTime()
    {
        string msg = ReceiptParser.BuildMessage(SampleDecoded, DefaultConfig);
        Assert.Contains("24/06/26", msg);
        Assert.Contains("21:08", msg);
    }

    [Fact]
    public void ContainsItemWithPrice()
    {
        string msg = ReceiptParser.BuildMessage(SampleDecoded, DefaultConfig);
        // Item code stripped, description present, price formatted with ₪
        Assert.Contains("תות שדה קפוא 2 קילו", msg);
        Assert.Contains("₪42.00", msg);
    }

    [Fact]
    public void ContainsTotal()
    {
        string msg = ReceiptParser.BuildMessage(SampleDecoded, DefaultConfig);
        Assert.Contains("₪104.00", msg);
    }

    [Fact]
    public void ContainsSeparators()
    {
        string msg = ReceiptParser.BuildMessage(SampleDecoded, DefaultConfig);
        Assert.Contains(DefaultConfig.MessageSeparator, msg);
    }

    [Fact]
    public void ContainsClosingMessage()
    {
        string msg = ReceiptParser.BuildMessage(SampleDecoded, DefaultConfig);
        Assert.Contains(DefaultConfig.MessageClosing, msg);
    }

    [Fact]
    public void ShortItemCode_DoesNotStripDescription()
    {
        // Delivery line with 1-digit code: "9משלוח בערבה 10.00*"
        // The 1-digit code is NOT stripped (only 3-6 digit codes are stripped).
        // This is the known edge case: code glues to description.
        string msg = ReceiptParser.BuildMessage(SampleDecoded, DefaultConfig);
        // The line is still parsed (has a price), so some form of "משלוח" appears
        Assert.Contains("משלוח", msg);
    }

    [Fact]
    public void EmptyDecoded_ReturnsGracefully()
    {
        string msg = ReceiptParser.BuildMessage("", DefaultConfig);
        // Should not throw; closing message is always appended
        Assert.Contains(DefaultConfig.MessageClosing, msg);
    }
}
