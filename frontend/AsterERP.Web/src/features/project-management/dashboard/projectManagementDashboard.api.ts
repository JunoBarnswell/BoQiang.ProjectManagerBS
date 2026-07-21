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
