using SqlSugar;

namespace AsterERP.Api.Infrastructure.Database;

public static class SqlSugarSetup
{
    public static IServiceCollection AddAsterErpSqlSugar(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "DataSource=./data/astererp.db";
        var slowSqlThresholdMs = configuration.GetValue("SqlSugar:SlowSqlThresholdMs", 800);

        services.AddScoped<ISqlSugarClient>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SqlSugar");
            var logSql = configuration.GetValue("SqlSugar:LogSql", false);
            var client = new SqlSugarScope(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = DbType.Sqlite,
                InitKeyType = InitKeyType.Attribute,
                IsAutoCloseConnection = true
            });

            client.Aop.OnLogExecuting = (sql, parameters) =>
            {
                if (logSql)
                {
                    logger.LogDebug(
                        "SQL executing: statement={Statement}; parameterCount={ParameterCount}",
                        NormalizeStatement(sql),
                        parameters?.Length ?? 0);
                }
            };

            client.Aop.OnLogExecuted = (sql, parameters) =>
            {
                var elapsedMs = client.Ado.SqlExecutionTime.TotalMilliseconds;
                if (elapsedMs >= slowSqlThresholdMs)
                {
                    logger.LogWarning(
                        "Slow SQL detected: elapsed={ElapsedMilliseconds}ms threshold={ThresholdMilliseconds}ms statement={Statement} parameterCount={ParameterCount}",
                        elapsedMs,
                        slowSqlThresholdMs,
                        NormalizeStatement(sql),
                        parameters?.Length ?? 0);
                }
            };

            return client;
        });

        return services;
    }

    private static string NormalizeStatement(string sql)
    {
        var normalized = string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 512 ? normalized : normalized[..512] + "...";
    }
}
