import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';

import {
  getProjectManagementActivities,
  getProjectManagementOverview
} from '../../api/project-management/projectManagement.api';
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { projectManagementQueryKeys } from '../../core/query/projectManagementQueryKeys';
import '../../features/project-management/projectManagement.css';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';

export function ProjectManagementOverviewPage() {
  const scope = useProjectManagementWorkspaceScope();
  const { hasPermission: canViewActivities } = usePermission('project-management:audit:view');
  const { projectId = '' } = useParams<{ projectId: string }>();
  const overviewQuery = useQuery({
    queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }),
    queryFn: ({ signal }) => getProjectManagementOverview({ projectId, pageIndex: 1, pageSize: 1 }, signal),
    enabled: scope.isAvailable && Boolean(projectId)
  });
  const overview = overviewQuery.data?.data?.items[0];
  const project = overview?.project;
  const activitiesQuery = useQuery({
    queryKey: projectManagementQueryKeys.activities(scope, projectId, 20),
    queryFn: ({ signal }) => getProjectManagementActivities(projectId, 20, signal),
    enabled: scope.isAvailable && Boolean(overview) && canViewActivities
  });

  if (overviewQuery.isLoading) return <PageLoading />;
  if (overviewQuery.isError) {
    if (isHttpError(overviewQuery.error) && overviewQuery.error.status === 403) return <Page403 />;
    return <PageError description="项目概览加载失败，请检查网络或权限后重试。" action={<button type="button" onClick={() => void overviewQuery.refetch()}>重试</button>} />;
  }
  if (!overview || !project) return <PageError description="项目不存在或当前账号无权访问。" />;

  const metrics = [
    { label: '整体进度', value: `${overview.taskProgressPercent}%`, tone: 'accent' },
    { label: '任务总数', value: overview.taskCount, tone: 'neutral' },
    { label: '逾期任务', value: overview.overdueTaskCount, tone: overview.overdueTaskCount > 0 ? 'danger' : 'neutral' },
    { label: '阻塞任务', value: overview.blockedTaskCount, tone: overview.blockedTaskCount > 0 ? 'warning' : 'neutral' },
    { label: '项目状态', value: projectStatusLabel(project.status), tone: project.status === 'Active' ? 'accent' : 'neutral' }
  ];

  return (
    <ResponsivePage
      className="pm-page"
      description={project.description ?? '查看任务推进、风险、里程碑健康与近期协作活动。'}
      eyebrow="ProjectManagement / Overview"
      title={project.projectName}
      toolbar={<div className="pm-toolbar-summary"><span>{project.projectCode}</span><ProjectStatus status={project.status} /><span>负责人：{project.ownerUserId || '未分配'}</span></div>}
    >
      <section aria-label="项目指标" className="pm-metric-grid">
        {metrics.map((metric) => <div className="pm-metric-card" data-tone={metric.tone} key={metric.label}><span className="pm-metric-card__label">{metric.label}</span><strong className="pm-metric-card__value">{metric.value}</strong></div>)}
      </section>
      <section className="pm-panel" aria-labelledby="milestone-health-title">
        <div className="pm-panel__heading"><div><h2 id="milestone-health-title">里程碑健康</h2><p className="pm-panel__meta">里程碑进度和健康状态由当前项目数据计算。</p></div><span className="pm-view-note">共 {overview.milestoneCount} 个</span></div>
        {overview.milestones.length === 0 ? <p className="pm-muted">暂无里程碑。创建里程碑后会在此显示健康状态和进度。</p> : <ul className="pm-list">{overview.milestones.map((milestone) => <li key={milestone.id}><div className="pm-milestone-row"><div><div className="pm-milestone-row__name">{milestone.name}</div><div className="pm-milestone-row__meta"><HealthBadge health={milestone.healthStatus} /><span>{milestone.dueDate ? `截止 ${formatDate(milestone.dueDate)}` : '未设置截止日期'}</span></div></div><div className="pm-progress"><progress aria-label={`${milestone.name} 完成进度 ${milestone.progressPercent}%`} max={100} value={milestone.progressPercent} /><span>{milestone.progressPercent}%</span></div></div></li>)}</ul>}
      </section>
      <section className="pm-panel" aria-labelledby="project-activity-title">
        <div className="pm-panel__heading"><div><h2 id="project-activity-title">最近活动</h2><p className="pm-panel__meta">仅展示当前项目范围内最近 20 条活动。</p></div></div>
        {!canViewActivities ? (
          <p className="pm-muted">当前账号无查看项目活动的权限。</p>
        ) : activitiesQuery.isError ? (
          <p className="pm-muted">项目活动暂时无法加载。</p>
        ) : (activitiesQuery.data?.data ?? []).length === 0 ? (
          <p className="pm-muted">暂无活动。项目和任务发生可审计变更后会在这里显示。</p>
        ) : (
          <ul className="pm-list">{(activitiesQuery.data?.data ?? []).map((item) => <li key={item.id}><div className="pm-activity-row"><div className="pm-activity-row__summary">{item.summary ?? item.activityType}</div><time className="pm-activity-row__meta" dateTime={item.createdTime}>{new Date(item.createdTime).toLocaleString()}</time></div></li>)}</ul>
        )}
      </section>
    </ResponsivePage>
  );
}

function ProjectStatus({ status }: { status: string }) {
  const tone = status === 'Active' ? 'in-progress' : status === 'Completed' ? 'done' : status === 'Paused' ? 'blocked' : status === 'Canceled' || status === 'Archived' ? 'cancelled' : 'todo';
  return <span className={`pm-status-badge pm-status-badge--${tone}`}>{projectStatusLabel(status)}</span>;
}

function HealthBadge({ health }: { health: string }) {
  const tone = health === 'OnTrack' ? 'on-track' : health === 'Done' ? 'done' : health === 'AtRisk' ? 'at-risk' : 'off-track';
  const label = ({ OnTrack: '正常', AtRisk: '有风险', OffTrack: '已偏离', Done: '已完成' } as Record<string, string>)[health] ?? health;
  return <span className={`pm-status-badge pm-status-badge--${tone}`}>{label}</span>;
}

function projectStatusLabel(status: string) {
  return ({ Planning: '规划中', Active: '进行中', Paused: '已暂停', Completed: '已完成', Canceled: '已取消', Archived: '已归档' } as Record<string, string>)[status] ?? status;
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString();
}
