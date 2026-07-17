import type { ComponentManifest } from '../pages/application-console/development-center/low-code-studio/components/ComponentManifest';
import type { DesignerDocumentNode } from '../pages/application-console/development-center/low-code-studio/document/DesignerDocument';
import type { RuntimeArtifact } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';

import { ActionHandlerRegistry, type RuntimeActionContext, type RuntimeActionHandler, type RuntimeActionStep } from './ActionHandlerRegistry';
import { BindingResolver, type RuntimeResourceResolver } from './BindingResolver';
import { RuntimeContractError, RuntimeDiagnostics, type RuntimeArtifactContext } from './Diagnostics';
import { LayoutResolver, type RuntimeLayoutContext } from './LayoutResolver';
import { PermissionEvaluator, type RuntimePermissionContext } from './PermissionEvaluator';
import { RUNTIME_CAPABILITY_CONTRACT } from './runtime-contract/RuntimeCapabilityContract';
import { LATEST_RUNTIME_COMPILER_REVISION, RuntimeArtifactIntegrity, type SignedRuntimeArtifact } from './RuntimeArtifactIntegrity';
import { RuntimeDependencyGraph, type RuntimeDependencyChange } from './RuntimeDependencyGraph';
import type { RuntimeMonitoringContext } from './RuntimeMonitoringContract';
import { RuntimeScopeStore } from './RuntimeScopeStore';
import { RUNTIME_SCOPE_NAMES } from './RuntimeTypes';

export interface RuntimeKernelOptions {
  actionHandlers?: readonly RuntimeActionHandler[];
  manifests: ReadonlyMap<string, ComponentManifest>;
  monitoringContext: RuntimeMonitoringContext;
  permissions?: RuntimePermissionContext;
  resolveRenderer?: (componentType: string) => boolean;
}

export interface RuntimeScopeState {
  loading?: Record<string, boolean>;
  resourceProviders?: ReadonlyMap<string, RuntimeResourceResolver>;
  resources?: Record<string, unknown>;
  scopeIds?: Partial<Record<(typeof RUNTIME_SCOPE_NAMES)[number], string>>;
  scopeStore?: import('./RuntimeScopeStore').RuntimeScopeStore;
  scopes: Record<string, Record<string, unknown>>;
  variables: Record<string, unknown>;
}

export interface RuntimeNodeSnapshot {
  bindings: Record<string, unknown>;
  children: string[];
  id: string;
  layout: Record<string, unknown>;
  loading: boolean;
  permission?: { code?: string | null; visibleWhen?: string | Record<string, unknown> | null };
  props: Record<string, unknown>;
  readOnly: boolean;
  style: Record<string, unknown>;
  type: string;
  visible: boolean;
  disabled: boolean;
}

export class RuntimeKernel {
  public readonly actions: ActionHandlerRegistry;
  public readonly bindings = new BindingResolver();
  public readonly diagnostics: RuntimeDiagnostics;
  public readonly layouts = new LayoutResolver();
  public readonly permissions: PermissionEvaluator;
  private readonly dependencyGraph = new Map<string, Set<string>>();
  private readonly compiledDependencies = new RuntimeDependencyGraph();
  private readonly nodes: Readonly<Record<string, DesignerDocumentNode>>;
  public readonly scopeStore = new RuntimeScopeStore();

  private constructor(public readonly artifact: SignedRuntimeArtifact, private readonly options: RuntimeKernelOptions) {
    const startedAt = now();
    this.diagnostics = new RuntimeDiagnostics(toArtifactContext(artifact), options.monitoringContext);
    this.actions = new ActionHandlerRegistry();
    for (const handler of options.actionHandlers ?? []) {
      try {
        this.actions.register(handler);
      } catch (error) {
        const type = isRecord(handler.manifest) && typeof handler.manifest.type === 'string' ? handler.manifest.type : '';
        this.diagnostics.error('invalidActionHandler', error instanceof Error ? error.message : `Runtime action handler is invalid: ${type}`, 'actionHandlers');
      }
    }
    this.permissions = new PermissionEvaluator(options.permissions ?? { granted: new Set() });
    this.nodes = artifact.elements;
    this.validateArtifact();
    this.validateActions();
    this.buildDependencyGraph();
    if (this.diagnostics.hasErrors) {
      this.diagnostics.recordArtifactLoadFailure(now() - startedAt);
      throw new RuntimeContractError(this.diagnostics.all);
    }
    this.diagnostics.recordArtifactLoadSuccess(now() - startedAt);
  }

