using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using EmbaixadasWorkManager.Configuration;

namespace EmbaixadasWorkManager.Services;

public class VerificacaoProcessorService : IVerificacaoProcessorService
{
	private readonly ILogger<VerificacaoProcessorService> _logger;
	private readonly IDynamoDbService _dynamoDbService;
	private readonly IConsultaService _consultaService;
	private readonly IResilientSqsService _resilientSqsService;
	private readonly SqsConfiguration _sqsConfiguration;

	public VerificacaoProcessorService(
		ILogger<VerificacaoProcessorService> logger,
		IDynamoDbService dynamoDbService,
		IConsultaService consultaService,
		IResilientSqsService resilientSqsService,
		SqsConfiguration sqsConfiguration)
	{
		_logger = logger;
		_dynamoDbService = dynamoDbService;
		_consultaService = consultaService;
		_resilientSqsService = resilientSqsService;
		_sqsConfiguration = sqsConfiguration;
	}

	public async Task<(int queriesEnviadas, ErroExecucao? erro)> ProcessarVerificacaoAsync(Execucao execucao, string verificacaoId)
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
				return (0, erro);
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
				return (0, erro);
			}
			_logger.LogDebug("ETAPA 2/5: SUCESSO - Consulta encontrada: {IdConsulta} - {Identificador}", consulta.Id, consulta.Identificador);

			// Processar consulta com substituição de parâmetros (verificação + execução)
			_logger.LogDebug("ETAPA 3/5: Processando SQL com substituicao de parametros");
			var sqlProcessado = await _consultaService.ProcessarConsultaComParametrosAsync(
				consulta.QuerySql, 
				verificacao.ValoresParametros,
				execucao.ParametrosExecucao);

			_logger.LogDebug("ETAPA 3/5: SUCESSO - SQL processado: {SqlProcessado}", sqlProcessado);

			// NOVA ARQUITETURA: Criar/atualizar ExecucaoVerificacao
			_logger.LogDebug("ETAPA 4/5: Criando/Atualizando ExecucaoVerificacao no DynamoDB");
			var execucaoVerificacao = await CriarOuAtualizarExecucaoVerificacaoAsync(
				execucao, verificacao, consulta, sqlProcessado);

			if (execucaoVerificacao == null)
			{
				_logger.LogError("ETAPA 4/5: FALHA - Falha ao criar ExecucaoVerificacao. ExecucaoId: {ExecucaoId}, VerificacaoId: {VerificacaoId}", execucao.Id, verificacao.Id);
				var erro = new ErroExecucao
				{
					VerificacaoId = verificacaoId,
					NomeVerificacao = verificacao.NomeVerificacao,
					IdentificadorConsulta = consulta.Identificador,
					ErrorCode = "EXECUCAO_VERIFICACAO_NAO_CRIADA",
					Message = $"Falha ao criar ExecucaoVerificacao para Execucao {execucao.Id} e Verificação '{verificacao.NomeVerificacao}' ({verificacaoId}).",
					OccurredAt = DateTime.UtcNow
				};
				return (0, erro);
			}
			_logger.LogDebug("ETAPA 4/5: SUCESSO - ExecucaoVerificacao criada: {ExecucaoVerificacaoId}", execucaoVerificacao.Id);

			var id = execucaoVerificacao.Id;

			// Enviar APENAS o ID para a fila (nova arquitetura)
			_logger.LogDebug("ETAPA 5/5: Enviando QueryExecutionMessage para fila SQS");
			var enviada = await EnviarQueryExecutionAsync(id);
			
			if (enviada)
			{
				_logger.LogInformation("=== SUCESSO TOTAL - Verificacao {VerificacaoId} processada e enviada para fila ===", verificacaoId);
				return (1, null);
			}
			else
			{
				_logger.LogError("=== FALHA - Verificacao {VerificacaoId} processada mas NAO enviada para fila ===", verificacaoId);
				var erro = new ErroExecucao
				{
					VerificacaoId = verificacaoId,
					NomeVerificacao = verificacao.NomeVerificacao,
					IdentificadorConsulta = consulta.Identificador,
					ErrorCode = "ENVIO_SQS_FALHOU",
					Message = $"Falha ao enviar mensagem para a fila SQS. Verificação '{verificacao.NomeVerificacao}' ({verificacaoId}). ExecucaoVerificacaoId: {id}",
					OccurredAt = DateTime.UtcNow
				};
				return (0, erro);
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
			return (0, erro);
		}
	}

	private async Task<ExecucaoVerificacao?> CriarOuAtualizarExecucaoVerificacaoAsync(
		Execucao execucao, Verificacao verificacao, Consulta consulta, string sqlProcessado)
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

	private async Task<bool> EnviarQueryExecutionAsync(string verificacaoId)
	{
		try
		{
			// Criar mensagem leve com apenas o ID
			var mensagem = new QueryExecutionMessage
			{
				ExecucaoVerificacaoId = verificacaoId,
				Timestamp = DateTime.UtcNow
			};

			var body = System.Text.Json.JsonSerializer.Serialize(mensagem);
			_logger.LogInformation("Enviando query execution para fila: {VerificacaoId} -> {FilaQuery}", 
				verificacaoId, _sqsConfiguration.FilaExecucaoQuery);
			_logger.LogDebug("Mensagem da query execution: {Body}", body);

			// Obter URL da fila de query
			var queueUrl = await _resilientSqsService.GetQueueUrlAsync(_sqsConfiguration.FilaExecucaoQuery);
			if (string.IsNullOrEmpty(queueUrl))
			{
				_logger.LogError("Não foi possível obter a URL da fila de query: {FilaQuery}", _sqsConfiguration.FilaExecucaoQuery);
				return false;
			}

			// Enviar mensagem para a fila de queries usando o serviço resiliente
			var enviado = await _resilientSqsService.SendMessageAsync(queueUrl, body);
			
			if (enviado)
			{
				_logger.LogInformation("Query execution enviada com sucesso: {VerificacaoId}", verificacaoId);
				return true;
			}
			else
			{
				_logger.LogError("Falha ao enviar query execution: {VerificacaoId}", verificacaoId);
				return false;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao enviar query execution: {VerificacaoId}", verificacaoId);
			return false;
		}
	}
}
