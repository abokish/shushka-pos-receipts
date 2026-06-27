using System.Net;
using System.Net.Sockets;
using System.Text;
using ShushkaReceipt.Config;
using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class TcpLoopbackTests
{
    private static readonly Encoding Cp862;

    static TcpLoopbackTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp862 = Encoding.GetEncoding(862);
    }

    private static byte[] BuildSyntheticJob()
    {
        // Construct a properly pre-reversed ESC/POS byte stream.
        // Pre-reversed form of each line (BidiFixLine applied to the desired output):
        //   "טבע בוקיש"         → "שיקוב עבט"
        //   "מספר הזמנה 1504"  → " 1504הנמזה רפסמ"  (leading space preserves inter-token space)
        //   "לתשלום 94.00"      → " 94.00 םולשתל"
        var lines = new[]
        {
            "שיקוב עבט",                  // pre-reversed store name
            " 1504הנמזה רפסמ",            // pre-reversed order number line
            " 42.00*ולוק 2 אוקפ הדש תות001", // pre-reversed item line
            " 42.00מולשתל",              // pre-reversed total line
        };

        var sb = new StringBuilder();
        foreach (var l in lines)
            sb.Append(l).Append('\n');

        byte[] text = Cp862.GetBytes(sb.ToString());

        // Wrap with a couple of ESC commands to verify they're stripped
        byte[] payload =
        [
            0x1B, 0x21, 0x08,   // ESC ! n  (bold on) — must be stripped
            .. text[..5],
            0x1B, 0x40,          // ESC @   (init)    — must be stripped
            .. text[5..],
        ];
        return payload;
    }

    [Fact]
    public void Pipeline_DecodesAndBuildsMessage()
    {
        var config = new ShushkaConfig();
        byte[] job = BuildSyntheticJob();

        string decoded = ReceiptDecoder.DecodeReceipt(job);
        string message = ReceiptParser.BuildMessage(decoded, config);

        // Store name is decoded from pre-reversed form
        Assert.Contains("טבע", decoded);
        // Parser picks up the price and formats it
        Assert.Contains("₪42.00", message);
        Assert.Contains(config.MessageSeparator, message);
        Assert.Contains(config.MessageClosing, message);
    }

    [Fact]
    public void Pipeline_WhatsAppLinkIsValid()
    {
        var config = new ShushkaConfig();
        byte[] job = BuildSyntheticJob();

        string decoded = ReceiptDecoder.DecodeReceipt(job);
        string message = ReceiptParser.BuildMessage(decoded, config);
        var    link    = WhatsAppService.BuildWhatsAppLink("972501234567", message);

        Assert.Equal("whatsapp", link.Scheme);
        Assert.Contains("phone=972501234567", link.AbsoluteUri);
    }

    /// <summary>
    /// True socket round-trip: start a listener on an ephemeral port,
    /// send pre-reversed Hebrew bytes, verify the decoder restores them.
    /// </summary>
    [Fact]
    public async Task Socket_SendBytes_DecoderReceivesAndRestores()
    {
        // "שלום" visually pre-reversed in stream as "םולש"
        byte[] sent = Cp862.GetBytes("םולש\n");

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var ms     = new MemoryStream();
            await client.GetStream().CopyToAsync(ms);
            return ms.ToArray();
        });

        using (var sender = new TcpClient())
        {
            await sender.ConnectAsync(IPAddress.Loopback, port);
            await sender.GetStream().WriteAsync(sent);
        }

        byte[] received = await serverTask;
        listener.Stop();

        Assert.Equal(sent, received);

        // Decoder must restore "שלום" from the pre-reversed bytes
        string decoded = ReceiptDecoder.DecodeReceipt(received);
        Assert.Contains("שלום", decoded);
    }
}
