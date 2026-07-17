import { expressionValueToGraph, isCanonicalExpressionValue } from '../../../../../api/runtime/expressionValue';
import { RUNTIME_CAPABILITY_CONTRACT } from '../../../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract';
import { RUNTIME_LAYOUT_DIAGNOSTICS_ATTRIBUTE, RUNTIME_RENDER_SURFACE, RUNTIME_RENDER_SURFACE_ATTRIBUTE } from '../../../../../runtime-kernel/runtime-contract/RuntimeRenderBoundaryContract';
import { projectRuntimeLayout } from '../../../../../runtime-kernel/RuntimeLayoutProjection';
import { validateBindingExpression } from '../binding/bindingTypes';
import { hasStableResource, listStableResources } from '../binding/resourceExplorerStore';
import { ComponentRegistry } from '../components/ComponentRegistry';
import type { DesignerDocument } from '../document/DesignerDocument';
import { diagnoseExpressionGraph } from '../expression/expressionGraph';
import { normalizeResourceId, type DesignerVariableExpression, type DesignerValueType } from '../expression/expressionTypes';
import { latestComponentInspectorRegistry } from '../inspector/registry/latestComponentInspectorRegistry';

import { diagnosePageStudioLayoutNode } from './PageStudioLayoutDiagnostics';

export interface PageStudioDiagnostic {
  code: string;
  elementId?: string;
  messageArgs?: Readonly<Record<string, string | number>>;
  messageKey?: string;
  path?: string;
  message: string;
  severity: 'error' | 'warning';
}

const INTERACTION_BOOLEAN_FIELDS = ['disabled', 'loading', 'readOnly', 'visible'] as const;

export function diagnosePageStudioDocument(document: DesignerDocument, registry: ComponentRegistry): PageStudioDiagnostic[] {
  const diagnostics: PageStudioDiagnostic[] = [];
  const resourceKeys: ReadonlySet<string> = new Set(listStableResources(document).map((resource) => normalizeResourceId(resource.id, resource.resourceType)));
  for (const node of Object.values(document.elements)) {
    diagnostics.push(...diagnosePageStudioLayoutNode(document, node).map((diagnostic) => ({
      ...diagnostic,
      elementId: node.id,
      path: diagnostic.path.startsWith('elements.') ? diagnostic.path : `elements.${node.id}.${diagnostic.path}`
    })));
    inspectInteractionState(node, diagnostics);
    if (!registry.get(node.type)) {
      diagnostics.push({ code: 'unknown-component', elementId: node.id, message: `组件 ${node.type} 没有可用的最新组件清单。`, severity: 'error' });
      continue;
    }
    const manifest = registry.get(node.type)!;
    if (!manifest.runtime.renderer.trim()) {
      diagnostics.push({ code: 'missing-runtime-renderer', elementId: node.id, message: `组件 ${node.type} 缺少运行时 renderer。`, severity: 'error' });
    }
    inspectNodeRuntimeBoundary(node, manifest.capability.acceptsChildren, document, diagnostics);
    inspectBindings(node.props, `elements.${node.id}.props`, node.id, resourceKeys, diagnostics, node.type);
    inspectBindings(node.layout, `elements.${node.id}.layout`, node.id, resourceKeys, diagnostics, node.type);
    inspectBindings(node.style, `elements.${node.id}.style`, node.id, resourceKeys, diagnostics, node.type);
    inspectBindings(node.bindings, `elements.${node.id}.bindings`, node.id, resourceKeys, diagnostics, node.type);
    inspectBindings(node.events, `elements.${node.id}.events`, node.id, resourceKeys, diagnostics, node.type);
    inspectActions(node.events, `elements.${node.id}.events`, node.id, diagnostics);
  }
  inspectBindings(document.actions, 'actions', undefined, resourceKeys, diagnostics);
  inspectActions(document.actions, 'actions', undefined, diagnostics);
  inspectBindings(document.apiBindings, 'apiBindings', undefined, resourceKeys, diagnostics);
  inspectBindings(document.dataSources, 'dataSources', undefined, resourceKeys, diagnostics);
  inspectBindings(document.pageMicroflows, 'pageMicroflows', undefined, resourceKeys, diagnostics);
  inspectBindings(document.workflowBindings, 'workflowBindings', undefined, resourceKeys, diagnostics);
  inspectPageMicroflows(document, diagnostics);
  diagnostics.push(...detectBindingCycles(document));
  if (!document.documentHash) {
    diagnostics.push({ code: 'missing-document-hash', message: '文档尚未生成完整 hash。', severity: 'error' });
  }
  return diagnostics.map((diagnostic) => ({
    ...diagnostic,
    messageArgs: { detail: diagnostic.message, path: diagnostic.path ?? diagnostic.elementId ?? 'document' },
    messageKey: `lowCode.pageStudio.diagnostic.${diagnostic.code}`
  }));
}

