import { useQueries } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties, type DragEvent } from 'react';

import { getProjectManagementTasks } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementMilestone, ProjectManagementTaskDependency, ProjectManagementTaskLabelFilter, ProjectManagementTaskListItem, ProjectManagementTaskQuery } from '../../../api/project-management/projectManagement.types';
import { queryKeys } from '../../../core/query/queryKeys';
import { useAuthStore } from '../../../core/state';
import { DataTable } from '../../../shared/table/DataTable';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import {
  buildVisibleTaskTreeRows,
  readTaskTreeExpansionState,
  taskTreeAriaLevel,
  taskTreeExpansionPreferenceKey,
  taskTreeRowHasChildren,
  toggleTaskTreeExpansion,
  writeTaskTreeExpansionState,
  type TaskTreeRow,
} from '../state/taskTreeState';
import type { TaskWorkspaceState } from '../state/taskWorkspaceState';
import { taskWorkspaceStateToQuery } from '../state/taskWorkspaceState';

import { TaskBoardColumnProjection } from './TaskBoardColumnProjection';
import { summarizeTaskBoardColumns, taskBoardStatuses, type TaskBoardStatus } from './taskBoardProjectionModel';
import { TaskCardProjection, type TaskCardDragHandlers } from './TaskCardProjection';
import type { TaskGroupDropTarget, TaskMoveDropTarget } from './taskMoveIntent';
import { TaskCalendarScheduleProjection, TaskGanttScheduleProjection } from './TaskScheduleProjections';

interface TaskWorkspaceProjectionProps {
  labelFilter?: ProjectManagementTaskLabelFilter;
  milestoneLabels?: Readonly<Record<string, string>>;
  onAddChildTask?: (task: ProjectManagementTaskListItem) => void;
  onCompleteTask?: (task: ProjectManagementTaskListItem) => void;
  onChangeTaskStatus?: (task: ProjectManagementTaskListItem, status: string) => void;
  onChangeTaskSchedule?: (task: ProjectManagementTaskListItem, startDate: string | undefined, dueDate: string | undefined) => void;
  onCreateTaskOnDate?: (date: string) => void;
  onGanttScheduleSaved?: () => Promise<void> | void;
  onGanttZoomChange?: (zoom: 28 | 56 | 84) => void;
  onDeleteTask?: (task: ProjectManagementTaskListItem) => void;
  onBoardRowsLoaded?: (rows: ProjectManagementTaskListItem[]) => void;
  participantLabels?: Readonly<Record<string, string>>;
  pendingTaskId?: string;
  projectId: string;
  onSelectTask: (taskId: string) => void;
  onMoveTask: (task: ProjectManagementTaskListItem, target: TaskMoveDropTarget) => void;
  onMoveTaskGroup?: (task: ProjectManagementTaskListItem, target: TaskGroupDropTarget) => void;
  onToggleTaskSelection: (taskId: string) => void;
  dependencies?: readonly ProjectManagementTaskDependency[];
  milestones?: readonly ProjectManagementMilestone[];
  optimisticBoardRows?: Readonly<Record<string, ProjectManagementTaskListItem>>;
  rows: ProjectManagementTaskListItem[];
  schedulePending?: boolean;
  selectedTaskIds: ReadonlySet<string>;
  state: TaskWorkspaceState;
}

