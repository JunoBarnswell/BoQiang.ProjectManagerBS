import { describe, expect, it } from 'vitest';

import { queryKeys } from './queryKeys';

describe('application data center query keys', () => {
  it('isolates workspace data by tenant and app', () => {
    const tenantA = queryKeys.applicationDataCenter.workspace('tenant-a', 'MES');
    const tenantB = queryKeys.applicationDataCenter.workspace('tenant-b', 'MES');
    const appWms = queryKeys.applicationDataCenter.workspace('tenant-a', 'WMS');

    expect(tenantA).not.toEqual(tenantB);
    expect(tenantA).not.toEqual(appWms);
  });

  it('keeps data-center object lists isolated by tenant and app', () => {
    const tenantA = ['application-data-center', 'tenant-a', 'MES', 'data-sources', 'list'] as const;
    const tenantB = ['application-data-center', 'tenant-b', 'MES', 'data-sources', 'list'] as const;
    const appWms = ['application-data-center', 'tenant-a', 'WMS', 'data-sources', 'list'] as const;

    expect(tenantA).not.toEqual(tenantB);
    expect(tenantA).not.toEqual(appWms);
  });

  it('isolates data source switcher results by tenant and app', () => {
    const mes = queryKeys.applicationDataCenter.workspaceSwitcher('tenant-a', 'MES');
    const wms = queryKeys.applicationDataCenter.workspaceSwitcher('tenant-a', 'WMS');

    expect(mes).not.toEqual(wms);
    expect(mes).toEqual([
      'astererp',
      'application-data-center',
      'workspace-switcher',
      'tenant-a',
      'MES',
      'data-sources'
    ]);
  });

  it('does not share runtime page schemas across tenant or app scopes', () => {
    const tenantA = queryKeys.runtime.pageSchemaScoped('tenant-a', 'MES', 'orders');
    const tenantB = queryKeys.runtime.pageSchemaScoped('tenant-b', 'MES', 'orders');
    const appWms = queryKeys.runtime.pageSchemaScoped('tenant-a', 'WMS', 'orders');

    expect(tenantA).not.toEqual(tenantB);
    expect(tenantA).not.toEqual(appWms);
  });
});
