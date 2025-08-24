using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

[DynamoDBTable("ExecucaoVerificacao")]
public class ExecucaoVerificacao
{
    // Chave primária da tabela
    [DynamoDBHashKey("Id")]
    public string Id { get; set; } = string.Empty;

    // Campos de relacionamento
    [DynamoDBProperty("ExecucaoId")]
    public string ExecucaoId { get; set; } = string.Empty;

    [DynamoDBProperty("VerificacaoId")]
    public string VerificacaoId { get; set; } = string.Empty;

    // Dados da Execução (para evitar joins)
    [DynamoDBProperty("Base")]
    public string Base { get; set; } = string.Empty;

    [DynamoDBProperty("DataBase")]
    public DateTime DataBase { get; set; }

    [DynamoDBProperty("Empresa")]
    public string Empresa { get; set; } = string.Empty;

    [DynamoDBProperty("Usuario")]
    public string Usuario { get; set; } = string.Empty;

    // Dados da Query
    [DynamoDBProperty("QueryId")]
    public string QueryId { get; set; } = string.Empty;

    [DynamoDBProperty("Sql")]
    public string Sql { get; set; } = string.Empty;

    [DynamoDBProperty("SqlOriginal")]
    public string SqlOriginal { get; set; } = string.Empty;

    [DynamoDBProperty("Parametros")]
    public Dictionary<string, string> Parametros { get; set; } = new();

    [DynamoDBProperty("ValoresParametros")]
    public List<VerificacaoParametroValor> ValoresParametros { get; set; } = new();

    // Controle de Execução
    [DynamoDBProperty("Status")]
    public string Status { get; set; } = "Pendente";

    [DynamoDBProperty("DataInicio")]
    public DateTime? DataInicio { get; set; }

    [DynamoDBProperty("DataFim")]
    public DateTime? DataFim { get; set; }

    [DynamoDBProperty("Resultado")]
    public string? Resultado { get; set; }

    [DynamoDBProperty("Erro")]
    public string? Erro { get; set; }

    [DynamoDBProperty("TempoExecucaoMs")]
    public long? TempoExecucaoMs { get; set; }

    // Configurações
    [DynamoDBProperty("TimeoutSegundos")]
    public int TimeoutSegundos { get; set; } = 60;

    [DynamoDBProperty("Prioridade")]
    public int Prioridade { get; set; } = 1;

    [DynamoDBProperty("Tentativas")]
    public int Tentativas { get; set; }

    [DynamoDBProperty("MaxTentativas")]
    public int MaxTentativas { get; set; } = 3;

    // Metadados
    [DynamoDBProperty("Metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    // Timestamps
    [DynamoDBProperty("DataCriacao")]
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    [DynamoDBProperty("DataAtualizacao")]
    public DateTime DataAtualizacao { get; set; } = DateTime.UtcNow;

    // Construtor para facilitar a criação
    public ExecucaoVerificacao() { }

    public ExecucaoVerificacao(string execucaoId, string verificacaoId)
    {
        // Criar ID único baseado na combinação ExecucaoId#VerificacaoId
        Id = $"{execucaoId}#{verificacaoId}";
        ExecucaoId = execucaoId;
        VerificacaoId = verificacaoId;
    }

    // Método para extrair ExecucaoId e VerificacaoId do ID composto
    public static (string execucaoId, string verificacaoId) ParseId(string id)
    {
        var partes = id.Split('#');
        if (partes.Length == 2)
        {
            return (partes[0], partes[1]);
        }
        return (string.Empty, string.Empty);
    }
}

