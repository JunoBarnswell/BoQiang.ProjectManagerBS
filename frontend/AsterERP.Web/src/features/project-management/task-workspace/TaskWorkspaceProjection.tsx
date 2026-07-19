import { useQueries } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties, type DragEvent } from 'react';

import { getProjectManagementTasks } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementTaskLabelFilter, ProjectManagementTaskListItem, ProjectManagementTaskQuery } from '../../../api/project-management/projectManagement.types';
import { useAuthStore } from '../../../core/state';
import { queryKeys } from '../../../core/query/queryKeys';
import { DataTable } from '../../../shared/table/DataTable';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import type { TaskWorkspaceState } from '../state/taskWorkspaceState';
import { taskWorkspaceStateToQuery } from '../state/taskWorkspaceState';
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

import type { TaskGroupDropTarget, TaskMoveDropTarget } from './taskMoveIntent';
import { TaskCardProjection, type TaskCardDragHandlers } from './TaskCardProjection';
import { TaskBoardColumnProjection } from './TaskBoardColumnProjection';
import { summarizeTaskBoardColumns, taskBoardStatuses, type TaskBoardStatus } from './taskBoardProjectionModel';

interface TaskWorkspaceProjectionProps {
  labelFilter?: ProjectManagementTaskLabelFilter;
  milestoneLabels?: Readonly<Record<string, string>>;
  onAddChildTask?: (task: ProjectManagementTaskListItem) => void;
  onCompleteTask?: (task: ProjectManagementTaskListItem) => void;
  onChangeTaskStatus?: (task: ProjectManagementTaskListItem, status: string) => void;
  onDeleteTask?: (task: ProjectManagementTaskListItem) => void;
  onBoardRowsLoaded?: (rows: ProjectManagementTaskListItem[]) => void;
  participantLabels?: Readonly<Record<string, string>>;
  pendingTaskId?: string;
  projectId: string;
  onSelectTask: (taskId: string) => void;
  onMoveTask: (task: ProjectManagementTaskListItem, target: TaskMoveDropTarget) => void;
  onMoveTaskGroup?: (task: ProjectManagementTaskListItem, target: TaskGroupDropTarget) => void;
  onToggleTaskSelection: (taskId: string) => void;
  optimisticBoardRows?: Readonly<Record<string, ProjectManagementTaskListItem>>;
  rows: ProjectManagementTaskListItem[];
  selectedTaskIds: ReadonlySet<string>;
  state: TaskWorkspaceState;
}

export function TaskWorkspaceProjection({ labelFilter, milestoneLabels, onAddChildTask, onBoardRowsLoaded, onChangeTaskStatus, onCompleteTask, onDeleteTask, onMoveTask, onMoveTaskGroup, onSelectTask, onToggleTaskSelection, optimisticBoardRows, participantLabels, pendingTaskId, projectId, rows, selectedTaskIds, state }: TaskWorkspaceProjectionProps) {
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
  if (state.viewKey === 'gantt') return <TaskGanttProjection rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} />;
  if (state.viewKey === 'calendar') return <TaskCalendarProjection rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} />;
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

  return <DataTable columnSettingsKey={`project-management-tasks-${state.viewKey}`} columns={columns} emptyText="暂无任务" rowActions={(row) => <button type="button" onClick={() => onSelectTask(row.id)}>查看</button>} rowKey={(row) => row.id} rowVirtualize={state.viewKey === 'tree' || state.viewKey === 'list'} rowVirtualization={{ overscan: 8, rowHeight: 56 }} rows={rows} showColumnSettings />;
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

