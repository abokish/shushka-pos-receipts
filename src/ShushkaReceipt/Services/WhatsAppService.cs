using System.Diagnostics;

namespace ShushkaReceipt.Services;

public static class WhatsAppService
{
    public static Uri BuildWhatsAppLink(string phoneE164, string message)
    {
        string encoded = Uri.EscapeDataString(message);
        return new Uri($"whatsapp://send?phone={phoneE164}&text={encoded}");
    }

    public static void LaunchDeepLink(Uri uri)
    {
        // AbsoluteUri preserves percent-encoding; ToString() may decode %20 → space
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }
}
