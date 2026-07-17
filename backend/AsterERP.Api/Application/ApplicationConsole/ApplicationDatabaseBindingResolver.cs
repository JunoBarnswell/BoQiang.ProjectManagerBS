using System.Text.Json;
using System.Text.Json.Nodes;

using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationDatabaseBindingResolver(
    IApplicationConnectionStringProtector connectionStringProtector,
    ApplicationManagedSqliteDatabaseResolver managedSqliteDatabaseResolver)
{
    private const string PrimaryNodeName = "applicationDatabase";
    private const string LegacyNodeName = "database";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public ApplicationDatabaseBindingOptions? Resolve(string? configJson) =>
        Resolve(configJson, null, null);

    public ApplicationDatabaseBindingOptions? Resolve(
        string? configJson,
        string? tenantId,
        string? appCode)
    {
        var resolution = ResolveStatus(configJson, tenantId, appCode);
        if (resolution.Status == ApplicationDatabaseBindingStatus.NotConfigured)
        {
            return null;
        }

        if (resolution.Status != ApplicationDatabaseBindingStatus.Ready || resolution.Options is null)
        {
            throw new ValidationException(
                resolution.Message ?? "应用数据库绑定配置不可用");
        }

        return resolution.Options;
    }

    public ApplicationDatabaseBindingResolution ResolveStatus(
        string? configJson,
        string? tenantId = null,
        string? appCode = null)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return NotConfigured("未配置应用数据库");
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(configJson) as JsonObject
                ?? throw new JsonException("root must be an object");
        }
        catch (JsonException)
        {
            return Invalid("应用配置 JSON 无法解析");
        }

        if (root[PrimaryNodeName] is null)
        {
            return root[LegacyNodeName] is not null
                ? MigrationRequired("检测到旧 database 绑定节点，请执行一次性迁移")
                : NotConfigured("未配置应用数据库");
        }

        if (root[PrimaryNodeName] is not JsonObject node)
        {
            return Invalid("applicationDatabase 必须是对象");
        }

        var provider = node["provider"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            return Invalid("applicationDatabase.provider 缺失");
        }

        if (!IsCanonicalProvider(provider))
        {
            return MigrationRequired("applicationDatabase.provider 使用了旧别名，请执行一次性迁移");
        }

        if (node["connectionString"] is not null || node["rawConnectionString"] is not null)
        {
            return MigrationRequired("applicationDatabase 包含未加密连接串，请执行一次性迁移");
        }

        var cipherText = node["connectionStringCipherText"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return Invalid("applicationDatabase.connectionStringCipherText 缺失");
        }

        string connectionString;
        try
        {
            connectionString = connectionStringProtector.Unprotect(cipherText);
        }
        catch (ValidationException)
        {
            return Invalid("应用数据库密文无法解密");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Invalid("应用数据库密文为空");
        }

        var options = new ApplicationDatabaseBindingOptions(
            provider,
            connectionString,
            ReadOptionalString(node, "displayName"),
            ReadOptionalString(node, "databaseName"),
            TryReadDateTime(node["updatedAt"]),
            ReadOptionalString(node, "updatedBy"));
        return new ApplicationDatabaseBindingResolution(
            ApplicationDatabaseBindingStatus.Ready,
            options,
            "应用数据库绑定配置有效",
            false);
    }

    public bool HasBinding(string? configJson) =>
        ResolveStatus(configJson).Status is
            ApplicationDatabaseBindingStatus.Ready or
            ApplicationDatabaseBindingStatus.Unavailable;

    public ApplicationDatabaseBindingOptions CreateOptions(
        ApplicationDatabaseBindingRequest request,
        string tenantId,
        string appCode)
    {
        var provider = NormalizeProvider(request.Provider);
        var (connectionString, databaseName) = ResolveConnection(provider, request, tenantId, appCode);
        return new ApplicationDatabaseBindingOptions(
            provider,
            connectionString,
            NormalizeOptional(request.DisplayName),
            databaseName,
            DateTime.UtcNow,
            null);
    }

    public string Merge(
        string? configJson,
        ApplicationDatabaseBindingOptions options,
        string updatedBy,
        DateTime updatedAt)
    {
        var root = ParseRoot(configJson);
        root[PrimaryNodeName] = BuildCanonicalNode(options, updatedBy, updatedAt);
        root.Remove(LegacyNodeName);
        return root.ToJsonString(SerializerOptions);
    }

    public string MigrateLegacy(
        string configJson,
        string tenantId,
        string appCode,
        string updatedBy,
        DateTime updatedAt)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(configJson) as JsonObject
                ?? throw new JsonException("root must be an object");
        }
        catch (JsonException)
        {
            throw new ValidationException("应用配置 JSON 无法迁移");
        }

        var node = root[PrimaryNodeName] as JsonObject ?? root[LegacyNodeName] as JsonObject;
        if (node is null)
        {
            throw new ValidationException("未找到可迁移的应用数据库绑定");
        }

        var legacyProvider = ReadOptionalString(node, "provider") ?? throw new ValidationException("Legacy application database provider is missing.");
        var provider = NormalizeProvider(legacyProvider, allowLegacyAliases: true);
        var cipherText = ReadOptionalString(node, "connectionStringCipherText");
        var connectionString = string.IsNullOrWhiteSpace(cipherText)
            ? ReadOptionalString(node, "connectionString") ?? ReadOptionalString(node, "rawConnectionString")
            : UnprotectLegacy(cipherText);

        if (string.IsNullOrWhiteSpace(connectionString) &&
            string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = ReadOptionalString(node, "databaseName")
                ?? throw new ValidationException("旧 SQLite 绑定 databaseName 缺失");
            connectionString = managedSqliteDatabaseResolver
                .Resolve(databaseName, tenantId, appCode)
                .ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ValidationException("旧应用数据库绑定缺少连接信息");
        }

        var options = new ApplicationDatabaseBindingOptions(
            provider,
            connectionString,
            ReadOptionalString(node, "displayName"),
            ReadOptionalString(node, "databaseName"),
            updatedAt,
            updatedBy);
        return Merge(configJson, options, updatedBy, updatedAt);
    }

    private JsonObject BuildCanonicalNode(
        ApplicationDatabaseBindingOptions options,
        string updatedBy,
        DateTime updatedAt) =>
        new()
        {
            ["provider"] = options.Provider,
            ["connectionStringCipherText"] = connectionStringProtector.Protect(options.ConnectionString),
            ["displayName"] = NormalizeOptional(options.DisplayName),
            ["databaseName"] = NormalizeOptional(options.DatabaseName),
            ["updatedAt"] = updatedAt.ToString("O"),
            ["updatedBy"] = NormalizeOptional(updatedBy)
        };

    private string UnprotectLegacy(string cipherText)
    {
        try
        {
            return connectionStringProtector.Unprotect(cipherText);
        }
        catch (ValidationException ex)
        {
            throw new ValidationException($"旧应用数据库密文无法解密：{ex.Message}");
        }
    }

    private static ApplicationDatabaseBindingResolution NotConfigured(string message) =>
        new(ApplicationDatabaseBindingStatus.NotConfigured, null, message, false);

    private static ApplicationDatabaseBindingResolution Invalid(string message) =>
        new(ApplicationDatabaseBindingStatus.InvalidConfiguration, null, message, false);

    private static ApplicationDatabaseBindingResolution MigrationRequired(string message) =>
        new(ApplicationDatabaseBindingStatus.MigrationRequired, null, message, true);

    private static JsonObject ParseRoot(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(configJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            throw new ValidationException("租户应用扩展配置 JSON 格式不正确");
        }
    }

    private static string NormalizeProvider(string value, bool allowLegacyAliases = false)
    {
        var normalized = NormalizeRequired(value, "数据库类型不能为空");
        return normalized.ToLowerInvariant() switch
        {
            "sqlite" => "Sqlite",
            "sqlite3" when allowLegacyAliases => "Sqlite",
            "mysql" => "MySql",
            "postgresql" => "PostgreSQL",
            "postgres" or "pgsql" when allowLegacyAliases => "PostgreSQL",
            "sqlserver" => "SqlServer",
            "mssql" when allowLegacyAliases => "SqlServer",
            _ => throw new ValidationException("暂不支持该数据库类型")
        };
    }

    private static bool IsCanonicalProvider(string provider) =>
        provider is "Sqlite" or "MySql" or "PostgreSQL" or "SqlServer";

    private (string ConnectionString, string? DatabaseName) ResolveConnection(
        string provider,
        ApplicationDatabaseBindingRequest request,
        string tenantId,
        string appCode)
    {
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = NormalizeRequired(request.DatabaseName, "SQLite 数据库文件名不能为空");
            var managedDatabase = managedSqliteDatabaseResolver.Resolve(databaseName, tenantId, appCode);
            return (managedDatabase.ConnectionString, managedDatabase.DatabaseName);
        }

        return (NormalizeRequired(request.ConnectionString, "数据库连接串不能为空"), null);
    }

    private static string NormalizeRequired(string? value, string message)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(message);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? ReadOptionalString(JsonObject node, string propertyName) =>
        node[propertyName]?.GetValue<string>() is { } value ? NormalizeOptional(value) : null;

    private static DateTime? TryReadDateTime(JsonNode? node)
    {
        var value = node?.GetValue<string>();
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
