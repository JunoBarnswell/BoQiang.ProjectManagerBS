import { Box, Stack, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState, type CSSProperties, type DragEvent } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';

import {
  changeProjectManagementTaskStatus,
  createProjectManagementSavedView,
  getProjectManagementMilestones,
  getProjectManagementOverview,
  getProjectManagementSavedViews,
  getProjectManagementTaskDependencies,
  getProjectManagementTasks,
} from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementSavedViewUpsertRequest, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useDict } from '../../../shared/dict/useDict';
import { useMessage } from '../../../shared/feedback/useMessage';
import { DataTable } from '../../../shared/table/DataTable';
import { PmIcon } from '../../../ui/project-management';
import { ProjectManagementTaskCalendar } from '../calendar/ProjectManagementTaskCalendar';
import { ProjectManagementProgressBar } from '../components/ProjectManagementProgressBar';
import { ProjectManagementScheduleTimelineBar } from '../components/ProjectManagementScheduleTimelineBar';
import { ProjectManagementCountdown } from '../components/ProjectManagementCountdown';
import { updateGanttSchedule } from '../gantt/ganttSchedule.api';
import { ProjectManagementGanttView } from '../gantt/ProjectManagementGanttView';
import { useProjectManagementGlobalShortcuts } from '../interactions/useProjectManagementGlobalShortcuts';
import { projectManagementEnumLabel, useProjectManagementI18n } from '../projectManagementI18n';
import { useProjectManagementProjectRealtime } from '../realtime/useProjectManagementProjectRealtime';
import type { ProjectManagementPreferredView } from '../state/projectManagementInteractionPreferences';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import {
  canTransitionProjectManagementTaskStatus,
  getAllowedProjectManagementTaskStatuses,
  PROJECT_MANAGEMENT_TASK_STATUSES,
} from '../state/projectManagementStatusTransitions';
import { computeScheduleUrgencyMetrics } from '../state/projectManagementScheduleUrgency';
import { isTaskStatusRevisionConflict, patchTaskListCaches, patchTaskListCachesFromDetail } from '../state/projectManagementTaskStatusCache';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { ProjectScreenHeader, ProjectWorkbenchFrame } from './ProjectWorkbenchFrame';
import { ProjectWorkItemEditor } from './ProjectWorkItemEditor';

type RequirementView = 'tree' | 'flat' | 'card' | 'board' | 'gantt' | 'calendar';

const viewOptions: RequirementView[] = ['tree', 'flat', 'card', 'board', 'gantt', 'calendar'];

function toApiViewKey(view: RequirementView): 'tree' | 'list' | 'card' | 'board' | 'gantt' | 'calendar' {
  if (view === 'flat') return 'list';
  return view;
}

function fromPreferredView(view: ProjectManagementPreferredView): RequirementView {
  return view === 'list' ? 'flat' : view;
}

