using System.Text.Json;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Services;

public class ProcessoProcessorService : IProcessoProcessorService
{
    private readonly ILogger<ProcessoProcessorService> _logger;
    private readonly IDynamoDbService _dynamoDbService;

    public ProcessoProcessorService(
        ILogger<ProcessoProcessorService> logger,
        IDynamoDbService dynamoDbService)
    {
        _logger = logger;
        _dynamoDbService = dynamoDbService;
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

            // CONSOLIDAÇÃO: Buscar execução diretamente
            var execucao = await _dynamoDbService.GetExecucaoAsync(mensagem.ExecucaoId);
            if (execucao == null)
            {
                _logger.LogError("Execução não encontrada: {ExecucaoId}", mensagem.ExecucaoId);
                return false;
            }

            // Atualizar contadores consolidados
            if (mensagem.IsSuccess)
            {
                execucao.VerificacoesProcessadas++;
            }
            else
            {
                execucao.VerificacoesComErro++;
            }

            // Verificar se todas as verificações foram processadas
            var totalProcessadas = execucao.VerificacoesProcessadas + execucao.VerificacoesComErro;
            if (totalProcessadas >= execucao.QuantidadeVerificacoes)
            {
                // Todas as verificações foram processadas
                if (execucao.VerificacoesComErro == 0)
                {
                    execucao.Status = StatusExecucao.FinalizadaComSucesso;
                    _logger.LogInformation("Execução finalizada com sucesso: {ExecucaoId}", execucao.Id);
                }
                else
                {
                    execucao.Status = StatusExecucao.FinalizadaComErro;
                    execucao.Erro = $"{execucao.VerificacoesComErro} verificações falharam";
                    _logger.LogWarning("Execução finalizada com erros: {ExecucaoId}. Erros: {Erros}", 
                        execucao.Id, execucao.VerificacoesComErro);
                }
                
                execucao.DataFim = DateTime.UtcNow;
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
            
            _logger.LogInformation("Contadores atualizados para execução {ExecucaoId}: Sucesso={Sucesso}, Erros={Erros}", 
                execucao.Id, execucao.VerificacoesProcessadas, execucao.VerificacoesComErro);

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
        if (string.IsNullOrWhiteSpace(message.ExecucaoId))
        {
            _logger.LogError("ExecucaoId é obrigatório");
            return false;
        }

        return true;
    }
}

