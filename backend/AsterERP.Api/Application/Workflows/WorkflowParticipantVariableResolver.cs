using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowParticipantVariableResolver(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IWorkflowCurrentUserContext currentUserContext)
{
    private const string StarterManagerVariable = "starterManagerUserId";
    private const string StarterDeptManagerVariable = "starterDeptManagerUserId";
    private const string StarterDeptManagerUsersVariable = "starterDeptManagerUserIds";

    public async Task EnrichStartVariablesAsync(
        IDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetCurrentDb();
        var starterUserId = ResolveText(variables, "starterUserId") ?? currentUserContext.UserId;
        if (string.IsNullOrWhiteSpace(starterUserId))
        {
            return;
        }

        var starter = await db.Queryable<SystemUserEntity>()
            .FirstAsync(item => item.Id == starterUserId && !item.IsDeleted, cancellationToken);
        var starterEmployments = await db.Queryable<SystemUserEmploymentEntity>()
            .Where(item => item.UserId == starterUserId && !item.IsDeleted && item.Status == "Enabled")
            .OrderBy(item => item.IsPrimary, OrderByType.Desc)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var starterEmployment = ResolveStarterEmployment(starterEmployments, variables, starterUserId);
        var starterDeptId = ResolveText(variables, "starterDeptId") ?? currentUserContext.DeptId ?? starterEmployment?.DeptId ?? starter?.DeptId;
        var starterPositionId = ResolveText(variables, "starterPositionId") ?? currentUserContext.PositionId ?? starterEmployment?.PositionId ?? starter?.PositionId;

        variables["starterUserId"] = starterUserId;
        variables["starterUserName"] = ResolveText(variables, "starterUserName") ?? starter?.DisplayName ?? starter?.UserName ?? currentUserContext.UserName;
        variables["starterEmploymentId"] = ResolveText(variables, "starterEmploymentId") ?? currentUserContext.EmploymentId ?? starterEmployment?.Id;
        variables["starterDeptId"] = starterDeptId;
        variables["starterPositionId"] = starterPositionId;
        variables["starterDeptIds"] = starterEmployments.Select(item => item.DeptId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        variables["starterPositionIds"] = starterEmployments.Select(item => item.PositionId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        variables["starterRoleIds"] = currentUserContext.RoleIds;

        if (!string.IsNullOrWhiteSpace(starterDeptId))
        {
            var deptManagerUserIds = await ResolveDepartmentManagerUserIdsAsync(db, starterDeptId, cancellationToken);
            if (deptManagerUserIds.Count > 0)
            {
                var deptManagerUserId = deptManagerUserIds[0];
                variables[StarterDeptManagerVariable] = deptManagerUserId;
                variables[StarterDeptManagerUsersVariable] = deptManagerUserIds;
                variables[StarterManagerVariable] = deptManagerUserId;
            }
        }
    }

    public async Task ValidateRequiredVariablesAsync(
        string? processDefinitionId,
        string processDefinitionKey,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        var bpmnXml = await ResolveProcessDefinitionXmlAsync(processDefinitionId, processDefinitionKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            return;
        }

        EnsureRequiredVariable(bpmnXml, variables, StarterManagerVariable, "审批人配置需要发起人上级，但当前发起人未配置可解析的上级");
        EnsureRequiredVariable(bpmnXml, variables, StarterDeptManagerVariable, "审批人配置需要部门负责人，但当前发起人部门未配置可解析的负责人");
        EnsureRequiredVariable(bpmnXml, variables, StarterDeptManagerUsersVariable, "审批人配置需要部门负责人，但当前发起人部门未配置可解析的负责人");
    }

    public async Task EnrichProcessDefinitionVariablesAsync(
        string? processDefinitionId,
        string processDefinitionKey,
        IDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        var bpmnXml = await ResolveProcessDefinitionXmlAsync(processDefinitionId, processDefinitionKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            return;
        }

        foreach (var collection in ResolveParticipantCollections(bpmnXml))
        {
            variables[collection.VariableName] = collection.UserIds;
        }
    }

    private static void EnsureRequiredVariable(
        string bpmnXml,
        IReadOnlyDictionary<string, object?> variables,
        string variableName,
        string errorMessage)
    {
        if (!bpmnXml.Contains($"${{{variableName}}}", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (variables.TryGetValue(variableName, out var value) && HasRequiredVariableValue(value))
        {
            return;
        }

        throw new ValidationException(errorMessage, ErrorCodes.WorkflowActionInvalid);
    }

    private static bool HasRequiredVariableValue(object? value)
    {
        return value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            JsonElement { ValueKind: JsonValueKind.String } element => !string.IsNullOrWhiteSpace(element.GetString()),
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray().Any(HasRequiredJsonElementValue),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => false,
            IEnumerable<string> values => values.Any(item => !string.IsNullOrWhiteSpace(item)),
            global::System.Collections.IEnumerable values => values.Cast<object?>().Any(HasRequiredVariableValue),
            _ => !string.IsNullOrWhiteSpace(Convert.ToString(value))
        };
    }

    private static bool HasRequiredJsonElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            _ => true
        };
    }

    private async Task<IReadOnlyList<string>> ResolveDepartmentManagerUserIdsAsync(
        ISqlSugarClient db,
        string departmentId,
        CancellationToken cancellationToken)
    {
        var department = await db.Queryable<SystemDepartmentEntity>()
            .FirstAsync(item => item.Id == departmentId && !item.IsDeleted && item.Status == "Enabled", cancellationToken);
        var leaderUserIds = DeserializeLeaderUserIds(department?.LeaderUserIdsJson);
        if (leaderUserIds.Count > 0)
        {
            var leaders = await db.Queryable<SystemUserEntity>()
                .Where(item => leaderUserIds.Contains(item.Id) && !item.IsDeleted && item.Status == "Enabled")
                .ToListAsync(cancellationToken);
            var enabledLeaderIds = leaders
                .Select(item => item.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return leaderUserIds
                .Where(enabledLeaderIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var managerName = department?.ManagerName?.Trim();
        if (string.IsNullOrWhiteSpace(managerName))
        {
            return [];
        }

        var manager = await db.Queryable<SystemUserEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.Status == "Enabled" &&
                (item.UserName == managerName || item.DisplayName == managerName),
                cancellationToken);
        return string.IsNullOrWhiteSpace(manager?.Id) ? [] : [manager.Id];
    }

    private SystemUserEmploymentEntity? ResolveStarterEmployment(
        IReadOnlyList<SystemUserEmploymentEntity> employments,
        IDictionary<string, object?> variables,
        string starterUserId)
    {
        var requestedEmploymentId = ResolveText(variables, "starterEmploymentId") ?? currentUserContext.EmploymentId;
        if (!string.IsNullOrWhiteSpace(requestedEmploymentId))
        {
            var requested = employments.FirstOrDefault(item => string.Equals(item.Id, requestedEmploymentId, StringComparison.OrdinalIgnoreCase));
            if (requested is not null)
            {
                return requested;
            }
        }

        if (string.Equals(starterUserId, currentUserContext.UserId, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(currentUserContext.DeptId) &&
            !string.IsNullOrWhiteSpace(currentUserContext.PositionId))
        {
            var current = employments.FirstOrDefault(item =>
                string.Equals(item.DeptId, currentUserContext.DeptId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.PositionId, currentUserContext.PositionId, StringComparison.OrdinalIgnoreCase));
            if (current is not null)
            {
                return current;
            }
        }

        return employments.FirstOrDefault(item => item.IsPrimary) ?? employments.FirstOrDefault();
    }

    private static IReadOnlyList<string> DeserializeLeaderUserIds(string? leaderUserIdsJson)
    {
        if (string.IsNullOrWhiteSpace(leaderUserIdsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(leaderUserIdsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<string?> ResolveProcessDefinitionXmlAsync(
        string? processDefinitionId,
        string processDefinitionKey,
        CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var definition = await db.Queryable<ProcessDefinitionEntity>()
            .WhereIF(!string.IsNullOrWhiteSpace(processDefinitionId), item => item.Id == processDefinitionId)
            .WhereIF(string.IsNullOrWhiteSpace(processDefinitionId), item => item.Key == processDefinitionKey)
            .OrderBy(item => item.Version, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        if (definition is null ||
            string.IsNullOrWhiteSpace(definition.DeploymentId) ||
            string.IsNullOrWhiteSpace(definition.ResourceName))
        {
            return null;
        }

        var resource = await db.Queryable<ResourceEntity>()
            .FirstAsync(item =>
                item.DeploymentId == definition.DeploymentId &&
                item.Name == definition.ResourceName,
                cancellationToken);
        return resource?.Bytes is { Length: > 0 }
            ? Encoding.UTF8.GetString(resource.Bytes)
            : null;
    }

    private static IEnumerable<ParticipantCollection> ResolveParticipantCollections(string bpmnXml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(bpmnXml);
        }
        catch
        {
            yield break;
        }

        foreach (var nodeConfig in document.Descendants().Where(item => item.Name.LocalName == "nodeConfig"))
        {
            ParticipantCollection? collection;
            try
            {
                collection = ResolveParticipantCollection(nodeConfig.Value);
            }
            catch (JsonException)
            {
                collection = null;
            }

            if (collection is not null)
            {
                yield return collection;
            }
        }
    }

    private static ParticipantCollection? ResolveParticipantCollection(string nodeConfigJson)
    {
        using var document = JsonDocument.Parse(nodeConfigJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("participantCollectionVariable", out var variableElement) ||
            variableElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var variableName = variableElement.GetString();
        if (string.IsNullOrWhiteSpace(variableName) ||
            !root.TryGetProperty("participantIds", out var participantIdsElement) ||
            participantIdsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var userIds = participantIdsElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return userIds.Count == 0 ? null : new ParticipantCollection(variableName, userIds);
    }

    private static string? ResolveText(IDictionary<string, object?> variables, string name)
    {
        return variables.TryGetValue(name, out var value) ? Convert.ToString(value) : null;
    }

    private sealed record ParticipantCollection(string VariableName, IReadOnlyList<string> UserIds);
}
