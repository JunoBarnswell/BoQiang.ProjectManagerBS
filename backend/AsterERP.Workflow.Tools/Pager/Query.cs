using System.Collections.Generic;

namespace AsterERP.Workflow.Tools.Pager
{
    public class Query
    {
        public int PageSize { get; set; } = 20;
        public int PageNum { get; set; }
        public Dictionary<string, OrderBy> SqlOrderBy { get; set; }
        public string SortField { get; set; }
        public string SortOrder { get; set; }

        public int GetPageNum()
        {
            if (PageNum <= 0)
            {
                PageNum = 1;
            }
            return PageNum;
        }
    }
}
