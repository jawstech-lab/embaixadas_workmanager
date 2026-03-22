using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services.PostProcessing.Steps;

/// <summary>
/// Step de pós-processamento que agrega resultados de apontamentos de erro.
/// 
/// Implementa o fluxo completo de agregação:
/// 1. Busca otimizada usando GSI (Query no GSI_Agregacao)
/// 2. Agrupamento e cálculo em memória
/// 3. Inserção na tabela ResultadoAgregado
/// 4. Atualização condicional na ExecucaoResumoView
/// </summary>
public class AgregacaoResultadosStep : IPostProcessingStep
{
    private readonly ILogger<AgregacaoResultadosStep> _logger;
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly IDynamoDbService _dynamoDbService;
    private readonly DynamoDbConfiguration _dynamoConfig;
    private readonly AgregacaoResultadosStepConfiguration _config;

    public string StepName => "AgregacaoResultados";
    public int Order => _config.Order;
    public bool IsEnabled => _config.Enabled;

    public AgregacaoResultadosStep(
        ILogger<AgregacaoResultadosStep> logger,
        IAmazonDynamoDB dynamoClient,
        IDynamoDbService dynamoDbService,
        IOptions<DynamoDbConfiguration> dynamoConfig,
        IOptions<PostProcessingConfiguration> config)
    {
        _logger = logger;
        _dynamoClient = dynamoClient;
        _dynamoDbService = dynamoDbService;
        _dynamoConfig = dynamoConfig.Value;
        _config = config.Value.AgregacaoResultados;
    }

    public async Task<bool> CanExecuteAsync(PostProcessingContext context)
    {
        // SEMPRE executa para criar grupos, mesmo com QTD=0
        // Isso garante que sempre temos registro no ResultadoAgregado
        return true;
    }

    public async Task<PostProcessingStepResult> ExecuteAsync(PostProcessingContext context)
    {
        try
        {
            var execucaoId = context.Execucao.Id;

            _logger.LogInformation(
                "Iniciando agregacao de resultados para execucao {ExecucaoId}. " +
                "Total de apontamentos: {TotalApontamentos}",
                execucaoId,
                context.Execucao.TotalApontamentos);

            // ETAPA 1: Busca otimizada usando GSI
            var resultados = await BuscarApontamentosAsync(execucaoId);
            _logger.LogInformation("Encontrados {Count} apontamentos na tabela Resultado", resultados.Count);

            // ETAPA 2: Agrupamento em memória (DUAS estruturas: Segmentada + Global)
            var (gruposSegmentados, gruposGlobais, totalApontamentos, empresas, embaixadas) 
                = AgruparResultados(resultados);

            // ETAPA 2.5: Limpar registros antigos das empresas solicitadas (Segmentado + Global)
            await LimparResultadosAnterioresAsync(
                context.Execucao.Empresa, 
                context.Execucao.IdEmbaixadas ?? new List<string>());

            // ETAPA 3: Inserção SEGMENTADA (Usuário Comum - replicação por embaixada)
            await InserirResultadosSegmentadosAsync(
                execucaoId, 
                gruposSegmentados, 
                context.Execucao.Empresa);

            // ETAPA 4: Inserção GLOBAL (Admin - sem replicação)
            await InserirResultadosGlobaisAsync(
                execucaoId, 
                gruposGlobais, 
                context.Execucao.Empresa);

            // ETAPA 4: Atualização condicional na ExecucaoResumoView (REMOVIDA)
            // TotalApontamentos foi removido da View - calcular somando QTDs do ResultadoAgregado

            var data = new Dictionary<string, object>
            {
                { "TotalApontamentos", totalApontamentos },
                { "TotalGruposSegmentados", gruposSegmentados.Count },
                { "TotalGruposGlobais", gruposGlobais.Count },
                { "TotalEmpresas", empresas.Count },
                { "TotalEmbaixadas", embaixadas.Count },
                { "Empresas", empresas },
                { "Embaixadas", embaixadas }
            };

            var message = $"Agregacao concluida. {totalApontamentos} apontamentos. " +
                         $"Segmentados: {gruposSegmentados.Count} grupos, Globais: {gruposGlobais.Count} grupos. " +
                         $"{empresas.Count} empresas, {embaixadas.Count} embaixadas";

            _logger.LogInformation(message);

            return PostProcessingStepResult.Ok(message, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar agregacao de resultados para execucao {ExecucaoId}",
                context.Execucao.Id);
            return PostProcessingStepResult.Fail($"Erro na agregacao: {ex.Message}");
        }
    }

