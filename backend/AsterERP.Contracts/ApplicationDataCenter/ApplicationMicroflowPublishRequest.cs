namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowPublishRequest(string RevisionId, IReadOnlyList<string>? ConfirmedRiskFields = null);
