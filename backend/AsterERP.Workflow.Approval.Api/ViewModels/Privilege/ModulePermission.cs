namespace AsterERP.Workflow.Approval.Api.ViewModels.Privilege;

public class ModulePermission
{
    public string ModuleSn { get; set; }
    public string PermissionName { get; set; }
    public int PermissionValue { get; set; }

    public ModulePermission() { }

    public ModulePermission(string moduleSn, string permissionName, int permissionValue)
    {
        ModuleSn = moduleSn;
        PermissionName = permissionName;
        PermissionValue = permissionValue;
    }
}
