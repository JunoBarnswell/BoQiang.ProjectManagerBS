namespace AsterERP.Contracts.ProjectManagement;

/// <summary>看板状态拖动的最小命令。服务端负责状态机、依赖、WIP、权限和版本原子校验。</summary>
public sealed record ProjectManagementTaskStatusChangeRequest(
    string Status,
    long VersionNo);
