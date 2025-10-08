namespace EmbaixadasWorkManager.Configuration;

/// <summary>
/// Configurações relacionadas ao processamento de execuções
/// </summary>
public class ProcessamentoConfiguration
{
    public const string SectionName = "Processamento";

    /// <summary>
    /// Se deve buscar todas as verificações quando a execução não tem validações específicas
    /// </summary>
    public bool BuscarTodasVerificacoesSeVazio { get; set; } = true;
}
