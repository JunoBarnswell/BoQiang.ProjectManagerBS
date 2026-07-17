// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { RUNTIME_CAPABILITY_CONTRACT } from '../../../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract';
import { RuntimeArtifactIntegrity } from '../../../../../runtime-kernel/RuntimeArtifactIntegrity';
import { createRuntimeManifestRegistry, runtimeComponentRegistry } from '../../../../../runtime-kernel/RuntimeComponentRegistry';
import { RuntimeKernel } from '../../../../../runtime-kernel/RuntimeKernel';
import { createResourceDropEvent } from '../binding/resourcePointerDrag';
import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import { createComponentPointerDropEvent } from '../components/componentInsertion';
import { ComponentRegistry } from '../components/ComponentRegistry';
import { latestComponentRegistry } from '../components/latestComponentManifestCatalog';
import type { DesignerDocument } from '../document/DesignerDocument';
import type { RuntimeArtifact } from '../document/RuntimeArtifact';
import type { BindingDocument } from '../expression/expressionTypes';
import { DesignerEditorSessionStore } from '../session/DesignerEditorSessionStore';

import { buildGeometry, createGeometryPatches, DesignerCanvas, toResponsiveGeometryPatches } from './DesignerCanvas';

afterEach(cleanup);

describe('DesignerCanvas latest slice', () => {
  it('renders previews from the compiled RuntimeKernel and reports unknown types', async () => {
    const bus = new DesignerCommandBus(createDocument());
    const previewKernel = await createPreviewKernel(bus.document);
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} previewKernel={previewKernel} sessionStore={createSession(bus.document.documentId)} />);
    expect(screen.getByRole('button', { name: 'Button' })).toBeTruthy();

    cleanup();
    const unknownBus = new DesignerCommandBus(createDocument('unknown.component'));
    render(<DesignerCanvas commandBus={unknownBus} manifests={latestComponentRegistry} previewError="Unknown component manifest: unknown.component" sessionStore={createSession(unknownBus.document.documentId)} />);
    expect(screen.getByRole('status').textContent).toContain('Unknown component manifest: unknown.component');
    expect(screen.queryByText(/includes\('button'\)/)).toBeNull();
  });

  it('starts a design move from the runtime button body instead of executing the runtime action', async () => {
    const bus = new DesignerCommandBus(createDocument());
    const previewKernel = await createPreviewKernel(bus.document);
    const onCommandResult = vi.fn();
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} previewKernel={previewKernel} sessionStore={createSession(bus.document.documentId)} />);
    const runtimeButton = screen.getByRole('treeitem', { name: 'Child' });

    fireEvent.pointerDown(runtimeButton, { button: 0, clientX: 10, clientY: 10, pointerId: 91 });
    fireEvent.pointerMove(window, { clientX: 30, clientY: 25, pointerId: 91 });
    fireEvent.pointerUp(window, { clientX: 30, clientY: 25, pointerId: 91 });

    expect(onCommandResult).toHaveBeenCalledTimes(1);
    expect(bus.document.elements.child.layout).toMatchObject({ position: 'absolute', x: 24, y: 16 });
  });

  it('exposes a roving tree with levels, selection, expansion, and keyboard navigation', async () => {
    const user = userEvent.setup();
    const bus = new DesignerCommandBus(createNestedDocument());
    const session = createSession(bus.document.documentId);
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={session} />);

    const tree = screen.getByRole('tree', { name: 'Page Studio canvas' });
    const child = screen.getByRole('treeitem', { name: 'Child' });
    expect(tree).toBeTruthy();
    expect(document.querySelector('[data-canvas-artboard="true"]')?.getAttribute('data-node-id')).toBeNull();
    expect(child.getAttribute('aria-level')).toBe('2');
    expect(child.getAttribute('aria-expanded')).toBe('true');
    expect(child.getAttribute('tabindex')).toBe('0');

    child.focus();
    await user.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(screen.getByRole('treeitem', { name: 'Grandchild' }));
    await user.keyboard('{Home}');
    expect(document.activeElement).toBe(child);
    await user.keyboard('{End}');
    expect(document.activeElement).toBe(screen.getByRole('treeitem', { name: 'Grandchild' }));
    await user.keyboard('{ArrowLeft}');
    expect(document.activeElement).toBe(child);
    expect(child.getAttribute('aria-expanded')).toBe('true');
    await user.keyboard('{ArrowLeft}');
    expect(child.getAttribute('aria-expanded')).toBe('false');
    expect(screen.queryByRole('treeitem', { name: 'Grandchild' })).toBeNull();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('treeitem', { name: 'Grandchild' })).toBeTruthy();
  });

  it('supports Shift multi-select, Alt hierarchy penetration, and locked-node skipping', () => {
    const document = createNestedDocument();
    document.elements.child.locked = true;
    const bus = new DesignerCommandBus(document);
    const session = createSession(bus.document.documentId);
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={session} />);

    const child = screen.getByRole('treeitem', { name: 'Child' });
    expect(child.getAttribute('aria-disabled')).toBe('true');
    fireEvent.click(child);
    expect(session.getSnapshot().selectedNodeIds).toEqual([]);

    const unlockedDocument = createNestedDocument();
    const unlockedBus = new DesignerCommandBus(unlockedDocument);
    cleanup();
    const unlockedSession = createSession(unlockedBus.document.documentId);
    render(<DesignerCanvas commandBus={unlockedBus} manifests={latestComponentRegistry} sessionStore={unlockedSession} />);
    const unlockedChild = screen.getByRole('treeitem', { name: 'Child' });
    const grandchild = screen.getByRole('treeitem', { name: 'Grandchild' });
    fireEvent.click(unlockedChild);
    fireEvent.click(grandchild, { shiftKey: true });
    expect(unlockedSession.getSnapshot().selectedNodeIds).toEqual(['child', 'grandchild']);

    fireEvent.click(grandchild, { altKey: true });
    expect(unlockedSession.getSnapshot().primaryNodeId).toBe('child');
  });

  it('binds a compatible resource dropped on a canvas node through BindValue and undo', () => {
    const bus = new DesignerCommandBus(createDocument());
    const bindingDocument: BindingDocument = { variables: [{ id: 'customer.name', name: 'Customer name', source: 'variables', valueType: 'string' }] };
    const onCommandResult = vi.fn();
    render(<DesignerCanvas bindingDocument={bindingDocument} commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} sessionStore={createSession(bus.document.documentId)} />);
    const node = screen.getByRole('treeitem', { name: 'Child' });

    node.dispatchEvent(createResourceDropEvent('variables:customer.name'));

    expect(onCommandResult).toHaveBeenCalledTimes(1);
    expect(onCommandResult.mock.calls[0][0]).toMatchObject({ changed: true });
    expect(bus.document.elements.child.props.text).toMatchObject({ resourceId: 'variables:customer.name', resourceType: 'variables', displayName: 'Customer name' });
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.child.props.value).toBeUndefined();
  });

  it('binds a resource to the explicitly selected component slot', () => {
    const document = createDocument('form.input');
    const bus = new DesignerCommandBus(document);
    const bindingDocument: BindingDocument = { variables: [{ id: 'customer.name', name: 'Customer name', source: 'variables', valueType: 'string' }] };
    render(<DesignerCanvas bindingDocument={bindingDocument} commandBus={bus} manifests={latestComponentRegistry} sessionStore={createSession(bus.document.documentId)} />);
    const child = screen.getByRole('treeitem', { name: 'Child' });
    fireEvent.click(child);
    const placeholderSlot = screen.getByRole('button', { name: 'Bind Placeholder' });

    placeholderSlot.dispatchEvent(createResourceDropEvent('variables:customer.name', 'props.placeholder'));

    expect(bus.document.elements.child.props.placeholder).toMatchObject({ resourceId: 'variables:customer.name' });
    expect(bus.document.elements.child.props.value).toBeUndefined();
  });

  it('inserts a component when an overlay is above the canvas drop point', () => {
    const bus = new DesignerCommandBus(createDocument());
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={createSession(bus.document.documentId)} />);
    const canvasNode = screen.getByRole('treeitem', { name: 'Child' });
    const dockOverlay = window.document.createElement('aside');
    const originalElementsFromPoint = window.document.elementsFromPoint;
    Object.defineProperty(window.document, 'elementsFromPoint', { configurable: true, value: () => [dockOverlay, canvasNode] });

    try {
      window.dispatchEvent(createComponentPointerDropEvent({ clientX: 24, clientY: 24, pointerId: 21, type: 'text.heading' }));
    } finally {
      Object.defineProperty(window.document, 'elementsFromPoint', { configurable: true, value: originalElementsFromPoint });
    }

    expect(Object.values(bus.document.elements).some((node) => node.type === 'text.heading')).toBe(true);
  });

  it('keeps drag state transient and emits one batch command on pointer up', () => {
    const bus = new DesignerCommandBus(createDocument());
    const commandResults: unknown[] = [];
    const session = createSession(bus.document.documentId);
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={(result) => commandResults.push(result)} sessionStore={session} />);
    const node = screen.getByRole('treeitem', { name: 'Child' });
    fireEvent.pointerDown(node, { button: 0, clientX: 10, clientY: 10, pointerId: 7 });
    fireEvent.pointerMove(window, { clientX: 30, clientY: 25, pointerId: 7 });
    expect(bus.document.elements.child.layout.x).toBeUndefined();
    fireEvent.pointerUp(window, { clientX: 30, clientY: 25, pointerId: 7 });
    expect(commandResults).toHaveLength(1);
    expect(bus.document.elements.child.layout).toMatchObject({ position: 'absolute', x: 24, y: 16 });
  });

  it('does not issue a command when a pointer transaction has no movement', () => {
    const bus = new DesignerCommandBus(createDocument());
    const onCommandResult = vi.fn();
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} sessionStore={createSession(bus.document.documentId)} />);
    const node = screen.getByRole('treeitem', { name: 'Child' });
    fireEvent.pointerDown(node, { button: 0, clientX: 10, clientY: 10, pointerId: 8 });
    fireEvent.pointerUp(window, { clientX: 10, clientY: 10, pointerId: 8 });
    expect(onCommandResult).not.toHaveBeenCalled();
  });

  it('restores the selection snapshot and document when a pointer transaction is cancelled', () => {
    const bus = new DesignerCommandBus(createDocument());
    const session = createSession(bus.document.documentId);
    session.patch({ anchorNodeId: 'child', primaryNodeId: 'child', selectedNodeIds: ['child'] });
    const onCommandResult = vi.fn();
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} sessionStore={session} />);
    const canvas = screen.getByRole('tree', { name: 'Page Studio canvas' });
    const before = JSON.stringify(bus.document);

    fireEvent.pointerDown(canvas, { button: 0, clientX: 10, clientY: 10, pointerId: 22 });
    fireEvent.pointerMove(window, { clientX: 80, clientY: 60, pointerId: 22 });
    fireEvent.pointerCancel(window, { clientX: 80, clientY: 60, pointerId: 22 });

    expect(onCommandResult).not.toHaveBeenCalled();
    expect(JSON.stringify(bus.document)).toBe(before);
    expect(session.getSnapshot()).toMatchObject({ anchorNodeId: 'child', primaryNodeId: 'child', selectedNodeIds: ['child'], transactionId: null });
    expect(document.querySelector('[data-selection-marquee="true"]')).toBeNull();
  });

  it('keeps a committed drag as one undoable command and redoes the same snapshot', () => {
    const bus = new DesignerCommandBus(createDocument());
    const onCommandResult = vi.fn();
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} sessionStore={createSession(bus.document.documentId)} />);
    const node = screen.getByRole('treeitem', { name: 'Child' });
    const initial = JSON.stringify(bus.document.elements.child.layout);

    fireEvent.pointerDown(node, { button: 0, clientX: 10, clientY: 10, pointerId: 23 });
    fireEvent.pointerMove(window, { clientX: 30, clientY: 25, pointerId: 23 });
    fireEvent.pointerMove(window, { clientX: 50, clientY: 40, pointerId: 23 });
    fireEvent.pointerUp(window, { clientX: 50, clientY: 40, pointerId: 23 });
    const committed = JSON.stringify(bus.document.elements.child.layout);

    expect(onCommandResult).toHaveBeenCalledTimes(1);
    expect(bus.undo()?.changed).toBe(true);
    expect(JSON.stringify(bus.document.elements.child.layout)).toBe(initial);
    expect(bus.redo()?.changed).toBe(true);
    expect(JSON.stringify(bus.document.elements.child.layout)).toBe(committed);
  });

  it('keeps selection handles in a non-layout overlay', () => {
    const bus = new DesignerCommandBus(createDocument());
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={createSession(bus.document.documentId)} />);
    const node = screen.getByRole('treeitem', { name: 'Child' });
    fireEvent.click(node);
    const overlay = node.querySelector('[data-selection-overlay="true"]') as HTMLElement;
    expect(overlay).toBeTruthy();
    expect(overlay.style.position).toBe('absolute');
    expect(overlay.style.pointerEvents).toBe('none');
    expect(overlay.querySelector('button')?.style.pointerEvents).toBe('auto');
  });

  it('uses the stage origin directly after removing the scroll-container inset', () => {
    const bus = new DesignerCommandBus(createDocument());
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={createSession(bus.document.documentId)} />);
    const world = document.querySelector('[data-canvas-world="true"]') as HTMLElement;
    expect(world.style.left).toBe('');
    expect(world.style.top).toBe('');
  });

  it('persists canvas pan in the editor session viewport', () => {
    const bus = new DesignerCommandBus(createDocument());
    const session = createSession(bus.document.documentId);
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={session} />);
    const canvas = screen.getByRole('tree', { name: 'Page Studio canvas' });

    fireEvent.pointerDown(canvas, { button: 1, clientX: 10, clientY: 10, pointerId: 19 });
    fireEvent.pointerMove(window, { clientX: 42, clientY: 28, pointerId: 19 });
    fireEvent.pointerUp(window, { clientX: 42, clientY: 28, pointerId: 19 });

    expect(session.getSnapshot().viewport.pan).toEqual({ x: 32, y: 18 });
  });

  it('renders session-controlled rulers, grid, and user guides without changing the document', () => {
    const bus = new DesignerCommandBus(createDocument());
    const session = createSession(bus.document.documentId);
    session.patch({ canvas: { gridVisible: true, rulersVisible: true, guides: [{ axis: 'x', id: 'guide-1', position: 48 }] } });
    const before = JSON.stringify(bus.document);
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={session} />);
    expect(screen.getByRole('group', { name: 'Canvas rulers' })).toBeTruthy();
    expect(screen.getByRole('tree', { name: 'Page Studio canvas' }).getAttribute('style')).toContain('background-image');
    expect(document.querySelector('[data-user-guide="x"]')).toBeTruthy();
    expect(JSON.stringify(bus.document)).toBe(before);
  });

  it('renders device pixel ratio, browser bar, and safe-area simulation from session state', () => {
    const bus = new DesignerCommandBus(createDocument());
    const session = createSession(bus.document.documentId);
    session.patch({ canvas: { device: { browserBar: { bottom: 0, top: 24 }, breakpointId: 'mobile', height: 844, id: 'phone', orientation: 'portrait', pixelRatio: 3, safeArea: { bottom: 34, left: 0, right: 0, top: 47 }, width: 390 } } });
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={session} />);
    const canvas = screen.getByRole('tree', { name: 'Page Studio canvas' });
    expect(canvas.getAttribute('data-device-id')).toBe('phone');
    expect(canvas.getAttribute('data-device-pixel-ratio')).toBe('3');
    expect(document.querySelector('[data-browser-bar="true"]')).toBeTruthy();
    expect(document.querySelector('[data-safe-area="true"]')).toBeTruthy();
  });

  it('uses touch pointer drags on the blank stage for session-only panning', () => {
    const bus = new DesignerCommandBus(createDocument());
    const session = createSession(bus.document.documentId);
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={session} />);
    const canvas = screen.getByRole('tree', { name: 'Page Studio canvas' });

    fireEvent.pointerDown(canvas, { button: 0, clientX: 10, clientY: 10, pointerId: 20, pointerType: 'touch' });
    fireEvent.pointerMove(window, { clientX: 42, clientY: 28, pointerId: 20, pointerType: 'touch' });
    fireEvent.pointerUp(window, { clientX: 42, clientY: 28, pointerId: 20, pointerType: 'touch' });

    expect(session.getSnapshot().viewport.pan).toEqual({ x: 32, y: 18 });
    expect(bus.document.revision).toBe(1);
  });

  it('moves the complete additive selection from one pointer transaction', () => {
    const document = createDocument();
    document.elements.root.children = ['child', 'second'];
    document.elements.child.layout = { x: 0, y: 0, width: 120 };
    document.elements.second = { children: [], events: [], id: 'second', layout: { x: 200, y: 0, width: 120 }, name: 'Second', parentId: 'root', props: { text: 'Second' }, style: {}, type: 'text' };
    const bus = new DesignerCommandBus(document);
    const onCommandResult = vi.fn();
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} sessionStore={createSession(bus.document.documentId)} />);

    const first = screen.getByRole('treeitem', { name: 'Child' });
    const second = screen.getByRole('treeitem', { name: 'Second' });
    fireEvent.pointerDown(first, { button: 0, clientX: 10, clientY: 10, pointerId: 41 });
    fireEvent.pointerUp(window, { clientX: 10, clientY: 10, pointerId: 41 });
    fireEvent.pointerDown(second, { button: 0, clientX: 210, clientY: 10, pointerId: 42, ctrlKey: true });
    fireEvent.pointerMove(window, { clientX: 230, clientY: 10, pointerId: 42 });
    fireEvent.pointerUp(window, { clientX: 230, clientY: 10, pointerId: 42 });

    expect(onCommandResult).toHaveBeenCalledTimes(1);
    expect(bus.document.elements.child.layout.x).toBe(24);
    expect(bus.document.elements.second.layout.x).toBe(224);
  });

  it('renders children of a free container at the geometry position', () => {
    const document = createDocument();
    document.elements.root.layout = { layoutMode: 'free', width: 400, height: 300 };
    document.elements.child.layout = { width: 120, height: 40 };
    const bus = new DesignerCommandBus(document);
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} sessionStore={createSession(bus.document.documentId)} />);
    const child = screen.getByRole('treeitem', { name: 'Child' }) as HTMLElement;
    expect(child.style.position).toBe('absolute');
    expect(child.style.left).toBe('0px');
    expect(child.style.top).toBe('0px');
  });

  it('uses the shared editor/runtime renderer contract', () => {
    const sourceManifest = latestComponentRegistry.get('text')!;
    const registry = new ComponentRegistry([{
      ...sourceManifest,
      editor: {
        ...sourceManifest.editor,
        inspector: {
          ...sourceManifest.editor.inspector!,
          componentType: 'contract.test',
          properties: sourceManifest.editor.inspector!.properties.map((property) => ({ ...property, ownerType: 'contract.test' })),
        },
      },
      type: 'contract.test',
    }]);
    const bus = new DesignerCommandBus(createDocument('contract.test'));
    render(<DesignerCanvas commandBus={bus} manifests={registry} sessionStore={createSession(bus.document.documentId)} />);
    expect(screen.getByText('Preparing latest runtime preview')).toBeTruthy();
  });

  it('writes nested move and resize coordinates in the immediate parent coordinate system', () => {
    const elements: DesignerDocument['elements'] = {};
    for (let index = 0; index < 5; index += 1) {
      const id = index === 0 ? 'root' : `level-${index}`;
      elements[id] = { children: index === 4 ? [] : [index === 0 ? 'level-1' : `level-${index + 1}`], events: [], id, layout: { x: 10, y: 20, width: 100, height: 50 }, parentId: index === 0 ? null : index === 1 ? 'root' : `level-${index - 1}`, props: {}, type: 'layout.container' };
    }
    const patches = createGeometryPatches([{ id: 'level-4', x: 150, y: 190, width: 100, height: 50 }], elements, 'move', [{ id: 'level-4', rect: { id: 'level-4', x: 140, y: 180, width: 100, height: 50 } }]);
    expect(patches['level-4'].layout).toMatchObject({ position: 'absolute', x: 110, y: 110 });
  });

  it('does not convert flex or grid children into absolute-coordinate commands', () => {
    const elements = {
      root: { children: ['flex'], events: [], id: 'root', layout: { layoutMode: 'free' }, parentId: null, props: {}, type: 'layout.page' },
      flex: { children: ['child'], events: [], id: 'flex', layout: { layoutMode: 'flex' }, parentId: 'root', props: {}, type: 'layout.container' },
      child: { children: [], events: [], id: 'child', layout: { width: 80, height: 32 }, parentId: 'flex', props: {}, type: 'text' }
    } satisfies DesignerDocument['elements'];
    const moved = createGeometryPatches([{ id: 'child', x: 100, y: 120, width: 80, height: 32 }], elements, 'move', [{ id: 'child', rect: { id: 'child', x: 0, y: 0, width: 80, height: 32 } }]);
    expect(moved).toEqual({});
  });

  it('persists constraint anchors when moving or resizing a constrained child', () => {
    const document = createDocument();
    document.elements.root.layout = { layoutMode: 'constraints', width: 400, height: 300 };
    document.elements.child.layout = { constraints: { left: 20, right: 300, top: 30, bottom: 230 }, width: 80, height: 40 };
    const geometry = buildGeometry(document, 'root', { x: 0, y: 0, width: 400, height: 300 });
    const before = geometry.rects.child;
    const moved = createGeometryPatches([{ ...before, x: 35, y: 45 }], document.elements, 'move', [{ id: 'child', rect: before }], geometry.parentOrigins, geometry.layoutModes, geometry.rects);
    const resized = createGeometryPatches([{ ...before, width: 100, height: 50 }], document.elements, 'resize', [{ id: 'child', rect: before }], geometry.parentOrigins, geometry.layoutModes, geometry.rects);
    expect(moved.child.layout).toEqual({ constraints: { left: 35, right: 285, top: 45, bottom: 215 }, width: 80, height: 40 });
    expect(resized.child.layout).toEqual({ constraints: { left: 20, right: 280, top: 30, bottom: 220 }, width: 100, height: 50 });
  });

  it('writes responsive constraint movement as one override command without base coordinates', () => {
    const document = createDocument();
    document.elements.root.layout = { layoutMode: 'constraints', width: 400, height: 300 };
    document.elements.child.layout = { constraints: { left: 20, right: 300, top: 30, bottom: 230 }, width: 80, height: 40 };
    const bus = new DesignerCommandBus(document);
    const onCommandResult = vi.fn();
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} responsiveBreakpoint={{ id: 'tablet', minWidth: 768 }} responsiveBreakpoints={[{ id: 'mobile', minWidth: 0 }, { id: 'tablet', minWidth: 768 }]} sessionStore={createSession(bus.document.documentId)} />);
    const node = screen.getByRole('treeitem', { name: 'Child' });
    fireEvent.pointerDown(node, { button: 0, clientX: 44, clientY: 54, pointerId: 31 });
    fireEvent.pointerUp(window, { clientX: 64, clientY: 74, pointerId: 31 });
    expect(onCommandResult).toHaveBeenCalledTimes(1);
    expect(bus.document.elements.child.layout.x).toBeUndefined();
    expect(bus.document.elements.child.responsiveOverrides?.tablet.layout).toMatchObject({ constraints: { left: 40, right: 280, top: 48, bottom: 212 } });
    expect(bus.document.elements.child.responsiveOverrides?.tablet.layout).not.toHaveProperty('x');
  });

  it('resolves free, flex and grid children in world coordinates without duplicating parent offsets', () => {
    const document = createDocument();
    document.elements.root.layout = { width: 800, height: 600 };
    document.elements.root.children = ['free', 'flex', 'grid'];
    document.elements.free = { children: ['nested'], events: [], id: 'free', layout: { x: 20, y: 30, width: 200, height: 100 }, parentId: 'root', props: {}, type: 'layout.container' };
    document.elements.nested = { children: [], events: [], id: 'nested', layout: { x: 5, y: 7, width: 80, height: 24 }, parentId: 'free', props: {}, type: 'text' };
    document.elements.flex = { children: ['flex-a', 'flex-b'], events: [], id: 'flex', layout: { display: 'flex', gap: 12, width: 300, height: 100 }, parentId: 'root', props: {}, type: 'layout.container' };
    document.elements['flex-a'] = { children: [], events: [], id: 'flex-a', layout: { width: 80, height: 24 }, parentId: 'flex', props: {}, type: 'text' };
    document.elements['flex-b'] = { children: [], events: [], id: 'flex-b', layout: { width: 60, height: 24 }, parentId: 'flex', props: {}, type: 'text' };
    document.elements.grid = { children: ['grid-a', 'grid-b'], events: [], id: 'grid', layout: { layoutMode: 'grid', columns: 2, gap: 10, width: 300, height: 100 }, parentId: 'root', props: {}, type: 'layout.container' };
    document.elements['grid-a'] = { children: [], events: [], id: 'grid-a', layout: {}, parentId: 'grid', props: {}, type: 'text' };
    document.elements['grid-b'] = { children: [], events: [], id: 'grid-b', layout: {}, parentId: 'grid', props: {}, type: 'text' };

    const geometry = buildGeometry(document, 'root', { x: 0, y: 0, width: 800, height: 600 });
    expect(geometry.rects.nested).toMatchObject({ x: 25, y: 37 });
    expect(geometry.parentOrigins.nested).toEqual({ x: 20, y: 30 });
    expect(geometry.rects['flex-b'].x - geometry.rects['flex-a'].x).toBe(92);
    expect(geometry.rects['grid-b'].x).toBeGreaterThan(geometry.rects['grid-a'].x);
  });

  it('resolves the selected breakpoint from the persisted override map', () => {
    const document = createDocument();
    document.elements.child.responsiveOverrides = { tablet: { layout: { x: 40, width: 240 } } };
    const geometry = buildGeometry(document, 'root', { x: 0, y: 0, width: 800, height: 600 }, { id: 'tablet', minWidth: 768 }, [{ id: 'mobile', minWidth: 0 }, { id: 'tablet', minWidth: 768 }]);
    expect(geometry.rects.child).toMatchObject({ x: 40, width: 240 });
  });

  it('converts responsive child movement back to the selected parent local coordinates', () => {
    const document = createDocument();
    document.elements.root.layout = { width: 800, height: 600 };
    document.elements.root.children = ['parent'];
    document.elements.parent = { children: ['child'], events: [], id: 'parent', layout: { x: 10, y: 20, width: 200, height: 100, layoutMode: 'free' }, parentId: 'root', props: {}, type: 'layout.container' };
    document.elements.child.parentId = 'parent';
    document.elements.child.layout = { x: 5, y: 6, width: 80, height: 24 };
    document.elements.parent.responsiveOverrides = { tablet: { layout: { x: 100, y: 120 } } };
    const geometry = buildGeometry(document, 'root', { x: 0, y: 0, width: 800, height: 600 }, { id: 'tablet', minWidth: 768 }, [{ id: 'mobile', minWidth: 0 }, { id: 'tablet', minWidth: 768 }]);
    const before = geometry.rects.child;
    const after = { ...before, x: before.x + 20, y: before.y + 20 };
    const patches = createGeometryPatches([after], document.elements, 'move', [{ id: 'child', rect: before }], geometry.parentOrigins, geometry.layoutModes);
    expect(geometry.parentOrigins.child).toEqual({ x: 100, y: 120 });
    expect(patches.child.layout).toMatchObject({ position: 'absolute', x: 25, y: 26 });
  });

  it('replaces responsive geometry diffs and clears stale nested override fields', () => {
    const document = createDocument();
    document.elements.child.layout = { constraints: { left: 10, right: 20 }, width: 100 };
    document.elements.child.responsiveOverrides = { tablet: { layout: { constraints: { left: 10, right: 20 }, width: 100 } } };
    const patches = toResponsiveGeometryPatches(
      { child: { layout: { constraints: { left: 12, right: 20 }, width: 100 } } },
      'resize',
      document.elements,
      document.elements,
      { id: 'tablet', minWidth: 768 },
      [{ id: 'mobile', minWidth: 0 }, { id: 'tablet', minWidth: 768 }]
    );
    expect(patches.child.layout).toEqual({ constraints: { left: 12, right: undefined }, width: undefined });
  });

  it('updates responsive constraint anchors from the resolved breakpoint layout', () => {
    const document = createDocument();
    document.elements.root.layout = { width: 800, height: 600, layoutMode: 'constraints' };
    document.elements.child.layout = { width: 120, height: 40 };
    document.elements.child.responsiveOverrides = { tablet: { layout: { constraints: { left: 10, right: 20, top: 8, bottom: 12 } } } };
    const geometry = buildGeometry(document, 'root', { x: 0, y: 0, width: 800, height: 600 }, { id: 'tablet', minWidth: 768 }, [{ id: 'mobile', minWidth: 0 }, { id: 'tablet', minWidth: 768 }]);
    const before = geometry.rects.child;
    const patches = createGeometryPatches([{ ...before, x: before.x + 20, y: before.y + 10 }], geometry.nodes, 'move', [{ id: 'child', rect: before }], geometry.parentOrigins, geometry.layoutModes, geometry.rects);
    expect(patches.child.layout).toMatchObject({ constraints: { left: 30, right: 650, top: 18, bottom: 542 } });
    expect(patches.child.layout).not.toHaveProperty('x');
  });

  it('writes non-base drag changes into one responsive command transaction', () => {
    const bus = new DesignerCommandBus(createDocument());
    const onCommandResult = vi.fn();
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} responsiveBreakpoint={{ id: 'tablet', minWidth: 768 }} responsiveBreakpoints={[{ id: 'mobile', minWidth: 0 }, { id: 'tablet', minWidth: 768 }]} sessionStore={createSession(bus.document.documentId)} />);
    const node = screen.getByRole('treeitem', { name: 'Child' });
    fireEvent.pointerDown(node, { button: 0, clientX: 10, clientY: 10, pointerId: 21 });
    fireEvent.pointerMove(window, { clientX: 30, clientY: 25, pointerId: 21 });
    fireEvent.pointerUp(window, { clientX: 30, clientY: 25, pointerId: 21 });
    expect(onCommandResult).toHaveBeenCalledTimes(1);
    expect(bus.document.elements.child.layout.x).toBeUndefined();
    expect(bus.document.elements.child.responsiveOverrides?.tablet.layout).toMatchObject({ position: 'absolute', x: 24, y: 16 });
    expect(bus.document.elements.child.responsiveOverrides?.tablet.layout).not.toHaveProperty('width');
  });

  it('resizes the selected node through a single pointer-up transaction', () => {
    const bus = new DesignerCommandBus(createDocument());
    const onCommandResult = vi.fn();
    render(<DesignerCanvas commandBus={bus} manifests={latestComponentRegistry} onCommandResult={onCommandResult} sessionStore={createSession(bus.document.documentId)} />);
    const node = screen.getByRole('treeitem', { name: 'Child' });
    fireEvent.pointerDown(node, { button: 0, clientX: 10, clientY: 10, pointerId: 12 });
    fireEvent.pointerUp(window, { clientX: 10, clientY: 10, pointerId: 12 });
    const handle = screen.getByRole('button', { name: 'Resize child southeast' });
    fireEvent.pointerDown(handle, { button: 0, clientX: 10, clientY: 10, pointerId: 13 });
    fireEvent.pointerMove(window, { clientX: 30, clientY: 25, pointerId: 13 });
    fireEvent.pointerUp(window, { clientX: 30, clientY: 25, pointerId: 13 });
    expect(onCommandResult).toHaveBeenCalledTimes(1);
    expect(bus.document.elements.child.layout).toMatchObject({ width: 140, height: 63 });
  });
});

