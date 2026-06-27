using ShushkaReceipt.Services;

namespace ShushkaReceipt.Tests;

public class FileJobLoggerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _logPath;

    public FileJobLoggerTests()
    {
        _dir     = Path.Combine(Path.GetTempPath(), "ShushkaLogTest_" + Guid.NewGuid());
        _logPath = Path.Combine(_dir, "test.log");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Log_CreatesFileAndDirectory()
    {
        var logger = new FileJobLogger(_logPath);
        logger.Log("hello");
        Assert.True(File.Exists(_logPath));
    }

    [Fact]
    public void Log_AppendsMultipleEntries()
    {
        var logger = new FileJobLogger(_logPath);
        logger.Log("entry1");
        logger.Log("entry2");
        string content = File.ReadAllText(_logPath);
        Assert.Contains("entry1", content);
        Assert.Contains("entry2", content);
    }

    [Fact]
    public void Log_EntryContainsTimestamp()
    {
        var logger = new FileJobLogger(_logPath);
        logger.Log("test");
        string content = File.ReadAllText(_logPath);
        // Timestamp format: yyyy-MM-dd HH:mm:ss
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", content);
    }

    [Fact]
    public void Rotation_RotatesWhenFileTooLarge()
    {
        // Set a tiny max so the first write triggers rotation on the second
        var logger = new FileJobLogger(_logPath, maxBytes: 10);

        logger.Log("first entry — this alone is more than 10 bytes");
        Assert.True(File.Exists(_logPath));

        logger.Log("second entry — should trigger rotation");

        // After rotation, the backup must exist
        string backupPath = _logPath + ".old";
        Assert.True(File.Exists(backupPath), "backup (.old) should exist after rotation");

        // The fresh log contains only the latest entry
        string current = File.ReadAllText(_logPath);
        Assert.Contains("second entry", current);
        Assert.DoesNotContain("first entry", current);
    }

    [Fact]
    public void Rotation_BackupOverwrittenOnSecondRotation()
    {
        var logger = new FileJobLogger(_logPath, maxBytes: 10);
        string backupPath = _logPath + ".old";

        logger.Log("A — first entry, longer than 10 bytes");
        logger.Log("B — triggers first rotation");
        Assert.True(File.Exists(backupPath));
        string backup1 = File.ReadAllText(backupPath);

        logger.Log("C — longer than 10 bytes too");
        logger.Log("D — triggers second rotation, overwrites old backup");
        string backup2 = File.ReadAllText(backupPath);

        // The backup was overwritten — it now contains "C", not "A"
        Assert.DoesNotContain("A", backup2);
        Assert.Contains("C", backup2);
    }

    [Fact]
    public void Rotation_NoRotationWhenBelowLimit()
    {
        var logger = new FileJobLogger(_logPath, maxBytes: 1024 * 1024);
        logger.Log("small entry");
        logger.Log("another small entry");

        string backupPath = _logPath + ".old";
        Assert.False(File.Exists(backupPath), "no rotation should occur within size limit");
    }
}
