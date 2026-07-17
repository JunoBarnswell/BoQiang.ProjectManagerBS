using AsterERP.Workflow.Persistence.Database;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowDatabaseInitializerTests : IDisposable
{
    private static readonly string[] RequiredNativeTables =
    [
        "ACT_GE_PROPERTY",
        "ACT_GE_BYTEARRAY",
        "ACT_RE_DEPLOYMENT",
        "ACT_RE_PROCDEF",
        "ACT_RE_MODEL",
        "ACT_RU_EXECUTION",
        "ACT_RU_TASK",
        "ACT_RU_VARIABLE",
        "ACT_RU_IDENTITYLINK",
        "ACT_RU_EVENT_SUBSCR",
        "ACT_RU_JOB",
        "ACT_RU_TIMER_JOB",
        "ACT_RU_SUSPENDED_JOB",
        "ACT_RU_DEADLETTER_JOB",
        "ACT_RU_EVENT_LOG",
        "ACT_RU_INTEGRATION_CONTEXT",
        "ACT_HI_PROCINST",
        "ACT_HI_TASKINST",
        "ACT_HI_ACTINST",
        "ACT_HI_VARINST",
        "ACT_HI_DETAIL",
        "ACT_HI_IDENTITYLINK",
        "ACT_HI_COMMENT",
        "ACT_HI_ATTACHMENT",
        "ACT_ID_USER",
        "ACT_ID_GROUP",
        "ACT_ID_MEMBERSHIP",
        "ACT_ID_INFO",
        "ACT_PROCDEF_INFO"
    ];

    private static readonly string[] RequiredIndexes =
    [
        "idx_act_re_procdef_key_version",
        "idx_act_ru_task_assignee",
        "idx_act_ru_identitylink_group",
        "idx_act_hi_procinst_start",
        "idx_act_id_membership_user"
    ];

    private readonly string _databasePath;

    public WorkflowDatabaseInitializerTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"astererp-workflow-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void Initialize_CreatesNativeWorkflowSchemaAndIsIdempotent()
    {
        using var db = CreateDb();
        var initializer = new DatabaseInitializer(db, new SqliteSchemaValidator(db));

        initializer.Initialize();
        initializer.Initialize();

        foreach (var tableName in RequiredNativeTables)
        {
            Assert.True(DatabaseObjectExists(db, "table", tableName), $"{tableName} should exist.");
        }

        foreach (var indexName in RequiredIndexes)
        {
            Assert.True(DatabaseObjectExists(db, "index", indexName), $"{indexName} should exist.");
        }

        Assert.True(ColumnExists(db, "ACT_HI_ACTINST", "CALL_PROC_INST_ID_"));
        Assert.True(ColumnExists(db, "ACT_HI_ACTINST", "ASSIGNEE_"));
        Assert.True(ColumnExists(db, "ACT_HI_ACTINST", "TRANSACTION_ORDER_"));
        Assert.True(ColumnExists(db, "ACT_HI_ACTINST", "DELETE_REASON_"));

        var schemaVersion = db.Queryable<PropertyEntity>().InSingle("schema.version");
        var initialized = db.Queryable<PropertyEntity>().InSingle("astererp.workflow.initialized");

        Assert.NotNull(schemaVersion);
        Assert.Equal("astererp-activiti-native-1", schemaVersion.Value);
        Assert.True(schemaVersion.Revision >= 2);
        Assert.NotNull(initialized);
        Assert.True(initialized.Revision >= 2);
    }

    [Fact]
    public void Initialize_RepairsLegacyNativeSchemas()
    {
        using var db = CreateDb();
        db.Ado.ExecuteCommand(
            """
            CREATE TABLE ACT_GE_BYTEARRAY(
              Id varchar(255) NOT NULL PRIMARY KEY,
              NAME_ varchar(255) NOT NULL,
              DEPLOYMENT_ID_ varchar(255) NOT NULL,
              BYTES_ BLOB NOT NULL,
              GENERATED_ bit NOT NULL,
              REV_ INTEGER NOT NULL,
              ID_ varchar(255)
            );
            """);
        db.Ado.ExecuteCommand(
            """
            INSERT INTO ACT_GE_BYTEARRAY(Id, NAME_, DEPLOYMENT_ID_, BYTES_, GENERATED_, REV_, ID_)
            VALUES('legacy-byte-array', 'model.bpmn', 'deployment-1', X'010203', 1, 7, NULL);
            """);
        db.Ado.ExecuteCommand(
            """
            CREATE TABLE ACT_RE_DEPLOYMENT(
              Id varchar(255) NOT NULL PRIMARY KEY,
              NAME_ varchar(255) NULL,
              CATEGORY_ varchar(255) NULL,
              KEY_ varchar(255) NULL,
              TENANT_ID_ varchar(255) NULL,
              DEPLOY_TIME_ datetime NULL,
              DERIVED_FROM_ varchar(255) NULL,
              ENGINE_VERSION_ varchar(255) NULL,
              ID_ varchar(255)
            );
            """);
        db.Ado.ExecuteCommand(
            """
            INSERT INTO ACT_RE_DEPLOYMENT(Id, NAME_, CATEGORY_, KEY_, TENANT_ID_, DEPLOY_TIME_, DERIVED_FROM_, ENGINE_VERSION_, ID_)
            VALUES('legacy-deployment', '流程部署', 'approval', 'purchase', 'tenant-a', '2026-06-14 00:00:00', NULL, 'native', NULL);
            """);

        var initializer = new DatabaseInitializer(db, new SqliteSchemaValidator(db));

        initializer.Initialize();

        Assert.False(ColumnExists(db, "ACT_GE_BYTEARRAY", "Id"));
        Assert.True(ColumnExists(db, "ACT_GE_BYTEARRAY", "ID_"));
        Assert.True(ColumnIsPrimaryKey(db, "ACT_GE_BYTEARRAY", "ID_"));
        Assert.True(ColumnIsNullable(db, "ACT_GE_BYTEARRAY", "NAME_"));
        Assert.True(ColumnIsNullable(db, "ACT_GE_BYTEARRAY", "DEPLOYMENT_ID_"));
        Assert.True(ColumnIsNullable(db, "ACT_GE_BYTEARRAY", "BYTES_"));

        var entity = db.Queryable<ByteArrayEntity>().InSingle("legacy-byte-array");
        Assert.NotNull(entity);
        Assert.Equal(7, entity.Revision);
        Assert.Equal("model.bpmn", entity.Name);
        Assert.Equal("deployment-1", entity.DeploymentId);
        Assert.Equal(new byte[] { 1, 2, 3 }, entity.Bytes);

        Assert.False(ColumnExists(db, "ACT_RE_DEPLOYMENT", "Id"));
        Assert.True(ColumnIsPrimaryKey(db, "ACT_RE_DEPLOYMENT", "ID_"));
        var deployment = db.Queryable<DeploymentEntity>().InSingle("legacy-deployment");
        Assert.NotNull(deployment);
        Assert.Equal("流程部署", deployment.Name);
        Assert.Equal("approval", deployment.Category);
        Assert.Equal("purchase", deployment.Key);
        Assert.Equal("tenant-a", deployment.TenantId);
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

    private SqlSugarClient CreateDb()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
    }

    private static bool DatabaseObjectExists(ISqlSugarClient db, string objectType, string objectName)
    {
        var safeType = objectType.Replace("'", "''", StringComparison.Ordinal);
        var safeName = objectName.Replace("'", "''", StringComparison.Ordinal);
        var result = db.Ado.GetDataTable(
            $"SELECT COUNT(1) AS ObjectCount FROM sqlite_master WHERE type = '{safeType}' AND name = '{safeName}'");

        return result.Rows.Count > 0 &&
               int.TryParse(result.Rows[0]["ObjectCount"]?.ToString(), out var count) &&
               count > 0;
    }

    private static bool ColumnExists(ISqlSugarClient db, string tableName, string columnName)
    {
        var safeTable = tableName.Replace("\"", "\"\"", StringComparison.Ordinal);
        var tableInfo = db.Ado.GetDataTable($"PRAGMA table_info(\"{safeTable}\")");

        return tableInfo.Rows
            .Cast<System.Data.DataRow>()
            .Any(row => string.Equals(row["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ColumnIsNullable(ISqlSugarClient db, string tableName, string columnName)
    {
        var row = GetColumnInfo(db, tableName, columnName);
        return row is not null && Convert.ToInt32(row["notnull"]) == 0;
    }

    private static bool ColumnIsPrimaryKey(ISqlSugarClient db, string tableName, string columnName)
    {
        var row = GetColumnInfo(db, tableName, columnName);
        return row is not null && Convert.ToInt32(row["pk"]) > 0;
    }

    private static System.Data.DataRow? GetColumnInfo(ISqlSugarClient db, string tableName, string columnName)
    {
        var safeTable = tableName.Replace("\"", "\"\"", StringComparison.Ordinal);
        var tableInfo = db.Ado.GetDataTable($"PRAGMA table_info(\"{safeTable}\")");

        return tableInfo.Rows
            .Cast<System.Data.DataRow>()
            .FirstOrDefault(row => string.Equals(row["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase));
    }
}
