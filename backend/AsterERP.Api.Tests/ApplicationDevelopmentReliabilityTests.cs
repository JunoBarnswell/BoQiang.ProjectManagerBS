using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDevelopmentReliabilityTests
{
    [Fact]
    public async Task Save_with_same_canonical_hash_does_not_create_a_revision()
    {
        using var db = CreateDatabase("save-idempotency");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var store = new ApplicationDesignerDocumentStore(new ApplicationDevelopmentSchemaValidator());
        var document = BuildDocumentJson();

        var first = await store.SaveAsync(db, workspace, "page-a", "version-a", document, null, null, "{\"type\":\"edit\"}");
        var second = await store.SaveAsync(db, workspace, "page-a", "version-a", "{ \"runtimeContext\": { \"pageName\": \"Page A\", \"pageCode\": \"page-a\" }, \"elements\": { \"root\": { \"props\": {}, \"children\": [], \"type\": \"layout.page\", \"id\": \"root\" } }, \"pages\": [{ \"rootElementId\": \"root\", \"name\": \"Page A\", \"id\": \"page-a\" }], \"revision\": 1, \"documentId\": \"page-a\" }", first.DocumentHash, null, "{\"type\":\"duplicate\"}");

        Assert.True(first.CreatedRevision);
        Assert.False(second.CreatedRevision);
        Assert.Equal(first.DocumentHash, second.DocumentHash);
        Assert.Equal(first.RevisionId, second.RevisionId);
        Assert.Equal(1, await db.Queryable<ApplicationDesignerDocumentEntity>().CountAsync());
        Assert.Equal(1, await db.Queryable<ApplicationDesignerRevisionEntity>().CountAsync());
    }

    [Fact]
    public async Task Save_assigns_the_document_revision_that_matches_the_new_revision_row()
    {
        using var db = CreateDatabase("save-revision-consistency");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var store = new ApplicationDesignerDocumentStore(new ApplicationDevelopmentSchemaValidator());

        var first = await store.SaveAsync(db, workspace, "page-a", "version-a", BuildDocumentJson(), null, null, "{\"type\":\"create\"}");
        var changedDocument = JsonNode.Parse(BuildDocumentJson())!.AsObject();
        changedDocument["runtimeContext"]!["pageName"] = "Page A v2";

        var second = await store.SaveAsync(
            db,
            workspace,
            "page-a",
            "version-a",
            changedDocument.ToJsonString(),
            first.DocumentHash,
            null,
            "{\"type\":\"edit\"}");

        Assert.True(second.CreatedRevision);
        Assert.NotEqual(first.DocumentHash, second.DocumentHash);
        var persistedRevision = await db.Queryable<ApplicationDesignerRevisionEntity>()
            .SingleAsync(item => item.Id == second.RevisionId);
        var persistedEntity = await db.Queryable<ApplicationDesignerDocumentEntity>()
            .SingleAsync(item => item.PageId == "page-a");
        var persistedDocument = JsonNode.Parse(persistedEntity.DocumentJson)!.AsObject();

        Assert.Equal(2, persistedDocument["revision"]!.GetValue<int>());
        Assert.Equal(2, persistedRevision.RevisionNumber);
        Assert.Equal(persistedEntity.Id, persistedRevision.DocumentId);
        Assert.Equal(persistedEntity.DocumentJson, persistedRevision.DocumentJson);
        Assert.Equal(persistedEntity.DocumentHash, persistedRevision.DocumentHash);

        var current = await store.RequireCurrentAsync(db, workspace, "page-a");
        Assert.Equal(persistedEntity.DocumentHash, current.DocumentHash);
    }

    [Fact]
    public async Task Save_rejects_a_stale_hash_without_changing_current_revision()
    {
        using var db = CreateDatabase("save-cas");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var store = new ApplicationDesignerDocumentStore(new ApplicationDevelopmentSchemaValidator());
        var first = await store.SaveAsync(db, workspace, "page-a", "version-a", BuildDocumentJson(), null, null, "{}");
        var changed = JsonNode.Parse(BuildDocumentJson())!.AsObject();
        changed["runtimeContext"]!["pageName"] = "Page A changed";
        var second = await store.SaveAsync(db, workspace, "page-a", "version-a", changed.ToJsonString(), first.DocumentHash, null, "{}");

        var stale = JsonNode.Parse(changed.ToJsonString())!.AsObject();
        stale["runtimeContext"]!["pageName"] = "Page A stale editor";
        var exception = await Assert.ThrowsAsync<ValidationException>(() => store.SaveAsync(
            db, workspace, "page-a", "version-a", stale.ToJsonString(), first.DocumentHash, null, "{}"));

        Assert.Equal(ErrorCodes.ApplicationDevelopmentPageRevisionConflict, exception.Code);
        var current = await store.RequireCurrentAsync(db, workspace, "page-a");
        Assert.Equal(second.RevisionId, current.CurrentRevisionId);
        Assert.Equal(2, await db.Queryable<ApplicationDesignerRevisionEntity>().CountAsync());
    }

    [Fact]
    public async Task Save_requires_cas_hash_for_a_changed_existing_document()
    {
        using var db = CreateDatabase("save-cas-required");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var store = new ApplicationDesignerDocumentStore(new ApplicationDevelopmentSchemaValidator());
        await store.SaveAsync(db, workspace, "page-a", "version-a", BuildDocumentJson(), null, null, "{}");
        var changed = JsonNode.Parse(BuildDocumentJson())!.AsObject();
        changed["runtimeContext"]!["pageName"] = "Page A changed";

        var exception = await Assert.ThrowsAsync<ValidationException>(() => store.SaveAsync(
            db, workspace, "page-a", "version-a", changed.ToJsonString(), null, null, "{}"));

        Assert.Equal(ErrorCodes.ApplicationDevelopmentPageRevisionConflict, exception.Code);
        Assert.Equal(1, await db.Queryable<ApplicationDesignerRevisionEntity>().CountAsync());
    }

    [Fact]
    public async Task Cancelled_save_does_not_persist_a_partial_document()
    {
        using var db = CreateDatabase("save-cancel");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new ApplicationDesignerDocumentStore(new ApplicationDevelopmentSchemaValidator());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(
            db,
            new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a"),
            "page-a",
            "version-a",
            BuildDocumentJson(),
            null,
            null,
            "{}",
            cancellation.Token));

        using var verificationDb = CreateDatabaseFrom(db.Ado.Connection.ConnectionString);
        Assert.Equal(0, await verificationDb.Queryable<ApplicationDesignerDocumentEntity>().CountAsync());
        Assert.Equal(0, await verificationDb.Queryable<ApplicationDesignerRevisionEntity>().CountAsync());
    }

    [Fact]
    public async Task Publish_failure_inside_the_service_transaction_does_not_update_the_document_pointer()
    {
        using var db = CreateDatabase("publish-rollback");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = new ApplicationDesignerDocumentEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            PageId = "page-a",
            VersionId = "version-a",
            DocumentJson = BuildDocumentJson(),
            DocumentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(BuildDocumentJson()),
            SourceHash = "source",
            TargetHash = "target",
            CurrentRevisionId = "revision-a",
            PublishedArtifactId = "previous-artifact",
            Status = "Published"
        };
        var previousArtifact = new ApplicationDesignerRuntimeArtifactEntity
        {
            Id = "previous-artifact",
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            DocumentId = document.Id,
            RevisionId = "revision-previous",
            ArtifactJson = "{\"documentId\":\"page-a\"}",
            ArtifactHash = "sha256:previous",
            SourceHash = "source-previous",
            TargetHash = "target-previous",
            ManifestHash = "manifest-previous",
            ManifestJson = "{}",
            SignatureHash = "signature-previous",
            RevisionNumber = 1,
            CompilerRevision = "runtime-1",
            MigrationRevision = "latest",
            Status = "Published",
            DiagnosticsJson = "[]",
            PublishedTime = DateTime.UtcNow
        };
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);
        await db.Insertable(previousArtifact).ExecuteCommandAsync(CancellationToken.None);
        await db.Ado.ExecuteCommandAsync("CREATE TRIGGER fail_publish_record AFTER INSERT ON app_designer_runtime_artifacts BEGIN SELECT RAISE(ABORT, 'injected publish failure'); END;");

        var compiler = new ApplicationDevelopmentSchemaCompiler();
        var artifactJson = compiler.CompileSchema(
            "page-a",
            "Page A",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            document.DocumentJson,
            "{}");
        var publisher = new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator());

        await db.Ado.BeginTranAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => publisher.PublishAsync(
            db, workspace, document, artifactJson, "previous-artifact", CancellationToken.None));
        await db.Ado.RollbackTranAsync();

        var persisted = await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync();
        Assert.Equal("previous-artifact", persisted.PublishedArtifactId);
        Assert.Equal("Published", persisted.Status);
        Assert.Equal(1, await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().CountAsync());
        Assert.Equal(0, await db.Queryable<ApplicationDesignerPublishRecordEntity>().CountAsync());
    }

    private static string BuildDocumentJson() => """
    {
      "documentId": "page-a",
      "revision": 1,
      "pages": [{ "id": "page-a", "name": "Page A", "rootElementId": "root" }],
      "elements": { "root": { "id": "root", "type": "layout.page", "children": [], "props": {} } },
      "runtimeContext": { "pageCode": "page-a", "pageName": "Page A" }
    }
    """;

    private static SqlSugarClient CreateDatabase(string name) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:application-development-reliability-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static SqlSugarClient CreateDatabaseFrom(string connectionString) => new(new ConnectionConfig
    {
        ConnectionString = connectionString,
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });
}
