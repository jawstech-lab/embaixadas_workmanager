using Amazon.SQS.Model;

namespace EmbaixadasWorkManager.Interfaces;

public interface IQueueManagerService
{
    /// <summary>
    /// Obtém a URL de uma fila pelo nome
    /// </summary>
    Task<string?> GetQueueUrlAsync(string queueName);
    
    /// <summary>
    /// Recebe mensagens de uma fila
    /// </summary>
    Task<List<Message>> ReceiveMessagesAsync(string queueName, int maxMessages, int waitTimeSeconds);
    
    /// <summary>
    /// Deleta mensagens processadas de uma fila
    /// </summary>
    Task<bool> DeleteProcessedMessagesAsync(string queueName, List<string> receiptHandles);
    
    /// <summary>
    /// Verifica se uma fila está disponível para processamento
    /// </summary>
    Task<bool> IsQueueReadyForProcessingAsync(string queueName);
    
    /// <summary>
    /// Lista todas as filas disponíveis
    /// </summary>
    Task<List<string>> ListAvailableQueuesAsync();
}

