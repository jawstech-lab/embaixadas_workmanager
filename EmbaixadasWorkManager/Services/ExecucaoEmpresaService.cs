using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager.Services;

/// <summary>
/// Serviço responsável por gerenciar as operações de execução por empresa.
/// Implementa a lógica de gravação nas tabelas ExecucaoResumoView e ExecucaoEmpresaStatus.
/// </summary>
public class ExecucaoEmpresaService : IExecucaoEmpresaService
{
    private readonly ILogger<ExecucaoEmpresaService> _logger;
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly DynamoDbConfiguration _config;

    public ExecucaoEmpresaService(
        ILogger<ExecucaoEmpresaService> logger,
        IAmazonDynamoDB dynamoClient,
        IOptions<DynamoDbConfiguration> config)
    {
        _logger = logger;
        _dynamoClient = dynamoClient;
        _config = config.Value;
    }

    /// <summary>
    /// Grava registros nas tabelas ExecucaoResumoView e ExecucaoEmpresaStatus
    /// para cada embaixada e empresa extraída da execução.
    /// </summary>
    public async Task GravarExecucaoPorEmbaixadasEEmpresasAsync(
        string execucaoId,
        List<string> idEmbaixadas,
        string empresasString, 
        DateTime dataSolicitacao, 
        string status)
    {
        try
        {
            _logger.LogInformation(
                "Iniciando gravacao de execucao por embaixadas e empresas. " +
                "ExecucaoId: {ExecucaoId}, Embaixadas: {Embaixadas}, Empresas: {Empresas}", 
                execucaoId, idEmbaixadas.Count, empresasString);

            // Extrair siglas das empresas
            var siglas = ExtrairSiglasEmpresas(empresasString);
            
            if (!siglas.Any())
            {
                _logger.LogWarning("Nenhuma sigla de empresa encontrada para processar. ExecucaoId: {ExecucaoId}", execucaoId);
                return;
            }

            if (!idEmbaixadas.Any())
            {
                _logger.LogWarning("Nenhuma embaixada encontrada para processar. ExecucaoId: {ExecucaoId}", execucaoId);
                return;
            }

            _logger.LogInformation(
                "Encontradas {Embaixadas} embaixadas e {Empresas} empresas para processar. " +
                "Total de combinacoes: {Total}",
                idEmbaixadas.Count, siglas.Count, idEmbaixadas.Count * siglas.Count);

            // Contadores de sucesso/falha
            var sucessoStatus = 0;
            var sucessoResumo = 0;
            var falhasStatus = 0;
            var falhasResumo = 0;

            // Iterar sobre cada embaixada e cada empresa
            foreach (var idEmbaixada in idEmbaixadas)
            {
                foreach (var sigla in siglas)
                {
                    try
                    {
                        _logger.LogDebug("Processando embaixada {Embaixada}, empresa: {Sigla}", 
                            idEmbaixada, sigla);

                        // AÇÃO 1: Gravar Status Detalhado (ExecucaoEmpresaStatus)
                        var statusGravado = await GravarExecucaoEmpresaStatusAsync(
                            execucaoId, sigla, dataSolicitacao, status);
                        if (statusGravado)
                        {
                            sucessoStatus++;
                        }
                        else
                        {
                            falhasStatus++;
                        }

                        // AÇÃO 2: Atualização Condicional (ExecucaoResumoView)
                        var resumoAtualizado = await AtualizarExecucaoResumoViewAsync(
                            execucaoId, idEmbaixada, sigla, dataSolicitacao, status);
                        if (resumoAtualizado)
                        {
                            sucessoResumo++;
                        }
                        else
                        {
                            falhasResumo++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, 
                            "Erro ao processar embaixada {Embaixada}, empresa {Sigla} para execucao {ExecucaoId}", 
                            idEmbaixada, sigla, execucaoId);
                        falhasStatus++;
                        falhasResumo++;
                        // Continua processando as outras combinações
                    }
                }
            }

            _logger.LogInformation(
                "Gravacao concluida. ExecucaoId: {ExecucaoId}. " +
                "Embaixadas: {Embaixadas}, Empresas: {Empresas}, Combinacoes: {Total}. " +
                "Status: {SucessoStatus} sucesso, {FalhasStatus} falhas. " +
                "Resumo: {SucessoResumo} atualizado, {FalhasResumo} nao atualizado", 
                execucaoId, idEmbaixadas.Count, siglas.Count, idEmbaixadas.Count * siglas.Count,
                sucessoStatus, falhasStatus, sucessoResumo, falhasResumo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gravar execucao por empresas. ExecucaoId: {ExecucaoId}", execucaoId);
            throw;
        }
    }

    /// <summary>
    /// Grava um registro na tabela ExecucaoEmpresaStatus (auditoria completa).
    /// Sempre grava, criando histórico completo de todas as execuções.
    /// </summary>
    private async Task<bool> GravarExecucaoEmpresaStatusAsync(
        string execucaoId, 
        string sigla, 
        DateTime dataSolicitacao, 
        string status)
    {
        try
        {
            var pk = ExecucaoEmpresaStatus.CriarPK(sigla);
            var sk = ExecucaoEmpresaStatus.CriarSK(dataSolicitacao, execucaoId);
            var dataFormatada = dataSolicitacao.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            var request = new PutItemRequest
            {
                TableName = _config.TableNameExecucaoEmpresaStatus,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "PK_STATUS", new AttributeValue { S = pk } },
                    { "SK_STATUS", new AttributeValue { S = sk } },
                    { "ExecucaoId", new AttributeValue { S = execucaoId } },
                    { "DataSolicitacao", new AttributeValue { S = dataFormatada } },
                    { "Status", new AttributeValue { S = status } },
                    { "SiglaEmpresa", new AttributeValue { S = sigla } }
                }
            };

            await _dynamoClient.PutItemAsync(request);
            
            _logger.LogDebug("Status detalhado gravado com sucesso: Empresa={Sigla}, PK={PK}, SK={SK}, Status={Status}", 
                sigla, pk, sk, status);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gravar status detalhado para empresa {Sigla}", sigla);
            return false;
        }
    }

    /// <summary>
    /// Atualiza registro na tabela ExecucaoResumoView (última execução).
    /// Usa condição para atualizar apenas se a data for mais recente ou se não existir.
    /// NOVA ESTRUTURA: PK inclui IdEmbaixada (VIEW#LAST_EXEC#EMB#<IdEmbaixada>)
    /// </summary>
    private async Task<bool> AtualizarExecucaoResumoViewAsync(
        string execucaoId,
        string idEmbaixada,
        string sigla, 
        DateTime dataSolicitacao, 
        string status)
    {
        try
        {
            var pk = ExecucaoResumoView.CriarPK(idEmbaixada);
            var sk = ExecucaoResumoView.CriarSK(sigla);
            var dataFormatada = dataSolicitacao.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            var request = new PutItemRequest
            {
                TableName = _config.TableNameExecucaoResumoView,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "PK_VIEW", new AttributeValue { S = pk } },
                    { "SK_VIEW", new AttributeValue { S = sk } },
                    { "ExecucaoId", new AttributeValue { S = execucaoId } },
                    { "DataSolicitacao", new AttributeValue { S = dataFormatada } },
                    { "Status", new AttributeValue { S = status } },
                    { "SiglaEmpresa", new AttributeValue { S = sigla } },
                    { "IdEmbaixada", new AttributeValue { S = idEmbaixada } }
                },
                // CONDIÇÃO: Atualiza apenas se não existe ou se a data é mais recente
                ConditionExpression = "attribute_not_exists(DataSolicitacao) OR :novaData > DataSolicitacao",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":novaData", new AttributeValue { S = dataFormatada } }
                }
            };

            await _dynamoClient.PutItemAsync(request);
            
            _logger.LogDebug(
                "Resumo atualizado condicionalmente: Embaixada={Embaixada}, Empresa={Sigla}, PK={PK}, SK={SK}, Data={Data}", 
                idEmbaixada, sigla, pk, sk, dataFormatada);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            _logger.LogDebug(
                "Condicao nao atendida para embaixada {Embaixada}, empresa {Sigla}. Registro existente e mais recente ou igual", 
                idEmbaixada, sigla);
            // Não é um erro - significa que já existe um registro mais recente
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar resumo para embaixada {Embaixada}, empresa {Sigla}", 
                idEmbaixada, sigla);
            return false;
        }
    }

    /// <summary>
    /// Atualiza o status final nas tabelas ExecucaoResumoView e ExecucaoEmpresaStatus
    /// após a finalização da execução.
    /// NOTA: TotalApontamentos NÃO é atualizado (calcular somando QTDs do ResultadoAgregado)
    /// </summary>
    public async Task AtualizarStatusFinalAsync(
        string execucaoId,
        List<string> idEmbaixadas,
        string empresasString,
        string statusFinal,
        int totalApontamentos)
    {
        try
        {
            _logger.LogInformation(
                "Atualizando status final nas tabelas de performance. " +
                "ExecucaoId: {ExecucaoId}, Status: {Status}",
                execucaoId, statusFinal);

            var siglas = ExtrairSiglasEmpresas(empresasString);

            if (!siglas.Any() || !idEmbaixadas.Any())
            {
                _logger.LogWarning("Nenhuma embaixada ou empresa para atualizar status final");
                return;
            }

            var sucessos = 0;
            var falhas = 0;

            // Atualizar ExecucaoResumoView para cada combinação
            foreach (var idEmbaixada in idEmbaixadas)
            {
                foreach (var sigla in siglas)
                {
                    try
                    {
                        var pk = ExecucaoResumoView.CriarPK(idEmbaixada);
                        var sk = ExecucaoResumoView.CriarSK(sigla);

                        var request = new UpdateItemRequest
                        {
                            TableName = _config.TableNameExecucaoResumoView,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                { "PK_VIEW", new AttributeValue { S = pk } },
                                { "SK_VIEW", new AttributeValue { S = sk } }
                            },
                            UpdateExpression = "SET #status = :status, #total = :total",
                            ConditionExpression = "ExecucaoId = :execId",
                            ExpressionAttributeNames = new Dictionary<string, string>
                            {
                                { "#status", "Status" },
                                { "#total", "TotalApontamentos" }
                            },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                { ":status", new AttributeValue { S = statusFinal } },
                                { ":total", new AttributeValue { N = totalApontamentos.ToString() } },
                                { ":execId", new AttributeValue { S = execucaoId } }
                            }
                        };

                        await _dynamoClient.UpdateItemAsync(request);
                        sucessos++;

                        _logger.LogDebug(
                            "Status final atualizado: Embaixada={Embaixada}, Empresa={Empresa}, Status={Status}",
                            idEmbaixada, sigla, statusFinal);
                    }
                    catch (ConditionalCheckFailedException)
                    {
                        falhas++;
                        _logger.LogDebug(
                            "Condicao nao atendida ao atualizar status final: " +
                            "Embaixada={Embaixada}, Empresa={Empresa}. Execucao mais recente ja processada.",
                            idEmbaixada, sigla);
                    }
                    catch (ResourceNotFoundException)
                    {
                        falhas++;
                        _logger.LogWarning(
                            "Registro nao encontrado ao atualizar status final: " +
                            "Embaixada={Embaixada}, Empresa={Empresa}. Pode nao ter sido criado.",
                            idEmbaixada, sigla);
                    }
                }
            }

            _logger.LogInformation(
                "Atualizacao de status final concluida. " +
                "Sucessos: {Sucessos}, Falhas: {Falhas}",
                sucessos, falhas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar status final. ExecucaoId: {ExecucaoId}", execucaoId);
            // Não propaga erro - atualização de status não deve interromper o fluxo
        }
    }

    /// <summary>
    /// Extrai siglas de empresas de uma string.
    /// Suporta separadores: vírgula, ponto e vírgula, pipe
    /// Remove espaços, converte para maiúsculas e remove duplicatas.
    /// </summary>
    public List<string> ExtrairSiglasEmpresas(string empresasString)
    {
        if (string.IsNullOrWhiteSpace(empresasString))
        {
            return new List<string>();
        }

        try
        {
            // Remove espaços e divide por separadores
            var siglas = empresasString
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpper())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            _logger.LogDebug("Siglas extraidas: {Siglas} (Total: {Count})", 
                string.Join(", ", siglas), siglas.Count);

            return siglas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao extrair siglas da string: {EmpresasString}", empresasString);
            return new List<string>();
        }
    }
}


