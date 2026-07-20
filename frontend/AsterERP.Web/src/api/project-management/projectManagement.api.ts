import type { ApiEnvelope } from "../../core/http/apiEnvelope";
import { httpClient } from "../../core/http/httpClient";
import { buildQueryString } from "../queryString";
import type { GridPageResult } from "../shared.types";

import type {
  ProjectManagementMemberCandidate,
  ProjectManagementMemberCandidateQuery,
  ProjectManagementProject,
  ProjectManagementProjectQuery,
  ProjectManagementHomeQuery,
  ProjectManagementHomeProjectsResponse,
  ProjectManagementHomeSummaryResponse,
  ProjectManagementProjectUpsertRequest,
  ProjectManagementRecycleQuery,
  ProjectManagementRecycleResponse,
  ProjectManagementOverviewItem,
  ProjectManagementOverviewQuery,
  ProjectManagementProjectUpdateRequest,
  ProjectManagementResource,
  ProjectManagementResourceUpsertRequest,
  ProjectManagementMyWorkItem,
  ProjectManagementMyWorkQuery,
  ProjectManagementMyWorkProjectOption,
  ProjectManagementMyWorkProjectOptionQuery,
    ProjectManagementTask,
  ProjectManagementTaskDetail,
  ProjectManagementTaskDependency,
    ProjectManagementTaskListItem,
  ProjectManagementTaskBatchUpdateRequest,
  ProjectManagementTaskBatchExecutionResult,
  ProjectManagementTaskQuery,
  ProjectManagementAuditPage,
  ProjectManagementAuditDetail,
  ProjectManagementAuditQuery,
  ProjectManagementAuditExportRequest,
  ProjectManagementAuditExportStartResponse,
  ProjectManagementOperationPage,
  ProjectManagementOperationQuery,
  ProjectManagementOperation,
  ProjectManagementReversibleCommand,
  ProjectManagementReversibleCommandExecuteRequest,
  ProjectManagementReversibleCommandStack,
  ProjectManagementRecyclePurgePreview,
  ProjectManagementRecycleTaskPurgePreview,
  ProjectManagementTaskUpsertRequest,
  ProjectManagementTaskComment,
  ProjectManagementTaskCommentPage,
  ProjectManagementTaskCommentQuery,
  ProjectManagementTaskCommentUpsertRequest,
  ProjectManagementTaskReminder,
  ProjectManagementTaskReminderCreateRequest,
  ProjectManagementTaskReminderUpdateRequest,
  ProjectManagementProjectSubscription,
  ProjectManagementProjectSubscriptionUpsertRequest,
  ProjectManagementProjectReminder,
  ProjectManagementProjectReminderCreateRequest,
  ProjectManagementNotificationQuery,
  ProjectManagementNotificationPage,
  ProjectManagementNotificationOpenResult,
  ProjectManagementTaskAttachment,
  ProjectManagementTaskTimeLog,
  ProjectManagementTaskTimeLogUpsertRequest,
  ProjectManagementTaskTimeLogUpdateRequest,
  ProjectManagementTaskWorkload,
  ProjectManagementTaskWorkloadQuery,
  ProjectManagementTaskFollower,
  ProjectManagementTaskFollowerUpsertRequest,
  ProjectManagementTaskParticipant,
  ProjectManagementTaskParticipantCandidate,
  ProjectManagementTaskParticipantCandidateQuery,
  ProjectManagementTaskParticipantUpsertRequest,
  ProjectManagementTaskDraft,
  ProjectManagementTaskDraftAttachment,
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
  ProjectManagementLabelUpsertRequest,
  ProjectManagementTaskLabel,
  ProjectManagementTaskLabelFilter,
  ProjectManagementTaskLabelSetRequest,
  ProjectManagementActivityPage,
  ProjectManagementActivity,
  ProjectManagementActivityQuery,
  ProjectManagementReportQuery,
  ProjectManagementProjectMarkdownOptions,
  ProjectManagementReportSnapshotRequest,
  ProjectManagementReportSnapshotStartResponse,
  ProjectManagementExcelImportPreview,
  ProjectManagementExcelImportResult,
  ProjectManagementSearchQuery,
  ProjectManagementSearchResponse,
  ProjectManagementSearchIndexStatus,
  ProjectManagementSyncJournalItem,
  ProjectManagementSyncWatermark,
  ProjectManagementSyncHistoryDetail,
  ProjectManagementSyncHistoryPage,
  ProjectManagementWebhookDelivery,
  ProjectManagementWebhookSubscription,
  ProjectManagementWebhookSubscriptionUpsertRequest,
} from "./projectManagement.types";

