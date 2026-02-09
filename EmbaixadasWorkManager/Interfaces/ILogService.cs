using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Interfaces;

/// <summary>
/// Interface para gerenciamento de logs em memória
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Adiciona um log à coleção em memória
    /// </summary>
    /// <param name="logEntry">Entrada de log</param>
    void AddLog(LogEntry logEntry);

    /// <summary>
    /// Obtém logs com filtros opcionais
    /// </summary>
    /// <param name="level">Nível do log (opcional)</param>
    /// <param name="source">Fonte do log (opcional)</param>
    /// <param name="limit">Limite de registros (padrão: 100)</param>
    /// <param name="offset">Offset para paginação (padrão: 0)</param>
    /// <returns>Lista de logs filtrados</returns>
    List<LogEntry> GetLogs(string? level = null, string? source = null, int limit = 100, int offset = 0);

    /// <summary>
    /// Obtém estatísticas dos logs
    /// </summary>
    /// <returns>Estatísticas dos logs</returns>
    LogStatistics GetStatistics();

    /// <summary>
    /// Limpa todos os logs da memória
    /// </summary>
    void ClearLogs();

    /// <summary>
    /// Obtém logs em tempo real (últimos N registros)
    /// </summary>
    /// <param name="count">Número de logs recentes</param>
    /// <returns>Lista dos logs mais recentes</returns>
    List<LogEntry> GetRecentLogs(int count = 50);
}

















