using Amazon.SQS.Model;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services;

public class QueueManagerService : IQueueManagerService
{
    private readonly ILogger<QueueManagerService> _logger;
    private readonly IResilientSqsService _resilientSqsService;
    private readonly SqsConfiguration _sqsConfig;
    private readonly Dictionary<string, string> _queueUrlCache = new();
    private readonly Dictionary<string, DateTime> _lastUrlCheck = new();

    public QueueManagerService(
        ILogger<QueueManagerService> logger,
        IResilientSqsService resilientSqsService,
        IOptions<SqsConfiguration> sqsConfig)
    {
        _logger = logger;
        _resilientSqsService = resilientSqsService;
        _sqsConfig = sqsConfig.Value;
    }

    public async Task<string?> GetQueueUrlAsync(string queueName)
    {
        try
        {
            // Verificar cache primeiro
            if (_queueUrlCache.TryGetValue(queueName, out var cachedUrl))
            {
                var lastCheck = _lastUrlCheck.GetValueOrDefault(queueName, DateTime.MinValue);
                if (DateTime.UtcNow - lastCheck < TimeSpan.FromMinutes(10))
                {
                    return cachedUrl;
                }
            }
            
            var queueUrl = await _resilientSqsService.GetQueueUrlAsync(queueName);
            
            if (!string.IsNullOrEmpty(queueUrl))
            {
                _queueUrlCache[queueName] = queueUrl;
                _lastUrlCheck[queueName] = DateTime.UtcNow;
                _logger.LogDebug("URL da fila {QueueName} obtida e cacheada", queueName);
            }
            else
            {
                _logger.LogError("Não foi possível obter a URL da fila: {QueueName}", queueName);
            }
            
            return queueUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter URL da fila: {QueueName}", queueName);
            return null;
        }
    }

    public async Task<List<Message>> ReceiveMessagesAsync(string queueName, int maxMessages, int waitTimeSeconds)
    {
        try
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            if (string.IsNullOrEmpty(queueUrl))
            {
                _logger.LogWarning("Não foi possível obter URL da fila {QueueName} para receber mensagens", queueName);
                return new List<Message>();
            }
            
            var messages = await _resilientSqsService.ReceiveMessagesAsync(
                queueUrl, 
                maxMessages, 
                waitTimeSeconds);
            
            if (messages.Any())
            {
                _logger.LogDebug("Recebidas {MessageCount} mensagens da fila {QueueName}", messages.Count, queueName);
            }
            
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao receber mensagens da fila: {QueueName}", queueName);
            return new List<Message>();
        }
    }

    public async Task<bool> DeleteProcessedMessagesAsync(string queueName, List<string> receiptHandles)
    {
        try
        {
            // Validação adicional dos parâmetros
            if (receiptHandles == null)
            {
                _logger.LogWarning("Lista de receiptHandles é null para fila {QueueName}", queueName);
                return false;
            }
            
            if (!receiptHandles.Any())
            {
                _logger.LogDebug("Nenhuma mensagem para deletar da fila {QueueName}", queueName);
                return true;
            }
            
            // Validar que todos os receiptHandles são válidos
            var validReceiptHandles = receiptHandles.Where(rh => !string.IsNullOrEmpty(rh)).ToList();
            if (validReceiptHandles.Count != receiptHandles.Count)
            {
                _logger.LogWarning("Alguns receiptHandles são inválidos para fila {QueueName}. Válidos: {ValidCount}, Total: {TotalCount}", 
                    queueName, validReceiptHandles.Count, receiptHandles.Count);
            }
            
            if (!validReceiptHandles.Any())
            {
                _logger.LogWarning("Nenhum receiptHandle válido para deletar da fila {QueueName}", queueName);
                return false;
            }
            
            var queueUrl = await GetQueueUrlAsync(queueName);
            if (string.IsNullOrEmpty(queueUrl))
            {
                _logger.LogWarning("Não foi possível obter URL da fila {QueueName} para deletar mensagens", queueName);
                return false;
            }
            
            _logger.LogDebug("Tentando deletar {Count} mensagens da fila {QueueName} com URL: {QueueUrl}", 
                validReceiptHandles.Count, queueName, queueUrl);
            
            var deleted = await _resilientSqsService.DeleteMessageBatchAsync(queueUrl, validReceiptHandles);
            
            if (deleted)
            {
                _logger.LogDebug("{Count} mensagens deletadas com sucesso da fila {QueueName}", validReceiptHandles.Count, queueName);
            }
            else
            {
                _logger.LogWarning("Falha ao deletar {Count} mensagens da fila {QueueName}", validReceiptHandles.Count, queueName);
            }
            
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar mensagens da fila: {QueueName}. ReceiptHandles: {ReceiptHandles}", 
                queueName, receiptHandles?.Count ?? 0);
            return false;
        }
    }

    public async Task<bool> IsQueueReadyForProcessingAsync(string queueName)
    {
        try
        {
            var queueUrl = await GetQueueUrlAsync(queueName);
            if (string.IsNullOrEmpty(queueUrl))
            {
                return false;
            }
            
            var isHealthy = await _resilientSqsService.IsHealthyAsync(queueUrl);
            
            if (!isHealthy)
            {
                _logger.LogDebug("Fila {QueueName} não está saudável, tentando reconectar...", queueName);
                var reconnected = await _resilientSqsService.ReconnectAsync(queueUrl);
                if (reconnected)
                {
                    _logger.LogDebug("Reconexão à fila {QueueName} bem-sucedida", queueName);
                    return true;
                }
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar se fila {QueueName} está pronta para processamento", queueName);
            return false;
        }
    }

    public async Task<List<string>> ListAvailableQueuesAsync()
    {
        try
        {
            var availableQueues = new List<string>();
            
            // Verificar filas configuradas
            var execucaoUrl = await GetQueueUrlAsync(_sqsConfig.FilaExecucao);
            if (!string.IsNullOrEmpty(execucaoUrl))
            {
                availableQueues.Add(_sqsConfig.FilaExecucao);
            }
            
            var queryUrl = await GetQueueUrlAsync(_sqsConfig.FilaExecucaoQuery);
            if (!string.IsNullOrEmpty(queryUrl))
            {
                availableQueues.Add(_sqsConfig.FilaExecucaoQuery);
            }
            
            return availableQueues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar filas disponíveis");
            return new List<string>();
        }
    }
}