function createSession(documentId: string): DesignerEditorSessionStore {
  return new DesignerEditorSessionStore({ anchorNodeId: null, canvas: { device: null, gridSize: 8, gridVisible: true, guides: [], minimapVisible: true, rulersVisible: true, snapThreshold: 6, tool: 'select' }, documentId, panelState: {}, primaryNodeId: null, selectedNodeIds: [], sessionId: 'canvas-test', transactionId: null, viewport: { height: 720, width: 1280, zoom: 1 } });
}

function createDocument(childType = 'action.button'): DesignerDocument {
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'canvas-test', elements: {
      child: { children: [], events: [], id: 'child', layout: { width: 120 }, name: 'Child', parentId: 'root', props: { text: 'Button' }, style: {}, type: childType },
      root: { children: ['child'], events: [], id: 'root', layout: { minHeight: 720 }, name: 'Page', parentId: null, props: {}, style: {}, type: 'layout.page' }
    }, metadata: {}, modals: [], pageParameters: [], pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }], pageType: 'standard', permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
  };
}

function createNestedDocument(): DesignerDocument {
  const document = createDocument();
  document.elements.child.children = ['grandchild'];
  document.elements.grandchild = { children: [], events: [], id: 'grandchild', layout: { width: 80 }, name: 'Grandchild', parentId: 'child', props: { text: 'Nested' }, style: {}, type: 'text' };
  return document;
}