export function ProjectRequirementsScreen() {
  const { format, t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const message = useMessage();
  const queryClient = useQueryClient();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const [params, setParams] = useSearchParams();
  const requirementTypeOptions = useDict('pm_task_requirement_type');
  const view = (viewOptions.includes((params.get('view') ?? '') as RequirementView) ? params.get('view') : 'tree') as RequirementView;
  const keyword = params.get('keyword') ?? '';
  const status = params.get('status') ?? '';
  const risk = params.get('risk') ?? '';
  const requirementType = params.get('type') ?? '';
  const focusTaskId = params.get('taskId');
  const focusComments = params.get('focus') === 'comments';
  const [editorTaskId, setEditorTaskId] = useState<string>();
  const [editorOpen, setEditorOpen] = useState(params.get('create') === '1');
  const [editorStartDate, setEditorStartDate] = useState<string>();
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [selectedTaskIds, setSelectedTaskIds] = useState<Set<string>>(new Set());
  const [filterOpen, setFilterOpen] = useState(false);
  const [viewOpen, setViewOpen] = useState(false);
  const [viewName, setViewName] = useState('');
  const [refreshing, setRefreshing] = useState(false);
  const pageIndex = Number(params.get('page') ?? 1) || 1;
  const largePage = view === 'board' || view === 'card' || view === 'gantt' || view === 'calendar';
  const request = useMemo(() => ({
    projectId,
    pageIndex: largePage ? 1 : pageIndex,
    pageSize: largePage ? 200 : 20,
    viewKey: toApiViewKey(view),
    keyword: keyword || undefined,
    status: status && status !== 'mine' ? status : undefined,
    assigneeUserId: status === 'mine' ? scope.userId : undefined,
    riskLevel: risk || undefined,
    requirementType: requirementType || undefined,
    workItemType: 'Requirement',
    includeCompleted: true,
    sortBy: view === 'tree' ? 'tree' as const : 'updated' as const,
    sortDirection: view === 'tree' ? 'asc' as const : 'desc' as const,
  }), [keyword, largePage, pageIndex, projectId, requirementType, risk, scope.userId, status, view]);
  const requirementFilterOptions = useMemo(() => {
    const options = requirementTypeOptions.options.filter((option) => !option.disabled);
    if (!requirementType || options.some((option) => option.value === requirementType)) return options;
    return [{ label: requirementType, value: requirementType, disabled: true }, ...options];
  }, [requirementType, requirementTypeOptions.options]);
  const query = useQuery({ enabled: scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.tasks(scope, request), queryFn: ({ signal }) => getProjectManagementTasks(request, signal) });
  const project = useQuery({ enabled: scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }), queryFn: ({ signal }) => getProjectManagementOverview({ projectId, pageIndex: 1, pageSize: 1 }, signal) });
  const milestones = useQuery({ enabled: scope.isAvailable && Boolean(projectId) && (view === 'calendar' || view === 'gantt'), queryKey: projectManagementQueryKeys.milestones(scope, projectId), queryFn: ({ signal }) => getProjectManagementMilestones(projectId, signal) });
  const dependencies = useQuery({ enabled: scope.isAvailable && Boolean(projectId) && (view === 'calendar' || view === 'gantt'), queryKey: [...projectManagementQueryKeys.tasksProject(scope, projectId), 'dependencies'], queryFn: ({ signal }) => getProjectManagementTaskDependencies(projectId, signal) });
  const rows = query.data?.data.items ?? [];
  const projectItem = project.data?.data.items[0];
  const savedViews = useQuery({ enabled: viewOpen && Boolean(projectId), queryKey: projectManagementQueryKeys.savedViews(scope, projectId), queryFn: ({ signal }) => getProjectManagementSavedViews(projectId, signal) });
  const saveView = useMutation({
    mutationFn: (requestToSave: ProjectManagementSavedViewUpsertRequest) => createProjectManagementSavedView(projectId, requestToSave),
    onSuccess: () => { setViewName(''); void savedViews.refetch(); message.success(t('projectManagement.workItems.saved')); },
    onError: () => message.error(t('projectManagement.workItems.saveFailed')),
  });
  const updateUrl = (key: string, value: string, resetPage = true) => {
    setParams((current) => {
      if (value) current.set(key, value); else current.delete(key);
      if (resetPage) current.delete('page');
      return current;
    }, { replace: true });
  };
  const setView = (next: RequirementView) => updateUrl('view', next);
  const invalidate = () => Promise.all([
    queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.tasksProject(scope, projectId) }),
    queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.overview(scope, { projectId }) }),
  ]);
  const refresh = async () => {
    if (refreshing) return;
    setRefreshing(true);
    try {
      const [tasksResult, projectResult] = await Promise.all([
        query.refetch(),
        project.refetch(),
      ]);
      const failed = [tasksResult.error, projectResult.error].find((error) => error && !isRequestCancelled(error));
      if (failed) {
        message.error(t('projectManagement.workItems.loadFailed'));
        return;
      }
      if (!tasksResult.error && !projectResult.error) message.success(t('projectManagement.workbench.refreshSuccess'));
    } catch (error) {
      if (!isRequestCancelled(error)) message.error(t('projectManagement.workItems.loadFailed'));
    } finally {
      setRefreshing(false);
    }
  };
  const statusMutation = useMutation({
    mutationFn: ({ task, nextStatus }: { task: ProjectManagementTaskListItem; nextStatus: string }) => {
      if (!canTransitionProjectManagementTaskStatus(task.status, nextStatus)) {
        return Promise.reject(new Error(t('projectManagement.workItems.statusRollback')));
      }
      return changeProjectManagementTaskStatus(task.id, { status: nextStatus, versionNo: task.versionNo });
    },
    onMutate: async ({ task, nextStatus }) => {
      const tasksKey = projectManagementQueryKeys.tasksProject(scope, projectId);
      const snapshot = queryClient.getQueriesData<{ data: { total: number; items: ProjectManagementTaskListItem[] } }>({ queryKey: tasksKey });
      patchTaskListCaches(queryClient, tasksKey, task.id, { status: nextStatus });
      return { snapshot };
    },
    onSuccess: (response, { task }) => {
      patchTaskListCachesFromDetail(queryClient, projectManagementQueryKeys.tasksProject(scope, projectId), {
        id: task.id,
        status: response.data.status,
        progressPercent: response.data.progressPercent,
        versionNo: response.data.versionNo,
      });
    },
    onError: (error, _variables, context) => {
      context?.snapshot.forEach(([key, value]) => queryClient.setQueryData(key, value));
      if (isTaskStatusRevisionConflict(error)) {
        void invalidate();
        message.info(t('projectManagement.workItems.conflictRefresh'));
        return;
      }
      message.error(t('projectManagement.workItems.statusRollback'));
    },
  });
  const scheduleMutation = useMutation({
    mutationFn: ({ task, startDate, dueDate }: { task: ProjectManagementTaskListItem; startDate?: string; dueDate?: string }) =>
      updateGanttSchedule({
        projectId,
        items: [{ taskId: task.id, startDate: startDate ?? task.startDate ?? dueDate ?? '', dueDate: dueDate ?? task.dueDate ?? startDate ?? '', versionNo: task.versionNo }],
      }),
    onSuccess: () => { void invalidate(); },
    onError: () => message.error(t('projectManagement.workItems.statusRollback')),
  });
  const handleAccessRevoked = useCallback(() => {
    navigate(toProjectManagementPlatformRoute());
  }, [navigate]);
  useProjectManagementProjectRealtime({ enabled: scope.isAvailable && Boolean(projectId), onAccessRevoked: handleAccessRevoked, projectId, scope, signalRUrl: '/hubs/system-notification' });
  useProjectManagementGlobalShortcuts(
    {
      newTask: () => openEditor(),
      search: () => {
        const input = document.querySelector<HTMLInputElement>('.pm-project-search');
        input?.focus();
      },
      switchView: (preferred) => setView(fromPreferredView(preferred)),
    },
    { canExecute: () => true, enabled: !editorOpen },
  );
  const openEditor = (taskId?: string, startDate?: string) => {
    setEditorTaskId(taskId);
    setEditorStartDate(startDate);
    setEditorOpen(true);
    updateUrl('create', taskId ? '' : '1', false);
  };
  const closeEditor = () => {
    setEditorOpen(false);
    setEditorTaskId(undefined);
    setEditorStartDate(undefined);
    updateUrl('create', '', false);
    if (params.get('taskId')) updateUrl('taskId', '', false);
    if (params.get('focus')) updateUrl('focus', '', false);
  };
  useEffect(() => {
    if (focusTaskId && !editorOpen) {
      setEditorTaskId(focusTaskId);
      setEditorOpen(true);
    }
  }, [editorOpen, focusTaskId]);
  const segments: Array<[string, string]> = [
    ['', t('projectManagement.workItems.segment.all')],
    ['mine', t('projectManagement.workItems.segment.mine')],
    ['InProgress', t('projectManagement.workItems.segment.inProgress')],
    ['Todo', t('projectManagement.workItems.segment.todo')],
    ['Done', t('projectManagement.workItems.segment.done')],
    ['Cancelled', t('projectManagement.workItems.segment.closed')],
  ];

  return (
    <ProjectWorkbenchFrame active="requirements">
      <Box className="pm-requirements-screen" sx={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, minWidth: 0 }}>
        <ProjectScreenHeader
          code={projectItem?.project.projectCode ?? t('projectManagement.workbench.project')}
          name={projectItem?.project.projectName ?? t('projectManagement.workbench.requirements')}
          onCreateRequirement={() => openEditor()}
          onRefresh={refresh}
          refreshing={refreshing}
        />
        <Box className="pm-requirement-unified-toolbar">
          <Box className="pm-requirement-view-tabs">
            {viewOptions.map((option) => (
              <button className={view === option ? 'pm-project-tab is-active' : 'pm-project-tab'} key={option} onClick={() => setView(option)} type="button">
                {t(`projectManagement.workItems.view.${option}`)}
              </button>
            ))}
          </Box>
          <Stack className="pm-requirement-toolbar" direction="row" spacing={1}>
            <input aria-label={t('projectManagement.workItems.searchAria')} className="pm-project-search" onChange={(event) => updateUrl('keyword', event.target.value)} placeholder={t('projectManagement.workItems.searchPlaceholder')} value={keyword} />
            <button className={filterOpen ? 'pm-workbench-command is-active' : 'pm-workbench-command'} onClick={() => setFilterOpen((current) => !current)} type="button">{t('projectManagement.workItems.filter')}</button>
            <button className={viewOpen ? 'pm-workbench-command is-active' : 'pm-workbench-command'} onClick={() => setViewOpen((current) => !current)} type="button">{t('projectManagement.workItems.view')}</button>
            <button aria-busy={refreshing} aria-label={t('projectManagement.workItems.refresh')} className="pm-workbench-command" disabled={refreshing} onClick={() => { void refresh(); }} type="button">
              <span className={refreshing ? 'pm-refresh-icon is-spinning' : 'pm-refresh-icon'}><PmIcon name="refresh" size={14} /></span> {t('projectManagement.workItems.refresh')}
            </button>
          </Stack>
        </Box>
        <Box className="pm-requirement-segments">
          {segments.map(([key, label]) => (
            <button className={status === key ? 'pm-requirement-segment is-active' : 'pm-requirement-segment'} key={key || 'all'} onClick={() => updateUrl('status', key)} type="button">{label}</button>
          ))}
        </Box>
        {filterOpen ? (
          <Box className="pm-filter-builder">
            <label>{t('projectManagement.workItems.type')}
              <select className="pm-editor-select" onChange={(event) => updateUrl('type', event.target.value)} value={requirementType}>
                <option value="">{t('projectManagement.workItems.all')}</option>
                {requirementFilterOptions.map((option) => <option disabled={option.disabled} key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </label>
            <label>{t('projectManagement.workItems.risk')}
              <select className="pm-editor-select" onChange={(event) => updateUrl('risk', event.target.value)} value={risk}>
                <option value="">{t('projectManagement.workItems.all')}</option>
                <option value="None">{t('projectManagement.workItems.risk.none')}</option>
                <option value="Low">{projectManagementEnumLabel(t, 'priority', 'Low')}</option>
                <option value="Medium">{projectManagementEnumLabel(t, 'priority', 'Medium')}</option>
                <option value="High">{projectManagementEnumLabel(t, 'priority', 'High')}</option>
              </select>
            </label>
          </Box>
        ) : null}
        {viewOpen ? (
          <Box className="pm-saved-view-panel">
            <input aria-label={t('projectManagement.workItems.saveViewAria')} className="pm-project-search" onChange={(event) => setViewName(event.target.value)} placeholder={t('projectManagement.workItems.saveViewPlaceholder')} value={viewName} />
            <button
              className="pm-primary-button"
              disabled={!viewName.trim() || saveView.isPending}
              onClick={() => saveView.mutate({
                viewName: viewName.trim(),
                viewKey: toApiViewKey(view),
                queryJson: JSON.stringify({ view, status, keyword, risk, type: requirementType }),
              })}
              type="button"
            >
              {t('projectManagement.workItems.saveView')}
            </button>
            {savedViews.data?.data.map((item) => (
              <button
                className="pm-workbench-command"
                key={item.id}
                onClick={() => {
                  try {
                    const saved = JSON.parse(item.queryJson) as Record<string, string>;
                    setParams((current) => {
                      ['view', 'status', 'keyword', 'risk', 'type'].forEach((key) => current.delete(key));
                      Object.entries(saved).forEach(([key, value]) => { if (value) current.set(key, value); });
                      return current;
                    }, { replace: true });
                    setViewOpen(false);
                  } catch {
                    message.error(t('projectManagement.workItems.savedInvalid'));
                  }
                }}
                type="button"
              >
                {item.viewName}
              </button>
            ))}
          </Box>
        ) : null}
        <Box className="pm-requirements-content">
          {query.isError && !rows.length ? (
            <Stack alignItems="center" justifyContent="center" spacing={1} sx={{ flex: 1 }}>
              <Typography color="error">{t('projectManagement.workItems.loadFailed')}</Typography>
              <button className="pm-primary-button" onClick={() => void query.refetch()} type="button">{t('projectManagement.workItems.retry')}</button>
            </Stack>
          ) : view === 'board' ? (
            <RequirementBoard format={format} onEdit={openEditor} onMove={(task, nextStatus) => statusMutation.mutate({ task, nextStatus })} rows={rows} t={t} />
          ) : view === 'card' ? (
            <RequirementCardGrid format={format} onEdit={openEditor} onStatusChange={(task, nextStatus) => statusMutation.mutate({ task, nextStatus })} rows={rows} t={t} />
          ) : view === 'gantt' ? (
            <ProjectManagementGanttView dependencies={dependencies.data?.data ?? []} onSelectTask={(id) => openEditor(id)} rows={rows} />
          ) : view === 'calendar' ? (
            <Box sx={{ display: 'flex', flex: 1, minHeight: 0, minWidth: 0, width: '100%' }}>
              <ProjectManagementTaskCalendar
                dependencies={dependencies.data?.data ?? []}
                milestones={milestones.data?.data.items ?? []}
                onChangeTaskSchedule={(task, startDate, dueDate) => scheduleMutation.mutate({ task, startDate, dueDate })}
                onCreateTask={(date) => openEditor(undefined, date)}
                onSelectTask={(id) => openEditor(id)}
                onToggleTaskSelection={(id) => setSelectedTaskIds((current) => {
                  const next = new Set(current);
                  if (next.has(id)) next.delete(id); else next.add(id);
                  return next;
                })}
                rows={rows}
                schedulePending={scheduleMutation.isPending}
                selectedTaskIds={selectedTaskIds}
              />
            </Box>
          ) : (
            <RequirementTable
              collapsed={collapsed}
              loading={query.isLoading}
              onEdit={openEditor}
              onPageChange={(nextPage) => updateUrl('page', String(nextPage), false)}
              onStatusChange={(task, nextStatus) => statusMutation.mutate({ task, nextStatus })}
              onToggle={(id) => setCollapsed((current) => {
                const next = new Set(current);
                if (next.has(id)) next.delete(id); else next.add(id);
                return next;
              })}
              pageIndex={pageIndex}
              rows={rows}
              t={t}
              total={query.data?.data.total ?? rows.length}
              tree={view === 'tree'}
            />
          )}
        </Box>
        <ProjectWorkItemEditor
          initialStartDate={editorStartDate}
          onClose={closeEditor}
          onSaved={() => { void invalidate(); }}
          open={editorOpen}
          projectId={projectId}
          taskId={editorTaskId}
          focusComments={focusComments}
        />
      </Box>
    </ProjectWorkbenchFrame>
  );
}

