using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 统一封装项目管理聚合写入的数据库事务边界；提交前不发布外部实时事件。
/// </summary>
public static class ProjectManagementMutationTransaction
{
    public static async Task RunAsync(ISqlSugarClient db, Func<Task> action)
    {
        db.Ado.BeginTran();
        try
        {
            await action();
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }
}
