using System.Text;
using System.Text.RegularExpressions;
using ShushkaReceipt.Config;

namespace ShushkaReceipt.Services;

public enum DocumentType { Receipt, Order, Internal }

public static class ReceiptParser
{
    /// <summary>
    /// Classifies a decoded print job.
    /// חשבונית עסקה → Receipt; מספר הזמנה → Order; anything else → Internal.
    /// </summary>
    public static DocumentType GetDocumentType(string decoded) =>
        decoded.Contains("חשבונית עסקה") ? DocumentType.Receipt :
        decoded.Contains("מספר הזמנה")   ? DocumentType.Order   :
                                            DocumentType.Internal;

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
            int labelIdx = lines[i].IndexOf(config.CustomerPhoneLabel, StringComparison.Ordinal);
            if (labelIdx < 0) continue;

            // Take everything after the label (e.g. "543090412" or "052-1234567" or "")
            string afterLabel = lines[i][(labelIdx + config.CustomerPhoneLabel.Length)..].Trim();
            if (string.IsNullOrEmpty(afterLabel)) return null;

            return ToE164(afterLabel);
        }

        return null;
    }

    // Normalises any Israeli phone representation to E.164 (972XXXXXXXXX).
    // Handles: leading 0 (052-1234567), no leading 0 (543090412), or 972-prefixed.
    // Returns null when the result fails the Israeli mobile sanity check (9-digit NSN starting with 5).
    private static string? ToE164(string raw)
    {
        string digits = Regex.Replace(raw, @"\D", "");

        if (digits.StartsWith("972"))
            digits = digits[3..];
        else if (digits.StartsWith("0"))
            digits = digits[1..];

        // Israeli mobile NSN: 9 digits, starts with 5
        if (digits.Length != 9 || !digits.StartsWith('5'))
            return null;

        return "972" + digits;
    }

    public static string BuildMessage(string decoded, ShushkaConfig config)
    {
        var lines = decoded.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        string storeName = lines.Length > 0 ? lines[0] : "";

        // Support order (מספר הזמנה) and transaction invoice (חשבונית עסקה)
        string? orderNum   = ExtractGroup(lines, @"מספר הזמנה\s+(\d+)", 1);
        string? invoiceNum = ExtractGroup(lines, @"חשבונית עסקה\s+([\d/]+)", 1);

        // תאריך and שעה may have a colon separator on some document types
        string date  = ExtractGroup(lines, @"תאריך[:\s]+([\d/]+)",  1) ?? "";
        string time  = ExtractGroup(lines, @"שעה[:\s]*([\d:]+)",    1) ?? "";
        string total = ExtractGroup(lines, @"לתשלום\s+(\d+\.\d{2})", 1) ?? "";

        var items = ExtractItems(lines);

        var sb = new StringBuilder();
        sb.AppendLine(storeName);

        if (orderNum is not null)
            sb.AppendLine($"הזמנה {orderNum}");
        else if (invoiceNum is not null)
            sb.AppendLine($"חשבונית עסקה {invoiceNum}");

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

    // Extracts items between the column-header line (containing "קוד") and the
    // לתשלום line.  Falls back to scanning all lines when no header is present
    // (e.g. synthetic test data or order receipts without an explicit header).
    private static List<(string desc, string price)> ExtractItems(string[] lines)
    {
        int headerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            // Column header contains the code column label
            if (lines[i].Contains("קוד") && (lines[i].Contains("סכום") || lines[i].Contains("תיאור")))
            {
                headerIdx = i;
                break;
            }
        }

        int totalIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"לתשלום"))
            {
                totalIdx = i;
                break;
            }
        }

        var items = new List<(string, string)>();

        if (headerIdx >= 0 && totalIdx > headerIdx)
        {
            for (int i = headerIdx + 1; i < totalIdx; i++)
            {
                var item = TryParseItemLine(lines[i]);
                if (item.HasValue) items.Add(item.Value);
            }
        }
        else
        {
            // Fallback: scan all lines (no column header found)
            foreach (var line in lines)
            {
                var item = TryParseItemLine(line);
                if (item.HasValue) items.Add(item.Value);
            }
        }

        return items;
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

    // Parses a single item line into (description, price).
    //
    // Item lines start with a numeric code (e.g. "22564 כרוב 20.83" or "001תות שדה 42.00*").
    // Weight/unit sub-lines start with a decimal (e.g. "2.340 ק\"ג X 8.90") and are rejected.
    // The asterisk (*) is a VAT marker — it may appear before or after the price; strip it.
    // Short codes (1-2 digits) glue to the description and are not stripped (known edge case).
    private static (string desc, string price)? TryParseItemLine(string line)
    {
        string trimmed = line.TrimStart();

        // Must start with digit(s) followed by a non-digit, non-dot character.
        // Rejects weight sub-lines like "2.340 ק\"ג X 8.90" (digit then dot).
        if (!Regex.IsMatch(trimmed, @"^\d+[^\d.]")) return null;

        // The item price is the last dd.dd pattern on the line
        var priceMatches = Regex.Matches(trimmed, @"\d+\.\d{2}");
        if (priceMatches.Count == 0) return null;

        var lastPrice = priceMatches[^1];
        string price = lastPrice.Value;

        // Everything before the price; trim trailing VAT marker and spaces
        string beforePrice = trimmed[..lastPrice.Index].TrimEnd('*', ' ');

        // Strip leading item code (3+ digits then optional space).
        // Short codes (1-2 digits) are not stripped — they glue to the description.
        string rest = Regex.Replace(beforePrice.TrimStart(), @"^\d{3,}\s*", "").Trim();

        // Remove any remaining VAT markers embedded in the description
        rest = rest.Replace("*", "").Trim();

        if (string.IsNullOrEmpty(rest)) return null;

        return (rest, price);
    }
}