  public static async create(artifact: RuntimeArtifact, options: RuntimeKernelOptions): Promise<RuntimeKernel> {
    const integrityDiagnostic = await RuntimeArtifactIntegrity.verify(artifact);
    if (integrityDiagnostic) {
      throw new RuntimeContractError([{
        ...integrityDiagnostic,
        details: { ...integrityDiagnostic.details, artifact: toArtifactContext(artifact) }
      }]);
    }
    if (!RuntimeArtifactIntegrity.isSignedArtifact(artifact)) {
      throw new RuntimeContractError([{
        code: 'invalidArtifactShape',
        message: 'Runtime artifact failed formal shape validation.',
        path: 'artifact',
        severity: 'error'
      }]);
    }
    const monitoringDiagnostic = validateMonitoringContext(options.monitoringContext);
    if (monitoringDiagnostic) throw new RuntimeContractError([monitoringDiagnostic]);
    return new RuntimeKernel(artifact, options);
  }

  public snapshot(nodeId: string, state: RuntimeScopeState, layout: RuntimeLayoutContext = {}): RuntimeNodeSnapshot | null {
    const node = this.nodes[nodeId];
    if (!node) return null;
    if (!isRecord(state) || !isRecord(state.scopes)) {
      this.diagnostics.error('invalidRuntimeScopeState', 'Runtime scope state must contain an object scopes record.', 'scopes');
      return null;
    }
    const invalidScope = Object.keys(state.scopes).find((scope) => !isRuntimeScope(scope));
    if (invalidScope) {
      this.diagnostics.error('unknownRuntimeScope', `Unknown runtime scope: ${invalidScope}`, `scopes.${invalidScope}`);
      return null;
    }
    const scopeState = normalizeScopeState(state);
    if (!scopeState) {
      this.diagnostics.error('invalidRuntimeScopeState', 'Runtime scope values and scope IDs must be valid records.', 'scopes');
      return null;
    }
    try {
      const bindingContext = this.createBindingContext(scopeState);
      const resolvedSections = this.layouts.resolveSections(node, layout);
      const bindings = this.bindings.resolveRecord(node.bindings ?? {}, bindingContext);
      const props = this.bindings.resolveRecord(resolvedSections.props ?? node.props, bindingContext);
      const permission = resolvePermission(node.permission);
      const permissionDiagnostic = this.permissions.diagnose(permission as { code?: string; required?: boolean }, `elements.${nodeId}`);
      if (permissionDiagnostic) {
        this.diagnostics.add(permissionDiagnostic);
        return null;
      }
      const visibleWhen = node.permission?.visibleWhen === undefined ? true : this.bindings.resolve(node.permission.visibleWhen, bindingContext).value !== false;
      const visible = props.visible !== false && visibleWhen;
      const loading = props.loading === true || scopeState.loading?.[nodeId] === true;
      return {
        bindings,
        children: [...node.children],
        disabled: props.disabled === true,
        id: node.id,
        layout: this.bindings.resolveRecord(resolvedSections.layout, bindingContext),
        loading,
        permission,
        props,
        readOnly: props.readOnly === true,
        style: this.bindings.resolveRecord(resolvedSections.style, bindingContext),
        type: node.type,
        visible
      };
    } catch (error) {
      this.diagnostics.error(
        'bindingResolutionFailed',
        error instanceof Error ? error.message : 'Runtime binding resolution failed.',
        `elements.${nodeId}`
      );
      return null;
    }
  }

  public affectedNodes(changedResourceId: string | readonly string[]): readonly string[] {
    const changed = typeof changedResourceId === 'string' ? [changedResourceId] : changedResourceId;
    return this.compiledDependencies.affected(changed);
  }

  public async recompute(
    state: RuntimeScopeState,
    changes: readonly RuntimeDependencyChange[] | readonly string[],
    layout: RuntimeLayoutContext = {},
    signal?: AbortSignal
  ): Promise<ReadonlyMap<string, RuntimeNodeSnapshot>> {
    const snapshots = new Map<string, RuntimeNodeSnapshot>();
    const affected = this.compiledDependencies.affected(changes);
    for (const nodeId of affected) {
      if (signal?.aborted) {
        const diagnostic = { code: 'recomputeCancelled', message: 'Runtime dependency recomputation was cancelled.', severity: 'warning' as const };
        this.diagnostics.add(diagnostic);
        this.diagnostics.recordRecomputeCancellation();
        throw new Error(diagnostic.message);
      }
      const snapshot = this.snapshot(nodeId, state, layout);
      if (snapshot) snapshots.set(nodeId, snapshot);
      await Promise.resolve();
    }
    return snapshots;
  }

