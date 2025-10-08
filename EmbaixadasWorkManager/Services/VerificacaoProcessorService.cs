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

	public async Task<int> ProcessarVerificacaoAsync(Execucao execucao, string verificacaoId)
	{
		
		try
		{
			_logger.LogInformation("Processando verificação: {VerificacaoId} para execução {ExecucaoId}", verificacaoId, execucao.Id);

			// Carregar verificação
			var verificacao = await _dynamoDbService.GetAsync<Verificacao>(verificacaoId);
			if (verificacao == null)
			{
				_logger.LogWarning("Validação não encontrada: {VerificacaoId}", verificacaoId);
				return 0;
			}

			// Buscar consulta relacionada usando o serviço de consultas
			var consulta = await _consultaService.GetConsultaAsync(verificacao.IdConsulta);
			if (consulta == null)
			{
				_logger.LogWarning("Consulta não encontrada para verificação: {VerificacaoId}, IdConsulta: {IdConsulta}", 
					verificacaoId, verificacao.IdConsulta);
				return 0;
			}

			_logger.LogInformation("Processando consulta: {IdConsulta} - {Identificador}", consulta.Id, consulta.Identificador);

			// Processar consulta com substituição de parâmetros (verificação + execução)
			var sqlProcessado = await _consultaService.ProcessarConsultaComParametrosAsync(
				consulta.QuerySql, 
				verificacao.ValoresParametros,
				execucao.ParametrosExecucao);

			_logger.LogInformation("SQL processado com parâmetros: {SqlProcessado}", sqlProcessado);

			// NOVA ARQUITETURA: Criar/atualizar ExecucaoVerificacao
			var execucaoVerificacao = await CriarOuAtualizarExecucaoVerificacaoAsync(
				execucao, verificacao, consulta, sqlProcessado);

			if (execucaoVerificacao == null)
			{
				_logger.LogError("Falha ao criar ExecucaoVerificacao para: {ExecucaoId}#{VerificacaoId}", execucao.Id, verificacao.Id);
				return 0;
			}

			var id = execucaoVerificacao.Id;

			// Enviar APENAS o ID para a fila (nova arquitetura)
			var enviada = await EnviarQueryExecutionAsync(id);
			
			if (enviada)
			{
				_logger.LogDebug("Query execution enviada para fila: {id}", id);
				return 1;
			}
			else
			{
				_logger.LogError("Falha ao enviar query execution: {id}", id);
				return 0;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao processar verificação: {id}", verificacaoId);
			return 0;
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
