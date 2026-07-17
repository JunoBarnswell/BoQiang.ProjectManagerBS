using AsterERP.Workflow.Processing.Definitions;
using AsterERP.Workflow.Processing.Graphs;

namespace AsterERP.Workflow.Processing.Analysis;

public interface IWorkflowProcessingGraphAnalyzer
{
    WorkflowProcessingSortResult TopologicalSort(WorkflowProcessingDefinition definition);

    IReadOnlyList<WorkflowProcessingPath> FindPaths(
        WorkflowProcessingDefinition definition,
        string fromNodeId,
        string toNodeId,
        int maxDepth,
        int limit);

    WorkflowProcessingImpactResult AnalyzeImpact(
        WorkflowProcessingDefinition definition,
        string rootNodeId,
        int maxDepth,
        int limit);

    WorkflowProcessingDiff Diff(WorkflowProcessingDefinition baseline, WorkflowProcessingDefinition candidate);
}