function RequirementTable({
  collapsed,
  loading,
  onEdit,
  onPageChange,
  onStatusChange,
  onToggle,
  pageIndex,
  rows,
  t,
  total,
  tree,
}: {
  collapsed: Set<string>;
  loading: boolean;
  onEdit: (id: string) => void;
  onPageChange: (page: number) => void;
  onStatusChange: (task: ProjectManagementTaskListItem, status: string) => void;
  onToggle: (id: string) => void;
  pageIndex: number;
  rows: ProjectManagementTaskListItem[];
  t: (key: string) => string;
  total: number;
  tree: boolean;
}) {
  const visibleRows = tree ? filterCollapsedRows(rows, collapsed) : rows;
  return (
    <DataTable<ProjectManagementTaskListItem>
      className="pm-requirement-table"
      columnSettingsKey="project-management-requirements-v2"
      columns={[
        { key: 'code', title: t('projectManagement.workItems.column.code'), width: '120px', render: (row) => row.taskCode },
        {
          key: 'title',
          title: t('projectManagement.workItems.column.title'),
          width: '80px',
          render: (row) => (
            <Stack alignItems="center" direction="row" spacing={0.5} sx={{ pl: tree ? row.depth * 1.6 : 0 }}>
              <button
                aria-label={row.hasChildren ? formatTemplate(t('projectManagement.workItems.expandCollapse'), { title: row.title }) : row.title}
                className="pm-tree-toggle"
                disabled={!row.hasChildren}
                onClick={(event) => { event.stopPropagation(); onToggle(row.id); }}
                type="button"
              >
                {tree && row.hasChildren ? collapsed.has(row.id) ? '›' : '⌄' : '·'}
              </button>
              <button className="pm-table-title" onClick={() => onEdit(row.id)} type="button">{row.title}</button>
            </Stack>
          ),
        },
        {
          key: 'schedule',
          title: t('projectManagement.workItems.column.schedule'),
          width: '176px',
          cellClassName: 'pm-schedule-cell',
          render: (row) => (
            <ProjectManagementScheduleTimelineBar dueDate={row.dueDate} layout="stack" startDate={row.startDate} status={row.status} />
          ),
        },
        {
          key: 'status',
          title: t('projectManagement.workItems.column.status'),
          width: '130px',
          render: (row) => (
            <select
              aria-label={t('projectManagement.workItems.column.status')}
              className="pm-status-select"
              onChange={(event) => {
                if (event.target.value !== row.status) onStatusChange(row, event.target.value);
              }}
              onClick={(event) => event.stopPropagation()}
              value={row.status}
            >
              {getAllowedProjectManagementTaskStatuses(row.status).map((option) => (
                <option key={option} value={option}>{projectManagementEnumLabel(t, 'status', option)}</option>
              ))}
            </select>
          ),
        },
        {
          key: 'progress',
          title: t('projectManagement.workItems.column.progress'),
          width: '120px',
          render: (row) => <ProjectManagementProgressBar dueDate={row.dueDate} progressPercent={row.progressPercent} status={row.status} />,
        },
        { key: 'assignee', title: t('projectManagement.workItems.column.assignee'), width: '120px', render: (row) => row.assigneeDisplayName ?? '—' },
        { key: 'priority', title: t('projectManagement.workItems.column.priority'), width: '90px', render: (row) => projectManagementEnumLabel(t, 'priority', row.priority) },
      ]}
      emptyText={t('projectManagement.workItems.empty')}
      fitScreen
      loading={loading}
      onPageChange={onPageChange}
      onRowDoubleClick={(row) => onEdit(row.id)}
      pagination={{ current: pageIndex, pageSize: 20, total }}
      rowKey={(row) => row.id}
      rows={visibleRows}
      showColumnSettings
    />
  );
}