export function diagnosePageStudioRuntimeDom(root: ParentNode): PageStudioDiagnostic[] {
  const diagnostics: PageStudioDiagnostic[] = [];
  const previews = [...root.querySelectorAll<HTMLElement>('[data-designer-runtime-preview="true"]')];
  previews.forEach((preview) => {
    if (!preview.classList.contains('page-studio__runtime-preview')) {
      diagnostics.push({ code: 'runtime-preview-boundary-missing', path: 'runtimePreview', message: '运行态预览必须使用独立的业务 DOM 边界。', severity: 'error' });
    }
    preview.querySelectorAll<HTMLElement>('[data-runtime-element-id]').forEach((element) => {
      if (element.getAttribute(RUNTIME_RENDER_SURFACE_ATTRIBUTE) !== RUNTIME_RENDER_SURFACE.business) {
        diagnostics.push({ code: 'runtime-business-surface-missing', elementId: element.dataset.runtimeElementId, path: `runtimePreview.${element.dataset.runtimeElementId ?? 'unknown'}`, message: '运行态业务 DOM 缺少 business surface 标记。', severity: 'error' });
      }
      if (element.closest('[data-canvas-transient-overlay="true"]')) {
        diagnostics.push({ code: 'runtime-dom-inside-overlay', elementId: element.dataset.runtimeElementId, path: `runtimePreview.${element.dataset.runtimeElementId ?? 'unknown'}`, message: '运行态业务 DOM 不得嵌套在设计态 overlay 内。', severity: 'error' });
      }
      if (!element.hasAttribute(RUNTIME_LAYOUT_DIAGNOSTICS_ATTRIBUTE)) return;
      const details = element.getAttribute(RUNTIME_LAYOUT_DIAGNOSTICS_ATTRIBUTE)?.trim();
      if (details) diagnostics.push({ code: 'runtime-layout-invalid', elementId: element.dataset.runtimeElementId, path: `runtimePreview.${element.dataset.runtimeElementId ?? 'unknown'}.layout`, message: `运行态布局无效：${details}`, severity: 'error' });
    });
  });
  root.querySelectorAll<HTMLElement>('[data-canvas-transient-overlay="true"] [data-runtime-element-id]').forEach((element) => {
    diagnostics.push({ code: 'runtime-dom-inside-overlay', elementId: element.dataset.runtimeElementId, path: `overlay.${element.dataset.runtimeElementId ?? 'unknown'}`, message: '设计态 overlay 不得承载运行态业务 DOM。', severity: 'error' });
  });
  return finalizeDiagnostics(diagnostics);
}

function inspectNodeRuntimeBoundary(node: DesignerDocument['elements'][string], acceptsChildren: boolean, document: DesignerDocument, diagnostics: PageStudioDiagnostic[]): void {
  if (!acceptsChildren && node.children.length > 0) {
    diagnostics.push({ code: 'children-not-supported', elementId: node.id, path: `elements.${node.id}.children`, message: `组件 ${node.type} 的运行时 renderer 不接受 children。`, severity: 'error' });
  }
  if (new Set(node.children).size !== node.children.length) {
    diagnostics.push({ code: 'duplicate-child', elementId: node.id, path: `elements.${node.id}.children`, message: '组件 children 不得包含重复节点。', severity: 'error' });
  }
  node.children.forEach((childId, index) => {
    const child = document.elements[childId];
    if (!child) diagnostics.push({ code: 'missing-child', elementId: node.id, path: `elements.${node.id}.children.${index}`, message: `组件引用了不存在的子节点：${childId}`, severity: 'error' });
    else if (child.parentId !== node.id) diagnostics.push({ code: 'parent-child-mismatch', elementId: node.id, path: `elements.${node.id}.children.${index}`, message: `子节点 ${childId} 的 parentId 与当前节点不一致。`, severity: 'error' });
  });
  const projected = projectRuntimeLayout({ layout: node.layout ?? {}, style: node.style ?? {} });
  projected.diagnostics.forEach((diagnostic) => diagnostics.push({ code: 'invalid-runtime-layout', elementId: node.id, path: `elements.${node.id}.layout.${diagnostic.field}`, message: diagnostic.message, severity: 'error' }));
}

