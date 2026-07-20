import { Box, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { createProjectManagementProject, getProjectManagementOverview, updateProjectManagementProject } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementProject, ProjectManagementProjectUpsertRequest } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useMessage } from '../../../shared/feedback/useMessage';
import { PmIcon } from '../../../ui/project-management';
import { ProjectCreateDialog } from '../project-create/ProjectCreateDialog';
import type { ProjectManagementProjectConflict } from '../project-create/projectManagementProjectConflict';
import { readProjectManagementProjectConflict } from '../project-create/projectManagementProjectConflict';
import { ProjectManagementCountdown } from '../components/ProjectManagementCountdown';
import { ProjectManagementProgressBar } from '../components/ProjectManagementProgressBar';
import { projectManagementEnumLabel, useProjectManagementI18n } from '../projectManagementI18n';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import './projectWorkbench.css';

const newProject: ProjectManagementProjectUpsertRequest = { projectCode: '', projectName: '', status: 'Planning', priority: 'Medium' };

function toProjectUpsertRequest(project: ProjectManagementProject): ProjectManagementProjectUpsertRequest {
  return {
    description: project.description,
    dueDate: project.dueDate,
    ownerUserId: project.ownerUserId,
    priority: project.priority,
    progressPercent: project.progressPercent,
    projectCode: project.projectCode,
    projectName: project.projectName,
    startDate: project.startDate,
    status: project.status,
    versionNo: project.versionNo,
    wipLimit: project.wipLimit,
  };
}

export function ProjectSelectorScreen() {
  const { t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const message = useMessage();
  const queryClient = useQueryClient();
  const { hasPermission: canEditProject } = usePermission('project-management:project:edit');
  const [keyword, setKeyword] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [editState, setEditState] = useState<{ id: string; value: ProjectManagementProjectUpsertRequest } | null>(null);
  const [conflict, setConflict] = useState<ProjectManagementProjectConflict | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const overviewKey = projectManagementQueryKeys.overview(scope, { keyword, pageIndex: 1, pageSize: 50 });
  const query = useQuery({
    enabled: scope.isAvailable,
    queryKey: overviewKey,
    queryFn: ({ signal }) => getProjectManagementOverview({ keyword, pageIndex: 1, pageSize: 50 }, signal),
  });
  const create = useMutation({
    mutationFn: createProjectManagementProject,
    onSuccess: (result) => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(result.data.id)}/overview`)),
  });
  const update = useMutation({
    mutationFn: ({ id, value }: { id: string; value: ProjectManagementProjectUpsertRequest }) => updateProjectManagementProject(id, value),
    onSuccess: async () => {
      message.success(t('projectManagement.home.updateSuccess'));
      setEditState(null);
      setConflict(null);
      await queryClient.invalidateQueries({ queryKey: overviewKey });
    },
    onError: (error) => {
      const conflictResult = readProjectManagementProjectConflict(error);
      if (conflictResult) {
        setConflict(conflictResult);
        message.error(t('projectManagement.home.conflict'));
        return;
      }
      message.error(t('projectManagement.home.updateFailed'));
    },
  });
  const projects = useMemo(() => query.data?.data.items ?? [], [query.data]);

  const closeDialog = () => {
    setCreateOpen(false);
    setEditState(null);
    setConflict(null);
  };

  const openEdit = (project: ProjectManagementProject) => {
    setCreateOpen(false);
    setConflict(null);
    setEditState({ id: project.id, value: toProjectUpsertRequest(project) });
  };

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
    <Box className="pm-workbench-page">
      <Box className="pm-workbench-page__inner">
        <Box className="pm-workbench-page__header">
          <Box className="pm-workbench-page__intro">
            <Typography className="pm-workbench-page__title" component="h1">
              {t('projectManagement.workbench.selector.title')}
            </Typography>
            <Typography className="pm-workbench-page__description" component="p">
              {t('projectManagement.workbench.selector.description')}
            </Typography>
          </Box>
          <Box className="pm-workbench-page__actions">
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

        <Box className="pm-project-selector-grid">
          {query.isLoading ? <Typography sx={{ p: 4, textAlign: 'center' }}>{t('projectManagement.workbench.selector.loading')}</Typography> : null}
          {query.isError && !projects.length ? <Typography color="error" sx={{ p: 4, textAlign: 'center' }}>{t('projectManagement.workbench.selector.loadFailed')}</Typography> : null}
          {!query.isLoading && !query.isError && projects.length === 0 ? (
            <Box className="pm-project-selector-empty">
              <Typography component="p" fontWeight={700}>{t('projectManagement.workbench.selector.empty')}</Typography>
              <Typography color="text.secondary" component="p" variant="body2">{t('projectManagement.workbench.selector.emptyHint')}</Typography>
              <button className="pm-primary-button" onClick={() => setCreateOpen(true)} type="button"><PmIcon name="plus" size={16} /> {t('projectManagement.workbench.selector.create')}</button>
            </Box>
          ) : null}
          {projects.map((item) => (
            <Box className="pm-project-selector-card" key={item.project.id}>
              {canEditProject && item.project.status !== 'Archived' ? (
                <button
                  aria-label={t('projectManagement.workbench.selector.edit')}
                  className="pm-project-selector-card__edit"
                  onClick={() => openEdit(item.project)}
                  title={t('projectManagement.workbench.selector.edit')}
                  type="button"
                >
                  <PmIcon name="settings" size={15} />
                </button>
              ) : null}
              <button
                className="pm-project-selector-card__body"
                onClick={() => navigate(toProjectManagementPlatformRoute(`projects/${encodeURIComponent(item.project.id)}/overview`))}
                type="button"
              >
                <Box className="pm-project-selector-card__heading">
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
                    <PmIcon name="folder" />
                    <Box sx={{ minWidth: 0 }}>
                      <Typography fontWeight={650} noWrap>{item.project.projectName}</Typography>
                      <Typography color="text.secondary" variant="caption">{item.project.projectCode}</Typography>
                    </Box>
                  </Box>
                  <ProjectManagementCountdown dueDate={item.project.dueDate} status={item.project.status} />
                </Box>
                <Box className="pm-project-selector-card__progress">
                  <ProjectManagementProgressBar dueDate={item.project.dueDate} progressPercent={item.taskProgressPercent} status={item.project.status} />
                </Box>
                <Box className="pm-project-selector-card__footer">
                  <span>{t('projectManagement.workbench.selector.owner')} · {item.project.ownerDisplayName ?? item.project.ownerUserId}</span>
                  <span>{projectManagementEnumLabel(t, 'status', item.project.status)} · {item.health}</span>
                </Box>
              </button>
            </Box>
          ))}
        </Box>
      </Box>
      <ProjectCreateDialog
        conflict={conflict}
        editing={Boolean(editState)}
        initialValue={editState?.value ?? newProject}
        onClose={closeDialog}
        onSubmit={(value) => {
          if (editState) {
            update.mutate({ id: editState.id, value: { ...value, versionNo: editState.value.versionNo } });
            return;
          }
          create.mutate(value);
        }}
        open={createOpen || Boolean(editState)}
        pending={create.isPending || update.isPending}
      />
    </Box>
  );
}

function isRequestCancelled(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  const name = 'name' in error ? String((error as { name?: unknown }).name ?? '') : '';
  return name === 'AbortError' || name === 'CancelledError' || name === 'CanceledError';
}
