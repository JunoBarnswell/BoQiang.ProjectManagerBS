import { Box, LinearProgress, Stack, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import { getProjectManagementActivities, getProjectManagementOverview } from '../../../api/project-management/projectManagement.api';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useMessage } from '../../../shared/feedback/useMessage';
import { useProjectManagementI18n } from '../projectManagementI18n';
import { useProjectManagementProjectRealtime } from '../realtime/useProjectManagementProjectRealtime';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { ProjectScreenHeader, ProjectWorkbenchFrame } from './ProjectWorkbenchFrame';

export function ProjectOverviewScreen() {
  const { dateTime, format, t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const message = useMessage();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const [refreshing, setRefreshing] = useState(false);
  const overview = useQuery({ enabled: scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }), queryFn: ({ signal }) => getProjectManagementOverview({ projectId, pageIndex: 1, pageSize: 1 }, signal) });
  const activities = useQuery({ enabled: scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.activities(scope, projectId, { pageIndex: 1, pageSize: 4 }), queryFn: ({ signal }) => getProjectManagementActivities(projectId, { pageIndex: 1, pageSize: 4 }, signal) });
  useProjectManagementProjectRealtime({ enabled: scope.isAvailable && Boolean(projectId), onAccessRevoked: () => navigate(toProjectManagementPlatformRoute()), projectId, scope, signalRUrl: '/hubs/system-notification' });
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

  if (overview.isLoading) return <Box sx={{ p: 4 }}>{t('projectManagement.workbench.overview.loading')}</Box>;
  if (!item) return <Box sx={{ p: 4 }}>{t('projectManagement.workbench.overview.notFound')}</Box>;
  const taskCount = item.taskCount;
  const pending = item.pendingTaskCount ?? Math.max(0, taskCount - item.completedTaskCount - item.inProgressTaskCount - item.blockedTaskCount);
  const storyPoints = item.storyPointsTotal ?? 0;
  const values: Record<string, number> = { TaskCount: taskCount, CompletedTaskCount: item.completedTaskCount, InProgressTaskCount: item.inProgressTaskCount, PendingTaskCount: pending, StoryPoints: storyPoints };
  const metricMeta = [
    [t('projectManagement.workbench.overview.metric.total'), 'TaskCount', '#eaf0ff'], [t('projectManagement.workbench.overview.metric.completed'), 'CompletedTaskCount', '#eaf9f0'], [t('projectManagement.workbench.overview.metric.inProgress'), 'InProgressTaskCount', '#fff6e7'], [t('projectManagement.workbench.overview.metric.pending'), 'PendingTaskCount', '#fff0f0'], [t('projectManagement.workbench.overview.metric.storyPoints'), 'StoryPoints', '#fff7e8'],
  ] as const;
  const risks = [ [t('projectManagement.workbench.risk.high'), item.riskSummary?.overdueTaskCount ?? 0, '#ef4444'], [t('projectManagement.workbench.risk.medium'), item.riskSummary?.blockedTaskCount ?? 0, '#f59e0b'], [t('projectManagement.workbench.risk.low'), item.riskSummary?.dueSoonIncompleteTaskCount ?? 0, '#22c55e'], [t('projectManagement.workbench.risk.closed'), item.completedTaskCount, '#94a3b8'] ];

  return <ProjectWorkbenchFrame active="overview"><Box sx={{ flex: '1 1 auto', minWidth: 0, overflow: 'auto' }}>
    <ProjectScreenHeader
      code={item.project.projectCode}
      name={item.project.projectName}
      onCreateRequirement={() => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(projectId)}/requirements?create=1`))}
      onRefresh={refresh}
      refreshing={refreshing}
    />
    <Box sx={{ p: { xs: 2, md: 3 }, display: 'grid', gap: 2, gridTemplateColumns: 'repeat(12, minmax(0, 1fr))' }}>
      <Box sx={{ gridColumn: '1 / -1', display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', md: 'repeat(5, minmax(0, 1fr))' } }}>{metricMeta.map(([label, key, color]) => <MetricCard key={key} color={color} label={label} value={values[key]} />)}</Box>
      <Panel sx={{ gridColumn: { xs: '1 / -1', lg: 'span 4' } }} title={t('projectManagement.workbench.overview.milestones')}>{item.milestones.slice(0, 4).map((milestone) => <Stack key={milestone.id} spacing={0.55} sx={{ mb: 1.5 }}><Stack direction="row" justifyContent="space-between"><Typography variant="body2">{milestone.name}</Typography><Typography color="text.secondary" variant="caption">{milestone.progressPercent}%</Typography></Stack><LinearProgress sx={{ height: 5, borderRadius: 4 }} value={Number(milestone.progressPercent)} variant="determinate" /></Stack>)}</Panel>
      <Panel sx={{ gridColumn: { xs: '1 / -1', lg: 'span 3' } }} title={t('projectManagement.workbench.overview.risk')}>{risks.map(([label, count, color]) => <Stack alignItems="center" direction="row" justifyContent="space-between" key={label as string} sx={{ py: 0.65 }}><Stack alignItems="center" direction="row" spacing={1}><Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: color }} /><Typography variant="body2">{label}</Typography></Stack><Typography fontWeight={700} variant="body2">{count}</Typography></Stack>)}</Panel>
      <Panel sx={{ gridColumn: { xs: '1 / -1', lg: 'span 5' } }} title={t('projectManagement.workbench.overview.workload')}>{item.people.slice(0, 5).map((person) => <Stack key={person.userId} spacing={0.5} sx={{ mb: 1.25 }}><Stack direction="row" justifyContent="space-between"><Typography variant="body2">{person.displayName ?? person.userId}</Typography><Typography color="text.secondary" variant="caption">{format('projectManagement.workbench.workloadValue', { estimated: person.estimatedMinutes ?? 0, capacity: person.capacityMinutes ?? 2400 })}</Typography></Stack><LinearProgress color="primary" sx={{ height: 5, borderRadius: 4 }} value={person.workloadPercent ?? 0} variant="determinate" /></Stack>)}</Panel>
      <Panel sx={{ gridColumn: { xs: '1 / -1', lg: 'span 8' } }} title={t('projectManagement.workbench.overview.activity')}>{activities.data?.data.items.length ? activities.data.data.items.map((activity) => <Stack direction="row" key={activity.id} spacing={1} sx={{ py: 0.8, borderBottom: '1px solid #f0f2f6' }}><Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: '#3b82f6', mt: 0.8 }} /><Typography sx={{ flex: 1 }} variant="body2">{activity.summaryText ? format(activity.summaryText.key, activity.summaryText.arguments) : activity.summary ?? activity.activityType}</Typography><Typography color="text.secondary" variant="caption">{dateTime(activity.createdTime)}</Typography></Stack>) : <Typography color="text.secondary" variant="body2">{t('projectManagement.workbench.overview.noActivity')}</Typography>}</Panel>
      <Panel sx={{ gridColumn: { xs: '1 / -1', lg: 'span 4' } }} title={t('projectManagement.workbench.overview.distribution')}>{item.requirementTypeDistribution?.map((entry) => <Stack direction="row" justifyContent="space-between" key={entry.key} sx={{ py: 0.7 }}><Typography variant="body2">{entry.key}</Typography><Typography color="text.secondary" variant="body2">{entry.count} · {entry.percent}%</Typography></Stack>)}</Panel>
    </Box>
  </Box></ProjectWorkbenchFrame>;
}

function MetricCard({ color, label, value }: { color: string; label: string; value: number }) { return <Box sx={{ p: 1.75, border: '1px solid #edf0f4', borderRadius: 2, bgcolor: '#fff' }}><Box sx={{ width: 26, height: 26, borderRadius: 1.5, bgcolor: color, mb: 1 }} /><Typography color="text.secondary" variant="caption">{label}</Typography><Typography fontWeight={750} sx={{ mt: 0.2, fontSize: 24 }}>{value}</Typography></Box>; }
function Panel({ children, sx, title }: { children: ReactNode; sx: object; title: string }) { return <Box sx={{ ...sx, minHeight: 190, p: 2, border: '1px solid #edf0f4', borderRadius: 2, bgcolor: '#fff' }}><Typography fontWeight={700} sx={{ mb: 1.5 }} variant="subtitle2">{title}</Typography>{children}</Box>; }

function isRequestCancelled(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  const name = 'name' in error ? String((error as { name?: unknown }).name ?? '') : '';
  return name === 'AbortError' || name === 'CancelledError' || name === 'CanceledError';
}
