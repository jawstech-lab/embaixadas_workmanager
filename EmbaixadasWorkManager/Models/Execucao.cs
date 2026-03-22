using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

[DynamoDBTable("Execucoes")]
public class Execucao
{
    [DynamoDBHashKey("Id")]
    public string Id { get; set; } = string.Empty;

    [DynamoDBProperty("Base")]
    public string Base { get; set; } = string.Empty;

    [DynamoDBProperty("DataBase")]
    public DateTime DataBase { get; set; }

    [DynamoDBProperty("DataSolicitacao")]
    public DateTime DataSolicitacao { get; set; }

    [DynamoDBProperty("Empresa")]
    public string Empresa { get; set; } = string.Empty;

    [DynamoDBProperty("Validacoes")]
    public List<string> Validacoes { get; set; } = new();

    [DynamoDBProperty("IdEmbaixadas")]
    public List<string> IdEmbaixadas { get; set; } = new();

    [DynamoDBProperty("Usuario")]
    public string Usuario { get; set; } = string.Empty;

    [DynamoDBProperty("Status")]
    public string Status { get; set; } = "Pendente";

    [DynamoDBProperty("DataInicio")]
    public DateTime? DataInicio { get; set; }

    [DynamoDBProperty("DataFim")]
    public DateTime? DataFim { get; set; }

    [DynamoDBProperty("Erro")]
    public string? Erro { get; set; }

    [DynamoDBProperty("Erros")]
    public List<ErroExecucao> Erros { get; set; } = new();

    [DynamoDBProperty("Resultado")]
    public string? Resultado { get; set; }

    // NOVOS CAMPOS (consolidados do ExecucaoProcesso)
    [DynamoDBProperty("QuantidadeVerificacoes")]
    public int QuantidadeVerificacoes { get; set; }

    [DynamoDBProperty("VerificacoesProcessadas")]
    public int VerificacoesProcessadas { get; set; }

    [DynamoDBProperty("VerificacoesComErro")]
    public int VerificacoesComErro { get; set; }

    [DynamoDBProperty("DataInicioProcessamento")]
    public DateTime? DataInicioProcessamento { get; set; }

    // NOVOS CAMPOS - Parâmetros específicos da execução
    [DynamoDBProperty("ParametrosExecucao")]
    public List<ParametroExecucao> ParametrosExecucao { get; set; } = new();

    [DynamoDBProperty("TotalApontamentos")]
    public int TotalApontamentos { get; set; }

    [DynamoDBProperty("TotalRegistrosEstimados")]
    public int TotalRegistrosEstimados { get; set; }

    [DynamoDBProperty("TotalRecordsProcessados")]
    public int TotalRecordsProcessados { get; set; }
}

