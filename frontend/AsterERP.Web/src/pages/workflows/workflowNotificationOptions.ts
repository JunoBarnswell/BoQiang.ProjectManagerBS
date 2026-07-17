import type { TranslateFn } from './workflowNotificationsTypes';

export function channelTypeLabel(value: string, translate: TranslateFn) {
  const label = ({
    'in-app': translate('page.workflowNotifications.option.channelType.inApp'),
    email: translate('page.workflowNotifications.option.channelType.email'),
    webhook: translate('page.workflowNotifications.option.channelType.webhook')
  } as Record<string, string>)[value];
  return label ?? value ?? '-';
}

export function failurePolicyLabel(value: string, translate: TranslateFn) {
  const label = ({
    block: translate('page.workflowNotifications.option.failurePolicy.block'),
    ignore: translate('page.workflowNotifications.option.failurePolicy.ignore')
  } as Record<string, string>)[value];
  return label ?? value ?? '-';
}

export function triggerLabel(value: string, translate: TranslateFn) {
  const label = ({
    'node-enter': translate('page.workflowNotifications.option.trigger.nodeEnter'),
    'process-end': translate('page.workflowNotifications.option.trigger.processEnd'),
    'process-start': translate('page.workflowNotifications.option.trigger.processStart'),
    'task-complete': translate('page.workflowNotifications.option.trigger.taskComplete'),
    timeout: translate('page.workflowNotifications.option.trigger.timeout')
  } as Record<string, string>)[value];
  return label ?? value ?? '-';
}

export function receiverLabel(value: string, translate: TranslateFn) {
  const label = ({
    approver: translate('page.workflowNotifications.option.receiverType.approver'),
    department: translate('page.workflowNotifications.option.receiverType.department'),
    deptManager: translate('page.workflowNotifications.option.receiverType.deptManager'),
    dynamic: translate('page.workflowNotifications.option.receiverType.dynamic'),
    manager: translate('page.workflowNotifications.option.receiverType.manager'),
    position: translate('page.workflowNotifications.option.receiverType.position'),
    role: translate('page.workflowNotifications.option.receiverType.role'),
    starter: translate('page.workflowNotifications.option.receiverType.starter'),
    user: translate('page.workflowNotifications.option.receiverType.user')
  } as Record<string, string>)[value];
  return label ?? value ?? '-';
}

export function notificationTaskStatusLabel(value: string, translate: TranslateFn) {
  const label = ({
    cancelled: translate('page.workflowNotifications.option.taskStatus.cancelled'),
    failed: translate('page.workflowNotifications.option.taskStatus.failed'),
    pending: translate('page.workflowNotifications.option.taskStatus.pending'),
    queued: translate('page.workflowNotifications.option.taskStatus.queued'),
    retrying: translate('page.workflowNotifications.option.taskStatus.retrying'),
    sending: translate('page.workflowNotifications.option.taskStatus.sending'),
    sent: translate('page.workflowNotifications.option.taskStatus.sent')
  } as Record<string, string>)[normalizeStatus(value)];
  return label ?? value ?? '-';
}

export function notificationLogResultLabel(value: string, translate: TranslateFn) {
  const label = ({
    failed: translate('page.workflowNotifications.option.logResult.failed'),
    ignored: translate('page.workflowNotifications.option.logResult.ignored'),
    pending: translate('page.workflowNotifications.option.logResult.pending'),
    skipped: translate('page.workflowNotifications.option.logResult.skipped'),
    success: translate('page.workflowNotifications.option.logResult.success')
  } as Record<string, string>)[normalizeStatus(value)];
  return label ?? value ?? '-';
}

export function channelTypeOptions(translate: TranslateFn) {
  return [
    { label: translate('page.workflowNotifications.option.channelType.inApp'), value: 'in-app' },
    { label: translate('page.workflowNotifications.option.channelType.email'), value: 'email' },
    { label: translate('page.workflowNotifications.option.channelType.webhook'), value: 'webhook' }
  ];
}

export function failurePolicyOptions(translate: TranslateFn) {
  return [
    { label: translate('page.workflowNotifications.option.failurePolicy.ignore'), value: 'ignore' },
    { label: translate('page.workflowNotifications.option.failurePolicy.block'), value: 'block' }
  ];
}

export function triggerOptions(translate: TranslateFn) {
  return [
    { label: translate('page.workflowNotifications.option.trigger.processStart'), value: 'process-start' },
    { label: translate('page.workflowNotifications.option.trigger.nodeEnter'), value: 'node-enter' },
    { label: translate('page.workflowNotifications.option.trigger.taskComplete'), value: 'task-complete' },
    { label: translate('page.workflowNotifications.option.trigger.timeout'), value: 'timeout' },
    { label: translate('page.workflowNotifications.option.trigger.processEnd'), value: 'process-end' }
  ];
}

export function receiverTypeOptions(translate: TranslateFn) {
  return [
    { label: translate('page.workflowNotifications.option.receiverType.approver'), value: 'approver' },
    { label: translate('page.workflowNotifications.option.receiverType.starter'), value: 'starter' },
    { label: translate('page.workflowNotifications.option.receiverType.user'), value: 'user' },
    { label: translate('page.workflowNotifications.option.receiverType.role'), value: 'role' },
    { label: translate('page.workflowNotifications.option.receiverType.department'), value: 'department' },
    { label: translate('page.workflowNotifications.option.receiverType.position'), value: 'position' },
    { label: translate('page.workflowNotifications.option.receiverType.dynamic'), value: 'dynamic' }
  ];
}

export function normalizeStatus(value: string) {
  return value.trim().toLowerCase();
}

export function formatDateTime(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-';
}

export function parseChannelCodes(value: string): string[] {
  const trimmed = value.trim();
  if (!trimmed) {
    return [];
  }

  if (trimmed.startsWith('[')) {
    try {
      const parsed = JSON.parse(trimmed) as unknown;
      if (Array.isArray(parsed)) {
        return parsed.map((item) => String(item).trim()).filter(Boolean);
      }
    } catch {
      // fall through to CSV parsing
    }
  }

  return trimmed
    .split(/[\n,，;]/)
    .map((item) => item.trim())
    .filter(Boolean);
}
