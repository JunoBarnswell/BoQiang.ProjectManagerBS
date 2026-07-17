// @vitest-environment jsdom

import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { findCanvasElementAtPoint } from './canvasHitTesting';
import { DesignerCanvasOverlay } from './DesignerCanvasOverlay';

describe('DesignerCanvasOverlay', () => {
  it('marks the overlay root and direct interaction children outside the business surface', () => {
    const { container } = render(<DesignerCanvasOverlay label="Canvas overlays"><button data-runtime-surface="business" type="button">Resize</button></DesignerCanvasOverlay>);
    const root = container.firstElementChild as HTMLElement;
    const child = root.firstElementChild as HTMLElement;

    expect(root.dataset.designerOverlayRoot).toBe('true');
    expect(root.dataset.canvasTransientOverlay).toBe('true');
    expect(root.dataset.runtimeSurface).toBe('overlay');
    expect(root.style.position).toBe('absolute');
    expect(root.style.pointerEvents).toBe('none');
    expect(root.style.contain).toBe('layout paint');
    expect(child.dataset.designerOverlay).toBe('true');
    expect(child.dataset.canvasTransientOverlay).toBe('true');
    expect(child.dataset.runtimeSurface).toBe('overlay');
  });

  it('excludes transient overlay descendants from canvas hit testing while preserving business siblings', () => {
    const { container } = render(<div data-canvas-artboard="true"><div data-node-id="business-node" data-runtime-surface="business">Business</div><DesignerCanvasOverlay><button data-canvas-interaction-control="true" type="button">Overlay</button></DesignerCanvasOverlay></div>);
    const stage = container.firstElementChild as HTMLElement;
    const overlayChild = stage.querySelector('[data-canvas-transient-overlay="true"] button') as HTMLElement;
    const business = stage.querySelector('[data-node-id="business-node"]') as HTMLElement;
    const originalElementsFromPoint = document.elementsFromPoint;
    Object.defineProperty(document, 'elementsFromPoint', { configurable: true, value: () => [overlayChild, business] });

    try {
      expect(findCanvasElementAtPoint(stage, 10, 10)).toBe(business);
      expect(overlayChild.closest('[data-runtime-surface="business"]')).toBeNull();
    } finally {
      Object.defineProperty(document, 'elementsFromPoint', { configurable: true, value: originalElementsFromPoint });
    }
  });

  it('keeps runtime business preview as a sibling of the designer overlay', () => {
    const { container } = render(<div><div data-designer-runtime-preview="true" data-runtime-surface="business"><div data-runtime-element-id="runtime-node" data-runtime-surface="business">Runtime</div></div><DesignerCanvasOverlay><div data-selection-overlay="true">Selection</div></DesignerCanvasOverlay></div>);
    const runtimeNode = container.querySelector('[data-runtime-element-id="runtime-node"]') as HTMLElement;

    expect(runtimeNode.closest('[data-canvas-transient-overlay="true"]')).toBeNull();
    expect(container.querySelector('[data-designer-overlay-root="true"]')?.getAttribute('data-runtime-surface')).toBe('overlay');
  });
});
