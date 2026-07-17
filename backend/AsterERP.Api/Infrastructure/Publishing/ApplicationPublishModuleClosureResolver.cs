namespace AsterERP.Api.Infrastructure.Publishing;

public sealed record ApplicationPublishClosureResolution(
    string PublishMode,
    IReadOnlyList<string> ModuleKeys,
    IReadOnlyList<ApplicationPublishClosureEdge> ClosureEdges,
    IReadOnlyList<ApplicationPublishUnresolvedDependency> UnresolvedDependencies);

public sealed class ApplicationPublishModuleClosureResolver
{
    public ApplicationPublishClosureResolution Resolve(
        ApplicationPublishModuleFileMap map,
        string appCode,
        IEnumerable<string> permissionCodes,
        IEnumerable<string> providerKeys)
    {
        if (string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            return new ApplicationPublishClosureResolution(
                "Full",
                map.Modules.Select(module => module.ModuleKey).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                [new ApplicationPublishClosureEdge("appCode", "SYSTEM", "*", "SYSTEM 应用发布走全量模块")],
                []);
        }

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "core.shell", "runtime.core" };
        var edges = new List<ApplicationPublishClosureEdge>
        {
            new("appCode", appCode, "core.shell", "业务应用运行需要登录、会话和基础壳"),
            new("appCode", appCode, "runtime.core", "业务应用菜单运行时页面依赖 Runtime")
        };
        var unresolved = new List<ApplicationPublishUnresolvedDependency>();

        var frontendModuleKey = $"frontend.app.{appCode.Trim().ToLowerInvariant()}";
        if (map.Modules.Any(module => string.Equals(module.ModuleKey, frontendModuleKey, StringComparison.OrdinalIgnoreCase)))
        {
            selected.Add(frontendModuleKey);
            edges.Add(new ApplicationPublishClosureEdge("appCode", appCode, frontendModuleKey, "目标应用前端运行时扩展"));
        }

        foreach (var permissionCode in permissionCodes.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var module = FindByPermissionPrefix(map, permissionCode);
            if (module is null)
            {
                unresolved.Add(new ApplicationPublishUnresolvedDependency("permissionCode", permissionCode, "权限码无法映射到任何发布模块"));
                continue;
            }

            selected.Add(module.ModuleKey);
            edges.Add(new ApplicationPublishClosureEdge("permissionCode", permissionCode, module.ModuleKey, "菜单/PageSchema/DataModel 权限码匹配模块前缀"));
        }

        foreach (var providerKey in providerKeys.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var module = map.Modules.FirstOrDefault(candidate =>
                candidate.ProviderKeys.Any(key => string.Equals(key, providerKey, StringComparison.OrdinalIgnoreCase)));
            if (module is null)
            {
                unresolved.Add(new ApplicationPublishUnresolvedDependency("providerKey", providerKey, "ProviderKey 无法映射到任何发布模块"));
                continue;
            }

            selected.Add(module.ModuleKey);
            edges.Add(new ApplicationPublishClosureEdge("providerKey", providerKey, module.ModuleKey, "DataModel ProviderKey 匹配模块"));
        }

        ExpandDependencies(map, selected, edges);
        return new ApplicationPublishClosureResolution(
            "Trimmed",
            selected.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
            edges,
            unresolved);
    }

    private static ApplicationPublishModuleFileMapEntry? FindByPermissionPrefix(
        ApplicationPublishModuleFileMap map,
        string permissionCode)
    {
        return map.Modules
            .SelectMany(module => module.PermissionPrefixes.Select(prefix => new { Module = module, Prefix = prefix }))
            .Where(item => permissionCode.StartsWith(item.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Prefix.Length)
            .Select(item => item.Module)
            .FirstOrDefault();
    }

    private static void ExpandDependencies(
        ApplicationPublishModuleFileMap map,
        HashSet<string> selected,
        List<ApplicationPublishClosureEdge> edges)
    {
        var byKey = map.Modules.ToDictionary(module => module.ModuleKey, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(selected);
        while (queue.Count > 0)
        {
            var moduleKey = queue.Dequeue();
            if (!byKey.TryGetValue(moduleKey, out var module))
            {
                continue;
            }

            foreach (var dependency in module.DependsOn)
            {
                if (selected.Add(dependency))
                {
                    edges.Add(new ApplicationPublishClosureEdge("moduleDependency", moduleKey, dependency, "模块 DependsOn 闭包"));
                    queue.Enqueue(dependency);
                }
            }
        }
    }
}
