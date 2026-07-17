using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiPromptTemplateService(ISqlSugarClient db, AiWorkspaceContext workspaceContext) : IAiPromptTemplateService
{
    public async Task<GridPageResult<AiPromptTemplateDto>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = db.Queryable<AiPromptTemplateEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.TemplateCode.Contains(keyword) || item.TemplateName.Contains(keyword) || item.Category.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.SortOrder)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total);
        return new GridPageResult<AiPromptTemplateDto> { Total = total.Value, Items = rows.Select(Map).ToList() };
    }

    public async Task<IReadOnlyList<AiPromptTemplateDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Queryable<AiPromptTemplateEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<AiPromptTemplateDto> CreateAsync(AiPromptTemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        await ValidateAsync(request, null, cancellationToken);
        var entity = new AiPromptTemplateEntity();
        Apply(entity, request, workspace);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<AiPromptTemplateDto> UpdateAsync(string id, AiPromptTemplateUpsertRequest request, CancellationToken cancellationToken = default)
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

    public async Task<AiPromptTemplateDto> CopyAsync(string id, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var source = await RequireAsync(id, cancellationToken);
        var copy = new AiPromptTemplateEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            TemplateCode = await BuildCopyCodeAsync(source.TemplateCode, cancellationToken),
            TemplateName = $"{source.TemplateName} 副本",
            Category = source.Category,
            SystemPrompt = source.SystemPrompt,
            UserPromptTemplate = source.UserPromptTemplate,
            VariablesJson = source.VariablesJson,
            IsEnabled = false,
            SortOrder = source.SortOrder + 1
        };
        await db.Insertable(copy).ExecuteCommandAsync(cancellationToken);
        return Map(copy);
    }

    public async Task<AiPromptTemplateDto> PublishAsync(string id, AiPromptPublishRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(id, cancellationToken);
        var latestVersion = await db.Queryable<AiPromptVersionEntity>()
            .Where(item => !item.IsDeleted && item.PromptTemplateId == id)
            .MaxAsync(item => item.VersionNo);
        await db.Insertable(new AiPromptVersionEntity
        {
            TenantId = entity.TenantId,
            AppCode = entity.AppCode,
            PromptTemplateId = entity.Id,
            VersionNo = latestVersion + 1,
            SystemPrompt = entity.SystemPrompt,
            UserPromptTemplate = entity.UserPromptTemplate,
            VariablesJson = entity.VariablesJson,
            Status = "Published",
            ChangeSummary = NormalizeOptional(request.ChangeSummary)
        }).ExecuteCommandAsync(cancellationToken);
        entity.IsEnabled = true;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<AiPromptVersionDto>> GetVersionsAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = await RequireAsync(id, cancellationToken);
        var rows = await db.Queryable<AiPromptVersionEntity>()
            .Where(item => !item.IsDeleted && item.PromptTemplateId == id)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new AiPromptVersionDto
        {
            Id = item.Id,
            PromptTemplateId = item.PromptTemplateId,
            VersionNo = item.VersionNo,
            SystemPrompt = item.SystemPrompt,
            UserPromptTemplate = item.UserPromptTemplate,
            VariablesJson = item.VariablesJson,
            Status = item.Status,
            CreatedTime = item.CreatedTime
        }).ToList();
    }

    public async Task<AiPromptTestResponse> TestAsync(AiPromptTestRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await RequireAsync(request.PromptTemplateId, cancellationToken);
        return new AiPromptTestResponse
        {
            RenderedSystemPrompt = RenderTemplate(entity.SystemPrompt, request.Variables),
            RenderedUserPrompt = RenderTemplate(string.IsNullOrWhiteSpace(entity.UserPromptTemplate) ? request.Input : entity.UserPromptTemplate, request.Variables)
        };
    }

    private async Task ValidateAsync(AiPromptTemplateUpsertRequest request, string? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateCode) || string.IsNullOrWhiteSpace(request.TemplateName) || string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            throw new ValidationException("请填写模板编码、名称和系统提示词", ErrorCodes.ParameterInvalid);
        }

        var code = request.TemplateCode.Trim().ToLowerInvariant();
        var exists = await db.Queryable<AiPromptTemplateEntity>()
            .AnyAsync(item => !item.IsDeleted && item.TemplateCode == code && item.Id != existingId, cancellationToken);
        if (exists)
        {
            throw new ValidationException("提示词模板编码已存在", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task<AiPromptTemplateEntity> RequireAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<AiPromptTemplateEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("提示词模板不存在", ErrorCodes.AiPromptTemplateNotFound);

    private async Task<string> BuildCopyCodeAsync(string sourceCode, CancellationToken cancellationToken)
    {
        for (var index = 1; index <= 100; index++)
        {
            var code = $"{sourceCode}-copy{index}";
            var exists = await db.Queryable<AiPromptTemplateEntity>().AnyAsync(item => !item.IsDeleted && item.TemplateCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        throw new ValidationException("无法生成唯一提示词副本编码", ErrorCodes.ParameterInvalid);
    }

    private static string RenderTemplate(string template, IReadOnlyDictionary<string, string> variables)
    {
        var rendered = template;
        foreach (var (key, value) in variables)
        {
            rendered = rendered.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return rendered;
    }

    private static void Apply(AiPromptTemplateEntity entity, AiPromptTemplateUpsertRequest request, AiWorkspace workspace)
    {
        entity.TenantId = workspace.TenantId;
        entity.AppCode = workspace.AppCode;
        entity.TemplateCode = request.TemplateCode.Trim().ToLowerInvariant();
        entity.TemplateName = request.TemplateName.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim();
        entity.SystemPrompt = request.SystemPrompt.Trim();
        entity.UserPromptTemplate = NormalizeOptional(request.UserPromptTemplate);
        entity.VariablesJson = NormalizeOptional(request.VariablesJson);
        entity.IsEnabled = request.IsEnabled;
        entity.SortOrder = request.SortOrder;
    }

    private static AiPromptTemplateDto Map(AiPromptTemplateEntity entity) => new()
    {
        Id = entity.Id,
        TemplateCode = entity.TemplateCode,
        TemplateName = entity.TemplateName,
        Category = entity.Category,
        SystemPrompt = entity.SystemPrompt,
        UserPromptTemplate = entity.UserPromptTemplate,
        VariablesJson = entity.VariablesJson,
        IsEnabled = entity.IsEnabled,
        SortOrder = entity.SortOrder
    };

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
