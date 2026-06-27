using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class WhatsAppServiceTests
{
    // Use AbsoluteUri (not ToString) — Uri.ToString() may decode %20 → space in .NET 9.
    // AbsoluteUri always returns the canonically percent-encoded form.

    [Fact]
    public void BuildWhatsAppLink_SchemeIsWhatsApp()
    {
        var uri = WhatsAppService.BuildWhatsAppLink("972501234567", "hello");
        Assert.Equal("whatsapp", uri.Scheme);
    }

    [Fact]
    public void BuildWhatsAppLink_PhoneInQuery()
    {
        var uri = WhatsAppService.BuildWhatsAppLink("972501234567", "hello");
        Assert.Contains("phone=972501234567", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildWhatsAppLink_TextUrlEncoded()
    {
        var uri = WhatsAppService.BuildWhatsAppLink("972501234567", "hello world");
        // space must be percent-encoded (not a literal space in the link)
        Assert.DoesNotContain("hello world", uri.AbsoluteUri); // no raw space
        Assert.Contains("hello", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildWhatsAppLink_ShekelSign_Encoded()
    {
        var uri = WhatsAppService.BuildWhatsAppLink("972501234567", "₪42.00");
        // ₪ (U+20AA) must be percent-encoded in the absolute URI
        Assert.DoesNotContain("₪", uri.AbsoluteUri);
        Assert.Contains("%", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildWhatsAppLink_HebrewText_Encoded()
    {
        var uri = WhatsAppService.BuildWhatsAppLink("972501234567", "שלום");
        // Hebrew chars must be percent-encoded
        Assert.DoesNotContain("שלום", uri.AbsoluteUri);
        Assert.Contains("%", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildWhatsAppLink_NewlinesEncoded()
    {
        var uri = WhatsAppService.BuildWhatsAppLink("972501234567", "line1\nline2");
        Assert.DoesNotContain("\n", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildWhatsAppLink_RoundTrip_MessageRecoverable()
    {
        string original = "שלום\n₪42.00\nthanks!";
        var uri = WhatsAppService.BuildWhatsAppLink("972501234567", original);

        // Extract text= parameter from AbsoluteUri and decode it
        string raw     = uri.AbsoluteUri;
        int textIdx    = raw.IndexOf("text=", StringComparison.Ordinal);
        Assert.True(textIdx >= 0, "text= parameter missing");
        string encoded = raw[(textIdx + 5)..];
        string decoded = Uri.UnescapeDataString(encoded);

        Assert.Equal(original, decoded);
    }
}
