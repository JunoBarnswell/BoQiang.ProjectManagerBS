using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public sealed class DataCenterTableCreateTool(ApplicationDataSourceTableWorkbenchService tableService) : AiDataCenterToolBase(
    AiDataCenterToolDefinition.Create(
        AiDataCenterToolCodes.TableCreate,
        "创建数据表",
        "在当前数据库中真实执行 DDL 创建数据表",
        "L4",
        AiDataCenterToolDefinition.AiWritePermission,
        PermissionCodes.AppDataCenterDataSourceEdit,
        ["Agent"],
        ["dataSourceId", "request"],
        requiresConfirmation: true))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var dataSourceId = AiDataCenterArgumentReader.RequiredString(context.Arguments, "dataSourceId");
        var request = AiDataCenterArgumentReader.ReadRequest<ApplicationDataSourceCreateTableRequest>(context.Arguments, "request");
        var plan = await tableService.CreateTablePlanAsync(dataSourceId, request, cancellationToken);
        var table = await tableService.DeployTablePlanAsync(dataSourceId, new ApplicationDataSourceSchemaChangePlanRequest(plan.PlanHash, request, true), cancellationToken);
        return Result($"已创建数据表 {table.Table.TableName}", table);
    }
}
