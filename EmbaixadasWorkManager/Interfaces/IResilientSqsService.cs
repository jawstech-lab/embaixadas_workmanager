using Amazon.SQS.Model;

namespace EmbaixadasWorkManager.Interfaces;

public interface IResilientSqsService
{
    Task<bool> SendMessageAsync(string queueUrl, string messageBody, Dictionary<string, string>? attributes = null);
    Task<bool> SendMessageBatchAsync(string queueUrl, List<string> messageBodies, Dictionary<string, string>? attributes = null);
    Task<List<Message>> ReceiveMessagesAsync(string queueUrl, int maxNumberOfMessages = 10, int waitTimeSeconds = 20);
    Task<bool> DeleteMessageAsync(string queueUrl, string receiptHandle);
    Task<bool> DeleteMessageBatchAsync(string queueUrl, List<string> receiptHandles);
    Task<bool> ChangeMessageVisibilityAsync(string queueUrl, string receiptHandle, int visibilityTimeoutSeconds);
    Task<string?> GetQueueUrlAsync(string queueName);
    Task<bool> QueueExistsAsync(string queueUrl);
    
    // Métodos de resiliência
    Task<bool> IsHealthyAsync(string queueUrl);
    Task<bool> WaitForQueueAvailabilityAsync(string queueUrl, TimeSpan timeout);
    Task<bool> ReconnectAsync(string queueUrl);
}