  public async executeActions(steps: readonly RuntimeActionStep[], context: RuntimeActionContext): Promise<readonly unknown[]> {
    const invalidScope = Object.keys(context.scopes).find((scope) => !isRuntimeScope(scope));
    if (invalidScope) {
      const diagnostic = { code: 'unknownRuntimeScope', message: `Unknown runtime scope: ${invalidScope}`, path: `scopes.${invalidScope}`, severity: 'error' as const };
      (context.diagnostics ?? this.diagnostics).add(diagnostic);
      throw new RuntimeContractError([diagnostic]);
    }
    const unsupportedAction = steps.find((step) => typeof step.type !== 'string' || !RUNTIME_CAPABILITY_CONTRACT.actions.includes(step.type));
    if (unsupportedAction) {
      const diagnostic = {
        code: 'unknownAction',
        message: `Unknown runtime action: ${String(unsupportedAction.type ?? '')}`,
        path: `actions.${unsupportedAction.id ?? 'unknown'}`,
        severity: 'error' as const
      };
      (context.diagnostics ?? this.diagnostics).add(diagnostic);
      throw new RuntimeContractError([diagnostic]);
    }
    const permissionContext = this.options.permissions ?? { granted: new Set<string>(), isSystemAdmin: false };
    try {
      const result = await this.actions.execute(steps, {
        ...context,
        diagnostics: context.diagnostics ?? this.diagnostics,
        isSystemAdmin: permissionContext.isSystemAdmin === true,
        permissions: permissionContext.granted
      });
      this.diagnostics.recordActionSuccess();
      return result;
    } catch (error) {
      this.diagnostics.recordActionFailure();
      throw error;
    }
  }

