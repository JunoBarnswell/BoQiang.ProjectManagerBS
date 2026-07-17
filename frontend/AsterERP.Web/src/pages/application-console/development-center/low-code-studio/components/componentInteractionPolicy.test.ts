import { describe, expect, it } from 'vitest';

import { canContainComponent, canResizeComponent, COMPONENT_RESIZE_HANDLES, resolveComponentInteractionPolicy, validateComponentInteractionPolicy } from './componentInteractionPolicy';
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

describe('component interaction policy', () => {
  it('uses geometry, content, parent and child policy in containment decisions', () => {
    const parent = manifest('layout.container', true, {
      allowedChildren: ['text.*'],
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

    expect(canContainComponent(parent, child, { layoutMode: 'flex' })).toBe(true);
    expect(canContainComponent(parent, child, { layoutMode: 'free' })).toBe(false);
    expect(canResizeComponent(child, 'east')).toBe(true);
    expect(canResizeComponent(child, 'north')).toBe(false);
    expect(resolveComponentInteractionPolicy(child)).toMatchObject({ contentModel: 'void', geometryModel: 'flow' });
  });

  it('keeps legacy manifests usable without fabricating restrictive declarations', () => {
    const legacy = manifest('legacy.leaf', false);
    const container = manifest('legacy.container', true);
    const registry = new ComponentRegistry([legacy, container]);

    expect(registry.canContain('legacy.container', 'legacy.leaf', { layoutMode: 'constraints' })).toBe(true);
    expect(registry.getInteractionPolicy('legacy.leaf')?.allowedParents).toEqual(['*']);
    expect(registry.canResize('legacy.leaf', 'southeast')).toBe(true);
  });

  it('applies the four parent layouts and all eight resize handles from one policy matrix', () => {
    const parent = manifest('layout.matrix', true, {
      allowedChildren: ['*'],
      allowedParents: ['*'],
      contentModel: 'children',
      geometryModel: 'intrinsic',
      resizeHandles: [],
      supportedParentLayouts: ['free', 'flex', 'grid', 'constraints']
    });
    const flowChild = manifest('flow.child', false, {
      allowedChildren: [],
      allowedParents: ['layout.matrix'],
      contentModel: 'void',
      geometryModel: 'flow',
      resizeHandles: [],
      supportedParentLayouts: ['flex', 'grid']
    });
    const absoluteChild = manifest('absolute.child', false, {
      allowedChildren: [],
      allowedParents: ['layout.matrix'],
      contentModel: 'void',
      geometryModel: 'absolute',
      resizeHandles: [...['north', 'west', 'east', 'south', 'northwest', 'northeast', 'southwest', 'southeast'] as const],
      supportedParentLayouts: ['free', 'constraints']
    });

    expect(canContainComponent(parent, flowChild, { layoutMode: 'flex' })).toBe(true);
    expect(canContainComponent(parent, flowChild, { layoutMode: 'grid' })).toBe(true);
    expect(canContainComponent(parent, flowChild, { layoutMode: 'free' })).toBe(false);
    expect(canContainComponent(parent, flowChild, { layoutMode: 'constraints' })).toBe(false);
    expect(canContainComponent(parent, absoluteChild, { layoutMode: 'free' })).toBe(true);
    expect(canContainComponent(parent, absoluteChild, { layoutMode: 'constraints' })).toBe(true);
    expect(canContainComponent(parent, absoluteChild, { layoutMode: 'flex' })).toBe(false);
    expect(canContainComponent(parent, absoluteChild, { layoutMode: 'grid' })).toBe(false);
    for (const handle of COMPONENT_RESIZE_HANDLES) expect(canResizeComponent(absoluteChild, handle)).toBe(true);
  });

  it('returns stable diagnostics and denies decisions for missing or invalid declarations', () => {
    const malformed = manifest('malformed.component', false, {
      allowedChildren: undefined,
      allowedParents: [],
      contentModel: 'unknown' as never,
      geometryModel: 'unknown' as never,
      resizeHandles: ['diagonal' as never],
      supportedParentLayouts: ['canvas' as never]
    } as unknown as ComponentManifest['interaction']);

    expect(validateComponentInteractionPolicy(malformed).map((item) => item.code)).toEqual(expect.arrayContaining([
      'missingAllowedChildren', 'missingAllowedParents', 'invalidContentModel', 'invalidGeometryModel',
      'invalidResizeHandle', 'invalidParentLayout'
    ]));
    expect(canResizeComponent(malformed, 'east')).toBe(false);
  });
});
