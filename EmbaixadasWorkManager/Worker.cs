using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IQueueHealthService _queueHealthService;
    private readonly IMessageProcessorService _messageProcessorService;
    private readonly SqsConfiguration _sqsConfig;
    private readonly DynamoDbConfiguration _dynamoConfig;
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private const int HealthCheckIntervalMinutes = 5;

    public Worker(
        ILogger<Worker> logger,
        IQueueHealthService queueHealthService,
        IMessageProcessorService messageProcessorService,
        IOptions<SqsConfiguration> sqsConfig,
        IOptions<DynamoDbConfiguration> dynamoConfig)
    {
        _logger = logger;
        _queueHealthService = queueHealthService;
        _messageProcessorService = messageProcessorService;
        _sqsConfig = sqsConfig.Value;
        _dynamoConfig = dynamoConfig.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciado em: {Time}", DateTimeOffset.Now);

        // Teste inicial de conectividade
        await PerformInitialHealthCheck();

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker executando em: {Time}", DateTimeOffset.Now);
            
            try
            {
                // Verificar saúde das filas periodicamente
                if (ShouldPerformHealthCheck())
                {
                    await PerformPeriodicHealthCheck();
                }
                
                // Processar mensagens da fila de execução
                await ProcessExecutionQueue();
                
                // Processar mensagens da fila de processo
                await ProcessProcessoQueue();
                
                // Processar mensagens da fila de queries (nova arquitetura)
                //await ProcessQueryExecutionQueue();
                
                // Aguardar antes da próxima execução
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker cancelado");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante execução do Worker");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Worker finalizado em: {Time}", DateTimeOffset.Now);
    }

    private async Task PerformInitialHealthCheck()
    {
        try
        {
            _logger.LogInformation("=== Teste inicial de conectividade ===");
            
            // Verificar configurações
            _logger.LogInformation("Configurações carregadas:");
            _logger.LogInformation("- Fila Execução: {FilaExecucao}", _sqsConfig.FilaExecucao);
            _logger.LogInformation("- Fila Execução Query: {FilaExecucaoQuery}", _sqsConfig.FilaExecucaoQuery);
            _logger.LogInformation("- Fila Execução Processo: {FilaExecucaoProcesso}", _sqsConfig.FilaExecucaoProcesso);
            _logger.LogInformation("- Tabela Execução: {TableName}", _dynamoConfig.TableNameExecucao);
            _logger.LogInformation("- Tabela Verificação: {TableName}", _dynamoConfig.TableNameVerificacao);
            
            // Testar conectividade SQS
            var sqsConnected = await _queueHealthService.TestSqsConnectivityAsync();
            if (!sqsConnected)
            {
                _logger.LogError("Falha na conectividade SQS. Verifique permissões IAM.");
                return;
            }
            
            // Verificar saúde das filas
            var queuesHealthy = await _queueHealthService.CheckAllQueuesHealthAsync();
            if (queuesHealthy)
            {
                _logger.LogInformation("Todas as filas estão saudáveis");
            }
            else
            {
                _logger.LogWarning("Algumas filas não estão saudáveis");
            }
            
            _logger.LogInformation("Teste inicial concluído");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante teste inicial de conectividade");
        }
    }

    private async Task PerformPeriodicHealthCheck()
    {
        try
        {
            _logger.LogDebug("Verificando saúde das filas...");
            
            var allHealthy = await _queueHealthService.CheckAllQueuesHealthAsync();
            if (allHealthy)
            {
                _logger.LogInformation("Verificação de saúde: todas as filas estão saudáveis");
            }
            else
            {
                _logger.LogWarning("Verificação de saúde: problemas detectados em algumas filas");
            }
            
            _lastHealthCheck = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante verificação de saúde das filas");
        }
    }

    private async Task ProcessExecutionQueue()
    {
        try
        {
            var result = await _messageProcessorService.ProcessQueueMessagesAsync(_sqsConfig.FilaExecucao);
            
            if (result.Success)
            {
                if (result.ProcessedCount > 0)
                {
                    _logger.LogInformation("Processamento concluído: {ProcessedCount} mensagens processadas, {FailedCount} falharam", 
                        result.ProcessedCount, result.FailedCount);
                }
            }
            else
            {
                _logger.LogWarning("Falha no processamento: {ErrorMessage}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar fila de execução");
        }
    }

    private async Task ProcessProcessoQueue()
    {
        try
        {
            var result = await _messageProcessorService.ProcessQueueMessagesAsync(_sqsConfig.FilaExecucaoProcesso);
            
            if (result.Success)
            {
                if (result.ProcessedCount > 0)
                {
                    _logger.LogInformation("Processamento de processo concluído: {ProcessedCount} mensagens processadas, {FailedCount} falharam", 
                        result.ProcessedCount, result.FailedCount);
                }
            }
            else
            {
                _logger.LogWarning("Falha no processamento de processo: {ErrorMessage}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar fila de processo");
        }
    }

    private async Task ProcessQueryExecutionQueue()
    {
        try
        {
            var result = await _messageProcessorService.ProcessQueueMessagesAsync(_sqsConfig.FilaExecucaoQuery);
            
            if (result.Success)
            {
                if (result.ProcessedCount > 0)
                {
                    _logger.LogInformation("Processamento de query execution concluído: {ProcessedCount} mensagens processadas, {FailedCount} falharam", 
                        result.ProcessedCount, result.FailedCount);
                }
            }
            else
            {
                _logger.LogWarning("Falha no processamento de query execution: {ErrorMessage}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar fila de query execution");
        }
    }

    private bool ShouldPerformHealthCheck()
    {
        return DateTime.UtcNow - _lastHealthCheck > TimeSpan.FromMinutes(HealthCheckIntervalMinutes);
    }
}