    /// <summary>
    /// ETAPA 1: Busca todos os apontamentos de uma execução usando o GSI
    /// </summary>
    private async Task<List<Resultado>> BuscarApontamentosAsync(string execucaoId)
    {
        try
        {
            var resultados = new List<Resultado>();
            Dictionary<string, AttributeValue>? lastEvaluatedKey = null;
            int pageCount = 0;

            _logger.LogDebug("Buscando apontamentos usando GSI_Agregacao. GSI1_PK = EXEC#{ExecucaoId}", execucaoId);

            do
            {
                pageCount++;
                
                var request = new QueryRequest
                {
                    TableName = _dynamoConfig.TableNameResultado,
                    IndexName = "GSI_Agregacao",
                    KeyConditionExpression = "GSI1_PK = :execId",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":execId", new AttributeValue { S = $"EXEC#{execucaoId}" } }
                    },
                    ExclusiveStartKey = lastEvaluatedKey
                };

                var response = await _dynamoClient.QueryAsync(request);

                if (pageCount % 50 == 0)
                {
                    _logger.LogInformation("[AGREGACAO] Progresso da busca: {PageCount} páginas lidas. {TotalAcumulado} registros carregados até agora...", 
                        pageCount, resultados.Count);
                }

                // Converter AttributeValue para objetos Resultado
                foreach (var item in response.Items)
                {
                    // Parsear Nivel com validação (campo é String no DynamoDB)
                    int nivelParsed = 1; // Padrão: Nivel 1 (caso não exista)
                    if (item.ContainsKey("Nivel"))
                    {
                        // Nivel é salvo como String (S) no DynamoDB, valores de 1 a 5
                        if (int.TryParse(item["Nivel"].S, out var nivelTemp))
                        {
                            nivelParsed = nivelTemp;
                            
                            // Validar se Nivel é válido (1 a 5)
                            if (nivelParsed < 1 || nivelParsed > 5)
                            {
                                _logger.LogWarning(
                                    "Nivel fora do range valido (1-5): {Nivel}. Usando Nivel=1 como padrao. PK={PK}",
                                    nivelParsed, item.ContainsKey("PK") ? item["PK"].S : "unknown");
                                nivelParsed = 1;
                            }
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Falha ao parsear Nivel (valor: {Valor}). Usando Nivel=1 como padrao. PK={PK}",
                                item["Nivel"].S, item.ContainsKey("PK") ? item["PK"].S : "unknown");
                        }
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Campo Nivel nao encontrado no item. Usando Nivel=1 como padrao. PK={PK}",
                            item.ContainsKey("PK") ? item["PK"].S : "unknown");
                    }

                    // Parsear IdEmbaixadas (Lista no DynamoDB)
                    var idEmbaixadas = new List<string>();
                    if (item.ContainsKey("IdEmbaixadas") && item["IdEmbaixadas"].L != null)
                    {
                        foreach (var embaixadaValue in item["IdEmbaixadas"].L)
                        {
                            if (!string.IsNullOrEmpty(embaixadaValue.S))
                            {
                                idEmbaixadas.Add(embaixadaValue.S);
                            }
                        }
                    }
                    // Fallback: Se IdEmbaixadas não existe, usar IdEmbaixada (campo único)
                    else if (item.ContainsKey("IdEmbaixada") && !string.IsNullOrEmpty(item["IdEmbaixada"].S))
                    {
                        idEmbaixadas.Add(item["IdEmbaixada"].S);
                    }

                    var resultado = new Resultado
                    {
                        PK = item.ContainsKey("PK") ? item["PK"].S : string.Empty,
                        SK = item.ContainsKey("SK") ? item["SK"].S : string.Empty,
                        GSI1_PK = item.ContainsKey("GSI1_PK") ? item["GSI1_PK"].S : string.Empty,
                        GSI1_SK = item.ContainsKey("GSI1_SK") ? item["GSI1_SK"].S : string.Empty,
                        ExecucaoId = item.ContainsKey("ExecucaoId") ? item["ExecucaoId"].S : string.Empty,
                        VerificacaoId = item.ContainsKey("VerificacaoId") ? item["VerificacaoId"].S : string.Empty,
                        Empresa = item.ContainsKey("Empresa") ? item["Empresa"].S : string.Empty,
                        Tabela = item.ContainsKey("Tabela") ? item["Tabela"].S : string.Empty,
                        Campo = item.ContainsKey("Campo") ? item["Campo"].S : string.Empty,
                        Referencia = item.ContainsKey("Referencia") ? item["Referencia"].S : string.Empty,
                        TipoApontamento = item.ContainsKey("TipoApontamento") ? item["TipoApontamento"].S : string.Empty,
                        Nivel = nivelParsed,  // ← VALIDADO (nunca será 0)
                        DetalheErro = item.ContainsKey("DetalheErro") ? item["DetalheErro"].S : string.Empty,  // ← CORRIGIDO
                        ValorEncontrado = item.ContainsKey("ValorEncontrado") ? item["ValorEncontrado"].S : null,
                        ValorEsperado = item.ContainsKey("ValorEsperado") ? item["ValorEsperado"].S : null,
                        IdEmbaixada = idEmbaixadas.FirstOrDefault() ?? string.Empty,  // Compatibilidade
                        IdEmbaixadas = idEmbaixadas  // ← LISTA DE EMBAIXADAS
                    };

