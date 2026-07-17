import { describe, expect, it } from 'vitest';

import { latestComponentManifests } from '../pages/application-console/development-center/low-code-studio/components/latestComponentManifestCatalog';

import { createRuntimeManifestRegistry, runtimeComponentRegistry } from './RuntimeComponentRegistry';

describe('RuntimeComponentRegistry', () => {
  it('registers the latest native renderer for every supported runtime component', () => {
    const manifests = createRuntimeManifestRegistry();

    expect(runtimeComponentRegistry.has('layout.page')).toBe(true);
    expect(runtimeComponentRegistry.has('input.text')).toBe(true);
    expect(runtimeComponentRegistry.has('select.dropdown')).toBe(true);
    expect(runtimeComponentRegistry.has('metric.progress')).toBe(true);
    expect(runtimeComponentRegistry.has('table.semantic')).toBe(true);
    expect(runtimeComponentRegistry.has('report.dataTable')).toBe(true);
    expect(manifests.get('layout.page')?.runtime.renderer).toBe('container');
    expect(manifests.size).toBe(runtimeComponentRegistry.types().length);
    expect([...runtimeComponentRegistry.types()].sort()).toEqual(latestComponentManifests.map((item) => item.type).sort());
  });

  it('fails closed for an unknown or unsupported delegated component', () => {
    expect(runtimeComponentRegistry.get('component.unknown')).toBeUndefined();
    expect(runtimeComponentRegistry.has('chart.basic')).toBe(true);
    expect(runtimeComponentRegistry.has('input.textarea')).toBe(true);
    expect(runtimeComponentRegistry.has('media.signature')).toBe(true);
    expect(runtimeComponentRegistry.has('modal.drawer')).toBe(true);
  });
});
