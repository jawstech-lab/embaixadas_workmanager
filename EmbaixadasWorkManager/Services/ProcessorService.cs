using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using EmbaixadasWorkManager.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EmbaixadasWorkManager.Services;

public class ProcessorService : IExecucaoProcessorService
{
    private readonly ILogger<ProcessorService> _logger;
    private readonly IDynamoDbService _dynamoDbService;
    private readonly IVerificacaoProcessorService _verificacaoProcessor;
    private readonly ISqsService _sqsService;
    private readonly ProcessamentoConfiguration _processamentoConfig;

    public ProcessorService(
        ILogger<ProcessorService> logger,
        IDynamoDbService dynamoDbService,
        IVerificacaoProcessorService verificacaoProcessor,
        ISqsService sqsService,
        IOptions<ProcessamentoConfiguration> processamentoConfig)
    {
        _logger = logger;
        _dynamoDbService = dynamoDbService;
        _verificacaoProcessor = verificacaoProcessor;
        _sqsService = sqsService;
        _processamentoConfig = processamentoConfig.Value;
    }

    public async Task<bool> ProcessExecucaoMessageAsync(string messageBody, string messageId)
    {
        try
        {
            _logger.LogInformation("Processando mensagem de execução: {MessageId}", messageId);

            // A mensagem SQS contém apenas o ID da execução como string pura
            var execucaoId = messageBody.Trim();
            
            // Validar a mensagem
            if (!ValidateExecucaoMessage(execucaoId))
            {
                _logger.LogError("Mensagem inválida: {MessageId}. ExecucaoId: {ExecucaoId}", messageId, execucaoId);
                return false;
            }

            // Buscar a execução existente no DynamoDB
            var execucao = await _dynamoDbService.GetExecucaoAsync(execucaoId);
            if (execucao == null)
            {
                _logger.LogError("Execução não encontrada no DynamoDB: {ExecucaoId}", execucaoId);
                return false;
            }

            // Verificar se a execução já foi processada
            if (execucao.Status != StatusExecucao.Pendente && execucao.Status != StatusExecucao.Cadastrado )
            {
                _logger.LogWarning("Execução já foi processada: {ExecucaoId}. Status atual: {Status}", 
                    execucao.Id, execucao.Status);
                return true; // Retorna true pois não é um erro, apenas já foi processada
            }

            // Definir status como em processamento e data de início
            execucao.Status = StatusExecucao.EmProcessamento;
            execucao.DataInicio = DateTime.UtcNow;

            // Atualizar no DynamoDB
            await _dynamoDbService.UpdateAsync(execucao);

            // Processar a execução (verificações e queries)
            var processResult = await ProcessExecucaoAsync(execucao);

            // CONSOLIDAÇÃO: Atualizar execução com contadores consolidados
            execucao.Status = StatusExecucao.AguardandoProcessamento;
            execucao.QuantidadeVerificacoes = execucao.Validacoes.Count;
            execucao.VerificacoesProcessadas = 0;
            execucao.VerificacoesComErro = 0;
            execucao.DataInicioProcessamento = DateTime.UtcNow;

            // Enviar mensagens para fila-execucao-processo (uma por verificação)
            //var filaProcesso = "fila-execucao-processo-dev"; // TODO: Mover para configuração
            //foreach (var verificacaoId in execucao.Validacoes)
            //{
            //    var mensagemProcesso = new ProcessoMessage
            //    {
            //        ExecucaoId = execucao.Id,
            //        VerificacaoId = verificacaoId,
            //        IsSuccess = false, // Será atualizado quando processado
            //        Timestamp = DateTime.UtcNow
            //    };
                
            //    await _sqsService.SendMessageAsync(filaProcesso, JsonSerializer.Serialize(mensagemProcesso));
            //}

            // Atualizar execução no DynamoDB com os campos consolidados
            await _dynamoDbService.UpdateAsync(execucao);

            _logger.LogInformation("Execução processada com sucesso: {ExecucaoId}. Status: {Status}. Verificações enviadas: {Verificacoes}", 
                execucao.Id, execucao.Status, execucao.QuantidadeVerificacoes);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem de execução: {MessageId}", messageId);
            return false;
        }
    }

    private bool ValidateExecucaoMessage(string execucaoId)
    {
        if (string.IsNullOrWhiteSpace(execucaoId))
        {
            _logger.LogError("ExecucaoId é obrigatório");
            return false;
        }

        // Validar se é um GUID válido
        if (!Guid.TryParse(execucaoId, out _))
        {
            _logger.LogError("ExecucaoId deve ser um GUID válido: {ExecucaoId}", execucaoId);
            return false;
        }

        return true;
    }

