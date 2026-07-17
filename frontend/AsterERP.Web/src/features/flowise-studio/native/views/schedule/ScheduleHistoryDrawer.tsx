import DragHandleIcon from '@mui/icons-material/DragHandle';
import { Alert, Box, Chip, CircularProgress, Divider, Drawer, IconButton, Stack, Tooltip, Typography } from '@mui/material';
import { IconCalendarEvent, IconClockHour4, IconRefresh, IconX } from '@tabler/icons-react';
import { useCallback, useEffect, useState } from 'react';

import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { chatflowsApi } from '../../../api/chatflows.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseScheduleStatusDto, FlowiseScheduleTriggerLogDto } from '../../../types/chatflow.types';

export interface FlowiseScheduleInfo {
  description: string;
  enabled: boolean;
  type: string;
}

interface ScheduleHistoryDrawerProps {
  flowKind: 'agentflows' | 'chatflows';
  open: boolean;
  resourceId: string;
  scheduleInfo: FlowiseScheduleInfo;
  translate: (key: string) => string;
  onClose: () => void;
}

const minDrawerWidth = 480;
const defaultDrawerWidth = 720;
const maxDrawerWidth = typeof window === 'undefined' ? 1920 : window.innerWidth;

export function ScheduleHistoryDrawer({ flowKind, open, resourceId, scheduleInfo, translate, onClose }: ScheduleHistoryDrawerProps) {
  const [drawerWidth, setDrawerWidth] = useState(() => Math.min(defaultDrawerWidth, maxDrawerWidth));
  const statusQuery = useApiQuery({
    enabled: open && Boolean(resourceId),
    queryKey: ['flowise', flowKind, resourceId, 'schedule', 'status'],
    queryFn: ({ signal }) => chatflowsApi.schedule.status(flowKind, resourceId, signal)
  });
  const logsQuery = useApiQuery({
    enabled: open && Boolean(resourceId),
    queryKey: ['flowise', flowKind, resourceId, 'schedule', 'logs'],
    queryFn: ({ signal }) => chatflowsApi.schedule.logs(flowKind, resourceId, { pageIndex: 1, pageSize: 20 }, signal)
  });
  const status = statusQuery.data?.data;
  const logs = logsQuery.data?.data.items ?? [];
  const isScheduleEnabled = status?.enabled ?? scheduleInfo.enabled;
  const inputMode = status?.scheduleInputMode || scheduleInfo.type;
  const statusLabel = isScheduleEnabled ? translate(flowiseI18nKeys.status.enabled) : translate(flowiseI18nKeys.status.disabled);
  const inputLabel = translate(isScheduleEnabled ? flowiseI18nKeys.canvas.scheduleInput : flowiseI18nKeys.canvas.manualInput);
  const refreshing = statusQuery.isFetching || logsQuery.isFetching;

  const handleMouseMove = useCallback((event: MouseEvent) => {
    const nextWidth = document.body.offsetWidth - event.clientX;
    if (nextWidth >= minDrawerWidth && nextWidth <= maxDrawerWidth) {
      setDrawerWidth(nextWidth);
    }
  }, []);

  const handleMouseUp = useCallback(() => {
    document.removeEventListener('mousemove', handleMouseMove);
    document.removeEventListener('mouseup', handleMouseUp);
    document.body.style.userSelect = '';
    document.body.style.cursor = '';
  }, [handleMouseMove]);

  const handleMouseDown = useCallback(() => {
    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'ew-resize';
    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
  }, [handleMouseMove, handleMouseUp]);

  useEffect(() => {
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
      document.body.style.userSelect = '';
      document.body.style.cursor = '';
    };
  }, [handleMouseMove, handleMouseUp]);

  const refresh = async () => {
    await Promise.all([statusQuery.refetch(), logsQuery.refetch()]);
  };

  return (
    <Drawer
      anchor="right"
      open={open}
      variant="temporary"
      slotProps={{
        paper: {
          sx: {
            bgcolor: 'background.default',
            borderLeft: '1px solid',
            borderColor: 'divider',
            display: 'flex',
            flexDirection: 'column',
            height: '100%',
            maxWidth: 'var(--erp-raw-viewport-width)',
            overflow: 'hidden',
            width: drawerWidth
          }
        }
      }}
      onClose={onClose}
    >
      <button
        aria-label={translate(flowiseI18nKeys.actions.resize)}
        style={{
          alignItems: 'center',
          background: 'transparent',
          border: 'none',
          bottom: 0,
          cursor: 'ew-resize',
          display: 'flex',
          justifyContent: 'center',
          left: 0,
          padding: 0,
          position: 'absolute',
          top: 0,
          width: 8,
          zIndex: 1
        }}
        type="button"
        onMouseDown={handleMouseDown}
      >
        <DragHandleIcon sx={{ color: 'text.disabled', fontSize: 20, transform: 'rotate(90deg)' }} />
      </button>
      <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 3, py: 2 }}>
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            <IconCalendarEvent size={22} />
            <Box>
              <Typography variant="h6">{translate(flowiseI18nKeys.canvas.scheduleHistory)}</Typography>
              <Typography color="text.secondary" variant="body2">
                {inputLabel}
              </Typography>
            </Box>
          </Stack>
          <IconButton aria-label={translate(flowiseI18nKeys.actions.close)} onClick={onClose}>
            <IconX size={20} />
          </IconButton>
        </Stack>
        <Stack direction="row" spacing={2.5} sx={{ borderBottom: '1px solid', borderColor: 'divider', px: 3, pb: 2 }}>
          <Box>
            <Typography color="text.secondary" variant="caption">
              {translate(flowiseI18nKeys.fields.status)}
            </Typography>
            <Box sx={{ mt: 0.5 }}>
              <Chip color={isScheduleEnabled ? 'success' : 'default'} label={statusLabel} size="small" variant={isScheduleEnabled ? 'filled' : 'outlined'} />
            </Box>
          </Box>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography color="text.secondary" variant="caption">
              {translate(flowiseI18nKeys.fields.type)}
            </Typography>
            <Tooltip title={inputMode}>
              <Typography noWrap sx={{ mt: 0.5 }} variant="body2">
                {inputMode}
              </Typography>
            </Tooltip>
          </Box>
          <Tooltip title={translate(flowiseI18nKeys.actions.refresh)}>
            <span>
              <IconButton disabled={refreshing} size="small" onClick={() => void refresh()}>
                {refreshing ? <CircularProgress size={16} /> : <IconRefresh size={16} />}
              </IconButton>
            </span>
          </Tooltip>
        </Stack>
        <Divider />
        <Box sx={{ flex: 1, overflow: 'auto', p: 3 }}>
          <Stack spacing={2.5}>
            {statusQuery.isError || logsQuery.isError ? (
              <Alert severity="error">{translate(flowiseI18nKeys.messages.loadFailed)}</Alert>
            ) : null}
            <Box>
              <Typography color="text.secondary" variant="caption">
                {translate(flowiseI18nKeys.fields.description)}
              </Typography>
              <Typography sx={{ mt: 0.5 }} variant="body1">
                {scheduleInfo.description || '-'}
              </Typography>
            </Box>
            <ScheduleStatusDetails scheduleInfo={scheduleInfo} status={status} translate={translate} />
            <Divider />
            <ScheduleLogList logs={logs} loading={logsQuery.isLoading} translate={translate} />
          </Stack>
        </Box>
      </Box>
    </Drawer>
  );
}

