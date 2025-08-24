using Amazon.SQS;
using Amazon.SQS.Model;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace EmbaixadasWorkManager.Services;

public class ResilientSqsService : IResilientSqsService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<ResilientSqsService> _logger;
    private readonly SqsConfiguration _config;
    
    // Circuit Breaker Pattern
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers = new();
    private readonly object _lockObject = new object();
    
    // Retry Policy
    private const int MaxRetryAttempts = 3;
    private const int BaseDelayMs = 1000; // 1 segundo
    
    // Health Check
    private readonly ConcurrentDictionary<string, DateTime> _lastHealthCheck = new();
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(1);

    public ResilientSqsService(
        IAmazonSQS sqsClient,
        IOptions<SqsConfiguration> config,
        ILogger<ResilientSqsService> logger)
    {
        _sqsClient = sqsClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(string queueUrl, string messageBody, Dictionary<string, string>? attributes = null)
    {
        return await ExecuteWithResilienceAsync(async () =>
        {
            _logger.LogDebug("Enviando mensagem para fila: {QueueUrl}", queueUrl);
            
            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = messageBody
            };

            if (attributes != null)
            {
                request.MessageAttributes = attributes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new MessageAttributeValue { StringValue = kvp.Value, DataType = "String" }
                );
            }

            var response = await _sqsClient.SendMessageAsync(request);
            _logger.LogDebug("Mensagem enviada com sucesso. MessageId: {MessageId}", response.MessageId);
            
            return true;
        }, queueUrl, "SendMessage");
    }

    public async Task<bool> SendMessageBatchAsync(string queueUrl, List<string> messageBodies, Dictionary<string, string>? attributes = null)
    {
        return await ExecuteWithResilienceAsync(async () =>
        {
            _logger.LogDebug("Enviando lote de {Count} mensagens para fila: {QueueUrl}", messageBodies.Count, queueUrl);
            
            var entries = messageBodies.Select((body, index) => new SendMessageBatchRequestEntry
            {
                Id = $"msg_{index}",
                MessageBody = body,
                MessageAttributes = attributes?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new MessageAttributeValue { StringValue = kvp.Value, DataType = "String" }
                )
            }).ToList();

            var request = new SendMessageBatchRequest
            {
                QueueUrl = queueUrl,
                Entries = entries
            };

            var response = await _sqsClient.SendMessageBatchAsync(request);
            
            if (response.Successful.Any())
            {
                _logger.LogDebug("Lote enviado com sucesso. {SuccessfulCount} mensagens enviadas", response.Successful.Count);
            }
            
            if (response.Failed.Any())
            {
                _logger.LogWarning("Falha ao enviar {FailedCount} mensagens do lote", response.Failed.Count);
            }
            
            return response.Successful.Any();
        }, queueUrl, "SendMessageBatch");
    }

    public async Task<List<Message>> ReceiveMessagesAsync(string queueUrl, int maxNumberOfMessages = 10, int waitTimeSeconds = 20)
    {
        return await ExecuteWithResilienceAsync(async () =>
        {
            _logger.LogDebug("Recebendo mensagens da fila: {QueueUrl}. Max: {Max}, WaitTime: {WaitTime}s", 
                queueUrl, maxNumberOfMessages, waitTimeSeconds);
            
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = Math.Min(maxNumberOfMessages, 10), // SQS limit
                WaitTimeSeconds = Math.Min(waitTimeSeconds, 20), // SQS limit
                MessageAttributeNames = new List<string> { "All" },
                MessageSystemAttributeNames = new List<string> { "All" }
            };

            var response = await _sqsClient.ReceiveMessageAsync(request);
            if(response.Messages != null)
                _logger.LogDebug("Recebidas {Count} mensagens da fila", response.Messages.Count);
            
            return response.Messages;
        }, queueUrl, "ReceiveMessages") ?? new List<Message>();
    }

    public async Task<bool> DeleteMessageAsync(string queueUrl, string receiptHandle)
    {
        return await ExecuteWithResilienceAsync(async () =>
        {
            _logger.LogDebug("Deletando mensagem da fila: {QueueUrl}. ReceiptHandle: {ReceiptHandle}", 
                queueUrl, receiptHandle);
            
            var request = new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle
            };

            await _sqsClient.DeleteMessageAsync(request);
            _logger.LogDebug("Mensagem deletada com sucesso");
            
            return true;
        }, queueUrl, "DeleteMessage");
    }

    public async Task<bool> DeleteMessageBatchAsync(string queueUrl, List<string> receiptHandles)
    {
        return await ExecuteWithResilienceAsync(async () =>
        {
            _logger.LogDebug("Deletando lote de {Count} mensagens da fila: {QueueUrl}", 
                receiptHandles.Count, queueUrl);
            
            var entries = receiptHandles.Select((handle, index) => new DeleteMessageBatchRequestEntry
            {
                Id = $"del_{index}",
                ReceiptHandle = handle
            }).ToList();

            var request = new DeleteMessageBatchRequest
            {
                QueueUrl = queueUrl,
                Entries = entries
            };

            var response = await _sqsClient.DeleteMessageBatchAsync(request);
            
            if (response.Successful?.Any() == true)
            {
                _logger.LogDebug("Lote deletado com sucesso. {SuccessfulCount} mensagens deletadas", response.Successful.Count);
            }
            
            if (response.Failed?.Any() == true)
            {
                _logger.LogWarning("Falha ao deletar {FailedCount} mensagens do lote", response.Failed.Count);
            }
            
            return response.Successful?.Any() == true;
        }, queueUrl, "DeleteMessageBatch");
    }

    public async Task<bool> ChangeMessageVisibilityAsync(string queueUrl, string receiptHandle, int visibilityTimeoutSeconds)
    {
        return await ExecuteWithResilienceAsync(async () =>
        {
            _logger.LogDebug("Alterando visibilidade da mensagem na fila: {QueueUrl}. Timeout: {Timeout}s", 
                queueUrl, visibilityTimeoutSeconds);
            
            var request = new ChangeMessageVisibilityRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle,
                VisibilityTimeout = visibilityTimeoutSeconds
            };

            await _sqsClient.ChangeMessageVisibilityAsync(request);
            _logger.LogDebug("Visibilidade da mensagem alterada com sucesso");
            
            return true;
        }, queueUrl, "ChangeMessageVisibility");
    }

    public async Task<string?> GetQueueUrlAsync(string queueName)
    {
        return await ExecuteWithResilienceAsync(async () =>
        {
            _logger.LogDebug("Obtendo URL da fila: {QueueName}", queueName);
            
            var request = new GetQueueUrlRequest { QueueName = queueName };
            var response = await _sqsClient.GetQueueUrlAsync(request);
            
            _logger.LogDebug("URL da fila obtida: {QueueUrl}", response.QueueUrl);
            return response.QueueUrl;
        }, queueName, "GetQueueUrl");
    }

    public async Task<bool> QueueExistsAsync(string queueUrl)
    {
        return await ExecuteWithResilienceAsync(async () =>
        {
            _logger.LogDebug("Verificando existência da fila: {QueueUrl}", queueUrl);
            
            var request = new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = new List<string> { "QueueArn" }
            };

            await _sqsClient.GetQueueAttributesAsync(request);
            _logger.LogDebug("Fila existe: {QueueUrl}", queueUrl);
            
            return true;
        }, queueUrl, "QueueExists");
    }

    // Métodos de resiliência
    public async Task<bool> IsHealthyAsync(string queueUrl)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (_lastHealthCheck.TryGetValue(queueUrl, out var lastCheck) && 
                now - lastCheck < _healthCheckInterval)
            {
                return true; // Cache recente
            }

            var isHealthy = await QueueExistsAsync(queueUrl);
            _lastHealthCheck.AddOrUpdate(queueUrl, now, (key, oldValue) => now);
            
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check falhou para fila: {QueueUrl}", queueUrl);
            return false;
        }
    }

    public async Task<bool> WaitForQueueAvailabilityAsync(string queueUrl, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        var endTime = startTime + timeout;

        while (DateTime.UtcNow < endTime)
        {
            if (await IsHealthyAsync(queueUrl))
            {
                _logger.LogInformation("Fila {QueueUrl} está disponível", queueUrl);
                return true;
            }

            _logger.LogDebug("Aguardando disponibilidade da fila {QueueUrl}...", queueUrl);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        _logger.LogWarning("Timeout aguardando disponibilidade da fila {QueueUrl}", queueUrl);
        return false;
    }

    public async Task<bool> ReconnectAsync(string queueUrl)
    {
        try
        {
            _logger.LogInformation("Tentando reconectar à fila: {QueueUrl}", queueUrl);
            
            // Reset do circuit breaker
            ResetCircuitBreaker(queueUrl);
            
            // Aguardar disponibilidade
            var isAvailable = await WaitForQueueAvailabilityAsync(queueUrl, TimeSpan.FromMinutes(2));
            
            if (isAvailable)
            {
                _logger.LogInformation("Reconexão bem-sucedida à fila: {QueueUrl}", queueUrl);
                return true;
            }
            else
            {
                _logger.LogError("Falha na reconexão à fila: {QueueUrl}", queueUrl);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante reconexão à fila: {QueueUrl}", queueUrl);
            return false;
        }
    }

    // Método principal de resiliência
    private async Task<T?> ExecuteWithResilienceAsync<T>(Func<Task<T>> operation, string queueUrl, string operationName)
    {
        var circuitBreaker = GetOrCreateCircuitBreaker(queueUrl);
        
        // Verificar se o circuit breaker está aberto
        if (circuitBreaker.IsOpen)
        {
            _logger.LogWarning("Circuit breaker aberto para fila {QueueUrl}. Operação {Operation} bloqueada.", 
                queueUrl, operationName);
            
            // Tentar reconectar
            if (await ReconnectAsync(queueUrl))
            {
                circuitBreaker.Reset();
            }
            else
            {
                return default(T);
            }
        }

        // Executar com retry policy
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                var result = await operation();
                
                // Sucesso - reset do circuit breaker
                circuitBreaker.OnSuccess();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tentativa {Attempt}/{MaxAttempts} falhou para operação {Operation} na fila {QueueUrl}", 
                    attempt, MaxRetryAttempts, operationName, queueUrl);

                if (attempt == MaxRetryAttempts)
                {
                    // Última tentativa falhou - abrir circuit breaker
                    circuitBreaker.OnFailure();
                    _logger.LogError(ex, "Todas as tentativas falharam para operação {Operation} na fila {QueueUrl}", 
                        operationName, queueUrl);
                    throw;
                }

                // Aguardar antes da próxima tentativa (exponential backoff)
                var delay = TimeSpan.FromMilliseconds(BaseDelayMs * Math.Pow(2, attempt - 1));
                await Task.Delay(delay);
            }
        }

        return default(T);
    }

    private CircuitBreakerState GetOrCreateCircuitBreaker(string queueUrl)
    {
        return _circuitBreakers.GetOrAdd(queueUrl, _ => new CircuitBreakerState());
    }

    private void ResetCircuitBreaker(string queueUrl)
    {
        if (_circuitBreakers.TryGetValue(queueUrl, out var circuitBreaker))
        {
            circuitBreaker.Reset();
        }
    }

    // Circuit Breaker State
    private class CircuitBreakerState
    {
        private int _failureCount = 0;
        private DateTime _lastFailureTime = DateTime.MinValue;
        private const int FailureThreshold = 5;
        private const int ResetTimeoutMinutes = 1;

        public bool IsOpen => _failureCount >= FailureThreshold && 
                             DateTime.UtcNow - _lastFailureTime < TimeSpan.FromMinutes(ResetTimeoutMinutes);

        public void OnSuccess()
        {
            _failureCount = 0;
            _lastFailureTime = DateTime.MinValue;
        }

        public void OnFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
        }

        public void Reset()
        {
            _failureCount = 0;
            _lastFailureTime = DateTime.MinValue;
        }
    }
}
