using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Scheduling;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Hangfire;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementWebhookService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    IApplicationDataSecretProtector secretProtector,
    IHttpClientFactory httpClientFactory,
    IBackgroundJobManager backgroundJobManager,
    IBackgroundJobClient backgroundJobClient) : IProjectManagementWebhookService
{
    private const string OperationType = "webhook.delivery";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ProjectManagementWebhookSubscriptionResponse>> GetSubscriptionsAsync(string projectId, CancellationToken ct = default)
    {
        RequireWorkspace(); await accessPolicy.EnsureCanViewProjectAsync(Required(projectId), ct);
        var rows = await Db().Queryable<ProjectManagementWebhookSubscriptionEntity>().Where(x => x.ProjectId == projectId && !x.IsDeleted).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<ProjectManagementWebhookSubscriptionResponse> SaveSubscriptionAsync(ProjectManagementWebhookSubscriptionUpsertRequest request, CancellationToken ct = default)
    {
        RequireWorkspace(); ArgumentNullException.ThrowIfNull(request); Validate(request);
        await accessPolicy.EnsureCanManageProjectAsync(request.ProjectId.Trim(), ct);
        var db = Db(); var now = DateTime.UtcNow;
        var entity = string.IsNullOrWhiteSpace(request.Id) ? null : (await db.Queryable<ProjectManagementWebhookSubscriptionEntity>().Where(x => x.Id == request.Id && !x.IsDeleted).Take(1).ToListAsync(ct)).FirstOrDefault();
        if (entity is null)
        {
            if (string.IsNullOrWhiteSpace(request.Secret)) throw new ValidationException("新建 WebHook 订阅必须提供密钥");
            entity = new ProjectManagementWebhookSubscriptionEntity { Id = Guid.NewGuid().ToString("N"), TenantId = Tenant(), AppCode = App(), ProjectId = request.ProjectId.Trim(), OwnerUserId = User(), CreatedBy = User(), CreatedTime = now };
            entity.SecretCipherText = secretProtector.Protect(request.Secret.Trim());
            await db.Insertable(entity).ExecuteCommandAsync(ct);
        }
        else { await accessPolicy.EnsureCanManageProjectAsync(entity.ProjectId, ct); if (!string.IsNullOrWhiteSpace(request.Secret)) entity.SecretCipherText = secretProtector.Protect(request.Secret.Trim()); }
        entity.Name = request.Name.Trim(); entity.EndpointUrl = request.EndpointUrl.Trim(); entity.EventTypesJson = JsonSerializer.Serialize(request.EventTypes.Distinct(StringComparer.Ordinal).Order(), JsonOptions); entity.IsEnabled = request.IsEnabled; entity.MaxAttempts = request.MaxAttempts; entity.UpdatedBy = User(); entity.UpdatedTime = now;
        await db.Updateable(entity).UpdateColumns(x => new { x.Name, x.EndpointUrl, x.SecretCipherText, x.EventTypesJson, x.IsEnabled, x.MaxAttempts, x.UpdatedBy, x.UpdatedTime }).ExecuteCommandAsync(ct);
        return Map(entity);
    }

    public async Task DeleteSubscriptionAsync(string id, CancellationToken ct = default)
    {
        RequireWorkspace(); var entity = await GetSubscriptionAsync(id, ct); await accessPolicy.EnsureCanManageProjectAsync(entity.ProjectId, ct);
        await Db().Updateable<ProjectManagementWebhookSubscriptionEntity>().SetColumns(x => new ProjectManagementWebhookSubscriptionEntity { IsDeleted = true, DeletedBy = User(), DeletedTime = DateTime.UtcNow }).Where(x => x.Id == entity.Id).ExecuteCommandAsync(ct);
    }

    public async Task<GridPageResult<ProjectManagementWebhookDeliveryResponse>> GetDeliveriesAsync(string projectId, GridQuery query, CancellationToken ct = default)
    {
        RequireWorkspace(); await accessPolicy.EnsureCanViewProjectAsync(Required(projectId), ct); var total = new RefAsync<int>();
        var rows = await Db().Queryable<ProjectManagementOperationEntity>().Where(x => x.OperationType == OperationType && x.ImpactJson.Contains($"\"projectId\":\"{projectId}\"") && !x.IsDeleted).OrderBy(x => x.CreatedTime, OrderByType.Desc).ToPageListAsync(Math.Max(1, query.PageIndex), Math.Clamp(query.PageSize, 1, 100), total, ct);
        return new GridPageResult<ProjectManagementWebhookDeliveryResponse> { Total = total.Value, Items = rows.Select(MapDelivery).ToList() };
    }

    public async Task<ProjectManagementWebhookDeliveryResponse> ReplayAsync(string eventId, ProjectManagementWebhookReplayRequest request, CancellationToken ct = default)
    {
        RequireWorkspace(); var operation = await GetDeliveryAsync(eventId, ct) ?? throw new ValidationException("WebHook 投递不存在"); var envelope = ReadEnvelope(operation); await accessPolicy.EnsureCanManageProjectAsync(envelope.Payload.ProjectId, ct);
        var now = DateTime.UtcNow; envelope = envelope with { AttemptCount = 0, NextAttemptAt = now, LastError = null };
        await Db().Updateable<ProjectManagementOperationEntity>().SetColumns(x => new ProjectManagementOperationEntity { Status = "Pending", Phase = "ReplayQueued", ErrorMessage = null, CompletedTime = null, ImpactJson = JsonSerializer.Serialize(envelope, JsonOptions), VersionNo = x.VersionNo + 1, UpdatedBy = User(), UpdatedTime = now }).Where(x => x.Id == operation.Id).ExecuteCommandAsync(ct);
        await EnqueueAsync(operation.Id, operation.ActorUserId, ct); return MapDelivery((await GetDeliveryAsync(operation.Id, ct))!);
    }

    public async Task PublishActivityAsync(ProjectManagementActivityEvent activity, CancellationToken ct = default)
    {
        var eventType = ToEventType(activity); if (eventType is null || string.IsNullOrWhiteSpace(activity.ProjectId)) return;
        var subscriptions = await Db().Queryable<ProjectManagementWebhookSubscriptionEntity>().Where(x => x.ProjectId == activity.ProjectId && x.IsEnabled && !x.IsDeleted).ToListAsync(ct);
        foreach (var subscription in subscriptions.Where(x => ReadEvents(x).Contains(eventType)))
        {
            var eventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{activity.TraceId}|{subscription.Id}|{activity.ActivityType}|{activity.AggregateId}"))).ToLowerInvariant();
            if (await Db().Queryable<ProjectManagementOperationEntity>().AnyAsync(x => x.Id == eventId && !x.IsDeleted, ct)) continue;
            var payload = new ProjectManagementWebhookEventPayload(eventId, eventType, new DateTimeOffset(activity.OccurredAt ?? DateTime.UtcNow, TimeSpan.Zero), activity.ProjectId!, activity.AggregateType, activity.AggregateId, activity.TraceId, new Dictionary<string, string?> { ["activityType"] = activity.ActivityType, ["source"] = activity.Source });
            var envelope = new DeliveryEnvelope(payload, subscription.Id, subscription.EndpointUrl, subscription.SecretCipherText, 0, subscription.MaxAttempts, DateTime.UtcNow, null);
            await Db().Insertable(new ProjectManagementOperationEntity { Id = eventId, TenantId = activity.TenantId, AppCode = activity.AppCode, OperationType = OperationType, Status = "Pending", Phase = "Queued", ImpactJson = JsonSerializer.Serialize(envelope, JsonOptions), TraceId = activity.TraceId, ActorUserId = subscription.OwnerUserId, StartedTime = DateTime.UtcNow, CreatedBy = subscription.OwnerUserId, CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync(ct);
            await EnqueueAsync(eventId, subscription.OwnerUserId, ct);
        }
    }

    public async Task DeliverAsync(ProjectManagementWebhookDeliveryJobArgs args, CancellationToken ct = default)
    {
        RequireContext(args); var operation = await GetDeliveryAsync(args.EventId, ct); if (operation is null || operation.Status is "Succeeded" or "DeadLetter") return;
        var envelope = ReadEnvelope(operation); if (envelope.NextAttemptAt > DateTime.UtcNow) { Schedule(operation.Id, args.ActorUserId, envelope.NextAttemptAt - DateTime.UtcNow); return; }
        var claimed = await Db().Updateable<ProjectManagementOperationEntity>().SetColumns(x => new ProjectManagementOperationEntity { Status = "Running", Phase = "Sending", VersionNo = x.VersionNo + 1, UpdatedBy = User(), UpdatedTime = DateTime.UtcNow }).Where(x => x.Id == operation.Id && x.Status == "Pending" && !x.IsDeleted).ExecuteCommandAsync(ct); if (claimed != 1) return;
        try
        {
            var body = JsonSerializer.Serialize(envelope.Payload, JsonOptions); var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(); var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretProtector.Unprotect(envelope.SecretCipherText)), Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
            using var request = new HttpRequestMessage(HttpMethod.Post, envelope.EndpointUrl) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.TryAddWithoutValidation("X-AsterERP-Event-Id", envelope.Payload.EventId); request.Headers.TryAddWithoutValidation("X-AsterERP-Timestamp", timestamp); request.Headers.TryAddWithoutValidation("X-AsterERP-Signature-256", $"sha256={signature}");
            using var response = await httpClientFactory.CreateClient(ApplicationDataOutboundHttpClient.Name).SendAsync(request, ct); if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Webhook 返回 {(int)response.StatusCode}");
            await Db().Updateable<ProjectManagementOperationEntity>().SetColumns(x => new ProjectManagementOperationEntity { Status = "Succeeded", Phase = "Completed", ProgressPercent = 100, CompletedTime = DateTime.UtcNow, ErrorMessage = null, VersionNo = x.VersionNo + 1, UpdatedBy = User(), UpdatedTime = DateTime.UtcNow }).Where(x => x.Id == operation.Id).ExecuteCommandAsync(ct);
        }
        catch (Exception ex)
        {
            var attempt = envelope.AttemptCount + 1; var dead = attempt >= envelope.MaxAttempts; var next = DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, attempt) * 10)); envelope = envelope with { AttemptCount = attempt, NextAttemptAt = next, LastError = Trim(ex.Message) };
            await Db().Updateable<ProjectManagementOperationEntity>().SetColumns(x => new ProjectManagementOperationEntity { Status = dead ? "DeadLetter" : "Pending", Phase = dead ? "DeadLetter" : "RetryScheduled", ErrorMessage = envelope.LastError, CompletedTime = dead ? DateTime.UtcNow : null, ImpactJson = JsonSerializer.Serialize(envelope, JsonOptions), VersionNo = x.VersionNo + 1, UpdatedBy = User(), UpdatedTime = DateTime.UtcNow }).Where(x => x.Id == operation.Id).ExecuteCommandAsync(CancellationToken.None);
            if (!dead) Schedule(operation.Id, args.ActorUserId, next - DateTime.UtcNow);
        }
    }

    private async Task EnqueueAsync(string eventId, string actorId, CancellationToken ct) => await backgroundJobManager.EnqueueAsync(new ProjectManagementWebhookDeliveryJobArgs(eventId, Tenant(), App(), actorId));
    private void Schedule(string eventId, string actorId, TimeSpan delay)
    {
        backgroundJobClient.Schedule<ProjectManagementWebhookDeliveryJob>(job => job.ExecuteAsync(new ProjectManagementWebhookDeliveryJobArgs(eventId, Tenant(), App(), actorId)), delay < TimeSpan.Zero ? TimeSpan.Zero : delay);
    }
    private async Task<ProjectManagementWebhookSubscriptionEntity> GetSubscriptionAsync(string id, CancellationToken ct) => (await Db().Queryable<ProjectManagementWebhookSubscriptionEntity>().Where(x => x.Id == id && !x.IsDeleted).Take(1).ToListAsync(ct)).FirstOrDefault() ?? throw new ValidationException("WebHook 订阅不存在");
    private async Task<ProjectManagementOperationEntity?> GetDeliveryAsync(string id, CancellationToken ct) => (await Db().Queryable<ProjectManagementOperationEntity>().Where(x => x.Id == id && x.OperationType == OperationType && !x.IsDeleted).Take(1).ToListAsync(ct)).FirstOrDefault();
    private static ProjectManagementWebhookSubscriptionResponse Map(ProjectManagementWebhookSubscriptionEntity x) => new(x.Id, x.ProjectId, x.Name, x.EndpointUrl, !string.IsNullOrWhiteSpace(x.SecretCipherText), ReadEvents(x).Order().ToList(), x.IsEnabled, x.MaxAttempts, x.CreatedTime, x.UpdatedTime);
    private static ProjectManagementWebhookDeliveryResponse MapDelivery(ProjectManagementOperationEntity x) { var e = ReadEnvelope(x); return new(x.Id, e.SubscriptionId, e.Payload.ProjectId, e.Payload.EventType, x.Status, e.AttemptCount, e.MaxAttempts, e.NextAttemptAt, x.ErrorMessage, x.CreatedTime, x.CompletedTime); }
    private static DeliveryEnvelope ReadEnvelope(ProjectManagementOperationEntity x) => JsonSerializer.Deserialize<DeliveryEnvelope>(x.ImpactJson, JsonOptions) ?? throw new ValidationException("WebHook 投递载荷损坏");
    private static HashSet<string> ReadEvents(ProjectManagementWebhookSubscriptionEntity x) => JsonSerializer.Deserialize<HashSet<string>>(x.EventTypesJson, JsonOptions) ?? [];
    private static string? ToEventType(ProjectManagementActivityEvent x) => x.AggregateType switch { "Project" => ProjectManagementWebhookEventTypes.ProjectChanged, "Milestone" => ProjectManagementWebhookEventTypes.MilestoneChanged, "Task" when x.ActivityType.Contains("status", StringComparison.OrdinalIgnoreCase) => ProjectManagementWebhookEventTypes.StatusChanged, "Task" => ProjectManagementWebhookEventTypes.TaskChanged, "TaskComment" => ProjectManagementWebhookEventTypes.CommentCreated, "TaskAttachment" => ProjectManagementWebhookEventTypes.AttachmentCreated, "TaskReminder" => ProjectManagementWebhookEventTypes.ReminderSent, "Sync" or "SyncJournal" => ProjectManagementWebhookEventTypes.SyncCompleted, _ => null };
    private static void Validate(ProjectManagementWebhookSubscriptionUpsertRequest x) { if (string.IsNullOrWhiteSpace(x.ProjectId) || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 128 || x.EventTypes.Count == 0 || x.EventTypes.Any(t => !ProjectManagementWebhookEventTypes.All.Contains(t))) throw new ValidationException("WebHook 订阅参数无效"); if (!Uri.TryCreate(x.EndpointUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrWhiteSpace(uri.UserInfo)) throw new ValidationException("WebHook 地址无效"); ApplicationDataOutboundHttpClient.EnsureAllowedUri(uri); if (x.Secret is { Length: > 0 and < 16 } or { Length: > 512 } || x.MaxAttempts is < 1 or > 10) throw new ValidationException("WebHook 密钥或最大重试次数无效"); }
    private void RequireWorkspace() => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser); private void RequireContext(ProjectManagementWebhookDeliveryJobArgs x) { if (x.TenantId != Tenant() || !string.Equals(x.AppCode, App(), StringComparison.OrdinalIgnoreCase) || x.ActorUserId != User()) throw new InvalidOperationException("WebHook 作业上下文不匹配"); }
    private ISqlSugarClient Db() => databaseAccessor.GetProjectManagementDb(); private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户"); private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用"); private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户"); private static string Required(string x) => string.IsNullOrWhiteSpace(x) ? throw new ValidationException("项目标识不能为空") : x.Trim(); private static string Trim(string x) => x.Length <= 2000 ? x : x[..2000];
    private sealed record DeliveryEnvelope(ProjectManagementWebhookEventPayload Payload, string SubscriptionId, string EndpointUrl, string SecretCipherText, int AttemptCount, int MaxAttempts, DateTime NextAttemptAt, string? LastError);
}
