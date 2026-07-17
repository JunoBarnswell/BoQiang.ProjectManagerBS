namespace AsterERP.Api.Infrastructure.Ai;

public interface IAiModelRouter
{
    Task<AiModelEndpoint> ResolveAsync(string? modelConfigId, CancellationToken cancellationToken);
}
