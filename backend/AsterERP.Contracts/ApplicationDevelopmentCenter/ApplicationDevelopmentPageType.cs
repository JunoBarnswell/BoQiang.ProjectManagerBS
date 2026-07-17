namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public static class ApplicationDevelopmentPageTypes
{
    public const string Dialog = "dialog";
    public const string Drawer = "drawer";
    public const string Standard = "standard";

    public static bool IsValid(string? value) =>
        value is Standard or Dialog or Drawer;
}
