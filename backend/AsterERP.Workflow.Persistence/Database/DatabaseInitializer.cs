using AsterERP.Workflow.Persistence.Entities;
using System.Data;
using System.Reflection;
using SqlSugar;

namespace AsterERP.Workflow.Persistence.Database;

public class DatabaseInitializer
{
    private const string EngineVersion = "astererp-activiti-native-1";
    private static readonly Type[] NativeEntityTypes =
    [
        typeof(PropertyEntity),
        typeof(ByteArrayEntity),
        typeof(DeploymentEntity),
        typeof(ProcessDefinitionEntity),
        typeof(ModelEntity),
        typeof(ExecutionEntity),
        typeof(TaskEntity),
        typeof(VariableInstanceEntity),
        typeof(IdentityLinkEntity),
        typeof(EventSubscriptionEntity),
        typeof(JobEntity),
        typeof(TimerJobEntity),
        typeof(SuspendedJobEntity),
        typeof(DeadLetterJobEntity),
        typeof(EventLogEntryEntity),
        typeof(IntegrationContextEntity),
        typeof(HistoricProcessInstanceEntity),
        typeof(HistoricTaskInstanceEntity),
        typeof(HistoricActivityInstanceEntity),
        typeof(HistoricVariableInstanceEntity),
        typeof(HistoricDetailEntity),
        typeof(HistoricIdentityLinkEntity),
        typeof(CommentEntity),
        typeof(AttachmentEntity),
        typeof(ActIdUserEntity),
        typeof(ActIdGroupEntity),
        typeof(ActIdMembershipEntity),
        typeof(ActIdInfoEntity),
        typeof(ProcessDefinitionInfoEntity)
    ];

    private readonly ISqlSugarClient _db;
    private readonly SqliteSchemaValidator _sqliteSchemaValidator;

    public DatabaseInitializer(ISqlSugarClient db, SqliteSchemaValidator sqliteSchemaValidator)
    {
        _db = db;
        _sqliteSchemaValidator = sqliteSchemaValidator;
    }

    public void Initialize()
    {
        RepairLegacyNativeTables();
        _db.CodeFirst.InitTables(NativeEntityTypes);
        CreateNativeIndexes();
        UpsertEngineProperties();

        _sqliteSchemaValidator.Validate();
    }

    private void RepairLegacyNativeTables()
    {
        if (_db.CurrentConnectionConfig?.DbType != SqlSugar.DbType.Sqlite)
        {
            return;
        }

        foreach (var entityType in NativeEntityTypes)
        {
            var tableName = ResolveTableName(entityType);
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            var actualColumns = GetTableColumns(tableName);
            if (actualColumns.Count == 0)
            {
                continue;
            }

            var expectedColumns = ResolveExpectedColumns(entityType);
            if (expectedColumns.Count == 0 || !NeedsLegacyTableRepair(actualColumns, expectedColumns))
            {
                continue;
            }

            RepairLegacyTable(tableName, actualColumns, expectedColumns);
        }
    }

    private Dictionary<string, SqliteColumn> GetTableColumns(string tableName)
    {
        var safeTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
        var tableInfo = _db.Ado.GetDataTable($"PRAGMA table_info('{safeTableName}')");
        return tableInfo.Rows
            .Cast<DataRow>()
            .Select(row => new SqliteColumn(
                Convert.ToString(row["name"]) ?? string.Empty,
                Convert.ToInt32(row["notnull"]) != 0,
                Convert.ToInt32(row["pk"]) > 0))
            .Where(column => !string.IsNullOrWhiteSpace(column.Name))
            .ToDictionary(column => column.Name, column => column, StringComparer.OrdinalIgnoreCase);
    }

    private void RepairLegacyTable(
        string tableName,
        IReadOnlyDictionary<string, SqliteColumn> actualColumns,
        IReadOnlyList<ExpectedColumn> expectedColumns)
    {
        var repairTableName = $"{tableName}_REPAIR";
        var createColumns = string.Join(
            ",\n  ",
            expectedColumns.Select(column => BuildColumnDefinition(column)));
        var targetColumns = string.Join(", ", expectedColumns.Select(column => QuoteIdentifier(column.Name)));
        var sourceColumns = string.Join(", ", expectedColumns.Select(column => BuildSourceExpression(column, actualColumns)));

        _db.Ado.BeginTran();
        try
        {
            _db.Ado.ExecuteCommand($"DROP TABLE IF EXISTS {QuoteIdentifier(repairTableName)};");
            _db.Ado.ExecuteCommand(
                $"""
                CREATE TABLE {QuoteIdentifier(repairTableName)}(
                  {createColumns}
                );
                """);
            _db.Ado.ExecuteCommand(
                $"""
                INSERT OR REPLACE INTO {QuoteIdentifier(repairTableName)}({targetColumns})
                SELECT {sourceColumns}
                FROM {QuoteIdentifier(tableName)};
                """);
            _db.Ado.ExecuteCommand($"DROP TABLE {QuoteIdentifier(tableName)};");
            _db.Ado.ExecuteCommand($"ALTER TABLE {QuoteIdentifier(repairTableName)} RENAME TO {QuoteIdentifier(tableName)};");
            _db.Ado.CommitTran();
        }
        catch
        {
            _db.Ado.RollbackTran();
            throw;
        }
    }

