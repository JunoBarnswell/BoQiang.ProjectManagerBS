using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseMcpServerService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowisePermissionGuard permissionGuard) : IFlowiseMcpServerService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex ToolNameRegex = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    public async Task<FlowiseMcpServerConfigDto> GetAsync(string chatflowId, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatflowAsync(chatflowId, cancellationToken);
        EnsureView(chatflow.Type);
        return Map(chatflow, ReadConfig(chatflow.McpServerConfig));
    }

    public async Task<FlowiseMcpServerConfigDto> CreateAsync(string chatflowId, FlowiseMcpServerUpsertRequest request, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatflowAsync(chatflowId, cancellationToken);
        EnsureEdit(chatflow.Type);
        var config = BuildEnabledConfig(request, ReadConfig(chatflow.McpServerConfig).Token);
        if (string.IsNullOrWhiteSpace(config.Token))
        {
            config.Token = GenerateToken();
        }

        return await SaveAsync(chatflow, config, "mcp-server.created", cancellationToken);
    }

    public async Task<FlowiseMcpServerConfigDto> UpdateAsync(string chatflowId, FlowiseMcpServerUpsertRequest request, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatflowAsync(chatflowId, cancellationToken);
        EnsureEdit(chatflow.Type);
        var current = ReadConfig(chatflow.McpServerConfig);
        var config = request.Enabled
            ? BuildEnabledConfig(request, string.IsNullOrWhiteSpace(current.Token) ? GenerateToken() : current.Token)
            : new FlowiseMcpServerConfigDocument
            {
                Description = NormalizeOptional(request.Description) ?? current.Description,
                Enabled = false,
                Token = current.Token,
                ToolName = NormalizeOptional(request.ToolName) ?? current.ToolName
            };

        return await SaveAsync(chatflow, config, "mcp-server.updated", cancellationToken);
    }

    public async Task<FlowiseMcpServerConfigDto> DisableAsync(string chatflowId, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatflowAsync(chatflowId, cancellationToken);
        EnsureEdit(chatflow.Type);
        var current = ReadConfig(chatflow.McpServerConfig);
        current.Enabled = false;
        return await SaveAsync(chatflow, current, "mcp-server.disabled", cancellationToken);
    }

    public async Task<FlowiseMcpServerConfigDto> RefreshTokenAsync(string chatflowId, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatflowAsync(chatflowId, cancellationToken);
        EnsureEdit(chatflow.Type);
        var current = ReadConfig(chatflow.McpServerConfig);
        if (!current.Enabled || string.IsNullOrWhiteSpace(current.ToolName) || string.IsNullOrWhiteSpace(current.Description))
        {
            throw new ValidationException("MCP Server 启用后才能轮换 Token", ErrorCodes.ParameterInvalid);
        }

        current.Token = GenerateToken();
        return await SaveAsync(chatflow, current, "mcp-server.token-refreshed", cancellationToken);
    }

    private async Task<FlowiseMcpServerConfigDto> SaveAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseMcpServerConfigDocument config,
        string eventType,
        CancellationToken cancellationToken)
    {
        chatflow.McpServerConfig = JsonSerializer.Serialize(config, JsonOptions);
        chatflow.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(chatflow).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync(eventType, chatflow, cancellationToken);
        return Map(chatflow, config);
    }

    private async Task<FlowiseChatFlowEntity> LoadChatflowAsync(string chatflowId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chatflowId))
        {
            throw new ValidationException("缺少 Flowise ChatFlow Id", ErrorCodes.ParameterInvalid);
        }

        return await db.Queryable<FlowiseChatFlowEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == chatflowId.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise ChatFlow 不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task WriteAuditAsync(string eventType, FlowiseChatFlowEntity chatflow, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        await db.Insertable(new FlowiseAuditLogEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            EventType = eventType,
            ResourceType = chatflow.Type,
            ResourceId = chatflow.Id,
            DetailJson = JsonSerializer.Serialize(new { chatflow.Name, chatflow.Type })
        }).ExecuteCommandAsync(cancellationToken);
    }

    private void EnsureView(string type)
    {
        if (type == FlowiseChatflowTypes.Agentflow)
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
    }

    private void EnsureEdit(string type)
    {
        if (type == FlowiseChatflowTypes.Agentflow)
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
    }

    private static FlowiseMcpServerConfigDocument BuildEnabledConfig(FlowiseMcpServerUpsertRequest request, string token)
    {
        var toolName = NormalizeToolName(request.ToolName);
        var description = NormalizeDescription(request.Description);
        return new FlowiseMcpServerConfigDocument
        {
            Description = description,
            Enabled = true,
            Token = token,
            ToolName = toolName
        };
    }

    private static FlowiseMcpServerConfigDocument ReadConfig(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new FlowiseMcpServerConfigDocument();
        }

        try
        {
            return JsonSerializer.Deserialize<FlowiseMcpServerConfigDocument>(value, JsonOptions) ?? new FlowiseMcpServerConfigDocument();
        }
        catch
        {
            return new FlowiseMcpServerConfigDocument();
        }
    }

    private static FlowiseMcpServerConfigDto Map(FlowiseChatFlowEntity chatflow, FlowiseMcpServerConfigDocument config) => new()
    {
        ChatflowId = chatflow.Id,
        Description = config.Description,
        Enabled = config.Enabled,
        EndpointPath = $"/api/v1/mcp/{chatflow.Id}",
        HasExistingConfig = !string.IsNullOrWhiteSpace(config.Token),
        Token = config.Token,
        ToolName = config.ToolName
    };

    private static string NormalizeToolName(string value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            throw new ValidationException("MCP Server toolName 不能为空", ErrorCodes.ParameterInvalid);
        }

        if (normalized.Length > 64 || !ToolNameRegex.IsMatch(normalized))
        {
            throw new ValidationException("MCP Server toolName 只能包含字母、数字、下划线和短横线，且最多 64 个字符", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string NormalizeDescription(string value)
    {
        var normalized = NormalizeOptional(value);
        return normalized ?? throw new ValidationException("MCP Server description 不能为空", ErrorCodes.ParameterInvalid);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
