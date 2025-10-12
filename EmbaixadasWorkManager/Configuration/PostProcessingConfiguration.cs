namespace EmbaixadasWorkManager.Configuration;

/// <summary>
/// Configuração do pipeline de pós-processamento
/// </summary>
public class PostProcessingConfiguration
{
    public const string SectionName = "PostProcessing";

    /// <summary>
    /// Habilita/desabilita todo o pipeline de pós-processamento
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Timeout global para execução de todo o pipeline (em segundos)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300; // 5 minutos

    /// <summary>
    /// Indica se deve continuar executando steps mesmo se um falhar
    /// </summary>
    public bool ContinueOnStepFailure { get; set; } = true;

    /// <summary>
    /// Configuração do step de agrupamento
    /// </summary>
    public AgrupamentoStepConfiguration Agrupamento { get; set; } = new();

    /// <summary>
    /// Configuração do step de exclusão de registros
    /// </summary>
    public ExclusaoRegistrosStepConfiguration ExclusaoRegistros { get; set; } = new();

    /// <summary>
    /// Configuração do step de agregação de resultados
    /// </summary>
    public AgregacaoResultadosStepConfiguration AgregacaoResultados { get; set; } = new();
}

/// <summary>
/// Configuração do step de agrupamento
/// </summary>
public class AgrupamentoStepConfiguration
{
    /// <summary>
    /// Habilita/desabilita o step de agrupamento
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Ordem de execução do step
    /// </summary>
    public int Order { get; set; } = 1;

    /// <summary>
    /// Timeout para este step (em segundos)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Campos para agrupar os dados
    /// </summary>
    public List<string> GroupByFields { get; set; } = new() { "Empresa", "Status" };

    /// <summary>
    /// Indica se deve salvar o resultado do agrupamento
    /// </summary>
    public bool SaveResult { get; set; } = true;
}

/// <summary>
/// Configuração do step de exclusão de registros
/// </summary>
public class ExclusaoRegistrosStepConfiguration
{
    /// <summary>
    /// Habilita/desabilita o step de exclusão
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Ordem de execução do step
    /// </summary>
    public int Order { get; set; } = 2;

    /// <summary>
    /// Timeout para este step (em segundos)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Idade mínima (em dias) para exclusão de registros
    /// </summary>
    public int MinimumAgeInDays { get; set; } = 30;
}

/// <summary>
/// Configuração do step de agregação de resultados
/// </summary>
public class AgregacaoResultadosStepConfiguration
{
    /// <summary>
    /// Habilita/desabilita o step de agregação
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Ordem de execução do step (depois do agrupamento)
    /// </summary>
    public int Order { get; set; } = 3;

    /// <summary>
    /// Timeout para este step (em segundos)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Batch size para inserção de resultados agregados
    /// </summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// Indica se deve atualizar o TotalApontamentos na ExecucaoResumoView
    /// </summary>
    public bool AtualizarResumoView { get; set; } = true;
}

