import type { ProjectManagementActivityPage } from '../../api/project-management/projectManagement.types';

interface ProjectManagementActivityTimelineProps {
  canView: boolean;
  isError: boolean;
  isLoading: boolean;
  page?: ProjectManagementActivityPage;
  pageSize: number;
}

export function ProjectManagementActivityTimeline({ canView, isError, isLoading, page, pageSize }: ProjectManagementActivityTimelineProps) {
  const activities = page?.items ?? [];

  return (
    <section className="pm-panel" aria-labelledby="project-activity-title">
      <div className="pm-panel__heading"><div><h2 id="project-activity-title">最近活动</h2><p className="pm-panel__meta">仅展示当前项目范围内最近 {pageSize} 条活动。</p></div></div>
      {!canView ? (
        <p className="pm-muted">当前账号无查看项目活动的权限。</p>
      ) : isLoading ? (
        <p className="pm-muted">项目活动加载中…</p>
      ) : isError ? (
        <p className="pm-muted">项目活动暂时无法加载。</p>
      ) : activities.length === 0 ? (
        <p className="pm-muted">暂无活动。项目和任务发生可审计变更后会在这里显示。</p>
      ) : (
        <ul className="pm-list">{activities.map((item) => <li key={item.id}><div className="pm-activity-row"><div className="pm-activity-row__summary">{item.summary ?? item.activityType}</div><time className="pm-activity-row__meta" dateTime={item.createdTime}>{new Date(item.createdTime).toLocaleString()}</time></div></li>)}</ul>
      )}
    </section>
  );
}
