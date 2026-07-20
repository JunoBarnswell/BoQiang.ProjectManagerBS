import { Box, Typography } from '@mui/material';
import { useMemo } from 'react';

import { useProjectManagementI18n } from '../projectManagementI18n';
import { computeScheduleUrgencyMetrics } from '../state/projectManagementScheduleUrgency';

export function ProjectManagementScheduleTimelineBar({
  compact = false,
  dueDate,
  layout = 'inline',
  showLabel = true,
  startDate,
  status,
}: {
  compact?: boolean;
  dueDate?: string;
  layout?: 'inline' | 'stack';
  showLabel?: boolean;
  startDate?: string;
  status?: string;
}) {
  const { format, t } = useProjectManagementI18n();
  const metrics = useMemo(
    () => computeScheduleUrgencyMetrics(startDate, dueDate, status),
    [dueDate, startDate, status],
  );

  const label = useMemo(() => {
    if (metrics.tone === 'none') return '—';
    if (metrics.tone === 'completed') return t('projectManagement.workbench.countdown.completed');
    if (metrics.tone === 'overdue') {
      return format('projectManagement.workbench.countdown.overdue', { value: metrics.remainingDays });
    }
    return format('projectManagement.workbench.countdown.days', { value: metrics.remainingDays });
  }, [format, metrics, t]);

  const value = metrics.tone === 'none' ? 0 : Math.max(0, Math.min(100, metrics.progressPercent));
  const stacked = layout === 'stack';

  return (
    <Box
      aria-label={label}
      className={`pm-schedule-timeline${stacked ? ' is-stack' : ''}`}
      sx={{
        display: 'grid',
        gridTemplateColumns: stacked || !showLabel ? '1fr' : 'minmax(48px, 1fr) auto',
        gridTemplateRows: stacked && showLabel ? 'auto auto' : undefined,
        alignItems: 'center',
        gap: stacked ? 0.375 : compact ? 0.5 : 0.75,
        minWidth: compact ? 0 : stacked ? 0 : 72,
        width: stacked ? '100%' : undefined,
      }}
    >
      <Box sx={{ height: compact ? 4 : 6, borderRadius: 99, bgcolor: 'var(--app-gray-200)', overflow: 'hidden', minWidth: 0 }}>
        <Box
          sx={{
            width: `${value}%`,
            height: '100%',
            bgcolor: metrics.urgencyColor,
            borderRadius: 99,
            transition: 'width .2s ease, background-color .2s ease',
          }}
        />
      </Box>
      {showLabel ? (
        <Typography
          color="text.secondary"
          sx={{
            fontSize: compact || stacked ? 'var(--app-text-3xs)' : undefined,
            fontVariantNumeric: 'tabular-nums',
            lineHeight: 1.35,
            minWidth: stacked ? 0 : compact ? 36 : 44,
            whiteSpace: 'nowrap',
          }}
          variant="caption"
        >
          {label}
        </Typography>
      ) : null}
    </Box>
  );
}
