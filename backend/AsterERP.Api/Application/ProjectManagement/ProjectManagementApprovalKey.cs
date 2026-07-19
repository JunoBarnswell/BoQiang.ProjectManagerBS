using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

public static class ProjectManagementApprovalKey
{
    public static string Build(string entityType, string entityId, string? idempotencyKey)
    {
        var type = Normalize(entityType, "审批对象类型不能为空");
        var id = Normalize(entityId, "审批对象不能为空");
        var key = Normalize(idempotencyKey, "审批幂等键不能为空");
        if (!ProjectManagementAutomationEntityTypes.IsSupported(type)) throw new ValidationException("审批对象类型不受支持");
        if (key.Length > 160 || key.Contains('\n') || key.Contains('\r')) throw new ValidationException("审批幂等键无效");
        return $"{type}:{id}:{key}";
    }

    public static bool TryParse(string? businessKey, out string entityType, out string entityId, out string idempotencyKey)
    {
        entityType = string.Empty;
        entityId = string.Empty;
        idempotencyKey = string.Empty;
        var parts = businessKey?.Split(':', 3, StringSplitOptions.TrimEntries) ?? [];
        if (parts.Length != 3 || !ProjectManagementAutomationEntityTypes.IsSupported(parts[0]) || parts.Any(string.IsNullOrWhiteSpace)) return false;
        entityType = parts[0]; entityId = parts[1]; idempotencyKey = parts[2];
        return true;
    }

    private static string Normalize(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
}
