namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>阻止反向重放再次登记为用户新命令，从而错误清空 redo 栈。</summary>
internal static class ProjectManagementReversibleCommandReplayScope
{
    private static readonly AsyncLocal<int> Depth = new();
    public static bool IsActive => Depth.Value > 0;

    public static IDisposable Enter()
    {
        Depth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Depth.Value = Math.Max(0, Depth.Value - 1);
    }
}
