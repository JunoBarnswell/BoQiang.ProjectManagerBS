using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Modules.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SqlSugar;

namespace AsterERP.Api.Application.AsterScene;

public sealed class AsterSceneAssetService(
    ISqlSugarClient db,
    IWebHostEnvironment environment,
    AsterSceneWorkspaceContext workspaceContext,
    AsterSceneDocumentService documentService)
{
    private static readonly HashSet<string> AllowedAssetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "model",
        "texture",
        "material",
        "panorama",
        "video",
        "audio",
        "image",
        "decal",
        "prefab",
        "document",
        "hdri",
        "mesh",
        "preset"
    };

    public async Task<GridPageResult<AsterSceneAssetDto>> GetAssetsAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        _ = workspaceContext.Resolve();
        var dbQuery = db.Queryable<AsterSceneAssetEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.ProjectId))
        {
            dbQuery = dbQuery.Where(item => item.ProjectId == query.ProjectId);
        }

        if (!string.IsNullOrWhiteSpace(query.AssetType))
        {
            var assetType = NormalizeAssetType(query.AssetType);
            dbQuery = dbQuery.Where(item => item.AssetType == assetType);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.FileName.Contains(keyword) || item.AssetCode.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);

        return new GridPageResult<AsterSceneAssetDto>
        {
            Total = total.Value,
            Items = rows.Select(MapAsset).ToList()
        };
    }

    public async Task<AsterSceneAssetDto> RegisterAssetAsync(
        AsterSceneAssetRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var project = await documentService.RequireProjectAsync(request.ProjectId, cancellationToken);
        var clientMutationId = NormalizeOptional(request.ClientMutationId);
        if (!string.IsNullOrWhiteSpace(clientMutationId))
        {
            var existing = await db.Queryable<AsterSceneAssetEntity>()
                .FirstAsync(item =>
                    !item.IsDeleted &&
                    item.ProjectId == project.Id &&
                    item.ClientMutationId == clientMutationId,
                    cancellationToken);
            if (existing is not null)
            {
                return MapAsset(existing);
            }
        }

        var assetType = NormalizeAssetType(request.AssetType);
        var fileName = NormalizeFileName(request.FileName);
        ValidateRuntimeUrl(request.SourceUrl);
        await EnsureStorageQuotaAsync(workspace.UserId, request.SizeBytes ?? 0, cancellationToken);

        var entity = new AsterSceneAssetEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            OwnerUserId = workspace.UserId,
            AssetCode = await GenerateAssetCodeAsync(project.Id, cancellationToken),
            AssetType = assetType,
            FileName = fileName,
            Status = "Ready",
            SourceUrl = request.SourceUrl.Trim(),
            RuntimeUrl = request.SourceUrl.Trim(),
            ContentType = NormalizeOptional(request.ContentType),
            SizeBytes = request.SizeBytes,
            Checksum = NormalizeOptional(request.Checksum),
            MetadataJson = request.Metadata.HasValue ? request.Metadata.Value.GetRawText() : null,
            ClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };

        var version = BuildAssetVersion(entity, entity.RuntimeUrl ?? request.SourceUrl);
        await ExecuteInTransactionAsync(async () =>
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(version).ExecuteCommandAsync(cancellationToken);
            await InsertStorageUsageAsync(workspace, project.Id, entity.Id, entity.SizeBytes ?? 0, cancellationToken);
        });

        return MapAsset(entity);
    }

    public async Task<AsterSceneAssetDto> CreateGeneratedAssetAsync(
        AsterSceneGeneratedAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var project = await documentService.RequireProjectAsync(request.ProjectId, cancellationToken);
        var clientMutationId = NormalizeOptional(request.ClientMutationId);
        if (string.IsNullOrWhiteSpace(clientMutationId))
        {
            throw new ValidationException("clientMutationId is required for generated assets.", ErrorCodes.ParameterInvalid);
        }

        var existing = await db.Queryable<AsterSceneAssetEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.ProjectId == project.Id &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existing is not null)
        {
            return MapAsset(existing);
        }

        var assetType = NormalizeAssetType(request.AssetType);
        if (!assetType.Equals("mesh", StringComparison.OrdinalIgnoreCase) &&
            !assetType.Equals("material", StringComparison.OrdinalIgnoreCase) &&
            !assetType.Equals("preset", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Generated assets must be mesh, material, or preset.", ErrorCodes.ParameterInvalid);
        }

        if (request.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new ValidationException("Generated asset payload is required.", ErrorCodes.ParameterInvalid);
        }

        var fileName = EnsureJsonFileName(NormalizeFileName(request.FileName));
        var payloadJson = AsterSceneDocumentKernel.NormalizeJson(request.Payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var actualChecksum = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        var declaredChecksum = NormalizeChecksum(request.Checksum);
        if (!string.IsNullOrWhiteSpace(declaredChecksum) &&
            !string.Equals(actualChecksum, declaredChecksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Generated asset checksum does not match.", ErrorCodes.AsterSceneUploadInvalid);
        }

        await EnsureStorageQuotaAsync(workspace.UserId, payloadBytes.LongLength, cancellationToken);

        var assetId = Guid.NewGuid().ToString("N");
        var finalDirectory = GetAssetDirectory(workspace, project.Id, assetId);
        Directory.CreateDirectory(finalDirectory);
        var finalPath = Path.Combine(finalDirectory, fileName);
        await File.WriteAllTextAsync(finalPath, payloadJson, Encoding.UTF8, cancellationToken);
        var runtimeUrl = ToPublicUploadUrl(workspace, project.Id, assetId, fileName);
        var metadataJson = BuildGeneratedMetadataJson(request, actualChecksum, payloadBytes.LongLength);
        var asset = new AsterSceneAssetEntity
        {
            Id = assetId,
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            OwnerUserId = workspace.UserId,
            AssetCode = await GenerateAssetCodeAsync(project.Id, cancellationToken),
            AssetType = assetType,
            FileName = fileName,
            Status = "Ready",
            CurrentVersion = 1,
            SourceUrl = runtimeUrl,
            RuntimeUrl = runtimeUrl,
            ContentType = NormalizeOptional(request.ContentType) ?? "application/json",
            SizeBytes = payloadBytes.LongLength,
            Checksum = actualChecksum,
            MetadataJson = metadataJson,
            ClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };
        var version = BuildAssetVersion(asset, runtimeUrl);
        var job = new AsterSceneJobEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            OwnerUserId = workspace.UserId,
            AssetId = asset.Id,
            JobCode = $"JOB{Guid.NewGuid():N}",
            JobType = "GeneratedAssetWrite",
            Status = "Succeeded",
            ProgressPercent = 100,
            OutputJson = JsonSerializer.Serialize(new { assetId = asset.Id, assetType, runtimeUrl, checksum = actualChecksum }),
            StartedTime = DateTime.UtcNow,
            FinishedTime = DateTime.UtcNow,
            CreatedBy = workspace.UserId
        };

        await ExecuteInTransactionAsync(async () =>
        {
            await db.Insertable(asset).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(version).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(job).ExecuteCommandAsync(cancellationToken);
            await InsertStorageUsageAsync(workspace, project.Id, asset.Id, payloadBytes.LongLength, cancellationToken);
        });

        return MapAsset(asset);
    }

    public async Task<AsterSceneUploadSessionDto> StartUploadAsync(
        AsterSceneStartUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var project = await documentService.RequireProjectAsync(request.ProjectId, cancellationToken);
        var clientMutationId = NormalizeOptional(request.ClientMutationId);
        if (!string.IsNullOrWhiteSpace(clientMutationId))
        {
            var existing = await db.Queryable<AsterSceneUploadSessionEntity>()
                .FirstAsync(item =>
                    !item.IsDeleted &&
                    item.ProjectId == project.Id &&
                    item.ClientMutationId == clientMutationId,
                    cancellationToken);
            if (existing is not null)
            {
                return MapUpload(existing);
            }
        }

        var totalChunks = Math.Clamp(request.TotalChunks, 1, 10000);
        if (request.SizeBytes <= 0)
        {
            throw new ValidationException("Upload size must be greater than 0.", ErrorCodes.AsterSceneUploadInvalid);
        }

        await EnsureStorageQuotaAsync(workspace.UserId, request.SizeBytes, cancellationToken);
        var entity = new AsterSceneUploadSessionEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            OwnerUserId = workspace.UserId,
            UploadCode = $"UP{Guid.NewGuid():N}",
            AssetType = NormalizeAssetType(request.AssetType),
            FileName = NormalizeFileName(request.FileName),
            ContentType = NormalizeOptional(request.ContentType),
            SizeBytes = request.SizeBytes,
            Checksum = NormalizeOptional(request.Checksum),
            TotalChunks = totalChunks,
            UploadedChunks = 0,
            Status = "Pending",
            ClientMutationId = clientMutationId,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedBy = workspace.UserId
        };

        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        Directory.CreateDirectory(GetUploadChunkDirectory(workspace, entity));
        return MapUpload(entity);
    }

    public async Task<AsterSceneUploadSessionDto> UploadChunkAsync(
        string uploadId,
        int chunkIndex,
        IFormFile chunk,
        string? checksum,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var session = await RequireUploadSessionAsync(uploadId, cancellationToken);
        var expectedChecksum = NormalizeChecksum(checksum);
        if (session.Status != "Pending")
        {
            throw new ValidationException("Upload session is not accepting chunks.", ErrorCodes.AsterSceneUploadInvalid);
        }

        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            session.Status = "Expired";
            session.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(session).ExecuteCommandAsync(cancellationToken);
            throw new ValidationException("Upload session expired.", ErrorCodes.AsterSceneUploadInvalid);
        }

        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            throw new ValidationException("Chunk index is out of range.", ErrorCodes.AsterSceneUploadInvalid);
        }

        if (chunk.Length <= 0)
        {
            throw new ValidationException("Chunk content is empty.", ErrorCodes.AsterSceneUploadInvalid);
        }

        var chunkDirectory = GetUploadChunkDirectory(workspace, session);
        Directory.CreateDirectory(chunkDirectory);
        var chunkPath = Path.Combine(chunkDirectory, $"{chunkIndex:D8}.part");
        await using (var stream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true))
        {
            await chunk.CopyToAsync(stream, cancellationToken);
        }

        var actualChecksum = await ComputeFileSha256Async(chunkPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(expectedChecksum) &&
            !string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(chunkPath);
            throw new ValidationException("Upload chunk checksum does not match.", ErrorCodes.AsterSceneUploadInvalid);
        }

        var existing = await db.Queryable<AsterSceneUploadChunkEntity>()
            .FirstAsync(item => !item.IsDeleted && item.UploadSessionId == session.Id && item.ChunkIndex == chunkIndex, cancellationToken);
        if (existing is null)
        {
            await db.Insertable(new AsterSceneUploadChunkEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                UploadSessionId = session.Id,
                OwnerUserId = workspace.UserId,
                ChunkIndex = chunkIndex,
                SizeBytes = chunk.Length,
                Checksum = actualChecksum,
                StoragePath = chunkPath,
                CreatedBy = workspace.UserId
            }).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            existing.SizeBytes = chunk.Length;
            existing.Checksum = actualChecksum;
            existing.StoragePath = chunkPath;
            existing.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }

        session.UploadedChunks = await db.Queryable<AsterSceneUploadChunkEntity>()
            .CountAsync(item => !item.IsDeleted && item.UploadSessionId == session.Id, cancellationToken);
        session.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(session).ExecuteCommandAsync(cancellationToken);
        return MapUpload(session);
    }

    public async Task<AsterSceneAssetDto> CompleteUploadAsync(
        string uploadId,
        AsterSceneCompleteUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var session = await RequireUploadSessionAsync(uploadId, cancellationToken);
        var project = await documentService.RequireProjectAsync(session.ProjectId, cancellationToken);
        var clientMutationId = NormalizeOptional(request.ClientMutationId);
        if (!string.IsNullOrWhiteSpace(clientMutationId))
        {
            var existing = await db.Queryable<AsterSceneAssetEntity>()
                .FirstAsync(item =>
                    !item.IsDeleted &&
                    item.ProjectId == project.Id &&
                    item.ClientMutationId == clientMutationId,
                    cancellationToken);
            if (existing is not null)
            {
                return MapAsset(existing);
            }
        }

        if (session.Status != "Pending")
        {
            if (!string.IsNullOrWhiteSpace(session.ClientMutationId))
            {
                var completedAsset = await db.Queryable<AsterSceneAssetEntity>()
                    .FirstAsync(item =>
                        !item.IsDeleted &&
                        item.ProjectId == project.Id &&
                        item.ClientMutationId == session.ClientMutationId,
                        cancellationToken);
                if (completedAsset is not null)
                {
                    return MapAsset(completedAsset);
                }
            }

            throw new ValidationException("Upload session cannot be completed.", ErrorCodes.AsterSceneUploadInvalid);
        }

        var chunks = await db.Queryable<AsterSceneUploadChunkEntity>()
            .Where(item => !item.IsDeleted && item.UploadSessionId == session.Id)
            .OrderBy(item => item.ChunkIndex)
            .ToListAsync(cancellationToken);
        if (chunks.Count != session.TotalChunks || chunks.Select(item => item.ChunkIndex).Distinct().Count() != session.TotalChunks)
        {
            throw new ValidationException("Upload chunks are incomplete.", ErrorCodes.AsterSceneUploadInvalid);
        }

        var assetId = Guid.NewGuid().ToString("N");
        var finalDirectory = GetAssetDirectory(workspace, project.Id, assetId);
        Directory.CreateDirectory(finalDirectory);
        var finalFileName = NormalizeFileName(session.FileName);
        var finalPath = Path.Combine(finalDirectory, finalFileName);
        await using (var output = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 256, true))
        {
            foreach (var chunk in chunks)
            {
                await using var input = new FileStream(chunk.StoragePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
                await input.CopyToAsync(output, cancellationToken);
            }
        }

        var actualSize = new FileInfo(finalPath).Length;
        if (actualSize != session.SizeBytes)
        {
            throw new ValidationException("Merged upload size does not match the declared size.", ErrorCodes.AsterSceneUploadInvalid);
        }

        var actualChecksum = await ComputeFileSha256Async(finalPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(session.Checksum) &&
            !string.Equals(actualChecksum, session.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Merged upload checksum does not match.", ErrorCodes.AsterSceneUploadInvalid);
        }

        var runtimeUrl = ToPublicUploadUrl(workspace, project.Id, assetId, finalFileName);
        var asset = new AsterSceneAssetEntity
        {
            Id = assetId,
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            OwnerUserId = workspace.UserId,
            AssetCode = await GenerateAssetCodeAsync(project.Id, cancellationToken),
            AssetType = session.AssetType,
            FileName = finalFileName,
            Status = "Ready",
            CurrentVersion = 1,
            SourceUrl = runtimeUrl,
            RuntimeUrl = runtimeUrl,
            ContentType = session.ContentType,
            SizeBytes = actualSize,
            Checksum = actualChecksum,
            MetadataJson = request.Metadata.HasValue ? request.Metadata.Value.GetRawText() : null,
            ClientMutationId = clientMutationId ?? session.ClientMutationId,
            CreatedBy = workspace.UserId
        };
        var version = BuildAssetVersion(asset, runtimeUrl);
        var job = new AsterSceneJobEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            OwnerUserId = workspace.UserId,
            AssetId = asset.Id,
            JobCode = $"JOB{Guid.NewGuid():N}",
            JobType = "AssetIngest",
            Status = "Succeeded",
            ProgressPercent = 100,
            OutputJson = JsonSerializer.Serialize(new { assetId = asset.Id, runtimeUrl }),
            StartedTime = DateTime.UtcNow,
            FinishedTime = DateTime.UtcNow,
            CreatedBy = workspace.UserId
        };

        await ExecuteInTransactionAsync(async () =>
        {
            session.Status = "Completed";
            session.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(session).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(asset).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(version).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(job).ExecuteCommandAsync(cancellationToken);
            await InsertStorageUsageAsync(workspace, project.Id, asset.Id, actualSize, cancellationToken);
        });

        TryDeleteDirectory(GetUploadChunkDirectory(workspace, session));
        return MapAsset(asset);
    }

    public async Task DeleteAssetAsync(string assetId, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var asset = await db.Queryable<AsterSceneAssetEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == assetId, cancellationToken);
        if (asset is null)
        {
            throw new NotFoundException("AsterScene asset was not found.", ErrorCodes.AsterSceneAssetNotFound);
        }

        _ = await documentService.RequireProjectAsync(asset.ProjectId, cancellationToken);
        asset.IsDeleted = true;
        asset.DeletedBy = workspace.UserId;
        asset.DeletedTime = DateTime.UtcNow;
        await ExecuteInTransactionAsync(async () =>
        {
            await db.Updateable(asset).ExecuteCommandAsync(cancellationToken);
            await InsertStorageCreditAsync(workspace, asset.ProjectId, asset.Id, asset.SizeBytes ?? 0, cancellationToken);
        });
    }

    public async Task<GridPageResult<AsterSceneJobDto>> GetJobsAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        _ = workspaceContext.Resolve();
        var dbQuery = db.Queryable<AsterSceneJobEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.ProjectId))
        {
            dbQuery = dbQuery.Where(item => item.ProjectId == query.ProjectId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);
        return new GridPageResult<AsterSceneJobDto>
        {
            Total = total.Value,
            Items = rows.Select(MapJob).ToList()
        };
    }

    public static AsterSceneAssetDto MapAsset(AsterSceneAssetEntity entity)
    {
        return new AsterSceneAssetDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            AssetCode = entity.AssetCode,
            AssetType = entity.AssetType,
            FileName = entity.FileName,
            Status = entity.Status,
            CurrentVersion = entity.CurrentVersion,
            RuntimeUrl = entity.RuntimeUrl,
            ThumbnailUrl = entity.ThumbnailUrl,
            ContentType = entity.ContentType,
            SizeBytes = entity.SizeBytes,
            Checksum = entity.Checksum,
            Metadata = string.IsNullOrWhiteSpace(entity.MetadataJson) ? null : AsterSceneDocumentKernel.ParseJson(entity.MetadataJson),
            CreatedTime = entity.CreatedTime
        };
    }

    public static AsterSceneJobDto MapJob(AsterSceneJobEntity entity)
    {
        return new AsterSceneJobDto
        {
            Id = entity.Id,
            JobCode = entity.JobCode,
            JobType = entity.JobType,
            Status = entity.Status,
            ProgressPercent = entity.ProgressPercent,
            ErrorCode = entity.ErrorCode,
            ErrorMessage = entity.ErrorMessage,
            Output = string.IsNullOrWhiteSpace(entity.OutputJson) ? null : AsterSceneDocumentKernel.ParseJson(entity.OutputJson),
            CreatedTime = entity.CreatedTime
        };
    }

    private async Task<AsterSceneUploadSessionEntity> RequireUploadSessionAsync(
        string uploadId,
        CancellationToken cancellationToken)
    {
        var session = await db.Queryable<AsterSceneUploadSessionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == uploadId, cancellationToken);
        if (session is null)
        {
            throw new NotFoundException("AsterScene upload session was not found.", ErrorCodes.AsterSceneUploadInvalid);
        }

        return session;
    }

    private async Task EnsureStorageQuotaAsync(
        string ownerUserId,
        long incomingBytes,
        CancellationToken cancellationToken)
    {
        var subscription = await db.Queryable<AsterSceneSubscriptionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.OwnerUserId == ownerUserId && item.Status == "Active", cancellationToken);
        var plan = AsterScenePlanCatalog.GetPlan(subscription?.PlanCode);
        var currentAssetSizes = await db.Queryable<AsterSceneAssetEntity>()
            .Where(item => !item.IsDeleted && item.SizeBytes != null)
            .Where(item => item.OwnerUserId == ownerUserId)
            .Select(item => item.SizeBytes)
            .ToListAsync(cancellationToken);
        var currentBytes = currentAssetSizes.Sum(item => item ?? 0);
        var limitBytes = plan.StorageGb * 1024L * 1024L * 1024L;
        if (currentBytes + incomingBytes > limitBytes)
        {
            throw new ValidationException("AsterScene storage quota exceeded.", ErrorCodes.AsterSceneQuotaExceeded);
        }
    }

    private async Task InsertStorageUsageAsync(
        AsterSceneWorkspace workspace,
        string projectId,
        string assetId,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"asset-storage:{assetId}";
        var exists = await db.Queryable<AsterSceneUsageLedgerEntity>()
            .AnyAsync(item => !item.IsDeleted && item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (exists)
        {
            return;
        }

        await db.Insertable(new AsterSceneUsageLedgerEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ProjectId = projectId,
            UsageType = "storage",
            Quantity = Math.Round(sizeBytes / 1024m / 1024m / 1024m, 6),
            Unit = "GB",
            Direction = "Debit",
            SourceType = "asset",
            SourceId = assetId,
            IdempotencyKey = idempotencyKey,
            CreatedBy = workspace.UserId
        }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task InsertStorageCreditAsync(
        AsterSceneWorkspace workspace,
        string projectId,
        string assetId,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        if (sizeBytes <= 0)
        {
            return;
        }

        var idempotencyKey = $"asset-storage-delete:{assetId}";
        var exists = await db.Queryable<AsterSceneUsageLedgerEntity>()
            .AnyAsync(item => !item.IsDeleted && item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (exists)
        {
            return;
        }

        await db.Insertable(new AsterSceneUsageLedgerEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ProjectId = projectId,
            UsageType = "storage",
            Quantity = Math.Round(sizeBytes / 1024m / 1024m / 1024m, 6),
            Unit = "GB",
            Direction = "Credit",
            SourceType = "asset-delete",
            SourceId = assetId,
            IdempotencyKey = idempotencyKey,
            CreatedBy = workspace.UserId
        }).ExecuteCommandAsync(cancellationToken);
    }

    private AsterSceneAssetVersionEntity BuildAssetVersion(AsterSceneAssetEntity entity, string url)
    {
        return new AsterSceneAssetVersionEntity
        {
            TenantId = entity.TenantId,
            AppCode = entity.AppCode,
            ProjectId = entity.ProjectId,
            OwnerUserId = entity.OwnerUserId,
            AssetId = entity.Id,
            Version = entity.CurrentVersion,
            VariantType = "original",
            Url = url,
            ContentType = entity.ContentType,
            SizeBytes = entity.SizeBytes,
            Checksum = entity.Checksum,
            MetadataJson = entity.MetadataJson,
            Status = entity.Status,
            CreatedBy = entity.CreatedBy
        };
    }

    private async Task<string> GenerateAssetCodeAsync(string projectId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = $"AST{Random.Shared.Next(100000, 999999)}";
            var exists = await db.Queryable<AsterSceneAssetEntity>()
                .AnyAsync(item => !item.IsDeleted && item.ProjectId == projectId && item.AssetCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"AST{Guid.NewGuid():N}"[..18].ToUpperInvariant();
    }

    private string GetUploadChunkDirectory(AsterSceneWorkspace workspace, AsterSceneUploadSessionEntity session)
    {
        return Path.Combine(GetUploadRoot(), workspace.TenantId, workspace.AppCode, "chunks", session.Id);
    }

    private string GetAssetDirectory(AsterSceneWorkspace workspace, string projectId, string assetId)
    {
        return Path.Combine(GetUploadRoot(), workspace.TenantId, workspace.AppCode, "projects", projectId, assetId);
    }

    private string GetUploadRoot()
    {
        var root = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        return Path.Combine(root, "uploads", "asterscene");
    }

    private static string ToPublicUploadUrl(AsterSceneWorkspace workspace, string projectId, string assetId, string fileName)
    {
        return $"/uploads/asterscene/{Uri.EscapeDataString(workspace.TenantId)}/{Uri.EscapeDataString(workspace.AppCode)}/projects/{Uri.EscapeDataString(projectId)}/{Uri.EscapeDataString(assetId)}/{Uri.EscapeDataString(fileName)}";
    }

    private static void ValidateRuntimeUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Asset sourceUrl is required.", ErrorCodes.ParameterInvalid);
        }

        if (value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return;
        }

        throw new ValidationException("Asset sourceUrl must be http(s) or a local upload URL.", ErrorCodes.AsterSceneUploadInvalid);
    }

    private static string NormalizeAssetType(string? value)
    {
        var normalized = NormalizeOptional(value)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !AllowedAssetTypes.Contains(normalized))
        {
            throw new ValidationException("Unsupported AsterScene asset type.", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string NormalizeFileName(string? value)
    {
        var fileName = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException("File name is required.", ErrorCodes.ParameterInvalid);
        }

        fileName = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        return fileName;
    }

    private static string EnsureJsonFileName(string fileName)
    {
        return Path.GetExtension(fileName).Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{Path.GetFileNameWithoutExtension(fileName)}.json";
    }

    private static string BuildGeneratedMetadataJson(AsterSceneGeneratedAssetRequest request, string checksum, long payloadSizeBytes)
    {
        return request.Metadata.HasValue && request.Metadata.Value.ValueKind != JsonValueKind.Undefined
            ? JsonSerializer.Serialize(new
            {
                source = "editor-generated",
                payloadChecksum = checksum,
                payloadSizeBytes,
                metadata = request.Metadata.Value
            })
            : JsonSerializer.Serialize(new
            {
                source = "editor-generated",
                payloadChecksum = checksum,
                payloadSizeBytes
            });
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeChecksum(string? value)
    {
        var normalized = NormalizeOptional(value)?.ToLowerInvariant();
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ValidationException("Checksum must be a SHA-256 hex string.", ErrorCodes.AsterSceneUploadInvalid);
        }

        return normalized;
    }

    private static AsterSceneUploadSessionDto MapUpload(AsterSceneUploadSessionEntity entity)
    {
        return new AsterSceneUploadSessionDto
        {
            UploadId = entity.Id,
            ProjectId = entity.ProjectId,
            Status = entity.Status,
            TotalChunks = entity.TotalChunks,
            UploadedChunks = entity.UploadedChunks,
            SizeBytes = entity.SizeBytes
        };
    }

    private static async Task<string> ComputeFileSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Upload cleanup failure must not roll back a completed asset.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; checksum validation still fails the request.
        }
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var ownsTransaction = db.Ado.Transaction is null;
        try
        {
            if (ownsTransaction)
            {
                await db.Ado.BeginTranAsync();
            }

            await action();

            if (ownsTransaction)
            {
                await db.Ado.CommitTranAsync();
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                await db.Ado.RollbackTranAsync();
            }

            throw;
        }
    }
}
