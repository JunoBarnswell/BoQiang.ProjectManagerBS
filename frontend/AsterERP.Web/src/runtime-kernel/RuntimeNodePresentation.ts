import { cloneElement, isValidElement, type CSSProperties, type ReactNode, type ReactElement } from 'react';

import { RUNTIME_BUSINESS_NODE_ATTRIBUTE, RUNTIME_LAYOUT_DIAGNOSTICS_ATTRIBUTE, RUNTIME_RENDER_SURFACE, RUNTIME_RENDER_SURFACE_ATTRIBUTE } from './runtime-contract/RuntimeRenderBoundaryContract';
import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { projectRuntimeLayout } from './RuntimeLayoutProjection';

export function applyRuntimeNodePresentation(context: RuntimeComponentRenderContext, node: ReactNode): ReactNode {
  if (!isValidElement(node)) return node;
  const element = node as ReactElement<Record<string, unknown>>;
  const existingStyle = isRecord(element.props.style) ? element.props.style as CSSProperties : {};
  const parentId = context.element.parentId;
  const parentLayout = parentId ? context.runtime.document.elements[parentId]?.layout : undefined;
  const projected = projectRuntimeLayout({ layout: context.layout, parentLayout, style: context.style });
  const layoutDiagnostics = projected.diagnostics.map((diagnostic) => `${diagnostic.field}:${diagnostic.code}`).join('|');
  return cloneElement(element, {
    'aria-busy': context.loading || undefined,
    'aria-disabled': context.disabled || undefined,
    'aria-readonly': context.readOnly || undefined,
    'data-runtime-component': context.componentType,
    'data-runtime-element-id': context.element.id,
    [RUNTIME_BUSINESS_NODE_ATTRIBUTE]: 'true',
    'data-runtime-layout': context.componentType.startsWith('layout.') ? context.componentType.slice('layout.'.length) : undefined,
    'data-runtime-loading': context.loading ? 'true' : undefined,
    [RUNTIME_LAYOUT_DIAGNOSTICS_ATTRIBUTE]: layoutDiagnostics || undefined,
    [RUNTIME_RENDER_SURFACE_ATTRIBUTE]: RUNTIME_RENDER_SURFACE.business,
    hidden: context.visible === false || undefined,
    style: { ...projected.style, ...existingStyle }
  });
}

export function toRuntimeStyle(layout: Record<string, unknown>, style: Record<string, unknown>): CSSProperties {
  return projectRuntimeLayout({ layout, style }).style as CSSProperties;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}
