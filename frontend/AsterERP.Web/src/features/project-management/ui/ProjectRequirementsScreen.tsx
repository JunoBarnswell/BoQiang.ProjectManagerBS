import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Chip, Stack as MuiStack, Typography as MuiTypography } from '@mui/material';
import { useEffect, useMemo, useState, type DragEvent } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';

import { changeProjectManagementTaskStatus, createProjectManagementSavedView, getProjectManagementOverview, getProjectManagementSavedViews, getProjectManagementTasks } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementSavedViewUpsertRequest, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { useMessage } from '../../../shared/feedback/useMessage';
import { DataTable } from '../../../shared/table/DataTable';
import { useProjectManagementProjectRealtime } from '../realtime/useProjectManagementProjectRealtime';
import { toProjectManagementPlatformRoute } from '../state/projectManagementPlatformRoutes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import { ProjectScreenHeader, ProjectWorkbenchFrame } from './ProjectWorkbenchFrame';
import { ProjectWorkItemEditor } from './ProjectWorkItemEditor';

const Stack = MuiStack as any;
const Typography = MuiTypography as any;
type RequirementView = 'tree' | 'flat' | 'board';
const boardColumns = [
  { status: 'Todo', label: '未开始', tone: '#94a3b8' },
  { status: 'InProgress', label: '进行中', tone: '#f59e0b' },
  { status: 'Blocked', label: '已阻塞', tone: '#ef4444' },
  { status: 'Done', label: '已完成', tone: '#22c55e' },
];