export function TaskWorkspaceProjection({ dependencies, labelFilter, milestoneLabels, milestones, onAddChildTask, onBoardRowsLoaded, onChangeTaskSchedule, onChangeTaskStatus, onCompleteTask, onCreateTaskOnDate, onDeleteTask, onGanttScheduleSaved, onGanttZoomChange, onMoveTask, onMoveTaskGroup, onSelectTask, onToggleTaskSelection, optimisticBoardRows, participantLabels, pendingTaskId, projectId, rows, schedulePending, selectedTaskIds, state }: TaskWorkspaceProjectionProps) {
  const scope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore((current) => current.user?.userId ?? '');
  const expansionKey = useMemo(
    () => userId && scope.tenantId && scope.appCode && projectId
      ? taskTreeExpansionPreferenceKey(userId, scope.tenantId, scope.appCode, projectId)
      : '',
    [projectId, scope.appCode, scope.tenantId, userId],
  );
  const [expandedState, setExpandedState] = useState(() => readTaskTreeExpansionState(''));
  const [childPageByParent, setChildPageByParent] = useState<Record<string, number>>({});
  const [loadedChildren, setLoadedChildren] = useState<Map<string, { items: TaskTreeRow[]; total: number }>>(() => new Map());
  const hydratedExpansionKey = useRef('');

  useEffect(() => {
    setExpandedState(readTaskTreeExpansionState(expansionKey));
    hydratedExpansionKey.current = expansionKey;
  }, [expansionKey]);

  useEffect(() => {
    if (hydratedExpansionKey.current === expansionKey && expansionKey) writeTaskTreeExpansionState(expansionKey, expandedState);
  }, [expandedState, expansionKey]);

  const knownRows = useMemo(() => {
    const byId = new Map<string, TaskTreeRow>(rows.map((row) => [row.id, row]));
    loadedChildren.forEach((value) => value.items.forEach((row) => byId.set(row.id, row)));
    return [...byId.values()];
  }, [loadedChildren, rows]);
  const expandedTaskIds = useMemo(
    () => expandedState.expandedTaskIds.filter((id) => {
      const row = knownRows.find((candidate) => candidate.id === id);
      return row ? taskTreeRowHasChildren(row, knownRows) : false;
    }),
    [expandedState.expandedTaskIds, knownRows],
  );
  const childQueries = useQueries({
    queries: expandedTaskIds.map((parentTaskId) => {
      const pageIndex = childPageByParent[parentTaskId] ?? 1;
      const query = { ...taskWorkspaceStateToQuery(projectId, { ...state, pageIndex }), labelFilter, parentTaskId };
      return {
        enabled: scope.isAvailable && state.viewKey === 'tree' && Boolean(projectId),
        queryFn: ({ signal }: { signal: AbortSignal }) => getProjectManagementTasks(query, signal),
        queryKey: [...queryKeys.projectManagement.tasks(scope, query), JSON.stringify(labelFilter ?? null)],
      };
    }),
  });

  const childQueryContext = JSON.stringify({ projectId, keyword: state.keyword, status: state.status, assignee: state.assigneeUserId, milestoneId: state.milestoneId, dueFrom: state.dueFrom, dueTo: state.dueTo, includeCompleted: state.includeCompleted, sortBy: state.sortBy, sortDirection: state.sortDirection, labelFilter });
  useEffect(() => {
    setLoadedChildren(new Map());
    setChildPageByParent({});
  }, [childQueryContext]);

  useEffect(() => {
    let changed = false;
    const next = new Map(loadedChildren);
    expandedTaskIds.forEach((parentTaskId, index) => {
      const data = childQueries[index]?.data?.data;
      if (!data) return;
      const existing = next.get(parentTaskId);
      const items = new Map<string, TaskTreeRow>((existing?.items ?? []).map((row) => [row.id, row]));
      data.items.forEach((row) => items.set(row.id, row as TaskTreeRow));
      const mergedItems = [...items.values()];
      if (!existing || existing.total !== data.total || existing.items.length !== mergedItems.length) {
        next.set(parentTaskId, { items: mergedItems, total: data.total });
        changed = true;
      }
    });
    if (changed) setLoadedChildren(next);
  }, [childQueries, expandedTaskIds, loadedChildren]);

  const visibleRows = useMemo(() => state.viewKey === 'tree'
    ? buildVisibleTaskTreeRows(knownRows, new Set(expandedTaskIds))
    : rows,
  [expandedTaskIds, knownRows, rows, state.viewKey]);
  const toggleExpansion = useCallback((taskId: string) => {
    setExpandedState((current) => toggleTaskTreeExpansion(current, taskId));
  }, []);
  const loadMoreChildren = useCallback((taskId: string) => {
    setChildPageByParent((current) => ({ ...current, [taskId]: (current[taskId] ?? 1) + 1 }));
  }, []);
  const [draggedTask, setDraggedTask] = useState<ProjectManagementTaskListItem>();
  const [keyboardDragging, setKeyboardDragging] = useState(false);
  const [focusTaskId, setFocusTaskId] = useState<string>();
  const [dragAnnouncement, setDragAnnouncement] = useState('看板支持鼠标拖动；聚焦任务卡片后按空格或回车开始，左右方向键移动到相邻状态列，Esc 取消；触屏可使用“移动到列”菜单。');

  useEffect(() => {
    if (!focusTaskId) return;
    const target = [...document.querySelectorAll<HTMLElement>('[data-project-task-id]')].find((element) => element.dataset.projectTaskId === focusTaskId);
    if (target) {
      target.focus();
      setFocusTaskId(undefined);
    }
  }, [focusTaskId, optimisticBoardRows, rows, state.viewKey]);

  const drag: TaskCardDragHandlers = {
    draggedTaskId: draggedTask?.id,
    keyboardHintId: 'project-management-board-drag-help',
    keyboardTaskId: keyboardDragging ? draggedTask?.id : undefined,
    onDragEnd: () => setDraggedTask(undefined),
    onDragOver: (event: DragEvent<HTMLElement>) => event.preventDefault(),
    onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTaskListItem) => {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', task.id);
      setKeyboardDragging(false);
      setDraggedTask(task);
      setDragAnnouncement(`已抓取任务“${task.title}”。拖到目标状态列或分组；按 Esc 可取消。`);
    },
    onKeyboardCancel: () => {
      setKeyboardDragging(false);
      setDraggedTask(undefined);
      setDragAnnouncement('已取消键盘移动。');
    },
    onKeyboardMove: (task, direction) => {
      if (state.viewKey !== 'board') {
        setDragAnnouncement('键盘移动状态列仅在看板视图可用。');
        return;
      }
      const currentIndex = taskBoardStatuses.indexOf(task.status as TaskBoardStatus);
      const nextIndex = currentIndex + (direction === 'next' ? 1 : -1);
      const nextStatus = taskBoardStatuses[nextIndex];
      if (currentIndex < 0 || !nextStatus) {
        setDragAnnouncement(direction === 'next' ? '已经是最后一个状态列。' : '已经是第一个状态列。');
        return;
      }
      setFocusTaskId(task.id);
      setKeyboardDragging(false);
      setDraggedTask(undefined);
      setDragAnnouncement(`正在把任务“${task.title}”移动到${nextStatus}列。`);
      onChangeTaskStatus?.(task, nextStatus);
    },
    onKeyboardStart: (task) => {
      setKeyboardDragging(true);
      setDraggedTask(task);
      setDragAnnouncement(`已选择任务“${task.title}”进行键盘移动。使用左右方向键选择状态列，Esc 取消。`);
    },
    onDrop: (event: DragEvent<HTMLElement>, target: WorkspaceDropTarget) => {
      event.preventDefault();
      event.stopPropagation();
      if (draggedTask) {
        setFocusTaskId(draggedTask.id);
        setDragAnnouncement(`正在提交任务“${draggedTask.title}”的移动。`);
        if (target.kind === 'status') onChangeTaskStatus?.(draggedTask, target.status);
        else if (target.kind === 'group') onMoveTaskGroup?.(draggedTask, target);
        else onMoveTask(draggedTask, target);
      }
      setKeyboardDragging(false);
      setDraggedTask(undefined);
    },
  };
  const rootDropZone = <div aria-label="拖到此处成为顶级任务" className="pm-root-drop-zone" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'root' })}>拖到此处成为顶级任务</div>;
  const dragHelp = <p className="sr-only" id="project-management-board-drag-help" role="status" aria-live="polite">{dragAnnouncement}</p>;

  if (state.viewKey === 'board') return <>{dragHelp}<TaskBoardProjection baseQuery={{ ...taskWorkspaceStateToQuery(projectId, state), labelFilter: labelFilter?.labelIds.length ? labelFilter : undefined }} drag={drag} groupBy={state.groupBy} milestoneLabels={milestoneLabels} onAddChildTask={onAddChildTask} onBoardRowsLoaded={onBoardRowsLoaded} onChangeTaskStatus={onChangeTaskStatus} onCompleteTask={onCompleteTask} onDeleteTask={onDeleteTask} optimisticBoardRows={optimisticBoardRows} participantLabels={participantLabels} pendingTaskId={pendingTaskId} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} />;</>;
  if (state.viewKey === 'card') return <>{dragHelp}{rootDropZone}<TaskCardProjection drag={drag} groupBy={state.groupBy} milestoneLabels={milestoneLabels} onAddChildTask={onAddChildTask} onCompleteTask={onCompleteTask} onDeleteTask={onDeleteTask} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} participantLabels={participantLabels} pendingTaskId={pendingTaskId} projectId={projectId} rows={rows} selectedTaskIds={selectedTaskIds} /></>;
  if (state.viewKey === 'gantt') return <TaskGanttScheduleProjection dependencies={dependencies} ganttZoom={state.ganttZoom} milestones={milestones} onChangeTaskSchedule={onChangeTaskSchedule} onGanttScheduleSaved={onGanttScheduleSaved} onGanttZoomChange={onGanttZoomChange} projectId={projectId} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} />;
  if (state.viewKey === 'calendar') return <TaskCalendarScheduleProjection dependencies={dependencies} milestones={milestones} onChangeTaskSchedule={onChangeTaskSchedule} onCreateTask={onCreateTaskOnDate} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} rows={rows} schedulePending={schedulePending} selectedTaskIds={selectedTaskIds} />;
  return <>{rootDropZone}<TaskTableProjection childStateByParent={loadedChildren} expandedTaskIds={new Set(expandedTaskIds)} onLoadMoreChildren={loadMoreChildren} onToggleTaskExpansion={toggleExpansion} drag={drag} rows={visibleRows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} state={state} /></>;
}

