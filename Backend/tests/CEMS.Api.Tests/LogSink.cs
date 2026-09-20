using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CEMS.Api.Tests;

public sealed record LogLine(LogLevel Level, string Category, string Message, Exception? Exception);

/// <summary>An in-memory logger provider that keeps every line at or above Information (what the app would ship).</summary>
public sealed class LogSink : ILoggerProvider
{
    private readonly ConcurrentQueue<LogLine> _lines = new();

    public IReadOnlyList<LogLine> Lines => _lines.ToArray();

    public ILogger CreateLogger(string categoryName) => new SinkLogger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class SinkLogger(string category, ConcurrentQueue<LogLine> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                lines.Enqueue(new LogLine(logLevel, category, formatter(state, exception), exception));
            }
        }
    }
}
