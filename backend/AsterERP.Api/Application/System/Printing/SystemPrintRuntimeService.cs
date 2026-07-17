using System.Text.Json.Nodes;
using AsterERP.Contracts.System.Printing;
using AsterERP.Api.Infrastructure.Security;

namespace AsterERP.Api.Application.System.Printing;

public sealed class SystemPrintRuntimeService(
    ICurrentUser currentUser,
    PrintWorkspaceResolver workspaceResolver,
    PrintTargetCatalog targetCatalog,
    SystemPrintTemplateService templateService,
    QueryViewListPrintDataProvider queryViewListPrintDataProvider,
    PrintDataProviderRegistry providerRegistry)
{
    public async Task<PrintTemplateResolveResponse> ResolveRuntimeAsync(
        PrintTemplateResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var definition = targetCatalog.GetRequiredDefinition(request.MenuCode);
        var sceneDefinition = targetCatalog.GetRequiredScene(definition, request.Scene);
        var targetDetail = await targetCatalog.GetTargetDetailAsync(definition.MenuCode, sceneDefinition.Scene, cancellationToken);
        var template = await templateService.GetRuntimeTemplateAsync(definition.MenuCode, sceneDefinition.Scene, request.TemplateId, cancellationToken);
        var templateData = PrintJsonNodeMapper.Deserialize(template.Status == "Published" && !string.IsNullOrWhiteSpace(template.PublishedDataJson)
            ? template.PublishedDataJson
            : template.DraftDataJson);

        JsonObject variables;
        if (string.Equals(sceneDefinition.Scene, "list", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(sceneDefinition.ListViewCode))
            {
                throw new InvalidOperationException($"打印目标 {definition.MenuCode} 未配置列表视图。");
            }

            var (rows, total) = await queryViewListPrintDataProvider.QueryAsync(sceneDefinition.ListViewCode, request, cancellationToken);
            variables = BuildListVariables(scope, targetDetail, request, rows, total);
        }
        else
        {
            var detailId = string.IsNullOrWhiteSpace(request.DetailId)
                ? request.SelectedIds.FirstOrDefault()
                : request.DetailId;
            if (string.IsNullOrWhiteSpace(detailId))
            {
                throw new InvalidOperationException("详情打印必须提供 detailId 或 selectedIds。");
            }

            if (string.IsNullOrWhiteSpace(sceneDefinition.DetailProviderKey))
            {
                throw new InvalidOperationException($"打印目标 {definition.MenuCode} 未配置详情提供器。");
            }

            var detail = await providerRegistry.GetRequired(sceneDefinition.DetailProviderKey).GetDetailAsync(detailId.Trim(), cancellationToken);
            variables = BuildDetailVariables(scope, targetDetail, detail);
        }

        return new PrintTemplateResolveResponse(
            template.Id,
            template.Name,
            template.TemplateCode,
            sceneDefinition.Scene,
            BuildSuggestedFileName(targetDetail.MenuName, template.Name, sceneDefinition.Scene),
            templateData,
            targetDetail.TestData,
            variables,
            targetDetail.SupportsAssets,
            targetDetail.AvailableVariables);
    }

    private JsonObject BuildListVariables(
        PrintWorkspaceScope scope,
        PrintTargetDetailResponse targetDetail,
        PrintTemplateResolveRequest request,
        JsonArray rows,
        long total)
    {
        return new JsonObject
        {
            ["meta"] = BuildMeta(scope, targetDetail, "list"),
            ["summary"] = new JsonObject
            {
                ["mode"] = string.IsNullOrWhiteSpace(request.Mode) ? "currentPage" : request.Mode.Trim(),
                ["total"] = total,
                ["selectedCount"] = request.SelectedIds.Count,
                ["pageIndex"] = request.PageIndex <= 0 ? 1 : request.PageIndex,
                ["pageSize"] = request.PageSize <= 0 ? 20 : request.PageSize,
                ["returnedCount"] = rows.Count
            },
            ["rows"] = rows
        };
    }

    private JsonObject BuildDetailVariables(
        PrintWorkspaceScope scope,
        PrintTargetDetailResponse targetDetail,
        JsonObject detail)
    {
        return new JsonObject
        {
            ["meta"] = BuildMeta(scope, targetDetail, "detail"),
            ["detail"] = detail
        };
    }

    private JsonObject BuildMeta(PrintWorkspaceScope scope, PrintTargetDetailResponse targetDetail, string scene)
    {
        return new JsonObject
        {
            ["menuCode"] = targetDetail.MenuCode,
            ["menuName"] = targetDetail.MenuName,
            ["routePath"] = targetDetail.RoutePath,
            ["scene"] = scene,
            ["printedAt"] = DateTime.UtcNow.ToString("O"),
            ["currentUser"] = new JsonObject
            {
                ["userId"] = scope.UserId,
                ["userName"] = scope.UserName,
                ["tenantId"] = scope.TenantId,
                ["appCode"] = scope.AppCode,
                ["isPlatformAdmin"] = currentUser.IsAsterErpPlatformAdmin(),
                ["isTenantAdmin"] = currentUser.IsAsterErpTenantAdmin()
            },
            ["workspace"] = new JsonObject
            {
                ["tenantId"] = scope.TenantId,
                ["appCode"] = scope.AppCode
            }
        };
    }

    private static string BuildSuggestedFileName(string menuName, string templateName, string scene)
    {
        var normalized = new string($"{menuName}-{templateName}-{scene}"
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? $"print-{scene}" : normalized;
    }
}
