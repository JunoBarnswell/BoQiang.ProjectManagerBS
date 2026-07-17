using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiAgentProfileService(ISqlSugarClient db, AiWorkspaceContext workspaceContext) : IAiAgentProfileService
{
    public async Task<GridPageResult<AiAgentProfileDto>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = db.Queryable<AiAgentProfileEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.AgentCode.Contains(keyword) || item.AgentName.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.SortOrder)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total);
        return new GridPageResult<AiAgentProfileDto> { Total = total.Value, Items = rows.Select(Map).ToList() };
    }

    public async Task<IReadOnlyList<AiAgentProfileDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Queryable<AiAgentProfileEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<AiAgentProfileDto> CreateAsync(AiAgentProfileUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        await ValidateAsync(request, null, cancellationToken);
        var entity = new AiAgentProfileEntity();
        Apply(entity, request, workspace);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<AiAgentProfileDto> UpdateAsync(string id, AiAgentProfileUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var entity = await RequireAsync(id, cancellationToken);
        await ValidateAsync(request, id, cancellationToken);
        Apply(entity, request, workspace);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<AiAgentProfileDto> CopyAsync(string id, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var source = await RequireAsync(id, cancellationToken);
        var copy = new AiAgentProfileEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            AgentCode = await BuildCopyCodeAsync(source.AgentCode, cancellationToken),
            AgentName = $"{source.AgentName} 副本",
            RolePrompt = source.RolePrompt,
            ModelConfigId = source.ModelConfigId,
            PromptTemplateId = source.PromptTemplateId,
            AllowedFunctionsJson = source.AllowedFunctionsJson,
            IsCoordinator = false,
            IsEnabled = false,
            SortOrder = source.SortOrder + 1
        };
        await db.Insertable(copy).ExecuteCommandAsync(cancellationToken);
        return Map(copy);
    }

    public async Task<AiAgentProfileDto> SetStatusAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(id, cancellationToken);
        entity.IsEnabled = enabled;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<bool> TestAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(entity.ModelConfigId))
        {
            var modelExists = await db.Queryable<AiModelConfigEntity>()
                .AnyAsync(item => !item.IsDeleted && item.IsEnabled && item.Id == entity.ModelConfigId, cancellationToken);
            if (!modelExists)
            {
                throw new ValidationException("智能体默认模型不存在或未启用", ErrorCodes.AiModelNotFound);
            }
        }

        if (!string.IsNullOrWhiteSpace(entity.PromptTemplateId))
        {
            var promptExists = await db.Queryable<AiPromptTemplateEntity>()
                .AnyAsync(item => !item.IsDeleted && item.IsEnabled && item.Id == entity.PromptTemplateId, cancellationToken);
            if (!promptExists)
            {
                throw new ValidationException("智能体默认提示词不存在或未启用", ErrorCodes.AiPromptTemplateNotFound);
            }
        }

        return true;
    }

    private async Task ValidateAsync(AiAgentProfileUpsertRequest request, string? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AgentCode) || string.IsNullOrWhiteSpace(request.AgentName) || string.IsNullOrWhiteSpace(request.RolePrompt))
        {
            throw new ValidationException("请填写智能体编码、名称和角色提示词", ErrorCodes.ParameterInvalid);
        }

        var code = request.AgentCode.Trim().ToLowerInvariant();
        var exists = await db.Queryable<AiAgentProfileEntity>()
            .AnyAsync(item => !item.IsDeleted && item.AgentCode == code && item.Id != existingId, cancellationToken);
        if (exists)
        {
            throw new ValidationException("智能体编码已存在", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task<AiAgentProfileEntity> RequireAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<AiAgentProfileEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("智能体配置不存在", ErrorCodes.AiAgentProfileNotFound);

    private async Task<string> BuildCopyCodeAsync(string sourceCode, CancellationToken cancellationToken)
    {
        for (var index = 1; index <= 100; index++)
        {
            var code = $"{sourceCode}-copy{index}";
            var exists = await db.Queryable<AiAgentProfileEntity>().AnyAsync(item => !item.IsDeleted && item.AgentCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        throw new ValidationException("无法生成唯一智能体副本编码", ErrorCodes.ParameterInvalid);
    }

    private static void Apply(AiAgentProfileEntity entity, AiAgentProfileUpsertRequest request, AiWorkspace workspace)
    {
        entity.TenantId = workspace.TenantId;
        entity.AppCode = workspace.AppCode;
        entity.AgentCode = request.AgentCode.Trim().ToLowerInvariant();
        entity.AgentName = request.AgentName.Trim();
        entity.RolePrompt = request.RolePrompt.Trim();
        entity.ModelConfigId = NormalizeOptional(request.ModelConfigId);
        entity.PromptTemplateId = NormalizeOptional(request.PromptTemplateId);
        entity.AllowedFunctionsJson = NormalizeOptional(request.AllowedFunctionsJson);
        entity.IsCoordinator = request.IsCoordinator;
        entity.IsEnabled = request.IsEnabled;
        entity.SortOrder = request.SortOrder;
    }

    private static AiAgentProfileDto Map(AiAgentProfileEntity entity) => new()
    {
        Id = entity.Id,
        AgentCode = entity.AgentCode,
        AgentName = entity.AgentName,
        RolePrompt = entity.RolePrompt,
        ModelConfigId = entity.ModelConfigId,
        PromptTemplateId = entity.PromptTemplateId,
        AllowedFunctionsJson = entity.AllowedFunctionsJson,
        IsCoordinator = entity.IsCoordinator,
        IsEnabled = entity.IsEnabled,
        SortOrder = entity.SortOrder
    };

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
