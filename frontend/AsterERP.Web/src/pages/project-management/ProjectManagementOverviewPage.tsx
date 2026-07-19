import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';

import { getProjectManagementActivities, getProjectManagementOverview } from '../../api/project-management/projectManagement.api';
import type { ProjectManagementActivityPage, ProjectManagementOverviewItem, ProjectManagementProjectUpsertRequest } from '../../api/project-management/projectManagement.types';
import { usePermission } from '../../core/auth/usePermission';
import { isHttpError } from '../../core/http/httpError';
import { useI18n } from '../../core/i18n/I18nProvider';
import { projectManagementQueryKeys } from '../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../core/query/useApiMutation';
import { ProjectCreateDialog } from '../../features/project-management/project-create/ProjectCreateDialog';
import { useProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';
import { useMessage } from '../../shared/feedback/useMessage';
import { Page403 } from '../../shared/status/Page403';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';
import { PmBox, PmButton, PmChip, PmDivider, PmIcon, PmIconButton, PmPage, PmPane, PmRow, PmSection, PmSurface, PmTab, PmTabs, PmText } from '../../ui/project-management';

export function ProjectManagementOverviewPage() {
  const scope = useProjectManagementWorkspaceScope();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const { translate } = useI18n();
  const navigate = useNavigate();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [editOpen, setEditOpen] = useState(false);
  const { hasPermission: canEdit } = usePermission('project-management:project:edit');
  const query = useQuery({ queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }), queryFn: ({ signal }) => getProjectManagementOverview({ projectId, pageIndex: 1, pageSize: 1 }, signal), enabled: scope.isAvailable && Boolean(projectId) });
  const project = query.data?.data.items[0];
  const activityQuery = useQuery({ queryKey: projectManagementQueryKeys.activities(scope, projectId, { pageIndex: 1, pageSize: 20 }), queryFn: ({ signal }) => getProjectManagementActivities(projectId, { pageIndex: 1, pageSize: 20 }, signal), enabled: scope.isAvailable && Boolean(projectId) && searchParams.get('tab') === 'activity' });
  const updateMutation = useApiMutation({ mutationFn: (value: ProjectManagementProjectUpsertRequest) => import('../../api/project-management/projectManagement.api').then(api => api.updateProjectManagementProject(projectId, value)), onSuccess: () => { setEditOpen(false); message.success(translate('projectManagement.home.updateSuccess')); void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.overview(scope, { projectId }) }); }, onError: error => message.error(isHttpError(error) && error.status === 409 ? translate('projectManagement.home.conflict') : translate('projectManagement.home.updateFailed')) });

  if (!scope.isAvailable) return <Page403 />;
  if (query.isLoading) return <PageLoading />;
  if (query.isError) return <PageError description={translate('projectManagement.home.loadingFailed')} action={<PmButton onClick={() => void query.refetch()}>{translate('projectManagement.home.retry')}</PmButton>} />;
  if (!project) return <PageError description={translate('projectManagement.home.notFound')} />;
  const tab = searchParams.get('tab') === 'activity' ? 'activity' : searchParams.get('tab') === 'issues' ? 'issues' : 'overview';
  const editValue = toEditValue(project);
  return <PmPage>
    <PmPane sx={{ flex: 1, overflow: 'auto' }}><PmSurface>
      <PmBox sx={{ display: 'flex', alignItems: 'center', gap: 1, minHeight: 42 }}><PmIconButton aria-label={translate('projectManagement.home.back')} onClick={() => navigate('/platform/project-management')}><PmIcon name="back" /></PmIconButton><PmText color="text.secondary" fontSize=".78rem">{translate('projectManagement.home.title')}</PmText><PmText color="text.secondary">/</PmText><PmText fontSize=".78rem" fontWeight={650}>{project.project.projectName}</PmText></PmBox><PmDivider />
      <PmBox sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, py: 2 }}><PmBox sx={{ minWidth: 0 }}><PmText variant="h1" fontSize="1.35rem" fontWeight={700}>{project.project.projectName}</PmText><PmText color="text.secondary" fontSize=".78rem">{project.project.projectCode}</PmText></PmBox><PmBox sx={{ display: 'flex', alignItems: 'center', gap: 1 }}><PmChip label={project.project.ownerDisplayName || translate('projectManagement.home.unassigned')} /><PmChip label={statusLabel(project.project.status, translate)} color={project.project.status === 'Completed' ? 'success' : project.project.status === 'Active' ? 'primary' : 'default'} />{canEdit && <PmButton onClick={() => setEditOpen(true)} startIcon={<PmIcon name="settings" />}>{translate('projectManagement.home.edit')}</PmButton>}</PmBox></PmBox>
      <PmTabs onChange={(_, value) => { const next = new URLSearchParams(searchParams); if (value === 'overview') next.delete('tab'); else next.set('tab', String(value)); setSearchParams(next); }} value={tab}><PmTab label="Overview" value="overview" /><PmTab label="Activity" value="activity" /><PmTab label="Issues" value="issues" /></PmTabs><PmDivider />
      {tab === 'issues' ? <PmSection><PmText color="text.secondary">{translate('projectManagement.home.overviewIssuesHint')}</PmText><PmButton onClick={() => navigate(`/platform/project-management/projects/${encodeURIComponent(projectId)}/tasks`)} sx={{ mt: 2 }} variant="outlined">{translate('projectManagement.home.openIssues')}</PmButton></PmSection> : tab === 'activity' ? <ActivityPanel data={activityQuery.data?.data} loading={activityQuery.isLoading} translate={translate} /> : <OverviewCanvas project={project} translate={translate} />}
    </PmSurface></PmPane>
    <PropertiesPanel project={project} translate={translate} />
    <ProjectCreateDialog editing initialValue={editValue} onClose={() => setEditOpen(false)} onSubmit={value => updateMutation.mutate(value)} open={editOpen} pending={updateMutation.isPending} />
  </PmPage>;
}

