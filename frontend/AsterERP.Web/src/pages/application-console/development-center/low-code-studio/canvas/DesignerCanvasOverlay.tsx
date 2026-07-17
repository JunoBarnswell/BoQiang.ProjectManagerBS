import { Children, cloneElement, isValidElement, type CSSProperties, type ReactNode } from 'react';

import {
  DESIGNER_OVERLAY_ATTRIBUTE,
  DESIGNER_OVERLAY_ROOT_ATTRIBUTE,
  DESIGNER_TRANSIENT_OVERLAY_ATTRIBUTE,
  RUNTIME_RENDER_SURFACE,
  RUNTIME_RENDER_SURFACE_ATTRIBUTE
} from '../../../../../runtime-kernel/runtime-contract/RuntimeRenderBoundaryContract';

export interface DesignerCanvasOverlayProps {
  children: ReactNode;
  label?: string;
}

const DESIGNER_CANVAS_OVERLAY_STYLE: CSSProperties = {
  contain: 'layout paint',
  inset: 0,
  pointerEvents: 'none',
  position: 'absolute',
  zIndex: 10
};

function markOverlayChildren(children: ReactNode): ReactNode {
  return Children.map(children, (child) => isValidElement(child)
    ? cloneElement(child, {
      [DESIGNER_OVERLAY_ATTRIBUTE]: 'true',
      [DESIGNER_TRANSIENT_OVERLAY_ATTRIBUTE]: 'true',
      [RUNTIME_RENDER_SURFACE_ATTRIBUTE]: RUNTIME_RENDER_SURFACE.overlay
    } as Record<string, unknown>)
    : child);
}

/** Owns designer-only interaction DOM without participating in the business layout tree. */
export function DesignerCanvasOverlay({ children, label }: DesignerCanvasOverlayProps): ReactNode {
  return <div aria-label={label} style={DESIGNER_CANVAS_OVERLAY_STYLE} {...{
    [DESIGNER_OVERLAY_ATTRIBUTE]: 'true',
    [DESIGNER_OVERLAY_ROOT_ATTRIBUTE]: 'true',
    [DESIGNER_TRANSIENT_OVERLAY_ATTRIBUTE]: 'true',
    [RUNTIME_RENDER_SURFACE_ATTRIBUTE]: RUNTIME_RENDER_SURFACE.overlay
  }}>{markOverlayChildren(children)}</div>;
}
