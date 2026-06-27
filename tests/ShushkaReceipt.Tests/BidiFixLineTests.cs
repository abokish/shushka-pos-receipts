using System.Text;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class BidiFixLineTests
{
    static BidiFixLineTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", ReceiptDecoder.BidiFixLine(""));
    }

    [Fact]
    public void PureLatinLine_Unchanged()
    {
        Assert.Equal("hello world", ReceiptDecoder.BidiFixLine("hello world"));
    }

    [Fact]
    public void PureDigitLine_Unchanged()
    {
        Assert.Equal("12345", ReceiptDecoder.BidiFixLine("12345"));
    }

    [Fact]
    public void PureHebrewLine_ReversedChars()
    {
        // "שלום" visually pre-reversed in stream as "םולש" → BidiFixLine reverses back → "שלום"
        string reversed = "םולש";
        string expected = "שלום";
        Assert.Equal(expected, ReceiptDecoder.BidiFixLine(reversed));
    }

    [Fact]
    public void PhoneNumber_NotReversed()
    {
        // Digits are non-Hebrew: token content is preserved, only token order changes.
        // A single non-Hebrew token has no order to reverse, so it stays as-is.
        string input = "054-6995623";
        Assert.Equal("054-6995623", ReceiptDecoder.BidiFixLine(input));
    }

    [Fact]
    public void MixedHebrewAndDigits_DigitsPreserved()
    {
        // Stream line " :ןופלט 054-6995623" represents "054-6995623 :טלפון" in visual RTL order.
        // Token analysis: [" :", "ןופלט", " 054-6995623"]
        // After bidi fix (reversed token order, Hebrew chars reversed):
        //   [" 054-6995623", "טלפון", " :"]  →  "054-6995623טלפון :"
        // Key requirement: phone digits must appear in CORRECT order, not reversed.
        string input = " :ןופלט 054-6995623";
        string result = ReceiptDecoder.BidiFixLine(input);

        // Phone digits appear in the correct order
        Assert.Contains("054-6995623", result);

        // Hebrew label appears (colon is a separate token, so check label without colon)
        Assert.Contains("טלפון", result);

        // Verify the phone digits are not reversed (check for the specific digit sequence)
        int idx = result.IndexOf("054-6995623", StringComparison.Ordinal);
        Assert.True(idx >= 0, "Phone should appear in correct order");
    }

    [Fact]
    public void MultiRunLine_TokenOrderReversed()
    {
        // Pure Hebrew token "עבט" followed by space and Hebrew token "שיקוב"
        // After bidi fix these should become "בוקיש עבט" → "טבע בוקיש"
        string input = "שיקוב עבט"; // pre-reversed form of "טבע בוקיש"
        string result = ReceiptDecoder.BidiFixLine(input);
        Assert.Equal("טבע בוקיש", result);
    }

    [Fact]
    public void WhitespaceOnlyLine_TrimmedToEmpty()
    {
        Assert.Equal("", ReceiptDecoder.BidiFixLine("   "));
    }

    [Fact]
    public void SeparatorLine_Unchanged()
    {
        string sep = "——————————————————————";
        Assert.Equal(sep, ReceiptDecoder.BidiFixLine(sep));
    }

    [Fact]
    public void BidiFixIsOwnInverse()
    {
        // Applying BidiFixLine twice on a trimmed string returns the original.
        // (The algorithm is its own inverse because it reverses both token order and Hebrew chars.)
        string original = "מספר הזמנה";
        string once  = ReceiptDecoder.BidiFixLine(original);
        string twice = ReceiptDecoder.BidiFixLine(once);
        Assert.Equal(original, twice);
    }
}
