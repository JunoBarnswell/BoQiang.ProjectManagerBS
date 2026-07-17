namespace AsterERP.Shared;

public static partial class PermissionCodes
{
    public const string AppHomeView = "app:home:view";
    public const string AppConsoleView = "app:console:view";
    public const string AppWorkbenchView = "app:workbench:view";
    public const string AppApplicationCenterView = "app:application-center:view";
    public const string AppDevelopmentCenterView = "app:development-center:view";
    public const string AppDataCenterView = "app:data-center:view";

    public static readonly IReadOnlyList<string> AppConsolePermissionCodes =
    [
        AppHomeView,
        AppConsoleView,
        AppWorkbenchView,
        AppApplicationCenterView,
        AppDevelopmentCenterView,
        AppDevelopmentCenterBusinessObjectView,
        AppDevelopmentCenterDesignerView,
        AppDevelopmentCenterDesignerEdit,
        AppDevelopmentCenterDesignerPreview,
        AppDevelopmentCenterDesignerPublish,
        AppDevelopmentCenterDesignerPermissionEdit,
        AppDevelopmentCenterMonitoringWrite,
        AppDataCenterView
    ];
}
