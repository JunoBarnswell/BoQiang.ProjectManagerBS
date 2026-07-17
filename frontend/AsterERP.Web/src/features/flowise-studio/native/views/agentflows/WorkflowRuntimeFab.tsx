import { Badge, Fab, Tooltip } from '@mui/material';
import { keyframes } from '@mui/system';
import { IconHistory, IconMessageChatbot, IconWebhook } from '@tabler/icons-react';

import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';

export type WorkflowRuntimeKind = 'chat' | 'schedule' | 'webhook';

interface WorkflowRuntimeFabProps {
  kind: WorkflowRuntimeKind;
  translate: (key: string) => string;
  onOpenChat: () => void;
  onOpenSchedule: () => void;
  onOpenWebhook: () => void;
}

const pulseSlow = keyframes`
  0%, 100% { transform: scale(1); opacity: 1; }
  50% { transform: scale(1.5); opacity: 0.35; }
`;

export function WorkflowRuntimeFab({
  kind,
  translate,
  onOpenChat,
  onOpenSchedule,
  onOpenWebhook
}: WorkflowRuntimeFabProps) {
  const runtime = resolveRuntime(kind, translate, onOpenChat, onOpenSchedule, onOpenWebhook);

  return (
    <Tooltip title={runtime.title}>
      <Badge
        color={runtime.badgeColor}
        invisible={runtime.badgeHidden}
        overlap="circular"
        variant="dot"
        sx={{
          position: 'absolute',
          right: 20,
          top: 20,
          pointerEvents: 'auto',
          zIndex: 20,
          '& .MuiBadge-dot': {
            animation: runtime.pulse ? `${pulseSlow} 1.4s ease-in-out infinite` : 'none',
            boxShadow: '0 0 0 2px var(--app-card, #fff)'
          }
        }}
      >
        <Fab
          aria-label={runtime.ariaLabel}
          color="secondary"
          size="small"
          sx={{
            minHeight: 40,
            pointerEvents: 'auto',
            width: 40
          }}
          onMouseDown={(event) => event.stopPropagation()}
          onPointerDown={(event) => event.stopPropagation()}
          onClick={runtime.onClick}
        >
          {runtime.icon}
        </Fab>
      </Badge>
    </Tooltip>
  );
}

function resolveRuntime(
  kind: WorkflowRuntimeKind,
  translate: (key: string) => string,
  onOpenChat: () => void,
  onOpenSchedule: () => void,
  onOpenWebhook: () => void
) {
  if (kind === 'schedule') {
    return {
      ariaLabel: 'schedule-history',
      badgeColor: 'warning' as const,
      badgeHidden: false,
      icon: <IconHistory />,
      onClick: onOpenSchedule,
      pulse: true,
      title: translate(flowiseI18nKeys.canvas.scheduleHistory)
    };
  }

  if (kind === 'webhook') {
    return {
      ariaLabel: 'webhook-listener',
      badgeColor: 'success' as const,
      badgeHidden: false,
      icon: <IconWebhook size={18} stroke={2.2} />,
      onClick: onOpenWebhook,
      pulse: true,
      title: translate(flowiseI18nKeys.canvas.webhookListener)
    };
  }

  return {
    ariaLabel: translate(flowiseI18nKeys.actions.chatTest),
    badgeColor: 'secondary' as const,
    badgeHidden: true,
    icon: <IconMessageChatbot size={18} stroke={2.2} />,
    onClick: onOpenChat,
    pulse: false,
    title: translate(flowiseI18nKeys.actions.chatTest)
  };
}
