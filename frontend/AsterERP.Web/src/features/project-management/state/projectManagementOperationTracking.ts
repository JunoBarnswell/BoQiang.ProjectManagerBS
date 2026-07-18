export function getProjectManagementOperationTrackingKey(tenantId: string, appCode: string, userId: string): string | null {
  if (!tenantId.trim() || !appCode.trim() || !userId.trim()) return null;
  return `project-management:operation:${tenantId.trim()}:${appCode.trim().toUpperCase()}:${userId.trim()}`;
}

export function readProjectManagementOperationTracking(key: string | null): string | null {
  if (!key) return null;
  return window.localStorage.getItem(key)?.trim() || null;
}

export function writeProjectManagementOperationTracking(key: string | null, operationId: string): void {
  if (key && operationId.trim()) window.localStorage.setItem(key, operationId.trim());
}

export function clearProjectManagementOperationTracking(key: string | null): void {
  if (key) window.localStorage.removeItem(key);
}
