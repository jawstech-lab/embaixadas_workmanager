namespace EmbaixadasWorkManager.Configuration;

public class AwsConfiguration
{
    public const string SectionName = "AWS";

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public bool UseProfile { get; set; } = false;
    public string ServiceUrl { get; set; } = string.Empty;
    public bool UseLocalStack { get; set; } = false;
}

public class SqsConfiguration
{
    public const string SectionName = "SQS";

    public string FilaExecucao { get; set; } = string.Empty;
    public string FilaExecucaoQuery { get; set; } = string.Empty;
    public string FilaExecucaoProcesso { get; set; } = string.Empty;
    public int MaxNumberOfMessages { get; set; } = 10;
    public int WaitTimeSeconds { get; set; } = 20;
    public int VisibilityTimeoutSeconds { get; set; } = 300;
}

public class DynamoDbConfiguration
{
    public const string SectionName = "DynamoDB";

    public string TableNameExecucao { get; set; } = "Execucao";
    public string TableNameVerificacao { get; set; } = "Verificacao";
    public string TableNameExecucaoVerificacao { get; set; } = "ExecucaoVerificacao";
    // TableNameExecucaoProcesso removido - CONSOLIDADO
    
    // Tabelas de consultas e parâmetros
    public string TableNameConsulta { get; set; } = "Consultas";
    public string TableNameParametro { get; set; } = "Parametros";
    
    // Novas tabelas para performance e auditoria por empresa
    public string TableNameExecucaoResumoView { get; set; } = "ExecucaoResumoView";
    public string TableNameExecucaoEmpresaStatus { get; set; } = "ExecucaoEmpresaStatus";
    
    // Tabelas para agregação de resultados
    public string TableNameResultado { get; set; } = "Resultado";
    public string TableNameResultadoAgregado { get; set; } = "ResultadoAgregado";
    
    // Tabela de justificativas
    public string TableNameJustificativa { get; set; } = "Justificativas";

    // Tabela de status do sistema (Heartbeat)
    public string TableNameSistemaStatus { get; set; } = "SistemaStatus";
    
    public string ServiceUrl { get; set; } = string.Empty;
    public bool UseLocalStack { get; set; } = false;
}