type TaskDragHandlers = TaskCardDragHandlers;
type WorkspaceDropTarget = TaskMoveDropTarget | { kind: 'status'; status: string } | TaskGroupDropTarget;

function TaskTableProjection({ childStateByParent, expandedTaskIds, onLoadMoreChildren, onToggleTaskExpansion, drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds, state }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds' | 'state'> & { childStateByParent: Map<string, { items: TaskTreeRow[]; total: number }>; expandedTaskIds: ReadonlySet<string>; onLoadMoreChildren: (taskId: string) => void; onToggleTaskExpansion: (taskId: string) => void; drag: TaskDragHandlers }) {
  const columns = useMemo<DataTableColumn<TaskTreeRow>[]>(() => [
    { key: 'select', title: '选择', width: '64px', render: (row) => <input aria-label={`选择任务 ${row.title}`} checked={selectedTaskIds.has(row.id)} type="checkbox" onChange={() => onToggleTaskSelection(row.id)} /> },
    { key: 'taskCode', title: '编码', width: '120px', responsivePriority: 100 },
    {
      key: 'title', title: '任务', responsivePriority: 100, render: (row) => {
        const hasChildren = taskTreeRowHasChildren(row, rows);
        const expanded = expandedTaskIds.has(row.id);
        const childState = childStateByParent.get(row.id);
        const hasMoreChildren = expanded && childState ? childState.total > childState.items.length : false;
        return (
        <div
          className={`pm-task-tree-title${drag.draggedTaskId === row.id ? ' is-dragging' : ''}`}
          draggable
          aria-level={state.viewKey === 'tree' ? taskTreeAriaLevel(row) : undefined}
          aria-expanded={state.viewKey === 'tree' && hasChildren ? expanded : undefined}
          role={state.viewKey === 'tree' ? 'treeitem' : undefined}
          tabIndex={state.viewKey === 'tree' ? 0 : undefined}
          onKeyDown={(event) => {
            if (state.viewKey !== 'tree' || !hasChildren) return;
            if (event.key === 'ArrowRight' && !expanded) {
              event.preventDefault();
              onToggleTaskExpansion(row.id);
            } else if (event.key === 'ArrowLeft' && expanded) {
              event.preventDefault();
              onToggleTaskExpansion(row.id);
            }
          }}
          onDragEnd={drag.onDragEnd}
          onDragOver={drag.onDragOver}
          onDragStart={(event) => drag.onDragStart(event, row)}
          onDrop={(event) => drag.onDrop(event, { kind: 'before', task: row })}
          style={{ '--pm-task-depth': state.viewKey === 'tree' ? row.depth : 0 } as CSSProperties}
        >
          {state.viewKey === 'tree' && hasChildren ? <button aria-label={`${expanded ? '折叠' : '展开'}任务 ${row.title}`} onClick={(event) => { event.stopPropagation(); onToggleTaskExpansion(row.id); }} type="button">{expanded ? '▾' : '▸'}</button> : <span aria-hidden="true" className="w-4" />}
          <span>{row.title}</span>
          <span className="pm-child-drop-zone" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task: row })}>作为子任务</span>
          {hasMoreChildren ? <button aria-label={`加载更多 ${row.title} 的子任务`} onClick={(event) => { event.stopPropagation(); onLoadMoreChildren(row.id); }} type="button">加载更多</button> : null}
        </div>
        );
      }
    },
    { key: 'status', title: '状态', width: '110px', render: (row) => <StatusBadge status={row.status} /> },
    { key: 'priority', title: '优先级', width: '96px', render: (row) => <PriorityBadge priority={row.priority} /> },
    { key: 'progressPercent', title: '进度', width: '138px', render: (row) => <Progress value={row.progressPercent} /> },
    { key: 'dueDate', title: '截止日期', width: '120px', render: (row) => formatDate(row.dueDate) },
    { key: 'blockedByCount', title: '阻塞', width: '96px', render: (row) => row.blockedByCount ? <StatusBadge status="Blocked" label={`${row.blockedByCount} 项`} /> : '—' },
  ], [childStateByParent, drag, expandedTaskIds, onLoadMoreChildren, onToggleTaskExpansion, onToggleTaskSelection, rows, selectedTaskIds, state.viewKey]);

  const visibleColumns = new Set(state.visibleColumns);
  return <DataTable columnSettingsKey={`project-management-tasks-${state.viewKey}`} columns={columns.filter((column) => column.key === 'select' || visibleColumns.has(String(column.key)))} emptyText="暂无任务" rowActions={(row) => <button type="button" onClick={() => onSelectTask(row.id)}>查看</button>} rowKey={(row) => row.id} rowVirtualize={state.viewKey === 'tree' || state.viewKey === 'list'} rowVirtualization={{ overscan: 8, rowHeight: 56 }} rows={rows} showColumnSettings />;
}

