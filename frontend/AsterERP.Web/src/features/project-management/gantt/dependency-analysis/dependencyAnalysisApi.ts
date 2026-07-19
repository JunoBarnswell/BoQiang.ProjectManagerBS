import type { ApiEnvelope } from '../../../../core/http/apiEnvelope';
import { httpClient } from '../../../../core/http/httpClient';

import type { DependencyAnalysisResponse, DependencyImpactPreviewResponse } from './dependencyAnalysisModel';

export interface DependencyImpactPreviewRequest {
  taskId: string;
  proposedStartDate: string;
  proposedDueDate: string;
}

export function getTaskDependencyAnalysis(projectId: string, signal?: AbortSignal): Promise<ApiEnvelope<DependencyAnalysisResponse>> {
  return httpClient.get<DependencyAnalysisResponse>(`/project-management/projects/${projectId}/task-dependency-analysis`, undefined, signal);
}

export function previewTaskDependencyImpact(
  projectId: string,
  request: DependencyImpactPreviewRequest,
  signal?: AbortSignal,
): Promise<ApiEnvelope<DependencyImpactPreviewResponse>> {
  return httpClient.post<DependencyImpactPreviewResponse, DependencyImpactPreviewRequest>(
    `/project-management/projects/${projectId}/task-dependency-analysis/impact-preview`,
    request,
    undefined,
    signal,
  );
}
