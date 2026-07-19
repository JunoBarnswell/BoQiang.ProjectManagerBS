import type { ProjectManagementPreferredView } from '../state/projectManagementInteractionPreferences';

export type ProjectManagementShortcutId =
  | 'newChildTask'
  | 'newTask'
  | 'redo'
  | 'search'
  | 'switchView'
  | 'undo';

export interface ProjectManagementShortcutInvocation {
  id: Exclude<ProjectManagementShortcutId, 'switchView'>;
}

export interface ProjectManagementViewShortcutInvocation {
  id: 'switchView';
  view: ProjectManagementPreferredView;
}

export type ProjectManagementShortcutInvocationResult = ProjectManagementShortcutInvocation | ProjectManagementViewShortcutInvocation;

export interface ProjectManagementShortcutHelpItem {
  id: ProjectManagementShortcutId;
  keys: string;
  label: string;
}

export const projectManagementShortcutHelpItems: readonly ProjectManagementShortcutHelpItem[] = [
  { id: 'newTask', keys: 'Ctrl+N', label: '新建任务' },
  { id: 'newChildTask', keys: 'Ctrl+Shift+N', label: '新建子任务' },
  { id: 'search', keys: 'Ctrl+F', label: '打开全局搜索' },
  { id: 'undo', keys: 'Ctrl+Z', label: '撤销' },
  { id: 'redo', keys: 'Ctrl+Y', label: '重做' },
  { id: 'switchView', keys: 'Ctrl+1…5', label: '切换树形、列表、卡片、看板、甘特视图' },
  { id: 'switchView', keys: 'Esc', label: '仅关闭最上层弹层' },
];

export function resolveProjectManagementShortcut(event: Pick<KeyboardEvent, 'ctrlKey' | 'key' | 'metaKey' | 'shiftKey'>): ProjectManagementShortcutInvocationResult | undefined {
  const modifier = event.ctrlKey || event.metaKey;
  const key = event.key.toLowerCase();
  if (!modifier) return undefined;
  if (key === 'n') return event.shiftKey ? { id: 'newChildTask' } : { id: 'newTask' };
  if (key === 'f' && !event.shiftKey) return { id: 'search' };
  if (key === 'z' && !event.shiftKey) return { id: 'undo' };
  if (key === 'y' && !event.shiftKey) return { id: 'redo' };

  const view = ({ 1: 'tree', 2: 'list', 3: 'card', 4: 'board', 5: 'gantt' } as const)[Number(key) as 1 | 2 | 3 | 4 | 5];
  return view && !event.shiftKey ? { id: 'switchView', view } : undefined;
}

export function shouldIgnoreProjectManagementShortcut(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable || target.getAttribute('contenteditable') === 'true') return true;
  if (target.closest('[contenteditable="true"], [data-project-management-shortcut-ignore="true"]')) return true;
  return target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement;
}
