using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Models;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace EmbaixadasWorkManager.Services.PostProcessing;

/// <summary>
/// Pipeline executor que gerencia a execução ordenada de todos os steps de pós-processamento.
/// </summary>
public class PostProcessingPipeline : IPostProcessingPipeline
{
    private readonly ILogger<PostProcessingPipeline> _logger;
    private readonly IEnumerable<IPostProcessingStep> _steps;
    private readonly PostProcessingConfiguration _config;

    public PostProcessingPipeline(
        ILogger<PostProcessingPipeline> logger,
        IEnumerable<IPostProcessingStep> steps,
        IOptions<PostProcessingConfiguration> config)
    {
        _logger = logger;
        _steps = steps;
        _config = config.Value;
    }

    public async Task<PostProcessingResult> ExecuteAsync(PostProcessingContext context)
    {
        var pipelineStopwatch = Stopwatch.StartNew();
        var result = new PostProcessingResult
        {
            Context = context
        };

        try
        {
            if (!_config.Enabled)
            {
                _logger.LogInformation("Pipeline de pos-processamento esta desabilitado. Pulando execucao.");
                result.Success = true;
                result.Messages.Add("Pipeline desabilitado via configuracao");
                return result;
            }

            _logger.LogInformation(
                "Iniciando pipeline de pos-processamento para execucao {ExecucaoId}. " +
                "Status: {Status}, Verificacoes: {Total} (Sucesso: {Sucesso}, Erros: {Erros})",
                context.Execucao.Id,
                context.Execucao.Status,
                context.Execucao.QuantidadeVerificacoes,
                context.Execucao.VerificacoesProcessadas,
                context.Execucao.VerificacoesComErro);

            // Ordenar steps por ordem de execução
            var orderedSteps = _steps
                .Where(s => s.IsEnabled)
                .OrderBy(s => s.Order)
                .ToList();

            if (!orderedSteps.Any())
            {
                _logger.LogWarning("Nenhum step de pos-processamento habilitado. Pipeline vazio.");
                result.Success = true;
                result.Messages.Add("Nenhum step habilitado");
                return result;
            }

            _logger.LogInformation("Pipeline configurado com {Count} steps habilitados: {Steps}",
                orderedSteps.Count,
                string.Join(", ", orderedSteps.Select(s => $"{s.StepName} (Order: {s.Order})")));

            // Executar cada step em ordem
            foreach (var step in orderedSteps)
            {
                if (context.ShouldStop)
                {
                    _logger.LogWarning("Pipeline interrompido antes do step {StepName}. Motivo: {ErrorMessage}",
                        step.StepName, context.ErrorMessage);
                    break;
                }

                await ExecuteStepAsync(step, context, result);

                // Verificar se deve continuar após falha
                if (!_config.ContinueOnStepFailure && result.TotalStepsFailed > 0)
                {
                    _logger.LogWarning("Pipeline interrompido apos falha do step. ContinueOnStepFailure = false");
                    break;
                }
            }

            pipelineStopwatch.Stop();
            result.TotalExecutionTime = pipelineStopwatch.Elapsed;

            // Determinar sucesso geral
            result.Success = result.TotalStepsFailed == 0 || _config.ContinueOnStepFailure;

            _logger.LogInformation(
                "Pipeline de pos-processamento concluido. " +
                "Success: {Success}, Steps executados: {Executed}, Falhas: {Failed}, " +
                "Tempo total: {Time}ms",
                result.Success,
                result.TotalStepsExecuted,
                result.TotalStepsFailed,
                result.TotalExecutionTime.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            pipelineStopwatch.Stop();
            result.TotalExecutionTime = pipelineStopwatch.Elapsed;
            result.Success = false;
            result.Messages.Add($"Erro critico no pipeline: {ex.Message}");

            _logger.LogError(ex, "Erro critico ao executar pipeline de pos-processamento para execucao {ExecucaoId}",
                context.Execucao.Id);

            return result;
        }
    }

    private async Task ExecuteStepAsync(
        IPostProcessingStep step,
        PostProcessingContext context,
        PostProcessingResult result)
    {
        var stepStopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Executando step: {StepName} (Order: {Order})",
                step.StepName, step.Order);

            // Validar se pode executar
            var canExecute = await step.CanExecuteAsync(context);
            if (!canExecute)
            {
                _logger.LogInformation("Step {StepName} pulado. CanExecute retornou false", step.StepName);
                result.Messages.Add($"{step.StepName}: Pulado (condicao nao atendida)");
                return;
            }

            // Executar step
            var stepResult = await step.ExecuteAsync(context);
            stepStopwatch.Stop();
            stepResult.ExecutionTime = stepStopwatch.Elapsed;

            // Processar resultado
            result.TotalStepsExecuted++;

            if (stepResult.Success)
            {
                context.ExecutedSteps.Add(step.StepName);
                result.Messages.Add($"{step.StepName}: {stepResult.Message} ({stepResult.ExecutionTime.TotalMilliseconds:F0}ms)");

                _logger.LogInformation("Step {StepName} executado com sucesso. Tempo: {Time}ms. Mensagem: {Message}",
                    step.StepName,
                    stepResult.ExecutionTime.TotalMilliseconds,
                    stepResult.Message);

                // Adicionar dados do step ao contexto
                if (stepResult.Data != null)
                {
                    foreach (var kvp in stepResult.Data)
                    {
                        context.Metadata[$"{step.StepName}_{kvp.Key}"] = kvp.Value;
                    }
                }
            }
            else
            {
                context.FailedSteps.Add(step.StepName);
                result.TotalStepsFailed++;
                result.Messages.Add($"{step.StepName}: FALHOU - {stepResult.Message}");

                _logger.LogError("Step {StepName} falhou. Tempo: {Time}ms. Mensagem: {Message}",
                    step.StepName,
                    stepResult.ExecutionTime.TotalMilliseconds,
                    stepResult.Message);
            }

            // Verificar se deve parar pipeline
            if (stepResult.ShouldStopPipeline)
            {
                context.ShouldStop = true;
                context.ErrorMessage = stepResult.Message;
                _logger.LogWarning("Step {StepName} solicitou interrupcao do pipeline", step.StepName);
            }
        }
        catch (Exception ex)
        {
            stepStopwatch.Stop();
            context.FailedSteps.Add(step.StepName);
            result.TotalStepsFailed++;
            result.Messages.Add($"{step.StepName}: ERRO - {ex.Message}");

            _logger.LogError(ex, "Erro ao executar step {StepName}. Tempo: {Time}ms",
                step.StepName, stepStopwatch.Elapsed.TotalMilliseconds);

            if (!_config.ContinueOnStepFailure)
            {
                context.ShouldStop = true;
                context.ErrorMessage = ex.Message;
            }
        }
    }
}



