using System.Text.Json.Serialization;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Mensagem leve enviada para a fila de execução de queries
/// Contém apenas o ID da ExecucaoVerificacao para buscar dados completos no DynamoDB
/// </summary>
public class QueryExecutionMessage
{
    /// <summary>
    /// ID da ExecucaoVerificacao (composite key: ExecucaoId#VerificacaoId)
    /// </summary>
    [JsonPropertyName("execucaoVerificacaoId")]
    public string ExecucaoVerificacaoId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp da mensagem
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
