using AsterERP.Api.Application.AsterScene;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared.Exceptions;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AsterSceneGovernanceWorkflowTests
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-asterscene-governance-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Moderation_decision_preserves_history_and_replay_is_idempotent()
    {
        using var db = CreateDb();
        InitTables(db);
        var work = CreateWork("work-1", "Published");
        await db.Insertable(work).ExecuteCommandAsync();
        var service = CreateService(db);

        var report = await service.ReportWorkAsync(work.Id, new AsterSceneModerationReportRequest
        {
            ReasonCode = "copyright",
            ClientMutationId = "report-1",
            Evidence = [new AsterSceneEvidenceInput { EvidenceType = "screenshot", Payload = JsonDocument.Parse("{\"url\":\"evidence\"}").RootElement }]
        });
        var removed = await service.DecideModerationAsync(report.Id, new AsterSceneModerationDecisionRequest
        {
            Decision = "Remove",
            Note = "confirmed",
            ClientMutationId = "decision-1"
        });
        var replay = await service.DecideModerationAsync(report.Id, new AsterSceneModerationDecisionRequest
        {
            Decision = "Remove",
            Note = "different note is ignored on replay",
            ClientMutationId = "decision-1"
        });

        Assert.Equal("Removed", removed.Status);
        Assert.Equal(removed.Status, replay.Status);
        Assert.Equal("Removed", (await db.Queryable<AsterScenePublicWorkEntity>().FirstAsync(item => item.Id == work.Id))!.Status);
        Assert.Single(await db.Queryable<AsterSceneModerationDecisionEntity>().ToListAsync());
        Assert.Single(await db.Queryable<AsterSceneModerationEvidenceEntity>().ToListAsync());
        var detail = await service.GetModerationCaseDetailAsync(report.Id);
        Assert.Single(detail.Decisions);
        Assert.Single(detail.Evidence);

        await Assert.ThrowsAsync<ValidationException>(() => service.DecideModerationAsync(report.Id, new AsterSceneModerationDecisionRequest
        {
            Decision = "Allow",
            ClientMutationId = "decision-2"
        }));
    }

    [Fact]
    public async Task Appeal_approval_restores_work_and_writes_appeal_and_restore_history()
    {
        using var db = CreateDb();
        InitTables(db);
        var work = CreateWork("work-2", "Published");
        await db.Insertable(work).ExecuteCommandAsync();
        var service = CreateService(db);
        var report = await service.ReportWorkAsync(work.Id, new AsterSceneModerationReportRequest
        {
            ReasonCode = "other",
            ClientMutationId = "report-2"
        });
        await service.DecideModerationAsync(report.Id, new AsterSceneModerationDecisionRequest
        {
            Decision = "Remove",
            ClientMutationId = "decision-2"
        });

        var appeal = await service.CreateAppealAsync(report.Id, new AsterSceneAppealRequest
        {
            Reason = "The decision is incorrect",
            ClientMutationId = "appeal-1",
            Evidence = [new AsterSceneEvidenceInput { EvidenceType = "document", Payload = JsonDocument.Parse("{\"id\":1}").RootElement }]
        });
        var approved = await service.DecideAppealAsync(appeal.Id, new AsterSceneAppealDecisionRequest
        {
            Decision = "Approve",
            Note = "appeal accepted",
            ClientMutationId = "appeal-decision-1"
        });
        var replay = await service.DecideAppealAsync(appeal.Id, new AsterSceneAppealDecisionRequest
        {
            Decision = "Approve",
            ClientMutationId = "appeal-decision-1"
        });

        Assert.Equal("Approved", approved.Status);
        Assert.Equal(approved.Status, replay.Status);
        Assert.Equal("Published", (await db.Queryable<AsterScenePublicWorkEntity>().FirstAsync(item => item.Id == work.Id))!.Status);
        Assert.Equal("Restored", (await db.Queryable<AsterSceneModerationCaseEntity>().FirstAsync(item => item.Id == report.Id))!.Status);
        Assert.Single(await db.Queryable<AsterSceneAppealEntity>().ToListAsync());
        Assert.Single(await db.Queryable<AsterSceneAppealDecisionEntity>().ToListAsync());
        Assert.Equal(2, await db.Queryable<AsterSceneModerationDecisionEntity>().CountAsync());
        Assert.Single(await db.Queryable<AsterSceneModerationEvidenceEntity>().Where(item => item.AppealId == appeal.Id).ToListAsync());
    }

    [Fact]
    public async Task Admin_can_manage_cross_owner_support_ticket_with_auditable_status_mutations()
    {
        using var db = CreateDb();
        InitTables(db);
        var ticket = new AsterSceneSupportTicketEntity
        {
            Id = "ticket-1",
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "creator-1",
            ProjectId = "project-1",
            Title = "Cannot publish",
            Severity = "High",
            Status = "Open",
            BundleJson = "{}"
        };
        await db.Insertable(ticket).ExecuteCommandAsync();
        var service = CreateService(db);

        var page = await service.GetSupportTicketsForAdminAsync(new AsterSceneGridQuery { PageSize = 20 });
        Assert.Single(page.Items);
        var commented = await service.AddAdminSupportCommentAsync(ticket.Id, new AsterSceneSupportCommentRequest
        {
            Message = "We are investigating",
            ClientMutationId = "admin-comment-1"
        });
        var closed = await service.ChangeAdminSupportTicketStatusAsync(ticket.Id, new AsterSceneSupportTicketStatusRequest
        {
            Status = "Closed",
            Note = "Resolved",
            ClientMutationId = "admin-status-1"
        });
        var replay = await service.ChangeAdminSupportTicketStatusAsync(ticket.Id, new AsterSceneSupportTicketStatusRequest
        {
            Status = "Closed",
            Note = "ignored replay",
            ClientMutationId = "admin-status-1"
        });

        Assert.Equal("Open", commented.Status);
        Assert.Equal("Closed", closed.Status);
        Assert.Equal("Closed", replay.Status);
        Assert.Equal(2, await db.Queryable<AsterSceneSupportCommentEntity>().CountAsync());
        Assert.Equal(2, (await service.GetSupportTicketForAdminAsync(ticket.Id)).Comments.Count);
    }

    private AsterSceneCommerceGovernanceService CreateService(ISqlSugarClient db)
    {
        return new AsterSceneCommerceGovernanceService(db, CreateWorkspaceContext(), new AsterSceneDocumentService(db, CreateWorkspaceContext()));
    }

    private SqlSugarClient CreateDb() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={databasePath}",
        DbType = DbType.Sqlite,
        InitKeyType = InitKeyType.Attribute,
        IsAutoCloseConnection = true
    });

    private static void InitTables(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables(
            typeof(AsterSceneProjectEntity), typeof(AsterSceneDocumentEntity), typeof(AsterScenePublishVersionEntity),
            typeof(AsterScenePublicWorkEntity), typeof(AsterSceneModerationCaseEntity), typeof(AsterSceneModerationDecisionEntity),
            typeof(AsterSceneModerationEvidenceEntity), typeof(AsterSceneAppealEntity), typeof(AsterSceneAppealDecisionEntity),
            typeof(AsterSceneSupportTicketEntity), typeof(AsterSceneSupportCommentEntity));
    }

    private static AsterScenePublicWorkEntity CreateWork(string id, string status) => new()
    {
        Id = id,
        TenantId = "tenant-system",
        AppCode = "SYSTEM",
        ProjectId = "project-1",
        PublishVersionId = "publish-1",
        PublishCode = $"PUB-{id}",
        Slug = id,
        Title = "Test work",
        CreatorUserId = "admin",
        CreatorHandle = "admin",
        Visibility = "Public",
        Status = status
    };

    private static AsterSceneWorkspaceContext CreateWorkspaceContext() => new(CreateCurrentUser());

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin", "admin", "tenant-system", "Tenant", "SYSTEM", "System", "root", "system-admin",
            ["role-admin"], ["admin"], ["*"], "ALL", true, true, true, "Admin"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return new Volo.Abp.Users.CurrentUser(new HttpContextCurrentPrincipalAccessor(accessor));
    }
}
