using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmbaixadasWorkManager.Controllers;

/// <summary>
/// Controller para gerenciamento e visualização de logs
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogService logService, ILogger<LogsController> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém logs com filtros opcionais
    /// </summary>
    /// <param name="level">Nível do log (Information, Warning, Error, Debug)</param>
    /// <param name="source">Fonte do log</param>
    /// <param name="limit">Limite de registros (padrão: 100, máximo: 1000)</param>
    /// <param name="offset">Offset para paginação (padrão: 0)</param>
    /// <returns>Lista de logs filtrados</returns>
    [HttpGet]
    public ActionResult<List<LogEntry>> GetLogs(
        [FromQuery] string? level = null,
        [FromQuery] string? source = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        try
        {
            // Validar parâmetros
            if (limit <= 0 || limit > 1000)
            {
                return BadRequest("Limit deve estar entre 1 e 1000");
            }

            if (offset < 0)
            {
                return BadRequest("Offset deve ser maior ou igual a 0");
            }

            var logs = _logService.GetLogs(level, source, limit, offset);
            
            _logger.LogInformation("Retornando {Count} logs (level: {Level}, source: {Source}, limit: {Limit}, offset: {Offset})", 
                logs.Count, level ?? "todos", source ?? "todos", limit, offset);

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter logs");
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Obtém logs recentes
    /// </summary>
    /// <param name="count">Número de logs recentes (padrão: 50, máximo: 500)</param>
    /// <returns>Lista dos logs mais recentes</returns>
    [HttpGet("recent")]
    public ActionResult<List<LogEntry>> GetRecentLogs([FromQuery] int count = 50)
    {
        try
        {
            if (count <= 0 || count > 500)
            {
                return BadRequest("Count deve estar entre 1 e 500");
            }

            var logs = _logService.GetRecentLogs(count);
            
            _logger.LogInformation("Retornando {Count} logs recentes", logs.Count);

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter logs recentes");
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Obtém estatísticas dos logs
    /// </summary>
    /// <returns>Estatísticas dos logs</returns>
    [HttpGet("statistics")]
    public ActionResult<LogStatistics> GetStatistics()
    {
        try
        {
            var stats = _logService.GetStatistics();
            
            _logger.LogInformation("Retornando estatísticas dos logs: {TotalLogs} logs totais", stats.TotalLogs);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas dos logs");
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Limpa todos os logs da memória
    /// </summary>
    /// <returns>Confirmação da operação</returns>
    [HttpDelete]
    public ActionResult ClearLogs()
    {
        try
        {
            _logService.ClearLogs();
            
            _logger.LogInformation("Logs limpos da memória");

            return Ok(new { message = "Logs limpos com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar logs");
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Obtém logs por nível específico
    /// </summary>
    /// <param name="level">Nível do log</param>
    /// <param name="limit">Limite de registros</param>
    /// <returns>Lista de logs do nível especificado</returns>
    [HttpGet("level/{level}")]
    public ActionResult<List<LogEntry>> GetLogsByLevel(string level, [FromQuery] int limit = 100)
    {
        try
        {
            if (limit <= 0 || limit > 1000)
            {
                return BadRequest("Limit deve estar entre 1 e 1000");
            }

            var logs = _logService.GetLogs(level: level, limit: limit);
            
            _logger.LogInformation("Retornando {Count} logs do nível {Level}", logs.Count, level);

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter logs por nível {Level}", level);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Obtém logs por fonte/categoria
    /// </summary>
    /// <param name="source">Fonte do log</param>
    /// <param name="limit">Limite de registros</param>
    /// <returns>Lista de logs da fonte especificada</returns>
    [HttpGet("source/{source}")]
    public ActionResult<List<LogEntry>> GetLogsBySource(string source, [FromQuery] int limit = 100)
    {
        try
        {
            if (limit <= 0 || limit > 1000)
            {
                return BadRequest("Limit deve estar entre 1 e 1000");
            }

            var logs = _logService.GetLogs(source: source, limit: limit);
            
            _logger.LogInformation("Retornando {Count} logs da fonte {Source}", logs.Count, source);

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter logs por fonte {Source}", source);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Interface web para visualização de logs
    /// </summary>
    /// <returns>Página HTML com logs formatados</returns>
    [HttpGet("view")]
    [Produces("text/html")]
    public ActionResult ViewLogs()
    {
        // Filtrar logs do sistema (Request, Response, etc.) para não poluir a interface
        var allLogs = _logService.GetRecentLogs(200);
        var filteredLogs = allLogs.Where(log => 
            !log.Category.Contains("Request") && 
            !log.Category.Contains("Response") &&
            !log.Category.Contains("Microsoft.AspNetCore.Mvc") &&
            !log.Category.Contains("ControllerActionInvoker") &&
            !log.Category.Contains("Routing") &&
            !log.Category.Contains("Microsoft.AspNetCore.Hosting.Diagnostics")
        ).Take(100).ToList();
        
        var statistics = _logService.GetStatistics();
        
        var html = GenerateLogsHtml(filteredLogs, statistics);
        return Content(html, "text/html");
    }

    /// <summary>
    /// Endpoint temporário para debug - mostra todos os logs sem filtro
    /// </summary>
    /// <returns>Página HTML com todos os logs</returns>
    [HttpGet("debug")]
    [Produces("text/html")]
    public ActionResult ViewAllLogs()
    {
        var allLogs = _logService.GetRecentLogs(200);
        var statistics = _logService.GetStatistics();
        
        var html = GenerateLogsHtml(allLogs, statistics);
        return Content(html, "text/html");
    }

    private string GenerateLogsHtml(List<LogEntry> logs, LogStatistics statistics)
    {
        var html = $@"
<!DOCTYPE html>
<html lang='pt-BR'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Logs - Embaixadas Work Manager</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f5;
            color: #333;
        }}
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 2em;
        }}
        .stats {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            padding: 20px;
            background: #f8f9fa;
            border-bottom: 1px solid #dee2e6;
        }}
        .stat-card {{
            background: white;
            padding: 15px;
            border-radius: 6px;
            text-align: center;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }}
        .stat-number {{
            font-size: 2em;
            font-weight: bold;
            color: #667eea;
        }}
        .stat-label {{
            color: #666;
            font-size: 0.9em;
        }}
        .controls {{
            padding: 20px;
            background: #f8f9fa;
            border-bottom: 1px solid #dee2e6;
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
            align-items: center;
        }}
        .btn {{
            padding: 8px 16px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            text-decoration: none;
            display: inline-block;
            font-size: 14px;
            transition: background-color 0.2s;
        }}
        .btn-primary {{
            background: #007bff;
            color: white;
        }}
        .btn-primary:hover {{
            background: #0056b3;
        }}
        .btn-danger {{
            background: #dc3545;
            color: white;
        }}
        .btn-danger:hover {{
            background: #c82333;
        }}
        .btn-success {{
            background: #28a745;
            color: white;
        }}
        .btn-success:hover {{
            background: #1e7e34;
        }}
        .filter-select {{
            padding: 8px 12px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
        }}
        .logs-container {{
            max-height: 600px;
            overflow-y: auto;
        }}
        .log-entry {{
            padding: 12px 20px;
            border-bottom: 1px solid #eee;
            font-family: 'Courier New', monospace;
            font-size: 13px;
            line-height: 1.4;
        }}
        .log-entry:hover {{
            background-color: #f8f9fa;
        }}
        .log-timestamp {{
            color: #666;
            font-size: 11px;
            margin-right: 10px;
        }}
        .log-level {{
            display: inline-block;
            padding: 2px 6px;
            border-radius: 3px;
            font-size: 11px;
            font-weight: bold;
            margin-right: 8px;
            min-width: 60px;
            text-align: center;
        }}
        .log-level-information {{
            background: #d1ecf1;
            color: #0c5460;
        }}
        .log-level-warning {{
            background: #fff3cd;
            color: #856404;
        }}
        .log-level-error {{
            background: #f8d7da;
            color: #721c24;
        }}
        .log-level-debug {{
            background: #d4edda;
            color: #155724;
        }}
        .log-category {{
            color: #6c757d;
            font-weight: bold;
            margin-right: 8px;
        }}
        .log-message {{
            color: #333;
        }}
        .log-exception {{
            color: #dc3545;
            background: #f8f9fa;
            padding: 8px;
            border-radius: 4px;
            margin-top: 5px;
            font-size: 12px;
            white-space: pre-wrap;
        }}
        .no-logs {{
            text-align: center;
            padding: 40px;
            color: #666;
            font-style: italic;
        }}
        .auto-refresh {{
            margin-left: auto;
        }}
        .auto-refresh input[type='checkbox'] {{
            margin-right: 5px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📊 Logs do Work Manager</h1>
            <p>Embaixadas Work Manager - Visualização em Tempo Real</p>
        </div>
        
        <div class='stats'>
            <div class='stat-card'>
                <div class='stat-number'>{statistics.TotalLogs}</div>
                <div class='stat-label'>Total de Logs</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{statistics.LogsByLevel.Count}</div>
                <div class='stat-label'>Níveis Diferentes</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{statistics.LogsByCategory.Count}</div>
                <div class='stat-label'>Categorias</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>{statistics.LogsByLevel.GetValueOrDefault("Error", 0)}</div>
                <div class='stat-label'>Erros</div>
            </div>
        </div>
        
        <div class='controls'>
            <a href='/api/logs/view' class='btn btn-primary'>🔄 Atualizar</a>
            <a href='/api/logs/statistics' class='btn btn-success' target='_blank'>📈 JSON Stats</a>
            <a href='/api/logs' class='btn btn-success' target='_blank'>📄 JSON Logs</a>
            <button onclick='clearLogs()' class='btn btn-danger'>🗑️ Limpar Logs</button>
            
            <select class='filter-select' onchange='filterByLevel(this.value)'>
                <option value=''>Todos os Níveis</option>
                <option value='Information'>Information</option>
                <option value='Warning'>Warning</option>
                <option value='Error'>Error</option>
                <option value='Debug'>Debug</option>
            </select>
            
            <div class='auto-refresh'>
                <label>
                    <input type='checkbox' onchange='toggleAutoRefresh(this.checked)'> 
                    Auto-refresh (30s)
                </label>
            </div>
        </div>
        
        <div class='logs-container' id='logsContainer'>
            {GenerateLogsHtml(logs)}
        </div>
    </div>

    <script>
        let autoRefreshInterval;
        
        function toggleAutoRefresh(enabled) {{
            if (enabled) {{
                autoRefreshInterval = setInterval(() => {{
                    location.reload();
                }}, 30000);
            }} else {{
                if (autoRefreshInterval) {{
                    clearInterval(autoRefreshInterval);
                }}
            }}
        }}
        
        function filterByLevel(level) {{
            const logs = document.querySelectorAll('.log-entry');
            logs.forEach(log => {{
                const logLevel = log.querySelector('.log-level').textContent.trim();
                if (!level || logLevel === level) {{
                    log.style.display = 'block';
                }} else {{
                    log.style.display = 'none';
                }}
            }});
        }}
        
        function clearLogs() {{
            if (confirm('Tem certeza que deseja limpar todos os logs?')) {{
                fetch('/api/logs/clear', {{ method: 'DELETE' }})
                    .then(response => response.json())
                    .then(data => {{
                        alert('Logs limpos com sucesso!');
                        location.reload();
                    }})
                    .catch(error => {{
                        alert('Erro ao limpar logs: ' + error);
                    }});
            }}
        }}
        
        // Scroll para o topo automaticamente
        window.scrollTo(0, 0);
    </script>
</body>
</html>";

        return html;
    }

    private string GenerateLogsHtml(List<LogEntry> logs)
    {
        if (!logs.Any())
        {
            return "<div class='no-logs'>Nenhum log encontrado</div>";
        }

        var html = "";
        foreach (var log in logs.OrderByDescending(l => l.Timestamp))
        {
            var levelClass = log.Level.ToLower() switch
            {
                "information" => "log-level-information",
                "warning" => "log-level-warning", 
                "error" => "log-level-error",
                "debug" => "log-level-debug",
                _ => "log-level-information"
            };

            var timestamp = log.Timestamp.ToString("HH:mm:ss.fff");
            var exceptionHtml = !string.IsNullOrEmpty(log.Exception) 
                ? $"<div class='log-exception'>{log.Exception}</div>" 
                : "";

            html += $@"
            <div class='log-entry'>
                <span class='log-timestamp'>{timestamp}</span>
                <span class='log-level {levelClass}'>{log.Level}</span>
                <span class='log-category'>{log.Category}</span>
                <span class='log-message'>{log.Message}</span>
                {exceptionHtml}
            </div>";
        }

        return html;
    }
}

