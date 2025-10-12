using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Detalhes de erro de uma verificação na execução
/// </summary>
public class ErroExecucao
{
    /// <summary>
    /// ID único da ExecucaoVerificacao
    /// </summary>
    [DynamoDBProperty("ExecucaoVerificacaoId")]
    public string ExecucaoVerificacaoId { get; set; } = string.Empty;

    /// <summary>
    /// ID da Verificação
    /// </summary>
    [DynamoDBProperty("VerificacaoId")]
    public string VerificacaoId { get; set; } = string.Empty;

    /// <summary>
    /// SQL processado que foi executado
    /// </summary>
    [DynamoDBProperty("Sql")]
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// SQL original antes da substituição de parâmetros
    /// </summary>
    [DynamoDBProperty("SqlOriginal")]
    public string SqlOriginal { get; set; } = string.Empty;

    /// <summary>
    /// Código do erro
    /// </summary>
    [DynamoDBProperty("ErrorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Mensagem de erro
    /// </summary>
    [DynamoDBProperty("Message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Quando o erro ocorreu
    /// </summary>
    [DynamoDBProperty("OccurredAt")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}









