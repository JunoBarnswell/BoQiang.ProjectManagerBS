using SqlSugar;

namespace AsterERP.Api.Application.Auth;

public sealed class DisposableApplicationDb(ISqlSugarClient client) : IDisposable
{
    public ISqlSugarClient Client { get; } = client;

    public void Dispose()
    {
        if (Client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
