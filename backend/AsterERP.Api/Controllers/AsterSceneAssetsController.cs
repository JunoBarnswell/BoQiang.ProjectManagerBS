using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/asterscene/assets")]
public sealed class AsterSceneAssetsController(AsterSceneAssetService assetService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AsterSceneAssetView)]
    public async Task<IActionResult> GetAssetsAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await assetService.GetAssetsAsync(query, cancellationToken));
    }

    [HttpPost("register")]
    [Permission(PermissionCodes.AsterSceneAssetUpload)]
    public async Task<IActionResult> RegisterAssetAsync([FromBody] AsterSceneAssetRegisterRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await assetService.RegisterAssetAsync(request, cancellationToken));
    }

    [HttpPost("generated")]
    [Permission(PermissionCodes.AsterSceneAssetUpload)]
    public async Task<IActionResult> CreateGeneratedAssetAsync([FromBody] AsterSceneGeneratedAssetRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await assetService.CreateGeneratedAssetAsync(request, cancellationToken));
    }

    [HttpPost("uploads")]
    [Permission(PermissionCodes.AsterSceneAssetUpload)]
    public async Task<IActionResult> StartUploadAsync([FromBody] AsterSceneStartUploadRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await assetService.StartUploadAsync(request, cancellationToken));
    }

    [HttpPost("uploads/{uploadId}/chunks/{chunkIndex:int}")]
    [Permission(PermissionCodes.AsterSceneAssetUpload)]
    [RequestSizeLimit(256L * 1024L * 1024L)]
    public async Task<IActionResult> UploadChunkAsync(
        string uploadId,
        int chunkIndex,
        [FromForm] IFormFile chunk,
        [FromForm] string? checksum,
        CancellationToken cancellationToken)
    {
        return ApiOk(await assetService.UploadChunkAsync(uploadId, chunkIndex, chunk, checksum, cancellationToken));
    }

    [HttpPost("uploads/{uploadId}/complete")]
    [Permission(PermissionCodes.AsterSceneAssetUpload)]
    public async Task<IActionResult> CompleteUploadAsync(
        string uploadId,
        [FromBody] AsterSceneCompleteUploadRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await assetService.CompleteUploadAsync(uploadId, request, cancellationToken));
    }

    [HttpDelete("{assetId}")]
    [Permission(PermissionCodes.AsterSceneAssetDelete)]
    public async Task<IActionResult> DeleteAssetAsync(string assetId, CancellationToken cancellationToken)
    {
        await assetService.DeleteAssetAsync(assetId, cancellationToken);
        return ApiOk(true);
    }
}
