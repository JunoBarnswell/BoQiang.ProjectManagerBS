import { translateCurrentLocale } from '../../core/i18n/I18nProvider';

export const milestoneStatuses = ['Planned', 'Active', 'Completed', 'Archived'] as const;

function enumLabel(group: 'priority' | 'status', value: string): string {
  const key = `projectManagement.enum.${group}.${value}`;
  const translated = translateCurrentLocale(key);
  return translated === key ? value : translated;
}

export function milestoneStatusLabel(status: string): string {
  return enumLabel('status', status);
}

export function projectStatusLabel(status: string): string {
  return enumLabel('status', status);
}

export function taskStatusLabel(status: string): string {
  return enumLabel('status', status);
}

export function priorityLabel(priority: string): string {
  return enumLabel('priority', priority);
}

export function taskStatusTone(status: string): string {
  return status === 'InProgress' ? 'in-progress'
    : status === 'Blocked' ? 'blocked'
      : status === 'Done' ? 'done'
        : status === 'Cancelled' || status === 'Canceled' || status === 'Closed' ? 'cancelled'
          : 'todo';
}

export function projectStatusTone(status: string): string {
  return status === 'Active' ? 'in-progress'
    : status === 'Completed' ? 'done'
      : status === 'Paused' ? 'blocked'
        : status === 'Canceled' || status === 'Archived' ? 'cancelled'
          : 'todo';
}
