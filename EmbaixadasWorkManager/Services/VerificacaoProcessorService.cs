using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using EmbaixadasWorkManager.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace EmbaixadasWorkManager.Services;

public class VerificacaoProcessorService : IVerificacaoProcessorService
{
	private readonly ILogger<VerificacaoProcessorService> _logger;
	private readonly IDynamoDbService _dynamoDbService;
	private readonly IConsultaService _consultaService;
	private readonly IResilientSqsService _resilientSqsService;
	private readonly SqsConfiguration _sqsConfiguration;
	private readonly IDatabaseCountService _databaseCountService;
	private readonly ProcessamentoConfiguration _processamentoConfig;
	private readonly string _dbConnectionString;

	public VerificacaoProcessorService(
		ILogger<VerificacaoProcessorService> logger,
		IDynamoDbService dynamoDbService,
		IConsultaService consultaService,
		IResilientSqsService resilientSqsService,
		SqsConfiguration sqsConfiguration,
		IDatabaseCountService databaseCountService,
		IOptions<ProcessamentoConfiguration> processamentoConfig,
		IConfiguration configuration)
	{
		_logger = logger;
		_dynamoDbService = dynamoDbService;
		_consultaService = consultaService;
		_resilientSqsService = resilientSqsService;
		_sqsConfiguration = sqsConfiguration;
		_databaseCountService = databaseCountService;
		_processamentoConfig = processamentoConfig.Value;
		_dbConnectionString = configuration["DB_CONNECTION_STRING"] ?? string.Empty;
	}

