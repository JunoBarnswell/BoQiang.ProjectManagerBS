using AsterERP.Workflow.Persistence.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlSugar;

namespace AsterERP.Workflow.Persistence.Database;

public static class SqlSugarConfiguration
{
    public static IServiceCollection AddSqlSugar(this IServiceCollection services, string connectionString, DbType dbType = DbType.Sqlite)
    {
        services.TryAddScoped(_ => new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = dbType,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityNameService = (type, entity) =>
                {
                    entity.IsDisabledUpdateAll = true;
                }
            }
        }));
        services.TryAddScoped<ISqlSugarClient>(sp => sp.GetRequiredService<SqlSugarScope>());
        services.TryAddScoped<SqliteSchemaValidator>();
        services.TryAddScoped<DatabaseInitializer>();

        return services;
    }
}
