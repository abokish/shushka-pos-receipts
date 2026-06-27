using System.Text;

namespace ShushkaReceipt.Services;

public static class ReceiptDecoder
{
    public static string DecodeReceipt(byte[] data)
    {
        var stripped = StripEscPos(data);
        var cp862 = Encoding.GetEncoding(862);
        var text = cp862.GetString(stripped);

        return string.Join('\n',
            text.Split('\n').Select(l => BidiFixLine(l.TrimEnd('\r'))));
    }

    private static byte[] StripEscPos(byte[] data)
    {
        var result = new List<byte>(data.Length);
        int i = 0;
        while (i < data.Length)
        {
            byte b = data[i];

            if (b == 0x1B && i + 1 < data.Length && data[i + 1] == 0x21)
            {
                i += 3; // ESC ! n  (select print mode)
                continue;
            }

            if (b == 0x1B)
            {
                i += 2; // other ESC x
                continue;
            }

            result.Add(b);
            i++;
        }
        return [.. result];
    }

    // Exposed for unit testing
    public static string BidiFixLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;

        var tokens = TokenizeBidi(line);
        var sb = new StringBuilder(line.Length);

        foreach (var token in tokens.AsEnumerable().Reverse())
        {
            if (token.Length > 0 && IsHebrew(token[0]))
                sb.Append(new string(token.Reverse().ToArray()));
            else
                sb.Append(token);
        }

        return sb.ToString().Trim();
    }

    private static List<string> TokenizeBidi(string line)
    {
        var tokens = new List<string>();
        if (line.Length == 0) return tokens;

        var current = new StringBuilder();
        bool inHebrew = IsHebrew(line[0]);

        foreach (char c in line)
        {
            bool hebrew = IsHebrew(c);
            if (hebrew != inHebrew)
            {
                tokens.Add(current.ToString());
                current.Clear();
                inHebrew = hebrew;
            }
            current.Append(c);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }

    private static bool IsHebrew(char c) => c >= '֐' && c <= '׿';
}