function RequirementBoard({
  format,
  onEdit,
  onMove,
  rows,
  t,
}: {
  format: (key: string, values?: Record<string, string | number>) => string;
  onEdit: (id: string) => void;
  onMove: (task: ProjectManagementTaskListItem, status: string) => void;
  rows: ProjectManagementTaskListItem[];
  t: (key: string) => string;
}) {
  const [dragged, setDragged] = useState<ProjectManagementTaskListItem>();
  const [overStatus, setOverStatus] = useState<string>();
  const columns = PROJECT_MANAGEMENT_TASK_STATUSES.map((status) => ({
    status,
    label: projectManagementEnumLabel(t, 'status', status),
  }));
  return (
    <Box className="pm-requirement-board">
      {columns.map((column) => {
        const columnTasks = rows.filter((row) => row.status === column.status);
        return (
          <Box
            className={overStatus === column.status ? 'pm-board-column is-over' : 'pm-board-column'}
            key={column.status}
            onDragLeave={() => setOverStatus(undefined)}
            onDragOver={(event: DragEvent<HTMLElement>) => {
              event.preventDefault();
              setOverStatus(column.status);
            }}
            onDrop={(event: DragEvent<HTMLElement>) => {
              event.preventDefault();
              if (dragged && dragged.status !== column.status && canTransitionProjectManagementTaskStatus(dragged.status, column.status)) {
                onMove(dragged, column.status);
              }
              setDragged(undefined);
              setOverStatus(undefined);
            }}
          >
            <header className="pm-board-column__header">
              <Box className="pm-board-column__title-row">
                <span className="pm-board-column-tone" data-status={column.status} />
                <Typography className="pm-board-column__title">{column.label}</Typography>
              </Box>
              <span className="pm-board-column__count">{columnTasks.length}</span>
            </header>
            <Box className="pm-board-column__body">
              {columnTasks.map((task) => (
                <RequirementTaskCard
                  compact
                  draggable
                  format={format}
                  key={task.id}
                  onDoubleClick={() => onEdit(task.id)}
                  onDragEnd={() => setDragged(undefined)}
                  onDragStart={(event: DragEvent<HTMLElement>) => {
                    setDragged(task);
                    event.dataTransfer.effectAllowed = 'move';
                    event.currentTarget.classList.add('is-dragging');
                  }}
                  showStatusSelect={false}
                  t={t}
                  task={task}
                />
              ))}
              {columnTasks.length === 0 ? (
                <Box className="pm-board-column__empty">{t('projectManagement.workItems.boardEmpty')}</Box>
              ) : null}
            </Box>
          </Box>
        );
      })}
    </Box>
  );
}

