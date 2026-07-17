using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class EscalationBoundaryEventActivityBehavior : BoundaryEventActivityBehavior
{
    public string? EscalationRef { get; set; }
    public string? EscalationCode { get; set; }

    public EscalationBoundaryEventActivityBehavior() { }

    public EscalationBoundaryEventActivityBehavior(string? escalationRef, string? escalationCode, bool interrupting = true)
        : base(interrupting)
    {
        EscalationRef = escalationRef;
        EscalationCode = escalationCode;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_escalationBoundarySubscription", true);
        execution.SetVariableLocal("_escalationBoundaryActive", true);
        if (!string.IsNullOrWhiteSpace(EscalationRef))
            execution.SetVariableLocal("_escalationBoundaryRef", EscalationRef);
        if (!string.IsNullOrWhiteSpace(EscalationCode))
            execution.SetVariableLocal("_escalationBoundaryCode", EscalationCode);
        await Task.CompletedTask;
    }

    public virtual async Task CatchEscalationAsync(ExecutionEntity execution, string? escalationRef, string? escalationCode, CancellationToken cancellationToken = default)
    {
        if (!MatchesEscalation(escalationRef, escalationCode))
            return;

        execution.SetVariableLocal("_escalationBoundaryActive", false);
        execution.SetVariable("_caughtEscalationRef", escalationRef);
        execution.SetVariable("_caughtEscalationCode", escalationCode);

        if (Interrupting)
            await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
        else
            await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
    }

    protected virtual bool MatchesEscalation(string? escalationRef, string? escalationCode)
    {
        if (!string.IsNullOrWhiteSpace(EscalationCode))
            return string.Equals(EscalationCode, escalationCode, System.StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(EscalationRef))
            return string.Equals(EscalationRef, escalationRef, System.StringComparison.Ordinal);
        return true;
    }
}

public class EscalationStartEventActivityBehavior : FlowNodeActivityBehavior
{
    public string? EscalationRef { get; set; }
    public string? EscalationCode { get; set; }

    public EscalationStartEventActivityBehavior() { }

    public EscalationStartEventActivityBehavior(string? escalationRef, string? escalationCode)
    {
        EscalationRef = escalationRef;
        EscalationCode = escalationCode;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_escalationStartTriggered", true);
        if (!string.IsNullOrWhiteSpace(EscalationRef))
            execution.SetVariableLocal("_escalationRef", EscalationRef);
        if (!string.IsNullOrWhiteSpace(EscalationCode))
            execution.SetVariableLocal("_escalationCode", EscalationCode);
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }
}

public class EscalationThrowEventActivityBehavior : FlowNodeActivityBehavior
{
    public string? EscalationRef { get; set; }
    public string? EscalationCode { get; set; }

    public EscalationThrowEventActivityBehavior() { }

    public EscalationThrowEventActivityBehavior(string? escalationRef, string? escalationCode)
    {
        EscalationRef = escalationRef;
        EscalationCode = escalationCode;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariable("_escalationThrown", true);
        if (!string.IsNullOrWhiteSpace(EscalationRef))
            execution.SetVariable("_escalationRef", EscalationRef);
        if (!string.IsNullOrWhiteSpace(EscalationCode))
            execution.SetVariable("_escalationCode", EscalationCode);
        execution.EventName = "escalation";
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }
}

public class EscalationEndEventActivityBehavior : FlowNodeActivityBehavior
{
    public string? EscalationRef { get; set; }
    public string? EscalationCode { get; set; }

    public EscalationEndEventActivityBehavior() { }

    public EscalationEndEventActivityBehavior(string? escalationRef, string? escalationCode)
    {
        EscalationRef = escalationRef;
        EscalationCode = escalationCode;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariable("_escalationThrown", true);
        if (!string.IsNullOrWhiteSpace(EscalationRef))
            execution.SetVariable("_escalationRef", EscalationRef);
        if (!string.IsNullOrWhiteSpace(EscalationCode))
            execution.SetVariable("_escalationCode", EscalationCode);
        execution.EventName = "escalation";
        execution.IsActive = false;
        execution.IsEnded = true;
        await Task.CompletedTask;
    }
}
