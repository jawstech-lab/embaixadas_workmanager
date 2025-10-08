namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Representa uma entrada de log
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Timestamp do log
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Nível do log (Information, Warning, Error, Debug)
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Categoria/fonte do log
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Mensagem do log
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Dados estruturados do log (se houver)
    /// </summary>
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Exceção (se houver)
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// ID único do log
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Estatísticas dos logs
/// </summary>
public class LogStatistics
{
    /// <summary>
    /// Total de logs
    /// </summary>
    public int TotalLogs { get; set; }

    /// <summary>
    /// Logs por nível
    /// </summary>
    public Dictionary<string, int> LogsByLevel { get; set; } = new();

    /// <summary>
    /// Logs por categoria
    /// </summary>
    public Dictionary<string, int> LogsByCategory { get; set; } = new();

    /// <summary>
    /// Logs das últimas 24 horas
    /// </summary>
    public int LogsLast24Hours { get; set; }

    /// <summary>
    /// Logs das últimas 1 hora
    /// </summary>
    public int LogsLastHour { get; set; }

    /// <summary>
    /// Timestamp do log mais antigo
    /// </summary>
    public DateTime? OldestLog { get; set; }

    /// <summary>
    /// Timestamp do log mais recente
    /// </summary>
    public DateTime? NewestLog { get; set; }
}








