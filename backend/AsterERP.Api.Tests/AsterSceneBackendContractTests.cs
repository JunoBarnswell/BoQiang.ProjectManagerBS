using System.Reflection;
using System.Text.Json;
using AsterERP.Api.Application.AsterScene;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AsterSceneBackendContractTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"astererp-asterscene-contract-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task UpdateProjectAsync_updates_project_and_public_runtime_visibility()
    {
        using var db = CreateDb();
        InitAsterSceneTables(db);
        var project = CreateProject(currentRevision: 2, visibility: "Public", status: "Published");
        project.CurrentPublishCode = "PUB001";
        var cover = new AsterSceneAssetEntity
        {
            Id = "cover-1",
            TenantId = project.TenantId,
            AppCode = project.AppCode,
            ProjectId = project.Id,
            OwnerUserId = project.OwnerUserId,
            AssetCode = "AST-COVER",
            AssetType = "image",
            FileName = "cover.png",
            Status = "Ready"
        };
        var publish = new AsterScenePublishVersionEntity
        {
            Id = "publish-version-1",
            TenantId = project.TenantId,
            AppCode = project.AppCode,
            ProjectId = project.Id,
            PublishCode = project.CurrentPublishCode,
            Status = "Active",
            DocumentRevision = project.CurrentRevision,
            DocumentHash = project.DocumentHash,
            Visibility = "Public",
            PublishedBy = project.OwnerUserId
        };
        var work = new AsterScenePublicWorkEntity
        {
            Id = "work-1",
            TenantId = project.TenantId,
            AppCode = project.AppCode,
            ProjectId = project.Id,
            PublishVersionId = publish.Id,
            PublishCode = publish.PublishCode,
            Slug = "old-project",
            Title = project.ProjectName,
            Summary = project.Description,
            CreatorUserId = project.OwnerUserId,
            CreatorHandle = "creator-admin",
            Visibility = "Public",
            Status = "Published"
        };
        await db.Insertable(project).ExecuteCommandAsync();
        await db.Insertable(cover).ExecuteCommandAsync();
        await db.Insertable(publish).ExecuteCommandAsync();
        await db.Insertable(work).ExecuteCommandAsync();

        var service = new AsterSceneDocumentService(db, CreateWorkspaceContext());
        var result = await service.UpdateProjectAsync(project.Id, new AsterSceneUpdateProjectRequest
        {
            ProjectName = "Updated Project",
            Description = "Updated description",
            Visibility = "Private",
            CoverAssetId = cover.Id,
            ClientMutationId = "edit-1"
        });

        var updatedProject = await db.Queryable<AsterSceneProjectEntity>().FirstAsync(item => item.Id == project.Id);
        var updatedPublish = await db.Queryable<AsterScenePublishVersionEntity>().FirstAsync(item => item.Id == publish.Id);
        var updatedWork = await db.Queryable<AsterScenePublicWorkEntity>().FirstAsync(item => item.Id == work.Id);

        Assert.Equal("Updated Project", result.ProjectName);
        Assert.Equal("Updated Project", updatedProject.ProjectName);
        Assert.Equal("Private", updatedProject.Visibility);
        Assert.Equal(cover.Id, updatedProject.CoverAssetId);
        Assert.Equal("Private", updatedPublish.Visibility);
        Assert.Equal("Updated Project", updatedWork.Title);
        Assert.Equal("Updated description", updatedWork.Summary);
        Assert.Equal(cover.Id, updatedWork.CoverAssetId);
        Assert.Equal("Private", updatedWork.Visibility);
        Assert.Equal("Private", updatedWork.Status);
    }

    [Fact]
    public async Task RestoreDocumentVersionAsync_creates_new_current_revision_and_replays_by_clientMutationId()
    {
        using var db = CreateDb();
        InitAsterSceneTables(db);
        var project = CreateProject(currentRevision: 2, visibility: "Private", status: "Draft");
        var revisionOneJson = AsterSceneDocumentKernel.CreateDefaultDocumentJson(project.Id, "Revision One");
        var revisionTwoJson = AsterSceneDocumentKernel.CreateDefaultDocumentJson(project.Id, "Revision Two");
        var revisionOneHash = AsterSceneDocumentKernel.ComputeHash(revisionOneJson);
        var revisionTwoHash = AsterSceneDocumentKernel.ComputeHash(revisionTwoJson);
        project.DocumentHash = revisionTwoHash;
        await db.Insertable(project).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new AsterSceneDocumentEntity
            {
                TenantId = project.TenantId,
                AppCode = project.AppCode,
                ProjectId = project.Id,
                Revision = 1,
                DocumentJson = revisionOneJson,
                DocumentHash = revisionOneHash,
                IsCurrent = false,
                SaveSource = "Create",
                SavedBy = project.OwnerUserId,
                ClientMutationId = "create-1"
            },
            new AsterSceneDocumentEntity
            {
                TenantId = project.TenantId,
                AppCode = project.AppCode,
                ProjectId = project.Id,
                Revision = 2,
                DocumentJson = revisionTwoJson,
                DocumentHash = revisionTwoHash,
                IsCurrent = true,
                SaveSource = "Manual",
                SavedBy = project.OwnerUserId,
                ClientMutationId = "save-2"
            }
        }).ExecuteCommandAsync();

        var service = new AsterSceneDocumentService(db, CreateWorkspaceContext());
        var restored = await service.RestoreDocumentVersionAsync(project.Id, 1, new AsterSceneRestoreDocumentVersionRequest
        {
            ExpectedRevision = 2,
            ClientMutationId = "restore-1"
        });
        var replayed = await service.RestoreDocumentVersionAsync(project.Id, 1, new AsterSceneRestoreDocumentVersionRequest
        {
            ExpectedRevision = 2,
            ClientMutationId = "restore-1"
        });

        var updatedProject = await db.Queryable<AsterSceneProjectEntity>().FirstAsync(item => item.Id == project.Id);
        var currentDocuments = await db.Queryable<AsterSceneDocumentEntity>()
            .Where(item => item.ProjectId == project.Id && item.IsCurrent)
            .ToListAsync();
        var restoredDocument = await db.Queryable<AsterSceneDocumentEntity>()
            .FirstAsync(item => item.ProjectId == project.Id && item.Revision == 3);

        Assert.Equal(3, restored.Revision);
        Assert.Equal(restored.Revision, replayed.Revision);
        Assert.Equal(3, updatedProject.CurrentRevision);
        Assert.Equal(revisionOneHash, updatedProject.DocumentHash);
        Assert.Single(currentDocuments);
        Assert.Equal(3, currentDocuments[0].Revision);
        Assert.Equal(revisionOneJson, restoredDocument.DocumentJson);
        Assert.Equal("RestoreRevision:1", restoredDocument.SaveSource);

        var conflict = await Assert.ThrowsAsync<ValidationException>(() => service.RestoreDocumentVersionAsync(project.Id, 1, new AsterSceneRestoreDocumentVersionRequest
        {
            ExpectedRevision = 2,
            ClientMutationId = "restore-2"
        }));
        Assert.Equal(ErrorCodes.AsterSceneDocumentConflict, conflict.Code);
    }

    [Fact]
    public async Task RecordRuntimeEventAsync_records_idempotent_ledger_for_published_non_private_work()
    {
        using var db = CreateDb();
        InitAsterSceneTables(db);
        var publish = CreatePublish("PUB-RUNTIME", visibility: "Public", status: "Active");
        var work = CreatePublicWork(publish, visibility: "Public", status: "Published");
        await db.Insertable(publish).ExecuteCommandAsync();
        await db.Insertable(work).ExecuteCommandAsync();

        var service = new AsterScenePublicService(db, CreateWorkspaceContext());
        var request = new AsterSceneRuntimeEventRequest
        {
            PublishCode = publish.PublishCode,
            EventType = "Hotspot-Click",
            SceneId = "scene-main",
            HotspotId = "hotspot-1",
            ClientEventId = "client-event-1"
        };

        var created = await service.RecordRuntimeEventAsync(request);
        var replayed = await service.RecordRuntimeEventAsync(request);
        var rows = await db.Queryable<AsterSceneUsageLedgerEntity>().ToListAsync();
        using var metadata = JsonDocument.Parse(rows[0].MetadataJson!);

        Assert.Equal(created.LedgerId, replayed.LedgerId);
        Assert.Single(rows);
        Assert.Equal("runtime-event", rows[0].UsageType);
        Assert.Equal("publish", rows[0].SourceType);
        Assert.Equal(publish.PublishCode, rows[0].SourceId);
        Assert.Equal("hotspot-click", created.EventType);
        Assert.Equal(publish.PublishCode, metadata.RootElement.GetProperty("publishCode").GetString());
        Assert.Equal("hotspot-click", metadata.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("client-event-1", metadata.RootElement.GetProperty("clientEventId").GetString());

        var conflict = await Assert.ThrowsAsync<ValidationException>(() => service.RecordRuntimeEventAsync(new AsterSceneRuntimeEventRequest
        {
            PublishCode = publish.PublishCode,
            EventType = "scene-enter",
            SceneId = "scene-other",
            ClientEventId = request.ClientEventId
        }));
        Assert.Equal(ErrorCodes.AsterSceneUsageLedgerConflict, conflict.Code);
    }

    [Theory]
    [InlineData("Private", "Active", "Public", "Published")]
    [InlineData("Public", "Superseded", "Public", "Published")]
    [InlineData("Public", "Active", "Private", "Private")]
    public async Task RecordRuntimeEventAsync_rejects_non_public_runtime(
        string publishVisibility,
        string publishStatus,
        string workVisibility,
        string workStatus)
    {
        using var db = CreateDb();
        InitAsterSceneTables(db);
        var publish = CreatePublish("PUB-PRIVATE", publishVisibility, publishStatus);
        var work = CreatePublicWork(publish, workVisibility, workStatus);
        await db.Insertable(publish).ExecuteCommandAsync();
        await db.Insertable(work).ExecuteCommandAsync();

        var service = new AsterScenePublicService(db, CreateWorkspaceContext());
        await Assert.ThrowsAsync<NotFoundException>(() => service.RecordRuntimeEventAsync(new AsterSceneRuntimeEventRequest
        {
            PublishCode = publish.PublishCode,
            EventType = "view",
            ClientEventId = "event-private"
        }));

        var ledgers = await db.Queryable<AsterSceneUsageLedgerEntity>().CountAsync();
        Assert.Equal(0, ledgers);
    }

    [Theory]
    [InlineData("Private", "Active", "Public", "Published")]
    [InlineData("Public", "Superseded", "Public", "Published")]
    [InlineData("Public", "Active", "Private", "Private")]
    public async Task GetRuntimeManifestAsync_rejects_non_public_runtime(
        string publishVisibility,
        string publishStatus,
        string workVisibility,
        string workStatus)
    {
        using var db = CreateDb();
        InitAsterSceneTables(db);
        var publish = CreatePublish("PUB-MANIFEST", publishVisibility, publishStatus);
        var work = CreatePublicWork(publish, workVisibility, workStatus);
        await db.Insertable(publish).ExecuteCommandAsync();
        await db.Insertable(work).ExecuteCommandAsync();

        var workspaceContext = CreateWorkspaceContext();
        var service = new AsterScenePublishService(
            db,
            workspaceContext,
            new AsterSceneDocumentService(db, workspaceContext));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetRuntimeManifestAsync(publish.PublishCode));
    }

    [Fact]
    public void UpdateProject_controller_action_uses_project_edit_permission()
    {
        var method = typeof(AsterSceneProjectsController).GetMethod(
            nameof(AsterSceneProjectsController.UpdateProjectAsync),
            BindingFlags.Instance | BindingFlags.Public);

        var route = method?.GetCustomAttribute<HttpPutAttribute>();
        var permission = method?.GetCustomAttribute<PermissionAttribute>();

        Assert.NotNull(method);
        Assert.Equal("{projectId}", route?.Template);
        Assert.Equal(PermissionCodes.AsterSceneProjectEdit, permission?.Code);
    }

    [Theory]
    [InlineData("/api/public/asterscene/explore")]
    [InlineData("/api/public/asterscene/player/PUB-1/manifest")]
    [InlineData("/api/community/asterscene/works/work-1/like")]
    public void Public_read_routes_use_the_public_data_permission_scope(string path)
    {
        var classifier = new DataPermissionRequestClassifier();

        Assert.True(classifier.IsAsterScenePublicReadApi(path));
    }

    [Theory]
    [InlineData("/api/asterscene/projects")]
    [InlineData("/api/usage/asterscene/ledger")]
    [InlineData("/api/admin/asterscene/moderation/cases")]
    public void Private_routes_do_not_use_the_public_data_permission_scope(string path)
    {
        var classifier = new DataPermissionRequestClassifier();

        Assert.False(classifier.IsAsterScenePublicReadApi(path));
    }

    [Theory]
    [InlineData(nameof(CommunityAsterSceneController.LikeAsync), "like")]
    [InlineData(nameof(CommunityAsterSceneController.FavoriteAsync), "favorite")]
    [InlineData(nameof(CommunityAsterSceneController.ReportAsync), "report")]
    public void Community_actions_keep_the_community_permission_boundary(string actionName, string route)
    {
        var method = typeof(CommunityAsterSceneController).GetMethod(actionName);

        Assert.NotNull(method);
        Assert.Equal(route, method?.GetCustomAttribute<HttpPostAttribute>()?.Template?.Split('/').Last());
        Assert.Equal(
            PermissionCodes.AsterSceneCommunityInteract,
            method?.GetCustomAttribute<PermissionAttribute>()?.Code);
    }

    [Fact]
    public void Remix_action_keeps_the_dedicated_remix_permission_boundary()
    {
        var method = typeof(CommunityAsterSceneController).GetMethod(nameof(CommunityAsterSceneController.RemixAsync));

        Assert.Equal(PermissionCodes.AsterSceneRemixCreate, method?.GetCustomAttribute<PermissionAttribute>()?.Code);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private SqlSugarClient CreateDb() =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    private static void InitAsterSceneTables(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables(
            typeof(AsterSceneProjectEntity),
            typeof(AsterSceneDocumentEntity),
            typeof(AsterSceneAssetEntity),
            typeof(AsterScenePublishVersionEntity),
            typeof(AsterScenePublicWorkEntity),
            typeof(AsterSceneUsageLedgerEntity));
    }

    private static AsterSceneProjectEntity CreateProject(int currentRevision, string visibility, string status)
    {
        return new AsterSceneProjectEntity
        {
            Id = "project-1",
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            ProjectCode = "AS-PROJECT-1",
            ProjectName = "Original Project",
            Description = "Original description",
            Visibility = visibility,
            Status = status,
            CurrentRevision = currentRevision,
            DocumentHash = "hash-current",
            CreatedBy = "admin"
        };
    }

    private static AsterScenePublishVersionEntity CreatePublish(string publishCode, string visibility, string status)
    {
        return new AsterScenePublishVersionEntity
        {
            Id = $"publish-{publishCode}",
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            ProjectId = "project-1",
            PublishCode = publishCode,
            Version = 1,
            Status = status,
            DocumentRevision = 1,
            DocumentHash = "hash-published",
            RuntimeManifestJson = "{}",
            EntrySceneId = "scene-main",
            Visibility = visibility,
            PublishedBy = "admin"
        };
    }

    private static AsterScenePublicWorkEntity CreatePublicWork(
        AsterScenePublishVersionEntity publish,
        string visibility,
        string status)
    {
        return new AsterScenePublicWorkEntity
        {
            Id = $"work-{publish.PublishCode}",
            TenantId = publish.TenantId,
            AppCode = publish.AppCode,
            ProjectId = publish.ProjectId,
            PublishVersionId = publish.Id,
            PublishCode = publish.PublishCode,
            Slug = publish.PublishCode.ToLowerInvariant(),
            Title = "Published Work",
            CreatorUserId = "admin",
            CreatorHandle = "creator-admin",
            Visibility = visibility,
            Status = status
        };
    }

    private static AsterSceneWorkspaceContext CreateWorkspaceContext()
    {
        return new AsterSceneWorkspaceContext(CreateCurrentUser());
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-system",
            "默认租户",
            "SYSTEM",
            "系统管理",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            ["*"],
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new Volo.Abp.Users.CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }
}
