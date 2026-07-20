import { Box, Stack, Typography } from '@mui/material';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { createProjectManagementProject, getProjectManagementOverview } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementProjectUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { PmIcon } from '../../../ui/project-management';
import { ProjectCreateDialog } from '../project-create/ProjectCreateDialog';
import { projectManagementEnumLabel, useProjectManagementI18n } from '../projectManagementI18n';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

const newProject: ProjectManagementProjectUpsertRequest = { projectCode: '', projectName: '', status: 'Planning', priority: 'Medium' };

export function ProjectSelectorScreen() {
  const { date, t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const query = useQuery({
    enabled: scope.isAvailable,
    queryKey: projectManagementQueryKeys.overview(scope, { keyword, pageIndex: 1, pageSize: 50 }),
    queryFn: ({ signal }) => getProjectManagementOverview({ keyword, pageIndex: 1, pageSize: 50 }, signal),
  });
  const create = useMutation({ mutationFn: createProjectManagementProject, onSuccess: (result) => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(result.data.id)}/overview`)) });
  const projects = useMemo(() => query.data?.data.items ?? [], [query.data]);

  return <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, p: { xs: 2, md: 4 }, bgcolor: '#f7f8fb' }}>
    <Stack spacing={2} sx={{ flex: '1 1 auto', minWidth: 0, maxWidth: 1440, mx: 'auto' }}>
      <Stack alignItems={{ xs: 'flex-start', md: 'center' }} direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" spacing={1.5}>
        <Stack spacing={0.25}><Typography fontWeight={750} variant="h5">{t('projectManagement.workbench.selector.title')}</Typography><Typography color="text.secondary" variant="body2">{t('projectManagement.workbench.selector.description')}</Typography></Stack>
        <button className="pm-primary-button" onClick={() => setCreateOpen(true)} type="button"><PmIcon name="plus" size={16} /> {t('projectManagement.workbench.selector.create')}</button>
      </Stack>
      <input aria-label={t('projectManagement.workbench.selector.searchAria')} className="pm-project-search" onChange={(event) => setKeyword(event.target.value)} placeholder={t('projectManagement.workbench.selector.searchPlaceholder')} value={keyword} />
      <Box sx={{ overflow: 'hidden', bgcolor: '#fff', border: '1px solid #e7eaf0', borderRadius: 2 }}>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(260px, 2fr) repeat(4, minmax(100px, 1fr))', gap: 2, px: 2.25, py: 1.25, bgcolor: '#fbfcfe', color: 'text.secondary', fontSize: 12 }}><span>{t('projectManagement.workbench.selector.name')}</span><span>{t('projectManagement.workbench.selector.health')}</span><span>{t('projectManagement.workbench.selector.owner')}</span><span>{t('projectManagement.workbench.selector.dueDate')}</span><span>{t('projectManagement.workbench.selector.status')}</span></Box>
        {query.isLoading ? <Typography sx={{ p: 4, textAlign: 'center' }}>{t('projectManagement.workbench.selector.loading')}</Typography> : null}
        {query.isError ? <Typography color="error" sx={{ p: 4, textAlign: 'center' }}>{t('projectManagement.workbench.selector.loadFailed')}</Typography> : null}
        {!query.isLoading && !query.isError && projects.length === 0 ? <Typography color="text.secondary" sx={{ p: 6, textAlign: 'center' }}>{t('projectManagement.workbench.selector.empty')}</Typography> : null}
        {projects.map((item) => <button className="pm-project-selector-row" key={item.project.id} onClick={() => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(item.project.id)}/overview`))} type="button">
          <Stack alignItems="center" direction="row" spacing={1}><PmIcon name="folder" /><Stack alignItems="flex-start"><Typography fontWeight={650}>{item.project.projectName}</Typography><Typography color="text.secondary" variant="caption">{item.project.projectCode}</Typography></Stack></Stack>
          <span>{item.health}</span><span>{item.project.ownerDisplayName ?? item.project.ownerUserId}</span><span>{item.project.dueDate ? date(item.project.dueDate) : '—'}</span><span>{projectManagementEnumLabel(t, 'status', item.project.status)}</span>
        </button>)}
      </Box>
    </Stack>
    <ProjectCreateDialog editing={false} initialValue={newProject} onClose={() => setCreateOpen(false)} onSubmit={(value) => create.mutate(value)} open={createOpen} pending={create.isPending} />
  </Box>;
}
