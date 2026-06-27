using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class ExtractCustomerPhoneTests
{
    private static readonly ShushkaConfig DefaultConfig = new();

    // Receipt that has the store's own phone in the header but NO customer phone
    private const string NoCustomerPhone = """
        טבע בוקיש
        טל: 054-6995623
        מספר הזמנה 1504
        תאריך 24/06/26
        """;

    // Receipt with a customer block that contains a phone
    private const string WithCustomerPhone = """
        טבע בוקיש
        טל: 054-6995623
        מספר הזמנה 1504
        מספר לקוח 42
        שם: ישראל ישראלי
        טלפון: 052-1234567
        """;

    // Receipt with a customer block but no phone on file
    private const string CustomerBlockNoPhone = """
        טבע בוקיש
        טל: 054-6995623
        מספר הזמנה 1504
        מספר לקוח 42
        שם: ישראל ישראלי
        טלפון:
        """;

    [Fact]
    public void StoreHeaderPhone_NotExtracted()
    {
        // The store phone (054-6995623) is in the header — must be ignored
        string? phone = ReceiptParser.ExtractCustomerPhone(NoCustomerPhone, DefaultConfig);
        Assert.Null(phone);
    }

    [Fact]
    public void CustomerBlockPhone_IsExtracted()
    {
        string? phone = ReceiptParser.ExtractCustomerPhone(WithCustomerPhone, DefaultConfig);
        Assert.NotNull(phone);
    }

    [Fact]
    public void CustomerBlockPhone_ConvertedToE164()
    {
        string? phone = ReceiptParser.ExtractCustomerPhone(WithCustomerPhone, DefaultConfig);
        // 052-1234567 → 9725 21234567 → "972521234567"
        Assert.Equal("972521234567", phone);
    }

    [Fact]
    public void CustomerBlockPhoneWithDash_DashRemoved()
    {
        string? phone = ReceiptParser.ExtractCustomerPhone(WithCustomerPhone, DefaultConfig);
        Assert.NotNull(phone);
        Assert.DoesNotContain("-", phone!);
    }

    [Fact]
    public void EmptyPhoneLabel_ReturnsNull()
    {
        string? phone = ReceiptParser.ExtractCustomerPhone(CustomerBlockNoPhone, DefaultConfig);
        Assert.Null(phone);
    }

    [Fact]
    public void NoCustomerBlock_ReturnsNull()
    {
        string? phone = ReceiptParser.ExtractCustomerPhone(NoCustomerPhone, DefaultConfig);
        Assert.Null(phone);
    }

    [Fact]
    public void PhoneWithoutDash_Accepted()
    {
        string decoded = """
            מספר לקוח 1
            טלפון: 0521234567
            """;
        string? phone = ReceiptParser.ExtractCustomerPhone(decoded, DefaultConfig);
        Assert.Equal("972521234567", phone);
    }
}
