using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Modelo de resultado agregado por grupo de apontamentos.
/// 
/// Estrutura de Chaves:
/// PK: EXEC#<ExecucaoId>
/// SK: EMP#<EmpresaUnica>#VER#<VerificacaoId>#<Tabela>#<Campo>#<Referencia>#<TipoApontamento>
/// 
/// IMPORTANTE: Empresa deve ser SIGLA ÚNICA (ex: "MA", não "MA,PI")
/// Se o registro original tem "MA,PI", criamos 2 registros separados.
/// 
/// Armazena a quantidade de apontamentos agrupados por critérios específicos,
/// permitindo consultas rápidas de estatísticas por execução.
/// 
/// Exemplo de item:
/// {
///   "PK": "EXEC#abc-123",
///   "SK": "EMP#MA#VER#verif-456#PIP#FAS_CON#12345#INCONSISTENCIA",
///   "QTD": 15,
///   "Empresa": "MA",
///   "VerificacaoId": "verif-456",
///   "Tabela": "PIP",
///   "Campo": "FAS_CON",
///   "Referencia": "12345",
///   "TipoApontamento": "INCONSISTENCIA"
/// }
/// </summary>
[DynamoDBTable("ResultadoAgregado")]
public class ResultadoAgregado
{
    /// <summary>
    /// Partition Key: EXEC#<ExecucaoId>
    /// </summary>
    [DynamoDBHashKey("PK")]
    public string PK { get; set; } = string.Empty;

    /// <summary>
    /// Sort Key: EMP#<Empresa>#<Tabela>#<Campo>#<Referencia>#<TipoApontamento>
    /// </summary>
    [DynamoDBRangeKey("SK")]
    public string SK { get; set; } = string.Empty;

    /// <summary>
    /// GSI2 Partition Key: EMP#<Empresa>
    /// Permite buscar/deletar todos os registros de uma empresa específica
    /// </summary>
    [DynamoDBGlobalSecondaryIndexHashKey("GSI2_PK", "GSI_Empresa")]
    public string GSI2_PK { get; set; } = string.Empty;

    /// <summary>
    /// GSI2 Sort Key: EXEC#<ExecId>#VER#<VerifId>#<Tabela>#<Campo>#<Referencia>
    /// Ordena registros por execução dentro de cada empresa
    /// </summary>
    [DynamoDBGlobalSecondaryIndexRangeKey("GSI2_SK", "GSI_Empresa")]
    public string GSI2_SK { get; set; } = string.Empty;

    /// <summary>
    /// Quantidade de apontamentos neste grupo
    /// </summary>
    [DynamoDBProperty("QTD")]
    public int Quantidade { get; set; }

    // === Campos Desnormalizados (para facilitar leitura) ===

    /// <summary>
    /// ID da execução
    /// </summary>
    [DynamoDBProperty("ExecucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    /// <summary>
    /// Sigla da empresa (ÚNICA - ex: "MA", não "MA,PI")
    /// </summary>
    [DynamoDBProperty("Empresa")]
    public string Empresa { get; set; } = string.Empty;

    /// <summary>
    /// ID da verificação
    /// </summary>
    [DynamoDBProperty("VerificacaoId")]
    public string VerificacaoId { get; set; } = string.Empty;

    /// <summary>
    /// Nome da tabela
    /// </summary>
    [DynamoDBProperty("Tabela")]
    public string Tabela { get; set; } = string.Empty;

    /// <summary>
    /// Nome do campo
    /// </summary>
    [DynamoDBProperty("Campo")]
    public string Campo { get; set; } = string.Empty;

    /// <summary>
    /// Referência do registro
    /// </summary>
    [DynamoDBProperty("Referencia")]
    public string Referencia { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do apontamento
    /// </summary>
    [DynamoDBProperty("TipoApontamento")]
    public string TipoApontamento { get; set; } = string.Empty;

    /// <summary>
    /// Nível da verificação (criticidade)
    /// </summary>
    [DynamoDBProperty("Nivel")]
    public int Nivel { get; set; }

    /// <summary>
    /// Data de criação do registro agregado
    /// </summary>
    [DynamoDBProperty("DataCriacao")]
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    // === Métodos Helper ===

    /// <summary>
    /// Cria a PK no formato padrão: EXEC#<ExecucaoId>
    /// </summary>
    public static string CriarPK(string execucaoId)
    {
        return $"EXEC#{execucaoId}";
    }

    /// <summary>
    /// Cria a PK no formato SEGMENTADO: EXEC#<ExecucaoId>#EMB#<IdEmbaixada>
    /// Usado para visualização de usuários comuns (filtrado por embaixada)
    /// </summary>
    public static string CriarPKSegmentado(string execucaoId, string idEmbaixada)
    {
        return $"EXEC#{execucaoId}#EMB#{idEmbaixada}";
    }

    /// <summary>
    /// Cria a PK no formato GLOBAL: EXEC#<ExecucaoId>#EMB#GLOBAL
    /// Usado para visualização de administradores (sem filtro de embaixada)
    /// </summary>
    public static string CriarPKGlobal(string execucaoId)
    {
        return $"EXEC#{execucaoId}#EMB#GLOBAL";
    }

    /// <summary>
    /// Cria a SK no formato correto: EMP#<EmpresaUnica>#VER#<VerificacaoId>#<Tabela>#<Campo>#<Referencia>#<TipoApontamento>
    /// IMPORTANTE: Empresa deve ser sigla ÚNICA (ex: "MA", não "MA,PI")
    /// </summary>
    public static string CriarSK(
        string empresaUnica,
        string verificacaoId,
        string tabela,
        string campo,
        string referencia,
        string tipoApontamento)
    {
        // Normalizar valores vazios para evitar problemas
        empresaUnica = string.IsNullOrEmpty(empresaUnica) ? "UNKNOWN" : empresaUnica;
        verificacaoId = string.IsNullOrEmpty(verificacaoId) ? "UNKNOWN" : verificacaoId;
        tabela = string.IsNullOrEmpty(tabela) ? "UNKNOWN" : tabela;
        campo = string.IsNullOrEmpty(campo) ? "UNKNOWN" : campo;
        referencia = string.IsNullOrEmpty(referencia) ? "UNKNOWN" : referencia;
        tipoApontamento = string.IsNullOrEmpty(tipoApontamento) ? "UNKNOWN" : tipoApontamento;

        return $"EMP#{empresaUnica}#VER#{verificacaoId}#{tabela}#{campo}#{referencia}#{tipoApontamento}";
    }

    /// <summary>
    /// Cria o GSI2_PK no formato correto: EMP#<Empresa>
    /// </summary>
    public static string CriarGSI2_PK(string empresa)
    {
        return $"EMP#{empresa.ToUpper()}";
    }

    /// <summary>
    /// Cria o GSI2_SK no formato correto: EXEC#<ExecId>#VER#<VerifId>#<Tabela>#<Campo>#<Referencia>
    /// </summary>
    public static string CriarGSI2_SK(
        string execucaoId,
        string verificacaoId,
        string tabela,
        string campo,
        string referencia)
    {
        tabela = string.IsNullOrEmpty(tabela) ? "UNKNOWN" : tabela;
        campo = string.IsNullOrEmpty(campo) ? "UNKNOWN" : campo;
        referencia = string.IsNullOrEmpty(referencia) ? "UNKNOWN" : referencia;

        return $"EXEC#{execucaoId}#VER#{verificacaoId}#{tabela}#{campo}#{referencia}";
    }
}



