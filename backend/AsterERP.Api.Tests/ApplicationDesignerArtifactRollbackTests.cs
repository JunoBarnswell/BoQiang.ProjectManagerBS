using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDesignerArtifactRollbackTests
{
    [Fact]
    public async Task Rollback_switches_document_and_runtime_artifact_and_replays_by_operation_id()
    {
        using var db = CreateDatabase("success");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        db.CodeFirst.InitTables<SystemMenuEntity>();
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var page = new ApplicationDevelopmentPageEntity
        {
            Id = "page-a", TenantId = workspace.TenantId, AppCode = workspace.AppCode,
            PageCode = "page-a", PageName = "Page A", VersionId = "version-a"
        };
        var document = new ApplicationDesignerDocumentEntity
        {
            Id = "document-a", TenantId = workspace.TenantId, AppCode = workspace.AppCode,
            PageId = page.Id, VersionId = page.VersionId, DocumentJson = CanonicalDocument("first"),
            DocumentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(CanonicalDocument("first")),
            SourceHash = "source", TargetHash = "target", MigrationRevision = "latest", CurrentRevisionId = "revision-a"
        };
        await db.Insertable(page).ExecuteCommandAsync();
        await db.Insertable(new SystemMenuEntity
        {
            Id = "menu-a",
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            MenuCode = "page-a-menu",
            MenuName = "Page A",
            PageCode = page.PageCode,
            ArtifactId = "legacy-artifact-id",
            ScopeType = "ApplicationRuntime",
            IsDeleted = false
        }).ExecuteCommandAsync();
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);
        var compiler = new ApplicationDevelopmentSchemaCompiler();
        var publisher = new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator());
        var first = await publisher.PublishAsync(db, workspace, document, compiler.CompileSchema("page-a", "Page A", "standard", [], document.DocumentJson, "{}"), null);
        document.DocumentJson = CanonicalDocument("second");
        document.DocumentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(document.DocumentJson);
        await db.Updateable(document).ExecuteCommandAsync();
        await ApplicationDesignerRevisionFixture.SynchronizeCurrentRevisionAsync(db, document);
        var second = await publisher.PublishAsync(db, workspace, document, compiler.CompileSchema("page-a", "Page A", "standard", [], document.DocumentJson, "{}"), first.Id);

        var service = CreateService(db);
        var request = new ApplicationDesignerArtifactRollbackRequest
        {
            ArtifactId = first.Id, ArtifactHash = first.ArtifactHash, OperationId = "rollback-1", Reason = "regression"
        };
        var result = await service.RollbackAsync(page.Id, request, "trace-1");
        var replay = await service.RollbackAsync(page.Id, request, "trace-1");

        Assert.Equal(first.Id, result.ArtifactId);
        Assert.Equal(page.Id, result.PageId);
        Assert.NotEmpty(result.PublishedArtifactId);
        Assert.Equal(result.AuditId, replay.AuditId);
        Assert.Equal(result.PageId, replay.PageId);
        Assert.Equal(result.PublishedArtifactId, replay.PublishedArtifactId);
        Assert.Equal(first.Id, (await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync()).PublishedArtifactId);
        Assert.Equal(first.ArtifactJson, (await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().SingleAsync(item => item.Id == first.Id)).ArtifactJson);
        Assert.Equal(result.PublishedArtifactId, (await db.Queryable<ApplicationDevelopmentPageEntity>().SingleAsync()).PublishedArtifactId);
        Assert.Equal(first.Id, (await db.Queryable<SystemMenuEntity>().SingleAsync()).ArtifactId);
        Assert.Equal(1, await db.Queryable<ApplicationDesignerPublishRecordEntity>().CountAsync(item => item.OperationId == "rollback-1"));
        Assert.Equal(second.ArtifactHash, (await db.Queryable<ApplicationDesignerPublishRecordEntity>().SingleAsync(item => item.OperationId == "rollback-1")).SourceArtifactHash);

        var tamperedArtifact = JsonNode.Parse(first.ArtifactJson)!.AsObject();
        tamperedArtifact["manifest"]![0]!.AsObject()["renderer"]!.AsObject()["runtime"] = "tampered-renderer";
        first.ArtifactJson = tamperedArtifact.ToJsonString();
        await db.Updateable(first).UpdateColumns(item => item.ArtifactJson).ExecuteCommandAsync();
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RollbackAsync(
            page.Id,
            new ApplicationDesignerArtifactRollbackRequest
            {
                ArtifactId = first.Id, ArtifactHash = first.ArtifactHash, OperationId = "rollback-tampered-manifest", Reason = "verify"
            },
            "trace-tampered-manifest"));
        Assert.Equal(first.Id, (await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync()).PublishedArtifactId);

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RollbackAsync(
            "page-b", request, "trace-1"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RollbackAsync(
            page.Id,
            new ApplicationDesignerArtifactRollbackRequest
            {
                ArtifactId = second.Id,
                ArtifactHash = request.ArtifactHash,
                OperationId = request.OperationId,
                Reason = request.Reason
            },
            "trace-1"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RollbackAsync(
            page.Id,
            new ApplicationDesignerArtifactRollbackRequest
            {
                ArtifactId = request.ArtifactId,
                ArtifactHash = "sha256:different",
                OperationId = request.OperationId,
                Reason = request.Reason
            },
            "trace-1"));
        Assert.Equal(1, await db.Queryable<ApplicationDesignerPublishRecordEntity>().CountAsync(item => item.OperationId == "rollback-1"));
    }

    [Fact]
    public async Task Rollback_rejects_replay_when_operation_id_belongs_to_another_workspace()
    {
        using var db = CreateDatabase("replay-workspace");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ApplicationDesignerPublishRecordEntity
        {
            TenantId = "tenant-b",
            AppCode = "CRM",
            PageId = "page-a",
            ArtifactId = "artifact-a",
            ArtifactHash = "sha256:artifact-a",
            OperationId = "rollback-cross-workspace",
            OperationType = "Rollback",
            Status = "RollbackSucceeded",
            DocumentId = "document-a",
            IsDeleted = false
        }).ExecuteCommandAsync();

        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RollbackAsync(
            "page-a",
            new ApplicationDesignerArtifactRollbackRequest
            {
                ArtifactId = "artifact-a",
                ArtifactHash = "sha256:artifact-a",
                OperationId = "rollback-cross-workspace",
                Reason = "verify"
            },
            "trace-cross-workspace"));

        Assert.Equal(AsterERP.Shared.ErrorCodes.DesignerSchemaInvalid, exception.Code);
        Assert.Equal(1, await db.Queryable<ApplicationDesignerPublishRecordEntity>().CountAsync(item => item.OperationId == "rollback-cross-workspace"));
        Assert.Equal("tenant-b", (await db.Queryable<ApplicationDesignerPublishRecordEntity>().SingleAsync(item => item.OperationId == "rollback-cross-workspace")).TenantId);
    }

    [Fact]
    public async Task Rollback_rejects_tampered_target_and_writes_failure_audit()
    {
        using var db = CreateDatabase("failure");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = new ApplicationDesignerDocumentEntity
        {
            Id = "document-a", TenantId = workspace.TenantId, AppCode = workspace.AppCode, PageId = "page-a",
            DocumentJson = CanonicalDocument("first"), DocumentHash = "document-hash", CurrentRevisionId = "revision-a"
        };
        var page = new ApplicationDevelopmentPageEntity { Id = "page-a", TenantId = workspace.TenantId, AppCode = workspace.AppCode, PageCode = "page-a", PageName = "Page A", VersionId = "version-a" };
        await db.Insertable(page).ExecuteCommandAsync();
        await db.Insertable(document).ExecuteCommandAsync();
        var artifact = new ApplicationDesignerRuntimeArtifactEntity
        {
            TenantId = workspace.TenantId, AppCode = workspace.AppCode, DocumentId = document.Id,
            ArtifactJson = "{\"artifactHash\":\"bad\"}", ArtifactHash = "sha256:bad", Status = "Published"
        };
        await db.Insertable(artifact).ExecuteCommandAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RollbackAsync(
            page.Id,
            new ApplicationDesignerArtifactRollbackRequest { ArtifactId = artifact.Id, ArtifactHash = artifact.ArtifactHash, OperationId = "rollback-failed", Reason = "verify" },
            "trace-failed"));

        var audit = await db.Queryable<ApplicationDesignerPublishRecordEntity>().SingleAsync(item => item.OperationId == "rollback-failed");
        Assert.Equal("RollbackFailed", audit.Status);
        Assert.Equal("trace-failed", audit.TraceId);
        Assert.Null((await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync()).PublishedArtifactId);
    }

    private static ApplicationDesignerArtifactRollbackService CreateService(ISqlSugarClient db)
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "user-a", "user-a", "tenant-a", "Tenant A", "MES", "MES", "root", "system-admin",
            ["role-admin"], ["admin"], ["*"], "ALL", true, true, true, "user-a"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new ApplicationDesignerArtifactRollbackService(
            new TestWorkspaceDatabaseAccessor(db),
            new ApplicationDataCenterWorkspaceResolver(new CurrentUser(new HttpContextCurrentPrincipalAccessor(accessor))),
            new ApplicationDevelopmentSchemaValidator());
    }

    private static SqlSugarClient CreateDatabase(string name) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:artifact-rollback-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite, IsAutoCloseConnection = false
    });

    private static string CanonicalDocument(string value) => ApplicationDesignerCanonicalJson.NormalizeDocument(
        $"{{\"documentId\":\"page-a\",\"revision\":1,\"pages\":[{{\"id\":\"page-a\",\"name\":\"{value}\",\"rootElementId\":\"root\"}}],\"elements\":{{\"root\":{{\"id\":\"root\",\"type\":\"layout.page\",\"children\":[],\"props\":{{}}}}}},\"runtimeContext\":{{\"pageCode\":\"page-a\"}}}}");
}
