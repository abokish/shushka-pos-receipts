using ShushkaReceipt.Forms;

namespace ShushkaReceipt.Tests;

public class PhoneInputHelperTests
{
    // ── TryParsePhone — valid inputs ──────────────────────────────────────

    [Theory]
    [InlineData("052-1234567",  "972521234567")]
    [InlineData("0521234567",   "972521234567")]
    [InlineData("054-6995623",  "972546995623")]
    [InlineData("050-1234567",  "972501234567")]
    [InlineData("058-1234567",  "972581234567")]
    public void ValidPhone_ReturnsE164(string input, string expected)
    {
        bool ok = PhoneInputHelper.TryParsePhone(input, out string e164);
        Assert.True(ok);
        Assert.Equal(expected, e164);
    }

    [Fact]
    public void PhoneWithSpaces_Trimmed_Accepted()
    {
        bool ok = PhoneInputHelper.TryParsePhone("  052-1234567  ", out string e164);
        Assert.True(ok);
        Assert.Equal("972521234567", e164);
    }

    // ── TryParsePhone — invalid inputs ────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]               // too short
    [InlineData("abc-1234567")]       // letters
    [InlineData("052-123456")]        // 6 digits instead of 7
    [InlineData("972521234567")]      // already E.164 — not a local number
    public void InvalidPhone_ReturnsFalse(string input)
    {
        bool ok = PhoneInputHelper.TryParsePhone(input, out _);
        Assert.False(ok);
    }

    // ── FormatForDisplay ─────────────────────────────────────────────────

    [Fact]
    public void FormatForDisplay_E164_FormatsCorrectly()
    {
        string display = PhoneInputHelper.FormatForDisplay("972521234567");
        Assert.Equal("052-1234567", display);
    }

    [Fact]
    public void FormatForDisplay_RoundTrip()
    {
        // display → parse → display should be stable
        string original = "972521234567";
        string display  = PhoneInputHelper.FormatForDisplay(original);
        bool   ok       = PhoneInputHelper.TryParsePhone(display, out string e164);
        Assert.True(ok);
        Assert.Equal(original, e164);
    }

    [Fact]
    public void FormatForDisplay_UnknownFormat_ReturnsInputUnchanged()
    {
        // If the E.164 doesn't match the expected pattern, return as-is
        string unknown = "12345";
        Assert.Equal(unknown, PhoneInputHelper.FormatForDisplay(unknown));
    }
}
