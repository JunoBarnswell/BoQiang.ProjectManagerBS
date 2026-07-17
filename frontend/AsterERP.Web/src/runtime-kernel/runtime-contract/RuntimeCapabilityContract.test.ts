import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import { latestComponentManifests } from '../../pages/application-console/development-center/low-code-studio/components/latestComponentManifestCatalog';

import { RUNTIME_CAPABILITY_CONTRACT } from './RuntimeCapabilityContract';

describe('Runtime Capability Contract', () => {
  it('is canonical, unique, and covers the latest component manifest catalog', () => {
    expect(RUNTIME_CAPABILITY_CONTRACT.contractRevision).toBe('latest');
    expect(RUNTIME_CAPABILITY_CONTRACT.compilerRevision).toBe('runtime-1');
    expect(RUNTIME_CAPABILITY_CONTRACT.migrationRevision).toBe('latest');
    expect(RUNTIME_CAPABILITY_CONTRACT.renderer).toBe('ComponentRuntimeHost');
    expect(new Set(RUNTIME_CAPABILITY_CONTRACT.components).size).toBe(RUNTIME_CAPABILITY_CONTRACT.components.length);
    expect(new Set(RUNTIME_CAPABILITY_CONTRACT.actions).size).toBe(RUNTIME_CAPABILITY_CONTRACT.actions.length);
    expect(Object.keys(RUNTIME_CAPABILITY_CONTRACT.actionManifests).sort()).toEqual([...RUNTIME_CAPABILITY_CONTRACT.actions].sort());
    for (const [type, manifest] of Object.entries(RUNTIME_CAPABILITY_CONTRACT.actionManifests)) {
      expect(manifest.inputSchema).toBeTypeOf('object');
      expect(manifest.outputSchema).toBeTypeOf('object');
      expect(manifest.timeoutMs).toBeGreaterThan(0);
      expect(manifest.triggers.length).toBeGreaterThan(0);
      expect(type).toBeTypeOf('string');
    }
    expect(new Set(RUNTIME_CAPABILITY_CONTRACT.converters).size).toBe(RUNTIME_CAPABILITY_CONTRACT.converters.length);
    expect(new Set(RUNTIME_CAPABILITY_CONTRACT.scopes).size).toBe(RUNTIME_CAPABILITY_CONTRACT.scopes.length);
    const capabilityTypes = RUNTIME_CAPABILITY_CONTRACT.componentCapabilities.flatMap((capability) => capability.types);
    expect(new Set(capabilityTypes).size).toBe(capabilityTypes.length);
    expect([...capabilityTypes].sort()).toEqual([...RUNTIME_CAPABILITY_CONTRACT.components].sort());
    expect([...RUNTIME_CAPABILITY_CONTRACT.components].sort()).toEqual(
      latestComponentManifests.map((manifest) => manifest.type).sort()
    );
  });

  it('has no drift from the backend Contracts canonical source', () => {
    const backendContractPath = resolve(
      fileURLToPath(new URL('../../../../../backend/AsterERP.Contracts/ApplicationDesigner/runtime-capability.latest.json', import.meta.url))
    );
    const backendContract = JSON.parse(readFileSync(backendContractPath, 'utf8')) as unknown;
    expect(RUNTIME_CAPABILITY_CONTRACT).toEqual(backendContract);
  });
});
