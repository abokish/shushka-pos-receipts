using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

/// <summary>
/// Verifies the parser handles the document types the store uses.
/// Input is already-decoded text (as it would appear after DecodeReceipt).
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

    // ── Type 2: חשבונית עסקה (tax invoice) — synthetic ───────────────────

    private const string TaxInvoiceDecoded = """
        טבע בוקיש
        חשבונית עסקה
        001בנז ינוב 30.00*
        לתשלום 30.00
        """;

    [Fact]
    public void TaxInvoice_ParsesItems()
    {
        string message = ReceiptParser.BuildMessage(TaxInvoiceDecoded, Config);
        Assert.Contains("₪30.00",             message);
        Assert.Contains(Config.MessageClosing, message);
    }

    // ── Type 2b: Real חשבונית עסקה layout ────────────────────────────────
    // Mirrors the actual receipt photo: column header, weight sub-line,
    // no-leading-zero phone, and sections below לתשלום that must be excluded.

    private const string TaxInvoiceRealLayout = """
        טבע בוקיש
        חשבונית עסקה 01/020550
        לקוח: ענת וצביקה בן חיים
        מספר לקוח: 29
        טלפון: 543090412
        קוד תיאור סכום
        22564 כרוב 20.83
        2.340 ק"ג X 8.90 ש"ח\ק"ג
        8850161161513 מי קוקוס קוקומקס ליטר ב 24.00
        לתשלום 44.83
        אשראי לקוהרון 44.83
        פירוט חיוב אשראי לקוח:
        חוב קודם: 25.00
        סה"כ חיוב בחשבון זה: 44.83
        חוב לאחר חשבון זה: 69.83
        סכום חייב במע"מ 0%: 20.83
        סכום חייב במע"מ 18% *: 20.34
        """;

    [Fact]
    public void TaxInvoice_RealLayout_ParsesItems()
    {
        string message = ReceiptParser.BuildMessage(TaxInvoiceRealLayout, Config);
        Assert.Contains("כרוב",   message);
        Assert.Contains("₪20.83", message);
        Assert.Contains("קוקוס",  message);
        Assert.Contains("₪24.00", message);
    }

    [Fact]
    public void TaxInvoice_RealLayout_Total()
    {
        string message = ReceiptParser.BuildMessage(TaxInvoiceRealLayout, Config);
        Assert.Contains("₪44.83", message);
    }

    [Fact]
    public void TaxInvoice_RealLayout_InvoiceNumber()
    {
        string message = ReceiptParser.BuildMessage(TaxInvoiceRealLayout, Config);
        Assert.Contains("חשבונית עסקה 01/020550", message);
    }

    [Fact]
    public void TaxInvoice_RealLayout_SkipsWeightSubLine()
    {
        // "2.340 ק\"ג X 8.90 ש\"ח\ק\"ג" must not appear as a separate item
        string message = ReceiptParser.BuildMessage(TaxInvoiceRealLayout, Config);
        Assert.DoesNotContain("2.340", message);
        Assert.DoesNotContain("₪8.90", message);
    }

    [Fact]
    public void TaxInvoice_RealLayout_ExcludesDebtSection()
    {
        // Account/debt/VAT detail lines appear after לתשלום — must not be in message
        string message = ReceiptParser.BuildMessage(TaxInvoiceRealLayout, Config);
        Assert.DoesNotContain("חוב קודם",   message);
        Assert.DoesNotContain("חוב לאחר",   message);
        Assert.DoesNotContain("מע\"מ 0%",   message);
        Assert.DoesNotContain("מע\"מ 18%",  message);
    }

    [Fact]
    public void TaxInvoice_RealLayout_PhoneExtraction()
    {
        string? phone = ReceiptParser.ExtractCustomerPhone(TaxInvoiceRealLayout, Config);
        Assert.Equal("972543090412", phone);
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
        string message = ReceiptParser.BuildMessage(DeliveryItemDecoded, Config);
        Assert.Contains("₪10.00", message);
        Assert.Contains("משלוח",  message);
    }

    [Fact]
    public void ShortItemCode_IsNotStripped()
    {
        // Only 3+ digit codes are stripped; 1-digit code "9" stays glued to description
        string message = ReceiptParser.BuildMessage(DeliveryItemDecoded, Config);
        Assert.Contains("9משלוח", message);
    }
}