export function ProjectRequirementsScreen() {
  const scope = useProjectManagementWorkspaceScope();
  const navigate = useNavigate();
  const message = useMessage();
  const queryClient = useQueryClient();
  const { projectId = '' } = useParams<{ projectId: string }>();
  const [params, setParams] = useSearchParams();
  const view = (['tree', 'flat', 'board'].includes(params.get('view') ?? '') ? params.get('view') : 'tree') as RequirementView;
  const keyword = params.get('keyword') ?? '';
  const status = params.get('status') ?? '';
  const risk = params.get('risk') ?? '';
  const requirementType = params.get('type') ?? '';
  const focusTaskId = params.get('taskId');
  const [editorTaskId, setEditorTaskId] = useState<string>();
  const [editorOpen, setEditorOpen] = useState(params.get('create') === '1');
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [filterOpen, setFilterOpen] = useState(false);
  const [viewOpen, setViewOpen] = useState(false);
  const [viewName, setViewName] = useState('');
  const pageIndex = Number(params.get('page') ?? 1) || 1;
  const request = useMemo(() => ({
    projectId, pageIndex, pageSize: view === 'board' ? 200 : 20,
    viewKey: view === 'flat' ? 'list' as const : view,
    keyword: keyword || undefined, status: status && status !== 'mine' ? status : undefined,
    assigneeUserId: status === 'mine' ? scope.userId : undefined,
    riskLevel: risk || undefined, requirementType: requirementType || undefined,
    workItemType: 'Requirement', includeCompleted: true,
    sortBy: view === 'tree' ? 'tree' as const : 'updated' as const,
    sortDirection: view === 'tree' ? 'asc' as const : 'desc' as const,
  }), [keyword, pageIndex, projectId, requirementType, risk, scope.userId, status, view]);
  const query = useQuery({ enabled: scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.tasks(scope, request), queryFn: ({ signal }) => getProjectManagementTasks(request, signal) });
  const project = useQuery({ enabled: scope.isAvailable && Boolean(projectId), queryKey: projectManagementQueryKeys.overview(scope, { projectId, pageIndex: 1, pageSize: 1 }), queryFn: ({ signal }) => getProjectManagementOverview({ projectId, pageIndex: 1, pageSize: 1 }, signal) });
  const rows = query.data?.data.items ?? [];
  const projectItem = project.data?.data.items[0];
  const savedViews = useQuery({ enabled: viewOpen && Boolean(projectId), queryKey: projectManagementQueryKeys.savedViews(scope, projectId), queryFn: ({ signal }) => getProjectManagementSavedViews(projectId, signal) });
  const saveView = useMutation({ mutationFn: (requestToSave: ProjectManagementSavedViewUpsertRequest) => createProjectManagementSavedView(projectId, requestToSave), onSuccess: () => { setViewName(''); void savedViews.refetch(); message.success('视图已保存'); }, onError: () => message.error('保存视图失败') });
  const updateUrl = (key: string, value: string, resetPage = true) => {
    setParams((current) => { if (value) current.set(key, value); else current.delete(key); if (resetPage) current.delete('page'); return current; }, { replace: true });
  };
  const invalidate = () => Promise.all([
    queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.tasksProject(scope, projectId) }),
    queryClient.invalidateQueries({ queryKey: projectManagementQueryKeys.overview(scope, { projectId }) }),
  ]);
  const statusMutation = useMutation({
    mutationFn: ({ task, nextStatus }: { task: ProjectManagementTaskListItem; nextStatus: string }) => changeProjectManagementTaskStatus(task.id, { status: nextStatus, versionNo: task.versionNo }),
    onMutate: async ({ task, nextStatus }) => {
      await queryClient.cancelQueries({ queryKey: projectManagementQueryKeys.tasksProject(scope, projectId) });
      const snapshot = queryClient.getQueriesData<{ data: { total: number; items: ProjectManagementTaskListItem[] } }>({ queryKey: projectManagementQueryKeys.tasksProject(scope, projectId) });
      queryClient.setQueriesData<{ data: { total: number; items: ProjectManagementTaskListItem[] } }>({ queryKey: projectManagementQueryKeys.tasksProject(scope, projectId) }, (current) => current ? { ...current, data: { ...current.data, items: current.data.items.map((item) => item.id === task.id ? { ...item, status: nextStatus } : item) } } : current);
      return { snapshot };
    },
    onError: (_error, _variables, context) => { context?.snapshot.forEach(([key, value]) => queryClient.setQueryData(key, value)); message.error('状态更新失败，已回滚'); },
    onSettled: () => { void invalidate(); },
  });
  useProjectManagementProjectRealtime({ enabled: scope.isAvailable && Boolean(projectId), onAccessRevoked: () => navigate(toProjectManagementPlatformRoute()), projectId, scope, signalRUrl: '/hubs/system-notification' });
  const openEditor = (taskId?: string) => { setEditorTaskId(taskId); setEditorOpen(true); updateUrl('create', taskId ? '' : '1', false); };
  const closeEditor = () => { setEditorOpen(false); setEditorTaskId(undefined); updateUrl('create', '', false); };
  useEffect(() => { if (focusTaskId && !editorOpen) { setEditorTaskId(focusTaskId); setEditorOpen(true); } }, [editorOpen, focusTaskId]);

  return <ProjectWorkbenchFrame active="requirements"><Box className="pm-requirements-screen" sx={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, minWidth: 0 }}>
    <ProjectScreenHeader code={projectItem?.project.projectCode ?? '项目'} name={projectItem?.project.projectName ?? '需求中心'} onCreateRequirement={() => openEditor()} />
    <Box className="pm-requirement-view-tabs"><button className={view === 'tree' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => updateUrl('view', 'tree')} type="button">树状</button><button className={view === 'flat' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => updateUrl('view', 'flat')} type="button">平铺</button><button className={view === 'board' ? 'pm-project-tab is-active' : 'pm-project-tab'} onClick={() => updateUrl('view', 'board')} type="button">看板</button></Box>
    <Box className="pm-requirement-segment-bar"><Stack direction="row" spacing={2}>{[['', '全部需求'], ['mine', '我的需求'], ['InProgress', '进行中'], ['Todo', '待处理'], ['Done', '已完成'], ['Closed', '已关闭']].map(([key, label]) => <button className={status === key ? 'pm-requirement-segment is-active' : 'pm-requirement-segment'} key={key || 'all'} onClick={() => updateUrl('status', key)} type="button">{label}</button>)}</Stack><Stack className="pm-requirement-toolbar" direction="row" spacing={1}><input aria-label="搜索需求" className="pm-project-search" onChange={(event) => updateUrl('keyword', event.target.value)} placeholder="搜索需求（编号/标题）" value={keyword} /><button className={filterOpen ? 'pm-workbench-command is-active' : 'pm-workbench-command'} onClick={() => setFilterOpen((current) => !current)} type="button">筛选</button><button className={viewOpen ? 'pm-workbench-command is-active' : 'pm-workbench-command'} onClick={() => setViewOpen((current) => !current)} type="button">视图</button></Stack></Box>
    {filterOpen ? <Box className="pm-filter-builder"><label>需求类型<select className="pm-editor-select" onChange={(event) => updateUrl('type', event.target.value)} value={requirementType}><option value="">全部</option><option value="Feature">功能需求</option><option value="NonFunctional">非功能需求</option><option value="Other">其他</option></select></label><label>风险<select className="pm-editor-select" onChange={(event) => updateUrl('risk', event.target.value)} value={risk}><option value="">全部</option><option value="None">无风险</option><option value="Low">低</option><option value="Medium">中</option><option value="High">高</option></select></label></Box> : null}
    {viewOpen ? <Box className="pm-saved-view-panel"><input aria-label="视图名称" className="pm-project-search" onChange={(event) => setViewName(event.target.value)} placeholder="输入视图名称" value={viewName} /><button className="pm-primary-button" disabled={!viewName.trim() || saveView.isPending} onClick={() => saveView.mutate({ viewName: viewName.trim(), viewKey: view === 'tree' ? 'tree' : view === 'flat' ? 'list' : 'board', queryJson: JSON.stringify({ view, status, keyword, risk, type: requirementType }) })} type="button">保存当前视图</button>{savedViews.data?.data.map((item) => <button className="pm-workbench-command" key={item.id} onClick={() => { try { const saved = JSON.parse(item.queryJson) as Record<string, string>; setParams((current) => { ['view', 'status', 'keyword', 'risk', 'type'].forEach((key) => current.delete(key)); Object.entries(saved).forEach(([key, value]) => { if (value) current.set(key, value); }); return current; }, { replace: true }); setViewOpen(false); } catch { message.error('视图配置损坏'); } }} type="button">{item.viewName}</button>)}</Box> : null}
    <Box className="pm-requirements-content" sx={{ display: 'flex', flex: 1, minHeight: 0, p: { xs: 1, md: 2 } }}>{query.isError ? <Stack alignItems="center" justifyContent="center" spacing={1} sx={{ flex: 1 }}><Typography color="error">需求列表加载失败</Typography><button className="pm-primary-button" onClick={() => void query.refetch()} type="button">重试</button></Stack> : view === 'board' ? <RequirementBoard onEdit={openEditor} onMove={(task, nextStatus) => statusMutation.mutate({ task, nextStatus })} rows={rows} /> : <RequirementTable collapsed={collapsed} loading={query.isLoading} onEdit={openEditor} onPageChange={(nextPage) => updateUrl('page', String(nextPage), false)} onToggle={(id) => setCollapsed((current) => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; })} pageIndex={pageIndex} rows={rows} total={query.data?.data.total ?? rows.length} tree={view === 'tree'} />}</Box>
    <ProjectWorkItemEditor onClose={closeEditor} onSaved={() => { void invalidate(); }} open={editorOpen} projectId={projectId} taskId={editorTaskId} />
  </Box></ProjectWorkbenchFrame>;
}

