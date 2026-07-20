import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

import { archiveProjectManagementProject, createProjectManagementProject, createProjectManagementProjectReminder, createProjectManagementProjectUpdate, createProjectManagementSavedView, deleteProjectManagementProject, deleteProjectManagementProjectSubscription, deleteProjectManagementSavedView, getProjectManagementHomeProjects, getProjectManagementHomeSummary, getProjectManagementMemberCandidates, getProjectManagementProjectSubscription, getProjectManagementSavedViews, saveProjectManagementProjectSubscription, updateProjectManagementProject } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementHomeFilterRule, ProjectManagementHomeProjectItem, ProjectManagementHomeQuery, ProjectManagementHomeSummaryResponse, ProjectManagementProjectReminderCreateRequest, ProjectManagementProjectUpsertRequest, ProjectManagementSavedView } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { isHttpError } from '../../../core/http/httpError';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useAuthStore } from '../../../core/state';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { Page403 } from '../../../shared/status/Page403';
import { PmActiveFilterBar, PmBox, PmBreadcrumbs, PmButton, PmDivider, PmDrawer, PmEntityContextMenu, PmFormInput, PmFormSelect, PmIcon, PmMenuItem, PmPage, PmPane, PmPopover, PmProjectTable, PmSkeletonRows, PmSurface, PmTab, PmTabs, PmText, PmStack, usePmMediaQuery, type PmContextMenuItem, type PmProjectTableColumn, type PmProjectTableRow } from '../../../ui/project-management';
import { ProjectCreateDialog } from '../project-create/ProjectCreateDialog';
import { readProjectManagementProjectConflict, type ProjectManagementProjectConflict } from '../project-create/projectManagementProjectConflict';
import { ProjectManagementLabelDialog } from '../project-overview/ProjectManagementLabelDialog';
import { ProjectManagementProjectUpdateDialog } from '../project-overview/ProjectManagementProjectUpdateDialog';
import { projectCenterPreferenceKey, readProjectCenterPreferences, rememberRecentProject, toggleProjectFavorite, writeProjectCenterPreferences, type ProjectCenterPreferences } from '../state/projectCenterPreferences';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { ProjectHomeFilterBuilder } from './ProjectHomeFilterBuilder';
import { ProjectHomeReminderDialog } from './ProjectHomeReminderDialog';
import { ProjectHomeSavedViews } from './ProjectHomeSavedViews';
import { parseProjectHomeUrlState, projectHomeQuery, serializeProjectHomeFilter, updateProjectHomeParam, type ProjectHomeDensity, type ProjectHomeInsightTab, type ProjectHomeUrlState } from './projectHomeState';
import { useProjectHomeRealtime } from './useProjectHomeRealtime';

const healthLabels: Record<string, string> = { Completed: 'projectManagement.home.health.Completed', UpdateMissing: 'projectManagement.home.health.UpdateMissing', AtRisk: 'projectManagement.home.health.AtRisk', OffTrack: 'projectManagement.home.health.OffTrack', OnTrack: 'projectManagement.home.health.OnTrack', NoUpdateExpected: 'projectManagement.home.health.NoUpdateExpected' };
const statusLabels: Record<string, string> = { Planning: 'projectManagement.home.status.Planning', Active: 'projectManagement.home.status.Active', Paused: 'projectManagement.home.status.Paused', Completed: 'projectManagement.home.status.Completed', Canceled: 'projectManagement.home.status.Canceled', Archived: 'projectManagement.home.status.Archived' };
const priorityLabels: Record<string, string> = { Low: 'projectManagement.home.priority.Low', Medium: 'projectManagement.home.priority.Medium', High: 'projectManagement.home.priority.High', Urgent: 'projectManagement.home.priority.Urgent' };
const emptyForm: ProjectManagementProjectUpsertRequest = { projectCode: '', projectName: '', status: 'Planning', priority: 'Medium', progressPercent: 0, versionNo: 0 };
const columnKeys = ['name', 'health', 'priority', 'lead', 'targetDate', 'issues', 'status'] as const;

