namespace EmbaixadasWorkManager.Interfaces;

using EmbaixadasWorkManager.Models;

public interface IVerificacaoProcessorService
{
	// Processa uma verificação (carrega a verificação e suas queries, e envia as queries para fila)
	// Retorna a quantidade de queries enviadas
	Task<int> ProcessarVerificacaoAsync(Execucao execucao, string verificacaoId);
}
