using ShushkaReceipt.Config;

namespace ShushkaReceipt.Services;

public static class LocalSaveService
{
    /// <summary>
    /// Saves <paramref name="content"/> to the configured local folder.
    /// Returns the full path written, or null on failure.
    /// </summary>
    public static string? Save(string content, DocumentType docType, ShushkaConfig config)
    {
        try
        {
            string dir = config.LocalSavePath;
            if (string.IsNullOrWhiteSpace(dir)) dir = @"C:\קופה\";
            Directory.CreateDirectory(dir);

            string label = docType switch
            {
                DocumentType.Receipt  => "חשבונית",
                DocumentType.Order    => "הזמנה",
                DocumentType.Internal => "דוח",
                _                     => "קובץ",
            };

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename  = $"{timestamp}_{label}.txt";
            string path      = Path.Combine(dir, filename);

            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
