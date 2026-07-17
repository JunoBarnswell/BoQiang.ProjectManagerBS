using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AsterERP.Api.Tests.Support;

internal sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "AsterERP.Api.Tests";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
}
