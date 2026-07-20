import type { ProjectManagementProject } from '../../../api/project-management/projectManagement.types';
import { isHttpError } from '../../../core/http/httpError';

export interface ProjectManagementProjectConflict {
  serverValues: ProjectManagementProject;
  localValues: Record<string, unknown>;
  fieldConflicts: Array<{ field: string; displayName: string; serverValue?: unknown; localValue?: unknown }>;
}

export function readProjectManagementProjectConflict(error: unknown): ProjectManagementProjectConflict | null {
  if (!isHttpError(error) || error.status !== 409 || !isRecord(error.data)) return null;
  const value = isRecord(error.data.data) ? error.data.data : error.data;
  if (!isRecord(value.serverValues) || !isRecord(value.localValues) || !Array.isArray(value.fieldConflicts)) return null;
  return {
    serverValues: value.serverValues as unknown as ProjectManagementProject,
    localValues: value.localValues,
    fieldConflicts: value.fieldConflicts.filter(isConflictField),
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function isConflictField(value: unknown): value is ProjectManagementProjectConflict['fieldConflicts'][number] {
  return isRecord(value) && typeof value.field === 'string' && typeof value.displayName === 'string';
}
