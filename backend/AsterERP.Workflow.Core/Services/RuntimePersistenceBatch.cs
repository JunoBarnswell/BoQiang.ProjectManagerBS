using System.Collections.Generic;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Services;

public sealed class RuntimePersistenceBatch
{
    public IReadOnlyCollection<ExecutionEntity> RootExecutions { get; init; } = [];
    public IReadOnlyCollection<TaskImplementation> StandaloneTasks { get; init; } = [];
    public IReadOnlyCollection<string> DeletedProcessInstanceIds { get; init; } = [];
    public IReadOnlyCollection<string> DeletedTaskIds { get; init; } = [];
}