export function ProjectHomeShell() {
  const scope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore(state => state.user?.userId ?? '');
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { translate } = useI18n();
  const { hasPermission: canAdd } = usePermission('project-management:project:add');
  const { hasPermission: canEdit } = usePermission('project-management:project:edit');
  const { hasPermission: canArchive } = usePermission('project-management:project:archive');
  const { hasPermission: canDelete } = usePermission('project-management:project:delete');
  const { hasPermission: canViewMembers } = usePermission('project-management:member:view');
  const { hasPermission: canManageLabels } = usePermission('project-management:label:manage');
  const confirm = useConfirm();
  const message = useMessage();
  const state = useMemo(() => parseProjectHomeUrlState(searchParams), [searchParams]);
  const [filterAnchor, setFilterAnchor] = useState<HTMLElement | null>(null);
  const [displayAnchor, setDisplayAnchor] = useState<HTMLElement | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ProjectManagementHomeProjectItem | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [conflict, setConflict] = useState<ProjectManagementProjectConflict | null>(null);
  const [selected, setSelected] = useState<Set<string>>(() => new Set());
  const [activeProject, setActiveProject] = useState<ProjectManagementHomeProjectItem | null>(null);
  const [context, setContext] = useState<{ top: number; left: number } | null>(null);
  const [contextRow, setContextRow] = useState<ProjectManagementHomeProjectItem | null>(null);
  const [commentProject, setCommentProject] = useState<ProjectManagementHomeProjectItem | null>(null);
  const [reminderProject, setReminderProject] = useState<ProjectManagementHomeProjectItem | null>(null);
  const [labelProject, setLabelProject] = useState<ProjectManagementHomeProjectItem | null>(null);
  const [batchPending, setBatchPending] = useState(false);
  const mediumViewport = usePmMediaQuery('(max-width: 1439px)');
  const narrowViewport = usePmMediaQuery('(max-width: 1023px)');
  const focusIndex = useRef(0);
  const preferenceKey = projectCenterPreferenceKey(userId, scope.tenantId, scope.appCode);
  const [preferences, setPreferences] = useState<ProjectCenterPreferences>(() => readProjectCenterPreferences(preferenceKey));
  const scrollStorageKey = `pm-home-scroll:${scope.tenantId}:${scope.appCode}:${state.view}:${searchParams.toString()}`;
  const initialScrollTop = useMemo(() => {
    try {
      if (typeof sessionStorage === 'undefined') return 0;
      const value = Number(sessionStorage.getItem(scrollStorageKey));
      return Number.isFinite(value) && value > 0 ? value : 0;
    } catch {
      return 0;
    }
  }, [scrollStorageKey]);
  const projectIds = state.collection === 'all' ? undefined : preferences.favoriteProjectIds.length > 0 || preferences.recentProjectIds.length > 0
    ? (state.collection === 'favorites' ? preferences.favoriteProjectIds : preferences.recentProjectIds).join(',') || '__none__'
    : '__none__';
  const query = useMemo<ProjectManagementHomeQuery>(() => projectHomeQuery(state, userId, projectIds), [projectIds, state, userId]);
  const projectsQuery = useQuery({ enabled: scope.isAvailable, queryKey: projectManagementQueryKeys.homeProjects(scope, query), queryFn: ({ signal }) => getProjectManagementHomeProjects(query, signal) });
  // The selected project is the authoritative source for the context panel.
  // URL state persists the user's preference, but must not delay the immediate
  // open caused by clicking a project row.
  const panelOpen = Boolean(activeProject);
  const summaryQuery = useQuery({ enabled: scope.isAvailable && panelOpen, queryKey: projectManagementQueryKeys.homeSummary(scope, query), queryFn: ({ signal }) => getProjectManagementHomeSummary(query, signal) });
  const savedViewsQuery = useQuery({ enabled: scope.isAvailable, queryKey: projectManagementQueryKeys.savedViews(scope, '__home__'), queryFn: ({ signal }) => getProjectManagementSavedViews('__home__', signal) });
  const subscriptionQuery = useQuery({ enabled: scope.isAvailable && Boolean(contextRow), queryKey: projectManagementQueryKeys.projectSubscription(scope, contextRow?.id ?? ''), queryFn: ({ signal }) => getProjectManagementProjectSubscription(contextRow!.id, signal) });
  const memberCandidatesQuery = useQuery({ enabled: scope.isAvailable && (Boolean(filterAnchor) || Boolean(state.leadUserId) || state.filter.rules.some(rule => rule.field === 'lead' || rule.field === 'members')), queryKey: projectManagementQueryKeys.memberCandidates(scope, { pageIndex: 1, pageSize: 100 }), queryFn: ({ signal }) => getProjectManagementMemberCandidates({ pageIndex: 1, pageSize: 100 }, signal) });
  const saveViewMutation = useApiMutation({ mutationFn: (request: { viewName: string; queryJson: string }) => createProjectManagementSavedView('__home__', { ...request, viewKey: 'home' }), onSuccess: () => { void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.savedViews(scope, '__home__') }); message.success(translate('projectManagement.home.saveViewSuccess')); } });
  const deleteViewMutation = useApiMutation({ mutationFn: (view: ProjectManagementSavedView) => deleteProjectManagementSavedView('__home__', view.id, view.versionNo), onSuccess: () => { void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.savedViews(scope, '__home__') }); } });
  const realtime = useProjectHomeRealtime('/hubs/system-notification', scope, scope.isAvailable);
  const allRows = projectsQuery.data?.data.items ?? [];
  const rows = state.collection === 'recent' ? [...allRows].sort((a, b) => preferences.recentProjectIds.indexOf(a.id) - preferences.recentProjectIds.indexOf(b.id)) : allRows;
  const activeFilterCount = Number(Boolean(state.keyword)) + Number(Boolean(state.status)) + Number(Boolean(state.priority)) + Number(Boolean(state.health)) + state.filter.rules.length;
  const refresh = useCallback(() => { void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.homeProjectsRoot(scope) }); void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.homeSummaryRoot(scope) }); }, [queryClient, scope]);
  const setParam = useCallback((name: string, value?: string) => setSearchParams(updateProjectHomeParam(searchParams, name, value)), [searchParams, setSearchParams]);
  const activeFilterItems = buildActiveFilterItems(state, memberCandidatesQuery.data?.data.items ?? [], translate, setParam);
  const savePreference = useCallback((next: ProjectCenterPreferences) => { setPreferences(next); writeProjectCenterPreferences(preferenceKey, next); }, [preferenceKey]);
  const closeDialog = () => { setDialogOpen(false); setEditing(null); setForm(emptyForm); setConflict(null); };
  const saveMutation = useApiMutation({ mutationFn: (value: ProjectManagementProjectUpsertRequest) => editing ? updateProjectManagementProject(editing.id, value) : createProjectManagementProject(value), onError: error => { setConflict(readProjectManagementProjectConflict(error)); message.error(isHttpError(error) && error.status === 409 ? translate('projectManagement.home.conflict') : editing ? translate('projectManagement.home.updateFailed') : translate('projectManagement.home.createFailed')); }, onSuccess: () => { message.success(editing ? translate('projectManagement.home.updateSuccess') : translate('projectManagement.home.createSuccess')); closeDialog(); refresh(); } });
  const archiveMutation = useApiMutation({ mutationFn: (row: ProjectManagementHomeProjectItem) => archiveProjectManagementProject(row.id, row.versionNo), onSuccess: refresh, onError: () => message.error(translate('projectManagement.home.archiveFailed')) });
  const deleteMutation = useApiMutation({ mutationFn: (row: ProjectManagementHomeProjectItem) => deleteProjectManagementProject(row.id, row.versionNo), onSuccess: refresh, onError: () => message.error(translate('projectManagement.home.deleteFailed')) });
  const openProject = (row: ProjectManagementHomeProjectItem) => { savePreference(rememberRecentProject(preferences, row.id)); navigate(`/platform/project-management/projects/${encodeURIComponent(row.id)}/overview?sourceView=${encodeURIComponent(state.view)}&sourceParams=${encodeURIComponent(searchParams.toString())}`); };
  const selectProject = (row: ProjectManagementHomeProjectItem) => { setActiveProject(row); if (!state.insights) setParam('insights', 'true'); };
  const closeProjectPanel = () => { setActiveProject(null); if (state.insights) setParam('insights', 'false'); };
  const toggleInsights = () => { if (panelOpen) { closeProjectPanel(); return; } const project = activeProject ?? rows[0]; if (project) selectProject(project); };
  const createSavedView = (name: string) => saveViewMutation.mutate({ viewName: name, queryJson: JSON.stringify({ version: 1, viewKey: 'home', collection: state.collection, view: state.view, filter: query.filter ? JSON.parse(query.filter) : { conjunction: 'and', rules: [] }, sortBy: state.sortBy, sortDirection: state.sortDirection, columns: state.columns, density: state.density, insights: state.insights, insightsTab: state.insightsTab }) });
  const applySavedView = (view: ProjectManagementSavedView) => { try { const saved = JSON.parse(view.queryJson) as Record<string, unknown>; const next = new URLSearchParams(); for (const key of ['collection', 'view', 'sortBy', 'sortDirection', 'density', 'insights', 'insightsTab']) { const value = saved[key]; if (value !== undefined && value !== null) next.set(key, String(value)); } if (saved.filter) next.set('filter', JSON.stringify(saved.filter)); if (Array.isArray(saved.columns)) next.set('columns', saved.columns.join(',')); setSearchParams(next); } catch { message.error(translate('projectManagement.home.savedViewInvalid')); } };
  const openCreate = () => { if (!canAdd) return; setEditing(null); setForm(emptyForm); setDialogOpen(true); };
  const openEdit = (row: ProjectManagementHomeProjectItem) => { if (!canEdit) return; setEditing(row); setForm({ projectCode: row.projectCode, projectName: row.projectName, status: row.status, priority: row.priority, ownerUserId: row.ownerUserId, startDate: row.startDate, dueDate: row.targetDate, progressPercent: row.progressPercent, versionNo: row.versionNo }); setDialogOpen(true); };
  const toggleSelected = (id: string) => setSelected(current => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; });
  const toggleAll = () => setSelected(selected.size === rows.length ? new Set() : new Set(rows.map(row => row.id)));
  const runBatch = async (operation: 'favorite' | 'archive' | 'delete' | 'status' | 'priority', value?: string) => {
    const selectedRows = rows.filter(row => selected.has(row.id));
    if (selectedRows.length === 0 || batchPending) return;
    setBatchPending(true);
    try {
      if (operation === 'favorite') {
        savePreference(selectedRows.reduce((current, row) => toggleProjectFavorite(current, row.id), preferences));
        return;
      }
      const results = await Promise.allSettled(selectedRows.map(row => operation === 'archive'
        ? archiveProjectManagementProject(row.id, row.versionNo)
        : operation === 'delete'
          ? deleteProjectManagementProject(row.id, row.versionNo)
          : updateProjectManagementProject(row.id, { projectCode: row.projectCode, projectName: row.projectName, status: operation === 'status' ? value ?? row.status : row.status, priority: operation === 'priority' ? value ?? row.priority : row.priority, ownerUserId: row.ownerUserId, progressPercent: row.progressPercent, versionNo: row.versionNo })));
      const succeeded = results.filter(result => result.status === 'fulfilled').length;
      message.success(formatMessage(translate('projectManagement.home.batchResult'), { succeeded, total: selectedRows.length }));
      refresh();
      setSelected(new Set());
    } finally {
      setBatchPending(false);
    }
  };
  const contextUpdateMutation = useApiMutation({ mutationFn: ({ row, patch }: { row: ProjectManagementHomeProjectItem; patch: Partial<ProjectManagementProjectUpsertRequest> }) => updateProjectManagementProject(row.id, { projectCode: row.projectCode, projectName: row.projectName, status: row.status, priority: row.priority, ownerUserId: row.ownerUserId, progressPercent: row.progressPercent, versionNo: row.versionNo, ...patch }), onSuccess: refresh, onError: error => message.error(isHttpError(error) && error.status === 409 ? translate('projectManagement.home.conflict') : translate('projectManagement.home.updateFailed')) });
  const commentMutation = useApiMutation({ mutationFn: ({ row, body }: { row: ProjectManagementHomeProjectItem; body: string }) => createProjectManagementProjectUpdate(row.id, { body, clientMutationId: typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}` }), onSuccess: () => { setCommentProject(null); message.success(translate('projectManagement.home.updatePosted')); refresh(); }, onError: () => message.error(translate('projectManagement.home.updatePostFailed')) });
  const subscriptionMutation = useApiMutation({ mutationFn: ({ row, subscribed, versionNo }: { row: ProjectManagementHomeProjectItem; subscribed: boolean; versionNo?: number }) => subscribed ? deleteProjectManagementProjectSubscription(row.id, versionNo) : saveProjectManagementProjectSubscription(row.id, { mode: 'AllUpdates', versionNo }), onSuccess: () => { void queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.projectSubscription(scope, contextRow?.id ?? '') }); message.success(translate('projectManagement.home.context.subscribeSuccess')); }, onError: () => message.error(translate('projectManagement.home.context.subscribeFailed')) });
  const reminderMutation = useApiMutation({ mutationFn: ({ row, request }: { row: ProjectManagementHomeProjectItem; request: ProjectManagementProjectReminderCreateRequest }) => createProjectManagementProjectReminder(row.id, request), onSuccess: () => { setReminderProject(null); message.success(translate('projectManagement.home.reminder.success')); }, onError: () => message.error(translate('projectManagement.home.reminder.failed')) });
  const contextItems: PmContextMenuItem[] = contextRow ? selected.size > 1 ? [
    ...(canEdit ? [{ id: 'batch-status', label: translate('projectManagement.home.context.status'), icon: <PmIcon name="check" size={15} />, children: ['Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'].map(status => ({ id: `batch-status-${status}`, label: translate(statusLabels[status]), action: `batch-status:${status}` })) }] : []),
    ...(canEdit ? [{ id: 'batch-priority', label: translate('projectManagement.home.context.priority'), icon: <PmIcon name="alert" size={15} />, children: ['Low', 'Medium', 'High', 'Urgent'].map(priority => ({ id: `batch-priority-${priority}`, label: translate(priorityLabels[priority]), action: `batch-priority:${priority}` })) }] : []),
    ...(canArchive ? [{ id: 'batch-archive', label: translate('projectManagement.home.archive'), icon: <PmIcon name="archive" size={15} />, action: 'archive', disabled: rows.filter(row => selected.has(row.id)).every(row => row.status === 'Archived') }] : []),
    ...(canDelete ? [{ id: 'batch-delete', label: translate('projectManagement.home.delete'), icon: <PmIcon name="trash" size={15} />, action: 'delete', danger: true }] : []),
  ] : [
    { id: 'status', label: translate('projectManagement.home.context.status'), icon: <PmIcon name="check" size={15} />, shortcut: 'P then S', disabled: !canEdit || contextRow.status === 'Archived', children: ['Planning', 'Active', 'Paused', 'Completed', 'Canceled', 'Archived'].map(status => ({ id: `status-${status}`, label: translate(statusLabels[status]), action: `status:${status}` })) },
    { id: 'priority', label: translate('projectManagement.home.context.priority'), icon: <PmIcon name="alert" size={15} />, shortcut: 'P then P', disabled: !canEdit || contextRow.status === 'Archived', children: ['Low', 'Medium', 'High', 'Urgent'].map(priority => ({ id: `priority-${priority}`, label: translate(priorityLabels[priority]), action: `priority:${priority}` })) },
    { id: 'lead', label: translate('projectManagement.home.context.lead'), icon: <PmIcon name="user" size={15} />, shortcut: 'P then A', action: 'edit', disabled: !canEdit || contextRow.status === 'Archived' },
    { id: 'members', label: translate('projectManagement.home.context.members'), icon: <PmIcon name="users" size={15} />, shortcut: 'P then M', action: 'members', disabled: !canViewMembers },
    { id: 'start-date', label: translate('projectManagement.home.context.startDate'), shortcut: 'Ctrl/Alt + S', action: 'edit', disabled: !canEdit || contextRow.status === 'Archived' },
    { id: 'target-date', label: translate('projectManagement.home.context.targetDate'), shortcut: 'Ctrl/Alt + D', action: 'edit', disabled: !canEdit || contextRow.status === 'Archived' },
    { id: 'labels', label: translate('projectManagement.home.context.labels'), icon: <PmIcon name="tag" size={15} />, shortcut: 'P then L', action: 'labels', disabled: !canManageLabels || contextRow.status === 'Archived' },
    { id: 'more-properties', label: translate('projectManagement.home.context.moreProperties'), icon: <PmIcon name="settings" size={15} />, action: 'edit', disabled: !canEdit || contextRow.status === 'Archived' },
    { id: 'separator-properties', label: '', separator: true },
    { id: 'copy', label: translate('projectManagement.home.context.copy'), icon: <PmIcon name="copy" size={15} />, children: [{ id: 'copy-url', label: translate('projectManagement.home.context.copyUrl'), action: 'copy-url' }, { id: 'copy-title', label: translate('projectManagement.home.context.copyTitle'), action: 'copy-title' }] },
    { id: 'move', label: translate('projectManagement.home.context.move'), action: 'unsupported-move', disabled: true },
    { id: 'open-in', label: translate('projectManagement.home.context.openIn'), icon: <PmIcon name="external" size={15} />, children: [{ id: 'open-overview', label: translate('projectManagement.home.context.openOverview'), action: 'open' }, { id: 'open-activity', label: translate('projectManagement.home.context.openActivity'), action: 'open-activity' }, { id: 'open-issues', label: translate('projectManagement.home.context.openIssues'), action: 'open-issues' }] },
    { id: 'separator-open', label: '', separator: true },
    { id: 'favorite', label: translate('projectManagement.home.favorite'), icon: <PmIcon name="star" size={15} />, shortcut: 'Alt + F', action: 'favorite' },
    { id: 'subscribe', label: subscriptionQuery.data?.data ? translate('projectManagement.home.context.unsubscribe') : translate('projectManagement.home.context.subscribe'), icon: <PmIcon name="bell" size={15} />, action: 'subscribe', disabled: subscriptionQuery.isLoading },
    { id: 'remind', label: translate('projectManagement.home.context.remind'), icon: <PmIcon name="clock" size={15} />, action: 'remind' },
    { id: 'comment', label: translate('projectManagement.home.context.comment'), icon: <PmIcon name="message" size={15} />, shortcut: 'N then C', action: 'comment', disabled: !canEdit },
    { id: 'separator-danger', label: '', separator: true },
    ...(canArchive ? [{ id: 'archive', label: translate('projectManagement.home.archive'), icon: <PmIcon name="archive" size={15} />, action: 'archive', disabled: contextRow.status === 'Archived' }] : []),
    ...(canDelete ? [{ id: 'delete', label: translate('projectManagement.home.delete'), icon: <PmIcon name="trash" size={15} />, action: 'delete', danger: true }] : []),
  ] : [];

  useEffect(() => setPreferences(readProjectCenterPreferences(preferenceKey)), [preferenceKey]);
  useEffect(() => {
    if (!activeProject || projectsQuery.isFetching || !projectsQuery.data) return;
    const current = rows.find(row => row.id === activeProject.id);
    if (!current) setActiveProject(null);
    else if (current !== activeProject) setActiveProject(current);
  }, [activeProject, projectsQuery.data, projectsQuery.isFetching, rows]);
  useEffect(() => { const onKeyDown = (event: KeyboardEvent) => { if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement || event.target instanceof HTMLSelectElement) return; if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'a') { event.preventDefault(); toggleAll(); } if (event.key === 'c') { event.preventDefault(); openCreate(); } if (event.key === 'Escape') { setSelected(new Set()); setContext(null); closeProjectPanel(); } if (event.key === 'ArrowDown') { focusIndex.current = Math.min(focusIndex.current + 1, Math.max(rows.length - 1, 0)); document.querySelector<HTMLElement>(`[data-home-row="${focusIndex.current}"]`)?.focus(); } if (event.key === 'ArrowUp') { focusIndex.current = Math.max(focusIndex.current - 1, 0); document.querySelector<HTMLElement>(`[data-home-row="${focusIndex.current}"]`)?.focus(); } if (event.key === 'Enter') { const row = rows[focusIndex.current]; if (row) openProject(row); } if (event.key === 'F10' && event.shiftKey) { const row = rows[focusIndex.current]; if (row) { setContextRow(row); setContext({ top: 120, left: 260 }); } } }; window.addEventListener('keydown', onKeyDown); return () => window.removeEventListener('keydown', onKeyDown); });

  if (!scope.isAvailable) return <Page403 />;
  const noResults = !projectsQuery.isLoading && rows.length === 0;
  const visibleColumns = state.columns.filter(column => columnKeys.includes(column as typeof columnKeys[number])) as PmProjectTableColumn[];
  const tableRows: PmProjectTableRow[] = rows.map(row => ({
    id: row.id,
    projectName: row.projectName,
    health: row.health,
    healthLabel: translate(healthLabels[row.health] || row.health),
    priorityLabel: translate(priorityLabels[row.priority] || row.priority),
    leadLabel: row.ownerDisplayName || translate('projectManagement.home.unassigned'),
    targetDateLabel: row.targetDate ? new Date(row.targetDate).toLocaleDateString() : '—',
    issueCount: row.openIssueCount,
    statusLabel: translate(statusLabels[row.status] || row.status),
    status: row.status,
  }));
  const tableLabels = {
    name: translate('projectManagement.home.name'),
    health: translate('projectManagement.home.health'),
    priority: translate('projectManagement.home.priority'),
    lead: translate('projectManagement.home.lead'),
    targetDate: translate('projectManagement.home.targetDate'),
    issues: translate('projectManagement.home.issues'),
    status: translate('projectManagement.home.status'),
    selectAll: translate('projectManagement.home.selectAll'),
    favorite: translate('projectManagement.home.favorite'),
    edit: translate('projectManagement.home.edit'),
    archive: translate('projectManagement.home.archive'),
    delete: translate('projectManagement.home.delete'),
  };
  const sortTable = (field: PmProjectTableColumn) => {
    const direction = state.sortBy === field && state.sortDirection === 'asc' ? 'desc' : 'asc';
    const next = new URLSearchParams(searchParams);
    next.set('sort', field);
    next.set('order', direction);
    setSearchParams(next);
  };
  const submit = (value: ProjectManagementProjectUpsertRequest) => { setForm(value); setConflict(null); saveMutation.mutate(value); };
  const content = projectsQuery.isLoading
    ? <PmSkeletonRows count={8} />
    : projectsQuery.isError
      ? isHttpError(projectsQuery.error) && projectsQuery.error.status === 403 ? <Page403 /> : <State title={translate('projectManagement.home.loadingFailed')} action={<PmButton onClick={() => void projectsQuery.refetch()}>{translate('projectManagement.home.retry')}</PmButton>} />
      : noResults
        ? <State title={activeFilterCount > 0 ? translate('projectManagement.home.noResults') : translate('projectManagement.home.empty')} action={canAdd ? <PmButton onClick={openCreate}>{translate('projectManagement.home.create')}</PmButton> : undefined} />
        : <PmBox onClick={event => { if (event.target === event.currentTarget) closeProjectPanel(); }} sx={{ mt: 1, minHeight: 0 }}>
          <PmProjectTable
            columns={visibleColumns}
            density={state.density}
            labels={tableLabels}
            onArchive={canArchive ? row => archiveMutation.mutate(rows.find(item => item.id === row.id)!) : undefined}
            onContext={(row, event) => { if (!selected.has(row.id)) setSelected(new Set([row.id])); setContextRow(rows.find(item => item.id === row.id) ?? null); setContext({ top: event.clientY, left: event.clientX }); }}
            onDelete={canDelete ? row => { const source = rows.find(item => item.id === row.id); if (source) void confirm({ title: translate('projectManagement.home.deleteTitle'), content: formatMessage(translate('projectManagement.home.deleteDescription'), { name: source.projectName }), confirmText: translate('projectManagement.home.delete'), onConfirm: () => deleteMutation.mutate(source) }); } : undefined}
            onEdit={canEdit ? row => { const source = rows.find(item => item.id === row.id); if (source) openEdit(source); } : undefined}
            onFavorite={row => { const source = rows.find(item => item.id === row.id); if (source) savePreference(toggleProjectFavorite(preferences, source.id)); }}
            onOpen={row => { const source = rows.find(item => item.id === row.id); if (source) openProject(source); }}
            onRowSelect={row => { const source = rows.find(item => item.id === row.id); if (source) selectProject(source); }}
            onSort={sortTable}
            onToggleAll={toggleAll}
            onToggleRow={toggleSelected}
            initialScrollTop={initialScrollTop}
            onViewportScroll={scrollTop => {
              try {
                if (typeof sessionStorage !== 'undefined') sessionStorage.setItem(scrollStorageKey, String(scrollTop));
              } catch {
                // Session storage can be unavailable in privacy mode; scrolling must remain functional.
              }
            }}
            rows={tableRows}
            selectedIds={selected}
            sort={{ field: state.sortBy, direction: state.sortDirection }}
          />
         </PmBox>;
  return <PmPage>
    <PmPane sx={{ flex: 1, overflow: 'auto' }}><PmSurface data-home-surface="true" onClick={event => {
      const target = event.target instanceof Element ? event.target : null;
      if (!target?.closest('[data-project-row], [data-home-interactive="true"], button, input, select, textarea, a')) closeProjectPanel();
    }} sx={{ minWidth: 0, minHeight: '100%', px: { xs: 1.5, sm: 2.5, lg: 3 } }}>
      <PmBreadcrumbs items={[{ icon: <PmIcon name="briefcase" size={15} />, label: translate('projectManagement.home.title'), onClick: () => navigate('/platform/project-management') }, { icon: <PmIcon name="layers" size={15} />, label: homeViewLabel(state.view, translate), current: true }]} />
      <PmDivider />
      <PmBox data-home-interactive="true" sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', py: 2 }}><PmText variant="h1" fontSize="1.35rem" fontWeight={700}>{translate('projectManagement.home.title')}</PmText>{canAdd && <PmButton onClick={openCreate} startIcon={<PmIcon name="plus" />} variant="contained">{translate('projectManagement.home.create')}</PmButton>}</PmBox>
      <PmBox data-home-interactive="true" sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}><PmTabs onChange={(_, value) => setParam('collection', String(value))} value={state.collection}><PmTab label={translate('projectManagement.home.allProjects')} value="all" /><PmTab label={translate('projectManagement.home.favorites')} value="favorites" /><PmTab label={translate('projectManagement.home.recent')} value="recent" /></PmTabs></PmBox>
      <PmBox data-home-interactive="true" sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2, flexWrap: 'wrap', py: 1 }}>
        <PmBox sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}><PmButton onClick={event => setFilterAnchor(event.currentTarget)} startIcon={<PmIcon name="filter" />}>{translate('projectManagement.home.filter')}{activeFilterCount > 0 ? ` · ${activeFilterCount}` : ''}</PmButton><PmText color="text.secondary" fontSize=".76rem">{formatMessage(translate('projectManagement.home.resultCount'), { count: projectsQuery.data?.data.total ?? 0 })}</PmText></PmBox>
        <PmBox sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', width: { xs: '100%', md: 'auto' } }}><PmFormInput aria-label={translate('projectManagement.home.search')} value={state.keyword} onChange={event => setParam('keyword', event.target.value || undefined)} placeholder={translate('projectManagement.home.search')} size="small" sx={{ width: { xs: '100%', sm: 190 } }} /><PmButton onClick={event => setDisplayAnchor(event.currentTarget)} startIcon={<PmIcon name="settings" />}>{translate('projectManagement.home.display')}</PmButton><ProjectHomeSavedViews autoOpen={searchParams.get('saveView') === 'true'} onClose={() => setParam('saveView')} onApply={applySavedView} onCreate={createSavedView} onDelete={view => deleteViewMutation.mutate(view)} pending={saveViewMutation.isPending || deleteViewMutation.isPending} translate={translate} views={savedViewsQuery.data?.data ?? []} /><PmButton onClick={toggleInsights}>{translate('projectManagement.home.insights')}</PmButton></PmBox>
      </PmBox>
      <PmPopover anchorEl={filterAnchor} anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }} onClose={() => setFilterAnchor(null)} open={Boolean(filterAnchor)}><PmBox sx={{ p: 2, display: 'grid', gap: 1.25, minWidth: 270 }}><ProjectHomeFilterBuilder filter={state.filter} healthLabels={healthLabels} memberOptions={memberCandidatesQuery.data?.data.items} onChange={filter => setParam('filter', JSON.stringify(filter))} priorityLabels={priorityLabels} statusLabels={statusLabels} translate={translate} />{(state.status || state.priority || state.health || state.keyword || state.filter.rules.length > 0) && <PmButton onClick={() => { setSearchParams(new URLSearchParams({ collection: state.collection })); setFilterAnchor(null); }}>{translate('projectManagement.home.clearFilters')}</PmButton>}</PmBox></PmPopover>
      <PmPopover anchorEl={displayAnchor} onClose={() => setDisplayAnchor(null)} open={Boolean(displayAnchor)}><PmBox sx={{ p: 2, display: 'grid', gap: 1.25, minWidth: 250 }}><PmText fontSize=".75rem" fontWeight={700}>{translate('projectManagement.home.sort')}</PmText><PmFormSelect label={translate('projectManagement.home.sort')} value={state.sortBy} onChange={event => setParam('sort', String(event.target.value))}><PmMenuItem value="name">{translate('projectManagement.home.sort.name')}</PmMenuItem><PmMenuItem value="health">{translate('projectManagement.home.sort.health')}</PmMenuItem><PmMenuItem value="priority">{translate('projectManagement.home.sort.priority')}</PmMenuItem><PmMenuItem value="lead">{translate('projectManagement.home.sort.lead')}</PmMenuItem><PmMenuItem value="targetDate">{translate('projectManagement.home.sort.targetDate')}</PmMenuItem><PmMenuItem value="issues">{translate('projectManagement.home.sort.issues')}</PmMenuItem><PmMenuItem value="updated">{translate('projectManagement.home.sort.updated')}</PmMenuItem></PmFormSelect><PmFormSelect label={translate('projectManagement.home.order')} value={state.sortDirection} onChange={event => setParam('order', String(event.target.value))}><PmMenuItem value="asc">{translate('projectManagement.home.ascending')}</PmMenuItem><PmMenuItem value="desc">{translate('projectManagement.home.descending')}</PmMenuItem></PmFormSelect><PmText fontSize=".75rem" fontWeight={700}>{translate('projectManagement.home.columns')}</PmText>{columnKeys.filter(column => column !== 'name').map(column => <PmBox component="label" key={column} sx={{ display: 'flex', alignItems: 'center', gap: 1, fontSize: 13 }}><input checked={visibleColumns.includes(column)} onChange={() => { const next = visibleColumns.includes(column) ? visibleColumns.filter(item => item !== column) : [...visibleColumns, column]; setParam('columns', next.join(',')); }} type="checkbox" />{translate(`projectManagement.home.${column}`)}</PmBox>)}<PmFormSelect label={translate('projectManagement.home.density')} value={state.density} onChange={event => setParam('density', String(event.target.value) as ProjectHomeDensity)}><PmMenuItem value="compact">{translate('projectManagement.home.compact')}</PmMenuItem><PmMenuItem value="default">{translate('projectManagement.home.defaultDensity')}</PmMenuItem><PmMenuItem value="comfortable">{translate('projectManagement.home.comfortable')}</PmMenuItem></PmFormSelect></PmBox></PmPopover>
      <PmActiveFilterBar clearLabel={translate('projectManagement.home.clearFilters')} items={activeFilterItems} onClear={() => { const next = new URLSearchParams(searchParams); ['keyword', 'search', 'health', 'priority', 'leadUserId', 'status', 'targetDateFrom', 'targetDateTo', 'filter'].forEach(key => next.delete(key)); setSearchParams(next); }} />
      {selected.size > 0 && <PmBox sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 1, px: 1.5, bgcolor: 'primary.main', color: 'primary.contrastText', borderRadius: 1 }}><PmText fontSize=".78rem">{selected.size}</PmText><PmButton color="inherit" disabled={batchPending} onClick={() => void runBatch('favorite')}>{translate('projectManagement.home.favorite')}</PmButton>{canArchive && <PmButton color="inherit" disabled={batchPending} onClick={() => confirm({ title: translate('projectManagement.home.archive'), content: translate('projectManagement.home.batchArchiveConfirm'), confirmText: translate('projectManagement.home.archive'), onConfirm: () => runBatch('archive') })}>{translate('projectManagement.home.archive')}</PmButton>}{canDelete && <PmButton color="inherit" disabled={batchPending} onClick={() => confirm({ title: translate('projectManagement.home.delete'), content: translate('projectManagement.home.batchDeleteConfirm'), confirmText: translate('projectManagement.home.delete'), onConfirm: () => runBatch('delete') })}>{translate('projectManagement.home.delete')}</PmButton>}<PmButton color="inherit" disabled={batchPending} onClick={() => setSelected(new Set())}>{translate('projectManagement.home.clearSelection')}</PmButton></PmBox>}
      {content}
    </PmSurface></PmPane>
    {panelOpen && !narrowViewport && <Insights compact={mediumViewport} project={activeProject} insightTab={state.insightsTab} setInsightTab={value => setParam('insightsTab', value)} summaryQuery={summaryQuery} setParam={setParam} translate={translate} connectionState={realtime.connectionState} />}
    {panelOpen && narrowViewport && <PmDrawer open onClose={closeProjectPanel}><Insights compact project={activeProject} insightTab={state.insightsTab} setInsightTab={value => setParam('insightsTab', value)} summaryQuery={summaryQuery} setParam={setParam} translate={translate} connectionState={realtime.connectionState} /></PmDrawer>}
  <PmEntityContextMenu anchorPosition={context} items={contextItems} onClose={() => setContext(null)} onAction={action => { if (!contextRow) return; if (action === 'edit') openEdit(contextRow); else if (action === 'members') navigate(`/platform/project-management/projects/${encodeURIComponent(contextRow.id)}/members`); else if (action === 'labels') setLabelProject(contextRow); else if (action === 'favorite') savePreference(toggleProjectFavorite(preferences, contextRow.id)); else if (action === 'subscribe') subscriptionMutation.mutate({ row: contextRow, subscribed: Boolean(subscriptionQuery.data?.data), versionNo: subscriptionQuery.data?.data?.versionNo }); else if (action === 'remind') setReminderProject(contextRow); else if (action === 'archive') { if (selected.size > 1) void runBatch('archive'); else archiveMutation.mutate(contextRow); } else if (action === 'delete') { if (selected.size > 1) void runBatch('delete'); else deleteMutation.mutate(contextRow); } else if (action === 'comment') setCommentProject(contextRow); else if (action.startsWith('status:')) contextUpdateMutation.mutate({ row: contextRow, patch: { status: action.slice(7) } }); else if (action.startsWith('priority:')) contextUpdateMutation.mutate({ row: contextRow, patch: { priority: action.slice(9) } }); else if (action.startsWith('batch-status:')) void runBatch('status', action.slice(13)); else if (action.startsWith('batch-priority:')) void runBatch('priority', action.slice(15)); else if (action === 'open') openProject(contextRow); else if (action === 'open-activity') navigate(`/platform/project-management/projects/${encodeURIComponent(contextRow.id)}/overview?sourceView=${encodeURIComponent(state.view)}&sourceParams=${encodeURIComponent(searchParams.toString())}&mainTab=activity`); else if (action === 'open-issues') navigate(`/platform/project-management/projects/${encodeURIComponent(contextRow.id)}/tasks`); else if (action === 'copy-title') void navigator.clipboard?.writeText(contextRow.projectName); else if (action === 'copy-url') void navigator.clipboard?.writeText(`${window.location.origin}/platform/project-management/projects/${encodeURIComponent(contextRow.id)}/overview?sourceView=${encodeURIComponent(state.view)}&sourceParams=${encodeURIComponent(searchParams.toString())}`); }} />
  <ProjectHomeReminderDialog open={Boolean(reminderProject)} projectName={reminderProject?.projectName ?? ''} pending={reminderMutation.isPending} onClose={() => setReminderProject(null)} onSubmit={(request: ProjectManagementProjectReminderCreateRequest) => { if (reminderProject) reminderMutation.mutate({ row: reminderProject, request }); }} />
    <ProjectManagementLabelDialog open={Boolean(labelProject)} projectId={labelProject?.id ?? ''} onClose={() => setLabelProject(null)} />
    <ProjectCreateDialog conflict={conflict} editing={Boolean(editing)} initialValue={form} onClose={closeDialog} onSubmit={submit} open={dialogOpen} pending={saveMutation.isPending} />
    <ProjectManagementProjectUpdateDialog onClose={() => setCommentProject(null)} onSubmit={body => { if (commentProject) commentMutation.mutate({ row: commentProject, body }); }} open={Boolean(commentProject)} pending={commentMutation.isPending} />
  </PmPage>;
}

function Insights({ compact, project, insightTab, setInsightTab, summaryQuery, setParam, translate, connectionState }: { compact?: boolean; project: ProjectManagementHomeProjectItem | null; insightTab: ProjectHomeInsightTab; setInsightTab: (value: ProjectHomeInsightTab) => void; summaryQuery: { isLoading: boolean; isError: boolean; error?: unknown; data?: { data: ProjectManagementHomeSummaryResponse }; refetch: () => unknown }; setParam: (name: string, value?: string) => void; translate: (key: string) => string; connectionState: string }) {
  const summary = summaryQuery.data?.data;
  const insightRows = insightTab === 'health'
    ? summary?.health.map(item => ({ key: item.key, label: translate(healthLabels[item.key] || item.key), count: item.count, onClick: () => setParam('health', item.key) })) ?? []
    : [...(summary?.leads.map(item => ({ key: item.userId || 'unassigned', label: item.displayName, count: item.count, onClick: item.userId ? () => setParam('leadUserId', item.userId) : undefined })) ?? []), { key: 'unassigned', label: translate('projectManagement.home.unassigned'), count: summary?.unassignedCount ?? 0, onClick: undefined }];
  const maximum = Math.max(1, ...insightRows.map(item => item.count));
  const statusRows = summary?.status?.map(item => ({ key: item.key, label: translate(statusLabels[item.key] || item.key), count: item.count, onClick: () => setParam('status', item.key) })) ?? [];
  return <PmPane data-project-context-panel="true" paneWidth={compact ? 300 : 360} sx={{ borderLeft: 1, borderColor: 'divider', height: '100%', overflow: 'auto' }}><PmSurface><PmText noWrap fontSize=".82rem" fontWeight={700} sx={{ display: 'block', mb: 1.5 }}>{project?.projectName}</PmText><PmTabs onChange={(_, value) => setInsightTab(String(value) as ProjectHomeInsightTab)} value={insightTab}><PmTab label={translate('projectManagement.home.health')} value="health" /><PmTab label={translate('projectManagement.home.leads')} value="leads" /></PmTabs><PmDivider sx={{ mb: 2 }} />{summaryQuery.isLoading ? <State title={translate('projectManagement.home.loading')} /> : summaryQuery.isError ? isHttpError(summaryQuery.error) && summaryQuery.error.status === 403 ? <Page403 /> : <State action={<PmButton onClick={() => void summaryQuery.refetch()}>{translate('projectManagement.home.retry')}</PmButton>} title={translate('projectManagement.home.loadingFailed')} /> : <><PmBox sx={{ display: 'flex', justifyContent: 'space-between', mb: 1.5 }}><PmText color="text.secondary" fontSize=".72rem">{translate('projectManagement.home.insightTotal')}</PmText><PmText fontSize=".82rem" fontWeight={700}>{summary?.total ?? 0}</PmText></PmBox>{insightRows.map(item => <InsightRow key={item.key} label={item.label} count={item.count} maximum={maximum} onClick={item.onClick} />)}{insightTab === 'health' && statusRows.length > 0 ? <><PmText color="text.secondary" fontSize=".7rem" sx={{ display: 'block', mt: 2, mb: .5 }}>{translate('projectManagement.home.status')}</PmText>{statusRows.map(item => <InsightRow key={`status-${item.key}`} label={item.label} count={item.count} maximum={Math.max(1, ...statusRows.map(row => row.count))} onClick={item.onClick} />)}</> : null}{summary?.updatedTime && <PmText color="text.secondary" fontSize=".7rem" sx={{ display: 'block', mt: 2 }}>{translate('projectManagement.home.insightUpdated')}: {new Date(summary.updatedTime).toLocaleString()}</PmText>}</>}{connectionState !== 'connected' && <PmText aria-live="polite" color="warning.main" fontSize=".72rem" sx={{ mt: 3 }}>{connectionState === 'reconnecting' ? translate('projectManagement.home.connection.reconnecting') : translate('projectManagement.home.connection.disconnected')}</PmText>}</PmSurface></PmPane>;
}

function InsightRow({ label, count, maximum, onClick }: { label: string; count: number; maximum: number; onClick?: () => void }) { return <PmBox component="button" onClick={onClick} sx={{ display: 'grid', gridTemplateColumns: 'minmax(0,1fr) auto', gap: .75, width: '100%', px: 1, py: 1.1, border: 0, borderBottom: 1, borderColor: 'divider', bgcolor: 'transparent', color: 'text.primary', textAlign: 'left', cursor: onClick ? 'pointer' : 'default', '&:hover': { bgcolor: 'action.hover' } }}><PmBox sx={{ minWidth: 0 }}><PmText noWrap fontSize=".8rem">{label}</PmText><PmBox sx={{ mt: .6, height: 4, borderRadius: 2, bgcolor: 'action.hover', overflow: 'hidden' }}><PmBox sx={{ width: `${Math.round((count / maximum) * 100)}%`, height: '100%', bgcolor: 'primary.main', borderRadius: 2 }} /></PmBox></PmBox><PmText fontSize=".8rem" fontWeight={700}>{count}</PmText></PmBox>; }
function State({ title, action }: { title: string; action?: ReactNode }) { return <PmStack sx={{ alignItems: 'center', justifyContent: 'center', minHeight: 280, gap: 1 }}><PmText color="text.secondary">{title}</PmText>{action}</PmStack>; }

function buildActiveFilterItems(state: ProjectHomeUrlState, members: Array<{ userId: string; displayName?: string; userName?: string }>, translate: (key: string) => string, setParam: (name: string, value?: string) => void) {
  const items: Array<{ id: string; label: string; onRemove: () => void }> = [];
  const add = (id: string, label: string, key: string) => items.push({ id, label, onRemove: () => setParam(key) });
  if (state.keyword) add('keyword', `${translate('projectManagement.home.search')}: ${state.keyword}`, 'keyword');
  if (state.health) add('health', `${translate('projectManagement.home.health')}: ${translate(healthLabels[state.health] ?? state.health)}`, 'health');
  if (state.priority) add('priority', `${translate('projectManagement.home.priority')}: ${translate(priorityLabels[state.priority] ?? state.priority)}`, 'priority');
  if (state.status) add('status', `${translate('projectManagement.home.status')}: ${translate(statusLabels[state.status] ?? state.status)}`, 'status');
  if (state.leadUserId) {
    const member = members.find(item => item.userId === state.leadUserId);
    add('leadUserId', `${translate('projectManagement.home.lead')}: ${member?.displayName || member?.userName || translate('projectManagement.home.selectedFilter')}`, 'leadUserId');
  }
  if (state.targetDateFrom) add('targetDateFrom', `${translate('projectManagement.home.field.targetDate')}: ≥ ${state.targetDateFrom}`, 'targetDateFrom');
  if (state.targetDateTo) add('targetDateTo', `${translate('projectManagement.home.field.targetDate')}: ≤ ${state.targetDateTo}`, 'targetDateTo');
  state.filter.rules.forEach((rule: ProjectManagementHomeFilterRule, index: number) => {
    const fieldLabel = translate(`projectManagement.home.filterField.${rule.field}`);
    const value = rule.field === 'health' ? rule.values.map(item => translate(healthLabels[item] ?? item)).join(', ') : rule.field === 'priority' ? rule.values.map(item => translate(priorityLabels[item] ?? item)).join(', ') : rule.field === 'status' ? rule.values.map(item => translate(statusLabels[item] ?? item)).join(', ') : rule.field === 'lead' || rule.field === 'members' ? rule.values.map(item => members.find(member => member.userId === item)?.displayName || members.find(member => member.userId === item)?.userName || translate('projectManagement.home.selectedFilter')).join(', ') : rule.values.join(', ');
    items.push({ id: `rule-${index}`, label: `${fieldLabel} ${translate(`projectManagement.home.filterOperator.${rule.operator}`)} ${value}`, onRemove: () => { const next = { conjunction: 'and' as const, rules: state.filter.rules.filter((_, ruleIndex) => ruleIndex !== index) }; setParam('filter', serializeProjectHomeFilter(next)); } });
  });
  return items;
}

function homeViewLabel(view: string, translate: (key: string) => string): string {
  const keys: Record<string, string> = {
    all: 'projectManagement.sidebar.allProjects',
    'my-projects': 'projectManagement.sidebar.myProjects',
    'due-this-week': 'projectManagement.sidebar.dueThisWeek',
    'at-risk': 'projectManagement.sidebar.atRisk',
    archived: 'projectManagement.sidebar.archived',
  };
  return translate(keys[view] ?? 'projectManagement.sidebar.allProjects');
}