function TaskBoardProjection({ baseQuery, drag, groupBy, milestoneLabels, onAddChildTask, onBoardRowsLoaded, onChangeTaskStatus, onCompleteTask, onDeleteTask, optimisticBoardRows, participantLabels, pendingTaskId, onSelectTask, onToggleTaskSelection, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'milestoneLabels' | 'onAddChildTask' | 'onBoardRowsLoaded' | 'onChangeTaskStatus' | 'onCompleteTask' | 'onDeleteTask' | 'onSelectTask' | 'onToggleTaskSelection' | 'optimisticBoardRows' | 'participantLabels' | 'pendingTaskId' | 'selectedTaskIds'> & { baseQuery: ProjectManagementTaskQuery; drag: TaskCardDragHandlers; groupBy?: TaskWorkspaceState['groupBy'] }) {
  const scope = useProjectManagementWorkspaceScope();
  const [rowsByStatus, setRowsByStatus] = useState<Partial<Record<TaskBoardStatus, ProjectManagementTaskListItem[]>>>({});
  const [metrics, setMetrics] = useState(() => taskBoardStatuses.map((status) => ({ loaded: 0, status })));
  const summary = summarizeTaskBoardColumns(metrics);
  const reportRows = useCallback((status: TaskBoardStatus, rows: ProjectManagementTaskListItem[]) => {
    setRowsByStatus((current) => ({ ...current, [status]: rows }));
  }, []);
  const reportMetric = useCallback((metric: { loaded: number; status: TaskBoardStatus; total?: number }) => {
    setMetrics((current) => current.map((item) => item.status === metric.status ? metric : item));
  }, []);

  useEffect(() => {
    onBoardRowsLoaded?.(taskBoardStatuses.flatMap((status) => rowsByStatus[status] ?? []));
  }, [onBoardRowsLoaded, rowsByStatus]);

  return <div aria-label="任务看板" className="pm-board-shell">
    <div aria-live="polite" className="pm-board-summary"><strong>看板数量摘要</strong><span>可见任务 {summary.total ?? '—'}</span><span>已加载 {summary.loaded}</span></div>
    <div className="pm-board">{taskBoardStatuses.map((status) => <div key={status} onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'status', status })}>
      <TaskBoardColumnProjection baseQuery={baseQuery} drag={drag} groupBy={groupBy} groupDropBy={isMovableTaskGroupBy(groupBy) ? groupBy : undefined} milestoneLabels={milestoneLabels} onAddChildTask={onAddChildTask} onChangeTaskStatus={onChangeTaskStatus} onCompleteTask={onCompleteTask} onDeleteTask={onDeleteTask} onMetricChange={reportMetric} onRowsLoaded={(rows) => reportRows(status, rows)} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} optimisticRows={optimisticBoardRows} participantLabels={participantLabels} pendingTaskId={pendingTaskId} scope={scope} selectedTaskIds={selectedTaskIds} status={status} />
    </div>)}</div>
  </div>;
}