    private static bool NeedsLegacyTableRepair(
        IReadOnlyDictionary<string, SqliteColumn> actualColumns,
        IReadOnlyList<ExpectedColumn> expectedColumns)
    {
        if (actualColumns.ContainsKey("Id") && expectedColumns.Any(column => string.Equals(column.Name, "ID_", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return expectedColumns.Any(column =>
            !actualColumns.TryGetValue(column.Name, out var actualColumn) ||
            actualColumn.IsPrimaryKey != column.IsPrimaryKey ||
            actualColumn.IsNotNull == column.IsNullable);
    }

    private static IReadOnlyList<ExpectedColumn> ResolveExpectedColumns(Type entityType)
    {
        return entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Column: property.GetCustomAttribute<SugarColumn>()))
            .Where(item => item.Column?.IsIgnore != true)
            .Select(item =>
            {
                var columnName = !string.IsNullOrWhiteSpace(item.Column?.ColumnName)
                    ? item.Column.ColumnName!
                    : item.Property.Name;

                return new ExpectedColumn(
                    columnName,
                    ResolveColumnType(item.Property, item.Column),
                    ResolveExpectedNullable(item.Property, item.Column),
                    item.Column?.IsPrimaryKey == true,
                    item.Column?.IsIdentity == true,
                    Nullable.GetUnderlyingType(item.Property.PropertyType) ?? item.Property.PropertyType);
            })
            .GroupBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static string? ResolveTableName(Type entityType)
    {
        return entityType.GetCustomAttribute<SugarTable>()?.TableName;
    }

    private static bool ResolveExpectedNullable(PropertyInfo property, SugarColumn? columnAttribute)
    {
        if (columnAttribute?.IsPrimaryKey == true)
        {
            return false;
        }

        if (columnAttribute != null)
        {
            return columnAttribute.IsNullable;
        }

        if (Nullable.GetUnderlyingType(property.PropertyType) != null)
        {
            return true;
        }

        return !property.PropertyType.IsValueType;
    }

    private static string ResolveColumnType(PropertyInfo property, SugarColumn? columnAttribute)
    {
        if (!string.IsNullOrWhiteSpace(columnAttribute?.ColumnDataType))
        {
            return columnAttribute.ColumnDataType!;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(string))
        {
            return "varchar(255)";
        }

        if (propertyType == typeof(int))
        {
            return "integer";
        }

        if (propertyType == typeof(long))
        {
            return "bigint";
        }

        if (propertyType == typeof(bool))
        {
            return "bit";
        }

        if (propertyType == typeof(DateTime))
        {
            return "datetime";
        }

        if (propertyType == typeof(double) || propertyType == typeof(float) || propertyType == typeof(decimal))
        {
            return "real";
        }

        if (propertyType == typeof(byte[]))
        {
            return "BLOB";
        }

        return "varchar(255)";
    }

    private static string BuildColumnDefinition(ExpectedColumn column)
    {
        var nullability = column.IsNullable && !column.IsPrimaryKey ? "NULL" : "NOT NULL";
        var primaryKey = column.IsPrimaryKey
            ? column.IsIdentity ? " PRIMARY KEY AUTOINCREMENT" : " PRIMARY KEY"
            : string.Empty;

        return $"{QuoteIdentifier(column.Name)} {column.SqlType} {nullability}{primaryKey}";
    }

    private static string BuildSourceExpression(
        ExpectedColumn column,
        IReadOnlyDictionary<string, SqliteColumn> actualColumns)
    {
        if (string.Equals(column.Name, "ID_", StringComparison.OrdinalIgnoreCase) && actualColumns.ContainsKey("Id"))
        {
            return actualColumns.ContainsKey("ID_")
                ? $"COALESCE(NULLIF({QuoteIdentifier("ID_")}, ''), NULLIF({QuoteIdentifier("Id")}, ''), {BuildDefaultExpression(column)})"
                : $"COALESCE(NULLIF({QuoteIdentifier("Id")}, ''), {BuildDefaultExpression(column)})";
        }

        return actualColumns.ContainsKey(column.Name)
            ? QuoteIdentifier(column.Name)
            : BuildDefaultExpression(column);
    }

    private static string BuildDefaultExpression(ExpectedColumn column)
    {
        if (column.IsNullable)
        {
            return "NULL";
        }

        if (column.IsPrimaryKey && column.ClrType == typeof(string))
        {
            return "lower(hex(randomblob(16)))";
        }

        if (column.ClrType == typeof(string))
        {
            return "''";
        }

        if (column.ClrType == typeof(DateTime))
        {
            return "CURRENT_TIMESTAMP";
        }

        if (column.ClrType == typeof(byte[]))
        {
            return "X''";
        }

        return "0";
    }

    private static string QuoteIdentifier(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private void CreateNativeIndexes()
    {
        if (_db.CurrentConnectionConfig?.DbType != SqlSugar.DbType.Sqlite)
        {
            return;
        }

        Execute("CREATE INDEX IF NOT EXISTS idx_act_ge_bytearray_deployment ON ACT_GE_BYTEARRAY(DEPLOYMENT_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_re_procdef_key_version ON ACT_RE_PROCDEF(KEY_, VERSION_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_re_procdef_deployment ON ACT_RE_PROCDEF(DEPLOYMENT_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_re_model_key ON ACT_RE_MODEL(KEY_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_execution_procinst ON ACT_RU_EXECUTION(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_execution_procdef ON ACT_RU_EXECUTION(PROC_DEF_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_task_assignee ON ACT_RU_TASK(ASSIGNEE_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_task_procinst ON ACT_RU_TASK(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_variable_exec ON ACT_RU_VARIABLE(EXECUTION_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_variable_procinst ON ACT_RU_VARIABLE(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_identitylink_task ON ACT_RU_IDENTITYLINK(TASK_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_identitylink_procinst ON ACT_RU_IDENTITYLINK(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_identitylink_user ON ACT_RU_IDENTITYLINK(USER_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_identitylink_group ON ACT_RU_IDENTITYLINK(GROUP_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_job_due ON ACT_RU_JOB(DUEDATE_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_ru_timer_job_due ON ACT_RU_TIMER_JOB(DUEDATE_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_hi_procinst_start ON ACT_HI_PROCINST(START_TIME_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_hi_taskinst_procinst ON ACT_HI_TASKINST(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_hi_taskinst_assignee ON ACT_HI_TASKINST(ASSIGNEE_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_hi_actinst_procinst ON ACT_HI_ACTINST(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_hi_varinst_procinst ON ACT_HI_VARINST(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_hi_identitylink_procinst ON ACT_HI_IDENTITYLINK(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_hi_comment_procinst ON ACT_HI_COMMENT(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_hi_attachment_procinst ON ACT_HI_ATTACHMENT(PROC_INST_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_id_membership_user ON ACT_ID_MEMBERSHIP(USER_ID_);");
        Execute("CREATE INDEX IF NOT EXISTS idx_act_id_membership_group ON ACT_ID_MEMBERSHIP(GROUP_ID_);");
    }

    private void UpsertEngineProperties()
    {
        UpsertProperty("schema.version", EngineVersion);
        UpsertProperty("schema.history", "create");
        UpsertProperty("astererp.workflow.initialized", AbpTimeIdProvider.UtcNow.ToString("O"));
    }

    private void UpsertProperty(string name, string value)
    {
        var existing = _db.Queryable<PropertyEntity>().InSingle(name);
        if (existing is null)
        {
            _db.Insertable(new PropertyEntity
            {
                Name = name,
                Value = value,
                Revision = 1
            }).ExecuteCommand();
            return;
        }

        existing.Value = value;
        existing.Revision += 1;
        _db.Updateable(existing).ExecuteCommand();
    }

    private void Execute(string sql)
    {
        _db.Ado.ExecuteCommand(sql);
    }

    private sealed record ExpectedColumn(
        string Name,
        string SqlType,
        bool IsNullable,
        bool IsPrimaryKey,
        bool IsIdentity,
        Type ClrType);

    private sealed record SqliteColumn(string Name, bool IsNotNull, bool IsPrimaryKey);
}

