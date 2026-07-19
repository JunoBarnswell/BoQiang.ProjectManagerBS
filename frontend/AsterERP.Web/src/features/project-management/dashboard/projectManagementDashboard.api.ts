import type { ProjectManagementOverviewItem, ProjectManagementOverviewQuery } from '../../../api/project-management/projectManagement.types';
import { buildQueryString } from '../../../api/queryString';
import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';

export interface ProjectManagementDashboardRiskSummary {
  overdueTaskCount: number;
  blockedTaskCount: number;
  dueSoonIncompleteTaskCount: number;
  inProgressTaskCount: number;
  wipLimit?: number;
  isWipExceeded: boolean;
  wipExceededBy: number;
  hasScheduleRisk: boolean;
}

export type ProjectManagementDashboardOverview = ProjectManagementOverviewItem & {
  riskSummary: ProjectManagementDashboardRiskSummary;
};

export interface ProjectManagementDashboardWorkload {
  userId: string;
  todoTaskCount: number;
  inProgressTaskCount: number;
  completedTaskCount: number;
  overdueTaskCount: number;
  estimatedMinutes: number;
  loggedMinutes: number;
}

export function getProjectManagementDashboardOverview(
  query: ProjectManagementOverviewQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<{ total: number; items: ProjectManagementDashboardOverview[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementDashboardOverview[] }>(
    `/project-management/overview${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementDashboardWorkload(
  projectId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementDashboardWorkload[]>> {
  return httpClient.get<ProjectManagementDashboardWorkload[]>(
    `/project-management/workloads${buildQueryString({ projectId })}`,
    undefined,
    signal,
  );
}
