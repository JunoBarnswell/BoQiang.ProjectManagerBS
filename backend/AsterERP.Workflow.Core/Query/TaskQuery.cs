using System.Collections.Generic;
using System.Linq;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public class TaskQuery : TaskQueryImpl
{
    public TaskQuery(IEnumerable<TaskImplementation> source) : base(source) { }

    public TaskQuery ByAssignee(string assignee) { TaskAssignee(assignee); return this; }
    public TaskQuery ByProcessInstanceId(string processInstanceId) { ProcessInstanceId(processInstanceId); return this; }

    public TaskQuery ByCandidateUser(string candidateUser) { TaskCandidateUser(candidateUser); return this; }

    public TaskQuery ByCandidateGroup(string candidateGroup) { TaskCandidateGroup(candidateGroup); return this; }

    public TaskQuery Unassigned() { TaskUnassigned(); return this; }
}
