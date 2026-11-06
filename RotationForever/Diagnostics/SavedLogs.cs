#nullable enable
using System.Text.Json;
namespace RotationUS.Diagnostics;

internal sealed record LogClearResult(int Cleared, string[] Errors);

internal static class SavedLogs
{
    internal static LogClearResult ClearContents(string directory)
        => ClearFiles(Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.log.txt", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>());

    internal static LogClearResult ClearFiles(IEnumerable<string> paths)
    {
        int cleared = 0;
        var errors = new List<string>();
        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(path)) continue;
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("跳过链接文件");
                using var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
                file.SetLength(0);
                cleared++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                errors.Add(Path.GetFileName(path) + "：" + e.Message);
            }
        }
        return new(cleared, errors.ToArray());
    }
}

internal sealed class SavedLogStore
{
    private readonly string historyPath;
    private readonly List<string> files = new();
    internal string DefaultDirectory { get; }
    internal string LastDirectory => files.Count == 0 ? DefaultDirectory : Path.GetDirectoryName(files[^1])!;
    internal string? LoadError { get; }

    internal SavedLogStore(string baseDirectory)
    {
        DefaultDirectory = Path.Combine(baseDirectory, "captures");
        historyPath = Path.Combine(baseDirectory, "saved-log-files.json");
        if (!File.Exists(historyPath)) return;
        try
        {
            var saved = JsonSerializer.Deserialize<string[]>(File.ReadAllText(historyPath)) ?? Array.Empty<string>();
            files.AddRange(saved.Where(path => !string.IsNullOrEmpty(path) && Path.IsPathFullyQualified(path)
                && path.EndsWith(".log.txt", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFullPath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            LoadError = "无法读取已保存日志记录：" + e.Message;
        }
    }

    internal void Remember(string logPath)
    {
        string path = Path.GetFullPath(logPath);
        files.RemoveAll(old => string.Equals(old, path, StringComparison.OrdinalIgnoreCase));
        files.Add(path);
        string temporary = historyPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(files));
        File.Move(temporary, historyPath, overwrite: true);
    }

    internal LogClearResult ClearContents()
    {
        // Recover older default exports even if they predate the history file.
        var defaults = Directory.Exists(DefaultDirectory)
            ? Directory.GetFiles(DefaultDirectory, "*.log.txt", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();
        // Custom folders contribute only the exact log files exported by this app.
        return SavedLogs.ClearFiles(defaults.Concat(files));
    }
}
