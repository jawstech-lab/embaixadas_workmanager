namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Chave composta para agrupar apontamentos de erro.
/// Utilizada para criar grupos únicos baseados nos critérios de agrupamento.
/// 
/// Critérios de agrupamento:
/// - Empresa (pode conter múltiplas siglas: "MA,PI")
/// - VerificacaoId
/// - Tabela
/// - Campo
/// - Referencia
/// - TipoApontamento
/// - Nivel
/// 
/// Exemplo de chave: "MA,PI|verif-123|PIP|FAS_CON|12345|INCONSISTENCIA|1"
/// </summary>
public class ChaveAgrupamento
{
    public string Empresa { get; set; } = string.Empty;  // Pode ser "MA,PI"
    public string VerificacaoId { get; set; } = string.Empty;
    public string Tabela { get; set; } = string.Empty;
    public string Campo { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string TipoApontamento { get; set; } = string.Empty;
    public int Nivel { get; set; }

    /// <summary>
    /// Converte a chave em string única para usar como key em dicionários
    /// </summary>
    public string ToKey()
    {
        return $"{Empresa}|{VerificacaoId}|{Tabela}|{Campo}|{Referencia}|{TipoApontamento}|{Nivel}";
    }

    /// <summary>
    /// Cria uma chave de agrupamento a partir de um resultado
    /// </summary>
    public static ChaveAgrupamento FromResultado(Resultado resultado)
    {
        return new ChaveAgrupamento
        {
            Empresa = resultado.Empresa,
            VerificacaoId = resultado.VerificacaoId,
            Tabela = resultado.Tabela,
            Campo = resultado.Campo,
            Referencia = resultado.Referencia,
            TipoApontamento = resultado.TipoApontamento,
            Nivel = resultado.Nivel
        };
    }

    /// <summary>
    /// Cria uma chave de agrupamento a partir de uma ExecucaoVerificacao
    /// </summary>
    public static ChaveAgrupamento FromExecucaoVerificacao(ExecucaoVerificacao execVerif)
    {
        return new ChaveAgrupamento
        {
            Empresa = execVerif.Empresa,
            VerificacaoId = execVerif.VerificacaoId,
            Tabela = "VERIFICACAO",
            Campo = execVerif.VerificacaoId,
            Referencia = "N/A",
            TipoApontamento = "VERIFICACAO",
            Nivel = execVerif.Nivel
        };
    }

    public override bool Equals(object? obj)
    {
        if (obj is ChaveAgrupamento other)
        {
            return ToKey() == other.ToKey();
        }
        return false;
    }

    public override int GetHashCode()
    {
        return ToKey().GetHashCode();
    }

    public override string ToString()
    {
        return ToKey();
    }
}




