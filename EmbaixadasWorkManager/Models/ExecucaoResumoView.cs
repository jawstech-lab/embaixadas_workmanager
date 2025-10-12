using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Visão rápida da última execução por embaixada e empresa.
/// Permite consultar rapidamente a última execução de cada empresa em cada embaixada.
/// 
/// Estrutura de Chaves:
/// PK: VIEW#LAST_EXEC#EMB#<IdEmbaixada>
/// SK: EMP#<Sigla> (ex: EMP#MA, EMP#RS)
/// 
/// Exemplo de item:
/// {
///   "PK_VIEW": "VIEW#LAST_EXEC#EMB#7530416f-b46f-4048-8d46-0c3702226bea",
///   "SK_VIEW": "EMP#MA",
///   "ExecucaoId": "c5a903ce-...",
///   "DataSolicitacao": "2025-10-05T20:27:59.457Z",
///   "Status": "Cadastrado",
///   "SiglaEmpresa": "MA",
///   "IdEmbaixada": "7530416f-b46f-4048-8d46-0c3702226bea"
/// }
/// </summary>
[DynamoDBTable("ExecucaoResumoView")]
public class ExecucaoResumoView
{
    /// <summary>
    /// Partition Key: VIEW#LAST_EXEC#EMB#<IdEmbaixada>
    /// </summary>
    [DynamoDBHashKey("PK_VIEW")]
    public string PK { get; set; } = string.Empty;

    /// <summary>
    /// Sort Key: EMP#<Sigla> (ex: EMP#MA)
    /// </summary>
    [DynamoDBRangeKey("SK_VIEW")]
    public string SK { get; set; } = string.Empty;

    /// <summary>
    /// ID da execução mais recente para esta empresa
    /// </summary>
    [DynamoDBProperty("ExecucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    /// <summary>
    /// Data e hora da solicitação da execução (ISO 8601)
    /// </summary>
    [DynamoDBProperty("DataSolicitacao")]
    public DateTime DataSolicitacao { get; set; }

    /// <summary>
    /// Status atual da execução (ex: Cadastrado, EmProcessamento, Concluida)
    /// </summary>
    [DynamoDBProperty("Status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Sigla da empresa (ex: MA, RS, SP)
    /// </summary>
    [DynamoDBProperty("SiglaEmpresa")]
    public string SiglaEmpresa { get; set; } = string.Empty;

    /// <summary>
    /// ID da embaixada
    /// </summary>
    [DynamoDBProperty("IdEmbaixada")]
    public string IdEmbaixada { get; set; } = string.Empty;

    /// <summary>
    /// Cria o Partition Key no formato correto: VIEW#LAST_EXEC#EMB#<IdEmbaixada>
    /// </summary>
    /// <param name="idEmbaixada">ID da embaixada (GUID)</param>
    /// <returns>Partition Key formatado</returns>
    public static string CriarPK(string idEmbaixada) => $"VIEW#LAST_EXEC#EMB#{idEmbaixada}";

    /// <summary>
    /// Cria o Sort Key no formato correto: EMP#<Sigla>
    /// </summary>
    /// <param name="siglaEmpresa">Sigla da empresa (ex: MA)</param>
    /// <returns>Sort Key formatado (ex: EMP#MA)</returns>
    public static string CriarSK(string siglaEmpresa) => $"EMP#{siglaEmpresa.ToUpper()}";
}


