export interface CanvasHitTestOptions {
  excludedNodeIds?: ReadonlySet<string>;
}

/** DOM hit testing is an adapter only; the returned node id is resolved back
 * into the document model before any command is created. */
export function findCanvasElementAtPoint(
  stage: HTMLElement,
  clientX: number,
  clientY: number,
  options: CanvasHitTestOptions = {}
): Element | null {
  const elements = elementsAtPoint(stage.ownerDocument, clientX, clientY);
  for (const element of elements) {
    if (!stage.contains(element)) continue;
    if (element.closest('[data-canvas-transient-overlay="true"]')) continue;
    const nodeId = element.closest<HTMLElement>('[data-node-id]')?.dataset.nodeId;
    if (nodeId && options.excludedNodeIds?.has(nodeId)) continue;
    return element;
  }
  return null;
}

export function elementsAtPoint(document: Document, clientX: number, clientY: number): Element[] {
  if (typeof document.elementsFromPoint === 'function') return document.elementsFromPoint(clientX, clientY);
  if (typeof document.elementFromPoint === 'function') {
    const element = document.elementFromPoint(clientX, clientY);
    return element ? [element] : [];
  }
  // JSDOM has no layout engine. Returning the artboard keeps pointer tests on
  // the same model-backed path without teaching production code test details.
  const artboard = document.querySelector('[data-canvas-artboard="true"]');
  return artboard ? [artboard] : [];
}
