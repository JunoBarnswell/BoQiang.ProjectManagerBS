using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

/// <summary>Defines the only data-source providers that may enter the production execution path.</summary>
public static class ApplicationDataSourceProviderPolicy
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ApplicationDataSourceType.Sqlite,
        ApplicationDataSourceType.MySql,
        ApplicationDataSourceType.PostgreSql,
        ApplicationDataSourceType.SqlServer,
        ApplicationDataSourceType.ApplicationDatabase,
        ApplicationDataSourceType.Excel,
        ApplicationDataSourceType.Csv
    };

    // These literals are deliberately isolated in the migration policy. They are not public provider contracts.
    private static readonly HashSet<string> RetiredTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "REST", "RestApi", "MinIO", "S3", "OSS", "Kafka", "RabbitMQ", "RabbitMq"
    };

    public static bool IsSupported(string type) => !string.IsNullOrWhiteSpace(type) && SupportedTypes.Contains(type);

    public static bool IsRetired(string type) => !string.IsNullOrWhiteSpace(type) && RetiredTypes.Contains(type);

    public static IReadOnlyCollection<string> RetiredProviderCodes => RetiredTypes;

    public static string GetMigrationDiagnostic(string type) =>
        $"数据源 Provider '{type}' 已下线，必须迁移到受支持的数据库、ApplicationDatabase、Excel 或 CSV 数据源后才能继续运行。";

    public static void EnsureSupportedForWrite(string type)
    {
        if (IsSupported(type))
            return;

        var message = IsRetired(type)
            ? GetMigrationDiagnostic(type)
            : $"不支持的数据源 Provider '{type}'。";
        throw new ValidationException(message, ErrorCodes.ApplicationDataCenterInvalidConfig);
    }
}
