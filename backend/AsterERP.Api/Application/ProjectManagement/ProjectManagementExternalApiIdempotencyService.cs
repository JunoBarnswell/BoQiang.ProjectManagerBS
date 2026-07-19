using System.Security.Cryptography;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>以租户、SYSTEM 工作区、调用用户、操作和幂等键为边界的持久化写入回放服务。</summary>
public sealed class ProjectManagementExternalApiIdempotencyService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementExternalApiIdempotencyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProjectManagementExternalApiIdempotencyResult<T>> ExecuteAsync<T>(
        ProjectManagementExternalApiIdempotencyRequest request,
        Func<CancellationToken, Task<T>> action,
        Func<T, ProjectManagementExternalApiResource> resourceSelector,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var existing = await FindAsync(normalized.Operation, normalized.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return Replay<T>(existing, normalized.RequestHash);

        var now = DateTime.UtcNow;
        var entity = new ProjectManagementExternalApiRequestEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = Tenant(), AppCode = App(), CallerUserId = User(), Source = normalized.Source,
            Operation = normalized.Operation, IdempotencyKey = normalized.IdempotencyKey, RequestHash = normalized.RequestHash,
            Status = "Pending", TraceId = normalized.TraceId, CreatedBy = User(), CreatedTime = now
        };

        try
        {
            await databaseAccessor.GetProjectManagementDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        catch (Exception)
        {
            existing = await FindAsync(normalized.Operation, normalized.IdempotencyKey, cancellationToken);
            if (existing is not null) return Replay<T>(existing, normalized.RequestHash);
            throw;
        }

        try
        {
            var result = await action(cancellationToken);
            var resource = resourceSelector(result);
            entity.Status = "Succeeded";
            entity.ProjectId = Required(resource.ProjectId, "项目标识不能为空");
            entity.AggregateType = Required(resource.AggregateType, "聚合类型不能为空");
            entity.AggregateId = Required(resource.AggregateId, "聚合标识不能为空");
            entity.ResultJson = JsonSerializer.Serialize(result, JsonOptions);
            entity.CompletedTime = DateTime.UtcNow;
            entity.UpdatedBy = User();
            entity.UpdatedTime = entity.CompletedTime;
            await databaseAccessor.GetProjectManagementDb().Updateable(entity)
                .Where(item => item.Id == entity.Id && item.Status == "Pending" && !item.IsDeleted)
                .ExecuteCommandAsync(cancellationToken);
            return new ProjectManagementExternalApiIdempotencyResult<T>(result, false);
        }
        catch (Exception exception)
        {
            entity.Status = "Failed";
            entity.ErrorCode = exception is ValidationException validation ? validation.Code : ErrorCodes.InternalError;
            entity.ErrorMessage = Truncate(exception.Message);
            entity.CompletedTime = DateTime.UtcNow;
            entity.UpdatedBy = User();
            entity.UpdatedTime = entity.CompletedTime;
            await databaseAccessor.GetProjectManagementDb().Updateable(entity)
                .Where(item => item.Id == entity.Id && item.Status == "Pending" && !item.IsDeleted)
                .ExecuteCommandAsync(CancellationToken.None);
            throw;
        }
    }

    private ProjectManagementExternalApiIdempotencyResult<T> Replay<T>(ProjectManagementExternalApiRequestEntity entity, string requestHash)
    {
        if (!string.Equals(entity.RequestHash, requestHash, StringComparison.Ordinal))
            throw new ProjectManagementExternalApiIdempotencyConflictException("同一 Idempotency-Key 不能用于不同请求");
        if (entity.Status == "Pending")
            throw new ProjectManagementExternalApiIdempotencyConflictException("该幂等请求正在处理，请稍后重试");
        if (entity.Status == "Failed")
            throw new ProjectManagementExternalApiIdempotencyConflictException($"该幂等请求已失败：{entity.ErrorMessage ?? "未知错误"}");
        if (string.IsNullOrWhiteSpace(entity.ResultJson))
            throw new ProjectManagementExternalApiIdempotencyConflictException("幂等请求结果不可回放");
        var result = JsonSerializer.Deserialize<T>(entity.ResultJson, JsonOptions)
            ?? throw new ProjectManagementExternalApiIdempotencyConflictException("幂等请求结果不可回放");
        return new ProjectManagementExternalApiIdempotencyResult<T>(result, true);
    }

    private async Task<ProjectManagementExternalApiRequestEntity?> FindAsync(string operation, string key, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementExternalApiRequestEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.CallerUserId == User() && item.Operation == operation && item.IdempotencyKey == key && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc).Take(1).ToListAsync(cancellationToken);
        return rows.FirstOrDefault();
    }

    private static ProjectManagementExternalApiIdempotencyRequest Normalize(ProjectManagementExternalApiIdempotencyRequest request)
    {
        var key = Required(request.IdempotencyKey, "Idempotency-Key 不能为空");
        if (key.Length is < 8 or > 160) throw new ValidationException("Idempotency-Key 长度必须在 8 到 160 个字符之间", ErrorCodes.ParameterInvalid);
        return request with
        {
            Operation = Required(request.Operation, "操作标识不能为空"),
            IdempotencyKey = key,
            RequestHash = Required(request.RequestHash, "请求摘要不能为空"),
            Source = NormalizeSource(request.Source),
            TraceId = Required(request.TraceId, "TraceId 不能为空")
        };
    }

    internal static string HashPayload(string value) => Convert.ToHexString(SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(value)));
    private static string NormalizeSource(string? source)
    {
        var normalized = string.IsNullOrWhiteSpace(source) ? "external-api" : source.Trim();
        if (normalized.Length > 128) throw new ValidationException("X-Integration-Source 不能超过 128 个字符", ErrorCodes.ParameterInvalid);
        return normalized;
    }
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message, ErrorCodes.ParameterInvalid) : value.Trim();
    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
}
