using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Interfaces;

public interface IMessageProcessorService
{
    Task<MessageProcessingResult> ProcessQueueMessagesAsync(string queueName);
}
