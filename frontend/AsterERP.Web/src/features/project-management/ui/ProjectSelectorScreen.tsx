import { Box, Typography } from '@mui/material';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { createProjectManagementProject, getProjectManagementOverview } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementProjectUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useMessage } from '../../../shared/feedback/useMessage';
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
  const message = useMessage();
  const [keyword, setKeyword] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const overviewKey = projectManagementQueryKeys.overview(scope, { keyword, pageIndex: 1, pageSize: 50 });
  const query = useQuery({
    enabled: scope.isAvailable,
    queryKey: overviewKey,
    queryFn: ({ signal }) => getProjectManagementOverview({ keyword, pageIndex: 1, pageSize: 50 }, signal),
  });
  const create = useMutation({ mutationFn: createProjectManagementProject, onSuccess: (result) => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(result.data.id)}/overview`)) });
  const projects = useMemo(() => query.data?.data.items ?? [], [query.data]);

  const refreshProjects = async () => {
    if (!scope.isAvailable || refreshing) return;
    setRefreshing(true);
    try {
      const result = await query.refetch();
      if (result.error && !isRequestCancelled(result.error)) {
        message.error(t('projectManagement.workbench.selector.loadFailed'));
        return;
      }
      if (!result.error) message.success(t('projectManagement.workbench.selector.refreshSuccess'));
    } catch (error) {
      if (!isRequestCancelled(error)) message.error(t('projectManagement.workbench.selector.loadFailed'));
    } finally {
      setRefreshing(false);
    }
  };

  return (
    <Box sx={{ display: 'flex', flex: '1 1 auto', minHeight: 0, p: { xs: 2, md: 3 }, bgcolor: '#f7f8fb' }}>
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, flex: '1 1 auto', minWidth: 0, maxWidth: 1440, mx: 'auto' }}>
        <Box
          sx={{
            display: 'flex',
            flexWrap: 'wrap',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 1.5,
            rowGap: 1,
          }}
        >
          <Box sx={{ minWidth: 0, flex: '1 1 240px' }}>
            <Typography component="h1" fontWeight={750} sx={{ lineHeight: 1.3 }} variant="h5">
              {t('projectManagement.workbench.selector.title')}
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 0.5, maxWidth: 520 }} variant="body2">
              {t('projectManagement.workbench.selector.description')}
            </Typography>
          </Box>
          <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, flexShrink: 0 }}>
            <button
              aria-busy={refreshing}
              aria-label={t('projectManagement.workbench.selector.refresh')}
              className="pm-workbench-command"
              disabled={refreshing || !scope.isAvailable}
              onClick={() => { void refreshProjects(); }}
              title={t('projectManagement.workbench.selector.refresh')}
              type="button"
            >
              <span className={refreshing ? 'pm-refresh-icon is-spinning' : 'pm-refresh-icon'}>
                <PmIcon name="refresh" size={15} />
              </span>
              {t('projectManagement.workbench.refresh')}
            </button>
            <button className="pm-primary-button" onClick={() => setCreateOpen(true)} type="button">
              <PmIcon name="plus" size={16} /> {t('projectManagement.workbench.selector.create')}
            </button>
          </Box>
        </Box>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <input
            aria-label={t('projectManagement.workbench.selector.searchAria')}
            className="pm-project-search"
            onChange={(event) => setKeyword(event.target.value)}
            placeholder={t('projectManagement.workbench.selector.searchPlaceholder')}
            style={{ flex: '1 1 auto', minWidth: 0, maxWidth: 420 }}
            value={keyword}
          />
        </Box>

        <Box sx={{ overflow: 'hidden', bgcolor: '#fff', border: '1px solid #e7eaf0', borderRadius: 2 }}>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: 'minmax(260px, 2fr) repeat(4, minmax(100px, 1fr))',
              gap: 2,
              px: 2.25,
              py: 1.25,
              bgcolor: '#fbfcfe',
              color: 'text.secondary',
              fontSize: 12,
            }}
          >
            <span>{t('projectManagement.workbench.selector.name')}</span>
            <span>{t('projectManagement.workbench.selector.health')}</span>
            <span>{t('projectManagement.workbench.selector.owner')}</span>
            <span>{t('projectManagement.workbench.selector.dueDate')}</span>
            <span>{t('projectManagement.workbench.selector.status')}</span>
          </Box>
          {query.isLoading ? <Typography sx={{ p: 4, textAlign: 'center' }}>{t('projectManagement.workbench.selector.loading')}</Typography> : null}
          {query.isError && !projects.length ? <Typography color="error" sx={{ p: 4, textAlign: 'center' }}>{t('projectManagement.workbench.selector.loadFailed')}</Typography> : null}
          {!query.isLoading && !query.isError && projects.length === 0 ? (
            <Typography color="text.secondary" sx={{ p: 6, textAlign: 'center' }}>{t('projectManagement.workbench.selector.empty')}</Typography>
          ) : null}
          {projects.map((item) => (
            <button
              className="pm-project-selector-row"
              key={item.project.id}
              onClick={() => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(item.project.id)}/overview`))}
              type="button"
            >
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
                <PmIcon name="folder" />
                <Box sx={{ minWidth: 0 }}>
                  <Typography fontWeight={650} noWrap>{item.project.projectName}</Typography>
                  <Typography color="text.secondary" variant="caption">{item.project.projectCode}</Typography>
                </Box>
              </Box>
              <span>{item.health}</span>
              <span>{item.project.ownerDisplayName ?? item.project.ownerUserId}</span>
              <span>{item.project.dueDate ? date(item.project.dueDate) : '—'}</span>
              <span>{projectManagementEnumLabel(t, 'status', item.project.status)}</span>
            </button>
          ))}
        </Box>
      </Box>
      <ProjectCreateDialog
        editing={false}
        initialValue={newProject}
        onClose={() => setCreateOpen(false)}
        onSubmit={(value) => create.mutate(value)}
        open={createOpen}
        pending={create.isPending}
      />
    </Box>
  );
}

function isRequestCancelled(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  const name = 'name' in error ? String((error as { name?: unknown }).name ?? '') : '';
  return name === 'AbortError' || name === 'CancelledError' || name === 'CanceledError';
}
