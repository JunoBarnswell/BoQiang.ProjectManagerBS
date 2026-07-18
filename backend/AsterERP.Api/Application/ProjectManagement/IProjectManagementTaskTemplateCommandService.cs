using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskTemplateCapability
{
    internal static readonly ProjectManagementTaskTemplateCapability Instance = new();
    internal ProjectManagementTaskTemplateCapability() { }
}

public sealed record ProjectManagementTaskTemplateNodeCreateCommand(string NodeKey, ProjectManagementTaskUpsertRequest Task, IReadOnlyList<string> LabelIds);
public sealed record ProjectManagementTaskTemplateDependencyCreateCommand(string PredecessorNodeKey, string SuccessorNodeKey, string DependencyType, int LagMinutes);
public sealed record ProjectManagementTaskTemplateCreationResult(IReadOnlyDictionary<string, ProjectManagementTaskResponse> Tasks);

public interface IProjectManagementTaskTemplateCommandService
{
    Task<ProjectManagementTaskTemplateCreationResult> CreateTemplateAsync(ProjectManagementTaskTemplateCapability capability, string projectId, IReadOnlyList<ProjectManagementTaskTemplateNodeCreateCommand> nodes, IReadOnlyList<ProjectManagementTaskTemplateDependencyCreateCommand> dependencies, CancellationToken cancellationToken = default);
}
