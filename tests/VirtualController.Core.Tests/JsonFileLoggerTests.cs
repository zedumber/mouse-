using System.Text.Json;
using VirtualController.Infrastructure.Diagnostics;
using VirtualController.Infrastructure.Persistence;

namespace VirtualController.Core.Tests;

public sealed class JsonFileLoggerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vc-logger-{Guid.NewGuid():N}");

    [Fact]
    public void WritesStructuredJsonLinesAndExceptionDetails()
    {
        var logger = new JsonFileLogger(new StoragePaths(_root));

        logger.Information("startup", "Aplicación iniciada");
        logger.Error("backend", "No conectado", new InvalidOperationException("driver"));

        var lines = File.ReadAllLines(logger.CurrentLogFile);
        Assert.Equal(2, lines.Length);

        using var first = JsonDocument.Parse(lines[0]);
        Assert.Equal("information", first.RootElement.GetProperty("level").GetString());
        Assert.Equal("startup", first.RootElement.GetProperty("event").GetString());

        using var second = JsonDocument.Parse(lines[1]);
        Assert.Contains("InvalidOperationException", second.RootElement.GetProperty("exception").GetString());
    }

    [Fact]
    public void RemovesExpiredDailyLogsButKeepsRecentOnes()
    {
        var paths = new StoragePaths(_root);
        Directory.CreateDirectory(paths.LogsDirectory);
        var old = Path.Combine(paths.LogsDirectory, "virtual-controller-20000101.jsonl");
        var recent = Path.Combine(paths.LogsDirectory, "virtual-controller-recent.jsonl");
        File.WriteAllText(old, "old");
        File.WriteAllText(recent, "recent");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30));

        _ = new JsonFileLogger(paths, retentionDays: 14);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
