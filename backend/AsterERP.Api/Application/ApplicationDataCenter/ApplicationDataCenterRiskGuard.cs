using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterRiskGuard
{
    private static readonly HashSet<string> HighRiskFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "objectType",
        "objectCode",
        "fieldCode",
        "fieldName",
        "dataType",
        "binding",
        "routePath",
        "httpMethod",
        "targetObjectId",
        "sourceObjectId",
        "permissionCode",
        "configJson"
    };

    public void EnsureConfirmed(
        ApplicationDataCenterObjectEntity existing,
        IReadOnlyCollection<string> changedFields,
        IReadOnlyCollection<string> confirmedRiskFields)
    {
        if (existing.ReferenceCount <= 0)
        {
            return;
        }

        var missing = changedFields
            .Where(field => HighRiskFields.Contains(field))
            .Where(field => !confirmedRiskFields.Contains(field, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        throw new ValidationException(
            $"当前对象已有 {existing.ReferenceCount} 个引用，修改高风险字段前需确认: {string.Join(", ", missing)}",
            ErrorCodes.ApplicationDataCenterRiskConfirmationRequired);
    }
}
