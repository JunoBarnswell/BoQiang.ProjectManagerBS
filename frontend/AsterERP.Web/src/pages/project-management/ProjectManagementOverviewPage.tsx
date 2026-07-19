import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';

import { getProjectManagementActivities, getProjectManagementMemberCandidates } from '../../api/project-management/projectManagement.api';
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { projectManagementQueryKeys } from '../../core/query/projectManagementQueryKeys';
import '../../features/project-management/projectManagement.css';
import {
  getProjectManagementDashboardOverview,
  getProjectManagementDashboardWorkload
} from '../../features/project-management/dashboard/projectManagementDashboard.api';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';

import { ProjectManagementActivityTimeline } from './ProjectManagementActivityTimeline';
import { ProjectWorkloadTable } from './components/ProjectWorkloadTable';

const activityQuery = { pageIndex: 1, pageSize: 20 };

export function ProjectManagementOverviewPage() {
  const scope = useProjectManagementWorkspaceScope();
  const { hasPermission: canViewActivities } = usePermission('project-management:audit:view');
  const { hasPermission: canViewTasks } = usePermission('project-management:task:view');
  const { projectId = '' } = useParams<{ projectId: string }>();
  const overviewQuery = useQuery({
    queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }),
    queryFn: ({ signal }) => getProjectManagementDashboardOverview({ projectId, pageIndex: 1, pageSize: 1 }, signal),
    enabled: scope.isAvailable && Boolean(projectId)
  });
  const overview = overviewQuery.data?.data?.items[0];
  const project = overview?.project;
  const workloadQuery = useQuery({
    queryKey: [...projectManagementQueryKeys.overview(scope, { projectId }), 'workload'],
    queryFn: ({ signal }) => getProjectManagementDashboardWorkload(projectId, signal),
    enabled: scope.isAvailable && Boolean(overview) && canViewTasks
  });
  const workloadPeopleQuery = useQuery({
    queryKey: projectManagementQueryKeys.memberCandidates(scope, { pageIndex: 1, pageSize: 100, projectId }),
    queryFn: ({ signal }) => getProjectManagementMemberCandidates({ pageIndex: 1, pageSize: 100, projectId }, signal),
    enabled: scope.isAvailable && Boolean(overview) && canViewTasks,
  });
  const activitiesQuery = useQuery({
    queryKey: projectManagementQueryKeys.activities(scope, projectId, activityQuery),
    queryFn: ({ signal }) => getProjectManagementActivities(projectId, activityQuery, signal),
    enabled: scope.isAvailable && Boolean(overview) && canViewActivities
  });

  if (overviewQuery.isLoading) return <PageLoading />;
  if (overviewQuery.isError) {
    if (isHttpError(overviewQuery.error) && overviewQuery.error.status === 403) return <Page403 />;
    return <PageError description="项目概览加载失败，请检查网络或权限后重试。" action={<button type="button" onClick={() => void overviewQuery.refetch()}>重试</button>} />;
  }
  if (!overview || !project) return <PageError description="项目不存在或当前账号无权访问。" />;

  const riskSummary = overview.riskSummary ?? {
    blockedTaskCount: overview.blockedTaskCount,
    dueSoonIncompleteTaskCount: 0,
    hasScheduleRisk: false,
    inProgressTaskCount: 0,
    isWipExceeded: false,
    overdueTaskCount: overview.overdueTaskCount,
    wipLimit: undefined,
    wipExceededBy: 0,
  };

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
      toolbar={(
        <div className="flex flex-col gap-2">
          <div className="pm-toolbar-summary">
            <span>{project.projectCode}</span>
            <ProjectStatus status={project.status} />
            <span>负责人：{project.ownerDisplayName || '未分配'}</span>
          </div>
        </div>
      )}
    >
      <section aria-label="项目指标" className="pm-metric-grid">
        {metrics.map((metric) => <div className="pm-metric-card" data-tone={metric.tone} key={metric.label}><span className="pm-metric-card__label">{metric.label}</span><strong className="pm-metric-card__value">{metric.value}</strong></div>)}
      </section>
      <section className="pm-panel" aria-labelledby="project-risk-title">
        <div className="pm-panel__heading"><div><h2 id="project-risk-title">风险与执行信号</h2><p className="pm-panel__meta">风险指标来自当前项目叶子任务和 WIP 限制的实时聚合。</p></div><HealthBadge health={riskSummary.hasScheduleRisk || riskSummary.isWipExceeded ? 'AtRisk' : 'OnTrack'} /></div>
        <div className="pm-metric-grid">
          <div className="pm-metric-card" data-tone={riskSummary.overdueTaskCount > 0 ? 'danger' : 'neutral'}><span className="pm-metric-card__label">逾期任务</span><strong className="pm-metric-card__value">{riskSummary.overdueTaskCount}</strong></div>
          <div className="pm-metric-card" data-tone={riskSummary.blockedTaskCount > 0 ? 'warning' : 'neutral'}><span className="pm-metric-card__label">阻塞任务</span><strong className="pm-metric-card__value">{riskSummary.blockedTaskCount}</strong></div>
          <div className="pm-metric-card" data-tone={riskSummary.dueSoonIncompleteTaskCount > 0 ? 'warning' : 'neutral'}><span className="pm-metric-card__label">7 日内到期</span><strong className="pm-metric-card__value">{riskSummary.dueSoonIncompleteTaskCount}</strong></div>
          <div className="pm-metric-card" data-tone={riskSummary.isWipExceeded ? 'danger' : 'neutral'}><span className="pm-metric-card__label">WIP</span><strong className="pm-metric-card__value">{riskSummary.inProgressTaskCount}{riskSummary.wipLimit == null ? '' : ` / ${riskSummary.wipLimit}`}</strong></div>
        </div>
      </section>
      <section className="pm-panel" aria-labelledby="milestone-health-title">
        <div className="pm-panel__heading"><div><h2 id="milestone-health-title">里程碑健康</h2><p className="pm-panel__meta">里程碑进度和健康状态由当前项目数据计算。</p></div><span className="pm-view-note">共 {overview.milestoneCount} 个</span></div>
        {overview.milestones.length === 0 ? <p className="pm-muted">暂无里程碑。创建里程碑后会在此显示健康状态和进度。</p> : <ul className="pm-list">{overview.milestones.map((milestone) => <li key={milestone.id}><div className="pm-milestone-row"><div><div className="pm-milestone-row__name">{milestone.name}</div><div className="pm-milestone-row__meta"><HealthBadge health={milestone.healthStatus} /><span>{milestone.dueDate ? `截止 ${formatDate(milestone.dueDate)}` : '未设置截止日期'}</span></div></div><div className="pm-progress"><progress aria-label={`${milestone.name} 完成进度 ${milestone.progressPercent}%`} max={100} value={milestone.progressPercent} /><span>{milestone.progressPercent}%</span></div></div></li>)}</ul>}
      </section>
      <section className="pm-panel" aria-labelledby="workload-title">
        <div className="pm-panel__heading"><div><h2 id="workload-title">人员工作量</h2><p className="pm-panel__meta">任务归属负责人，已登记工时归属实际填写日志的人员。</p></div></div>
        {!canViewTasks ? <p className="pm-muted">当前账号没有任务查看权限。</p> : <ProjectWorkloadTable displayNames={Object.fromEntries((workloadPeopleQuery.data?.data.items ?? []).map((candidate) => [candidate.userId, candidate.displayName || candidate.userName]))} isError={workloadQuery.isError} isLoading={workloadQuery.isLoading} onRetry={() => void workloadQuery.refetch()} rows={workloadQuery.data?.data ?? []} />}
      </section>
      <ProjectManagementActivityTimeline
        canView={canViewActivities}
        isError={activitiesQuery.isError}
        isLoading={activitiesQuery.isLoading}
        page={activitiesQuery.data?.data}
        pageSize={activityQuery.pageSize}
      />
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
