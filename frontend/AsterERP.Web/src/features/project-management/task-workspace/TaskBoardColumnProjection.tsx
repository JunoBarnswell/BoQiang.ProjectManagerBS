import { useQuery } from '@tanstack/react-query';
import { useVirtualizer } from '@tanstack/react-virtual';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { getProjectManagementTasks } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementTaskListItem, ProjectManagementTaskQuery } from '../../../api/project-management/projectManagement.types';
import { queryKeys } from '../../../core/query/queryKeys';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { buildTaskBoardColumnQuery, taskBoardStatuses, type TaskBoardStatus } from './taskBoardProjectionModel';
import { TaskCard, type TaskCardDragHandlers } from './TaskCardProjection';
import { groupTaskCards, type TaskCardGroup } from './taskCardProjectionModel';
import type { TaskGroupBy } from './taskMoveIntent';

interface TaskBoardColumnProjectionProps {
  baseQuery: ProjectManagementTaskQuery;
  drag: TaskCardDragHandlers;
  groupBy?: ProjectManagementTaskQuery['groupBy'];
  groupDropBy?: TaskGroupBy;
  milestoneLabels?: Readonly<Record<string, string>>;
  onAddChildTask?: (task: ProjectManagementTaskListItem) => void;
  onChangeTaskStatus?: (task: ProjectManagementTaskListItem, status: string) => void;
  onCompleteTask?: (task: ProjectManagementTaskListItem) => void;
  onDeleteTask?: (task: ProjectManagementTaskListItem) => void;
  onMetricChange?: (metric: { loaded: number; status: TaskBoardStatus; total?: number }) => void;
  onRowsLoaded?: (rows: ProjectManagementTaskListItem[]) => void;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  participantLabels?: Readonly<Record<string, string>>;
  pendingTaskId?: string;
  scope: ProjectManagementWorkspaceScope;
  selectedTaskIds: ReadonlySet<string>;
  status: TaskBoardStatus;
  optimisticRows?: Readonly<Record<string, ProjectManagementTaskListItem>>;
}

type BoardVirtualItem =
  | { kind: 'lane'; lane: TaskCardGroup }
  | { kind: 'task'; laneKey: string; task: ProjectManagementTaskListItem };

