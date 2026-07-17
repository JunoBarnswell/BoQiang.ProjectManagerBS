using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDesigner;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AsterERP.Api.Tests;

public sealed class RuntimePageSchemaServiceTests : IDisposable
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-runtime-page-{Guid.NewGuid():N}.db");
    private SqlSugarClient? database;

    [Fact]
    public async Task PublishedPage_RejectsArtifactMetadataMismatch()
    {
        var db = CreateDb();
        InitTables(db);
        await InsertPublishedRuntimeFixtureAsync(db, "page-orders", "tenant-a", "MES");
        await db.Updateable<ApplicationDesignerRuntimeArtifactEntity>()
            .SetColumns(item => new ApplicationDesignerRuntimeArtifactEntity
            {
                TargetHash = "sha256:tampered-target"
            })
            .Where(item => item.Id == "artifact-page-page-orders-tenant-a")
            .ExecuteCommandAsync();

        var service = CreateService(db, [PermissionCodes.BuildAppRuntimePagePermission("page-orders", "view")]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.GetPublishedPageAsync("page-orders"));

        Assert.Equal(ErrorCodes.RuntimePageSchemaInvalid, exception.Code);
    }

    [Fact]
    public async Task PublishedPage_DeniesUserWithoutViewPermission()
    {
        var db = CreateDb();
        InitTables(db);
        await InsertPublishedRuntimeFixtureAsync(db, "page-orders", "tenant-a", "MES");

        var service = CreateService(db, []);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.GetPublishedPageAsync("page-orders"));

        Assert.Equal(ErrorCodes.PermissionDenied, exception.Code);
    }

    [Fact]
    public async Task PublishedPage_RejectsRuntimeEditorState()
    {
        var db = CreateDb();
        InitTables(db);
        await InsertPublishedRuntimeFixtureAsync(
            db,
            "page-orders",
            "tenant-a",
            "MES",
            CreateRuntimeSchemaJson("page-orders").Replace("\"document\":{", "\"document\":{\"viewport\":{},", StringComparison.Ordinal));

        var service = CreateService(db, [PermissionCodes.BuildAppRuntimePagePermission("page-orders", "view")]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.GetPublishedPageAsync("page-orders"));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, exception.Code);
    }

    [Fact]
    public async Task PublishedPage_RejectsArtifactContentTamperingBeforeReturningRuntimeSchema()
    {
        var db = CreateDb();
        InitTables(db);
        var tampered = JsonNode.Parse(CreateRuntimeSchemaJson("page-orders"))!.AsObject();
        tampered["document"]!["pages"]![0]!.AsObject()["name"] = "Tampered after publish";
        await InsertPublishedRuntimeFixtureAsync(db, "page-orders", "tenant-a", "MES", tampered.ToJsonString());

        var service = CreateService(db, [PermissionCodes.BuildAppRuntimePagePermission("page-orders", "view")]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.GetPublishedPageAsync("page-orders"));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, exception.Code);
    }

    [Fact]
    public async Task PublishedPage_IsBoundToCurrentTenantAndApplication()
    {
        var db = CreateDb();
        InitTables(db);
        await InsertPublishedRuntimeFixtureAsync(db, "page-orders", "tenant-b", "MES");

        var service = CreateService(db, [PermissionCodes.BuildAppRuntimePagePermission("page-orders", "view")]);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => service.GetPublishedPageAsync("page-orders"));

        Assert.Equal(ErrorCodes.RuntimePageSchemaNotFound, exception.Code);
    }

    [Fact]
    public async Task PreviewPage_RequiresPreviewPermissionAndUsesCurrentWorkspacePage()
    {
        var db = CreateDb();
        InitTables(db);
        var page = new ApplicationDevelopmentPageEntity
        {
            Id = "preview-page-a",
            TenantId = "tenant-a",
            AppCode = "MES",
            PageCode = "page-orders",
            PageName = "Orders",
            PublishedArtifactId = "artifact-orders",
            VersionId = "version-a",
        };
        await db.Insertable(page).ExecuteCommandAsync();
        await db.Insertable(CreateDesignerDocument(page)).ExecuteCommandAsync();

        var deniedService = CreateService(db, []);
        var denied = await Assert.ThrowsAsync<ValidationException>(() =>
            deniedService.GetPublishedPageAsync("page-orders", page.Id));
        Assert.Equal(ErrorCodes.PermissionDenied, denied.Code);

        var allowedService = CreateService(db, [PermissionCodes.AppDevelopmentCenterDesignerPreview]);
        var response = await allowedService.GetPublishedPageAsync("page-orders", page.Id);

        Assert.Equal(page.Id, response.Id);
        Assert.Equal("tenant-a", response.TenantId);
        Assert.Equal("MES", response.AppCode);
        Assert.Equal("page-orders", response.PageCode);
    }

    [Fact]
    public async Task PreviewPage_CompilesDraftWithoutPublishedSchema()
    {
        var db = CreateDb();
        InitTables(db);
        var page = new ApplicationDevelopmentPageEntity
        {
            Id = "preview-draft-only",
            TenantId = "tenant-a",
            AppCode = "MES",
            PageCode = "page-draft-only",
            PageName = "Draft only",
            VersionId = "version-a"
        };
        await db.Insertable(page).ExecuteCommandAsync();
        await db.Insertable(CreateDesignerDocument(page)).ExecuteCommandAsync();

        var service = CreateService(db, [PermissionCodes.AppDevelopmentCenterDesignerPreview]);
        var response = await service.GetPublishedPageAsync(page.PageCode, page.Id);

        Assert.Equal(page.Id, response.Id);
        Assert.Equal(0, response.VersionNo);
        Assert.Contains("page-draft-only", response.ArtifactJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishedPage_AcceptsCompilerArtifactWithManifestDeclarations()
    {
        var db = CreateDb();
        InitTables(db);
        await InsertPublishedRuntimeFixtureAsync(db, "page-orders", "tenant-a", "MES");

        var service = CreateService(db, [PermissionCodes.BuildAppRuntimePagePermission("page-orders", "view")]);

        var response = await service.GetPublishedPageAsync("page-orders");

        Assert.Equal("page-orders", response.PageCode);
    }

    [Fact]
    public async Task PublishedPage_RejectsMissingManifestDeclarations()
    {
        var db = CreateDb();
        InitTables(db);
        await InsertPublishedRuntimeFixtureAsync(
            db,
            "page-orders",
            "tenant-a",
            "MES",
            CreateRuntimeSchemaJson("page-orders", includeManifestDeclarations: false));

        var service = CreateService(db, [PermissionCodes.BuildAppRuntimePagePermission("page-orders", "view")]);

        await Assert.ThrowsAsync<ValidationException>(() => service.GetPublishedPageAsync("page-orders"));
    }

    [Fact]
    public async Task PublishedPage_RejectsManifestDeclarationTamperingEvenWhenDocumentHashIsUnchanged()
    {
        var db = CreateDb();
        InitTables(db);
        var tampered = JsonNode.Parse(CreateRuntimeSchemaJson("page-orders"))!.AsObject();
        tampered["manifest"]![0]!.AsObject()["renderer"]!.AsObject()["runtime"] = "tampered-renderer";
        await InsertPublishedRuntimeFixtureAsync(db, "page-orders", "tenant-a", "MES", tampered.ToJsonString());

        var service = CreateService(db, [PermissionCodes.BuildAppRuntimePagePermission("page-orders", "view")]);

        await Assert.ThrowsAsync<ValidationException>(() => service.GetPublishedPageAsync("page-orders"));
    }

    private RuntimePageSchemaService CreateService(ISqlSugarClient db, IReadOnlyList<string> permissions)
    {
        return new RuntimePageSchemaService(
            new TestWorkspaceDatabaseAccessor(db),
            CreateCurrentUser(permissions),
            new AsterERP.Api.Application.ApplicationDevelopmentCenter.ApplicationDevelopmentSchemaCompiler(),
            new AsterERP.Api.Application.ApplicationDevelopmentCenter.ApplicationDevelopmentSchemaValidator());
    }

    private static async Task InsertPublishedRuntimeFixtureAsync(
        ISqlSugarClient db,
        string pageCode,
        string tenantId,
        string appCode,
        string? artifactJson = null)
    {
        var page = new ApplicationDevelopmentPageEntity
        {
            Id = $"page-{pageCode}-{tenantId}",
            TenantId = tenantId,
            AppCode = appCode,
            PageCode = pageCode,
            PageName = "Orders",
            PageType = "standard",
            VersionId = $"version-{pageCode}-{tenantId}",
            Status = "Published"
        };
        var document = CreatePublishedDocument(page);
        artifactJson ??= CreateRuntimeSchemaJson(pageCode);
        var artifact = CreateRuntimeArtifact(page, document, artifactJson);
        await db.Insertable(page).ExecuteCommandAsync();
        await db.Insertable(document).ExecuteCommandAsync();
        await db.Insertable(artifact).ExecuteCommandAsync();
    }

    private static ApplicationDesignerDocumentEntity CreatePublishedDocument(ApplicationDevelopmentPageEntity page) => new()
    {
        Id = $"document-{page.Id}",
        TenantId = page.TenantId,
        AppCode = page.AppCode,
        PageId = page.Id,
        VersionId = page.VersionId,
        DocumentJson = CreateDraftJson(page.PageCode),
        DocumentHash = $"sha256:document-{page.Id}",
        SourceHash = $"sha256:source-{page.Id}",
        TargetHash = $"sha256:target-{page.Id}",
        MigrationRevision = "latest",
        Status = "Published",
        CurrentRevisionId = $"revision-{page.Id}",
        PublishedArtifactId = $"artifact-{page.Id}"
    };

    private static ApplicationDesignerRuntimeArtifactEntity CreateRuntimeArtifact(
        ApplicationDevelopmentPageEntity page,
        ApplicationDesignerDocumentEntity document,
        string artifactJson)
    {
        using var parsed = JsonDocument.Parse(artifactJson);
        var root = parsed.RootElement;
        var manifestTypes = root.TryGetProperty("manifestTypes", out var types)
            ? types.GetRawText()
            : "[]";
        var declarations = root.TryGetProperty("manifest", out var manifest)
            ? manifest.GetRawText()
            : "[]";
        var manifestJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(
            $"{{\"types\":{manifestTypes},\"declarations\":{declarations}}}");
        return new ApplicationDesignerRuntimeArtifactEntity
        {
            Id = $"artifact-{page.Id}",
            TenantId = page.TenantId,
            AppCode = page.AppCode,
            DocumentId = document.Id,
            RevisionId = document.CurrentRevisionId!,
            ArtifactJson = artifactJson,
            ArtifactHash = ReadString(root, "artifactHash") ?? "sha256:fixture",
            SourceHash = document.SourceHash,
            TargetHash = document.DocumentHash,
            ManifestHash = ApplicationDesignerCanonicalJson.ComputeHash(manifestJson),
            ManifestJson = manifestJson,
            SignatureHash = ReadString(root, "signature") ?? string.Empty,
            RevisionNumber = root.TryGetProperty("revision", out var revision) && revision.TryGetInt32(out var value) ? value : 1,
            CompilerRevision = ReadString(root, "compilerVersion") ?? "runtime-1",
            MigrationRevision = ReadString(root, "migrationRevision") ?? "latest",
            Status = "Published",
            PublishedTime = DateTime.UtcNow
        };
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string CreateRuntimeSchemaJson(string pageCode, bool includeManifestDeclarations = true)
    {
        var document = $"{{\"documentId\":\"{pageCode}\",\"revision\":1,\"pages\":[{{\"id\":\"{pageCode}\",\"rootElementId\":\"{pageCode}_root\"}}],\"elements\":{{\"{pageCode}_root\":{{\"id\":\"{pageCode}_root\",\"parentId\":null,\"children\":[],\"type\":\"layout.page\"}}}}}}";
        var canonicalDocument = ApplicationDesignerCanonicalJson.NormalizeObject(document);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalDocument))).ToLowerInvariant();
        var artifactHash = $"sha256:{hash}";
        var canonicalDeclaration = RuntimeCapabilityContract.BuildArtifactManifest("layout.page").ToJsonString();
        var declarations = includeManifestDeclarations ? $"[{canonicalDeclaration}]" : "[]";
        var manifestHash = ApplicationDesignerCanonicalJson.ComputeHash(ApplicationDesignerCanonicalJson.NormalizeRuntimeObject($"{{\"types\":[\"layout.page\"],\"declarations\":{declarations}}}"));
        var signature = ApplicationDesignerCanonicalJson.ComputeSignature(pageCode, artifactHash, manifestHash, "runtime-1", "1");
        var manifest = includeManifestDeclarations ? $",\"manifest\":{declarations}" : string.Empty;
        return $"{{\"artifactHash\":\"{artifactHash}\",\"signature\":\"{signature}\",\"compilerVersion\":\"runtime-1\",\"migrationRevision\":\"latest\",\"manifestTypes\":[\"layout.page\"]{manifest},\"revision\":1,\"document\":{canonicalDocument}}}";
    }

    private static string CreateDraftJson(string pageCode)
    {
        using var runtime = JsonDocument.Parse(CreateRuntimeSchemaJson(pageCode));
        return runtime.RootElement.GetProperty("document").GetRawText();
    }

    private static ApplicationDesignerDocumentEntity CreateDesignerDocument(ApplicationDevelopmentPageEntity page) => new()
    {
        Id = $"document-{page.Id}",
        TenantId = page.TenantId,
        AppCode = page.AppCode,
        PageId = page.Id,
        VersionId = page.VersionId,
        DocumentJson = CreateDraftJson(page.PageCode),
        DocumentHash = "sha256:test",
        SourceHash = "sha256:test",
        TargetHash = "sha256:test",
        MigrationRevision = "test",
        Status = "Draft"
    };

    private static void InitTables(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<ApplicationDevelopmentPageEntity, ApplicationDesignerDocumentEntity, ApplicationDesignerRuntimeArtifactEntity>();
    }

    private SqlSugarClient CreateDb()
    {
        if (database is not null)
        {
            throw new InvalidOperationException("Each test instance may create only one database client.");
        }

        database = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath};Pooling=False",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
        return database;
    }

    private static ICurrentUser CreateCurrentUser(IReadOnlyList<string> permissions)
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-a",
            "客户A",
            "MES",
            "客户A MES",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            permissions,
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }

    public void Dispose()
    {
        if (database is not null)
        {
            database.Ado.Close();
            database.Dispose();
            database = null;
        }

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}
