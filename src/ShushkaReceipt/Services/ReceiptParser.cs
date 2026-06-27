using System.Text;
using System.Text.RegularExpressions;
using ShushkaReceipt.Config;

namespace ShushkaReceipt.Services;

public static class ReceiptParser
{
    // PLACEHOLDER — to be finalized after a real receipt-with-phone is captured.
    // Must anchor on the customer-block טלפון: label (after מספר לקוח),
    // NOT the store header phone (054-6995623).
    public static string? ExtractCustomerPhone(string decoded, ShushkaConfig config)
    {
        var lines = decoded.Split('\n');

        int customerBlockStart = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(config.CustomerBlockAnchor))
            {
                customerBlockStart = i;
                break;
            }
        }

        if (customerBlockStart < 0) return null;

        for (int i = customerBlockStart; i < lines.Length; i++)
        {
            if (!lines[i].Contains(config.CustomerPhoneLabel)) continue;

            var m = Regex.Match(lines[i], config.PhoneRegex);
            if (m.Success) return ToE164(m.Value);
        }

        return null;
    }

    private static string ToE164(string phone)
    {
        string digits = phone.TrimStart('0').Replace("-", "");
        return "972" + digits;
    }

    public static string BuildMessage(string decoded, ShushkaConfig config)
    {
        var lines = decoded.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        string storeName = lines.Length > 0 ? lines[0] : "";
        string orderNum  = ExtractGroup(lines, @"מספר הזמנה\s+(\d+)", 1) ?? "";
        string date      = ExtractGroup(lines, @"תאריך\s+([\d/]+)", 1) ?? "";
        string time      = ExtractGroup(lines, @"שעה\s*([\d:]+)", 1) ?? "";

        var items = lines
            .Select(TryParseItemLine)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        string total = ExtractGroup(lines, @"לתשלום\s+(\d+\.\d{2})", 1) ?? "";

        var sb = new StringBuilder();
        sb.AppendLine(storeName);

        if (!string.IsNullOrEmpty(orderNum))
            sb.AppendLine($"הזמנה {orderNum}");

        string dateTime = $"{date}  {time}".Trim();
        if (!string.IsNullOrEmpty(dateTime))
            sb.AppendLine(dateTime);

        sb.AppendLine(config.MessageSeparator);

        foreach (var (desc, price) in items)
            sb.AppendLine($"{desc} — ₪{price}");

        sb.AppendLine(config.MessageSeparator);

        if (!string.IsNullOrEmpty(total))
            sb.AppendLine($"סה\"כ: ₪{total}");

        sb.Append(config.MessageClosing);

        return sb.ToString();
    }

    private static string? ExtractGroup(string[] lines, string pattern, int group)
    {
        foreach (var line in lines)
        {
            var m = Regex.Match(line, pattern);
            if (m.Success) return m.Groups[group].Value;
        }
        return null;
    }

    private static (string desc, string price)? TryParseItemLine(string line)
    {
        var priceMatch = Regex.Match(line, @"(\d+\.\d{2})\s*\*");
        if (!priceMatch.Success) return null;

        string price = priceMatch.Groups[1].Value;
        string rest  = line[..priceMatch.Index].Trim();

        // Strip leading item code (3–6 digits). Short codes (1–2 digits) glue to the
        // description — edge case tracked in the open items, to fix per document type.
        rest = Regex.Replace(rest, @"^\d{3,6}\s*", "").Trim();

        if (string.IsNullOrEmpty(rest)) return null;

        return (rest, price);
    }
}
