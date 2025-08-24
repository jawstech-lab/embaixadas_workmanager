using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

[DynamoDBTable("Consultas")]
public class Consulta
{
    [DynamoDBHashKey("Id")]
    public string Id { get; set; } = string.Empty;

    [DynamoDBProperty("CreatedAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [DynamoDBProperty("CreatedBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [DynamoDBProperty("CreationTime")]
    public long CreationTime { get; set; }

    [DynamoDBProperty("Descricao")]
    public string Descricao { get; set; } = string.Empty;

    [DynamoDBProperty("Identificador")]
    public string Identificador { get; set; } = string.Empty;

    [DynamoDBProperty("ModificationTime")]
    public long ModificationTime { get; set; }

    [DynamoDBProperty("ModifiedAt")]
    public string ModifiedAt { get; set; } = string.Empty;

    [DynamoDBProperty("ModifiedBy")]
    public string ModifiedBy { get; set; } = string.Empty;

    [DynamoDBProperty("QuerySql")]
    public string QuerySql { get; set; } = string.Empty;

    [DynamoDBProperty("SearchText")]
    public string SearchText { get; set; } = string.Empty;

    // Propriedades computadas para compatibilidade com o código existente
    public string Nome => Identificador;
    public string Sql => QuerySql;
    public int OrdemExecucao => 1; // Valor padrão, pode ser ajustado conforme necessário
    public int TimeoutSegundos => 60; // Valor padrão
    public int Prioridade => 1; // Valor padrão
    public Dictionary<string, string> Parametros => new(); // Valor padrão vazio
}
