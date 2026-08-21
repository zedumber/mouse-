namespace VirtualController.Core.Diagnostics;

/// <summary>
/// Registro persistente de eventos operativos. La interfaz vive en Core para que la aplicación no
/// dependa de cómo se guardan los logs y para que los ViewModels sigan siendo comprobables.
/// </summary>
public interface IAppLogger
{
    string CurrentLogFile { get; }

    void Information(string eventName, string message);

    void Warning(string eventName, string message);

    void Error(string eventName, string message, Exception? exception = null);
}

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    private NullAppLogger()
    {
    }

    public string CurrentLogFile => string.Empty;

    public void Information(string eventName, string message)
    {
    }

    public void Warning(string eventName, string message)
    {
    }

    public void Error(string eventName, string message, Exception? exception = null)
    {
    }
}
