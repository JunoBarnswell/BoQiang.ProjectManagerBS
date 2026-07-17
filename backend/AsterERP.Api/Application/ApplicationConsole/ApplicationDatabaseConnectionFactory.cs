using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationDatabaseConnectionFactory(ILogger<ApplicationDatabaseConnectionFactory> logger) : IApplicationDatabaseConnectionFactory
{
    public ISqlSugarClient Create(ApplicationDatabaseBindingOptions options)
    {
        EnsureSqliteDirectory(options);

        var client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = options.ConnectionString,
            DbType = ResolveDbType(options.Provider),
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

        client.Aop.OnLogExecuted = (_, _) =>
        {
            var elapsedMs = client.Ado.SqlExecutionTime.TotalMilliseconds;
            if (elapsedMs >= 800)
            {
                logger.LogWarning("Slow application database SQL detected: elapsed={ElapsedMilliseconds}ms", elapsedMs);
            }
        };

        return client;
    }

    public async Task ValidateAsync(ApplicationDatabaseBindingOptions options, CancellationToken cancellationToken = default)
    {
        ISqlSugarClient? db = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            db = Create(options);
            await db.Ado.GetIntAsync("SELECT 1");
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            logger.LogWarning(ex, "Application database validation failed for provider {Provider}", options.Provider);
            var message = string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
                ? "应用数据库连接失败，请检查 SQLite 数据库文件名"
                : "应用数据库连接失败，请检查数据库类型和连接串";
            throw new ValidationException(message);
        }
        finally
        {
            if (db is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static DbType ResolveDbType(string provider)
    {
        return provider.Trim().ToLowerInvariant() switch
        {
            "sqlite" or "sqlite3" => DbType.Sqlite,
            "mysql" => DbType.MySql,
            "postgresql" or "postgres" or "pgsql" => DbType.PostgreSQL,
            "sqlserver" or "mssql" => DbType.SqlServer,
            _ => throw new ValidationException("暂不支持该数据库类型")
        };
    }

    private static void EnsureSqliteDirectory(ApplicationDatabaseBindingOptions options)
    {
        if (!string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dataSource = options.ConnectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .FirstOrDefault(pair => pair.Length == 2 && pair[0].Equals("Data Source", StringComparison.OrdinalIgnoreCase))?[1];
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
