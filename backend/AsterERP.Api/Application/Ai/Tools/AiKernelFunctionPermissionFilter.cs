using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionPermissionFilter(ICurrentUser currentUser)
{
    public void EnsureAllowed(AiKernelFunctionDefinition definition)
    {
        var permissionCodes = ResolvePermissionCodes(definition);
        foreach (var permissionCode in permissionCodes)
        {
            EnsurePermission(permissionCode);
        }
    }

    public void EnsureHighRiskConfirmationAllowed() =>
        EnsurePermission(PermissionCodes.AiToolConfirmHighRisk);

    private static IReadOnlyList<string> ResolvePermissionCodes(AiKernelFunctionDefinition definition)
    {
        if (definition.RequiredPermissionCodes.Count > 0)
        {
            return definition.RequiredPermissionCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var permissionCodes = new List<string>();
        if (!string.IsNullOrWhiteSpace(definition.PermissionCode))
        {
            permissionCodes.Add(definition.PermissionCode.Trim());
        }

        if (!string.IsNullOrWhiteSpace(definition.WorkflowPermissionCode))
        {
            permissionCodes.Add(definition.WorkflowPermissionCode.Trim());
        }

        return permissionCodes;
    }

    private void EnsurePermission(string permissionCode)
    {
        if (!currentUser.HasAsterErpPermission(permissionCode))
        {
            throw new ValidationException($"无权限执行 AI 工具：{permissionCode}", ErrorCodes.AiWorkflowPermissionDenied);
        }
    }
}