  private validateArtifact(): void {
    if (!Number.isInteger(this.artifact.revision) || this.artifact.revision < 1) this.diagnostics.error('invalidRevision', 'Runtime artifact revision must be a positive integer');
    if (typeof this.artifact.artifactHash !== 'string' || !this.artifact.artifactHash.trim()) {
      this.diagnostics.error('missingArtifactHash', 'Runtime artifact hash is required');
    } else if (!/^sha256:[0-9a-f]{64}$/i.test(this.artifact.artifactHash)) {
      this.diagnostics.error('invalidArtifactHash', 'Runtime artifact hash must be a SHA-256 digest');
    }
    if (!Array.isArray(this.artifact.manifestTypes)) this.diagnostics.error('invalidManifestTypes', 'Runtime artifact manifestTypes must be an array', 'manifestTypes');
    else if (new Set(this.artifact.manifestTypes).size !== this.artifact.manifestTypes.length) this.diagnostics.error('duplicateManifestType', 'Runtime artifact manifestTypes must be unique');
    this.validateManifestDeclarations();
    if (typeof this.artifact.documentId !== 'string' || !this.artifact.documentId.trim()) this.diagnostics.error('missingDocumentId', 'Runtime artifact documentId is required', 'documentId');
    if (this.artifact.compilerVersion !== LATEST_RUNTIME_COMPILER_REVISION) this.diagnostics.error('unsupportedCompilerVersion', 'Runtime artifact compilerVersion must match the latest runtime compiler revision', 'compilerVersion');
    if (!isRecord(this.artifact.elements) || Object.keys(this.artifact.elements).length === 0) this.diagnostics.error('missingElements', 'Runtime artifact must contain at least one element', 'elements');
    const actualTypes = new Set<string>();
    for (const type of Array.isArray(this.artifact.manifestTypes) ? this.artifact.manifestTypes : []) {
      if (!RUNTIME_CAPABILITY_CONTRACT.components.includes(type)) {
        this.diagnostics.error('unknownManifest', `Unknown runtime capability component: ${type}`, `manifestTypes.${type}`);
      } else if (!this.options.manifests.has(type)) {
        this.diagnostics.error('unknownManifest', `Unknown component manifest: ${type}`, `manifestTypes.${type}`);
      }
      const manifest = this.options.manifests.get(type);
      if (manifest && manifest.type !== type) this.diagnostics.error('manifestTypeMismatch', `Manifest key does not match manifest.type: ${type}`, `manifestTypes.${type}`);
      if (manifest && !manifest.runtime.renderer.trim()) this.diagnostics.error('missingRuntimeRenderer', `Manifest has no runtime renderer: ${type}`, `manifestTypes.${type}`);
      if (manifest && manifest.runtime.supportedScopes.some((scope) => !isRuntimeScope(scope))) {
        this.diagnostics.error('invalidRuntimeScope', `Manifest contains an unsupported runtime scope: ${type}`, `manifestTypes.${type}.runtime.supportedScopes`);
      }
    }
    let rootCount = 0;
    const elements = isRecord(this.artifact.elements) ? this.artifact.elements as Record<string, DesignerDocumentNode> : {};
    for (const [id, node] of Object.entries(elements)) {
      if (!isRecord(node)) {
        this.diagnostics.error('invalidElement', 'Runtime artifact element must be an object', `elements.${id}`);
        continue;
      }
      actualTypes.add(node.type);
      if (!node.id || node.id !== id) this.diagnostics.error('invalidNodeId', `Element key does not match node id: ${id}`, `elements.${id}`);
      if (!RUNTIME_CAPABILITY_CONTRACT.components.includes(node.type)) {
        this.diagnostics.error('unknownManifest', `Element uses unknown runtime capability component: ${node.type}`, `elements.${id}`);
      } else if (!this.options.manifests.has(node.type)) {
        this.diagnostics.error('unknownManifest', `Element uses unknown component manifest: ${node.type}`, `elements.${id}`);
      }
      if (this.options.resolveRenderer && !this.options.resolveRenderer(node.type)) this.diagnostics.error('unknownRuntimeRenderer', `No runtime renderer is registered for ${node.type}`, `elements.${id}`);
      if (node.parentId === null) rootCount += 1;
      if (!Array.isArray(node.children) || new Set(node.children).size !== node.children.length) this.diagnostics.error('invalidChildren', `Element children must be unique: ${id}`, `elements.${id}.children`);
      for (const child of node.children) {
        if (!elements[child]) this.diagnostics.error('missingChild', `Element references missing child: ${child}`, `elements.${id}.children`);
        else if (elements[child].parentId !== id) this.diagnostics.error('parentChildMismatch', `Child ${child} does not point back to ${id}`, `elements.${id}.children`);
      }
      if (node.parentId !== null && (!node.parentId || !elements[node.parentId])) this.diagnostics.error('missingParent', `Element references missing parent: ${id}`, `elements.${id}.parentId`);
      this.validateBindings(node.props, `elements.${id}.props`);
      this.validateBindings(node.layout, `elements.${id}.layout`);
      this.validateBindings(node.style, `elements.${id}.style`);
      this.validateBindings(node.bindings, `elements.${id}.bindings`);
      for (const [eventIndex, event] of node.events.entries()) this.validateAction(event, `elements.${id}.events.${eventIndex}`);
    }
    if (rootCount !== 1) this.diagnostics.error('invalidRootCount', 'Runtime artifact must contain exactly one root element', 'elements');
    if (actualTypes.size !== new Set(this.artifact.manifestTypes).size || [...actualTypes].some((type) => !this.artifact.manifestTypes.includes(type))) {
      this.diagnostics.error('manifestTypesIncomplete', 'manifestTypes must exactly match component types used by elements', 'manifestTypes');
    }
    this.validateBindings(this.artifact.bindings, 'bindings');
    for (const [index, action] of this.artifact.actions.entries()) this.validateAction(action, `actions.${index}`);
    this.validateTreeCycles();
  }

  private buildDependencyGraph(): void {
    const elements = isRecord(this.artifact.elements) ? this.artifact.elements as Record<string, DesignerDocumentNode> : {};
    const compiled = new Map<string, readonly string[]>();
    for (const [nodeId, node] of Object.entries(elements)) {
      const dependencies = new Set<string>();
      for (const value of [node.props, node.layout, node.style, node.bindings, node.events]) {
        for (const resourceId of readResourceIds(value)) {
          dependencies.add(resourceId);
          const nodes = this.dependencyGraph.get(resourceId) ?? new Set<string>();
          nodes.add(nodeId);
          this.dependencyGraph.set(resourceId, nodes);
        }
      }
      compiled.set(nodeId, [...dependencies]);
    }
    this.compiledDependencies.compile(compiled, this.diagnostics);
  }

