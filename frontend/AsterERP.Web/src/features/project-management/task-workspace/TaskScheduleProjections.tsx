import { useVirtualizer } from '@tanstack/react-virtual';
import { useMemo, useRef, useState, type MutableRefObject, type PointerEvent } from 'react';

import type { ProjectManagementMilestone, ProjectManagementTaskDependency, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { ProjectManagementTaskCalendar } from '../calendar/ProjectManagementTaskCalendar';
import { previewTaskDependencyImpact } from '../gantt/dependency-analysis/dependencyAnalysisApi';
import { DependencyAnalysisOverlay } from '../gantt/dependency-analysis/DependencyAnalysisOverlay';
import { DependencyImpactPreviewPanel } from '../gantt/dependency-analysis/DependencyImpactPreviewPanel';
import { useDependencyAnalysis } from '../gantt/dependency-analysis/useDependencyAnalysis';
import { updateGanttSchedule } from '../gantt/ganttSchedule.api';

import {
  buildTaskScheduleRows,
  adjustTaskSchedule,
  buildSubtreeScheduleChanges,
  createScheduleWindow,
  getSchedulePlacement,
  milestonePlacement,
  type GanttScheduleEditMode, type TaskScheduleRow,
} from './taskScheduleProjectionModel';

interface TaskScheduleProjectionProps {
  dependencies?: readonly ProjectManagementTaskDependency[];
  ganttZoom?: 28 | 56 | 84;
  milestones?: readonly ProjectManagementMilestone[];
  onChangeTaskSchedule?: (task: ProjectManagementTaskListItem, startDate: string | undefined, dueDate: string | undefined) => void;
  onCreateTask?: (date: string) => void;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  onGanttScheduleSaved?: () => Promise<void> | void;
  onGanttZoomChange?: (zoom: 28 | 56 | 84) => void;
  projectId?: string;
  rows: ProjectManagementTaskListItem[];
  schedulePending?: boolean;
  selectedTaskIds: ReadonlySet<string>;
}

const dayWidth = 36;
const labelWidth = 240;

export function TaskGanttScheduleProjection({ dependencies = [], ganttZoom = 56, milestones = [], onChangeTaskSchedule, onGanttScheduleSaved, onGanttZoomChange, onSelectTask, onToggleTaskSelection, projectId, rows, selectedTaskIds }: TaskScheduleProjectionProps) {
  const dependencyAnalysisQuery = useDependencyAnalysis(projectId);
  const criticalTaskIds = useMemo(() => new Set((dependencyAnalysisQuery.data?.data?.tasks ?? []).filter((task) => task.isCritical).map((task) => task.taskId)), [dependencyAnalysisQuery.data?.data?.tasks]);
  const scheduleRows = useMemo(() => buildTaskScheduleRows(rows, dependencies).map((task) => ({ ...task, isCritical: dependencyAnalysisQuery.data ? criticalTaskIds.has(task.id) : task.isCritical })), [criticalTaskIds, dependencies, dependencyAnalysisQuery.data, rows]);
  const datedRows = useMemo(() => scheduleRows.filter((task) => task.scheduleStartDate || task.scheduleDueDate), [scheduleRows]);
  const unscheduledRows = useMemo(() => scheduleRows.filter((task) => !task.scheduleStartDate && !task.scheduleDueDate), [scheduleRows]);
  const ganttRows = useMemo(() => [...datedRows, ...unscheduledRows], [datedRows, unscheduledRows]);
  const firstDate = datedRows.map((task) => task.scheduleStartDate ?? task.scheduleDueDate).filter((value): value is string => Boolean(value)).sort()[0];
  const lastDate = datedRows.map((task) => task.scheduleDueDate ?? task.scheduleStartDate).filter((value): value is string => Boolean(value)).sort().at(-1);
  const rangeDays = ganttZoom;
  const [anchorDate, setAnchorDate] = useState(() => firstDate ? new Date(`${firstDate.slice(0, 10)}T00:00:00`) : new Date());
  const [rangeMode, setRangeMode] = useState<'window' | 'project'>('window');
  const range = useMemo(() => {
    if (rangeMode !== 'project' || !firstDate || !lastDate) return createScheduleWindow(anchorDate, rangeDays);
    const start = new Date(`${firstDate.slice(0, 10)}T00:00:00`);
    const end = new Date(`${lastDate.slice(0, 10)}T00:00:00`);
    const days = Math.max(1, Math.floor((end.getTime() - start.getTime()) / 86_400_000) + 1);
    return createScheduleWindow(start, days);
  }, [anchorDate, firstDate, lastDate, rangeDays, rangeMode]);
  const scrollRef = useRef<HTMLDivElement>(null);
  const pointerRef = useRef<{ mode: GanttScheduleEditMode; task: TaskScheduleRow; startX: number } | null>(null);
  const [impactPreview, setImpactPreview] = useState<Awaited<ReturnType<typeof previewTaskDependencyImpact>>['data']>();
  const [pendingScheduleMove, setPendingScheduleMove] = useState<{ change: ReturnType<typeof adjustTaskSchedule>; includeSubtree: boolean; task: TaskScheduleRow }>();
  const [savingSchedule, setSavingSchedule] = useState(false);
  const [previewError, setPreviewError] = useState<string>();
  const virtualizer = useVirtualizer({ count: ganttRows.length, getScrollElement: () => scrollRef.current, estimateSize: () => 58, overscan: 8 });
  const virtualItems = virtualizer.getVirtualItems();
  const contentWidth = labelWidth + range.days.length * dayWidth;
  const markers = milestonePlacement(milestones, range);
  const dependencyRows = useMemo(() => virtualItems.flatMap((virtualRow) => {
    const task = ganttRows[virtualRow.index];
    const placement = task ? getSchedulePlacement(task.scheduleStartDate, task.scheduleDueDate, range) : undefined;
    return task && placement ? [{ taskId: task.id, left: labelWidth + placement.startOffset * dayWidth, top: virtualRow.start + 52, width: (placement.endOffset - placement.startOffset + 1) * dayWidth, height: 56 }] : [];
  }), [ganttRows, range, virtualItems]);
  const previewScheduleMove = async (task: TaskScheduleRow, change: ReturnType<typeof adjustTaskSchedule>) => {
    if (!projectId || !change) {
      setPreviewError('任务必须同时具有开始和完成日期，才能预览依赖影响。');
      return;
    }
    setPreviewError(undefined);
    try {
      const response = await previewTaskDependencyImpact(projectId, { taskId: task.id, proposedStartDate: change.startDate, proposedDueDate: change.dueDate });
      setImpactPreview(response.data);
      setPendingScheduleMove({ change, includeSubtree: false, task });
    } catch {
      setPreviewError('无法计算依赖影响，当前日期调整未保存。请检查任务依赖诊断后重试。');
    }
  };
  const savePendingSchedule = async () => {
    if (!projectId || !pendingScheduleMove?.change) return;
    const dayDelta = calendarDayDelta(pendingScheduleMove.task.startDate, pendingScheduleMove.change.startDate);
    const items = pendingScheduleMove.includeSubtree && dayDelta !== undefined
      ? buildSubtreeScheduleChanges(pendingScheduleMove.task.id, scheduleRows, dayDelta)
      : [pendingScheduleMove.change];
    if (!items.length) return;
    setSavingSchedule(true); setPreviewError(undefined);
    try {
      await updateGanttSchedule({ projectId, items });
      await onGanttScheduleSaved?.();
      setImpactPreview(undefined); setPendingScheduleMove(undefined);
    } catch {
      setPreviewError('任务调期失败或存在并发冲突，已保留原甘特条并刷新跨视图缓存。');
      await onGanttScheduleSaved?.();
    } finally { setSavingSchedule(false); }
  };

  if (!scheduleRows.length) return <div className="pm-gantt"><p className="pm-projection-empty">当前项目暂无任务。</p></div>;
  return <div className="pm-gantt">
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
      <p className="pm-prototype-note">父任务显示后代汇总条；红色条为关键路径，线段为真实依赖，菱形为里程碑。大范围计划按行虚拟渲染。</p>
      <div className="flex flex-wrap gap-2"><button className="rounded border border-gray-300 px-2 py-1 text-xs" onClick={() => { setRangeMode('window'); setAnchorDate((value) => addDays(value, -rangeDays)); }} type="button">前移窗口</button><button className="rounded border border-gray-300 px-2 py-1 text-xs" onClick={() => { setRangeMode('window'); setAnchorDate((value) => addDays(value, rangeDays)); }} type="button">后移窗口</button><button className="rounded border border-gray-300 px-2 py-1 text-xs" onClick={() => { setRangeMode('window'); setAnchorDate(new Date()); }} type="button">返回今天</button><button aria-pressed={rangeMode === 'project'} className={rangeMode === 'project' ? 'rounded bg-blue-600 px-2 py-1 text-xs text-white' : 'rounded border border-gray-300 px-2 py-1 text-xs'} onClick={() => setRangeMode('project')} type="button">项目全范围</button>{([28, 56, 84] as const).map((days) => <button aria-pressed={rangeMode === 'window' && rangeDays === days} className={rangeMode === 'window' && rangeDays === days ? 'rounded bg-blue-600 px-2 py-1 text-xs text-white' : 'rounded border border-gray-300 px-2 py-1 text-xs'} key={days} onClick={() => { setRangeMode('window'); onGanttZoomChange?.(days); }} type="button">{days / 7} 周</button>)}</div>
    </div>
    {previewError ? <p className="mb-2 text-sm text-red-600" role="alert">{previewError}</p> : null}
    {impactPreview && pendingScheduleMove ? <div className="mb-3 rounded border border-amber-300 bg-amber-50 p-3"><DependencyImpactPreviewPanel preview={impactPreview} /><label className="mt-3 flex items-center gap-2 text-sm"><input checked={pendingScheduleMove.includeSubtree} disabled={!pendingScheduleMove.task.hasChildren} onChange={(event) => setPendingScheduleMove((current) => current ? { ...current, includeSubtree: event.target.checked } : current)} type="checkbox" />同时平移子树（仅手动日期任务）</label><div className="mt-3 flex gap-2"><button className="rounded bg-blue-600 px-3 py-1 text-sm text-white" disabled={savingSchedule} onClick={() => void savePendingSchedule()} type="button">{savingSchedule ? '保存中…' : '确认并保存调期'}</button><button className="rounded border border-gray-300 px-3 py-1 text-sm" disabled={savingSchedule} onClick={() => { setImpactPreview(undefined); setPendingScheduleMove(undefined); }} type="button">取消</button></div></div> : null}
    <div className="overflow-auto rounded border border-gray-200" ref={scrollRef} style={{ maxHeight: '68vh' }}>
      <div className="relative" style={{ height: virtualizer.getTotalSize() + 52, minWidth: contentWidth }}>
        <div className="sticky top-0 z-20 grid border-b border-gray-200 bg-gray-50 text-xs text-gray-500" style={{ gridTemplateColumns: `${labelWidth}px repeat(${range.days.length}, ${dayWidth}px)` }}><div className="p-2 font-medium">任务 / {formatDateKey(range.start)}</div>{range.days.map((day) => <div className={day.getDay() === 0 || day.getDay() === 6 ? 'border-l border-gray-100 bg-gray-100 p-1 text-center' : 'border-l border-gray-100 p-1 text-center'} key={formatDateKey(day)}>{day.getDate()}</div>)}</div>
        {virtualItems.map((virtualRow) => {
          const task = ganttRows[virtualRow.index];
          if (!task) return null;
          const placement = getSchedulePlacement(task.scheduleStartDate, task.scheduleDueDate, range);
          return <div className="absolute left-0 right-0 grid min-h-14 border-b border-gray-100" data-index={virtualRow.index} key={task.id} ref={virtualizer.measureElement} style={{ gridTemplateColumns: `${labelWidth}px repeat(${range.days.length}, ${dayWidth}px)`, top: virtualRow.start + 52 }}>
            <div className="flex items-center gap-2 p-2"><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="truncate text-left text-sm hover:underline" onClick={() => onSelectTask(task.id)} title={task.title} type="button">{task.isSummary ? '▰ ' : ''}{task.title}<span className="ml-1 text-xs text-slate-500">{task.childTaskCount ? `(${task.childTaskCount} 项)` : ''}</span>{task.isCritical ? <span className="ml-1 text-xs text-red-600">关键路径</span> : null}</button></div>
            {placement ? <div className={task.isSummary ? 'my-3 flex min-w-0 rounded bg-slate-500 text-xs text-white' : task.isCritical ? 'my-3 flex min-w-0 rounded bg-red-600 text-xs text-white' : 'my-3 flex min-w-0 rounded bg-blue-600 text-xs text-white'} style={{ gridColumn: `${placement.startOffset + 2} / ${placement.endOffset + 3}` }} title={`${task.title} · ${formatDate(task.scheduleStartDate)} — ${formatDate(task.scheduleDueDate)}`}><span aria-label={`调整 ${task.title} 开始日期`} aria-orientation="horizontal" aria-valuemax={placement.endOffset} aria-valuemin={placement.startOffset} className="cursor-ew-resize px-1" onPointerDown={(event) => startSchedulePointer(event, task, 'resize-start', pointerRef)} onPointerUp={(event) => finishSchedulePointer(event, pointerRef, previewScheduleMove)} role="slider" tabIndex={task.isSummary && !task.hasManualSchedule ? -1 : 0}>⋮</span><button className="min-w-0 flex-1 truncate px-1 text-left" disabled={task.isSummary && !task.hasManualSchedule || !onChangeTaskSchedule} onClick={() => onSelectTask(task.id)} onPointerDown={(event) => startSchedulePointer(event, task, 'move', pointerRef)} onPointerUp={(event) => finishSchedulePointer(event, pointerRef, previewScheduleMove)} type="button">{task.taskCode} · {task.progressPercent}%</button><span aria-label={`调整 ${task.title} 完成日期`} aria-orientation="horizontal" aria-valuemax={placement.endOffset} aria-valuemin={placement.startOffset} className="cursor-ew-resize px-1" onPointerDown={(event) => startSchedulePointer(event, task, 'resize-end', pointerRef)} onPointerUp={(event) => finishSchedulePointer(event, pointerRef, previewScheduleMove)} role="slider" tabIndex={task.isSummary && !task.hasManualSchedule ? -1 : 0}>⋮</span></div> : <span className="my-3 flex items-center px-2 text-xs italic text-slate-400" style={{ gridColumn: '2 / -1' }}>待排期</span>}
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
function calendarDayDelta(from: string | undefined, to: string): number | undefined { const fromTime = from ? Date.parse(`${from.slice(0, 10)}T00:00:00Z`) : Number.NaN; const toTime = Date.parse(`${to.slice(0, 10)}T00:00:00Z`); return Number.isFinite(fromTime) && Number.isFinite(toTime) ? Math.round((toTime - fromTime) / 86_400_000) : undefined; }
function startSchedulePointer(event: PointerEvent<HTMLElement>, task: TaskScheduleRow, mode: GanttScheduleEditMode, pointerRef: MutableRefObject<{ mode: GanttScheduleEditMode; task: TaskScheduleRow; startX: number } | null>) { if (task.isSummary && !task.hasManualSchedule) return; event.currentTarget.setPointerCapture(event.pointerId); pointerRef.current = { mode, task, startX: event.clientX }; }
function finishSchedulePointer(event: PointerEvent<HTMLElement>, pointerRef: MutableRefObject<{ mode: GanttScheduleEditMode; task: TaskScheduleRow; startX: number } | null>, preview: (task: TaskScheduleRow, change: ReturnType<typeof adjustTaskSchedule>) => Promise<void>) { const pointer = pointerRef.current; pointerRef.current = null; if (!pointer) return; const delta = Math.round((event.clientX - pointer.startX) / dayWidth); if (delta) void preview(pointer.task, adjustTaskSchedule(pointer.task, pointer.mode, delta)); }
