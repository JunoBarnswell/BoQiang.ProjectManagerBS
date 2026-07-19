import { useEffect, useRef } from 'react';

import type { ProjectManagementPreferredView } from '../state/projectManagementInteractionPreferences';
import {
  resolveProjectManagementShortcut,
  shouldIgnoreProjectManagementShortcut,
  type ProjectManagementShortcutId,
} from './projectManagementShortcutDefinitions';

export interface ProjectManagementShortcutHandlers {
  newChildTask?: () => void;
  newTask?: () => void;
  redo?: () => void;
  search?: () => void;
  switchView?: (view: ProjectManagementPreferredView) => void;
  undo?: () => void;
}

export interface ProjectManagementGlobalShortcutOptions {
  canExecute: (shortcut: ProjectManagementShortcutId) => boolean;
  enabled?: boolean;
}

export function useProjectManagementGlobalShortcuts(
  handlers: ProjectManagementShortcutHandlers,
  { canExecute, enabled = true }: ProjectManagementGlobalShortcutOptions,
): void {
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;
  const canExecuteRef = useRef(canExecute);
  canExecuteRef.current = canExecute;

  useEffect(() => {
    if (!enabled) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || shouldIgnoreProjectManagementShortcut(event.target)) return;
      const shortcut = resolveProjectManagementShortcut(event);
      if (!shortcut) return;
      if (!canExecuteRef.current(shortcut.id)) return;

      if (shortcut.id === 'switchView') {
        if (!handlersRef.current.switchView) return;
        event.preventDefault();
        handlersRef.current.switchView(shortcut.view);
        return;
      }

      const handler = handlersRef.current[shortcut.id as Exclude<ProjectManagementShortcutId, 'switchView'>];
      if (!handler) return;
      event.preventDefault();
      handler();
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [enabled]);
}
