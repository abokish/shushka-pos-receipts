using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

/// <summary>
/// Sanity check: verifies the PARSER handles the three document types the store uses.
/// Input is already-decoded text (as it would appear after DecodeReceipt), so these
/// tests focus on ReceiptParser.BuildMessage rather than the decoder.
/// The decoder is exercised end-to-end in DecodeReceiptTests and TcpLoopbackTests.
/// </summary>
public class DocumentTypeCoverageTests
{
    private static readonly ShushkaConfig Config = new();

    // ── Type 1: Order (הזמנה) ─────────────────────────────────────────────

    private const string OrderDecoded = """
        טבע בוקיש
        מספר הזמנה 1504
        תאריך 24/06/26
        שעה 21:08
        001תות שדה 42.00*
        לתשלום 42.00
        """;

    [Fact]
    public void Order_DecodesStoreName()
    {
        string message = ReceiptParser.BuildMessage(OrderDecoded, Config);
        Assert.Contains("טבע בוקיש", message);
    }

    [Fact]
    public void Order_BuildsMessage()
    {
        string message = ReceiptParser.BuildMessage(OrderDecoded, Config);
        Assert.Contains("הזמנה 1504", message);
        Assert.Contains("₪42.00",     message);
    }

    // ── Type 2: חשבונית עסקה (tax invoice) ───────────────────────────────

    private const string TaxInvoiceDecoded = """
        טבע בוקיש
        חשבונית עסקה
        001בנז ינוב 30.00*
        לתשלום 30.00
        """;

    [Fact]
    public void TaxInvoice_ParsesItems()
    {
        // Tax invoice has no order number, but items and total are still parsed
        string message = ReceiptParser.BuildMessage(TaxInvoiceDecoded, Config);
        Assert.Contains("₪30.00",             message);
        Assert.Contains(Config.MessageClosing, message);
    }

    // ── Type 3: Refund / זיכוי ────────────────────────────────────────────

    private const string RefundDecoded = """
        טבע בוקיש
        זיכוי
        001בנז ינוב 30.00*
        לתשלום 30.00
        """;

    [Fact]
    public void Refund_ParsesItems()
    {
        string message = ReceiptParser.BuildMessage(RefundDecoded, Config);
        Assert.Contains("₪30.00",             message);
        Assert.Contains(Config.MessageClosing, message);
    }

    // ── Edge case: item with short code ───────────────────────────────────

    private const string DeliveryItemDecoded = """
        טבע בוקיש
        9משלוח בערבה 10.00*
        לתשלום 10.00
        """;

    [Fact]
    public void ShortItemCode_DeliveryLine_AppearInMessage()
    {
        // Short code "9" (1 digit) glues to description — known edge case.
        // The item IS still parsed (price found), so delivery appears in the output.
        string message = ReceiptParser.BuildMessage(DeliveryItemDecoded, Config);
        Assert.Contains("₪10.00", message);
        Assert.Contains("משלוח",  message);
    }

    [Fact]
    public void ShortItemCode_IsNotStripped()
    {
        // Only 3-6 digit codes are stripped; 1-digit code "9" stays glued to description
        string message = ReceiptParser.BuildMessage(DeliveryItemDecoded, Config);
        // "9משלוח" (with code) rather than just "משלוח" (without) — code is present
        Assert.Contains("9משלוח", message);
    }
}
