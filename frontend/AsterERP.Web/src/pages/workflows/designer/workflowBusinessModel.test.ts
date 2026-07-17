import { describe, expect, it } from 'vitest';

import { createDefaultBusinessDesign, readBusinessDesign, serializeBusinessDesign } from './workflowBusinessModel';

const translate = (key: string) => key;

describe('WorkflowBusinessModelLatest', () => {
  it('round-trips only the versioned latest envelope', () => {
    const serialized = serializeBusinessDesign(createDefaultBusinessDesign(translate), translate);

    expect(readBusinessDesign(serialized, translate).version).toBe('latest');
  });

  it('marks unversioned persisted business models as migration blocked', () => {
    expect(() => readBusinessDesign('{"businessDesign":{"nodes":[]}}', translate))
      .toThrow('MigrationBlocked');
  });
});
