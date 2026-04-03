using Amazon.DynamoDBv2.DataModel;

namespace EmbaixadasWorkManager.Models;

[DynamoDBTable("SistemaStatus")]
public class SistemaStatus
{
    [DynamoDBHashKey("PK")]
    public string PK { get; set; } = "SYSTEM_HEALTH"; // Fixo para o status geral

    [DynamoDBRangeKey("SK")]
    public string SK { get; set; } = "METRICS";

    [DynamoDBProperty("LastManagerPulse")]
    public DateTime LastManagerPulse { get; set; }

    [DynamoDBProperty("LastQueuePulse")]
    public DateTime? LastQueuePulse { get; set; }

    [DynamoDBProperty("QueueBacklog")]
    public int QueueBacklog { get; set; }

    [DynamoDBProperty("QueueBacklogWorkManager")]
    public int QueueBacklogWorkManager { get; set; }

    [DynamoDBProperty("QueueBacklogQueryExecutor")]
    public int QueueBacklogQueryExecutor { get; set; }

    [DynamoDBProperty("ActiveWorkers")]
    public int ActiveWorkers { get; set; }

    [DynamoDBProperty("Status")]
    public string Status { get; set; } = "Operacional";
}

[DynamoDBTable("SistemaStatus")]
public class WorkerPulse
{
    [DynamoDBHashKey("PK")]
    public string PK { get; set; } = "WORKER_PULSE";

    [DynamoDBRangeKey("SK")]
    public string WorkerId { get; set; } = string.Empty; // Nome da máquina ou ID da instância

    [DynamoDBProperty("LastPulse")]
    public DateTime LastPulse { get; set; }

    [DynamoDBProperty("Status")]
    public string Status { get; set; } = "Ativo";
}
