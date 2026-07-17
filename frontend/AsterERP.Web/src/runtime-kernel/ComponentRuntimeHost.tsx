import type { ReactNode } from 'react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { queryRuntimeModel } from '../api/runtime/runtime.api';
import type { RuntimeArtifact } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';
import type { RuntimeExpressionScope } from '../shared/runtime/runtimeExpression';
import { resolveRuntimeValue } from '../shared/runtime/runtimeExpression';

import { createRuntimeActionHandlers } from './ActionHandlerRegistry';
import { loadRuntimeResourceContext, type RuntimeResourceResolver } from './BindingResolver';
import { LayoutResolver, type RuntimeLayoutContext } from './LayoutResolver';
import { mapDesignerNodeToRuntimeElement, RUNTIME_BUSINESS_ROOT_ATTRIBUTE, RUNTIME_RENDER_SURFACE, RUNTIME_RENDER_SURFACE_ATTRIBUTE } from './runtime-contract/RuntimeRenderBoundaryContract';
import { RuntimeArtifactIntegrity } from './RuntimeArtifactIntegrity';
import { runtimeComponentRegistry } from './RuntimeComponentRegistry';
import { RuntimeKernel, type RuntimeScopeState } from './RuntimeKernel';
import type { RuntimeMonitoringContext } from './RuntimeMonitoringContract';
import { executeRuntimePageMicroflow } from './RuntimePageMicroflow';
import { createRuntimePageFormValues } from './RuntimePageMicroflowFormDefaults';
import type { RuntimeActionFlow, RuntimeContext, RuntimeDesignerDocument, RuntimeDesignerElement, RuntimeResourceBinding } from './RuntimeTypes';

export interface ComponentRuntimeHostProps {
  artifact: RuntimeArtifact;
  breakpoint?: string;
  kernel?: RuntimeKernel;
  layoutContext?: RuntimeLayoutContext;
  manifests: ReadonlyMap<string, import('../pages/application-console/development-center/low-code-studio/components/ComponentManifest').ComponentManifest>;
  onCloseModal?: () => void;
  onNavigate?: (path: string) => void;
  onOpenModal?: (modalId: string, row?: Record<string, unknown> | null, pageInputs?: Record<string, unknown>) => void;
  onOpenPageInvocation?: (invocation: Record<string, unknown>, row?: Record<string, unknown> | null, pageInputs?: Record<string, unknown>) => void;
  onOpenPrint?: (request: import('../features/print-center/types').PrintLaunchRequest) => void;
  onRefreshModel?: () => Promise<void>;
  permissions?: { granted: ReadonlySet<string>; isSystemAdmin?: boolean };
  state?: RuntimeScopeState;
  viewport?: NonNullable<RuntimeLayoutContext['viewport']>;
}

export { createRuntimeManifestRegistry } from './RuntimeComponentRegistry';

export interface RuntimeComponentTreePreviewProps {
  kernel: RuntimeKernel;
  layoutContext: RuntimeLayoutContext;
  runtime: RuntimeContext;
  state: RuntimeScopeState;
}

/**
 * Renders the same recursive component tree used by the published runtime.
 * The designer uses this as its only visual layer; its own nodes are interaction overlays.
 */
export function RuntimeComponentTreePreview({ kernel, layoutContext, runtime, state }: RuntimeComponentTreePreviewProps): ReactNode {
  const rootId = Object.values(kernel.artifact.elements).find((node) => node.parentId === null)?.id;
  if (!rootId) return <RuntimeDiagnosticView message="Runtime artifact has no root element." />;
  return <div style={{ display: 'contents' }} {...{ [RUNTIME_BUSINESS_ROOT_ATTRIBUTE]: 'true', [RUNTIME_RENDER_SURFACE_ATTRIBUTE]: RUNTIME_RENDER_SURFACE.business }}><RuntimeNodeView executeAction={async (_action, currentRuntime) => currentRuntime} kernel={kernel} nodeId={rootId} runtime={runtime} state={state} layoutContext={layoutContext} /></div>;
}

