import { describe, expect, it } from 'vitest';

import { RUNTIME_INSPECTOR_CONTRACT } from '../../../../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract';
import { latestComponentRegistry } from '../../components/latestComponentManifestCatalog';

import { inspectorEditorRegistry } from './InspectorEditorRegistry';

describe('Inspector property contract coverage', () => {
  it('requires every canonical type to expose valid descriptor metadata', () => {
    const diagnostics = RUNTIME_INSPECTOR_CONTRACT.validate(RUNTIME_INSPECTOR_CONTRACT.types());
    expect(diagnostics).toEqual([]);
    for (const type of RUNTIME_INSPECTOR_CONTRACT.types()) {
      const definition = RUNTIME_INSPECTOR_CONTRACT.get(type);
      expect(definition?.properties.length).toBeGreaterThan(0);
      expect(definition?.properties.every((property) => property.ownerType === type && property.runtimeConsumer.length > 0)).toBe(true);
      expect(new Set(definition?.properties.map((property) => property.path)).size).toBe(definition?.properties.length);
    }
  });

  it('keeps every descriptor connected to a visual editor and manifest validation schema', () => {
    expect(latestComponentRegistry.validate()).toEqual([]);

    for (const manifest of latestComponentRegistry.values()) {
      const definition = manifest.editor.inspector;
      expect(definition).toBeDefined();
      inspectorEditorRegistry.assertValid(definition!.properties);

      const schema = manifest.validation.schema as { properties?: Record<string, unknown> };
      for (const property of definition!.properties.filter((item) => item.path.startsWith('props.'))) {
        const schemaNode = property.path
          .split('.')
          .slice(1)
          .reduce<unknown>((current, segment) => (current as { properties?: Record<string, unknown> } | undefined)?.properties?.[segment], schema);
        expect(schemaNode, `${manifest.type}:${property.path}`).toBeDefined();
      }
    }
  });
});