function RequirementCardGrid({
  format,
  onEdit,
  onStatusChange,
  rows,
  t,
}: {
  format: (key: string, values?: Record<string, string | number>) => string;
  onEdit: (id: string) => void;
  onStatusChange: (task: ProjectManagementTaskListItem, status: string) => void;
  rows: ProjectManagementTaskListItem[];
  t: (key: string) => string;
}) {
  if (!rows.length) {
    return <Box className="pm-requirement-empty">{t('projectManagement.workItems.empty')}</Box>;
  }
  return (
    <Box className="pm-requirement-card-grid">
      {rows.map((task) => (
        <RequirementTaskCard
          format={format}
          key={task.id}
          onDoubleClick={() => onEdit(task.id)}
          onStatusChange={onStatusChange}
          showStatusSelect
          t={t}
          task={task}
        />
      ))}
    </Box>
  );
}

function RequirementTaskCard({
  compact = false,
  draggable = false,
  format,
  onDoubleClick,
  onDragEnd,
  onDragStart,
  onStatusChange,
  showStatusSelect = false,
  t,
  task,
}: {
  compact?: boolean;
  draggable?: boolean;
  format: (key: string, values?: Record<string, string | number>) => string;
  onDoubleClick?: () => void;
  onDragEnd?: (event: DragEvent<HTMLElement>) => void;
  onDragStart?: (event: DragEvent<HTMLElement>) => void;
  onStatusChange?: (task: ProjectManagementTaskListItem, status: string) => void;
  showStatusSelect?: boolean;
  t: (key: string) => string;
  task: ProjectManagementTaskListItem;
}) {
  const urgency = useMemo(
    () => computeScheduleUrgencyMetrics(task.startDate, task.dueDate, task.status),
    [task.dueDate, task.startDate, task.status],
  );

  return (
    <Box
      className={`pm-requirement-card is-urgency-${urgency.tone}`}
      data-status={task.status}
      draggable={draggable}
      onDoubleClick={onDoubleClick}
      onDragEnd={(event) => {
        event.currentTarget.classList.remove('is-dragging');
        onDragEnd?.(event);
      }}
      onDragStart={onDragStart}
      style={{ '--pm-card-urgency-color': urgency.urgencyColor } as CSSProperties}
    >
      <Typography className="pm-requirement-card__code" component="div" variant="caption">{task.taskCode}</Typography>
      <Typography className="pm-requirement-card__title" component="div" variant="body2">{task.title}</Typography>
      <ProjectManagementProgressBar compact={compact} dueDate={task.dueDate} progressPercent={task.progressPercent} status={task.status} />
      <ProjectManagementCountdown dueDate={task.dueDate} status={task.status} />
      <Box className="pm-requirement-card__footer">
        {showStatusSelect && onStatusChange ? (
          <select
            aria-label={t('projectManagement.workItems.column.status')}
            className="pm-status-select"
            onChange={(event) => {
              if (event.target.value !== task.status) onStatusChange(task, event.target.value);
            }}
            onClick={(event) => event.stopPropagation()}
            value={task.status}
          >
            {getAllowedProjectManagementTaskStatuses(task.status).map((option) => (
              <option key={option} value={option}>{projectManagementEnumLabel(t, 'status', option)}</option>
            ))}
          </select>
        ) : (
          <span className={priorityBadgeClass(task.priority)}>{projectManagementEnumLabel(t, 'priority', task.priority)}</span>
        )}
        <Typography className="pm-requirement-card__assignee" component="span" variant="caption">
          {task.assigneeDisplayName ?? t('projectManagement.workItems.unassigned')}
        </Typography>
      </Box>
      {!compact ? (
        <Typography className="pm-requirement-card__meta" component="div" variant="caption">
          {format('projectManagement.workItems.children', { completed: task.completedChildCount ?? 0, total: task.childCount ?? 0, points: task.storyPoints ?? '—' })}
        </Typography>
      ) : null}
    </Box>
  );
}

