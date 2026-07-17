import type { ExpressionValue } from '../../api/runtime/expression-value.latest';
import { isCanonicalExpressionValue } from '../../api/runtime/expressionValue';
import type { DesignerDocumentNode } from '../../pages/application-console/development-center/low-code-studio/document/DesignerDocument';
import type { RuntimeActionFlow, RuntimeDesignerElement } from '../RuntimeTypes';

export const RUNTIME_RENDER_SURFACE_ATTRIBUTE = 'data-runtime-surface';
export const RUNTIME_RENDER_SURFACE = {
  business: 'business',
  overlay: 'overlay'
} as const;
export const RUNTIME_LAYOUT_DIAGNOSTICS_ATTRIBUTE = 'data-runtime-layout-diagnostics';
export const RUNTIME_BUSINESS_ROOT_ATTRIBUTE = 'data-runtime-business-root';
export const RUNTIME_BUSINESS_NODE_ATTRIBUTE = 'data-runtime-business-node';
export const DESIGNER_OVERLAY_ATTRIBUTE = 'data-designer-overlay';
export const DESIGNER_OVERLAY_ROOT_ATTRIBUTE = 'data-designer-overlay-root';
export const DESIGNER_TRANSIENT_OVERLAY_ATTRIBUTE = 'data-canvas-transient-overlay';

export type RuntimeRenderSurface = typeof RUNTIME_RENDER_SURFACE[keyof typeof RUNTIME_RENDER_SURFACE];

export interface RuntimeRenderBoundaryNode {
  bindings: Record<string, unknown>;
  children: readonly string[];
  disabled: boolean;
  id: string;
  layout: Record<string, unknown>;
  loading: boolean;
  permission?: unknown;
  props: Record<string, unknown>;
  readOnly: boolean;
  style: Record<string, unknown>;
  type: string;
  visible: boolean;
}

/** Maps the Designer node into the exact, resolved shape accepted by runtime renderers. */
export function mapDesignerNodeToRuntimeElement(source: DesignerDocumentNode, node: RuntimeRenderBoundaryNode): RuntimeDesignerElement {
  const permission = mapRuntimePermission(node.permission);
  return {
    bindings: { ...node.bindings },
    children: [...node.children],
    ...(permission ? { permission } : {}),
    events: source.events.map(mapRuntimeAction).filter((action): action is RuntimeActionFlow => action !== null),
    id: node.id,
    layout: { ...node.layout },
    name: source.name?.trim() || node.type,
    parentId: source.parentId,
    props: { ...node.props },
    style: { ...node.style },
    type: node.type,
    ...(source.validation ? { validation: source.validation.map((item) => ({ ...item })) } : {})
  };
}

function mapRuntimeAction(value: Record<string, unknown>): RuntimeActionFlow | null {
  const id = readNonEmptyString(value.id);
  const trigger = readNonEmptyString(value.trigger);
  const rawSteps = value.steps;
  const steps = Array.isArray(rawSteps)
    ? rawSteps.map(mapRuntimeStep).filter((step): step is RuntimeActionFlow['steps'][number] => step !== null)
    : null;
  if (!id || !trigger || !steps || !Array.isArray(rawSteps) || steps.length !== rawSteps.length) return null;
  const condition = readRuntimeExpression(value.condition);
  const permissionCode = readRuntimeExpression(value.permissionCode);
  const errorPolicy = value.errorPolicy === 'continue' || value.errorPolicy === 'stop' ? value.errorPolicy : undefined;
  return {
    ...(condition === undefined ? {} : { condition }),
    ...(errorPolicy === undefined ? {} : { errorPolicy }),
    id,
    name: readNonEmptyString(value.name) ?? id,
    ...(permissionCode === undefined ? {} : { permissionCode }),
    steps,
    trigger
  };
}

function mapRuntimeStep(value: unknown): RuntimeActionFlow['steps'][number] | null {
  if (!isRecord(value)) return null;
  const id = readNonEmptyString(value.id);
  const type = readNonEmptyString(value.type);
  if (!id || !type) return null;
  const config = isRecord(value.config) ? { ...value.config } : undefined;
  return config ? { config, id, type } : { id, type };
}

function mapRuntimePermission(value: unknown): RuntimeDesignerElement['permission'] | undefined {
  if (!isRecord(value)) return undefined;
  const visibleWhen = readRuntimeExpression(value.visibleWhen);
  return {
    code: typeof value.code === 'string' ? value.code : null,
    ...(visibleWhen === undefined ? {} : { visibleWhen })
  };
}

function readRuntimeExpression(value: unknown): ExpressionValue | string | null | undefined {
  if (value === null) return null;
  if (typeof value === 'string') return value;
  return isCanonicalExpressionValue(value) ? value : undefined;
}

function readNonEmptyString(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value.trim() : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}
