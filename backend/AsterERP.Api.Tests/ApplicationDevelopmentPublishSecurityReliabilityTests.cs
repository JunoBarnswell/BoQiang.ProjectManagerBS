using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDevelopmentPublishSecurityReliabilityTests
{
    [Fact]
    public async Task Publisher_rejects_tampered_artifact_before_any_publish_rows_are_inserted()
    {
        using var db = CreateDatabase("tampered-artifact");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var documentJson = BuildDocumentJson();
        var document = CreateDocument(workspace, documentJson);
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);
        var compiler = new ApplicationDevelopmentSchemaCompiler();
        var artifactJson = compiler.CompileSchema(
            "page-a",
            "Page A",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            documentJson,
            "{}");
        var tamperedArtifact = JsonNode.Parse(artifactJson)!.AsObject();
        var tamperedPages = tamperedArtifact["document"]!["pages"]!.AsArray();
        tamperedPages[0]!.AsObject()["name"] = "Tampered after signing";

        await Assert.ThrowsAsync<ValidationException>(() => new ApplicationDesignerArtifactPublisher(
            new ApplicationDevelopmentSchemaValidator()).PublishAsync(
            db,
            workspace,
            document,
            tamperedArtifact.ToJsonString(ApplicationDataCenterJson.Options),
            null,
            CancellationToken.None));

        Assert.Equal(0, await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().CountAsync());
        Assert.Equal(0, await db.Queryable<ApplicationDesignerPublishRecordEntity>().CountAsync());
        var persistedDocument = await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync();
        Assert.Null(persistedDocument.PublishedArtifactId);
        Assert.Equal("Draft", persistedDocument.Status);
    }

    [Fact]
    public async Task Publisher_reuses_the_same_artifact_and_publish_record_for_duplicate_content()
    {
        using var db = CreateDatabase("duplicate-artifact");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = CreateDocument(workspace, BuildDocumentJson());
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);
        var artifactJson = new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "page-a",
            "Page A",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            document.DocumentJson,
            "{}");
        var publisher = new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator());

        var first = await publisher.PublishAsync(db, workspace, document, artifactJson, null, CancellationToken.None);
        var firstPublishedAt = first.PublishedTime;
        var second = await publisher.PublishAsync(db, workspace, document, artifactJson, first.Id, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ArtifactHash, second.ArtifactHash);
        Assert.Equal(firstPublishedAt, second.PublishedTime);
        Assert.Equal(1, await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().CountAsync());
        Assert.Equal(1, await db.Queryable<ApplicationDesignerPublishRecordEntity>().CountAsync());
        Assert.Equal(document.PageId, (await db.Queryable<ApplicationDesignerPublishRecordEntity>().SingleAsync()).PageId);
        var publishedArtifact = JsonNode.Parse(first.ArtifactJson)!.AsObject();
        var expectedManifestJson = ApplicationDesignerCanonicalJson.NormalizeObject(new JsonObject
        {
            ["types"] = publishedArtifact["manifestTypes"]!.DeepClone(),
            ["declarations"] = publishedArtifact["manifest"]!.DeepClone()
        }.ToJsonString());
        Assert.Equal(expectedManifestJson, first.ManifestJson);
        Assert.Equal(ApplicationDesignerCanonicalJson.ComputeHash(expectedManifestJson), first.ManifestHash);
        var persistedDocument = await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync();
        Assert.Equal(first.Id, persistedDocument.PublishedArtifactId);
        Assert.Equal("Published", persistedDocument.Status);
    }

    [Fact]
    public async Task Publisher_rejects_missing_manifest_declarations_before_any_publish_rows_are_inserted()
    {
        using var db = CreateDatabase("missing-manifest-declarations");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = CreateDocument(workspace, BuildDocumentJson());
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);
        var artifact = JsonNode.Parse(new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "page-a", "Page A", ApplicationDevelopmentPageTypes.Standard, [], document.DocumentJson, "{}"))!.AsObject();
        artifact.Remove("manifest");

        await Assert.ThrowsAsync<ValidationException>(() => new ApplicationDesignerArtifactPublisher(
            new ApplicationDevelopmentSchemaValidator()).PublishAsync(
            db, workspace, document, artifact.ToJsonString(ApplicationDataCenterJson.Options), null, CancellationToken.None));

        Assert.Empty(await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().ToListAsync(CancellationToken.None));
        Assert.Empty(await db.Queryable<ApplicationDesignerPublishRecordEntity>().ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Publisher_rejects_unknown_renderer_contract_before_any_publish_rows_are_inserted()
    {
        using var db = CreateDatabase("unknown-renderer");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = CreateDocument(workspace, BuildDocumentJson());
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);
        var artifact = JsonNode.Parse(new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "page-a", "Page A", ApplicationDevelopmentPageTypes.Standard, [], document.DocumentJson, "{}"))!.AsObject();
        artifact["manifest"]![0]!["renderer"]!["runtime"] = "LegacyRenderer";

        await Assert.ThrowsAsync<ValidationException>(() => new ApplicationDesignerArtifactPublisher(
            new ApplicationDevelopmentSchemaValidator()).PublishAsync(
            db, workspace, document, artifact.ToJsonString(ApplicationDataCenterJson.Options), null, CancellationToken.None));

        Assert.Empty(await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().ToListAsync(CancellationToken.None));
        Assert.Empty(await db.Queryable<ApplicationDesignerPublishRecordEntity>().ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Publisher_rejects_unknown_compiler_revision_before_any_publish_rows_are_inserted()
    {
        using var db = CreateDatabase("unknown-compiler");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = CreateDocument(workspace, BuildDocumentJson());
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);
        var artifact = JsonNode.Parse(new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "page-a", "Page A", ApplicationDevelopmentPageTypes.Standard, [], document.DocumentJson, "{}"))!.AsObject();
        artifact["compilerVersion"] = "runtime-legacy";

        await Assert.ThrowsAsync<ValidationException>(() => new ApplicationDesignerArtifactPublisher(
            new ApplicationDevelopmentSchemaValidator()).PublishAsync(
            db, workspace, document, artifact.ToJsonString(ApplicationDataCenterJson.Options), null, CancellationToken.None));

        Assert.Empty(await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().ToListAsync(CancellationToken.None));
        Assert.Empty(await db.Queryable<ApplicationDesignerPublishRecordEntity>().ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Cancelled_publish_does_not_insert_artifact_or_advance_the_previous_pointer()
    {
        using var db = CreateDatabase("publish-cancel");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = CreateDocument(workspace, BuildDocumentJson());
        document.PublishedArtifactId = "previous-artifact";
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);
        var artifactJson = new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "page-a",
            "Page A",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            document.DocumentJson,
            "{}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ApplicationDesignerArtifactPublisher(
            new ApplicationDevelopmentSchemaValidator()).PublishAsync(
            db,
            workspace,
            document,
            artifactJson,
            "previous-artifact",
            cancellation.Token));

        var persistedDocument = (await db.Queryable<ApplicationDesignerDocumentEntity>().ToListAsync(CancellationToken.None)).Single();
        Assert.Equal("previous-artifact", persistedDocument.PublishedArtifactId);
        Assert.Equal("Draft", persistedDocument.Status);
        Assert.Empty(await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().ToListAsync(CancellationToken.None));
        Assert.Empty(await db.Queryable<ApplicationDesignerPublishRecordEntity>().ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Publisher_persists_the_previous_artifact_as_source_lineage()
    {
        using var db = CreateDatabase("artifact-lineage");
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "user-a");
        var document = CreateDocument(workspace, BuildDocumentJson());
        var previous = new ApplicationDesignerRuntimeArtifactEntity
        {
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
        await db.Insertable(previous).ExecuteCommandAsync(CancellationToken.None);
        document.PublishedArtifactId = previous.Id;
        await ApplicationDesignerRevisionFixture.InsertDocumentAsync(db, document);

        var artifactJson = new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "page-a",
            "Page A",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            document.DocumentJson,
            "{}");

        var published = await new ApplicationDesignerArtifactPublisher(
            new ApplicationDevelopmentSchemaValidator()).PublishAsync(
            db,
            workspace,
            document,
            artifactJson,
            null,
            CancellationToken.None);

        Assert.Equal(previous.Id, published.SourceArtifactId);
        Assert.Equal(previous.ArtifactHash, published.SourceArtifactHash);
        Assert.Equal(previous.ArtifactJson, published.SourceArtifactJson);
        var record = (await db.Queryable<ApplicationDesignerPublishRecordEntity>().ToListAsync(CancellationToken.None)).Single();
        Assert.Equal(previous.Id, record.SourceArtifactId);
        Assert.Equal(previous.Id, record.BackupLocation);
    }

    [Fact]
    public void Monitoring_schema_allows_only_redacted_context_fields()
    {
        var root = FindRepositoryRoot();
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "docs",
            "low-code-refactor",
            "runtime-monitoring-event.schema.json")));
        var context = schema.RootElement.GetProperty("$defs").GetProperty("context");
        var contextProperties = context.GetProperty("properties");

        var forbidden = new[]
        {
            "secret",
            "secretRef",
            "cipherText",
            "connectionString",
            "password",
            "token",
            "apiKey",
            "rawSql",
            "parameters",
            "payload"
        };

        foreach (var property in forbidden)
        {
            Assert.False(contextProperties.TryGetProperty(property, out _), $"Sensitive monitoring field was allowed: {property}");
        }

        Assert.False(context.GetProperty("additionalProperties").GetBoolean());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
    }

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

    private static SqlSugarClient CreateDatabase(string name) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:application-development-publish-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsterERP.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("AsterERP.sln was not found.");
    }
}
