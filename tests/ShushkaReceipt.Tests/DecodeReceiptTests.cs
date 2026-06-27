using System.Text;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class DecodeReceiptTests
{
    private static readonly Encoding Cp862;

    static DecodeReceiptTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp862 = Encoding.GetEncoding(862);
    }

    [Fact]
    public void EmptyPayload_ReturnsEmpty()
    {
        string result = ReceiptDecoder.DecodeReceipt([]);
        Assert.Equal("", result.Trim());
    }

    [Fact]
    public void EscPrintMode_SkipsThreeBytes()
    {
        // ESC ! 0x00  followed by "AB" → only "AB" should survive
        byte[] data = [0x1B, 0x21, 0x00, .. Cp862.GetBytes("AB")];
        string result = ReceiptDecoder.DecodeReceipt(data);
        Assert.Equal("AB", result.Trim());
    }

    [Fact]
    public void OtherEsc_SkipsTwoBytes()
    {
        // ESC @ (init printer) followed by "XY"
        byte[] data = [0x1B, 0x40, .. Cp862.GetBytes("XY")];
        string result = ReceiptDecoder.DecodeReceipt(data);
        Assert.Equal("XY", result.Trim());
    }

    [Fact]
    public void MultipleEscCommands_AllStripped()
    {
        // ESC!0 "A" ESC@ "B" → "AB"
        byte[] data =
        [
            0x1B, 0x21, 0x08,
            .. Cp862.GetBytes("A"),
            0x1B, 0x40,
            .. Cp862.GetBytes("B"),
        ];
        string result = ReceiptDecoder.DecodeReceipt(data);
        Assert.Equal("AB", result.Trim());
    }

    [Fact]
    public void Cp862_DecodesHebrew()
    {
        // 0x99 = ש (Shin) in CP862. Verify CP862 decoding is wired up.
        byte[] data = [0x99];
        string result = ReceiptDecoder.DecodeReceipt(data);
        // BidiFixLine on a single Hebrew char reverses it (still "ש" — a single char is its own reverse)
        Assert.Contains("ש", result);
    }

    [Fact]
    public void SyntheticOrderReceipt_ContainsExpectedFields()
    {
        // Construct a properly pre-reversed order line so DecodeReceipt restores it.
        // Pre-reversed form of "מספר הזמנה 1504":
        //   tokens = ["מספר", " ", "הזמנה", " 1504"]
        //   reverse token order + reverse Hebrew runs → " 1504הנמזה רפסמ"
        // (with a leading space so the space between "הזמנה" and "1504" is preserved)
        var orderLineStream = " 1504הנמזה רפסמ\n";   // pre-reversed "מספר הזמנה 1504"
        var storeLineStream = "שיקוב עבט\n";           // pre-reversed "טבע בוקיש"

        // Add an ESC!0 at the start to exercise stripping
        byte[] data =
        [
            0x1B, 0x21, 0x00,
            .. Cp862.GetBytes(storeLineStream),
            .. Cp862.GetBytes(orderLineStream),
        ];

        string decoded = ReceiptDecoder.DecodeReceipt(data);

        Assert.Contains("טבע בוקיש", decoded);
        Assert.Contains("מספר הזמנה", decoded);
        Assert.Contains("1504", decoded);
    }
}
