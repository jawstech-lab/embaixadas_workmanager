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

				// IMPORTANTE: Usar FormatarParametroComContexto para detectar se é identificador ou valor
				// Isso garante que schemas/tabelas não recebam aspas simples
				var valorSubstituicao = FormatarParametroComContexto(sqlProcessado, alias, valorParametro.ValorParametro);

				// Substituição global e case-insensitive
				sqlProcessado = Regex.Replace(sqlProcessado, padrao, valorSubstituicao, RegexOptions.IgnoreCase);

				var contexto = DeterminarContextoParametro(sqlProcessado, alias) ? "identifier" : "value";
				_logger.LogDebug("Substituído parâmetro da verificação {Padrao} por {Valor} (ParametroId={ParametroId}, Alias={Alias}, Contexto={Contexto})", padrao, valorSubstituicao, parametro.Id, alias, contexto);
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
			var tamanhoMatch = match.Length; // Usar o tamanho real do match
			
			// PRIMEIRO: Verificar se está em contexto de identificador (schema, tabela, coluna)
			if (EhContextoDeIdentificador(sql, posicao, tamanhoMatch, padrao))
			{
				return true; // É um identificador (sem aspas)
			}
			
			// SEGUNDO: Verificar se está em contexto de valor
			if (EhContextoDeValor(sql, posicao, tamanhoMatch))
			{
				return false; // É um valor (precisa de aspas)
			}
		}
		
		// Por padrão, se não detectar contexto claro, assumir que é identificador
		// (mais seguro para schemas, tabelas e colunas)
		return true; // É um identificador (sem aspas)
	}

	private bool EhContextoDeIdentificador(string sql, int posicaoParametro, int tamanhoParametro, string padraoRegex)
	{
		// Palavras-chave SQL que indicam que o próximo token é um identificador (schema, tabela, coluna)
		var palavrasChaveIdentificador = new[]
		{
			"FROM", "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN",
			"INTO", "UPDATE", "TABLE", "SCHEMA", "DATABASE", "INDEX",
			"ON", "USING", "SET", "AS", "ALIAS"
		};
		
		// Pegar contexto antes do parâmetro (últimas 50 caracteres)
		var inicioContexto = Math.Max(0, posicaoParametro - 50);
		var contextoAnterior = sql.Substring(inicioContexto, posicaoParametro - inicioContexto);
		
		// Verificar se há uma palavra-chave de identificador antes do parâmetro
		foreach (var palavraChave in palavrasChaveIdentificador)
		{
			var padrao = $@"\b{Regex.Escape(palavraChave)}\s+[^@]*$";
			if (Regex.IsMatch(contextoAnterior, padrao, RegexOptions.IgnoreCase))
			{
				_logger.LogDebug("Palavra-chave de identificador '{PalavraChave}' encontrada - É IDENTIFICADOR", palavraChave);
				return true; // É um identificador
			}
		}
		
		// Verificar se o parâmetro está ANTES de um ponto (schema.tabela ou tabela.coluna)
		// tamanhoParametro já é o tamanho real do match
		var posicaoDepois = posicaoParametro + tamanhoParametro;
		if (posicaoDepois < sql.Length && sql[posicaoDepois] == '.')
		{
			_logger.LogDebug("Parâmetro seguido por ponto (.) - É IDENTIFICADOR (schema/tabela). Posição: {Posicao}, Tamanho: {Tamanho}, Posição depois: {PosicaoDepois}, Caractere: '{Caractere}'", 
				posicaoParametro, tamanhoParametro, posicaoDepois, posicaoDepois < sql.Length ? sql[posicaoDepois].ToString() : "EOF");
			return true; // É um identificador (schema ou tabela)
		}
		
		// Verificar se o parâmetro está DEPOIS de um ponto (schema.tabela ou tabela.coluna)
		if (posicaoParametro > 0 && sql[posicaoParametro - 1] == '.')
		{
			_logger.LogDebug("Parâmetro precedido por ponto (.) - É IDENTIFICADOR (tabela/coluna)");
			return true; // É um identificador (tabela ou coluna)
		}
		
		return false; // Não é claramente um identificador
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
