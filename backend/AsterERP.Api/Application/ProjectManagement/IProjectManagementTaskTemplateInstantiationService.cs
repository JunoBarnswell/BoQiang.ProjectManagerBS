using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 模板实例化的专属编排接缝。实现必须通过任务、标签和依赖聚合服务创建数据，
/// 不能向 pm_tasks、pm_task_labels 或 pm_task_dependencies 直接写入。
/// </summary>
public interface IProjectManagementTaskTemplateInstantiationService
{
    Task<ProjectManagementTaskTemplateInstantiationResponse> InstantiateAsync(
        ProjectManagementTaskTemplateResponse template,
        ProjectManagementTaskTemplateDefinition definition,
        ProjectManagementTaskTemplateInstantiateRequest request,
        CancellationToken cancellationToken = default);
}
