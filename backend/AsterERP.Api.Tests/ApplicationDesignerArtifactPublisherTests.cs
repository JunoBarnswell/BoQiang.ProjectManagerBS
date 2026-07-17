using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDesignerArtifactPublisherTests
{
    [Fact]
    public async Task Concurrent_publish_of_same_artifact_creates_one_artifact_and_one_auditable_record()
    {
        var connectionString = $"Data Source=file:designer-publish-concurrency-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var setupDb = CreateDatabase(connectionString);
        await MigrateAsync(setupDb);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = CreateDocument(workspace, BuildDocumentJson());
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(setupDb, document);
        var artifactJson = new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "page-a", "Page A", ApplicationDevelopmentPageTypes.Standard, [], document.DocumentJson, "{}");

        using var firstDb = CreateDatabase(connectionString);
        using var secondDb = CreateDatabase(connectionString);
        var firstDocument = (await firstDb.Queryable<ApplicationDesignerDocumentEntity>().ToListAsync(CancellationToken.None)).Single();
        var secondDocument = (await secondDb.Queryable<ApplicationDesignerDocumentEntity>().ToListAsync(CancellationToken.None)).Single();
        var firstPublisher = new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator());
        var secondPublisher = new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator());

        var results = await Task.WhenAll(
            Task.Run(() => firstPublisher.PublishAsync(firstDb, workspace, firstDocument, artifactJson, null, CancellationToken.None)),
            Task.Run(() => secondPublisher.PublishAsync(secondDb, workspace, secondDocument, artifactJson, null, CancellationToken.None)));

        var artifacts = await setupDb.Queryable<ApplicationDesignerRuntimeArtifactEntity>().ToListAsync(CancellationToken.None);
        var records = await setupDb.Queryable<ApplicationDesignerPublishRecordEntity>().ToListAsync(CancellationToken.None);
        Assert.Single(artifacts);
        var record = Assert.Single(records);
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(artifacts[0].Id, record.ArtifactId);
        Assert.Equal(artifacts[0].ArtifactHash, record.ArtifactHash);
        Assert.Equal(artifacts[0].Id, record.TargetArtifactId);
        Assert.Equal(artifacts[0].ArtifactHash, record.TargetArtifactHash);
        Assert.StartsWith("publish:", record.OperationId);
    }

    [Fact]
    public async Task Migration_fails_closed_with_diagnostic_for_missing_publish_operation_id()
    {
        using var db = CreateDatabase($"Data Source=file:designer-publish-missing-operation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await MigrateAsync(db);
        await db.Insertable(new ApplicationDesignerPublishRecordEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            DocumentId = "document-a",
            ArtifactHash = "sha256:missing-operation",
            OperationType = "Publish",
            OperationId = null,
            Status = "Published",
            IsDeleted = false
        }).ExecuteCommandAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => MigrateAsync(db));
        Assert.Contains("missing OperationId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("document-a", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration_fails_closed_with_diagnostic_for_duplicate_active_artifacts()
    {
        using var db = CreateDatabase($"Data Source=file:designer-publish-duplicate-artifact-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await MigrateAsync(db);
        db.Ado.ExecuteCommand("DROP INDEX IF EXISTS idx_app_designer_artifacts_content;");
        await db.Insertable(CreateArtifact("artifact-a")).ExecuteCommandAsync(CancellationToken.None);
        await db.Insertable(CreateArtifact("artifact-b")).ExecuteCommandAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => MigrateAsync(db));
        Assert.Contains("duplicate active runtime artifacts", exception.Message, StringComparison.Ordinal);
        Assert.Contains("sha256:duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration_fails_closed_with_diagnostic_for_duplicate_publish_operation_ids()
    {
        using var db = CreateDatabase($"Data Source=file:designer-publish-duplicate-operation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await MigrateAsync(db);
        db.Ado.ExecuteCommand("DROP INDEX IF EXISTS idx_app_designer_publish_operation;");
        await db.Insertable(CreatePublishRecord("sha256:one")).ExecuteCommandAsync(CancellationToken.None);
        await db.Insertable(CreatePublishRecord("sha256:two")).ExecuteCommandAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => MigrateAsync(db));
        Assert.Contains("duplicate active OperationIds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("publish:duplicate", exception.Message, StringComparison.Ordinal);
    }

    private static async Task MigrateAsync(SqlSugarClient db) =>
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db))
            .MigrateAsync(db, CancellationToken.None);

    private static ApplicationDesignerRuntimeArtifactEntity CreateArtifact(string id) => new()
    {
        Id = id,
        TenantId = "tenant-a",
        AppCode = "MES",
        DocumentId = "document-a",
        ArtifactHash = "sha256:duplicate",
        Status = "Published",
        IsDeleted = false
    };

    private static ApplicationDesignerPublishRecordEntity CreatePublishRecord(string artifactHash) => new()
    {
        TenantId = "tenant-a",
        AppCode = "MES",
        DocumentId = "document-a",
        ArtifactHash = artifactHash,
        OperationType = "Publish",
        OperationId = "publish:duplicate",
        Status = "Published",
        IsDeleted = false
    };

    private static ApplicationDesignerDocumentEntity CreateDocument(
        ApplicationDataCenterWorkspace workspace,
        string documentJson)
    {
        var canonical = ApplicationDesignerCanonicalJson.NormalizeObject(documentJson);
        return new ApplicationDesignerDocumentEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            PageId = "page-a",
            VersionId = "version-a",
            DocumentJson = canonical,
            DocumentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(canonical),
            SourceHash = "source-hash",
            TargetHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(canonical),
            MigrationRevision = "latest",
            CurrentRevisionId = "revision-a",
            Status = "Draft"
        };
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

    private static SqlSugarClient CreateDatabase(string connectionString) => new(new ConnectionConfig
    {
        ConnectionString = connectionString,
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });
}
