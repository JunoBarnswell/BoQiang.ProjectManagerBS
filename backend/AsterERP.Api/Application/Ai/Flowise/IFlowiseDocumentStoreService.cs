using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.DocumentStores;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseDocumentStoreService
{
    Task<GridPageResult<FlowiseDocumentStoreListItemDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseDocumentStoreListItemDto> CreateAsync(FlowiseDocumentStoreSaveRequest request, CancellationToken cancellationToken);

    Task<FlowiseDocumentStoreListItemDto> UpdateAsync(string id, FlowiseDocumentStoreSaveRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseDocumentStoreDto> GetDetailAsync(string storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowiseDocumentStoreFileDto>> GetFilesAsync(string storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowiseDocumentStoreChunkDto>> GetChunksAsync(string storeId, string? fileId, CancellationToken cancellationToken);

    Task<FlowiseVectorStoreConfigDto?> GetVectorConfigAsync(string storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowiseDocumentStoreUpsertHistoryDto>> GetUpsertHistoryAsync(string storeId, CancellationToken cancellationToken);

    Task<FlowiseDocumentStoreUpsertHistoryDto> UpsertAsync(FlowiseDocumentStoreUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseDocumentStoreQueryResultDto> QueryAsync(FlowiseDocumentStoreQueryRequest request, CancellationToken cancellationToken);
}
