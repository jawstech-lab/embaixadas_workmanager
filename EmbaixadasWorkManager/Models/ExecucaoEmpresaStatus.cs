using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Auditoria e histórico completo de todas as execuções por empresa.
/// Permite consultar o histórico de execuções de cada empresa ordenado por data.
/// 
/// Estrutura de Chaves:
/// PK: EMP#<Sigla> (ex: EMP#MA, EMP#RS)
/// SK: DATA#<DataISO>#<ExecucaoId> (ex: DATA#2025-10-05T20:27:59.457Z#c5a903ce-...)
/// 
/// Exemplo de item:
/// {
///   "PK_STATUS": "EMP#MA",
///   "SK_STATUS": "DATA#2025-10-05T20:27:59.457Z#c5a903ce-...",
///   "ExecucaoId": "c5a903ce-...",
///   "DataSolicitacao": "2025-10-05T20:27:59.457Z",
///   "Status": "Cadastrado",
///   "SiglaEmpresa": "MA"
/// }
/// </summary>
[DynamoDBTable("ExecucaoEmpresaStatus")]
public class ExecucaoEmpresaStatus
{
    /// <summary>
    /// Partition Key: EMP#<Sigla> (ex: EMP#MA)
    /// </summary>
    [DynamoDBHashKey("PK_STATUS")]
    public string PK { get; set; } = string.Empty;

    /// <summary>
    /// Sort Key: DATA#<DataISO>#<ExecucaoId>
    /// Permite ordenação cronológica das execuções
    /// </summary>
    [DynamoDBRangeKey("SK_STATUS")]
    public string SK { get; set; } = string.Empty;

    /// <summary>
    /// ID da execução
    /// </summary>
    [DynamoDBProperty("ExecucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    /// <summary>
    /// Data e hora da solicitação da execução (ISO 8601)
    /// </summary>
    [DynamoDBProperty("DataSolicitacao")]
    public DateTime DataSolicitacao { get; set; }

    /// <summary>
    /// Status da execução para esta empresa
    /// (ex: Cadastrado, EmProcessamento, Concluida, Erro)
    /// </summary>
    [DynamoDBProperty("Status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Sigla da empresa (ex: MA, RS, SP)
    /// </summary>
    [DynamoDBProperty("SiglaEmpresa")]
    public string SiglaEmpresa { get; set; } = string.Empty;

    /// <summary>
    /// Cria o Partition Key no formato correto: EMP#<Sigla>
    /// </summary>
    /// <param name="siglaEmpresa">Sigla da empresa (ex: MA)</param>
    /// <returns>Partition Key formatado (ex: EMP#MA)</returns>
    public static string CriarPK(string siglaEmpresa) => $"EMP#{siglaEmpresa.ToUpper()}";
    
    /// <summary>
    /// Cria o Sort Key no formato correto: DATA#<DataISO>#<ExecucaoId>
    /// </summary>
    /// <param name="dataSolicitacao">Data e hora da solicitação</param>
    /// <param name="execucaoId">ID da execução</param>
    /// <returns>Sort Key formatado (ex: DATA#2025-10-05T20:27:59.457Z#c5a903ce-...)</returns>
    public static string CriarSK(DateTime dataSolicitacao, string execucaoId)
    {
        var dataFormatada = dataSolicitacao.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        return $"DATA#{dataFormatada}#{execucaoId}";
    }
}




