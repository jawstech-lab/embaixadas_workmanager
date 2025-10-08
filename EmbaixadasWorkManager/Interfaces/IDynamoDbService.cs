using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Interfaces;

public interface IDynamoDbService
{
    Task<T?> GetAsync<T>(string id) where T : class;
    Task<T?> GetAsync<T>(string hashKey, string rangeKey) where T : class;
    Task<IEnumerable<T>> GetAllAsync<T>() where T : class;
    Task<IEnumerable<T>> QueryAsync<T>(string hashKey, string? rangeKey = null) where T : class;
    Task SaveAsync<T>(T item) where T : class;
    Task UpdateAsync<T>(T item) where T : class;
    Task DeleteAsync<T>(string id) where T : class;
    Task DeleteAsync<T>(string hashKey, string rangeKey) where T : class;
    Task<bool> ExistsAsync<T>(string id) where T : class;
    Task<bool> ExistsAsync<T>(string hashKey, string rangeKey) where T : class;
    
    // Métodos específicos para Execucao
    Task<bool> SaveExecucaoAsync(Execucao execucao);
    Task<Execucao?> GetExecucaoAsync(string execucaoId);
    
    // Métodos específicos para ExecucaoVerificacao
    Task<ExecucaoVerificacao?> GetExecucaoVerificacaoAsync(string execucaoVerificacaoId);
    
    // Métodos específicos para Verificacao
    Task<List<Verificacao>> GetTodasVerificacoesAsync();
    Task<List<Verificacao>> GetVerificacoesPorEmbaixadasAsync(List<string> idEmbaixadas);
}

