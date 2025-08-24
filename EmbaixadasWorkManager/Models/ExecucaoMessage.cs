namespace EmbaixadasWorkManager.Models;

public class ExecucaoMessage
{
    public string ExecucaoId { get; set; } = string.Empty;

    // Construtor para facilitar a criação a partir de uma string
    public ExecucaoMessage(string execucaoId)
    {
        ExecucaoId = execucaoId;
    }

    // Construtor padrão
    public ExecucaoMessage() { }
}
