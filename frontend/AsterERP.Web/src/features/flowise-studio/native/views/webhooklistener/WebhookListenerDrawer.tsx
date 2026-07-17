import DragHandleIcon from '@mui/icons-material/DragHandle';
import { Box, Button, Chip, Collapse, Divider, Drawer, IconButton, Stack, Tooltip, Typography } from '@mui/material';
import { IconArrowsMaximize, IconArrowsMinimize, IconChevronRight, IconCircleCheck, IconCopy, IconWebhook, IconX } from '@tabler/icons-react';
import { useCallback, useEffect, useMemo, useState } from 'react';

import { webhookListenerApi } from '../../../api/webhookListener.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseWebhookStreamEvent } from '../../../types/webhookListener.types';

interface WebhookListenerDrawerProps {
  chatflowId: string;
  endpoint: string;
  open: boolean;
  translate: (key: string) => string;
  webhookSecret: string;
  onClose: () => void;
  onCopyEndpoint: () => void;
}

const minDrawerWidth = 380;
const defaultDrawerWidth = 460;
const maxDrawerWidth = typeof window === 'undefined' ? 900 : Math.min(900, window.innerWidth - 80);
const maxVisibleEvents = 80;

export function WebhookListenerDrawer({ chatflowId, endpoint, open, translate, webhookSecret, onClose, onCopyEndpoint }: WebhookListenerDrawerProps) {
  const [copiedCurl, setCopiedCurl] = useState(false);
  const [drawerWidth, setDrawerWidth] = useState(defaultDrawerWidth);
  const [events, setEvents] = useState<FlowiseWebhookStreamEvent[]>([]);
  const [listenerId, setListenerId] = useState('');
  const [maximized, setMaximized] = useState(false);
  const [streamError, setStreamError] = useState('');
  const [streamStatus, setStreamStatus] = useState<'closed' | 'connecting' | 'listening'>('closed');
  const [showCurl, setShowCurl] = useState(false);
  const curlSnippet = useMemo(() => `curl -X POST '${endpoint}' \\\n  -H 'Content-Type: application/json' \\\n  -d '{ "question": "Hello from cURL" }'`, [endpoint]);
  const effectiveWidth = maximized ? Math.min(720, maxDrawerWidth) : drawerWidth;

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

  useEffect(() => {
    if (!open || !chatflowId) {
      return undefined;
    }

    const controller = new AbortController();
    let activeListenerId = '';
    setEvents([]);
    setStreamError('');
    setStreamStatus('connecting');
    setListenerId('');

    void webhookListenerApi.register(chatflowId)
      .then(async (response) => {
        if (controller.signal.aborted) {
          return;
        }

        activeListenerId = response.data.listenerId;
        setListenerId(activeListenerId);
        setStreamStatus('listening');
        await webhookListenerApi.stream(
          chatflowId,
          activeListenerId,
          (event) => {
            setEvents((current) => [event, ...current].slice(0, maxVisibleEvents));
          },
          controller.signal
        );
      })
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setStreamStatus('closed');
          setStreamError(error instanceof Error ? error.message : String(error));
        }
      });

    return () => {
      controller.abort();
      if (activeListenerId) {
        void webhookListenerApi.unregister(chatflowId, activeListenerId);
      }
    };
  }, [chatflowId, open]);

  const copyCurl = async () => {
    await navigator.clipboard.writeText(curlSnippet);
    setCopiedCurl(true);
    window.setTimeout(() => setCopiedCurl(false), 1200);
  };

  return (
    <Drawer
      anchor="right"
      hideBackdrop
      open={open}
      variant="persistent"
      slotProps={{
        paper: {
          sx: {
            bgcolor: 'background.default',
            borderLeft: '1px solid',
            borderColor: 'divider',
            boxShadow: '-8px 0 24px rgba(15,23,42,0.08)',
            display: 'flex',
            flexDirection: 'column',
            height: 'calc(var(--erp-raw-viewport-height) - calc(70px * var(--app-ui-scale)))',
            maxWidth: 'var(--erp-raw-viewport-width)',
            overflow: 'hidden',
            top: 'calc(70px * var(--app-ui-scale))',
            transition: 'width 180ms ease',
            width: effectiveWidth
          }
        }
      }}
      sx={{ pointerEvents: 'none', '& .MuiDrawer-paper': { pointerEvents: 'auto' } }}
      onClose={onClose}
    >
      <button
        aria-label={translate(flowiseI18nKeys.actions.resize)}
        style={{
          background: 'transparent',
          border: 'none',
          bottom: 0,
          cursor: 'ew-resize',
          left: 0,
          padding: 0,
          position: 'absolute',
          top: 0,
          width: 6,
          zIndex: 2
        }}
        type="button"
        onMouseDown={handleMouseDown}
      >
        <DragHandleIcon sx={{ color: 'text.disabled', fontSize: 16, left: -4, position: 'absolute', top: '50%', transform: 'translateY(-50%) rotate(90deg)' }} />
      </button>
      <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 2.5, py: 2 }}>
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            <IconWebhook size={22} />
            <Box>
              <Typography sx={{ letterSpacing: '0.12em', textTransform: 'uppercase' }} variant="caption">
                {translate(flowiseI18nKeys.canvas.webhookListener)}
              </Typography>
              <Typography color="text.secondary" variant="body2">
                {streamError || translate(streamStatus === 'connecting' ? flowiseI18nKeys.status.running : flowiseI18nKeys.messages.webhookWaiting)}
              </Typography>
            </Box>
          </Stack>
          <Tooltip title={translate(maximized ? flowiseI18nKeys.actions.restore : flowiseI18nKeys.actions.expand)}>
            <IconButton aria-label={translate(maximized ? flowiseI18nKeys.actions.restore : flowiseI18nKeys.actions.expand)} size="small" onClick={() => setMaximized((value) => !value)}>
              {maximized ? <IconArrowsMinimize size={18} /> : <IconArrowsMaximize size={18} />}
            </IconButton>
          </Tooltip>
          <IconButton aria-label={translate(flowiseI18nKeys.actions.close)} onClick={onClose}>
            <IconX size={20} />
          </IconButton>
        </Stack>
        <Divider />
        <Box sx={{ flex: 1, overflow: 'auto', p: 3 }}>
          <Stack spacing={2.5}>
            <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
              <Typography color="text.secondary" variant="body2">
                {translate(flowiseI18nKeys.fields.status)}
              </Typography>
              <Chip
                color={streamError ? 'error' : streamStatus === 'listening' ? 'success' : 'default'}
                label={streamError ? translate(flowiseI18nKeys.status.error) : translate(flowiseI18nKeys.status.running)}
                size="small"
                variant="outlined"
              />
            </Stack>
            <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
              <Typography color="text.secondary" variant="body2">
                {translate(flowiseI18nKeys.detail.listenerId)}
              </Typography>
              <Box component="code" sx={{ fontSize: 12, maxWidth: 260, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={listenerId || '-'}>
                {listenerId || '-'}
              </Box>
            </Stack>
            <Box>
              <Typography color="text.secondary" variant="body2">
                {translate(flowiseI18nKeys.detail.apiEndpoint)}
              </Typography>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', bgcolor: 'action.hover', border: '1px solid', borderColor: 'divider', borderRadius: 1, mt: 0.75, p: 1 }}>
                <Chip label="POST" size="small" sx={{ fontFamily: 'monospace', fontSize: 10, fontWeight: 700, height: 20 }} />
                <Box component="code" sx={{ flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={endpoint}>
                  {endpoint}
                </Box>
                <Tooltip title={translate(flowiseI18nKeys.actions.copy)}>
                  <IconButton size="small" onClick={onCopyEndpoint}>
                    <IconCopy size={16} />
                  </IconButton>
                </Tooltip>
              </Stack>
              <Button
                size="small"
                startIcon={<IconChevronRight size={14} style={{ transform: showCurl ? 'rotate(90deg)' : 'none', transition: 'transform 120ms' }} />}
                sx={{ mt: 1, textTransform: 'none' }}
                onClick={() => setShowCurl((value) => !value)}
              >
                {translate(flowiseI18nKeys.canvas.curlExample)}
              </Button>
              <Collapse in={showCurl} timeout="auto" unmountOnExit>
                <Box
                  component="pre"
                  sx={{
                    bgcolor: 'action.hover',
                    border: '1px solid',
                    borderColor: 'divider',
                    borderRadius: 1,
                    fontFamily: 'monospace',
                    fontSize: 12,
                    lineHeight: 1.55,
                    mt: 0.5,
                    overflow: 'auto',
                    p: 1.5,
                    position: 'relative',
                    whiteSpace: 'pre-wrap'
                  }}
                >
                  {curlSnippet}
                  <Tooltip title={translate(copiedCurl ? flowiseI18nKeys.actions.copied : flowiseI18nKeys.actions.copy)}>
                    <IconButton size="small" sx={{ position: 'absolute', right: 6, top: 6 }} onClick={() => void copyCurl()}>
                      {copiedCurl ? <IconCircleCheck size={14} /> : <IconCopy size={14} />}
                    </IconButton>
                  </Tooltip>
                </Box>
              </Collapse>
            </Box>
            <Box>
              <Typography color="text.secondary" variant="body2">
                {translate(flowiseI18nKeys.detail.webhookSecret)}
              </Typography>
              <Box
                component="code"
                sx={{
                  bgcolor: 'action.hover',
                  border: '1px solid',
                  borderColor: 'divider',
                  borderRadius: 1,
                  display: 'block',
                  mt: 0.75,
                  overflowWrap: 'anywhere',
                  p: 1.25
                }}
              >
                {webhookSecret}
              </Box>
            </Box>
            <Box>
              <Typography color="text.secondary" sx={{ mb: 1 }} variant="body2">
                {translate(flowiseI18nKeys.detail.events)}
              </Typography>
              {events.length === 0 ? (
                <Box sx={{ border: '1px dashed', borderColor: 'divider', borderRadius: 1, color: 'text.secondary', p: 2, textAlign: 'center' }}>
                  {translate(flowiseI18nKeys.messages.webhookWaiting)}
                </Box>
              ) : (
                <Stack spacing={1}>
                  {events.map((item, index) => (
                    <Box
                      key={`${item.event}-${index}`}
                      sx={{
                        bgcolor: 'action.hover',
                        border: '1px solid',
                        borderColor: 'divider',
                        borderRadius: 1,
                        p: 1.25
                      }}
                    >
                      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 0.75 }}>
                        <Chip label={item.event} size="small" sx={{ fontFamily: 'monospace', fontSize: 10, fontWeight: 700, height: 20 }} />
                      </Stack>
                      <Box component="pre" sx={{ fontFamily: 'monospace', fontSize: 12, m: 0, overflow: 'auto', whiteSpace: 'pre-wrap' }}>
                        {formatEventPayload(item.data)}
                      </Box>
                    </Box>
                  ))}
                </Stack>
              )}
            </Box>
          </Stack>
        </Box>
      </Box>
    </Drawer>
  );
}

function formatEventPayload(value: unknown): string {
  if (typeof value === 'string') {
    return value;
  }

  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}
