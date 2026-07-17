using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowUserTaskActivityBehaviorTests
{
    [Fact]
    public async Task ExecuteAsync_ResolvesCandidateUsersExpressionCollection()
    {
        var userTask = new UserTask
        {
            Id = "approveTask",
            Name = "审批",
            CandidateUsers = ["${starterDeptManagerUserIds}"]
        };
        var execution = new ExecutionEntity
        {
            Id = "execution-1",
            ProcessInstanceId = "process-instance-1",
            ProcessDefinitionId = "MES:test:1",
            Variables = new Dictionary<string, object?>
            {
                ["starterDeptManagerUserIds"] = new List<string> { "wf_manager_approver", "wf_position_approver", "wf_dept_approver" }
            }
        };
        var behavior = new UserTaskActivityBehavior(userTask, new ExpressionManagerImplementation());

        await behavior.ExecuteAsync(execution);

        var task = Assert.Single(execution.TaskEntities);
        Assert.Null(task.Assignee);
        Assert.Equal(
            ["wf_manager_approver", "wf_position_approver", "wf_dept_approver"],
            task.CandidateUsers);
    }
}
