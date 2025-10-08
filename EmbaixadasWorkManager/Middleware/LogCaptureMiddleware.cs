using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using System.Text.Json;

namespace EmbaixadasWorkManager.Middleware;

/// <summary>
/// Middleware para capturar logs do sistema e armazená-los em memória
/// </summary>
public class LogCaptureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogService _logService;
    private readonly ILogger<LogCaptureMiddleware> _logger;

    public LogCaptureMiddleware(RequestDelegate next, ILogService logService, ILogger<LogCaptureMiddleware> logger)
    {
        _next = next;
        _logService = logService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Capturar logs de requisição
            CaptureRequestLog(context, startTime);

            await _next(context);

            // Capturar logs de resposta
            CaptureResponseLog(context, startTime);
        }
        catch (Exception ex)
        {
            // Capturar logs de erro
            CaptureErrorLog(context, ex, startTime);
            throw;
        }
    }

    private void CaptureRequestLog(HttpContext context, DateTime startTime)
    {
        try
        {
            var logEntry = new LogEntry
            {
                Level = "Information",
                Category = "Request",
                Message = $"Incoming request: {context.Request.Method} {context.Request.Path}",
                Properties = new Dictionary<string, object>
                {
                    ["Method"] = context.Request.Method,
                    ["Path"] = context.Request.Path.Value ?? "",
                    ["QueryString"] = context.Request.QueryString.Value ?? "",
                    ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
                    ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "",
                    ["Timestamp"] = startTime
                }
            };

            _logService.AddLog(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar log de requisição");
        }
    }

    private void CaptureResponseLog(HttpContext context, DateTime startTime)
    {
        try
        {
            var duration = DateTime.UtcNow - startTime;
            var level = context.Response.StatusCode >= 400 ? "Warning" : "Information";

            var logEntry = new LogEntry
            {
                Level = level,
                Category = "Response",
                Message = $"Response: {context.Response.StatusCode} - {context.Request.Method} {context.Request.Path}",
                Properties = new Dictionary<string, object>
                {
                    ["StatusCode"] = context.Response.StatusCode,
                    ["Method"] = context.Request.Method,
                    ["Path"] = context.Request.Path.Value ?? "",
                    ["Duration"] = duration.TotalMilliseconds,
                    ["ContentType"] = context.Response.ContentType ?? "",
                    ["Timestamp"] = DateTime.UtcNow
                }
            };

            _logService.AddLog(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar log de resposta");
        }
    }

    private void CaptureErrorLog(HttpContext context, Exception exception, DateTime startTime)
    {
        try
        {
            var duration = DateTime.UtcNow - startTime;

            var logEntry = new LogEntry
            {
                Level = "Error",
                Category = "Exception",
                Message = $"Exception: {exception.Message}",
                Exception = exception.ToString(),
                Properties = new Dictionary<string, object>
                {
                    ["Method"] = context.Request.Method,
                    ["Path"] = context.Request.Path.Value ?? "",
                    ["Duration"] = duration.TotalMilliseconds,
                    ["ExceptionType"] = exception.GetType().Name,
                    ["Timestamp"] = DateTime.UtcNow
                }
            };

            _logService.AddLog(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar log de exceção");
        }
    }
}
