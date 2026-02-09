using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services.PostProcessing.Steps;

/// <summary>
/// Step de pós-processamento que remove apontamentos justificados e aprovados.
/// 
/// Fluxo:
/// 1. Busca justificativas aprovadas (Status = "Aprovado") usando GSI_Status
/// 2. Para cada justificativa:
///    a. Se SelecionarTodos = 1: Remove todos os registros que atendem critérios
///    b. Se SelecionarTodos = 0: Remove apenas IDs específicos da lista
/// 3. Executa ANTES da agregação para não contar registros justificados
/// </summary>
public class ProcessamentoJustificativasStep : IPostProcessingStep
{
    private readonly ILogger<ProcessamentoJustificativasStep> _logger;
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly IDynamoDbService _dynamoDbService;
    private readonly DynamoDbConfiguration _dynamoConfig;
    private readonly ProcessamentoJustificativasStepConfiguration _config;

    public string StepName => "ProcessamentoJustificativas";
    public int Order => _config.Order;
    public bool IsEnabled => _config.Enabled;

    public ProcessamentoJustificativasStep(
        ILogger<ProcessamentoJustificativasStep> logger,
        IAmazonDynamoDB dynamoClient,
        IDynamoDbService dynamoDbService,
        IOptions<DynamoDbConfiguration> dynamoConfig,
        IOptions<PostProcessingConfiguration> config)
    {
        _logger = logger;
        _dynamoClient = dynamoClient;
        _dynamoDbService = dynamoDbService;
        _dynamoConfig = dynamoConfig.Value;
        _config = config.Value.ProcessamentoJustificativas;
    }

    public async Task<bool> CanExecuteAsync(PostProcessingContext context)
    {
        // Sempre pode executar (se não houver justificativas, não faz nada)
        await Task.CompletedTask;
        return true;
    }

