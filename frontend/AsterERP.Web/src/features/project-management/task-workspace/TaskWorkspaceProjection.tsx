import { useMemo, useState, type DragEvent } from 'react';

import type { ProjectManagementTask } from '../../../api/project-management/projectManagement.types';
import { DataTable } from '../../../shared/table/DataTable';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import type { TaskWorkspaceState } from '../state/taskWorkspaceState';

import type { TaskMoveDropTarget } from './taskMoveIntent';

interface TaskWorkspaceProjectionProps {
  onSelectTask: (taskId: string) => void;
  onMoveTask: (task: ProjectManagementTask, target: TaskMoveDropTarget) => void;
  onToggleTaskSelection: (taskId: string) => void;
  rows: ProjectManagementTask[];
  selectedTaskIds: ReadonlySet<string>;
  state: TaskWorkspaceState;
}

export function TaskWorkspaceProjection({ onMoveTask, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds, state }: TaskWorkspaceProjectionProps) {
  const [draggedTask, setDraggedTask] = useState<ProjectManagementTask>();
  const drag = {
    draggedTaskId: draggedTask?.id,
    onDragEnd: () => setDraggedTask(undefined),
    onDragOver: (event: DragEvent<HTMLElement>) => event.preventDefault(),
    onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTask) => {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', task.id);
      setDraggedTask(task);
    },
    onDrop: (event: DragEvent<HTMLElement>, target: TaskMoveDropTarget) => {
      event.preventDefault();
      event.stopPropagation();
      if (draggedTask) onMoveTask(draggedTask, target);
      setDraggedTask(undefined);
    },
  };
  const rootDropZone = <div aria-label="拖到此处成为顶级任务" className="mb-3 rounded border border-dashed border-blue-300 bg-blue-50 px-3 py-2 text-sm text-blue-800" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'root' })}>拖到此处成为顶级任务</div>;
  if (state.viewKey === 'board') return <>{rootDropZone}<TaskBoardProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} /></>;
  if (state.viewKey === 'card') return <>{rootDropZone}<TaskCardProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} /></>;
  if (state.viewKey === 'gantt') return <>{rootDropZone}<TaskGanttProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} /></>;
  if (state.viewKey === 'calendar') return <>{rootDropZone}<TaskCalendarProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} /></>;
  return <>{rootDropZone}<TaskTableProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} state={state} /></>;
}

interface TaskDragHandlers {
  draggedTaskId?: string;
  onDragEnd: () => void;
  onDragOver: (event: DragEvent<HTMLElement>) => void;
  onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTask) => void;
  onDrop: (event: DragEvent<HTMLElement>, target: TaskMoveDropTarget) => void;
}

function TaskTableProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds, state }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds' | 'state'> & { drag: TaskDragHandlers }) {
  const columns = useMemo<DataTableColumn<ProjectManagementTask>[]>(() => [
    { key: 'select', title: '选择', width: '64px', render: (row) => <input aria-label={`选择任务 ${row.title}`} checked={selectedTaskIds.has(row.id)} type="checkbox" onChange={() => onToggleTaskSelection(row.id)} /> },
    { key: 'taskCode', title: '编码', width: '120px', responsivePriority: 100 },
    { key: 'title', title: '任务', responsivePriority: 100, render: (row) => <div draggable className={drag.draggedTaskId === row.id ? 'opacity-50' : undefined} onDragEnd={drag.onDragEnd} onDragOver={drag.onDragOver} onDragStart={(event) => drag.onDragStart(event, row)} onDrop={(event) => drag.onDrop(event, { kind: 'before', task: row })} style={{ paddingLeft: `${state.viewKey === 'tree' ? row.depth * 16 : 0}px` }}><span>{row.title}</span><span className="ml-2 rounded border border-dashed border-gray-300 px-1 text-xs text-gray-500" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task: row })}>作为子任务</span></div> },
    { key: 'status', title: '状态', width: '110px' },
    { key: 'priority', title: '优先级', width: '90px' },
    { key: 'progressPercent', title: '进度', width: '80px', render: (row) => `${row.progressPercent}%` },
    { key: 'dueDate', title: '截止日期', width: '120px', render: (row) => row.dueDate ? new Date(row.dueDate).toLocaleDateString() : '-' },
    { key: 'blockedByCount', title: '阻塞', width: '80px', render: (row) => row.blockedByCount ? `${row.blockedByCount} 项` : '否' },
  ], [drag, onToggleTaskSelection, selectedTaskIds, state.viewKey]);

  return (
    <DataTable
      columnSettingsKey={`project-management-tasks-${state.viewKey}`}
      columns={columns}
      emptyText="暂无任务"
      rowActions={(row) => <button type="button" onClick={() => onSelectTask(row.id)}>查看</button>}
      rowKey={(row) => row.id}
      rows={rows}
      showColumnSettings
    />
  );
}

function TaskCardProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'> & { drag: TaskDragHandlers }) {
  return <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">{rows.map((task) => <TaskCard drag={drag} key={task.id} selected={selectedTaskIds.has(task.id)} task={task} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} />)}</div>;
}

function TaskBoardProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'> & { drag: TaskDragHandlers }) {
  const groups = ['Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled'].map((status) => ({ status, rows: rows.filter((task) => task.status === status) }));
  return <div className="grid gap-3 md:grid-cols-5">{groups.map((group) => <section className="rounded-lg border border-gray-200 p-3" key={group.status}><h3 className="mb-2 font-semibold">{group.status} ({group.rows.length})</h3>{group.rows.length ? group.rows.map((task) => <TaskCard drag={drag} key={task.id} selected={selectedTaskIds.has(task.id)} task={task} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} />) : <p className="text-sm text-gray-500">暂无任务</p>}</section>)}</div>;
}

function TaskGanttProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'> & { drag: TaskDragHandlers }) {
  const datedRows = rows.filter((task) => task.startDate || task.dueDate);
  return <div className="space-y-2 rounded-lg border border-gray-200 p-3">{datedRows.length ? datedRows.map((task) => <div className={drag.draggedTaskId === task.id ? 'flex gap-2 opacity-50' : 'flex gap-2'} draggable key={task.id} onDragEnd={drag.onDragEnd} onDragOver={drag.onDragOver} onDragStart={(event) => drag.onDragStart(event, task)} onDrop={(event) => drag.onDrop(event, { kind: 'before', task })}><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="grid w-full grid-cols-[minmax(12rem,1fr)_2fr] gap-3 rounded border border-gray-100 p-3 text-left" onClick={() => onSelectTask(task.id)} type="button"><span>{task.title}</span><span className="rounded bg-blue-100 px-2 py-1 text-sm text-blue-800">{formatDate(task.startDate)} — {formatDate(task.dueDate)}</span></button><span className="rounded border border-dashed border-gray-300 px-2 py-1 text-xs text-gray-500" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task })}>作为子任务</span></div>) : <p className="text-sm text-gray-500">没有包含计划日期的任务</p>}</div>;
}

function TaskCalendarProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'> & { drag: TaskDragHandlers }) {
  const groups = rows.filter((task) => task.dueDate).reduce<Record<string, ProjectManagementTask[]>>((result, task) => {
    const key = task.dueDate?.slice(0, 10) ?? '';
    result[key] = [...(result[key] ?? []), task];
    return result;
  }, {});
  const dates = Object.keys(groups).sort();
  return <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">{dates.length ? dates.map((date) => <section className="rounded-lg border border-gray-200 p-3" key={date}><h3 className="font-semibold">{date}</h3><div className="mt-2 space-y-2">{groups[date].map((task) => <TaskCard drag={drag} key={task.id} selected={selectedTaskIds.has(task.id)} task={task} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} />)}</div></section>) : <p className="text-sm text-gray-500">没有包含截止日期的任务</p>}</div>;
}

function TaskCard({ drag, onSelectTask, onToggleTaskSelection, selected, task }: { drag: TaskDragHandlers; onSelectTask: (taskId: string) => void; onToggleTaskSelection: (taskId: string) => void; selected: boolean; task: ProjectManagementTask }) {
  return <article className={drag.draggedTaskId === task.id ? 'mb-2 flex gap-2 rounded border border-gray-200 p-3 opacity-50' : 'mb-2 flex gap-2 rounded border border-gray-200 p-3'} draggable onDragEnd={drag.onDragEnd} onDragOver={drag.onDragOver} onDragStart={(event) => drag.onDragStart(event, task)} onDrop={(event) => drag.onDrop(event, { kind: 'before', task })}><input aria-label={`选择任务 ${task.title}`} checked={selected} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="min-w-0 flex-1 text-left" onClick={() => onSelectTask(task.id)} type="button"><div className="text-xs text-gray-500">{task.taskCode}</div><div className="font-medium">{task.title}</div><div className="mt-1 text-xs text-gray-500">{task.progressPercent}% · {task.canStart ? '可开始' : task.blockedReason ?? '阻塞'}</div></button><span className="rounded border border-dashed border-gray-300 px-1 text-xs text-gray-500" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task })}>作为子任务</span></article>;
}

function formatDate(value: string | undefined): string {
  return value ? new Date(value).toLocaleDateString() : '未设置';
}
