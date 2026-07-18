using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementProjectVersionConflictException(
    ProjectManagementProjectVersionConflictResponse conflict)
    : BusinessException(ErrorCodes.ApplicationDevelopmentPageRevisionConflict, "项目已被其他用户修改，请刷新后处理冲突")
{
    public ProjectManagementProjectVersionConflictResponse Conflict { get; } = conflict;
}