  private createBindingContext(state: RuntimeScopeState): import('./BindingResolver').RuntimeBindingContext {
    return {
      componentValues: state.scopes.component,
      form: state.scopes.form,
      page: state.scopes.page,
      resources: state.resources,
      resourceProviders: state.resourceProviders,
      scopeIds: state.scopeIds,
      scopes: state.scopes,
      scopeStore: state.scopeStore,
      variables: state.variables
    };
  }

  private validateAction(action: unknown, path: string): void {
    if (!isRecord(action) || !Array.isArray(action.steps)) {
      this.diagnostics.error('invalidAction', 'Runtime action must contain steps', path);
      return;
    }
    this.validateBindings(action, path);
  }

  private validateBindings(value: unknown, path: string): void {
    if (Array.isArray(value)) {
      value.forEach((item, index) => this.validateBindings(item, `${path}.${index}`));
      return;
    }
    if (!isRecord(value)) return;
    if ('resourceId' in value && (typeof value.resourceId !== 'string' || !value.resourceId.trim())) {
      this.diagnostics.error('invalidBinding', 'Binding resourceId must be a non-empty string', path);
    }
    if ('path' in value || 'source' in value) this.diagnostics.error('invalidBinding', 'Retired source/path bindings are not supported by the latest runtime contract', path);
    if (path.includes('.bindings.props')) this.diagnostics.error('invalidBinding', 'Property bindings must be stored in props/layout/style', path);
    if ('conversionPipeline' in value) {
      const pipeline = value.conversionPipeline;
      if (!Array.isArray(pipeline)) {
        this.diagnostics.error('invalidConversionPipeline', 'Binding conversionPipeline must be an array', `${path}.conversionPipeline`);
      } else {
        pipeline.forEach((step, index) => {
          const stepPath = `${path}.conversionPipeline.${index}`;
          if (!isRecord(step) || !isNonEmptyString(step.name) || !isNonEmptyString(step.from) || !isNonEmptyString(step.to)) {
            this.diagnostics.error('invalidConversionPipeline', 'Conversion steps must declare non-empty from, name, and to fields', stepPath);
          } else if (!RUNTIME_CAPABILITY_CONTRACT.converters.includes(step.name)) {
            this.diagnostics.error('unknownConverter', `Unknown runtime binding converter: ${step.name}`, `${stepPath}.name`);
          }
        });
      }
    }
    for (const [key, nested] of Object.entries(value)) this.validateBindings(nested, `${path}.${key}`);
  }

  private validateTreeCycles(): void {
    const visiting = new Set<string>();
    const visited = new Set<string>();
    const visit = (nodeId: string): void => {
      if (visiting.has(nodeId)) {
        this.diagnostics.error('cyclicTree', `Runtime element tree contains a cycle at ${nodeId}`, `elements.${nodeId}`);
        return;
      }
      if (visited.has(nodeId)) return;
      visiting.add(nodeId);
      for (const child of this.artifact.elements[nodeId]?.children ?? []) if (this.artifact.elements[child]) visit(child);
      visiting.delete(nodeId);
      visited.add(nodeId);
    };
    for (const nodeId of Object.keys(isRecord(this.artifact.elements) ? this.artifact.elements : {})) visit(nodeId);
  }

  private validateActions(): void {
    for (const [index, action] of this.artifact.actions.entries()) {
      if (!isRecord(action) || !Array.isArray(action.steps)) {
        this.diagnostics.error('invalidAction', 'Runtime artifact action must contain steps', `actions.${index}`);
        continue;
      }
      for (const [stepIndex, step] of action.steps.entries()) {
        if (!isRecord(step) || typeof step.type !== 'string' ||
            !RUNTIME_CAPABILITY_CONTRACT.actions.includes(step.type) || !this.actions.has(step.type)) {
          this.diagnostics.error('unknownAction', `Unknown runtime action: ${isRecord(step) ? String(step.type ?? '') : ''}`, `actions.${index}.steps.${stepIndex}`);
        }
      }
    }
    for (const node of Object.values(this.artifact.elements)) {
      for (const [eventIndex, action] of (node.events ?? []).entries()) {
        if (!isRecord(action) || !Array.isArray(action.steps)) continue;
        for (const step of action.steps) if (!isRecord(step) || typeof step.type !== 'string' ||
            !RUNTIME_CAPABILITY_CONTRACT.actions.includes(step.type) || !this.actions.has(step.type)) {
          this.diagnostics.error('unknownAction', `Unknown runtime action: ${isRecord(step) ? String(step.type ?? '') : ''}`, `elements.${node.id}.events.${eventIndex}`);
        }
      }
    }
  }

