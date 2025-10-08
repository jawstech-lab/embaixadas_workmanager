using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Detalhes de erro para ExecucaoVerificacao
/// </summary>
public class ErroDetalhado
{
    [DynamoDBProperty("ErrorCode")]
    public string? ErrorCode { get; set; }

    [DynamoDBProperty("Message")]
    public string? Message { get; set; }

    [DynamoDBProperty("TechnicalDetails")]
    public string? TechnicalDetails { get; set; }

    [DynamoDBProperty("IsRecoverable")]
    public bool IsRecoverable { get; set; }

    [DynamoDBProperty("RetryAttempts")]
    public int RetryAttempts { get; set; }

    [DynamoDBProperty("OccurredAt")]
    public DateTime? OccurredAt { get; set; }
}

