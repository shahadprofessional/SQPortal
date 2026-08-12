using System.Text;

namespace SQPortal.Helpers;

/// <summary>
/// Minimal file logger: one file per day (sqportal-yyyy-MM-dd.log, UTC) in the
/// configured folder. Dependency-free so the offline servers need no extra
/// packages. Writes are lock-serialized; a logging failure never takes the
/// app down.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _folder;
    private readonly LogLevel _minimumLevel;
    private readonly object _sync = new();

    public FileLoggerProvider(string folder, LogLevel minimumLevel)
    {
        _folder = folder;
        _minimumLevel = minimumLevel;
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string text)
    {
        try
        {
            lock (_sync)
            {
                var path = Path.Combine(_folder, $"sqportal-{DateTime.UtcNow:yyyy-MM-dd}.log");
                File.AppendAllText(path, text, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && logLevel >= _provider._minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var sb = new StringBuilder()
                .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(logLevel).Append("] ")
                .Append(_category)
                .Append(" — ")
                .AppendLine(formatter(state, exception));

            if (exception != null)
            {
                sb.AppendLine(exception.ToString());
            }

            _provider.Write(sb.ToString());
        }
    }
}
