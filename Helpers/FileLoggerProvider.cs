using System.Globalization;
using System.Text;

namespace SQPortal.Helpers;

/// <summary>
/// Minimal file logger: one file per day (sqportal-yyyy-MM-dd.log, UTC) in the
/// configured folder, keeping the last <c>retainDays</c> days. Dependency-free
/// so the offline servers need no extra packages. Writes are lock-serialized;
/// a logging failure never takes the app down.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const string FilePrefix = "sqportal-";
    private const string FileSuffix = ".log";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly string _folder;
    private readonly LogLevel _minimumLevel;
    private readonly int _retainDays;
    private readonly object _sync = new();

    private DateOnly _lastCleanupDate;

    /// <param name="retainDays">Days of log files to keep; 0 keeps them forever.</param>
    public FileLoggerProvider(string folder, LogLevel minimumLevel, int retainDays)
    {
        _folder = folder;
        _minimumLevel = minimumLevel;
        _retainDays = retainDays;
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
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                RemoveExpiredFiles(today);

                var path = Path.Combine(_folder, $"{FilePrefix}{today.ToString(DateFormat)}{FileSuffix}");
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

    /// <summary>
    /// Deletes log files older than the retention window, once per day. Only
    /// files this provider named are considered, so anything else in the
    /// folder is left alone. Called from inside the write lock.
    /// </summary>
    private void RemoveExpiredFiles(DateOnly today)
    {
        if (_retainDays <= 0 || _lastCleanupDate == today) return;
        _lastCleanupDate = today;

        var cutoff = today.AddDays(-_retainDays);

        try
        {
            // Materialized first: deleting during a lazy enumeration is fragile.
            foreach (var path in Directory.GetFiles(_folder, $"{FilePrefix}*{FileSuffix}"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.Length != FilePrefix.Length + DateFormat.Length) continue;

                var stamp = name[FilePrefix.Length..];
                if (!DateOnly.TryParseExact(stamp, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                {
                    continue;
                }

                if (fileDate < cutoff)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
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
