import { Typography } from '@mui/material';
import { useEffect, useState } from 'react';

import { useProjectManagementI18n } from '../projectManagementI18n';

type CountdownVariant = 'compact' | 'hero';

export function ProjectManagementCountdown({
  dueDate,
  status,
  variant = 'compact',
}: {
  dueDate?: string;
  status?: string;
  variant?: CountdownVariant;
}) {
  const { format, t } = useProjectManagementI18n();
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (variant !== 'hero' || !dueDate) return undefined;
    const intervalId = window.setInterval(() => setNow(Date.now()), 60_000);
    return () => window.clearInterval(intervalId);
  }, [dueDate, variant]);

  const state = countdownState(dueDate, status, now);
  const text = state.kind === 'none'
    ? t('projectManagement.workbench.countdown.noDueDate')
    : state.kind === 'completed'
      ? t('projectManagement.workbench.countdown.completed')
      : state.kind === 'overdue'
        ? format('projectManagement.workbench.countdown.overdue', { value: state.value })
        : state.kind === 'hours'
          ? format('projectManagement.workbench.countdown.hours', { value: state.value })
          : format('projectManagement.workbench.countdown.days', { value: state.value });

  return <Typography className={`pm-countdown pm-countdown--${variant} is-${state.kind}`} component="span">{text}</Typography>;
}

function countdownState(dueDate?: string, status?: string, now = Date.now()): {
  kind: 'none' | 'completed' | 'overdue' | 'hours' | 'days';
  value: number;
} {
  if (!dueDate) return { kind: 'none', value: 0 };
  if (status === 'Done' || status === 'Cancelled' || status === 'Completed' || status === 'Archived') return { kind: 'completed', value: 0 };
  const due = new Date(dueDate).getTime();
  if (Number.isNaN(due)) return { kind: 'none', value: 0 };
  const remainingMs = due - now;
  if (remainingMs < 0) return { kind: 'overdue', value: Math.max(1, Math.ceil(-remainingMs / 86_400_000)) };
  if (remainingMs < 86_400_000) return { kind: 'hours', value: Math.max(1, Math.ceil(remainingMs / 3_600_000)) };
  return { kind: 'days', value: Math.ceil(remainingMs / 86_400_000) };
}
