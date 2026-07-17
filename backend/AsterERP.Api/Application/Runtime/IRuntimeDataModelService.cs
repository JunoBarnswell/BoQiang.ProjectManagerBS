using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime;

public interface IRuntimeDataModelService
{
    Task<RuntimeDataModelDefinition> GetPublishedDefinitionAsync(string modelCode, CancellationToken cancellationToken = default);

    Task<RuntimeQueryResponse> QueryAsync(string modelCode, RuntimeQueryRequest request, CancellationToken cancellationToken = default);

    Task<RuntimeDetailResponse> GetDetailAsync(string modelCode, string id, CancellationToken cancellationToken = default);

    Task<RuntimeCompositeDetailResponse> GetCompositeDetailAsync(
        RuntimeCompositeDetailRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeModelOperationResponse> ExecuteOperationAsync(
        string modelCode,
        RuntimeModelOperationRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeCreateResponse> CreateAsync(
        string modelCode,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default);

    Task UpdateFieldsAsync(
        string modelCode,
        string id,
        IReadOnlyDictionary<string, object?> updates,
        CancellationToken cancellationToken = default);

    Task<RuntimeDeleteResponse> DeleteAsync(
        string modelCode,
        string id,
        CancellationToken cancellationToken = default);

    Task<RuntimeCompositeCreateResponse> CreateCompositeAsync(
        RuntimeCompositeCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeCompositeUpdateResponse> UpdateCompositeAsync(
        RuntimeCompositeUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<RuntimeCompositeDeleteResponse> DeleteCompositeAsync(
        RuntimeCompositeDeleteRequest request,
        CancellationToken cancellationToken = default);
}
