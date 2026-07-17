using SqlSugar;

namespace AsterERP.Workflow.Core.Services;

public interface IWorkflowSqlSugarClientAccessor
{
    ISqlSugarClient Db { get; }
}