function RequirementTable({ collapsed, loading, onEdit, onPageChange, onToggle, pageIndex, rows, total, tree }: { collapsed: Set<string>; loading: boolean; onEdit: (id: string) => void; onPageChange: (page: number) => void; onToggle: (id: string) => void; pageIndex: number; rows: ProjectManagementTaskListItem[]; total: number; tree: boolean }) {
  const visibleRows = tree ? filterCollapsedRows(rows, collapsed) : rows;
  return <DataTable<ProjectManagementTaskListItem> className="pm-requirement-table" columnSettingsKey="project-management-requirements" columns={[{ key: 'code', title: '编号', width: '120px', render: (row) => row.taskCode }, { key: 'title', title: '标题', width: '320px', render: (row) => <Stack alignItems="center" direction="row" spacing={0.5} sx={{ pl: tree ? row.depth * 1.6 : 0 }}><button aria-label={row.hasChildren ? `展开或收起 ${row.title}` : row.title} className="pm-tree-toggle" disabled={!row.hasChildren} onClick={(event) => { event.stopPropagation(); onToggle(row.id); }} type="button">{tree && row.hasChildren ? collapsed.has(row.id) ? '›' : '⌄' : '·'}</button><button className="pm-table-title" onClick={() => onEdit(row.id)} type="button">{row.title}</button></Stack> }, { key: 'status', title: '状态', width: '110px', render: (row) => <Chip label={row.status} size="small" /> }, { key: 'assignee', title: '负责人', width: '120px', render: (row) => row.assigneeDisplayName ?? '—' }, { key: 'priority', title: '优先级', width: '90px', render: (row) => row.priority }, { key: 'parent', title: '父工作项', width: '130px', render: (row) => row.parentTaskId ?? '—' }, { key: 'type', title: '需求类型', width: '120px', render: (row) => row.requirementType ?? '—' }, { key: 'points', title: '故事点', width: '90px', render: (row) => row.storyPoints ?? '—' }] } emptyText="暂无需求" fitScreen loading={loading} onPageChange={onPageChange} onRowDoubleClick={(row) => onEdit(row.id)} pagination={{ current: pageIndex, pageSize: 20, total }} rowKey={(row) => row.id} rows={visibleRows} showColumnSettings />;
}

