using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Runtime;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowFormResourceAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IRuntimeDataModelService runtimeDataModelService,
    ApplicationDevelopmentSchemaValidator schemaValidator) : IWorkflowFormResourceAppService
{
    public async Task<GridPageResult<WorkflowFormResourceResponse>> GetPageAsync(
        GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(query.TenantId);
        var appCode = ResolveApp(query.AppCode);
        EnsureWorkspaceBoundary(tenantId, appCode);
        var resources = await LoadResourcesAsync(tenantId, appCode, query.Keyword, cancellationToken);
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        return new GridPageResult<WorkflowFormResourceResponse>
        {
            Total = resources.Count,
            Items = resources
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        };
    }

    public async Task<WorkflowFormResourceResponse?> ValidateBindingResourceAsync(
        WorkflowBindingUpsertRequest request,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        if (!HasStrongResourceRequest(request))
        {
            return null;
        }

        tenantId = tenantId.Trim();
        appCode = appCode.Trim().ToUpperInvariant();
        EnsureWorkspaceBoundary(tenantId, appCode);

        var resource = await FindResourceAsync(
            tenantId,
            appCode,
            request.FormResourceCode,
            request.MenuCode,
            request.PageCode,
            request.ModelCode,
            cancellationToken);

        if (resource is null)
        {
            throw new ValidationException("所选审批表单资源不存在、未发布或无权限访问", ErrorCodes.RuntimeDataModelNotFound);
        }

        EnsureMatches(request.MenuCode, resource.MenuCode, "菜单编码与审批表单资源不一致");
        EnsureMatches(request.BusinessType, resource.BusinessType, "业务类型与审批表单资源不一致");
        EnsureMatches(request.PageCode, resource.PageCode, "页面编码与审批表单资源不一致");
        EnsureMatches(request.ModelCode, resource.ModelCode, "模型编码与审批表单资源不一致");
        EnsureMatches(request.KeyField, resource.KeyField, "主键字段与审批表单资源不一致");

        if (resource.Fields.All(field => !string.Equals(field.FieldCode, resource.KeyField, StringComparison.OrdinalIgnoreCase) &&
                                         !string.Equals(field.Binding, resource.KeyField, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("审批表单资源缺少有效主键字段", ErrorCodes.RuntimeDataModelInvalid);
        }

        return resource;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetFieldLabelsForBindingAsync(
        string tenantId,
        string appCode,
        string menuCode,
        string businessType,
        CancellationToken cancellationToken = default)
    {
        var binding = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>()
            .FirstAsync(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.MenuCode == menuCode &&
                    item.BusinessType == businessType,
                cancellationToken);
        if (binding is null || string.IsNullOrWhiteSpace(binding.ModelCode))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var definition = await runtimeDataModelService.GetPublishedDefinitionAsync(binding.ModelCode, cancellationToken);
            return BuildFieldLabelMap(definition);
        }
        catch (Exception ex) when (ex is ValidationException or NotFoundException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<WorkflowFormResourceResponse?> FindResourceAsync(
        string tenantId,
        string appCode,
        string? resourceCode,
        string? menuCode,
        string? pageCode,
        string? modelCode,
        CancellationToken cancellationToken)
    {
        var resources = await LoadResourcesAsync(tenantId, appCode, null, cancellationToken);
        return resources.FirstOrDefault(item =>
            Matches(item.ResourceCode, resourceCode) ||
            (Matches(item.MenuCode, menuCode) &&
             Matches(item.PageCode, pageCode) &&
             Matches(item.ModelCode, modelCode)));
    }

    private async Task<List<WorkflowFormResourceResponse>> LoadResourcesAsync(
        string tenantId,
        string appCode,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var menus = await databaseAccessor.GetCurrentDb().Queryable<SystemMenuEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.Visible &&
                item.MenuType != "Button" &&
                item.PageCode != null)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        if (menus.Count == 0)
        {
            return [];
        }

        var pageCodes = menus
            .Select(item => item.PageCode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var pages = await databaseAccessor.GetCurrentDb().Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.Status == "Published" &&
                pageCodes.Contains(item.PageCode))
            .ToListAsync(cancellationToken);
        var pageLookup = pages
            .GroupBy(item => item.PageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime).First(),
                StringComparer.OrdinalIgnoreCase);

        var pageIds = pageLookup.Values.Select(item => item.Id).ToList();
        var documents = pageIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<ApplicationDesignerDocumentEntity>()
                .Where(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.Status == "Published" &&
                    item.PublishedArtifactId != null &&
                    item.PublishedArtifactId != "" &&
                    pageIds.Contains(item.PageId))
                .ToListAsync(cancellationToken);
        var documentLookup = documents
            .GroupBy(item => item.PageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime).First(),
                StringComparer.OrdinalIgnoreCase);

        var artifactIds = documents
            .Select(item => item.PublishedArtifactId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var artifacts = artifactIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<ApplicationDesignerRuntimeArtifactEntity>()
                .Where(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    artifactIds.Contains(item.Id) &&
                    item.Status == "Published")
                .ToListAsync(cancellationToken);
        var artifactLookup = artifacts.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        var models = await databaseAccessor.GetCurrentDb().Queryable<SystemDataModelEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.Status == "Published")
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        var modelLookup = models
            .GroupBy(item => item.ModelCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var resources = new List<WorkflowFormResourceResponse>();
        foreach (var menu in menus)
        {
            if (string.IsNullOrWhiteSpace(menu.PageCode) ||
                !pageLookup.TryGetValue(menu.PageCode, out var page) ||
                !documentLookup.TryGetValue(page.Id, out var document) ||
                string.IsNullOrWhiteSpace(document.PublishedArtifactId) ||
                !artifactLookup.TryGetValue(document.PublishedArtifactId, out var artifact))
            {
                continue;
            }

            JsonObject runtimeArtifact;
            try
            {
                runtimeArtifact = ValidatePublishedArtifact(page, document, artifact);
            }
            catch (ValidationException)
            {
                continue;
            }

            var modelCode = ReadModelCode(runtimeArtifact);
            if (string.IsNullOrWhiteSpace(modelCode) ||
                !modelLookup.TryGetValue(modelCode, out var model) ||
                !CanAccess(menu.PermissionCode) ||
                !CanAccess(PermissionCodes.BuildAppRuntimePagePermission(page.PageCode, "view")) ||
                !CanAccess(model.PermissionCode))
            {
                continue;
            }

            var definition = await TryGetDefinitionAsync(model.ModelCode, cancellationToken);
            if (definition is null)
            {
                continue;
            }

            resources.Add(MapResource(menu, page, model, definition));
        }

        return resources
            .Where(item => MatchesKeyword(item, keyword))
            .OrderBy(item => item.ResourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ResourceCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<RuntimeDataModelDefinition?> TryGetDefinitionAsync(
        string modelCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return await runtimeDataModelService.GetPublishedDefinitionAsync(modelCode, cancellationToken);
        }
        catch (Exception ex) when (ex is ValidationException or NotFoundException)
        {
            return null;
        }
    }

    private WorkflowFormResourceResponse MapResource(
        SystemMenuEntity menu,
        ApplicationDevelopmentPageEntity page,
        SystemDataModelEntity model,
        RuntimeDataModelDefinition definition)
    {
        return new WorkflowFormResourceResponse(
            BuildResourceCode(menu.MenuCode, page.PageCode, model.ModelCode),
            $"{menu.MenuName} / {page.PageName}",
            menu.MenuCode,
            model.ModelCode,
            menu.RoutePath,
            page.PageCode,
            model.ModelCode,
            definition.KeyField,
            FirstNonEmpty(model.PermissionCode, PermissionCodes.BuildAppRuntimePagePermission(page.PageCode, "view"), menu.PermissionCode),
            definition.Fields
                .OrderBy(item => item.Order)
                .Select(item => new WorkflowFormFieldResponse(
                    item.FieldCode,
                    string.IsNullOrWhiteSpace(item.FieldName) ? item.FieldCode : item.FieldName,
                    item.DataType,
                    item.Binding,
                    item.Visible,
                    item.Queryable,
                    item.Sortable,
                    item.Writable,
                    item.Renderer,
                    item.DictType,
                    item.Order))
                .ToList());
    }

    private bool CanAccess(string? permissionCode)
    {
        return string.IsNullOrWhiteSpace(permissionCode) ||
               currentUser.HasAsterErpPermission(permissionCode);
    }

    private void EnsureWorkspaceBoundary(string tenantId, string appCode)
    {
        if (!string.Equals(tenantId, currentUser.GetAsterErpTenantId(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(appCode, currentUser.GetAsterErpAppCode(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("当前用户无权访问请求的租户和应用工作区", ErrorCodes.PermissionDenied);
        }
    }

    private JsonObject ValidatePublishedArtifact(
        ApplicationDevelopmentPageEntity page,
        ApplicationDesignerDocumentEntity document,
        ApplicationDesignerRuntimeArtifactEntity artifact)
    {
        if (!string.Equals(page.TenantId, document.TenantId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(page.AppCode, document.AppCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(page.TenantId, artifact.TenantId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(page.AppCode, artifact.AppCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(document.PageId, page.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(document.PublishedArtifactId, artifact.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(artifact.DocumentId, document.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(page.Status, "Published", StringComparison.Ordinal) ||
            !string.Equals(document.Status, "Published", StringComparison.Ordinal) ||
            !string.Equals(artifact.Status, "Published", StringComparison.Ordinal))
        {
            throw InvalidArtifact("正式运行产物工作区边界或发布指针无效");
        }

        try
        {
            var runtimeArtifact = JsonNode.Parse(artifact.ArtifactJson) as JsonObject
                ?? throw InvalidArtifact("正式运行产物必须是 JSON 对象");
            RuntimeArtifactContractValidator.Validate(runtimeArtifact);
            var runtimeDocument = runtimeArtifact["document"] as JsonObject
                ?? throw InvalidArtifact("正式运行产物缺少 document");
            var validatedDocument = schemaValidator.ValidateRuntimeArtifact(
                runtimeDocument.ToJsonString(ApplicationDataCenterJson.Options));
            var artifactHash = RequireString(runtimeArtifact, "artifactHash");
            var actualArtifactHash = ApplicationDesignerCanonicalJson.ComputeRuntimeArtifactHash(validatedDocument);
            if (!string.Equals(artifactHash, actualArtifactHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(artifact.ArtifactHash, actualArtifactHash, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidArtifact("正式运行产物 hash 校验失败");
            }

            var manifestTypes = runtimeArtifact["manifestTypes"] as JsonArray
                ?? throw InvalidArtifact("正式运行产物缺少 manifestTypes");
            var manifest = runtimeArtifact["manifest"] as JsonArray
                ?? throw InvalidArtifact("正式运行产物缺少 manifest");
            var manifestJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(new JsonObject
            {
                ["types"] = manifestTypes.DeepClone(),
                ["declarations"] = manifest.DeepClone()
            }.ToJsonString());
            var manifestHash = ApplicationDesignerCanonicalJson.ComputeHash(manifestJson);
            if (!string.Equals(artifact.ManifestHash, manifestHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(artifact.ManifestJson), manifestJson, StringComparison.Ordinal))
            {
                throw InvalidArtifact("正式运行产物 manifest 校验失败");
            }

            var revision = runtimeArtifact["revision"]?.GetValue<int>() ?? 0;
            var compilerVersion = RequireString(runtimeArtifact, "compilerVersion");
            var signature = RequireString(runtimeArtifact, "signature");
            var documentId = RequireString(runtimeDocument, "documentId");
            var expectedSignature = ApplicationDesignerCanonicalJson.ComputeSignature(
                documentId,
                artifactHash,
                manifestHash,
                compilerVersion,
                revision.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
            if (!string.Equals(signature, expectedSignature, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(artifact.SignatureHash, expectedSignature, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidArtifact("正式运行产物 signature 校验失败");
            }

            var runtimeContext = runtimeDocument["runtimeContext"] as JsonObject
                ?? throw InvalidArtifact("正式运行产物缺少 runtimeContext");
            if (!string.Equals(RequireString(runtimeContext, "pageCode"), page.PageCode, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidArtifact("正式运行产物 pageCode 与正式页面不一致");
            }

            return runtimeArtifact;
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw InvalidArtifact($"正式运行产物 JSON 校验失败: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            throw InvalidArtifact($"正式运行产物字段校验失败: {exception.Message}");
        }
    }

    private static string? ReadModelCode(JsonObject artifact)
    {
        var document = artifact["document"] as JsonObject;
        var runtimeContext = document?["runtimeContext"] as JsonObject;
        var topLevel = ReadOptionalString(artifact, "modelCode");
        var context = ReadOptionalString(runtimeContext, "modelCode");
        if (!string.IsNullOrWhiteSpace(topLevel) &&
            !string.IsNullOrWhiteSpace(context) &&
            !string.Equals(topLevel, context, StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidArtifact("正式运行产物 modelCode 声明不一致");
        }

        return FirstNonEmpty(topLevel, context);
    }

    private static string RequireString(JsonObject source, string propertyName) =>
        ReadOptionalString(source, propertyName)
        ?? throw InvalidArtifact($"正式运行产物缺少 {propertyName}");

    private static string? ReadOptionalString(JsonObject? source, string propertyName) =>
        source?[propertyName] is JsonValue value && value.TryGetValue<string>(out var result) && !string.IsNullOrWhiteSpace(result)
            ? result.Trim()
            : null;

    private static ValidationException InvalidArtifact(string message) =>
        new(message, ErrorCodes.DesignerSchemaInvalid);

    private string ResolveTenant(string? requestedTenantId)
    {
        var tenantId = string.IsNullOrWhiteSpace(requestedTenantId)
            ? currentUser.GetAsterErpTenantId()
            : requestedTenantId.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ValidationException("租户不能为空", ErrorCodes.ParameterInvalid);
        }

        return tenantId;
    }

    private string ResolveApp(string? requestedAppCode)
    {
        var appCode = string.IsNullOrWhiteSpace(requestedAppCode)
            ? currentUser.GetAsterErpAppCode()
            : requestedAppCode.Trim();
        if (string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("应用不能为空", ErrorCodes.ParameterInvalid);
        }

        return appCode.ToUpperInvariant();
    }

    private static IReadOnlyDictionary<string, string> BuildFieldLabelMap(RuntimeDataModelDefinition definition)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.Fields)
        {
            var label = string.IsNullOrWhiteSpace(field.FieldName) ? field.FieldCode : field.FieldName;
            AddLabel(labels, field.FieldCode, label);
            AddLabel(labels, field.Binding, label);
        }

        return labels;
    }

    private static void AddLabel(IDictionary<string, string> labels, string? key, string label)
    {
        if (!string.IsNullOrWhiteSpace(key) && !labels.ContainsKey(key.Trim()))
        {
            labels[key.Trim()] = label;
        }
    }

    private static bool HasStrongResourceRequest(WorkflowBindingUpsertRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.FormResourceCode) ||
               !string.IsNullOrWhiteSpace(request.PageCode) ||
               !string.IsNullOrWhiteSpace(request.ModelCode) ||
               !string.IsNullOrWhiteSpace(request.KeyField);
    }

    private static bool MatchesKeyword(WorkflowFormResourceResponse resource, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var value = keyword.Trim();
        return Contains(resource.ResourceName, value) ||
               Contains(resource.ResourceCode, value) ||
               Contains(resource.MenuCode, value) ||
               Contains(resource.PageCode, value) ||
               Contains(resource.ModelCode, value) ||
               Contains(resource.BusinessType, value);
    }

    private static bool Matches(string actual, string? expected)
    {
        return !string.IsNullOrWhiteSpace(expected) &&
               string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureMatches(string? requested, string actual, string message)
    {
        if (!string.IsNullOrWhiteSpace(requested) &&
            !string.Equals(requested.Trim(), actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }
    }

    private static bool Contains(string? source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildResourceCode(string menuCode, string pageCode, string modelCode)
    {
        return $"{menuCode}::{pageCode}::{modelCode}";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}

