import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import {
  getApplicationDevelopmentPage,
  getApplicationDevelopmentPermissionOptions,
  listApplicationDevelopmentVersions,
  publishApplicationDevelopmentPage,
  refreshApplicationDevelopmentPreviewMenu,
  updateApplicationDevelopmentPage
} from '../../../api/application-development-center/applicationDevelopmentCenter.api';
import type { ApplicationDevelopmentPageDetail, ApplicationDevelopmentPageUpsertRequest, ApplicationDevelopmentPublishResponse } from '../../../api/application-development-center/applicationDevelopmentCenter.types';
import { isApplicationDevelopmentDraftConflictCode } from '../../../api/application-development-center/applicationDevelopmentCenter.types';
import { saveWorkflowBinding, type WorkflowBindingUpsertRequest } from '../../../api/workflow/workflows.api';
import { isHttpError } from '../../../core/http/httpError';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useMessage } from '../../../shared/feedback/useMessage';
import { PageError } from '../../../shared/status/PageError';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { ApplicationConsolePageFrame } from '../ApplicationConsolePageFrame';

import type { DesignerDocument } from './low-code-studio/document/DesignerDocument';
import { parseDesignerDocument, serializeDesignerDocument } from './low-code-studio/document/DesignerDocumentCodec';
import { PageStudioHost } from './low-code-studio/page-studio/PageStudioHost';

const emptyPermissionState = { allowAdd: true, allowDelete: true, allowEdit: true, allowExport: true, allowImport: true, menuCode: '', menuName: '', parentMenuCode: 'dev-center', roleCodes: [] as string[] };

