using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Modelo de apontamento de erro detalhado da execução de verificações.
/// 
/// Estrutura de Chaves:
/// PK: EXEC#<ExecucaoId>#VERIF#<VerificacaoId>
/// SK: ERRO#<Timestamp>#<Seq>
/// 
/// GSI para Agregação:
/// GSI1_PK: EXEC#<ExecucaoId>
/// GSI1_SK: EMP#<Empresa>#<Timestamp>
/// 
/// Permite buscar todos os apontamentos de uma execução de forma otimizada
/// usando o GSI_Agregacao.
/// </summary>
[DynamoDBTable("Resultado")]
public class Resultado
{
    /// <summary>
    /// Partition Key: EXEC#<ExecucaoId>#VERIF#<VerificacaoId>
    /// </summary>
    [DynamoDBHashKey("PK")]
    public string PK { get; set; } = string.Empty;

    /// <summary>
    /// Sort Key: ERRO#<Timestamp>#<Seq>
    /// </summary>
    [DynamoDBRangeKey("SK")]
    public string SK { get; set; } = string.Empty;

    /// <summary>
    /// GSI Partition Key: EXEC#<ExecucaoId>
    /// Permite buscar todos os apontamentos de uma execução
    /// </summary>
    [DynamoDBGlobalSecondaryIndexHashKey("GSI1_PK", "GSI_Agregacao")]
    public string GSI1_PK { get; set; } = string.Empty;

    /// <summary>
    /// GSI Sort Key: EMP#<Empresa>#<Timestamp>
    /// Permite ordenar por empresa e data
    /// </summary>
    [DynamoDBGlobalSecondaryIndexRangeKey("GSI1_SK", "GSI_Agregacao")]
    public string GSI1_SK { get; set; } = string.Empty;

    // === Campos de Agrupamento ===

    /// <summary>
    /// ID da embaixada (campo único - deprecated, usar IdEmbaixadas)
    /// </summary>
    [DynamoDBProperty("IdEmbaixada")]
    public string IdEmbaixada { get; set; } = string.Empty;

    /// <summary>
    /// Lista de IDs das embaixadas relacionadas ao apontamento
    /// </summary>
    [DynamoDBProperty("IdEmbaixadas")]
    public List<string> IdEmbaixadas { get; set; } = new();

    /// <summary>
    /// Sigla da empresa (ex: MA, RS, SP)
    /// </summary>
    [DynamoDBProperty("Empresa")]
    public string Empresa { get; set; } = string.Empty;

    /// <summary>
    /// Nome da tabela do banco de dados
    /// </summary>
    [DynamoDBProperty("Tabela")]
    public string Tabela { get; set; } = string.Empty;

    /// <summary>
    /// Nome do campo com erro
    /// </summary>
    [DynamoDBProperty("Campo")]
    public string Campo { get; set; } = string.Empty;

    /// <summary>
    /// Referência do registro (ex: número do documento)
    /// </summary>
    [DynamoDBProperty("Referencia")]
    public string Referencia { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do apontamento (ex: INCONSISTENCIA, DUPLICIDADE, etc.)
    /// </summary>
    [DynamoDBProperty("TipoApontamento")]
    public string TipoApontamento { get; set; } = string.Empty;

    /// <summary>
    /// Nível da verificação (criticidade)
    /// </summary>
    [DynamoDBProperty("Nivel")]
    public int Nivel { get; set; }

    // === Dados do Erro ===

    /// <summary>
    /// Descrição detalhada do erro
    /// </summary>
    [DynamoDBProperty("Descricao")]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>
    /// Valor encontrado que causou o erro
    /// </summary>
    [DynamoDBProperty("ValorEncontrado")]
    public string? ValorEncontrado { get; set; }

    /// <summary>
    /// Valor esperado
    /// </summary>
    [DynamoDBProperty("ValorEsperado")]
    public string? ValorEsperado { get; set; }

    /// <summary>
    /// Data e hora do apontamento
    /// </summary>
    [DynamoDBProperty("DataApontamento")]
    public DateTime DataApontamento { get; set; }

    // === Metadados ===

    /// <summary>
    /// ID da execução (desnormalizado para facilitar consultas)
    /// </summary>
    [DynamoDBProperty("ExecucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    /// <summary>
    /// ID da verificação (desnormalizado)
    /// </summary>
    [DynamoDBProperty("VerificacaoId")]
    public string VerificacaoId { get; set; } = string.Empty;

    /// <summary>
    /// Nome da verificação
    /// </summary>
    [DynamoDBProperty("NomeVerificacao")]
    public string? NomeVerificacao { get; set; }

    // === Métodos Helper ===

    /// <summary>
    /// Cria a PK no formato correto
    /// </summary>
    public static string CriarPK(string execucaoId, string verificacaoId)
    {
        return $"EXEC#{execucaoId}#VERIF#{verificacaoId}";
    }

    /// <summary>
    /// Cria a SK no formato correto
    /// </summary>
    public static string CriarSK(DateTime timestamp, int sequencia)
    {
        var timestampStr = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        return $"ERRO#{timestampStr}#{sequencia:D6}";
    }

    /// <summary>
    /// Cria o GSI1_PK no formato correto
    /// </summary>
    public static string CriarGSI1_PK(string execucaoId)
    {
        return $"EXEC#{execucaoId}";
    }

    /// <summary>
    /// Cria o GSI1_SK no formato correto
    /// </summary>
    public static string CriarGSI1_SK(string empresa, DateTime timestamp)
    {
        var timestampStr = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        return $"EMP#{empresa}#{timestampStr}";
    }
}


