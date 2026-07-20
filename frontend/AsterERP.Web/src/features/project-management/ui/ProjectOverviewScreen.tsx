import { Box, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useCallback, useState, type ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import { getProjectManagementActivities, getProjectManagementOverview } from '../../../api/project-management/projectManagement.api';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useMessage } from '../../../shared/feedback/useMessage';
import { useProjectManagementI18n } from '../projectManagementI18n';
import { useProjectManagementProjectRealtime } from '../realtime/useProjectManagementProjectRealtime';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { ProjectScreenHeader, ProjectWorkbenchFrame } from './ProjectWorkbenchFrame';

const metricTones = {
  total: 'var(--app-primary-100)',
  completed: 'color-mix(in srgb, var(--app-success) 18%, var(--app-white))',
  inProgress: 'color-mix(in srgb, var(--app-warning) 16%, var(--app-white))',
  pending: 'color-mix(in srgb, var(--app-danger) 12%, var(--app-white))',
  storyPoints: 'color-mix(in srgb, var(--app-warning) 14%, var(--app-white))',
} as const;

export function ProjectOverviewScreen() {
  const { dateTime, format, t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const message = useMessage();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const [refreshing, setRefreshing] = useState(false);
  const overview = useQuery({ enabled: scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }), queryFn: ({ signal }) => getProjectManagementOverview({ projectId, pageIndex: 1, pageSize: 1 }, signal) });
  const activities = useQuery({ enabled: scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.activities(scope, projectId, { pageIndex: 1, pageSize: 4 }), queryFn: ({ signal }) => getProjectManagementActivities(projectId, { pageIndex: 1, pageSize: 4 }, signal) });
  const handleAccessRevoked = useCallback(() => {
    navigate(toProjectManagementPlatformRoute());
  }, [navigate]);
  useProjectManagementProjectRealtime({ enabled: scope.isAvailable && Boolean(projectId), onAccessRevoked: handleAccessRevoked, projectId, scope, signalRUrl: '/hubs/system-notification' });
  const item = overview.data?.data.items[0];

  const refresh = async () => {
    if (refreshing) return;
    setRefreshing(true);
    try {
      const [overviewResult, activitiesResult] = await Promise.all([overview.refetch(), activities.refetch()]);
      const failed = [overviewResult.error, activitiesResult.error].find((error) => error && !isRequestCancelled(error));
      if (failed) {
        message.error(t('projectManagement.workbench.overview.notFound'));
        return;
      }
      if (!overviewResult.error && !activitiesResult.error) message.success(t('projectManagement.workbench.refreshSuccess'));
    } catch (error) {
      if (!isRequestCancelled(error)) message.error(t('projectManagement.workbench.overview.notFound'));
    } finally {
      setRefreshing(false);
    }
  };

  if (overview.isLoading) return <Box className="pm-overview-page" sx={{ p: 4 }}>{t('projectManagement.workbench.overview.loading')}</Box>;
  if (!item) return <Box className="pm-overview-page" sx={{ p: 4 }}>{t('projectManagement.workbench.overview.notFound')}</Box>;
  const taskCount = item.taskCount;
  const pending = item.pendingTaskCount ?? Math.max(0, taskCount - item.completedTaskCount - item.inProgressTaskCount - item.blockedTaskCount);
  const storyPoints = item.storyPointsTotal ?? 0;
  const values: Record<string, number> = { TaskCount: taskCount, CompletedTaskCount: item.completedTaskCount, InProgressTaskCount: item.inProgressTaskCount, PendingTaskCount: pending, StoryPoints: storyPoints };
  const metricMeta = [
    [t('projectManagement.workbench.overview.metric.total'), 'TaskCount', 'total'],
    [t('projectManagement.workbench.overview.metric.completed'), 'CompletedTaskCount', 'completed'],
    [t('projectManagement.workbench.overview.metric.inProgress'), 'InProgressTaskCount', 'inProgress'],
    [t('projectManagement.workbench.overview.metric.pending'), 'PendingTaskCount', 'pending'],
    [t('projectManagement.workbench.overview.metric.storyPoints'), 'StoryPoints', 'storyPoints'],
  ] as const;
  const risks = [
    [t('projectManagement.workbench.risk.high'), item.riskSummary?.overdueTaskCount ?? 0, 'var(--app-danger)'],
    [t('projectManagement.workbench.risk.medium'), item.riskSummary?.blockedTaskCount ?? 0, 'var(--app-warning)'],
    [t('projectManagement.workbench.risk.low'), item.riskSummary?.dueSoonIncompleteTaskCount ?? 0, 'var(--app-success)'],
    [t('projectManagement.workbench.risk.closed'), item.completedTaskCount, 'var(--app-gray-400)'],
  ];

  return (
    <ProjectWorkbenchFrame active="overview">
      <Box className="pm-overview-page">
        <ProjectScreenHeader
          code={item.project.projectCode}
          name={item.project.projectName}
          onCreateRequirement={() => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(projectId)}/requirements?create=1`))}
          onRefresh={refresh}
          refreshing={refreshing}
        />
        <Box className="pm-overview-grid">
          <Box className="pm-overview-metrics">
            {metricMeta.map(([label, key, tone]) => (
              <MetricCard key={key} label={label} tone={tone} value={values[key]} />
            ))}
          </Box>
          <Panel className="pm-overview-panel--milestones" title={t('projectManagement.workbench.overview.milestones')}>
            {item.milestones.slice(0, 4).map((milestone) => (
              <Box className="pm-overview-milestone" key={milestone.id}>
                <Box className="pm-overview-milestone__head">
                  <Typography className="pm-overview-milestone__name" component="span">{milestone.name}</Typography>
                  <Typography className="pm-overview-milestone__percent" component="span">{milestone.progressPercent}%</Typography>
                </Box>
                <Box aria-hidden className="pm-overview-progress">
                  <span style={{ width: `${Number(milestone.progressPercent)}%` }} />
                </Box>
              </Box>
            ))}
          </Panel>
          <Panel className="pm-overview-panel--risk" title={t('projectManagement.workbench.overview.risk')}>
            {risks.map(([label, count, color]) => (
              <Box className="pm-overview-list-row" key={label as string}>
                <Box className="pm-overview-list-row__label">
                  <span className="pm-overview-dot" style={{ background: color as string }} />
                  <Typography component="span" variant="body2">{label}</Typography>
                </Box>
                <Typography component="span" fontWeight={700} variant="body2">{count}</Typography>
              </Box>
            ))}
          </Panel>
          <Panel className="pm-overview-panel--workload" title={t('projectManagement.workbench.overview.workload')}>
            {item.people.slice(0, 5).map((person) => (
              <Box className="pm-overview-milestone" key={person.userId}>
                <Box className="pm-overview-milestone__head">
                  <Typography className="pm-overview-milestone__name" component="span">{person.displayName ?? person.userId}</Typography>
                  <Typography className="pm-overview-milestone__percent" component="span">
                    {format('projectManagement.workbench.workloadValue', { estimated: person.estimatedMinutes ?? 0, capacity: person.capacityMinutes ?? 2400 })}
                  </Typography>
                </Box>
                <Box aria-hidden className="pm-overview-progress">
                  <span style={{ width: `${person.workloadPercent ?? 0}%`, background: 'var(--app-accent)' }} />
                </Box>
              </Box>
            ))}
          </Panel>
          <Panel className="pm-overview-panel--activity" title={t('projectManagement.workbench.overview.activity')}>
            {activities.data?.data.items.length ? activities.data.data.items.map((activity) => (
              <Box className="pm-overview-activity-row" key={activity.id}>
                <span className="pm-overview-dot" style={{ background: 'var(--app-accent)', marginTop: 6 }} />
                <Typography className="pm-overview-activity-row__summary" component="span">
                  {activity.summaryText ? format(activity.summaryText.key, activity.summaryText.arguments) : activity.summary ?? activity.activityType}
                </Typography>
                <Typography className="pm-overview-activity-row__time" component="span">{dateTime(activity.createdTime)}</Typography>
              </Box>
            )) : <Typography className="pm-overview-empty" component="p">{t('projectManagement.workbench.overview.noActivity')}</Typography>}
          </Panel>
          <Panel className="pm-overview-panel--distribution" title={t('projectManagement.workbench.overview.distribution')}>
            {item.requirementTypeDistribution?.map((entry) => (
              <Box className="pm-overview-list-row" key={entry.key}>
                <Typography component="span" variant="body2">{entry.key}</Typography>
                <Typography color="text.secondary" component="span" variant="body2">{entry.count} · {entry.percent}%</Typography>
              </Box>
            ))}
          </Panel>
        </Box>
      </Box>
    </ProjectWorkbenchFrame>
  );
}

function MetricCard({ label, tone, value }: { label: string; tone: keyof typeof metricTones; value: number }) {
  return (
    <Box className="pm-metric-card">
      <Box className="pm-metric-card__icon" sx={{ bgcolor: metricTones[tone] }} />
      <Typography className="pm-metric-card__label" component="span">{label}</Typography>
      <Typography className="pm-metric-card__value" component="span">{value}</Typography>
    </Box>
  );
}

function Panel({ children, className, title }: { children: ReactNode; className?: string; title: string }) {
  return (
    <Box className={`pm-overview-panel ${className ?? ''}`.trim()}>
      <Typography className="pm-overview-panel__title" component="h2">{title}</Typography>
      {children}
    </Box>
  );
}

function isRequestCancelled(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  const name = 'name' in error ? String((error as { name?: unknown }).name ?? '') : '';
  return name === 'AbortError' || name === 'CancelledError' || name === 'CanceledError';
}
