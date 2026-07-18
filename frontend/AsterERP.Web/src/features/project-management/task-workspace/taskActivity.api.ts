import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import { buildQueryString } from '../../../api/queryString';
import type { ProjectManagementActivityPage, ProjectManagementActivityQuery } from '../../../api/project-management/projectManagement.types';

export function getProjectManagementTaskActivities(
  taskId: string,
  query: ProjectManagementActivityQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementActivityPage>> {
  return httpClient.get<ProjectManagementActivityPage>(
    `/project-management/tasks/${encodeURIComponent(taskId)}/activities${buildQueryString(query)}`,
    undefined,
    signal,
  );
}
