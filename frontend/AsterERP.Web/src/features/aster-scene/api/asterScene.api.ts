import { buildQueryString } from '@/api/queryString';
import type { GridPageResult } from '@/api/shared.types';
import { httpClient } from '@/core/http/httpClient';

import type {
  AsterSceneAsset,
  AsterSceneAppeal,
  AsterSceneGeneratedAssetRequest,
  AsterSceneAssetRegisterRequest,
  AsterSceneCreateProjectRequest,
  AsterSceneCreatorProfile,
  AsterSceneDocumentResponse,
  AsterSceneDocumentVersion,
  AsterSceneGridQuery,
  AsterSceneJob,
  AsterSceneModerationCase,
  AsterSceneProject,
  AsterScenePublicWork,
  AsterScenePublishRequest,
  AsterScenePublishVersion,
  AsterSceneRestoreDocumentVersionRequest,
  AsterSceneRuntimeEventRequest,
  AsterSceneRuntimeEventResponse,
  AsterSceneSaveDocumentRequest,
  AsterSceneSaveDocumentResponse,
  AsterSceneSubscription,
  AsterSceneSubscriptionPlan,
  AsterSceneSupportTicket,
  AsterSceneSupportTicketDetail,
  AsterSceneUploadSession,
  AsterSceneUsageSummary,
  AsterSceneUsageLedgerEntry,
  AsterSceneUpdateProjectRequest,
  RuntimeManifest,
  SceneDocument
} from '../model/types';

