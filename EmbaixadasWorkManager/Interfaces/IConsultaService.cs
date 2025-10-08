namespace EmbaixadasWorkManager.Interfaces;

using EmbaixadasWorkManager.Models;

public interface IConsultaService
{
	// Busca uma consulta pelo ID
	Task<Consulta?> GetConsultaAsync(string consultaId);
	
	// Busca consultas ativas
	Task<IEnumerable<Consulta>> GetConsultasAtivasAsync();

	// Processa consulta substituindo parâmetros com base em ValoresParametros (IdParametro -> Valor) e ParametrosExecucao (Alias -> Valor)
	Task<string> ProcessarConsultaComParametrosAsync(string sqlOriginal, List<VerificacaoParametroValor> valoresParametros, List<ParametroExecucao>? parametrosExecucao = null);
}
