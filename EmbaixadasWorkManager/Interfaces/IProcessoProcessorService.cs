namespace EmbaixadasWorkManager.Interfaces;

public interface IProcessoProcessorService
{
    /// <summary>
    /// Processa mensagens da fila de processo de execução
    /// </summary>
    Task<bool> ProcessarMensagemProcessoAsync(string messageBody, string messageId);
}