export function getProjectManagementHomeProjects(
  query: ProjectManagementHomeQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementHomeProjectsResponse>> {
  return httpClient.get<ProjectManagementHomeProjectsResponse>(
    `/project-management/home/projects${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementHomeSummary(
  query: ProjectManagementHomeQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementHomeSummaryResponse>> {
  return httpClient.get<ProjectManagementHomeSummaryResponse>(
    `/project-management/home/summary${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

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

export function getProjectManagementMyWorkProjectOptions(
  query: ProjectManagementMyWorkProjectOptionQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<{ total: number; items: ProjectManagementMyWorkProjectOption[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementMyWorkProjectOption[] }>(
    `/project-management/my-work/project-options${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function createProjectManagementProject(
  request: ProjectManagementProjectUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementProject>> {
  return httpClient.post<ProjectManagementProject, ProjectManagementProjectUpsertRequest>(
    "/project-management/projects",
    withClientMutationId(request),
  );
}

export function updateProjectManagementProject(
  id: string,
  request: ProjectManagementProjectUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementProject>> {
  return httpClient.put<ProjectManagementProject, ProjectManagementProjectUpsertRequest>(
    `/project-management/projects/${id}`,
    withClientMutationId(request),
  );
}

export function deleteProjectManagementProject(id: string, versionNo: number, clientMutationId?: string): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/projects/${id}${buildQueryString({ versionNo, clientMutationId: clientMutationId ?? createClientMutationId() })}`);
}

export function archiveProjectManagementProject(id: string, versionNo: number, clientMutationId?: string): Promise<ApiEnvelope<ProjectManagementProject>> {
  return httpClient.post<ProjectManagementProject, { versionNo: number; clientMutationId?: string }>(`/project-management/projects/${id}/archive`, { versionNo, clientMutationId: clientMutationId ?? createClientMutationId() });
}

function withClientMutationId(request: ProjectManagementProjectUpsertRequest): ProjectManagementProjectUpsertRequest {
  return { ...request, clientMutationId: request.clientMutationId ?? createClientMutationId() };
}

function createClientMutationId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
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
): Promise<ApiEnvelope<GridPageResult<ProjectManagementMember>>> {
  return httpClient.get<GridPageResult<ProjectManagementMember>>(
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
): Promise<ApiEnvelope<GridPageResult<ProjectManagementMilestone>>> {
  return httpClient.get<GridPageResult<ProjectManagementMilestone>>(
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

export function getProjectManagementPublicLabels(signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementLabel[]>> {
  return httpClient.get<ProjectManagementLabel[]>("/project-management/labels", undefined, signal);
}

export function createProjectManagementProjectLabel(
  projectId: string,
  request: ProjectManagementLabelUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementLabel>> {
  return httpClient.post<ProjectManagementLabel, ProjectManagementLabelUpsertRequest>(`/project-management/projects/${projectId}/labels`, request);
}

export function createProjectManagementPublicLabel(
  request: ProjectManagementLabelUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementLabel>> {
  return httpClient.post<ProjectManagementLabel, ProjectManagementLabelUpsertRequest>("/project-management/labels", request);
}

export function updateProjectManagementProjectLabel(
  projectId: string,
  id: string,
  request: ProjectManagementLabelUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementLabel>> {
  return httpClient.put<ProjectManagementLabel, ProjectManagementLabelUpsertRequest>(`/project-management/projects/${projectId}/labels/${id}`, request);
}

export function updateProjectManagementPublicLabel(
  id: string,
  request: ProjectManagementLabelUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementLabel>> {
  return httpClient.put<ProjectManagementLabel, ProjectManagementLabelUpsertRequest>(`/project-management/labels/${id}`, request);
}

export function deleteProjectManagementProjectLabel(projectId: string, id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/projects/${projectId}/labels/${id}${buildQueryString({ versionNo })}`);
}

export function deleteProjectManagementPublicLabel(id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/labels/${id}${buildQueryString({ versionNo })}`);
}

export function getProjectManagementTaskLabels(
  taskId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskLabel[]>> {
  return httpClient.get<ProjectManagementTaskLabel[]>(`/project-management/tasks/${taskId}/labels`, undefined, signal);
}

export function setProjectManagementTaskLabels(
  taskId: string,
  request: ProjectManagementTaskLabelSetRequest,
): Promise<ApiEnvelope<{ taskId: string }>> {
  return httpClient.put<{ taskId: string }, ProjectManagementTaskLabelSetRequest>(`/project-management/tasks/${taskId}/labels`, request);
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

export function previewProjectManagementTaskPurge(id: string, versionNo: number, purgeDescendants: boolean): Promise<ApiEnvelope<ProjectManagementRecycleTaskPurgePreview>> {
  return httpClient.get<ProjectManagementRecycleTaskPurgePreview>(`/project-management/recycle/tasks/${id}/purge-preview${buildQueryString({ versionNo, purgeDescendants })}`);
}

export function purgeProjectManagementTask(id: string, request: { versionNo: number; currentPassword: string; confirmRisk: boolean; purgeDescendants: boolean }): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.post<{ id: string }, typeof request>(`/project-management/recycle/tasks/${id}/purge`, request);
}

export function getProjectManagementActivities(
  projectId: string,
  query: ProjectManagementActivityQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementActivityPage>> {
  return httpClient.get<ProjectManagementActivityPage>(
    `/project-management/projects/${projectId}/activities${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementTaskActivities(
  taskId: string,
  query: ProjectManagementActivityQuery = {},
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementActivityPage>> {
  return httpClient.get<ProjectManagementActivityPage>(
    `/project-management/tasks/${taskId}/activities${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementProjectUpdates(
  projectId: string,
  query: ProjectManagementActivityQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementActivityPage>> {
  return httpClient.get<ProjectManagementActivityPage>(
    `/project-management/projects/${projectId}/updates${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function createProjectManagementProjectUpdate(
  projectId: string,
  request: ProjectManagementProjectUpdateRequest,
): Promise<ApiEnvelope<ProjectManagementActivity>> {
  return httpClient.post<ProjectManagementActivity, ProjectManagementProjectUpdateRequest>(
    `/project-management/projects/${projectId}/updates`,
    request,
  );
}

export function getProjectManagementProjectResources(projectId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementResource[]>> {
  return httpClient.get<ProjectManagementResource[]>(`/project-management/projects/${projectId}/resources`, undefined, signal);
}

export function createProjectManagementProjectResource(projectId: string, request: ProjectManagementResourceUpsertRequest): Promise<ApiEnvelope<ProjectManagementResource>> {
  return httpClient.post<ProjectManagementResource, ProjectManagementResourceUpsertRequest>(`/project-management/projects/${projectId}/resources`, request);
}

export function updateProjectManagementProjectResource(projectId: string, id: string, request: ProjectManagementResourceUpsertRequest): Promise<ApiEnvelope<ProjectManagementResource>> {
  return httpClient.patch<ProjectManagementResource, ProjectManagementResourceUpsertRequest>(`/project-management/projects/${projectId}/resources/${id}`, request);
}

export function deleteProjectManagementProjectResource(projectId: string, id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/projects/${projectId}/resources/${id}?versionNo=${encodeURIComponent(versionNo)}`);
}

export function getProjectManagementTasks(
  query: ProjectManagementTaskQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<{ total: number; items: ProjectManagementTaskListItem[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementTaskListItem[] }>(
    `/project-management/projects/${encodeURIComponent(query.projectId)}/work-items${buildProjectManagementLabelFilterQuery(query)}`,
    undefined,
    signal,
  );
}

export function getProjectManagementTask(
  id: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskDetail>> {
  return httpClient.get<ProjectManagementTaskDetail>(`/project-management/work-items/${id}`, undefined, signal);
}

export function getProjectManagementTaskDependencies(
  projectId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskDependency[]>> {
  return httpClient.get<ProjectManagementTaskDependency[]>(
    `/project-management/projects/${projectId}/task-dependencies`,
    undefined,
    signal,
  );
}

export function createProjectManagementTask(
  projectId: string,
  request: ProjectManagementTaskUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementTaskDetail>> {
  return httpClient.post<ProjectManagementTaskDetail, ProjectManagementTaskUpsertRequest>(
    `/project-management/projects/${encodeURIComponent(projectId)}/work-items`,
    request,
  );
}

export function updateProjectManagementTask(
  id: string,
  request: ProjectManagementTaskUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementTaskDetail>> {
  return httpClient.put<ProjectManagementTaskDetail, ProjectManagementTaskUpsertRequest>(
    `/project-management/work-items/${id}`,
    request,
  );
}

export function changeProjectManagementTaskStatus(
  id: string,
  request: { status: string; versionNo: number },
): Promise<ApiEnvelope<ProjectManagementTaskDetail>> {
  return httpClient.post<ProjectManagementTaskDetail, typeof request>(
    `/project-management/work-items/${id}/status`,
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
): Promise<ApiEnvelope<ProjectManagementTaskDetail>> {
  return httpClient.post<ProjectManagementTaskDetail, typeof request>(`/project-management/work-items/${id}/move`, request);
}

export function updateProjectManagementTasksBatch(
  request: ProjectManagementTaskBatchUpdateRequest,
): Promise<ApiEnvelope<ProjectManagementTask[]>> {
  return httpClient.post<ProjectManagementTask[], ProjectManagementTaskBatchUpdateRequest>(
    '/project-management/tasks/batch/update',
    request,
  );
}

export function executeProjectManagementTasksBatch(
  request: ProjectManagementTaskBatchUpdateRequest,
): Promise<ApiEnvelope<ProjectManagementTaskBatchExecutionResult>> {
  return httpClient.post<ProjectManagementTaskBatchExecutionResult, ProjectManagementTaskBatchUpdateRequest>(
    '/project-management/tasks/batch/execute',
    request,
  );
}

export function deleteProjectManagementTask(id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/tasks/${id}${buildQueryString({ versionNo })}`);
}

export function getProjectManagementTaskFollowers(taskId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementTaskFollower[]>> {
  return httpClient.get<ProjectManagementTaskFollower[]>(`/project-management/work-items/${taskId}/followers`, undefined, signal);
}

export function addProjectManagementTaskFollower(taskId: string, request: ProjectManagementTaskFollowerUpsertRequest): Promise<ApiEnvelope<ProjectManagementTaskFollower>> {
  return httpClient.post<ProjectManagementTaskFollower, ProjectManagementTaskFollowerUpsertRequest>(`/project-management/work-items/${taskId}/followers`, request);
}

export function removeProjectManagementTaskFollower(taskId: string, userId: string, versionNo: number): Promise<ApiEnvelope<{ userId: string }>> {
  return httpClient.delete<{ userId: string }>(`/project-management/work-items/${taskId}/followers/${encodeURIComponent(userId)}${buildQueryString({ versionNo })}`);
}

export function getProjectManagementTaskParticipants(taskId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementTaskParticipant[]>> {
  return httpClient.get<ProjectManagementTaskParticipant[]>(`/project-management/tasks/${taskId}/participants`, undefined, signal);
}

export function getProjectManagementTaskParticipantCandidates(
  taskId: string,
  query: ProjectManagementTaskParticipantCandidateQuery = {},
  signal?: AbortSignal,
): Promise<ApiEnvelope<GridPageResult<ProjectManagementTaskParticipantCandidate>>> {
  return httpClient.get<GridPageResult<ProjectManagementTaskParticipantCandidate>>(
    `/project-management/tasks/${taskId}/participants/candidates${buildQueryString(query)}`,
    undefined,
    signal,
  );
}

export function addProjectManagementTaskParticipant(
  taskId: string,
  request: ProjectManagementTaskParticipantUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementTaskParticipant>> {
  return httpClient.post<ProjectManagementTaskParticipant, ProjectManagementTaskParticipantUpsertRequest>(
    `/project-management/tasks/${taskId}/participants`,
    request,
  );
}

export function removeProjectManagementTaskParticipant(taskId: string, id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/tasks/${taskId}/participants/${encodeURIComponent(id)}${buildQueryString({ versionNo })}`);
}

export function createProjectManagementTaskDraft(projectId: string, payloadJson = '{}'): Promise<ApiEnvelope<ProjectManagementTaskDraft>> {
  return httpClient.post<ProjectManagementTaskDraft, { projectId: string; payloadJson: string }>('/project-management/drafts', { projectId, payloadJson });
}

export function uploadProjectManagementTaskDraftAttachment(draftId: string, file: File): Promise<ApiEnvelope<ProjectManagementTaskDraftAttachment>> {
  const formData = new FormData();
  formData.append('file', file);
  return httpClient.postForm<ProjectManagementTaskDraftAttachment>(`/project-management/drafts/${draftId}/attachments`, formData, { timeoutMs: 120_000 });
}

export function getProjectManagementTaskComments(
  taskId: string,
  query: ProjectManagementTaskCommentQuery = {},
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskCommentPage>> {
  return httpClient.get<ProjectManagementTaskCommentPage>(
    `/project-management/tasks/${taskId}/comments${buildQueryString(query)}`,
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

export function updateProjectManagementTaskComment(
  taskId: string,
  id: string,
  request: ProjectManagementTaskCommentUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementTaskComment>> {
  return httpClient.put<ProjectManagementTaskComment, ProjectManagementTaskCommentUpsertRequest>(
    `/project-management/tasks/${taskId}/comments/${id}`,
    request,
  );
}

export function deleteProjectManagementTaskComment(taskId: string, id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(`/project-management/tasks/${taskId}/comments/${id}${buildQueryString({ versionNo })}`);
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

export function getProjectManagementProjectSubscription(projectId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementProjectSubscription | null>> {
  return httpClient.get<ProjectManagementProjectSubscription | null>(`/project-management/projects/${projectId}/subscription`, undefined, signal);
}

export function saveProjectManagementProjectSubscription(projectId: string, request: ProjectManagementProjectSubscriptionUpsertRequest): Promise<ApiEnvelope<ProjectManagementProjectSubscription>> {
  return httpClient.put<ProjectManagementProjectSubscription, ProjectManagementProjectSubscriptionUpsertRequest>(`/project-management/projects/${projectId}/subscription`, request);
}

export function deleteProjectManagementProjectSubscription(projectId: string, versionNo?: number): Promise<ApiEnvelope<{ projectId: string }>> {
  return httpClient.delete<{ projectId: string }>(`/project-management/projects/${projectId}/subscription${buildQueryString({ versionNo })}`);
}

export function getProjectManagementProjectReminders(projectId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementProjectReminder[]>> {
  return httpClient.get<ProjectManagementProjectReminder[]>(`/project-management/projects/${projectId}/reminders`, undefined, signal);
}

export function createProjectManagementProjectReminder(projectId: string, request: ProjectManagementProjectReminderCreateRequest): Promise<ApiEnvelope<ProjectManagementProjectReminder>> {
  return httpClient.post<ProjectManagementProjectReminder, ProjectManagementProjectReminderCreateRequest>(`/project-management/projects/${projectId}/reminders`, request);
}

export function cancelProjectManagementProjectReminder(projectId: string, id: string, versionNo: number): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.post<{ id: string }, undefined>(`/project-management/projects/${projectId}/reminders/${id}/cancel${buildQueryString({ versionNo })}`, undefined);
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

export function downloadProjectManagementTaskAttachment(
  attachment: ProjectManagementTaskAttachment,
  signal?: AbortSignal,
): Promise<{ blob: Blob; fileName: string }> {
  if (!attachment.downloadUrl) return Promise.reject(new Error('附件下载链接已失效'));
  return httpClient.downloadBlob(normalizeProjectManagementAttachmentPath(attachment.downloadUrl), { signal, timeoutMs: 120_000 });
}

export function previewProjectManagementTaskAttachment(
  attachment: ProjectManagementTaskAttachment,
  signal?: AbortSignal,
): Promise<{ blob: Blob; fileName: string }> {
  if (!attachment.previewSupported) return Promise.reject(new Error('当前文件格式不支持在线预览，请下载后查看'));
  if (!attachment.previewUrl) return Promise.reject(new Error('附件预览链接已失效'));
  return httpClient.downloadBlob(normalizeProjectManagementAttachmentPath(attachment.previewUrl), { signal, timeoutMs: 120_000 });
}

export function deleteProjectManagementTaskAttachment(
  taskId: string,
  id: string,
  versionNo: number,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(
    `/project-management/tasks/${taskId}/attachments/${id}${buildQueryString({ versionNo })}`,
  );
}

export function getProjectManagementTaskTimeLogs(
  taskId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskTimeLog[]>> {
  return httpClient.get<ProjectManagementTaskTimeLog[]>(
    `/project-management/tasks/${taskId}/time-logs`,
    undefined,
    signal,
  );
}

export function createProjectManagementTaskTimeLog(
  taskId: string,
  request: ProjectManagementTaskTimeLogUpsertRequest,
): Promise<ApiEnvelope<ProjectManagementTaskTimeLog>> {
  return httpClient.post<ProjectManagementTaskTimeLog, ProjectManagementTaskTimeLogUpsertRequest>(
    `/project-management/tasks/${taskId}/time-logs`,
    request,
  );
}

export function updateProjectManagementTaskTimeLog(
  taskId: string,
  id: string,
  request: ProjectManagementTaskTimeLogUpdateRequest,
): Promise<ApiEnvelope<ProjectManagementTaskTimeLog>> {
  return httpClient.put<ProjectManagementTaskTimeLog, ProjectManagementTaskTimeLogUpdateRequest>(
    `/project-management/tasks/${taskId}/time-logs/${id}`,
    request,
  );
}

export function deleteProjectManagementTaskTimeLog(
  taskId: string,
  id: string,
  versionNo: number,
): Promise<ApiEnvelope<{ id: string }>> {
  return httpClient.delete<{ id: string }>(
    `/project-management/tasks/${taskId}/time-logs/${id}${buildQueryString({ versionNo })}`,
  );
}

export function getProjectManagementWorkloads(
  query: ProjectManagementTaskWorkloadQuery,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementTaskWorkload[]>> {
  return httpClient.get<ProjectManagementTaskWorkload[]>(
    `/project-management/workloads${buildQueryString(query)}`,
    undefined,
    signal,
  );
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

export function getProjectManagementSearchIndexStatus(
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementSearchIndexStatus>> {
  return httpClient.get<ProjectManagementSearchIndexStatus>(
    "/project-management/search/index/status",
    undefined,
    signal,
  );
}

export function exportProjectManagementReportCsv(
  query: ProjectManagementReportQuery,
): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(
    `/project-management/reports/projects.csv${buildProjectManagementLabelFilterQuery(query)}`,
    { timeoutMs: 120_000 },
  );
}

export function exportProjectManagementReportExcel(
  query: ProjectManagementReportQuery,
): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(
    `/project-management/reports/projects.xlsx${buildProjectManagementLabelFilterQuery(query)}`,
    { timeoutMs: 120_000 },
  );
}

export function exportProjectManagementTasksCsv(
  query: ProjectManagementTaskQuery,
): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(
    `/project-management/reports/tasks.csv${buildProjectManagementLabelFilterQuery(query)}`,
    { timeoutMs: 120_000 },
  );
}

export function exportProjectManagementProjectMarkdown(
  projectId: string,
  options: ProjectManagementProjectMarkdownOptions = {},
): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(
    `/project-management/reports/projects/${encodeURIComponent(projectId)}/summary.md${buildQueryString(options)}`,
    { timeoutMs: 120_000 },
  );
}

export function startProjectManagementReportSnapshot(
  request: ProjectManagementReportSnapshotRequest,
): Promise<ApiEnvelope<ProjectManagementReportSnapshotStartResponse>> {
  return httpClient.post<ProjectManagementReportSnapshotStartResponse, ProjectManagementReportSnapshotRequest>(
    '/project-management/reports/snapshots',
    request,
  );
}

export function downloadProjectManagementReportSnapshot(operationId: string): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(`/project-management/reports/snapshots/${encodeURIComponent(operationId)}/download`, { timeoutMs: 120_000 });
}

export function retryProjectManagementReportSnapshot(operationId: string): Promise<ApiEnvelope<ProjectManagementReportSnapshotStartResponse>> {
  return httpClient.post<ProjectManagementReportSnapshotStartResponse, undefined>(`/project-management/reports/snapshots/${encodeURIComponent(operationId)}/retry`, undefined);
}

export function downloadProjectManagementExcelTemplate(signal?: AbortSignal): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob('/project-management/excel-import/template', { timeoutMs: 120_000 }, signal);
}

export function previewProjectManagementExcel(
  file: File,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementExcelImportPreview>> {
  const formData = new FormData();
  formData.append('file', file);
  return httpClient.postForm<ProjectManagementExcelImportPreview>('/project-management/excel-import/preview', formData, { timeoutMs: 120_000 }, signal);
}

export function confirmProjectManagementExcel(
  file: File,
  request: { previewId: string; idempotencyKey: string },
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementExcelImportResult>> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('previewId', request.previewId);
  formData.append('idempotencyKey', request.idempotencyKey);
  return httpClient.postForm<ProjectManagementExcelImportResult>('/project-management/excel-import/confirm', formData, { timeoutMs: 120_000 }, signal);
}

export function getProjectManagementExcelImportResult(
  importId: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementExcelImportResult>> {
  return httpClient.get<ProjectManagementExcelImportResult>(`/project-management/excel-import/results/${encodeURIComponent(importId)}`, undefined, signal);
}

export function exportProjectManagementSync(request: {
  projectId?: string;
  includeAttachments?: boolean;
  deviceId?: string;
  mode?: 'Full' | 'Incremental' | 'History';
  sinceSequenceNo?: number;
}): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.postDownloadBlob("/project-management/sync/export", request, { timeoutMs: 120_000 });
}

export function previewProjectManagementSync(file: File, signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementSyncPreviewResponse>> {
  const formData = new FormData();
  formData.append("file", file);
  return httpClient.postForm<ProjectManagementSyncPreviewResponse>("/project-management/sync/preview", formData, {
    timeoutMs: 120_000,
    signal,
  });
}

export function applyProjectManagementSync(
  file: File,
  request: { currentPassword: string; confirmRisk: boolean; conflictStrategy: "Skip" | "Overwrite" | "Merge" | "Reject"; idempotencyKey?: string; deviceId?: string },
): Promise<ApiEnvelope<ProjectManagementSyncImportResponse>> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("currentPassword", request.currentPassword);
  formData.append("confirmRisk", String(request.confirmRisk));
  formData.append("conflictStrategy", request.conflictStrategy);
  if (request.idempotencyKey?.trim()) formData.append("idempotencyKey", request.idempotencyKey.trim());
  if (request.deviceId?.trim()) formData.append("deviceId", request.deviceId.trim());
  return httpClient.postForm<ProjectManagementSyncImportResponse>("/project-management/sync/apply", formData, {
    timeoutMs: 120_000,
  });
}

export function retryProjectManagementSync(
  historyId: string,
  file: File,
  request: { currentPassword: string; confirmRisk: boolean; conflictStrategy: "Skip" | "Overwrite" | "Merge" | "Reject"; idempotencyKey?: string; deviceId?: string },
): Promise<ApiEnvelope<ProjectManagementSyncImportResponse>> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("currentPassword", request.currentPassword);
  formData.append("confirmRisk", String(request.confirmRisk));
  formData.append("conflictStrategy", request.conflictStrategy);
  if (request.idempotencyKey?.trim()) formData.append("idempotencyKey", request.idempotencyKey.trim());
  if (request.deviceId?.trim()) formData.append("deviceId", request.deviceId.trim());
  return httpClient.postForm<ProjectManagementSyncImportResponse>(`/project-management/sync/history/${encodeURIComponent(historyId)}/retry`, formData, { timeoutMs: 120_000 });
}

export function getProjectManagementSyncHistory(params: { pageIndex?: number; pageSize?: number; status?: string } = {}): Promise<ApiEnvelope<ProjectManagementSyncHistoryPage>> {
  return httpClient.get<ProjectManagementSyncHistoryPage>(`/project-management/sync/history${buildQueryString(params)}`);
}

export function getProjectManagementSyncHistoryDetail(historyId: string): Promise<ApiEnvelope<ProjectManagementSyncHistoryDetail>> {
  return httpClient.get<ProjectManagementSyncHistoryDetail>(`/project-management/sync/history/${encodeURIComponent(historyId)}`);
}

export function downloadProjectManagementSyncHistoryReport(historyId: string): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(`/project-management/sync/history/${encodeURIComponent(historyId)}/report`, { timeoutMs: 120_000 });
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

export function getProjectManagementAuditDetail(
  id: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementAuditDetail>> {
  return httpClient.get<ProjectManagementAuditDetail>(`/project-management/audit/${encodeURIComponent(id)}`, undefined, signal);
}

export function getProjectManagementOperations(
  query: ProjectManagementOperationQuery,
): Promise<ApiEnvelope<ProjectManagementOperationPage>> {
  return httpClient.get<ProjectManagementOperationPage>(
    `/project-management/audit/operations${buildQueryString(query)}`,
  );
}

export function getProjectManagementOperation(
  id: string,
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementOperation>> {
  return httpClient.get<ProjectManagementOperation>(`/project-management/operations/${id}`, undefined, signal);
}

export function startProjectManagementWorkspaceValidation(): Promise<ApiEnvelope<ProjectManagementOperation>> {
  return httpClient.post<ProjectManagementOperation, undefined>("/project-management/operations/maintenance/workspace-validation", undefined);
}

export function cancelProjectManagementOperation(id: string): Promise<ApiEnvelope<ProjectManagementOperation>> {
  return httpClient.post<ProjectManagementOperation, undefined>(`/project-management/operations/${id}/cancel`, undefined);
}

export function getProjectManagementReversibleCommandStack(
  signal?: AbortSignal,
): Promise<ApiEnvelope<ProjectManagementReversibleCommandStack>> {
  return httpClient.get<ProjectManagementReversibleCommandStack>("/project-management/reversible-commands", undefined, signal);
}

export function undoProjectManagementReversibleCommand(
  request: ProjectManagementReversibleCommandExecuteRequest,
): Promise<ApiEnvelope<ProjectManagementReversibleCommand>> {
  return httpClient.post<ProjectManagementReversibleCommand, ProjectManagementReversibleCommandExecuteRequest>(
    "/project-management/reversible-commands/undo",
    request,
  );
}

export function redoProjectManagementReversibleCommand(
  request: ProjectManagementReversibleCommandExecuteRequest,
): Promise<ApiEnvelope<ProjectManagementReversibleCommand>> {
  return httpClient.post<ProjectManagementReversibleCommand, ProjectManagementReversibleCommandExecuteRequest>(
    "/project-management/reversible-commands/redo",
    request,
  );
}

export function startProjectManagementAuditExport(
  request: ProjectManagementAuditExportRequest,
): Promise<ApiEnvelope<ProjectManagementAuditExportStartResponse>> {
  return httpClient.post<ProjectManagementAuditExportStartResponse, ProjectManagementAuditExportRequest>('/project-management/audit/exports', request);
}

export function downloadProjectManagementAuditExport(operationId: string): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(`/project-management/audit/exports/${encodeURIComponent(operationId)}/download`, { timeoutMs: 120_000 });
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

function buildProjectManagementLabelFilterQuery<TQuery extends { labelFilter?: ProjectManagementTaskLabelFilter }>(query: TQuery): string {
  const { labelFilter, ...baseQuery } = query;
  const searchParams = new URLSearchParams(buildQueryString(baseQuery));
  if (!labelFilter || labelFilter.labelIds.length === 0) return searchParams.size ? `?${searchParams.toString()}` : '';

  labelFilter.labelIds.forEach((labelId, index) => searchParams.append(`labelFilter.labelIds[${index}]`, labelId));
  if (labelFilter.matchMode) searchParams.set('labelFilter.matchMode', labelFilter.matchMode);
  return `?${searchParams.toString()}`;
}

export function getProjectManagementWebhookSubscriptions(projectId: string, signal?: AbortSignal): Promise<ApiEnvelope<ProjectManagementWebhookSubscription[]>> {
  return httpClient.get<ProjectManagementWebhookSubscription[]>(`/project-management/webhooks/subscriptions${buildQueryString({ projectId })}`, undefined, signal);
}

export function saveProjectManagementWebhookSubscription(request: ProjectManagementWebhookSubscriptionUpsertRequest): Promise<ApiEnvelope<ProjectManagementWebhookSubscription>> {
  return httpClient.put<ProjectManagementWebhookSubscription, ProjectManagementWebhookSubscriptionUpsertRequest>('/project-management/webhooks/subscriptions', request);
}

export function deleteProjectManagementWebhookSubscription(id: string): Promise<ApiEnvelope<null>> {
  return httpClient.delete<null>(`/project-management/webhooks/subscriptions/${id}`);
}

export function getProjectManagementWebhookDeliveries(projectId: string, query: { pageIndex?: number; pageSize?: number }, signal?: AbortSignal): Promise<ApiEnvelope<{ total: number; items: ProjectManagementWebhookDelivery[] }>> {
  return httpClient.get<{ total: number; items: ProjectManagementWebhookDelivery[] }>(`/project-management/webhooks/deliveries${buildQueryString({ projectId, ...query })}`, undefined, signal);
}

export function replayProjectManagementWebhookDelivery(eventId: string, reason?: string): Promise<ApiEnvelope<ProjectManagementWebhookDelivery>> {
  return httpClient.post<ProjectManagementWebhookDelivery, { reason?: string }>(`/project-management/webhooks/deliveries/${eventId}/replay`, { reason });
}


function normalizeProjectManagementAttachmentPath(path: string): string {
  const trimmed = path.trim();
  if (trimmed.startsWith('/api/')) return trimmed.slice(4);
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
}
