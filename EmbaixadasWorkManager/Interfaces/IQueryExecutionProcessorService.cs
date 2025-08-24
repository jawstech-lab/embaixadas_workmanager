namespace EmbaixadasWorkManager.Interfaces;

/// <summary>
/// Serviço responsável por processar mensagens de execução de queries
/// Recebe IDs da fila e busca dados completos na tabela ExecucaoVerificacao
/// </summary>
public interface IQueryExecutionProcessorService
{
    /// <summary>
    /// Processa uma mensagem de execução de query
    /// </summary>
    /// <param name="messageBody">Corpo da mensagem SQS</param>
    /// <param name="messageId">ID da mensagem SQS</param>
    /// <returns>True se processado com sucesso, false caso contrário</returns>
    Task<bool> ProcessarQueryExecutionAsync(string messageBody, string messageId);
}