function priorityBadgeClass(priority: string): string {
  const normalized = priority.toLowerCase();
  if (normalized === 'urgent') return 'pm-priority-badge pm-priority-badge--urgent';
  if (normalized === 'high') return 'pm-priority-badge pm-priority-badge--high';
  if (normalized === 'low') return 'pm-priority-badge pm-priority-badge--low';
  return 'pm-priority-badge pm-priority-badge--medium';
}

function filterCollapsedRows(rows: ProjectManagementTaskListItem[], collapsed: Set<string>): ProjectManagementTaskListItem[] {
  const hiddenAncestorDepths: number[] = [];
  return rows.filter((row) => {
    while (hiddenAncestorDepths.length > 0 && row.depth <= hiddenAncestorDepths[hiddenAncestorDepths.length - 1]) {
      hiddenAncestorDepths.pop();
    }
    const visible = hiddenAncestorDepths.length === 0;
    if (visible && collapsed.has(row.id)) hiddenAncestorDepths.push(row.depth);
    return visible;
  });
}

function formatTemplate(template: string, values: Record<string, string>): string {
  return Object.entries(values).reduce((result, [key, value]) => result.replace(`{${key}}`, value), template);
}

function isRequestCancelled(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  const name = 'name' in error ? String((error as { name?: unknown }).name ?? '') : '';
  return name === 'AbortError' || name === 'CancelledError' || name === 'CanceledError';
}
