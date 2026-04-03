using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EmbaixadasWorkManager.Configuration;

namespace EmbaixadasWorkManager.Services;

public class SystemHeartbeatService : BackgroundService
{
    private readonly IDynamoDbService _dynamoDbService;
    private readonly ISqsService _sqsService;
    private readonly SqsConfiguration _sqsConfig;
    private readonly ILogger<SystemHeartbeatService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(1);

    public SystemHeartbeatService(
        IDynamoDbService dynamoDbService,
        ISqsService sqsService,
        IOptions<SqsConfiguration> sqsConfig,
        ILogger<SystemHeartbeatService> logger)
    {
        _dynamoDbService = dynamoDbService;
        _sqsService = sqsService;
        _sqsConfig = sqsConfig.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SystemHeartbeatService iniciado (Intervalo: {Period})", _period);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportHealthAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no ciclo de Heartbeat do Sistema");
            }

            await Task.Delay(_period, stoppingToken);
        }
    }

    private async Task ReportHealthAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Iniciando coleta de métricas de saúde do sistema...");

        // 2. Coletar Backlog do SQS
        var queueUrlMain = await _sqsService.GetQueueUrlAsync(_sqsConfig.FilaExecucao);
        var queueUrlQuery = await _sqsService.GetQueueUrlAsync(_sqsConfig.FilaExecucaoQuery);

        int backlogMain = !string.IsNullOrEmpty(queueUrlMain) 
            ? await _sqsService.GetQueueApproximateMessageCountAsync(queueUrlMain) 
            : 0;
            
        int backlogQuery = !string.IsNullOrEmpty(queueUrlQuery) 
            ? await _sqsService.GetQueueApproximateMessageCountAsync(queueUrlQuery) 
            : 0;
            
        int totalBacklog = backlogMain + backlogQuery;

        // 3. Coletar Workers ativos (Pulsos nos últimos 5 minutos)
        var allPulses = await _dynamoDbService.GetAllAsync<WorkerPulse>();
        var activeWorkers = allPulses?.Count(p => p.LastPulse > DateTime.UtcNow.AddMinutes(-5)) ?? 0;

        // 4. Determinar Status Geral
        string status = "Operacional";
        if (activeWorkers == 0 && totalBacklog > 0)
        {
            status = "Alerta: Sem Workers ativos com mensagens na fila";
        }
        else if (totalBacklog > 1000)
        {
            status = "Sobrecarga: Backlog alto";
        }

        // 5. Salvar/Atualizar no DynamoDB
        var metrics = new SistemaStatus
        {
            LastManagerPulse = DateTime.UtcNow,
            QueueBacklog = totalBacklog,
            QueueBacklogWorkManager = backlogMain,
            QueueBacklogQueryExecutor = backlogQuery,
            ActiveWorkers = activeWorkers,
            Status = status
        };

        await _dynamoDbService.SaveAsync(metrics);

        _logger.LogInformation("Heartbeat do Sistema reportado: Status={Status}, Workers={Workers}, BacklogTotal={Backlog}, Main={Main}, Query={Query}", 
            status, activeWorkers, totalBacklog, backlogMain, backlogQuery);
    }
}
