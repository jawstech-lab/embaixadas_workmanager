using Amazon.SQS;
using Amazon.SQS.Model;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services;

public class QueueHealthService : IQueueHealthService
{
    private readonly ILogger<QueueHealthService> _logger;
    private readonly IResilientSqsService _resilientSqsService;
    private readonly IAmazonSQS _sqsClient;
    private readonly SqsConfiguration _sqsConfig;
    private readonly Dictionary<string, bool> _queuesHealthStatus = new();
    private readonly Dictionary<string, DateTime> _lastHealthCheck = new();

    public QueueHealthService(
        ILogger<QueueHealthService> logger,
        IResilientSqsService resilientSqsService,
        IAmazonSQS sqsClient,
        IOptions<SqsConfiguration> sqsConfig)
    {
        _logger = logger;
        _resilientSqsService = resilientSqsService;
        _sqsClient = sqsClient;
        _sqsConfig = sqsConfig.Value;
    }

    public async Task<bool> CheckAllQueuesHealthAsync()
    {
        try
        {
            _logger.LogDebug("Verificando saúde de todas as filas SQS...");
            
            var execucaoHealthy = await CheckQueueHealthAsync(_sqsConfig.FilaExecucao);
            var queryHealthy = await CheckQueueHealthAsync(_sqsConfig.FilaExecucaoQuery);
            var processoHealthy = await CheckQueueHealthAsync(_sqsConfig.FilaExecucaoProcesso);
            
            var allHealthy = execucaoHealthy && queryHealthy && processoHealthy;
            
            if (allHealthy)
            {
                _logger.LogInformation("Todas as filas estão saudáveis");
            }
            else
            {
                _logger.LogWarning("Problemas detectados em algumas filas");
            }
            
            return allHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar saúde das filas");
            return false;
        }
    }

    public async Task<bool> CheckQueueHealthAsync(string queueName)
    {
        try
        {
            var queueUrl = await _resilientSqsService.GetQueueUrlAsync(queueName);
            if (string.IsNullOrEmpty(queueUrl))
            {
                _logger.LogError("Não foi possível obter a URL da fila: {QueueName}", queueName);
                _queuesHealthStatus[queueName] = false;
                return false;
            }
            
            var isHealthy = await _resilientSqsService.IsHealthyAsync(queueUrl);
            _queuesHealthStatus[queueName] = isHealthy;
            _lastHealthCheck[queueName] = DateTime.UtcNow;
            
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar saúde da fila: {QueueName}", queueName);
            _queuesHealthStatus[queueName] = false;
            return false;
        }
    }

    public async Task<bool> TestSqsConnectivityAsync()
    {
        try
        {
            _logger.LogInformation("Testando conectividade básica com SQS...");
            
            var allQueues = await _sqsClient.ListQueuesAsync(new ListQueuesRequest());
            var totalQueues = allQueues.QueueUrls?.Count ?? 0;
            
            _logger.LogInformation("Conectividade SQS OK. Total de filas encontradas: {Count}", totalQueues);
            
            if (allQueues.QueueUrls?.Any() == true)
            {
                _logger.LogInformation("Filas disponíveis:");
                foreach (var queueUrl in allQueues.QueueUrls.Take(5))
                {
                    _logger.LogInformation("  - {QueueUrl}", queueUrl);
                }
            }
            
            return totalQueues > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na conectividade básica SQS. Verifique permissões IAM.");
            return false;
        }
    }

    public async Task<bool> ReconnectToQueueAsync(string queueName)
    {
        try
        {
            _logger.LogInformation("Tentando reconectar à fila: {QueueName}", queueName);
            
            var queueUrl = await _resilientSqsService.GetQueueUrlAsync(queueName);
            if (string.IsNullOrEmpty(queueUrl))
            {
                _logger.LogError("Não foi possível obter a URL da fila para reconexão: {QueueName}", queueName);
                return false;
            }
            
            var reconnected = await _resilientSqsService.ReconnectAsync(queueUrl);
            
            if (reconnected)
            {
                _logger.LogInformation("Reconexão à fila {QueueName} bem-sucedida", queueName);
                _queuesHealthStatus[queueName] = true;
            }
            else
            {
                _logger.LogError("Falha na reconexão à fila: {QueueName}", queueName);
                _queuesHealthStatus[queueName] = false;
            }
            
            return reconnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante reconexão à fila: {QueueName}", queueName);
            _queuesHealthStatus[queueName] = false;
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> GetQueuesHealthStatusAsync()
    {
        // Verificar se precisamos atualizar o status
        var now = DateTime.UtcNow;
        var needsUpdate = _lastHealthCheck.Count == 0 || 
                         _lastHealthCheck.Values.Any(lastCheck => now - lastCheck > TimeSpan.FromMinutes(5));
        
        if (needsUpdate)
        {
            await CheckAllQueuesHealthAsync();
        }
        
        return new Dictionary<string, bool>(_queuesHealthStatus);
    }
}
