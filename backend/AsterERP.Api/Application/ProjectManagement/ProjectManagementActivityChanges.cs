using System.Globalization;
using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

internal static class ProjectManagementActivityChanges
{
    public static ProjectManagementActivityFieldChange? Create(
        string field,
        string displayName,
        object? before,
        object? after,
        bool isSensitive = false)
    {
        var beforeValue = Format(before);
        var afterValue = Format(after);
        return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
            ? null
            : new ProjectManagementActivityFieldChange(field, displayName, beforeValue, afterValue, isSensitive);
    }

    public static IReadOnlyList<ProjectManagementActivityFieldChange> Collect(params ProjectManagementActivityFieldChange?[] changes) =>
        changes.Where(change => change is not null).Select(change => change!).ToList();

    private static string? Format(object? value) => value switch
    {
        null => null,
        DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
        double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
        float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
}
