using System.Collections.Generic;

namespace AsterERP.Workflow.Tools.Pager
{
    public class PagerModel<T>
    {
        public long Total { get; set; }
        public List<T> Rows { get; set; } = new List<T>();

        public PagerModel() { }

        public PagerModel(long total, List<T> rows)
        {
            Total = total;
            Rows = rows;
        }
    }
}
