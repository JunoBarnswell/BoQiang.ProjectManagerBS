import type { TaskCardGroup } from './taskCardProjectionModel';

export interface TaskGroupPreference {
  collapsedKeys: string[];
  orderedKeys: string[];
}

export const emptyTaskGroupPreference: TaskGroupPreference = { collapsedKeys: [], orderedKeys: [] };

export function orderTaskGroups(groups: readonly TaskCardGroup[], preference: TaskGroupPreference): TaskCardGroup[] {
  const position = new Map(preference.orderedKeys.map((key, index) => [key, index]));
  return [...groups].sort((left, right) => {
    const leftPosition = position.get(left.key);
    const rightPosition = position.get(right.key);
    if (leftPosition !== undefined && rightPosition !== undefined) return leftPosition - rightPosition;
    if (leftPosition !== undefined) return -1;
    if (rightPosition !== undefined) return 1;
    return 0;
  });
}

export function toggleTaskGroup(preference: TaskGroupPreference, groupKey: string): TaskGroupPreference {
  const collapsed = new Set(preference.collapsedKeys);
  if (collapsed.has(groupKey)) collapsed.delete(groupKey);
  else collapsed.add(groupKey);
  return { ...preference, collapsedKeys: [...collapsed] };
}

export function moveTaskGroup(preference: TaskGroupPreference, groupKey: string, beforeKey?: string): TaskGroupPreference {
  const ordered = preference.orderedKeys.filter((key) => key !== groupKey);
  const index = beforeKey ? ordered.indexOf(beforeKey) : -1;
  ordered.splice(index < 0 ? ordered.length : index, 0, groupKey);
  return { ...preference, orderedKeys: ordered };
}

export function taskGroupPreferenceKey(userId: string, tenantId: string, appCode: string, projectId: string, viewKey: string, groupBy: string): string {
  return ['project-management', 'task-groups', userId, tenantId, appCode, projectId, viewKey, groupBy].map(encodeURIComponent).join(':');
}

export function readTaskGroupPreference(key: string): TaskGroupPreference {
  if (!key || typeof window === 'undefined') return emptyTaskGroupPreference;
  try {
    const parsed = JSON.parse(window.localStorage.getItem(key) ?? 'null') as Partial<TaskGroupPreference> | null;
    return {
      collapsedKeys: Array.isArray(parsed?.collapsedKeys) ? parsed.collapsedKeys.filter((value): value is string => typeof value === 'string') : [],
      orderedKeys: Array.isArray(parsed?.orderedKeys) ? parsed.orderedKeys.filter((value): value is string => typeof value === 'string') : [],
    };
  } catch {
    return emptyTaskGroupPreference;
  }
}

export function writeTaskGroupPreference(key: string, preference: TaskGroupPreference): void {
  if (!key || typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(key, JSON.stringify(preference));
  } catch {
    // 浏览器存储被禁用时不影响看板渲染。
  }
}
