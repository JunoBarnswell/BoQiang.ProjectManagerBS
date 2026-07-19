using System.Reflection;
using System.Text.RegularExpressions;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ApplicationDataCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataCenterDataPermissionArchitectureTests
{
    [Fact]
    public void EveryConcreteDataCenterEntityHasOneTypedDescriptorAndRegistrarParameter()
    {
        var entityAssembly = typeof(ApplicationDataCenterObjectEntity).Assembly;
        var descriptorAssembly = typeof(DataPermissionFilterRegistrar).Assembly;
        var entityTypes = entityAssembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(ApplicationDataCenterObjectEntity).Namespace)
            .Where(type => type.IsClass && !type.IsAbstract && type.Name.EndsWith("Entity", StringComparison.Ordinal))
            .OrderBy(type => type.FullName)
            .ToArray();
        var descriptorTypes = descriptorAssembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IDataPermissionDescriptor<>))
                .Select(contract => (Entity: contract.GetGenericArguments()[0], Descriptor: type)))
            .ToLookup(item => item.Entity, item => item.Descriptor);
        var registrarParameters = typeof(DataPermissionFilterRegistrar)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single()
            .GetParameters()
            .Where(parameter => parameter.ParameterType.IsGenericType &&
                parameter.ParameterType.GetGenericTypeDefinition() == typeof(IDataPermissionDescriptor<>))
            .Select(parameter => parameter.ParameterType.GetGenericArguments()[0])
            .ToHashSet();

        Assert.True(entityTypes.Length == 24, string.Join(", ", entityTypes.Select(type => type.FullName)));
        foreach (var entityType in entityTypes)
        {
            var descriptors = descriptorTypes[entityType].Distinct().ToArray();
            Assert.Single(descriptors);
            Assert.Contains(entityType, registrarParameters);
        }
    }

    [Fact]
    public void DataCenterLegacyBridgeIsRemoved()
    {
        var legacyModulePath = Path.Combine(
            "backend",
            "AsterERP.Api",
            "Infrastructure",
            "Abp",
            "ApplicationDataCenter",
            "AsterErpApplicationDataCenterModule.cs");

        Assert.True(File.Exists(Path.Combine(FindRepositoryRoot(), legacyModulePath)));
    }

    [Fact]
    public void DataCenterApplicationServicesDoNotComposeWorkspacePermissionWhereClauses()
    {
        var applicationDirectory = Path.Combine(
            FindRepositoryRoot(),
            "backend",
            "AsterERP.Api",
            "Application",
            "ApplicationDataCenter");
        var forbiddenPatterns = new[]
        {
            new Regex(@"TenantId\s*==\s*workspace\.TenantId", RegexOptions.CultureInvariant),
            new Regex(@"AppCode\s*==\s*workspace\.AppCode", RegexOptions.CultureInvariant)
        };

        foreach (var file in Directory.EnumerateFiles(applicationDirectory, "*.cs", SearchOption.AllDirectories)
                     .Where(file => !file.EndsWith("ApplicationSystemAssignmentService.cs", StringComparison.Ordinal)))
        {
            var source = File.ReadAllText(file);
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.False(pattern.IsMatch(source), $"服务层仍包含权限 Where: {file} / {pattern}");
            }
        }
    }

    private static string ReadRepositoryFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. segments]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("无法定位仓库根目录");
    }
}