function isMovableTaskGroupBy(value: TaskWorkspaceState['groupBy']): value is 'assignee' | 'milestone' | 'parent' | 'label' {
  return value === 'assignee' || value === 'milestone' || value === 'parent' || value === 'label';
}

function StatusBadge({ label, status }: { label?: string; status: string }) {
  return <span className={`pm-status-badge pm-status-badge--${toKebabCase(status)}`}>{label ?? statusLabel(status)}</span>;
}

function PriorityBadge({ priority }: { priority: string }) {
  return <span className={`pm-priority-badge pm-priority-badge--${priority.toLowerCase()}`}>{priorityLabel(priority)}</span>;
}

function Progress({ value }: { value: number }) {
  return <div className="pm-progress"><progress max={100} value={value} /><span>{value}%</span></div>;
}

function statusLabel(status: string): string {
  return ({ Todo: '待开始', InProgress: '进行中', Blocked: '受阻塞', Done: '已完成', Cancelled: '已取消' } as Record<string, string>)[status] ?? status;
}

function priorityLabel(priority: string): string {
  return ({ Low: '低', Medium: '中', High: '高', Urgent: '紧急' } as Record<string, string>)[priority] ?? priority;
}

function formatDate(value: string | undefined): string {
  return value ? new Date(value).toLocaleDateString() : '未设置';
}

function toKebabCase(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
}
