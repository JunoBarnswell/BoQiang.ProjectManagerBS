using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceSqlitePathApprovalService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ICurrentUser currentUser,
    ApplicationDataCenterSqlScriptAuditWriter? auditWriter = null)
{
    private const string Pending = "Pending";
    private const string Approved = "Approved";
    private const string Rejected = "Rejected";
    private const string Revoked = "Revoked";
    private const string RequestPermission = PermissionCodes.AppDataCenterDataSourceEdit;
    private const string ViewPermission = PermissionCodes.AppDataCenterDataSourceView;
    private const string ApprovalPermission = PermissionCodes.AppDataCenterDataSourcePublish;
    private static readonly TimeSpan MaximumApprovalLifetime = TimeSpan.FromDays(7);

    public async Task<IReadOnlyList<ApplicationDataSourceSqlitePathApprovalResponse>> ListAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        RequirePermission(ViewPermission);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await RequireBoundSqliteDataSourceAsync(db, workspace, dataSourceId, cancellationToken);
        var normalizedDataSourceId = dataSourceId.Trim();
        var entities = await db.Queryable<ApplicationDataSourceSqlitePathApprovalEntity>()
            .Where(item => item.DataSourceId == normalizedDataSourceId && !item.IsDeleted)
            .OrderBy(item => item.RequestedAt, SqlSugar.OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<ApplicationDataSourceSqlitePathApprovalResponse> RequestAsync(
        ApplicationDataSourceSqlitePathApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePermission(RequestPermission);
        var workspace = workspaceResolver.Resolve();
        var path = NormalizeAbsolutePath(request.Path);
        ValidateRequest(request.Reason, request.ExpiresAt);
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await RequireBoundSqliteDataSourceAsync(db, workspace, request.DataSourceId, cancellationToken);

        var duplicate = await db.Queryable<ApplicationDataSourceSqlitePathApprovalEntity>()
            .Where(item => item.DataSourceId == request.DataSourceId &&
                item.Path == path &&
                (item.Status == Pending || item.Status == Approved) &&
                item.ExpiresAt > DateTime.UtcNow)
            .AnyAsync(cancellationToken);
        if (duplicate)
            throw new ValidationException("该 SQLite 路径已有待审批或有效批准", ErrorCodes.ApplicationDataCenterRiskConfirmationRequired);

        var entity = new ApplicationDataSourceSqlitePathApprovalEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            DataSourceId = request.DataSourceId.Trim(),
            Path = path,
            Reason = request.Reason.Trim(),
            Status = Pending,
            RequestedBy = RequiredUserId(),
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt.ToUniversalTime()
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync(db, entity, "Requested", entity.RequestedBy, cancellationToken);
        return Map(entity);
    }

    public async Task<ApplicationDataSourceSqlitePathApprovalResponse> ApproveAsync(
        string dataSourceId,
        ApplicationDataSourceSqlitePathApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePermission(ApprovalPermission);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await RequireBoundSqliteDataSourceAsync(db, workspace, dataSourceId, cancellationToken);
        var entity = await RequireApprovalAsync(db, workspace, dataSourceId, request.ApprovalId, cancellationToken);
        if (!string.Equals(entity.Status, Pending, StringComparison.Ordinal))
            throw new ValidationException("SQLite 路径审批不在待审批状态", ErrorCodes.ApplicationDataCenterRiskConfirmationRequired);
        if (string.Equals(entity.RequestedBy, RequiredUserId(), StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("SQLite 路径审批必须由不同用户批准", ErrorCodes.PermissionDenied);
        if (entity.ExpiresAt <= DateTime.UtcNow)
            throw new ValidationException("SQLite 路径审批已过期", ErrorCodes.PermissionDenied);

        entity.Status = Approved;
        entity.ApprovedBy = RequiredUserId();
        entity.ApprovedAt = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync(db, entity, "Approved", entity.ApprovedBy, cancellationToken);
        return Map(entity);
    }

    public async Task<ApplicationDataSourceSqlitePathApprovalResponse> RejectAsync(
        string dataSourceId,
        ApplicationDataSourceSqlitePathApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePermission(ApprovalPermission);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await RequireBoundSqliteDataSourceAsync(db, workspace, dataSourceId, cancellationToken);
        var entity = await RequireApprovalAsync(db, workspace, dataSourceId, request.ApprovalId, cancellationToken);
        if (!string.Equals(entity.Status, Pending, StringComparison.Ordinal))
            throw new ValidationException("SQLite 路径审批不在待审批状态", ErrorCodes.ApplicationDataCenterRiskConfirmationRequired);

        entity.Status = Rejected;
        entity.RevokedBy = RequiredUserId();
        entity.RevokedAt = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync(db, entity, "Rejected", entity.RevokedBy, cancellationToken);
        return Map(entity);
    }

    public async Task<ApplicationDataSourceSqlitePathApprovalResponse> RevokeAsync(
        string dataSourceId,
        ApplicationDataSourceSqlitePathApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePermission(ApprovalPermission);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await RequireBoundSqliteDataSourceAsync(db, workspace, dataSourceId, cancellationToken);
        var entity = await RequireApprovalAsync(db, workspace, dataSourceId, request.ApprovalId, cancellationToken);
        if (!string.Equals(entity.Status, Approved, StringComparison.Ordinal))
            throw new ValidationException("SQLite 路径审批不在有效状态", ErrorCodes.ApplicationDataCenterRiskConfirmationRequired);

        entity.Status = Revoked;
        entity.RevokedBy = RequiredUserId();
        entity.RevokedAt = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync(db, entity, "Revoked", entity.RevokedBy, cancellationToken);
        return Map(entity);
    }

    public async Task RequireActiveAsync(
        string dataSourceId,
        string absolutePath,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var normalizedPath = NormalizeAbsolutePath(absolutePath);
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var approval = await db.Queryable<ApplicationDataSourceSqlitePathApprovalEntity>()
            .Where(item => item.DataSourceId == dataSourceId &&
                item.Path == normalizedPath &&
                item.Status == Approved &&
                item.ExpiresAt > DateTime.UtcNow)
            .OrderBy(item => item.ExpiresAt, SqlSugar.OrderByType.Desc)
            .FirstAsync(cancellationToken);
        if (approval is not null)
        {
            EnsureWorkspaceBinding(approval, workspace);
            return;
        }

        await WriteDeniedAuditAsync(db, workspace, dataSourceId, normalizedPath, cancellationToken);
        throw new ValidationException("SQLite 外部路径没有有效的租户应用审批", ErrorCodes.PermissionDenied);
    }

    private async Task<ApplicationDataSourceSqlitePathApprovalEntity> RequireApprovalAsync(
        SqlSugar.ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string dataSourceId,
        string approvalId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dataSourceId))
            throw new ValidationException("数据源编号不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
        if (string.IsNullOrWhiteSpace(approvalId))
            throw new ValidationException("审批编号不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var normalizedDataSourceId = dataSourceId.Trim();
        var entity = await db.Queryable<ApplicationDataSourceSqlitePathApprovalEntity>()
            .Where(item => item.Id == approvalId.Trim() && item.DataSourceId == normalizedDataSourceId && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("SQLite 路径审批不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
        EnsureWorkspaceBinding(entity, workspace);
        return entity;
    }

    private static async Task RequireBoundSqliteDataSourceAsync(
        SqlSugar.ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string dataSourceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dataSourceId))
            throw new ValidationException("数据源编号不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var source = await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.Id == dataSourceId.Trim() && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
        if (!string.Equals(source.TenantId, workspace.TenantId, StringComparison.Ordinal) ||
            !string.Equals(source.AppCode, workspace.AppCode, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("数据源不属于当前租户应用", ErrorCodes.PermissionDenied);
        if (!string.Equals(source.ObjectType, ApplicationDataSourceType.Sqlite, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(source.ObjectType, ApplicationDataSourceType.ApplicationDatabase, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("只有 SQLite 数据源支持路径例外审批", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private async Task WriteAuditAsync(
        SqlSugar.ISqlSugarClient db,
        ApplicationDataSourceSqlitePathApprovalEntity entity,
        string action,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        await db.Insertable(new ApplicationDataSourceSqlitePathApprovalAuditEntity
        {
            TenantId = entity.TenantId,
            AppCode = entity.AppCode,
            ApprovalId = entity.Id,
            DataSourceId = entity.DataSourceId,
            Action = action,
            ActorUserId = actorUserId,
            Path = entity.Path,
            Reason = entity.Reason,
            Status = entity.Status,
            OccurredAt = DateTime.UtcNow
        }).ExecuteCommandAsync(cancellationToken);

        if (auditWriter is null)
            return;

        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{entity.TenantId}:{entity.AppCode}:{entity.DataSourceId}:{entity.Path}:{action}"))).ToLowerInvariant();
        await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
        {
            TraceId = Guid.NewGuid().ToString("N"),
            SourceKind = "SqlitePathApproval",
            SourceId = entity.Id,
            SourceName = "SQLite path approval",
            DataSourceId = entity.DataSourceId,
            ScriptHash = requestHash,
            RequestHash = requestHash,
            ScriptPreview = "SQLite path approval state changed",
            StatementSummary = action,
            RiskSummary = "sqlite-path",
            Operation = "sqlite.path.approval",
            ResourceKind = "sqlite.path",
            PermissionCode = action == "Requested" ? RequestPermission : ApprovalPermission,
            Outcome = "Succeeded",
            Provider = ApplicationDataSourceType.Sqlite,
            TimeoutMs = 30_000,
            IsSuccess = true,
            RedactedDetailsJson = $"{{\"approvalId\":\"{entity.Id}\",\"status\":\"{entity.Status}\",\"path\":\"[REDACTED]\"}}"
        }, CancellationToken.None);
    }

    private async Task WriteDeniedAuditAsync(
        SqlSugar.ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string dataSourceId,
        string path,
        CancellationToken cancellationToken)
    {
        await db.Insertable(new ApplicationDataSourceSqlitePathApprovalAuditEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ApprovalId = string.Empty,
            DataSourceId = dataSourceId,
            Action = "AccessDenied",
            ActorUserId = workspace.UserId,
            Path = path,
            Reason = "没有有效的 SQLite 外部路径审批",
            Status = "Denied",
            OccurredAt = DateTime.UtcNow
        }).ExecuteCommandAsync(cancellationToken);

        if (auditWriter is not null)
        {
            var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{workspace.TenantId}:{workspace.AppCode}:{dataSourceId}:{path}"))).ToLowerInvariant();
            await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
            {
                TraceId = Guid.NewGuid().ToString("N"),
                SourceKind = "SqlitePathApproval",
                SourceId = dataSourceId,
                SourceName = "SQLite path access",
                DataSourceId = dataSourceId,
                ScriptHash = requestHash,
                RequestHash = requestHash,
                ScriptPreview = "SQLite path access denied",
                StatementSummary = "AccessDenied",
                RiskSummary = "sqlite-path",
                Operation = "sqlite.path.access",
                ResourceKind = "sqlite.path",
                PermissionCode = PermissionCodes.AppDataCenterDataSourceEdit,
                Outcome = "Denied",
                FailureCode = ErrorCodes.PermissionDenied.ToString(),
                Provider = ApplicationDataSourceType.Sqlite,
                TimeoutMs = 30_000,
                IsSuccess = false,
                ErrorMessage = "No active approval",
                RedactedDetailsJson = "{\"pathRecorded\":true,\"pathValue\":\"[REDACTED]\"}"
            }, CancellationToken.None);
        }
    }

    private static string NormalizeAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            throw new ValidationException("SQLite 例外审批必须使用绝对路径", ErrorCodes.ApplicationDataCenterInvalidConfig);
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ValidationException("SQLite 路径格式无效", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static void ValidateRequest(string reason, DateTime expiresAt)
    {
        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            throw new ValidationException("SQLite 路径审批原因至少需要 10 个字符", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var expiry = expiresAt.ToUniversalTime();
        if (expiry <= now || expiry > now.Add(MaximumApprovalLifetime))
            throw new ValidationException("SQLite 路径审批有效期必须在当前时间至 7 天内", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private string RequiredUserId() =>
        !string.IsNullOrWhiteSpace(currentUser.GetAsterErpUserId())
            ? currentUser.GetAsterErpUserId()
            : throw new ValidationException("当前用户不能为空", ErrorCodes.AuthenticationRequired);

    private void RequirePermission(string permission)
    {
        if (!currentUser.IsAsterErpAuthenticated() || !currentUser.HasAsterErpPermission(permission))
            throw new ValidationException("当前用户没有 SQLite 路径审批权限", ErrorCodes.PermissionDenied);
    }

    private static void EnsureWorkspaceBinding(ApplicationDataSourceSqlitePathApprovalEntity entity, ApplicationDataCenterWorkspace workspace)
    {
        if (!string.Equals(entity.TenantId, workspace.TenantId, StringComparison.Ordinal) ||
            !string.Equals(entity.AppCode, workspace.AppCode, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("SQLite 路径审批不属于当前租户应用", ErrorCodes.PermissionDenied);
    }

    private static ApplicationDataSourceSqlitePathApprovalResponse Map(ApplicationDataSourceSqlitePathApprovalEntity entity) =>
        new(entity.Id, entity.DataSourceId, entity.Path, entity.Reason, entity.Status, entity.RequestedBy,
            entity.RequestedAt, entity.ApprovedBy, entity.ApprovedAt, entity.ExpiresAt, entity.RevokedAt);
}
