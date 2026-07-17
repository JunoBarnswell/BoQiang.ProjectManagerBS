using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimeDataMutationPermissionService(
    IRuntimePageSchemaService pageSchemaService,
    ICurrentUser currentUser)
{
    public async Task EnsureAsync(
        string modelCode,
        string? pageCode,
        string? previewPageId,
        string action,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pageCode))
        {
            throw new ValidationException("运行时写入必须提供页面编码", ErrorCodes.PermissionDenied);
        }

        var page = await pageSchemaService.GetPublishedPageAsync(pageCode, previewPageId, cancellationToken);
        if (!RuntimePageModelAccessPolicy.IncludesModel(page, modelCode))
        {
            throw new ValidationException("运行时页面与数据模型不匹配", ErrorCodes.RuntimeDataModelInvalid);
        }

        var permissionCode = ResolveActionPermission(page.PermissionCode, page.PageCode, action);
        if (!currentUser.HasAsterErpPermission(permissionCode))
        {
            throw new ValidationException("无权限执行该运行时页面动作", ErrorCodes.PermissionDenied);
        }
    }

    private static string ResolveActionPermission(string? viewPermissionCode, string pageCode, string action)
    {
        if (!string.IsNullOrWhiteSpace(viewPermissionCode) &&
            viewPermissionCode.EndsWith(":view", StringComparison.OrdinalIgnoreCase))
        {
            return $"{viewPermissionCode[..^":view".Length]}:{action}";
        }

        return PermissionCodes.BuildAppRuntimePagePermission(pageCode, action);
    }
}
