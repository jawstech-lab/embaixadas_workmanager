using EmbaixadasWorkManager.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;

namespace EmbaixadasWorkManager.Services;

public class DatabaseCountService : IDatabaseCountService
{
    private readonly ILogger<DatabaseCountService> _logger;

    public DatabaseCountService(ILogger<DatabaseCountService> logger)
    {
        _logger = logger;
    }

    public async Task<int> GetTotalCountAsync(string sql, string databaseType, string connectionString, IDictionary<string, object>? parameters = null)
    {
        try
        {
            // Remover ponto e vírgula se existir para não quebrar a subquery
            var query = sql.Trim();
            if (query.EndsWith(";"))
            {
                query = query.Substring(0, query.Length - 1);
            }

            var countSql = $"SELECT COUNT(*) FROM ({query}) as subquery_count";
            _logger.LogInformation("Executando contagem preliminar de registros para sharding. Tipo: {DatabaseType}", databaseType);

            if (databaseType.ToLower().Contains("postgres"))
            {
                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(countSql, conn);
                AddParameters(cmd, parameters);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            else if (databaseType.ToLower().Contains("sqlserver") || databaseType.ToLower().Contains("mssql"))
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(countSql, conn);
                AddParameters(cmd, parameters);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }

            throw new NotSupportedException($"Tipo de banco '{databaseType}' não suportado para contagem automática.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha crítica ao obter contagem de registros para sharding no banco {DatabaseType}. Verifique a sintaxe da query.", databaseType);
            throw;
        }
    }

    private void AddParameters(IDbCommand cmd, IDictionary<string, object>? parameters)
    {
        if (parameters == null) return;
        foreach (var param in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = param.Key.StartsWith("@") ? param.Key : "@" + param.Key;
            
            // Tratamento especial para nomes de parâmetros no Npgsql (usa : ou sem prefixo)
            if (cmd is NpgsqlCommand)
            {
                p.ParameterName = param.Key.Replace("@", "").Replace(":", "");
            }

            p.Value = param.Value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
