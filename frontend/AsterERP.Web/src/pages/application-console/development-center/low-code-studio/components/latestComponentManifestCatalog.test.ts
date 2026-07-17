import { readFileSync } from 'node:fs';

import { describe, expect, it } from 'vitest';

import { componentCapabilityFor, RUNTIME_CAPABILITY_CONTRACT } from '../../../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract';

import { COMPONENT_PARENT_LAYOUTS, COMPONENT_RESIZE_HANDLES, canContainComponent, canResizeComponent } from './componentInteractionPolicy';
import { assertCanonicalComponentCapabilityFamily } from './ComponentManifest';
import { latestComponentManifests, latestComponentRegistry , validateLatestComponentManifest } from './latestComponentManifestCatalog';

describe('latest component manifest catalog', () => {
  it('registers unique latest component contracts with preview and defaults', () => {
    expect(new Set(latestComponentManifests.map((item) => item.type)).size).toBe(latestComponentManifests.length);
    expect(latestComponentRegistry.validate()).toEqual([]);
    expect(latestComponentRegistry.get('form.input')).toMatchObject({
      defaults: { props: { placeholder: 'Input' } },
      editor: { previewRenderer: 'input' }
    });
  });

  it('declares the complete runtime catalog and keeps the capability matrix aligned', () => {
    expect(latestComponentManifests).toHaveLength(RUNTIME_CAPABILITY_CONTRACT.components.length);
    expect(latestComponentManifests.map((item) => item.type).sort()).toEqual([...RUNTIME_CAPABILITY_CONTRACT.components].sort());

    const matrixUrl = new URL('../../../../../../../../docs/low-code-refactor/component-capability-matrix.json', import.meta.url);
    const matrix = JSON.parse(readFileSync(matrixUrl, 'utf8')) as { components: Array<{ type: string }> };
    expect(matrix.components.map((item) => item.type).sort()).toEqual(latestComponentManifests.map((item) => item.type).sort());
  });

  it('declares the complete runtime data-table editing contract', () => {
    expect(latestComponentRegistry.get('report.dataTable')?.editing).toEqual({
      commitTriggers: ['blur', 'enter', 'escape'],
      enabled: true,
      primaryKeyNonEditable: true,
      supportsConflictResolution: true,
      supportedDataTypes: ['boolean', 'date', 'datetime', 'json', 'number', 'string']
    });
  });

  it('covers the six publish dimensions from the canonical capability profiles', () => {
    const dimensions = ['binding', 'defaults', 'events', 'security', 'responsive', 'runtime'] as const;
    for (const capability of RUNTIME_CAPABILITY_CONTRACT.componentCapabilities) {
      for (const type of capability.types) {
        const manifest = latestComponentRegistry.get(type);
        expect(manifest, type).toBeDefined();
        for (const dimension of dimensions) expect(manifest, `${type}.${dimension}`).toHaveProperty(dimension);
        expect(manifest?.binding.acceptedTypes.length, `${type}.binding`).toBeGreaterThan(0);
        expect(manifest?.events.length, `${type}.events`).toBeGreaterThan(0);
        expect(manifest?.responsive.supportedLayouts.length, `${type}.responsive`).toBeGreaterThan(0);
        expect(manifest?.defaults.props, `${type}.defaults`).toBeTypeOf('object');
      }
    }
  });

  it('declares a complete explicit interaction policy for every latest runtime component', () => {
    const geometryModels = new Set(['flow', 'absolute', 'intrinsic']);
    const contentModels = new Set(['void', 'text', 'children', 'mixed']);
    const resizeHandles = new Set(COMPONENT_RESIZE_HANDLES);
    const parentLayouts = new Set(COMPONENT_PARENT_LAYOUTS);

    for (const manifest of latestComponentManifests) {
      const interaction = manifest.interaction;
      expect(interaction, `${manifest.type}.interaction`).toBeDefined();
      if (!interaction) continue;
      expect(geometryModels.has(interaction.geometryModel), `${manifest.type}.geometryModel`).toBe(true);
      expect(contentModels.has(interaction.contentModel), `${manifest.type}.contentModel`).toBe(true);
      expect(Array.isArray(interaction.allowedParents), `${manifest.type}.allowedParents`).toBe(true);
      expect(Array.isArray(interaction.allowedChildren), `${manifest.type}.allowedChildren`).toBe(true);
      expect(Array.isArray(interaction.resizeHandles), `${manifest.type}.resizeHandles`).toBe(true);
      expect(Array.isArray(interaction.supportedParentLayouts), `${manifest.type}.supportedParentLayouts`).toBe(true);
      expect(interaction.allowedParents.every((rule) => rule.trim().length > 0), `${manifest.type}.allowedParents rules`).toBe(true);
      expect(interaction.allowedChildren.every((rule) => rule.trim().length > 0), `${manifest.type}.allowedChildren rules`).toBe(true);
      expect(interaction.resizeHandles.every((handle) => resizeHandles.has(handle)), `${manifest.type}.resizeHandles values`).toBe(true);
      expect(interaction.supportedParentLayouts.every((layout) => parentLayouts.has(layout)), `${manifest.type}.supportedParentLayouts values`).toBe(true);
    }
  });

  it('generates and validates every capability family without omitted policy dimensions', () => {
    const capabilityByType = new Map(RUNTIME_CAPABILITY_CONTRACT.componentCapabilities.flatMap((capability) => capability.types.map((type) => [type, capability] as const)));
    for (const manifest of latestComponentManifests) {
      const capability = capabilityByType.get(manifest.type);
      expect(capability, `${manifest.type}.capability`).toBeDefined();
      if (!capability) continue;
      expect(validateLatestComponentManifest(manifest, capability), manifest.type).toEqual([]);
      expect(manifest.interaction?.allowedParents.length, `${manifest.type}.allowedParents`).toBeGreaterThan(0);
      expect(manifest.interaction?.supportedParentLayouts.length, `${manifest.type}.supportedParentLayouts`).toBeGreaterThan(0);
    }
  });

  it('fails closed for unknown capability families and unsupported parent layouts', () => {
    expect(() => assertCanonicalComponentCapabilityFamily('future-family')).toThrow('Unknown canonical component capability family');
    const manifest = latestComponentRegistry.get('action.button');
    expect(manifest?.interaction).toBeDefined();
    if (!manifest?.interaction) return;
    const invalidLayoutDeclaration = { ...manifest, interaction: { ...manifest.interaction, supportedParentLayouts: [...COMPONENT_PARENT_LAYOUTS] } };
    expect(validateLatestComponentManifest(invalidLayoutDeclaration, componentCapabilityFor('action.button')).map((item) => item.code)).toContain('unsupportedLayoutDeclaration');
    expect(canContainComponent(latestComponentRegistry.get('layout.container')!, manifest, { layoutMode: 'grid' })).toBe(false);
    expect(canResizeComponent(manifest, 'southeast')).toBe(true);
  });
});
