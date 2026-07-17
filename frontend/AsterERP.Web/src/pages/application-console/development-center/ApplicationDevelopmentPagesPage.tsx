import { Plus } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import {
  createApplicationDevelopmentModule,
  createApplicationDevelopmentPage,
  deleteApplicationDevelopmentModule,
  deleteApplicationDevelopmentPage,
  getApplicationDevelopmentPage,
  getApplicationDevelopmentWorkspace,
  publishApplicationDevelopmentPage,
  refreshApplicationDevelopmentPreviewMenu,
  updateApplicationDevelopmentModule,
  updateApplicationDevelopmentPage
} from '../../../api/application-development-center/applicationDevelopmentCenter.api';
import type {
  ApplicationDevelopmentModuleUpsertRequest,
  ApplicationDevelopmentPageDetail,
  ApplicationDevelopmentPageListItem,
  ApplicationDevelopmentPageUpsertRequest,
  ApplicationDevelopmentPublishResponse
} from '../../../api/application-development-center/applicationDevelopmentCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { FormRenderer } from '../../../shared/forms/FormRenderer';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { ApplicationConsolePageFrame } from '../ApplicationConsolePageFrame';

import { MenuTreeCrudPanel } from './MenuTreeCrudPanel';
import { PageBoard } from './PageBoard';
import { PageCardEditorDialog, type PageCardEditorValues } from './PageCardEditorDialog';
import { buildPageDraftRequest, createInitialPageForm, type CreatePageFormState } from './pageDraftDefaults';

const designerPermissions = {
  edit: 'app:development-center:designer:edit',
  preview: 'app:development-center:designer:preview',
  publish: 'app:development-center:designer:publish'
} as const;

