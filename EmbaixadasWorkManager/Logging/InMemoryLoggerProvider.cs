using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Logging;

namespace EmbaixadasWorkManager.Logging;

/// <summary>
/// Provider de logger personalizado para capturar logs em memória
/// </summary>
public class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ILogService _logService;

    public InMemoryLoggerProvider(ILogService logService)
    {
        _logService = logService;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new InMemoryLogger(categoryName, _logService);
    }

    public void Dispose()
    {
        // Não há recursos para liberar
    }
}

/// <summary>
/// Logger personalizado que captura logs em memória
/// </summary>
public class InMemoryLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ILogService _logService;

    public InMemoryLogger(string categoryName, ILogService logService)
    {
        _categoryName = categoryName;
        _logService = logService;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Debug; // Capturar todos os níveis incluindo Debug
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        try
        {
            var message = formatter(state, exception);
            var level = logLevel.ToString();

            var logEntry = new LogEntry
            {
                Level = level,
                Category = _categoryName,
                Message = message,
                Exception = exception?.ToString(),
                Properties = new Dictionary<string, object>
                {
                    ["EventId"] = eventId.Id,
                    ["EventName"] = eventId.Name ?? "",
                    ["State"] = state?.ToString() ?? ""
                }
            };

            _logService.AddLog(logEntry);
        }
        catch
        {
            // Ignorar erros na captura de logs para evitar loops infinitos
        }
    }
}

