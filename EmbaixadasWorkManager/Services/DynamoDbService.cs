using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
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
    
    /// <summary>
    /// Obtém o nome da tabela configurada para um tipo específico.
    /// Retorna null se o tipo não tiver configuração (usa o nome do atributo [DynamoDBTable]).
    /// </summary>
    private string? GetTableNameForType(Type type)
    {
        if (type == typeof(Execucao))
            return _config.TableNameExecucao;
        if (type == typeof(Verificacao))
            return _config.TableNameVerificacao;
        if (type == typeof(ExecucaoVerificacao))
            return _config.TableNameExecucaoVerificacao;
        if (type == typeof(Consulta))
            return _config.TableNameConsulta;
        if (type == typeof(Parametro))
            return _config.TableNameParametro;
        if (type == typeof(ExecucaoResumoView))
            return _config.TableNameExecucaoResumoView;
        if (type == typeof(ExecucaoEmpresaStatus))
            return _config.TableNameExecucaoEmpresaStatus;
        if (type == typeof(Resultado))
            return _config.TableNameResultado;
        if (type == typeof(ResultadoAgregado))
            return _config.TableNameResultadoAgregado;
        if (type == typeof(Justificativa))
            return _config.TableNameJustificativa;
        
        return null; // Usa o nome do atributo [DynamoDBTable]
    }
    
    /// <summary>
    /// Obtém o nome do atributo hash key de um tipo usando reflection
    /// </summary>
    private string? GetHashKeyName(Type type)
    {
        var hashKeyProp = type.GetProperties()
            .FirstOrDefault(p => p.GetCustomAttributes(typeof(DynamoDBHashKeyAttribute), false).Any());
        
        if (hashKeyProp == null)
            return null;
        
        var hashKeyAttr = hashKeyProp.GetCustomAttributes(typeof(DynamoDBHashKeyAttribute), false)
            .Cast<DynamoDBHashKeyAttribute>()
            .FirstOrDefault();
        
        return hashKeyAttr?.AttributeName ?? hashKeyProp.Name;
    }
    
    /// <summary>
    /// Obtém o nome do atributo range key de um tipo usando reflection
    /// </summary>
    private string? GetRangeKeyName(Type type)
    {
        var rangeKeyProp = type.GetProperties()
            .FirstOrDefault(p => p.GetCustomAttributes(typeof(DynamoDBRangeKeyAttribute), false).Any());
        
        if (rangeKeyProp == null)
            return null;
        
        var rangeKeyAttr = rangeKeyProp.GetCustomAttributes(typeof(DynamoDBRangeKeyAttribute), false)
            .Cast<DynamoDBRangeKeyAttribute>()
            .FirstOrDefault();
        
        return rangeKeyAttr?.AttributeName ?? rangeKeyProp.Name;
    }

    public async Task<T?> GetAsync<T>(string id) where T : class
    {
        try
        {
            // Interceptar chamadas para Execucao e usar o método específico que usa o nome de tabela correto
            if (typeof(T) == typeof(Execucao))
            {
                _logger.LogDebug("INTERCEPTADO: GetAsync<Execucao> redirecionado para GetExecucaoAsync. ID: {Id}", id);
                var execucao = await GetExecucaoAsync(id);
                return execucao as T;
            }
            
            // Interceptar tipos que têm configuração de tabela
            var tableName = GetTableNameForType(typeof(T));
            if (tableName != null)
            {
                _logger.LogDebug("INTERCEPTADO: GetAsync<{Type}> usando tabela configurada: {TableName}. ID: {Id}", typeof(T).Name, tableName, id);
                
                var hashKeyName = GetHashKeyName(typeof(T));
                if (hashKeyName == null)
                {
                    _logger.LogError("Tipo {Type} não possui atributo DynamoDBHashKey", typeof(T).Name);
                    throw new InvalidOperationException($"Tipo {typeof(T).Name} não possui atributo DynamoDBHashKey");
                }
                
                var request = new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { hashKeyName, new AttributeValue { S = id } }
                    }
                };
                
                var response = await _dynamoDbClient.GetItemAsync(request);
                
                if (response.Item == null || response.Item.Count == 0)
                {
                    _logger.LogDebug("Item não encontrado: {Type} com ID: {Id}", typeof(T).Name, id);
                    return null;
                }
                
                // Converter AttributeValue para objeto T usando DynamoDBContext
                var doc = Document.FromAttributeMap(response.Item);
                var result = _dynamoDbContext.FromDocument<T>(doc);
                
                _logger.LogDebug("Item encontrado: {Found}", result != null);
                return result;
            }
            
            _logger.LogDebug("Buscando item do tipo {Type} com ID: {Id}", typeof(T).Name, id);
            var result2 = await _dynamoDbContext.LoadAsync<T>(id);
            _logger.LogDebug("Item encontrado: {Found}", result2 != null);
            return result2;
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
            // Interceptar tipos que têm configuração de tabela
            var tableName = GetTableNameForType(typeof(T));
            if (tableName != null)
            {
                _logger.LogDebug("INTERCEPTADO: GetAsync<{Type}> usando tabela configurada: {TableName}. HashKey: {HashKey}, RangeKey: {RangeKey}", 
                    typeof(T).Name, tableName, hashKey, rangeKey);
                
                var hashKeyName = GetHashKeyName(typeof(T));
                var rangeKeyName = GetRangeKeyName(typeof(T));
                
                if (hashKeyName == null || rangeKeyName == null)
                {
                    _logger.LogError("Tipo {Type} não possui atributos DynamoDBHashKey ou DynamoDBRangeKey", typeof(T).Name);
                    throw new InvalidOperationException($"Tipo {typeof(T).Name} não possui atributos de chave necessários");
                }
                
                var request = new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { hashKeyName, new AttributeValue { S = hashKey } },
                        { rangeKeyName, new AttributeValue { S = rangeKey } }
                    }
                };
                
                var response = await _dynamoDbClient.GetItemAsync(request);
                
                if (response.Item == null || response.Item.Count == 0)
                {
                    _logger.LogDebug("Item não encontrado: {Type} com HashKey: {HashKey}, RangeKey: {RangeKey}", typeof(T).Name, hashKey, rangeKey);
                    return null;
                }
                
                var doc = Document.FromAttributeMap(response.Item);
                var result = _dynamoDbContext.FromDocument<T>(doc);
                
                _logger.LogDebug("Item encontrado: {Found}", result != null);
                return result;
            }
            
            _logger.LogDebug("Buscando item do tipo {Type} com HashKey: {HashKey}, RangeKey: {RangeKey}", 
                typeof(T).Name, hashKey, rangeKey);
            var result2 = await _dynamoDbContext.LoadAsync<T>(hashKey, rangeKey);
            _logger.LogDebug("Item encontrado: {Found}", result2 != null);
            return result2;
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
            // Interceptar tipos que têm configuração de tabela
            var tableName = GetTableNameForType(typeof(T));
            if (tableName != null)
            {
                _logger.LogDebug("INTERCEPTADO: GetAllAsync<{Type}> usando tabela configurada: {TableName}", typeof(T).Name, tableName);
                
                var request = new ScanRequest
                {
                    TableName = tableName
                };
                
                var items = new List<T>();
                Dictionary<string, AttributeValue>? lastEvaluatedKey = null;
                
                do
                {
                    request.ExclusiveStartKey = lastEvaluatedKey;
                    var response = await _dynamoDbClient.ScanAsync(request);
                    
                    foreach (var item in response.Items)
                    {
                        // Converter AttributeValue para objeto T usando DynamoDBContext
                        var doc = Document.FromAttributeMap(item);
                        var obj = _dynamoDbContext.FromDocument<T>(doc);
                        if (obj != null)
                            items.Add(obj);
                    }
                    
                    lastEvaluatedKey = response.LastEvaluatedKey;
                } while (lastEvaluatedKey != null && lastEvaluatedKey.Count > 0);
                
                _logger.LogDebug("Encontrados {Count} itens do tipo {Type}", items.Count, typeof(T).Name);
                return items;
            }
            
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
            // Interceptar chamadas para Execucao e usar o método específico que usa o nome de tabela correto
            if (typeof(T) == typeof(Execucao) && item is Execucao execucao)
            {
                _logger.LogDebug("INTERCEPTADO: SaveAsync<Execucao> redirecionado para SaveExecucaoAsync");
                await SaveExecucaoAsync(execucao);
                return;
            }
            
            // Interceptar outros tipos que têm configuração de tabela
            var tableName = GetTableNameForType(typeof(T));
            if (tableName != null)
            {
                _logger.LogDebug("INTERCEPTADO: SaveAsync<{Type}> usando tabela configurada: {TableName}", typeof(T).Name, tableName);
                
                // Converter objeto para Document e depois para Dictionary<string, AttributeValue>
                var doc = _dynamoDbContext.ToDocument<T>(item);
                var itemDict = doc.ToAttributeMap();
                
                // Usar PutItemAsync para criar/atualizar
                var putRequest = new PutItemRequest
                {
                    TableName = tableName,
                    Item = itemDict
                };
                
                await _dynamoDbClient.PutItemAsync(putRequest);
                _logger.LogDebug("Item salvo com sucesso");
                return;
            }
            
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
            // Interceptar chamadas para Execucao e usar o método específico que usa o nome de tabela correto
            if (typeof(T) == typeof(Execucao) && item is Execucao execucao)
            {
                _logger.LogDebug("INTERCEPTADO: UpdateAsync<Execucao> redirecionado para SaveExecucaoAsync");
                await SaveExecucaoAsync(execucao);
                return;
            }
            
            // Interceptar outros tipos que têm configuração de tabela
            var tableName = GetTableNameForType(typeof(T));
            if (tableName != null)
            {
                _logger.LogDebug("INTERCEPTADO: UpdateAsync<{Type}> usando tabela configurada: {TableName}", typeof(T).Name, tableName);
                
                // Converter objeto para Document e depois para Dictionary<string, AttributeValue>
                var doc = _dynamoDbContext.ToDocument<T>(item);
                var itemDict = doc.ToAttributeMap();
                
                // Obter chave primária
                var hashKeyName = GetHashKeyName(typeof(T));
                if (hashKeyName == null)
                {
                    _logger.LogError("Tipo {Type} não possui atributo DynamoDBHashKey", typeof(T).Name);
                    throw new InvalidOperationException($"Tipo {typeof(T).Name} não possui atributo DynamoDBHashKey");
                }
                
                var hashKeyValue = itemDict.ContainsKey(hashKeyName) ? itemDict[hashKeyName] : null;
                if (hashKeyValue == null)
                {
                    _logger.LogError("Chave primária vazia para tipo {Type}", typeof(T).Name);
                    throw new InvalidOperationException($"Chave primária vazia para tipo {typeof(T).Name}");
                }
                
                // Remover chave primária do update (não pode ser atualizada)
                var updateItem = itemDict.Where(kvp => kvp.Key != hashKeyName).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                // Preparar chave completa (pode ter range key também)
                var key = new Dictionary<string, AttributeValue> { { hashKeyName, hashKeyValue } };
                var rangeKeyName = GetRangeKeyName(typeof(T));
                if (rangeKeyName != null && itemDict.ContainsKey(rangeKeyName))
                {
                    key[rangeKeyName] = itemDict[rangeKeyName];
                    updateItem = updateItem.Where(kvp => kvp.Key != rangeKeyName).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                
                var updateRequest = new UpdateItemRequest
                {
                    TableName = tableName,
                    Key = key,
                    AttributeUpdates = updateItem.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new AttributeValueUpdate
                        {
                            Action = AttributeAction.PUT,
                            Value = kvp.Value
                        }
                    )
                };
                
                await _dynamoDbClient.UpdateItemAsync(updateRequest);
                _logger.LogDebug("Item atualizado com sucesso");
                return;
            }
            
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
            // Interceptar tipos que têm configuração de tabela
            var tableName = GetTableNameForType(typeof(T));
            if (tableName != null)
            {
                _logger.LogDebug("INTERCEPTADO: DeleteAsync<{Type}> usando tabela configurada: {TableName}. ID: {Id}", typeof(T).Name, tableName, id);
                
                var hashKeyName = GetHashKeyName(typeof(T));
                if (hashKeyName == null)
                {
                    _logger.LogError("Tipo {Type} não possui atributo DynamoDBHashKey", typeof(T).Name);
                    throw new InvalidOperationException($"Tipo {typeof(T).Name} não possui atributo DynamoDBHashKey");
                }
                
                var request = new DeleteItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { hashKeyName, new AttributeValue { S = id } }
                    }
                };
                
                await _dynamoDbClient.DeleteItemAsync(request);
                _logger.LogDebug("Item deletado com sucesso");
                return;
            }
            
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
            // Interceptar tipos que têm configuração de tabela
            var tableName = GetTableNameForType(typeof(T));
            if (tableName != null)
            {
                _logger.LogDebug("INTERCEPTADO: DeleteAsync<{Type}> usando tabela configurada: {TableName}. HashKey: {HashKey}, RangeKey: {RangeKey}", 
                    typeof(T).Name, tableName, hashKey, rangeKey);
                
                var hashKeyName = GetHashKeyName(typeof(T));
                var rangeKeyName = GetRangeKeyName(typeof(T));
                
                if (hashKeyName == null || rangeKeyName == null)
                {
                    _logger.LogError("Tipo {Type} não possui atributos DynamoDBHashKey ou DynamoDBRangeKey", typeof(T).Name);
                    throw new InvalidOperationException($"Tipo {typeof(T).Name} não possui atributos de chave necessários");
                }
                
                var request = new DeleteItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { hashKeyName, new AttributeValue { S = hashKey } },
                        { rangeKeyName, new AttributeValue { S = rangeKey } }
                    }
                };
                
                await _dynamoDbClient.DeleteItemAsync(request);
                _logger.LogDebug("Item deletado com sucesso");
                return;
            }
            
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
            _logger.LogDebug("Salvando execução: {ExecucaoId} na tabela: {TableName}", execucao.Id, _config.TableNameExecucao);
            
            // Verificar se a execução já existe usando GetExecucaoAsync (que usa o nome correto)
            var existing = await GetExecucaoAsync(execucao.Id);
            
            // Converter Execucao para Dictionary<string, AttributeValue> usando DynamoDBContext
            var doc = _dynamoDbContext.ToDocument<Execucao>(execucao);
            var itemDict = doc.ToAttributeMap();
            
            if (existing != null)
            {
                _logger.LogDebug("Execução já existe, atualizando: {ExecucaoId}", execucao.Id);
                
                // Remover Id do item pois não pode ser atualizado (é parte da chave primária)
                var updateItem = itemDict.Where(kvp => kvp.Key != "Id").ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                // Usar UpdateItemAsync para atualizar
                var updateRequest = new UpdateItemRequest
                {
                    TableName = _config.TableNameExecucao,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "Id", new AttributeValue { S = execucao.Id } }
                    },
                    AttributeUpdates = updateItem.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new AttributeValueUpdate
                        {
                            Action = AttributeAction.PUT,
                            Value = kvp.Value
                        }
                    )
                };
                
                await _dynamoDbClient.UpdateItemAsync(updateRequest);
            }
            else
            {
                _logger.LogDebug("Nova execução, salvando: {ExecucaoId}", execucao.Id);
                
                // Usar PutItemAsync para criar
                var putRequest = new PutItemRequest
                {
                    TableName = _config.TableNameExecucao,
                    Item = itemDict
                };
                
                await _dynamoDbClient.PutItemAsync(putRequest);
            }
            
            _logger.LogDebug("Execução salva com sucesso: {ExecucaoId}", execucao.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar execução: {ExecucaoId} na tabela: {TableName}", execucao.Id, _config.TableNameExecucao);
            return false;
        }
    }

    public async Task<Execucao?> GetExecucaoAsync(string execucaoId)
    {
        try
        {
            _logger.LogDebug("Buscando execução: {ExecucaoId} na tabela: {TableName}", execucaoId, _config.TableNameExecucao);
            
            // Usar GetItemAsync diretamente com o nome de tabela da configuração
            // para evitar o problema do [DynamoDBTable("Execucoes")] hardcoded
            var request = new GetItemRequest
            {
                TableName = _config.TableNameExecucao,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = execucaoId } }
                }
            };
            
            var response = await _dynamoDbClient.GetItemAsync(request);
            
            if (response.Item == null || response.Item.Count == 0)
            {
                _logger.LogWarning("Execução não encontrada: {ExecucaoId} na tabela: {TableName}", execucaoId, _config.TableNameExecucao);
                return null;
            }
            
            // Converter o item DynamoDB para o objeto Execucao
            var doc = Document.FromAttributeMap(response.Item);
            var execucao = _dynamoDbContext.FromDocument<Execucao>(doc);
            
            _logger.LogDebug("Execução encontrada: {ExecucaoId}", execucaoId);
            return execucao;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar execução: {ExecucaoId} na tabela: {TableName}", execucaoId, _config.TableNameExecucao);
            return null;
        }
    }

    public async Task<ExecucaoVerificacao?> GetExecucaoVerificacaoAsync(string execucaoVerificacaoId)
    {
        try
        {
            _logger.LogDebug("Buscando ExecucaoVerificacao: {ExecucaoVerificacaoId} na tabela: {TableName}", 
                execucaoVerificacaoId, _config.TableNameExecucaoVerificacao);
            
            // Usar GetItemAsync diretamente com o nome de tabela da configuração
            var request = new GetItemRequest
            {
                TableName = _config.TableNameExecucaoVerificacao,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue { S = execucaoVerificacaoId } }
                }
            };
            
            var response = await _dynamoDbClient.GetItemAsync(request);
            
            if (response.Item == null || response.Item.Count == 0)
            {
                _logger.LogWarning("ExecucaoVerificacao não encontrada: {ExecucaoVerificacaoId} na tabela: {TableName}", 
                    execucaoVerificacaoId, _config.TableNameExecucaoVerificacao);
                return null;
            }
            
            // Converter AttributeValue para objeto ExecucaoVerificacao usando DynamoDBContext
            var doc = Document.FromAttributeMap(response.Item);
            var execucaoVerificacao = _dynamoDbContext.FromDocument<ExecucaoVerificacao>(doc);
            
            _logger.LogDebug("ExecucaoVerificacao encontrada: {ExecucaoVerificacaoId}", execucaoVerificacaoId);
            return execucaoVerificacao;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar ExecucaoVerificacao: {ExecucaoVerificacaoId} na tabela: {TableName}", 
                execucaoVerificacaoId, _config.TableNameExecucaoVerificacao);
            return null;
        }
    }

    public async Task<List<Verificacao>> GetTodasVerificacoesAsync()
    {
        try
        {
            _logger.LogDebug("Buscando todas as verificações");
            
            // Usar o método GetAllAsync que já existe
            var todasVerificacoes = await GetAllAsync<Verificacao>();
            var verificacoesList = todasVerificacoes.ToList();
            
            _logger.LogInformation("Encontradas {Count} verificações no total", verificacoesList.Count);
            
            return verificacoesList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar todas as verificações");
            return new List<Verificacao>();
        }
    }

    public async Task<List<Verificacao>> GetVerificacoesPorEmbaixadasAsync(List<string> idEmbaixadas)
    {
        try
        {
            _logger.LogInformation("Buscando verificações filtradas por {Count} embaixadas", idEmbaixadas.Count);
            _logger.LogDebug("IDs de Embaixadas: {Embaixadas}", string.Join(", ", idEmbaixadas));
            
            // Buscar todas as verificações
            var todasVerificacoes = await GetAllAsync<Verificacao>();
            
            // Filtrar verificações que têm pelo menos uma embaixada em comum com a lista fornecida
            var verificacoesFiltradas = todasVerificacoes
                .Where(v => v.IdEmbaixadas != null && 
                           v.IdEmbaixadas.Any() && 
                           v.IdEmbaixadas.Any(idEmb => idEmbaixadas.Contains(idEmb)))
                .ToList();
            
            _logger.LogInformation("Encontradas {Count} verificações relacionadas às embaixadas especificadas (de {Total} verificações totais)", 
                verificacoesFiltradas.Count, todasVerificacoes.Count());
            
            // Log detalhado das verificações encontradas
            if (verificacoesFiltradas.Any())
            {
                _logger.LogDebug("Verificações encontradas:");
                foreach (var verif in verificacoesFiltradas.Take(10)) // Mostrar apenas as primeiras 10
                {
                    var embaixadasComum = verif.IdEmbaixadas.Intersect(idEmbaixadas).ToList();
                    _logger.LogDebug("- ID: {Id} | Nome: {Nome} | Embaixadas em comum: {Count}", 
                        verif.Id, verif.NomeVerificacao, embaixadasComum.Count);
                }
                
                if (verificacoesFiltradas.Count > 10)
                {
                    _logger.LogDebug("... e mais {Count} verificações", verificacoesFiltradas.Count - 10);
                }
            }
            else
            {
                _logger.LogWarning("Nenhuma verificação encontrada para as embaixadas especificadas");
            }
            
            return verificacoesFiltradas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar verificações por embaixadas");
            return new List<Verificacao>();
        }
    }

}
