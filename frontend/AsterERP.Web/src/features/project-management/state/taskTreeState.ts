import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

import { projectCenterPreferenceKey } from './projectCenterPreferences';

export type TaskTreeRow = ProjectManagementTaskListItem & { hasChildren?: boolean };

export interface TaskTreeExpansionState {
  expandedTaskIds: string[];
}

const emptyExpansionState: TaskTreeExpansionState = { expandedTaskIds: [] };

export function taskTreeExpansionPreferenceKey(userId: string, tenantId: string, appCode: string, projectId: string): string {
  return `${projectCenterPreferenceKey(userId, tenantId, appCode)}:task-tree:${projectId}`;
}

export function readTaskTreeExpansionState(key: string): TaskTreeExpansionState {
  if (typeof window === 'undefined') return emptyExpansionState;
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) return emptyExpansionState;
    const parsed = JSON.parse(raw) as Partial<TaskTreeExpansionState>;
    return { expandedTaskIds: normalizeIds(parsed.expandedTaskIds) };
  } catch {
    return emptyExpansionState;
  }
}

export function writeTaskTreeExpansionState(key: string, state: TaskTreeExpansionState): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(key, JSON.stringify({ expandedTaskIds: normalizeIds(state.expandedTaskIds) }));
  } catch {
    // 存储不可用时不影响任务查询和编辑。
  }
}

export function toggleTaskTreeExpansion(state: TaskTreeExpansionState, taskId: string): TaskTreeExpansionState {
  const expandedTaskIds = state.expandedTaskIds.includes(taskId)
    ? state.expandedTaskIds.filter((id) => id !== taskId)
    : [...state.expandedTaskIds, taskId];
  return { expandedTaskIds };
}

export function buildVisibleTaskTreeRows(rows: TaskTreeRow[], expandedTaskIds: ReadonlySet<string>): TaskTreeRow[] {
  const byParent = new Map<string | undefined, TaskTreeRow[]>();
  const byId = new Map(rows.map((row) => [row.id, row]));
  rows.forEach((row) => {
    const parentKey = row.parentTaskId && byId.has(row.parentTaskId) ? row.parentTaskId : undefined;
    byParent.set(parentKey, [...(byParent.get(parentKey) ?? []), row]);
  });

  const visible: TaskTreeRow[] = [];
  const append = (row: TaskTreeRow) => {
    visible.push(row);
    if (!expandedTaskIds.has(row.id)) return;
    (byParent.get(row.id) ?? []).forEach(append);
  };
  (byParent.get(undefined) ?? []).forEach(append);
  return visible;
}

export function taskTreeRowHasChildren(row: TaskTreeRow, rows: TaskTreeRow[]): boolean {
  return row.hasChildren === true || rows.some((candidate) => candidate.parentTaskId === row.id);
}

export function taskTreeAriaLevel(row: Pick<TaskTreeRow, 'depth'>): number {
  return Math.max(1, row.depth + 1);
}

function normalizeIds(value: unknown): string[] {
  if (!Array.isArray(value)) return [];
  return [...new Set(value.filter((id): id is string => typeof id === 'string' && id.trim().length > 0).map((id) => id.trim()))];
}