export function ComponentRuntimeHost({ artifact, breakpoint, kernel: providedKernel, layoutContext, manifests, onCloseModal, onNavigate, onOpenModal, onOpenPageInvocation, onOpenPrint, onRefreshModel, permissions, state = { scopes: {}, variables: {} }, viewport }: ComponentRuntimeHostProps) {
  const [variables, setVariables] = useState<Record<string, unknown>>(() => ({ ...state.variables }));
  const [formValues, setFormValues] = useState<Record<string, unknown>>(() => createRuntimePageFormValues(artifact));
  const [componentValues, setComponentValues] = useState<Record<string, unknown>>({});
  const [activeModal, setActiveModal] = useState<RuntimeModalState | null>(null);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const [resources, setResources] = useState<Record<string, unknown>>(() => ({ ...(state.resources ?? {}) }));
  const [runtimeError, setRuntimeError] = useState<string | null>(null);
  const pageLoadKeyRef = useRef('');
  const runtimeAbortControllerRef = useRef<AbortController | null>(null);
  const runtimeArtifactHashRef = useRef<string | null>(null);
  const runtimeLifecycleTokenRef = useRef(0);
  if (runtimeAbortControllerRef.current === null || runtimeArtifactHashRef.current !== artifact.artifactHash) {
    runtimeAbortControllerRef.current = new AbortController();
    runtimeArtifactHashRef.current = artifact.artifactHash;
  }
  const runtimeAbortController = runtimeAbortControllerRef.current;
  useEffect(() => {
    if (!runtimeError) return undefined;
    const timeoutId = window.setTimeout(() => setRuntimeError(null), 6000);
    return () => window.clearTimeout(timeoutId);
  }, [runtimeError]);
  useEffect(() => {
    const controller = runtimeAbortController;
    const lifecycleToken = ++runtimeLifecycleTokenRef.current;
    return () => {
      queueMicrotask(() => {
        // The controller is recreated when the artifact changes; only abort the
        // controller that belongs to this lifecycle generation.
        // eslint-disable-next-line react-hooks/exhaustive-deps
        if (runtimeAbortControllerRef.current === controller && runtimeLifecycleTokenRef.current === lifecycleToken) {
          controller.abort('Runtime host unmounted.');
        }
      });
    };
  }, [runtimeAbortController]);
  const resolvedLayoutContext = useMemo<RuntimeLayoutContext>(() => ({ ...layoutContext, ...(breakpoint === undefined ? {} : { breakpoint }), ...(viewport === undefined ? {} : { viewport }) }), [breakpoint, layoutContext, viewport]);
  const runtimeDocument = useMemo(() => toRuntimeDocument(artifact, resolvedLayoutContext), [artifact, resolvedLayoutContext]);
  const monitoringContext = useMemo(() => createRuntimeMonitoringContext(artifact, state), [artifact, state]);
  const resourceResolvers = useMemo(() => createRuntimeResourceResolvers(artifact, state, variables, formValues, componentValues), [artifact, componentValues, formValues, state, variables]);
  useEffect(() => {
    const bindings = artifact.bindings.filter(isRuntimeResourceBinding);
    const controller = new AbortController();
    if (bindings.length === 0) {
      setResources(state.resources ?? {});
      return () => controller.abort('Runtime resource loading cancelled.');
    }
    void loadRuntimeResourceContext(bindings, resourceResolvers, {
      componentValues,
      form: formValues,
      page: variables,
      resources: state.resources,
      resourceProviders: resourceResolvers,
      scopes: {
        ...state.scopes,
        component: componentValues,
        form: formValues,
        page: variables,
        variables
      },
      scopeStore: state.scopeStore,
      variables
    }, controller.signal)
      .then(setResources)
      .catch((error: unknown) => {
        if (!controller.signal.aborted) setKernelError(error instanceof Error ? error : new Error(String(error)));
      });
    return () => controller.abort('Runtime resource loading cancelled.');
  }, [artifact.bindings, componentValues, formValues, refreshVersion, resourceResolvers, state.resources, state.scopeStore, state.scopes, variables]);
  const actionHandlers = useMemo(() => createRuntimeActionHandlers(artifact), [artifact]);
  const [kernel, setKernel] = useState<RuntimeKernel | null>(providedKernel ?? null);
  const [kernelError, setKernelError] = useState<Error | null>(null);
  useEffect(() => {
    let active = true;
    setKernelError(null);
    if (providedKernel) {
      setKernel(null);
      void Promise.all([
        RuntimeArtifactIntegrity.verify(artifact),
        RuntimeArtifactIntegrity.verify(providedKernel.artifact)
      ]).then(async ([artifactDiagnostic, kernelDiagnostic]) => {
        if (!active) return;
        if (artifactDiagnostic) {
          setKernelError(new Error(`${artifactDiagnostic.code}: ${artifactDiagnostic.message}`));
          return;
        }
        if (kernelDiagnostic) {
          setKernelError(new Error(`Provided RuntimeKernel artifact is invalid: ${kernelDiagnostic.code}: ${kernelDiagnostic.message}`));
          return;
        }
        if (!await RuntimeArtifactIntegrity.hasSameIdentity(providedKernel.artifact, artifact)) {
          setKernelError(new Error('Provided RuntimeKernel does not match the verified runtime artifact.'));
          return;
        }
        setKernel(providedKernel);
      }).catch((error: unknown) => {
        if (active) setKernelError(error instanceof Error ? error : new Error(String(error)));
      });
      return () => { active = false; };
    }

    setKernel(null);
    void RuntimeKernel.create(artifact, {
      actionHandlers,
      manifests,
      monitoringContext,
      permissions,
      resolveRenderer: (componentType) => runtimeComponentRegistry.has(componentType)
    }).then((createdKernel) => {
      if (active) setKernel(createdKernel);
    }).catch((error: unknown) => {
      if (active) setKernelError(error instanceof Error ? error : new Error(String(error)));
    });
    return () => { active = false; };
  }, [actionHandlers, artifact, manifests, monitoringContext, permissions, providedKernel]);
  const runtime = useMemo<RuntimeContext>(() => ({
    closeModal: () => {
      setActiveModal(null);
      onCloseModal?.();
    },
    clearComponentValues: (elementIds) => setComponentValues((current) => elementIds
      ? Object.fromEntries(Object.entries(current).filter(([id]) => !elementIds.includes(id)))
      : {}),
    componentValues,
    document: runtimeDocument,
    formValues,
    navigate: (path) => onNavigate?.(path),
    openPrint: (request) => onOpenPrint?.(request),
    openModal: (modalId, row, pageInputs) => {
      setActiveModal({ modalId, pageInputs: pageInputs ?? {}, row: row ?? null });
      onOpenModal?.(modalId, row, pageInputs);
    },
    openPageInvocation: (invocation, row, pageInputs) => onOpenPageInvocation?.(invocation, row, pageInputs),
    refreshModel: async () => {
      await onRefreshModel?.();
      setRefreshVersion((current) => current + 1);
    },
    refreshVersion,
    mergeVariables: (values) => setVariables((current) => ({ ...current, ...values })),
    setComponentValue: (elementId, value) => setComponentValues((current) => ({ ...current, [elementId]: value })),
    setFormValue: (field, value) => setFormValues((current) => ({ ...current, [field]: value })),
    setFormValues,
    setVariable: (key, value) => setVariables((current) => ({ ...current, [key]: value })),
    setVariablePath: (path, value) => setVariables((current) => writePath(current, path, value)),
    signal: runtimeAbortController.signal,
    scopes: {
      ...state.scopes,
      component: componentValues,
      form: formValues,
      page: variables,
      variables
    },
    resources,
    variables
  }), [componentValues, formValues, onCloseModal, onNavigate, onOpenModal, onOpenPageInvocation, onOpenPrint, onRefreshModel, refreshVersion, resources, runtimeAbortController.signal, runtimeDocument, state.scopes, variables]);
  useEffect(() => {
    if (!kernel) return undefined;
    const bindings = artifact.pageMicroflows.filter((binding) => binding.trigger === 'pageLoad');
    if (bindings.length === 0) return undefined;
    const loadKey = `${artifact.artifactHash}:${bindings.map((binding) => binding.alias).join(',')}`;
    if (pageLoadKeyRef.current === loadKey) return undefined;
    pageLoadKeyRef.current = loadKey;
    let active = true;
    void Promise.all(bindings.map((binding) => executeRuntimePageMicroflow(artifact, binding.alias, {
      formValues: runtime.formValues,
      mergeVariables: (values) => setVariables((current) => ({ ...current, ...values })),
      scopes: runtime.scopes ?? {},
      setFormValues: runtime.setFormValues,
      signal: runtime.signal
    }))).catch((error: unknown) => {
      if (active) setRuntimeError(error instanceof Error ? error.message : String(error));
    });
    return () => { active = false; };
  }, [artifact, kernel, runtime]);
  const executeAction = useCallback(async (action: RuntimeActionFlow, currentRuntime: RuntimeContext) => {
    if (!kernel) throw new Error('Runtime kernel is not ready.');
    ensureRuntimeActionActive(currentRuntime.signal);
    const actionScope: RuntimeExpressionScope = {
      form: currentRuntime.formValues,
      page: currentRuntime.variables,
      variables: currentRuntime.variables
    };
    if (action.condition !== undefined && action.condition !== null && action.condition !== '') {
      const resolvedCondition = resolveRuntimeValue(action.condition, actionScope);
      if (resolvedCondition !== true && resolvedCondition !== 'true') return currentRuntime;
    }
    const permissionCode = resolveRuntimeValue(action.permissionCode, actionScope);
    if (typeof permissionCode === 'string' && permissionCode.trim() && !kernel.permissions.can({ code: permissionCode.trim(), required: true })) {
      kernel.diagnostics.error('permissionDenied', `Permission is required for action ${action.id}`, `actions.${action.id}`);
      return currentRuntime;
    }
    setRuntimeError(null);
    try {
      const actionResults = await kernel.executeActions(action.steps, {
        closeModal: currentRuntime.closeModal,
        navigate: currentRuntime.navigate,
        openModal: currentRuntime.openModal,
        openPageInvocation: (config) => currentRuntime.openPageInvocation(config),
        openPrint: (config) => currentRuntime.openPrint(config as unknown as import('../features/print-center/types').PrintLaunchRequest),
        refresh: currentRuntime.refreshModel,
        formValues: currentRuntime.formValues,
        errorPolicy: action.errorPolicy,
        mergeVariables: currentRuntime.mergeVariables,
        setFormValues: currentRuntime.setFormValues,
        setVariable: currentRuntime.setVariablePath,
        scopes: {
          action: currentRuntime.variables,
          form: currentRuntime.formValues,
          page: currentRuntime.variables,
          row: isRecord(currentRuntime.variables.currentRow) ? currentRuntime.variables.currentRow : {},
          variables: currentRuntime.variables
        },
        signal: currentRuntime.signal
      });
      const businessError = readRuntimeActionBusinessError(actionResults);
      setRuntimeError(businessError);
    } catch (error) {
      setRuntimeError(error instanceof Error ? error.message : String(error));
      throw error;
    }
    return currentRuntime;
  }, [kernel]);
  if (kernelError) return <RuntimeDiagnosticView message={kernelError.message} />;
  if (!kernel) return <RuntimeDiagnosticView message="Runtime artifact is being verified." />;
  const rootId = Object.values(artifact.elements).find((node) => node.parentId === null)?.id;
  if (!rootId) return <RuntimeDiagnosticView message="Runtime artifact has no root element." />;
  const activeModalState = activeModal;
  const modal = activeModalState ? runtimeDocument.modals.find((item) => item.id === activeModalState.modalId) : undefined;
  const modalRuntime = modal && activeModalState ? { ...runtime, scopes: { ...runtime.scopes, modal: activeModalState.pageInputs, row: activeModalState.row ?? {} }, variables } : null;
  return <>
     {runtimeError ? <div aria-live="assertive" className="pointer-events-none fixed inset-x-0 top-16 z-[80] flex justify-center px-4" role="alert"><div className="pointer-events-auto flex w-full max-w-xl items-start gap-3 rounded-lg border border-red-200 bg-white px-4 py-3 text-sm text-red-700 shadow-lg"><span className="min-w-0 flex-1 leading-6">{runtimeError}</span><button aria-label="关闭错误提示" className="shrink-0 rounded px-2 py-1 text-xs font-medium text-red-700 hover:bg-red-50" type="button" onClick={() => setRuntimeError(null)}>关闭</button></div></div> : null}
    <RuntimeNodeView executeAction={executeAction} kernel={kernel} nodeId={rootId} runtime={runtime} state={{
    ...state,
    resources,
    resourceProviders: resourceResolvers,
    scopes: runtime.scopes ?? state.scopes,
    variables
    }} layoutContext={resolvedLayoutContext} />
    {modal && modalRuntime ? <div aria-modal="true" className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40" role="dialog">
      <div className="max-h-[90vh] w-[min(640px,calc(100vw-2rem))] overflow-auto rounded-xl bg-white p-5 shadow-2xl">
        <div className="mb-4 flex items-center justify-between"><h2 className="font-semibold text-slate-900">{modal.name}</h2><button type="button" onClick={modalRuntime.closeModal}>Close</button></div>
      <RuntimeNodeView executeAction={executeAction} kernel={kernel} nodeId={modal.rootElementId} runtime={modalRuntime} state={{ ...state, resources, resourceProviders: resourceResolvers, scopes: modalRuntime.scopes ?? state.scopes, variables }} layoutContext={resolvedLayoutContext} />
      </div>
    </div> : null}
  </>;
}

