import type { ApiEnvelope } from "../../core/http/apiEnvelope";
import { httpClient } from "../../core/http/httpClient";
import { buildQueryString } from "../queryString";

import type {
  ProjectManagementMemberCandidate,
  ProjectManagementMemberCandidateQuery,
  ProjectManagementProject,
  ProjectManagementProjectQuery,
  ProjectManagementProjectUpsertRequest,
  ProjectManagementRecycleQuery,
  ProjectManagementRecycleResponse,
  ProjectManagementOverviewItem,
  ProjectManagementOverviewQuery,
  ProjectManagementMyWorkItem,
  ProjectManagementMyWorkQuery,
  ProjectManagementTask,
  ProjectManagementTaskBatchUpdateRequest,
  ProjectManagementTaskQuery,
  ProjectManagementAuditPage,
  ProjectManagementAuditQuery,
  ProjectManagementOperationPage,
  ProjectManagementOperationQuery,
  ProjectManagementBackup,
  ProjectManagementBackupRestorePreview,
  ProjectManagementRecyclePurgePreview,
  ProjectManagementTaskUpsertRequest,
  ProjectManagementTaskComment,
  ProjectManagementTaskCommentUpsertRequest,
  ProjectManagementTaskReminder,
  ProjectManagementTaskReminderCreateRequest,
  ProjectManagementTaskReminderUpdateRequest,
  ProjectManagementNotificationQuery,
  ProjectManagementNotificationPage,
  ProjectManagementNotificationOpenResult,
  ProjectManagementTaskAttachment,
  ProjectManagementDataSpaceSummary,
  ProjectManagementSavedView,
  ProjectManagementSavedViewUpsertRequest,
  ProjectManagementWorkspaceOverview,
  ProjectManagementWorkspaceQuery,
  ProjectManagementSyncImportResponse,
  ProjectManagementSyncPreviewResponse,
  ProjectManagementMember,
  ProjectManagementMemberUpsertRequest,
  ProjectManagementImConversation,
  ProjectManagementImConversationEnsureRequest,
  ProjectManagementImConversationTarget,
  ProjectManagementMilestone,
  ProjectManagementMilestoneUpsertRequest,
  ProjectManagementLabel,
  ProjectManagementActivity,
  ProjectManagementReportQuery,
  ProjectManagementSearchQuery,
  ProjectManagementSearchResponse,
  ProjectManagementSyncJournalItem,
  ProjectManagementSyncWatermark,
} from "./projectManagement.types";