export function DevelopmentCenterDesignerPage() {
  const navigate = useNavigate();
  const message = useMessage();
  const queryClient = useQueryClient();
  const { appCode, pageId = '', tenantId } = useParams();
  const [document, setDocument] = useState<DesignerDocument | null>(null);
  const [documentKey, setDocumentKey] = useState('');
  const [page, setPage] = useState<ApplicationDevelopmentPageDetail | null>(null);
  const [permissionState, setPermissionState] = useState(emptyPermissionState);
  const [publishResult, setPublishResult] = useState<ApplicationDevelopmentPublishResponse | null>(null);
  const sourceKey = useRef('');
  const queryKey = useMemo(() => ['application-development-center', 'designer-page', pageId] as const, [pageId]);
  const versionsQuery = useApiQuery({ queryFn: ({ signal }) => listApplicationDevelopmentVersions(signal).then((response) => response.data), queryKey: ['application-development-center', 'versions'] });
  const detailQuery = useApiQuery({ enabled: Boolean(pageId), queryFn: ({ signal }) => getApplicationDevelopmentPage(pageId, signal).then((response) => response.data), queryKey, refetchOnMount: false, refetchOnReconnect: false, retry: false, staleTimeMs: 30_000 });
  const permissionOptionsQuery = useApiQuery({ queryFn: ({ signal }) => getApplicationDevelopmentPermissionOptions(signal).then((response) => response.data), queryKey: ['application-development-center', 'permission-options'] });

  useEffect(() => { sourceKey.current = ''; setDocument(null); setPage(null); setPublishResult(null); setPermissionState(emptyPermissionState); }, [pageId]);
  useEffect(() => {
    const detail = detailQuery.data;
    if (!detail) return;
    const nextKey = `${detail.id}:${detail.updatedTime ?? ''}:${hashString(detail.documentJson)}`;
    setPage(detail);
    setPermissionState(parsePermissionState(detail.permissionConfigJson));
    if (sourceKey.current === nextKey) return;
    const nextDocument = parseDesignerDocument(detail.documentJson, { pageCode: detail.pageCode, pageName: detail.pageName, pageType: detail.pageType });
    sourceKey.current = nextKey;
    setDocument(nextDocument);
    setDocumentKey(nextKey);
  }, [detailQuery.data, detailQuery.dataUpdatedAt]);

  const saveMutation = useApiMutation({
    mutationFn: (request: ApplicationDevelopmentPageUpsertRequest) => updateApplicationDevelopmentPage(pageId, request),
    onError: (error) => message.error(isDraftConflict(error) ? translateCurrentLiteral('页面已被其他人修改，请刷新后重新编辑。') : getErrorMessage(error, translateCurrentLiteral('保存设计器页面失败'))),
    onSuccess: async () => { message.success(translateCurrentLiteral('页面设计已保存')); await detailQuery.refetch(); }
  });
  const refreshMutation = useApiMutation({ mutationFn: () => refreshApplicationDevelopmentPreviewMenu(pageId), onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('刷新预览菜单失败'))), onSuccess: async () => { message.success(translateCurrentLiteral('预览菜单已刷新')); await detailQuery.refetch(); } });
  const publishMutation = useApiMutation({
    mutationFn: () => publishApplicationDevelopmentPage(pageId),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('发布失败'))),
    onSuccess: async (response) => {
      if (!page) return;
      setPublishResult(response.data);
      message.success(translateCurrentLiteral('页面已发布'));
      await invalidateRuntimeCaches(page);
      const refreshed = (await detailQuery.refetch()).data ?? page;
      const refreshedDocument = parseDesignerDocument(refreshed.documentJson, { pageCode: refreshed.pageCode, pageName: refreshed.pageName, pageType: refreshed.pageType });
      await syncWorkflowBinding(refreshedDocument, refreshed);
    }
  });

  if (detailQuery.isError) {
    const notFound = isHttpError(detailQuery.error) && detailQuery.error.status === 404;
    return <ApplicationConsolePageFrame density="compact" hideDescription pageKey="development-center" surface="ide">{() => <PageError action={<div className="flex flex-wrap justify-center gap-2"><button className="rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50" onClick={returnToWorkspace} type="button">{translateCurrentLiteral('返回页面列表')}</button><button className="rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50" onClick={() => void detailQuery.refetch()} type="button">{translateCurrentLiteral('重试')}</button></div>} description={translateCurrentLiteral(notFound ? '页面不存在或已被删除，请返回页面列表重新选择。' : '页面数据暂时无法加载，请重试或返回页面列表。')} />}</ApplicationConsolePageFrame>;
  }
  if (!page || !document) return <ApplicationConsolePageFrame density="compact" hideDescription pageKey="development-center" surface="ide">{() => <div className="grid h-full place-items-center text-sm text-slate-500">{translateCurrentLiteral('正在加载 Page Studio…')}</div>}</ApplicationConsolePageFrame>;
  const selectedVersion = versionsQuery.data?.find((version) => version.id === page.versionId) ?? null;
  return <ApplicationConsolePageFrame density="compact" hideDescription pageKey="development-center" surface="ide">{() => <main className="h-full min-h-0 overflow-hidden"><PageStudioHost key={documentKey} appCode={appCode} tenantId={tenantId} initialDocument={document} page={page} pageSubtitle={`${selectedVersion?.versionName ?? page.status} / ${page.status}`} pageTitle={page.pageName || 'Page Studio'} permissionOptions={permissionOptionsQuery.data ?? { menuOptions: [], roleOptions: [] }} permissionState={permissionState} publishResult={publishResult} saving={saveMutation.isPending} publishing={publishMutation.isPending} refreshingPreview={refreshMutation.isPending} onBack={returnToWorkspace} onOpenPreview={openPreview} onPermissionChange={setPermissionState} onPublish={() => publishMutation.mutateAsync().then((response) => response.data)} onRefreshPreviewMenu={() => refreshMutation.mutate()} onSave={saveDocument} onWorkflowSync={(nextDocument) => syncWorkflowBinding(nextDocument, page)} /></main>}</ApplicationConsolePageFrame>;

  function saveDocument(nextDocument: DesignerDocument): Promise<void> {
    if (!page) return Promise.resolve();
    const activePage = page;
    const request: ApplicationDevelopmentPageUpsertRequest = { designerMode: 'structured', expectedUpdatedTime: activePage.updatedTime ?? null, documentJson: serializeDesignerDocument(nextDocument), moduleId: activePage.moduleId ?? undefined, pageCode: activePage.pageCode, pageName: activePage.pageName, pageParameters: activePage.pageParameters ?? [], pageType: activePage.pageType ?? 'standard', permissionConfigJson: JSON.stringify(permissionState), sortOrder: activePage.sortOrder, templateCode: activePage.templateCode || 'designer-document', versionId: activePage.versionId };
    return saveMutation.mutateAsync(request).then(() => undefined);
  }
  function returnToWorkspace() { if (!tenantId || !appCode) { navigate(-1); return; } navigate(`/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/development-center/pages`); }
  function openPreview() { const activePage = page; if (!activePage?.previewRoutePath || !tenantId || !appCode) return; navigate(`/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin${activePage.previewRoutePath}`); }
  async function invalidateRuntimeCaches(detail: ApplicationDevelopmentPageDetail | null) {
    if (!detail?.pageCode) return;
    const scopedKeys = [
      queryKeys.runtime.pageSchemaScoped(tenantId ?? '', appCode ?? '', detail.pageCode, ''),
      queryKeys.runtime.pageSchemaScoped(tenantId ?? '', appCode ?? '', detail.pageCode, pageId)
    ];
    await Promise.all([
      ...scopedKeys.map((queryKey) => queryClient.invalidateQueries({ exact: true, queryKey })),
      queryClient.invalidateQueries({ exact: true, queryKey: queryKeys.runtime.gridView(detail.pageCode, '') }),
      queryClient.invalidateQueries({ exact: true, queryKey: queryKeys.runtime.gridView(detail.pageCode, pageId) })
    ]);
  }
  async function syncWorkflowBinding(nextDocument: DesignerDocument, detail: ApplicationDevelopmentPageDetail) {
    const binding = nextDocument.workflowBindings.find((item) => item.type === 'workflow');
    if (!binding) return;
    const config = isRecord(binding.config) ? binding.config : {};
    if (config.enabled === false) return;
    const tenant = typeof config.tenantId === 'string' && config.tenantId.trim() ? config.tenantId.trim() : tenantId?.trim() ?? '';
    const application = typeof config.appCode === 'string' && config.appCode.trim() ? config.appCode.trim() : appCode?.trim() ?? '';
    const processDefinitionKey = textValue(config.processDefinitionKey);
    const processDefinitionId = textValue(config.processDefinitionId) || textValue(binding.targetId);
    if (!tenant || !application || !detail.publishedMenuCode || !processDefinitionKey || !processDefinitionId) {
      message.error(translateCurrentLiteral('审批流绑定缺少租户、应用、正式菜单或审批流版本，未执行同步。'));
      return;
    }
    const request: WorkflowBindingUpsertRequest = { appCode: application, businessType: textValue(config.businessType) || detail.pageCode, callbackConfig: null, detailRoute: detail.publishedRoutePath ?? `/pages/${detail.pageCode}`, formResourceCode: null, isEnabled: true, keyField: textValue(config.keyField) || 'id', menuCode: detail.publishedMenuCode, modelCode: textValue(config.businessType) || detail.pageCode, modelId: null, modelKey: textValue(config.modelKey) || null, pageCode: detail.pageCode, processDefinitionId, processDefinitionKey, remark: '由最新 Page Studio 发布同步', startFormJson: null, tenantId: tenant, titleTemplate: textValue(config.titleTemplate) || `${detail.pageName}审批` };
    try { await saveWorkflowBinding(request); await queryClient.invalidateQueries({ queryKey: ['workflows', 'bindings'] }); message.success(translateCurrentLiteral('审批流绑定已同步')); } catch (error) { message.error(getErrorMessage(error, translateCurrentLiteral('审批流绑定同步失败'))); }
  }
}

function isDraftConflict(error: unknown): boolean { return isHttpError(error) && isApplicationDevelopmentDraftConflictCode(error.code, error.status); }
function parsePermissionState(json: string) { try { const value = JSON.parse(json) as Partial<typeof emptyPermissionState>; return { ...emptyPermissionState, ...value, roleCodes: Array.isArray(value.roleCodes) ? value.roleCodes : [] }; } catch { return emptyPermissionState; } }
function hashString(value: string): string { let hash = 5381; for (let index = 0; index < value.length; index += 1) hash = ((hash << 5) + hash) ^ value.charCodeAt(index); return (hash >>> 0).toString(36); }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
function textValue(value: unknown): string { return typeof value === 'string' ? value.trim() : ''; }
