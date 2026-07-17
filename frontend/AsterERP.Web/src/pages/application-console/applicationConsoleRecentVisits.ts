import type { ApplicationConsoleNavItem } from './applicationConsoleCatalog';

export type ApplicationConsoleRecentVisitKind =
  | 'console'
  | 'data-modeling'
  | 'data-sources'
  | 'designer'
  | 'integration-tasks'
  | 'microflow'
  | 'page-design'
  | 'query-datasets'
  | 'workflow-design'

export interface ApplicationConsoleRecentVisit {
  description?: string | null;
  kind?: ApplicationConsoleRecentVisitKind | string;
  path: string;
  pageId?: string | null;
  section?: string | null;
  targetTitle?: string | null;
  title: string;
  visitedAt: string;
}

export interface ApplicationConsoleRecentVisitInput {
  description?: string | null;
  kind?: ApplicationConsoleRecentVisitKind | string;
  path: string;
  pageId?: string | null;
  section?: string | null;
  targetTitle?: string | null;
  title: string;
}

const maxRecentVisits = 8;

export function buildRecentVisitStorageKey(userId?: string | null, tenantId?: string | null, appCode?: string | null): string | null {
  if (!userId || !tenantId || !appCode) {
    return null;
  }

  return `astererp:application-console:recent-visits:${userId}:${tenantId}:${appCode.toUpperCase()}`;
}

export function loadRecentVisits(storageKey: string | null): ApplicationConsoleRecentVisit[] {
  if (!storageKey) {
    return [];
  }

  try {
    const raw = window.localStorage.getItem(storageKey);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed.filter(isRecentVisit).slice(0, maxRecentVisits) : [];
  } catch {
    return [];
  }
}

export function recordRecentVisit(storageKey: string | null, item: ApplicationConsoleNavItem, path: string): ApplicationConsoleRecentVisit[] {
  return recordDetailedRecentVisit(storageKey, {
    kind: item.key,
    path,
    title: item.title
  });
}

export function recordDetailedRecentVisit(storageKey: string | null, input: ApplicationConsoleRecentVisitInput): ApplicationConsoleRecentVisit[] {
  if (!storageKey) {
    return [];
  }

  const next: ApplicationConsoleRecentVisit = {
    description: input.description ?? null,
    kind: input.kind,
    pageId: input.pageId ?? null,
    path: input.path,
    section: input.section ?? null,
    targetTitle: input.targetTitle ?? null,
    title: input.title,
    visitedAt: new Date().toISOString()
  };
  const visits = [next, ...loadRecentVisits(storageKey).filter((visit) => visit.path !== input.path)].slice(0, maxRecentVisits);
  window.localStorage.setItem(storageKey, JSON.stringify(visits));
  return visits;
}

function isRecentVisit(value: unknown): value is ApplicationConsoleRecentVisit {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const item = value as ApplicationConsoleRecentVisit;
  return typeof item.path === 'string' && typeof item.title === 'string' && typeof item.visitedAt === 'string';
}
