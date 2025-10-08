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

	public async Task<string> ProcessarConsultaComParametrosAsync(string sqlOriginal, List<VerificacaoParametroValor> valoresParametros, List<ParametroExecucao>? parametrosExecucao = null)
	{
		try
		{
			_logger.LogDebug("Processando consulta com parâmetros. SQL original: {SqlOriginal}", sqlOriginal);
			_logger.LogDebug("ValoresParametros: {Valores}", string.Join(", ", valoresParametros.Select(v => $"{v.IdParametro}={v.ValorParametro}")));
			
			if (parametrosExecucao != null && parametrosExecucao.Any())
			{
				_logger.LogDebug("ParametrosExecucao: {Parametros}", string.Join(", ", parametrosExecucao.Select(p => $"{p.Alias}={p.Valor}")));
			}

			var sqlProcessado = sqlOriginal;

			// PRIMEIRO: Processar parâmetros da EXECUÇÃO (mais específicos, têm prioridade)
			if (parametrosExecucao != null)
			{
				foreach (var parametroExec in parametrosExecucao)
				{
					if (string.IsNullOrWhiteSpace(parametroExec.Alias))
					{
						continue;
					}

					var alias = parametroExec.Alias.Trim();
					var padrao = $"@{Regex.Escape(alias)}";
					
					// Determinar formatação baseada no contexto SQL
					var valorSubstituicao = FormatarParametroComContexto(sqlProcessado, alias, parametroExec.Valor);

					// Substituição global e case-insensitive
					sqlProcessado = Regex.Replace(sqlProcessado, padrao, valorSubstituicao, RegexOptions.IgnoreCase);

					var contexto = DeterminarContextoParametro(sqlProcessado, alias) ? "identifier" : "value";
					_logger.LogDebug("Substituído parâmetro da execução {Padrao} por {Valor} (Alias={Alias}, Contexto={Contexto})", 
						padrao, valorSubstituicao, alias, contexto);
				}
			}

			// SEGUNDO: Processar parâmetros da VERIFICAÇÃO (busca na tabela Parametro)
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
				
				// Verificar se o parâmetro já foi substituído pelos parâmetros da execução
				if (!sqlProcessado.Contains($"@{alias}"))
				{
					_logger.LogDebug("Parâmetro {Alias} já foi substituído por parâmetro da execução", alias);
					continue;
				}

				var valorSubstituicao = FormatarValorParametroPorTipoCodigo(valorParametro.ValorParametro, parametro.TipoValor);

				// Substituição global e case-insensitive
				sqlProcessado = Regex.Replace(sqlProcessado, padrao, valorSubstituicao, RegexOptions.IgnoreCase);

				_logger.LogDebug("Substituído parâmetro da verificação {Padrao} por {Valor} (ParametroId={ParametroId}, Alias={Alias})", padrao, valorSubstituicao, parametro.Id, alias);
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

	private string FormatarParametroComContexto(string sql, string alias, string valor)
	{
		var ehIdentificador = DeterminarContextoParametro(sql, alias);
		
		if (ehIdentificador)
		{
			// Identificador SQL (schema, tabela, coluna) - sem aspas
			return valor;
		}
		else
		{
			// Valor SQL - com aspas e escape
			return $"'{valor.Replace("'", "''")}'";
		}
	}

	private bool DeterminarContextoParametro(string sql, string alias)
	{
		var padrao = $"@{Regex.Escape(alias)}";
		
		// Encontrar todas as ocorrências do parâmetro
		var matches = Regex.Matches(sql, padrao, RegexOptions.IgnoreCase);
		
		foreach (Match match in matches)
		{
			var posicao = match.Index;
			
			// Analisar o contexto antes e depois do parâmetro
			if (EhContextoDeValor(sql, posicao, padrao.Length))
			{
				return false; // É um valor (precisa de aspas)
			}
		}
		
		return true; // É um identificador (sem aspas)
	}

	private bool EhContextoDeValor(string sql, int posicaoParametro, int tamanhoParametro)
	{
		// Operadores que indicam que o parâmetro seguinte é um VALOR
		var operadoresDeComparacao = new[]
		{
			"=", "!=", "<>", "<", ">", "<=", ">=",
			"LIKE", "NOT LIKE", "ILIKE", "NOT ILIKE",
			"IN", "NOT IN",
			"BETWEEN", "NOT BETWEEN"
		};
		
		// Pegar contexto antes do parâmetro (últimas 50 caracteres)
		var inicioContexto = Math.Max(0, posicaoParametro - 50);
		var contextoAnterior = sql.Substring(inicioContexto, posicaoParametro - inicioContexto);
		
		_logger.LogDebug("Analisando contexto: '{ContextoAnterior}' na posição {Posicao}", 
			contextoAnterior, posicaoParametro);
		
		// Verificar se há um operador de comparação antes do parâmetro
		foreach (var operador in operadoresDeComparacao)
		{
			// Para operadores com letras (LIKE, IN, etc.) usar word boundary
			// Para operadores simbólicos (=, !=, etc.) usar espaço ou início
			string padrao;
			if (Regex.IsMatch(operador, @"^[A-Z]"))
			{
				// Operadores com letras: LIKE, IN, BETWEEN, etc.
				padrao = $@"\b{Regex.Escape(operador)}\s*$";
			}
			else
			{
				// Operadores simbólicos: =, !=, <>, <, >, etc.
				padrao = $@"{Regex.Escape(operador)}\s*$";
			}
			
			if (Regex.IsMatch(contextoAnterior, padrao, RegexOptions.IgnoreCase))
			{
				_logger.LogDebug("Operador '{Operador}' encontrado com padrão '{Padrao}' - É VALOR", 
					operador, padrao);
				return true; // Encontrou operador de comparação = é um valor
			}
		}
		
		// Verificar se está em uma cláusula VALUES
		if (Regex.IsMatch(contextoAnterior, @"\bVALUES\s*\([^)]*$", RegexOptions.IgnoreCase))
		{
			return true; // Está em VALUES(...) = é um valor
		}
		
		// Verificar se está após vírgula em INSERT/UPDATE
		if (Regex.IsMatch(contextoAnterior, @",\s*$"))
		{
			// Pode ser valor em INSERT VALUES ou lista de colunas
			// Se está após VALUES, é valor; senão pode ser identificador
			var temValues = Regex.IsMatch(sql.Substring(0, posicaoParametro), @"\bVALUES\b", RegexOptions.IgnoreCase);
			return temValues;
		}
		
		_logger.LogDebug("Nenhum operador encontrado - É IDENTIFICADOR");
		return false; // Não encontrou contexto de valor = é identificador
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