function readRuntimeActionBusinessError(results: readonly unknown[]): string | null {
  for (const result of results) {
    const record = findRuntimeResultRecord(result);
    if (!record) continue;
    const deleted = record.deleted;
    if (deleted === 0 || deleted === false || deleted === '0') {
      return '当前操作未产生变更，订单可能存在关联明细或不满足 SQL 微流的业务条件。';
    }
  }
  return null;
}

function findRuntimeResultRecord(value: unknown): Record<string, unknown> | null {
  if (Array.isArray(value)) {
    for (const item of value) {
      const record = findRuntimeResultRecord(item);
      if (record) return record;
    }
    return null;
  }
  return value && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null;
}

export function RuntimeNodeView({ executeAction, kernel, layoutContext, nodeId, runtime, state }: { executeAction: (action: RuntimeActionFlow, runtime: RuntimeContext) => Promise<RuntimeContext>; kernel: RuntimeKernel; layoutContext: RuntimeLayoutContext; nodeId: string; runtime: RuntimeContext; state: RuntimeScopeState }): ReactNode {
  const node = kernel.snapshot(nodeId, state, layoutContext);
  if (!node) return null;
  if (!node.visible) return null;
  const renderer = runtimeComponentRegistry.get(node.type);
  if (!renderer) return <RuntimeDiagnosticView message={`No runtime renderer registered for ${node.type}.`} />;
  const sourceElement = kernel.artifact.elements[nodeId];
  const element = mapDesignerNodeToRuntimeElement(sourceElement, node);
  const scope: RuntimeExpressionScope = {
    component: node.props,
    form: runtime.formValues,
    page: runtime.variables,
    system: {},
    variables: runtime.variables
  };
  const children = node.children.map((childId) => <RuntimeNodeView key={childId} executeAction={executeAction} kernel={kernel} layoutContext={layoutContext} nodeId={childId} runtime={runtime} state={state} />);
  return renderer({
    action: findRuntimeAction(element, 'click'),
    bindings: node.bindings,
    changeAction: findRuntimeAction(element, 'change'),
    children,
    componentType: node.type,
    disabled: node.disabled,
    element,
    executeAction,
    onChange: (value, changeAction) => {
      runtime.setComponentValue?.(nodeId, value);
      const field = readFormField(sourceElement.bindings?.data);
      const nextFormValues = field ? { ...runtime.formValues, [field]: value } : runtime.formValues;
      if (field) runtime.setFormValues(nextFormValues);
      if (changeAction) void executeAction(changeAction, { ...runtime, formValues: nextFormValues });
    },
    layout: node.layout,
    loading: node.loading,
    permission: node.permission,
    props: node.props,
    readOnly: node.readOnly,
    runtime,
    scope,
    style: node.style,
    title: String(node.props.title ?? sourceElement.name ?? node.type),
    value: resolveRuntimeComponentValue(element, node.props, runtime, nodeId),
    visible: node.visible
  });
}