                    resultados.Add(resultado);
                }

                lastEvaluatedKey = response.LastEvaluatedKey;

            } while (lastEvaluatedKey != null && lastEvaluatedKey.Count > 0);

            _logger.LogInformation(
                "Busca no GSI concluida. Total de {Pages} páginas processadas. " +
                "Total de apontamentos encontrados: {Total}",
                pageCount, resultados.Count);
            
            // ✅ LOG DETALHADO: Contagem por empresa/verificacao para debug
            var contagemPorEmpresaVerif = resultados
                .GroupBy(r => new { r.Empresa, r.VerificacaoId })
                .Select(g => new { g.Key.Empresa, g.Key.VerificacaoId, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();
            
            foreach (var item in contagemPorEmpresaVerif)
            {
                _logger.LogDebug("Empresa={Empresa}, VerificacaoId={VerifId}: {Count} apontamentos",
                    item.Empresa, item.VerificacaoId, item.Count);
            }

            return resultados;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar apontamentos da execucao {ExecucaoId} no GSI", execucaoId);
            throw;
        }
    }


    /// <summary>
    /// ETAPA 2: Agrupa resultados em memória e calcula totais
    /// Cria DUAS estruturas: Segmentada (por embaixada) e Global (admin)
    /// </summary>
    private (
        Dictionary<string, GrupoAgregadoSegmentado> gruposSegmentados,
        Dictionary<string, GrupoAgregadoGlobal> gruposGlobais,
        int totalApontamentos,
        HashSet<string> empresas,
        HashSet<string> embaixadas
    ) AgruparResultados(List<Resultado> resultados)
    {
        try
        {
            var gruposSegmentados = new Dictionary<string, GrupoAgregadoSegmentado>();
            var gruposGlobais = new Dictionary<string, GrupoAgregadoGlobal>();
            var empresas = new HashSet<string>();
            var embaixadas = new HashSet<string>();
            var totalApontamentos = 0;

            _logger.LogDebug("Iniciando agrupamento de {Count} resultados em memoria (Segmentado + Global)", 
                resultados.Count);

            // Processar apontamentos e criar ambas estruturas
            foreach (var resultado in resultados)
            {
                // Criar chave de agrupamento
                var chave = ChaveAgrupamento.FromResultado(resultado);
                var chaveStr = chave.ToKey();

                // === AGREGAÇÃO SEGMENTADA (Usuário Comum) ===
                if (!gruposSegmentados.ContainsKey(chaveStr))
                {
                    gruposSegmentados[chaveStr] = new GrupoAgregadoSegmentado(chave);
                }
                
                // ✅ Primeira vez: Coletar descrição de erro (exemplo do grupo)
                if (gruposSegmentados[chaveStr].Quantidade == 0 && !string.IsNullOrEmpty(resultado.DetalheErro))
                {
                    gruposSegmentados[chaveStr].DescricaoErro = resultado.DetalheErro;
                }
                
                gruposSegmentados[chaveStr].Quantidade++;
                
                // ✅ CRÍTICO: Coletar IdEmbaixadas (Union)
                if (resultado.IdEmbaixadas != null && resultado.IdEmbaixadas.Any())
                {
                    _logger.LogDebug(
                        "Coletando {Count} IdEmbaixadas para grupo {Chave}. IdEmbaixadas: [{Ids}]",
                        resultado.IdEmbaixadas.Count, 
                        chaveStr,
                        string.Join(", ", resultado.IdEmbaixadas));
                    
                    foreach (var idEmbaixada in resultado.IdEmbaixadas)
                    {
                        if (!string.IsNullOrEmpty(idEmbaixada))
                        {
                            gruposSegmentados[chaveStr].IdEmbaixadas.Add(idEmbaixada);
                            embaixadas.Add(idEmbaixada);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Resultado sem IdEmbaixadas ou lista vazia. Grupo: {Chave}, Resultado.Empresa: {Empresa}, Resultado.VerificacaoId: {VerificacaoId}",
                        chaveStr, resultado.Empresa, resultado.VerificacaoId);
                }

                // === AGREGAÇÃO GLOBAL (Admin) ===
                if (!gruposGlobais.ContainsKey(chaveStr))
                {
                    gruposGlobais[chaveStr] = new GrupoAgregadoGlobal(chave);
                }
                
                // ✅ Primeira vez: Coletar descrição de erro (exemplo do grupo)
                if (gruposGlobais[chaveStr].Quantidade == 0 && !string.IsNullOrEmpty(resultado.DetalheErro))
                {
                    gruposGlobais[chaveStr].DescricaoErro = resultado.DetalheErro;
                }
                
                gruposGlobais[chaveStr].Quantidade++;

                totalApontamentos++;

                // Rastrear empresas únicas
                if (!string.IsNullOrEmpty(resultado.Empresa))
                {
                    empresas.Add(resultado.Empresa);
                }
            }

            _logger.LogInformation(
                "Agrupamento concluido. Segmentados: {Segmentados} grupos, Globais: {Globais} grupos, " +
                "Total: {Total} apontamentos, Empresas: {Empresas}, Embaixadas: {Embaixadas}",
                gruposSegmentados.Count, gruposGlobais.Count, totalApontamentos, 
                empresas.Count, embaixadas.Count);
            
            // ✅ VALIDACAO DE INTEGRIDADE: Verificar se contagem bate
            var totalContadoSegmentado = gruposSegmentados.Sum(g => g.Value.Quantidade);
            var totalContadoGlobal = gruposGlobais.Sum(g => g.Value.Quantidade);
            
            if (totalContadoSegmentado != resultados.Count || totalContadoGlobal != resultados.Count)
            {
                _logger.LogError(
                    "DISCREPANCIA DETECTADA! Buscados: {Buscados}, " +
                    "Segmentado: {Segmentado}, Global: {Global}",
                    resultados.Count, totalContadoSegmentado, totalContadoGlobal);
            }

            return (gruposSegmentados, gruposGlobais, totalApontamentos, empresas, embaixadas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao agrupar resultados em memoria");
            throw;
        }
    }

    /// <summary>
    /// ETAPA 2.5: Limpa registros antigos das empresas solicitadas
    /// Remove TODOS os registros (Segmentados + Global) de cada empresa
    /// Usa GSI_Empresa para buscar eficientemente todos os registros de cada empresa
    /// </summary>
    private async Task LimparResultadosAnterioresAsync(
        string empresasSolicitadas, 
        List<string> idEmbaixadasSolicitadas)
    {
        try
        {
            var siglasSolicitadas = ExtrairSiglasEmpresas(empresasSolicitadas);
            
            if (!siglasSolicitadas.Any())
            {
                _logger.LogWarning("Nenhuma empresa para limpar");
                return;
            }

            _logger.LogWarning(
                "╔════════════════════════════════════════════════════════════════════╗");
            _logger.LogWarning(
                "║ 🗑️  INICIANDO LIMPEZA DE REGISTROS ANTIGOS                        ║");
            _logger.LogWarning(
                "╠════════════════════════════════════════════════════════════════════╣");
            _logger.LogWarning(
                "║ Empresas: {Empresas}", 
                string.Join(", ", siglasSolicitadas).PadRight(54) + "║");
            _logger.LogWarning(
                "║ Embaixadas: {Embaixadas}", 
                idEmbaixadasSolicitadas.Count.ToString().PadRight(52) + "║");
            _logger.LogWarning(
                "╚════════════════════════════════════════════════════════════════════╝");

            int totalDeletados = 0;

            // Deletar registros GLOBAIS das empresas solicitadas
            totalDeletados += await LimparRegistrosGlobaisAsync(siglasSolicitadas);

            // Deletar registros SEGMENTADOS (por embaixada)
            totalDeletados += await LimparRegistrosSegmentadosAsync(
                siglasSolicitadas, 
                idEmbaixadasSolicitadas);

            _logger.LogWarning(
                "╔════════════════════════════════════════════════════════════════════╗");
            _logger.LogWarning(
                "║ ✅ LIMPEZA CONCLUÍDA                                               ║");
            _logger.LogWarning(
                "╠════════════════════════════════════════════════════════════════════╣");
            _logger.LogWarning(
                "║ Total Deletados: {Total}", 
                totalDeletados.ToString().PadRight(49) + "║");
            _logger.LogWarning(
                "║ Empresas: {Empresas}", 
                string.Join(", ", siglasSolicitadas).PadRight(54) + "║");
            _logger.LogWarning(
                "╚════════════════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar registros antigos");
            // Não propaga erro - limpeza não deve interromper o fluxo
        }
    }

    /// <summary>
    /// Limpa registros GLOBAIS das empresas solicitadas (PK = EXEC#id#EMB#GLOBAL)
    /// </summary>
    private async Task<int> LimparRegistrosGlobaisAsync(List<string> siglasSolicitadas)
    {
        int totalDeletados = 0;

        foreach (var sigla in siglasSolicitadas)
        {
            try
            {
                _logger.LogInformation(
                    "Iniciando limpeza GLOBAL para empresa {Empresa}. GSI2_PK = EMP#{Empresa}",
                    sigla, sigla);

                // Query usando GSI_Empresa para registros GLOBAIS
                var queryRequest = new QueryRequest
                {
                    TableName = _dynamoConfig.TableNameResultadoAgregado,  // ← Usa dynamoConfig!
                    IndexName = "GSI_EMPRESA",  // ← Nome correto (maiúsculo)
                    KeyConditionExpression = "GSI2_PK = :pk",
                    FilterExpression = "begins_with(PK, :pkGlobal)",  // Apenas registros GLOBAL
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":pk", new AttributeValue { S = $"EMP#{sigla}" } },
                        { ":pkGlobal", new AttributeValue { S = "EXEC#" } }
                    },
                    ProjectionExpression = "PK, SK"
                };

                _logger.LogDebug(
                    "Query GSI_Empresa: Table={Table}, Index={Index}, GSI2_PK={PK}",
                    queryRequest.TableName, queryRequest.IndexName, $"EMP#{sigla}");

                var response = await _dynamoClient.QueryAsync(queryRequest);
                
                _logger.LogInformation(
                    "Query GSI_Empresa retornou {Count} registros para empresa {Empresa}",
                    response.Items?.Count ?? 0, sigla);
                
                if (response.Items == null || !response.Items.Any())
                {
                    _logger.LogDebug("Nenhum registro encontrado para empresa {Empresa} (GLOBAL)", sigla);
                    continue;
                }
                
                // Filtrar apenas PKs que terminam com #EMB#GLOBAL
                var itensGlobais = response.Items
                    .Where(item => item["PK"].S.EndsWith("#EMB#GLOBAL"))
                    .ToList();

                _logger.LogWarning(
                    "Empresa {Empresa} (GLOBAL): {Total} registros encontrados no GSI, {Filtrados} são GLOBAIS",
                    sigla, response.Items?.Count ?? 0, itensGlobais.Count);
                
                // LOG DETALHADO: Listar TODOS os registros encontrados
                if (itensGlobais.Any())
                {
                    _logger.LogWarning("  Registros GLOBAIS encontrados para {Empresa}:", sigla);
                    foreach (var item in itensGlobais)
                    {
                        _logger.LogWarning("    - PK: {PK}", item["PK"].S);
                        _logger.LogWarning("      SK: {SK}", item["SK"].S);
                    }
                }

                // Deletar em lotes
                totalDeletados += await DeletarBatchAsync(itensGlobais, $"GLOBAL:{sigla}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar registros globais da empresa {Empresa}", sigla);
            }
        }

        return totalDeletados;
    }

    /// <summary>
    /// Limpa registros SEGMENTADOS das empresas solicitadas (PK = EXEC#id#EMB#embaixada)
    /// </summary>
    private async Task<int> LimparRegistrosSegmentadosAsync(
        List<string> siglasSolicitadas,
        List<string> idEmbaixadasSolicitadas)
    {
        int totalDeletados = 0;

        foreach (var sigla in siglasSolicitadas)
        {
            try
            {
                _logger.LogInformation(
                    "Iniciando limpeza SEGMENTADA para empresa {Empresa}. GSI2_PK = EMP#{Empresa}",
                    sigla, sigla);

                // Query usando GSI_Empresa para todos os registros
                var queryRequest = new QueryRequest
                {
                    TableName = _dynamoConfig.TableNameResultadoAgregado,  // ← Usa dynamoConfig!
                    IndexName = "GSI_EMPRESA",  // ← Nome correto (maiúsculo)
                    KeyConditionExpression = "GSI2_PK = :pk",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":pk", new AttributeValue { S = $"EMP#{sigla}" } }
                    },
                    ProjectionExpression = "PK, SK"
                };

                _logger.LogDebug(
                    "Query GSI_Empresa SEGMENTADO: Table={Table}, Index={Index}, GSI2_PK={PK}",
                    queryRequest.TableName, queryRequest.IndexName, $"EMP#{sigla}");

                var response = await _dynamoClient.QueryAsync(queryRequest);

                _logger.LogInformation(
                    "Query GSI_Empresa retornou {Count} registros totais para empresa {Empresa}",
                    response.Items?.Count ?? 0, sigla);

                if (response.Items == null || !response.Items.Any())
                {
                    _logger.LogDebug("Nenhum registro encontrado para empresa {Empresa} (SEGMENTADO)", sigla);
                    continue;
                }

                // Filtrar apenas PKs que contêm #EMB# mas NÃO terminam com GLOBAL
                var itensSegmentados = response.Items
                    .Where(item => 
                    {
                        var pk = item["PK"].S;
                        return pk.Contains("#EMB#") && !pk.EndsWith("#EMB#GLOBAL");
                    })
                    .ToList();

                _logger.LogWarning(
                    "Empresa {Empresa} (SEGMENTADOS): {Total} registros encontrados no GSI, {Filtrados} são SEGMENTADOS",
                    sigla, response.Items?.Count ?? 0, itensSegmentados.Count);
                
                // LOG DETALHADO: Listar TODOS os registros encontrados
                if (itensSegmentados.Any())
                {
                    _logger.LogWarning("  Registros SEGMENTADOS encontrados para {Empresa}:", sigla);
                    foreach (var item in itensSegmentados.Take(20))  // Limita a 20 para não poluir log
                    {
                        _logger.LogWarning("    - PK: {PK}", item["PK"].S);
                        _logger.LogWarning("      SK: {SK}", item["SK"].S);
                    }
                    if (itensSegmentados.Count > 20)
                    {
                        _logger.LogWarning("    ... e mais {Mais} registros", itensSegmentados.Count - 20);
                    }
                }

                // Deletar em lotes
                totalDeletados += await DeletarBatchAsync(itensSegmentados, $"SEGMENTADO:{sigla}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar registros segmentados da empresa {Empresa}", sigla);
            }
        }

        return totalDeletados;
    }

    /// <summary>
    /// Deleta uma lista de items em lotes de 25 (BatchWriteItem)
    /// </summary>
    private async Task<int> DeletarBatchAsync(List<Dictionary<string, AttributeValue>> items, string contexto)
    {
        int totalDeletados = 0;

        if (items == null || !items.Any())
        {
            _logger.LogDebug("Nenhum item para deletar ({Contexto})", contexto);
            return 0;
        }

        _logger.LogInformation(
            "Iniciando delecao de {Total} registros em lotes de 25 ({Contexto})",
            items.Count, contexto);

        for (int i = 0; i < items.Count; i += 25)
        {
            var batch = items.Skip(i).Take(25).ToList();

            var deleteRequests = batch.Select(item => new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "PK", item["PK"] },
                        { "SK", item["SK"] }
                    }
                }
            }).ToList();

            var batchRequest = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    { _dynamoConfig.TableNameResultadoAgregado, deleteRequests }  // ← Usa dynamoConfig!
                }
            };

            _logger.LogDebug(
                "Executando BatchWriteItem com {Count} deletes. Batch {Current}/{Total} ({Contexto})",
                batch.Count, (i / 25) + 1, (items.Count + 24) / 25, contexto);

            try
            {
                var response = await _dynamoClient.BatchWriteItemAsync(batchRequest);
                
                // Verificar se há itens não processados
                if (response.UnprocessedItems != null && response.UnprocessedItems.Any())
                {
                    _logger.LogWarning(
                        "BatchWriteItem tem {Count} itens nao processados ({Contexto})",
                        response.UnprocessedItems.Count, contexto);
                }
                
                totalDeletados += batch.Count;

                _logger.LogInformation(
                    "Batch de {Count} registros deletados com sucesso ({Contexto})", 
                    batch.Count, contexto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Erro ao deletar batch de {Count} registros ({Contexto})", 
                    batch.Count, contexto);
                throw;
            }
        }

        _logger.LogInformation(
            "Delecao concluida. {Total} registros deletados ({Contexto})",
            totalDeletados, contexto);

        return totalDeletados;
    }

    /// <summary>
    /// ETAPA 3: Insere resultados SEGMENTADOS (Usuário Comum)
    /// REPLICAÇÃO MÍNIMA: Para cada embaixada do grupo
    /// FILTRO: Apenas empresas solicitadas na execução
    /// DESNORMALIZAÇÃO: Se Empresa contém vírgulas ("MA,PI"), cria um registro para cada sigla
    /// </summary>
    private async Task InserirResultadosSegmentadosAsync(
        string execucaoId,
        Dictionary<string, GrupoAgregadoSegmentado> gruposSegmentados,
        string empresasSolicitadas)
    {
        try
        {
            var siglasSolicitadas = ExtrairSiglasEmpresas(empresasSolicitadas);
            
            _logger.LogInformation(
                "Inserindo grupos SEGMENTADOS na tabela ResultadoAgregado. " +
                "Total: {Total}, Empresas solicitadas: {Empresas}",
                gruposSegmentados.Count, string.Join(", ", siglasSolicitadas));

            var batch = new List<ResultadoAgregado>();
            var totalRegistrosSegmentados = 0;
            var totalReplicacoes = 0;
            var totalIgnorados = 0;

            // Iterar sobre cada grupo segmentado
            foreach (var grupoEntry in gruposSegmentados)
            {
                var grupo = grupoEntry.Value;
                var empresaOriginal = grupo.Chave.Empresa;
                
                // FILTRO: Verificar se empresa foi solicitada
                bool empresaFoiSolicitada = VerificarEmpresaSolicitada(
                    empresaOriginal, 
                    siglasSolicitadas);
                
                _logger.LogDebug(
                    "Verificando grupo. EmpresaOriginal: {Empresa}, SiglasSolicitadas: [{Siglas}], FoiSolicitada: {FoiSolicitada}, IdEmbaixadasCount: {Count}",
                    empresaOriginal,
                    string.Join(", ", siglasSolicitadas),
                    empresaFoiSolicitada,
                    grupo.IdEmbaixadas.Count);
                
                if (!empresaFoiSolicitada)
                {
                    totalIgnorados++;
                    _logger.LogWarning(
                        "Grupo IGNORADO (empresa nao solicitada): Empresa={Empresa}, SiglasSolicitadas=[{Siglas}]",
                        empresaOriginal,
                        string.Join(", ", siglasSolicitadas));
                    continue;
                }

                // REPLICAÇÃO MÍNIMA: Para cada embaixada deste grupo
                // FALLBACK: Se não houver embaixadas, usar string vazia para criar pelo menos um registro
                var embaixadasParaReplicar = grupo.IdEmbaixadas.Any() 
                    ? grupo.IdEmbaixadas.ToList() 
                    : new List<string> { string.Empty }; // Fallback para garantir inserção
                
                if (!grupo.IdEmbaixadas.Any())
                {
                    _logger.LogWarning(
                        "Grupo SEM IdEmbaixadas! Usando fallback. Grupo: {Chave}, Empresa: {Empresa}, Quantidade: {Quantidade}",
                        grupo.Chave.ToKey(), empresaOriginal, grupo.Quantidade);
                }
                else
                {
                    _logger.LogDebug(
                        "Grupo com {Count} IdEmbaixadas. Empresa: {Empresa}, IdEmbaixadas: [{Ids}]",
                        grupo.IdEmbaixadas.Count, 
                        empresaOriginal,
                        string.Join(", ", grupo.IdEmbaixadas));
                }
                
                foreach (var idEmbaixada in embaixadasParaReplicar)
                {
                    // DESNORMALIZAÇÃO: Se empresa tem múltiplas siglas
                    if (empresaOriginal.Contains(','))
                    {
                        var siglas = empresaOriginal
                            .Split(',')
                            .Select(s => s.Trim().ToUpper())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Where(s => siglasSolicitadas.Contains(s))  // ← FILTRO
                            .ToList();

                        foreach (var sigla in siglas)
                        {
                            var item = new ResultadoAgregado
                            {
                                PK = ResultadoAgregado.CriarPKSegmentado(execucaoId, idEmbaixada),
                                SK = ResultadoAgregado.CriarSK(
                                    sigla,
                                    grupo.Chave.VerificacaoId,
                                    grupo.Chave.Tabela,
                                    grupo.Chave.Campo,
                                    grupo.Chave.Referencia,
                                    grupo.Chave.TipoApontamento),
                                GSI2_PK = ResultadoAgregado.CriarGSI2_PK(sigla),
                                GSI2_SK = ResultadoAgregado.CriarGSI2_SK(
                                    execucaoId,
                                    grupo.Chave.VerificacaoId,
                                    grupo.Chave.Tabela,
                                    grupo.Chave.Campo,
                                    grupo.Chave.Referencia),
                                Quantidade = grupo.Quantidade,
                                ExecucaoId = execucaoId,
                                Empresa = sigla,
                                VerificacaoId = grupo.Chave.VerificacaoId,
                                Tabela = grupo.Chave.Tabela,
                                Campo = grupo.Chave.Campo,
                                Referencia = grupo.Chave.Referencia,
                                TipoApontamento = grupo.Chave.TipoApontamento,
                                Nivel = grupo.Chave.Nivel,
                                DescricaoErro = grupo.DescricaoErro,  // ← ADICIONADO
                                DataCriacao = DateTime.UtcNow
                            };

                            batch.Add(item);
                            totalReplicacoes++;
                        }
                    }
                    else
                    {
                        // Empresa única
                        var item = new ResultadoAgregado
                        {
                            PK = ResultadoAgregado.CriarPKSegmentado(execucaoId, idEmbaixada),
                            SK = ResultadoAgregado.CriarSK(
                                empresaOriginal,
                                grupo.Chave.VerificacaoId,
                                grupo.Chave.Tabela,
                                grupo.Chave.Campo,
                                grupo.Chave.Referencia,
                                grupo.Chave.TipoApontamento),
                            GSI2_PK = ResultadoAgregado.CriarGSI2_PK(empresaOriginal),
                            GSI2_SK = ResultadoAgregado.CriarGSI2_SK(
                                execucaoId,
                                grupo.Chave.VerificacaoId,
                                grupo.Chave.Tabela,
                                grupo.Chave.Campo,
                                grupo.Chave.Referencia),
                            Quantidade = grupo.Quantidade,
                            ExecucaoId = execucaoId,
                            Empresa = empresaOriginal,
                            VerificacaoId = grupo.Chave.VerificacaoId,
                            Tabela = grupo.Chave.Tabela,
                            Campo = grupo.Chave.Campo,
                            Referencia = grupo.Chave.Referencia,
                            TipoApontamento = grupo.Chave.TipoApontamento,
                            Nivel = grupo.Chave.Nivel,
                            DescricaoErro = grupo.DescricaoErro,  // ← ADICIONADO
                            DataCriacao = DateTime.UtcNow
                        };

                        batch.Add(item);
                        totalReplicacoes++;
                    }

                    // Inserir em lotes
                    if (batch.Count >= _config.BatchSize)
                    {
                        await InserirBatchAsync(batch);
                        totalRegistrosSegmentados += batch.Count;
                        batch.Clear();
                    }
                }
            }

            // Inserir registros restantes
            if (batch.Any())
            {
                await InserirBatchAsync(batch);
                totalRegistrosSegmentados += batch.Count;
            }

            _logger.LogInformation(
                "Insercao SEGMENTADA concluida. {Total} registros inseridos, {Ignorados} grupos ignorados " +
                "(de {Grupos} grupos base, replicados para {Replicacoes} embaixadas)",
                totalRegistrosSegmentados, totalIgnorados, gruposSegmentados.Count, totalReplicacoes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inserir resultados segmentados");
            throw;
        }
    }

    /// <summary>
    /// ETAPA 4: Insere resultados GLOBAIS (Admin)
    /// SEM REPLICAÇÃO: Um registro por grupo (PK = EXEC#id#EMB#GLOBAL)
    /// FILTRO: Apenas empresas solicitadas na execução
    /// DESNORMALIZAÇÃO: Se Empresa contém vírgulas ("MA,PI"), cria um registro para cada sigla
    /// </summary>
    private async Task InserirResultadosGlobaisAsync(
        string execucaoId,
        Dictionary<string, GrupoAgregadoGlobal> gruposGlobais,
        string empresasSolicitadas)
    {
        try
        {
            var siglasSolicitadas = ExtrairSiglasEmpresas(empresasSolicitadas);
            
            _logger.LogInformation(
                "Inserindo grupos GLOBAIS na tabela ResultadoAgregado. " +
                "Total: {Total}, Empresas solicitadas: {Empresas}",
                gruposGlobais.Count, string.Join(", ", siglasSolicitadas));

            var batch = new List<ResultadoAgregado>();
            var totalRegistrosGlobais = 0;
            var totalIgnorados = 0;

            // Iterar sobre cada grupo global
            foreach (var grupoEntry in gruposGlobais)
            {
                var grupo = grupoEntry.Value;
                var empresaOriginal = grupo.Chave.Empresa;
                
                // FILTRO: Verificar se empresa foi solicitada
                bool empresaFoiSolicitada = VerificarEmpresaSolicitada(
                    empresaOriginal, 
                    siglasSolicitadas);
                
                if (!empresaFoiSolicitada)
                {
                    totalIgnorados++;
                    _logger.LogDebug(
                        "Grupo GLOBAL IGNORADO (empresa nao solicitada): Empresa={Empresa}",
                        empresaOriginal);
                    continue;
                }

                // DESNORMALIZAÇÃO: Se empresa tem múltiplas siglas
                if (empresaOriginal.Contains(','))
                {
                    var siglas = empresaOriginal
                        .Split(',')
                        .Select(s => s.Trim().ToUpper())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Where(s => siglasSolicitadas.Contains(s))
                        .ToList();

                    foreach (var sigla in siglas)
                    {
                        var item = new ResultadoAgregado
                        {
                            PK = ResultadoAgregado.CriarPKGlobal(execucaoId),
                            SK = ResultadoAgregado.CriarSK(
                                sigla,
                                grupo.Chave.VerificacaoId,
                                grupo.Chave.Tabela,
                                grupo.Chave.Campo,
                                grupo.Chave.Referencia,
                                grupo.Chave.TipoApontamento),
                            GSI2_PK = ResultadoAgregado.CriarGSI2_PK(sigla),
                            GSI2_SK = ResultadoAgregado.CriarGSI2_SK(
                                execucaoId,
                                grupo.Chave.VerificacaoId,
                                grupo.Chave.Tabela,
                                grupo.Chave.Campo,
                                grupo.Chave.Referencia),
                            Quantidade = grupo.Quantidade,
                            ExecucaoId = execucaoId,
                            Empresa = sigla,
                            VerificacaoId = grupo.Chave.VerificacaoId,
                            Tabela = grupo.Chave.Tabela,
                            Campo = grupo.Chave.Campo,
                            Referencia = grupo.Chave.Referencia,
                            TipoApontamento = grupo.Chave.TipoApontamento,
                            Nivel = grupo.Chave.Nivel,
                            DescricaoErro = grupo.DescricaoErro,  // ← ADICIONADO
                            DataCriacao = DateTime.UtcNow
                        };

                        batch.Add(item);
                    }
                }
                else
                {
                    // Empresa única
                    var item = new ResultadoAgregado
                    {
                        PK = ResultadoAgregado.CriarPKGlobal(execucaoId),
                        SK = ResultadoAgregado.CriarSK(
                            empresaOriginal,
                            grupo.Chave.VerificacaoId,
                            grupo.Chave.Tabela,
                            grupo.Chave.Campo,
                            grupo.Chave.Referencia,
                            grupo.Chave.TipoApontamento),
                        GSI2_PK = ResultadoAgregado.CriarGSI2_PK(empresaOriginal),
                        GSI2_SK = ResultadoAgregado.CriarGSI2_SK(
                            execucaoId,
                            grupo.Chave.VerificacaoId,
                            grupo.Chave.Tabela,
                            grupo.Chave.Campo,
                            grupo.Chave.Referencia),
                        Quantidade = grupo.Quantidade,
                        ExecucaoId = execucaoId,
                        Empresa = empresaOriginal,
                        VerificacaoId = grupo.Chave.VerificacaoId,
                        Tabela = grupo.Chave.Tabela,
                        Campo = grupo.Chave.Campo,
                        Referencia = grupo.Chave.Referencia,
                        TipoApontamento = grupo.Chave.TipoApontamento,
                        Nivel = grupo.Chave.Nivel,
                        DescricaoErro = grupo.DescricaoErro,  // ← ADICIONADO
                        DataCriacao = DateTime.UtcNow
                    };

                    batch.Add(item);
                }

                // Inserir em lotes
                if (batch.Count >= _config.BatchSize)
                {
                    await InserirBatchAsync(batch);
                    totalRegistrosGlobais += batch.Count;
                    batch.Clear();
                }
            }

            // Inserir registros restantes
            if (batch.Any())
            {
                await InserirBatchAsync(batch);
                totalRegistrosGlobais += batch.Count;
            }

            _logger.LogInformation(
                "Insercao GLOBAL concluida. {Total} registros inseridos, {Ignorados} grupos ignorados " +
                "(de {Grupos} grupos base)",
                totalRegistrosGlobais, totalIgnorados, gruposGlobais.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inserir resultados globais");
            throw;
        }
    }

    private async Task InserirBatchAsync(List<ResultadoAgregado> batch)
    {
        try
        {
            _logger.LogDebug("Inserindo batch de {Count} itens", batch.Count);

            foreach (var item in batch)
            {
                await _dynamoDbService.SaveAsync(item);
            }

            _logger.LogDebug("Batch de {Count} itens inserido com sucesso", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inserir batch");
            throw;
        }
    }

    /// <summary>
    /// Extrai lista de siglas de empresas de uma string (ex: "MA,PI,RS" → ["MA", "PI", "RS"])
    /// </summary>
    private List<string> ExtrairSiglasEmpresas(string empresasString)
    {
        if (string.IsNullOrEmpty(empresasString))
            return new List<string>();

        return empresasString
            .Split(',')
            .Select(s => s.Trim().ToUpper())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Verifica se a empresa (ou alguma de suas siglas) foi solicitada na execução
    /// </summary>
    private bool VerificarEmpresaSolicitada(string empresaOriginal, List<string> siglasSolicitadas)
    {
        if (string.IsNullOrEmpty(empresaOriginal))
            return false;

        // Se empresa tem múltiplas siglas (ex: "MA,PI")
        if (empresaOriginal.Contains(','))
        {
            var siglas = empresaOriginal
                .Split(',')
                .Select(s => s.Trim().ToUpper())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            // Verificar se ALGUMA sigla foi solicitada
            return siglas.Any(s => siglasSolicitadas.Contains(s));
        }
        else
        {
            // Empresa única
            return siglasSolicitadas.Contains(empresaOriginal.ToUpper());
        }
    }

}


