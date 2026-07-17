import { Activity, ArrowLeft, Eye, Maximize2, Redo2, Save, Send, Undo2, ZoomIn, ZoomOut } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';

import { checkApplicationDevelopmentPageEnvironment, compileApplicationDevelopmentPreviewArtifact } from '../../../../../api/application-development-center/applicationDevelopmentCenter.api';
import type {
  ApplicationDevelopmentMenuOption,
  ApplicationDevelopmentEnvironmentDiagnostic,
  ApplicationDevelopmentPageDetail,
  ApplicationDevelopmentPermissionOptions,
  ApplicationDevelopmentPublishResponse,
  ApplicationDevelopmentRoleOption
} from '../../../../../api/application-development-center/applicationDevelopmentCenter.types';
import { sendApplicationMonitoringEvent } from '../../../../../api/application-development-center/applicationMonitoring.api';
import { getWorkflowProcessDefinitions } from '../../../../../api/workflow/workflows.api';
import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { useAuthStore } from '../../../../../core/state';
import { usePermissionStore } from '../../../../../core/state/permissionStore';
import { createRuntimeActionHandlers } from '../../../../../runtime-kernel/ActionHandlerRegistry';
import { parseRuntimePageArtifact, toRuntimeArtifact } from '../../../../../runtime-kernel/RuntimeArtifactCodec';
import { createRuntimeManifestRegistry, runtimeComponentRegistry } from '../../../../../runtime-kernel/RuntimeComponentRegistry';
import { RuntimeKernel } from '../../../../../runtime-kernel/RuntimeKernel';
import { PermissionButton } from '../../../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { ResourceExplorer } from '../binding/ResourceExplorer';
import { WorkflowBindingPanel } from '../binding/WorkflowBindingPanel';
import type { WorkflowDefinitionOption } from '../binding/workflowBindingTypes';
import { planCanvasMove } from '../canvas/canvasMovePlanner';
import { CanvasSettingsPopover } from '../canvas/CanvasSettingsPopover';
import { CanvasViewportController } from '../canvas/CanvasViewportController';
import { buildGeometry, DesignerCanvas } from '../canvas/DesignerCanvas';
import { createMoveNodesCommand } from '../commands/createDesignerCommands';
import type { DesignerCommandResult } from '../commands/DesignerCommand';
import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import { ComponentInsertPalette } from '../components/ComponentInsertPalette';
import { latestComponentRegistry } from '../components/latestComponentManifestCatalog';
import type { DesignerDocument } from '../document/DesignerDocument';
import { serializeDesignerDocument } from '../document/DesignerDocumentCodec';
import { canonicalizeDesignerContent } from '../document/DesignerDocumentHash';
import type { DesignerEditorSession } from '../document/DesignerEditorSession';
import { InspectorPanel } from '../inspector/InspectorPanel';
import { LayoutEditorToolbar } from '../layout/LayoutEditorToolbar';
import { applyDevicePreviewProfile, clearDevicePreview, DEFAULT_DEVICE_PROFILES, DEFAULT_RESPONSIVE_BREAKPOINTS } from '../responsive/deviceProfiles';
import { DesignerEditorSessionStore } from '../session/DesignerEditorSessionStore';

import { diagnosePageStudioDocument } from './PageStudioDiagnostics';
import { PageStudioDock, readStudioDockState, toStudioDockPreference, type StudioDockState } from './PageStudioDock';
import { PageStudioLayerTree } from './PageStudioLayerTree';

import './PageStudio.css';

export interface PageStudioPermissionState {
  allowAdd: boolean;
  allowDelete: boolean;
  allowEdit: boolean;
  allowExport: boolean;
  allowImport: boolean;
  menuCode: string;
  menuName: string;
  parentMenuCode: string;
  roleCodes: string[];
}

export interface PageStudioHostProps {
  appCode?: string | null;
  tenantId?: string | null;
  /** Seed loaded by the parent; the CommandBus owns all live document state after mount. */
  initialDocument: DesignerDocument;
  page: ApplicationDevelopmentPageDetail;
  pageSubtitle: string;
  pageTitle: string;
  permissionOptions: ApplicationDevelopmentPermissionOptions;
  permissionState: PageStudioPermissionState;
  publishResult: ApplicationDevelopmentPublishResponse | null;
  saving: boolean;
  publishing: boolean;
  refreshingPreview: boolean;
  onBack: () => void;
  onOpenPreview: () => void;
  onPermissionChange: (state: PageStudioPermissionState) => void;
  onPublish: () => Promise<ApplicationDevelopmentPublishResponse | undefined> | ApplicationDevelopmentPublishResponse | undefined;
  onRefreshPreviewMenu: () => void;
  onSave: (document: DesignerDocument) => Promise<void> | void;
  onWorkflowSync: (document: DesignerDocument) => Promise<void> | void;
}

