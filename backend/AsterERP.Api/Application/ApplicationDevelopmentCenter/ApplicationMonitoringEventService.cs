using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationMonitoringEventService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "designer.command", "designer.command.failed", "designer.save", "designer.publish", "designer.migration",
        "runtime.render", "runtime.action", "runtime.binding.error",
        "dataStudio.connection.test", "dataStudio.catalog.refresh", "dataStudio.query.execute",
        "dataStudio.schema.deploy", "dataStudio.data.write"
    };

    public async Task<ApplicationMonitoringEventResponse> AcceptAsync(
        ApplicationMonitoringEventRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var workspace = workspaceResolver.Resolve();
        var traceId = httpContextAccessor.HttpContext?.TraceIdentifier ?? Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var existing = await db.Queryable<ApplicationMonitoringEventEntity>()
            .Where(item => item.EventId == request.EventId && !item.IsDeleted)
            .FirstAsync(cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.EventType, request.EventType, StringComparison.Ordinal) || !string.Equals(existing.Source, request.Source, StringComparison.Ordinal))
                throw new ValidationException("Monitoring event id already belongs to a different event.", ErrorCodes.ApplicationDataCenterInvalidConfig);
            return new(existing.EventId, existing.EventType, existing.TraceId, true, true);
        }

        var entity = new ApplicationMonitoringEventEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            EventId = request.EventId,
            EventType = request.EventType,
            Source = request.Source,
            PageId = NormalizeOptional(request.PageId),
            RevisionId = NormalizeOptional(request.RevisionId),
            ArtifactHash = NormalizeOptional(request.ArtifactHash),
            UserId = workspace.UserId,
            TraceId = traceId,
            Success = request.Success,
            DurationMs = request.DurationMs,
            PayloadJson = Redact(request.Payload),
            OccurredAt = DateTime.UtcNow
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return new(entity.EventId, entity.EventType, entity.TraceId, true, false);
    }

    private static void Validate(ApplicationMonitoringEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventId) || request.EventId.Length > 128)
            throw new ValidationException("Monitoring event id is required and must be at most 128 characters.", ErrorCodes.ApplicationDataCenterInvalidConfig);
        if (!AllowedEventTypes.Contains(request.EventType))
            throw new ValidationException("Unknown monitoring event type.", ErrorCodes.ApplicationDataCenterInvalidConfig);
        if (string.IsNullOrWhiteSpace(request.Source) || request.Source.Length > 128)
            throw new ValidationException("Monitoring event source is required and must be at most 128 characters.", ErrorCodes.ApplicationDataCenterInvalidConfig);
        if (request.DurationMs is < 0 or > 86_400_000)
            throw new ValidationException("Monitoring event duration is outside the allowed range.", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static string Redact(JsonElement payload)
    {
        var node = JsonNode.Parse(payload.GetRawText()) ?? new JsonObject();
        RedactNode(node);
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void RedactNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (IsSensitive(property.Key)) obj[property.Key] = "[REDACTED]";
                else RedactNode(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) RedactNode(item);
        }
    }

    private static bool IsSensitive(string key) => key.Contains("secret", StringComparison.OrdinalIgnoreCase) || key.Contains("token", StringComparison.OrdinalIgnoreCase) || key.Contains("password", StringComparison.OrdinalIgnoreCase) || key.Contains("authorization", StringComparison.OrdinalIgnoreCase) || key.Contains("connectionstring", StringComparison.OrdinalIgnoreCase) || key.Equals("sql", StringComparison.OrdinalIgnoreCase);
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
