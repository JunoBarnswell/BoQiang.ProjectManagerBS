namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterOperationResponse(
    ApplicationDataCenterObjectDetailResponse Object,
    ApplicationDataCenterReferenceSummaryResponse ReferenceSummary,
    IReadOnlyList<ApplicationDataCenterNextActionResponse> NextActions);
