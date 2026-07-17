using Microsoft.Data.Sqlite;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static class ApplicationIntegrationDuplicateViolationClassifier
{
    public static bool IsUniqueConstraintViolation(string provider, Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite && sqlite.SqliteErrorCode == 19)
            {
                return true;
            }

            var message = current.Message;
            if (ContainsProviderUniqueSignal(provider, message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsProviderUniqueSignal(string provider, string message)
    {
        var normalized = message.ToLowerInvariant();
        if (normalized.Contains("duplicate key") || normalized.Contains("unique constraint") || normalized.Contains("duplicate entry"))
        {
            return true;
        }

        return provider.ToLowerInvariant() switch
        {
            "mysql" => normalized.Contains("1062") || normalized.Contains("23000"),
            "sqlserver" => normalized.Contains("2601") || normalized.Contains("2627"),
            "postgresql" => normalized.Contains("23505"),
            "sqlite" => normalized.Contains("sqlite error 19") || normalized.Contains("unique constraint failed"),
            _ => false
        };
    }
}
