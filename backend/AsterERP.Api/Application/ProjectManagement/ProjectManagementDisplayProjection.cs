namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementDisplayProjection
{
    private readonly IReadOnlyDictionary<string, string> projects;
    private readonly IReadOnlyDictionary<string, string> aggregates;
    private readonly IReadOnlyDictionary<string, string> users;

    public ProjectManagementDisplayProjection(
        IReadOnlyDictionary<string, string> projects,
        IReadOnlyDictionary<string, string> aggregates,
        IReadOnlyDictionary<string, string> users)
    {
        this.projects = projects;
        this.aggregates = aggregates;
        this.users = users;
    }

    public string Project(string? id) => Resolve(projects, id, "项目已删除或无权查看");
    public string Aggregate(string aggregateType, string? id) => Resolve(aggregates, Key(aggregateType, id), "对象已删除或无权查看");
    public string? User(string? id) => string.IsNullOrWhiteSpace(id) ? null : Resolve(users, id, "用户别名暂不可用");
    public static string Key(string aggregateType, string? id) => $"{aggregateType}\u001f{id}";

    private static string Resolve(IReadOnlyDictionary<string, string> values, string? id, string unavailable) =>
        !string.IsNullOrWhiteSpace(id) && values.TryGetValue(id, out var displayName) ? displayName : unavailable;
}
