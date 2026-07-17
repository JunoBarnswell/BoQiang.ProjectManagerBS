namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record ScheduledJobTypesResponse(
    IReadOnlyList<ScheduledJobTypeOptionResponse> PresetJobs,
    IReadOnlyList<string> JobTypes,
    IReadOnlyList<string> HttpMethods,
    IReadOnlyList<string> ScheduleKinds);
