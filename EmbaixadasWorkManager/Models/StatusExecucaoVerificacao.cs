namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Status possíveis para uma execução de verificação
/// </summary>
public static class StatusExecucaoVerificacao
{
    /// <summary>
    /// Verificação criada, aguardando processamento
    /// </summary>
    public const string Pendente = "Pendente";

    /// <summary>
    /// Verificação sendo executada
    /// </summary>
    public const string EmProcessamento = "EmProcessamento";

    /// <summary>
    /// Verificação executada com sucesso
    /// </summary>
    public const string Concluida = "Concluida";

    /// <summary>
    /// Verificação falhou na execução
    /// </summary>
    public const string Erro = "Erro";

    /// <summary>
    /// Verificação excedeu tempo limite
    /// </summary>
    public const string Timeout = "Timeout";

    /// <summary>
    /// Verificação cancelada pelo usuário
    /// </summary>
    public const string Cancelada = "Cancelada";
}
