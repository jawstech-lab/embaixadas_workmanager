using Amazon.SQS;
using Amazon.SQS.Model;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services;

public class SqsService : ISqsService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsService> _logger;
    private readonly SqsConfiguration _config;

    public SqsService(
        IAmazonSQS sqsClient,
        IOptions<SqsConfiguration> config,
        ILogger<SqsService> logger)
    {
        _sqsClient = sqsClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(string queueUrl, string messageBody, Dictionary<string, string>? attributes = null)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar mensagem para fila: {QueueUrl}", queueUrl);
            return false;
        }
    }

    public async Task<bool> SendMessageBatchAsync(string queueUrl, List<string> messageBodies, Dictionary<string, string>? attributes = null)
    {
        try
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
            
            if (response.Successful?.Any() == true)
            {
                _logger.LogDebug("Lote enviado com sucesso. {SuccessfulCount} mensagens enviadas", response.Successful.Count);
            }
            
            if (response.Failed?.Any() == true)
            {
                _logger.LogWarning("Falha ao enviar {FailedCount} mensagens do lote", response.Failed.Count);
            }
            
            return response.Successful?.Any() == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar lote de mensagens para fila: {QueueUrl}", queueUrl);
            return false;
        }
    }

    public async Task<List<Message>> ReceiveMessagesAsync(string queueUrl, int maxNumberOfMessages = 10, int waitTimeSeconds = 20)
    {
        try
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
            _logger.LogDebug("Recebidas {Count} mensagens da fila", response.Messages.Count);
            
            return response.Messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao receber mensagens da fila: {QueueUrl}", queueUrl);
            return new List<Message>();
        }
    }

    public async Task<bool> DeleteMessageAsync(string queueUrl, string receiptHandle)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar mensagem da fila: {QueueUrl}", queueUrl);
            return false;
        }
    }

    public async Task<bool> DeleteMessageBatchAsync(string queueUrl, List<string> receiptHandles)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar lote de mensagens da fila: {QueueUrl}", queueUrl);
            return false;
        }
    }

    public async Task<bool> ChangeMessageVisibilityAsync(string queueUrl, string receiptHandle, int visibilityTimeoutSeconds)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar visibilidade da mensagem na fila: {QueueUrl}", queueUrl);
            return false;
        }
    }

    public async Task<string?> GetQueueUrlAsync(string queueName)
    {
        try
        {
            _logger.LogDebug("Obtendo URL da fila: {QueueName}", queueName);
            
            var request = new GetQueueUrlRequest { QueueName = queueName };
            var response = await _sqsClient.GetQueueUrlAsync(request);
            
            _logger.LogDebug("URL da fila obtida: {QueueUrl}", response.QueueUrl);
            return response.QueueUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter URL da fila: {QueueName}", queueName);
            return null;
        }
    }

    public async Task<bool> QueueExistsAsync(string queueUrl)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Fila não existe: {QueueUrl}. Erro: {Error}", queueUrl, ex.Message);
            return false;
        }
    }
}
