import type { DesignerDocumentNode } from '../pages/application-console/development-center/low-code-studio/document/DesignerDocument';
import { DEFAULT_RESPONSIVE_BREAKPOINTS, resolveResponsiveNode, type ResponsiveBreakpoint, type ResponsiveSections } from '../pages/application-console/development-center/low-code-studio/responsive/responsiveModel';

export interface RuntimeLayoutContext {
  breakpoint?: string;
  breakpoints?: readonly ResponsiveBreakpoint[];
  viewport?: { height: number; width: number };
}

export class LayoutResolver {
  public resolveSections(node: DesignerDocumentNode, context: RuntimeLayoutContext = {}): Required<ResponsiveSections> {
    const base = { layout: node.layout ?? {}, props: node.props ?? {}, style: node.style ?? {} };
    const breakpoint = context.breakpoint ? (context.breakpoints ?? DEFAULT_RESPONSIVE_BREAKPOINTS).find((candidate) => candidate.id === context.breakpoint) ?? { id: context.breakpoint, minWidth: 0 } : null;
    const sections = breakpoint
      ? resolveResponsiveNode({ base, responsiveOverrides: node.responsiveOverrides ?? {} }, breakpoint, context.breakpoints ?? DEFAULT_RESPONSIVE_BREAKPOINTS)
      : base;
    return {
      layout: applyViewport(sections.layout ?? {}, context.viewport),
      props: sections.props ?? {},
      style: sections.style ?? {}
    };
  }

  public resolve(node: DesignerDocumentNode, context: RuntimeLayoutContext = {}): Record<string, unknown> {
    return this.resolveSections(node, context).layout;
  }
}

function applyViewport(layout: Record<string, unknown>, viewport: RuntimeLayoutContext['viewport']): Record<string, unknown> {
  if (!viewport) return layout;
  return Object.fromEntries(Object.entries(layout).map(([key, value]) => [key, resolveViewportValue(value, viewport)]));
}

function resolveViewportValue(value: unknown, viewport: NonNullable<RuntimeLayoutContext['viewport']>): unknown {
  if (typeof value !== 'string') return value;
  if (value === '100vw') return viewport.width;
  if (value === '100vh') return viewport.height;
  return value;
}
