/** Mirrors backend EnsureTaskStatusTransition in ProjectManagementDomainRules. */
export const PROJECT_MANAGEMENT_TASK_STATUSES = [
  'Backlog',
  'Todo',
  'InProgress',
  'Blocked',
  'Done',
  'Cancelled',
] as const;

export type ProjectManagementTaskStatus = (typeof PROJECT_MANAGEMENT_TASK_STATUSES)[number];

const allowedTransitions: Record<string, readonly string[]> = {
  Backlog: ['Todo', 'InProgress', 'Done', 'Cancelled'],
  Todo: ['Backlog', 'InProgress', 'Done', 'Cancelled'],
  InProgress: ['Backlog', 'Blocked', 'Done', 'Cancelled'],
  Blocked: ['Backlog', 'Todo', 'InProgress', 'Done', 'Cancelled'],
  Done: ['Backlog', 'Todo', 'InProgress', 'Cancelled'],
  Cancelled: [],
};

export function getAllowedProjectManagementTaskStatuses(current: string): string[] {
  const next = allowedTransitions[current] ?? [];
  return [current, ...next.filter((status) => status !== current)];
}

export function canTransitionProjectManagementTaskStatus(current: string, next: string): boolean {
  if (current === next) return true;
  return (allowedTransitions[current] ?? []).includes(next);
}

export function progressBarTone(progressPercent: number, overdue = false): 'green' | 'amber' | 'orange' | 'red' {
  if (overdue || progressPercent >= 100) return 'red';
  if (progressPercent >= 80) return 'orange';
  if (progressPercent >= 50) return 'amber';
  return 'green';
}

export function isProjectManagementTaskOverdue(status: string, dueDate?: string): boolean {
  if (!dueDate || status === 'Done' || status === 'Cancelled') return false;
  const due = dueDate.slice(0, 10);
  const today = new Date().toISOString().slice(0, 10);
  return due < today;
}
