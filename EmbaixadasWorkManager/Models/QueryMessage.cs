using System.Text.Json.Serialization;

namespace EmbaixadasWorkManager.Models;

public class QueryMessage
{
    [JsonPropertyName("execucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    [JsonPropertyName("verificacaoId")]
    public string VerificacaoId { get; set; } = string.Empty;

    [JsonPropertyName("queryId")]
    public string QueryId { get; set; } = string.Empty;

    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    [JsonPropertyName("dataBase")]
    public DateTime DataBase { get; set; }

    [JsonPropertyName("empresa")]
    public string Empresa { get; set; } = string.Empty;

    [JsonPropertyName("usuario")]
    public string Usuario { get; set; } = string.Empty;

    [JsonPropertyName("sql")]
    public string Sql { get; set; } = string.Empty;

    [JsonPropertyName("parametros")]
    public Dictionary<string, string> Parametros { get; set; } = new();

    [JsonPropertyName("timeoutSegundos")]
    public int TimeoutSegundos { get; set; } = 60;

    [JsonPropertyName("prioridade")]
    public int Prioridade { get; set; } = 1;

    [JsonPropertyName("dataSolicitacao")]
    public DateTime DataSolicitacao { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}

