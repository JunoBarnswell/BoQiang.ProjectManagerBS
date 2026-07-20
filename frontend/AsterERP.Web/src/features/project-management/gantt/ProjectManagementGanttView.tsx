import { Box, Stack, Typography } from '@mui/material';
import { useMemo, useState } from 'react';

import type { ProjectManagementTaskDependency, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { ProjectManagementProgressBar } from '../components/ProjectManagementProgressBar';
import { createScheduleWindow, buildTaskScheduleRows, type TaskScheduleRow } from '../state/projectManagementScheduleModel';
import { isProjectManagementTaskOverdue } from '../state/projectManagementStatusTransitions';

import '../projectManagement.css';

export function ProjectManagementGanttView({
  dependencies,
  onSelectTask,
  rows,
}: {
  dependencies: readonly ProjectManagementTaskDependency[];
  onSelectTask: (taskId: string) => void;
  rows: ProjectManagementTaskListItem[];
}) {
  const [dayWidth, setDayWidth] = useState(28);
  const scheduleRows = useMemo(() => buildTaskScheduleRows(rows, dependencies), [dependencies, rows]);
  const window = useMemo(() => {
    const dates = scheduleRows
      .flatMap((row) => [row.scheduleStartDate, row.scheduleDueDate])
      .filter((value): value is string => Boolean(value))
      .sort();
    if (!dates.length) return undefined;
    const start = new Date(`${dates[0].slice(0, 10)}T00:00:00`);
    const end = new Date(`${dates[dates.length - 1].slice(0, 10)}T00:00:00`);
    const dayCount = Math.max(14, Math.round((end.getTime() - start.getTime()) / 86_400_000) + 3);
    return createScheduleWindow(new Date(start.getTime() - 86_400_000), Math.min(90, dayCount));
  }, [scheduleRows]);
  const days = window?.days ?? [];

  if (!window || !days.length) {
    return (
      <Box className="pm-gantt pm-gantt--empty" sx={{ p: 4, textAlign: 'center' }}>
        <Typography color="text.secondary">暂无带日期的任务，请先设置开始/截止日期。</Typography>
      </Box>
    );
  }

  return (
    <Box className="pm-gantt" sx={{ display: 'flex', flexDirection: 'column', gap: 1, minHeight: 0, overflow: 'auto' }}>
      <Stack alignItems="center" direction="row" justifyContent="space-between" sx={{ px: 0.5 }}>
        <Typography color="text.secondary" variant="caption">
          {days[0].toISOString().slice(0, 10)} — {days[days.length - 1].toISOString().slice(0, 10)}
        </Typography>
        <Stack direction="row" spacing={1}>
          <button className="pm-workbench-command" onClick={() => setDayWidth((value) => Math.max(12, value - 4))} type="button">缩小</button>
          <button className="pm-workbench-command" onClick={() => setDayWidth((value) => Math.min(56, value + 4))} type="button">放大</button>
        </Stack>
      </Stack>
      <Box sx={{ display: 'grid', gridTemplateColumns: '240px minmax(0, 1fr)', minWidth: 720, border: '1px solid #e7eaf0', borderRadius: 2, overflow: 'hidden' }}>
        <Box sx={{ borderRight: '1px solid #e7eaf0', bgcolor: '#f8fafc' }}>
          <Box sx={{ height: 36, px: 1.5, display: 'flex', alignItems: 'center', borderBottom: '1px solid #e7eaf0', fontSize: 12, color: '#64748b' }}>任务</Box>
          {scheduleRows.map((row) => (
            <button
              className="pm-gantt-label"
              key={row.id}
              onClick={() => onSelectTask(row.id)}
              style={{ paddingLeft: 12 + row.depth * 14 }}
              type="button"
            >
              <span className="pm-gantt-label__code">{row.taskCode}</span>
              <span className="pm-gantt-label__title">{row.title}</span>
            </button>
          ))}
        </Box>
        <Box sx={{ overflowX: 'auto' }}>
          <Box sx={{ minWidth: days.length * dayWidth }}>
            <Box sx={{ display: 'grid', gridTemplateColumns: `repeat(${days.length}, ${dayWidth}px)`, height: 36, borderBottom: '1px solid #e7eaf0', bgcolor: '#f8fafc' }}>
              {days.map((day) => (
                <Box key={day.toISOString()} sx={{ borderRight: '1px solid #eef2f7', fontSize: 10, color: '#94a3b8', display: 'grid', placeItems: 'center' }}>
                  {day.getUTCDate()}
                </Box>
              ))}
            </Box>
            {scheduleRows.map((row) => (
              <GanttBarRow dayWidth={dayWidth} days={days} key={row.id} onSelect={() => onSelectTask(row.id)} row={row} windowStart={window.start} />
            ))}
          </Box>
        </Box>
      </Box>
    </Box>
  );
}

function GanttBarRow({
  dayWidth,
  days,
  onSelect,
  row,
  windowStart,
}: {
  dayWidth: number;
  days: Date[];
  onSelect: () => void;
  row: TaskScheduleRow;
  windowStart: Date;
}) {
  const start = row.scheduleStartDate ?? row.scheduleDueDate;
  const due = row.scheduleDueDate ?? row.scheduleStartDate;
  const overdue = isProjectManagementTaskOverdue(row.status, row.dueDate);
  let left = 0;
  let width = dayWidth;
  if (start && due) {
    const startOffset = dayOffset(windowStart, start);
    const endOffset = dayOffset(windowStart, due);
    left = Math.max(0, startOffset) * dayWidth;
    width = Math.max(1, endOffset - Math.max(0, startOffset) + 1) * dayWidth;
  }
  const barColor = overdue ? '#ef4444' : row.status === 'Done' ? '#22c55e' : row.status === 'InProgress' ? '#3b82f6' : row.isCritical ? '#f97316' : '#94a3b8';

  return (
    <Box className="pm-gantt-row" sx={{ position: 'relative', height: 36, borderBottom: '1px solid #f1f5f9' }}>
      <Box sx={{ display: 'grid', gridTemplateColumns: `repeat(${days.length}, ${dayWidth}px)`, position: 'absolute', inset: 0 }}>
        {days.map((day) => (
          <Box key={day.toISOString()} sx={{ borderRight: '1px solid #f8fafc' }} />
        ))}
      </Box>
      {start && due ? (
        <button
          aria-label={`${row.title} ${start} — ${due}`}
          onClick={onSelect}
          style={{
            position: 'absolute',
            left,
            top: 7,
            width,
            height: 22,
            border: 0,
            borderRadius: 5,
            background: barColor,
            color: '#fff',
            cursor: 'pointer',
            overflow: 'hidden',
            textAlign: 'left',
            padding: '0 6px',
            fontSize: 11,
          }}
          title={`${row.taskCode} · ${row.title} · ${Math.round(row.progressPercent)}%`}
          type="button"
        >
          <Box sx={{ position: 'absolute', inset: 0, width: `${row.progressPercent}%`, bgcolor: 'rgba(255,255,255,.35)' }} />
          <span style={{ position: 'relative', zIndex: 1, whiteSpace: 'nowrap' }}>{row.title}</span>
        </button>
      ) : (
        <Box sx={{ position: 'absolute', left: 8, top: 8, width: 120 }}>
          <ProjectManagementProgressBar progressPercent={row.progressPercent} status={row.status} />
        </Box>
      )}
    </Box>
  );
}

function dayOffset(windowStart: Date, isoDate: string): number {
  const target = Date.UTC(Number(isoDate.slice(0, 4)), Number(isoDate.slice(5, 7)) - 1, Number(isoDate.slice(8, 10)));
  const start = Date.UTC(windowStart.getUTCFullYear(), windowStart.getUTCMonth(), windowStart.getUTCDate());
  return Math.round((target - start) / 86_400_000);
}