    public async Task<PostProcessingStepResult> ExecuteAsync(PostProcessingContext context)
    {
        try
        {
            var execucaoId = context.Execucao.Id;

            _logger.LogInformation(
                "Iniciando processamento de justificativas aprovadas para execucao {ExecucaoId}",
                execucaoId);

            // ETAPA 1: Buscar justificativas aprovadas
            var justificativas = await BuscarJustificativasAprovadasAsync();
            
            if (!justificativas.Any())
            {
                _logger.LogInformation("Nenhuma justificativa aprovada encontrada. Pulando step.");
                return PostProcessingStepResult.Ok(
                    "Nenhuma justificativa aprovada para processar",
                    new Dictionary<string, object>
                    {
                        { "TotalJustificativas", 0 },
                        { "TotalRemovidos", 0 }
                    });
            }

            _logger.LogInformation(
                "Encontradas {Count} justificativas aprovadas para processamento",
                justificativas.Count);

            // ETAPA 2: Processar cada justificativa
            var totalRemovidos = 0;
            var justificativasProcessadas = 0;

            foreach (var justificativa in justificativas)
            {
                try
                {
                    _logger.LogInformation(
                        "Processando justificativa {Id}: SelecionarTodos={ST}, " +
                        "Verificacao={VerifId}, Empresa={Empresa}, Tabela={Tabela}, Campo={Campo}",
                        justificativa.Id, justificativa.IsSelecionarTodos,
                        justificativa.VerificacaoId, justificativa.Empresa,
                        justificativa.TabelaReferencia, justificativa.Campo);

                    int removidos;
                    
                    if (justificativa.IsSelecionarTodos)
                    {
                        // Remover todos os registros que atendem critérios
                        removidos = await RemoverPorCriteriosAsync(execucaoId, justificativa);
                    }
                    else
                    {
                        // Remover apenas IDs específicos
                        removidos = await RemoverPorIdsAsync(execucaoId, justificativa);
                    }

                    totalRemovidos += removidos;
                    justificativasProcessadas++;

                    _logger.LogInformation(
                        "Justificativa {Id}: {Count} registros removidos",
                        justificativa.Id, removidos);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Erro ao processar justificativa {Id}. Continuando com proximas...",
                        justificativa.Id);
                    // Não interrompe o processamento de outras justificativas
                }
            }

            _logger.LogInformation(
                "Processamento de justificativas concluido. " +
                "{Processadas} justificativas processadas, {Total} registros removidos",
                justificativasProcessadas, totalRemovidos);

            // Aguardar propagação das deleções no GSI antes da próxima etapa (Agregação)
            if (totalRemovidos > 0)
            {
                _logger.LogInformation(
                    "Aguardando 5 segundos para propagacao das delecoes no GSI_Agregacao...");
                await Task.Delay(5000);
                _logger.LogInformation("Propagacao concluida. Proximo step pode executar.");
            }

            return PostProcessingStepResult.Ok(
                $"{justificativasProcessadas} justificativas processadas, {totalRemovidos} registros removidos",
                new Dictionary<string, object>
                {
                    { "TotalJustificativas", justificativasProcessadas },
                    { "TotalRemovidos", totalRemovidos },
                    { "JustificativasIds", justificativas.Select(j => j.Id).ToList() }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no processamento de justificativas");
            return PostProcessingStepResult.Fail(
                $"Erro no processamento de justificativas: {ex.Message}",
                shouldStop: false);
        }
    }

    /// <summary>
    /// Busca justificativas aprovadas usando GSI_Status
    /// </summary>
    private async Task<List<Justificativa>> BuscarJustificativasAprovadasAsync()
    {
        try
        {
            var justificativas = new List<Justificativa>();
            Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

            _logger.LogDebug("Buscando justificativas aprovadas usando GSI_Status");

            do
            {
                var request = new QueryRequest
                {
                    TableName = _dynamoConfig.TableNameJustificativa,
                    IndexName = "GSI_Status",
                    KeyConditionExpression = "GSI1_PK = :status",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":status", new AttributeValue { S = "STATUS#APROVADO" } }
                    },
                    ExclusiveStartKey = lastEvaluatedKey
                };

                var response = await _dynamoClient.QueryAsync(request);

                // Converter AttributeValue para objetos Justificativa
                foreach (var item in response.Items)
                {
                    var justificativa = new Justificativa
                    {
                        Id = item.ContainsKey("Id") ? item["Id"].S : string.Empty,
                        VerificacaoId = item.ContainsKey("VerificacaoId") ? item["VerificacaoId"].S : string.Empty,
                        Empresa = item.ContainsKey("Empresa") ? item["Empresa"].S : string.Empty,
                        TabelaReferencia = item.ContainsKey("TabelaReferencia") ? item["TabelaReferencia"].S : string.Empty,
                        Campo = item.ContainsKey("Campo") ? item["Campo"].S : string.Empty,
                        Status = item.ContainsKey("Status") ? item["Status"].S : string.Empty,
                        SelecionarTodos = item.ContainsKey("SelecionarTodos") && int.TryParse(item["SelecionarTodos"].N, out var st) ? st : 0,
                        TipoApontamento = item.ContainsKey("TipoApontamento") ? item["TipoApontamento"].S : string.Empty,
                        Referencia = item.ContainsKey("Referencia") ? item["Referencia"].S : null
                    };

                    // Parsear IdsRelacionados (Set de Strings no DynamoDB)
                    if (item.ContainsKey("IdsRelacionados") && item["IdsRelacionados"].SS != null)
                    {
                        justificativa.IdsRelacionados = item["IdsRelacionados"].SS.ToList();
                    }

                    // Validar se é realmente aprovada
                    if (justificativa.IsAprovado)
                    {
                        justificativas.Add(justificativa);
                    }
                }

                lastEvaluatedKey = response.LastEvaluatedKey;

            } while (lastEvaluatedKey != null && lastEvaluatedKey.Count > 0);

            _logger.LogDebug(
                "Busca concluida. {Count} justificativas aprovadas encontradas",
                justificativas.Count);

            return justificativas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar justificativas aprovadas");
            throw;
        }
    }

    /// <summary>
    /// Remove registros por critérios (SelecionarTodos = 1)
    /// Usa GSI_Agregacao com GSI1_SK para busca otimizada
    /// </summary>
    private async Task<int> RemoverPorCriteriosAsync(
        string execucaoId,
        Justificativa justificativa)
    {
        try
        {
            // Construir GSI1_SK conforme configuração
            string gsi1SkPrefix;
            
            if (_config.UsarFiltroCompleto)
            {
                // Filtro completo: VER#{VerifId}#EMP#{Emp}#TAB#{Tab}#CAMPO#{Campo}
                gsi1SkPrefix = $"VER#{justificativa.VerificacaoId}#EMP#{justificativa.Empresa}#TAB#{justificativa.TabelaReferencia}#CAMPO#{justificativa.Campo}";
                
                _logger.LogInformation(
                    "Buscando registros para deletar (Filtro COMPLETO): " +
                    "GSI1_PK=EXEC#{ExecId}, GSI1_SK begins_with={Prefix}",
                    execucaoId, gsi1SkPrefix);
            }
            else
            {
                // Filtro simples: apenas VER#{VerifId}
                gsi1SkPrefix = $"VER#{justificativa.VerificacaoId}";
                
                _logger.LogInformation(
                    "Buscando registros para deletar (Filtro SIMPLES): " +
                    "GSI1_PK=EXEC#{ExecId}, GSI1_SK begins_with={Prefix}",
                    execucaoId, gsi1SkPrefix);
            }
            
            // Query usando GSI_Agregacao
            var resultados = await QueryResultadosPorGSIAsync(execucaoId, gsi1SkPrefix);

            if (!resultados.Any())
            {
                _logger.LogWarning(
                    "Nenhum registro encontrado para deletar! ExecucaoId={ExecId}, GSI1_SK_Prefix={Prefix}",
                    execucaoId, gsi1SkPrefix);
                return 0;
            }

            _logger.LogInformation(
                "Encontrados {Count} registros para deletar (VerificacaoId={VerifId}, Empresa={Emp}, Tabela={Tab}, Campo={Campo})",
                resultados.Count, justificativa.VerificacaoId, justificativa.Empresa, 
                justificativa.TabelaReferencia, justificativa.Campo);

            // Deletar registros
            return await BatchDeleteResultadosAsync(resultados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao remover registros por criterios (Justificativa={Id})",
                justificativa.Id);
            throw;
        }
    }

    /// <summary>
    /// Remove registros por IDs específicos (SelecionarTodos = 0)
    /// Usa SK com begins_with: RES#{CodId}#... para suportar formato com GUID
    /// </summary>
    private async Task<int> RemoverPorIdsAsync(
        string execucaoId,
        Justificativa justificativa)
    {
        try
        {
            if (justificativa.IdsRelacionados == null || !justificativa.IdsRelacionados.Any())
            {
                _logger.LogWarning(
                    "Justificativa {Id} tem SelecionarTodos=0 mas IdsRelacionados esta vazio. Pulando.",
                    justificativa.Id);
                return 0;
            }

            _logger.LogInformation(
                "Removendo {Count} IDs especificos: {Ids}",
                justificativa.IdsRelacionados.Count,
                string.Join(", ", justificativa.IdsRelacionados.Take(5)) + 
                (justificativa.IdsRelacionados.Count > 5 ? "..." : ""));

            // Buscar registros por PK + SK
            // Formato REAL da tabela: VER#{ExecId}#{VerifId}
            var pk = $"VER#{execucaoId}#{justificativa.VerificacaoId}";
            var resultados = new List<Resultado>();
            
            _logger.LogDebug(
                "PK para busca: {PK} (ExecId={ExecId}, VerifId={VerifId})",
                pk, execucaoId, justificativa.VerificacaoId);
            
            // Para cada ID, criar SK prefix e buscar com begins_with
            foreach (var codId in justificativa.IdsRelacionados)
            {
                var skPrefix = $"RES#{codId}#";  // Prefix para begins_with
                
                _logger.LogDebug(
                    "Buscando registro: PK={PK}, SK begins_with={SKPrefix}",
                    pk, skPrefix);
                
                // Query com PK e begins_with no SK (suporta múltiplos registros por CodId)
                var itens = await QueryResultadosPorSKAsync(pk, skPrefix);
                
                if (itens.Any())
                {
                    resultados.AddRange(itens);
                    _logger.LogDebug(
                        "Encontrados {Count} registros para CodId={CodId}",
                        itens.Count, codId);
                }
                else
                {
                    _logger.LogDebug(
                        "Nenhum registro encontrado: PK={PK}, SK begins_with={SKPrefix}, CodId={CodId}",
                        pk, skPrefix, codId);
                }
            }

            if (!resultados.Any())
            {
                _logger.LogWarning(
                    "Nenhum dos {Total} IDs foi encontrado na tabela Resultado! " +
                    "ExecucaoId={ExecId}, VerificacaoId={VerifId}",
                    justificativa.IdsRelacionados.Count, execucaoId, justificativa.VerificacaoId);
                return 0;
            }

            _logger.LogInformation(
                "Encontrados {Count} registros (pode haver multiplos por CodId devido ao GUID) de {Total} IDs para deletar",
                resultados.Count, justificativa.IdsRelacionados.Count);

            // Deletar registros encontrados
            return await BatchDeleteResultadosAsync(resultados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao remover registros por IDs (Justificativa={Id})",
                justificativa.Id);
            throw;
        }
    }

    /// <summary>
    /// Query usando GSI_Agregacao para buscar registros por critérios
    /// Usa begins_with no GSI1_SK para filtros flexíveis
    /// </summary>
    private async Task<List<Resultado>> QueryResultadosPorGSIAsync(string execucaoId, string gsi1SkPrefix)
    {
        try
        {
            var resultados = new List<Resultado>();
            Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

            do
            {
                var request = new QueryRequest
                {
                    TableName = _dynamoConfig.TableNameResultado,
                    IndexName = "GSI_Agregacao",
                    KeyConditionExpression = "GSI1_PK = :gsi1pk AND begins_with(GSI1_SK, :gsi1sk)",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":gsi1pk", new AttributeValue { S = $"EXEC#{execucaoId}" } },
                        { ":gsi1sk", new AttributeValue { S = gsi1SkPrefix } }
                    },
                    ExclusiveStartKey = lastEvaluatedKey
                };

                var response = await _dynamoClient.QueryAsync(request);

                foreach (var item in response.Items)
                {
                    var resultado = new Resultado
                    {
                        PK = item.ContainsKey("PK") ? item["PK"].S : string.Empty,
                        SK = item.ContainsKey("SK") ? item["SK"].S : string.Empty,
                        GSI1_PK = item.ContainsKey("GSI1_PK") ? item["GSI1_PK"].S : string.Empty,
                        GSI1_SK = item.ContainsKey("GSI1_SK") ? item["GSI1_SK"].S : string.Empty,
                        Empresa = item.ContainsKey("Empresa") ? item["Empresa"].S : string.Empty,
                        Tabela = item.ContainsKey("Tabela") ? item["Tabela"].S : string.Empty,
                        Campo = item.ContainsKey("Campo") ? item["Campo"].S : string.Empty,
                        Referencia = item.ContainsKey("Referencia") ? item["Referencia"].S : string.Empty,
                        VerificacaoId = item.ContainsKey("VerificacaoId") ? item["VerificacaoId"].S : string.Empty
                    };

                    resultados.Add(resultado);
                }

                lastEvaluatedKey = response.LastEvaluatedKey;

            } while (lastEvaluatedKey != null && lastEvaluatedKey.Count > 0);

            _logger.LogDebug(
                "Query GSI concluida. GSI1_PK=EXEC#{ExecId}, GSI1_SK begins_with={Prefix}, Total={Count}",
                execucaoId, gsi1SkPrefix, resultados.Count);

            return resultados;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Erro ao buscar resultados por GSI (ExecucaoId={ExecId}, GSI1_SK_Prefix={Prefix})", 
                execucaoId, gsi1SkPrefix);
            throw;
        }
    }

    /// <summary>
    /// Query registros na tabela principal usando PK e begins_with no SK
    /// Suporta formato SK: RES#<CodId>#GUID#<GuidAleatorio>
    /// </summary>
    private async Task<List<Resultado>> QueryResultadosPorSKAsync(string pk, string skPrefix)
    {
        try
        {
            var resultados = new List<Resultado>();
            Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

            do
            {
                var request = new QueryRequest
                {
                    TableName = _dynamoConfig.TableNameResultado,
                    KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":pk", new AttributeValue { S = pk } },
                        { ":sk", new AttributeValue { S = skPrefix } }
                    },
                    ExclusiveStartKey = lastEvaluatedKey
                };

                var response = await _dynamoClient.QueryAsync(request);

                foreach (var item in response.Items)
                {
                    var resultado = new Resultado
                    {
                        PK = item.ContainsKey("PK") ? item["PK"].S : string.Empty,
                        SK = item.ContainsKey("SK") ? item["SK"].S : string.Empty,
                        Empresa = item.ContainsKey("Empresa") ? item["Empresa"].S : string.Empty,
                        Tabela = item.ContainsKey("Tabela") ? item["Tabela"].S : string.Empty,
                        Campo = item.ContainsKey("Campo") ? item["Campo"].S : string.Empty,
                        Referencia = item.ContainsKey("Referencia") ? item["Referencia"].S : string.Empty,
                        VerificacaoId = item.ContainsKey("VerificacaoId") ? item["VerificacaoId"].S : string.Empty
                    };

                    resultados.Add(resultado);
                }

                lastEvaluatedKey = response.LastEvaluatedKey;

            } while (lastEvaluatedKey != null && lastEvaluatedKey.Count > 0);

            _logger.LogDebug(
                "Query por SK concluida. PK={PK}, SK begins_with={SKPrefix}, Total={Count}",
                pk, skPrefix, resultados.Count);

            return resultados;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar resultado (PK={PK}, SK begins_with={SKPrefix})", pk, skPrefix);
            throw;
        }
    }

    /// <summary>
    /// Deleta registros em lotes (BatchWriteItem - 25 por vez)
    /// </summary>
    private async Task<int> BatchDeleteResultadosAsync(List<Resultado> resultados)
    {
        try
        {
            var totalDeletados = 0;
            var batches = resultados.Chunk(_config.BatchSize).ToList();

            _logger.LogDebug(
                "Deletando {Total} registros em {Batches} lotes de ate {Size} itens",
                resultados.Count, batches.Count, _config.BatchSize);

            foreach (var batch in batches)
            {
                var writeRequests = batch.Select(r => new WriteRequest
                {
                    DeleteRequest = new DeleteRequest
                    {
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "PK", new AttributeValue { S = r.PK } },
                            { "SK", new AttributeValue { S = r.SK } }
                        }
                    }
                }).ToList();

                var request = new BatchWriteItemRequest
                {
                    RequestItems = new Dictionary<string, List<WriteRequest>>
                    {
                        { _dynamoConfig.TableNameResultado, writeRequests }
                    }
                };

                var response = await _dynamoClient.BatchWriteItemAsync(request);
                totalDeletados += batch.Count();

                // Tratar itens não processados (throttling)
                if (response.UnprocessedItems != null && response.UnprocessedItems.Any())
                {
                    _logger.LogWarning(
                        "BatchDelete teve {Count} itens nao processados. Tentando novamente...",
                        response.UnprocessedItems.Count);

                    // Retry com backoff
                    await Task.Delay(500);
                    
                    var retryRequest = new BatchWriteItemRequest
                    {
                        RequestItems = response.UnprocessedItems
                    };
                    
                    await _dynamoClient.BatchWriteItemAsync(retryRequest);
                }
            }

            _logger.LogDebug("BatchDelete concluido. {Total} registros deletados", totalDeletados);
            return totalDeletados;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar registros em lote");
            throw;
        }
    }
}