function filterCollapsedRows(rows: ProjectManagementTaskListItem[], collapsed: Set<string>) {
  const ancestors: string[] = [];
  return rows.filter((row) => { ancestors.length = row.depth; const hidden = ancestors.some((id) => collapsed.has(id)); ancestors[row.depth] = row.id; return !hidden; });
}

function RequirementBoard({ onEdit, onMove, rows }: { onEdit: (id: string) => void; onMove: (task: ProjectManagementTaskListItem, nextStatus: string) => void; rows: ProjectManagementTaskListItem[] }) {
  const [dragged, setDragged] = useState<ProjectManagementTaskListItem>();
  const [overStatus, setOverStatus] = useState<string>();
  return <Box className="pm-requirement-board">{boardColumns.map((column) => <Stack className={overStatus === column.status ? 'pm-board-column is-over' : 'pm-board-column'} key={column.status} onDragOver={(event: DragEvent<HTMLElement>) => { event.preventDefault(); setOverStatus(column.status); }} onDrop={(event: DragEvent<HTMLElement>) => { event.preventDefault(); if (dragged && dragged.status !== column.status) onMove(dragged, column.status); setDragged(undefined); setOverStatus(undefined); }}><Stack direction="row" justifyContent="space-between"><Typography fontWeight={700}>{column.label}</Typography><Typography color="text.secondary">{rows.filter((row) => row.status === column.status).length}</Typography></Stack>{rows.filter((row) => row.status === column.status).map((task) => <Box className="pm-requirement-card" draggable key={task.id} onDoubleClick={() => onEdit(task.id)} onDragStart={(event: DragEvent<HTMLElement>) => { setDragged(task); event.dataTransfer.effectAllowed = 'move'; }}><Typography color="text.secondary" variant="caption">{task.taskCode}</Typography><Typography fontWeight={650} variant="body2">{task.title}</Typography><Stack direction="row" justifyContent="space-between"><Chip label={task.priority} size="small" /><Typography color="text.secondary" variant="caption">{task.assigneeDisplayName ?? '未分配'}</Typography></Stack><Typography color="text.secondary" variant="caption">子任务 {task.completedChildCount ?? 0}/{task.childCount ?? 0} · {task.storyPoints ?? '—'} 点</Typography></Box>)}</Stack>)}</Box>;
}
