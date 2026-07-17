using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Modules.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.AsterScene;

public sealed class AsterSceneCommerceGovernanceService(
    ISqlSugarClient db,
    AsterSceneWorkspaceContext workspaceContext,
    AsterSceneDocumentService documentService)
{
    public Task<IReadOnlyList<AsterSceneSubscriptionPlanDto>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AsterScenePlanCatalog.GetPlans());
    }

    public async Task<AsterSceneSubscriptionDto> GetSubscriptionAsync(CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var subscription = await EnsureSubscriptionAsync(workspace, cancellationToken);
        return MapSubscription(subscription);
    }

    public async Task<AsterSceneSubscriptionDto> SubscribeAsync(
        AsterSceneSubscribeRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var plan = AsterScenePlanCatalog.GetPlan(request.PlanCode);
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var subscription = await EnsureSubscriptionAsync(workspace, cancellationToken);
        if (string.Equals(subscription.ClientMutationId, clientMutationId, StringComparison.OrdinalIgnoreCase))
        {
            return MapSubscription(subscription);
        }

        await ExecuteInTransactionAsync(async () =>
        {
            subscription.PlanCode = plan.PlanCode;
            subscription.Status = "Active";
            subscription.StartedAt = DateTime.UtcNow;
            subscription.EndsAt = null;
            subscription.ClientMutationId = clientMutationId;
            subscription.UpdatedBy = workspace.UserId;
            subscription.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(subscription).ExecuteCommandAsync(cancellationToken);
            await InsertUsageLedgerAsync(
                workspace,
                null,
                "subscription",
                1,
                "event",
                "Debit",
                "subscription-change",
                plan.PlanCode,
                $"subscription-change:{subscription.Id}:{clientMutationId}",
                JsonSerializer.Serialize(new { plan = plan.PlanCode }),
                cancellationToken);
            await InsertAiCreditLedgerAsync(
                workspace,
                null,
                plan.AiCreditsMonthly,
                "Credit",
                $"subscription-credit:{subscription.Id}:{clientMutationId}",
                cancellationToken);
        });

        return MapSubscription(subscription);
    }

    public Task<AsterSceneSubscriptionDto> CancelSubscriptionAsync(
        AsterSceneSubscriptionLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        return ChangeSubscriptionStateAsync(request, "Canceled", "subscription-cancel", closePeriod: true, cancellationToken);
    }

    public Task<AsterSceneSubscriptionDto> MarkSubscriptionPaymentFailedAsync(
        AsterSceneSubscriptionLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        return ChangeSubscriptionStateAsync(request, "PastDue", "subscription-payment-failed", closePeriod: false, cancellationToken);
    }

    public Task<AsterSceneSubscriptionDto> ExpireSubscriptionAsync(
        AsterSceneSubscriptionLifecycleRequest request,
        CancellationToken cancellationToken = default)
    {
        return ChangeSubscriptionStateAsync(request, "Expired", "subscription-expire", closePeriod: true, cancellationToken);
    }

    private async Task<AsterSceneSubscriptionDto> ChangeSubscriptionStateAsync(
        AsterSceneSubscriptionLifecycleRequest request,
        string status,
        string sourceType,
        bool closePeriod,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var subscription = await EnsureSubscriptionAsync(workspace, cancellationToken);
        var idempotencyKey = $"{sourceType}:{subscription.Id}:{clientMutationId}";
        var exists = await db.Queryable<AsterSceneUsageLedgerEntity>()
            .AnyAsync(item => !item.IsDeleted && item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (exists)
        {
            return MapSubscription(subscription);
        }

        await ExecuteInTransactionAsync(async () =>
        {
            subscription.Status = status;
            subscription.ClientMutationId = clientMutationId;
            subscription.UpdatedBy = workspace.UserId;
            subscription.UpdatedTime = DateTime.UtcNow;
            if (closePeriod)
            {
                subscription.EndsAt = DateTime.UtcNow;
            }

            await db.Updateable(subscription).ExecuteCommandAsync(cancellationToken);
            await InsertUsageLedgerAsync(
                workspace,
                null,
                "subscription",
                1,
                "event",
                "Debit",
                sourceType,
                subscription.Id,
                idempotencyKey,
                JsonSerializer.Serialize(new { reason = NormalizeOptional(request.Reason), status }),
                cancellationToken);
        });

        return MapSubscription(subscription);
    }

    public async Task<AsterSceneUsageSummaryDto> GetUsageSummaryAsync(CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var subscription = await EnsureSubscriptionAsync(workspace, cancellationToken);
        var plan = ResolveEffectivePlan(subscription);
        var storageRows = await db.Queryable<AsterSceneUsageLedgerEntity>()
            .Where(item => !item.IsDeleted && item.OwnerUserId == workspace.UserId && item.UsageType == "storage")
            .ToListAsync(cancellationToken);
        var storageUsed = storageRows.Sum(item => item.Direction == "Credit" ? -item.Quantity : item.Quantity);
        var publishRows = await db.Queryable<AsterSceneUsageLedgerEntity>()
            .Where(item => !item.IsDeleted && item.OwnerUserId == workspace.UserId && item.UsageType == "published-work")
            .ToListAsync(cancellationToken);
        var publishedUsed = publishRows.Sum(item => item.Direction == "Credit" ? -item.Quantity : item.Quantity);
        var aiRows = await db.Queryable<AsterSceneAiCreditLedgerEntity>()
            .Where(item => !item.IsDeleted && item.OwnerUserId == workspace.UserId)
            .ToListAsync(cancellationToken);
        var aiCredits = aiRows.Sum(item => item.Direction == "Credit" ? item.Credits : -item.Credits);

        return new AsterSceneUsageSummaryDto
        {
            PlanCode = plan.PlanCode,
            StorageGbUsed = Math.Max(0, storageUsed),
            StorageGbLimit = plan.StorageGb,
            AiCreditsRemaining = aiCredits,
            PublishedWorksUsed = Math.Max(0, publishedUsed),
            PublishedWorksLimit = plan.PublishedWorks
        };
    }

    public async Task<GridPageResult<AsterSceneUsageLedgerDto>> GetUsageLedgerAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var dbQuery = db.Queryable<AsterSceneUsageLedgerEntity>()
            .Where(item => !item.IsDeleted && item.OwnerUserId == workspace.UserId);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var type = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.UsageType == type);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.OccurredAt, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        return new GridPageResult<AsterSceneUsageLedgerDto>
        {
            Total = total.Value,
            Items = rows.Select(MapUsage).ToList()
        };
    }

    public async Task<AsterSceneModerationCaseDto> ReportWorkAsync(
        string workId,
        AsterSceneModerationReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var reason = NormalizeRequired(request.ReasonCode, "Reason code is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existing = await db.Queryable<AsterSceneModerationCaseEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.ReporterUserId == workspace.UserId &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.WorkId, workId, StringComparison.Ordinal))
            {
                throw new ValidationException("clientMutationId has already been used for another report.", ErrorCodes.StateChangeNotAllowed);
            }
            return MapModeration(existing);
        }

        var work = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == workId && item.Status == "Published", cancellationToken);
        if (work is null)
        {
            throw new NotFoundException("AsterScene public work was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        var entity = new AsterSceneModerationCaseEntity
        {
            TenantId = work.TenantId,
            AppCode = work.AppCode,
            WorkId = work.Id,
            ProjectId = work.ProjectId,
            ReporterUserId = workspace.UserId,
            ReasonCode = reason,
            Detail = NormalizeOptional(request.Detail),
            Status = "Open",
            ClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };
        await ExecuteInTransactionAsync(async () =>
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await InsertEvidenceAsync(entity.TenantId, entity.AppCode, entity.Id, null, workspace.UserId, request.Evidence, cancellationToken);
        });
        return MapModeration(entity);
    }

    public async Task<GridPageResult<AsterSceneModerationCaseDto>> GetModerationCasesAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        _ = workspaceContext.Resolve();
        var dbQuery = db.Queryable<AsterSceneModerationCaseEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        return new GridPageResult<AsterSceneModerationCaseDto>
        {
            Total = total.Value,
            Items = rows.Select(MapModeration).ToList()
        };
    }

    public async Task<AsterSceneModerationCaseDto> DecideModerationAsync(
        string caseId,
        AsterSceneModerationDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var decision = NormalizeModerationDecision(request.Decision);
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var entity = await db.Queryable<AsterSceneModerationCaseEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == caseId, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException("AsterScene moderation case was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        var existingDecision = await db.Queryable<AsterSceneModerationDecisionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.CaseId == caseId && item.ClientMutationId == clientMutationId, cancellationToken);
        if (existingDecision is not null)
        {
            return MapModeration(entity);
        }

        EnsureModerationTransition(entity.Status, decision);
        await ExecuteInTransactionAsync(async () =>
        {
            entity.Status = decision switch
            {
                "Allow" => "Allowed",
                "Remove" => "Removed",
                "Restore" => "Restored",
                _ => throw new ValidationException("Unsupported moderation decision.", ErrorCodes.StateChangeNotAllowed)
            };
            entity.Decision = decision;
            entity.DecisionNote = NormalizeOptional(request.Note);
            entity.DecidedBy = workspace.UserId;
            entity.DecidedAt = DateTime.UtcNow;
            entity.UpdatedBy = workspace.UserId;
            entity.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(new AsterSceneModerationDecisionEntity
            {
                TenantId = entity.TenantId,
                AppCode = entity.AppCode,
                CaseId = entity.Id,
                Decision = decision,
                Note = entity.DecisionNote,
                ActorUserId = workspace.UserId,
                ClientMutationId = clientMutationId,
                CreatedBy = workspace.UserId
            }).ExecuteCommandAsync(cancellationToken);

            if ((decision == "Remove" || decision == "Restore" || decision == "Allow") && !string.IsNullOrWhiteSpace(entity.WorkId))
            {
                var work = await db.Queryable<AsterScenePublicWorkEntity>()
                    .FirstAsync(item => !item.IsDeleted && item.Id == entity.WorkId, cancellationToken);
                if (work is not null)
                {
                    work.Status = decision == "Remove" ? "Removed" : "Published";
                    if (decision == "Remove")
                    {
                        work.UpdatedBy = workspace.UserId;
                    }
                    work.UpdatedTime = DateTime.UtcNow;
                    await db.Updateable(work).ExecuteCommandAsync(cancellationToken);
                }
            }
        });

        return MapModeration(entity);
    }

    public async Task<AsterSceneModerationCaseDetailDto> GetModerationCaseDetailAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequireModerationCaseAsync(caseId, cancellationToken);
        var decisions = await db.Queryable<AsterSceneModerationDecisionEntity>()
            .Where(item => !item.IsDeleted && item.CaseId == entity.Id)
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);
        var evidence = await db.Queryable<AsterSceneModerationEvidenceEntity>()
            .Where(item => !item.IsDeleted && item.CaseId == entity.Id)
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);
        var appeals = await db.Queryable<AsterSceneAppealEntity>()
            .Where(item => !item.IsDeleted && item.CaseId == entity.Id)
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);
        return new AsterSceneModerationCaseDetailDto
        {
            Id = entity.Id,
            WorkId = entity.WorkId,
            ProjectId = entity.ProjectId,
            ReasonCode = entity.ReasonCode,
            Status = entity.Status,
            Decision = entity.Decision,
            DecisionNote = entity.DecisionNote,
            DecidedAt = entity.DecidedAt,
            CreatedTime = entity.CreatedTime,
            Decisions = decisions.Select(MapDecision).ToList(),
            Evidence = evidence.Select(MapEvidence).ToList(),
            Appeals = appeals.Select(MapAppeal).ToList()
        };
    }

    public async Task<AsterSceneAppealDto> CreateAppealAsync(
        string caseId,
        AsterSceneAppealRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var entity = await RequireModerationCaseAsync(caseId, cancellationToken);
        var work = await RequireAppealableWorkAsync(entity, workspace.UserId, cancellationToken);
        if (!string.Equals(entity.Status, "Removed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Only removed works can be appealed.", ErrorCodes.StateChangeNotAllowed);
        }

        var reason = NormalizeRequired(request.Reason, "Appeal reason is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existing = await db.Queryable<AsterSceneAppealEntity>()
            .FirstAsync(item => !item.IsDeleted && item.CaseId == entity.Id && item.AppellantUserId == workspace.UserId && item.ClientMutationId == clientMutationId, cancellationToken);
        if (existing is not null)
        {
            return MapAppeal(existing);
        }

        var activeAppeal = await db.Queryable<AsterSceneAppealEntity>()
            .AnyAsync(item => !item.IsDeleted && item.CaseId == entity.Id && item.AppellantUserId == workspace.UserId && item.Status == "Submitted", cancellationToken);
        if (activeAppeal)
        {
            throw new ValidationException("An appeal is already pending for this case.", ErrorCodes.StateChangeNotAllowed);
        }

        var appeal = new AsterSceneAppealEntity
        {
            TenantId = entity.TenantId,
            AppCode = entity.AppCode,
            CaseId = entity.Id,
            AppellantUserId = work.CreatorUserId,
            Reason = reason,
            Status = "Submitted",
            ClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };
        await ExecuteInTransactionAsync(async () =>
        {
            await db.Insertable(appeal).ExecuteCommandAsync(cancellationToken);
            await InsertEvidenceAsync(entity.TenantId, entity.AppCode, entity.Id, appeal.Id, workspace.UserId, request.Evidence, cancellationToken);
        });
        return MapAppeal(appeal);
    }

    public async Task<GridPageResult<AsterSceneAppealDto>> GetAppealsAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        _ = workspaceContext.Resolve();
        var dbQuery = db.Queryable<AsterSceneAppealEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            dbQuery = dbQuery.Where(item => item.Status == query.Status.Trim());
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        return new GridPageResult<AsterSceneAppealDto> { Total = total.Value, Items = rows.Select(MapAppeal).ToList() };
    }

    public async Task<AsterSceneAppealDto> DecideAppealAsync(
        string appealId,
        AsterSceneAppealDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var decision = NormalizeAppealDecision(request.Decision);
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var appeal = await db.Queryable<AsterSceneAppealEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == appealId, cancellationToken);
        if (appeal is null)
        {
            throw new NotFoundException("AsterScene appeal was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        var existing = await db.Queryable<AsterSceneAppealDecisionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.AppealId == appeal.Id && item.ClientMutationId == clientMutationId, cancellationToken);
        if (existing is not null)
        {
            return MapAppeal(appeal);
        }
        if (!string.Equals(appeal.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Only submitted appeals can be decided.", ErrorCodes.StateChangeNotAllowed);
        }

        var moderationCase = await RequireModerationCaseAsync(appeal.CaseId, cancellationToken);
        await ExecuteInTransactionAsync(async () =>
        {
            appeal.Status = decision == "Approve" ? "Approved" : "Rejected";
            appeal.UpdatedBy = workspace.UserId;
            appeal.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(appeal).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(new AsterSceneAppealDecisionEntity
            {
                TenantId = appeal.TenantId,
                AppCode = appeal.AppCode,
                AppealId = appeal.Id,
                Decision = decision,
                Note = NormalizeOptional(request.Note),
                ActorUserId = workspace.UserId,
                ClientMutationId = clientMutationId,
                CreatedBy = workspace.UserId
            }).ExecuteCommandAsync(cancellationToken);
            if (decision == "Approve")
            {
                EnsureModerationTransition(moderationCase.Status, "Restore");
                moderationCase.Status = "Restored";
                moderationCase.Decision = "Restore";
                moderationCase.DecisionNote = NormalizeOptional(request.Note);
                moderationCase.DecidedBy = workspace.UserId;
                moderationCase.DecidedAt = DateTime.UtcNow;
                moderationCase.UpdatedBy = workspace.UserId;
                moderationCase.UpdatedTime = DateTime.UtcNow;
                await db.Updateable(moderationCase).ExecuteCommandAsync(cancellationToken);
                await db.Insertable(new AsterSceneModerationDecisionEntity
                {
                    TenantId = moderationCase.TenantId,
                    AppCode = moderationCase.AppCode,
                    CaseId = moderationCase.Id,
                    Decision = "Restore",
                    Note = NormalizeOptional(request.Note),
                    ActorUserId = workspace.UserId,
                    ClientMutationId = $"appeal:{clientMutationId}",
                    CreatedBy = workspace.UserId
                }).ExecuteCommandAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(moderationCase.WorkId))
                {
                    var work = await db.Queryable<AsterScenePublicWorkEntity>().FirstAsync(item => !item.IsDeleted && item.Id == moderationCase.WorkId, cancellationToken);
                    if (work is not null)
                    {
                        work.Status = "Published";
                        work.UpdatedBy = workspace.UserId;
                        work.UpdatedTime = DateTime.UtcNow;
                        await db.Updateable(work).ExecuteCommandAsync(cancellationToken);
                    }
                }
            }
        });
        return MapAppeal(appeal);
    }

    public async Task<AsterSceneJobDto> CreateAiGenerateJobAsync(
        AsterSceneAiGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var project = await documentService.RequireProjectAsync(request.ProjectId, cancellationToken);
        if (request.ExpectedRevision != project.CurrentRevision)
        {
            throw new ValidationException("AI generation request has a stale revision.", ErrorCodes.AsterSceneDocumentConflict);
        }

        var prompt = NormalizeRequired(request.Prompt, "Prompt is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existing = await db.Queryable<AsterSceneJobEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.OwnerUserId == workspace.UserId &&
                item.ProjectId == project.Id &&
                item.IdempotencyKey == clientMutationId,
                cancellationToken);
        if (existing is not null)
        {
            return AsterSceneAssetService.MapJob(existing);
        }

        var job = new AsterSceneJobEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            OwnerUserId = workspace.UserId,
            JobCode = $"AI{Guid.NewGuid():N}",
            JobType = "AiSceneGenerate",
            Status = "Pending",
            ProgressPercent = 0,
            IdempotencyKey = clientMutationId,
            InputJson = JsonSerializer.Serialize(new { prompt, request.ExpectedRevision }),
            CreatedBy = workspace.UserId
        };

        await ExecuteInTransactionAsync(async () =>
        {
            await EnsureAiCreditsAsync(workspace.UserId, 10, cancellationToken);
            await db.Insertable(job).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(new AsterSceneAiCreditLedgerEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                JobId = job.Id,
                Credits = 10,
                Direction = "Debit",
                IdempotencyKey = $"ai-job:{job.Id}",
                CreatedBy = workspace.UserId
            }).ExecuteCommandAsync(cancellationToken);
        });

        return AsterSceneAssetService.MapJob(job);
    }

    public async Task<AsterSceneSupportTicketDto> CreateSupportTicketAsync(
        AsterSceneSupportBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var project = await documentService.RequireProjectAsync(request.ProjectId, cancellationToken);
        var title = NormalizeRequired(request.Title, "Support title is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existing = await db.Queryable<AsterSceneSupportTicketEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.OwnerUserId == workspace.UserId &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existing is not null)
        {
            return MapSupport(existing);
        }

        var ticket = new AsterSceneSupportTicketEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ProjectId = project.Id,
            Title = title,
            Severity = NormalizeSeverity(request.Severity),
            Status = "Open",
            BundleJson = SerializeDiagnostics(request.Diagnostics),
            ClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };
        await db.Insertable(ticket).ExecuteCommandAsync(cancellationToken);
        return MapSupport(ticket);
    }

    public async Task<AsterSceneSupportTicketDetailDto> GetSupportTicketAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var ticket = await RequireSupportTicketAsync(ticketId, workspace.UserId, cancellationToken);
        var comments = await LoadSupportCommentsAsync(ticket.Id, cancellationToken);
        return MapSupportDetail(ticket, comments);
    }

    public async Task<AsterSceneSupportTicketDetailDto> AddSupportCommentAsync(
        string ticketId,
        AsterSceneSupportCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var ticket = await RequireSupportTicketAsync(ticketId, workspace.UserId, cancellationToken);
        if (ticket.Status == "Closed")
        {
            throw new ValidationException("Support ticket is already closed.", ErrorCodes.ParameterInvalid);
        }

        var message = NormalizeRequired(request.Message, "Support comment is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existing = await db.Queryable<AsterSceneSupportCommentEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.TicketId == ticket.Id &&
                item.OwnerUserId == workspace.UserId &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existing is null)
        {
            await db.Insertable(new AsterSceneSupportCommentEntity
            {
                TenantId = ticket.TenantId,
                AppCode = ticket.AppCode,
                OwnerUserId = workspace.UserId,
                TicketId = ticket.Id,
                AuthorUserId = workspace.UserId,
                CommentType = "Comment",
                Message = message,
                StatusAfter = ticket.Status,
                ClientMutationId = clientMutationId,
                CreatedBy = workspace.UserId
            }).ExecuteCommandAsync(cancellationToken);
        }

        return MapSupportDetail(ticket, await LoadSupportCommentsAsync(ticket.Id, cancellationToken));
    }

    public async Task<AsterSceneSupportTicketDetailDto> CloseSupportTicketAsync(
        string ticketId,
        AsterSceneSupportCloseRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var ticket = await RequireSupportTicketAsync(ticketId, workspace.UserId, cancellationToken);
        var resolution = NormalizeRequired(request.Resolution, "Support resolution is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existing = await db.Queryable<AsterSceneSupportCommentEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.TicketId == ticket.Id &&
                item.OwnerUserId == workspace.UserId &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existing is not null || ticket.Status == "Closed")
        {
            return MapSupportDetail(ticket, await LoadSupportCommentsAsync(ticket.Id, cancellationToken));
        }

        await ExecuteInTransactionAsync(async () =>
        {
            ticket.Status = "Closed";
            ticket.UpdatedBy = workspace.UserId;
            ticket.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(ticket).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(new AsterSceneSupportCommentEntity
            {
                TenantId = ticket.TenantId,
                AppCode = ticket.AppCode,
                OwnerUserId = workspace.UserId,
                TicketId = ticket.Id,
                AuthorUserId = workspace.UserId,
                CommentType = "Close",
                Message = resolution,
                StatusAfter = "Closed",
                ClientMutationId = clientMutationId,
                CreatedBy = workspace.UserId
            }).ExecuteCommandAsync(cancellationToken);
        });

        return MapSupportDetail(ticket, await LoadSupportCommentsAsync(ticket.Id, cancellationToken));
    }

    public async Task<GridPageResult<AsterSceneSupportTicketDto>> GetSupportTicketsForAdminAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var dbQuery = db.Queryable<AsterSceneSupportTicketEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            dbQuery = dbQuery.Where(item => item.Status == query.Status.Trim());
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        return new GridPageResult<AsterSceneSupportTicketDto> { Total = total.Value, Items = rows.Select(MapSupport).ToList() };
    }

    public async Task<AsterSceneSupportTicketDetailDto> GetSupportTicketForAdminAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await RequireSupportTicketForAdminAsync(ticketId, cancellationToken);
        return MapSupportDetail(ticket, await LoadSupportCommentsAsync(ticket.Id, cancellationToken));
    }

    public async Task<AsterSceneSupportTicketDetailDto> AddAdminSupportCommentAsync(
        string ticketId,
        AsterSceneSupportCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var ticket = await RequireSupportTicketForAdminAsync(ticketId, cancellationToken);
        var message = NormalizeRequired(request.Message, "Support comment is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existing = await db.Queryable<AsterSceneSupportCommentEntity>()
            .FirstAsync(item => !item.IsDeleted && item.TicketId == ticket.Id && item.ClientMutationId == clientMutationId, cancellationToken);
        if (existing is null)
        {
            await db.Insertable(new AsterSceneSupportCommentEntity
            {
                TenantId = ticket.TenantId,
                AppCode = ticket.AppCode,
                OwnerUserId = ticket.OwnerUserId,
                TicketId = ticket.Id,
                AuthorUserId = workspace.UserId,
                CommentType = "AdminComment",
                Message = message,
                StatusAfter = ticket.Status,
                ClientMutationId = clientMutationId,
                CreatedBy = workspace.UserId
            }).ExecuteCommandAsync(cancellationToken);
        }

        return MapSupportDetail(ticket, await LoadSupportCommentsAsync(ticket.Id, cancellationToken));
    }

    public async Task<AsterSceneSupportTicketDetailDto> ChangeAdminSupportTicketStatusAsync(
        string ticketId,
        AsterSceneSupportTicketStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var ticket = await RequireSupportTicketForAdminAsync(ticketId, cancellationToken);
        var status = NormalizeSupportStatus(request.Status);
        var note = NormalizeRequired(request.Note, "Support status note is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existing = await db.Queryable<AsterSceneSupportCommentEntity>()
            .FirstAsync(item => !item.IsDeleted && item.TicketId == ticket.Id && item.ClientMutationId == clientMutationId, cancellationToken);
        if (existing is not null)
        {
            return MapSupportDetail(ticket, await LoadSupportCommentsAsync(ticket.Id, cancellationToken));
        }
        if (string.Equals(ticket.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Support ticket is already in the requested state.", ErrorCodes.StateChangeNotAllowed);
        }

        await ExecuteInTransactionAsync(async () =>
        {
            ticket.Status = status;
            ticket.UpdatedBy = workspace.UserId;
            ticket.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(ticket).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(new AsterSceneSupportCommentEntity
            {
                TenantId = ticket.TenantId,
                AppCode = ticket.AppCode,
                OwnerUserId = ticket.OwnerUserId,
                TicketId = ticket.Id,
                AuthorUserId = workspace.UserId,
                CommentType = "AdminStatus",
                Message = note,
                StatusAfter = status,
                ClientMutationId = clientMutationId,
                CreatedBy = workspace.UserId
            }).ExecuteCommandAsync(cancellationToken);
        });
        return MapSupportDetail(ticket, await LoadSupportCommentsAsync(ticket.Id, cancellationToken));
    }

    private async Task<AsterSceneSupportTicketEntity> RequireSupportTicketAsync(
        string ticketId,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var ticket = await db.Queryable<AsterSceneSupportTicketEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == ticketId && item.OwnerUserId == ownerUserId, cancellationToken);
        if (ticket is null)
        {
            throw new NotFoundException("AsterScene support ticket was not found.", ErrorCodes.ParameterInvalid);
        }

        return ticket;
    }

    private async Task<AsterSceneSupportTicketEntity> RequireSupportTicketForAdminAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var ticket = await db.Queryable<AsterSceneSupportTicketEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == ticketId && item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode, cancellationToken);
        if (ticket is null)
        {
            throw new NotFoundException("AsterScene support ticket was not found.", ErrorCodes.ParameterInvalid);
        }

        return ticket;
    }

    private async Task<IReadOnlyList<AsterSceneSupportCommentEntity>> LoadSupportCommentsAsync(
        string ticketId,
        CancellationToken cancellationToken)
    {
        return await db.Queryable<AsterSceneSupportCommentEntity>()
            .Where(item => !item.IsDeleted && item.TicketId == ticketId)
            .OrderBy(item => item.CreatedTime)
            .ToListAsync(cancellationToken);
    }

    private async Task<AsterSceneSubscriptionEntity> EnsureSubscriptionAsync(
        AsterSceneWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var subscription = await db.Queryable<AsterSceneSubscriptionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.OwnerUserId == workspace.UserId, cancellationToken);
        if (subscription is not null)
        {
            return subscription;
        }

        subscription = new AsterSceneSubscriptionEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            PlanCode = "free",
            Status = "Active",
            CreatedBy = workspace.UserId
        };
        await db.Insertable(subscription).ExecuteCommandAsync(cancellationToken);
        await db.Insertable(new AsterSceneAiCreditLedgerEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            Credits = AsterScenePlanCatalog.GetPlan("free").AiCreditsMonthly,
            Direction = "Credit",
            IdempotencyKey = $"subscription-credit:{subscription.Id}:initial",
            CreatedBy = workspace.UserId
        }).ExecuteCommandAsync(cancellationToken);
        return subscription;
    }

    private async Task EnsureAiCreditsAsync(string ownerUserId, decimal requiredCredits, CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<AsterSceneAiCreditLedgerEntity>()
            .Where(item => !item.IsDeleted && item.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);
        var balance = rows.Sum(item => item.Direction == "Credit" ? item.Credits : -item.Credits);
        if (balance < requiredCredits)
        {
            throw new ValidationException("AsterScene AI credits are not enough.", ErrorCodes.AsterSceneQuotaExceeded);
        }
    }

    private async Task InsertUsageLedgerAsync(
        AsterSceneWorkspace workspace,
        string? projectId,
        string usageType,
        decimal quantity,
        string unit,
        string direction,
        string sourceType,
        string sourceId,
        string idempotencyKey,
        string? metadataJson,
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
            UsageType = usageType,
            Quantity = quantity,
            Unit = unit,
            Direction = direction,
            SourceType = sourceType,
            SourceId = sourceId,
            IdempotencyKey = idempotencyKey,
            MetadataJson = metadataJson,
            CreatedBy = workspace.UserId
        }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task InsertAiCreditLedgerAsync(
        AsterSceneWorkspace workspace,
        string? jobId,
        decimal credits,
        string direction,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<AsterSceneAiCreditLedgerEntity>()
            .AnyAsync(item => !item.IsDeleted && item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (exists)
        {
            return;
        }

        await db.Insertable(new AsterSceneAiCreditLedgerEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            JobId = jobId,
            Credits = credits,
            Direction = direction,
            IdempotencyKey = idempotencyKey,
            CreatedBy = workspace.UserId
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static AsterSceneSubscriptionDto MapSubscription(AsterSceneSubscriptionEntity entity)
    {
        return new AsterSceneSubscriptionDto
        {
            Id = entity.Id,
            PlanCode = entity.PlanCode,
            Status = entity.Status,
            StartedAt = entity.StartedAt,
            EndsAt = entity.EndsAt
        };
    }

    private static AsterSceneSubscriptionPlanDto ResolveEffectivePlan(AsterSceneSubscriptionEntity subscription)
    {
        return subscription.Status == "Active"
            ? AsterScenePlanCatalog.GetPlan(subscription.PlanCode)
            : AsterScenePlanCatalog.GetPlan("free");
    }

    private static AsterSceneUsageLedgerDto MapUsage(AsterSceneUsageLedgerEntity entity)
    {
        return new AsterSceneUsageLedgerDto
        {
            Id = entity.Id,
            UsageType = entity.UsageType,
            Quantity = entity.Quantity,
            Unit = entity.Unit,
            Direction = entity.Direction,
            SourceType = entity.SourceType,
            SourceId = entity.SourceId,
            OccurredAt = entity.OccurredAt
        };
    }

    private static AsterSceneModerationCaseDto MapModeration(AsterSceneModerationCaseEntity entity)
    {
        return new AsterSceneModerationCaseDto
        {
            Id = entity.Id,
            WorkId = entity.WorkId,
            ProjectId = entity.ProjectId,
            ReasonCode = entity.ReasonCode,
            Status = entity.Status,
            Decision = entity.Decision,
            DecisionNote = entity.DecisionNote,
            DecidedAt = entity.DecidedAt,
            CreatedTime = entity.CreatedTime
        };
    }

    private static AsterSceneModerationDecisionDto MapDecision(AsterSceneModerationDecisionEntity entity)
    {
        return new AsterSceneModerationDecisionDto
        {
            Id = entity.Id,
            Decision = entity.Decision,
            Note = entity.Note,
            ActorUserId = entity.ActorUserId,
            ClientMutationId = entity.ClientMutationId,
            CreatedTime = entity.CreatedTime
        };
    }

    private static AsterSceneEvidenceDto MapEvidence(AsterSceneModerationEvidenceEntity entity)
    {
        return new AsterSceneEvidenceDto
        {
            Id = entity.Id,
            AppealId = entity.AppealId,
            EvidenceType = entity.EvidenceType,
            EvidenceHash = entity.EvidenceHash,
            SubmittedBy = entity.SubmittedBy,
            CreatedTime = entity.CreatedTime
        };
    }

    private static AsterSceneAppealDto MapAppeal(AsterSceneAppealEntity entity)
    {
        return new AsterSceneAppealDto
        {
            Id = entity.Id,
            CaseId = entity.CaseId,
            AppellantUserId = entity.AppellantUserId,
            Reason = entity.Reason,
            Status = entity.Status,
            ClientMutationId = entity.ClientMutationId,
            CreatedTime = entity.CreatedTime
        };
    }

    private static AsterSceneSupportTicketDto MapSupport(AsterSceneSupportTicketEntity entity)
    {
        return new AsterSceneSupportTicketDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            Title = entity.Title,
            Status = entity.Status,
            Severity = entity.Severity,
            CreatedTime = entity.CreatedTime
        };
    }

    private static AsterSceneSupportTicketDetailDto MapSupportDetail(
        AsterSceneSupportTicketEntity entity,
        IReadOnlyList<AsterSceneSupportCommentEntity> comments)
    {
        return new AsterSceneSupportTicketDetailDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            Title = entity.Title,
            Status = entity.Status,
            Severity = entity.Severity,
            CreatedTime = entity.CreatedTime,
            Diagnostics = string.IsNullOrWhiteSpace(entity.BundleJson)
                ? null
                : AsterSceneDocumentKernel.ParseJson(entity.BundleJson),
            Comments = comments.Select(MapSupportComment).ToList()
        };
    }

    private static AsterSceneSupportCommentDto MapSupportComment(AsterSceneSupportCommentEntity entity)
    {
        return new AsterSceneSupportCommentDto
        {
            Id = entity.Id,
            TicketId = entity.TicketId,
            CommentType = entity.CommentType,
            Message = entity.Message,
            StatusAfter = entity.StatusAfter,
            CreatedTime = entity.CreatedTime
        };
    }

    private static string NormalizeModerationDecision(string? value)
    {
        var decision = NormalizeRequired(value, "Moderation decision is required.");
        if (decision.Equals("Allow", StringComparison.OrdinalIgnoreCase)) return "Allow";
        if (decision.Equals("Remove", StringComparison.OrdinalIgnoreCase)) return "Remove";
        if (decision.Equals("Restore", StringComparison.OrdinalIgnoreCase)) return "Restore";
        throw new ValidationException("Unsupported moderation decision.", ErrorCodes.StateChangeNotAllowed);
    }

    private static string NormalizeAppealDecision(string? value)
    {
        var decision = NormalizeRequired(value, "Appeal decision is required.");
        if (decision.Equals("Approve", StringComparison.OrdinalIgnoreCase)) return "Approve";
        if (decision.Equals("Reject", StringComparison.OrdinalIgnoreCase)) return "Reject";
        throw new ValidationException("Unsupported appeal decision.", ErrorCodes.StateChangeNotAllowed);
    }

    private static string NormalizeSupportStatus(string? value)
    {
        var status = NormalizeRequired(value, "Support status is required.");
        if (status.Equals("Open", StringComparison.OrdinalIgnoreCase)) return "Open";
        if (status.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return "Closed";
        throw new ValidationException("Unsupported support ticket status.", ErrorCodes.StateChangeNotAllowed);
    }

    private static void EnsureModerationTransition(string currentStatus, string decision)
    {
        var allowed = decision switch
        {
            "Allow" => currentStatus.Equals("Open", StringComparison.OrdinalIgnoreCase),
            "Remove" => currentStatus.Equals("Open", StringComparison.OrdinalIgnoreCase),
            "Restore" => currentStatus.Equals("Removed", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        if (!allowed)
        {
            throw new ValidationException($"Moderation state '{currentStatus}' cannot transition through '{decision}'.", ErrorCodes.StateChangeNotAllowed);
        }
    }

    private async Task<AsterSceneModerationCaseEntity> RequireModerationCaseAsync(string caseId, CancellationToken cancellationToken)
    {
        var entity = await db.Queryable<AsterSceneModerationCaseEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == caseId, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException("AsterScene moderation case was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        return entity;
    }

    private async Task<AsterScenePublicWorkEntity> RequireAppealableWorkAsync(
        AsterSceneModerationCaseEntity moderationCase,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(moderationCase.WorkId))
        {
            throw new ValidationException("The moderation case has no appealable work.", ErrorCodes.StateChangeNotAllowed);
        }

        var work = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == moderationCase.WorkId && item.CreatorUserId == userId, cancellationToken);
        if (work is null)
        {
            throw new ValidationException("Only the work owner can appeal this moderation case.", ErrorCodes.PermissionDenied);
        }

        return work;
    }

    private async Task InsertEvidenceAsync(
        string tenantId,
        string appCode,
        string caseId,
        string? appealId,
        string submittedBy,
        IReadOnlyList<AsterSceneEvidenceInput> inputs,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var evidenceType = NormalizeRequired(input.EvidenceType, "Evidence type is required.");
            if (evidenceType.Length > 64)
            {
                throw new ValidationException("Evidence type is too long.", ErrorCodes.ParameterInvalid);
            }

            var payload = input.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "{}" : input.Payload.GetRawText();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            await db.Insertable(new AsterSceneModerationEvidenceEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                CaseId = caseId,
                AppealId = appealId,
                SubmittedBy = submittedBy,
                EvidenceType = evidenceType,
                EvidenceJson = payload,
                EvidenceHash = hash,
                ClientMutationId = $"{appealId ?? "report"}:{caseId}:{index}:{hash}",
                CreatedBy = submittedBy
            }).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static string NormalizeSeverity(string? value)
    {
        var severity = string.IsNullOrWhiteSpace(value) ? "Normal" : value.Trim();
        return severity.Equals("High", StringComparison.OrdinalIgnoreCase) ||
               severity.Equals("Critical", StringComparison.OrdinalIgnoreCase)
            ? severity[..1].ToUpperInvariant() + severity[1..].ToLowerInvariant()
            : "Normal";
    }

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string SerializeDiagnostics(JsonElement diagnostics)
    {
        return diagnostics.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? "{}"
            : diagnostics.GetRawText();
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
