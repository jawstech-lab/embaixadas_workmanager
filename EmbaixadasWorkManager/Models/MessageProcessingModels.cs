namespace EmbaixadasWorkManager.Models;

public class MessageProcessingResult
{
    public bool Success { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ProcessedReceiptHandles { get; set; } = new();
}

public class MessageProcessingStats
{
    public int TotalProcessed { get; set; }
    public int TotalFailed { get; set; }
    public DateTime LastProcessingTime { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
}