export const asterSceneApi = {
  projects: {
    list: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterSceneProject>>(`/asterscene/projects${buildQueryString(query)}`, undefined, signal),
    create: (request: AsterSceneCreateProjectRequest, signal?: AbortSignal) =>
      httpClient.post<AsterSceneProject, AsterSceneCreateProjectRequest>('/asterscene/projects', request, undefined, signal),
    update: (projectId: string, request: AsterSceneUpdateProjectRequest, signal?: AbortSignal) =>
      httpClient.put<AsterSceneProject, AsterSceneUpdateProjectRequest>(
        `/asterscene/projects/${encodeURIComponent(projectId)}`,
        request,
        undefined,
        signal
      ),
    document: (projectId: string, signal?: AbortSignal) =>
      httpClient.get<AsterSceneDocumentResponse>(`/asterscene/projects/${encodeURIComponent(projectId)}/document`, undefined, signal),
    saveDocument: (projectId: string, request: AsterSceneSaveDocumentRequest, signal?: AbortSignal) =>
      httpClient.put<AsterSceneSaveDocumentResponse, AsterSceneSaveDocumentRequest>(
        `/asterscene/projects/${encodeURIComponent(projectId)}/document`,
        request,
        undefined,
        signal
      ),
    documentVersions: (projectId: string, query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterSceneDocumentVersion>>(
        `/asterscene/projects/${encodeURIComponent(projectId)}/document/versions${buildQueryString(query)}`,
        undefined,
        signal
      ),
    documentVersion: (projectId: string, revision: number, signal?: AbortSignal) =>
      httpClient.get<AsterSceneDocumentResponse>(
        `/asterscene/projects/${encodeURIComponent(projectId)}/document/versions/${revision}`,
        undefined,
        signal
      ),
    restoreDocumentVersion: (projectId: string, revision: number, request: AsterSceneRestoreDocumentVersionRequest, signal?: AbortSignal) =>
      httpClient.post<AsterSceneSaveDocumentResponse, AsterSceneRestoreDocumentVersionRequest>(
        `/asterscene/projects/${encodeURIComponent(projectId)}/document/versions/${revision}/restore`,
        request,
        undefined,
        signal
      ),
    validateDocument: (projectId: string, document: SceneDocument, signal?: AbortSignal) =>
      httpClient.post<{ errors: unknown[]; isValid: boolean; warnings: unknown[] }, SceneDocument>(
        `/asterscene/projects/${encodeURIComponent(projectId)}/document/validate`,
        document,
        undefined,
        signal
      ),
    delete: (projectId: string, signal?: AbortSignal) =>
      httpClient.delete<boolean>(`/asterscene/projects/${encodeURIComponent(projectId)}`, undefined, signal)
  },
  assets: {
    list: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterSceneAsset>>(`/asterscene/assets${buildQueryString(query)}`, undefined, signal),
    register: (request: AsterSceneAssetRegisterRequest, signal?: AbortSignal) =>
      httpClient.post<AsterSceneAsset, AsterSceneAssetRegisterRequest>('/asterscene/assets/register', request, undefined, signal),
    createGenerated: (request: AsterSceneGeneratedAssetRequest, signal?: AbortSignal) =>
      httpClient.post<AsterSceneAsset, AsterSceneGeneratedAssetRequest>('/asterscene/assets/generated', request, undefined, signal),
    startUpload: (
      request: {
        assetType: string;
        checksum?: string | null;
        clientMutationId?: string;
        contentType?: string | null;
        fileName: string;
        projectId: string;
        sizeBytes: number;
        totalChunks: number;
      },
      signal?: AbortSignal
    ) => httpClient.post<AsterSceneUploadSession, typeof request>('/asterscene/assets/uploads', request, undefined, signal),
    uploadChunk: (uploadId: string, chunkIndex: number, chunk: Blob, checksum: string, signal?: AbortSignal) => {
      const formData = new FormData();
      formData.append('chunk', chunk, `chunk-${chunkIndex}.part`);
      formData.append('checksum', checksum);
      return httpClient.postForm<AsterSceneUploadSession>(
        `/asterscene/assets/uploads/${encodeURIComponent(uploadId)}/chunks/${chunkIndex}`,
        formData,
        120_000,
        signal
      );
    },
    completeUpload: (uploadId: string, request: { clientMutationId: string; metadata: Record<string, unknown> | null }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneAsset, typeof request>(
        `/asterscene/assets/uploads/${encodeURIComponent(uploadId)}/complete`,
        request,
        undefined,
        signal
      ),
    delete: (assetId: string, signal?: AbortSignal) =>
      httpClient.delete<boolean>(`/asterscene/assets/${encodeURIComponent(assetId)}`, undefined, signal)
  },
  jobs: {
    list: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterSceneJob>>(`/asterscene/jobs${buildQueryString(query)}`, undefined, signal),
    aiGenerate: (request: { clientMutationId: string; expectedRevision: number; projectId: string; prompt: string }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneJob, typeof request>('/asterscene/jobs/ai-generate', request, undefined, signal)
  },
  publish: {
    versions: (projectId: string, query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterScenePublishVersion>>(
        `/asterscene/projects/${encodeURIComponent(projectId)}/publish/versions${buildQueryString(query)}`,
        undefined,
        signal
      ),
    execute: (projectId: string, request: AsterScenePublishRequest, signal?: AbortSignal) =>
      httpClient.post<{ manifest: RuntimeManifest; publicWork?: AsterScenePublicWork | null; publishVersion: unknown }, AsterScenePublishRequest>(
        `/asterscene/projects/${encodeURIComponent(projectId)}/publish`,
        request,
        undefined,
        signal
      ),
    rollback: (
      projectId: string,
      publishCode: string,
      request: { clientMutationId: string; documentHash: string; expectedRevision: number },
      signal?: AbortSignal
    ) =>
      httpClient.post<{ manifest: RuntimeManifest; publicWork?: AsterScenePublicWork | null; publishVersion: unknown }, typeof request>(
        `/asterscene/projects/${encodeURIComponent(projectId)}/publish/${encodeURIComponent(publishCode)}/rollback`,
        request,
        undefined,
        signal
      )
  },
  public: {
    explore: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterScenePublicWork>>(`/public/asterscene/explore${buildQueryString(query)}`, undefined, signal),
    templates: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterScenePublicWork>>(`/public/asterscene/templates${buildQueryString(query)}`, undefined, signal),
    work: (slug: string, signal?: AbortSignal) =>
      httpClient.get<AsterScenePublicWork>(`/public/asterscene/works/${encodeURIComponent(slug)}`, undefined, signal),
    creator: (handle: string, signal?: AbortSignal) =>
      httpClient.get<AsterSceneCreatorProfile>(`/public/asterscene/creator/${encodeURIComponent(handle)}`, undefined, signal),
    manifest: (publishCode: string, signal?: AbortSignal) =>
      httpClient.get<RuntimeManifest>(`/public/asterscene/player/${encodeURIComponent(publishCode)}/manifest`, undefined, signal),
    recordRuntimeEvent: (request: AsterSceneRuntimeEventRequest, signal?: AbortSignal) =>
      httpClient.post<AsterSceneRuntimeEventResponse, AsterSceneRuntimeEventRequest>(
        '/public/asterscene/runtime-events',
        request,
        undefined,
        signal
      )
  },
  community: {
    like: (workId: string, clientMutationId: string, signal?: AbortSignal) =>
      httpClient.post<AsterScenePublicWork, { clientMutationId: string }>(
        `/community/asterscene/works/${encodeURIComponent(workId)}/like`,
        { clientMutationId },
        undefined,
        signal
      ),
    favorite: (workId: string, clientMutationId: string, signal?: AbortSignal) =>
      httpClient.post<AsterScenePublicWork, { clientMutationId: string }>(
        `/community/asterscene/works/${encodeURIComponent(workId)}/favorite`,
        { clientMutationId },
        undefined,
        signal
      ),
    remix: (workId: string, request: { clientMutationId: string; projectName: string }, signal?: AbortSignal) =>
      httpClient.post<{ project: AsterSceneProject; sourceWorkId: string }, typeof request>(
        `/community/asterscene/works/${encodeURIComponent(workId)}/remix`,
        request,
        undefined,
        signal
      ),
    report: (workId: string, request: { clientMutationId: string; detail?: string | null; reasonCode: string }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneModerationCase, typeof request>(
        `/community/asterscene/works/${encodeURIComponent(workId)}/report`,
        request,
        undefined,
        signal
      )
  },
  subscriptions: {
    plans: (signal?: AbortSignal) => httpClient.get<AsterSceneSubscriptionPlan[]>('/subscriptions/asterscene/plans', undefined, signal),
    current: (signal?: AbortSignal) => httpClient.get<AsterSceneSubscription>('/subscriptions/asterscene/current', undefined, signal),
    subscribe: (request: { clientMutationId: string; planCode: string }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneSubscription, typeof request>('/subscriptions/asterscene/current', request, undefined, signal),
    cancel: (request: { clientMutationId: string; reason?: string | null }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneSubscription, typeof request>('/subscriptions/asterscene/current/cancel', request, undefined, signal),
    markPaymentFailed: (request: { clientMutationId: string; reason?: string | null }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneSubscription, typeof request>('/subscriptions/asterscene/current/payment-failed', request, undefined, signal),
    expire: (request: { clientMutationId: string; reason?: string | null }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneSubscription, typeof request>('/subscriptions/asterscene/current/expire', request, undefined, signal)
  },
  usage: {
    summary: (signal?: AbortSignal) => httpClient.get<AsterSceneUsageSummary>('/usage/asterscene/summary', undefined, signal),
    ledger: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterSceneUsageLedgerEntry>>(`/usage/asterscene/ledger${buildQueryString(query)}`, undefined, signal)
  },
  admin: {
    moderationCases: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterSceneModerationCase>>(`/admin/asterscene/moderation/cases${buildQueryString(query)}`, undefined, signal),
    decide: (caseId: string, request: { clientMutationId?: string; decision: string; note?: string | null }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneModerationCase, typeof request>(
        `/admin/asterscene/moderation/cases/${encodeURIComponent(caseId)}/decision`,
        request,
        undefined,
        signal
      )
    ,
    appeals: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterSceneAppeal>>(`/admin/asterscene/moderation/appeals${buildQueryString(query)}`, undefined, signal),
    decideAppeal: (
      appealId: string,
      request: { clientMutationId: string; decision: 'Approve' | 'Reject'; note?: string | null },
      signal?: AbortSignal
    ) =>
      httpClient.post<AsterSceneAppeal, typeof request>(
        `/admin/asterscene/moderation/appeals/${encodeURIComponent(appealId)}/decision`,
        request,
        undefined,
        signal
      ),
    supportTickets: (query: AsterSceneGridQuery = {}, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AsterSceneSupportTicket>>(`/asterscene/support/admin/tickets${buildQueryString(query)}`, undefined, signal),
    supportTicket: (ticketId: string, signal?: AbortSignal) =>
      httpClient.get<AsterSceneSupportTicketDetail>(`/asterscene/support/admin/tickets/${encodeURIComponent(ticketId)}`, undefined, signal),
    addSupportComment: (ticketId: string, request: { clientMutationId: string; message: string }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneSupportTicketDetail, typeof request>(
        `/asterscene/support/admin/tickets/${encodeURIComponent(ticketId)}/comments`,
        request,
        undefined,
        signal
      ),
    changeSupportStatus: (
      ticketId: string,
      request: { clientMutationId: string; note: string; status: 'Open' | 'Closed' },
      signal?: AbortSignal
    ) =>
      httpClient.post<AsterSceneSupportTicketDetail, typeof request>(
        `/asterscene/support/admin/tickets/${encodeURIComponent(ticketId)}/status`,
        request,
        undefined,
        signal
      )
  },
  support: {
    create: (
      request: { clientMutationId: string; diagnostics: Record<string, unknown>; projectId: string; severity: string; title: string },
      signal?: AbortSignal
    ) => httpClient.post<AsterSceneSupportTicket, typeof request>('/asterscene/support/tickets', request, undefined, signal),
    detail: (ticketId: string, signal?: AbortSignal) =>
      httpClient.get<AsterSceneSupportTicketDetail>(`/asterscene/support/tickets/${encodeURIComponent(ticketId)}`, undefined, signal),
    comment: (ticketId: string, request: { clientMutationId: string; message: string }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneSupportTicketDetail, typeof request>(
        `/asterscene/support/tickets/${encodeURIComponent(ticketId)}/comments`,
        request,
        undefined,
        signal
      ),
    close: (ticketId: string, request: { clientMutationId: string; resolution: string }, signal?: AbortSignal) =>
      httpClient.post<AsterSceneSupportTicketDetail, typeof request>(
        `/asterscene/support/tickets/${encodeURIComponent(ticketId)}/close`,
        request,
        undefined,
        signal
      )
  }
};
