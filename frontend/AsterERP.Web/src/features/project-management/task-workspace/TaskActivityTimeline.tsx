import { Link } from 'react-router-dom';

import type {
  ProjectManagementActivityPage,
  ProjectManagementActivityQuery,
} from '../../../api/project-management/projectManagement.types';
import { normalizeProjectManagementTargetRoute } from '../state/projectManagementPlatformRoutes';

interface TaskActivityTimelineProps {
  canView: boolean;
  isError: boolean;
  isLoading: boolean;
  page?: ProjectManagementActivityPage;
  query: ProjectManagementActivityQuery;
  onQueryChange: (query: ProjectManagementActivityQuery) => void;
}

export function TaskActivityTimeline({ canView, isError, isLoading, page, query, onQueryChange }: TaskActivityTimelineProps) {
  const activities = page?.items ?? [];
  const updateFilter = (next: Partial<ProjectManagementActivityQuery>) => onQueryChange({ ...query, ...next, pageIndex: 1 });
  const pageIndex = query.pageIndex ?? 1;
  const pageSize = query.pageSize ?? 20;
  const total = page?.total ?? 0;

  return (
    <section aria-labelledby="task-activity-title" className="rounded-lg border border-gray-200 p-4">
      <div className="mb-3 flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 className="font-semibold" id="task-activity-title">任务活动时间线</h2>
          <p className="text-xs text-gray-500">评论、人员、状态、进度、标签、依赖、附件和提醒沿用统一活动语义。</p>
        </div>
        {canView ? <span className="text-xs text-gray-500">共 {total} 条</span> : null}
      </div>
      {!canView ? <p className="text-sm text-gray-500">当前账号无查看任务活动的权限。</p> : (
        <>
          <div className="mb-3 grid gap-2 md:grid-cols-4">
            <input aria-label="活动类型筛选" className="rounded border border-gray-200 p-2 text-sm" onChange={(event) => updateFilter({ activityType: event.target.value || undefined })} placeholder="活动类型" value={query.activityType ?? ''} />
            <input aria-label="操作者筛选" className="rounded border border-gray-200 p-2 text-sm" onChange={(event) => updateFilter({ actorUserId: event.target.value || undefined })} placeholder="操作者 UserId" value={query.actorUserId ?? ''} />
            <input aria-label="活动开始日期" className="rounded border border-gray-200 p-2 text-sm" onChange={(event) => updateFilter({ from: event.target.value ? `${event.target.value}T00:00:00Z` : undefined })} type="date" value={query.from?.slice(0, 10) ?? ''} />
            <input aria-label="活动结束日期" className="rounded border border-gray-200 p-2 text-sm" onChange={(event) => updateFilter({ to: event.target.value ? `${event.target.value}T23:59:59Z` : undefined })} type="date" value={query.to?.slice(0, 10) ?? ''} />
          </div>
          {isLoading ? <p className="text-sm text-gray-500">任务活动加载中…</p> : isError ? <p className="text-sm text-amber-700">任务活动加载失败，请重试。</p> : activities.length === 0 ? <p className="text-sm text-gray-500">暂无任务活动。</p> : (
            <ol className="space-y-2">
              {activities.map((activity) => (
                <li className="rounded border border-gray-100 p-3" key={activity.id}>
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <div className="text-sm font-medium">{activity.summary ?? activity.activityType}</div>
                      <div className="text-xs text-gray-500">{activity.activityType} · {activity.actorUserId} · {new Date(activity.createdTime).toLocaleString()}</div>
                    </div>
                    {activity.isTargetDeleted ? <span className="text-xs text-gray-500">关联已删除</span> : activity.targetRoute ? <Link className="text-xs text-blue-600 underline" to={normalizeProjectManagementTargetRoute(activity.targetRoute)}>查看任务详情</Link> : null}
                  </div>
                  {activity.batch ? <details className="mt-2 text-xs text-gray-600"><summary>批量明细（{activity.batch.totalCount} 项，成功 {activity.batch.successCount} 项）</summary><ul className="mt-1 space-y-1 pl-4">{(activity.batch.details ?? []).map((detail, index) => <li key={`${detail.aggregateType}-${detail.aggregateId}-${index}`}>{detail.summary ?? `${detail.aggregateType} ${detail.aggregateId}`}</li>)}</ul></details> : null}
                </li>
              ))}
            </ol>
          )}
          {total > pageSize ? <div className="mt-3 flex items-center justify-between text-xs"><button disabled={pageIndex <= 1} type="button" onClick={() => onQueryChange({ ...query, pageIndex: pageIndex - 1 })}>上一页</button><span>第 {pageIndex} 页</span><button disabled={pageIndex * pageSize >= total} type="button" onClick={() => onQueryChange({ ...query, pageIndex: pageIndex + 1 })}>下一页</button></div> : null}
        </>
      )}
    </section>
  );
}
