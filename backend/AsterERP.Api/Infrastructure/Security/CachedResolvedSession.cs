namespace AsterERP.Api.Infrastructure.Security;

public sealed record CachedResolvedSession(
    string SessionId,
    string TokenHash,
    int SessionVersion,
    DateTime ExpiresAt,
    DateTime? LastSeenSyncedAt,
    ResolvedAuthenticatedUser User);
