import { useVirtualizer } from '@tanstack/react-virtual';
import { useMemo, useRef, useState } from 'react';

import type { ProjectManagementMilestone, ProjectManagementTaskDependency, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { ProjectManagementTaskCalendar } from '../calendar/ProjectManagementTaskCalendar';
import { DependencyAnalysisOverlay } from '../gantt/dependency-analysis/DependencyAnalysisOverlay';
import { DependencyImpactPreviewPanel } from '../gantt/dependency-analysis/DependencyImpactPreviewPanel';
import { previewTaskDependencyImpact } from '../gantt/dependency-analysis/dependencyAnalysisApi';
import { useDependencyAnalysis } from '../gantt/dependency-analysis/useDependencyAnalysis';
import {
  buildTaskScheduleRows,
  createScheduleWindow,
  getSchedulePlacement,
  milestonePlacement,
  shiftScheduleDate,
  type TaskScheduleRow,
} from './taskScheduleProjectionModel';

interface TaskScheduleProjectionProps {
  dependencies?: readonly ProjectManagementTaskDependency[];
  milestones?: readonly ProjectManagementMilestone[];
  onChangeTaskSchedule?: (task: ProjectManagementTaskListItem, startDate: string | undefined, dueDate: string | undefined) => void;
  onCreateTask?: (date: string) => void;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  projectId?: string;
  rows: ProjectManagementTaskListItem[];
  schedulePending?: boolean;
  selectedTaskIds: ReadonlySet<string>;
}

const dayWidth = 36;
const labelWidth = 240;

export function TaskGanttScheduleProjection({ dependencies = [], milestones = [], onChangeTaskSchedule, onSelectTask, onToggleTaskSelection, projectId, rows, selectedTaskIds }: TaskScheduleProjectionProps) {
  const dependencyAnalysisQuery = useDependencyAnalysis(projectId);
  const criticalTaskIds = useMemo(() => new Set((dependencyAnalysisQuery.data?.data?.tasks ?? []).filter((task) => task.isCritical).map((task) => task.taskId)), [dependencyAnalysisQuery.data?.data?.tasks]);
  const scheduleRows = useMemo(() => buildTaskScheduleRows(rows, dependencies).map((task) => ({ ...task, isCritical: dependencyAnalysisQuery.data ? criticalTaskIds.has(task.id) : task.isCritical })), [criticalTaskIds, dependencies, dependencyAnalysisQuery.data, rows]);
  const datedRows = scheduleRows.filter((task) => task.scheduleStartDate || task.scheduleDueDate);
  const firstDate = datedRows.map((task) => task.scheduleStartDate ?? task.scheduleDueDate).filter((value): value is string => Boolean(value)).sort()[0];
  const [rangeDays, setRangeDays] = useState(56);
  const [anchorDate, setAnchorDate] = useState(() => firstDate ? new Date(`${firstDate.slice(0, 10)}T00:00:00`) : new Date());
  const range = useMemo(() => createScheduleWindow(anchorDate, rangeDays), [anchorDate, rangeDays]);
  const scrollRef = useRef<HTMLDivElement>(null);
  const pointerRef = useRef<{ task: TaskScheduleRow; startX: number } | null>(null);
  const [impactPreview, setImpactPreview] = useState<Awaited<ReturnType<typeof previewTaskDependencyImpact>>['data']>();
  const [pendingScheduleMove, setPendingScheduleMove] = useState<{ dueDate: string; startDate: string; task: ProjectManagementTaskListItem }>();
  const [previewError, setPreviewError] = useState<string>();
  const virtualizer = useVirtualizer({ count: datedRows.length, getScrollElement: () => scrollRef.current, estimateSize: () => 58, overscan: 8 });
  const virtualItems = virtualizer.getVirtualItems();
  const contentWidth = labelWidth + range.days.length * dayWidth;
  const markers = milestonePlacement(milestones, range);
  const dependencyRows = useMemo(() => virtualItems.flatMap((virtualRow) => {
    const task = datedRows[virtualRow.index];
    const placement = task ? getSchedulePlacement(task.scheduleStartDate, task.scheduleDueDate, range) : undefined;
    return task && placement ? [{ taskId: task.id, left: labelWidth + placement.startOffset * dayWidth, top: virtualRow.start + 52, width: (placement.endOffset - placement.startOffset + 1) * dayWidth, height: 56 }] : [];
  }), [datedRows, range, virtualItems]);
  const previewScheduleMove = async (task: TaskScheduleRow, startDate: string | undefined, dueDate: string | undefined) => {
    if (!projectId || !startDate || !dueDate) {
      setPreviewError('任务必须同时具有开始和完成日期，才能预览依赖影响。');
      return;
    }
    setPreviewError(undefined);
    try {
      const response = await previewTaskDependencyImpact(projectId, { taskId: task.id, proposedStartDate: startDate, proposedDueDate: dueDate });
      setImpactPreview(response.data);
      setPendingScheduleMove({ dueDate, startDate, task });
    } catch {
      setPreviewError('无法计算依赖影响，当前日期调整未保存。请检查任务依赖诊断后重试。');
    }
  };

  if (!datedRows.length) return <div className="pm-gantt"><p className="pm-projection-empty">没有包含计划日期的任务。请在任务详情填写开始日期或截止日期。</p></div>;
  return <div className="pm-gantt">
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
      <p className="pm-prototype-note">父任务显示后代汇总条；红色条为关键路径，线段为真实依赖，菱形为里程碑。大范围计划按行虚拟渲染。</p>
      <div className="flex flex-wrap gap-2"><button className="rounded border border-gray-300 px-2 py-1 text-xs" onClick={() => setAnchorDate((value) => addDays(value, -rangeDays))} type="button">前移窗口</button><button className="rounded border border-gray-300 px-2 py-1 text-xs" onClick={() => setAnchorDate((value) => addDays(value, rangeDays))} type="button">后移窗口</button>{[28, 56, 84].map((days) => <button aria-pressed={rangeDays === days} className={rangeDays === days ? 'rounded bg-blue-600 px-2 py-1 text-xs text-white' : 'rounded border border-gray-300 px-2 py-1 text-xs'} key={days} onClick={() => setRangeDays(days)} type="button">{days / 7} 周</button>)}</div>
    </div>
    {previewError ? <p className="mb-2 text-sm text-red-600" role="alert">{previewError}</p> : null}
    {impactPreview && pendingScheduleMove ? <div className="mb-3 rounded border border-amber-300 bg-amber-50 p-3"><DependencyImpactPreviewPanel preview={impactPreview} /><div className="mt-3 flex gap-2"><button className="rounded bg-blue-600 px-3 py-1 text-sm text-white" onClick={() => { onChangeTaskSchedule?.(pendingScheduleMove.task, pendingScheduleMove.startDate, pendingScheduleMove.dueDate); setImpactPreview(undefined); setPendingScheduleMove(undefined); }} type="button">确认并保存调期</button><button className="rounded border border-gray-300 px-3 py-1 text-sm" onClick={() => { setImpactPreview(undefined); setPendingScheduleMove(undefined); }} type="button">取消</button></div></div> : null}
    <div className="overflow-auto rounded border border-gray-200" ref={scrollRef} style={{ maxHeight: '68vh' }}>
      <div className="relative" style={{ height: virtualizer.getTotalSize() + 52, minWidth: contentWidth }}>
        <div className="sticky top-0 z-20 grid border-b border-gray-200 bg-gray-50 text-xs text-gray-500" style={{ gridTemplateColumns: `${labelWidth}px repeat(${range.days.length}, ${dayWidth}px)` }}><div className="p-2 font-medium">任务 / {formatDateKey(range.start)}</div>{range.days.map((day) => <div className={day.getDay() === 0 || day.getDay() === 6 ? 'border-l border-gray-100 bg-gray-100 p-1 text-center' : 'border-l border-gray-100 p-1 text-center'} key={formatDateKey(day)}>{day.getDate()}</div>)}</div>
        {virtualItems.map((virtualRow) => {
          const task = datedRows[virtualRow.index];
          if (!task) return null;
          const placement = getSchedulePlacement(task.scheduleStartDate, task.scheduleDueDate, range);
          return <div className="absolute left-0 right-0 grid min-h-14 border-b border-gray-100" data-index={virtualRow.index} key={task.id} ref={virtualizer.measureElement} style={{ gridTemplateColumns: `${labelWidth}px repeat(${range.days.length}, ${dayWidth}px)`, top: virtualRow.start + 52 }}>
            <div className="flex items-center gap-2 p-2"><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="truncate text-left text-sm hover:underline" onClick={() => onSelectTask(task.id)} title={task.title} type="button">{task.isSummary ? '▰ ' : ''}{task.title}<span className="ml-1 text-xs text-slate-500">{task.childTaskCount ? `(${task.childTaskCount} 项)` : ''}</span>{task.isCritical ? <span className="ml-1 text-xs text-red-600">关键路径</span> : null}</button></div>
            {placement ? <button className={task.isSummary ? 'my-3 min-w-0 rounded bg-slate-500 px-2 text-left text-xs text-white' : task.isCritical ? 'my-3 min-w-0 rounded bg-red-600 px-2 text-left text-xs text-white' : 'my-3 min-w-0 rounded bg-blue-600 px-2 text-left text-xs text-white'} disabled={task.isSummary || !onChangeTaskSchedule} onClick={() => onSelectTask(task.id)} onPointerDown={(event) => { if (!task.isSummary) { event.currentTarget.setPointerCapture(event.pointerId); pointerRef.current = { task, startX: event.clientX }; } }} onPointerUp={(event) => { const pointer = pointerRef.current; pointerRef.current = null; if (!pointer || task.isSummary) return; const delta = Math.round((event.clientX - pointer.startX) / dayWidth); if (delta) void previewScheduleMove(task, shiftScheduleDate(task.startDate, delta), shiftScheduleDate(task.dueDate, delta)); }} style={{ gridColumn: `${placement.startOffset + 2} / ${placement.endOffset + 3}` }} title={`${task.title} · ${formatDate(task.scheduleStartDate)} — ${formatDate(task.scheduleDueDate)}`} type="button">{task.taskCode} · {task.progressPercent}%</button> : null}
          </div>;
        })}
        {dependencyAnalysisQuery.data?.data ? <DependencyAnalysisOverlay analysis={dependencyAnalysisQuery.data.data} height={virtualizer.getTotalSize() + 52} onSelectTask={onSelectTask} rows={dependencyRows} width={contentWidth} /> : null}
        {dependencyAnalysisQuery.isError ? <p className="absolute bottom-2 right-2 z-30 rounded bg-amber-50 px-2 py-1 text-xs text-amber-800" role="status">依赖分析暂不可用；日期调整仍会在保存前请求影响预览。</p> : null}
        {markers.map(({ milestone, offset }) => <span className="pointer-events-none absolute top-0 z-30 text-amber-600" key={milestone.id} style={{ left: labelWidth + offset * dayWidth }} title={`${milestone.milestoneName} · ${formatDate(milestone.dueDate ?? milestone.startDate)}`}>◆</span>)}
      </div>
    </div>
          </div>;
}

export function TaskCalendarScheduleProjection({ dependencies = [], milestones = [], onChangeTaskSchedule, onCreateTask, onSelectTask, onToggleTaskSelection, rows, schedulePending, selectedTaskIds }: TaskScheduleProjectionProps) {
  return <ProjectManagementTaskCalendar
    dependencies={dependencies}
    milestones={milestones}
    onChangeTaskSchedule={onChangeTaskSchedule}
    onCreateTask={onCreateTask}
    onSelectTask={onSelectTask}
    onToggleTaskSelection={onToggleTaskSelection}
    rows={rows}
    schedulePending={schedulePending}
    selectedTaskIds={selectedTaskIds}
  />;
}

function addDays(value: Date, days: number): Date { const next = new Date(value); next.setDate(next.getDate() + days); return next; }
function formatDate(value: string | undefined): string { return value ? new Date(value).toLocaleDateString() : '未设置'; }
function formatDateKey(value: Date): string { return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`; }
