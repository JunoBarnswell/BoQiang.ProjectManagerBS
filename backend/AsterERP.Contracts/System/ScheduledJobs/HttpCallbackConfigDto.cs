namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record HttpCallbackConfigDto(
    string Url,
    string Method,
    string? BodyJson,
    IReadOnlyDictionary<string, string>? Headers);
