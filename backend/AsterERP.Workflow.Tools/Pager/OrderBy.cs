namespace AsterERP.Workflow.Tools.Pager
{
    public enum OrderBy
    {
        DESC,
        ASC
    }

    public static class OrderByExtensions
    {
        public static OrderBy Reverse(this OrderBy orderBy)
        {
            return orderBy == OrderBy.ASC ? OrderBy.DESC : OrderBy.ASC;
        }
    }
}
