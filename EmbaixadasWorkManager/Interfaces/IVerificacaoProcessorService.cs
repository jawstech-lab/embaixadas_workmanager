namespace EmbaixadasWorkManager.Interfaces;

using EmbaixadasWorkManager.Models;

public interface IVerificacaoProcessorService
{
	// Processa uma verificação (carrega a verificação e suas queries, e envia as queries para fila)
	// Retorna tupla: (quantidade de queries (shards) enviadas, total de registros encontrados, erro caso tenha ocorrido)
	Task<(int queriesEnviadas, int totalRegistros, ErroExecucao? erro)> ProcessarVerificacaoAsync(Execucao execucao, string verificacaoId);
}
