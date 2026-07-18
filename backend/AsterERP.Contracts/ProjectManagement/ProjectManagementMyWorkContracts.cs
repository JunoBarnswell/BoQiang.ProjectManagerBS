namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementMyWorkQuery(
    int PageIndex = 1,
    int PageSize = 50,
    string? ProjectId = null,
    string? Category = null,
    string SortBy = "dueDate",
    string SortDirection = "asc",
    bool IncludeCompleted = false);

public sealed record ProjectManagementMyWorkItem(
    ProjectManagementTaskResponse Task,
    string ProjectName,
    bool IsAssignee,
    bool IsParticipant,
    bool IsCreator,
    bool IsMentioned);
