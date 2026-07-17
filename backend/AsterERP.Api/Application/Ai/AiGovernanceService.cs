using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiGovernanceService(ISqlSugarClient db, AiWorkspaceContext workspaceContext)
{
    public async Task<AiSecuritySettingsDto> GetSecuritySettingsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Queryable<AiSecurityPolicyEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled)
            .ToListAsync(cancellationToken);
        return MapSettings(rows);
    }

    public async Task<AiSecuritySettingsDto> UpdateSecuritySettingsAsync(AiSecuritySettingsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RequireToolConfirmation"] = request.RequireToolConfirmation.ToString().ToLowerInvariant(),
            ["MaxParallelAgents"] = Math.Clamp(request.MaxParallelAgents, 1, 8).ToString(),
            ["MaxInputCharacters"] = Math.Clamp(request.MaxInputCharacters, 1000, 200000).ToString(),
            ["MaxContextMessages"] = Math.Clamp(request.MaxContextMessages, 4, 200).ToString(),
            ["AllowReasoningDisplay"] = request.AllowReasoningDisplay.ToString().ToLowerInvariant(),
            ["MultiAgentFailurePolicy"] = string.IsNullOrWhiteSpace(request.MultiAgentFailurePolicy) ? "SkipFailed" : request.MultiAgentFailurePolicy.Trim()
        };

        foreach (var (key, value) in values)
        {
            var existing = await db.Queryable<AiSecurityPolicyEntity>()
                .FirstAsync(item => item.PolicyKey == key && !item.IsDeleted, cancellationToken);
            if (existing is null)
            {
                await db.Insertable(new AiSecurityPolicyEntity
                {
                    TenantId = workspace.TenantId,
                    AppCode = workspace.AppCode,
                    PolicyKey = key,
                    PolicyValue = value,
                    IsEnabled = true
                }).ExecuteCommandAsync(cancellationToken);
                continue;
            }

            existing.PolicyValue = value;
            existing.IsEnabled = true;
            existing.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }

        return await GetSecuritySettingsAsync(cancellationToken);
    }

    public static AiSecuritySettingsDto MapSettings(IReadOnlyCollection<AiSecurityPolicyEntity> policies)
    {
        var values = policies.ToDictionary(item => item.PolicyKey, item => item.PolicyValue, StringComparer.OrdinalIgnoreCase);
        return new AiSecuritySettingsDto
        {
            RequireToolConfirmation = ReadBool(values, "RequireToolConfirmation", true),
            MaxParallelAgents = ReadInt(values, "MaxParallelAgents", 3),
            MaxInputCharacters = ReadInt(values, "MaxInputCharacters", 16000),
            MaxContextMessages = ReadInt(values, "MaxContextMessages", 40),
            AllowReasoningDisplay = ReadBool(values, "AllowReasoningDisplay", true),
            MultiAgentFailurePolicy = values.TryGetValue("MultiAgentFailurePolicy", out var policy) ? policy : "SkipFailed"
        };
    }

    private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var result) ? result : fallback;

    private static int ReadInt(Dictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var result) ? result : fallback;
}
