using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using System.Collections.Concurrent;

namespace EmbaixadasWorkManager.Services;

/// <summary>
/// Serviço para gerenciamento de logs em memória
/// </summary>
public class LogService : ILogService
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();
    private readonly object _lock = new();
    private const int MaxLogs = 10000; // Limite máximo de logs em memória

    public void AddLog(LogEntry logEntry)
    {
        lock (_lock)
        {
            _logs.Enqueue(logEntry);

            // Manter apenas os logs mais recentes
            while (_logs.Count > MaxLogs)
            {
                _logs.TryDequeue(out _);
            }
        }
    }

    public List<LogEntry> GetLogs(string? level = null, string? source = null, int limit = 100, int offset = 0)
    {
        var logs = _logs.ToList();

        // Aplicar filtros
        if (!string.IsNullOrEmpty(level))
        {
            logs = logs.Where(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(source))
        {
            logs = logs.Where(l => l.Category.Contains(source, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Ordenar por timestamp (mais recentes primeiro)
        logs = logs.OrderByDescending(l => l.Timestamp).ToList();

        // Aplicar paginação
        return logs.Skip(offset).Take(limit).ToList();
    }

    public LogStatistics GetStatistics()
    {
        var logs = _logs.ToList();
        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);
        var lastHour = now.AddHours(-1);

        var stats = new LogStatistics
        {
            TotalLogs = logs.Count,
            LogsLast24Hours = logs.Count(l => l.Timestamp >= last24Hours),
            LogsLastHour = logs.Count(l => l.Timestamp >= lastHour),
            OldestLog = logs.Any() ? logs.Min(l => l.Timestamp) : null,
            NewestLog = logs.Any() ? logs.Max(l => l.Timestamp) : null
        };

        // Logs por nível
        stats.LogsByLevel = logs
            .GroupBy(l => l.Level)
            .ToDictionary(g => g.Key, g => g.Count());

        // Logs por categoria
        stats.LogsByCategory = logs
            .GroupBy(l => l.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        return stats;
    }

    public void ClearLogs()
    {
        lock (_lock)
        {
            while (_logs.TryDequeue(out _)) { }
        }
    }

    public List<LogEntry> GetRecentLogs(int count = 50)
    {
        return GetLogs(limit: count);
    }
}











