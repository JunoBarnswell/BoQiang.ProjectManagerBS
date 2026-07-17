using System.Reflection;
using System.Text;
using SqlSugar;

namespace AsterERP.Workflow.Persistence.Database;

public sealed class SqliteSchemaValidator
{
    private static readonly Type[] ContractTypes =
    [
        typeof(Entities.PropertyEntity),
        typeof(Entities.ByteArrayEntity),
        typeof(Entities.DeploymentEntity),
        typeof(Entities.ProcessDefinitionEntity),
        typeof(Entities.ModelEntity),
        typeof(Entities.ExecutionEntity),
        typeof(Entities.TaskEntity),
        typeof(Entities.VariableInstanceEntity),
        typeof(Entities.IdentityLinkEntity),
        typeof(Entities.EventSubscriptionEntity),
        typeof(Entities.JobEntity),
        typeof(Entities.TimerJobEntity),
        typeof(Entities.SuspendedJobEntity),
        typeof(Entities.DeadLetterJobEntity),
        typeof(Entities.EventLogEntryEntity),
        typeof(Entities.IntegrationContextEntity),
        typeof(Entities.CommentEntity),
        typeof(Entities.AttachmentEntity),
        typeof(Entities.ProcessDefinitionInfoEntity),
        typeof(Entities.HistoricProcessInstanceEntity),
        typeof(Entities.HistoricTaskInstanceEntity),
        typeof(Entities.HistoricActivityInstanceEntity),
        typeof(Entities.HistoricVariableInstanceEntity),
        typeof(Entities.HistoricDetailEntity),
        typeof(Entities.HistoricIdentityLinkEntity),
        typeof(Entities.ActIdUserEntity),
        typeof(Entities.ActIdGroupEntity),
        typeof(Entities.ActIdMembershipEntity),
        typeof(Entities.ActIdInfoEntity)
    ];

    private static readonly IReadOnlyDictionary<string, string[]> RequiredIndexes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ACT_GE_BYTEARRAY"] = ["idx_act_ge_bytearray_deployment"],
            ["ACT_RE_PROCDEF"] = ["idx_act_re_procdef_key_version", "idx_act_re_procdef_deployment"],
            ["ACT_RE_MODEL"] = ["idx_act_re_model_key"],
            ["ACT_RU_EXECUTION"] = ["idx_act_ru_execution_procinst", "idx_act_ru_execution_procdef"],
            ["ACT_RU_TASK"] = ["idx_act_ru_task_assignee", "idx_act_ru_task_procinst"],
            ["ACT_RU_VARIABLE"] = ["idx_act_ru_variable_exec", "idx_act_ru_variable_procinst"],
            ["ACT_RU_IDENTITYLINK"] = ["idx_act_ru_identitylink_task", "idx_act_ru_identitylink_procinst", "idx_act_ru_identitylink_user", "idx_act_ru_identitylink_group"],
            ["ACT_RU_JOB"] = ["idx_act_ru_job_due"],
            ["ACT_RU_TIMER_JOB"] = ["idx_act_ru_timer_job_due"],
            ["ACT_HI_PROCINST"] = ["idx_act_hi_procinst_start"],
            ["ACT_HI_TASKINST"] = ["idx_act_hi_taskinst_procinst", "idx_act_hi_taskinst_assignee"],
            ["ACT_HI_ACTINST"] = ["idx_act_hi_actinst_procinst"],
            ["ACT_HI_VARINST"] = ["idx_act_hi_varinst_procinst"],
            ["ACT_HI_IDENTITYLINK"] = ["idx_act_hi_identitylink_procinst"],
            ["ACT_HI_COMMENT"] = ["idx_act_hi_comment_procinst"],
            ["ACT_HI_ATTACHMENT"] = ["idx_act_hi_attachment_procinst"],
            ["ACT_ID_MEMBERSHIP"] = ["idx_act_id_membership_user", "idx_act_id_membership_group"]
        };

    private readonly ISqlSugarClient _db;
    private readonly NullabilityInfoContext _nullabilityContext = new();

    public SqliteSchemaValidator(ISqlSugarClient db)
    {
        _db = db;
    }

    public void Validate()
    {
        if (_db.CurrentConnectionConfig?.DbType != SqlSugar.DbType.Sqlite)
        {
            return;
        }

        var mismatches = new List<string>();
        foreach (var contractType in ContractTypes)
        {
            ValidateTable(contractType, mismatches);
        }
        ValidateIndexes(mismatches);

        if (mismatches.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine("Detected incompatible SQLite schema for workflow persistence compatibility tables.");
        message.AppendLine("The existing database does not match the entity nullability contract.");
        foreach (var mismatch in mismatches)
        {
            message.AppendLine($"- {mismatch}");
        }

        throw new InvalidOperationException(message.ToString());
    }

    private void ValidateIndexes(List<string> mismatches)
    {
        foreach (var (tableName, indexNames) in RequiredIndexes)
        {
            foreach (var indexName in indexNames)
            {
                var safeIndexName = indexName.Replace("'", "''", StringComparison.Ordinal);
                var result = _db.Ado.GetDataTable(
                    $"SELECT COUNT(1) AS IndexCount FROM sqlite_master WHERE type = 'index' AND name = '{safeIndexName}'");

                var exists = result.Rows.Count > 0 &&
                             int.TryParse(result.Rows[0]["IndexCount"]?.ToString(), out var count) &&
                             count > 0;

                if (!exists)
                {
                    mismatches.Add($"{tableName}: required index {indexName} missing");
                }
            }
        }
    }

    private void ValidateTable(Type entityType, List<string> mismatches)
    {
        var tableAttribute = entityType.GetCustomAttribute<SugarTable>();
        var tableName = tableAttribute?.TableName;
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return;
        }

        var dataTable = _db.Ado.GetDataTable($"PRAGMA table_info('{tableName}')");
        if (dataTable.Rows.Count == 0)
        {
            mismatches.Add($"{tableName}: table missing");
            return;
        }

        var actualColumns = dataTable.Rows.Cast<System.Data.DataRow>()
            .ToDictionary(
                row => Convert.ToString(row["name"]) ?? string.Empty,
                row => Convert.ToInt32(row["notnull"]) == 0,
                StringComparer.OrdinalIgnoreCase);

        foreach (var property in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var columnAttribute = property.GetCustomAttribute<SugarColumn>();
            if (columnAttribute?.IsIgnore == true)
            {
                continue;
            }

            var columnName = !string.IsNullOrWhiteSpace(columnAttribute?.ColumnName)
                ? columnAttribute.ColumnName!
                : property.Name;

            if (!actualColumns.TryGetValue(columnName, out var actualNullable))
            {
                mismatches.Add($"{tableName}.{columnName}: column missing");
                continue;
            }

            var expectedNullable = ResolveExpectedNullable(property, columnAttribute);
            if (expectedNullable != actualNullable)
            {
                mismatches.Add($"{tableName}.{columnName}: expected nullable={expectedNullable}, actual nullable={actualNullable}");
            }
        }
    }

    private bool ResolveExpectedNullable(PropertyInfo property, SugarColumn? columnAttribute)
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

        if (!property.PropertyType.IsValueType)
        {
            var nullabilityInfo = _nullabilityContext.Create(property);
            return nullabilityInfo.WriteState == NullabilityState.Nullable;
        }

        return false;
    }
}
