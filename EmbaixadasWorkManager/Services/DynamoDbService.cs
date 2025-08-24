using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services;

public class DynamoDbService : IDynamoDbService
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly ILogger<DynamoDbService> _logger;
    private readonly DynamoDbConfiguration _config;

    public DynamoDbService(
        IAmazonDynamoDB dynamoDbClient,
        IDynamoDBContext dynamoDbContext,
        IOptions<DynamoDbConfiguration> config,
        ILogger<DynamoDbService> logger)
    {
        _dynamoDbClient = dynamoDbClient;
        _dynamoDbContext = dynamoDbContext;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string id) where T : class
    {
        try
        {
            _logger.LogDebug("Buscando item do tipo {Type} com ID: {Id}", typeof(T).Name, id);
            var result = await _dynamoDbContext.LoadAsync<T>(id);
            _logger.LogDebug("Item encontrado: {Found}", result != null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar item do tipo {Type} com ID: {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public async Task<T?> GetAsync<T>(string hashKey, string rangeKey) where T : class
    {
        try
        {
            _logger.LogDebug("Buscando item do tipo {Type} com HashKey: {HashKey}, RangeKey: {RangeKey}", 
                typeof(T).Name, hashKey, rangeKey);
            var result = await _dynamoDbContext.LoadAsync<T>(hashKey, rangeKey);
            _logger.LogDebug("Item encontrado: {Found}", result != null);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar item do tipo {Type} com HashKey: {HashKey}, RangeKey: {RangeKey}", 
                typeof(T).Name, hashKey, rangeKey);
            throw;
        }
    }

    public async Task<IEnumerable<T>> GetAllAsync<T>() where T : class
    {
        try
        {
            _logger.LogDebug("Buscando todos os itens do tipo {Type}", typeof(T).Name);
            var scanConditions = new List<ScanCondition>();
            var result = await _dynamoDbContext.ScanAsync<T>(scanConditions).GetRemainingAsync();
            _logger.LogDebug("Encontrados {Count} itens do tipo {Type}", result.Count, typeof(T).Name);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar todos os itens do tipo {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string hashKey, string? rangeKey = null) where T : class
    {
        try
        {
            _logger.LogDebug("Consultando itens do tipo {Type} com HashKey: {HashKey}, RangeKey: {RangeKey}", 
                typeof(T).Name, hashKey, rangeKey ?? "null");
            
            var queryConfig = new QueryConfig
            {
                ConsistentRead = false
            };
            var result = await _dynamoDbContext.QueryAsync<T>(hashKey, queryConfig).GetRemainingAsync();
            
            _logger.LogDebug("Encontrados {Count} itens na consulta", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar itens do tipo {Type} com HashKey: {HashKey}", typeof(T).Name, hashKey);
            throw;
        }
    }

    public async Task SaveAsync<T>(T item) where T : class
    {
        try
        {
            _logger.LogDebug("Salvando item do tipo {Type}", typeof(T).Name);
            await _dynamoDbContext.SaveAsync(item);
            _logger.LogDebug("Item salvo com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar item do tipo {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task UpdateAsync<T>(T item) where T : class
    {
        try
        {
            _logger.LogDebug("Atualizando item do tipo {Type}", typeof(T).Name);
            await _dynamoDbContext.SaveAsync(item);
            _logger.LogDebug("Item atualizado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar item do tipo {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task DeleteAsync<T>(string id) where T : class
    {
        try
        {
            _logger.LogDebug("Deletando item do tipo {Type} com ID: {Id}", typeof(T).Name, id);
            await _dynamoDbContext.DeleteAsync<T>(id);
            _logger.LogDebug("Item deletado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar item do tipo {Type} com ID: {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public async Task DeleteAsync<T>(string hashKey, string rangeKey) where T : class
    {
        try
        {
            _logger.LogDebug("Deletando item do tipo {Type} com HashKey: {HashKey}, RangeKey: {RangeKey}", 
                typeof(T).Name, hashKey, rangeKey);
            await _dynamoDbContext.DeleteAsync<T>(hashKey, rangeKey);
            _logger.LogDebug("Item deletado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar item do tipo {Type} com HashKey: {HashKey}, RangeKey: {RangeKey}", 
                typeof(T).Name, hashKey, rangeKey);
            throw;
        }
    }

    public async Task<bool> ExistsAsync<T>(string id) where T : class
    {
        try
        {
            var item = await GetAsync<T>(id);
            return item != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência do item do tipo {Type} com ID: {Id}", typeof(T).Name, id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync<T>(string hashKey, string rangeKey) where T : class
    {
        try
        {
            var item = await GetAsync<T>(hashKey, rangeKey);
            return item != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência do item do tipo {Type} com HashKey: {HashKey}, RangeKey: {RangeKey}", 
                typeof(T).Name, hashKey, rangeKey);
            throw;
        }
    }

    public async Task<bool> SaveExecucaoAsync(Execucao execucao)
    {
        try
        {
            _logger.LogDebug("Salvando execução: {ExecucaoId}", execucao.Id);
            
            // Verificar se a execução já existe
            var existing = await GetAsync<Execucao>(execucao.Id);
            if (existing != null)
            {
                _logger.LogDebug("Execução já existe, atualizando: {ExecucaoId}", execucao.Id);
                await UpdateAsync(execucao);
            }
            else
            {
                _logger.LogDebug("Nova execução, salvando: {ExecucaoId}", execucao.Id);
                await SaveAsync(execucao);
            }
            
            _logger.LogDebug("Execução salva com sucesso: {ExecucaoId}", execucao.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar execução: {ExecucaoId}", execucao.Id);
            return false;
        }
    }

    public async Task<Execucao?> GetExecucaoAsync(string execucaoId)
    {
        try
        {
            _logger.LogDebug("Buscando execução: {ExecucaoId}", execucaoId);
            var execucao = await GetAsync<Execucao>(execucaoId);
            
            if (execucao != null)
            {
                _logger.LogDebug("Execução encontrada: {ExecucaoId}", execucaoId);
            }
            else
            {
                _logger.LogWarning("Execução não encontrada: {ExecucaoId}", execucaoId);
            }
            
            return execucao;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar execução: {ExecucaoId}", execucaoId);
            return null;
        }
    }

}
