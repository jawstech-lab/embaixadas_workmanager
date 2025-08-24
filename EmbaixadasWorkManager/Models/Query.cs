using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

[DynamoDBTable("Query")]
public class Query
{
    [DynamoDBHashKey("Id")]
    public string Id { get; set; } = string.Empty;

    [DynamoDBProperty("VerificacaoId")]
    public string VerificacaoId { get; set; } = string.Empty;

    [DynamoDBProperty("Nome")]
    public string Nome { get; set; } = string.Empty;

    [DynamoDBProperty("Descricao")]
    public string Descricao { get; set; } = string.Empty;

    [DynamoDBProperty("Sql")]
    public string Sql { get; set; } = string.Empty;

    [DynamoDBProperty("Tipo")]
    public string Tipo { get; set; } = string.Empty;

    [DynamoDBProperty("Status")]
    public string Status { get; set; } = "Ativo";

    [DynamoDBProperty("OrdemExecucao")]
    public int OrdemExecucao { get; set; } = 1;

    [DynamoDBProperty("TimeoutSegundos")]
    public int TimeoutSegundos { get; set; } = 60;

    [DynamoDBProperty("Prioridade")]
    public int Prioridade { get; set; } = 1;

    [DynamoDBProperty("Parametros")]
    public Dictionary<string, string> Parametros { get; set; } = new();

    [DynamoDBProperty("Ativo")]
    public bool Ativo { get; set; } = true;

    [DynamoDBProperty("DataCriacao")]
    public DateTime DataCriacao { get; set; }

    [DynamoDBProperty("DataAtualizacao")]
    public DateTime DataAtualizacao { get; set; }

    [DynamoDBProperty("Tags")]
    public List<string> Tags { get; set; } = new();

    [DynamoDBProperty("Metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
