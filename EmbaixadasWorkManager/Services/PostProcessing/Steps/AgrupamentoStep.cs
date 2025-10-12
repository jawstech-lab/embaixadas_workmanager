using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services.PostProcessing.Steps;

/// <summary>
/// Step de pós-processamento que realiza agrupamento de dados da execução.
/// Exemplo concreto de implementação de IPostProcessingStep.
/// 
/// Este step pode ser expandido para:
/// - Agrupar resultados por critérios específicos
/// - Consolidar métricas
/// - Gerar estatísticas agregadas
/// </summary>
public class AgrupamentoStep : IPostProcessingStep
{
    private readonly ILogger<AgrupamentoStep> _logger;
    private readonly IDynamoDbService _dynamoDbService;
    private readonly AgrupamentoStepConfiguration _config;

    public string StepName => "Agrupamento";
    public int Order => _config.Order;
    public bool IsEnabled => _config.Enabled;

    public AgrupamentoStep(
        ILogger<AgrupamentoStep> logger,
        IDynamoDbService dynamoDbService,
        IOptions<PostProcessingConfiguration> config)
    {
        _logger = logger;
        _dynamoDbService = dynamoDbService;
        _config = config.Value.Agrupamento;
    }

    public async Task<bool> CanExecuteAsync(PostProcessingContext context)
    {
        // Só executa se a execução foi finalizada com sucesso
        if (!context.FinalizadaComSucesso)
        {
            _logger.LogInformation("Agrupamento sera pulado pois execucao finalizou com erro");
            return false;
        }

        // Só executa se tem apontamentos para agrupar
        if (context.Execucao.TotalApontamentos == 0)
        {
            _logger.LogInformation("Agrupamento sera pulado pois nao ha apontamentos para agrupar");
            return false;
        }

        return true;
    }

    public async Task<PostProcessingStepResult> ExecuteAsync(PostProcessingContext context)
    {
        try
        {
            _logger.LogInformation(
                "Iniciando agrupamento de dados para execucao {ExecucaoId}. " +
                "Total de apontamentos: {TotalApontamentos}",
                context.Execucao.Id,
                context.Execucao.TotalApontamentos);

            // Buscar todas as ExecucaoVerificacao da execução
            var verificacoes = await BuscarVerificacoesDaExecucaoAsync(context.Execucao.Id);

            if (!verificacoes.Any())
            {
                return PostProcessingStepResult.Fail("Nenhuma verificacao encontrada para agrupar");
            }

            _logger.LogInformation("Encontradas {Count} verificacoes para agrupar", verificacoes.Count);

            // Agrupar dados
            var agrupamento = RealizarAgrupamento(verificacoes, context);

            // Adicionar resultado ao contexto
            var data = new Dictionary<string, object>
            {
                { "TotalVerificacoes", verificacoes.Count },
                { "TotalApontamentos", context.Execucao.TotalApontamentos },
                { "GruposGerados", agrupamento.Count },
                { "Agrupamento", agrupamento }
            };

            // Salvar resultado se configurado
            if (_config.SaveResult)
            {
                await SalvarResultadoAgrupamentoAsync(context.Execucao.Id, agrupamento);
            }

            var message = $"Agrupamento concluido. {verificacoes.Count} verificacoes agrupadas em {agrupamento.Count} grupos. " +
                         $"Total de apontamentos: {context.Execucao.TotalApontamentos}";

            _logger.LogInformation(message);

            return PostProcessingStepResult.Ok(message, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar agrupamento para execucao {ExecucaoId}",
                context.Execucao.Id);
            return PostProcessingStepResult.Fail($"Erro no agrupamento: {ex.Message}");
        }
    }

    private async Task<List<ExecucaoVerificacao>> BuscarVerificacoesDaExecucaoAsync(string execucaoId)
    {
        try
        {
            // Buscar todas as ExecucaoVerificacao que pertencem a esta execução
            // Isso pode ser otimizado com um índice secundário no DynamoDB
            var todasVerificacoes = await _dynamoDbService.GetAllAsync<ExecucaoVerificacao>();
            
            var verificacoesDaExecucao = todasVerificacoes
                .Where(v => v.ExecucaoId == execucaoId)
                .ToList();

            _logger.LogDebug("Buscadas {Total} ExecucaoVerificacao para execucao {ExecucaoId}",
                verificacoesDaExecucao.Count, execucaoId);

            return verificacoesDaExecucao;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar verificacoes da execucao {ExecucaoId}", execucaoId);
            return new List<ExecucaoVerificacao>();
        }
    }

    private Dictionary<string, AgrupamentoInfo> RealizarAgrupamento(
        List<ExecucaoVerificacao> verificacoes,
        PostProcessingContext context)
    {
        var agrupamento = new Dictionary<string, AgrupamentoInfo>();

        try
        {
            // Agrupar por Status
            var porStatus = verificacoes
                .GroupBy(v => v.Status)
                .Select(g => new
                {
                    Chave = $"Status:{g.Key}",
                    Info = new AgrupamentoInfo
                    {
                        Criterio = "Status",
                        Valor = g.Key,
                        Quantidade = g.Count(),
                        TotalRegistros = g.Sum(v => v.TotalRecordsProcessados),
                        Verificacoes = g.Select(v => v.Id).ToList()
                    }
                });

            foreach (var grupo in porStatus)
            {
                agrupamento[grupo.Chave] = grupo.Info;
            }

            // Agrupar por Empresa (se configurado)
            if (_config.GroupByFields.Contains("Empresa"))
            {
                var porEmpresa = verificacoes
                    .GroupBy(v => v.Empresa)
                    .Select(g => new
                    {
                        Chave = $"Empresa:{g.Key}",
                        Info = new AgrupamentoInfo
                        {
                            Criterio = "Empresa",
                            Valor = g.Key,
                            Quantidade = g.Count(),
                            TotalRegistros = g.Sum(v => v.TotalRecordsProcessados),
                            Verificacoes = g.Select(v => v.Id).ToList()
                        }
                    });

                foreach (var grupo in porEmpresa)
                {
                    agrupamento[grupo.Chave] = grupo.Info;
                }
            }

            _logger.LogDebug("Agrupamento gerado com {Count} grupos", agrupamento.Count);

            return agrupamento;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao realizar agrupamento");
            return agrupamento;
        }
    }

    private async Task SalvarResultadoAgrupamentoAsync(
        string execucaoId,
        Dictionary<string, AgrupamentoInfo> agrupamento)
    {
        try
        {
            // Aqui você pode implementar a lógica de salvamento
            // Por exemplo:
            // - Salvar em uma tabela DynamoDB de resultados
            // - Salvar em arquivo S3
            // - Enviar para API externa
            
            _logger.LogInformation(
                "Resultado do agrupamento salvo para execucao {ExecucaoId}. {Count} grupos salvos",
                execucaoId, agrupamento.Count);

            // Implementação futura
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar resultado do agrupamento");
            throw;
        }
    }
}

/// <summary>
/// Informações de um grupo no agrupamento
/// </summary>
public class AgrupamentoInfo
{
    public string Criterio { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public int TotalRegistros { get; set; }
    public List<string> Verificacoes { get; set; } = new();
}



