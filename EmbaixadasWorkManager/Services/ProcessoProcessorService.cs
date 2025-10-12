using System.Text.Json;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Services;

public class ProcessoProcessorService : IProcessoProcessorService
{
    private readonly ILogger<ProcessoProcessorService> _logger;
    private readonly IDynamoDbService _dynamoDbService;
    private readonly IPostProcessingPipeline _postProcessingPipeline;
    private readonly IExecucaoEmpresaService _execucaoEmpresaService;

    public ProcessoProcessorService(
        ILogger<ProcessoProcessorService> logger,
        IDynamoDbService dynamoDbService,
        IPostProcessingPipeline postProcessingPipeline,
        IExecucaoEmpresaService execucaoEmpresaService)
    {
        _logger = logger;
        _dynamoDbService = dynamoDbService;
        _postProcessingPipeline = postProcessingPipeline;
        _execucaoEmpresaService = execucaoEmpresaService;
    }

    public async Task<bool> ProcessarMensagemProcessoAsync(string messageBody, string messageId)
    {
        try
        {
            _logger.LogInformation("Processando mensagem de processo: {MessageId}", messageId);

            // Deserializar a mensagem
            var mensagem = JsonSerializer.Deserialize<ProcessoMessage>(messageBody);
            if (mensagem == null)
            {
                _logger.LogError("Falha ao deserializar mensagem de processo: {MessageId}", messageId);
                return false;
            }

            // Validar a mensagem
            if (!ValidateProcessoMessage(mensagem))
            {
                _logger.LogError("Mensagem de processo inválida: {MessageId}", messageId);
                return false;
            }

            // NOVA LÓGICA: Buscar ExecucaoVerificacao pelo ID único para obter ExecucaoId
            var execucaoVerificacao = await _dynamoDbService.GetExecucaoVerificacaoAsync(mensagem.ExecucaoVerificacaoId);
            
            if (execucaoVerificacao == null)
            {
                _logger.LogError("ExecucaoVerificacao não encontrada: {ExecucaoVerificacaoId}", mensagem.ExecucaoVerificacaoId);
                return false;
            }

            _logger.LogDebug("Encontrada ExecucaoVerificacao: {ExecucaoVerificacaoId}, ExecucaoId: {ExecucaoId}, VerificacaoId: {VerificacaoId}", 
                mensagem.ExecucaoVerificacaoId, execucaoVerificacao.ExecucaoId, execucaoVerificacao.VerificacaoId);

            // Buscar execução usando o ExecucaoId da ExecucaoVerificacao
            var execucao = await _dynamoDbService.GetExecucaoAsync(execucaoVerificacao.ExecucaoId);
            if (execucao == null)
            {
                _logger.LogError("Execução não encontrada: {ExecucaoId}", execucaoVerificacao.ExecucaoId);
                return false;
            }

            // Atualizar contadores consolidados
            if (mensagem.IsSuccess)
            {
                execucao.VerificacoesProcessadas++;
                
                // Somar TotalRecordsProcessados da ExecucaoVerificacao no TotalApontamentos da Execucao
                execucao.TotalApontamentos += execucaoVerificacao.TotalRecordsProcessados;
                
                _logger.LogDebug("Soma de TotalRecordsProcessados: {TotalRecordsProcessados} adicionado ao TotalApontamentos. Total atual: {TotalApontamentos}", 
                    execucaoVerificacao.TotalRecordsProcessados, execucao.TotalApontamentos);
            }
            else
            {
                execucao.VerificacoesComErro++;
                
                // Adicionar detalhes do erro à lista de erros
                var erroExecucao = new ErroExecucao
                {
                    ExecucaoVerificacaoId = execucaoVerificacao.Id,
                    VerificacaoId = execucaoVerificacao.VerificacaoId,
                    Sql = execucaoVerificacao.Sql,
                    SqlOriginal = execucaoVerificacao.SqlOriginal,
                    ErrorCode = execucaoVerificacao.Erro?.ErrorCode ?? "UNKNOWN_ERROR",
                    Message = execucaoVerificacao.Erro?.Message ?? "Erro desconhecido",
                    OccurredAt = execucaoVerificacao.Erro?.OccurredAt ?? DateTime.UtcNow
                };
                
                execucao.Erros.Add(erroExecucao);
                
                _logger.LogDebug("Erro adicionado à lista de erros da execução: {ExecucaoVerificacaoId} - {ErrorCode}: {Message}", 
                    erroExecucao.ExecucaoVerificacaoId, erroExecucao.ErrorCode, erroExecucao.Message);
            }

            // Verificar se todas as verificações foram processadas
            var totalProcessadas = execucao.VerificacoesProcessadas + execucao.VerificacoesComErro;
            if (totalProcessadas >= execucao.QuantidadeVerificacoes)
            {
                // Todas as verificações foram processadas
                var finalizadaComSucesso = execucao.VerificacoesComErro == 0;
                
                if (finalizadaComSucesso)
                {
                    execucao.Status = StatusExecucao.FinalizadaComSucesso;
                    _logger.LogInformation("Execução finalizada com sucesso: {ExecucaoId}. Total de apontamentos: {TotalApontamentos}", 
                        execucao.Id, execucao.TotalApontamentos);
                }
                else
                {
                    execucao.Status = StatusExecucao.FinalizadaComErro;
                    _logger.LogWarning("Execução finalizada com erros: {ExecucaoId}. Total de erros: {TotalErros}. Total de apontamentos: {TotalApontamentos}. Erros detalhados: {ErrosDetalhados}", 
                        execucao.Id, execucao.VerificacoesComErro, execucao.TotalApontamentos, execucao.Erros.Count);
                }
                
                execucao.DataFim = DateTime.UtcNow;

                // NOVO: Atualizar status final nas tabelas de performance
                await AtualizarTabelasPerformanceAsync(execucao);

                // NOVO: Executar pipeline de pós-processamento
                await ExecutarPosProcessamentoAsync(execucao, finalizadaComSucesso);
            }
            else
            {
                // Ainda há verificações sendo processadas
                execucao.Status = StatusExecucao.ProcessandoVerificacoes;
                _logger.LogDebug("Execução em andamento: {ExecucaoId}. Processadas: {Processadas}/{Total}", 
                    execucao.Id, totalProcessadas, execucao.QuantidadeVerificacoes);
            }

            // Atualizar execução no DynamoDB
            await _dynamoDbService.UpdateAsync(execucao);
            
            _logger.LogInformation("Contadores atualizados para execução {ExecucaoId}: Sucesso={Sucesso}, Erros={Erros}, TotalApontamentos={TotalApontamentos}", 
                execucao.Id, execucao.VerificacoesProcessadas, execucao.VerificacoesComErro, execucao.TotalApontamentos);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem de processo: {MessageId}", messageId);
            return false;
        }
    }

