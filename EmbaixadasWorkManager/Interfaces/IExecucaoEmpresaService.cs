namespace EmbaixadasWorkManager.Interfaces;

/// <summary>
/// Serviço responsável por gerenciar as operações de execução por empresa.
/// Grava registros nas tabelas ExecucaoResumoView e ExecucaoEmpresaStatus
/// para melhorar a performance de consultas e manter auditoria completa.
/// </summary>
public interface IExecucaoEmpresaService
{
    /// <summary>
    /// Grava registros nas tabelas ExecucaoResumoView e ExecucaoEmpresaStatus
    /// para cada combinação de embaixada e empresa.
    /// 
    /// Ação 1: Grava em ExecucaoEmpresaStatus (auditoria completa)
    /// Ação 2: Atualiza ExecucaoResumoView condicionalmente (apenas se mais recente)
    /// 
    /// NOVA ESTRUTURA ExecucaoResumoView:
    /// PK: VIEW#LAST_EXEC#EMB#<IdEmbaixada>
    /// SK: EMP#<Empresa>
    /// </summary>
    /// <param name="execucaoId">ID da execução</param>
    /// <param name="idEmbaixadas">Lista de IDs de embaixadas</param>
    /// <param name="empresasString">String com empresas separadas por vírgula (ex: "MA,RS,SP")</param>
    /// <param name="dataSolicitacao">Data e hora da solicitação</param>
    /// <param name="status">Status atual da execução</param>
    Task GravarExecucaoPorEmbaixadasEEmpresasAsync(
        string execucaoId,
        List<string> idEmbaixadas,
        string empresasString,
        DateTime dataSolicitacao,
        string status);
    
    /// <summary>
    /// Atualiza o status final nas tabelas ExecucaoResumoView e ExecucaoEmpresaStatus
    /// após a finalização da execução.
    /// NOTA: TotalApontamentos NÃO é atualizado (calcular somando QTDs do ResultadoAgregado)
    /// </summary>
    /// <param name="execucaoId">ID da execução</param>
    /// <param name="idEmbaixadas">Lista de IDs de embaixadas</param>
    /// <param name="empresasString">String com empresas</param>
    /// <param name="statusFinal">Status final da execução</param>
    /// <param name="totalApontamentos">A volumetria perfeitamente cálculada no banco</param>
    Task AtualizarStatusFinalAsync(
        string execucaoId,
        List<string> idEmbaixadas,
        string empresasString,
        string statusFinal,
        int totalApontamentos);

    /// <summary>
    /// Extrai siglas de empresas de uma string separada por vírgulas, ponto e vírgula ou pipe.
    /// Remove espaços, converte para maiúsculas e remove duplicatas.
    /// </summary>
    /// <param name="empresasString">String com empresas (ex: "MA,RS,SP" ou "ma, rs, sp")</param>
    /// <returns>Lista de siglas únicas em maiúsculas</returns>
    List<string> ExtrairSiglasEmpresas(string empresasString);
}