  private validateManifestDeclarations(): void {
    const declarations = this.artifact.manifest;
    if (!Array.isArray(declarations) || declarations.length === 0) {
      this.diagnostics.error('missingManifestDeclarations', 'Runtime artifact manifest declarations are required and cannot be empty', 'manifest');
      return;
    }

    for (const [index, declaration] of declarations.entries()) {
      const type = isRecord(declaration) && typeof declaration.type === 'string' ? declaration.type.trim() : '';
      if (!type) {
        this.diagnostics.error('invalidManifestDeclaration', 'Runtime artifact manifest declaration type is required', `manifest.${index}.type`);
        continue;
      }
      if (!RUNTIME_CAPABILITY_CONTRACT.components.includes(type) || !this.options.manifests.has(type)) {
        this.diagnostics.error('unknownManifest', `Unknown component manifest declaration: ${type}`, `manifest.${index}.type`);
      }
      const renderer = isRecord(declaration) ? declaration.renderer : undefined;
      if (!isRecord(renderer) ||
          renderer.runtime !== RUNTIME_CAPABILITY_CONTRACT.renderer || renderer.preview !== RUNTIME_CAPABILITY_CONTRACT.renderer) {
        this.diagnostics.error('unknownRuntimeRenderer', `Runtime artifact renderer contract is not supported: ${type}`, `manifest.${index}.renderer`);
      }
    }
  }
}

function readResourceIds(value: unknown): readonly string[] {
  const ids = new Set<string>();
  const visit = (current: unknown): void => {
    if (Array.isArray(current)) {
      current.forEach(visit);
      return;
    }
    if (!isRecord(current)) return;
    if (typeof current.resourceId === 'string' && current.resourceId.trim()) ids.add(current.resourceId);
    Object.values(current).forEach(visit);
  };
  visit(value);
  return [...ids];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

function isRuntimeScope(value: string): boolean {
  return (RUNTIME_SCOPE_NAMES as readonly string[]).includes(value);
}

function normalizeScopeState(state: RuntimeScopeState): RuntimeScopeState | null {
  if (!isRecord(state) || !isRecord(state.scopes) || !isRecord(state.variables)) return null;
  const scopes = { ...state.scopes };
  for (const scope of Object.keys(scopes)) {
    if (!isRuntimeScope(scope) || !isRecord(scopes[scope])) return null;
  }
  if (state.scopeIds && (!isRecord(state.scopeIds) || Object.values(state.scopeIds).some((scopeId) => typeof scopeId !== 'string' || !scopeId.trim()))) return null;
  return { ...state, scopes };
}

function resolvePermission(value: Record<string, unknown> | undefined): { code?: string | null; visibleWhen?: string | Record<string, unknown> | null } | undefined {
  if (!value) return undefined;
  return { code: typeof value.code === 'string' ? value.code : null, visibleWhen: value.visibleWhen as string | Record<string, unknown> | null | undefined };
}

function toArtifactContext(artifact: Pick<RuntimeArtifact, 'artifactHash' | 'compilerVersion' | 'documentId' | 'revision'>): RuntimeArtifactContext {
  return {
    artifactHash: typeof artifact.artifactHash === 'string' ? artifact.artifactHash : '',
    compilerVersion: typeof artifact.compilerVersion === 'string' ? artifact.compilerVersion : '',
    documentId: typeof artifact.documentId === 'string' ? artifact.documentId : '',
    revision: typeof artifact.revision === 'number' ? artifact.revision : 0
  };
}

function validateMonitoringContext(context: RuntimeMonitoringContext): { code: string; message: string; path: string; severity: 'error' } | null {
  const required: Array<keyof RuntimeMonitoringContext> = ['tenantId', 'appCode', 'pageCode', 'userId', 'traceId'];
  const missing = required.filter((field) => typeof context[field] !== 'string' || !context[field]?.trim());
  return missing.length === 0
    ? null
    : { code: 'missingMonitoringContext', message: `Runtime monitoring context is missing: ${missing.join(', ')}`, path: 'monitoringContext', severity: 'error' };
}

function now(): number {
  return typeof performance !== 'undefined' ? performance.now() : Date.now();
}