function TaskGanttProjection({ onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'>) {
  const datedRows = rows.filter((task) => task.startDate || task.dueDate);
  const [rangeDays, setRangeDays] = useState(56);
  const range = useMemo(() => createGanttRange(datedRows, rangeDays), [datedRows, rangeDays]);

  return <div className="pm-gantt">
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
      <p className="pm-prototype-note">基于真实开始/截止日期的只读计划时间轴。日期调整仍须从任务详情保存，避免将层级拖动误当作排期。</p>
      <div aria-label="甘特时间轴密度" className="flex gap-2">
        {[28, 56, 84].map((days) => <button aria-pressed={rangeDays === days} className={rangeDays === days ? 'rounded bg-blue-600 px-2 py-1 text-xs text-white' : 'rounded border border-gray-300 px-2 py-1 text-xs'} key={days} onClick={() => setRangeDays(days)} type="button">{days / 7} 周</button>)}
      </div>
    </div>
    {!datedRows.length ? <p className="pm-projection-empty">没有包含计划日期的任务。请在任务详情填写开始日期或截止日期。</p> : <div className="overflow-x-auto rounded border border-gray-200"><div className="min-w-[920px]">
      <div className="grid border-b border-gray-200 bg-gray-50 text-xs text-gray-500" style={{ gridTemplateColumns: `minmax(220px, 1fr) repeat(${range.days.length}, minmax(20px, 1fr))` }}><div className="p-2 font-medium">任务 / {formatDateOnly(range.start)} 起</div>{range.days.map((day) => <div className={day.getDay() === 0 || day.getDay() === 6 ? 'border-l border-gray-100 bg-gray-100 p-1 text-center' : 'border-l border-gray-100 p-1 text-center'} key={toDateKey(day)}>{day.getDate()}</div>)}</div>
      {datedRows.map((task) => {
        const placement = getGanttPlacement(task, range);
        return <div className="grid min-h-14 border-b border-gray-100 last:border-b-0" key={task.id} style={{ gridTemplateColumns: `minmax(220px, 1fr) repeat(${range.days.length}, minmax(20px, 1fr))` }}>
          <div className="flex items-center gap-2 p-2"><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="truncate text-left text-sm hover:underline" onClick={() => onSelectTask(task.id)} title={task.title} type="button">{task.title}</button></div>
          {placement ? <button className="my-3 min-w-0 rounded bg-blue-600 px-2 text-left text-xs text-white hover:bg-blue-700" onClick={() => onSelectTask(task.id)} style={{ gridColumn: `${placement.startColumn} / span ${placement.span}` }} title={`${task.title} · ${formatDate(task.startDate)} — ${formatDate(task.dueDate)}`} type="button">{task.taskCode}</button> : null}
        </div>;
      })}
    </div></div>}
  </div>;
}

function TaskCalendarProjection({ onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'>) {
  const [mode, setMode] = useState<'month' | 'week'>('month');
  const [anchorDate, setAnchorDate] = useState(() => firstDueDate(rows) ?? new Date());
  const groups = rows.filter((task) => task.dueDate).reduce<Record<string, ProjectManagementTaskListItem[]>>((result, task) => {
    const key = task.dueDate?.slice(0, 10) ?? '';
    result[key] = [...(result[key] ?? []), task];
    return result;
  }, {});
  const calendar = useMemo(() => createCalendarRange(anchorDate, mode), [anchorDate, mode]);

  return <div className="pm-calendar">
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
      <p className="pm-prototype-note">按真实截止日期呈现的只读{mode === 'month' ? '月' : '周'}视图；日期调整请从任务详情保存，日历中不支持拖放排期。</p>
      <div className="flex items-center gap-2"><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate((value) => moveCalendarPeriod(value, mode, -1))} type="button">上一{mode === 'month' ? '月' : '周'}</button><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate(firstDueDate(rows) ?? new Date())} type="button">回到有任务日期</button><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate((value) => moveCalendarPeriod(value, mode, 1))} type="button">下一{mode === 'month' ? '月' : '周'}</button><select aria-label="日历视图" onChange={(event) => setMode(event.target.value as 'month' | 'week')} value={mode}><option value="month">月视图</option><option value="week">周视图</option></select></div>
    </div>
    <div className="grid grid-cols-7 overflow-hidden rounded border border-gray-200"><div className="col-span-7 grid grid-cols-7 border-b border-gray-200 bg-gray-50 text-center text-xs text-gray-500">{['日', '一', '二', '三', '四', '五', '六'].map((day) => <div className="p-2" key={day}>周{day}</div>)}</div>{calendar.days.map((day) => { const key = toDateKey(day); const isCurrentPeriod = mode === 'week' || day.getMonth() === anchorDate.getMonth(); const tasks = groups[key] ?? []; return <section className={`min-h-28 border-b border-r border-gray-100 p-2 ${isCurrentPeriod ? 'bg-white' : 'bg-gray-50 text-gray-400'}`} key={key}><div className="mb-1 flex items-center justify-between text-xs"><span>{day.getDate()}</span>{tasks.length ? <span>{tasks.length} 项</span> : null}</div><div className="space-y-1">{tasks.map((task) => <div className="flex items-center gap-1" key={task.id}><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="truncate rounded bg-blue-50 px-1 py-0.5 text-left text-xs text-blue-800 hover:bg-blue-100" onClick={() => onSelectTask(task.id)} title={task.title} type="button">{task.title}</button></div>)}</div></section>; })}</div>
  </div>;
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

