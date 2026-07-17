import { describe, expect, it } from 'vitest';

import type { DesignerDocument } from '../document/DesignerDocument';

import { resolveComponentInsertionPlacement, resolveComponentInsertionTarget, resolveComponentResizeDecision } from './componentInsertionTarget';
import type { ComponentManifest } from './ComponentManifest';
import { ComponentRegistry } from './ComponentRegistry';

function manifest(type: string, acceptsChildren: boolean, interaction?: ComponentManifest['interaction']): ComponentManifest {
  return {
    binding: { acceptedSources: ['variables'], acceptedTypes: ['string'], supportsConversion: true },
    capability: { acceptsChildren, capabilities: acceptsChildren ? ['container'] : ['content'] },
    defaults: { layout: {}, props: {}, style: {} },
    editor: { inspectorSections: ['content'], previewRenderer: 'preview', selectionMode: 'single' },
    events: [],
    i18n: { diagnosticKey: `${type}.diagnostic`, helpKey: `${type}.help`, labelKey: `${type}.label` },
    migrations: [],
    ...(interaction ? { interaction } : {}),
    responsive: { supportedLayouts: ['flow'], supportsOverrides: true },
    runtime: { renderer: 'runtime', supportedScopes: ['page'] },
    security: { actionPermissions: [], requiresPermission: false },
    type,
    validation: { schema: {}, supportsDiagnostics: true }
  };
}

function documentWithRoot(rootType: string, rootLayout: Record<string, unknown>, children: string[] = []): DesignerDocument {
  return {
    actions: [],
    apiBindings: [],
    dataSources: [],
    documentId: 'test',
    elements: {
      root: { children, events: [], id: 'root', layout: rootLayout, parentId: null, props: {}, type: rootType }
    },
    metadata: {},
    modals: [],
    pageParameters: [],
    pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }],
    permissions: {},
    revision: 1,
    runtimeContext: {},
    styleTokens: {},
    variables: [],
    workflowBindings: []
  };
}

