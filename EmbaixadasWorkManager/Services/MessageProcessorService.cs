using Amazon.SQS.Model;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services;

public class MessageProcessorService : IMessageProcessorService
{
    private readonly ILogger<MessageProcessorService> _logger;
    private readonly IExecucaoProcessorService _execucaoProcessor;
    private readonly IProcessoProcessorService _processoProcessor;
    private readonly IQueueManagerService _queueManagerService;
    private readonly SqsConfiguration _sqsConfig;
    private readonly MessageProcessingStats _stats = new();

    public MessageProcessorService(
        ILogger<MessageProcessorService> logger,
        IExecucaoProcessorService execucaoProcessor,
        IProcessoProcessorService processoProcessor,
        IQueueManagerService queueManagerService,
        IOptions<SqsConfiguration> sqsConfig)
    {
        _logger = logger;
        _execucaoProcessor = execucaoProcessor;
        _processoProcessor = processoProcessor;
        _queueManagerService = queueManagerService;
        _sqsConfig = sqsConfig.Value;
    }

    public async Task<MessageProcessingResult> ProcessQueueMessagesAsync(string queueName)
    {
        var result = new MessageProcessingResult();
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogDebug("Processando mensagens da fila: {QueueName}", queueName);
            
            // Verificar se a fila está pronta para processamento
            var isReady = await _queueManagerService.IsQueueReadyForProcessingAsync(queueName);
            if (!isReady)
            {
                result.ErrorMessage = $"Fila {queueName} não está pronta para processamento";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }
            
            // Receber mensagens da fila
            var messages = await _queueManagerService.ReceiveMessagesAsync(
                queueName,
                _sqsConfig.MaxNumberOfMessages,
                _sqsConfig.WaitTimeSeconds);
            
            if (!messages.Any())
            {
                _logger.LogDebug("Nenhuma mensagem para processar na fila: {QueueName}", queueName);
                result.Success = true;
                return result;
            }
            
            _logger.LogInformation("Recebidas {MessageCount} mensagens da fila {QueueName}", messages.Count, queueName);
            
            // Processar mensagens em paralelo para maximizar a vazão (Throughput)
            var processingTasks = messages.Select(async message =>
            {
                try
                {
                    _logger.LogDebug("Iniciando processamento paralelo da mensagem: {MessageId}", message.MessageId);
                    var success = await ProcessMessageAsync(message, queueName);
                    return new { Success = success, ReceiptHandle = message.ReceiptHandle, MessageId = message.MessageId };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro fatal ao processar mensagem {MessageId} em paralelo", message.MessageId);
                    return new { Success = false, ReceiptHandle = message.ReceiptHandle, MessageId = message.MessageId };
                }
            });

            var processingResults = await Task.WhenAll(processingTasks);

            foreach (var processResult in processingResults)
            {
                if (processResult.Success)
                {
                    result.ProcessedCount++;
                    result.ProcessedReceiptHandles.Add(processResult.ReceiptHandle);
                }
                else
                {
                    result.FailedCount++;
                }
            }
            
            // Deletar mensagens processadas com sucesso
            if (result.ProcessedReceiptHandles?.Any() == true)
            {
                // Validar que todos os receiptHandles são válidos
                var validReceiptHandles = result.ProcessedReceiptHandles
                    .Where(rh => !string.IsNullOrEmpty(rh))
                    .ToList();
                
                if (validReceiptHandles.Count != result.ProcessedReceiptHandles.Count)
                {
                    _logger.LogWarning("Alguns receiptHandles são inválidos. Válidos: {ValidCount}, Total: {TotalCount}", 
                        validReceiptHandles.Count, result.ProcessedReceiptHandles.Count);
                }
                
                if (validReceiptHandles.Any())
                {
                    var deleted = await _queueManagerService.DeleteProcessedMessagesAsync(
                        queueName, 
                        validReceiptHandles);
                    
                    if (deleted)
                    {
                        _logger.LogInformation("{ProcessedCount} mensagens processadas e removidas da fila {QueueName}", 
                            result.ProcessedCount, queueName);
                    }
                    else
                    {
                        _logger.LogWarning("Falha ao remover {ProcessedCount} mensagens da fila {QueueName}", 
                            result.ProcessedCount, queueName);
                    }
                }
                else
                {
                    _logger.LogWarning("Nenhum receiptHandle válido para deletar da fila {QueueName}", queueName);
                }
            }
            
            if (result.FailedCount > 0)
            {
                _logger.LogWarning("{FailedCount} mensagens falharam no processamento da fila {QueueName}", 
                    result.FailedCount, queueName);
            }
            
            // Atualizar estatísticas
            UpdateStats(result, startTime);
            
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Erro ao processar mensagens da fila: {QueueName}", queueName);
            return result;
        }
    }

    public async Task<bool> ProcessMessageAsync(Message message)
    {
        try
        {
            // Determinar o tipo de mensagem baseado na fila atual
            // Esta informação seria passada como parâmetro ou determinada pelo contexto
            // Por enquanto, vamos assumir que é uma mensagem de execução
            var success = await _execucaoProcessor.ProcessExecucaoMessageAsync(
                message.Body, 
                message.MessageId);
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem: {MessageId}", message.MessageId);
            return false;
        }
    }

    public async Task<bool> ProcessMessageAsync(Message message, string queueName)
    {
        try
        {
            // Rotear mensagens baseado no nome da fila
            if (queueName.Contains("execucao-processo"))
            {
                // Processar mensagem de processo
                var success = await _processoProcessor.ProcessarMensagemProcessoAsync(
                    message.Body, 
                    message.MessageId);
                return success;
            }
            else
            {
                // Processar mensagem de execução (padrão)
                var success = await _execucaoProcessor.ProcessExecucaoMessageAsync(
                    message.Body, 
                    message.MessageId);
                return success;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem: {MessageId} da fila {QueueName}", message.MessageId, queueName);
            return false;
        }
    }

    public MessageProcessingStats GetProcessingStats()
    {
        return new MessageProcessingStats
        {
            TotalProcessed = _stats.TotalProcessed,
            TotalFailed = _stats.TotalFailed,
            LastProcessingTime = _stats.LastProcessingTime,
            AverageProcessingTime = _stats.AverageProcessingTime
        };
    }

    private void UpdateStats(MessageProcessingResult result, DateTime startTime)
    {
        var processingTime = DateTime.UtcNow - startTime;
        
        _stats.TotalProcessed += result.ProcessedCount;
        _stats.TotalFailed += result.FailedCount;
        _stats.LastProcessingTime = DateTime.UtcNow;
        
        // Calcular tempo médio de processamento (simplificado)
        if (_stats.TotalProcessed > 0)
        {
            var totalTime = _stats.AverageProcessingTime.TotalMilliseconds * (_stats.TotalProcessed - result.ProcessedCount) + processingTime.TotalMilliseconds;
            _stats.AverageProcessingTime = TimeSpan.FromMilliseconds(totalTime / _stats.TotalProcessed);
        }
        else
        {
            _stats.AverageProcessingTime = processingTime;
        }
    }
}
