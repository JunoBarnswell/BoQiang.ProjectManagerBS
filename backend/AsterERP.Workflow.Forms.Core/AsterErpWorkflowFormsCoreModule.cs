using AsterERP.Workflow.Approval.Core;
using Volo.Abp.Modularity;

namespace AsterERP.Workflow.Forms.Core;

[DependsOn(typeof(AsterErpWorkflowApprovalCoreModule))]
public sealed class AsterErpWorkflowFormsCoreModule : AbpModule
{
}
