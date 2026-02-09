namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Grupo agregado para visualização SEGMENTADA (Usuário Comum)
/// Coleta IdEmbaixadas para replicação mínima
/// </summary>
public class GrupoAgregadoSegmentado
{
    public ChaveAgrupamento Chave { get; set; } = null!;
    public int Quantidade { get; set; }
    public HashSet<string> IdEmbaixadas { get; set; } = new();
    public string DescricaoErro { get; set; } = string.Empty;  // Primeira descrição do grupo

    public GrupoAgregadoSegmentado()
    {
    }

    public GrupoAgregadoSegmentado(ChaveAgrupamento chave)
    {
        Chave = chave;
        Quantidade = 0;
        IdEmbaixadas = new HashSet<string>();
        DescricaoErro = string.Empty;
    }
}

/// <summary>
/// Grupo agregado para visualização GLOBAL (Admin)
/// Apenas contagem total, sem segmentação por embaixada
/// </summary>
public class GrupoAgregadoGlobal
{
    public ChaveAgrupamento Chave { get; set; } = null!;
    public int Quantidade { get; set; }
    public string DescricaoErro { get; set; } = string.Empty;  // Primeira descrição do grupo

    public GrupoAgregadoGlobal()
    {
    }

    public GrupoAgregadoGlobal(ChaveAgrupamento chave)
    {
        Chave = chave;
        Quantidade = 0;
        DescricaoErro = string.Empty;
    }
}

