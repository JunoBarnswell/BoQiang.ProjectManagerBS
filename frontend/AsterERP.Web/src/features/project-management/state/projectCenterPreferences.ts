export type ProjectCenterCollection = 'all' | 'favorites' | 'recent';

export interface ProjectCenterPreferences {
  favoriteProjectIds: string[];
  recentProjectIds: string[];
}

const emptyPreferences: ProjectCenterPreferences = {
  favoriteProjectIds: [],
  recentProjectIds: [],
};

export function projectCenterPreferenceKey(userId: string, tenantId: string, appCode: string): string {
  return `project-management:project-center:${tenantId}:${appCode}:${userId}`;
}

export function readProjectCenterPreferences(key: string): ProjectCenterPreferences {
  if (typeof window === 'undefined') return emptyPreferences;
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) return emptyPreferences;
    const parsed = JSON.parse(raw) as Partial<ProjectCenterPreferences>;
    return {
      favoriteProjectIds: normalizeIds(parsed.favoriteProjectIds),
      recentProjectIds: normalizeIds(parsed.recentProjectIds),
    };
  } catch {
    return emptyPreferences;
  }
}

export function writeProjectCenterPreferences(key: string, preferences: ProjectCenterPreferences): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(key, JSON.stringify(preferences));
  } catch {
    // 浏览器禁用存储时，项目查询和页面操作仍保持可用。
  }
}

export function toggleProjectFavorite(preferences: ProjectCenterPreferences, projectId: string): ProjectCenterPreferences {
  const favoriteProjectIds = preferences.favoriteProjectIds.includes(projectId)
    ? preferences.favoriteProjectIds.filter((id) => id !== projectId)
    : [projectId, ...preferences.favoriteProjectIds];
  return { ...preferences, favoriteProjectIds };
}

export function rememberRecentProject(preferences: ProjectCenterPreferences, projectId: string): ProjectCenterPreferences {
  return {
    ...preferences,
    recentProjectIds: [projectId, ...preferences.recentProjectIds.filter((id) => id !== projectId)].slice(0, 20),
  };
}

function normalizeIds(value: unknown): string[] {
  if (!Array.isArray(value)) return [];
  return [...new Set(value.filter((id): id is string => typeof id === 'string' && id.trim().length > 0).map((id) => id.trim()))];
}
