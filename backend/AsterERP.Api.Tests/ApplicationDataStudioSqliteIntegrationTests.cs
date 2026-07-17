using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataStudioSqliteIntegrationTests : IDisposable
{
    private readonly ApplicationDataStudioSqliteFixture fixture = new();

    [Fact]
    public async Task CatalogRefreshReadsRealSqliteSchemaObjectsAndPersistsLineage()
    {
        var service = fixture.CreateCatalogService();

        var first = await service.RefreshAsync(fixture.DataSourceId);

        Assert.Equal(1, first.VersionNo);
        Assert.Equal("Sqlite", first.Provider);
        Assert.False(string.IsNullOrWhiteSpace(first.SnapshotHash));
        var people = Assert.Single(first.Tables, item => item.TableName == "dc_people");
        Assert.Collection(
            people.Columns,
            column =>
            {
                Assert.Equal("id", column.ColumnName);
                Assert.True(column.PrimaryKey);
            },
            column => Assert.Equal("name", column.ColumnName),
            column => Assert.Equal("age", column.ColumnName));
        Assert.Contains(people.Indexes, item => item.Name == "ix_dc_people_name");
        Assert.Contains(people.Triggers, item => item.Name == "tr_dc_people_insert");

        var persisted = await service.GetLatestAsync(fixture.DataSourceId);
        Assert.NotNull(persisted);
        Assert.Equal(first.SnapshotHash, persisted!.SnapshotHash);
        Assert.Equal(first.Id, persisted.Id);

        using var sourceDb = ApplicationDataStudioSqliteFixture.CreateDb(fixture.SourceDatabasePath);
        sourceDb.Ado.ExecuteCommand("ALTER TABLE dc_people ADD COLUMN status TEXT NULL");
        var second = await service.RefreshNodeAsync(
            fixture.DataSourceId,
            new ApplicationDataSourceCatalogRefreshRequest(null, "dc_people"));

        Assert.Equal(2, second.VersionNo);
        Assert.Equal(first.Id, second.PreviousSnapshotId);
        Assert.Equal(first.SnapshotHash, second.PreviousSnapshotHash);
        Assert.Contains(second.Changes, item => item.ChangeType == "Added" && item.NodeName == "status");
        Assert.Contains(Assert.Single(second.Tables, item => item.TableName == "dc_people").Columns, item => item.ColumnName == "status");
    }

    [Fact]
    public async Task SchemaChangePlanDeploysRealSqliteTableAndWritesAuditedStatus()
    {
        var service = fixture.CreateTableWorkbenchService();
        var table = new ApplicationDataSourceCreateTableRequest(
            "dc_created",
            null,
            "Created",
            null,
            [
                new ApplicationDataSourceCreateTableColumnRequest("tenant", "TEXT", false, true, null, null),
                new ApplicationDataSourceCreateTableColumnRequest("item_id", "INTEGER", false, true, null, null),
                new ApplicationDataSourceCreateTableColumnRequest("label", "TEXT", true, false, null, null)
            ]);

        var plan = await service.CreateTablePlanAsync(fixture.DataSourceId, table);
        Assert.Equal("CreateTable", plan.Operation);
        Assert.True(plan.RequiresConfirmation);
        Assert.True(plan.Reversible);
        Assert.NotEmpty(plan.Risks);

        var detail = await service.DeployTablePlanAsync(
            fixture.DataSourceId,
            new ApplicationDataSourceSchemaChangePlanRequest(plan.PlanHash, table, true));

        Assert.Equal("dc_created", detail.Table.TableName);
        Assert.Equal(["tenant", "item_id", "label"], detail.Columns.Select(item => item.ColumnName).ToArray());
        using var sourceDb = ApplicationDataStudioSqliteFixture.CreateDb(fixture.SourceDatabasePath);
        Assert.Equal(1, sourceDb.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'dc_created'"));

        var savedPlan = await fixture.AppDb.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .SingleAsync(item => item.PlanHash == plan.PlanHash);
        Assert.Equal("Applied", savedPlan.Status);
        var audit = await fixture.AppDb.Queryable<ApplicationSqlScriptAuditEntity>()
            .SingleAsync(item => item.SourceId == plan.PlanId);
        Assert.True(audit.IsSuccess);
        Assert.Equal("SchemaChangePlan", audit.SourceKind);
        Assert.Equal("schema.apply", audit.Operation);
        Assert.Equal("Sqlite", audit.Provider);
        Assert.DoesNotContain("password", audit.ScriptPreview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedSchemaChangeLeavesExistingSqliteObjectAndRecordsFailureAudit()
    {
        var service = fixture.CreateTableWorkbenchService();
        var table = new ApplicationDataSourceCreateTableRequest(
            "dc_existing",
            null,
            null,
            null,
            [new ApplicationDataSourceCreateTableColumnRequest("id", "INTEGER", false, true, null, null)]);
        var plan = await service.CreateTablePlanAsync(fixture.DataSourceId, table);

        using (var sourceDb = ApplicationDataStudioSqliteFixture.CreateDb(fixture.SourceDatabasePath))
        {
            sourceDb.Ado.ExecuteCommand("CREATE TABLE dc_existing (id INTEGER PRIMARY KEY, marker TEXT NOT NULL DEFAULT 'original')");
            sourceDb.Ado.ExecuteCommand("INSERT INTO dc_existing (id) VALUES (1)");
        }

        await Assert.ThrowsAnyAsync<Exception>(() => service.DeployTablePlanAsync(
            fixture.DataSourceId,
            new ApplicationDataSourceSchemaChangePlanRequest(plan.PlanHash, table, true)));

        using var verificationDb = ApplicationDataStudioSqliteFixture.CreateDb(fixture.SourceDatabasePath);
        Assert.Equal(1, verificationDb.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'dc_existing'"));
        Assert.Equal("original", verificationDb.Ado.GetString("SELECT marker FROM dc_existing"));
        var savedPlan = await fixture.AppDb.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .SingleAsync(item => item.PlanHash == plan.PlanHash);
        Assert.Equal("Failed", savedPlan.Status);
        var audit = await fixture.AppDb.Queryable<ApplicationSqlScriptAuditEntity>()
            .SingleAsync(item => item.SourceId == plan.PlanId);
        Assert.False(audit.IsSuccess);
        Assert.Equal("Failed", audit.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(audit.ErrorMessage));
    }

    [Fact]
    public async Task ViewReplacementValidatesCandidateAndPreservesOldSqliteViewOnFailure()
    {
        var service = fixture.CreateViewWorkbenchService();
        var created = await service.CreateAsync(
            fixture.DataSourceId,
            new ApplicationDataSourceViewUpsertRequest("people_view", null, "People", "SELECT id, name FROM dc_people", null));

        using (var sourceDb = ApplicationDataStudioSqliteFixture.CreateDb(fixture.SourceDatabasePath))
            Assert.Equal("Alice", sourceDb.Ado.GetString("SELECT name FROM people_view WHERE id = 1"));

        await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateAsync(
            fixture.DataSourceId,
            created.Id,
            new ApplicationDataSourceViewUpsertRequest("people_view", null, "People", "SELECT missing_column FROM dc_people", null)));

        using var verificationDb = ApplicationDataStudioSqliteFixture.CreateDb(fixture.SourceDatabasePath);
        Assert.Equal("Alice", verificationDb.Ado.GetString("SELECT name FROM people_view WHERE id = 1"));
        Assert.Equal(0, verificationDb.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE name LIKE 'people_view_candidate_%'"));
        var saved = await fixture.AppDb.Queryable<ApplicationQueryDatasetEntity>().SingleAsync(item => item.Id == created.Id);
        Assert.Equal("SELECT id, name FROM dc_people", saved.ViewSql);
        var audits = await fixture.AppDb.Queryable<ApplicationSqlScriptAuditEntity>()
            .Where(item => item.SourceId == created.Id)
            .ToListAsync();
        Assert.Contains(audits, item => item.Operation == "view.create" && item.IsSuccess);
    }

    [Fact]
    public async Task SecretDetailRedactsValuesWhileConnectionFactoryUsesProtectedSecret()
    {
        var service = fixture.CreateDataSourceService();
        var request = new ApplicationDataCenterObjectUpsertRequest
        {
            ObjectCode = "secret_sqlite",
            ObjectName = "Secret SQLite",
            ObjectType = ApplicationDataSourceType.Sqlite,
            ConfigJson = "{\"databaseName\":\"studio.db\"}",
            SecretConfigJson = "{\"password\":\"s3cr3t-value\",\"token\":\"token-value\"}"
        };

        var created = await service.CreateAsync(request);
        var detail = await service.GetAsync(created.Object.Id);
        var entity = await fixture.AppDb.Queryable<ApplicationDataSourceEntity>().SingleAsync(item => item.Id == created.Object.Id);
        var connection = fixture.ConnectionFactory.Resolve(entity);

        Assert.NotEqual(request.SecretConfigJson, entity.SecretConfigCipherText);
        Assert.Contains("s3cr3t-value", connection.Password, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t-value", detail.ConfigJson, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", detail.ConfigJson, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t-value", detail.PublicConfigJson, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", detail.PublicConfigJson, StringComparison.Ordinal);
        var publicConfigJson = detail.PublicConfigJson ?? throw new Xunit.Sdk.XunitException("PublicConfigJson must be present for a configured secret.");
        using var publicConfig = JsonDocument.Parse(publicConfigJson);
        Assert.True(publicConfig.RootElement.GetProperty("hasSecret").GetBoolean());
        Assert.True(publicConfig.RootElement.GetProperty("masked").GetBoolean());
        Assert.Equal(entity.SecretRef, publicConfig.RootElement.GetProperty("secretRef").GetString());
        Assert.True(publicConfig.RootElement.TryGetProperty("updatedAt", out _));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("token")]
    [InlineData("apiKey")]
    [InlineData("secret")]
    [InlineData("connectionString")]
    [InlineData("connectionStringCipherText")]
    public async Task SensitivePublicConfigIsRejectedBeforeDataSourceInsert(string sensitiveProperty)
    {
        var service = fixture.CreateDataSourceService();
        var objectCode = $"rejected_{sensitiveProperty.ToLowerInvariant()}";

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new ApplicationDataCenterObjectUpsertRequest
        {
            ObjectCode = objectCode,
            ObjectName = "Rejected secret config",
            ObjectType = ApplicationDataSourceType.Sqlite,
            ConfigJson = $"{{\"databaseName\":\"studio.db\",\"nested\":{{\"{sensitiveProperty}\":\"must-not-persist\"}}}}",
            SecretConfigJson = "{\"password\":\"must-use-secret-chain\"}"
        }));

        Assert.Equal(AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig, exception.Code);
        Assert.Contains("SecretConfigJson", exception.Message, StringComparison.Ordinal);
        Assert.False(await fixture.AppDb.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.ObjectCode == objectCode)
            .AnyAsync());
    }

    [Fact]
    public async Task SensitivePublicConfigIsRejectedBeforeExistingDataSourceUpdate()
    {
        var service = fixture.CreateDataSourceService();
        var created = await service.CreateAsync(new ApplicationDataCenterObjectUpsertRequest
        {
            ObjectCode = "reject_update_secret",
            ObjectName = "Reject update secret",
            ObjectType = ApplicationDataSourceType.Sqlite,
            ConfigJson = "{\"databaseName\":\"studio.db\"}",
            SecretConfigJson = "{\"password\":\"stored-secret\"}"
        });

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(created.Object.Id, new ApplicationDataCenterObjectUpsertRequest
        {
            ObjectCode = created.Object.ObjectCode,
            ObjectName = created.Object.ObjectName,
            ObjectType = created.Object.ObjectType,
            ConfigJson = "{\"databaseName\":\"studio.db\",\"headers\":[{\"apiKey\":\"must-not-persist\"}]}"
        }));

        var entity = await fixture.AppDb.Queryable<ApplicationDataSourceEntity>()
            .SingleAsync(item => item.Id == created.Object.Id);
        Assert.Equal("{\"databaseName\":\"studio.db\"}", entity.ConfigJson);
        Assert.NotNull(entity.SecretConfigCipherText);
        Assert.NotNull(entity.SecretRef);
    }

    [Fact]
    public async Task ReplacingSecretUsesDedicatedCommandAndWritesRedactedAudit()
    {
        var service = fixture.CreateDataSourceService();
        var created = await service.CreateAsync(new ApplicationDataCenterObjectUpsertRequest
        {
            ObjectCode = "secret_replace_sqlite",
            ObjectName = "Secret replace SQLite",
            ObjectType = ApplicationDataSourceType.Sqlite,
            ConfigJson = "{\"databaseName\":\"studio.db\"}"
        });

        var response = await service.ReplaceSecretAsync(created.Object.Id, new ApplicationDataSourceSecretReplaceRequest
        {
            Reason = "rotate test credential",
            SecretConfigJson = "{\"password\":\"replace-me\",\"token\":\"replace-token\"}"
        });

        var entity = await fixture.AppDb.Queryable<ApplicationDataSourceEntity>().SingleAsync(item => item.Id == created.Object.Id);
        var audit = await fixture.AppDb.Queryable<ApplicationSqlScriptAuditEntity>()
            .Where(item => item.SourceId == created.Object.Id && item.Operation == "data-source.secret.replace")
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .FirstAsync();

        Assert.NotNull(entity.SecretConfigCipherText);
        Assert.NotNull(entity.SecretRef);
        Assert.Equal(entity.SecretRef, JsonDocument.Parse(response.Object.PublicConfigJson ?? "{}").RootElement.GetProperty("secretRef").GetString());
        Assert.True(audit.IsSuccess);
        Assert.DoesNotContain("replace-me", audit.RedactedDetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("replace-token", audit.RedactedDetailsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClearingSecretConfigRemovesCiphertextSecretRefAndPublicSummary()
    {
        var service = fixture.CreateDataSourceService();
        var created = await service.CreateAsync(new ApplicationDataCenterObjectUpsertRequest
        {
            ObjectCode = "secret_clear_sqlite",
            ObjectName = "Secret clear SQLite",
            ObjectType = ApplicationDataSourceType.Sqlite,
            ConfigJson = "{\"databaseName\":\"studio.db\"}",
            SecretConfigJson = "{\"password\":\"clear-me\"}"
        });

        await service.ClearSecretAsync(created.Object.Id, new ApplicationDataSourceSecretClearRequest
        {
            Reason = "remove obsolete test credential"
        });

        var entity = await fixture.AppDb.Queryable<ApplicationDataSourceEntity>()
            .SingleAsync(item => item.Id == created.Object.Id);
        var detail = await service.GetAsync(created.Object.Id);

        Assert.Null(entity.SecretConfigCipherText);
        Assert.Null(entity.SecretRef);
        Assert.Equal("{}", entity.PublicConfigJson);
        Assert.Equal("{}", detail.PublicConfigJson);
        Assert.True(await fixture.AppDb.Queryable<ApplicationSqlScriptAuditEntity>()
            .Where(item => item.SourceId == created.Object.Id && item.Operation == "data-source.secret.clear" && item.IsSuccess)
            .AnyAsync());
    }

    [Fact]
    public async Task SqliteConnectionUsesSandboxedRelativePathAndRejectsTraversal()
    {
        var entity = fixture.ReadDataSource();
        var connectionString = await fixture.ConnectionFactory.BuildConnectionStringAsync(
            fixture.ConnectionFactory.Resolve(entity),
            DbType.Sqlite,
            fixture.DataSourceId);

        Assert.Contains(Path.Combine("data", "application-databases", "tenant-a", "MES", "studio.db"), connectionString, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Sandbox.ResolveAsync(
            Path.Combine("..", "outside.db"), fixture.DataSourceId));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Sandbox.ResolveAsync(
            "bad\0path.db", fixture.DataSourceId));
    }

    public void Dispose() => fixture.Dispose();
}
