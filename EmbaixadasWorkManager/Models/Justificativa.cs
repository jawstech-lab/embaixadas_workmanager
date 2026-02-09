using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Modelo de justificativa aprovada para remoção de apontamentos.
/// 
/// Quando uma justificativa é aprovada, os apontamentos relacionados devem ser
/// removidos da tabela Resultado antes da agregação.
/// 
/// Estrutura de Chaves:
/// PK: Id (UUID da justificativa)
/// 
/// GSI para Status:
/// GSI1_PK: STATUS#<Status>
/// GSI1_SK: DATA#<Data>
/// </summary>
[DynamoDBTable("Justificativas")]
public class Justificativa
{
    /// <summary>
    /// ID único da justificativa
    /// </summary>
    [DynamoDBHashKey("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// GSI Partition Key: STATUS#<Status>
    /// Permite buscar justificativas por status (ex: STATUS#Aprovado)
    /// </summary>
    [DynamoDBGlobalSecondaryIndexHashKey("GSI1_PK", "GSI_Status")]
    public string GSI1_PK { get; set; } = string.Empty;

    /// <summary>
    /// GSI Sort Key: DATA#<Data>
    /// Permite ordenar por data dentro de cada status
    /// </summary>
    [DynamoDBGlobalSecondaryIndexRangeKey("GSI1_SK", "GSI_Status")]
    public string GSI1_SK { get; set; } = string.Empty;

    // === Campos de Identificação do Apontamento ===

    /// <summary>
    /// ID da verificação relacionada
    /// </summary>
    [DynamoDBProperty("VerificacaoId")]
    public string VerificacaoId { get; set; } = string.Empty;

    /// <summary>
    /// Sigla da empresa (ex: MA, RS, SP)
    /// </summary>
    [DynamoDBProperty("Empresa")]
    public string Empresa { get; set; } = string.Empty;

    /// <summary>
    /// Nome da tabela do banco de dados
    /// </summary>
    [DynamoDBProperty("TabelaReferencia")]
    public string TabelaReferencia { get; set; } = string.Empty;

    /// <summary>
    /// Nome do campo com erro
    /// </summary>
    [DynamoDBProperty("Campo")]
    public string Campo { get; set; } = string.Empty;

    /// <summary>
    /// Referência do registro (opcional)
    /// </summary>
    [DynamoDBProperty("Referencia")]
    public string? Referencia { get; set; }

    /// <summary>
    /// Tipo do apontamento (ex: VIF, DUPLICIDADE, etc.)
    /// </summary>
    [DynamoDBProperty("TipoApontamento")]
    public string TipoApontamento { get; set; } = string.Empty;

    // === Controle de Seleção ===

    /// <summary>
    /// Status da justificativa (Solicitado, Aprovado, Rejeitado)
    /// </summary>
    [DynamoDBProperty("Status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Se 1: remove todos os registros que atendem os critérios
    /// Se 0: remove apenas os IDs específicos da lista IdsRelacionados
    /// </summary>
    [DynamoDBProperty("SelecionarTodos")]
    public int SelecionarTodos { get; set; }

    /// <summary>
    /// Lista de IDs específicos para remover (usado quando SelecionarTodos = 0)
    /// </summary>
    [DynamoDBProperty("IdsRelacionados")]
    public List<string>? IdsRelacionados { get; set; }

    // === Campos Descritivos ===

    /// <summary>
    /// Descrição da justificativa
    /// </summary>
    [DynamoDBProperty("Descricao")]
    public string? Descricao { get; set; }

    /// <summary>
    /// Descrição do erro justificado
    /// </summary>
    [DynamoDBProperty("DescricaoErro")]
    public string? DescricaoErro { get; set; }

    /// <summary>
    /// Motivo da solicitação
    /// </summary>
    [DynamoDBProperty("MotivoSolicitacao")]
    public string? MotivoSolicitacao { get; set; }

    /// <summary>
    /// Data da justificativa
    /// </summary>
    [DynamoDBProperty("Data")]
    public string? Data { get; set; }

    // === Metadados ===

    /// <summary>
    /// ID da execução original (NÃO usar - usar execução corrente)
    /// </summary>
    [DynamoDBProperty("ExecucaoId")]
    public string? ExecucaoId { get; set; }

    /// <summary>
    /// Usuário que criou a justificativa
    /// </summary>
    [DynamoDBProperty("CreatedBy")]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Timestamp de criação
    /// </summary>
    [DynamoDBProperty("CreationTime")]
    public long CreationTime { get; set; }

    /// <summary>
    /// Usuário que modificou pela última vez
    /// </summary>
    [DynamoDBProperty("ModifiedBy")]
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Timestamp de modificação
    /// </summary>
    [DynamoDBProperty("ModificationTime")]
    public long ModificationTime { get; set; }

    /// <summary>
    /// Usuário que mudou o status
    /// </summary>
    [DynamoDBProperty("StatusChangedBy")]
    public string? StatusChangedBy { get; set; }

    /// <summary>
    /// Data/hora da mudança de status
    /// </summary>
    [DynamoDBProperty("StatusChangedAt")]
    public string? StatusChangedAt { get; set; }

    // === Propriedades Computadas ===

    /// <summary>
    /// Verifica se deve selecionar todos os registros
    /// </summary>
    [DynamoDBIgnore]
    public bool IsSelecionarTodos => SelecionarTodos == 1;

    /// <summary>
    /// Verifica se a justificativa está aprovada
    /// </summary>
    [DynamoDBIgnore]
    public bool IsAprovado => Status == "Aprovado";

    // === Métodos Helper ===

    /// <summary>
    /// Cria o GSI1_PK no formato correto: STATUS#<Status>
    /// </summary>
    public static string CriarGSI1_PK(string status)
    {
        return $"STATUS#{status.ToUpper()}";
    }

    /// <summary>
    /// Cria o GSI1_SK no formato correto: DATA#<Data>
    /// </summary>
    public static string CriarGSI1_SK(string data)
    {
        return $"DATA#{data}";
    }
}

