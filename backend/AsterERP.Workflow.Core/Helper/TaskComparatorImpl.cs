using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Helper;

public class TaskComparatorImpl : ITaskComparator
{
    public int Compare(TaskImplementation? x, TaskImplementation? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (x == null)
            return 1;

        if (y == null)
            return -1;

        var priorityComparison = y.Priority.CompareTo(x.Priority);
        if (priorityComparison != 0)
            return priorityComparison;

        var dueDateComparison = Nullable.Compare(x.DueDate, y.DueDate);
        if (dueDateComparison != 0)
            return dueDateComparison;

        var createTimeComparison = Nullable.Compare(x.CreateTime, y.CreateTime);
        if (createTimeComparison != 0)
            return createTimeComparison;

        return string.CompareOrdinal(x.Id, y.Id);
    }
}
