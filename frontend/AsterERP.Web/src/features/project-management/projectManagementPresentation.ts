export const milestoneStatuses = ['Planned', 'Active', 'Completed', 'Archived'] as const;

const labels: Record<string, string> = {
  Active: '进行中',
  Archived: '已归档',
  AtRisk: '有风险',
  Blocked: '已阻塞',
  Canceled: '已取消',
  Cancelled: '已取消',
  Completed: '已完成',
  Done: '已完成',
  High: '高',
  InProgress: '进行中',
  Low: '低',
  Medium: '中',
  OffTrack: '已偏离',
  OnTrack: '正常',
  Paused: '已暂停',
  Planned: '未开始',
  Planning: '规划中',
  Todo: '待办',
  Urgent: '紧急',
};

export function milestoneStatusLabel(status: string): string {
  return labels[status] ?? status;
}

export function projectStatusLabel(status: string): string {
  return labels[status] ?? status;
}

export function taskStatusLabel(status: string): string {
  return labels[status] ?? status;
}

export function priorityLabel(priority: string): string {
  return labels[priority] ?? priority;
}

export function taskStatusTone(status: string): string {
  return status === 'InProgress' ? 'in-progress'
    : status === 'Blocked' ? 'blocked'
      : status === 'Done' ? 'done'
        : status === 'Cancelled' || status === 'Canceled' ? 'cancelled'
          : 'todo';
}

export function projectStatusTone(status: string): string {
  return status === 'Active' ? 'in-progress'
    : status === 'Completed' ? 'done'
      : status === 'Paused' ? 'blocked'
        : status === 'Canceled' || status === 'Archived' ? 'cancelled'
          : 'todo';
}
