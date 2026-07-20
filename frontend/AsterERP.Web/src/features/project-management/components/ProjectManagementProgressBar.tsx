import { Box, Typography } from '@mui/material';

import { isProjectManagementTaskOverdue, progressBarTone } from '../state/projectManagementStatusTransitions';

const toneColors = {
  green: '#22c55e',
  amber: '#f59e0b',
  orange: '#f97316',
  red: '#ef4444',
} as const;

export function ProjectManagementProgressBar({
  dueDate,
  progressPercent,
  showLabel = true,
  status,
}: {
  dueDate?: string;
  progressPercent: number;
  showLabel?: boolean;
  status?: string;
}) {
  const value = Math.max(0, Math.min(100, Number(progressPercent) || 0));
  const overdue = status ? isProjectManagementTaskOverdue(status, dueDate) : false;
  const color = toneColors[progressBarTone(value, overdue)];

  return (
    <Box className="pm-task-progress" sx={{ display: 'grid', gridTemplateColumns: showLabel ? 'minmax(48px, 1fr) auto' : '1fr', alignItems: 'center', gap: 0.75, minWidth: 72 }}>
      <Box sx={{ height: 6, borderRadius: 99, bgcolor: '#e2e8f0', overflow: 'hidden' }}>
        <Box sx={{ width: `${value}%`, height: '100%', bgcolor: color, borderRadius: 99, transition: 'width .2s ease' }} />
      </Box>
      {showLabel ? <Typography color="text.secondary" sx={{ fontVariantNumeric: 'tabular-nums', minWidth: 28 }} variant="caption">{Math.round(value)}%</Typography> : null}
    </Box>
  );
}
