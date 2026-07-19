using System.Security.Claims;
using AsterERP.Api.Application.Im;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.Im;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.Im;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementImConversationServiceTests
{
    [Fact]
    public async Task Ensure_is_idempotent_and_sends_the_complete_effective_member_set()
    {
        using var db = await CreateDbAsync("ensure");
        await SeedProjectAsync(db);
        var im = new RecordingImConversationService();
        var activities = new RecordingActivityWriter();
        var service = CreateService(db, "manager", im, activities);

        var first = await service.EnsureAsync("project-a", new ProjectManagementImConversationEnsureRequest("task-a"));
        var second = await service.EnsureAsync("project-a", new ProjectManagementImConversationEnsureRequest("task-a"));

        Assert.Equal(first.ConversationId, second.ConversationId);
        Assert.Single(await db.Queryable<ProjectManagementImConversationLinkEntity>().Where(item => !item.IsDeleted).ToListAsync());
        Assert.Equal(2, im.EnsureRequests.Count);
        Assert.Equal(["assignee", "manager", "participant"], im.EnsureRequests[0].ParticipantUserIds.OrderBy(item => item).ToArray());
        Assert.Contains(activities.Events, item => item.ActivityType == "im-conversation.linked");
        Assert.Contains(activities.Entries, item => item.Operation == "im-conversation.linked");
    }

    [Fact]
    public async Task Removing_a_project_member_synchronizes_the_active_conversation_without_that_member()
    {
        using var db = await CreateDbAsync("revoke");
        await SeedProjectAsync(db);
        var im = new RecordingImConversationService();
        var service = CreateService(db, "manager", im);
        await service.EnsureAsync("project-a", new ProjectManagementImConversationEnsureRequest("task-a"));

        await service.RevokeProjectMemberAsync("project-a", "assignee");

        var participants = Assert.Single(im.SynchronizationRequests);
        Assert.DoesNotContain("assignee", participants.ParticipantUserIds);
        Assert.Contains("manager", participants.ParticipantUserIds);
    }

    [Fact]
    public async Task Resolving_a_conversation_target_rechecks_project_visibility()
    {
        using var db = await CreateDbAsync("target");
        await SeedProjectAsync(db);
        var im = new RecordingImConversationService();
        var ownerService = CreateService(db, "manager", im);
        var conversation = await ownerService.EnsureAsync("project-a", new ProjectManagementImConversationEnsureRequest("task-a"));
        var outsiderService = CreateService(db, "outsider", im);

        await Assert.ThrowsAsync<ValidationException>(() => outsiderService.ResolveTargetAsync(conversation.ConversationId));
        var target = await ownerService.ResolveTargetAsync(conversation.ConversationId);
        Assert.True(target.IsAvailable);
        Assert.Equal("/projects/project-a/tasks?taskId=task-a", target.TargetRoute);
    }

    private static ProjectManagementImConversationService CreateService(
        ISqlSugarClient db,
        string userId,
        RecordingImConversationService im,
        RecordingActivityWriter? activities = null) =>
        new(
            new TestWorkspaceDatabaseAccessor(db),
            CreateUser(userId),
            im,
            new ProjectManagementAccessPolicy(new TestWorkspaceDatabaseAccessor(db), CreateUser(userId)),
            activities ?? new RecordingActivityWriter(),
            activities ?? new RecordingActivityWriter());

    private static async Task<ISqlSugarClient> CreateDbAsync(string name)
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-im-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await new ImSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        return db;
    }

    private static async Task SeedProjectAsync(ISqlSugarClient db)
    {
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "项目 A", OwnerUserId = "owner"
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity
        {
            Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "A-1", Title = "任务 A-1", AssigneeUserId = "assignee"
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectMemberEntity { Id = "member-manager", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "manager", RoleCode = "Manager", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-assignee", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "assignee", RoleCode = "Member", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "member-participant", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "participant", RoleCode = "Member", IsActive = true }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskParticipantEntity
        {
            Id = "task-participant", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", UserId = "participant"
        }).ExecuteCommandAsync();
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId) => new(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(AsterErpClaimTypes.UserId, userId),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
    ], "test")));

    private sealed class RecordingImConversationService : IImConversationService
    {
        public List<ImGroupConversationRequest> EnsureRequests { get; } = [];
        public List<ImGroupConversationRequest> SynchronizationRequests { get; } = [];

        public Task<ImConversationResponse> EnsureGroupConversationAsync(ImGroupConversationRequest request, CancellationToken cancellationToken = default)
        {
            EnsureRequests.Add(request);
            return Task.FromResult(Response("conversation-a", request.ConversationKey, request.Title));
        }

        public Task SynchronizeGroupParticipantsAsync(string conversationId, IReadOnlyCollection<string> participantUserIds, CancellationToken cancellationToken = default)
        {
            SynchronizationRequests.Add(new ImGroupConversationRequest(conversationId, "", participantUserIds.ToArray()));
            return Task.CompletedTask;
        }

        public Task<ImConversationResponse> CreateDirectConversationAsync(string targetUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<ImConversationResponse>> GetConversationsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ArchiveGroupConversationAsync(string conversationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ActivateGroupConversationAsync(string conversationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ImMessagePageResponse> GetMessagesAsync(string conversationId, string? cursor, int take, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ImMessageResponse> SendMessageAsync(string conversationId, ImSendMessageRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ImUnreadSummaryResponse> MarkReadAsync(string conversationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ImUnreadSummaryResponse> GetUnreadSummaryAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        private static ImConversationResponse Response(string id, string key, string title) => new(id, "tenant-a", key, "Group", title, "Active", "", "", null, null, null, 0, 3, DateTime.UtcNow);
    }

    private sealed class RecordingActivityWriter : IProjectManagementActivityWriter, IProjectManagementSyncJournalWriter
    {
        public List<ProjectManagementActivityEvent> Events { get; } = [];
        public List<ProjectManagementSyncJournalEvent> Entries { get; } = [];
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default) { Events.Add(activity); return Task.CompletedTask; }
        public Task AppendAsync(ProjectManagementSyncJournalEvent entry, CancellationToken cancellationToken = default) { Entries.Add(entry); return Task.CompletedTask; }
    }

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