function OverviewCanvas({ project, translate }: { project: ProjectManagementOverviewItem; translate: (key: string) => string }) {
  return <><PmSection><PmText variant="h2" fontSize=".95rem" fontWeight={700}>{translate('projectManagement.home.field.description')}</PmText><PmText color="text.secondary" sx={{ display: 'block', mt: 1 }}>{project.project.description || translate('projectManagement.home.overviewEmptyDescription')}</PmText></PmSection><PmDivider /><PmSection><PmText variant="h2" fontSize=".95rem" fontWeight={700}>{translate('projectManagement.home.latestUpdate')}</PmText><PmText color="text.secondary" sx={{ display: 'block', mt: 1 }}>{translate('projectManagement.home.overviewEmptyUpdate')}</PmText></PmSection><PmDivider /><PmSection><PmBox sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}><PmText variant="h2" fontSize=".95rem" fontWeight={700}>{translate('projectManagement.home.milestones')}</PmText><PmText color="text.secondary" fontSize=".76rem">{project.milestoneCount}</PmText></PmBox>{project.milestones.length === 0 ? <PmText color="text.secondary" sx={{ display: 'block', mt: 1 }}>{translate('projectManagement.home.noMilestones')}</PmText> : <PmBox sx={{ mt: 1 }}>{project.milestones.map(milestone => <PmRow key={milestone.id} sx={{ gridTemplateColumns: 'minmax(0, 1fr) 100px 100px', minHeight: 46, fontSize: '.78rem' }}><PmBox sx={{ display: 'flex', gap: 1, alignItems: 'center' }}><PmIcon name={milestone.progressPercent >= 100 ? 'circleCheck' : 'check'} size={15} /><PmText noWrap fontSize="inherit">{milestone.name}</PmText></PmBox><PmText color="text.secondary" fontSize="inherit">{milestone.dueDate ? new Date(milestone.dueDate).toLocaleDateString() : '—'}</PmText><PmText color="text.secondary" fontSize="inherit">{milestone.progressPercent}%</PmText></PmRow>)}</PmBox>}</PmSection></>;
}

function PropertiesPanel({ project, translate }: { project: ProjectManagementOverviewItem; translate: (key: string) => string }) { return <PmPane paneWidth={320} sx={{ display: { xs: 'none', lg: 'block' }, borderLeft: 1, borderColor: 'divider' }}><PmSurface><PmTabs value="properties"><PmTab label={translate('projectManagement.home.properties')} value="properties" /><PmTab label={translate('projectManagement.home.milestones')} value="milestones" /><PmTab label={translate('projectManagement.home.activity')} value="activity" /></PmTabs><PmDivider sx={{ mb: 2 }} /><PmBox sx={{ display: 'grid', gap: 1.5 }}>{[['Lead', project.project.ownerDisplayName || translate('projectManagement.home.unassigned')], [translate('projectManagement.home.priority'), project.project.priority], [translate('projectManagement.home.status'), statusLabel(project.project.status, translate)], [translate('projectManagement.home.targetDate'), project.project.dueDate ? new Date(project.project.dueDate).toLocaleDateString() : '—'], [translate('projectManagement.home.updatedTime'), project.project.updatedTime ? new Date(project.project.updatedTime).toLocaleString() : '—']].map(([label, value]) => <PmBox key={label} sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}><PmText color="text.secondary" fontSize=".76rem">{label}</PmText><PmText fontSize=".76rem" fontWeight={600}>{value}</PmText></PmBox>)}</PmBox></PmSurface></PmPane>; }
function ActivityPanel({ data, loading, translate }: { data?: ProjectManagementActivityPage; loading: boolean; translate: (key: string) => string }) { if (loading) return <PmSection><PmText color="text.secondary">{translate('common.loading')}</PmText></PmSection>; if (!data?.items.length) return <PmSection><PmText color="text.secondary">{translate('projectManagement.home.noActivity')}</PmText></PmSection>; return <PmSection>{data.items.map(item => <PmRow key={item.id} sx={{ gridTemplateColumns: 'minmax(0, 1fr) 150px', minHeight: 48, fontSize: '.78rem' }}><PmText fontSize="inherit">{item.summary}</PmText><PmText color="text.secondary" fontSize=".72rem">{new Date(item.createdTime).toLocaleString()}</PmText></PmRow>)}</PmSection>; }
function statusLabel(status: string, translate: (key: string) => string) { return translate(`projectManagement.home.status.${status}`); }
function toEditValue(project: ProjectManagementOverviewItem): ProjectManagementProjectUpsertRequest { return { projectCode: project.project.projectCode, projectName: project.project.projectName, description: project.project.description, status: project.project.status, priority: project.project.priority, ownerUserId: project.project.ownerUserId, startDate: project.project.startDate, dueDate: project.project.dueDate, progressPercent: project.project.progressPercent, versionNo: project.project.versionNo }; }