	public async Task<(int queriesEnviadas, int totalRegistros, ErroExecucao? erro)> ProcessarVerificacaoAsync(Execucao execucao, string verificacaoId)
	{
		_logger.LogInformation("=== INICIO Processamento Verificacao: {VerificacaoId} para Execucao: {ExecucaoId} ===", verificacaoId, execucao.Id);
		
		Verificacao? verificacao = null;
		Consulta? consulta = null;
		
		try
		{
			// Carregar verificação
			_logger.LogDebug("ETAPA 1/5: Buscando Verificacao no DynamoDB. VerificacaoId: {VerificacaoId}", verificacaoId);
			verificacao = await _dynamoDbService.GetAsync<Verificacao>(verificacaoId);
			if (verificacao == null)
			{
				_logger.LogWarning("ETAPA 1/5: FALHA - Validação não encontrada no DynamoDB: {VerificacaoId}", verificacaoId);
				var erro = new ErroExecucao
				{
					VerificacaoId = verificacaoId,
					ErrorCode = "VERIFICACAO_NAO_ENCONTRADA",
					Message = $"Verificação com ID {verificacaoId} não encontrada no DynamoDB.",
					OccurredAt = DateTime.UtcNow
				};
				return (0, 0, erro);
			}
			_logger.LogDebug("ETAPA 1/5: SUCESSO - Verificacao encontrada: {NomeVerificacao}", verificacao.NomeVerificacao);

			// Buscar consulta relacionada usando o serviço de consultas
			_logger.LogDebug("ETAPA 2/5: Buscando Consulta no DynamoDB. IdConsulta: {IdConsulta}", verificacao.IdConsulta);
			consulta = await _consultaService.GetConsultaAsync(verificacao.IdConsulta);
			if (consulta == null)
			{
				_logger.LogWarning("ETAPA 2/5: FALHA - Consulta não encontrada. VerificacaoId: {VerificacaoId}, IdConsulta: {IdConsulta}", 
					verificacaoId, verificacao.IdConsulta);
				var erro = new ErroExecucao
				{
					VerificacaoId = verificacaoId,
					NomeVerificacao = verificacao.NomeVerificacao,
					ErrorCode = "CONSULTA_NAO_ENCONTRADA",
					Message = $"Consulta com ID {verificacao.IdConsulta} não encontrada no DynamoDB para a Verificação '{verificacao.NomeVerificacao}' ({verificacaoId}).",
					OccurredAt = DateTime.UtcNow
				};
				return (0, 0, erro);
			}
			_logger.LogDebug("ETAPA 2/5: SUCESSO - Consulta encontrada: {IdConsulta} - {Identificador}", consulta.Id, consulta.Identificador);

			// Processar consulta com substituição de parâmetros (verificação + execução)
			_logger.LogDebug("ETAPA 3/5: Processando SQL com substituicao de parametros");
			var sqlProcessado = await _consultaService.ProcessarConsultaComParametrosAsync(
				consulta.QuerySql, 
				verificacao.ValoresParametros,
				execucao.ParametrosExecucao);

			_logger.LogDebug("ETAPA 3/5: SUCESSO - SQL processado: {SqlProcessado}", sqlProcessado);

			// --- LÓGICA DE VOLUMETRIA E SHARDING ---
			_logger.LogInformation("ETAPA 4/5: Analisando volumetria para decidir Sharding. Verificacao: {VerificacaoId}", verificacaoId);
			
			int totalRegistros = 0;
			try
			{
				totalRegistros = await _databaseCountService.GetTotalCountAsync(sqlProcessado, "PostgreSQL", _dbConnectionString, verificacao.ValoresParametros.ToDictionary(p => p.IdParametro, p => (object)p.ValorParametro));
				_logger.LogInformation("Volumetria detectada: {TotalRegistros} registros", totalRegistros);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Falha ao obter contagem para sharding. Seguindo com mensagem única (sem sharding).");
				totalRegistros = -1; // Sinaliza que não foi possível contar
			}

			var shardSize = _processamentoConfig.ShardSize;
			var queriesEnviadasComSucesso = 0;

			// ETAPA 5/5: Criar metadados e enviar para as filas
			if (totalRegistros > shardSize)
			{
				var numShards = (int)Math.Ceiling((double)totalRegistros / shardSize);
				_logger.LogInformation("SHARDING ATIVADO: Dividindo {Total} registros em {NumShards} lotes de {Size}", totalRegistros, numShards, shardSize);

				for (int i = 0; i < numShards; i++)
				{
					var offset = i * shardSize;
					var limit = shardSize;
					
					// Criar um registro único de metadados para ESTE shard
					var shardMetadata = await CriarOuAtualizarExecucaoVerificacaoAsync(
						execucao, verificacao, consulta, sqlProcessado, offset, limit, i);

					if (shardMetadata != null)
					{
						var enviadoShard = await EnviarQueryExecutionAsync(shardMetadata.Id, offset, limit);
						if (enviadoShard) queriesEnviadasComSucesso++;
					}
				}
			}
			else
			{
				// Envio normal (sem sharding ou count falhou)
				_logger.LogInformation("SHARDING DESATIVADO: Criando metadados e enviando mensagem única.");
				
				var execucaoVerificacao = await CriarOuAtualizarExecucaoVerificacaoAsync(
					execucao, verificacao, consulta, sqlProcessado);

				if (execucaoVerificacao != null)
				{
					var enviada = await EnviarQueryExecutionAsync(execucaoVerificacao.Id);
					if (enviada) queriesEnviadasComSucesso = 1;
				}
			}
			
			if (queriesEnviadasComSucesso > 0)
			{
				_logger.LogInformation("=== SUCESSO TOTAL - Verificacao {VerificacaoId} processada. Mensagens enviadas: {Count} ===", verificacaoId, queriesEnviadasComSucesso);
				
				return (queriesEnviadasComSucesso, totalRegistros, null);
			}
			else
			{
				_logger.LogError("=== FALHA - Nenhuma mensagem enviada para fila SQS. Verificacao: {VerificacaoId} ===", verificacaoId);
				var erro = new ErroExecucao
				{
					VerificacaoId = verificacaoId,
					NomeVerificacao = verificacao.NomeVerificacao,
					IdentificadorConsulta = consulta.Identificador,
					ErrorCode = "ENVIO_SQS_FALHOU",
					Message = $"Falha ao enviar mensagens SQS (com ou sem sharding). Verificação '{verificacao.NomeVerificacao}' ({verificacaoId}).",
					OccurredAt = DateTime.UtcNow
				};
				return (0, 0, erro);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "=== EXCEPTION - Erro ao processar verificacao: {VerificacaoId}. Mensagem: {Message} ===", verificacaoId, ex.Message);
			var erro = new ErroExecucao
			{
				VerificacaoId = verificacaoId,
				NomeVerificacao = verificacao?.NomeVerificacao ?? string.Empty,
				IdentificadorConsulta = consulta?.Identificador ?? string.Empty,
				ErrorCode = "EXCEPTION",
				Message = $"Exceção ao processar verificação: {ex.Message}",
				OccurredAt = DateTime.UtcNow
			};
			return (0, 0, erro);
		}
	}

