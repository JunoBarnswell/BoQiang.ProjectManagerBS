import { useCallback, useEffect, useMemo, useState } from 'react';

import { useAuthStore } from '../../../core/state';
import { useProjectManagementWorkspaceScope } from './projectManagementWorkspaceScope';

export type ProjectManagementPreferredView = 'tree' | 'list' | 'card' | 'board' | 'gantt' | 'calendar';

export interface ProjectManagementInteractionScope {
  appCode: string;
  tenantId: string;
  userId: string;
}

export interface ProjectManagementRecentPosition {
  hash?: string;
  pathname: string;
  search?: string;
  updatedAt: number;
}

export interface ProjectManagementInteractionPreferences {
  preferredView?: ProjectManagementPreferredView;
  recentPosition?: ProjectManagementRecentPosition;
}

const emptyPreferences: ProjectManagementInteractionPreferences = {};
const views: readonly ProjectManagementPreferredView[] = ['tree', 'list', 'card', 'board', 'gantt', 'calendar'];

export function projectManagementInteractionPreferenceKey(scope: ProjectManagementInteractionScope): string {
  return `project-management:interactions:v1:${encode(scope.tenantId)}:${encode(scope.appCode)}:${encode(scope.userId)}`;
}

export function readProjectManagementInteractionPreferences(key: string): ProjectManagementInteractionPreferences {
  if (typeof window === 'undefined') return emptyPreferences;

  try {
    return normalizePreferences(JSON.parse(window.localStorage.getItem(key) ?? ''));
  } catch {
    return emptyPreferences;
  }
}

export function writeProjectManagementInteractionPreferences(
  key: string,
  preferences: ProjectManagementInteractionPreferences,
): void {
  if (typeof window === 'undefined') return;

  try {
    window.localStorage.setItem(key, JSON.stringify(normalizePreferences(preferences)));
  } catch {
    // 禁用浏览器存储时，交互功能仍应可用，只是不跨刷新保存偏好。
  }
}

export function restoreProjectManagementRecentPosition(
  preferences: ProjectManagementInteractionPreferences,
  isAllowed: (position: ProjectManagementRecentPosition) => boolean,
): ProjectManagementRecentPosition | undefined {
  const position = preferences.recentPosition;
  return position && isAllowed(position) ? position : undefined;
}

export function useProjectManagementInteractionPreferences() {
  const workspaceScope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore((state) => state.user?.userId?.trim() ?? '');
  const scope = useMemo<ProjectManagementInteractionScope | undefined>(() => {
    if (!workspaceScope.isAvailable || !workspaceScope.tenantId || !workspaceScope.appCode || !userId) return undefined;
    return { appCode: workspaceScope.appCode, tenantId: workspaceScope.tenantId, userId };
  }, [userId, workspaceScope.appCode, workspaceScope.isAvailable, workspaceScope.tenantId]);
  const key = useMemo(() => scope && projectManagementInteractionPreferenceKey(scope), [scope]);
  const [preferences, setPreferences] = useState<ProjectManagementInteractionPreferences>(emptyPreferences);

  useEffect(() => {
    setPreferences(key ? readProjectManagementInteractionPreferences(key) : emptyPreferences);
  }, [key]);

  const update = useCallback((updater: (current: ProjectManagementInteractionPreferences) => ProjectManagementInteractionPreferences) => {
    setPreferences((current) => {
      const next = normalizePreferences(updater(current));
      if (key) writeProjectManagementInteractionPreferences(key, next);
      return next;
    });
  }, [key]);

  const rememberPosition = useCallback((position: Omit<ProjectManagementRecentPosition, 'updatedAt'>) => {
    update((current) => ({ ...current, recentPosition: { ...position, updatedAt: Date.now() } }));
  }, [update]);

  const setPreferredView = useCallback((preferredView: ProjectManagementPreferredView) => {
    update((current) => ({ ...current, preferredView }));
  }, [update]);

  return {
    isAvailable: Boolean(scope),
    preferences,
    rememberPosition,
    restoreRecentPosition: (isAllowed: (position: ProjectManagementRecentPosition) => boolean) => restoreProjectManagementRecentPosition(preferences, isAllowed),
    scope,
    setPreferredView,
  };
}

function normalizePreferences(value: unknown): ProjectManagementInteractionPreferences {
  if (!isRecord(value)) return emptyPreferences;
  const preferredView = typeof value.preferredView === 'string' && views.includes(value.preferredView as ProjectManagementPreferredView)
    ? value.preferredView as ProjectManagementPreferredView
    : undefined;
  const recentPosition = normalizePosition(value.recentPosition);
  return { ...(preferredView ? { preferredView } : {}), ...(recentPosition ? { recentPosition } : {}) };
}

function normalizePosition(value: unknown): ProjectManagementRecentPosition | undefined {
  if (!isRecord(value) || typeof value.pathname !== 'string' || !value.pathname.startsWith('/')) return undefined;
  const pathname = value.pathname.trim();
  if (!pathname) return undefined;
  const search = typeof value.search === 'string' && value.search.startsWith('?') ? value.search : undefined;
  const hash = typeof value.hash === 'string' && value.hash.startsWith('#') ? value.hash : undefined;
  const updatedAt = typeof value.updatedAt === 'number' && Number.isFinite(value.updatedAt) ? value.updatedAt : 0;
  return { ...(hash ? { hash } : {}), pathname, ...(search ? { search } : {}), updatedAt };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function encode(value: string): string {
  return encodeURIComponent(value);
}
