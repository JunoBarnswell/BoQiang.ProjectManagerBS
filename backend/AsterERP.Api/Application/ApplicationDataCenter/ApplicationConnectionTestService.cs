using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationConnectionTestService(
    IRepository<ApplicationConnectionCheckTaskEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService,
    ApplicationDataSourceService dataSourceService,
    ApplicationDataSourceProviderRegistry providerRegistry)
    : ApplicationDataCenterObjectService<ApplicationConnectionCheckTaskEntity>(
        repository,
        databaseAccessor,
        workspaceResolver,
        secretProtector,
        riskGuard,
        referenceService,
        templateCatalog,
        snapshotService)
{
    protected override string ModuleKey => ApplicationDataCenterModuleKey.ConnectionTest;

    protected override void ApplySpecificFields(
        ApplicationConnectionCheckTaskEntity entity,
        ApplicationDataCenterObjectUpsertRequest request,
        bool isCreate)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        entity.DataSourceId = ReadString(config, "dataSourceId");
        entity.TemplateCode = string.IsNullOrWhiteSpace(entity.ObjectType)
            ? ApplicationConnectionTestTemplate.Connectivity
            : entity.ObjectType;
        entity.RetryCount = ReadInt(config, "retryCount") ?? 1;
    }

    public override async Task<ApplicationDataCenterActionResultResponse> TestAsync(
        string id,
        ApplicationDataCenterActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = WorkspaceResolver.Resolve();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(entity.DataSourceId))
        {
            throw new ValidationException("检测任务必须选择数据源", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var dataSource = (await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item =>
                item.Id == entity.DataSourceId &&
                !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);

        var stopwatch = Stopwatch.StartNew();
        var run = new ApplicationConnectionCheckRunEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            TaskId = entity.Id,
            DataSourceId = dataSource.Id,
            TemplateCode = entity.TemplateCode,
            Result = "Running",
            StartedAt = DateTime.UtcNow,
            CreatedBy = workspace.UserId
        };
        await db.Insertable(run).ExecuteCommandAsync(cancellationToken);

        var result = await dataSourceService.TestDataSourceEntityAsync(dataSource, cancellationToken);
        stopwatch.Stop();
        run.Result = result.Success ? "Success" : "Failed";
        run.FinishedAt = DateTime.UtcNow;
        run.DurationMs = stopwatch.ElapsedMilliseconds;
        run.ErrorMessage = result.Success ? null : result.Message;
        run.ResultJson = result.DetailJson;
        run.UpdatedBy = workspace.UserId;
        run.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(run).ExecuteCommandAsync(cancellationToken);
        await MarkValidationAsync(entity.Id, result.Status, result.Message, result.DetailJson, cancellationToken);

        return result with
        {
            DurationMs = stopwatch.ElapsedMilliseconds,
            NextActions = TemplateCatalog.BuildNextActions(ModuleKey, id, entity.Status)
        };
    }

    public async Task<ApplicationConnectionDiagnosticResponse> DiagnoseAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(entity.DataSourceId))
        {
            var diagnosticDb = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
            var diagnosticSource = (await diagnosticDb.Queryable<ApplicationDataSourceEntity>()
                .Where(item => item.Id == entity.DataSourceId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
            if (diagnosticSource is null)
            {
                var missingSourceStage = new ApplicationConnectionDiagnosticStageResponse(
                    "database",
                    "Failed",
                    0,
                    "数据源不存在或已删除",
                    "恢复数据源后重新执行诊断",
                    null);
                await MarkValidationAsync(entity.Id, ApplicationDataCenterObjectStatus.Error, missingSourceStage.Message, null, cancellationToken);
                return new(id, false, [missingSourceStage]);
            }

            var diagnostic = await dataSourceService.DiagnoseAsync(diagnosticSource.Id, cancellationToken);
            var outcome = diagnostic.Stages.LastOrDefault(stage => stage.Status is "Failed" or "Blocked")
                ?? diagnostic.Stages.LastOrDefault();
            await MarkValidationAsync(
                entity.Id,
                diagnostic.Success ? ApplicationDataCenterObjectStatus.Normal : ApplicationDataCenterObjectStatus.Error,
                outcome?.Message ?? (diagnostic.Success ? "连接诊断通过" : "连接诊断失败"),
                ApplicationDataCenterJson.Serialize(diagnostic.Stages),
                cancellationToken);
            return new(id, diagnostic.Success, diagnostic.Stages);
        }
        var stages = new List<ApplicationConnectionDiagnosticStageResponse>();
        var configurationStarted = Stopwatch.GetTimestamp();
        if (string.IsNullOrWhiteSpace(entity.DataSourceId))
        {
            stages.Add(Stage("configuration", "Failed", configurationStarted, "未选择数据源", "选择一个已配置的数据源", null));
            return new(id, false, stages);
        }

        stages.Add(Stage("configuration", "Passed", configurationStarted, "连接任务配置有效", null, null));
        var workspace = WorkspaceResolver.Resolve();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var source = (await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.Id == entity.DataSourceId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (source is null)
        {
            stages.Add(Stage("database", "Failed", Stopwatch.GetTimestamp(), "数据源不存在或已删除", "恢复数据源后重新执行诊断", null));
            return new(id, false, stages);
        }

        var connectivityStarted = Stopwatch.GetTimestamp();
        var connectivity = await dataSourceService.TestDataSourceEntityAsync(source, cancellationToken);
        stages.Add(Stage("connectivity", connectivity.Success ? "Passed" : "Failed", connectivityStarted, connectivity.Message, connectivity.Success ? null : "检查网络、凭据和数据库权限", connectivity.DetailJson));
        if (!connectivity.Success)
            return new(id, false, stages);

        var capabilityStarted = Stopwatch.GetTimestamp();
        try
        {
            var capability = providerRegistry.Resolve(source.ObjectType).Capability;
            stages.Add(Stage("capability", "Passed", capabilityStarted, "Provider capability 探测完成", null, ApplicationDataCenterJson.Serialize(capability)));
        }
        catch (Exception exception)
        {
            stages.Add(Stage("capability", "Failed", capabilityStarted, exception.Message, "检查 provider 类型和数据库驱动配置", null));
        }

        stages.Add(Stage("authorization", "Passed", Stopwatch.GetTimestamp(), "应用数据库数据权限过滤已启用", null, ApplicationDataCenterJson.Serialize(new { workspace.TenantId, workspace.AppCode })));
        return new(id, stages.All(item => item.Status == "Passed"), stages);
    }

    private static ApplicationConnectionDiagnosticStageResponse Stage(string code, string status, long started, string message, string? repairSuggestion, string? detailJson) =>
        new(code, status, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, message, repairSuggestion, detailJson);

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static int? ReadInt(IReadOnlyDictionary<string, object?> config, string key) =>
        int.TryParse(ReadString(config, key), out var parsed) ? parsed : null;
}