    private bool ValidateProcessoMessage(ProcessoMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ExecucaoVerificacaoId))
        {
            _logger.LogError("ExecucaoVerificacaoId é obrigatório");
            return false;
        }

        // Validar se é um GUID válido
        if (!Guid.TryParse(message.ExecucaoVerificacaoId, out _))
        {
            _logger.LogError("ExecucaoVerificacaoId deve ser um GUID válido: {ExecucaoVerificacaoId}", message.ExecucaoVerificacaoId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Atualiza status final nas tabelas de performance (ExecucaoResumoView e ExecucaoEmpresaStatus)
    /// </summary>
    private async Task AtualizarTabelasPerformanceAsync(Execucao execucao)
    {
        try
        {
            _logger.LogInformation(
                "Atualizando status final nas tabelas de performance. " +
                "ExecucaoId: {ExecucaoId}, Status: {Status}",
                execucao.Id, execucao.Status);

            await _execucaoEmpresaService.AtualizarStatusFinalAsync(
                execucao.Id,
                execucao.IdEmbaixadas,
                execucao.Empresa,
                execucao.Status);

            _logger.LogInformation("Status final atualizado nas tabelas de performance");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Erro ao atualizar status final nas tabelas de performance. Continuando... ExecucaoId: {ExecucaoId}",
                execucao.Id);
            // Não propaga o erro
        }
    }

    /// <summary>
    /// Executa o pipeline de pós-processamento após finalização da execução
    /// </summary>
    private async Task ExecutarPosProcessamentoAsync(Execucao execucao, bool finalizadaComSucesso)
    {
        try
        {
            _logger.LogInformation("Iniciando pos-processamento para execucao {ExecucaoId}", execucao.Id);

            // Criar contexto do pós-processamento
            var context = new PostProcessingContext
            {
                Execucao = execucao,
                FinalizadaComSucesso = finalizadaComSucesso,
                Metadata = new Dictionary<string, object>
                {
                    { "TotalVerificacoes", execucao.QuantidadeVerificacoes },
                    { "VerificacoesProcessadas", execucao.VerificacoesProcessadas },
                    { "VerificacoesComErro", execucao.VerificacoesComErro },
                    { "TotalApontamentos", execucao.TotalApontamentos }
                }
            };

            // Executar pipeline
            var resultado = await _postProcessingPipeline.ExecuteAsync(context);

            if (resultado.Success)
            {
                _logger.LogInformation(
                    "Pos-processamento concluido com sucesso para execucao {ExecucaoId}. " +
                    "Steps executados: {Executed}, Tempo: {Time}ms",
                    execucao.Id,
                    resultado.TotalStepsExecuted,
                    resultado.TotalExecutionTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Pos-processamento concluido com falhas para execucao {ExecucaoId}. " +
                    "Steps executados: {Executed}, Falhas: {Failed}, Tempo: {Time}ms",
                    execucao.Id,
                    resultado.TotalStepsExecuted,
                    resultado.TotalStepsFailed,
                    resultado.TotalExecutionTime.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar pos-processamento para execucao {ExecucaoId}. Continuando...", 
                execucao.Id);
            // Não propaga o erro - pós-processamento não deve interromper o fluxo principal
        }
    }
}

