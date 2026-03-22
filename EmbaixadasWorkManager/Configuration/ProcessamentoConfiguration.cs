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

    /// <summary>
    /// Tamanho máximo de registros por shard (padrão 100.000)
    /// </summary>
    public int ShardSize { get; set; } = 100000;
}