export function ApplicationDevelopmentPagesPage() {
  const message = useMessage();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const { appCode, tenantId } = useParams();
  const [selectedVersionId, setSelectedVersionId] = useState('');
  const [selectedModuleId, setSelectedModuleId] = useState<string | null>(null);
  const [createPageOpen, setCreatePageOpen] = useState(false);
  const [createPageForm, setCreatePageForm] = useState<CreatePageFormState>(() => createInitialPageForm());
  const [editingPage, setEditingPage] = useState<ApplicationDevelopmentPageListItem | null>(null);

  const workspaceQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getApplicationDevelopmentWorkspace(selectedVersionId || null, signal).then((response) => response.data),
    queryKey: ['application-development-center', 'pages-workspace', selectedVersionId || 'default'],
    refetchOnMount: 'always',
    refetchOnReconnect: 'always',
    staleTimeMs: 0
  });
  const workspace = workspaceQuery.data;
  const activeVersionId = selectedVersionId || workspace?.selectedVersionId || '';
  const versions = workspace?.versions ?? [];
  const modules = useMemo(() => workspace?.modules ?? [], [workspace?.modules]);
  const pages = useMemo(() => workspace?.pages ?? [], [workspace?.pages]);

  useEffect(() => {
    if (!selectedVersionId && workspace?.selectedVersionId) setSelectedVersionId(workspace.selectedVersionId);
  }, [selectedVersionId, workspace?.selectedVersionId]);

  useEffect(() => {
    if (selectedModuleId && !modules.some((module) => module.id === selectedModuleId)) setSelectedModuleId(null);
  }, [modules, selectedModuleId]);

  const createPageFields = useMemo<FormFieldConfig<CreatePageFormState>[]>(
    () => [
      { label: translateCurrentLiteral('页面名称'), name: 'pageName', required: true, section: '页面信息', type: 'text' },
      { label: translateCurrentLiteral('页面编码'), name: 'pageCode', required: true, section: '页面信息', type: 'text' },
      {
        label: translateCurrentLiteral('页面类型'),
        name: 'pageType',
        options: [
          { label: translateCurrentLiteral('标准页面'), value: 'standard' },
          { label: translateCurrentLiteral('对话框'), value: 'dialog' },
          { label: translateCurrentLiteral('抽屉'), value: 'drawer' }
        ],
        section: '页面信息',
        type: 'select'
      },
      { label: translateCurrentLiteral('父页面'), name: 'parentPageId', options: [{ label: translateCurrentLiteral('无父页面'), value: '' }, ...pages.filter((page) => page.pageType === 'standard').map((page) => ({ label: `${page.pageName} / ${page.pageCode}`, value: page.id }))], section: '页面信息', type: 'select' }
    ],
    [pages]
  );

  const refreshWorkspace = async () => {
    await workspaceQuery.refetch();
  };

  const createModuleMutation = useApiMutation({
    mutationFn: (request: ApplicationDevelopmentModuleUpsertRequest) => createApplicationDevelopmentModule(request),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('创建应用菜单目录失败'))),
    onSuccess: async () => { message.success(translateCurrentLiteral('应用菜单目录已创建')); await refreshWorkspace(); }
  });
  const updateModuleMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: ApplicationDevelopmentModuleUpsertRequest }) => updateApplicationDevelopmentModule(id, request),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('更新应用菜单目录失败'))),
    onSuccess: async () => { message.success(translateCurrentLiteral('应用菜单目录已更新')); await refreshWorkspace(); }
  });
  const deleteModuleMutation = useApiMutation({
    mutationFn: (id: string) => deleteApplicationDevelopmentModule(id),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('删除应用菜单目录失败'))),
    onSuccess: async () => { setSelectedModuleId(null); message.success(translateCurrentLiteral('应用菜单目录已删除')); await refreshWorkspace(); }
  });
  const deletePageMutation = useApiMutation({
    mutationFn: (id: string) => deleteApplicationDevelopmentPage(id),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('删除页面失败'))),
    onSuccess: async () => { message.success(translateCurrentLiteral('页面已删除')); await refreshWorkspace(); }
  });
  const createPageMutation = useApiMutation({
    mutationFn: ({ form, moduleId, versionId }: { form: CreatePageFormState; moduleId: string | null; versionId: string }) => createApplicationDevelopmentPage(buildPageDraftRequest({ form, moduleId, versionId })),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('创建页面失败'))),
    onSuccess: async (response) => {
      setCreatePageOpen(false);
      message.success(translateCurrentLiteral('页面草稿已创建，即将进入最新 DesignerDocument 设计器'));
      await refreshWorkspace();
      openDesigner(response.data);
    }
  });
  const updatePageMutation = useApiMutation({
    mutationFn: ({ page, values }: { page: ApplicationDevelopmentPageListItem; values: PageCardEditorValues }) => updatePageMetadata(page, values),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('更新页面信息失败'))),
    onSuccess: async () => { setEditingPage(null); message.success(translateCurrentLiteral('页面信息已更新')); await refreshWorkspace(); }
  });
  const publishPageMutation = useApiMutation({
    mutationFn: (pageId: string) => publishApplicationDevelopmentPage(pageId),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('页面发布失败'))),
    onSuccess: async (response) => { reportPublishResult(response.data); await refreshWorkspace(); }
  });
  const refreshPreviewMutation = useApiMutation({
    mutationFn: (pageId: string) => refreshApplicationDevelopmentPreviewMenu(pageId),
    onError: (error) => message.error(getErrorMessage(error, translateCurrentLiteral('刷新预览菜单失败'))),
    onSuccess: async () => { message.success(translateCurrentLiteral('预览菜单已刷新')); await refreshWorkspace(); }
  });

  if (workspaceQuery.isLoading && !workspace) return <ApplicationConsolePageFrame density="compact" hideDescription pageKey="development-center">{() => <div className="grid h-full place-items-center text-sm text-slate-500">{translateCurrentLiteral('正在加载页面工作区…')}</div>}</ApplicationConsolePageFrame>;
  if (workspaceQuery.isError && !workspace) return <ApplicationConsolePageFrame density="compact" hideDescription pageKey="development-center">{() => <div className="grid h-full place-items-center text-sm text-rose-600">{getErrorMessage(workspaceQuery.error, translateCurrentLiteral('页面工作区加载失败'))}</div>}</ApplicationConsolePageFrame>;

  return <ApplicationConsolePageFrame density="compact" hideDescription pageKey="development-center">{() => <div className="flex min-h-0 flex-col gap-3">
    <section className="card shrink-0">
      <div className="card-header">
        <div><p className="card-kicker">{translateCurrentLiteral('页面工作区')}</p><h2>{translateCurrentLiteral('Application Development Pages')}</h2><p className="muted">{translateCurrentLiteral('页面元数据、DesignerDocument、预览与发布统一使用 Application Development Center 链路。')}</p></div>
        <div className="flex flex-wrap items-center gap-2">
          <select aria-label={translateCurrentLiteral('选择版本')} className="form-input h-8" value={activeVersionId} onChange={(event) => { setSelectedVersionId(event.target.value); setSelectedModuleId(null); }}>
            <option value="">{translateCurrentLiteral('选择版本')}</option>{versions.map((version) => <option key={version.id} value={version.id}>{version.versionName} / {version.versionCode}</option>)}
          </select>
          <PermissionButton code={designerPermissions.edit} className="primary-button h-8" disabled={!activeVersionId || createPageMutation.isPending} type="button" onClick={openCreatePage}><Plus className="h-3.5 w-3.5" />{translateCurrentLiteral('新建页面')}</PermissionButton>
        </div>
      </div>
      <p className="muted">{translateCurrentLiteral('当前版本：')} {workspace?.selectedVersion?.versionName ?? translateCurrentLiteral('未选择')} · {translateCurrentLiteral('页面数：')} {pages.length}</p>
    </section>
    <div className="grid min-h-0 flex-1 gap-3 xl:grid-cols-[260px_minmax(0,1fr)]">
      <MenuTreeCrudPanel deleting={deleteModuleMutation.isPending} disabled={!activeVersionId} modules={modules} saving={createModuleMutation.isPending || updateModuleMutation.isPending} selectedModuleId={selectedModuleId} selectedVersionId={activeVersionId} onCreate={(request) => createModuleMutation.mutate(request)} onDelete={(id) => deleteModuleMutation.mutate(id)} onSelect={setSelectedModuleId} onUpdate={(id, request) => updateModuleMutation.mutate({ id, request })} />
      <PageBoard deletingPageId={deletePageMutation.isPending ? deletePageMutation.variables ?? null : null} errorMessage={workspaceQuery.isError ? getErrorMessage(workspaceQuery.error, translateCurrentLiteral('页面列表加载失败')) : null} isLoading={workspaceQuery.isFetching && !workspace} modules={modules} pages={pages} publishingPageId={publishPageMutation.isPending ? publishPageMutation.variables ?? null : null} refreshingPageId={refreshPreviewMutation.isPending ? refreshPreviewMutation.variables ?? null : null} selectedModuleId={selectedModuleId} onCreatePage={openCreatePage} onDeletePage={requestDeletePage} onEditPage={setEditingPage} onOpenDesigner={openDesigner} onOpenPreview={openPreview} onPublish={(page) => publishPageMutation.mutate(page.id)} onRefreshPreview={(page) => refreshPreviewMutation.mutate(page.id)} />
    </div>
    <PageCardEditorDialog modules={modules} open={Boolean(editingPage)} page={editingPage} pages={pages} saving={updatePageMutation.isPending} onClose={() => setEditingPage(null)} onSubmit={(values) => { if (editingPage) updatePageMutation.mutate({ page: editingPage, values }); }} />
    <ResponsiveModal open={createPageOpen} title={translateCurrentLiteral('新建页面')} onClose={() => setCreatePageOpen(false)} footer={<><button className="secondary-button h-8" type="button" onClick={() => setCreatePageOpen(false)}>{translateCurrentLiteral('取消')}</button><PermissionButton code={designerPermissions.edit} className="primary-button h-8" disabled={createPageMutation.isPending || !activeVersionId} type="button" onClick={() => createPageMutation.mutate({ form: createPageForm, moduleId: selectedModuleId, versionId: activeVersionId })}>{createPageMutation.isPending ? translateCurrentLiteral('创建中…') : translateCurrentLiteral('创建并进入 Designer')}</PermissionButton></>}>
      <FormRenderer fields={createPageFields} value={createPageForm} onValueChange={(name, value) => setCreatePageForm((current) => ({ ...current, [name]: value }))} />
    </ResponsiveModal>
  </div>}</ApplicationConsolePageFrame>;

  function openCreatePage() {
    setCreatePageForm(createInitialPageForm());
    setCreatePageOpen(true);
  }

  function openDesigner(page: ApplicationDevelopmentPageListItem | ApplicationDevelopmentPageDetail) {
    if (!tenantId || !appCode) return;
    navigate(`/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/development-center/pages/${encodeURIComponent(page.id)}/designer`);
  }

  function openPreview(page: ApplicationDevelopmentPageListItem) {
    if (!tenantId || !appCode || !page.previewRoutePath) return;
    navigate(`/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin${page.previewRoutePath}`);
  }

  function requestDeletePage(page: ApplicationDevelopmentPageListItem) {
    confirm({
      title: translateCurrentLiteral('删除页面'),
      content: <span>{translateCurrentLiteral('确认删除页面“')}{page.pageName}{translateCurrentLiteral('”吗？删除后页面、预览入口和已发布入口都会被移除。')}</span>,
      confirmText: translateCurrentLiteral('删除'),
      onConfirm: async () => { await deletePageMutation.mutateAsync(page.id); }
    });
  }

  function reportPublishResult(result: ApplicationDevelopmentPublishResponse) {
    const artifact = result.publishedArtifactHash ? `${translateCurrentLiteral('artifact hash')} ${result.publishedArtifactHash}` : translateCurrentLiteral('最新 RuntimeArtifact');
    message.success(`${translateCurrentLiteral('页面已发布')}：${artifact}`);
  }
}

async function updatePageMetadata(page: ApplicationDevelopmentPageListItem, values: PageCardEditorValues) {
  const detail = (await getApplicationDevelopmentPage(page.id)).data;
  const request: ApplicationDevelopmentPageUpsertRequest = {
    designerMode: detail.designerMode || 'structured',
    documentJson: detail.documentJson,
    expectedUpdatedTime: detail.updatedTime ?? null,
    moduleId: values.moduleId || null,
    pageCode: values.pageCode.trim(),
    pageName: values.pageName.trim(),
    parentPageId: values.parentPageId || null,
    pageParameters: detail.pageParameters ?? [],
    pageType: detail.pageType,
    permissionConfigJson: detail.permissionConfigJson || '{}',
    sortOrder: Number(values.sortOrder) || 0,
    templateCode: detail.templateCode || 'designer-document',
    versionId: detail.versionId
  };
  return updateApplicationDevelopmentPage(page.id, request);
}
