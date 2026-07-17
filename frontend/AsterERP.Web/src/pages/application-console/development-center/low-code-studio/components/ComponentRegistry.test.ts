import { describe, expect, it } from 'vitest';

import type { ComponentManifest } from './ComponentManifest';
import { ComponentRegistry } from './ComponentRegistry';

function createManifest(overrides: Partial<ComponentManifest> = {}): ComponentManifest {
  const type = overrides.type ?? 'text.paragraph';
  const manifest: ComponentManifest = {
    binding: { acceptedSources: ['variables'], acceptedTypes: ['string'], supportsConversion: true },
    capability: { acceptsChildren: false, capabilities: [] },
    defaults: { layout: {}, props: {}, style: {} },
    events: [],
    i18n: { diagnosticKey: 'test.component.diagnostic', helpKey: 'test.component.help', labelKey: 'test.component.label' },
    migrations: [],
    responsive: { supportedLayouts: ['block'], supportsOverrides: true },
    runtime: { renderer: 'runtime', supportedScopes: ['page'] },
    security: { actionPermissions: [], requiresPermission: false },
    type: 'text.paragraph',
    validation: { schema: {}, supportsDiagnostics: true },
    ...overrides,
    editor: { inspector: { componentType: type, ownerType: type, onlyInherited: true, sections: [], properties: [] }, inspectorSections: ['content'], previewRenderer: 'preview', selectionMode: 'single', ...overrides.editor }
  };
  return manifest;
}

describe('ComponentRegistry', () => {
  it('rejects duplicate types and validates startup completeness', () => {
    expect(() => new ComponentRegistry([createManifest(), createManifest({ type: 'text.heading' })])).not.toThrow();
    expect(() => new ComponentRegistry([createManifest(), createManifest()])).toThrow('Duplicate component type');
    expect(() => new ComponentRegistry([
      createManifest({
        editor: { inspector: { componentType: 'text.paragraph', ownerType: 'text.paragraph', onlyInherited: true, sections: [], properties: [] }, inspectorSections: [], previewRenderer: '', selectionMode: 'single' },
        runtime: { renderer: '', supportedScopes: ['page'] }
      })
    ])).toThrow('missingRuntimeRenderer');
  });

  it('accepts a complete manifest', () => {
    const registry = new ComponentRegistry([createManifest()]);
    expect(registry.validate()).toEqual([]);
    expect(registry.get('text.paragraph')?.runtime.renderer).toBe('runtime');
  });

  it('blocks deleting a referenced component without a migration', () => {
    const registry = new ComponentRegistry([createManifest()]);
    expect(registry.validateRemoval('text.paragraph', ['text.paragraph']).map((item) => item.code)).toEqual(['componentInUse']);
    expect(() => registry.assertRemovable('text.paragraph', ['text.paragraph'])).toThrow('cannot be removed');
  });

  it('validates every manifest declaration boundary', () => {
    const registry = new ComponentRegistry();
    registry.register(createManifest({
      binding: { acceptedSources: ['variables'], acceptedTypes: [], supportsConversion: false },
      defaults: { layout: null as unknown as Record<string, unknown>, props: {}, style: {} },
      events: [{ name: '', payloadSchema: null as unknown as Record<string, unknown>, trigger: '' }],
      migrations: [{ from: '', migrate: '' }],
      responsive: { supportedLayouts: [], supportsOverrides: false },
      security: { actionPermissions: [], requiresPermission: true },
      runtime: { renderer: 'runtime', supportedScopes: [] }
    }));

    expect(registry.validate().map((item) => item.code)).toEqual(expect.arrayContaining([
      'missingBindingTypes', 'missingResponsiveSchema', 'missingRuntimeScopes', 'missingEventName',
      'missingEventTrigger', 'missingEventPayloadSchema', 'missingMigration', 'missingMigrationSource',
      'missingPermission', 'missingDefaults'
    ]));
  });

  it('reports stable interaction diagnostics without requiring policy on generated legacy manifests', () => {
    const registry = new ComponentRegistry();
    registry.register(createManifest({
      interaction: {
        allowedChildren: undefined,
        allowedParents: [],
        contentModel: 'invalid' as never,
        geometryModel: 'invalid' as never,
        resizeHandles: ['diagonal' as never],
        supportedParentLayouts: ['canvas' as never]
      } as unknown as ComponentManifest['interaction']
    }));

    expect(registry.validate().map((item) => item.code)).toEqual(expect.arrayContaining([
      'missingAllowedChildren', 'missingAllowedParents', 'invalidContentModel', 'invalidGeometryModel',
      'invalidResizeHandle', 'invalidParentLayout'
    ]));
    expect(new ComponentRegistry([createManifest()]).validate()).toEqual([]);
  });
});
