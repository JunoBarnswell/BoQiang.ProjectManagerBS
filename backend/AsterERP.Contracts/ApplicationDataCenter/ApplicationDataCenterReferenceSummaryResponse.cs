namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterReferenceSummaryResponse(
    string ObjectType,
    string ObjectId,
    int Total,
    int MicroflowCount,
    int QueryDatasetCount,
    int IntegrationTaskCount,
    int PageCount,
    IReadOnlyList<ApplicationDataCenterReferenceItemResponse> Items);
