using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Representa um parâmetro específico de uma execução
/// </summary>
public class ParametroExecucao
{
    /// <summary>
    /// Alias do parâmetro usado no SQL (ex: "TABELA", "SCHEMA")
    /// </summary>
    [DynamoDBProperty("Alias")]
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// Valor do parâmetro para esta execução específica
    /// </summary>
    [DynamoDBProperty("Valor")]
    public string Valor { get; set; } = string.Empty;

}
