using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

[DynamoDBTable("Verificacoes")]
public class Verificacao
{
    [DynamoDBHashKey("Id")]
    public string Id { get; set; } = string.Empty;

    [DynamoDBProperty("CreationTime")]
    public long CreationTime { get; set; }

    [DynamoDBProperty("Entidade")]
    public string Entidade { get; set; } = string.Empty;

    [DynamoDBProperty("IdConsulta")]
    public string IdConsulta { get; set; } = string.Empty;

    [DynamoDBProperty("IdEmbaixadas")]
    public List<string> IdEmbaixadas { get; set; } = new();

    [DynamoDBProperty("IdentificadorConsulta")]
    public string IdentificadorConsulta { get; set; } = string.Empty;

    [DynamoDBProperty("IdTipo")]
    public string IdTipo { get; set; } = string.Empty;

    [DynamoDBProperty("ModificationTime")]
    public long ModificationTime { get; set; }

    [DynamoDBProperty("ModifiedBy")]
    public string ModifiedBy { get; set; } = string.Empty;

    [DynamoDBProperty("Nivel")]
    public int Nivel { get; set; }

    [DynamoDBProperty("NomeVerificacao")]
    public string NomeVerificacao { get; set; } = string.Empty;

    [DynamoDBProperty("Ordem")]
    public int Ordem { get; set; }

    [DynamoDBProperty("ValoresParametros")]
    public List<VerificacaoParametroValor> ValoresParametros { get; set; } = new();

    // Propriedade computada para compatibilidade com o código existente
    public string Nome => NomeVerificacao;
}