export function PageStudioHost({ appCode, tenantId, initialDocument, page, pageSubtitle, pageTitle, permissionOptions, permissionState, publishResult, saving, publishing, refreshingPreview, onBack, onOpenPreview, onPermissionChange, onPublish, onRefreshPreviewMenu, onSave, onWorkflowSync }: PageStudioHostProps) {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const studioText = useCallback((key: string) => translate(`lowCode.pageStudio.${key}`), [translate]);
  const commandBusRef = useRef<{ documentId: string; value: DesignerCommandBus } | null>(null);
  const sessionStoreRef = useRef<{ documentId: string; value: DesignerEditorSessionStore } | null>(null);
  if (!commandBusRef.current || commandBusRef.current.documentId !== initialDocument.documentId) commandBusRef.current = {
    documentId: initialDocument.documentId,
    value: new DesignerCommandBus(initialDocument, {
      monitoringContext: {
        appCode: appCode ?? undefined,
        documentId: initialDocument.documentId,
        pageCode: page.pageCode,
        tenantId: tenantId ?? undefined
      }
    })
  };
  if (!sessionStoreRef.current || sessionStoreRef.current.documentId !== initialDocument.documentId) sessionStoreRef.current = { documentId: initialDocument.documentId, value: new DesignerEditorSessionStore(createSession(initialDocument)) };
  const commandBus = commandBusRef.current.value;
  const sessionStore = sessionStoreRef.current.value;
  const viewportController = useMemo(() => new CanvasViewportController(sessionStore), [sessionStore]);
  const currentUser = useAuthStore((state) => state.user);
  const previewUserId = currentUser?.userId ?? currentUser?.userName ?? '';
  const permissionCodes = usePermissionStore((state) => state.permissionCodes);
  const currentDocument = useSyncExternalStore(commandBus.subscribe.bind(commandBus), () => commandBus.document, () => commandBus.document);
  const session = useSyncExternalStore(sessionStore.subscribe.bind(sessionStore), () => sessionStore.getSnapshot(), () => sessionStore.getSnapshot());
  const dockStorageKey = `page-studio-dock:v2:${tenantId ?? 'default'}:${appCode ?? 'default'}:${page.id}`;
  const [dockState, setDockState] = useState<StudioDockState>(() => readStudioDockState(dockStorageKey));
  const [pageToolTab, setPageToolTab] = useState<'workflow' | 'publish'>('workflow');
  const [selectedResourceId, setSelectedResourceId] = useState<string | null>(null);
  const [savedDocumentContent, setSavedDocumentContent] = useState(() => canonicalizeDesignerContent(initialDocument));
  const savedDocumentIdRef = useRef(initialDocument.documentId);
  const dockStorageKeyRef = useRef(dockStorageKey);
  const saveWithMonitoringRef = useRef<() => Promise<void>>(async () => undefined);
  const effectiveBreakpointId = session.canvas.device?.breakpointId ?? null;
  const selectedDeviceId = session.canvas.device?.id ?? '';
  const selectedBreakpoint = DEFAULT_RESPONSIVE_BREAKPOINTS.find((breakpoint) => breakpoint.id === effectiveBreakpointId) ?? null;
  const previewManifests = useMemo(() => createRuntimeManifestRegistry(), []);
  const [previewKernel, setPreviewKernel] = useState<RuntimeKernel | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [environmentDiagnostics, setEnvironmentDiagnostics] = useState<ApplicationDevelopmentEnvironmentDiagnostic[]>([]);
  const [environmentCheckedContent, setEnvironmentCheckedContent] = useState<string | null>(null);
  const [checkingEnvironment, setCheckingEnvironment] = useState(false);
  const diagnostics = useMemo(() => diagnosePageStudioDocument(currentDocument, latestComponentRegistry), [currentDocument]);
  const hasErrors = diagnostics.some((diagnostic) => diagnostic.severity === 'error');
  const currentDocumentContent = useMemo(() => canonicalizeDesignerContent(currentDocument), [currentDocument]);
  const isDirty = currentDocumentContent !== savedDocumentContent;
  const environmentIsCurrent = environmentCheckedContent === currentDocumentContent;
  const environmentHasErrors = environmentIsCurrent && environmentDiagnostics.some((diagnostic) => diagnostic.severity === 'error');
  const canRunRuntime = !hasErrors && environmentIsCurrent && !environmentHasErrors && !checkingEnvironment;

  useEffect(() => {
    if (environmentCheckedContent && environmentCheckedContent !== currentDocumentContent) setEnvironmentCheckedContent(null);
  }, [currentDocumentContent, environmentCheckedContent]);

  const runEnvironmentCheck = useCallback(async (): Promise<boolean> => {
    setCheckingEnvironment(true);
    try {
      const response = await checkApplicationDevelopmentPageEnvironment(page.id, { documentJson: serializeDesignerDocument(currentDocument) });
      setEnvironmentDiagnostics(response.data.diagnostics);
      setEnvironmentCheckedContent(currentDocumentContent);
      if (!response.data.passed) message.error(response.data.diagnostics.find((item) => item.severity === 'error')?.message ?? '运行环境检测未通过。');
      else message.success('运行环境检测通过。');
      return response.data.passed;
    } catch (error) {
      const detail = error instanceof Error ? error.message : String(error);
      setEnvironmentDiagnostics([{ category: 'page', code: 'environment-check-unavailable', message: `运行环境检测未完成：${detail}`, severity: 'error' }]);
      setEnvironmentCheckedContent(currentDocumentContent);
      message.error('运行环境检测未完成，已阻断预览和发布。');
      return false;
    } finally { setCheckingEnvironment(false); }
  }, [currentDocument, currentDocumentContent, message, page.id]);

  useEffect(() => {
    if (savedDocumentIdRef.current === initialDocument.documentId) return;
    savedDocumentIdRef.current = initialDocument.documentId;
    setSavedDocumentContent(canonicalizeDesignerContent(initialDocument));
  }, [initialDocument]);

  useEffect(() => {
    if (dockStorageKeyRef.current !== dockStorageKey) {
      dockStorageKeyRef.current = dockStorageKey;
      setDockState(readStudioDockState(dockStorageKey));
      return;
    }
    window.localStorage.setItem(dockStorageKey, JSON.stringify(toStudioDockPreference(dockState)));
  }, [dockState, dockStorageKey]);

  useEffect(() => {
    if (!isDirty) return undefined;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', beforeUnload);
    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [isDirty]);

  const collapseOverlayDock = useCallback(() => {
    setDockState((current) => current.mode === 'overlay' ? { ...current, mode: 'collapsed' } : current);
  }, []);
  const clearSelection = useCallback(() => {
    sessionStore.patch({ anchorNodeId: null, primaryNodeId: null, selectedNodeIds: [], transactionId: null });
    collapseOverlayDock();
  }, [collapseOverlayDock, sessionStore]);
  const selectNode = useCallback((nodeId: string) => {
    sessionStore.patch({ anchorNodeId: nodeId, primaryNodeId: nodeId, selectedNodeIds: [nodeId], transactionId: null });
    collapseOverlayDock();
  }, [collapseOverlayDock, sessionStore]);
  const handleCommandResult = useCallback((result: DesignerCommandResult) => {
    if (!result.changed && result.diagnostics.length > 0) message.error(result.diagnostics.join('；'));
  }, [message]);

  useEffect(() => {
    let active = true;
    const controller = new AbortController();
    if (hasErrors) {
      setPreviewError(diagnostics.find((diagnostic) => diagnostic.severity === 'error')?.message ?? studioText('previewInvalid'));
      return () => controller.abort();
    }
    setPreviewError(null);
    const compileTimer = window.setTimeout(() => {
      void compileApplicationDevelopmentPreviewArtifact(page.id, { documentJson: serializeDesignerDocument(currentDocument) }, controller.signal)
        .then((response) => parseRuntimePageArtifact(response.data.artifactJson))
        .then((envelope) => {
          const artifact = envelope ? toRuntimeArtifact(envelope) : null;
          if (!artifact) throw new Error('Preview artifact contract is invalid.');
          return RuntimeKernel.create(artifact, {
            actionHandlers: createRuntimeActionHandlers(artifact),
            manifests: previewManifests,
            monitoringContext: { appCode: appCode ?? undefined, artifactHash: artifact.artifactHash, documentId: artifact.documentId, pageCode: page.pageCode, revision: artifact.revision, tenantId: tenantId ?? undefined, traceId: `designer-preview:${artifact.artifactHash}`, userId: previewUserId },
            permissions: { granted: new Set(permissionCodes), isSystemAdmin: false },
            resolveRenderer: (componentType) => runtimeComponentRegistry.has(componentType)
          });
        })
        .then((kernel) => {
          if (active) setPreviewKernel(kernel);
        })
        .catch((error) => {
          if (active && !(error instanceof DOMException && error.name === 'AbortError')) {
            setPreviewError(error instanceof Error ? error.message : String(error));
          }
        });
    }, 150);
    return () => { active = false; controller.abort(); window.clearTimeout(compileTimer); };
  }, [appCode, currentDocument, diagnostics, hasErrors, page.id, page.pageCode, permissionCodes, previewManifests, previewUserId, studioText, tenantId]);
  const definitionsQuery = useApiQuery({
    queryFn: ({ signal }) => getWorkflowProcessDefinitions(undefined, signal).then((response) => response.data),
    queryKey: ['workflows', 'process-definitions', 'latest-page-studio']
  });
  const selectedNode = session.primaryNodeId ? currentDocument.elements[session.primaryNodeId] : undefined;
  const selectedManifest = selectedNode ? latestComponentRegistry.get(selectedNode.type) : undefined;
  const componentInsertParentId = useMemo(() => {
    if (selectedNode && selectedManifest?.capability.acceptsChildren) return selectedNode.id;
    return selectedNode?.parentId ?? currentDocument.pages[0]?.rootElementId ?? null;
  }, [currentDocument.pages, selectedManifest, selectedNode]);
  const selectInsertedNode = (nodeId: string) => selectNode(nodeId);
  const saveWithMonitoring = async () => {
    const startedAt = performance.now();
    commandBus.endTransaction();
    const nextDocument = commandBus.document;
    try {
      await onSave(nextDocument);
      setSavedDocumentContent(canonicalizeDesignerContent(nextDocument));
      void sendApplicationMonitoringEvent({
        cancellationRequested: false,
        context: { appCode: appCode ?? undefined, commandId: nextDocument.documentId, commandType: 'save', documentId: nextDocument.documentId, revision: nextDocument.revision },
        durationMs: Math.max(0, performance.now() - startedAt),
        eventId: `${nextDocument.documentId}:save:${nextDocument.revision}`,
        eventName: 'designer.save',
        occurredAt: new Date().toISOString(),
        outcome: 'succeeded'
      }).catch(() => undefined);
    } catch (error) {
      void sendApplicationMonitoringEvent({
        cancellationRequested: false,
        context: { appCode: appCode ?? undefined, commandId: nextDocument.documentId, commandType: 'save', documentId: nextDocument.documentId, revision: nextDocument.revision },
        durationMs: Math.max(0, performance.now() - startedAt),
        errorCode: error instanceof Error ? error.name : 'saveFailed',
        eventId: `${nextDocument.documentId}:save:${nextDocument.revision}:failed`,
        eventName: 'designer.save',
        occurredAt: new Date().toISOString(),
        outcome: 'failed'
      }).catch(() => undefined);
    }
  };
  const requestBack = () => {
    if (!isDirty) {
      onBack();
      return;
    }
    confirm({
      cancelText: studioText('stayEditing'),
      confirmText: studioText('leaveWithoutSaving'),
      content: studioText('unsavedChangesDescription'),
      onConfirm: onBack,
      title: studioText('unsavedChanges')
    });
  };
  saveWithMonitoringRef.current = saveWithMonitoring;
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey)) return;
      if (event.key.toLowerCase() === 's') {
        event.preventDefault();
        if (!saving) void saveWithMonitoringRef.current();
        return;
      }
      if (event.key.toLowerCase() === 'z') {
        event.preventDefault();
        const result = event.shiftKey ? commandBus.redo() : commandBus.undo();
        if (result?.changed) return;
      }
      if (event.key.toLowerCase() === 'y') {
        event.preventDefault();
        commandBus.redo();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [commandBus, saving]);
  const publishWithMonitoring = async () => {
    if (!await runEnvironmentCheck()) return;
    const startedAt = performance.now();
    commandBus.endTransaction();
    try {
      const result = await onPublish();
      const artifactHash = result?.publishedArtifactHash ?? publishResult?.publishedArtifactHash;
      if (!artifactHash) return result;
      void sendApplicationMonitoringEvent({
        cancellationRequested: false,
        context: { appCode: appCode ?? undefined, artifactHash, documentId: currentDocument.documentId, revision: currentDocument.revision },
        durationMs: Math.max(0, performance.now() - startedAt),
        eventId: `${currentDocument.documentId}:publish:${artifactHash}`,
        eventName: 'designer.publish',
        occurredAt: new Date().toISOString(),
        outcome: 'succeeded'
      }).catch(() => undefined);
      return result;
    } catch (error) {
      void sendApplicationMonitoringEvent({
        cancellationRequested: false,
        context: { appCode: appCode ?? undefined, artifactHash: currentDocument.documentId, documentId: currentDocument.documentId, revision: currentDocument.revision },
        durationMs: Math.max(0, performance.now() - startedAt),
        errorCode: error instanceof Error ? error.name : 'publishFailed',
        eventId: `${currentDocument.documentId}:publish:failed:${Date.now()}`,
        eventName: 'designer.publish',
        occurredAt: new Date().toISOString(),
        outcome: 'failed'
      }).catch(() => undefined);
      throw error;
    }
  };

  const selectDeviceProfile = (profileId: string) => {
    if (!profileId) {
      const cleared = clearDevicePreview();
      sessionStore.patch({ canvas: { device: cleared.device }, viewport: { width: cleared.width, height: cleared.height } });
      return;
    }
    const preview = applyDevicePreviewProfile(profileId, DEFAULT_RESPONSIVE_BREAKPOINTS);
    if (!preview) return;
    sessionStore.patch({ canvas: { device: preview.device }, viewport: { width: preview.width, height: preview.height } });
    window.requestAnimationFrame(() => viewportController.fitWidth());
  };
  const selectResource = async (resource: { id: string; label: string }) => {
    setSelectedResourceId(resource.id);
    try {
      await navigator.clipboard?.writeText(resource.id);
      message.success(studioText('resourceReferenceCopied'));
    } catch {
      message.info(studioText('resourceSelected'));
    }
  };
  const moveLayerNode = (nodeId: string, parentId: string, index: number) => {
    const rootId = currentDocument.pages.find((candidate) => Boolean(currentDocument.elements[candidate.rootElementId]))?.rootElementId;
    if (!rootId) return;
    const geometry = buildGeometry(
      currentDocument,
      rootId,
      { height: session.viewport.height, width: session.viewport.width, x: 0, y: 0 },
      selectedBreakpoint,
      DEFAULT_RESPONSIVE_BREAKPOINTS
    );
    const rect = geometry.rects[nodeId];
    if (!rect) return;
    const planned = planCanvasMove({
      breakpointId: selectedBreakpoint?.id,
      document: currentDocument,
      geometry,
      nodeIds: [nodeId],
      rects: [rect],
      target: { index, parentId, placement: 'inside', targetNodeId: parentId }
    });
    if (!planned.ok) {
      message.error(planned.diagnostics.join('；'));
      return;
    }
    if (!planned.changed) return;
    const result = commandBus.execute(createMoveNodesCommand(planned.plan));
    handleCommandResult(result);
    if (result.changed) selectNode(nodeId);
  };
  const openPageSettings = () => setDockState((current) => ({
    ...current,
    activeTool: 'page',
    mode: current.mode === 'pinned' ? 'pinned' : 'overlay'
  }));
  const runFitAction = (action: string) => {
    if (action === 'width') viewportController.fitWidth();
    else if (action === 'selection') viewportController.fitSelection();
    else viewportController.fitPage();
  };
  const dockContent = dockState.activeTool === 'components'
    ? <ComponentInsertPalette commandBus={commandBus} document={currentDocument} manifests={latestComponentRegistry} onCommandResult={handleCommandResult} onNodeInserted={selectInsertedNode} targetParentId={componentInsertParentId} />
    : dockState.activeTool === 'layers'
      ? <PageStudioLayerTree document={currentDocument} manifests={latestComponentRegistry} onMove={moveLayerNode} onOpenPageSettings={openPageSettings} onSelect={selectNode} selectedNodeIds={session.selectedNodeIds} />
      : dockState.activeTool === 'resources'
        ? <div className="space-y-2 p-3"><ResourceExplorer document={currentDocument} onSelect={selectResource} />{selectedResourceId ? <p className="page-studio__resource-selection">{studioText('resourceReferenceCopied')}: <span className="font-mono">{selectedResourceId}</span></p> : null}</div>
        : <section className="page-studio__page-tools"><nav aria-label={studioText('pageTools')} className="page-studio__page-tabs" role="tablist"><button aria-controls="page-studio-workflow-panel" aria-selected={pageToolTab === 'workflow'} className={`page-studio__page-tab ${pageToolTab === 'workflow' ? 'is-active' : ''}`} role="tab" type="button" onClick={() => setPageToolTab('workflow')}>{studioText('workflow')}</button><button aria-controls="page-studio-publish-panel" aria-selected={pageToolTab === 'publish'} className={`page-studio__page-tab ${pageToolTab === 'publish' ? 'is-active' : ''}`} role="tab" type="button" onClick={() => setPageToolTab('publish')}>{studioText('publish')}</button></nav><div aria-label={studioText(pageToolTab)} id={`page-studio-${pageToolTab}-panel`} role="tabpanel">{pageToolTab === 'workflow' ? <WorkflowBindingPanel commandBus={commandBus} definitions={toWorkflowDefinitionOptions(definitionsQuery.data ?? [])} document={currentDocument} page={{ appCode, businessType: page.pageCode, keyField: 'id', menuCode: page.publishedMenuCode, pageCode: page.pageCode, pageName: page.pageName, tenantId }} onSave={(_draft, nextDocument) => onWorkflowSync(nextDocument)} /> : <PublishPanel diagnostics={diagnostics} page={page} permissionOptions={permissionOptions} permissionState={permissionState} publishResult={publishResult} refreshingPreview={refreshingPreview} onOpenPreview={onOpenPreview} onPermissionChange={onPermissionChange} onRefreshPreviewMenu={onRefreshPreviewMenu} />}</div></section>;

  return (
    <div className="page-studio">
      <header className="page-studio__toolbar" data-canvas-interaction-control="true">
        <div className="page-studio__identity">
          <button aria-label={studioText('backToPages')} className="secondary-button h-8" type="button" onClick={requestBack}><ArrowLeft aria-hidden="true" className="h-4 w-4" /></button>
          <div className="min-w-0"><h1 className="page-studio__title">{pageTitle}</h1><p className="page-studio__subtitle">{page.pageCode} / {pageSubtitle} / <span className={`page-studio__save-status ${isDirty ? 'is-dirty' : 'is-saved'}`}>{studioText(isDirty ? 'unsavedChanges' : 'saved')}</span></p></div>
        </div>
        <div className="page-studio__toolbar-actions">
          <button aria-label={studioText('undo')} className="icon-button h-8 w-8" disabled={!commandBus.canUndo} title={studioText('undo')} type="button" onClick={() => commandBus.undo()}><Undo2 aria-hidden="true" className="h-4 w-4" /></button>
          <button aria-label={studioText('redo')} className="icon-button h-8 w-8" disabled={!commandBus.canRedo} title={studioText('redo')} type="button" onClick={() => commandBus.redo()}><Redo2 aria-hidden="true" className="h-4 w-4" /></button>
          <div className="page-studio__zoom-group">
            <button aria-label={studioText('zoomOut')} className="page-studio__toolbar-control page-studio__toolbar-control--icon" type="button" onClick={() => viewportController.zoomBy(-0.1)}><ZoomOut aria-hidden="true" className="h-4 w-4" /></button>
            <select aria-label={studioText('zoomPresets')} className="page-studio__zoom-select" value={Math.round(session.viewport.zoom * 100)} onChange={(event) => viewportController.setZoom(Number(event.target.value) / 100)}>
              {![50, 75, 100, 125, 150, 200].includes(Math.round(session.viewport.zoom * 100)) ? <option value={Math.round(session.viewport.zoom * 100)}>{Math.round(session.viewport.zoom * 100)}%</option> : null}
              <option value="50">50%</option>
              <option value="75">75%</option>
              <option value="100">100%</option>
              <option value="125">125%</option>
              <option value="150">150%</option>
              <option value="200">200%</option>
            </select>
            <button aria-label={studioText('zoomIn')} className="page-studio__toolbar-control page-studio__toolbar-control--icon" type="button" onClick={() => viewportController.zoomBy(0.1)}><ZoomIn aria-hidden="true" className="h-4 w-4" /></button>
          </div>
          <label className="page-studio__fit-control"><Maximize2 aria-hidden="true" className="h-4 w-4" /><span className="sr-only">{studioText('fitOptions')}</span><select aria-label={studioText('fitOptions')} value="" onChange={(event) => runFitAction(event.target.value)}><option value="" disabled>{studioText('fitCanvas')}</option><option value="width">{studioText('fitWidth')}</option><option value="page">{studioText('fitPage')}</option><option value="selection">{studioText('fitSelection')}</option></select></label>
          <label className="page-studio__breakpoint-control"><span>{studioText('devicePreview')}</span><select aria-label={studioText('devicePreview')} className="form-input h-8" value={selectedDeviceId} onChange={(event) => selectDeviceProfile(event.target.value)}><option value="">{studioText('editorCanvas')}</option><optgroup label={studioText('mobile')}>{DEFAULT_DEVICE_PROFILES.filter((profile) => profile.width < 768).map((profile) => <option key={profile.id} value={profile.id}>{profile.name} · {profile.width}×{profile.height}</option>)}</optgroup><optgroup label={studioText('tablet')}>{DEFAULT_DEVICE_PROFILES.filter((profile) => profile.width >= 768 && profile.width < 1200).map((profile) => <option key={profile.id} value={profile.id}>{profile.name} · {profile.width}×{profile.height}</option>)}</optgroup><optgroup label={studioText('desktop')}>{DEFAULT_DEVICE_PROFILES.filter((profile) => profile.width >= 1200).map((profile) => <option key={profile.id} value={profile.id}>{profile.name} · {profile.width}×{profile.height}</option>)}</optgroup></select></label>
          <CanvasSettingsPopover session={session} sessionStore={sessionStore} />
          <button aria-label="环境检测" className="secondary-button h-8" type="button" disabled={checkingEnvironment || hasErrors} onClick={() => void runEnvironmentCheck()}><Activity aria-hidden="true" className="h-4 w-4" />{checkingEnvironment ? '检测中' : '环境检测'}{environmentIsCurrent && environmentDiagnostics.length > 0 ? ` ${environmentDiagnostics.length}` : ''}</button>
          <button aria-label={studioText('preview')} className="secondary-button h-8" type="button" disabled={!page.previewRoutePath || !canRunRuntime} onClick={onOpenPreview}><Eye aria-hidden="true" className="h-4 w-4" />{studioText('preview')}</button>
          <PermissionButton aria-label={studioText(saving ? 'saving' : 'saveDraft')} className="secondary-button h-8" code="app:development-center:designer:edit" type="button" disabled={saving} onClick={saveWithMonitoring}><Save aria-hidden="true" className="h-4 w-4" />{studioText(saving ? 'saving' : 'saveDraft')}</PermissionButton>
          <PermissionButton aria-label={studioText('publish')} className="primary-button h-8" code="app:development-center:designer:publish" type="button" disabled={hasErrors || publishing || checkingEnvironment} onClick={publishWithMonitoring}><Send aria-hidden="true" className="h-4 w-4" />{studioText('publish')}</PermissionButton>
        </div>
      </header>
      {hasErrors || environmentHasErrors ? <div className="page-studio__error-banner" role="alert">{[...diagnostics.filter((diagnostic) => diagnostic.severity === 'error').map((diagnostic) => `${diagnostic.path ?? diagnostic.elementId ?? 'document'}: ${diagnostic.message}`), ...environmentDiagnostics.filter((diagnostic) => diagnostic.severity === 'error').map((diagnostic) => `${diagnostic.path ?? diagnostic.category}: ${diagnostic.message}`)].join('；')}</div> : null}
      <div className="page-studio__workspace">
        <PageStudioDock state={dockState} onChange={setDockState}>{dockContent}</PageStudioDock>
        <div className="page-studio__canvas-column">
          {session.selectedNodeIds.length > 0 ? <LayoutEditorToolbar commandBus={commandBus} document={currentDocument} selectedNodeIds={session.selectedNodeIds} studioText={studioText} /> : null}
          <DesignerCanvas bindingDocument={currentDocument} canvasText={{ previewInvalid: studioText('previewInvalid'), unknownComponent: studioText('unknownComponent'), previewPreparing: studioText('preparingPreview') }} commandBus={commandBus} manifests={latestComponentRegistry} onCanvasBlank={clearSelection} onCommandResult={handleCommandResult} onNodeInserted={selectInsertedNode} onNodeSelected={collapseOverlayDock} previewError={previewError} previewKernel={previewKernel} previewLayoutContext={{ breakpoint: selectedBreakpoint?.id }} previewState={{ scopes: {}, variables: {} }} responsiveBreakpoint={selectedBreakpoint} responsiveBreakpoints={DEFAULT_RESPONSIVE_BREAKPOINTS} sessionStore={sessionStore} studioText={studioText} viewportController={viewportController} />
        </div>
        {session.selectedNodeIds.length > 0 ? <InspectorPanel bindingDocument={currentDocument} commandBus={commandBus} document={currentDocument} manifest={selectedManifest} manifests={latestComponentRegistry} responsiveBreakpoints={DEFAULT_RESPONSIVE_BREAKPOINTS} selectedBreakpoint={selectedBreakpoint} selectedNodeIds={session.selectedNodeIds} /> : null}
      </div>
    </div>
  );

}