	private async Task<ExecucaoVerificacao?> CriarOuAtualizarExecucaoVerificacaoAsync(
		Execucao execucao, Verificacao verificacao, Consulta consulta, string sqlProcessado, 
		int? offset = null, int? limit = null, int? shardIndex = null)
	{
		try
		{
			var execucaoVerificacao = new ExecucaoVerificacao(execucao.Id, verificacao.Id)
			{
				QueryId = consulta.Id,
				Base = execucao.Base,
				DataBase = execucao.DataBase,
				Empresa = execucao.Empresa,
				Usuario = execucao.Usuario,
				Sql = sqlProcessado,
				SqlOriginal = consulta.QuerySql,
				Parametros = consulta.Parametros,
				ValoresParametros = verificacao.ValoresParametros,
				TimeoutSegundos = consulta.TimeoutSegundos,
				Prioridade = consulta.Prioridade,
				Status = StatusExecucaoVerificacao.Pendente,
				Offset = offset,
				Limit = limit,
				TotalRegistrosEstimados = limit ?? 0,
				// Novos campos da Verificação
				TipoApontamento = verificacao.IdTipo,
				Nivel = verificacao.Nivel,
				IdEmbaixadas = verificacao.IdEmbaixadas,
				Metadata = new Dictionary<string, string>
				{
					["verificacaoNome"] = verificacao.NomeVerificacao,
					["queryNome"] = consulta.Identificador,
					["origem"] = "verificacao-processor",
					["sqlOriginal"] = consulta.QuerySql,
					["parametrosSubstituidos"] = string.Join(", ", verificacao.ValoresParametros.Select(p => $"{p.IdParametro}={p.ValorParametro}"))
				}
			};

			if (shardIndex.HasValue)
			{
				execucaoVerificacao.Metadata["shardIndex"] = shardIndex.Value.ToString();
				execucaoVerificacao.Metadata["offset"] = offset?.ToString() ?? "0";
				execucaoVerificacao.Metadata["limit"] = limit?.ToString() ?? "0";
			}

			await _dynamoDbService.SaveAsync(execucaoVerificacao);
			_logger.LogInformation("ExecucaoVerificacao criada/atualizada: {ExecucaoId}#{VerificacaoId}", 
				execucaoVerificacao.ExecucaoId, execucaoVerificacao.VerificacaoId);

			return execucaoVerificacao;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao criar/atualizar ExecucaoVerificacao para verificação: {VerificacaoId}", verificacao.Id);
			return null;
		}
	}

	private async Task<bool> EnviarQueryExecutionAsync(string verificacaoId, int? offset = null, int? limit = null)
	{
		try
		{
			// Criar mensagem leve com apenas o ID e parâmetros de sharding
			var mensagem = new QueryExecutionMessage
			{
				ExecucaoVerificacaoId = verificacaoId,
				Offset = offset,
				Limit = limit,
				Timestamp = DateTime.UtcNow
			};

			var body = System.Text.Json.JsonSerializer.Serialize(mensagem);
			
			if (offset.HasValue)
			{
				_logger.LogInformation("Enviando SHARD para fila: {VerificacaoId} (Offset: {Offset}, Limit: {Limit})", 
					verificacaoId, offset, limit);
			}
			else
			{
				_logger.LogInformation("Enviando query execution normal para fila: {VerificacaoId}", verificacaoId);
			}

			// Obter URL da fila de query
			var queueUrl = await _resilientSqsService.GetQueueUrlAsync(_sqsConfiguration.FilaExecucaoQuery);
			if (string.IsNullOrEmpty(queueUrl))
			{
				_logger.LogError("Não foi possível obter a URL da fila de query: {FilaQuery}", _sqsConfiguration.FilaExecucaoQuery);
				return false;
			}

			// Enviar mensagem para a fila de queries usando o serviço resiliente
			var enviado = await _resilientSqsService.SendMessageAsync(queueUrl, body);
			
			return enviado;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao enviar query execution: {VerificacaoId}", verificacaoId);
			return false;
		}
	}
}