interface RuntimeModalState { modalId: string; pageInputs: Record<string, unknown>; row: Record<string, unknown> | null; }

export function createRuntimePreviewContext(artifact: RuntimeArtifact, layoutContext: RuntimeLayoutContext, state: RuntimeScopeState): RuntimeContext {
  const variables = { ...state.variables };
  const formValues = createRuntimePageFormValues(artifact);
  const componentValues: Record<string, unknown> = {};
  const runtimeDocument = toRuntimeDocument(artifact, layoutContext);
  return {
    closeModal: () => undefined,
    clearComponentValues: () => undefined,
    componentValues,
    document: runtimeDocument,
    formValues,
    navigate: () => undefined,
    openPrint: () => undefined,
    openModal: () => undefined,
    openPageInvocation: () => undefined,
    refreshModel: async () => undefined,
    refreshVersion: 0,
    mergeVariables: () => undefined,
    setComponentValue: (elementId, value) => { componentValues[elementId] = value; },
    setFormValue: (field, value) => { formValues[field] = value; },
    setFormValues: (values) => Object.assign(formValues, values),
    setVariable: (key, value) => { variables[key] = value; },
    setVariablePath: (path, value) => { variables[path] = value; },
    variables,
    scopes: { ...state.scopes, component: componentValues, form: formValues, page: variables, variables }
  };
}

