using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskVersionConflictException(
    ProjectManagementTaskVersionConflictResponse conflict)
    : BusinessException(ErrorCodes.ApplicationDevelopmentPageRevisionConflict, "任务已被其他用户修改，请刷新后处理冲突")
{
    public ProjectManagementTaskVersionConflictResponse Conflict { get; } = conflict;
}
