using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.ObjectStorage;

[DependsOn(
    typeof(AbpBlobStoringModule),
    typeof(AbpBlobStoringFileSystemModule),
    typeof(AbpBlobStoringMinioModule))]
public sealed class AsterErpObjectStorageModule : AbpModule
{
}
