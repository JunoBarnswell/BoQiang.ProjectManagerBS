using System.Text;
using System.Text.Json;
using AsterERP.Api.Modules.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.AsterScene;

public sealed class AsterScenePublishService(
    ISqlSugarClient db,
    AsterSceneWorkspaceContext workspaceContext,
    AsterSceneDocumentService documentService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<GridPageResult<AsterScenePublishVersionDto>> GetPublishVersionsAsync(
        string projectId,
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        _ = await documentService.RequireProjectAsync(projectId, cancellationToken);
        var dbQuery = db.Queryable<AsterScenePublishVersionEntity>()
            .Where(item => !item.IsDeleted && item.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.Version, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);
        return new GridPageResult<AsterScenePublishVersionDto>
        {
            Total = total.Value,
            Items = rows.Select(MapPublish).ToList()
        };
    }

    public async Task<AsterScenePublishResponse> PublishAsync(
        string projectId,
        AsterScenePublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existingPublish = await db.Queryable<AsterScenePublishVersionEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.ProjectId == projectId &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existingPublish is not null)
        {
            return new AsterScenePublishResponse
            {
                PublishVersion = MapPublish(existingPublish),
                Manifest = DeserializeManifest(existingPublish.RuntimeManifestJson),
                PublicWork = await GetPublicWorkByPublishAsync(existingPublish.Id, cancellationToken)
            };
        }

        var project = await documentService.RequireProjectAsync(projectId, cancellationToken);
        var currentDocument = await documentService.RequireCurrentDocumentAsync(project.Id, cancellationToken);
        if (request.ExpectedRevision != project.CurrentRevision ||
            !string.Equals(request.DocumentHash, project.DocumentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Publish request does not match the current SceneDocument revision.", ErrorCodes.AsterSceneDocumentConflict);
        }

        await EnsurePublishedWorkQuotaAsync(workspace.UserId, project.Id, cancellationToken);

        var document = AsterSceneDocumentKernel.ParseJson(currentDocument.DocumentJson);
        var validation = await ValidatePublishReadinessAsync(project.Id, document, request.QualityGateMode, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors[0].Message, ErrorCodes.AsterScenePublishBlocked);
        }

        if (!AsterSceneDocumentKernel.TryGetEntrySceneId(document, out var entrySceneId))
        {
            throw new ValidationException("Runtime entrySceneId is required.", ErrorCodes.AsterScenePublishBlocked);
        }

        var nextVersion = project.PublishedVersion + 1;
        var publishCode = await GeneratePublishCodeAsync(cancellationToken);
        var manifest = await BuildManifestAsync(publishCode, currentDocument.DocumentHash, entrySceneId, document, project.Id, cancellationToken);
        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        var publish = new AsterScenePublishVersionEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            PublishCode = publishCode,
            Version = nextVersion,
            Status = "Active",
            DocumentRevision = currentDocument.Revision,
            DocumentHash = currentDocument.DocumentHash,
            RuntimeManifestJson = manifestJson,
            EntrySceneId = entrySceneId,
            Visibility = NormalizeVisibility(request.Visibility),
            PublishedBy = workspace.UserId,
            ClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };

        AsterScenePublicWorkEntity? publicWork = null;
        await ExecuteInTransactionAsync(async () =>
        {
            var activePublishes = await db.Queryable<AsterScenePublishVersionEntity>()
                .Where(item => !item.IsDeleted && item.ProjectId == project.Id && item.Status == "Active")
                .ToListAsync(cancellationToken);
            foreach (var active in activePublishes)
            {
                active.Status = "Superseded";
                active.UpdatedTime = DateTime.UtcNow;
            }

            if (activePublishes.Count > 0)
            {
                await db.Updateable(activePublishes).ExecuteCommandAsync(cancellationToken);
            }

            await db.Insertable(publish).ExecuteCommandAsync(cancellationToken);
            project.Status = "Published";
            project.Visibility = publish.Visibility;
            project.PublishedVersion = nextVersion;
            project.CurrentPublishCode = publish.PublishCode;
            project.UpdatedBy = workspace.UserId;
            project.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(project).ExecuteCommandAsync(cancellationToken);
            publicWork = await UpsertPublicWorkAsync(workspace, project, publish, cancellationToken);
            await InsertPublishUsageAsync(workspace, project.Id, publish.Id, cancellationToken);
        });

        return new AsterScenePublishResponse
        {
            PublishVersion = MapPublish(publish),
            Manifest = manifest,
            PublicWork = publicWork is null ? null : AsterScenePublicService.MapWork(publicWork)
        };
    }

    public async Task<AsterScenePublishResponse> RollbackAsync(
        string projectId,
        string publishCode,
        AsterSceneRollbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var project = await documentService.RequireProjectAsync(projectId, cancellationToken);
        if (request.ExpectedRevision != project.CurrentRevision ||
            !string.Equals(request.DocumentHash, project.DocumentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Rollback request does not match the current SceneDocument revision.", ErrorCodes.AsterSceneDocumentConflict);
        }

        var target = await db.Queryable<AsterScenePublishVersionEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.ProjectId == project.Id &&
                item.PublishCode == publishCode,
                cancellationToken);
        if (target is null)
        {
            throw new NotFoundException("AsterScene publish version was not found.", ErrorCodes.AsterScenePublishNotFound);
        }

        var rollbackKey = BuildRollbackIdempotencyKey(project.Id, clientMutationId);
        var replay = await db.Queryable<AsterSceneUsageLedgerEntity>()
            .AnyAsync(item => !item.IsDeleted && item.IdempotencyKey == rollbackKey, cancellationToken);
        if (replay && string.Equals(project.CurrentPublishCode, target.PublishCode, StringComparison.OrdinalIgnoreCase))
        {
            return new AsterScenePublishResponse
            {
                PublishVersion = MapPublish(target),
                Manifest = DeserializeManifest(target.RuntimeManifestJson),
                PublicWork = await GetPublicWorkByPublishAsync(target.Id, cancellationToken)
            };
        }

        AsterScenePublicWorkEntity? publicWork = null;
        await ExecuteInTransactionAsync(async () =>
        {
            var activePublishes = await db.Queryable<AsterScenePublishVersionEntity>()
                .Where(item => !item.IsDeleted && item.ProjectId == project.Id && item.Status == "Active")
                .ToListAsync(cancellationToken);
            foreach (var active in activePublishes)
            {
                active.Status = active.PublishCode == target.PublishCode ? "Active" : "RolledBack";
                active.RolledBackAt = active.PublishCode == target.PublishCode ? null : DateTime.UtcNow;
                active.UpdatedTime = DateTime.UtcNow;
            }

            if (activePublishes.All(item => item.Id != target.Id))
            {
                target.Status = "Active";
                target.RolledBackAt = null;
                activePublishes.Add(target);
            }

            await db.Updateable(activePublishes).ExecuteCommandAsync(cancellationToken);
            project.CurrentPublishCode = target.PublishCode;
            project.PublishedVersion = target.Version;
            project.Visibility = target.Visibility;
            project.Status = "Published";
            project.UpdatedBy = workspace.UserId;
            project.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(project).ExecuteCommandAsync(cancellationToken);
            publicWork = await UpsertPublicWorkAsync(workspace, project, target, cancellationToken);
            await InsertRollbackUsageAsync(workspace, project.Id, target.Id, rollbackKey, cancellationToken);
        });

        return new AsterScenePublishResponse
        {
            PublishVersion = MapPublish(target),
            Manifest = DeserializeManifest(target.RuntimeManifestJson),
            PublicWork = publicWork is null ? null : AsterScenePublicService.MapWork(publicWork)
        };
    }

    public async Task<AsterSceneRuntimeManifestDto> GetRuntimeManifestAsync(
        string publishCode,
        CancellationToken cancellationToken = default)
    {
        var publish = await db.Queryable<AsterScenePublishVersionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.PublishCode == publishCode && item.Status == "Active", cancellationToken);
        if (publish is null || publish.Visibility == "Private")
        {
            throw new NotFoundException("AsterScene runtime manifest was not found.", ErrorCodes.AsterScenePublishNotFound);
        }

        var work = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item => !item.IsDeleted && item.PublishVersionId == publish.Id, cancellationToken);
        if (work is null || work.Status != "Published" || work.Visibility == "Private")
        {
            throw new NotFoundException("AsterScene runtime manifest was not found.", ErrorCodes.AsterScenePublishNotFound);
        }

        await InsertRuntimeViewAsync(work, cancellationToken);

        return DeserializeManifest(publish.RuntimeManifestJson);
    }

    public async Task<AsterSceneValidationResultDto> ValidatePublishReadinessAsync(
        string projectId,
        JsonElement document,
        string? qualityGateMode,
        CancellationToken cancellationToken)
    {
        var result = AsterSceneDocumentKernel.Validate(document);
        if (!result.IsValid)
        {
            return result;
        }

        var assetIds = ExtractDocumentAssetIds(document);
        var assetKinds = ExtractDocumentAssetKinds(document);
        if (assetIds.Count > 0)
        {
            var existingAssets = await db.Queryable<AsterSceneAssetEntity>()
                .Where(item => !item.IsDeleted && item.ProjectId == projectId && assetIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
            var existingIds = existingAssets.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var missingId in assetIds.Except(existingIds, StringComparer.OrdinalIgnoreCase))
            {
                result.Errors.Add(new AsterSceneValidationIssueDto
                {
                    Code = "AssetMissing",
                    Path = "$.assets",
                    Message = $"Asset {missingId} is referenced by the document but is not available.",
                    Severity = "error"
                });
            }

            foreach (var asset in existingAssets.Where(item => !string.Equals(item.Status, "Ready", StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add(new AsterSceneValidationIssueDto
                {
                    Code = "AssetNotReady",
                    Path = "$.assets",
                    Message = $"Asset {asset.Id} is not ready for publishing.",
                    Severity = "error"
                });
            }

            foreach (var asset in existingAssets)
            {
                if (!assetKinds.TryGetValue(asset.Id, out var documentKind))
                {
                    continue;
                }

                if (!documentKind.Equals(asset.AssetType, StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(new AsterSceneValidationIssueDto
                    {
                        Code = "AssetKindMismatch",
                        Path = "$.assets",
                        Message = $"Asset {asset.Id} is declared as {documentKind} but registry type is {asset.AssetType}.",
                        Severity = "error"
                    });
                }
            }
        }

        if (qualityGateMode?.Equals("Strict", StringComparison.OrdinalIgnoreCase) == true &&
            document.TryGetProperty("interactions", out var interactions) &&
            interactions.TryGetProperty("blueprints", out var blueprints) &&
            blueprints.ValueKind == JsonValueKind.Array)
        {
            foreach (var blueprint in blueprints.EnumerateArray())
            {
                if (blueprint.TryGetProperty("api", out var api) &&
                    api.ValueKind == JsonValueKind.String &&
                    !IsSafeBlueprintApi(api.GetString()))
                {
                    result.Errors.Add(new AsterSceneValidationIssueDto
                    {
                        Code = "BlueprintApiDenied",
                        Path = "$.interactions.blueprints",
                        Message = "Blueprint API binding must use an allowlisted relative /api/public path.",
                        Severity = "error"
                    });
                }
            }
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public static AsterScenePublishVersionDto MapPublish(AsterScenePublishVersionEntity entity)
    {
        return new AsterScenePublishVersionDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            PublishCode = entity.PublishCode,
            Version = entity.Version,
            Status = entity.Status,
            DocumentRevision = entity.DocumentRevision,
            DocumentHash = entity.DocumentHash,
            Visibility = entity.Visibility,
            PublishedAt = entity.PublishedAt
        };
    }

    private async Task<AsterSceneRuntimeManifestDto> BuildManifestAsync(
        string publishCode,
        string documentHash,
        string entrySceneId,
        JsonElement document,
        string projectId,
        CancellationToken cancellationToken)
    {
        var assetIds = ExtractDocumentAssetIds(document);
        List<AsterSceneAssetVersionEntity> variants = assetIds.Count == 0
            ? []
            : await db.Queryable<AsterSceneAssetVersionEntity>()
                .Where(item => !item.IsDeleted && item.ProjectId == projectId && item.Status == "Ready" && assetIds.Contains(item.AssetId))
                .OrderBy(item => item.AssetId)
                .ToListAsync(cancellationToken);
        List<AsterSceneAssetEntity> assets = assetIds.Count == 0
            ? []
            : await db.Queryable<AsterSceneAssetEntity>()
                .Where(item => !item.IsDeleted && assetIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var assetMap = assets.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        return new AsterSceneRuntimeManifestDto
        {
            PublishCode = publishCode,
            DocumentHash = documentHash,
            EntrySceneId = entrySceneId,
            Document = document.Clone(),
            CapabilityPolicy = ParseObject(new
            {
                allowBlueprintApis = new[] { "/api/public/asterscene/runtime-events" },
                allowIFrame = true,
                allowRemix = true,
                allowExternalLinks = false
            }),
            AssetVariants = ParseObject(variants.GroupBy(item => item.AssetId).ToDictionary(
                group => group.Key,
                group => group.Select(item => new
                {
                    assetVersionId = item.Id,
                    version = item.Version,
                    item.VariantType,
                    runtimeUrl = item.Url,
                    sourceUrl = assetMap.TryGetValue(item.AssetId, out var asset) ? asset.SourceUrl : item.Url,
                    item.ContentType,
                    item.SizeBytes,
                    item.Checksum
                }).ToArray())),
            Preload = ParseObject(new
            {
                groups = new[] { entrySceneId },
                budgetBytes = 20 * 1024 * 1024
            }),
            LazyGroups = ParseObject(new
            {
                strategy = "scene-distance",
                maxParallel = 4
            }),
            Security = ParseObject(new
            {
                csp = "default-src 'self'; img-src 'self' data: https:; media-src 'self' https:; connect-src 'self'",
                signedAssetUrls = false
            }),
            Analytics = ParseObject(new
            {
                events = new[] { "view", "scene-enter", "hotspot-click", "remix" },
                sampleRate = 1
            })
        };
    }

    private async Task<AsterScenePublicWorkEntity> UpsertPublicWorkAsync(
        AsterSceneWorkspace workspace,
        AsterSceneProjectEntity project,
        AsterScenePublishVersionEntity publish,
        CancellationToken cancellationToken)
    {
        var profile = await EnsureCreatorProfileAsync(workspace, cancellationToken);
        var work = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item => !item.IsDeleted && item.ProjectId == project.Id, cancellationToken);
        if (work is null)
        {
            work = new AsterScenePublicWorkEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                ProjectId = project.Id,
                Slug = await GenerateSlugAsync(project.ProjectName, project.ProjectCode, cancellationToken),
                CreatorUserId = workspace.UserId,
                CreatorHandle = profile.Handle,
                CreatedBy = workspace.UserId
            };
            profile.WorksCount += 1;
            profile.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(profile).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(work).ExecuteCommandAsync(cancellationToken);
        }

        work.PublishVersionId = publish.Id;
        work.PublishCode = publish.PublishCode;
        work.Title = project.ProjectName;
        work.Summary = project.Description;
        work.CoverAssetId = project.CoverAssetId;
        work.Visibility = publish.Visibility;
        work.Status = publish.Visibility == "Private" ? "Private" : "Published";
        work.PublishedAt = publish.PublishedAt;
        work.LastIndexedAt = DateTime.UtcNow;
        work.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(work).ExecuteCommandAsync(cancellationToken);
        return work;
    }

    private async Task<AsterSceneCreatorProfileEntity> EnsureCreatorProfileAsync(
        AsterSceneWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var profile = await db.Queryable<AsterSceneCreatorProfileEntity>()
            .FirstAsync(item => !item.IsDeleted && item.UserId == workspace.UserId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new AsterSceneCreatorProfileEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            UserId = workspace.UserId,
            Handle = await GenerateHandleAsync(workspace.UserId, cancellationToken),
            DisplayName = $"Creator {workspace.UserId[..Math.Min(workspace.UserId.Length, 6)]}",
            CreatedBy = workspace.UserId
        };
        await db.Insertable(profile).ExecuteCommandAsync(cancellationToken);
        return profile;
    }

    private async Task InsertPublishUsageAsync(
        AsterSceneWorkspace workspace,
        string projectId,
        string publishVersionId,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"published-work:{projectId}";
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
            UsageType = "published-work",
            Quantity = 1,
            Unit = "count",
            Direction = "Debit",
            SourceType = "project",
            SourceId = projectId,
            IdempotencyKey = idempotencyKey,
            CreatedBy = workspace.UserId
        }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task EnsurePublishedWorkQuotaAsync(
        string ownerUserId,
        string projectId,
        CancellationToken cancellationToken)
    {
        var existingWork = await db.Queryable<AsterScenePublicWorkEntity>()
            .AnyAsync(item => !item.IsDeleted && item.ProjectId == projectId, cancellationToken);
        if (existingWork)
        {
            return;
        }

        var subscription = await db.Queryable<AsterSceneSubscriptionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.OwnerUserId == ownerUserId && item.Status == "Active", cancellationToken);
        var plan = AsterScenePlanCatalog.GetPlan(subscription?.PlanCode);
        var currentWorks = await db.Queryable<AsterScenePublicWorkEntity>()
            .CountAsync(item => !item.IsDeleted && item.CreatorUserId == ownerUserId && item.Status != "Removed", cancellationToken);
        if (currentWorks >= plan.PublishedWorks)
        {
            throw new ValidationException("AsterScene published works quota exceeded.", ErrorCodes.AsterSceneQuotaExceeded);
        }
    }

    private async Task InsertRollbackUsageAsync(
        AsterSceneWorkspace workspace,
        string projectId,
        string publishVersionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
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
            UsageType = "publish-rollback",
            Quantity = 1,
            Unit = "count",
            Direction = "Debit",
            SourceType = "publish",
            SourceId = publishVersionId,
            IdempotencyKey = idempotencyKey,
            CreatedBy = workspace.UserId
        }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task InsertRuntimeViewAsync(
        AsterScenePublicWorkEntity work,
        CancellationToken cancellationToken)
    {
        var eventBucket = DateTime.UtcNow.ToString("yyyyMMddHH", global::System.Globalization.CultureInfo.InvariantCulture);
        var idempotencyKey = $"runtime-view:{work.Id}:{eventBucket}";
        var exists = await db.Queryable<AsterSceneUsageLedgerEntity>()
            .AnyAsync(item => !item.IsDeleted && item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (exists)
        {
            return;
        }

        await ExecuteInTransactionAsync(async () =>
        {
            await db.Insertable(new AsterSceneUsageLedgerEntity
            {
                TenantId = work.TenantId,
                AppCode = work.AppCode,
                OwnerUserId = work.CreatorUserId,
                ProjectId = work.ProjectId,
                UsageType = "runtime-view",
                Quantity = 1,
                Unit = "event",
                Direction = "Debit",
                SourceType = "public-work",
                SourceId = work.Id,
                IdempotencyKey = idempotencyKey,
                CreatedBy = "runtime"
            }).ExecuteCommandAsync(cancellationToken);

            work.ViewCount += 1;
            work.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(work).ExecuteCommandAsync(cancellationToken);
        });
    }

    private static string BuildRollbackIdempotencyKey(string projectId, string clientMutationId)
    {
        return $"rollback:{projectId}:{clientMutationId}";
    }

    private async Task<AsterScenePublicWorkDto?> GetPublicWorkByPublishAsync(
        string publishVersionId,
        CancellationToken cancellationToken)
    {
        var work = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item => !item.IsDeleted && item.PublishVersionId == publishVersionId, cancellationToken);
        return work is null ? null : AsterScenePublicService.MapWork(work);
    }

    private async Task<string> GeneratePublishCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = $"ASR{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(100000, 999999)}";
            var exists = await db.Queryable<AsterScenePublishVersionEntity>()
                .AnyAsync(item => !item.IsDeleted && item.PublishCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"ASR{Guid.NewGuid():N}"[..20].ToUpperInvariant();
    }

    private async Task<string> GenerateSlugAsync(string title, string suffix, CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(title);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "work";
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var candidate = attempt == 0
                ? $"{baseSlug}-{suffix.ToLowerInvariant()}"
                : $"{baseSlug}-{suffix.ToLowerInvariant()}-{attempt + 1}";
            var exists = await db.Queryable<AsterScenePublicWorkEntity>()
                .AnyAsync(item => !item.IsDeleted && item.Slug == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(baseSlug.Length + 17, 80)];
    }

    private async Task<string> GenerateHandleAsync(string userId, CancellationToken cancellationToken)
    {
        var seed = string.IsNullOrWhiteSpace(userId) ? Guid.NewGuid().ToString("N")[..8] : userId[..Math.Min(userId.Length, 8)];
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var handle = attempt == 0 ? $"creator-{seed}".ToLowerInvariant() : $"creator-{seed}-{attempt + 1}".ToLowerInvariant();
            var exists = await db.Queryable<AsterSceneCreatorProfileEntity>()
                .AnyAsync(item => !item.IsDeleted && item.Handle == handle, cancellationToken);
            if (!exists)
            {
                return handle;
            }
        }

        return $"creator-{Guid.NewGuid():N}"[..24];
    }

    private static List<string> ExtractDocumentAssetIds(JsonElement document)
    {
        var result = new List<string>();
        if (!document.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                var value = id.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<string, string> ExtractDocumentAssetKinds(JsonElement document)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!document.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.String ||
                !asset.TryGetProperty("kind", out var kind) ||
                kind.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var assetId = id.GetString();
            var assetKind = kind.GetString();
            if (!string.IsNullOrWhiteSpace(assetId) && !string.IsNullOrWhiteSpace(assetKind))
            {
                result[assetId] = assetKind;
            }
        }

        return result;
    }

    private static bool IsSafeBlueprintApi(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("/api/public/", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("..", StringComparison.Ordinal);
    }

    private static AsterSceneRuntimeManifestDto DeserializeManifest(string json)
    {
        return JsonSerializer.Deserialize<AsterSceneRuntimeManifestDto>(json, JsonOptions) ??
               throw new ValidationException("RuntimeManifest is invalid.", ErrorCodes.AsterScenePublishNotFound);
    }

    private static JsonElement ParseObject<T>(T value)
    {
        return AsterSceneDocumentKernel.ParseJson(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static string NormalizeVisibility(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Public" : value.Trim();
        return normalized.Equals("Private", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Unlisted", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Template", StringComparison.OrdinalIgnoreCase)
            ? normalized[..1].ToUpperInvariant() + normalized[1..].ToLowerInvariant()
            : "Public";
    }

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return value.Trim();
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '-' or '_')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }
        }

        return builder.ToString().Trim('-');
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
