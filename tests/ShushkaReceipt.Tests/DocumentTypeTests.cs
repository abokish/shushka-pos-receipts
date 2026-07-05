using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class DocumentTypeTests
{
    [Theory]
    [InlineData("חשבונית עסקה 01/020550\nכרוב 20.83\nלתשלום 44.83")]
    [InlineData("טבע בוקיש\nחשבונית עסקה\nלתשלום 30.00")]
    public void Receipt_IsClassifiedAsReceipt(string decoded)
    {
        Assert.Equal(DocumentType.Receipt, ReceiptParser.GetDocumentType(decoded));
    }

    [Theory]
    [InlineData("טבע בוקיש\nמספר הזמנה 1504\nתאריך 24/06/26\nלתשלום 238.00")]
    [InlineData("מספר הזמנה 999\nפריט אחד 10.00\nלתשלום 10.00")]
    public void Order_IsClassifiedAsOrder(string decoded)
    {
        Assert.Equal(DocumentType.Order, ReceiptParser.GetDocumentType(decoded));
    }

    [Theory]
    [InlineData("דוח Z\nסה\"כ מכירות 1234.56\nסגירת יום")]
    [InlineData("פתיחת קופאי\nמאיה\n08:30")]
    [InlineData("")]
    public void Internal_IsClassifiedAsInternal(string decoded)
    {
        Assert.Equal(DocumentType.Internal, ReceiptParser.GetDocumentType(decoded));
    }

    [Fact]
    public void Receipt_TakesPriorityOverOrder_WhenBothPatternsPresent()
    {
        // חשבונית עסקה wins — that's a tax invoice, not a plain order
        string decoded = "חשבונית עסקה\nמספר הזמנה 1\nלתשלום 10.00";
        Assert.Equal(DocumentType.Receipt, ReceiptParser.GetDocumentType(decoded));
    }
}