function inspectInteractionState(node: DesignerDocument['elements'][string], diagnostics: PageStudioDiagnostic[]): void {
  for (const field of INTERACTION_BOOLEAN_FIELDS) {
    const value = node.props?.[field];
    if (value === undefined || typeof value === 'boolean' || isRecord(value) && asDesignerExpression(value)) continue;
    diagnostics.push({ code: 'interaction-state-invalid', elementId: node.id, path: `elements.${node.id}.props.${field}`, message: `交互状态 ${field} 必须是布尔值或有效绑定表达式。`, severity: 'error' });
  }
}

function inspectPageMicroflows(document: DesignerDocument, diagnostics: PageStudioDiagnostic[]): void {
  const aliases = new Set<string>();
  (document.pageMicroflows ?? []).forEach((binding, index) => {
    const alias = text(binding.alias);
    const flowCode = text(binding.flowCode);
    const path = `pageMicroflows.${index}`;
    if (!alias) diagnostics.push({ code: 'page-microflow-alias-missing', path, message: '页面微流缺少别名。', severity: 'error' });
    else if (aliases.has(alias)) diagnostics.push({ code: 'page-microflow-alias-duplicate', path: `${path}.alias`, message: `页面微流别名重复：${alias}`, severity: 'error' });
    else aliases.add(alias);
    if (!flowCode) diagnostics.push({ code: 'page-microflow-code-missing', path: `${path}.flowCode`, message: '页面微流缺少 flowCode。', severity: 'error' });
  });
}

function inspectActions(value: unknown, path: string, elementId: string | undefined, diagnostics: PageStudioDiagnostic[]): void {
  if (Array.isArray(value)) {
    value.forEach((item, index) => inspectActions(item, `${path}.${index}`, elementId, diagnostics));
    return;
  }
  if (!isRecord(value)) return;

  if (typeof value.type === 'string' && ('config' in value || 'id' in value)) {
    const type = value.type.trim();
    const isActionStep = path.includes('.steps.');
    if (!type || !RUNTIME_CAPABILITY_CONTRACT.actions.includes(type)) {
      diagnostics.push({ code: 'unknown-action', elementId, path: `${path}.type`, message: `动作未注册：${value.type}`, severity: 'error' });
    } else if (!RUNTIME_CAPABILITY_CONTRACT.actionManifests[type]) {
      diagnostics.push({ code: 'missing-action-manifest', elementId, path: `${path}.type`, message: `动作缺少契约：${type}`, severity: 'error' });
    } else if (!isActionStep && !Array.isArray(value.steps)) {
      diagnostics.push({ code: 'action-steps-required', elementId, path, message: `动作必须包含步骤：${path}`, severity: 'error' });
    }
  }

  Object.entries(value).forEach(([key, child]) => inspectActions(child, `${path}.${key}`, elementId, diagnostics));
}

function inspectBindings(value: unknown, path: string, elementId: string | undefined, resourceKeys: ReadonlySet<string>, diagnostics: PageStudioDiagnostic[], componentType?: string): void {
  if (Array.isArray(value)) {
    value.forEach((item, index) => inspectBindings(item, `${path}.${index}`, elementId, resourceKeys, diagnostics, componentType));
    return;
  }
  if (!isRecord(value)) return;

  const expression = asDesignerExpression(value);
  if (expression) validateExpression(expression, path, elementId, resourceKeys, diagnostics, componentType);
  Object.entries(value).forEach(([key, child]) => inspectBindings(child, `${path}.${key}`, elementId, resourceKeys, diagnostics, componentType));
}

function validateExpression(expression: DesignerVariableExpression, path: string, elementId: string | undefined, resourceKeys: ReadonlySet<string>, diagnostics: PageStudioDiagnostic[], componentType?: string): void {
  const resourceIds = new Set([expression.resourceId?.trim() ?? '', ...readResourceIds(expression.graph)]);
  for (const resourceId of resourceIds) {
    const canonicalResourceId = resourceId ? normalizeResourceId(resourceId, expression.resourceType) : '';
    if (canonicalResourceId && !hasStableResource(resourceKeys, canonicalResourceId)) {
      diagnostics.push({ code: 'missing-resource', elementId, path, message: `绑定资源不存在：${resourceId}`, severity: 'error' });
    diagnostics.push({ code: 'missing-resource', elementId, path, message: `绑定资源不存在：${resourceId}`, severity: 'error' });
    }
  }

  const pipeline = expression.conversionPipeline ?? [];
  pipeline.forEach((step, index) => {
    if (!RUNTIME_CAPABILITY_CONTRACT.converters.includes(step.name)) {
      diagnostics.push({ code: 'missing-converter', elementId, path: `${path}.conversionPipeline.${index}.name`, message: `绑定转换器未注册：${step.name}`, severity: 'error' });
    }
  });

  const expectedType = expectedBindingType(path, expression, componentType);
  const validation = validateBindingExpression(expression, expectedType);
  if (!validation.valid) {
    diagnostics.push({ code: 'incompatible-binding', elementId, path, message: `绑定不可用：${validation.reason}`, severity: 'error' });
  }
  if (expression.graph) {
    diagnoseExpressionGraph(expression.graph, expectedType).forEach((diagnostic) => {
      diagnostics.push({ code: `expression-${diagnostic.code}`, elementId, path: `${path}.${diagnostic.path}`, message: diagnostic.message, severity: diagnostic.severity });
    });
  }
}

