namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementActivityResponse(string Id, string ProjectId, string AggregateType, string AggregateId, string ActivityType, string? Summary, string TraceId, string ActorUserId, DateTime CreatedTime);