describe('component insertion target policy', () => {
  it('rejects a child whose geometry model does not support the parent layout', () => {
    const parent = manifest('layout.container', true, {
      allowedChildren: ['text.paragraph'],
      allowedParents: ['*'],
      contentModel: 'children',
      geometryModel: 'intrinsic',
      resizeHandles: [],
      supportedParentLayouts: ['free', 'flex']
    });
    const child = manifest('text.paragraph', false, {
      allowedChildren: [],
      allowedParents: ['layout.container'],
      contentModel: 'void',
      geometryModel: 'flow',
      resizeHandles: ['east'],
      supportedParentLayouts: ['flex']
    });
    const registry = new ComponentRegistry([parent, child]);

    expect(resolveComponentInsertionTarget({ component: child, document: documentWithRoot(parent.type, { layoutMode: 'free' }), dropTargetNodeId: 'root', manifests: registry })).toBeNull();
    expect(resolveComponentInsertionTarget({ component: child, document: documentWithRoot(parent.type, { layoutMode: 'flex' }), dropTargetNodeId: 'root', manifests: registry })).toMatchObject({ parentId: 'root', placement: 'inside' });
  });

  it('does not turn a void target into an inside placement', () => {
    expect(resolveComponentInsertionPlacement({ clientX: 50, clientY: 50, rect: { height: 100, left: 0, top: 0, width: 100 }, targetAcceptsChildren: true, targetContentModel: 'void' })).toBe('after');
  });

  it.each([
    [{ layoutMode: 'free' }, { clientX: 25, clientY: 25 }, 'before'],
    [{ layoutMode: 'constraints' }, { clientX: 25, clientY: 75 }, 'after'],
    [{ layoutMode: 'flex', flexDirection: 'row' }, { clientX: 25, clientY: 75 }, 'before'],
    [{ display: 'grid' }, { clientX: 75, clientY: 25 }, 'after']
  ] as const)('uses the %s parent layout axis for placement', (parentLayout, point, expected) => {
    expect(resolveComponentInsertionPlacement({ ...point, rect: { height: 100, left: 0, top: 0, width: 100 }, parentLayout, targetAcceptsChildren: false })).toBe(expected);
  });

  it('rejects moving a node into its own descendant', () => {
    const container = manifest('layout.container', true);
    const child = manifest('text.paragraph', false);
    const document = documentWithRoot(container.type, { layoutMode: 'free' }, ['moving']);
    document.elements.moving = { children: ['descendant'], events: [], id: 'moving', layout: {}, parentId: 'root', props: {}, type: child.type };
    document.elements.descendant = { children: [], events: [], id: 'descendant', layout: {}, parentId: 'moving', props: {}, type: child.type };
    const registry = new ComponentRegistry([container, child]);

    expect(resolveComponentInsertionTarget({ component: child, document, manifests: registry, dropTargetNodeId: 'descendant', movingNodeIds: ['moving'] })).toBeNull();
  });

  it('rejects locked sources and targets, nested selections, and page roots without mutating the document', () => {
    const container = manifest('layout.container', true);
    const child = manifest('text.paragraph', false);
    const document = documentWithRoot(container.type, { layoutMode: 'free' }, ['left', 'right']);
    document.elements.left = { children: [], events: [], id: 'left', layout: {}, parentId: 'root', props: {}, type: child.type, locked: true };
    document.elements.right = { children: [], events: [], id: 'right', layout: {}, parentId: 'root', props: {}, type: child.type };
    const registry = new ComponentRegistry([container, child]);
    const before = JSON.stringify(document);

    expect(resolveComponentInsertionTarget({ component: child, document, manifests: registry, dropTargetNodeId: 'right', movingNodeIds: ['left'] })).toBeNull();
    expect(resolveComponentInsertionTarget({ component: child, document, manifests: registry, dropTargetNodeId: 'left' })).toBeNull();
    expect(resolveComponentInsertionTarget({ component: child, document, manifests: registry, movingNodeIds: ['root'] })).toBeNull();
    expect(JSON.stringify(document)).toBe(before);
  });

  it('keeps same-parent move indices stable after source removal', () => {
    const container = manifest('layout.container', true);
    const child = manifest('text.paragraph', false);
    const document = documentWithRoot(container.type, { layoutMode: 'flex', flexDirection: 'row' }, ['a', 'b', 'c']);
    for (const id of ['a', 'b', 'c']) document.elements[id] = { children: [], events: [], id, layout: {}, parentId: 'root', props: {}, type: child.type };
    const registry = new ComponentRegistry([container, child]);

    expect(resolveComponentInsertionTarget({ component: child, document, manifests: registry, dropTargetNodeId: 'b', movingNodeIds: ['a'], placement: 'after' })).toMatchObject({ index: 1, parentId: 'root', placement: 'after' });
  });

  it('returns a pure resize decision for every supported handle and locked/unsupported failures', () => {
    const resizable = manifest('action.button', false, { allowedChildren: [], allowedParents: ['*'], contentModel: 'void', geometryModel: 'absolute', resizeHandles: ['north', 'west', 'east', 'south', 'northwest', 'northeast', 'southwest', 'southeast'], supportedParentLayouts: ['free', 'constraints'] });
    const leaf = manifest('text.paragraph', false, { allowedChildren: [], allowedParents: ['*'], contentModel: 'void', geometryModel: 'intrinsic', resizeHandles: [], supportedParentLayouts: ['free', 'flex', 'grid', 'constraints'] });
    const locked = { children: [], events: [], id: 'locked', layout: {}, parentId: null, props: {}, type: resizable.type, locked: true };
    const registry = new ComponentRegistry([resizable, leaf]);

    expect(resolveComponentResizeDecision({ manifests: registry, node: { ...locked, type: resizable.type }, handle: 'southeast' })).toEqual({ allowed: false, reason: 'locked-node' });
    expect(resolveComponentResizeDecision({ manifests: registry, node: { ...locked, id: 'leaf', type: leaf.type, locked: false }, handle: 'southeast' })).toEqual({ allowed: false, reason: 'unsupported-handle' });
    expect(resolveComponentResizeDecision({ manifests: registry, node: undefined, handle: 'southeast' })).toEqual({ allowed: false, reason: 'missing-node' });
    for (const handle of ['north', 'west', 'east', 'south', 'northwest', 'northeast', 'southwest', 'southeast'] as const) expect(resolveComponentResizeDecision({ manifests: registry, node: { ...locked, locked: false, type: resizable.type }, handle })).toEqual({ allowed: true, reason: 'allowed' });
  });
});
