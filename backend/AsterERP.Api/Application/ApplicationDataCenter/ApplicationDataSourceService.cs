using System.Diagnostics;
using System.Data;
using System.Data.Common;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceService(
    IRepository<ApplicationDataSourceEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataPreviewReader previewReader,
    ApplicationDataSourceProviderRegistry providerRegistry,
    ApplicationDataCenterSqlScriptAuditWriter? auditWriter = null,
    IUnitOfWork? unitOfWork = null)
    : ApplicationDataCenterObjectService<ApplicationDataSourceEntity>(
        repository,
        databaseAccessor,
        workspaceResolver,
        secretProtector,
        riskGuard,
        referenceService,
        templateCatalog,
        snapshotService)
{
    protected override string ModuleKey => ApplicationDataCenterModuleKey.DataSource;

    protected override void ValidatePublicConfigJson(string normalizedConfigJson) =>
        EnsureSecretRefOnlyPublicConfig(normalizedConfigJson);

    private static readonly Regex IdentifierRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    protected override void ApplySpecificFields(
        ApplicationDataSourceEntity entity,
        ApplicationDataCenterObjectUpsertRequest request,
        bool isCreate)
    {
        ApplicationDataSourceProviderPolicy.EnsureSupportedForWrite(entity.ObjectType);
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        entity.IsReadOnly = ReadBoolean(config, "readOnly");
        entity.IsSystemManaged = ReadBoolean(config, "systemManaged");
        entity.Endpoint ??= ResolveEndpoint(entity.ObjectType, config);
    }

    public override Task<ApplicationDataCenterOperationResponse> UpdateAsync(
        string id,
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SecretConfigJson is not null)
        {
            throw new ValidationException(
                "数据源凭据必须通过 SecretRef 专用替换或清除命令提交。",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return base.UpdateAsync(id, request, cancellationToken);
    }

    public Task<ApplicationDataCenterOperationResponse> UpdateDefinitionAsync(
        string id,
        UpdateDataSourceDefinitionCommand request,
        CancellationToken cancellationToken = default) =>
        UpdateDefinitionCoreAsync(id, request, cancellationToken);

    public new async Task<ApplicationDataCenterObjectDetailResponse> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        await EnsureOperationalProviderAsync(entity, cancellationToken);
        return await base.GetAsync(id, cancellationToken);
    }

    public new async Task<ApplicationDataCenterOperationResponse> EnableAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        await EnsureOperationalProviderAsync(entity, cancellationToken);
        return await base.EnableAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationDataSourceProviderMigrationItemResponse>> GetMigrationInventoryAsync(
        CancellationToken cancellationToken = default)
    {
        var workspace = WorkspaceResolver.Resolve();
        await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var retiredProviders = ApplicationDataSourceProviderPolicy.RetiredProviderCodes.ToArray();
        var entities = await ScopedQuery(workspace)
            .Where(item => retiredProviders.Contains(item.ObjectType))
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);

        foreach (var entity in entities)
            await MarkMigrationRequiredAsync(entity, cancellationToken);

        return entities.Select(entity => new ApplicationDataSourceProviderMigrationItemResponse(
            entity.Id,
            entity.ObjectCode,
            entity.ObjectName,
            entity.ObjectType,
            ApplicationDataCenterObjectStatus.MigrationRequired,
            entity.CreatedTime,
            entity.UpdatedTime,
            ApplicationDataSourceProviderPolicy.GetMigrationDiagnostic(entity.ObjectType))).ToArray();
    }

    public override async Task<ApplicationDataCenterOperationResponse> CreateAsync(
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        async Task<ApplicationDataCenterOperationResponse> SaveAsync()
        {
            var response = await base.CreateAsync(request, cancellationToken);
            var entity = await EnsureEntityAsync(response.Object.Id, cancellationToken);
            var fingerprint = ApplicationDataSourceConnectionFingerprint.Compute(entity);
            if (request is UpdateDataSourceDefinitionCommand command &&
                string.Equals(command.DiagnosticFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                entity.LastValidationStatus = "Normal";
                entity.LastValidationMessage = "连接诊断通过。";
                entity.LastValidatedAt = DateTime.UtcNow;
                entity.LastValidationFingerprint = fingerprint;
                await Repository.UpdateAsync(entity, cancellationToken);
            }

            if (auditWriter is not null)
            {
                await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
                {
                    TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
                    SourceKind = "DataSourceDefinition",
                    SourceId = entity.Id,
                    SourceName = entity.ObjectName,
                    DataSourceId = entity.Id,
                    ScriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{entity.Id}:{entity.VersionNo}:create"))).ToLowerInvariant(),
                    ScriptPreview = "Data source definition creation (redacted)",
                    StatementSummary = "PublicConfig and SecretRef committed atomically",
                    RiskSummary = "connection-definition-create",
                    Operation = "data-source.definition.create",
                    ResourceKind = "data-source.connection",
                    PermissionCode = PermissionCodes.AppDataCenterDataSourceAdd,
                    Outcome = "Succeeded",
                    Provider = entity.ObjectType,
                    RedactedDetailsJson = JsonSerializer.Serialize(new { secretIncluded = entity.SecretConfigCipherText is not null }),
                    IsSuccess = true
                }, cancellationToken);
            }

            var detail = await MapDetailAsync(entity, cancellationToken);
            return new ApplicationDataCenterOperationResponse(detail, response.ReferenceSummary, detail.NextActions);
        }

        return unitOfWork is null
            ? await SaveAsync()
            : await unitOfWork.ExecuteAsync(SaveAsync, cancellationToken);
    }

    private async Task<ApplicationDataCenterOperationResponse> UpdateDefinitionCoreAsync(
        string id,
        UpdateDataSourceDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        async Task<ApplicationDataCenterOperationResponse> SaveAsync()
        {
            var before = await EnsureEntityAsync(id, cancellationToken);
            var beforeFingerprint = ApplicationDataSourceConnectionFingerprint.Compute(before);
            var response = await base.UpdateAsync(id, request, cancellationToken);
            var after = await EnsureEntityAsync(id, cancellationToken);
            var afterFingerprint = ApplicationDataSourceConnectionFingerprint.Compute(after);

            if (!string.IsNullOrWhiteSpace(request.DiagnosticFingerprint) &&
                !string.Equals(request.DiagnosticFingerprint, afterFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("连接配置与诊断结果不一致，请重新诊断后保存。", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (!string.Equals(beforeFingerprint, afterFingerprint, StringComparison.Ordinal))
            {
                after.LastValidationStatus = "Stale";
                after.LastValidationMessage = "连接配置已变化，请重新诊断。";
                after.LastValidatedAt = null;
                after.LastValidationFingerprint = null;
                await Repository.UpdateAsync(after, cancellationToken);
            }

            if (auditWriter is not null)
            {
                await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
                {
                    TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
                    SourceKind = "DataSourceDefinition",
                    SourceId = after.Id,
                    SourceName = after.ObjectName,
                    DataSourceId = after.Id,
                    ScriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{after.Id}:{after.VersionNo}"))).ToLowerInvariant(),
                    ScriptPreview = "Data source definition mutation (redacted)",
                    StatementSummary = "PublicConfig and SecretRef committed atomically",
                    RiskSummary = "connection-definition-change",
                    Operation = "data-source.definition.update",
                    ResourceKind = "data-source.connection",
                    PermissionCode = PermissionCodes.AppDataCenterDataSourceEdit,
                    Outcome = "Succeeded",
                    Provider = after.ObjectType,
                    RedactedDetailsJson = JsonSerializer.Serialize(new { connectionChanged = beforeFingerprint != afterFingerprint }),
                    IsSuccess = true
                }, cancellationToken);
            }

            var detail = await MapDetailAsync(after, cancellationToken);
            return new ApplicationDataCenterOperationResponse(detail, response.ReferenceSummary, detail.NextActions);
        }

        return unitOfWork is null
            ? await SaveAsync()
            : await unitOfWork.ExecuteAsync(SaveAsync, cancellationToken);
    }

    public async Task<ApplicationDataCenterOperationResponse> ReplaceSecretAsync(
        string id,
        ApplicationDataSourceSecretReplaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = NormalizeSecretOperationReason(request.Reason);
        var secretJson = ApplicationDataCenterJson.NormalizeObjectJson(request.SecretConfigJson, "凭据配置");
        var entity = await EnsureEntityAsync(id, cancellationToken);
        entity.SecretConfigCipherText = SecretProtector.Protect(secretJson);
        entity.SecretRef ??= $"{entity.Id}:secret";
        entity.PublicConfigJson = SecretProtector.BuildPublicSecretSummary(
            entity.SecretConfigCipherText,
            entity.SecretRef,
            DateTime.UtcNow);
        await SaveSecretMutationAsync(entity, "replace", reason, cancellationToken);
        return await BuildSecretOperationResponseAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationDataCenterOperationResponse> ClearSecretAsync(
        string id,
        ApplicationDataSourceSecretClearRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = NormalizeSecretOperationReason(request.Reason);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        entity.SecretConfigCipherText = null;
        entity.SecretRef = null;
        entity.PublicConfigJson = "{}";
        await SaveSecretMutationAsync(entity, "clear", reason, cancellationToken);
        return await BuildSecretOperationResponseAsync(entity.Id, cancellationToken);
    }

    private async Task SaveSecretMutationAsync(
        ApplicationDataSourceEntity entity,
        string operation,
        string reason,
        CancellationToken cancellationToken)
    {
        var workspace = WorkspaceResolver.Resolve();
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        entity.VersionNo += 1;
        if (string.Equals(entity.Status, ApplicationDataCenterObjectStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            entity.Status = ApplicationDataCenterObjectStatus.Draft;
        }

        entity.LastValidationStatus = "Stale";
        entity.LastValidationMessage = "凭据已变化，请重新诊断。";
        entity.LastValidatedAt = null;
        entity.LastValidationFingerprint = null;

        await Repository.UpdateAsync(entity, cancellationToken);
        if (auditWriter is not null)
        {
            await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
            {
                TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
                SourceKind = "DataSourceSecret",
                SourceId = entity.Id,
                SourceName = entity.ObjectName,
                DataSourceId = entity.Id,
                ScriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{entity.Id}:{operation}:{entity.VersionNo}"))).ToLowerInvariant(),
                ScriptPreview = "SecretRef mutation (redacted)",
                StatementSummary = $"SecretRef {operation}; reason provided",
                RiskSummary = "secret-ref-mutation",
                Operation = $"data-source.secret.{operation}",
                ResourceKind = "data-source.secret-ref",
                PermissionCode = PermissionCodes.AppDataCenterDataSourceEdit,
                Outcome = "Succeeded",
                Provider = entity.ObjectType,
                RedactedDetailsJson = JsonSerializer.Serialize(new { operation, reasonProvided = !string.IsNullOrWhiteSpace(reason) }),
                IsSuccess = true
            }, cancellationToken);
        }
    }

    private async Task<ApplicationDataCenterOperationResponse> BuildSecretOperationResponseAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var summary = await ReferenceService.RecalculateReferencesAsync(ModuleKey, id, cancellationToken);
        var detail = await MapDetailAsync(await EnsureEntityAsync(id, cancellationToken), cancellationToken);
        return new ApplicationDataCenterOperationResponse(detail, summary, detail.NextActions);
    }

    private static string NormalizeSecretOperationReason(string reason)
    {
        var normalized = reason?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 500)
        {
            throw new ValidationException(
                "SecretRef 操作必须提供 1-500 字符的原因。",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return normalized;
    }

    public override async Task<ApplicationDataCenterActionResultResponse> TestAsync(
        string id,
        ApplicationDataCenterActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        return await TestDataSourceEntityAsync(entity, cancellationToken);
    }

    public async Task<ApplicationConnectionDiagnosticResponse> DiagnoseAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        await EnsureOperationalProviderAsync(entity, cancellationToken);
        var started = Stopwatch.StartNew();
        try
        {
            var diagnostic = await DiagnoseEntityAsync(entity, cancellationToken);
            await PersistDiagnosticAsync(entity, diagnostic.Success, diagnostic.Stages, cancellationToken);
            await WriteDiagnosticAuditAsync(entity, diagnostic.Stages, diagnostic.Success, null, started.ElapsedMilliseconds);
            return new(id, diagnostic.Success, diagnostic.Stages, ApplicationDataSourceConnectionFingerprint.Compute(entity));
        }
        catch (Exception exception)
        {
            await WriteDiagnosticAuditAsync(entity, [], false, exception, started.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<ApplicationDataSourceDraftDiagnosticResponse> DiagnoseDraftAsync(
        ApplicationDataSourceDraftDiagnosticRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = BuildDraftEntity(request);
        ApplicationDataSourceProviderPolicy.EnsureSupportedForWrite(entity.ObjectType);
        var started = Stopwatch.StartNew();
        try
        {
            var diagnostic = await DiagnoseEntityAsync(entity, cancellationToken);
            await WriteDiagnosticAuditAsync(entity, diagnostic.Stages, diagnostic.Success, null, started.ElapsedMilliseconds);
            return new(diagnostic.Success, diagnostic.Stages, ApplicationDataSourceConnectionFingerprint.Compute(entity));
        }
        catch (Exception exception)
        {
            await WriteDiagnosticAuditAsync(entity, [], false, exception, started.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task WriteDiagnosticAuditAsync(
        ApplicationDataSourceEntity entity,
        IReadOnlyList<ApplicationConnectionDiagnosticStageResponse> stages,
        bool success,
        Exception? exception,
        long durationMs)
    {
        if (auditWriter is null)
            return;

        var stageSummary = stages.Select(stage => new { stage.Code, stage.Status, stage.DurationMs, error = ReadDiagnosticErrorCode(stage.DetailJson) });
        var resourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{entity.ObjectType}:{entity.Id}:{entity.TenantId}:{entity.AppCode}"))).ToLowerInvariant();
        await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
        {
            TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
            SourceKind = "DataSourceDiagnostic",
            SourceId = entity.Id,
            SourceName = entity.ObjectName,
            DataSourceId = entity.Id,
            ScriptHash = resourceHash,
            RequestHash = resourceHash,
            ScriptPreview = "Data source diagnostic",
            StatementSummary = string.Join(",", stages.Select(stage => $"{stage.Code}:{stage.Status}")),
            RiskSummary = "connection-diagnostic",
            Operation = "data-source.diagnose",
            ResourceKind = "data-source.connection",
            PermissionCode = PermissionCodes.AppDataCenterDataSourceTest,
            Outcome = exception is OperationCanceledException ? "Canceled" : success ? "Succeeded" : "Failed",
            FailureCode = exception is BusinessException businessException ? businessException.Code.ToString() : exception?.GetType().Name,
            Provider = entity.ObjectType,
            TimeoutMs = 30_000,
            CancellationRequested = exception is OperationCanceledException,
            DurationMs = durationMs,
            IsSuccess = success,
            ErrorMessage = exception is null ? null : SanitizeDiagnosticMessage(exception),
            RedactedDetailsJson = JsonSerializer.Serialize(stageSummary)
        }, CancellationToken.None);
    }

    private async Task PersistDiagnosticAsync(
        ApplicationDataSourceEntity entity,
        bool success,
        IReadOnlyList<ApplicationConnectionDiagnosticStageResponse> stages,
        CancellationToken cancellationToken)
    {
        entity.LastValidationStatus = success ? "Normal" : "Error";
        entity.LastValidationMessage = success ? "连接诊断通过。" : stages.FirstOrDefault(item => item.Status == "Failed")?.Message ?? "连接诊断失败。";
        entity.LastValidatedAt = DateTime.UtcNow;
        entity.LastValidationFingerprint = ApplicationDataSourceConnectionFingerprint.Compute(entity);
        await Repository.UpdateAsync(entity, cancellationToken);
    }

    private static string? ReadDiagnosticErrorCode(string? detailJson)
    {
        if (string.IsNullOrWhiteSpace(detailJson))
            return null;
        try
        {
            using var document = JsonDocument.Parse(detailJson);
            return document.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<(bool Success, IReadOnlyList<ApplicationConnectionDiagnosticStageResponse> Stages)> DiagnoseEntityAsync(
        ApplicationDataSourceEntity entity,
        CancellationToken cancellationToken)
    {
        var options = connectionFactory.Resolve(entity);
        var stages = new List<ApplicationConnectionDiagnosticStageResponse>();

        AddConfigurationStage(entity, options, stages);
        if (stages[^1].Status == "Failed")
            return (false, stages);

        if (ApplicationDataSourceConnectionFactory.IsDatabaseType(entity.ObjectType))
        {
            await DiagnoseDatabaseAsync(entity, options, stages, cancellationToken);
        }
        else
        {
            var started = Stopwatch.GetTimestamp();
            var result = await TestDataSourceEntityAsync(entity, cancellationToken);
            stages.Add(CreateStage(
                "connection",
                result.Success ? "Passed" : "Failed",
                started,
                result.Message,
                result.Success ? null : "检查数据源端点、凭据和网络策略后重试。",
                result.DetailJson,
                result.Success ? null : "CONNECTION_FAILED"));
            AddNotApplicableStage(stages, "network", "非数据库数据源不执行数据库网络探测。", "NETWORK_NOT_APPLICABLE");
            AddNotApplicableStage(stages, "tls", "非数据库数据源不执行数据库 TLS 配置探测。", "TLS_NOT_APPLICABLE");
            AddNotApplicableStage(stages, "authentication", "认证已包含在数据源连接测试中。", "AUTHENTICATION_INCLUDED");
            AddNotApplicableStage(stages, "database", "当前数据源不是数据库。", "DATABASE_NOT_APPLICABLE");
            AddNotApplicableStage(stages, "permission", "当前数据源没有数据库目录权限探测。", "PERMISSION_NOT_APPLICABLE");
            AddNotApplicableStage(stages, "capability", "当前 provider 不提供数据库能力矩阵。", "CAPABILITY_NOT_APPLICABLE");
        }

        return (stages.All(item => item.Status is "Passed" or "NotApplicable"), stages);
    }

    private ApplicationDataSourceEntity BuildDraftEntity(ApplicationDataSourceDraftDiagnosticRequest request)
    {
        var objectType = ApplicationDataCenterCodePolicy.NormalizeCode(request.ObjectType, "对象类型");
        var configJson = ApplicationDataCenterJson.NormalizeObjectJson(request.ConfigJson, "配置");
        var secretConfigJson = string.IsNullOrWhiteSpace(request.SecretConfigJson)
            ? null
            : ApplicationDataCenterJson.NormalizeObjectJson(request.SecretConfigJson, "凭据");

        return new ApplicationDataSourceEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ObjectCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.ObjectCode, "对象编码"),
            ObjectName = ApplicationDataCenterCodePolicy.NormalizeName(request.ObjectName, "对象名称"),
            ObjectType = objectType,
            ConfigJson = configJson,
            SecretConfigCipherText = string.IsNullOrWhiteSpace(secretConfigJson) ? null : SecretProtector.Protect(secretConfigJson),
            Endpoint = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Endpoint, 1000),
            Environment = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Environment),
            Status = ApplicationDataCenterObjectStatus.Draft,
            IsDeleted = false
        };
    }

    private async Task DiagnoseDatabaseAsync(
        ApplicationDataSourceEntity entity,
        ApplicationDataSourceConnectionOptions options,
        List<ApplicationConnectionDiagnosticStageResponse> stages,
        CancellationToken cancellationToken)
    {
        var isLocal = string.Equals(entity.ObjectType, ApplicationDataSourceType.Sqlite, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(entity.ObjectType, ApplicationDataSourceType.ApplicationDatabase, StringComparison.OrdinalIgnoreCase);
        if (isLocal)
            AddNotApplicableStage(stages, "network", "SQLite 通过受控 sandbox 文件访问，不需要网络探测。", "NETWORK_NOT_APPLICABLE");
        else
            await AddNetworkStageAsync(options, stages, cancellationToken);

        if (stages.Any(item => item.Code == "network" && item.Status == "Failed"))
            return;

        AddTlsStage(entity, options, stages);
        if (stages.Any(item => item.Code == "tls" && item.Status == "Failed"))
        {
            AddBlockedStage(stages, "authentication", "TLS 策略不满足，未执行认证。", "AUTHENTICATION_BLOCKED");
            AddBlockedStage(stages, "database", "TLS 策略不满足，未执行数据库查询。", "DATABASE_BLOCKED");
            AddBlockedStage(stages, "permission", "TLS 策略不满足，未执行权限探测。", "PERMISSION_BLOCKED");
            AddBlockedStage(stages, "capability", "TLS 策略不满足，未执行能力探测。", "CAPABILITY_BLOCKED");
            return;
        }
        var connectionStarted = Stopwatch.GetTimestamp();
        try
        {
            using var db = await connectionFactory.CreateDatabaseClientAsync(entity, cancellationToken);
            stages.Add(CreateStage("authentication", "Passed", connectionStarted, "数据库连接与认证成功。", null, null, null));

            var databaseStarted = Stopwatch.GetTimestamp();
            _ = Convert.ToInt32(await ExecuteScalarAsync(db, "SELECT 1", [], cancellationToken) ?? 0);
            stages.Add(CreateStage("database", "Passed", databaseStarted, "数据库可执行基础查询。", null, null, null));

            var permissionStarted = Stopwatch.GetTimestamp();
            var provider = providerRegistry.Resolve(entity.ObjectType);
            if (provider is null)
            {
                stages.Add(CreateStage("permission", "Failed", permissionStarted, "未找到数据库 provider。", "检查 provider 注册和数据源类型。", null, "PROVIDER_NOT_FOUND"));
            }
            else
            {
                await ExecuteDataTableAsync(db, provider.Catalog.TablesSql, [], cancellationToken);
                stages.Add(CreateStage("permission", "Passed", permissionStarted, "数据库目录读取权限可用。", null, null, null));

                var capabilityStarted = Stopwatch.GetTimestamp();
                stages.Add(CreateStage("capability", "Passed", capabilityStarted, "Provider capability 探测完成。", null, JsonSerializer.Serialize(provider.Capability), null));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (stages.Any(item => item.Code == "authentication" && item.Status == "Passed"))
            {
                stages.Add(CreateStage("permission", "Failed", Stopwatch.GetTimestamp(), SanitizeDiagnosticMessage(exception), "检查数据库账号的目录读取权限。", null, "PERMISSION_PROBE_FAILED"));
                AddBlockedStage(stages, "capability", "权限探测失败，未确认完整能力。", "CAPABILITY_BLOCKED");
            }
            else
            {
                stages.Add(CreateStage("authentication", "Failed", connectionStarted, SanitizeDiagnosticMessage(exception), "检查用户名、密码、数据库地址和 TLS 配置。", null, "AUTHENTICATION_FAILED"));
                AddBlockedStage(stages, "database", "认证失败，未执行数据库查询。", "DATABASE_BLOCKED");
                AddBlockedStage(stages, "permission", "认证失败，未执行权限探测。", "PERMISSION_BLOCKED");
                AddBlockedStage(stages, "capability", "认证失败，未执行能力探测。", "CAPABILITY_BLOCKED");
            }
        }
    }

    private static void AddConfigurationStage(
        ApplicationDataSourceEntity entity,
        ApplicationDataSourceConnectionOptions options,
        List<ApplicationConnectionDiagnosticStageResponse> stages)
    {
        var started = Stopwatch.GetTimestamp();
        var valid = !string.IsNullOrWhiteSpace(entity.ObjectType) &&
                    (ApplicationDataSourceConnectionFactory.IsDatabaseType(entity.ObjectType) ||
                     !string.IsNullOrWhiteSpace(options.BaseUrl) || !string.IsNullOrWhiteSpace(options.FilePath));
        stages.Add(CreateStage("configuration", valid ? "Passed" : "Failed", started, valid ? "数据源配置字段完整。" : "数据源缺少 provider 或连接目标。", valid ? null : "按 provider 补充真实连接字段后重试。", null, valid ? null : "CONFIGURATION_INVALID"));
    }

    private static async Task AddNetworkStageAsync(
        ApplicationDataSourceConnectionOptions options,
        List<ApplicationConnectionDiagnosticStageResponse> stages,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (string.IsNullOrWhiteSpace(options.Host) || options.Port is null)
                throw new ValidationException("数据库网络地址或端口缺失。", ErrorCodes.ApplicationDataCenterInvalidConfig);
            using var client = new TcpClient();
            await client.ConnectAsync(options.Host, options.Port.Value, cancellationToken);
            stages.Add(CreateStage("network", "Passed", started, "数据库主机端口可达。", null, null, null));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            stages.Add(CreateStage("network", "Failed", started, SanitizeDiagnosticMessage(exception), "检查 DNS、防火墙、主机和端口。", null, "NETWORK_UNREACHABLE"));
            AddBlockedStage(stages, "tls", "网络不可达，未执行 TLS 配置检查。", "TLS_BLOCKED");
            AddBlockedStage(stages, "authentication", "网络不可达，未执行认证。", "AUTHENTICATION_BLOCKED");
            AddBlockedStage(stages, "database", "网络不可达，未执行数据库查询。", "DATABASE_BLOCKED");
            AddBlockedStage(stages, "permission", "网络不可达，未执行权限探测。", "PERMISSION_BLOCKED");
            AddBlockedStage(stages, "capability", "网络不可达，未执行能力探测。", "CAPABILITY_BLOCKED");
        }
    }

    private static void AddTlsStage(ApplicationDataSourceEntity entity, ApplicationDataSourceConnectionOptions options, List<ApplicationConnectionDiagnosticStageResponse> stages)
    {
        var started = Stopwatch.GetTimestamp();
        if (string.Equals(entity.ObjectType, ApplicationDataSourceType.SqlServer, StringComparison.OrdinalIgnoreCase) && options.Encrypt is false && options.TrustServerCertificate is not true)
        {
            stages.Add(CreateStage("tls", "Failed", started, "SQL Server 未启用加密且未明确接受服务器证书。", "启用 Encrypt；仅在审批通过的受控环境使用 TrustServerCertificate。", null, "TLS_POLICY_INVALID"));
            return;
        }

        stages.Add(CreateStage("tls", "Passed", started, "TLS/传输安全配置满足当前 provider 的安全策略。", null, JsonSerializer.Serialize(new { options.SslMode, options.Encrypt, options.TrustServerCertificate }), null));
    }

    private static void AddNotApplicableStage(List<ApplicationConnectionDiagnosticStageResponse> stages, string code, string message, string errorCode) =>
        stages.Add(CreateStage(code, "NotApplicable", Stopwatch.GetTimestamp(), message, null, null, errorCode));

    private static void AddBlockedStage(List<ApplicationConnectionDiagnosticStageResponse> stages, string code, string message, string errorCode) =>
        stages.Add(CreateStage(code, "Blocked", Stopwatch.GetTimestamp(), message, "修复前置阶段后重新执行诊断。", null, errorCode));

    private static ApplicationConnectionDiagnosticStageResponse CreateStage(string code, string status, long started, string message, string? suggestion, string? detail, string? errorCode) =>
        new(code, status, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, message, suggestion, errorCode is null ? detail : JsonSerializer.Serialize(new { errorCode, detail }));

    private static string SanitizeDiagnosticMessage(Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? "连接失败。" : exception.Message.Replace("Password", "[REDACTED]", StringComparison.OrdinalIgnoreCase);

    public async Task<ApplicationDataCenterActionResultResponse> TestDataSourceEntityAsync(
        ApplicationDataSourceEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (!ApplicationDataSourceProviderPolicy.IsSupported(entity.ObjectType))
        {
            var diagnostic = ApplicationDataSourceProviderPolicy.GetMigrationDiagnostic(entity.ObjectType);
            await MarkMigrationRequiredAsync(entity, cancellationToken);
            return new ApplicationDataCenterActionResultResponse(
                false,
                ApplicationDataCenterObjectStatus.MigrationRequired,
                diagnostic,
                0,
                "{}",
                TemplateCatalog.BuildNextActions(ModuleKey, entity.Id, ApplicationDataCenterObjectStatus.MigrationRequired));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var detail = entity.ObjectType switch
            {
                ApplicationDataSourceType.Sqlite or
                ApplicationDataSourceType.MySql or
                ApplicationDataSourceType.PostgreSql or
                ApplicationDataSourceType.SqlServer or
                ApplicationDataSourceType.ApplicationDatabase => await TestDatabaseAsync(entity, cancellationToken),
                ApplicationDataSourceType.Excel => TestFile(entity, "Excel 文件"),
                ApplicationDataSourceType.Csv => TestFile(entity, "CSV 文件"),
                _ => throw new ValidationException("不支持的数据源 Provider。", ErrorCodes.ApplicationDataCenterInvalidConfig)
            };
            stopwatch.Stop();
            await MarkValidationAsync(entity.Id, ApplicationDataCenterObjectStatus.Normal, "测试通过", detail, cancellationToken);
            return new ApplicationDataCenterActionResultResponse(
                true,
                ApplicationDataCenterObjectStatus.Normal,
                "测试通过",
                stopwatch.ElapsedMilliseconds,
                detail,
                TemplateCatalog.BuildNextActions(ModuleKey, entity.Id, entity.Status));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await MarkValidationAsync(entity.Id, ApplicationDataCenterObjectStatus.Error, ex.Message, null, cancellationToken);
            return new ApplicationDataCenterActionResultResponse(
                false,
                ApplicationDataCenterObjectStatus.Error,
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                "{}",
                TemplateCatalog.BuildNextActions(ModuleKey, entity.Id, entity.Status));
        }
    }

    public override async Task<ApplicationDataCenterPreviewResponse> PreviewAsync(
        string id,
        ApplicationDataCenterPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await EnsureEntityAsync(id, cancellationToken);
        await EnsureOperationalProviderAsync(entity, cancellationToken);
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var options = connectionFactory.Resolve(entity);
        if (ApplicationDataSourceConnectionFactory.IsDatabaseType(entity.ObjectType))
        {
            using var db = await connectionFactory.CreateDatabaseClientAsync(entity, cancellationToken);
            return await previewReader.PreviewDatabaseAsync(
                db,
                ReadString(config, "previewSql") ?? ReadString(config, "sql"),
                ReadString(config, "tableName"),
                request.MaxRows,
                cancellationToken);
        }

        if (string.Equals(entity.ObjectType, ApplicationDataSourceType.Excel, StringComparison.OrdinalIgnoreCase))
        {
            return previewReader.PreviewExcel(
                Required(options.FilePath, "Excel 文件路径不能为空"),
                ReadString(config, "sheetName"),
                ReadInt(config, "headerRow") ?? 1,
                ReadInt(config, "dataStartRow") ?? 2,
                request.MaxRows);
        }

        if (string.Equals(entity.ObjectType, ApplicationDataSourceType.Csv, StringComparison.OrdinalIgnoreCase))
        {
            return previewReader.PreviewCsv(
                Required(options.FilePath, "CSV 文件路径不能为空"),
                ReadString(config, "delimiter") ?? ",",
                ReadBoolean(config, "firstRowHeader", true),
                ReadInt(config, "dataStartRow") ?? 1,
                ResolveEncoding(ReadString(config, "encoding")),
                request.MaxRows);
        }

        return await base.PreviewAsync(id, request, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationDataSourceTableResponse>> GetTablesAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await EnsureEntityAsync(id, cancellationToken);
        await EnsureOperationalProviderAsync(entity, cancellationToken);
        if (!ApplicationDataSourceConnectionFactory.IsDatabaseType(entity.ObjectType))
        {
            throw new ValidationException("当前数据源不是数据库，无法读取表清单", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        using var db = await connectionFactory.CreateDatabaseClientAsync(entity, cancellationToken);
        return await ReadTablesAsync(db, providerRegistry.Resolve(entity.ObjectType), entity.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationDataSourceColumnResponse>> GetColumnsAsync(
        string id,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await EnsureEntityAsync(id, cancellationToken);
        await EnsureOperationalProviderAsync(entity, cancellationToken);
        if (!ApplicationDataSourceConnectionFactory.IsDatabaseType(entity.ObjectType))
        {
            throw new ValidationException("当前数据源不是数据库，无法读取字段清单", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var table = ParseQualifiedTableName(tableName);
        using var db = await connectionFactory.CreateDatabaseClientAsync(entity, cancellationToken);
        var provider = ResolveProvider(entity.ObjectType);
        cancellationToken.ThrowIfCancellationRequested();
        var dataTable = await ExecuteDataTableAsync(
            db,
            provider.Catalog.ColumnsSql,
            [new SugarParameter("@table", table.TableName), new SugarParameter("@schema", table.SchemaName)],
            cancellationToken);
        return dataTable.Rows.Cast<DataRow>()
            .Select(row =>
            {
                var columnName = ReadCell(row, "ColumnName");
                var tableResourceId = ApplicationDataResourceId.Table(id, table.SchemaName, table.TableName);
                return new ApplicationDataSourceColumnResponse(
                    columnName,
                    EmptyToNull(ReadCell(row, "DataType")) ?? "TEXT",
                    ReadBooleanCell(row, "Nullable"),
                    ReadBooleanCell(row, "PrimaryKey"),
                    ReadInt(row, "OrdinalPosition"))
                {
                    ResourceId = ApplicationDataResourceId.Column(tableResourceId, columnName)
                };
            })
            .OrderBy(item => item.Order)
            .ToArray();
    }

    private async Task<string> TestDatabaseAsync(
        ApplicationDataSourceEntity entity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = await connectionFactory.CreateDatabaseClientAsync(entity, cancellationToken);
        var started = Stopwatch.GetTimestamp();
        var value = Convert.ToInt32(await ExecuteScalarAsync(db, "SELECT 1", [], cancellationToken) ?? 0);
        var elapsed = Stopwatch.GetElapsedTime(started);
        return ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
        {
            ["selectOne"] = value,
            ["elapsedMs"] = elapsed.TotalMilliseconds
        });
    }

    private static async Task<IReadOnlyList<ApplicationDataSourceTableResponse>> ReadTablesAsync(ISqlSugarClient db, IApplicationDataSourceProvider provider, string dataSourceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataTable = await ExecuteDataTableAsync(db, provider.Catalog.TablesSql, [], cancellationToken);
        return dataTable.Rows.Cast<DataRow>()
            .Select(row =>
            {
                var tableName = ReadCell(row, "TableName");
                var schemaName = EmptyToNull(ReadCell(row, "SchemaName"));
                return new ApplicationDataSourceTableResponse(
                    tableName,
                    schemaName,
                    EmptyToNull(ReadCell(row, "TableType")) ?? "TABLE")
                {
                    ResourceId = ApplicationDataResourceId.Table(dataSourceId, schemaName, tableName)
                };
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<ApplicationDataSourceTableResponse>> ReadTablesAsync(ISqlSugarClient db, string sourceType, string dataSourceId, CancellationToken cancellationToken)
    {
        var dataTable = sourceType switch
        {
            ApplicationDataSourceType.Sqlite or ApplicationDataSourceType.ApplicationDatabase => await ExecuteDataTableAsync(
                db,
                "SELECT name AS TableName, NULL AS SchemaName, type AS TableType FROM sqlite_master WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%' ORDER BY name",
                [],
                cancellationToken),
            ApplicationDataSourceType.MySql => await ExecuteDataTableAsync(
                db,
                "SELECT TABLE_NAME AS TableName, TABLE_SCHEMA AS SchemaName, TABLE_TYPE AS TableType FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME",
                [],
                cancellationToken),
            ApplicationDataSourceType.PostgreSql => await ExecuteDataTableAsync(
                db,
                "SELECT table_name AS TableName, table_schema AS SchemaName, table_type AS TableType FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_schema, table_name",
                [],
                cancellationToken),
            ApplicationDataSourceType.SqlServer => await ExecuteDataTableAsync(
                db,
                "SELECT TABLE_NAME AS TableName, TABLE_SCHEMA AS SchemaName, TABLE_TYPE AS TableType FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_SCHEMA, TABLE_NAME",
                [],
                cancellationToken),
            _ => throw new ValidationException("当前数据源类型不支持读取表清单", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };

        return dataTable.Rows.Cast<DataRow>()
            .Select(row =>
            {
                var tableName = ReadCell(row, "TableName");
                var schemaName = EmptyToNull(ReadCell(row, "SchemaName"));
                return new ApplicationDataSourceTableResponse(
                    tableName,
                    schemaName,
                    EmptyToNull(ReadCell(row, "TableType")) ?? "TABLE")
                {
                    ResourceId = ApplicationDataResourceId.Table(dataSourceId, schemaName, tableName)
                };
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<ApplicationDataSourceColumnResponse>> ReadColumnsAsync(
        ISqlSugarClient db,
        string sourceType,
        string? schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var dataTable = sourceType switch
        {
            ApplicationDataSourceType.Sqlite or ApplicationDataSourceType.ApplicationDatabase => await ExecuteDataTableAsync(
                db,
                $"PRAGMA table_info({QuoteSqliteQualifiedName(schemaName, tableName)})",
                [],
                cancellationToken),
            ApplicationDataSourceType.MySql => await ExecuteDataTableAsync(
                db,
                ResolveMySqlColumnSql(schemaName),
                BuildTableParameters(schemaName, tableName),
                cancellationToken),
            ApplicationDataSourceType.PostgreSql => await ExecuteDataTableAsync(
                db,
                ResolvePostgreSqlColumnSql(schemaName),
                BuildTableParameters(schemaName ?? "public", tableName),
                cancellationToken),
            ApplicationDataSourceType.SqlServer => await ExecuteDataTableAsync(
                db,
                ResolveSqlServerColumnSql(schemaName),
                BuildTableParameters(schemaName ?? "dbo", tableName),
                cancellationToken),
            _ => throw new ValidationException("当前数据源类型不支持读取字段清单", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };

        if (sourceType is ApplicationDataSourceType.Sqlite or ApplicationDataSourceType.ApplicationDatabase)
        {
            return dataTable.Rows.Cast<DataRow>()
                .Select(row => new ApplicationDataSourceColumnResponse(
                    ReadCell(row, "name"),
                    EmptyToNull(ReadCell(row, "type")) ?? "TEXT",
                    ReadInt(row, "notnull") == 0,
                    ReadInt(row, "pk") > 0,
                    ReadInt(row, "cid") + 1))
                .ToArray();
        }

        return dataTable.Rows.Cast<DataRow>()
            .Select(row => new ApplicationDataSourceColumnResponse(
                ReadCell(row, "ColumnName"),
                EmptyToNull(ReadCell(row, "DataType")) ?? "Text",
                IsNullable(row),
                IsPrimaryKey(row),
                ReadInt(row, "OrdinalPosition")))
            .ToArray();
    }

    private static string ResolveMySqlColumnSql(string? schemaName) =>
        string.IsNullOrWhiteSpace(schemaName)
            ? "SELECT COLUMN_NAME AS ColumnName, DATA_TYPE AS DataType, IS_NULLABLE AS IsNullable, COLUMN_KEY AS ColumnKey, ORDINAL_POSITION AS OrdinalPosition FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName ORDER BY ORDINAL_POSITION"
            : "SELECT COLUMN_NAME AS ColumnName, DATA_TYPE AS DataType, IS_NULLABLE AS IsNullable, COLUMN_KEY AS ColumnKey, ORDINAL_POSITION AS OrdinalPosition FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @schemaName AND TABLE_NAME = @tableName ORDER BY ORDINAL_POSITION";

    private static string ResolvePostgreSqlColumnSql(string? schemaName) =>
        """
        SELECT c.column_name AS "ColumnName",
               c.data_type AS "DataType",
               c.is_nullable AS "IsNullable",
               c.ordinal_position AS "OrdinalPosition",
               CASE WHEN kcu.column_name IS NULL THEN 'NO' ELSE 'YES' END AS "IsPrimaryKey"
        FROM information_schema.columns c
        LEFT JOIN information_schema.table_constraints tc
          ON tc.table_schema = c.table_schema
         AND tc.table_name = c.table_name
         AND tc.constraint_type = 'PRIMARY KEY'
        LEFT JOIN information_schema.key_column_usage kcu
          ON kcu.constraint_schema = tc.constraint_schema
         AND kcu.constraint_name = tc.constraint_name
         AND kcu.table_schema = c.table_schema
         AND kcu.table_name = c.table_name
         AND kcu.column_name = c.column_name
        WHERE c.table_schema = @schemaName AND c.table_name = @tableName
        ORDER BY c.ordinal_position
        """;

    private static string ResolveSqlServerColumnSql(string? schemaName) =>
        """
        SELECT c.COLUMN_NAME AS ColumnName,
               c.DATA_TYPE AS DataType,
               c.IS_NULLABLE AS IsNullable,
               c.ORDINAL_POSITION AS OrdinalPosition,
               CASE WHEN kcu.COLUMN_NAME IS NULL THEN 'NO' ELSE 'YES' END AS IsPrimaryKey
        FROM INFORMATION_SCHEMA.COLUMNS c
        LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
          ON tc.TABLE_SCHEMA = c.TABLE_SCHEMA
         AND tc.TABLE_NAME = c.TABLE_NAME
         AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
        LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
          ON kcu.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
         AND kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
         AND kcu.TABLE_SCHEMA = c.TABLE_SCHEMA
         AND kcu.TABLE_NAME = c.TABLE_NAME
         AND kcu.COLUMN_NAME = c.COLUMN_NAME
        WHERE c.TABLE_SCHEMA = @schemaName AND c.TABLE_NAME = @tableName
        ORDER BY c.ORDINAL_POSITION
        """;

    private static SugarParameter[] BuildTableParameters(string? schemaName, string tableName) =>
        string.IsNullOrWhiteSpace(schemaName)
            ? [new SugarParameter("@tableName", tableName)]
            : [new SugarParameter("@schemaName", schemaName), new SugarParameter("@tableName", tableName)];

    private static (string? SchemaName, string TableName) ParseQualifiedTableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("表名不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var parts = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2 || parts.Any(part => !IdentifierRegex.IsMatch(part)))
        {
            throw new ValidationException("表名只能包含字母、数字、下划线，并可使用 schema.table 格式", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return parts.Length == 1 ? (null, parts[0]) : (parts[0], parts[1]);
    }

    private static string QuoteSqliteQualifiedName(string? schemaName, string tableName) =>
        string.IsNullOrWhiteSpace(schemaName)
            ? QuoteSqliteIdentifier(tableName)
            : $"{QuoteSqliteIdentifier(schemaName)}.{QuoteSqliteIdentifier(tableName)}";

    private static string QuoteSqliteIdentifier(string value) => $"\"{value}\"";

    private static bool IsNullable(DataRow row) =>
        ReadCell(row, "IsNullable").Equals("YES", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrimaryKey(DataRow row)
    {
        var value = ReadCell(row, "IsPrimaryKey");
        if (string.IsNullOrWhiteSpace(value))
        {
            value = ReadCell(row, "ColumnKey");
        }

        return value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("PRI", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value == "1";
    }

    private static string ReadCell(DataRow row, string columnName) =>
        row.Table.Columns.Contains(columnName) && row[columnName] is not DBNull and not null
            ? row[columnName]?.ToString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(DataRow row, string columnName) =>
        int.TryParse(ReadCell(row, columnName), out var value) ? value : 0;

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string TestFile(ApplicationDataSourceEntity entity, string displayName)
    {
        var options = connectionFactory.Resolve(entity);
        var filePath = Required(options.FilePath, $"{displayName}路径不能为空");
        var exists = File.Exists(filePath);
        if (!exists)
        {
            throw new FileNotFoundException($"{displayName}不存在", filePath);
        }

        var info = new FileInfo(filePath);
        return ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["length"] = info.Length,
            ["lastWriteTime"] = info.LastWriteTimeUtc
        });
    }

    private static string? ResolveEndpoint(string type, IReadOnlyDictionary<string, object?> config)
    {
        if (ApplicationDataSourceConnectionFactory.IsDatabaseType(type))
        {
            var host = ReadString(config, "host");
            var database = ReadString(config, "database") ?? ReadString(config, "databaseName");
            return string.IsNullOrWhiteSpace(host) ? database : $"{host}/{database}";
        }

        return ReadString(config, "baseUrl") ?? ReadString(config, "filePath") ?? ReadString(config, "endpoint");
    }

    private static string Required(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return value.Trim();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static async Task<object?> ExecuteScalarAsync(
        ISqlSugarClient db,
        string sql,
        IReadOnlyList<SugarParameter> parameters,
        CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("当前数据源不支持异步结果读取");
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        AddParameters(command, parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<DataTable> ExecuteDataTableAsync(
        ISqlSugarClient db,
        string sql,
        IReadOnlyList<SugarParameter> parameters,
        CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("当前数据源不支持异步结果读取");
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        for (var index = 0; index < reader.FieldCount; index++)
            table.Columns.Add(reader.GetName(index), reader.GetFieldType(index));
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = table.NewRow();
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = await reader.IsDBNullAsync(index, cancellationToken) ? DBNull.Value : reader.GetValue(index);
            table.Rows.Add(row);
        }

        return table;
    }

    private static void AddParameters(DbCommand command, IReadOnlyList<SugarParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.ParameterName;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }
    }

    protected override async Task ValidateForPublishAsync(ApplicationDataSourceEntity entity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ApplicationDataSourceProviderPolicy.IsSupported(entity.ObjectType))
        {
            await MarkMigrationRequiredAsync(entity, cancellationToken);
            ApplicationDataSourceProviderPolicy.EnsureSupportedForWrite(entity.ObjectType);
        }

        ApplicationDataSourceProviderPolicy.EnsureSupportedForWrite(entity.ObjectType);
        var currentFingerprint = ApplicationDataSourceConnectionFingerprint.Compute(entity);
        if (!string.Equals(entity.LastValidationStatus, "Normal", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(entity.LastValidationFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("连接诊断已失效，请先完成当前配置的连接诊断。", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

    }

    private async Task EnsureOperationalProviderAsync(ApplicationDataSourceEntity entity, CancellationToken cancellationToken)
    {
        if (ApplicationDataSourceProviderPolicy.IsSupported(entity.ObjectType))
            return;

        await MarkMigrationRequiredAsync(entity, cancellationToken);
        ApplicationDataSourceProviderPolicy.EnsureSupportedForWrite(entity.ObjectType);
    }

    private async Task MarkMigrationRequiredAsync(ApplicationDataSourceEntity entity, CancellationToken cancellationToken)
    {
        if (string.Equals(entity.Status, ApplicationDataCenterObjectStatus.MigrationRequired, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entity.LastValidationStatus, ApplicationDataCenterObjectStatus.MigrationRequired, StringComparison.OrdinalIgnoreCase))
            return;

        entity.Status = ApplicationDataCenterObjectStatus.MigrationRequired;
        entity.LastValidationStatus = ApplicationDataCenterObjectStatus.MigrationRequired;
        entity.LastValidationMessage = ApplicationDataSourceProviderPolicy.GetMigrationDiagnostic(entity.ObjectType);
        entity.LastValidatedAt = DateTime.UtcNow;
        entity.LastValidationFingerprint = null;
        entity.UpdatedBy = WorkspaceResolver.Resolve().UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await Repository.UpdateAsync(entity, cancellationToken);
    }

    private IApplicationDataSourceProvider ResolveProvider(string sourceType) => providerRegistry.Resolve(sourceType);

    private static bool ReadBooleanCell(DataRow row, string columnName)
    {
        var value = ReadCell(row, columnName);
        return value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("PRI", StringComparison.OrdinalIgnoreCase) ||
               value == "1";
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> config, string key) =>
        int.TryParse(ReadString(config, key), out var parsed) ? parsed : null;

    private static bool ReadBoolean(IReadOnlyDictionary<string, object?> config, string key, bool defaultValue = false)
    {
        var value = ReadString(config, key);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static Encoding ResolveEncoding(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Encoding.UTF8;
        }

        return value.Trim().Equals("unicode", StringComparison.OrdinalIgnoreCase)
            ? Encoding.Unicode
            : Encoding.UTF8;
    }
}
