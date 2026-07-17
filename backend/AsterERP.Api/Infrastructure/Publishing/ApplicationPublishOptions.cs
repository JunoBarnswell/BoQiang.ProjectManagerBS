namespace AsterERP.Api.Infrastructure.Publishing;

public sealed class ApplicationPublishOptions
{
    public string OutputRoot { get; set; } = "../../output/publish-apps";

    public string RuntimeIdentifier { get; set; } = "win-x64";

    public bool SelfContained { get; set; } = true;

    public int KeepSuccessfulCount { get; set; } = 5;
}
