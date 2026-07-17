namespace AsterERP.Api.Application.Runtime;

public interface IDataModelProviderRegistry
{
    IDataModelProvider GetRequired(string providerKey);
}