function PublishPanel({ diagnostics, page, permissionOptions, permissionState, publishResult, refreshingPreview, onOpenPreview, onPermissionChange, onRefreshPreviewMenu }: { diagnostics: ReturnType<typeof diagnosePageStudioDocument>; page: ApplicationDevelopmentPageDetail; permissionOptions: ApplicationDevelopmentPermissionOptions; permissionState: PageStudioPermissionState; publishResult: ApplicationDevelopmentPublishResponse | null; refreshingPreview: boolean; onOpenPreview: () => void; onPermissionChange: (state: PageStudioPermissionState) => void; onRefreshPreviewMenu: () => void }) {
  const { translate } = useI18n();
  const studioText = (key: string) => translate(`lowCode.pageStudio.${key}`);
  return <section aria-label={studioText('publish')} className="page-studio__publish-panel"><div><p className="page-studio__eyebrow">{studioText('publish')}</p><h2 className="page-studio__panel-heading">{studioText('latestRuntimeChain')}</h2></div><div className="space-y-2">{diagnostics.length === 0 ? <p className="page-studio__diagnostic page-studio__diagnostic--success" role="status">{studioText('checksPassed')}</p> : diagnostics.map((diagnostic) => <p key={`${diagnostic.code}:${diagnostic.elementId ?? ''}`} className={`page-studio__diagnostic page-studio__diagnostic--${diagnostic.severity}`} role={diagnostic.severity === 'error' ? 'alert' : 'status'}>{diagnostic.elementId ? `${diagnostic.elementId}: ` : ''}{diagnostic.message}</p>)}</div><PermissionEditor options={permissionOptions} state={permissionState} onChange={onPermissionChange} /><div className="grid grid-cols-2 gap-2"><button aria-label={studioText('openPreview')} className="secondary-button h-9 justify-center text-xs" disabled={!page.previewRoutePath} type="button" onClick={onOpenPreview}>{studioText('openPreview')}</button><PermissionButton aria-label={studioText('refreshMenu')} className="secondary-button h-9 justify-center text-xs" code="app:development-center:designer:preview" disabled={refreshingPreview} type="button" onClick={onRefreshPreviewMenu}>{studioText('refreshMenu')}</PermissionButton></div>{publishResult ? <div aria-live="polite" className="page-studio__publish-result"><div>{studioText('artifact')}: {publishResult.publishedArtifactId ?? studioText('artifact')}</div><div>{studioText('menu')}: {publishResult.publishedMenuCode ?? '-'}</div><div>{studioText('route')}: {publishResult.publishedRoutePath ?? '-'}</div></div> : null}</section>;
}

