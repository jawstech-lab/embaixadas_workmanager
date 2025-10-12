using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Interfaces;

/// <summary>
/// Pipeline que executa todos os steps de pós-processamento em ordem.
/// </summary>
public interface IPostProcessingPipeline
{
    /// <summary>
    /// Executa o pipeline completo de pós-processamento
    /// </summary>
    /// <param name="context">Contexto com dados da execução finalizada</param>
    /// <returns>Resultado do pipeline completo</returns>
    Task<PostProcessingResult> ExecuteAsync(PostProcessingContext context);
}