export function getProjectManagementProjects(
  query: ProjectManagementProjectQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<{ total: number; items: ProjectManagementProject[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementProject[] }>(
    `/project-management/projects${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementOverview(
  query: ProjectManagementOverviewQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<{ total: number; items: ProjectManagementOverviewItem[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementOverviewItem[] }>(
    `/project-management/overview${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementMyWork(
  query: ProjectManagementMyWorkQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<{ total: number; items: ProjectManagementMyWorkItem[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementMyWorkItem[] }>(
    `/project-management/my-work${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function createProjectManagementProject(
  request: ProjectManagementProjectUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementProject>> {
  return httpClient.post<ProjectManagementProject, ProjectManagementProjectUpsertRequest>(
    "/project-management/projects",
    request,
  );
}

export function updateProjectManagementProject(
  id: string,
  request: ProjectManagementProjectUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementProject>> {
  return httpClient.put<ProjectManagementProject, ProjectManagementProjectUpsertRequest>(
    `/project-management/projects/${id}`,
    request,
  );
}

export function deleteProjectManagementProject(id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/projects/${id}${buildQueryString({ versionNo })}`);
}

export function getProjectManagementRecycle(
  query: ProjectManagementRecycleQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementRecycleResponse>> {
  return httpClient.get<ProjectManagementRecycleResponse>(
    `/project-management/recycle${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementWorkspaceOverview(
  query: ProjectManagementWorkspaceQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementWorkspaceOverview>> {
  return httpClient.get<ProjectManagementWorkspaceOverview>(
    `/project-management/workspace/overview${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementMemberCandidates(
  query: ProjectManagementMemberCandidateQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<{ total: number; items: ProjectManagementMemberCandidate[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementMemberCandidate[] }>(
    `/project-management/member-candidates${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementMembers(
  projectId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementMember[]>> {
  return httpClient.get<ProjectManagementMember[]>(
    `/project-management/projects/${projectId}/members`,
    undefined,
    signal,
  );
}

export function getProjectManagementImConversation(
  projectId: string,
  taskId?: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementImConversation | null>> {
  return httpClient.get<ProjectManagementImConversation | null>(
    `/project-management/projects/${projectId}/im-conversation${buildQueryString({ taskId })}`,
    undefined,
    signal,
  );
}

export function ensureProjectManagementImConversation(
  projectId: string,
  request: ProjectManagementImConversationEnsureRequest,
): Promise<ApiEnvelope<ProjectManagementImConversation>> {
  return httpClient.post<ProjectManagementImConversation, ProjectManagementImConversationEnsureRequest>(
    `/project-management/projects/${projectId}/im-conversation`,
    request,
  );
}

export function getProjectManagementImConversationTarget(
  conversationId: string,
): Promise<ApiEnvelope<ProjectManagementImConversationTarget>> {
  return httpClient.get<ProjectManagementImConversationTarget>(
    `/project-management/im-conversations/${conversationId}/target`,
  );
}

export function createProjectManagementMember(
  projectId: string,
  request: ProjectManagementMemberUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementMember>> {
  return httpClient.post<ProjectManagementMember, ProjectManagementMemberUpsertRequest>(
    `/project-management/projects/${projectId}/members`,
    request,
  );
}

export function updateProjectManagementMember(
  projectId: string,
  id: string,
  request: ProjectManagementMemberUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementMember>> {
  return httpClient.put<ProjectManagementMember, ProjectManagementMemberUpsertRequest>(
    `/project-management/projects/${projectId}/members/${id}`,
    request,
  );
}

export function deleteProjectManagementMember(
  projectId: string,
  id: string,
  versionNo: number,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(
    `/project-management/projects/${projectId}/members/${id}${buildQueryString({ versionNo })}`,
  );
}

export function getProjectManagementMilestones(
  projectId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementMilestone[]>> {
  return httpClient.get<ProjectManagementMilestone[]>(
    `/project-management/projects/${projectId}/milestones`,
    undefined,
    signal,
  );
}

export function getProjectManagementLabels(
  projectId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementLabel[]>> {
  return httpClient.get<ProjectManagementLabel[]>(
    `/project-management/projects/${projectId}/labels`,
    undefined,
    signal,
  );
}

export function createProjectManagementMilestone(
  projectId: string,
  request: ProjectManagementMilestoneUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementMilestone>> {
  return httpClient.post<ProjectManagementMilestone, ProjectManagementMilestoneUpsertRequest>(
    `/project-management/projects/${projectId}/milestones`,
    request,
  );
}

export function updateProjectManagementMilestone(
  projectId: string,
  id: string,
  request: ProjectManagementMilestoneUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementMilestone>> {
  return httpClient.put<ProjectManagementMilestone, ProjectManagementMilestoneUpsertRequest>(
    `/project-management/projects/${projectId}/milestones/${id}`,
    request,
  );
}

export function deleteProjectManagementMilestone(
  projectId: string,
  id: string,
  versionNo: number,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(
    `/project-management/projects/${projectId}/milestones/${id}${buildQueryString({ versionNo })}`,
  );
}

export function restoreProjectManagementProject(
  id: string,
  versionNo: number,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.post<{ id: string }, { versionNo: number }>(
    `/project-management/recycle/projects/${id}/restore`,
    { versionNo },
  );
}

export function restoreProjectManagementTask(
  id: string,
  versionNo: number,
  restoreDescendants = false,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.post<{ id: string }, { versionNo: number; restoreDescendants: boolean }>(
    `/project-management/recycle/tasks/${id}/restore`,
    { versionNo, restoreDescendants },
  );
}

export function previewProjectManagementProjectPurge(id: string, versionNo: number): Promise<ApiEnvelope<ProjectManagementRecyclePurgePreview>> {
  return httpClient.get<ProjectManagementRecyclePurgePreview>(`/project-management/recycle/projects/${id}/purge-preview${buildQueryString({ versionNo })}`);
}

export function purgeProjectManagementProject(id: string, request: { versionNo: number; currentPassword: string; confirmRisk: boolean }): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.post<{ id: string }, typeof request>(`/project-management/recycle/projects/${id}/purge`, request);
}

export function getProjectManagementActivities(
  projectId: string,
  limit = 20,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementActivity[]>> {
  return httpClient.get<ProjectManagementActivity[]>(
    `/project-management/projects/${projectId}/activities${buildQueryString({ limit })}`,
    undefined,
    signal,
  );
}

export function getProjectManagementTasks(
  query: ProjectManagementTaskQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<{ total: number; items: ProjectManagementTask[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementTask[] }>(
    `/project-management/tasks${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function createProjectManagementTask(
  projectId: string,
  request: ProjectManagementTaskUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementTask>> {
  return httpClient.post<ProjectManagementTask, ProjectManagementTaskUpsertRequest>(
    `/project-management/tasks/${projectId}`,
    request,
  );
}

export function updateProjectManagementTask(
  id: string,
  request: ProjectManagementTaskUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementTask>> {
  return httpClient.put<ProjectManagementTask, ProjectManagementTaskUpsertRequest>(
    `/project-management/tasks/${id}`,
    request,
  );
}

export function moveProjectManagementTask(
  id: string,
  request: {
    parentTaskId?: string;
    sortOrder: number;
    versionNo: number;
    beforeTaskId?: string;
    milestoneId?: string;
    updateMilestone?: boolean;
  },
): Promise<ApiEnvelope<ProjectManagementTask>> {
  return httpClient.post<ProjectManagementTask, typeof request>(`/project-management/tasks/${id}/move`, request);
}

export function updateProjectManagementTasksBatch(
  request: ProjectManagementTaskBatchUpdateRequest,
): Promise<ApiEnvelope<ProjectManagementTask[]>> {
  return httpClient.post<ProjectManagementTask[], ProjectManagementTaskBatchUpdateRequest>(
    '/project-management/tasks/batch/update',
    request,
  );
}

export function deleteProjectManagementTask(id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/tasks/${id}${buildQueryString({ versionNo })}`);
}

export function getProjectManagementTaskComments(
  taskId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskComment[]>> {
  return httpClient.get<ProjectManagementTaskComment[]>(
    `/project-management/tasks/${taskId}/comments`,
    undefined,
    signal,
  );
}

export function createProjectManagementTaskComment(
  taskId: string,
  request: ProjectManagementTaskCommentUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementTaskComment>> {
  return httpClient.post<ProjectManagementTaskComment, ProjectManagementTaskCommentUpsertRequest>(
    `/project-management/tasks/${taskId}/comments`,
    request,
  );
}

export function getProjectManagementTaskReminders(
  taskId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskReminder[]>> {
  return httpClient.get<ProjectManagementTaskReminder[]>(
    `/project-management/tasks/${taskId}/reminders`,
    undefined,
    signal,
  );
}

export function getProjectManagementNotifications(query: ProjectManagementNotificationQuery, signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementNotificationPage>> {
  return httpClient.get<ProjectManagementNotificationPage>(`/project-management/notifications${buildQueryString(query)}`, undefined, signal);
}

export function markProjectManagementNotificationRead(id: string): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.post<{ id: string }, undefined>(`/project-management/notifications/${id}/read`, undefined);
}

export function markAllProjectManagementNotificationsRead(): Promise<ApiEnvelope<Record<string, never>>> {
  return httpClient.post<Record<string, never>, undefined>('/project-management/notifications/read-all', undefined);
}

export function openProjectManagementNotification(id: string): Promise<ApiEnvelope<ProjectManagementNotificationOpenResult>> {
  return httpClient.post<ProjectManagementNotificationOpenResult, undefined>(`/project-management/notifications/${id}/open`, undefined);
}

export function createProjectManagementTaskReminders(
  taskId: string,
  request: ProjectManagementTaskReminderCreateRequest,
): Promise<ApiEnvelope<ProjectManagementTaskReminder[]>> {
  return httpClient.post<ProjectManagementTaskReminder[], ProjectManagementTaskReminderCreateRequest>(
    `/project-management/tasks/${taskId}/reminders`,
    request,
  );
}

export function updateProjectManagementTaskReminder(
  taskId: string,
  id: string,
  request: ProjectManagementTaskReminderUpdateRequest,
): Promise<ApiEnvelope<ProjectManagementTaskReminder>> {
  return httpClient.put<ProjectManagementTaskReminder, ProjectManagementTaskReminderUpdateRequest>(
    `/project-management/tasks/${taskId}/reminders/${id}`,
    request,
  );
}

export function cancelProjectManagementTaskReminder(
  taskId: string,
  id: string,
  versionNo: number,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.post<{ id: string }, undefined>(
    `/project-management/tasks/${taskId}/reminders/${id}/cancel${buildQueryString({ versionNo })}`,
    undefined,
  );
}

export function deleteProjectManagementTaskReminder(
  taskId: string,
  id: string,
  versionNo: number,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(
    `/project-management/tasks/${taskId}/reminders/${id}${buildQueryString({ versionNo })}`,
  );
}

export function getProjectManagementTaskAttachments(
  taskId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskAttachment[]>> {
  return httpClient.get<ProjectManagementTaskAttachment[]>(
    `/project-management/tasks/${taskId}/attachments`,
    undefined,
    signal,
  );
}

export function uploadProjectManagementTaskAttachment(
  taskId: string,
  file: File,
): Promise<ApiEnvelope<ProjectManagementTaskAttachment>> {
  const formData = new FormData();
  formData.append("file", file);
  return httpClient.postForm<ProjectManagementTaskAttachment>(
    `/project-management/tasks/${taskId}/attachments`,
    formData,
    { timeoutMs: 120_000 },
  );
}

export function getProjectManagementDataSpaceSummary(
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementDataSpaceSummary>> {
  return httpClient.get<ProjectManagementDataSpaceSummary>("/project-management/data-space/summary", undefined, signal);
}

export function searchProjectManagement(
  query: ProjectManagementSearchQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementSearchResponse>> {
  return httpClient.get<ProjectManagementSearchResponse>(
    `/project-management/search${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function exportProjectManagementReportCsv(
  query: ProjectManagementReportQuery,
): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(
    `/project-management/reports/projects.csv${buildQueryString(query)}`,
    { timeoutMs: 120_000 },
  );
}

export function exportProjectManagementReportExcel(
  query: ProjectManagementReportQuery,
): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(
    `/project-management/reports/projects.xlsx${buildQueryString(query)}`,
    { timeoutMs: 120_000 },
  );
}

export function exportProjectManagementSync(request: {
  projectId?: string;
  includeAttachments?: boolean;
  deviceId?: string;
}): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.postDownloadBlob("/project-management/sync/export", request, { timeoutMs: 120_000 });
}

export function previewProjectManagementSync(file: File): Promise<ApiEnvelope<ProjectManagementSyncPreviewResponse>> {
  const formData = new FormData();
  formData.append("file", file);
  return httpClient.postForm<ProjectManagementSyncPreviewResponse>("/project-management/sync/preview", formData, {
    timeoutMs: 120_000,
  });
}

export function applyProjectManagementSync(
  file: File,
  request: { currentPassword: string; confirmRisk: boolean; conflictStrategy: "Skip" | "Overwrite" | "Reject" },
): Promise<ApiEnvelope<ProjectManagementSyncImportResponse>> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("currentPassword", request.currentPassword);
  formData.append("confirmRisk", String(request.confirmRisk));
  formData.append("conflictStrategy", request.conflictStrategy);
  return httpClient.postForm<ProjectManagementSyncImportResponse>("/project-management/sync/apply", formData, {
    timeoutMs: 120_000,
  });
}

export function getProjectManagementSyncWatermark(deviceId: string): Promise<ApiEnvelope<ProjectManagementSyncWatermark>> {
  return httpClient.get<ProjectManagementSyncWatermark>(`/project-management/sync/watermark${buildQueryString({ deviceId })}`);
}

export function getProjectManagementSyncChanges(params: {
  projectId?: string;
  sinceSequenceNo?: number;
  limit?: number;
}): Promise<ApiEnvelope<ProjectManagementSyncJournalItem[]>> {
  return httpClient.get<ProjectManagementSyncJournalItem[]>(`/project-management/sync/changes${buildQueryString(params)}`);
}

export function acknowledgeProjectManagementSync(request: {
  deviceId: string;
  sequenceNo: number;
}): Promise<ApiEnvelope<ProjectManagementSyncWatermark>> {
  return httpClient.post<ProjectManagementSyncWatermark, typeof request>("/project-management/sync/acknowledge", request);
}

export function getProjectManagementAudit(
  query: ProjectManagementAuditQuery,
): Promise<ApiEnvelope<ProjectManagementAuditPage>> {
  return httpClient.get<ProjectManagementAuditPage>(`/project-management/audit${buildQueryString(query)}`);
}

export function getProjectManagementOperations(
  query: ProjectManagementOperationQuery,
): Promise<ApiEnvelope<ProjectManagementOperationPage>> {
  return httpClient.get<ProjectManagementOperationPage>(
    `/project-management/audit/operations${buildQueryString(query)}`,
  );
}

export function exportProjectManagementAudit(
  query: ProjectManagementAuditQuery,
): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(`/project-management/audit/export${buildQueryString(query)}`, { timeoutMs: 120_000 });
}

export function getProjectManagementBackups(): Promise<ApiEnvelope<ProjectManagementBackup[]>> {
  return httpClient.get<ProjectManagementBackup[]>("/project-management/backups");
}

export function createProjectManagementBackup(request: {
  currentPassword: string;
  confirmRisk: boolean;
  reason?: string;
}): Promise<ApiEnvelope<ProjectManagementBackup>> {
  return httpClient.post<ProjectManagementBackup, typeof request>("/project-management/backups", request);
}

export function restoreProjectManagementBackup(
  id: string,
  request: { currentPassword: string; confirmRisk: boolean },
): Promise<ApiEnvelope<ProjectManagementBackup>> {
  return httpClient.post<ProjectManagementBackup, typeof request>(`/project-management/backups/${id}/restore`, request);
}

export function previewProjectManagementBackupRestore(id: string): Promise<ApiEnvelope<ProjectManagementBackupRestorePreview>> {
  return httpClient.get<ProjectManagementBackupRestorePreview>(`/project-management/backups/${id}/restore-preview`);
}

export function getProjectManagementSavedViews(
  projectId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementSavedView[]>> {
  return httpClient.get<ProjectManagementSavedView[]>(
    `/project-management/projects/${projectId}/saved-views`,
    undefined,
    signal,
  );
}

export function createProjectManagementSavedView(
  projectId: string,
  request: ProjectManagementSavedViewUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementSavedView>> {
  return httpClient.post<ProjectManagementSavedView, ProjectManagementSavedViewUpsertRequest>(
    `/project-management/projects/${projectId}/saved-views`,
    request,
  );
}

export function updateProjectManagementSavedView(
  projectId: string,
  id: string,
  request: ProjectManagementSavedViewUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementSavedView>> {
  return httpClient.put<ProjectManagementSavedView, ProjectManagementSavedViewUpsertRequest>(
    `/project-management/projects/${projectId}/saved-views/${id}`,
    request,
  );
}

export function deleteProjectManagementSavedView(
  projectId: string,
  id: string,
  versionNo: number,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(
    `/project-management/projects/${projectId}/saved-views/${id}${buildQueryString({ versionNo })}`,
  );
}
