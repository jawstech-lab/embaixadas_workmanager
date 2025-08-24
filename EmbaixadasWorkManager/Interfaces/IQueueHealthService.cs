namespace EmbaixadasWorkManager.Interfaces;

public interface IQueueHealthService
{
    /// <summary>
    /// Verifica a saúde de todas as filas configuradas
    /// </summary>
    Task<bool> CheckAllQueuesHealthAsync();
    
    /// <summary>
    /// Verifica a saúde de uma fila específica
    /// </summary>
    Task<bool> CheckQueueHealthAsync(string queueName);
    
    /// <summary>
    /// Testa a conectividade básica com SQS
    /// </summary>
    Task<bool> TestSqsConnectivityAsync();
    
    /// <summary>
    /// Tenta reconectar a uma fila específica
    /// </summary>
    Task<bool> ReconnectToQueueAsync(string queueName);
    
    /// <summary>
    /// Obtém o status de saúde de todas as filas
    /// </summary>
    Task<Dictionary<string, bool>> GetQueuesHealthStatusAsync();
}

