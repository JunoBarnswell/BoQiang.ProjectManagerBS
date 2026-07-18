import { describe, expect, it } from 'vitest';

import { resolveProjectManagementWorkspaceScope } from './projectManagementWorkspaceScope';

describe('resolveProjectManagementWorkspaceScope', () => {
  it('enables PM data only for the SYSTEM platform workspace', () => {
    expect(resolveProjectManagementWorkspaceScope({
      tenantId: ' tenant-a ',
      appCode: ' system ',
      workspaceLevel: 'platform',
    })).toEqual({
      appCode: 'SYSTEM',
      isAvailable: true,
      tenantId: 'tenant-a',
    });
  });

  it('does not reuse an application workspace scope for PM data', () => {
    expect(resolveProjectManagementWorkspaceScope({
      tenantId: 'tenant-a',
      appCode: 'MES',
      workspaceLevel: 'application',
    }).isAvailable).toBe(false);
  });
});
