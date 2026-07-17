using System;

namespace AsterERP.Workflow.Tools.Pager
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PermissionAttribute : Attribute
    {
        public string SystemSn { get; set; }
        public string ModuleSn { get; set; }
        public int Value { get; set; }

        public PermissionAttribute(string systemSn, string moduleSn, int value)
        {
            SystemSn = systemSn;
            ModuleSn = moduleSn;
            Value = value;
        }
    }
}
