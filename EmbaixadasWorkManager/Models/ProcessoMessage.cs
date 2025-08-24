using System.Text.Json.Serialization;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Mensagem recebida da fila de processo de execução (CONSOLIDADA)
/// </summary>
public class ProcessoMessage
{
    /// <summary>
    /// ID da execução
    /// </summary>
    [JsonPropertyName("execucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    /// <summary>
    /// Indica se a verificação foi processada com sucesso
    /// </summary>
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }
}

