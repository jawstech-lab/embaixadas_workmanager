using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace EmbaixadasWorkManager.Services;

public class ConsultaService : IConsultaService
{
	private readonly ILogger<ConsultaService> _logger;
	private readonly IDynamoDbService _dynamoDbService;

	public ConsultaService(
		ILogger<ConsultaService> logger,
		IDynamoDbService dynamoDbService)
	{
		_logger = logger;
		_dynamoDbService = dynamoDbService;
	}

	public async Task<Consulta?> GetConsultaAsync(string consultaId)
	{
		try
		{
			_logger.LogDebug("Buscando consulta: {ConsultaId}", consultaId);
			var consulta = await _dynamoDbService.GetAsync<Consulta>(consultaId);
			
			if (consulta == null)
			{
				_logger.LogWarning("Consulta não encontrada: {ConsultaId}", consultaId);
				return null;
			}

			_logger.LogDebug("Consulta encontrada: {ConsultaId} - {Identificador}", consulta.Id, consulta.Identificador);
			return consulta;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao buscar consulta: {ConsultaId}", consultaId);
			return null;
		}
	}

	public async Task<IEnumerable<Consulta>> GetConsultasAtivasAsync()
	{
		try
		{
			_logger.LogDebug("Buscando todas as consultas");
			var consultas = await _dynamoDbService.GetAllAsync<Consulta>();
		
			return consultas;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao buscar consultas ativas");
			return Enumerable.Empty<Consulta>();
		}
	}

	public async Task<string> ProcessarConsultaComParametrosAsync(string sqlOriginal, List<VerificacaoParametroValor> valoresParametros)
	{
		try
		{
			_logger.LogDebug("Processando consulta com parâmetros. SQL original: {SqlOriginal}", sqlOriginal);
			_logger.LogDebug("ValoresParametros: {Valores}", string.Join(", ", valoresParametros.Select(v => $"{v.IdParametro}={v.ValorParametro}")));

			var sqlProcessado = sqlOriginal;

			// Para cada valor informado na verificação, buscar o Parametro (para obter Alias e Tipos) e substituir
			foreach (var valorParametro in valoresParametros)
			{
				if (string.IsNullOrWhiteSpace(valorParametro.IdParametro))
				{
					continue;
				}

				var parametro = await _dynamoDbService.GetAsync<Parametro>(valorParametro.IdParametro);
				if (parametro == null)
				{
					_logger.LogWarning("Parametro não encontrado: {ParametroId}", valorParametro.IdParametro);
					continue;
				}

				var alias = parametro.Alias?.Trim();
				if (string.IsNullOrWhiteSpace(alias))
				{
					_logger.LogWarning("Parametro sem alias: {ParametroId}", parametro.Id);
					continue;
				}

				var padrao = $"@{Regex.Escape(alias)}";
				var valorSubstituicao = FormatarValorParametroPorTipoCodigo(valorParametro.ValorParametro, parametro.TipoValor);

				// Substituição global e case-insensitive
				sqlProcessado = Regex.Replace(sqlProcessado, padrao, valorSubstituicao, RegexOptions.IgnoreCase);

				_logger.LogDebug("Substituído {Padrao} por {Valor} (ParametroId={ParametroId}, Alias={Alias})", padrao, valorSubstituicao, parametro.Id, alias);
			}

			_logger.LogInformation("Consulta processada com sucesso.");
			_logger.LogDebug("SQL processado: {SqlProcessado}", sqlProcessado);

			return sqlProcessado;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao processar consulta com parâmetros");
			return sqlOriginal; // Retorna SQL original em caso de erro
		}
	}

	private string FormatarValorParametroPorTipoCodigo(string valor, int tipoValor)
	{
		// Mapeamento básico: 1=string, 2=number, 3=date, 4=boolean (ajuste conforme regra real)
		switch (tipoValor)
		{
			case 2:
				return valor; // número
			case 3:
				return $"'{valor}'"; // data/datetime
			case 4:
				return valor.Equals("true", StringComparison.OrdinalIgnoreCase) ? "1" : (valor.Equals("false", StringComparison.OrdinalIgnoreCase) ? "0" : valor);
			case 1:
			default:
				return $"'{valor.Replace("'", "''")}'"; // string/text por padrão
		}
	}
}
