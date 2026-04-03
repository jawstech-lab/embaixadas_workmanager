namespace EmbaixadasWorkManager.Models;

/// <summary>
/// Status possíveis para uma execução
/// </summary>
public static class StatusExecucao
{
    /// <summary>
    /// Execução pendente de processamento
    /// </summary>
    public const string Pendente = "Pendente";

    /// <summary>
    /// Execução em processamento
    /// </summary>
    public const string EmProcessamento = "EmProcessamento";

    /// <summary>
    /// Execução cadastrada
    /// </summary>
    public const string Cadastrado = "Cadastrado";

    // NOVOS STATUS (consolidados do ExecucaoProcesso)
    /// <summary>
    /// Verificações enviadas para processamento
    /// </summary>
    public const string AguardandoProcessamento = "AguardandoProcessamento";

    /// <summary>
    /// Verificações sendo processadas
    /// </summary>
    public const string ProcessandoVerificacoes = "ProcessandoVerificacoes";

    /// <summary>
    /// Todas as verificações processadas
    /// </summary>
    public const string VerificacoesConcluidas = "VerificacoesConcluidas";

    /// <summary>
    /// Agregando resultados e consolidando dashboards
    /// </summary>
    public const string AgregandoResultados = "AgregandoResultados";

    /// <summary>
    /// Execução finalizada com sucesso
    /// </summary>
    public const string FinalizadaComSucesso = "FinalizadaComSucesso";

    /// <summary>
    /// Execução finalizada com erro
    /// </summary>
    public const string FinalizadaComErro = "FinalizadaComErro";
}


