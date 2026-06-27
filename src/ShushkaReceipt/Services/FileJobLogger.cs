namespace ShushkaReceipt.Services;

public sealed class FileJobLogger
{
    private readonly string _path;
    private readonly string _backupPath;
    private readonly long   _maxBytes;
    private readonly object _lock = new();

    public FileJobLogger(string path, long maxBytes = 5 * 1024 * 1024 /* 5 MB */)
    {
        _path       = path;
        _backupPath = path + ".old";
        _maxBytes   = maxBytes;

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public void Log(string entry)
    {
        string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} | {entry}";
        lock (_lock)
        {
            RotateIfNeeded();
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_path)) return;
        if (new FileInfo(_path).Length < _maxBytes) return;

        // Overwrite the single backup and start a fresh log
        if (File.Exists(_backupPath))
            File.Delete(_backupPath);
        File.Move(_path, _backupPath);
    }
}
