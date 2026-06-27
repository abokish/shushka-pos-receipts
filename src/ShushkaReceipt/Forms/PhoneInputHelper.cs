using System.Text.RegularExpressions;

namespace ShushkaReceipt.Forms;

/// <summary>
/// Stateless phone-input helpers shared between DispatchForm and its unit tests.
/// </summary>
public static class PhoneInputHelper
{
    // Accepts: 0521234567, 052-1234567, 0521234567, 521234567 (9 digits without leading 0)
    private static readonly Regex Pattern = new(@"^\d{9,10}$|^0\d{1,2}-?\d{7}$");

    /// <summary>
    /// Validates user-entered phone text and converts it to E.164 (972XXXXXXXXX).
    /// Returns true if valid, and sets <paramref name="e164"/>; otherwise false.
    /// </summary>
    public static bool TryParsePhone(string input, out string e164)
    {
        e164 = "";
        string trimmed = input.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        if (!Pattern.IsMatch(trimmed)) return false;

        string digits = Regex.Replace(trimmed, @"\D", "");
        if (digits.StartsWith("0")) digits = digits[1..];
        if (digits.Length < 8) return false;

        e164 = "972" + digits;
        return true;
    }

    /// <summary>
    /// Converts E.164 (972XXXXXXXXX) back to local display format (0XX-XXXXXXX).
    /// Used to pre-fill the phone box when a phone is already on the customer record.
    /// </summary>
    public static string FormatForDisplay(string e164)
    {
        if (e164.StartsWith("972") && e164.Length >= 12)
        {
            string local = "0" + e164[3..];
            return local.Length == 10
                ? local[..3] + "-" + local[3..]
                : local;
        }
        return e164;
    }
}