export function TaskBoardColumnProjection({ baseQuery, drag, groupBy, groupDropBy, milestoneLabels, onAddChildTask, onChangeTaskStatus, onCompleteTask, onDeleteTask, onMetricChange, onRowsLoaded, onSelectTask, onToggleTaskSelection, optimisticRows, participantLabels, pendingTaskId, scope, selectedTaskIds, status }: TaskBoardColumnProjectionProps) {
  const [pageIndex, setPageIndex] = useState(1);
  const [loadedRows, setLoadedRows] = useState<Map<string, ProjectManagementTaskListItem>>(() => new Map());
  const scrollRef = useRef<HTMLDivElement>(null);
  const querySignature = JSON.stringify({ ...baseQuery, status });
  const pageQuery = useMemo(() => buildTaskBoardColumnQuery(baseQuery, status, pageIndex), [baseQuery, pageIndex, status]);
  const pageQueryResult = useQuery({
    enabled: scope.isAvailable && Boolean(baseQuery.projectId),
    queryFn: ({ signal }) => getProjectManagementTasks(pageQuery, signal),
    queryKey: [...queryKeys.projectManagement.tasks(scope, pageQuery), JSON.stringify(pageQuery.labelFilter ?? null)],
  });

  useEffect(() => {
    setPageIndex(1);
    setLoadedRows(new Map());
  }, [querySignature]);

  useEffect(() => {
    const page = pageQueryResult.data?.data?.items;
    if (!page) return;
    setLoadedRows((current) => {
      const next = new Map(current);
      page.forEach((row) => next.set(row.id, row));
      return next;
    });
  }, [pageQueryResult.data?.data?.items]);

  const rows = useMemo(() => {
    const merged = new Map(loadedRows);
    Object.values(optimisticRows ?? {}).forEach((task) => {
      if (task.status === status) merged.set(task.id, task);
      else merged.delete(task.id);
    });
    return [...merged.values()].filter((task) => task.status === status);
  }, [loadedRows, optimisticRows, status]);
  const total = pageQueryResult.data?.data?.total;
  const hasMore = typeof total === 'number' && rows.length < total;
  const groups = useMemo(() => groupTaskCards(rows, groupBy), [groupBy, rows]);
  const virtualRows = useMemo<BoardVirtualItem[]>(() => groups.flatMap((lane) => [
    ...(groupBy ? [{ kind: 'lane' as const, lane }] : []),
    ...lane.rows.map((task) => ({ kind: 'task' as const, laneKey: lane.key, task })),
  ]), [groupBy, groups]);
  const virtualizer = useVirtualizer({
    count: virtualRows.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: (index) => virtualRows[index]?.kind === 'lane' ? 40 : 226,
    overscan: 5,
  });

  useEffect(() => {
    onRowsLoaded?.(rows);
  }, [onRowsLoaded, rows]);

  useEffect(() => {
    onMetricChange?.({ loaded: rows.length, status, total });
  }, [onMetricChange, rows.length, status, total]);

  const loadMore = useCallback(() => {
    if (hasMore && !pageQueryResult.isFetching) setPageIndex((current) => current + 1);
  }, [hasMore, pageQueryResult.isFetching]);
  const handleScroll = useCallback(() => {
    const element = scrollRef.current;
    if (element && element.scrollTop + element.clientHeight >= element.scrollHeight - 180) loadMore();
  }, [loadMore]);

  return <section aria-label={`${status} 看板列`} className="pm-board-column">
    <header><strong>{status}</strong><span>{typeof total === 'number' ? total : rows.length}</span></header>
    {pageQueryResult.isError && rows.length ? <p role="alert">本列刷新失败。<button type="button" onClick={() => { void pageQueryResult.refetch(); }}>重试</button></p> : null}
    {!rows.length && pageQueryResult.isPending ? <p className="pm-board-column__body" role="status">加载中…</p> : null}
    {!rows.length && !pageQueryResult.isPending && !pageQueryResult.isError ? <p className="pm-board-column__body">暂无任务</p> : null}
    {rows.length ? <div className="pm-board-column__scroll" onScroll={handleScroll} ref={scrollRef}>
      <div className="pm-board-column__virtual-content" style={{ height: virtualizer.getTotalSize(), position: 'relative' }}>
        {virtualizer.getVirtualItems().map((virtualRow) => {
          const item = virtualRows[virtualRow.index];
          if (!item) return null;
          return <div className="pm-board-column__virtual-item" data-index={virtualRow.index} key={`${item.kind}-${item.kind === 'lane' ? item.lane.key : item.task.id}`} ref={virtualizer.measureElement} style={{ position: 'absolute', left: 0, right: 0, top: 0, transform: `translateY(${virtualRow.start}px)` }}>
            {item.kind === 'lane'
              ? <div
                className="pm-board-lane__heading"
                onDragOver={groupDropBy ? drag.onDragOver : undefined}
                onDrop={groupDropBy ? (event) => drag.onDrop(event, { kind: 'group', groupBy: groupDropBy, groupValue: item.lane.key, status }) : undefined}
              >
                <strong>{item.lane.label}</strong><span>{item.lane.rows.length}</span>
              </div>
              : <TaskCard drag={drag} milestoneLabels={milestoneLabels} onAddChildTask={onAddChildTask} onChangeTaskStatus={onChangeTaskStatus} onCompleteTask={onCompleteTask} onDeleteTask={onDeleteTask} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} participantLabels={participantLabels} pending={pendingTaskId === item.task.id} selected={selectedTaskIds.has(item.task.id)} statusOptions={taskBoardStatuses} task={item.task} />}
          </div>;
        })}
      </div>
    </div> : null}
    {hasMore ? <button className="pm-board-column__load-more" disabled={pageQueryResult.isFetching} onClick={loadMore} type="button">{pageQueryResult.isFetching ? '加载中…' : '加载更多'}</button> : null}
  </section>;
}
