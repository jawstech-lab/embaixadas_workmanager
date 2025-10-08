using System.Text.Json;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Services;

/// <summary>
/// Serviço responsável por processar mensagens de execução de queries
/// Recebe IDs da fila e busca dados completos na tabela ExecucaoVerificacao
/// </summary>
public class QueryExecutionProcessorService : IQueryExecutionProcessorService
{
    private readonly ILogger<QueryExecutionProcessorService> _logger;
    private readonly IDynamoDbService _dynamoDbService;
    private readonly IConsultaService _consultaService;

    public QueryExecutionProcessorService(
        ILogger<QueryExecutionProcessorService> logger,
        IDynamoDbService dynamoDbService,
        IConsultaService consultaService)
    {
        _logger = logger;
        _dynamoDbService = dynamoDbService;
        _consultaService = consultaService;
    }

    public async Task<bool> ProcessarQueryExecutionAsync(string messageBody, string messageId)
    {
        try
        {
            _logger.LogInformation("Processando query execution: {MessageId}", messageId);

            // Deserializar a mensagem
            var mensagem = JsonSerializer.Deserialize<QueryExecutionMessage>(messageBody);
            if (mensagem == null)
            {
                _logger.LogError("Falha ao deserializar mensagem de query execution: {MessageId}", messageId);
                return false;
            }

            // Validar a mensagem
            if (!ValidateQueryExecutionMessage(mensagem))
            {
                _logger.LogError("Mensagem de query execution inválida: {MessageId}", messageId);
                return false;
            }

            // Buscar dados completos na tabela ExecucaoVerificacao
            var execucaoVerificacao = await BuscarExecucaoVerificacaoAsync(mensagem.ExecucaoVerificacaoId);
            if (execucaoVerificacao == null)
            {
                _logger.LogError("ExecucaoVerificacao não encontrada: {Id}", mensagem.ExecucaoVerificacaoId);
                return false;
            }

            // Verificar se já foi processada
            if (execucaoVerificacao.Status != StatusExecucaoVerificacao.Pendente)
            {
                _logger.LogWarning("ExecucaoVerificacao já foi processada: {Id}. Status atual: {Status}", 
                    mensagem.ExecucaoVerificacaoId, execucaoVerificacao.Status);
                return true; // Retorna true pois não é um erro, apenas já foi processada
            }

            // Atualizar status para em processamento
            execucaoVerificacao.Status = StatusExecucaoVerificacao.EmProcessamento;
            execucaoVerificacao.DataInicio = DateTime.UtcNow;
            execucaoVerificacao.DataAtualizacao = DateTime.UtcNow;
            await _dynamoDbService.UpdateAsync(execucaoVerificacao);

            // Executar a query
            var resultado = await ExecutarQueryAsync(execucaoVerificacao);

            // Atualizar resultado
            execucaoVerificacao.Status = resultado.Success ? StatusExecucaoVerificacao.Concluida : StatusExecucaoVerificacao.Erro;
            execucaoVerificacao.DataFim = DateTime.UtcNow;
            execucaoVerificacao.Resultado = resultado.Success ? resultado.Result : null;
            execucaoVerificacao.Erro = resultado.Success ? null : new ErroDetalhado
            {
                ErrorCode = "QUERY_EXECUTION_FAILED",
                Message = resultado.Error,
                TechnicalDetails = resultado.Error,
                IsRecoverable = false,
                RetryAttempts = 0,
                OccurredAt = DateTime.UtcNow
            };
            execucaoVerificacao.TempoExecucaoMs = resultado.TempoExecucaoMs;
            execucaoVerificacao.DataAtualizacao = DateTime.UtcNow;

            await _dynamoDbService.UpdateAsync(execucaoVerificacao);

            _logger.LogInformation("Query execution processada: {VerificacaoId}. Status: {Status}. Tempo: {Tempo}ms", 
                execucaoVerificacao.VerificacaoId, execucaoVerificacao.Status, resultado.TempoExecucaoMs);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar query execution: {MessageId}", messageId);
            return false;
        }
    }

    private bool ValidateQueryExecutionMessage(QueryExecutionMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ExecucaoVerificacaoId))
        {
            _logger.LogError("ExecucaoVerificacaoId é obrigatório");
            return false;
        }

        return true;
    }

    private async Task<ExecucaoVerificacao?> BuscarExecucaoVerificacaoAsync(string execucaoVerificacaoId)
    {
        try
        {
            // Validar formato do ID
            var (execucaoId, verificacaoId) = ExecucaoVerificacao.ParseId(execucaoVerificacaoId);
            if (string.IsNullOrEmpty(execucaoId) || string.IsNullOrEmpty(verificacaoId))
            {
                _logger.LogError("ExecucaoVerificacaoId inválido: {Id}. Formato esperado: ExecucaoId#VerificacaoId", execucaoVerificacaoId);
                return null;
            }

            // Buscar pelo ID composto (que é a chave primária)
            var execucaoVerificacao = await _dynamoDbService.GetAsync<ExecucaoVerificacao>(execucaoVerificacaoId);
            return execucaoVerificacao;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar ExecucaoVerificacao: {Id}", execucaoVerificacaoId);
            return null;
        }
    }

    private async Task<QueryExecutionResult> ExecutarQueryAsync(ExecucaoVerificacao execucaoVerificacao)
    {
        var inicio = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Executando query para verificação: {VerificacaoId}. SQL: {Sql}", 
                execucaoVerificacao.VerificacaoId, execucaoVerificacao.Sql);

            // TODO: Implementar execução real da query
            // Por enquanto, simular execução
            await Task.Delay(100); // Simular processamento

            var fim = DateTime.UtcNow;
            var tempoExecucao = (long)(fim - inicio).TotalMilliseconds;

            _logger.LogInformation("Query executada com sucesso: {VerificacaoId}. Tempo: {Tempo}ms", 
                execucaoVerificacao.VerificacaoId, tempoExecucao);

            return new QueryExecutionResult
            {
                Success = true,
                Result = "Query executada com sucesso (simulado)",
                TempoExecucaoMs = tempoExecucao
            };
        }
        catch (Exception ex)
        {
            var fim = DateTime.UtcNow;
            var tempoExecucao = (long)(fim - inicio).TotalMilliseconds;

            _logger.LogError(ex, "Erro ao executar query: {VerificacaoId}", execucaoVerificacao.VerificacaoId);

            return new QueryExecutionResult
            {
                Success = false,
                Error = ex.Message,
                TempoExecucaoMs = tempoExecucao
            };
        }
    }
}

/// <summary>
/// Resultado da execução de uma query
/// </summary>
public class QueryExecutionResult
{
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public long TempoExecucaoMs { get; set; }
}
