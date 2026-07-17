using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.DocumentStores;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise/document-stores")]
public sealed class AiFlowiseDocumentStoresController(IFlowiseDocumentStoreService documentStoreService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.FlowiseDocumentStoresView)]
    public async Task<IActionResult> GetDocumentStoresAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.GetPageAsync(query, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.FlowiseDocumentStoresEdit)]
    public async Task<IActionResult> CreateDocumentStoreAsync([FromBody] FlowiseDocumentStoreSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{storeId}")]
    [Permission(PermissionCodes.FlowiseDocumentStoresEdit)]
    public async Task<IActionResult> UpdateDocumentStoreAsync(string storeId, [FromBody] FlowiseDocumentStoreSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.UpdateAsync(storeId, request, cancellationToken));
    }

    [HttpDelete("{storeId}")]
    [Permission(PermissionCodes.FlowiseDocumentStoresEdit)]
    public async Task<IActionResult> DeleteDocumentStoreAsync(string storeId, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.DeleteAsync(storeId, cancellationToken));
    }

    [HttpGet("{storeId}/detail")]
    [Permission(PermissionCodes.FlowiseDocumentStoresView)]
    public async Task<IActionResult> GetDetailAsync(string storeId, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.GetDetailAsync(storeId, cancellationToken));
    }

    [HttpGet("{storeId}/files")]
    [Permission(PermissionCodes.FlowiseDocumentStoresView)]
    public async Task<IActionResult> GetFilesAsync(string storeId, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.GetFilesAsync(storeId, cancellationToken));
    }

    [HttpGet("{storeId}/chunks")]
    [Permission(PermissionCodes.FlowiseDocumentStoresView)]
    public async Task<IActionResult> GetChunksAsync(string storeId, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.GetChunksAsync(storeId, null, cancellationToken));
    }

    [HttpGet("{storeId}/chunks/{fileId}")]
    [Permission(PermissionCodes.FlowiseDocumentStoresView)]
    public async Task<IActionResult> GetFileChunksAsync(string storeId, string fileId, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.GetChunksAsync(storeId, fileId, cancellationToken));
    }

    [HttpGet("{storeId}/vector-config")]
    [Permission(PermissionCodes.FlowiseDocumentStoresView)]
    public async Task<IActionResult> GetVectorConfigAsync(string storeId, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.GetVectorConfigAsync(storeId, cancellationToken));
    }

    [HttpGet("{storeId}/upsert-history")]
    [Permission(PermissionCodes.FlowiseDocumentStoresView)]
    public async Task<IActionResult> GetUpsertHistoryAsync(string storeId, CancellationToken cancellationToken)
    {
        return ApiOk(await documentStoreService.GetUpsertHistoryAsync(storeId, cancellationToken));
    }

    [HttpPost("{storeId}/upsert")]
    [Permission(PermissionCodes.FlowiseDocumentStoresUpsert)]
    public async Task<IActionResult> UpsertAsync(string storeId, [FromBody] FlowiseDocumentStoreUpsertRequest request, CancellationToken cancellationToken)
    {
        request.StoreId = storeId;
        return ApiOk(await documentStoreService.UpsertAsync(request, cancellationToken));
    }

    [HttpPost("{storeId}/query")]
    [Permission(PermissionCodes.FlowiseDocumentStoresView)]
    public async Task<IActionResult> QueryAsync(string storeId, [FromBody] FlowiseDocumentStoreQueryRequest request, CancellationToken cancellationToken)
    {
        request.StoreId = storeId;
        return ApiOk(await documentStoreService.QueryAsync(request, cancellationToken));
    }
}
