import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';

export interface GanttScheduleTaskChange {
  taskId: string;
  startDate: string;
  dueDate: string;
  versionNo: number;
}

export interface GanttScheduleBatchUpdateRequest {
  projectId: string;
  items: GanttScheduleTaskChange[];
}

export interface GanttScheduleBatchUpdateResponse {
  projectId: string;
  items: Array<GanttScheduleTaskChange>;
}

export function updateGanttSchedule(request: GanttScheduleBatchUpdateRequest): Promise<ApiEnvelope<GanttScheduleBatchUpdateResponse>> {
  return httpClient.post<GanttScheduleBatchUpdateResponse, GanttScheduleBatchUpdateRequest>(
    `/project-management/projects/${request.projectId}/gantt-schedule`,
    request,
  );
}