function PermissionEditor({ options, state, onChange }: { options: ApplicationDevelopmentPermissionOptions; state: PageStudioPermissionState; onChange: (state: PageStudioPermissionState) => void }) {
  const { translate } = useI18n();
  const studioText = (key: string) => translate(`lowCode.pageStudio.${key}`);
  const toggle = (key: keyof Pick<PageStudioPermissionState, 'allowAdd' | 'allowEdit' | 'allowDelete' | 'allowImport' | 'allowExport'>) => onChange({ ...state, [key]: !state[key] });
  return <fieldset aria-label={studioText('permissions')} className="page-studio__permission-editor"><legend>{studioText('permissions')}</legend><label className="sr-only" htmlFor="designer-menu-code">{studioText('menuCode')}</label><input id="designer-menu-code" aria-label={studioText('menuCode')} className="form-input h-8" placeholder={studioText('menuCode')} value={state.menuCode} onChange={(event) => onChange({ ...state, menuCode: event.target.value })} /><label className="sr-only" htmlFor="designer-menu-name">{studioText('menuName')}</label><input id="designer-menu-name" aria-label={studioText('menuName')} className="form-input h-8" placeholder={studioText('menuName')} value={state.menuName} onChange={(event) => onChange({ ...state, menuName: event.target.value })} /><label className="sr-only" htmlFor="designer-parent-menu">{studioText('parentMenu')}</label><select id="designer-parent-menu" aria-label={studioText('parentMenu')} className="form-input h-8" value={state.parentMenuCode} onChange={(event) => onChange({ ...state, parentMenuCode: event.target.value })}><option value="">{studioText('selectParentMenu')}</option>{options.menuOptions.map((item: ApplicationDevelopmentMenuOption) => <option key={item.menuCode} value={item.menuCode}>{item.menuName} / {item.menuCode}</option>)}</select><div aria-label={studioText('actionPermissions')} className="grid grid-cols-2 gap-1 text-xs">{([['allowAdd', 'add'], ['allowEdit', 'edit'], ['allowDelete', 'delete'], ['allowImport', 'import'], ['allowExport', 'export']] as const).map(([key, label]) => <label key={key} className="flex items-center gap-1"><input aria-label={studioText(label)} checked={state[key]} type="checkbox" onChange={() => toggle(key)} />{studioText(label)}</label>)}</div><div aria-label={studioText('roles')} className="max-h-24 overflow-y-auto text-xs">{options.roleOptions.map((role: ApplicationDevelopmentRoleOption) => <label key={role.roleCode} className="flex items-center gap-1"><input aria-label={role.roleName} checked={state.roleCodes.includes(role.roleCode)} type="checkbox" onChange={(event) => onChange({ ...state, roleCodes: event.target.checked ? [...state.roleCodes, role.roleCode] : state.roleCodes.filter((code) => code !== role.roleCode) })} />{role.roleName}</label>)}</div></fieldset>;
}

function createSession(document: DesignerDocument): DesignerEditorSession { return { anchorNodeId: null, canvas: { device: null, gridSize: 8, gridVisible: true, guides: [], minimapVisible: true, rulersVisible: false, snapThreshold: 6, tool: 'select' }, documentId: document.documentId, panelState: { properties: true, publish: false }, primaryNodeId: null, selectedNodeIds: [], sessionId: `${document.documentId}:page-studio`, transactionId: null, viewport: { height: 720, pan: { x: 0, y: 0 }, width: 1280, zoom: 1 } }; }

function toWorkflowDefinitionOptions(definitions: readonly { id: string; key?: string | null; name?: string | null; version: number }[]): WorkflowDefinitionOption[] { return definitions.map((definition) => ({ id: definition.id, key: definition.key ?? definition.id, name: definition.name ?? definition.key ?? definition.id, version: definition.version })); }
