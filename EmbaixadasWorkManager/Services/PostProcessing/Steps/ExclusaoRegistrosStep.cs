using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services.PostProcessing.Steps;

/// <summary>
/// Step de pós-processamento que realiza exclusão de registros antigos.
/// Exemplo de step adicional que pode ser habilitado/desabilitado via configuração.
/// 
/// Este step pode ser expandido para:
/// - Excluir registros temporários
/// - Limpar dados de cache
/// - Arquivar registros antigos
/// </summary>
public class ExclusaoRegistrosStep : IPostProcessingStep
{
    private readonly ILogger<ExclusaoRegistrosStep> _logger;
    private readonly IDynamoDbService _dynamoDbService;
    private readonly ExclusaoRegistrosStepConfiguration _config;

    public string StepName => "ExclusaoRegistros";
    public int Order => _config.Order;
    public bool IsEnabled => _config.Enabled;

    public ExclusaoRegistrosStep(
        ILogger<ExclusaoRegistrosStep> logger,
        IDynamoDbService dynamoDbService,
        IOptions<PostProcessingConfiguration> config)
    {
        _logger = logger;
        _dynamoDbService = dynamoDbService;
        _config = config.Value.ExclusaoRegistros;
    }

    public async Task<bool> CanExecuteAsync(PostProcessingContext context)
    {
        // Este step pode executar independente do status da execução
        return true;
    }

    public async Task<PostProcessingStepResult> ExecuteAsync(PostProcessingContext context)
    {
        try
        {
            _logger.LogInformation(
                "Iniciando exclusao de registros antigos para execucao {ExecucaoId}. " +
                "Idade minima: {MinimumAgeInDays} dias",
                context.Execucao.Id,
                _config.MinimumAgeInDays);

            // Calcular data de corte
            var dataCorte = DateTime.UtcNow.AddDays(-_config.MinimumAgeInDays);

            // Aqui você implementaria a lógica de exclusão
            // Por exemplo:
            // - Buscar registros temporários antigos
            // - Excluir do DynamoDB
            // - Limpar cache

            var registrosExcluidos = 0; // Placeholder

            var message = $"Exclusao concluida. {registrosExcluidos} registros excluidos (mais antigos que {dataCorte:yyyy-MM-dd})";

            _logger.LogInformation(message);

            var data = new Dictionary<string, object>
            {
                { "RegistrosExcluidos", registrosExcluidos },
                { "DataCorte", dataCorte }
            };

            return PostProcessingStepResult.Ok(message, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar exclusao de registros para execucao {ExecucaoId}",
                context.Execucao.Id);
            return PostProcessingStepResult.Fail($"Erro na exclusao: {ex.Message}");
        }
    }
}



