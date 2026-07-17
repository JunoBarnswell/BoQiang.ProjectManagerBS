using AsterERP.Api.Infrastructure.Abp.ObjectStorage;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.FileManagement;

[DependsOn(typeof(AsterErpObjectStorageModule))]
public sealed class AsterErpFileManagementModule : AbpModule
{
}
