import { describe, expect, it } from 'vitest';

import { latestComponentManifests, latestComponentRegistry } from '../../../pages/application-console/development-center/low-code-studio/components/latestComponentManifestCatalog';
import { createRuntimeManifestRegistry, runtimeComponentRegistry } from '../../../runtime-kernel/RuntimeComponentRegistry';

describe('latest runtime and manifest parity contract', () => {
  it('keeps every latest editor manifest registered with its manifest contract', () => {
    for (const manifest of latestComponentManifests) {
      expect(manifest.editor.previewRenderer).toBeTruthy();
      expect(manifest.runtime.renderer).toBeTruthy();
      expect(latestComponentRegistry.get(manifest.type)).toBe(manifest);
    }
  });

  it('provides a validated runtime manifest for every runtime renderer', () => {
    const runtimeManifests = createRuntimeManifestRegistry();
    expect(runtimeManifests.size).toBe(runtimeComponentRegistry.types().length);
    for (const type of runtimeComponentRegistry.types()) {
      const manifest = runtimeManifests.get(type);
      expect(manifest?.type).toBe(type);
      expect(manifest?.runtime.renderer).toBeTruthy();
      expect(runtimeComponentRegistry.has(type)).toBe(true);
      expect(manifest?.validation.schema).toEqual(expect.any(Object));
    }
  });
});
