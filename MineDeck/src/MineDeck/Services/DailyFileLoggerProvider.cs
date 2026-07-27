using System.Collections.Concurrent;

namespace MineDeck.Services;

public sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.Ordinal);

    public DailyFileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, category => new DailyFileLogger(this, category));
    public void Dispose() => _loggers.Clear();

    private void Write(LogLevel level, string category, EventId eventId, string message, Exception? exception)
    {
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O}\t{level}\t{category}\t{eventId.Id}\t{SingleLine(message)}";
            if (exception is not null) line += $"\t{SingleLine(exception.ToString())}";
            lock (_writeLock)
                File.AppendAllText(Path.Combine(_directory, $"minedeck-{DateTimeOffset.UtcNow:yyyy-MM-dd}.log"), line + Environment.NewLine);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Logging must never take down the family server supervisor.
        }
    }

    private static string SingleLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ');

    private sealed class DailyFileLogger : ILogger
    {
        private readonly DailyFileLoggerProvider _provider;
        private readonly string _category;
        public DailyFileLogger(DailyFileLoggerProvider provider, string category) { _provider = provider; _category = category; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) _provider.Write(logLevel, _category, eventId, formatter(state, exception), exception);
        }
    }
}