function toRuntimeDocument(artifact: RuntimeArtifact, layoutContext: RuntimeLayoutContext): RuntimeDesignerDocument {
  const resolver = new LayoutResolver();
  return {
    apiBindings: [],
    elements: Object.fromEntries(Object.entries(artifact.elements).map(([id, node]) => {
      const sections = resolver.resolveSections(node, layoutContext);
      return [id, mapDesignerNodeToRuntimeElement(node, {
        bindings: node.bindings ?? {},
        children: node.children,
        disabled: false,
        id: node.id,
        layout: sections.layout,
        loading: false,
        permission: node.permission,
        props: sections.props,
        readOnly: false,
        style: sections.style,
        type: node.type,
        visible: true
      })];
    })),
    modals: readRuntimeModals(artifact.runtimeContext),
      pageMicroflows: artifact.pageMicroflows as unknown as RuntimeDesignerDocument['pageMicroflows'],
    pages: artifact.pages ?? [{ id: artifact.documentId, name: artifact.documentId, rootElementId: Object.values(artifact.elements).find((node) => node.parentId === null)?.id ?? '' }],
    runtimeContext: artifact.runtimeContext,
      variables: artifact.variables
  };
}

function createRuntimeMonitoringContext(artifact: RuntimeArtifact, state: RuntimeScopeState): RuntimeMonitoringContext {
  const workspace = readRuntimeWorkspace();
  const currentUser = state.scopes.system?.currentUser;
  const userId = isRecord(currentUser)
    ? String(currentUser.userId ?? currentUser.id ?? currentUser.userName ?? '').trim()
    : '';
  const pageCode = typeof artifact.runtimeContext.pageCode === 'string'
    ? artifact.runtimeContext.pageCode.trim()
    : artifact.documentId;
  return {
    appCode: workspace.appCode,
    pageCode,
    tenantId: workspace.tenantId,
    traceId: createRuntimeTraceId(artifact.artifactHash),
    userId
  };
}

