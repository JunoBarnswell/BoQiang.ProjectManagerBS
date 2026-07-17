using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationManagedSqliteDatabaseResolver(IHostEnvironment environment)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".db",
        ".sqlite",
        ".sqlite3"
    };

    public ApplicationManagedSqliteDatabase Resolve(string databaseName, string tenantId, string appCode)
    {
        var normalizedName = NormalizeDatabaseName(databaseName);
        var directory = Path.Combine(
            environment.ContentRootPath,
            "data",
            "application-databases",
            NormalizePathSegment(tenantId, "租户标识不合法"),
            NormalizePathSegment(appCode, "应用编码不合法"));
        var absolutePath = Path.GetFullPath(Path.Combine(directory, normalizedName));
        var absoluteDirectory = Path.GetFullPath(directory);

        if (!absolutePath.StartsWith(absoluteDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("SQLite 数据库文件名不合法");
        }

        return new ApplicationManagedSqliteDatabase(
            normalizedName,
            absolutePath,
            $"Data Source={absolutePath}");
    }

    private static string NormalizeDatabaseName(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException("SQLite 数据库文件名不能为空");
        }

        if (Path.IsPathFullyQualified(normalized) ||
            normalized.Contains(Path.DirectorySeparatorChar) ||
            normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ValidationException("SQLite 数据库文件名只能填写文件名，例如 wms.db");
        }

        if (normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ValidationException("SQLite 数据库文件名包含非法字符");
        }

        var extension = Path.GetExtension(normalized);
        if (string.IsNullOrWhiteSpace(extension))
        {
            normalized += ".db";
            extension = ".db";
        }

        if (!AllowedExtensions.Contains(extension))
        {
            throw new ValidationException("SQLite 数据库文件扩展名只能是 .db、.sqlite 或 .sqlite3");
        }

        return normalized;
    }

    private static string NormalizePathSegment(string value, string message)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            normalized.Contains(Path.DirectorySeparatorChar) ||
            normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ValidationException(message);
        }

        return normalized;
    }
}
