using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementRiskConfirmationService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IPasswordHashService passwordHashService) : IProjectManagementRiskConfirmationService
{
    public async Task EnsureConfirmedAsync(string currentPassword, bool confirmRisk, CancellationToken cancellationToken = default)
    {
        if (!confirmRisk) throw new ValidationException("必须确认高风险数据操作");
        var userId = currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
        var user = (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => item.Id == userId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new ValidationException("当前用户不存在");
        if (!passwordHashService.Verify(user.PasswordHash, currentPassword).Success) throw new ValidationException("当前密码不正确");
    }
}