function finalizeDiagnostics(diagnostics: PageStudioDiagnostic[]): PageStudioDiagnostic[] {
  return diagnostics.map((diagnostic) => ({
    ...diagnostic,
    messageArgs: { detail: diagnostic.message, path: diagnostic.path ?? diagnostic.elementId ?? 'document' },
    messageKey: `lowCode.pageStudio.diagnostic.${diagnostic.code}`
  }));
}

function expectedBindingType(path: string, expression: DesignerVariableExpression, componentType?: string): DesignerValueType {
  const match = path.match(/^elements\.([^.]+)\.(props|layout|style|bindings)\..+$/);
  const fieldKey = match ? path.replace(/^elements\.[^.]+\./, '') : '';
  const propertyType = match && componentType ? latestComponentInspectorRegistry.get(componentType)?.properties.find((property) => property.path === fieldKey)?.valueType : undefined;
  return propertyType
    ?? expression.expectedType
    ?? expression.graph?.root?.valueType
    ?? expression.conversionPipeline?.at(-1)?.to
    ?? 'json';
}

function asDesignerExpression(value: Record<string, unknown>): DesignerVariableExpression | null {
  if (isCanonicalExpressionValue(value)) {
    return {
      expectedType: value.dataType,
      fallback: value.fallback,
      graph: expressionValueToGraph(value),
      helpers: []
    };
  }
  if (typeof value.resourceId === 'string' || value.graph !== undefined || value.conversionPipeline !== undefined) {
    return value as unknown as DesignerVariableExpression;
  }
  return null;
}

function detectBindingCycles(document: DesignerDocument): PageStudioDiagnostic[] {
  const aliases = new Map<string, string>();
  const dependencies = new Map<string, Set<string>>();
  const register = (key: string, path: string, value: unknown) => {
    aliases.set(key, path);
    const refs = new Set(readResourceIds(value));
    dependencies.set(key, refs);
  };
  document.variables.forEach((variable, index) => {
    const id = text(variable.id);
    if (id) register(`variables:${id}`, `variables.${index}`, variable);
  });
  document.pageParameters.forEach((parameter, index) => {
    const code = text(parameter.code);
    if (code) register(`page:${parameter.direction === 'output' ? 'outputs' : 'inputs'}.${code}`, `pageParameters.${index}`, parameter);
  });
  document.apiBindings.forEach((binding, index) => { const id = text(binding.id); if (id) register(`api:${id}`, `apiBindings.${index}`, binding); });
  document.workflowBindings.forEach((binding, index) => { const id = text(binding.id); if (id) register(`workflow:${id}`, `workflowBindings.${index}`, binding); });

  const diagnostics: PageStudioDiagnostic[] = [];
  const visiting = new Set<string>();
  const visited = new Set<string>();
  const visit = (key: string, stack: string[]) => {
    if (visiting.has(key)) {
      const cycle = [...stack.slice(stack.indexOf(key)), key];
      diagnostics.push({ code: 'cyclic-binding', path: aliases.get(key), message: `绑定存在循环依赖：${cycle.join(' -> ')}`, severity: 'error' });
      return;
    }
    if (visited.has(key)) return;
    visiting.add(key);
    for (const dependency of dependencies.get(key) ?? []) if (dependencies.has(dependency)) visit(dependency, [...stack, key]);
    visiting.delete(key);
    visited.add(key);
  };
  for (const key of dependencies.keys()) visit(key, []);
  return diagnostics;
}

function readResourceIds(value: unknown): string[] {
  const ids = new Set<string>();
  const visit = (current: unknown): void => {
    if (Array.isArray(current)) { current.forEach(visit); return; }
    if (!isRecord(current)) return;
    if (typeof current.resourceId === 'string' && current.resourceId.trim()) ids.add(current.resourceId.trim());
    Object.values(current).forEach(visit);
  };
  visit(value);
  return [...ids];
}

function text(value: unknown): string { return typeof value === 'string' ? value.trim() : ''; }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
