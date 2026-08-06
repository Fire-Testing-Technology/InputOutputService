namespace InputOutput.ModbusMaster.Api.Services;

/// <summary>Feeds <see cref="DiagnosticLogService"/> from the standard logging pipeline.</summary>
public sealed class DiagnosticLoggerProvider(DiagnosticLogService store) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DiagnosticLogger(categoryName, store);

    public void Dispose()
    {
    }

    private sealed class DiagnosticLogger(string categoryName, DiagnosticLogService store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            // Keep noise down: only our assemblies + warnings from everything else.
            var ours = categoryName.StartsWith("InputOutput.", StringComparison.Ordinal);
            if (!ours && logLevel < LogLevel.Warning)
            {
                return;
            }

            store.Add(new DiagnosticLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = ShortCategory(categoryName),
                Message = formatter(state, exception),
                Exception = exception?.ToString()
            });
        }

        private static string ShortCategory(string category)
        {
            var last = category.LastIndexOf('.');
            return last < 0 ? category : category[(last + 1)..];
        }
    }
}
