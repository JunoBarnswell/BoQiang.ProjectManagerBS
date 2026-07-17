using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDesigner;
using AsterERP.Shared.Exceptions;
using System.Text.Json.Nodes;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDevelopmentMigrationTests
{
    [Fact]
    public async Task Startup_schema_ensure_requires_a_committed_deployment_watermark()
    {
        using var db = CreateDatabase();
        var migrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.EnsureCurrentSchemaAsync(db, CancellationToken.None));
        Assert.Equal(0, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'app_designer_migration_watermarks'"));
    }

    [Fact]
    public async Task Deployment_migration_commits_watermark_and_startup_ensure_is_idempotent()
    {
        using var db = CreateDatabase();
        var migrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db));

        await migrator.RunDeploymentMigrationAsync(db, CancellationToken.None);
        await migrator.EnsureCurrentSchemaAsync(db, CancellationToken.None);
        await migrator.EnsureCurrentSchemaAsync(db, CancellationToken.None);

        var watermark = Assert.Single(await db.Queryable<ApplicationDesignerMigrationWatermarkEntity>().ToListAsync(CancellationToken.None));
        Assert.Equal("Retired", watermark.Status);
        Assert.Equal("designer-document-latest-v1", watermark.TargetSchemaFingerprint);
        Assert.Equal(1, db.Ado.GetInt("SELECT [unique] FROM pragma_index_list('app_designer_revisions') WHERE name = 'idx_app_designer_revisions_document'"));
    }

    [Fact]
    public async Task Deployment_migration_rejects_duplicate_revision_numbers_before_creating_cas_index()
    {
        using var db = CreateDatabase();
        var migrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db));
        await migrator.RunDeploymentMigrationAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("DROP INDEX idx_app_designer_revisions_document;");
        await db.Insertable(new ApplicationDesignerRevisionEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            DocumentId = "document-a",
            RevisionNumber = 1,
            DocumentJson = "{}"
        }).ExecuteCommandAsync(CancellationToken.None);
        await db.Insertable(new ApplicationDesignerRevisionEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            DocumentId = "document-a",
            RevisionNumber = 1,
            DocumentJson = "{}"
        }).ExecuteCommandAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => migrator.RunDeploymentMigrationAsync(db, CancellationToken.None));

        Assert.Contains("duplicate revision", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Startup_schema_ensure_does_not_retire_a_late_legacy_column()
    {
        using var db = CreateDatabase();
        var migrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db));
        await migrator.RunDeploymentMigrationAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");

        await migrator.EnsureCurrentSchemaAsync(db, CancellationToken.None);

        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM pragma_table_info('app_dev_pages') WHERE name = 'LayoutDraftJson'"));
    }

    [Fact]
    public async Task Deployment_migration_is_idempotent_for_multiple_tenant_workspaces()
    {
        using var db = CreateDatabase();
        var migrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db));
        await migrator.RunDeploymentMigrationAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN SchemaDraftJson TEXT NOT NULL DEFAULT '{}';");
        await SeedLegacyDraftPageAsync(db, "tenant-a", "MES", "mes-page");
        await SeedLegacyDraftPageAsync(db, "tenant-b", "WMS", "wms-page");

        await migrator.RunDeploymentMigrationAsync(db, CancellationToken.None);
        await migrator.RunDeploymentMigrationAsync(db, CancellationToken.None);

        Assert.Equal(2, await db.Queryable<ApplicationDesignerDocumentEntity>().CountAsync(CancellationToken.None));
        Assert.Equal(2, await db.Queryable<ApplicationDesignerRevisionEntity>().CountAsync(CancellationToken.None));
        Assert.Equal(1, await db.Queryable<ApplicationDesignerMigrationWatermarkEntity>().CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Failed_deployment_keeps_legacy_schema_and_leaves_watermark_non_retired()
    {
        using var db = CreateDatabase();
        var migrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db));
        await migrator.RunDeploymentMigrationAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN SchemaDraftJson TEXT NOT NULL DEFAULT '{}';");
        var page = new ApplicationDevelopmentPageEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            PageCode = "broken-deployment",
            PageName = "Broken deployment"
        };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        await db.Ado.ExecuteCommandAsync(
            "UPDATE app_dev_pages SET LayoutDraftJson=@json WHERE Id=@id",
            new SugarParameter("@json", "{not-json"),
            new SugarParameter("@id", page.Id));

        await Assert.ThrowsAsync<ValidationException>(() => migrator.RunDeploymentMigrationAsync(db, CancellationToken.None));

        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM pragma_table_info('app_dev_pages') WHERE name = 'LayoutDraftJson'"));
        var watermark = Assert.Single(await db.Queryable<ApplicationDesignerMigrationWatermarkEntity>().ToListAsync(CancellationToken.None));
        Assert.Equal("Pending", watermark.Status);
        Assert.Equal("Failed", (await db.Queryable<ApplicationDesignerMigrationRunEntity>()
            .OrderByDescending(item => item.StartedTime)
            .Take(1)
            .ToListAsync(CancellationToken.None)).Single().Status);
    }

    [Fact]
    public async Task Legacy_page_schema_migration_reads_raw_rows_publishes_artifact_and_drops_table()
    {
        using var db = CreateDatabase();
        var documentMigration = new ApplicationDesignerDocumentMigrationService(db);
        await new ApplicationDevelopmentCenterSchemaMigrator(documentMigration).MigrateAsync(db, CancellationToken.None);
        var page = await SeedPageWithDocumentAsync(db, "legacy-runtime");
        CreateLegacyPageSchemaTable(db);
        await InsertLegacyPageSchemaAsync(db, page, "{\"elements\":{\"root\":{\"type\":\"layout.page\",\"children\":[]}}}");

        var migration = new ApplicationLegacyPageSchemaMigrationService(
            db,
            documentMigration,
            new ApplicationDevelopmentSchemaCompiler(),
            new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator()));
        var migrated = await migration.MigrateAsync(CancellationToken.None);

        Assert.Equal(1, migrated);
        Assert.Equal(0, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'system_page_schemas'"));
        var document = await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync();
        Assert.Equal("Published", document.Status);
        Assert.NotNull(document.PublishedArtifactId);
        Assert.Equal(1, await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().CountAsync());
        var watermark = Assert.Single(await db.Queryable<ApplicationDesignerMigrationWatermarkEntity>().ToListAsync(CancellationToken.None));
        Assert.Equal("Retired", watermark.Status);
        Assert.Equal("designer-document-latest-v1", watermark.TargetSchemaFingerprint);

        await new ApplicationDevelopmentCenterSchemaMigrator(documentMigration).MigrateAsync(db, CancellationToken.None);
        Assert.Single(await db.Queryable<ApplicationDesignerMigrationWatermarkEntity>().ToListAsync(CancellationToken.None));
        Assert.Equal(1, await db.Queryable<ApplicationDesignerRevisionEntity>().CountAsync());
    }

    [Fact]
    public async Task Legacy_page_schema_migration_rolls_back_new_rows_when_conversion_fails()
    {
        using var db = CreateDatabase();
        var documentMigration = new ApplicationDesignerDocumentMigrationService(db);
        await new ApplicationDevelopmentCenterSchemaMigrator(documentMigration).MigrateAsync(db, CancellationToken.None);
        var page = await SeedPageWithDocumentAsync(db, "legacy-invalid");
        CreateLegacyPageSchemaTable(db);
        await InsertLegacyPageSchemaAsync(db, page, "{not-json");

        var migration = new ApplicationLegacyPageSchemaMigrationService(
            db,
            documentMigration,
            new ApplicationDevelopmentSchemaCompiler(),
            new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator()));

        await Assert.ThrowsAsync<ValidationException>(() => migration.MigrateAsync(CancellationToken.None));

        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'system_page_schemas'"));
        Assert.Equal(0, await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>().CountAsync());
        var document = await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync();
        Assert.Equal("{}", document.DocumentJson);
    }

    [Fact]
    public async Task Legacy_page_schema_migration_without_table_is_a_noop()
    {
        using var db = CreateDatabase();
        var documentMigration = new ApplicationDesignerDocumentMigrationService(db);
        await new ApplicationDevelopmentCenterSchemaMigrator(documentMigration).MigrateAsync(db, CancellationToken.None);

        var migration = new ApplicationLegacyPageSchemaMigrationService(
            db,
            documentMigration,
            new ApplicationDevelopmentSchemaCompiler(),
            new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator()));

        Assert.Equal(0, await migration.MigrateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Schema_migrator_migrates_legacy_columns_and_drops_them()
    {
        using var db = CreateDatabase();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN SchemaDraftJson TEXT NOT NULL DEFAULT '{}';");
        var page = new ApplicationDevelopmentPageEntity { TenantId = "tenant-a", AppCode = "MES", PageCode = "legacy", PageName = "Legacy" };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        await db.Ado.ExecuteCommandAsync("UPDATE app_dev_pages SET LayoutDraftJson=@layout, SchemaDraftJson=@schema WHERE Id=@id", new SugarParameter("@layout", "{\"elements\":{\"root\":{\"type\":\"layout.page\"}}}"), new SugarParameter("@schema", "{\"elements\":{\"legacy\":{\"type\":\"layout.page\"}}}"), new SugarParameter("@id", page.Id));

        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);

        var document = Assert.Single(await db.Queryable<ApplicationDesignerDocumentEntity>().ToListAsync(CancellationToken.None));
        Assert.Contains("root", document.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(0, db.Ado.GetInt("SELECT COUNT(1) FROM pragma_table_info('app_dev_pages') WHERE name IN ('LayoutDraftJson','SchemaDraftJson')"));
    }

    [Fact]
    public async Task Preview_requires_document_entity_after_legacy_columns_are_removed()
    {
        using var db = CreateDatabase();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        var page = new ApplicationDevelopmentPageEntity { TenantId = "tenant-a", AppCode = "MES", PageCode = "page", PageName = "Page" };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        await Assert.ThrowsAsync<ValidationException>(() => RequireCurrentAsync(db, page));
    }

    [Fact]
    public async Task Invalid_legacy_source_does_not_create_document()
    {
        using var db = CreateDatabase();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN SchemaDraftJson TEXT NOT NULL DEFAULT '{}';");
        var page = new ApplicationDevelopmentPageEntity { TenantId = "tenant-a", AppCode = "MES", PageCode = "broken", PageName = "Broken" };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        await db.Ado.ExecuteCommandAsync("UPDATE app_dev_pages SET LayoutDraftJson=@json WHERE Id=@id", new SugarParameter("@json", "{not-json"), new SugarParameter("@id", page.Id));
        await Assert.ThrowsAsync<ValidationException>(() => new ApplicationDesignerDocumentMigrationService(db).MigrateAsync(CancellationToken.None));
        Assert.Equal(0, await db.Queryable<ApplicationDesignerDocumentEntity>().CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Legacy_node_data_binding_is_migrated_to_latest_bindings_data()
    {
        using var db = CreateDatabase();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN SchemaDraftJson TEXT NOT NULL DEFAULT '{}';");
        var page = new ApplicationDevelopmentPageEntity { TenantId = "tenant-a", AppCode = "MES", PageCode = "legacy-binding", PageName = "Legacy binding" };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        var legacyDocument = "{\"pages\":[{\"id\":\"legacy-binding\",\"rootElementId\":\"root\"}],\"elements\":{\"root\":{\"id\":\"root\",\"type\":\"layout.page\",\"children\":[],\"props\":{\"text\":\"fixed\"},\"bindings\":{\"props\":{\"text\":{\"source\":\"variables\",\"path\":\"title\",\"expectedType\":\"string\"}},\"row\":{\"source\":\"variables\",\"path\":\"order.status\",\"expectedType\":\"string\"}},\"dataBinding\":{\"source\":\"page\",\"path\":\"orders\"}}}}";
        await db.Ado.ExecuteCommandAsync(
            "UPDATE app_dev_pages SET LayoutDraftJson=@json WHERE Id=@id",
            new SugarParameter("@json", legacyDocument),
            new SugarParameter("@id", page.Id));

        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);

        var document = Assert.Single(await db.Queryable<ApplicationDesignerDocumentEntity>().ToListAsync(CancellationToken.None));
        var documentJson = JsonNode.Parse(document.DocumentJson)!.AsObject();
        var root = documentJson["elements"]!.AsObject()["root"]!.AsObject();
        Assert.Equal("page:orders", root["bindings"]!["data"]!["resourceId"]!.GetValue<string>());
        Assert.Equal("page", root["bindings"]!["data"]!["resourceType"]!.GetValue<string>());
        Assert.Equal("page.orders", root["bindings"]!["data"]!["displayName"]!.GetValue<string>());
        Assert.Equal("json", root["bindings"]!["data"]!["valueType"]!.GetValue<string>());
        Assert.Equal("variables:order.status", root["bindings"]!["row"]!["resourceId"]!.GetValue<string>());
        Assert.Equal("variables", root["bindings"]!["row"]!["resourceType"]!.GetValue<string>());
        Assert.Equal("variables.order.status", root["bindings"]!["row"]!["displayName"]!.GetValue<string>());
        Assert.Equal("variables:title", root["props"]!["text"]!["resourceId"]!.GetValue<string>());
        Assert.DoesNotContain("props", root["bindings"]!.AsObject().Select(item => item.Key));
        Assert.Equal("standard", documentJson["pageType"]!.GetValue<string>());
        Assert.Equal("standard", documentJson["runtimeContext"]!["pageType"]!.GetValue<string>());
        Assert.DoesNotContain("dataBinding", document.DocumentJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("constant")]
    [InlineData("form")]
    [InlineData("tableRow")]
    public void Microflow_source_expression_without_path_is_preserved_during_document_migration(string source)
    {
        using var db = CreateDatabase();
        var page = new ApplicationDevelopmentPageEntity
        {
            Id = "microflow-expression-page",
            PageCode = "microflow-expression",
            PageName = "Microflow expression"
        };
        var document = """
        {
          "pages": [{"id":"microflow-expression-page","rootElementId":"root"}],
          "elements": {"root": {"id":"root","type":"layout.page","children":[]}},
          "pageMicroflows": [{
            "id":"invoke",
            "flowCode":"demo",
            "inputMappings":[{"targetVariable":"input","sourceExpression":{"source":"SOURCE_VALUE"}}]
          }]
        }
        """.Replace("SOURCE_VALUE", source, StringComparison.Ordinal);

        var normalized = new ApplicationDesignerDocumentMigrationService(db)
            .NormalizeLegacyDocument(document, page);
        var sourceExpression = JsonNode.Parse(normalized)!["pageMicroflows"]![0]!["inputMappings"]![0]!["sourceExpression"]!.AsObject();

        Assert.Equal(source, sourceExpression["source"]!.GetValue<string>());
        Assert.False(sourceExpression.ContainsKey("path"));
    }

    [Fact]
    public void Page_property_binding_without_source_or_path_still_fails_closed()
    {
        using var db = CreateDatabase();
        var page = new ApplicationDevelopmentPageEntity { Id = "invalid-binding-page", PageCode = "invalid-binding", PageName = "Invalid binding" };
        var document = """
        {
          "pages": [{"id":"invalid-binding-page","rootElementId":"root"}],
          "elements": {"root": {
            "id":"root",
            "type":"layout.page",
            "children":[],
            "bindings":{"props":{"title":{"source":"form"}}}
          }}
        }
        """;

        var exception = Assert.Throws<ValidationException>(() =>
            new ApplicationDesignerDocumentMigrationService(db).NormalizeLegacyDocument(document, page));

        Assert.Contains("source/path binding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_microflow_input_mapping_array_preserves_expression_context()
    {
        using var db = CreateDatabase();
        var page = new ApplicationDevelopmentPageEntity { Id = "nested-expression-page", PageCode = "nested-expression", PageName = "Nested expression" };
        var document = """
        {
          "pages": [{"id":"nested-expression-page","rootElementId":"root"}],
          "elements": {"root": {"id":"root","type":"layout.page","children":[]}},
          "pageMicroflows": [{
            "id":"invoke",
            "inputMappings":[
              {"targetVariable":"constantValue","sourceExpression":{"source":"constant"}},
              {"targetVariable":"formValue","sourceExpression":{"source":"form"}},
              {"targetVariable":"rowValue","sourceExpression":{"source":"tableRow"}},
              {"targetVariable":"value","valueExpression":{"source":"constant"}}
            ]
          }]
        }
        """;

        var normalized = new ApplicationDesignerDocumentMigrationService(db)
            .NormalizeLegacyDocument(document, page);
        var mappings = JsonNode.Parse(normalized)!["pageMicroflows"]![0]!["inputMappings"]!.AsArray();

        Assert.Equal("constant", mappings[0]!["sourceExpression"]!["source"]!.GetValue<string>());
        Assert.Equal("form", mappings[1]!["sourceExpression"]!["source"]!.GetValue<string>());
        Assert.Equal("tableRow", mappings[2]!["sourceExpression"]!["source"]!.GetValue<string>());
        Assert.Equal("constant", mappings[3]!["valueExpression"]!["source"]!.GetValue<string>());
        Assert.DoesNotContain("resourceId", mappings.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Top_level_canonical_variables_are_not_treated_as_legacy_bindings()
    {
        using var db = CreateDatabase();
        var page = new ApplicationDevelopmentPageEntity { Id = "canonical-variables-page", PageCode = "canonical-variables", PageName = "Canonical variables" };
        var document = """
        {
          "pages": [{"id":"canonical-variables-page","rootElementId":"root"}],
          "elements": {"root": {"id":"root","type":"layout.page","children":[]}},
          "variables":[
            {"id":"system.currentUser","source":"system","path":null,"valueType":"json"},
            {"id":"row.current","source":"currentRow","path":null,"valueType":"json"},
            {"id":"form.values","source":"formField","path":null,"valueType":"json"}
          ]
        }
        """;

        var normalized = new ApplicationDesignerDocumentMigrationService(db)
            .NormalizeLegacyDocument(document, page);
        var variables = JsonNode.Parse(normalized)!["variables"]!.AsArray();

        Assert.Equal("system", variables[0]!["source"]!.GetValue<string>());
        Assert.Null(variables[0]!["path"]);
        Assert.Equal("currentRow", variables[1]!["source"]!.GetValue<string>());
        Assert.Equal("formField", variables[2]!["source"]!.GetValue<string>());
        Assert.DoesNotContain("resourceId", variables.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_resource_refs_are_canonicalized_and_typed_during_document_migration()
    {
        using var db = CreateDatabase();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN SchemaDraftJson TEXT NOT NULL DEFAULT '{}';");
        var page = new ApplicationDevelopmentPageEntity { TenantId = "tenant-a", AppCode = "MES", PageCode = "resource-ref", PageName = "Resource ref" };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        var legacyDocument = "{\"pages\":[{\"id\":\"resource-ref\",\"rootElementId\":\"root\"}],\"elements\":{\"root\":{\"id\":\"root\",\"type\":\"layout.page\",\"children\":[],\"bindings\":{\"row\":{\"displayName\":\"Order status\",\"resourceId\":\"variables::order.status\",\"valueType\":\"string\"}}}}}";
        await db.Ado.ExecuteCommandAsync(
            "UPDATE app_dev_pages SET LayoutDraftJson=@json WHERE Id=@id",
            new SugarParameter("@json", legacyDocument),
            new SugarParameter("@id", page.Id));

        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);

        var document = Assert.Single(await db.Queryable<ApplicationDesignerDocumentEntity>().ToListAsync(CancellationToken.None));
        var root = JsonNode.Parse(document.DocumentJson)!["elements"]!["root"]!.AsObject();
        Assert.Equal("variables:order.status", root["bindings"]!["row"]!["resourceId"]!.GetValue<string>());
        Assert.Equal("variables", root["bindings"]!["row"]!["resourceType"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("free")]
    [InlineData("flex")]
    [InlineData("grid")]
    [InlineData("constraints")]
    public void NormalizeLegacyDocument_migrates_each_legacy_layout_mode_to_canonical_protocol(string mode)
    {
        using var db = CreateDatabase();
        var page = new ApplicationDevelopmentPageEntity
        {
            Id = $"legacy-layout-{mode}",
            PageCode = $"legacy-layout-{mode}",
            PageName = $"Legacy layout {mode}"
        };
        var legacyLayout = mode switch
        {
            "flex" => new JsonObject
            {
                ["display"] = "flex",
                ["layoutMode"] = "flex",
                ["flexDirection"] = "column",
                ["gap"] = 8,
                ["flexGrow"] = 1,
                ["flexBasis"] = "50%",
                ["width"] = "100%",
                ["height"] = "auto"
            },
            "grid" => new JsonObject
            {
                ["display"] = "grid",
                ["layoutMode"] = "grid",
                ["gridTemplateColumns"] = "repeat(2, minmax(0, 1fr))",
                ["gridRow"] = "2 / span 2",
                ["gridColumn"] = 3,
                ["width"] = 640,
                ["height"] = 360
            },
            "constraints" => new JsonObject
            {
                ["layoutMode"] = "constraints",
                ["constraints"] = new JsonObject { ["left"] = 10, ["right"] = 10, ["stretchX"] = true },
                ["width"] = 100,
                ["height"] = 50
            },
            _ => new JsonObject
            {
                ["display"] = "block",
                ["layoutMode"] = "free",
                ["x"] = 12,
                ["y"] = 24,
                ["width"] = 320,
                ["height"] = "240px"
            }
        };
        var source = new JsonObject
        {
            ["pages"] = new JsonArray(new JsonObject { ["id"] = page.PageCode, ["rootElementId"] = "root" }),
            ["elements"] = new JsonObject
            {
                ["root"] = new JsonObject
                {
                    ["id"] = "root",
                    ["type"] = "layout.page",
                    ["children"] = new JsonArray(),
                    ["layout"] = legacyLayout
                }
            }
        }.ToJsonString();

        var normalized = new ApplicationDesignerDocumentMigrationService(db).NormalizeLegacyDocument(source, page);
        var layout = JsonNode.Parse(normalized)!["elements"]!["root"]!["layout"]!.AsObject();
        var protocol = layout["protocol"]!.AsObject();

        Assert.Equal(mode, protocol["container"]!["mode"]!.GetValue<string>());
        if (mode == "free")
        {
            Assert.Equal(320, protocol["size"]!["width"]!.GetValue<double>());
        }
        else if (mode == "flex")
        {
            Assert.Equal("100%", protocol["size"]!["width"]!.GetValue<string>());
        }
        else if (mode == "grid")
        {
            Assert.Equal(640, protocol["size"]!["width"]!.GetValue<double>());
        }
        else
        {
            Assert.Equal(100, protocol["size"]!["width"]!.GetValue<double>());
        }
        Assert.DoesNotContain("layoutMode", layout.Select(item => item.Key));
        Assert.DoesNotContain("display", layout.Select(item => item.Key));
        Assert.DoesNotContain("constraints", layout.Select(item => item.Key));
        Assert.DoesNotContain("x", layout.Select(item => item.Key));
        Assert.DoesNotContain("y", layout.Select(item => item.Key));

        var repeated = new ApplicationDesignerDocumentMigrationService(db).NormalizeLegacyDocument(normalized, page);
        Assert.Equal(
            JsonNode.Parse(normalized)!.ToJsonString(),
            JsonNode.Parse(repeated)!.ToJsonString());
    }

    [Fact]
    public void MigrateLegacyLayoutToCanonicalProtocol_is_pure_and_preserves_existing_protocol()
    {
        var layout = new JsonObject
        {
            ["layoutMode"] = "grid",
            ["display"] = "grid",
            ["protocol"] = new JsonObject
            {
                ["container"] = new JsonObject { ["mode"] = "free" },
                ["placement"] = new JsonObject
                {
                    ["kind"] = "absolute",
                    ["absolute"] = new JsonObject { ["x"] = 1, ["y"] = 2 }
                },
                ["size"] = new JsonObject { ["width"] = 100, ["height"] = 50 }
            }
        };

        var migrated = ApplicationDesignerDocumentMigrationService.MigrateLegacyLayoutToCanonicalProtocol(layout);

        Assert.Equal("free", migrated["protocol"]!["container"]!["mode"]!.GetValue<string>());
        Assert.False(migrated.ContainsKey("layoutMode"));
        Assert.False(migrated.ContainsKey("display"));
        Assert.Equal("grid", layout["layoutMode"]!.GetValue<string>());
        Assert.True(layout.ContainsKey("protocol"));
    }

    [Fact]
    public async Task Existing_documents_are_scanned_for_legacy_layout_and_second_migration_is_idempotent()
    {
        using var db = CreateDatabase();
        var validator = new ApplicationDevelopmentSchemaValidator();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db, validator)).MigrateAsync(db, CancellationToken.None);
        var page = new ApplicationDevelopmentPageEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            PageCode = "legacy-layout-scan",
            PageName = "Legacy layout scan"
        };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        var documentJson = """
        {
          "documentId":"legacy-layout-scan",
          "revision":1,
          "pages":[{"id":"legacy-layout-scan","rootElementId":"root"}],
          "elements":{"root":{
            "id":"root",
            "type":"layout.page",
            "children":[],
            "layout":{"display":"grid","layoutMode":"grid","gridTemplateColumns":"repeat(2, minmax(0, 1fr))","width":640,"height":360}
          }}
        }
        """;
        var store = new ApplicationDesignerDocumentStore(validator);
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "migration-user");
        await store.SaveAsync(db, workspace, page.Id, page.VersionId, documentJson, null, null, "{\"type\":\"seed\"}", CancellationToken.None);
        var migrationService = new ApplicationDesignerDocumentMigrationService(db, validator);

        Assert.Equal(1, await migrationService.MigrateAsync(CancellationToken.None));
        var first = await store.RequireCurrentAsync(db, workspace, page.Id, CancellationToken.None);
        var firstJson = first.DocumentJson;
        var firstRevision = first.CurrentRevisionId;
        var firstRoot = JsonNode.Parse(firstJson)!["elements"]!["root"]!;
        Assert.Equal("grid", firstRoot["layout"]!["protocol"]!["container"]!["mode"]!.GetValue<string>());
        Assert.DoesNotContain("layoutMode", firstRoot["layout"]!.AsObject().Select(item => item.Key));

        Assert.Equal(0, await migrationService.MigrateAsync(CancellationToken.None));
        var second = await store.RequireCurrentAsync(db, workspace, page.Id, CancellationToken.None);
        Assert.Equal(firstRevision, second.CurrentRevisionId);
        Assert.Equal(firstJson, second.DocumentJson);
    }

    [Fact]
    public async Task Workspace_migration_entry_targets_resolved_application_database_and_is_idempotent()
    {
        using var mainDb = CreateDatabase();
        using var workspaceDb = CreateDatabase();
        var mainMigrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(mainDb));
        var workspaceMigrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(workspaceDb));
        await mainMigrator.RunDeploymentMigrationAsync(mainDb, CancellationToken.None);
        await workspaceMigrator.RunDeploymentMigrationAsync(workspaceDb, CancellationToken.None);

        var page = new ApplicationDevelopmentPageEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            PageCode = "workspace-entry",
            PageName = "Workspace entry"
        };
        await workspaceDb.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        var legacyDocument = """
        {
          "documentId":"workspace-entry",
          "revision":1,
          "pages":[{"id":"workspace-entry","rootElementId":"root"}],
          "elements":{"root":{
            "id":"root",
            "type":"layout.page",
            "children":[],
            "layout":{"display":"grid","layoutMode":"grid","gridTemplateColumns":"repeat(2, minmax(0, 1fr))","width":640,"height":360}
          }}
        }
        """;
        await new ApplicationDesignerDocumentStore(new ApplicationDevelopmentSchemaValidator()).SaveAsync(
            workspaceDb,
            new ApplicationDataCenterWorkspace("tenant-a", "MES", "migration-entry"),
            page.Id,
            page.VersionId,
            legacyDocument,
            null,
            null,
            "{\"type\":\"seed\"}",
            CancellationToken.None);

        var documentMigration = new ApplicationDesignerDocumentMigrationService(mainDb);
        var migration = new ApplicationLegacyPageSchemaMigrationService(
            mainDb,
            documentMigration,
            new ApplicationDevelopmentSchemaCompiler(),
            new ApplicationDesignerArtifactPublisher(new ApplicationDevelopmentSchemaValidator()));

        Assert.Equal(1, await migration.MigrateWorkspaceOnceAsync(workspaceDb, "tenant-a", "MES", CancellationToken.None));

        var document = Assert.Single(await workspaceDb.Queryable<ApplicationDesignerDocumentEntity>().ToListAsync(CancellationToken.None));
        var root = JsonNode.Parse(document.DocumentJson)!["elements"]!["root"]!;
        Assert.Equal("grid", root["layout"]!["protocol"]!["container"]!["mode"]!.GetValue<string>());
        Assert.DoesNotContain("layoutMode", root["layout"]!.AsObject().Select(item => item.Key));
        var migrationState = Assert.Single(await workspaceDb.Queryable<ApplicationDesignerMigrationEntity>().ToListAsync(CancellationToken.None));
        Assert.Equal(ApplicationDesignerDocumentMigrationService.MigrationRevision, migrationState.MigrationRevision);
        var firstRevisionId = document.CurrentRevisionId;
        var firstDocumentJson = document.DocumentJson;

        Assert.Equal(0, await migration.MigrateWorkspaceOnceAsync(workspaceDb, "tenant-a", "MES", CancellationToken.None));
        var secondDocument = Assert.Single(await workspaceDb.Queryable<ApplicationDesignerDocumentEntity>().ToListAsync(CancellationToken.None));
        Assert.Equal(firstRevisionId, secondDocument.CurrentRevisionId);
        Assert.Equal(firstDocumentJson, secondDocument.DocumentJson);
        Assert.Equal(0, await mainDb.Queryable<ApplicationDesignerDocumentEntity>().CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Migration_inventory_covers_all_document_revision_and_artifact_stores_and_blocks_retirement_on_legacy_fields()
    {
        using var db = CreateDatabase();
        CreateLayoutInventoryTables(db);
        var legacyJson = "{\"elements\":{\"root\":{\"layout\":{\"display\":\"grid\",\"layoutMode\":\"grid\",\"gridRow\":1}}}}";
        var canonicalJson = "{\"elements\":{\"root\":{\"layout\":{\"protocol\":{\"container\":{\"mode\":\"grid\"},\"placement\":{\"kind\":\"grid-item\",\"gridItem\":{}},\"size\":{\"width\":100,\"height\":50}}}}}}";

        await InsertLayoutInventoryRowAsync(db, "app_designer_documents", "DocumentJson", legacyJson);
        await InsertLayoutInventoryRowAsync(db, "app_designer_revisions", "DocumentJson", canonicalJson);
        await InsertLayoutInventoryRowAsync(db, "app_designer_runtime_artifacts", "ArtifactJson", legacyJson);
        await InsertLayoutInventoryRowAsync(db, "app_dev_documents", "DocumentJson", canonicalJson);
        await InsertLayoutInventoryRowAsync(db, "app_dev_document_revisions", "DocumentJson", legacyJson);
        await InsertLayoutInventoryRowAsync(db, "app_dev_runtime_artifacts", "ArtifactJson", canonicalJson);

        var inventory = await new ApplicationDesignerDocumentMigrationService(db).GetMigrationInventoryAsync();

        Assert.Equal(6, inventory.Tables.Count);
        Assert.Equal(6, inventory.TotalRows);
        Assert.Equal(3, inventory.LegacyLayoutRows);
        Assert.Equal(3, inventory.CanonicalProtocolRows);
        Assert.Equal(0, inventory.InvalidJsonRows);
        Assert.Equal(0, inventory.InvalidProtocolRows);
        Assert.False(inventory.CanRetireLegacyLayoutFields);
        Assert.Contains(inventory.RetirementBlockers, item => item.StartsWith("app_designer_documents contains 1", StringComparison.Ordinal));
        Assert.Contains(inventory.RetirementBlockers, item => item.StartsWith("app_dev_document_revisions contains 1", StringComparison.Ordinal));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new ApplicationDesignerDocumentMigrationService(db).EnsureRetirementReadyAsync(db));
        Assert.Contains("retirement is blocked", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Migration_inventory_marks_canonical_six_store_snapshot_retirement_ready_and_rejects_invalid_protocol()
    {
        using var db = CreateDatabase();
        CreateLayoutInventoryTables(db);
        const string canonicalJson = "{\"elements\":{\"root\":{\"layout\":{\"protocol\":{\"container\":{\"mode\":\"free\"},\"placement\":{\"kind\":\"absolute\",\"absolute\":{\"x\":0,\"y\":0}},\"size\":{\"width\":100,\"height\":50}}}}}}";
        foreach (var (table, column) in LayoutInventoryTableDefinitions)
        {
            await InsertLayoutInventoryRowAsync(db, table, column, canonicalJson);
        }

        var service = new ApplicationDesignerDocumentMigrationService(db);
        var ready = await service.GetMigrationInventoryAsync();
        Assert.True(ready.CanRetireLegacyLayoutFields);
        Assert.Equal(6, ready.CanonicalProtocolRows);
        Assert.Empty(ready.RetirementBlockers);
        await service.EnsureRetirementReadyAsync(db);

        await InsertLayoutInventoryRowAsync(db, "app_designer_documents", "DocumentJson", "{\"elements\":{\"root\":{\"layout\":{\"protocol\":{\"container\":{}}}}}}");
        var blocked = await service.GetMigrationInventoryAsync();
        Assert.False(blocked.CanRetireLegacyLayoutFields);
        Assert.Equal(1, blocked.InvalidProtocolRows);
        Assert.Contains(blocked.RetirementBlockers, item => item.StartsWith("app_designer_documents contains 1 invalid layout.protocol", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Existing_designer_documents_are_revised_to_stable_resource_ids()
    {
        using var db = CreateDatabase();
        var validator = new ApplicationDevelopmentSchemaValidator();
        var migrationService = new ApplicationDesignerDocumentMigrationService(
            db,
            schemaCompiler: new ApplicationDevelopmentSchemaCompiler(validator),
            artifactPublisher: new ApplicationDesignerArtifactPublisher(validator));
        await new ApplicationDevelopmentCenterSchemaMigrator(migrationService).MigrateAsync(db, CancellationToken.None);
        var page = new ApplicationDevelopmentPageEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            PageCode = "existing-resource-ref",
            PageName = "Existing resource ref"
        };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        var store = new ApplicationDesignerDocumentStore(new ApplicationDevelopmentSchemaValidator());
        var documentJson = """
        {
          "documentId": "existing-resource-ref",
          "revision": 1,
          "pages": [{"id":"existing-resource-ref","name":"Existing resource ref","rootElementId":"root"}],
          "elements": {"root": {"id":"root","type":"layout.page","parentId":null,"children":[],"props":{},"layout":{},"style":{},"bindings":{"text":{"displayName":"Order status","resourceId":"variables::order.status","valueType":"string"}},"events":[],"permission":{},"validation":[]}},
          "actions":[],"apiBindings":[],"dataSources":[],"metadata":{},"modals":[],"pageMicroflows":[],"pageParameters":[],"pageType":"standard","permissions":{},"runtimeContext":{},"styleTokens":{},"variables":[],"workflowBindings":[]
        }
        """;
        var workspace = new ApplicationDataCenterWorkspace("tenant-a", "MES", "migration-user");
        var saved = await store.SaveAsync(db, workspace, page.Id, page.VersionId, documentJson, null, null, "{\"type\":\"seed\"}", CancellationToken.None);
        var beforeRevision = saved.RevisionId;

        var migrated = await migrationService.MigrateAsync(CancellationToken.None);

        Assert.Equal(1, migrated);
        var current = await store.RequireCurrentAsync(db, workspace, page.Id, CancellationToken.None);
        Assert.NotEqual(beforeRevision, current.CurrentRevisionId);
        Assert.DoesNotContain("::", current.DocumentJson, StringComparison.Ordinal);
        Assert.Contains("variables:order.status", current.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(2, JsonNode.Parse(current.DocumentJson)!["revision"]!.GetValue<int>());
        Assert.Equal(1, await db.Queryable<ApplicationDesignerMigrationEntity>().CountAsync(item => item.DocumentId == current.Id));
        var artifact = await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
            .Where(item => item.Id == current.PublishedArtifactId)
            .FirstAsync(CancellationToken.None);
        Assert.NotNull(artifact);
        Assert.DoesNotContain("::", artifact!.ArtifactJson, StringComparison.Ordinal);
        Assert.Equal(RuntimeCapabilityContract.MigrationRevision, artifact.MigrationRevision);
        Assert.Equal(RuntimeCapabilityContract.MigrationRevision, JsonNode.Parse(artifact.ArtifactJson)!["migrationRevision"]!.GetValue<string>());
    }

    [Fact]
    public async Task Missing_migration_dependency_fails_before_legacy_columns_are_dropped()
    {
        using var db = CreateDatabase();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN SchemaDraftJson TEXT NOT NULL DEFAULT '{}';");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ApplicationDevelopmentCenterSchemaMigrator().MigrateAsync(db, CancellationToken.None));
        Assert.Equal(2, db.Ado.GetInt("SELECT COUNT(1) FROM pragma_table_info('app_dev_pages') WHERE name IN ('LayoutDraftJson','SchemaDraftJson')"));
    }

    [Fact]
    public async Task Preview_rejects_document_hash_or_revision_integrity_failures()
    {
        using var db = CreateDatabase();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN LayoutDraftJson TEXT NOT NULL DEFAULT '{}';");
        db.Ado.ExecuteCommand("ALTER TABLE app_dev_pages ADD COLUMN SchemaDraftJson TEXT NOT NULL DEFAULT '{}';");
        var page = new ApplicationDevelopmentPageEntity { TenantId = "tenant-a", AppCode = "MES", PageCode = "integrity", PageName = "Integrity" };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        var source = "{\"pages\":[{\"id\":\"integrity\",\"rootElementId\":\"root\"}],\"elements\":{\"root\":{\"id\":\"root\",\"type\":\"layout.page\",\"children\":[]}}}";
        await db.Ado.ExecuteCommandAsync("UPDATE app_dev_pages SET LayoutDraftJson=@json WHERE Id=@id", new SugarParameter("@json", source), new SugarParameter("@id", page.Id));
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);

        var document = await db.Queryable<ApplicationDesignerDocumentEntity>().SingleAsync();
        document.DocumentHash = "sha256:tampered";
        await db.Updateable(document).UpdateColumns(item => item.DocumentHash).ExecuteCommandAsync(CancellationToken.None);
        await Assert.ThrowsAsync<ValidationException>(() => RequireCurrentAsync(db, page));

        document.DocumentHash = document.TargetHash;
        await db.Updateable(document).UpdateColumns(item => item.DocumentHash).ExecuteCommandAsync(CancellationToken.None);
        await db.Updateable<ApplicationDesignerRevisionEntity>().SetColumns(item => new ApplicationDesignerRevisionEntity { DocumentJson = "{}" }).Where(item => item.Id == document.CurrentRevisionId).ExecuteCommandAsync(CancellationToken.None);
        await Assert.ThrowsAsync<ValidationException>(() => RequireCurrentAsync(db, page));
    }

    [Fact]
    public async Task Preview_rejects_duplicate_current_designer_documents_instead_of_selecting_one()
    {
        using var db = CreateDatabase();
        await new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db)).MigrateAsync(db, CancellationToken.None);
        db.Ado.ExecuteCommand("DROP INDEX idx_app_designer_documents_page;");
        var page = new ApplicationDevelopmentPageEntity { TenantId = "tenant-a", AppCode = "MES", PageCode = "duplicate", PageName = "Duplicate" };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        await db.Insertable(new ApplicationDesignerDocumentEntity
        {
            TenantId = page.TenantId,
            AppCode = page.AppCode,
            PageId = page.Id,
            VersionId = page.VersionId,
            DocumentJson = "{}"
        }).ExecuteCommandAsync(CancellationToken.None);
        await db.Insertable(new ApplicationDesignerDocumentEntity
        {
            TenantId = page.TenantId,
            AppCode = page.AppCode,
            PageId = page.Id,
            VersionId = page.VersionId,
            DocumentJson = "{}"
        }).ExecuteCommandAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => RequireCurrentAsync(db, page));
        Assert.Contains("Multiple current DesignerDocuments", exception.Message, StringComparison.Ordinal);
    }

    private static async Task<ApplicationDevelopmentPageEntity> SeedPageWithDocumentAsync(
        SqlSugarClient db,
        string pageCode)
    {
        var page = new ApplicationDevelopmentPageEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            PageCode = pageCode,
            PageName = pageCode,
            Status = "Draft"
        };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        await db.Insertable(new ApplicationDesignerDocumentEntity
        {
            TenantId = page.TenantId,
            AppCode = page.AppCode,
            PageId = page.Id,
            VersionId = page.VersionId,
            DocumentJson = "{}"
        }).ExecuteCommandAsync(CancellationToken.None);
        return page;
    }

    private static async Task SeedLegacyDraftPageAsync(
        SqlSugarClient db,
        string tenantId,
        string appCode,
        string pageCode)
    {
        var page = new ApplicationDevelopmentPageEntity
        {
            TenantId = tenantId,
            AppCode = appCode,
            PageCode = pageCode,
            PageName = pageCode,
            Status = "Draft"
        };
        await db.Insertable(page).ExecuteCommandAsync(CancellationToken.None);
        var source = $"{{\"pages\":[{{\"id\":\"{pageCode}\",\"rootElementId\":\"root\"}}],\"elements\":{{\"root\":{{\"id\":\"root\",\"type\":\"layout.page\",\"children\":[]}}}}}}";
        await db.Ado.ExecuteCommandAsync(
            "UPDATE app_dev_pages SET LayoutDraftJson=@json WHERE Id=@id",
            new SugarParameter("@json", source),
            new SugarParameter("@id", page.Id));
    }

    private static readonly (string Table, string Column)[] LayoutInventoryTableDefinitions =
    [
        ("app_designer_documents", "DocumentJson"),
        ("app_designer_revisions", "DocumentJson"),
        ("app_designer_runtime_artifacts", "ArtifactJson"),
        ("app_dev_documents", "DocumentJson"),
        ("app_dev_document_revisions", "DocumentJson"),
        ("app_dev_runtime_artifacts", "ArtifactJson")
    ];

    private static void CreateLayoutInventoryTables(SqlSugarClient db)
    {
        foreach (var (table, column) in LayoutInventoryTableDefinitions)
        {
            db.Ado.ExecuteCommand($"CREATE TABLE {table} (Id TEXT NOT NULL PRIMARY KEY, {column} TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0);");
        }
    }

    private static Task InsertLayoutInventoryRowAsync(SqlSugarClient db, string table, string column, string json) =>
        db.Ado.ExecuteCommandAsync(
            $"INSERT INTO {table} (Id, {column}, IsDeleted) VALUES (@id, @json, 0)",
            new SugarParameter("@id", $"inventory-{table}-{Guid.NewGuid():N}"),
            new SugarParameter("@json", json));

    private static void CreateLegacyPageSchemaTable(SqlSugarClient db) =>
        db.Ado.ExecuteCommand("""
CREATE TABLE system_page_schemas (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    PageCode TEXT NOT NULL,
    PageName TEXT NOT NULL,
    PageType TEXT NOT NULL DEFAULT 'custom',
    ModelCode TEXT NULL,
    PermissionCode TEXT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    Status TEXT NOT NULL DEFAULT 'Published',
    SchemaJson TEXT NOT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    CreatedTime TEXT NOT NULL
);
""");

    private static Task InsertLegacyPageSchemaAsync(
        SqlSugarClient db,
        ApplicationDevelopmentPageEntity page,
        string schemaJson) =>
        db.Ado.ExecuteCommandAsync(
            "INSERT INTO system_page_schemas (Id, TenantId, AppCode, PageCode, PageName, PageType, VersionNo, Status, SchemaJson, CreatedTime) VALUES (@id, @tenant, @app, @code, @name, @type, @version, @status, @schema, @created)",
            new SugarParameter("@id", $"legacy-{page.PageCode}"),
            new SugarParameter("@tenant", page.TenantId),
            new SugarParameter("@app", page.AppCode),
            new SugarParameter("@code", page.PageCode),
            new SugarParameter("@name", page.PageName),
            new SugarParameter("@type", page.PageType),
            new SugarParameter("@version", 1),
            new SugarParameter("@status", "Published"),
            new SugarParameter("@schema", schemaJson),
            new SugarParameter("@created", DateTime.UtcNow));

    private static SqlSugarClient CreateDatabase() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:designer-migration-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static Task<ApplicationDesignerDocumentEntity> RequireCurrentAsync(
        SqlSugarClient db,
        ApplicationDevelopmentPageEntity page) =>
        new ApplicationDesignerDocumentStore(new ApplicationDevelopmentSchemaValidator()).RequireCurrentAsync(
            db,
            new ApplicationDataCenterWorkspace(page.TenantId, page.AppCode, "user-a"),
            page.Id,
            CancellationToken.None);
}