    private async Task<ProcessResult> ProcessExecucaoAsync(Execucao execucao)
    {
        try
        {
            _logger.LogInformation("Iniciando processamento da execução: {ExecucaoId}", execucao.Id);
            _logger.LogInformation("Base: {Base}, Empresa: {Empresa}, Validações: {ValidacoesCount}", 
                execucao.Base, execucao.Empresa, execucao.Validacoes.Count);

            var queriesEnviadas = 0;
            var validacoesParaProcessar = new List<string>();

            // Verificar se a execução tem validações específicas
            if (execucao.Validacoes != null && execucao.Validacoes.Any())
            {
                _logger.LogInformation("Execução tem {Count} validações específicas", execucao.Validacoes.Count);
                validacoesParaProcessar = execucao.Validacoes;
            }
            else if (_processamentoConfig.BuscarTodasVerificacoesSeVazio)
            {
                List<Verificacao> todasVerificacoes;
                
                // Verificar se a execução tem filtro de embaixadas
                if (execucao.IdEmbaixadas != null && execucao.IdEmbaixadas.Any())
                {
                    _logger.LogInformation("Execução sem validações específicas. Buscando verificações filtradas por {Count} embaixadas", 
                        execucao.IdEmbaixadas.Count);
                    _logger.LogDebug("Embaixadas da execução: {Embaixadas}", string.Join(", ", execucao.IdEmbaixadas));
                    
                    // Buscar verificações filtradas por embaixadas
                    todasVerificacoes = await _dynamoDbService.GetVerificacoesPorEmbaixadasAsync(execucao.IdEmbaixadas);
                }
                else
                {
                    _logger.LogInformation("Execução sem validações específicas e sem filtro de embaixadas. Buscando todas as verificações disponíveis");
                    
                    // Buscar todas as verificações disponíveis
                    todasVerificacoes = await _dynamoDbService.GetTodasVerificacoesAsync();
                }
                
                validacoesParaProcessar = todasVerificacoes.Select(v => v.Id).ToList();
                
                /* 🔍 TODO LOG TEMPORÁRIO: Listar todas as validações disponíveis
                _logger.LogInformation("🔍 Validações disponíveis ({Total}):", todasVerificacoes.Count);
                foreach (var v in todasVerificacoes.Take(10)) // Mostra apenas as primeiras 10
                {
                    _logger.LogInformation("🔍 - ID: {Id} | Nome: {Nome}", v.Id, v.NomeVerificacao);
                }
                if (todasVerificacoes.Count > 10)
                {
                    _logger.LogInformation("🔍 ... e mais {Count} validações", todasVerificacoes.Count - 10);
                }
                
                // 🧪 FILTRO TEMPORÁRIO PARA TESTE - REMOVER DEPOIS
                var idValidacaoTeste = "07f19dda-4778-48de-8629-1fa322c71ed0"; // ⚠️ ALTERE AQUI com o ID exato
                var validacaoEncontrada = todasVerificacoes.FirstOrDefault(v => v.Id == idValidacaoTeste);
                
                if (validacaoEncontrada != null)
                {
                    validacoesParaProcessar = new List<string> { idValidacaoTeste };
                    _logger.LogWarning("🧪 MODO TESTE: Processando apenas validação {Id} - {Nome}", 
                        validacaoEncontrada.Id, validacaoEncontrada.NomeVerificacao);
                }
                else
                {
                    _logger.LogWarning("🧪 MODO TESTE: Validação com ID '{IdValidacao}' não encontrada. Processando todas as {Total} validações.", 
                        idValidacaoTeste, todasVerificacoes.Count);
                }
                // 🧪 TODO FIM DO FILTRO TEMPORÁRIO */
                
                // Atualizar a execução com as validações encontradas
                execucao.Validacoes = validacoesParaProcessar;
                
                _logger.LogInformation("Encontradas {Count} verificações para processar", validacoesParaProcessar.Count);
            }
            else
            {
                _logger.LogWarning("Execução sem validações e busca automática desabilitada. Nenhuma verificação será processada.");
                validacoesParaProcessar = new List<string>();
            }

            // Iterar sobre cada validação, delegando ao VerificacaoProcessor
            foreach (var verificacaoId in validacoesParaProcessar)
            {
                try
                {
                    var enviadas = await _verificacaoProcessor.ProcessarVerificacaoAsync(execucao, verificacaoId);
                    if (enviadas > 0)
                    {
                        queriesEnviadas += enviadas;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar verificação {VerificacaoId}", verificacaoId);
                }
            }

            var result = new
            {
                ExecucaoId = execucao.Id,
                ValidacoesProcessadas = 0,
                QueriesEnviadas = queriesEnviadas,
                ProcessadoEm = DateTime.UtcNow
            };

            _logger.LogInformation("Processamento concluído: {ExecucaoId}. Validações: {Validacoes}", 
                execucao.Id, queriesEnviadas);

            return new ProcessResult
            {
                Success = true,
                Result = System.Text.Json.JsonSerializer.Serialize(result),
                Error = null,
                ValidacoesProcessadas = 0,
                QueriesEnviadas = queriesEnviadas
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante processamento da execução: {ExecucaoId}", execucao.Id);
            
            return new ProcessResult
            {
                Success = false,
                Error = ex.Message,
                Result = null
            };
        }
    }
}

public class ProcessResult
{
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public int ValidacoesProcessadas { get; set; }
    public int QueriesEnviadas { get; set; }
}