function readRuntimeWorkspace(): { appCode: string; tenantId: string } {
  if (typeof window === 'undefined') return { appCode: '', tenantId: '' };
  const segments = window.location.pathname.split('/').filter(Boolean).map(decodeRuntimeSegment);
  const tenantIndex = segments.indexOf('tenants');
  const appIndex = segments.indexOf('apps');
  return {
    appCode: appIndex >= 0 ? segments[appIndex + 1] ?? '' : '',
    tenantId: tenantIndex >= 0 ? segments[tenantIndex + 1] ?? '' : ''
  };
}

function decodeRuntimeSegment(value: string): string {
  try { return decodeURIComponent(value); } catch { return ''; }
}

function createRuntimeTraceId(artifactHash: string): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return `runtime-${artifactHash.slice(-16)}-${Date.now().toString(36)}`;
}

function createRuntimeResourceResolvers(artifact: RuntimeArtifact, state: RuntimeScopeState, variables: Record<string, unknown>, formValues: Record<string, unknown>, componentValues: Record<string, unknown>): ReadonlyMap<string, RuntimeResourceResolver> {
  const scopes: Record<string, Record<string, unknown>> = { ...state.scopes, component: componentValues, form: formValues, page: variables, variables };
  const configured = isRecord(artifact.runtimeContext.resourceProviders) ? artifact.runtimeContext.resourceProviders : {};
  const providers = new Map<string, RuntimeResourceResolver>();
  Object.entries(configured).forEach(([provider, values]) => {
    if (!isRecord(values)) return;
    providers.set(provider, ({ resourceId }) => readProviderValue(values, resourceId));
  });
  ['page', 'component', 'form', 'row', 'action', 'modal', 'variables', 'system'].forEach((provider) => {
    providers.set(provider, ({ resourceId }) => readProviderValue(provider === 'system' ? { currentUser: state.scopes.system?.currentUser } : scopes[provider] ?? {}, resourceId));
  });
  providers.set('microflow', ({ resourceId }) => readProviderValue(
    isRecord(variables.microflows) ? variables.microflows : {},
    resourceId
  ));
  providers.set('runtime.data-model', async ({ binding, resourceId }) => {
    const modelCode = binding?.modelCode?.trim();
    if (!modelCode) throw new Error(`Runtime data-model binding requires modelCode: ${resourceId}`);
    const response = await queryRuntimeModel(modelCode, {
      pageIndex: 1,
      pageSize: 100,
      pageCode: binding?.pageCode ?? String(artifact.runtimeContext.pageCode ?? ''),
      previewPageId: binding?.previewPageId ?? null
    });
    const data = response.data;
    return {
      [resourceId]: data,
      [`${resourceId}.fields`]: data.fields,
      [`${resourceId}.rows`]: data.rows,
      [`${resourceId}.total`]: data.total,
      ...(binding?.field ? { [`${resourceId}.${binding.field}`]: data.rows[0]?.[binding.field] } : {})
    };
  });
  return providers;
}

