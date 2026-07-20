import { Box, Typography } from '@mui/material';

import { isProjectManagementTaskOverdue, progressBarTone } from '../state/projectManagementStatusTransitions';

const toneColors = {
  green: '#22c55e',
  amber: '#f59e0b',
  orange: '#f97316',
  red: '#ef4444',
} as const;

export function ProjectManagementProgressBar({
  compact = false,
  dueDate,
  progressPercent,
  showLabel = true,
  status,
}: {
  compact?: boolean;
  dueDate?: string;
  progressPercent: number;
  showLabel?: boolean;
  status?: string;
}) {
  const value = Math.max(0, Math.min(100, Number(progressPercent) || 0));
  const overdue = status ? isProjectManagementTaskOverdue(status, dueDate) : false;
  const color = toneColors[progressBarTone(value, overdue)];

  return (
    <Box className="pm-task-progress" sx={{ display: 'grid', gridTemplateColumns: showLabel ? 'minmax(48px, 1fr) auto' : '1fr', alignItems: 'center', gap: compact ? 0.5 : 0.75, minWidth: compact ? 0 : 72 }}>
      <Box sx={{ height: compact ? 4 : 6, borderRadius: 99, bgcolor: 'var(--app-gray-200)', overflow: 'hidden' }}>
        <Box sx={{ width: `${value}%`, height: '100%', bgcolor: color, borderRadius: 99, transition: 'width .2s ease' }} />
      </Box>
      {showLabel ? <Typography color="text.secondary" sx={{ fontSize: compact ? 'var(--app-text-3xs)' : undefined, fontVariantNumeric: 'tabular-nums', minWidth: compact ? 24 : 28 }} variant="caption">{Math.round(value)}%</Typography> : null}
    </Box>
  );
}
