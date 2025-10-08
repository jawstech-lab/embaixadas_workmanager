using System.Text.Json.Serialization;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Mensagem recebida da fila de processo de execução (CONSOLIDADA)
/// </summary>
public class ProcessoMessage
{
    /// <summary>
    /// ID composto da ExecucaoVerificacao (execucaoId#verificacaoId)
    /// </summary>
    [JsonPropertyName("executionId")]
    public string ExecucaoVerificacaoId { get; set; } = string.Empty;

    /// <summary>
    /// Indica se a verificação foi processada com sucesso
    /// </summary>
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Resultado da verificação
    /// </summary>
    [JsonPropertyName("resultado")]
    public string? Resultado { get; set; }

    /// <summary>
    /// Timestamp do resultado
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