function ScheduleStatusDetails({
  scheduleInfo,
  status,
  translate
}: {
  scheduleInfo: FlowiseScheduleInfo;
  status?: FlowiseScheduleStatusDto;
  translate: (key: string) => string;
}) {
  const rows = [
    { label: translate(flowiseI18nKeys.fields.type), value: status?.scheduleInputMode || scheduleInfo.type },
    { label: 'Cron', value: status?.cronExpression || '-' },
    { label: 'Timezone', value: status?.timezone || '-' },
    { label: 'Last run', value: formatDateTime(status?.lastRunAt) },
    { label: 'Next run', value: formatDateTime(status?.nextRunAt) },
    { label: 'End date', value: formatDateTime(status?.endDate) },
    { label: 'Default input', value: status?.defaultInput || '-' }
  ];

  return (
    <Box
      sx={{
        border: '1px solid',
        borderColor: 'divider',
        borderRadius: 2,
        display: 'grid',
        gap: 1.25,
        gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))' },
        p: 2
      }}
    >
      {rows.map((row) => (
        <Box key={row.label} sx={{ minWidth: 0 }}>
          <Typography color="text.secondary" variant="caption">
            {row.label}
          </Typography>
          <Typography noWrap title={row.value} variant="body2">
            {row.value}
          </Typography>
        </Box>
      ))}
    </Box>
  );
}

function ScheduleLogList({
  loading,
  logs,
  translate
}: {
  loading: boolean;
  logs: FlowiseScheduleTriggerLogDto[];
  translate: (key: string) => string;
}) {
  if (loading) {
    return (
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', color: 'text.secondary' }}>
        <CircularProgress size={18} />
        <Typography variant="body2">{translate(flowiseI18nKeys.status.running)}</Typography>
      </Stack>
    );
  }

  if (logs.length === 0) {
    return (
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', color: 'text.secondary' }}>
        <IconClockHour4 size={18} />
        <Typography variant="body2">{translate(flowiseI18nKeys.messages.noScheduleRuns)}</Typography>
      </Stack>
    );
  }

  return (
    <Stack spacing={1.5}>
      {logs.map((log) => (
        <Box key={log.id} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2, p: 2 }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
            <Typography sx={{ fontWeight: 600 }} variant="body2">
              {formatDateTime(log.scheduledAt)}
            </Typography>
            <Chip color={statusColor(log.status)} label={log.status} size="small" variant="outlined" />
          </Stack>
          <Box sx={{ display: 'grid', gap: 1, gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, minmax(0, 1fr))' }, mt: 1.5 }}>
            <LogField label="Execution" value={log.executionId || '-'} />
            <LogField label="Started" value={formatDateTime(log.startedAt)} />
            <LogField label="Completed" value={formatDateTime(log.completedAt)} />
            <LogField label="Error" value={log.error || '-'} />
          </Box>
        </Box>
      ))}
    </Stack>
  );
}

function LogField({ label, value }: { label: string; value: string }) {
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography color="text.secondary" variant="caption">
        {label}
      </Typography>
      <Typography noWrap title={value} variant="body2">
        {value}
      </Typography>
    </Box>
  );
}

function statusColor(status: string): 'default' | 'error' | 'success' | 'warning' {
  const normalized = status.toLowerCase();
  if (normalized === 'completed' || normalized === 'success') {
    return 'success';
  }
  if (normalized === 'failed' || normalized === 'error') {
    return 'error';
  }
  if (normalized === 'running' || normalized === 'queued') {
    return 'warning';
  }
  return 'default';
}

function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
