using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Interfaces;

/// <summary>
/// Interface genérica para steps de pós-processamento.
/// Cada step implementa uma ação específica após a finalização da execução.
/// 
/// Exemplos de steps:
/// - AgrupamentoStep: Agrupa dados processados
/// - ExclusaoRegistrosStep: Remove registros temporários
/// - NotificacaoStep: Envia notificações
/// - RelatorioStep: Gera relatórios
/// </summary>
public interface IPostProcessingStep
{
    /// <summary>
    /// Nome identificador do step (usado para logs e rastreamento)
    /// </summary>
    string StepName { get; }

    /// <summary>
    /// Ordem de execução (menor executa primeiro)
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Indica se o step está habilitado
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Executa o pós-processamento
    /// </summary>
    /// <param name="context">Contexto compartilhado com dados da execução</param>
    /// <returns>Resultado da execução do step</returns>
    Task<PostProcessingStepResult> ExecuteAsync(PostProcessingContext context);

    /// <summary>
    /// Valida se o step pode ser executado no contexto atual
    /// </summary>
    /// <param name="context">Contexto compartilhado</param>
    /// <returns>True se pode executar, False caso contrário</returns>
    Task<bool> CanExecuteAsync(PostProcessingContext context);
}