function isRuntimeResourceBinding(value: Record<string, unknown>): value is Record<string, unknown> & RuntimeResourceBinding {
  return typeof value.resourceId === 'string' && value.resourceId.trim().length > 0 && typeof value.provider === 'string' && value.provider.trim().length > 0;
}

function readProviderValue(values: Record<string, unknown>, resourceId: string): unknown {
  if (Object.prototype.hasOwnProperty.call(values, resourceId)) return values[resourceId];
  const parts = resourceId.split(':');
  const path = parts.length >= 3 ? parts.slice(2).join(':') : parts.slice(1).join(':');
  if (!path || path === '*') return values;
  return path.split('.').filter(Boolean).reduce<unknown>((current, part) => current && typeof current === 'object' ? (current as Record<string, unknown>)[part] : undefined, values);
}

function readRuntimeModals(value: Record<string, unknown>): RuntimeDesignerDocument['modals'] {
  const modals = value.modals;
  return Array.isArray(modals) ? modals.filter(isRecord).map((modal) => ({ id: String(modal.id ?? ''), name: String(modal.name ?? modal.id ?? ''), rootElementId: String(modal.rootElementId ?? ''), type: String(modal.type ?? 'dialog') })).filter((modal) => modal.id && modal.rootElementId) : [];
}

function findRuntimeAction(element: RuntimeDesignerElement, trigger: string): RuntimeActionFlow | undefined {
  return element.events?.find((action) => action.trigger === trigger);
}

function writePath(values: Record<string, unknown>, path: string, value: unknown): Record<string, unknown> {
  const parts = path.split('.').filter(Boolean);
  if (parts.length === 0) return values;
  const result = { ...values };
  let cursor: Record<string, unknown> = result;
  parts.slice(0, -1).forEach((part) => {
    const next = cursor[part];
    const child = next && typeof next === 'object' && !Array.isArray(next) ? { ...(next as Record<string, unknown>) } : {};
    cursor[part] = child;
    cursor = child;
  });
  cursor[parts.at(-1)!] = value;
  return result;
}

function readFormField(value: unknown): string | null {
  if (!isRecord(value)) return null;
  if (typeof value.field === 'string' && value.field.trim()) return value.field.trim();
  if (typeof value.resourceId !== 'string') return null;
  const match = /^form:(.+)$/.exec(value.resourceId.trim());
  return match?.[1]?.split('.').filter(Boolean).at(-1) ?? null;
}

/**
 * Data bindings are resolved by RuntimeKernel before the renderer receives a
 * snapshot. Keep the field metadata from the source element so a form-backed
 * input can still read the current form scope after a microflow writes it.
 */
export function resolveRuntimeComponentValue(
  sourceElement: RuntimeDesignerElement,
  props: Record<string, unknown>,
  runtime: Pick<RuntimeContext, 'componentValues' | 'formValues'>,
  nodeId: string
): unknown {
  if (runtime.componentValues && Object.prototype.hasOwnProperty.call(runtime.componentValues, nodeId)) {
    return runtime.componentValues[nodeId];
  }
  const field = readFormField(sourceElement.bindings?.data);
  if (field && runtime.formValues && Object.prototype.hasOwnProperty.call(runtime.formValues, field)) {
    return runtime.formValues[field];
  }
  return props.value;
}

function ensureRuntimeActionActive(signal: AbortSignal | undefined): void {
  if (signal?.aborted) throw new DOMException('Runtime action execution was cancelled.', 'AbortError');
}

function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value && typeof value === 'object' && !Array.isArray(value)); }

function RuntimeDiagnosticView({ message }: { message: string }) {
  return <div role="alert" className="rounded border border-red-200 bg-red-50 p-3 text-sm text-red-700">{message}</div>;
}
