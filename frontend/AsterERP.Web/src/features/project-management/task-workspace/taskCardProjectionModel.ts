import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

export type TaskCardGroupBy = 'status' | 'priority' | 'assignee' | 'milestone' | 'parent';
export type TaskCardRisk = 'overdue' | 'urgent' | 'blocked' | 'done';

export interface TaskCardGroup {
  key: string;
  label: string;
  rows: ProjectManagementTaskListItem[];
}

export function groupTaskCards(rows: ProjectManagementTaskListItem[], groupBy?: TaskCardGroupBy): TaskCardGroup[] {
  if (!rows.length) return [{ key: 'all', label: '全部任务', rows: [] }];
  if (!groupBy) return [{ key: 'all', label: '全部任务', rows }];
  const groups = new Map<string, TaskCardGroup>();
  rows.forEach((row) => {
    const key = taskGroupValue(row, groupBy);
    const group = groups.get(key) ?? { key, label: taskGroupLabel(key, groupBy), rows: [] };
    group.rows.push(row);
    groups.set(key, group);
  });
  return [...groups.values()];
}

export function getTaskCardRisks(task: Pick<ProjectManagementTaskListItem, 'blockedByCount' | 'canStart' | 'dueDate' | 'priority' | 'status'>, now = new Date()): TaskCardRisk[] {
  const risks: TaskCardRisk[] = [];
  if (task.status === 'Done') risks.push('done');
  if (task.priority === 'Urgent') risks.push('urgent');
  if (task.status === 'Blocked' || task.blockedByCount > 0 || !task.canStart) risks.push('blocked');
  if (isOverdue(task.dueDate, task.status, now)) risks.push('overdue');
  return risks;
}

export function isOverdue(dueDate: string | undefined, status: string, now = new Date()): boolean {
  if (!dueDate || status === 'Done' || status === 'Cancelled') return false;
  const due = new Date(`${dueDate.slice(0, 10)}T00:00:00`);
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  return !Number.isNaN(due.getTime()) && due < today;
}

function taskGroupValue(task: ProjectManagementTaskListItem, groupBy: TaskCardGroupBy): string {
  if (groupBy === 'status') return task.status || 'Unknown';
  if (groupBy === 'priority') return task.priority || 'Unknown';
  if (groupBy === 'assignee') return task.assigneeUserId || 'unassigned';
  if (groupBy === 'milestone') return task.milestoneId || 'unassigned';
  return task.parentTaskId || 'root';
}

function taskGroupLabel(value: string, groupBy: TaskCardGroupBy): string {
  if (value === 'unassigned') return groupBy === 'assignee' ? '未分配负责人' : '未设置';
  if (value === 'root') return '顶级任务';
  if (groupBy === 'status') return ({ Todo: '待开始', InProgress: '进行中', Blocked: '受阻塞', Done: '已完成', Cancelled: '已取消' } as Record<string, string>)[value] ?? value;
  if (groupBy === 'priority') return ({ Low: '低', Medium: '中', High: '高', Urgent: '紧急' } as Record<string, string>)[value] ?? value;
  return value;
}