async function createPreviewKernel(document: DesignerDocument): Promise<RuntimeKernel> {
  const manifestTypes = [...new Set(Object.values(document.elements).map((node) => node.type))].sort();
  const integrityPayload = {
    actions: document.actions,
    apiBindings: document.apiBindings,
    dataSources: document.dataSources,
    documentId: document.documentId,
    elements: document.elements,
    manifestTypes,
    pageMicroflows: (document.pageMicroflows ?? []) as unknown as RuntimeArtifact['pageMicroflows'],
    pageParameters: document.pageParameters,
    permissions: document.permissions,
    revision: document.revision,
    runtimeContext: document.runtimeContext,
    variables: document.variables,
    workflowBindings: document.workflowBindings
  };
  const artifact = {
    actions: integrityPayload.actions,
    artifactHash: await RuntimeArtifactIntegrity.computeHash(integrityPayload),
    bindings: [...document.apiBindings, ...document.dataSources, ...document.workflowBindings],
    compilerVersion: RUNTIME_CAPABILITY_CONTRACT.compilerRevision,
    documentId: integrityPayload.documentId,
    elements: integrityPayload.elements,
    integrityPayload,
    manifest: manifestTypes.map((type) => ({ renderer: { preview: RUNTIME_CAPABILITY_CONTRACT.renderer, runtime: RUNTIME_CAPABILITY_CONTRACT.renderer }, type })),
    manifestTypes,
    pageMicroflows: integrityPayload.pageMicroflows as RuntimeArtifact['pageMicroflows'],
    pageParameters: integrityPayload.pageParameters,
    permissions: integrityPayload.permissions,
    revision: integrityPayload.revision,
    runtimeContext: integrityPayload.runtimeContext,
    signature: '',
    variables: integrityPayload.variables
  } as RuntimeArtifact;
  artifact.signature = await RuntimeArtifactIntegrity.computeSignature(artifact);
  return RuntimeKernel.create(artifact, {
    manifests: createRuntimeManifestRegistry(),
    monitoringContext: { appCode: 'TEST', artifactHash: artifact.artifactHash, documentId: artifact.documentId, pageCode: 'canvas-test', revision: artifact.revision, tenantId: 'tenant-a', traceId: 'trace-canvas', userId: 'user-canvas' },
    permissions: { granted: new Set(), isSystemAdmin: false },
    resolveRenderer: (componentType) => runtimeComponentRegistry.has(componentType)
  });
}
