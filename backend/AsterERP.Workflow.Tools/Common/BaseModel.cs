using System;

namespace AsterERP.Workflow.Tools.Common
{
    public abstract class BaseModel
    {
        public DateTime? CreateTime { get; set; }
        public string Creator { get; set; }
        public DateTime? UpdateTime { get; set; }
        public string Updator { get; set; }
        public int? DelFlag { get; set; } = 1;
        public string Keyword { get; set; }
    }
}
