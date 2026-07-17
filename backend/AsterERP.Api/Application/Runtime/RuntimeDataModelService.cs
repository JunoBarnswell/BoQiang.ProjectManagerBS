using System.Text.Json;
using System.Globalization;
using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using System.Text.RegularExpressions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Runtime;
using AsterERP.Api.Modules.Runtime;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimeDataModelService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IDataModelProviderRegistry providerRegistry,
    IRuntimeGridViewService runtimeGridViewService,
    RuntimeValueExpressionEvaluator expressionEvaluator,
    ILogger<RuntimeDataModelService> logger) : IRuntimeDataModelService
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private const int MaxSchemaJsonLength = 262144;
    private static readonly JsonSerializerOptions SchemaJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "contains",
        "equals",
        "notEquals",
        "startsWith",
        "endsWith",
        "gt",
        "gte",
        "lt",
        "lte",
        "between"
    };
    private static readonly Regex TemplateTokenRegex = new(@"\{(?<field>[A-Za-z][A-Za-z0-9_.:-]*)\}", RegexOptions.Compiled);

    public Task<RuntimeDataModelDefinition> GetPublishedDefinitionAsync(
        string modelCode,
        CancellationToken cancellationToken = default)
    {
        return GetPublishedModelAsync(modelCode, cancellationToken);
    }

    public async Task<RuntimeQueryResponse> QueryAsync(
        string modelCode,
        RuntimeQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime model query requested. ModelCode={ModelCode} PageIndex={PageIndex} PageSize={PageSize} FilterCount={FilterCount}",
            modelCode,
            request.PageIndex,
            request.PageSize,
            request.Filters?.Count ?? 0);
        try
        {
        var model = await GetPublishedModelAsync(
            modelCode,
            ShouldBypassModelPermissionForPreview(request),
            cancellationToken);
        var query = BuildValidatedQuery(model, request);
        logger.LogDebug(
            "Runtime model query executing. ModelCode={ModelCode} ProviderKey={ProviderKey} PageIndex={PageIndex} PageSize={PageSize} FilterCount={FilterCount}",
            model.ModelCode,
            model.ProviderKey,
            query.PageIndex,
            query.PageSize,
            query.Filters.Count);
        var provider = providerRegistry.GetRequired(model.ProviderKey);
        var result = await provider.QueryAsync(model, query, cancellationToken);
        var sourceRows = result.Rows
            .Select(row => ApplyDisplayHelpers(model, EnsureRuntimeKey(model, row)))
            .ToList();
        var fields = model.ToFieldResponses();
        var (rows, cellSpans) = string.IsNullOrWhiteSpace(request.PageCode)
            ? (sourceRows, (IReadOnlyList<RuntimeCellSpanResponse>?)null)
            : await ProjectRowsAsync(request.PageCode, request.PreviewPageId, model, sourceRows, cancellationToken);

        logger.LogInformation(
            "Runtime model query completed. ModelCode={ModelCode} ProviderKey={ProviderKey} RowCount={RowCount} Total={Total} ElapsedMs={ElapsedMs}",
            model.ModelCode,
            model.ProviderKey,
            rows.Count,
            result.Total,
            elapsed.ElapsedMilliseconds);

        return new RuntimeQueryResponse(
            fields,
            rows,
            result.Total,
            query.PageIndex,
            query.PageSize,
            cellSpans,
            model.KeyField);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime model query rejected. ModelCode={ModelCode} PageIndex={PageIndex} PageSize={PageSize} ElapsedMs={ElapsedMs}",
                modelCode,
                request.PageIndex,
                request.PageSize,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime model query failed. ModelCode={ModelCode} PageIndex={PageIndex} PageSize={PageSize} ElapsedMs={ElapsedMs}",
                modelCode,
                request.PageIndex,
                request.PageSize,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RuntimeDetailResponse> GetDetailAsync(
        string modelCode,
        string id,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime model detail requested. ModelCode={ModelCode} DataId={DataId}",
            modelCode,
            id);
        try
        {
        var model = await GetPublishedModelAsync(modelCode, cancellationToken);
        var normalizedId = NormalizeDataId(id);

        var provider = providerRegistry.GetRequired(model.ProviderKey);
        var row = await provider.GetDetailAsync(model, normalizedId, cancellationToken)
            ?? throw new NotFoundException("运行时数据不存在", ErrorCodes.RuntimeDataModelNotFound);

        logger.LogDebug(
            "Runtime model detail loaded. ModelCode={ModelCode} ProviderKey={ProviderKey} DataId={DataId} ElapsedMs={ElapsedMs}",
            model.ModelCode,
            model.ProviderKey,
            normalizedId,
            elapsed.ElapsedMilliseconds);
        return new RuntimeDetailResponse(model.ToFieldResponses(), EnsureRuntimeKey(model, row));
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime model detail rejected. ModelCode={ModelCode} DataId={DataId} ElapsedMs={ElapsedMs}",
                modelCode,
                id,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime model detail failed. ModelCode={ModelCode} DataId={DataId} ElapsedMs={ElapsedMs}",
                modelCode,
                id,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RuntimeCompositeDetailResponse> GetCompositeDetailAsync(
        RuntimeCompositeDetailRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime composite detail requested. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount}",
            request.RootModelCode,
            request.RootId,
            request.Children?.Count ?? 0);
        try
        {
        var rootModelCode = NormalizeCode(request.RootModelCode, "主对象模型编码");
        var rootId = NormalizeDataId(request.RootId);
        var root = await GetDetailAsync(rootModelCode, rootId, cancellationToken);
        var children = new List<RuntimeCompositeChildDetailResponse>();

        foreach (var child in request.Children ?? [])
        {
            var childModelCode = NormalizeCode(child.ModelCode, "子对象模型编码");
            var foreignKeyField = NormalizeCode(child.ForeignKeyField, "子对象外键字段");
            var parentKey = ResolveParentKey(root.Row, rootId, child.ParentKeyField ?? string.Empty);
            var childQuery = BuildCompositeChildDetailQuery(child.Query, foreignKeyField, parentKey, request);
            var childData = await QueryAsync(childModelCode, childQuery, cancellationToken);
            children.Add(new RuntimeCompositeChildDetailResponse(
                childModelCode,
                child.BindingKey,
                child.ParentKeyField,
                foreignKeyField,
                childData));
        }

        logger.LogInformation(
            "Runtime composite detail loaded. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
            rootModelCode,
            rootId,
            children.Count,
            elapsed.ElapsedMilliseconds);

        return new RuntimeCompositeDetailResponse(root, children);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime composite detail rejected. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
                request.RootModelCode,
                request.RootId,
                request.Children?.Count ?? 0,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime composite detail failed. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
                request.RootModelCode,
                request.RootId,
                request.Children?.Count ?? 0,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RuntimeModelOperationResponse> ExecuteOperationAsync(
        string modelCode,
        RuntimeModelOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime model operation executing. ModelCode={ModelCode} OperationCode={OperationCode} VariableCount={VariableCount}",
            modelCode,
            request.OperationCode,
            request.Variables?.Count ?? 0);
        try
        {
        var model = await GetPublishedModelAsync(modelCode, cancellationToken);
        var operation = model.Operations?.FirstOrDefault(item =>
            string.Equals(item.OperationCode, request.OperationCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException($"模型操作不存在: {request.OperationCode}", ErrorCodes.RuntimeDataModelInvalid);
        var context = BuildOperationContext(request.Variables ?? new Dictionary<string, object?>());
        var operationModelCode = string.IsNullOrWhiteSpace(operation.ModelCode) ? model.ModelCode : operation.ModelCode.Trim();
        var operationType = operation.OperationType.Trim().ToLowerInvariant();
        object? result = operationType switch
        {
            "query" => await QueryAsync(operationModelCode, BuildOperationQuery(operation, request, context), cancellationToken),
            "create" => await CreateAsync(operationModelCode, BuildOperationValues(operation, context), cancellationToken),
            "update" => await ExecuteUpdateOperationAsync(operationModelCode, operation, context, cancellationToken),
            "delete" => await DeleteAsync(operationModelCode, ResolveOperationId(operation, context), cancellationToken),
            "compositecreate" => await CreateCompositeAsync(BuildCompositeCreateOperationRequest(operationModelCode, operation, request, context), cancellationToken),
            "compositeupdate" => await UpdateCompositeAsync(BuildCompositeUpdateOperationRequest(operationModelCode, operation, request, context), cancellationToken),
            "compositedelete" => await DeleteCompositeAsync(BuildCompositeDeleteOperationRequest(operationModelCode, operation, request, context), cancellationToken),
            _ => throw new ValidationException($"模型操作类型不支持: {operation.OperationType}", ErrorCodes.RuntimeDataModelInvalid)
        };

        logger.LogInformation(
            "Runtime model operation completed. ModelCode={ModelCode} OperationCode={OperationCode} OperationType={OperationType} ElapsedMs={ElapsedMs}",
            model.ModelCode,
            operation.OperationCode,
            operation.OperationType,
            elapsed.ElapsedMilliseconds);
        return new RuntimeModelOperationResponse(
            operation.OperationCode,
            operation.OperationType,
            result,
            request.Variables ?? new Dictionary<string, object?>());
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime model operation rejected. ModelCode={ModelCode} OperationCode={OperationCode} ElapsedMs={ElapsedMs}",
                modelCode,
                request.OperationCode,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime model operation failed. ModelCode={ModelCode} OperationCode={OperationCode} ElapsedMs={ElapsedMs}",
                modelCode,
                request.OperationCode,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RuntimeCreateResponse> CreateAsync(
        string modelCode,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime model create executing. ModelCode={ModelCode} FieldCount={FieldCount}",
            modelCode,
            values.Count);
        try
        {
        if (values.Count == 0)
        {
            throw new ValidationException("创建字段不能为空", ErrorCodes.ParameterInvalid);
        }

        var model = await GetPublishedModelAsync(modelCode, cancellationToken);
        var validatedValues = BuildValidatedCreateValues(model, values);
        var provider = providerRegistry.GetRequired(model.ProviderKey);
        var row = await provider.CreateAsync(model, validatedValues, cancellationToken);
        if (row is null)
        {
            throw new ValidationException("运行时数据模型不支持创建", ErrorCodes.RuntimeFieldNotAllowed);
        }

        var id = ResolveCreatedId(model, row);
        logger.LogInformation(
            "Runtime model create completed. ModelCode={ModelCode} ProviderKey={ProviderKey} DataId={DataId} FieldCount={FieldCount} ElapsedMs={ElapsedMs}",
            model.ModelCode,
            model.ProviderKey,
            id,
            validatedValues.Count,
            elapsed.ElapsedMilliseconds);
        return new RuntimeCreateResponse(id, row);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime model create rejected. ModelCode={ModelCode} FieldCount={FieldCount} ElapsedMs={ElapsedMs}",
                modelCode,
                values.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime model create failed. ModelCode={ModelCode} FieldCount={FieldCount} ElapsedMs={ElapsedMs}",
                modelCode,
                values.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task UpdateFieldsAsync(
        string modelCode,
        string id,
        IReadOnlyDictionary<string, object?> updates,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime model update executing. ModelCode={ModelCode} DataId={DataId} FieldCount={FieldCount}",
            modelCode,
            id,
            updates.Count);
        try
        {
        if (updates.Count == 0)
        {
            throw new ValidationException("更新字段不能为空", ErrorCodes.ParameterInvalid);
        }

        var model = await GetPublishedModelAsync(modelCode, cancellationToken);
        var normalizedId = NormalizeDataId(id);
        var validatedUpdates = BuildValidatedUpdates(model, updates);
        var provider = providerRegistry.GetRequired(model.ProviderKey);
        var updated = await provider.UpdateFieldsAsync(model, normalizedId, validatedUpdates, cancellationToken);
        if (!updated)
        {
            throw new NotFoundException("运行时数据不存在或字段不支持更新", ErrorCodes.RuntimeDataModelNotFound);
        }

        logger.LogInformation(
            "Runtime model update completed. ModelCode={ModelCode} ProviderKey={ProviderKey} DataId={DataId} FieldCount={FieldCount} ElapsedMs={ElapsedMs}",
            model.ModelCode,
            model.ProviderKey,
            normalizedId,
            validatedUpdates.Count,
            elapsed.ElapsedMilliseconds);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime model update rejected. ModelCode={ModelCode} DataId={DataId} FieldCount={FieldCount} ElapsedMs={ElapsedMs}",
                modelCode,
                id,
                updates.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime model update failed. ModelCode={ModelCode} DataId={DataId} FieldCount={FieldCount} ElapsedMs={ElapsedMs}",
                modelCode,
                id,
                updates.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RuntimeDeleteResponse> DeleteAsync(
        string modelCode,
        string id,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime model delete executing. ModelCode={ModelCode} DataId={DataId}",
            modelCode,
            id);
        try
        {
        var model = await GetPublishedModelAsync(modelCode, cancellationToken);
        var normalizedId = NormalizeDataId(id);
        var provider = providerRegistry.GetRequired(model.ProviderKey);
        var deleted = await provider.DeleteAsync(model, normalizedId, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("运行时数据不存在或不支持删除", ErrorCodes.RuntimeDataModelNotFound);
        }

        logger.LogInformation(
            "Runtime model delete completed. ModelCode={ModelCode} ProviderKey={ProviderKey} DataId={DataId} ElapsedMs={ElapsedMs}",
            model.ModelCode,
            model.ProviderKey,
            normalizedId,
            elapsed.ElapsedMilliseconds);
        return new RuntimeDeleteResponse(normalizedId, true);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime model delete rejected. ModelCode={ModelCode} DataId={DataId} ElapsedMs={ElapsedMs}",
                modelCode,
                id,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime model delete failed. ModelCode={ModelCode} DataId={DataId} ElapsedMs={ElapsedMs}",
                modelCode,
                id,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RuntimeCompositeCreateResponse> CreateCompositeAsync(
        RuntimeCompositeCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime composite create executing. RootModelCode={RootModelCode} RootFieldCount={RootFieldCount} ChildGroupCount={ChildGroupCount}",
            request.RootModelCode,
            request.RootValues.Count,
            request.Children.Count);
        try
        {
        if (request.RootValues.Count == 0)
        {
            throw new ValidationException("主对象创建字段不能为空", ErrorCodes.ParameterInvalid);
        }

        var rootModelCode = NormalizeCode(request.RootModelCode, "主对象模型编码");
        var execution = await ResolveCompositeExecutionAsync(
            rootModelCode,
            request.Children.Select(child => NormalizeCode(child.ModelCode, "子对象模型编码")).ToArray(),
            cancellationToken);

        var response = await execution.Provider.ExecuteInTransactionAsync(execution.Models, async () =>
        {
            var root = await CreateAsync(rootModelCode, request.RootValues, cancellationToken);
            var children = new List<RuntimeCompositeChildCreateResponse>();
            foreach (var child in request.Children)
            {
                var childModelCode = NormalizeCode(child.ModelCode, "子对象模型编码");
                var foreignKeyField = NormalizeCode(child.ForeignKeyField, "子对象外键字段");
                var parentKey = ResolveParentKey(root, child.ParentKeyField);
                var createdRows = new List<RuntimeCreateResponse>();
                foreach (var row in child.Rows)
                {
                    var values = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)
                    {
                        [foreignKeyField] = parentKey
                    };
                    var created = await CreateAsync(childModelCode, values, cancellationToken);
                    createdRows.Add(created);
                }

                children.Add(new RuntimeCompositeChildCreateResponse(childModelCode, createdRows));
            }

            return new RuntimeCompositeCreateResponse(root, children);
        }, cancellationToken);
        logger.LogInformation(
            "Runtime composite create completed. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
            rootModelCode,
            response.Root.Id,
            response.Children.Count,
            elapsed.ElapsedMilliseconds);
        return response;
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime composite create rejected. RootModelCode={RootModelCode} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
                request.RootModelCode,
                request.Children.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime composite create failed. RootModelCode={RootModelCode} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
                request.RootModelCode,
                request.Children.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RuntimeCompositeDeleteResponse> DeleteCompositeAsync(
        RuntimeCompositeDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug(
            "Runtime composite delete executing. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount}",
            request.RootModelCode,
            request.RootId,
            request.Children.Count);
        try
        {
        var rootModelCode = NormalizeCode(request.RootModelCode, "主对象模型编码");
        var rootId = NormalizeDataId(request.RootId);
        var execution = await ResolveCompositeExecutionAsync(
            rootModelCode,
            request.Children.Select(child => NormalizeCode(child.ModelCode, "子对象模型编码")).ToArray(),
            cancellationToken);

        var response = await execution.Provider.ExecuteInTransactionAsync(execution.Models, async () =>
        {
            var rootDetail = await GetDetailAsync(rootModelCode, rootId, cancellationToken);
            var children = new List<RuntimeCompositeChildDeleteResponse>();
            foreach (var child in request.Children)
            {
                var childModelCode = NormalizeCode(child.ModelCode, "子对象模型编码");
                var foreignKeyField = NormalizeCode(child.ForeignKeyField, "子对象外键字段");
                var parentKey = ResolveDeleteParentKey(rootDetail.Row, rootId, child.ParentId, child.ParentKeyField);
                var deleted = await DeleteCompositeChildrenAsync(
                    childModelCode,
                    foreignKeyField,
                    parentKey,
                    child.Required,
                    cancellationToken);

                children.Add(new RuntimeCompositeChildDeleteResponse(childModelCode, deleted));
            }

            var root = await DeleteAsync(rootModelCode, rootId, cancellationToken);
            return new RuntimeCompositeDeleteResponse(root, children);
        }, cancellationToken);
        logger.LogInformation(
            "Runtime composite delete completed. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount} DeletedChildCount={DeletedChildCount} ElapsedMs={ElapsedMs}",
            rootModelCode,
            rootId,
            response.Children.Count,
            response.Children.Sum(child => child.DeletedCount),
            elapsed.ElapsedMilliseconds);
        return response;
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime composite delete rejected. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
                request.RootModelCode,
                request.RootId,
                request.Children.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime composite delete failed. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
                request.RootModelCode,
                request.RootId,
                request.Children.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<RuntimeCompositeUpdateResponse> UpdateCompositeAsync(
        RuntimeCompositeUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        var rootValues = request.RootValues ?? new Dictionary<string, object?>();
        var childRequests = request.Children ?? [];
        logger.LogDebug(
            "Runtime composite update executing. RootModelCode={RootModelCode} RootId={RootId} RootFieldCount={RootFieldCount} ChildGroupCount={ChildGroupCount}",
            request.RootModelCode,
            request.RootId,
            rootValues.Count,
            childRequests.Count);
        try
        {
        if (rootValues.Count == 0 && childRequests.Count == 0)
        {
            throw new ValidationException("复合更新必须提供主对象字段或子对象变更", ErrorCodes.ParameterInvalid);
        }

        var rootModelCode = NormalizeCode(request.RootModelCode, "主对象模型编码");
        var rootId = NormalizeDataId(request.RootId);
        var execution = await ResolveCompositeExecutionAsync(
            rootModelCode,
            childRequests.Select(child => NormalizeCode(child.ModelCode, "子对象模型编码")).ToArray(),
            cancellationToken);

        var response = await execution.Provider.ExecuteInTransactionAsync(execution.Models, async () =>
        {
            if (rootValues.Count > 0)
            {
                await UpdateFieldsAsync(rootModelCode, rootId, rootValues, cancellationToken);
            }
            else
            {
                await GetDetailAsync(rootModelCode, rootId, cancellationToken);
            }

            var rootDetail = await GetDetailAsync(rootModelCode, rootId, cancellationToken);
            var root = new RuntimeMutationResponse(rootId, rootDetail.Row, true);
            var children = new List<RuntimeCompositeChildUpdateResponse>();
            foreach (var child in childRequests)
            {
                var childModelCode = NormalizeCode(child.ModelCode, "子对象模型编码");
                var foreignKeyField = NormalizeCode(child.ForeignKeyField, "子对象外键字段");
                var parentKey = ResolveParentKey(rootDetail.Row, rootId, child.ParentKeyField);
                var result = await UpdateCompositeChildrenAsync(
                    childModelCode,
                    foreignKeyField,
                    parentKey,
                    child,
                    cancellationToken);
                children.Add(result);
            }

            return new RuntimeCompositeUpdateResponse(root, children);
        }, cancellationToken);
        logger.LogInformation(
            "Runtime composite update completed. RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
            rootModelCode,
            rootId,
            response.Children.Count,
            elapsed.ElapsedMilliseconds);
        return response;
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Runtime composite update rejected. RootModelCode={RootModelCode} RootId={RootId} RootFieldCount={RootFieldCount} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
                request.RootModelCode,
                request.RootId,
                rootValues.Count,
                childRequests.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Runtime composite update failed. RootModelCode={RootModelCode} RootId={RootId} RootFieldCount={RootFieldCount} ChildGroupCount={ChildGroupCount} ElapsedMs={ElapsedMs}",
                request.RootModelCode,
                request.RootId,
                rootValues.Count,
                childRequests.Count,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task<RuntimeCompositeChildUpdateResponse> UpdateCompositeChildrenAsync(
        string childModelCode,
        string foreignKeyField,
        object parentKey,
        RuntimeCompositeChildUpdateRequest child,
        CancellationToken cancellationToken)
    {
        var existingIds = child.DeleteMissing
            ? await QueryCompositeChildIdsAsync(childModelCode, foreignKeyField, parentKey, cancellationToken)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var retainedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deletedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var createdRows = new List<RuntimeCreateResponse>();
        var updatedRows = new List<RuntimeMutationResponse>();
        var deletedCount = 0;
        var childModel = await GetPublishedModelAsync(childModelCode, cancellationToken);

        foreach (var row in child.Rows ?? [])
        {
            var values = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
            var rowId = ResolveCompositeRowId(values, childModel.KeyField);
            if (string.IsNullOrWhiteSpace(rowId))
            {
                RemoveRuntimeKeyValues(values, childModel.KeyField);
                values[foreignKeyField] = parentKey;
                createdRows.Add(await CreateAsync(childModelCode, values, cancellationToken));
                continue;
            }

            retainedIds.Add(rowId);
            await EnsureCompositeChildBelongsToParentAsync(
                childModelCode,
                rowId,
                foreignKeyField,
                parentKey,
                cancellationToken);
            RemoveRuntimeKeyValues(values, childModel.KeyField);
            if (values.Count > 0)
            {
                await UpdateFieldsAsync(childModelCode, rowId, values, cancellationToken);
            }

            var detail = await GetDetailAsync(childModelCode, rowId, cancellationToken);
            updatedRows.Add(new RuntimeMutationResponse(rowId, detail.Row, true));
        }

        foreach (var deleteId in child.DeleteIds ?? [])
        {
            var normalizedDeleteId = NormalizeDataId(deleteId);
            if (retainedIds.Contains(normalizedDeleteId))
            {
                continue;
            }

            await EnsureCompositeChildBelongsToParentAsync(
                childModelCode,
                normalizedDeleteId,
                foreignKeyField,
                parentKey,
                cancellationToken);
            await DeleteAsync(childModelCode, normalizedDeleteId, cancellationToken);
            deletedIds.Add(normalizedDeleteId);
            deletedCount += 1;
        }

        if (child.DeleteMissing)
        {
            foreach (var existingId in existingIds)
            {
                if (retainedIds.Contains(existingId) || deletedIds.Contains(existingId))
                {
                    continue;
                }

                await DeleteAsync(childModelCode, existingId, cancellationToken);
                deletedCount += 1;
            }
        }

        return new RuntimeCompositeChildUpdateResponse(childModelCode, createdRows, updatedRows, deletedCount);
    }

    private static RuntimeQueryRequest BuildCompositeChildDetailQuery(
        RuntimeQueryRequest? request,
        string foreignKeyField,
        object parentKey,
        RuntimeCompositeDetailRequest compositeRequest)
    {
        var filters = (request?.Filters ?? [])
            .Concat([new RuntimeFilterRequest(foreignKeyField, "equals", parentKey.ToString(), null)])
            .ToArray();
        var pageIndex = request?.PageIndex > 0 ? request.PageIndex : DefaultPageIndex;
        var pageSize = request?.PageSize > 0 ? Math.Min(request.PageSize, MaxPageSize) : MaxPageSize;

        return new RuntimeQueryRequest(
            pageIndex,
            pageSize,
            request?.Keyword,
            filters,
            request?.Sorts ?? [],
            string.IsNullOrWhiteSpace(request?.PageCode) ? compositeRequest.PageCode : request.PageCode,
            string.IsNullOrWhiteSpace(request?.PreviewPageId) ? compositeRequest.PreviewPageId : request.PreviewPageId);
    }

    private async Task EnsureCompositeChildBelongsToParentAsync(
        string childModelCode,
        string childId,
        string foreignKeyField,
        object parentKey,
        CancellationToken cancellationToken)
    {
        var detail = await GetDetailAsync(childModelCode, childId, cancellationToken);
        var childParentKey = ReadRowValue(detail.Row, foreignKeyField);
        if (!RuntimeValuesEqual(childParentKey, parentKey))
        {
            throw new ValidationException($"子对象 {childModelCode} 不属于当前主对象，拒绝一对多同步", ErrorCodes.PermissionDenied);
        }
    }

    private static void RemoveRuntimeKeyValues(IDictionary<string, object?> values, string keyField)
    {
        values.Remove("__runtimeKey");
        values.Remove("id");
        values.Remove("ID");
        if (!string.IsNullOrWhiteSpace(keyField))
        {
            values.Remove(keyField);
        }
    }

    private async Task<HashSet<string>> QueryCompositeChildIdsAsync(
        string childModelCode,
        string foreignKeyField,
        object parentKey,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pageIndex = DefaultPageIndex;
        while (true)
        {
            var query = new RuntimeQueryRequest(
                pageIndex,
                MaxPageSize,
                null,
                [new RuntimeFilterRequest(foreignKeyField, "equals", parentKey.ToString(), null)],
                []);
            var rows = await QueryAsync(childModelCode, query, cancellationToken);
            foreach (var row in rows.Rows)
            {
                var childId = ResolveDeleteChildId(row);
                if (!string.IsNullOrWhiteSpace(childId))
                {
                    ids.Add(childId);
                }
            }

            if (rows.Rows.Count < MaxPageSize)
            {
                return ids;
            }

            pageIndex += 1;
        }
    }

    private async Task<int> DeleteCompositeChildrenAsync(
        string childModelCode,
        string foreignKeyField,
        object parentKey,
        bool required,
        CancellationToken cancellationToken)
    {
        var deleted = 0;
        while (true)
        {
            var query = new RuntimeQueryRequest(
                DefaultPageIndex,
                MaxPageSize,
                null,
                [new RuntimeFilterRequest(foreignKeyField, "equals", parentKey.ToString(), null)],
                []);
            var rows = await QueryAsync(childModelCode, query, cancellationToken);
            if (rows.Rows.Count == 0)
            {
                return deleted;
            }

            var deletedInPass = 0;
            foreach (var row in rows.Rows)
            {
                var childId = ResolveDeleteChildId(row);
                if (childId is null)
                {
                    if (required)
                    {
                        throw new ValidationException($"子对象 {childModelCode} 缺少主键，无法级联删除", ErrorCodes.ParameterInvalid);
                    }

                    continue;
                }

                await DeleteAsync(childModelCode, childId, cancellationToken);
                deleted += 1;
                deletedInPass += 1;
            }

            if (deletedInPass == 0)
            {
                return deleted;
            }
        }
    }

    private static object ResolveParentKey(RuntimeCreateResponse root, string parentKeyField)
    {
        var keyField = string.IsNullOrWhiteSpace(parentKeyField) ? "id" : parentKeyField.Trim();
        var value =
            ReadRowValue(root.Row, keyField) ??
            ReadRowValue(root.Row, "__runtimeKey") ??
            ReadRowValue(root.Row, "id") ??
            root.Id;
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            throw new ValidationException("主对象创建后无法解析主键，不能保存子对象", ErrorCodes.ParameterInvalid);
        }

        return value;
    }

    private static object ResolveParentKey(
        IReadOnlyDictionary<string, object?> rootRow,
        string rootId,
        string parentKeyField)
    {
        var keyField = string.IsNullOrWhiteSpace(parentKeyField) ? "id" : parentKeyField.Trim();
        var value =
            ReadRowValue(rootRow, keyField) ??
            ReadRowValue(rootRow, "__runtimeKey") ??
            ReadRowValue(rootRow, "id") ??
            rootId;
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            throw new ValidationException("主对象更新后无法解析主键，不能同步子对象", ErrorCodes.ParameterInvalid);
        }

        return value;
    }

    private static object ResolveDeleteParentKey(
        IReadOnlyDictionary<string, object?> rootRow,
        string rootId,
        string? configuredParentId,
        string parentKeyField)
    {
        if (!string.IsNullOrWhiteSpace(configuredParentId))
        {
            return NormalizeDataId(configuredParentId);
        }

        return ResolveParentKey(rootRow, rootId, parentKeyField);
    }

    private static string? ResolveCompositeRowId(IReadOnlyDictionary<string, object?> row, string keyField)
    {
        var value =
            ReadRowValue(row, "__runtimeKey") ??
            ReadRowValue(row, keyField) ??
            ReadRowValue(row, "id") ??
            ReadRowValue(row, "ID");
        return value is null || string.IsNullOrWhiteSpace(value.ToString()) ? null : value.ToString();
    }

    private static string? ResolveDeleteChildId(IReadOnlyDictionary<string, object?> row)
    {
        var value =
            ReadRowValue(row, "__runtimeKey") ??
            ReadRowValue(row, "id") ??
            ReadRowValue(row, "ID");
        return value is null || string.IsNullOrWhiteSpace(value.ToString()) ? null : value.ToString();
    }

    private async Task<CompositeExecutionContext> ResolveCompositeExecutionAsync(
        string rootModelCode,
        IReadOnlyList<string> childModelCodes,
        CancellationToken cancellationToken)
    {
        var models = new List<RuntimeDataModelDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootModel = await GetPublishedModelAsync(rootModelCode, cancellationToken);
        models.Add(rootModel);
        seen.Add(rootModel.ModelCode);

        foreach (var childModelCode in childModelCodes)
        {
            if (!seen.Add(childModelCode))
            {
                continue;
            }

            models.Add(await GetPublishedModelAsync(childModelCode, cancellationToken));
        }

        var provider = providerRegistry.GetRequired(rootModel.ProviderKey);
        if (provider is not ITransactionalDataModelProvider transactionalProvider)
        {
            throw new ValidationException("当前模型数据源不支持一对多事务写入", ErrorCodes.RuntimeDataModelInvalid);
        }

        foreach (var model in models)
        {
            if (!string.Equals(model.ProviderKey, rootModel.ProviderKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("一对多复合操作只能绑定同一类数据模型 Provider", ErrorCodes.RuntimeDataModelInvalid);
            }
        }

        return new CompositeExecutionContext(transactionalProvider, models);
    }

    private async Task<RuntimeDataModelDefinition> GetPublishedModelAsync(
        string modelCode,
        CancellationToken cancellationToken)
    {
        return await GetPublishedModelAsync(modelCode, bypassModelPermission: false, cancellationToken);
    }

    private async Task<RuntimeDataModelDefinition> GetPublishedModelAsync(
        string modelCode,
        bool bypassModelPermission,
        CancellationToken cancellationToken)
    {
        EnsureWorkspace();
        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        var normalizedModelCode = NormalizeCode(modelCode, "模型编码");
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<SystemDataModelEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.ModelCode == normalizedModelCode &&
                item.Status == "Published")
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("运行时数据模型不存在", ErrorCodes.RuntimeDataModelNotFound);

        if (!bypassModelPermission &&
            !string.IsNullOrWhiteSpace(entity.PermissionCode) &&
            !currentUser.HasAsterErpPermission(entity.PermissionCode))
        {
            throw new ValidationException("无权限访问该运行时数据模型", ErrorCodes.PermissionDenied);
        }

        if (string.IsNullOrWhiteSpace(entity.SchemaJson) || entity.SchemaJson.Length > MaxSchemaJsonLength)
        {
            throw new ValidationException("运行时数据模型配置无效", ErrorCodes.RuntimeDataModelInvalid);
        }

        RuntimeDataModelSchema? schema;
        try
        {
            schema = JsonSerializer.Deserialize<RuntimeDataModelSchema>(entity.SchemaJson, SchemaJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"运行时数据模型配置不是合法 JSON: {ex.Message}", ErrorCodes.RuntimeDataModelInvalid);
        }

        if (schema?.Fields is null || schema.Fields.Count == 0)
        {
            throw new ValidationException("运行时数据模型未配置字段", ErrorCodes.RuntimeDataModelInvalid);
        }

        return RuntimeDataModelDefinition.FromEntity(entity, schema);
    }

    private bool ShouldBypassModelPermissionForPreview(RuntimeQueryRequest request) =>
        !string.IsNullOrWhiteSpace(request.PageCode) &&
        !string.IsNullOrWhiteSpace(request.PreviewPageId) &&
        currentUser.HasAsterErpPermission(PermissionCodes.AppDevelopmentCenterDesignerPreview);

    private RuntimeDataModelQuery BuildValidatedQuery(RuntimeDataModelDefinition model, RuntimeQueryRequest request)
    {
        var fieldMap = model.Fields.ToDictionary(item => item.FieldCode, StringComparer.OrdinalIgnoreCase);
        var pageIndex = Math.Max(request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex, 1);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? DefaultPageSize : request.PageSize, 1, MaxPageSize);
        var filters = ValidateFilters(fieldMap, request.Filters ?? []);
        var sorts = ValidateSorts(fieldMap, request.Sorts ?? []);

        return new RuntimeDataModelQuery(pageIndex, pageSize, request.Keyword?.Trim(), filters, sorts);
    }

    private RuntimeExpressionEvaluationContext BuildOperationContext(IReadOnlyDictionary<string, object?> variables)
    {
        var sources = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase)
        {
            ["variables"] = variables,
            ["vars"] = variables,
            ["system"] = new Dictionary<string, object?>
            {
                ["currentUserId"] = currentUser.GetAsterErpUserId(),
                ["tenantId"] = currentUser.GetAsterErpTenantId(),
                ["appCode"] = currentUser.GetAsterErpAppCode(),
                ["now"] = DateTime.UtcNow
            }
        };

        if (variables.TryGetValue("form", out var form))
        {
            sources["form"] = form;
        }

        if (variables.TryGetValue("currentRow", out var currentRow))
        {
            sources["currentRow"] = currentRow;
            sources["model"] = currentRow;
            sources["row"] = currentRow;
        }

        if (variables.TryGetValue("model", out var model))
        {
            sources["model"] = model;
        }

        return new RuntimeExpressionEvaluationContext(sources);
    }

    private RuntimeQueryRequest BuildOperationQuery(
        RuntimeModelOperationDefinitionDto operation,
        RuntimeModelOperationRequest request,
        RuntimeExpressionEvaluationContext context)
    {
        var filters = operation.Filters
            .Where(item => !string.IsNullOrWhiteSpace(item.Field))
            .Select(item => new RuntimeFilterRequest(
                item.Field.Trim(),
                string.IsNullOrWhiteSpace(item.Operator) ? "equals" : item.Operator.Trim(),
                item.ValueExpression is null ? null : expressionEvaluator.Evaluate(item.ValueExpression, context),
                item.ValueToExpression is null ? null : expressionEvaluator.Evaluate(item.ValueToExpression, context)))
            .ToArray();
        return new RuntimeQueryRequest(
            operation.PageIndex <= 0 ? DefaultPageIndex : operation.PageIndex,
            operation.PageSize <= 0 ? DefaultPageSize : operation.PageSize,
            null,
            filters,
            [],
            request.PageCode,
            request.PreviewPageId);
    }

    private Dictionary<string, object?> BuildOperationValues(
        RuntimeModelOperationDefinitionDto operation,
        RuntimeExpressionEvaluationContext context)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in operation.FieldMappings)
        {
            var field = NormalizeCode(mapping.TargetField, "目标字段");
            values[field] = mapping.Expression is null
                ? null
                : expressionEvaluator.Evaluate(mapping.Expression, context);
        }

        return values;
    }

    private RuntimeCompositeCreateRequest BuildCompositeCreateOperationRequest(
        string rootModelCode,
        RuntimeModelOperationDefinitionDto operation,
        RuntimeModelOperationRequest request,
        RuntimeExpressionEvaluationContext context)
    {
        return new RuntimeCompositeCreateRequest(
            rootModelCode,
            BuildOperationValues(operation, context),
            operation.Children.Select(child => new RuntimeCompositeChildCreateRequest(
                child.ModelCode,
                string.IsNullOrWhiteSpace(child.ParentKeyField) ? "id" : child.ParentKeyField,
                child.ForeignKeyField,
                ReadCompositeChildRows(child, context))).ToArray(),
            request.PageCode,
            request.PreviewPageId);
    }

    private RuntimeCompositeUpdateRequest BuildCompositeUpdateOperationRequest(
        string rootModelCode,
        RuntimeModelOperationDefinitionDto operation,
        RuntimeModelOperationRequest request,
        RuntimeExpressionEvaluationContext context)
    {
        var rootId = ResolveOperationId(operation, context);
        return new RuntimeCompositeUpdateRequest(
            rootModelCode,
            rootId,
            BuildOperationValues(operation, context),
            operation.Children.Select(child => new RuntimeCompositeChildUpdateRequest(
                child.ModelCode,
                string.IsNullOrWhiteSpace(child.ParentKeyField) ? "id" : child.ParentKeyField,
                child.ForeignKeyField,
                ReadCompositeChildRows(child, context),
                ReadCompositeDeleteIds(child, context),
                child.DeleteMissing)).ToArray(),
            request.PageCode,
            request.PreviewPageId);
    }

    private RuntimeCompositeDeleteRequest BuildCompositeDeleteOperationRequest(
        string rootModelCode,
        RuntimeModelOperationDefinitionDto operation,
        RuntimeModelOperationRequest request,
        RuntimeExpressionEvaluationContext context)
    {
        var rootId = ResolveOperationId(operation, context);
        return new RuntimeCompositeDeleteRequest(
            rootModelCode,
            rootId,
            operation.Children.Select(child => new RuntimeCompositeChildDeleteRequest(
                child.ModelCode,
                string.IsNullOrWhiteSpace(child.ParentKeyField) ? "id" : child.ParentKeyField,
                child.ForeignKeyField,
                ReadCompositeParentId(child, context, rootId),
                child.Required)).ToArray(),
            request.PageCode,
            request.PreviewPageId);
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object?>> ReadCompositeChildRows(
        RuntimeModelCompositeChildDefinitionDto child,
        RuntimeExpressionEvaluationContext context)
    {
        var descriptor = CreateCompositeChildExpressionDescriptor(child, "rowsExpression");
        object? value;
        try
        {
            value = child.RowsExpression is null ? null : EvaluateExpression(child.RowsExpression, context, descriptor);
        }
        catch (ValidationException exception)
        {
            if (!IsDataTypeValidationException(exception))
            {
                throw;
            }

            value = EvaluateWithoutExpectedType(child.RowsExpression, context, descriptor);
            throw new ValidationException(
                BuildCompositeChildExpressionError(child, child.RowsExpression, "rowsExpression", "子对象行数据表达式必须返回数组或集合", value),
                exception.Code);
        }

        if (value is null)
        {
            return [];
        }

        if (value is JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Array)
            {
                throw new ValidationException(
                    BuildCompositeChildExpressionError(child, child.RowsExpression, "rowsExpression", "子对象行数据表达式必须返回数组或集合", value),
                    ErrorCodes.ParameterInvalid);
            }

            return json.EnumerateArray()
                .Select(item => ApplyCompositeChildFieldMappings(child, ConvertCompositeRow(item), context))
                .ToArray();
        }

        if (value is IEnumerable<IReadOnlyDictionary<string, object?>> typedRows)
        {
            return typedRows
                .Select(row => ApplyCompositeChildFieldMappings(child, row, context))
                .ToArray();
        }

        if (value is IEnumerable<Dictionary<string, object?>> dictionaryRows)
        {
            return dictionaryRows
                .Select(row => ApplyCompositeChildFieldMappings(
                    child,
                    new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase),
                    context))
                .ToArray();
        }

        if (value is IEnumerable<object?> rows && value is not string)
        {
            return rows
                .Select(row => ApplyCompositeChildFieldMappings(child, ConvertCompositeRow(row), context))
                .ToArray();
        }

        throw new ValidationException(
            BuildCompositeChildExpressionError(child, child.RowsExpression, "rowsExpression", "子对象行数据表达式必须返回数组或集合", value),
            ErrorCodes.ParameterInvalid);
    }

    private IReadOnlyDictionary<string, object?> ApplyCompositeChildFieldMappings(
        RuntimeModelCompositeChildDefinitionDto child,
        IReadOnlyDictionary<string, object?> row,
        RuntimeExpressionEvaluationContext parentContext)
    {
        if (child.FieldMappings.Count == 0)
        {
            return row;
        }

        var sources = new Dictionary<string, object?>(parentContext.Sources, StringComparer.OrdinalIgnoreCase)
        {
            ["childRow"] = row,
            ["currentRow"] = row,
            ["item"] = row,
            ["model"] = row,
            ["row"] = row
        };
        var rowContext = new RuntimeExpressionEvaluationContext(sources);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in child.FieldMappings)
        {
            var field = NormalizeCode(mapping.TargetField, "子对象目标字段");
            values[field] = mapping.Expression is null
                ? null
                : expressionEvaluator.Evaluate(mapping.Expression, rowContext);
        }

        PreserveCompositeChildRuntimeKey(values, row);
        return values;
    }

    private static void PreserveCompositeChildRuntimeKey(
        Dictionary<string, object?> values,
        IReadOnlyDictionary<string, object?> row)
    {
        foreach (var key in new[] { "__runtimeKey", "id", "ID" })
        {
            if (!values.ContainsKey(key) && row.TryGetValue(key, out var value))
            {
                values[key] = value;
            }
        }
    }

    private IReadOnlyList<string> ReadCompositeDeleteIds(
        RuntimeModelCompositeChildDefinitionDto child,
        RuntimeExpressionEvaluationContext context)
    {
        var value = child.DeleteIdsExpression is null
            ? null
            : EvaluateWithoutExpectedType(
                child.DeleteIdsExpression,
                context,
                CreateCompositeChildExpressionDescriptor(child, "deleteIdsExpression"));
        if (value is null)
        {
            return [];
        }

        if (value is JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Array)
            {
                var singleJsonValue = ResolveCompositeDeleteId(json);
                return string.IsNullOrWhiteSpace(singleJsonValue) ? [] : [singleJsonValue];
            }

            return json.EnumerateArray()
                .Select(item => ResolveCompositeDeleteId(item))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToArray();
        }

        if (value is IEnumerable<object?> items && value is not string)
        {
            return items
                .Select(ResolveCompositeDeleteId)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToArray();
        }

        var single = ResolveCompositeDeleteId(value);
        return string.IsNullOrWhiteSpace(single) ? [] : [single];
    }

    private static string? ResolveCompositeDeleteId(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Object)
            {
                var jsonRow = ConvertCompositeRow(json);
                return ResolveCompositeRowId(jsonRow, "id");
            }

            return json.ValueKind == JsonValueKind.String
                ? json.GetString()?.Trim()
                : json.ToString().Trim();
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyRow)
        {
            return ResolveCompositeRowId(readOnlyRow, "id");
        }

        if (value is IDictionary<string, object?> row)
        {
            return ResolveCompositeRowId(new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase), "id");
        }

        return value.ToString()?.Trim();
    }

    private object? EvaluateExpression(
        RuntimeValueExpressionDto? expression,
        RuntimeExpressionEvaluationContext context,
        RuntimeExpressionEvaluationDescriptor descriptor) =>
        expression is null ? null : expressionEvaluator.Evaluate(expression, context, descriptor);

    private object? EvaluateWithoutExpectedType(
        RuntimeValueExpressionDto? expression,
        RuntimeExpressionEvaluationContext context,
        RuntimeExpressionEvaluationDescriptor descriptor) =>
        expression is null ? null : expressionEvaluator.Evaluate(expression, context, descriptor);

    private static RuntimeExpressionEvaluationDescriptor CreateCompositeChildExpressionDescriptor(
        RuntimeModelCompositeChildDefinitionDto child,
        string expressionName) =>
        new()
        {
            BindingKey = child.ForeignKeyField,
            ExpressionName = expressionName,
            ModelCode = child.ModelCode,
            OwnerId = child.ModelCode,
            OwnerName = child.ModelCode,
            OwnerType = "RuntimeModelCompositeChild"
        };

    private static bool IsDataTypeValidationException(ValidationException exception) =>
        exception.Message.StartsWith("变量表达式结果必须", StringComparison.Ordinal);

    private static string BuildCompositeChildExpressionError(
        RuntimeModelCompositeChildDefinitionDto child,
        RuntimeValueExpressionDto? expression,
        string expressionName,
        string message,
        object? actualValue)
    {
        var modelCode = string.IsNullOrWhiteSpace(child.ModelCode) ? "未配置" : child.ModelCode.Trim();
        var foreignKeyField = string.IsNullOrWhiteSpace(child.ForeignKeyField) ? "未配置" : child.ForeignKeyField.Trim();
        var actualType = actualValue is null ? "null" : actualValue.GetType().Name;
        return $"{message}。modelCode={modelCode}，foreignKeyField={foreignKeyField}，expressionName={expressionName}，expression={FormatExpression(expression)}，actualType={actualType}";
    }

    private static string FormatExpression(RuntimeValueExpressionDto? expression)
    {
        if (expression is null)
        {
            return "未配置";
        }

        var kind = string.IsNullOrWhiteSpace(expression.Kind) ? "未配置" : expression.Kind.Trim();
        if (expression.Ref is not null)
        {
            var refPath = expression.Ref.FieldPath.Count == 0
                ? expression.Ref.OutputKey ?? expression.Ref.VariableId
                : $"{expression.Ref.OutputKey ?? expression.Ref.VariableId}.{string.Join('.', expression.Ref.FieldPath)}";
            return $"{kind}:{expression.Ref.SourceType}:{refPath}";
        }

        return string.IsNullOrWhiteSpace(expression.FunctionId) ? kind : $"{kind}:{expression.FunctionId}";
    }

    private string? ReadCompositeParentId(
        RuntimeModelCompositeChildDefinitionDto child,
        RuntimeExpressionEvaluationContext context,
        string fallback)
    {
        var value = child.ParentIdExpression is null ? null : expressionEvaluator.Evaluate(child.ParentIdExpression, context);
        var text = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static IReadOnlyDictionary<string, object?> ConvertCompositeRow(object? value)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyRow)
        {
            return readOnlyRow;
        }

        if (value is IDictionary<string, object?> row)
        {
            return new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
        }

        if (value is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(
                json.GetRawText(),
                SchemaJsonOptions) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        throw new ValidationException("复合子对象行必须是对象", ErrorCodes.ParameterInvalid);
    }

    private async Task<RuntimeDetailResponse> ExecuteUpdateOperationAsync(
        string modelCode,
        RuntimeModelOperationDefinitionDto operation,
        RuntimeExpressionEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var id = ResolveOperationId(operation, context);
        await UpdateFieldsAsync(modelCode, id, BuildOperationValues(operation, context), cancellationToken);
        return await GetDetailAsync(modelCode, id, cancellationToken);
    }

    private string ResolveOperationId(
        RuntimeModelOperationDefinitionDto operation,
        RuntimeExpressionEvaluationContext context)
    {
        var value = operation.IdExpression is null
            ? RuntimeExpressionPathReader.Read(context.Sources.TryGetValue("currentRow", out var row) ? row : null, "__runtimeKey") ??
              RuntimeExpressionPathReader.Read(context.Sources.TryGetValue("currentRow", out row) ? row : null, "id")
            : expressionEvaluator.Evaluate(operation.IdExpression, context);
        var text = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("模型操作缺少数据主键", ErrorCodes.ParameterInvalid);
        }

        return text;
    }

    private async Task<(IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows, IReadOnlyList<RuntimeCellSpanResponse> CellSpans)> ProjectRowsAsync(
        string? pageCode,
        string? previewPageId,
        RuntimeDataModelDefinition model,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> sourceRows,
        CancellationToken cancellationToken)
    {
        var gridView = await runtimeGridViewService.GetAsync(pageCode ?? string.Empty, previewPageId, cancellationToken);
        var leaves = FlattenLeafColumns(gridView.Columns);
        if (leaves.Count == 0)
        {
            throw new ValidationException("运行时列视图未配置可显示列", ErrorCodes.RuntimeGridViewInvalid);
        }

        var fieldMap = model.Fields.ToDictionary(item => item.FieldCode, StringComparer.OrdinalIgnoreCase);
        foreach (var leaf in leaves)
        {
            EnsureColumnFieldReferences(leaf, fieldMap);
        }

        var projectedRows = sourceRows
            .Select(row => ProjectRow(model, row, leaves))
            .ToList();
        var cellSpans = BuildCellSpans(projectedRows, leaves);
        return (projectedRows, cellSpans);
    }

    private static IReadOnlyList<RuntimeGridViewColumnResponse> FlattenLeafColumns(IReadOnlyList<RuntimeGridViewColumnResponse> columns)
    {
        var result = new List<RuntimeGridViewColumnResponse>();
        foreach (var column in columns.OrderBy(item => item.Order ?? 0))
        {
            if (column.Children is { Count: > 0 })
            {
                result.AddRange(FlattenLeafColumns(column.Children));
                continue;
            }

            if (column.IsVisible is false)
            {
                continue;
            }

            result.Add(column);
        }

        return result;
    }

    private IReadOnlyDictionary<string, object?> ApplyDisplayHelpers(
        RuntimeDataModelDefinition model,
        IReadOnlyDictionary<string, object?> row)
    {
        var result = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
        foreach (var field in model.Fields)
        {
            if (field.DisplayHelpers.Count == 0 || !result.TryGetValue(field.FieldCode, out var value))
            {
                continue;
            }

            result[field.FieldCode] = expressionEvaluator.ApplyHelpers(value, field.DisplayHelpers);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, object?> EnsureRuntimeKey(
        RuntimeDataModelDefinition model,
        IReadOnlyDictionary<string, object?> row)
    {
        var result = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
        var keyValue = ReadRowValue(result, model.KeyField) ?? ReadRowValue(result, "id") ?? ReadRowValue(result, "ID");
        if (keyValue is not null && !string.IsNullOrWhiteSpace(keyValue.ToString()))
        {
            result["__runtimeKey"] = keyValue.ToString();
            if (!result.ContainsKey(model.KeyField))
            {
                result[model.KeyField] = keyValue;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, object?> ProjectRow(
        RuntimeDataModelDefinition model,
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<RuntimeGridViewColumnResponse> columns)
    {
        var projected = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            projected[column.Key] = ResolveColumnValue(row, column);
        }

        if (row.TryGetValue("id", out var idValue) && !projected.ContainsKey("id"))
        {
            projected["id"] = idValue;
        }

        if (row.TryGetValue(model.KeyField, out var keyValue) && !projected.ContainsKey(model.KeyField))
        {
            projected[model.KeyField] = keyValue;
        }

        if (row.TryGetValue("__runtimeKey", out var runtimeKey) && !projected.ContainsKey("__runtimeKey"))
        {
            projected["__runtimeKey"] = runtimeKey;
        }

        return projected;
    }

    private static object? ResolveColumnValue(
        IReadOnlyDictionary<string, object?> row,
        RuntimeGridViewColumnResponse column)
    {
        if (column.ValueSource is null)
        {
            var binding = FirstNonEmpty(column.Binding, column.Key);
            return ReadRowValue(row, binding);
        }

        var type = column.ValueSource.Type.Trim().ToLowerInvariant();
        return type switch
        {
            "field" => ReadRowValue(row, FirstNonEmpty(column.ValueSource.Field, column.Binding, column.Key)),
            "template" => RenderTemplate(row, column.ValueSource.Template ?? string.Empty),
            "concat" => string.Join("", (column.ValueSource.Fields ?? []).Select(field => ReadRowValue(row, field)?.ToString() ?? string.Empty)),
            "nested" => ReadRowValue(row, FirstNonEmpty(column.ValueSource.Field, column.ValueSource.Path, column.Binding, column.Key)),
            _ => ReadRowValue(row, FirstNonEmpty(column.Binding, column.Key))
        };
    }

    private static string RenderTemplate(IReadOnlyDictionary<string, object?> row, string template) =>
        TemplateTokenRegex.Replace(template, match =>
            ReadRowValue(row, match.Groups["field"].Value)?.ToString() ?? string.Empty);

    private static object? ReadRowValue(IReadOnlyDictionary<string, object?> row, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return row.TryGetValue(key, out var value) ? value : null;
    }

    private static bool RuntimeValuesEqual(object? left, object? right)
    {
        var leftText = NormalizeRuntimeValueText(left);
        var rightText = NormalizeRuntimeValueText(right);
        return !string.IsNullOrWhiteSpace(leftText) &&
            !string.IsNullOrWhiteSpace(rightText) &&
            string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeRuntimeValueText(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString()?.Trim(),
                JsonValueKind.Number => jsonElement.GetRawText().Trim(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => jsonElement.GetRawText().Trim()
            };
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static void EnsureColumnFieldReferences(
        RuntimeGridViewColumnResponse column,
        IReadOnlyDictionary<string, RuntimeDataFieldDefinition> fieldMap)
    {
        EnsureKnownField(column.Binding, fieldMap, $"列 {column.Key} 的 binding 不在字段白名单内", false, false);
        if (string.IsNullOrWhiteSpace(column.Binding) && column.ValueSource is null && !fieldMap.ContainsKey(column.Key))
        {
            throw new ValidationException($"列 {column.Key} 必须配置 binding 或 valueSource", ErrorCodes.RuntimeFieldNotAllowed);
        }
        EnsureKnownField(column.QueryField, fieldMap, $"列 {column.Key} 的 queryField 不允许查询", true, false);
        EnsureKnownField(column.SortField, fieldMap, $"列 {column.Key} 的 sortField 不允许排序", false, true);

        if (column.ValueSource is null)
        {
            return;
        }

        var type = column.ValueSource.Type.Trim().ToLowerInvariant();
        if (type == "field")
        {
            EnsureKnownField(column.ValueSource.Field, fieldMap, $"列 {column.Key} 的 valueSource.field 不在字段白名单内", false, false);
        }
        else if (type == "template" && !string.IsNullOrWhiteSpace(column.ValueSource.Template))
        {
            foreach (Match match in TemplateTokenRegex.Matches(column.ValueSource.Template))
            {
                EnsureKnownField(match.Groups["field"].Value, fieldMap, $"列 {column.Key} 的模板字段不在字段白名单内", false, false);
            }
        }
        else
        {
            foreach (var field in column.ValueSource.Fields ?? [])
            {
                EnsureKnownField(field, fieldMap, $"列 {column.Key} 的 valueSource.fields 不在字段白名单内", false, false);
            }
        }
    }

    private static void EnsureKnownField(
        string? fieldCode,
        IReadOnlyDictionary<string, RuntimeDataFieldDefinition> fieldMap,
        string message,
        bool requireQueryable,
        bool requireSortable)
    {
        if (string.IsNullOrWhiteSpace(fieldCode))
        {
            return;
        }

        if (!fieldMap.TryGetValue(fieldCode.Trim(), out var field))
        {
            throw new ValidationException(message, ErrorCodes.RuntimeFieldNotAllowed);
        }

        if (requireQueryable && !field.Queryable)
        {
            throw new ValidationException(message, ErrorCodes.RuntimeFieldNotAllowed);
        }

        if (requireSortable && !field.Sortable)
        {
            throw new ValidationException(message, ErrorCodes.RuntimeFieldNotAllowed);
        }
    }

    private static IReadOnlyList<RuntimeCellSpanResponse> BuildCellSpans(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyList<RuntimeGridViewColumnResponse> columns)
    {
        var spans = new List<RuntimeCellSpanResponse>();
        foreach (var column in columns)
        {
            if (column.Merge is not { Enabled: true } || column.Merge.Direction != "vertical")
            {
                continue;
            }

            var rowIndex = 0;
            while (rowIndex < rows.Count)
            {
                var value = ReadRowValue(rows[rowIndex], column.Key)?.ToString() ?? string.Empty;
                var span = 1;
                while (rowIndex + span < rows.Count)
                {
                    var nextValue = ReadRowValue(rows[rowIndex + span], column.Key)?.ToString() ?? string.Empty;
                    if (!string.Equals(value, nextValue, StringComparison.Ordinal))
                    {
                        break;
                    }

                    span += 1;
                }

                if (span > 1)
                {
                    spans.Add(new RuntimeCellSpanResponse(rowIndex, column.Key, span, 1));
                    for (var hiddenIndex = rowIndex + 1; hiddenIndex < rowIndex + span; hiddenIndex += 1)
                    {
                        spans.Add(new RuntimeCellSpanResponse(hiddenIndex, column.Key, 0, 0));
                    }
                }

                rowIndex += span;
            }
        }

        return spans;
    }

    private IReadOnlyList<RuntimeDataModelFilter> ValidateFilters(
        IReadOnlyDictionary<string, RuntimeDataFieldDefinition> fieldMap,
        IReadOnlyList<RuntimeFilterRequest> filters)
    {
        var result = new List<RuntimeDataModelFilter>();
        foreach (var filter in filters)
        {
            var fieldCode = filter.Field.Trim();
            if (!fieldMap.TryGetValue(fieldCode, out var field) || !field.Queryable)
            {
                throw new ValidationException($"字段不允许查询: {fieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
            }

            var operatorName = filter.Operator.Trim();
            if (!SupportedOperators.Contains(operatorName))
            {
                throw new ValidationException($"查询操作符不支持: {operatorName}", ErrorCodes.RuntimeFieldNotAllowed);
            }

            result.Add(new RuntimeDataModelFilter(
                field,
                operatorName,
                expressionEvaluator.ApplyHelpers(filter.Value, field.QueryHelpers),
                expressionEvaluator.ApplyHelpers(filter.ValueTo, field.QueryHelpers)));
        }

        return result;
    }

    private static IReadOnlyList<RuntimeDataModelSort> ValidateSorts(
        IReadOnlyDictionary<string, RuntimeDataFieldDefinition> fieldMap,
        IReadOnlyList<RuntimeSortRequest> sorts)
    {
        var result = new List<RuntimeDataModelSort>();
        foreach (var sort in sorts)
        {
            var fieldCode = sort.Field.Trim();
            if (!fieldMap.TryGetValue(fieldCode, out var field) || !field.Sortable)
            {
                throw new ValidationException($"字段不允许排序: {fieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
            }

            var order = sort.Order.Trim();
            if (!string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("排序方向只能是 asc 或 desc", ErrorCodes.RuntimeFieldNotAllowed);
            }

            result.Add(new RuntimeDataModelSort(field, order.ToLowerInvariant()));
        }

        return result;
    }

    private void EnsureWorkspace()
    {
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        if (string.IsNullOrWhiteSpace(currentUser.GetAsterErpTenantId()) ||
            string.IsNullOrWhiteSpace(currentUser.GetAsterErpAppCode()))
        {
            throw new ValidationException("请先选择租户应用工作区", ErrorCodes.PermissionDenied);
        }
    }

    private static RuntimeDataFieldDefinition ResolveWritableField(
        RuntimeDataModelDefinition model,
        string fieldCode)
    {
        var normalizedFieldCode = NormalizeCode(fieldCode, "字段编码");
        var field = model.Fields.FirstOrDefault(item =>
            string.Equals(item.FieldCode, normalizedFieldCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Binding, normalizedFieldCode, StringComparison.OrdinalIgnoreCase));
        if (field is null || string.IsNullOrWhiteSpace(field.Binding) || !field.Writable)
        {
            throw new ValidationException($"字段不允许更新: {normalizedFieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
        }

        if (string.Equals(field.FieldCode, model.KeyField, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.Binding, model.KeyField, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("主键字段不允许更新", ErrorCodes.RuntimeFieldNotAllowed);
        }

        return field;
    }

    private IReadOnlyList<RuntimeDataModelFieldUpdate> BuildValidatedUpdates(
        RuntimeDataModelDefinition model,
        IReadOnlyDictionary<string, object?> updates)
    {
        var result = new List<RuntimeDataModelFieldUpdate>(updates.Count);
        var fieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var update in updates)
        {
            var field = ResolveWritableField(model, update.Key);
            if (!fieldCodes.Add(field.FieldCode))
            {
                throw new ValidationException($"字段重复更新: {field.FieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
            }

            result.Add(new RuntimeDataModelFieldUpdate(
                field,
                RuntimeDataProviderSupport.CoerceValue(
                    expressionEvaluator.ApplyHelpers(update.Value, field.WriteHelpers),
                    field.DataType)));
        }

        return result;
    }

    private IReadOnlyList<RuntimeDataModelFieldUpdate> BuildValidatedCreateValues(
        RuntimeDataModelDefinition model,
        IReadOnlyDictionary<string, object?> values)
    {
        var fieldMap = model.Fields
            .Where(item => !string.IsNullOrWhiteSpace(item.Binding))
            .ToDictionary(item => item.FieldCode, StringComparer.OrdinalIgnoreCase);
        var bindingMap = model.Fields
            .Where(item => !string.IsNullOrWhiteSpace(item.Binding))
            .ToDictionary(item => item.Binding, StringComparer.OrdinalIgnoreCase);
        var result = new List<RuntimeDataModelFieldUpdate>(values.Count);
        var fieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var normalizedFieldCode = NormalizeCode(value.Key, "字段编码");
            if (!fieldMap.TryGetValue(normalizedFieldCode, out var field) &&
                !bindingMap.TryGetValue(normalizedFieldCode, out field))
            {
                throw new ValidationException($"字段不允许创建: {normalizedFieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
            }

            if (!field.Writable)
            {
                throw new ValidationException($"字段不允许创建: {normalizedFieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
            }

            if (!fieldCodes.Add(field.FieldCode))
            {
                throw new ValidationException($"字段重复创建: {field.FieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
            }

            result.Add(new RuntimeDataModelFieldUpdate(
                field,
                RuntimeDataProviderSupport.CoerceValue(
                    expressionEvaluator.ApplyHelpers(value.Value, field.WriteHelpers),
                    field.DataType)));
        }

        return result;
    }

    private static string ResolveCreatedId(
        RuntimeDataModelDefinition model,
        IReadOnlyDictionary<string, object?> row)
    {
        if (row.TryGetValue(model.KeyField, out var keyValue) ||
            row.TryGetValue("id", out keyValue))
        {
            var text = keyValue?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        throw new ValidationException("创建成功但未返回主键", ErrorCodes.RuntimeDataModelInvalid);
    }

    private static string NormalizeDataId(string value)
    {
        var normalizedId = value.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId) || normalizedId.Length > 128)
        {
            throw new ValidationException("数据主键不能为空且长度不能超过 128", ErrorCodes.ParameterInvalid);
        }

        return normalizedId;
    }

    private static string NormalizeCode(string value, string displayName)
    {
        var normalized = Uri.UnescapeDataString(value).Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 128)
        {
            throw new ValidationException($"{displayName}不能为空且长度不能超过 128", ErrorCodes.ParameterInvalid);
        }

        if (!global::System.Text.RegularExpressions.Regex.IsMatch(normalized, "^[A-Za-z][A-Za-z0-9_.:-]*$"))
        {
            throw new ValidationException($"{displayName}格式无效", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private sealed record CompositeExecutionContext(
        ITransactionalDataModelProvider Provider,
        IReadOnlyList<RuntimeDataModelDefinition> Models);
}
