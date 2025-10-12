namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Contexto compartilhado entre todos os steps de pós-processamento.
/// Contém os dados da execução finalizada e metadados do processamento.
/// </summary>
public class PostProcessingContext
{
    /// <summary>
    /// Execução que foi finalizada e precisa de pós-processamento
    /// </summary>
    public Execucao Execucao { get; set; } = null!;

    /// <summary>
    /// Status de finalização (sucesso ou erro)
    /// </summary>
    public bool FinalizadaComSucesso { get; set; }

    /// <summary>
    /// Metadados adicionais que podem ser compartilhados entre steps
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Indica se o pós-processamento deve ser interrompido
    /// </summary>
    public bool ShouldStop { get; set; }

    /// <summary>
    /// Mensagem de erro se algum step falhar
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp do início do pós-processamento
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Lista de steps executados com sucesso
    /// </summary>
    public List<string> ExecutedSteps { get; set; } = new();

    /// <summary>
    /// Lista de steps que falharam
    /// </summary>
    public List<string> FailedSteps { get; set; } = new();
}

/// <summary>
/// Resultado da execução de um step de pós-processamento
/// </summary>
public class PostProcessingStepResult
{
    /// <summary>
    /// Indica se o step foi executado com sucesso
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensagem descritiva do resultado
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Dados adicionais retornados pelo step
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// Indica se o pipeline deve parar após este step
    /// </summary>
    public bool ShouldStopPipeline { get; set; }

    /// <summary>
    /// Tempo de execução do step
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Cria um resultado de sucesso
    /// </summary>
    public static PostProcessingStepResult Ok(string message, Dictionary<string, object>? data = null)
    {
        return new PostProcessingStepResult
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Cria um resultado de falha
    /// </summary>
    public static PostProcessingStepResult Fail(string message, bool shouldStop = false)
    {
        return new PostProcessingStepResult
        {
            Success = false,
            Message = message,
            ShouldStopPipeline = shouldStop
        };
    }
}

/// <summary>
/// Resultado completo do pipeline de pós-processamento
/// </summary>
public class PostProcessingResult
{
    /// <summary>
    /// Indica se todo o pipeline foi executado com sucesso
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Total de steps executados
    /// </summary>
    public int TotalStepsExecuted { get; set; }

    /// <summary>
    /// Total de steps que falharam
    /// </summary>
    public int TotalStepsFailed { get; set; }

    /// <summary>
    /// Mensagens de cada step
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Tempo total de execução do pipeline
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Contexto final após execução de todos os steps
    /// </summary>
    public PostProcessingContext? Context { get; set; }
}



