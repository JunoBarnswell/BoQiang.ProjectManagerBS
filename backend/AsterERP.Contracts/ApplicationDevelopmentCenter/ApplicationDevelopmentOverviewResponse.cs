namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentOverviewResponse
{
    public int DraftPageCount { get; set; }

    public int DraftVersionCount { get; set; }

    public int PreviewMenuCount { get; set; }

    public int PublishedPageCount { get; set; }

    public int PublishedVersionCount { get; set; }

    public int TotalModuleCount { get; set; }
}
