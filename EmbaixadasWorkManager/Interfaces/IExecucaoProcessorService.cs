namespace EmbaixadasWorkManager.Interfaces;

public interface IExecucaoProcessorService
{
	Task<bool> ProcessExecucaoMessageAsync(string messageBody, string messageId);
}
