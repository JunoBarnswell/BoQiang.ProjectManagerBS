namespace AsterERP.Contracts.Platform;

public sealed record ApplicationPublishArtifactResponse(
    string Id,
    string TaskId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string StoredPath,
    DateTime CreatedTime,
    DateTime? ExpiresAt,
    string DownloadUrl);
