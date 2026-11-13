using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TechStore.Logging
{
    /// <summary>
    /// Opciones de configuración para el proveedor nativo de logs en archivo diario.
    /// </summary>
    public sealed class FileLoggerOptions
    {
        public string FilePrefix { get; set; } = "techstore-";
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

        public IDictionary<string, LogLevel> CategoryMinimumLevels { get; } =
            new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase)
            {
                ["TechStore"] = LogLevel.Information,
                ["Microsoft.Hosting.Lifetime"] = LogLevel.Information,
                ["Microsoft.AspNetCore"] = LogLevel.Warning,
                ["Microsoft.EntityFrameworkCore"] = LogLevel.Warning,
                ["System.Net.Http"] = LogLevel.Warning
            };

        internal LogLevel ResolveMinimumLevel(string categoryName)
        {
            var bestMatchLength = -1;
            var resolvedLevel = MinimumLevel;

            foreach (var entry in CategoryMinimumLevels)
            {
                if (IsCategoryMatch(categoryName, entry.Key) && entry.Key.Length > bestMatchLength)
                {
                    bestMatchLength = entry.Key.Length;
                    resolvedLevel = entry.Value;
                }
            }

            return resolvedLevel;
        }

        private static bool IsCategoryMatch(string categoryName, string prefix)
        {
            return categoryName.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || categoryName.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Proveedor nativo de <see cref="ILoggerProvider"/> que escribe logs diarios en formato
    /// <c>techstore-yyyy-MM-dd.log</c> de manera segura para múltiples hilos sin retener bloqueos
    /// abiertos sobre el archivo entre escrituras.
    /// </summary>
    [ProviderAlias("File")]
    public sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

        private readonly string _logDirectory;
        private readonly FileLoggerOptions _options;
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
        private readonly object _writeLock = new();

        private bool _disposed;

        internal IExternalScopeProvider? ScopeProvider { get; private set; }
        internal bool IsDisposed => Volatile.Read(ref _disposed);

        public FileLoggerProvider(string logDirectory, FileLoggerOptions? options = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

            _logDirectory = logDirectory;
            _options = options ?? new FileLoggerOptions();

            Directory.CreateDirectory(_logDirectory);
        }

        public ILogger CreateLogger(string categoryName)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

            return _loggers.GetOrAdd(categoryName, name =>
            {
                var minimumLevel = _options.ResolveMinimumLevel(name);
                return new FileLogger(name, this, minimumLevel);
            });
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            ScopeProvider = scopeProvider;
        }

        internal void WriteLogEntry(
            DateTimeOffset timestamp,
            LogLevel logLevel,
            EventId eventId,
            string categoryName,
            string message,
            Exception? exception)
        {
            if (IsDisposed)
            {
                return;
            }

            var formattedLine = FormatEntry(timestamp, logLevel, eventId, categoryName, message, exception);
            var entryDate = DateOnly.FromDateTime(timestamp.LocalDateTime);
            var filePath = Path.Combine(_logDirectory, $"{_options.FilePrefix}{entryDate:yyyy-MM-dd}.log");

            lock (_writeLock)
            {
                if (_disposed)
                {
                    return;
                }

                Directory.CreateDirectory(_logDirectory);

                using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, Utf8WithoutBom);
                writer.WriteLine(formattedLine);
            }
        }

        public void Dispose()
        {
            lock (_writeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _loggers.Clear();
        }

        private static string FormatEntry(
            DateTimeOffset timestamp,
            LogLevel logLevel,
            EventId eventId,
            string categoryName,
            string message,
            Exception? exception)
        {
            var levelTag = GetLevelTag(logLevel);
            var eventTag = string.IsNullOrEmpty(eventId.Name)
                ? eventId.Id.ToString()
                : $"{eventId.Id}:{eventId.Name}";

            var builder = new StringBuilder(256);
            builder.Append(timestamp.ToString("O"));
            builder.Append(" [").Append(levelTag).Append(']');
            builder.Append(" [").Append(eventTag).Append(']');
            builder.Append(' ').Append(categoryName);
            builder.Append(" - ").Append(message);

            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            return builder.ToString();
        }

        private static string GetLevelTag(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "FAIL",
            LogLevel.Critical => "CRIT",
            _ => "NONE"
        };
    }
}
