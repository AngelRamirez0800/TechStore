using Microsoft.Extensions.Logging;

namespace TechStore.Logging
{
    /// <summary>
    /// Implementación nativa de <see cref="ILogger"/> que delega la escritura serializada a <see cref="FileLoggerProvider"/>.
    /// </summary>
    internal sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly FileLoggerProvider _provider;
        private readonly LogLevel _minimumLevel;

        public FileLogger(string categoryName, FileLoggerProvider provider, LogLevel minimumLevel)
        {
            _categoryName = categoryName;
            _provider = provider;
            _minimumLevel = minimumLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _provider.ScopeProvider?.Push(state) ?? NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None
                && !_provider.IsDisposed
                && logLevel >= _minimumLevel;
        }

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

            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);

            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            _provider.WriteLogEntry(
                DateTimeOffset.Now,
                logLevel,
                eventId,
                _categoryName,
                message,
                exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
