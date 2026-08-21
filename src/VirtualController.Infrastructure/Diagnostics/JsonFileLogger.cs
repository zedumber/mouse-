using System.Text.Json;
using VirtualController.Core.Diagnostics;
using VirtualController.Infrastructure.Persistence;

namespace VirtualController.Infrastructure.Diagnostics;

/// <summary>
/// Escribe una entrada JSON por línea en %LOCALAPPDATA%\VirtualController\Logs. Un archivo diario
/// limita el impacto de un fallo y permite adjuntar exactamente el día de una prueba.
/// </summary>
public sealed class JsonFileLogger : IAppLogger
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public JsonFileLogger(StoragePaths paths, TimeProvider? timeProvider = null, int retentionDays = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, 1);

        _timeProvider = timeProvider ?? TimeProvider.System;
        Directory.CreateDirectory(paths.LogsDirectory);

        var now = _timeProvider.GetLocalNow();
        CurrentLogFile = Path.Combine(paths.LogsDirectory, $"virtual-controller-{now:yyyyMMdd}.jsonl");
        DeleteExpiredLogs(paths.LogsDirectory, now.Date.AddDays(-retentionDays));
    }

    public string CurrentLogFile { get; }

    public void Information(string eventName, string message) => Write("information", eventName, message, null);

    public void Warning(string eventName, string message) => Write("warning", eventName, message, null);

    public void Error(string eventName, string message, Exception? exception = null) =>
        Write("error", eventName, message, exception);

    private void Write(string level, string eventName, string message, Exception? exception)
    {
        var entry = new LogEntry(
            _timeProvider.GetUtcNow(),
            level,
            eventName,
            message,
            exception?.ToString());
        var json = JsonSerializer.Serialize(entry, _jsonOptions);

        lock (_gate)
        {
            try
            {
                File.AppendAllText(CurrentLogFile, json + Environment.NewLine);
            }
            catch (IOException)
            {
                // El diagnóstico nunca debe tumbar el bucle de emulación (disco lleno/bloqueado).
            }
            catch (UnauthorizedAccessException)
            {
                // Puede ocurrir si una política corporativa cambia permisos durante la ejecución.
            }
        }
    }

    private static void DeleteExpiredLogs(string directory, DateTimeOffset cutoff)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "virtual-controller-*.jsonl"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff.UtcDateTime)
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // El log puede estar abierto por otra instancia; no se arriesga el arranque por limpieza.
            }
            catch (UnauthorizedAccessException)
            {
                // Igual que arriba: registrar tiene prioridad sobre borrar un archivo antiguo.
            }
        }
    }

    private sealed record LogEntry(
        DateTimeOffset Timestamp,
        string Level,
        string Event,
        string Message,
        string? Exception);
}
