using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimeDataReadPermissionService(
    IRuntimePageSchemaService pageSchemaService,
    ICurrentUser currentUser)
{
    public async Task EnsureAsync(
        string modelCode,
        string? pageCode,
        string? previewPageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pageCode))
        {
            if (currentUser.HasAsterErpPermission(PermissionCodes.RuntimeDataQuery))
            {
                return;
            }

            throw new ValidationException("运行时查询必须提供页面编码", ErrorCodes.PermissionDenied);
        }

        var page = await pageSchemaService.GetPublishedPageAsync(pageCode, previewPageId, cancellationToken);
        if (!RuntimePageModelAccessPolicy.IncludesModel(page, modelCode))
        {
            throw new ValidationException("运行时页面与数据模型不匹配", ErrorCodes.RuntimeDataModelInvalid);
        }
    }
}
