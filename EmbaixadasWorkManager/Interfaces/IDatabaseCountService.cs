using EmbaixadasWorkManager.Models;

namespace EmbaixadasWorkManager.Interfaces;

public interface IDatabaseCountService
{
    /// <summary>
    /// Obtém o total de registros de uma consulta SQL
    /// </summary>
    Task<int> GetTotalCountAsync(string sql, string databaseType, string connectionString, IDictionary<string, object>? parameters = null);
}
