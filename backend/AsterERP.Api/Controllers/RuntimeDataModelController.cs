using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/runtime/models")]
public sealed class RuntimeDataModelController(
    IRuntimeDataModelService runtimeDataModelService,
    RuntimeDataReadPermissionService readPermissionService,
    RuntimeDataMutationPermissionService mutationPermissionService) : BaseApiController
{
    private const int MaxCompositeChildGroups = 20;
    private const int MaxCompositeChildRows = 500;

    [HttpPost("{modelCode}/query")]
    public async Task<IActionResult> QueryAsync(
        string modelCode,
        [FromBody] RuntimeQueryRequest request,
        CancellationToken cancellationToken)
    {
        await readPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, cancellationToken);
        return ApiOk(await runtimeDataModelService.QueryAsync(modelCode, request, cancellationToken));
    }

    [HttpGet("{modelCode}/{id}")]
    public async Task<IActionResult> GetDetailAsync(
        string modelCode,
        string id,
        [FromQuery] string? pageCode,
        [FromQuery] string? previewPageId,
        CancellationToken cancellationToken)
    {
        await readPermissionService.EnsureAsync(modelCode, pageCode, previewPageId, cancellationToken);
        return ApiOk(await runtimeDataModelService.GetDetailAsync(modelCode, id, cancellationToken));
    }

    [HttpPost("{modelCode}/composite-detail")]
    public async Task<IActionResult> GetCompositeDetailAsync(
        string modelCode,
        [FromBody] RuntimeCompositeDetailRequest? request,
        CancellationToken cancellationToken)
    {
        request = NormalizeCompositeDetailRequest(request);
        EnsureRootModelMatchesRoute(modelCode, request.RootModelCode);
        await readPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, cancellationToken);
        await EnsureCompositeReadPermissionsAsync(request.Children, request.PageCode, request.PreviewPageId, cancellationToken);
        return ApiOk(await runtimeDataModelService.GetCompositeDetailAsync(request, cancellationToken));
    }

    [HttpPost("{modelCode}/operations/execute")]
    public async Task<IActionResult> ExecuteOperationAsync(
        string modelCode,
        [FromBody] RuntimeModelOperationRequest request,
        CancellationToken cancellationToken)
    {
        var definition = await runtimeDataModelService.GetPublishedDefinitionAsync(modelCode, cancellationToken);
        var operation = definition.Operations?.FirstOrDefault(item =>
            string.Equals(item.OperationCode, request.OperationCode, StringComparison.OrdinalIgnoreCase));
        var operationType = operation?.OperationType?.Trim().ToLowerInvariant();
        if (TryResolveMutationAction(operationType, out var action))
        {
            await mutationPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, action, cancellationToken);
            await EnsureModelOperationChildMutationPermissionsAsync(
                operationType,
                operation?.Children ?? [],
                request.PageCode,
                request.PreviewPageId,
                cancellationToken);
        }
        else
        {
            await readPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, cancellationToken);
        }

        return ApiOk(await runtimeDataModelService.ExecuteOperationAsync(modelCode, request, cancellationToken));
    }

    private static bool TryResolveMutationAction(string? operationType, out string action)
    {
        action = operationType switch
        {
            "create" or "compositecreate" => "add",
            "update" or "compositeupdate" => "edit",
            "delete" or "compositedelete" => "delete",
            _ => string.Empty
        };
        return action.Length > 0;
    }

    [HttpPost("{modelCode}")]
    public async Task<IActionResult> CreateAsync(
        string modelCode,
        [FromBody] RuntimeCreateRequest request,
        CancellationToken cancellationToken)
    {
        await mutationPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, "add", cancellationToken);
        var response = await runtimeDataModelService.CreateAsync(modelCode, request.Values, cancellationToken);
        return ApiOk(new RuntimeMutationResponse(response.Id, response.Row, true));
    }

    [HttpPost("{modelCode}/composite")]
    public async Task<IActionResult> CreateCompositeAsync(
        string modelCode,
        [FromBody] RuntimeCompositeCreateRequest? request,
        CancellationToken cancellationToken)
    {
        request = EnsureCompositeCreateRequest(request);
        EnsureRootModelMatchesRoute(modelCode, request.RootModelCode);
        await mutationPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, "add", cancellationToken);
        await EnsureCompositeMutationPermissionsAsync(
            request.Children.Select(child => child.ModelCode),
            request.PageCode,
            request.PreviewPageId,
            "add",
            cancellationToken);
        return ApiOk(await runtimeDataModelService.CreateCompositeAsync(request, cancellationToken));
    }

    [HttpPatch("{modelCode}/{id}")]
    public async Task<IActionResult> UpdateAsync(
        string modelCode,
        string id,
        [FromBody] RuntimeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await mutationPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, "edit", cancellationToken);
        await runtimeDataModelService.UpdateFieldsAsync(modelCode, id, request.Values, cancellationToken);
        var detail = await runtimeDataModelService.GetDetailAsync(modelCode, id, cancellationToken);
        return ApiOk(new RuntimeMutationResponse(id, detail.Row, true));
    }

    [HttpPatch("{modelCode}/composite/{id}")]
    public async Task<IActionResult> UpdateCompositeAsync(
        string modelCode,
        string id,
        [FromBody] RuntimeCompositeUpdateRequest? request,
        CancellationToken cancellationToken)
    {
        request = NormalizeCompositeUpdateRequest(request, id);
        EnsureRootModelMatchesRoute(modelCode, request.RootModelCode);
        await mutationPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, "edit", cancellationToken);
        await EnsureCompositeMutationPermissionsAsync(
            request.Children.Select(child => child.ModelCode),
            request.PageCode,
            request.PreviewPageId,
            "edit",
            cancellationToken);
        await EnsureCompositeMutationPermissionsAsync(
            request.Children
                .Where(child => child.DeleteMissing || (child.DeleteIds is not null && child.DeleteIds.Count > 0))
                .Select(child => child.ModelCode),
            request.PageCode,
            request.PreviewPageId,
            "delete",
            cancellationToken);
        return ApiOk(await runtimeDataModelService.UpdateCompositeAsync(request, cancellationToken));
    }

    [HttpDelete("{modelCode}/{id}")]
    public async Task<IActionResult> DeleteAsync(
        string modelCode,
        string id,
        [FromQuery] string? pageCode,
        [FromQuery] string? previewPageId,
        CancellationToken cancellationToken)
    {
        await mutationPermissionService.EnsureAsync(modelCode, pageCode, previewPageId, "delete", cancellationToken);
        var response = await runtimeDataModelService.DeleteAsync(modelCode, id, cancellationToken);
        return ApiOk(new RuntimeMutationResponse(response.Id, null, response.Deleted));
    }

    [HttpPost("{modelCode}/composite-delete")]
    public async Task<IActionResult> DeleteCompositeAsync(
        string modelCode,
        [FromBody] RuntimeCompositeDeleteRequest? request,
        CancellationToken cancellationToken)
    {
        request = NormalizeCompositeDeleteRequest(request);
        EnsureRootModelMatchesRoute(modelCode, request.RootModelCode);
        await mutationPermissionService.EnsureAsync(modelCode, request.PageCode, request.PreviewPageId, "delete", cancellationToken);
        await EnsureCompositeMutationPermissionsAsync(
            request.Children.Select(child => child.ModelCode),
            request.PageCode,
            request.PreviewPageId,
            "delete",
            cancellationToken);
        return ApiOk(await runtimeDataModelService.DeleteCompositeAsync(request, cancellationToken));
    }

    private static void EnsureRootModelMatchesRoute(string routeModelCode, string requestRootModelCode)
    {
        if (!string.Equals(routeModelCode, requestRootModelCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("复合模型操作的根模型编码必须与路由模型一致", ErrorCodes.ParameterInvalid);
        }
    }

    private static RuntimeCompositeCreateRequest EnsureCompositeCreateRequest(RuntimeCompositeCreateRequest? request)
    {
        if (request is null)
        {
            throw new ValidationException("复合创建请求不能为空", ErrorCodes.ParameterInvalid);
        }

        if (request.RootValues is null || request.RootValues.Count == 0)
        {
            throw new ValidationException("复合创建必须提供主对象字段", ErrorCodes.ParameterInvalid);
        }

        var children = request.Children ?? [];
        EnsureCompositeChildGroupLimit(children);
        foreach (var child in children)
        {
            EnsureCompositeChildModel(child.ModelCode, child.ForeignKeyField);
            if (child.Rows is null)
            {
                throw new ValidationException("复合创建子对象行集合不能为空", ErrorCodes.ParameterInvalid);
            }

            if (child.Rows.Count > MaxCompositeChildRows)
            {
                throw new ValidationException($"复合创建单个子对象最多允许 {MaxCompositeChildRows} 行", ErrorCodes.ParameterInvalid);
            }
        }

        return request;
    }

    private static RuntimeCompositeDetailRequest NormalizeCompositeDetailRequest(RuntimeCompositeDetailRequest? request)
    {
        if (request is null)
        {
            throw new ValidationException("复合详情请求不能为空", ErrorCodes.ParameterInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.RootId))
        {
            throw new ValidationException("复合详情必须提供主对象主键", ErrorCodes.ParameterInvalid);
        }

        var children = request.Children ?? [];
        EnsureCompositeChildGroupLimit(children);
        foreach (var child in children)
        {
            EnsureCompositeChildModel(child.ModelCode, child.ForeignKeyField);
        }

        return request with { Children = children };
    }

    private static RuntimeCompositeDeleteRequest NormalizeCompositeDeleteRequest(RuntimeCompositeDeleteRequest? request)
    {
        if (request is null)
        {
            throw new ValidationException("复合删除请求不能为空", ErrorCodes.ParameterInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.RootId))
        {
            throw new ValidationException("复合删除必须提供主对象主键", ErrorCodes.ParameterInvalid);
        }

        EnsureCompositeChildGroupLimit(request.Children);
        var children = (request.Children ?? [])
            .Select(child =>
            {
                EnsureCompositeChildModel(child.ModelCode, child.ForeignKeyField);
                return string.IsNullOrWhiteSpace(child.ParentId)
                    ? child with { ParentId = request.RootId }
                    : child;
            })
            .ToArray();

        return request with { Children = children };
    }

    private static RuntimeCompositeUpdateRequest NormalizeCompositeUpdateRequest(
        RuntimeCompositeUpdateRequest? request,
        string routeId)
    {
        if (request is null)
        {
            throw new ValidationException("复合更新请求不能为空", ErrorCodes.ParameterInvalid);
        }

        var rootId = string.IsNullOrWhiteSpace(request.RootId) ? routeId : request.RootId;
        if (string.IsNullOrWhiteSpace(rootId))
        {
            throw new ValidationException("复合更新必须提供主对象主键", ErrorCodes.ParameterInvalid);
        }

        var children = request.Children ?? [];
        EnsureCompositeChildGroupLimit(children);
        foreach (var child in children)
        {
            EnsureCompositeChildModel(child.ModelCode, child.ForeignKeyField);
            if (child.Rows is null)
            {
                throw new ValidationException("复合更新子对象行集合不能为空", ErrorCodes.ParameterInvalid);
            }

            if (child.Rows.Count > MaxCompositeChildRows)
            {
                throw new ValidationException($"复合更新单个子对象最多允许 {MaxCompositeChildRows} 行", ErrorCodes.ParameterInvalid);
            }

            if (child.DeleteIds is not null && child.DeleteIds.Count > MaxCompositeChildRows)
            {
                throw new ValidationException($"复合更新单个子对象最多允许删除 {MaxCompositeChildRows} 行", ErrorCodes.ParameterInvalid);
            }
        }

        return request with { RootId = rootId, RootValues = request.RootValues ?? new Dictionary<string, object?>(), Children = children };
    }

    private static void EnsureCompositeChildGroupLimit<TChild>(IReadOnlyCollection<TChild>? children)
    {
        if (children is null)
        {
            throw new ValidationException("复合操作子对象集合不能为空", ErrorCodes.ParameterInvalid);
        }

        if (children.Count > MaxCompositeChildGroups)
        {
            throw new ValidationException($"复合操作最多允许 {MaxCompositeChildGroups} 个子对象分组", ErrorCodes.ParameterInvalid);
        }
    }

    private static void EnsureCompositeChildModel(string modelCode, string foreignKeyField)
    {
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            throw new ValidationException("复合操作子对象模型编码不能为空", ErrorCodes.ParameterInvalid);
        }

        if (string.IsNullOrWhiteSpace(foreignKeyField))
        {
            throw new ValidationException("复合操作子对象外键字段不能为空", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task EnsureCompositeReadPermissionsAsync(
        IEnumerable<RuntimeCompositeChildDetailRequest> children,
        string? pageCode,
        string? previewPageId,
        CancellationToken cancellationToken)
    {
        foreach (var modelCode in children.Select(child => child.ModelCode).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await readPermissionService.EnsureAsync(modelCode, pageCode, previewPageId, cancellationToken);
        }
    }

    private async Task EnsureCompositeMutationPermissionsAsync(
        IEnumerable<string> modelCodes,
        string? pageCode,
        string? previewPageId,
        string action,
        CancellationToken cancellationToken)
    {
        foreach (var modelCode in modelCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await mutationPermissionService.EnsureAsync(modelCode, pageCode, previewPageId, action, cancellationToken);
        }
    }

    private async Task EnsureModelOperationChildMutationPermissionsAsync(
        string? operationType,
        IEnumerable<RuntimeModelCompositeChildDefinitionDto> children,
        string? pageCode,
        string? previewPageId,
        CancellationToken cancellationToken)
    {
        var childList = children
            .Where(child => !string.IsNullOrWhiteSpace(child.ModelCode))
            .ToArray();
        if (childList.Length == 0)
        {
            return;
        }

        if (string.Equals(operationType, "compositecreate", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureCompositeMutationPermissionsAsync(childList.Select(child => child.ModelCode), pageCode, previewPageId, "add", cancellationToken);
            return;
        }

        if (string.Equals(operationType, "compositeupdate", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureCompositeMutationPermissionsAsync(childList.Select(child => child.ModelCode), pageCode, previewPageId, "edit", cancellationToken);
            await EnsureCompositeMutationPermissionsAsync(
                childList
                    .Where(child => child.DeleteMissing || child.DeleteIdsExpression is not null)
                    .Select(child => child.ModelCode),
                pageCode,
                previewPageId,
                "delete",
                cancellationToken);
            return;
        }

        if (string.Equals(operationType, "compositedelete", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureCompositeMutationPermissionsAsync(childList.Select(child => child.ModelCode), pageCode, previewPageId, "delete", cancellationToken);
        }
    }
}
