using AsterERP.Api.Modules.System.Messaging;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Messaging;

public sealed class MessageSendLogWriter(ISqlSugarClient db) : IMessageSendLogWriter
{
    public async Task WriteAsync(MessageSendLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new SystemMessageSendLogEntity
        {
            Channel = Normalize(request.Channel, 32),
            Provider = Normalize(request.Provider, 64),
            MaskedTarget = MaskTarget(request.Target),
            TraceId = Normalize(request.TraceId, 128),
            CorrelationId = NormalizeOptional(request.CorrelationId, 128),
            Result = request.Success ? AsterErpMessageSendResults.Success : AsterErpMessageSendResults.Failed,
            ErrorSummary = NormalizeOptional(request.ErrorSummary, 512),
            DurationMs = Math.Max(0, request.DurationMs),
            CreatedTime = DateTime.UtcNow
        };

        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private static string Normalize(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? MaskTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        var normalized = target.Trim();
        if (normalized.Contains('@', StringComparison.Ordinal))
        {
            var parts = normalized.Split('@', 2);
            var prefix = parts[0].Length <= 2 ? "***" : $"{parts[0][0]}***{parts[0][^1]}";
            return $"{prefix}@{parts[1]}";
        }

        if (normalized.Length <= 4)
        {
            return "****";
        }

        return $"{normalized[..3]}****{normalized[^4..]}";
    }
}