function createGanttRange(rows: ProjectManagementTaskListItem[], dayCount: number) {
  const dates = rows.flatMap((task) => [task.startDate, task.dueDate]).flatMap((value) => value ? [toLocalDate(value)] : []).filter((value): value is Date => Boolean(value));
  const first = dates.length ? new Date(Math.min(...dates.map((value) => value.getTime()))) : new Date();
  const start = startOfDay(first);
  return { start, days: Array.from({ length: dayCount }, (_, index) => addCalendarDays(start, index)) };
}

function getGanttPlacement(task: ProjectManagementTaskListItem, range: ReturnType<typeof createGanttRange>) {
  const start = toLocalDate(task.startDate ?? task.dueDate);
  const end = toLocalDate(task.dueDate ?? task.startDate);
  if (!start || !end) return undefined;
  const startOffset = Math.floor((start.getTime() - range.start.getTime()) / 86_400_000);
  const endOffset = Math.floor((end.getTime() - range.start.getTime()) / 86_400_000);
  const visibleStart = Math.max(0, startOffset);
  const visibleEnd = Math.min(range.days.length - 1, Math.max(visibleStart, endOffset));
  if (visibleStart > range.days.length - 1 || visibleEnd < 0) return undefined;
  return { span: Math.max(1, visibleEnd - visibleStart + 1), startColumn: visibleStart + 2 };
}

function createCalendarRange(anchor: Date, mode: 'month' | 'week') {
  const normalized = startOfDay(anchor);
  const start = mode === 'week'
    ? addCalendarDays(normalized, -normalized.getDay())
    : addCalendarDays(new Date(normalized.getFullYear(), normalized.getMonth(), 1), -new Date(normalized.getFullYear(), normalized.getMonth(), 1).getDay());
  return { days: Array.from({ length: mode === 'week' ? 7 : 42 }, (_, index) => addCalendarDays(start, index)) };
}

function firstDueDate(rows: ProjectManagementTaskListItem[]): Date | undefined {
  const values = rows.flatMap((task) => task.dueDate ? [toLocalDate(task.dueDate)] : []).filter((value): value is Date => Boolean(value));
  return values.length ? new Date(Math.min(...values.map((value) => value.getTime()))) : undefined;
}

function toLocalDate(value: string | undefined): Date | undefined {
  if (!value) return undefined;
  const date = new Date(`${value.slice(0, 10)}T00:00:00`);
  return Number.isNaN(date.getTime()) ? undefined : date;
}

function startOfDay(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate());
}

function addCalendarDays(value: Date, days: number): Date {
  const next = new Date(value);
  next.setDate(next.getDate() + days);
  return next;
}

function moveCalendarPeriod(value: Date, mode: 'month' | 'week', direction: -1 | 1): Date {
  if (mode === 'week') return addCalendarDays(value, direction * 7);
  const next = new Date(value);
  next.setMonth(next.getMonth() + direction);
  return next;
}

function toDateKey(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function formatDateOnly(value: Date): string {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
}

function toKebabCase(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
}
