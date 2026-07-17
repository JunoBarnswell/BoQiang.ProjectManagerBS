using Medallion.Threading;

namespace AsterERP.Api.Application.Platform.ApplicationPublishing;

public sealed class ApplicationPublishLockRegistry(IDistributedLockProvider lockProvider)
{
    public async Task<IDisposable?> TryAcquireAsync(string appCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appCode))
        {
            throw new ArgumentException("AppCode 不能为空。", nameof(appCode));
        }

        var publishLock = lockProvider.CreateLock($"application-publish:{appCode.Trim().ToUpperInvariant()}");
        return await publishLock.TryAcquireAsync(TimeSpan.Zero, cancellationToken);
    }
}
