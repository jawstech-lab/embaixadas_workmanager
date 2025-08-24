using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

[DynamoDBTable("Parametros")]
public class Parametro
{
	[DynamoDBHashKey("Id")]
	public string Id { get; set; } = string.Empty;

	[DynamoDBProperty("Alias")]
	public string Alias { get; set; } = string.Empty;

	[DynamoDBProperty("TipoParametro")]
	public int TipoParametro { get; set; }

	[DynamoDBProperty("TipoValor")]
	public int TipoValor { get; set; }
}
